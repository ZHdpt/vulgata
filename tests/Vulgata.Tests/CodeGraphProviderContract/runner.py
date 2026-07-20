import hashlib
import hmac
import importlib.metadata
import json
import os
import re
import shutil
import socket
import subprocess
import tempfile
import time
import unicodedata
import urllib.error
import urllib.request
from collections import Counter, defaultdict
from pathlib import Path


EXPECTED_A = "dbba4ff5ac4834ae2857307dad36c177d9655ac951744c646f9c6e54d3923f33"
EXPECTED_B = "40d2da3b9b520b3c4eca3fd7c3bad4f4fb10643736e3a9e9b4ddf62c92adc096"
EXPECTED_LOCK = "10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95"
EXPECTED_DISTRIBUTION = "3cd79efe6c9525d69ae250c063404b500ef7595b6c63284d841c78a1ecf48658"
EVIDENCE_SCHEMA = "cgc-provider-contract-evidence-v2"
MAX_EVIDENCE_BYTES = 16 * 1024
MAX_RESPONSE_BYTES = 64 * 1024 * 1024
MAX_VULGATA_ROWS = 250000
PRIVATE_TOOL_LIMITS = '{"execute_cypher_query":250001}'
LANGUAGE_PATTERN = re.compile(r"^[a-z][a-z0-9_+#.-]{0,63}$")
SAFE_NAMES = {
    "status", "success", "error", "message", "data", "query", "record_count", "results",
    "record_kind", "provider_kind", "path", "name", "start_line", "end_line",
    "parser_language", "truncated", "result_limit",
    "relationship_kind", "source_provider_kind", "source_path", "source_name",
    "source_start_line", "source_end_line", "target_provider_kind", "target_path",
    "target_name", "target_start_line", "target_end_line",
}
NODE_FIELDS = {
    "record_kind", "provider_kind", "path", "name", "start_line", "end_line",
    "parser_language",
}
RELATIONSHIP_FIELDS = {
    "record_kind", "relationship_kind", "source_provider_kind", "source_path",
    "source_name", "source_start_line", "source_end_line", "target_provider_kind",
    "target_path", "target_name", "target_start_line", "target_end_line",
}


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def tree_hash(root):
    root = Path(root)
    if root.is_file() and not root.is_symlink():
        return sha256(root.read_bytes())
    digest = hashlib.sha256()
    for path in sorted(root.rglob("*")):
        if path.is_file() and not path.is_symlink():
            relative = path.relative_to(root).as_posix().encode("utf-8")
            digest.update(len(relative).to_bytes(4, "big"))
            digest.update(relative)
            content = path.read_bytes()
            digest.update(len(content).to_bytes(8, "big"))
            digest.update(content)
    return digest.hexdigest()


def run(home, *arguments, stdin=None):
    environment = os.environ.copy()
    environment["HOME"] = str(home)
    completed = subprocess.run(
        ["cgc", *arguments],
        input=stdin,
        text=True,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        env=environment,
        timeout=120,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError("provider-command-failed")


def query(port, text):
    request = urllib.request.Request(
        f"http://127.0.0.1:{port}/api/v1/query",
        data=json.dumps({"query": text, "params": {}}, separators=(",", ":")).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            raw = response.read(MAX_RESPONSE_BYTES + 1)
            if len(raw) > MAX_RESPONSE_BYTES:
                raise RuntimeError("response-over-bound")
            return response.status, raw
    except urllib.error.HTTPError as error:
        error.read(1024)
        return error.code, b""


def value_kind(value):
    if value is None:
        return "null"
    if value is True or value is False:
        return "boolean"
    if isinstance(value, str):
        return "string"
    if isinstance(value, (int, float)):
        return "number"
    if isinstance(value, dict):
        return "object"
    if isinstance(value, list):
        return "array"
    return "unknown"


def observe_json(raw, http_status, memory_key):
    observation = {
        "httpStatus": http_status,
        "responseBytes": len(raw),
        "responseSha256": sha256(raw),
        "rootKind": "invalid",
        "containers": [],
        "arrayKinds": [],
    }
    try:
        payload = json.loads(raw.decode("utf-8"))
    except Exception:
        return observation
    observation["rootKind"] = value_kind(payload)
    containers = []
    arrays = []
    unknown_ordinal = 0

    def visit(value, depth, container_ordinal):
        nonlocal unknown_ordinal
        if depth > 4 or len(containers) >= 32:
            return
        if isinstance(value, dict):
            known = []
            unknown = []
            current = len(containers)
            containers.append({
                "ordinal": current,
                "parentOrdinal": container_ordinal,
                "depth": depth,
                "kind": "object",
                "knownProperties": known,
                "unknownProperties": unknown,
            })
            for name, child in list(value.items())[:128]:
                kind = value_kind(child)
                encoded_name = name.encode("utf-8", "strict")
                if name in SAFE_NAMES and len(encoded_name) <= 128:
                    known.append({"name": name, "kind": kind})
                else:
                    digest = hmac.new(
                        memory_key,
                        b"vulgata-cgc-unknown-property-v1\0"
                        + len(encoded_name).to_bytes(8, "big") + b"\0" + encoded_name,
                        hashlib.sha256,
                    ).hexdigest()
                    unknown.append({
                        "digest": digest,
                        "utf8Length": len(encoded_name),
                        "kind": kind,
                        "firstSeenOrdinal": unknown_ordinal,
                    })
                    unknown_ordinal += 1
                if isinstance(child, (dict, list)):
                    visit(child, depth + 1, current)
        elif isinstance(value, list):
            kinds = Counter(value_kind(item) for item in value)
            arrays.append({
                "containerOrdinal": container_ordinal,
                "depth": depth,
                "length": len(value),
                "itemKinds": dict(sorted(kinds.items())),
            })
            # One representative container is enough to observe the closed row
            # shape; parse_envelope still validates every row exhaustively.
            for child in value[:1]:
                if isinstance(child, (dict, list)):
                    visit(child, depth + 1, container_ordinal)

    visit(payload, 0, None)
    observation["containers"] = containers
    observation["arrayKinds"] = arrays[:32]
    return observation


def decode_json_strict(raw):
    def reject_duplicates(pairs):
        result = {}
        for name, value in pairs:
            if name in result:
                raise RuntimeError("duplicate-json-property")
            result[name] = value
        return result

    return json.loads(raw.decode("utf-8"), object_pairs_hook=reject_duplicates)


def parse_envelope(raw, expected_query, expected_fields):
    payload = decode_json_strict(raw)
    if not isinstance(payload, dict):
        raise RuntimeError("envelope-root")
    if set(payload) != {"status", "message", "data", "error"}:
        raise RuntimeError("envelope-properties")
    if (
        payload.get("status") != "ok"
        or payload.get("message", object()) is not None
        or payload.get("error", object()) is not None
    ):
        raise RuntimeError("envelope-status")
    data = payload.get("data")
    if not isinstance(data, dict):
        raise RuntimeError("envelope-data")
    if set(data) != {"success", "query", "record_count", "results"}:
        raise RuntimeError("envelope-data-properties")
    if (
        data.get("success") is not True
        or data.get("query") != expected_query
        or not isinstance(data.get("record_count"), int)
        or not isinstance(data.get("results"), list)
        or data["record_count"] != len(data["results"])
    ):
        raise RuntimeError("envelope-contract")
    for row in data["results"]:
        if not isinstance(row, dict) or set(row) != expected_fields:
            raise RuntimeError("row-contract")
        if any(isinstance(value, (dict, list)) for value in row.values()):
            raise RuntimeError("row-scalar-contract")
    return data["results"], data["query"]


def normalize_language(value):
    if not isinstance(value, str):
        raise RuntimeError("parser-language-type")
    normalized = unicodedata.normalize("NFKC", value).strip().lower()
    if not LANGUAGE_PATTERN.fullmatch(normalized):
        raise RuntimeError("parser-language-value")
    return normalized


def normalize_path(value):
    if value is None:
        return ""
    normalized = value.replace("\\", "/").lstrip("/")
    if not normalized or any(part in ("", ".", "..") for part in normalized.split("/")):
        raise RuntimeError("endpoint-path")
    return normalized


def select_file_language(values):
    normalized = {normalize_language(value) for value in values}
    if len(normalized) > 1:
        raise RuntimeError("parser-language-conflict")
    return next(iter(normalized)) if normalized else None


def validate_combined_bound(first_count, second_count):
    if first_count + second_count > MAX_VULGATA_ROWS:
        raise RuntimeError("combined-row-bound")


def match_fixture_path(value, candidate_paths, required):
    normalized = normalize_path(value)
    matches = {
        candidate
        for candidate in candidate_paths
        if normalized == candidate or normalized.endswith("/" + candidate)
    }
    if len(matches) == 1:
        return next(iter(matches))
    if required:
        raise RuntimeError("structural-path-outside-fixture")
    return None


def reconcile_languages(rows, fixture):
    candidate_paths = {
        path.relative_to(fixture).as_posix()
        for path in fixture.rglob("*")
        if path.is_file()
        and not path.is_symlink()
        and path.suffix in {".cs", ".py"}
    }
    structural_tokens = defaultdict(set)
    structural_observations = 0
    file_null_observations = 0
    for row in rows:
        value = row["parser_language"]
        if row["provider_kind"] == "File":
            if value is not None:
                raise RuntimeError("file-language-authority")
            file_null_observations += 1
            continue
        if value is not None:
            if not normalize_path(row["path"]):
                continue
            path = match_fixture_path(row["path"], candidate_paths, required=True)
            structural_tokens[path].add(normalize_language(value))
            structural_observations += 1

    file_languages = {
        path: select_file_language(values)
        for path, values in structural_tokens.items()
        if values
    }
    unknown_paths = candidate_paths.difference(file_languages)
    empty_relative = "empty_file.cs"
    if unknown_paths != {empty_relative}:
        raise RuntimeError("unknown-language-reconciliation")
    if (fixture / empty_relative).read_bytes() != b"":
        raise RuntimeError("empty-file-fixture-drift")

    retained_rows = [
        row for row in rows
        if match_fixture_path(row["path"], candidate_paths, required=False) in file_languages
    ]
    if any(
        match_fixture_path(row["path"], candidate_paths, required=False) not in file_languages
        for row in retained_rows
    ):
        raise RuntimeError("unassigned-code-unit")
    return {
        "aliasObserved": all("parser_language" in row for row in rows),
        "structuralBindingsObserved": structural_observations > 0,
        "fileNullObserved": file_null_observations > 0,
        "oneIdentityPerPath": all(len(values) == 1 for values in structural_tokens.values()),
        "allCodeUnitsAssigned": len(retained_rows) == sum(
            1 for row in rows
            if match_fixture_path(row["path"], candidate_paths, required=False) in file_languages),
        "emptyFileUnknownLanguage": len(unknown_paths) == 1,
        "emptyFileRetainedCodeUnitCount": sum(
            1 for row in retained_rows
            if match_fixture_path(row["path"], candidate_paths, required=False) in unknown_paths),
        "unknownLanguageCount": len(unknown_paths),
    }, unknown_paths, candidate_paths


def rejected(action):
    try:
        action()
    except (RuntimeError, ValueError, TypeError, json.JSONDecodeError):
        return True
    return False


def endpoint(kind, path, name, start, end):
    if kind not in ("File", "Module", "Class", "Function"):
        raise RuntimeError("endpoint-kind")
    normalized_path = normalize_path(path) if path else ""
    normalized_name = name.strip() if isinstance(name, str) else ""
    values = [
        kind, normalized_path, normalized_name,
        str(start) if isinstance(start, int) else "",
        str(end) if isinstance(end, int) else "",
    ]
    return "cgc-v1:" + sha256("\0".join(values).encode("utf-8"))


def reserve_port():
    listener = socket.socket()
    listener.bind(("127.0.0.1", 0))
    port = listener.getsockname()[1]
    listener.close()
    return port


def wait_for_health(port, gateway):
    for _ in range(200):
        if gateway.poll() is not None:
            raise RuntimeError("gateway-exited")
        try:
            with urllib.request.urlopen(f"http://127.0.0.1:{port}/health", timeout=1) as response:
                if response.status == 200:
                    response.read()
                    return
        except Exception:
            time.sleep(0.1)
    raise RuntimeError("gateway-not-ready")


def remove_no_follow(path):
    path = Path(path)
    if not path.exists() and not path.is_symlink():
        return True, "absent"
    if path.is_symlink():
        return False, "link"
    if path.is_file():
        path.unlink()
        return not path.exists(), "file"
    if path.is_dir():
        for entry in path.rglob("*"):
            if entry.is_symlink():
                return False, "link"
        shutil.rmtree(path)
        return not path.exists(), "directory"
    return False, "special"


def publish_create_new(path, value):
    encoded = json.dumps(value, separators=(",", ":"), sort_keys=True).encode("utf-8")
    if len(encoded) > MAX_EVIDENCE_BYTES:
        raise RuntimeError("evidence-over-bound")
    destination = Path(path)
    temporary = destination.with_name(f".{destination.name}.{os.getpid()}.tmp")
    descriptor = os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "wb", closefd=True) as stream:
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
        os.link(temporary, destination)
        directory_fd = os.open(destination.parent, os.O_RDONLY)
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    finally:
        if temporary.exists():
            temporary.unlink()


def main():
    contract = Path("/contract")
    evidence_root = Path("/evidence")
    accepted_correction = os.environ["ACCEPTED_CORRECTION_SHA256"]
    claim_sha256 = os.environ["CLAIM_SHA256"]
    claim_path = evidence_root / "claim.json"
    if not claim_path.is_file() or sha256(claim_path.read_bytes()) != claim_sha256:
        raise RuntimeError("claim-mismatch")
    claim = json.loads(claim_path.read_text("utf-8"))
    if claim.get("acceptedCorrectionSha256") != accepted_correction:
        raise RuntimeError("claim-boundary-mismatch")

    fixture = contract / "fixture"
    projection_a = (contract / "projection-a.cypher").read_bytes()
    projection_b = (contract / "projection-b.cypher").read_bytes()
    failure_category = None
    provider_version = "unknown"
    state = Path(tempfile.mkdtemp(prefix="vulgata-provider-contract-"))
    home = state / "home"
    database = state / "graph.kuzu"
    home.mkdir()
    context_name = "vulgata-provider-contract"
    gateway = None
    context_created = False
    response_a = b""
    response_b = b""
    status_a = 0
    status_b = 0
    rows_a = []
    rows_b = []
    echo_b = ""
    snapshot_a = ""
    snapshot_b = ""
    gateway_stopped = False
    context_deleted = False
    database_deleted = False
    provider_home_deleted = False
    database_entry_type = "absent"
    memory_key = os.urandom(32)
    observations = []
    coverage = {
        "independentNodeKinds": {},
        "relationshipKinds": {},
        "filteredTargetUnresolvedOnly": False,
        "projectionEndpointIdentityEqual": False,
        "parserLanguage": {},
    }
    completeness = {
        "privateLimitExact": False,
        "projectionATruncationMetadataAbsent": False,
        "projectionBTruncationMetadataAbsent": False,
        "injectedTruncationRejected": False,
        "queryDriftRejected": False,
        "languageConflictRejected": False,
        "combinedBoundRejected": False,
    }
    row_counts = {"projectionA": 0, "projectionB": 0, "combined": 0}
    try:
        if sha256(projection_a) != EXPECTED_A or sha256(projection_b) != EXPECTED_B:
            raise RuntimeError("query-hash-drift")
        lock = Path("/opt/codegraph/requirements.lock")
        if not lock.is_file() or sha256(lock.read_bytes()) != EXPECTED_LOCK:
            raise RuntimeError("provider-lock-drift")
        provider_version = importlib.metadata.version("codegraphcontext")
        if provider_version != "0.5.1":
            raise RuntimeError("provider-version-drift")

        run(home, "context", "create", context_name, "--database", "kuzudb", "--db-path", str(database))
        context_created = True
        run(home, "context", "mode", "named")
        run(home, "context", "default", context_name)
        run(home, "index", str(fixture), "--force", "--context", context_name)
        port = reserve_port()
        environment = os.environ.copy()
        environment["HOME"] = str(home)
        environment["TOOL_RESULT_LIMITS"] = PRIVATE_TOOL_LIMITS
        completeness["privateLimitExact"] = (
            environment["TOOL_RESULT_LIMITS"] == PRIVATE_TOOL_LIMITS
            and os.environ.get("TOOL_RESULT_LIMITS") is None
        )
        gateway = subprocess.Popen(
            ["cgc", "api", "start", "--host", "127.0.0.1", "--port", str(port)],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            env=environment,
        )
        wait_for_health(port, gateway)
        status_a, response_a = query(port, projection_a.decode("utf-8"))
        observations.append(observe_json(response_a, status_a, memory_key))
        if status_a != 200:
            raise RuntimeError("projection-a-http")
        rows_a, _ = parse_envelope(response_a, projection_a.decode("utf-8"), NODE_FIELDS)
        snapshot_a = tree_hash(database)
        status_b, response_b = query(port, projection_b.decode("utf-8"))
        observations.append(observe_json(response_b, status_b, memory_key))
        if status_b != 200:
            raise RuntimeError("projection-b-http")
        rows_b, echo_b = parse_envelope(response_b, projection_b.decode("utf-8"), RELATIONSHIP_FIELDS)
        snapshot_b = tree_hash(database)
        validate_combined_bound(len(rows_a), len(rows_b))
        row_counts = {
            "projectionA": len(rows_a),
            "projectionB": len(rows_b),
            "combined": len(rows_a) + len(rows_b),
        }
        payload_a = decode_json_strict(response_a)
        payload_b = decode_json_strict(response_b)
        completeness["projectionATruncationMetadataAbsent"] = not {
            "truncated", "result_limit",
        }.intersection(payload_a["data"])
        completeness["projectionBTruncationMetadataAbsent"] = not {
            "truncated", "result_limit",
        }.intersection(payload_b["data"])
        injected = decode_json_strict(response_a)
        injected["data"]["truncated"] = False
        injected["data"]["result_limit"] = 250001
        injected_raw = json.dumps(injected, separators=(",", ":")).encode("utf-8")
        completeness["injectedTruncationRejected"] = rejected(
            lambda: parse_envelope(
                injected_raw, projection_a.decode("utf-8"), NODE_FIELDS))
        completeness["queryDriftRejected"] = rejected(
            lambda: parse_envelope(
                response_a, projection_a.decode("utf-8") + " ", NODE_FIELDS))
        completeness["languageConflictRejected"] = rejected(
            lambda: select_file_language(["python", "ruby"])
        )
        completeness["combinedBoundRejected"] = rejected(
            lambda: validate_combined_bound(MAX_VULGATA_ROWS, 1)
        )

        node_identities = {}
        incident = set()
        node_kinds = {"File": 0, "Module": 0, "Class": 0, "Function": 0}
        relationship_kinds = {"CONTAINS": 0, "IMPORTS": 0, "CALLS": 0, "INHERITS": 0}
        parser_language, unknown_paths, candidate_paths = reconcile_languages(rows_a, fixture)
        for row in rows_a:
            identity = endpoint(row["provider_kind"], row["path"], row["name"], row["start_line"], row["end_line"])
            node_identities[identity] = row
            node_kinds[row["provider_kind"]] += 1
        filtered_target_relationships = 0
        for row in rows_b:
            source = endpoint(
                row["source_provider_kind"], row["source_path"], row["source_name"],
                row["source_start_line"], row["source_end_line"])
            target = endpoint(
                row["target_provider_kind"], row["target_path"], row["target_name"],
                row["target_start_line"], row["target_end_line"])
            incident.update((source, target))
            relationship_kinds[row["relationship_kind"]] += 1
            target_path = row["target_path"]
            if (
                row["relationship_kind"] == "CALLS"
                and isinstance(target_path, str)
                and target_path.replace("\\", "/").endswith("filtered_target.py")
            ):
                filtered_target_relationships += 1
            if (
                match_fixture_path(row["source_path"], candidate_paths, required=False) in unknown_paths
                or match_fixture_path(row["target_path"], candidate_paths, required=False) in unknown_paths
            ):
                raise RuntimeError("unknown-language-relationship-retained")
        shared_endpoints = incident.intersection(node_identities)
        coverage = {
            "nodeKinds": node_kinds,
            "independentNodeKinds": node_kinds,
            "relationshipKinds": relationship_kinds,
            "filteredTargetUnresolvedOnly": filtered_target_relationships == 1,
            "projectionEndpointIdentityEqual": bool(shared_endpoints)
                and all(identity.startswith("cgc-v1:") for identity in shared_endpoints),
            "parserLanguage": parser_language,
        }
        if (
            any(count < 1 for count in node_kinds.values())
            or any(count < 1 for count in relationship_kinds.values())
            or not coverage["filteredTargetUnresolvedOnly"]
            or not coverage["projectionEndpointIdentityEqual"]
            or snapshot_a != snapshot_b
            or sha256(echo_b.encode("utf-8")) != EXPECTED_B
            or row_counts["projectionA"] <= 100
            or row_counts["projectionB"] <= 100
            or not all(completeness.values())
            or not all((
                parser_language["aliasObserved"],
                parser_language["structuralBindingsObserved"],
                parser_language["fileNullObserved"],
                parser_language["oneIdentityPerPath"],
                parser_language["allCodeUnitsAssigned"],
                parser_language["emptyFileUnknownLanguage"],
            ))
            or parser_language["emptyFileRetainedCodeUnitCount"] != 0
            or parser_language["unknownLanguageCount"] != 1
        ):
            raise RuntimeError("coverage-mismatch")
    except Exception as error:
        failure_category = str(error)
    finally:
        if gateway is not None:
            gateway.terminate()
            try:
                gateway.wait(timeout=10)
            except subprocess.TimeoutExpired:
                gateway.kill()
                gateway.wait(timeout=5)
            gateway_stopped = gateway.poll() is not None
        if context_created:
            try:
                run(home, "context", "delete", context_name, stdin="y\n")
                context_deleted = True
            except Exception:
                context_deleted = False
        try:
            database_deleted, database_entry_type = remove_no_follow(database)
        except Exception:
            database_deleted = False
            database_entry_type = "cleanup-error"
        try:
            if home.exists():
                shutil.rmtree(home)
            provider_home_deleted = not home.exists()
        except Exception:
            provider_home_deleted = False
        try:
            if state.exists():
                shutil.rmtree(state)
        except Exception:
            provider_home_deleted = False

    cleanup = {
        "gatewayStopped": gateway_stopped,
        "contextDeleted": context_deleted,
        "databaseDeleted": database_deleted,
        "providerHomeDeleted": provider_home_deleted,
        "databaseEntryType": database_entry_type,
    }
    if not all((gateway_stopped, context_deleted, database_deleted, provider_home_deleted)):
        failure_category = failure_category or "cleanup-failure"
    verdict = "pass" if failure_category is None else "fail"
    evidence = {
        "schema": EVIDENCE_SCHEMA,
        "verdict": verdict,
        "failureCategory": failure_category,
        "acceptedCorrectionSha256": accepted_correction,
        "claimSha256": claim_sha256,
        "finalImageSha256": os.environ["FINAL_IMAGE_SHA256"],
        "verifierBaseImageSha256": os.environ["VERIFIER_BASE_IMAGE_SHA256"],
        "providerVersion": provider_version,
        "providerLockSha256": EXPECTED_LOCK,
        "providerDistributionSha256": EXPECTED_DISTRIBUTION,
        "fixtureTreeSha256": tree_hash(fixture),
        "projectionARequestSha256": EXPECTED_A,
        "projectionBRequestSha256": EXPECTED_B,
        "observations": observations,
        "coverage": coverage,
        "rowCounts": row_counts,
        "completeness": completeness,
        "immutableSnapshot": {
            "indexCount": 1 if context_created else 0,
            "watcherStarted": False,
            "mutationBetweenProjections": False,
            "snapshotEqual": bool(snapshot_a) and snapshot_a == snapshot_b,
        },
        "cleanup": cleanup,
        "forbiddenRetention": {
            "sourceRetained": False, "rowRetained": False, "rawJsonRetained": False,
            "queryTextRetained": False, "pathRetained": False, "symbolRetained": False,
            "credentialRetained": False, "tokenRetained": False, "diagnosticRetained": False,
            "hmacKeyRetained": False,
        },
    }
    memory_key = b""
    publish_create_new(evidence_root / "evidence.json", evidence)
    if verdict != "pass":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
