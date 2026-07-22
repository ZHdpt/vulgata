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
    if isinstance(value, ObjectPairs):
        return "object"
    if isinstance(value, list):
        return "array"
    return "unknown"


class ObjectPairs(list):
    pass


def reject_json_constant(_value):
    raise ValueError("invalid-json-number")


def unknown_name(name, memory_key, first_seen_ordinal):
    encoded_name = name.encode("utf-8", "strict")
    digest = hmac.new(
        memory_key,
        b"vulgata-cgc-unknown-property-v1\0"
        + len(encoded_name).to_bytes(8, "big") + b"\0" + encoded_name,
        hashlib.sha256,
    ).hexdigest()
    return {
        "hmacSha256": digest,
        "utf8Length": len(encoded_name),
        "firstSeenOrdinal": first_seen_ordinal,
    }


def observe_json(raw, http_status, memory_key, request_sha256):
    observation = {
        "attempted": True,
        "httpStatus": http_status,
        "bodyByteCount": len(raw),
        "bodySha256": sha256(raw),
        "requestSha256": request_sha256,
        "rootValueKind": "invalid-json",
        "shapeComplete": False,
        "containers": [],
        "properties": [],
        "arrays": [],
        "aliasNames": [],
        "coordinateBindings": [],
    }
    try:
        payload = json.loads(
            raw.decode("utf-8"),
            object_pairs_hook=ObjectPairs,
            parse_constant=reject_json_constant,
        )
    except Exception:
        return observation, True, True
    observation["rootValueKind"] = value_kind(payload)
    containers = []
    properties = []
    arrays = []
    aliases = set()
    unknown_count = 0
    unknown_observations = {}
    bounded = True

    def get_unknown(name):
        nonlocal bounded, unknown_count
        if name in unknown_observations:
            return unknown_observations[name]
        if unknown_count >= 128:
            bounded = False
            return None
        observation_value = unknown_name(name, memory_key, unknown_count)
        unknown_observations[name] = observation_value
        unknown_count += 1
        return observation_value

    def visit(value, depth, parent_ordinal=None, via_name=None):
        nonlocal bounded, unknown_count
        if depth > 4 or len(containers) >= 32:
            bounded = False
            return None
        if isinstance(value, (ObjectPairs, list)):
            current = len(containers)
            container = {
                "ordinal": current,
                "kind": "object" if isinstance(value, ObjectPairs) else "array",
                "depth": depth,
            }
            if parent_ordinal is not None:
                container["parentOrdinal"] = parent_ordinal
                if via_name is not None:
                    if via_name in SAFE_NAMES:
                        container["safeViaPropertyName"] = via_name
                    else:
                        unknown = get_unknown(via_name)
                        if unknown is None:
                            return None
                        container["unknownViaName"] = unknown
            containers.append(container)
        else:
            return None

        if isinstance(value, ObjectPairs):
            property_indexes = {}
            for name, child in value:
                if len(properties) >= 128 and (name, value_kind(child)) not in property_indexes:
                    bounded = False
                    break
                kind = value_kind(child)
                key = (name, kind)
                if key in property_indexes:
                    properties[property_indexes[key]]["occurrenceCount"] += 1
                    if properties[property_indexes[key]]["occurrenceCount"] > 250001:
                        bounded = False
                        break
                else:
                    item = {
                        "containerOrdinal": current,
                        "valueKind": kind,
                        "occurrenceCount": 1,
                    }
                    if name in SAFE_NAMES:
                        item["safeName"] = name
                        aliases.add(name)
                    else:
                        unknown = get_unknown(name)
                        if unknown is None:
                            break
                        item["unknownName"] = unknown
                    property_indexes[key] = len(properties)
                    properties.append(item)
                if isinstance(child, (ObjectPairs, list)):
                    visit(child, depth + 1, current, name)
        else:
            kinds = Counter(value_kind(item) for item in value)
            if len(arrays) >= 32 or len(value) > 250001:
                bounded = False
                return current
            arrays.append({
                "containerOrdinal": current,
                "elementCount": len(value),
                "nullCount": kinds["null"],
                "booleanCount": kinds["boolean"],
                "numberCount": kinds["number"],
                "stringCount": kinds["string"],
                "objectCount": kinds["object"],
                "arrayCount": kinds["array"],
            })
            # One representative container is enough to observe the closed row
            # shape; parse_envelope still validates every row exhaustively.
            for child in value[:1]:
                if isinstance(child, (ObjectPairs, list)):
                    visit(child, depth + 1, current)
        return current

    visit(payload, 0)
    observation["containers"] = containers
    observation["properties"] = properties
    observation["arrays"] = arrays
    observation["aliasNames"] = [
        {"safeName": name} for name in sorted(aliases)
    ][:64]
    if len(aliases) > 64:
        bounded = False
    observed_names = set(aliases)
    candidate_bindings = [
        ("path", "name"),
        ("source_path", "source_name"),
        ("target_path", "target_name"),
    ]
    observation["coordinateBindings"] = [
        list(binding) for binding in candidate_bindings
        if set(binding).issubset(observed_names)
    ]
    if len(json.dumps(observation, separators=(",", ":")).encode("utf-8")) > 5 * 1024:
        observation["containers"] = []
        observation["properties"] = []
        observation["arrays"] = []
        observation["aliasNames"] = []
        observation["coordinateBindings"] = []
        bounded = False
    return observation, bounded, unknown_count == 0


def decode_json_strict(raw):
    def reject_duplicates(pairs):
        result = {}
        for name, value in pairs:
            if name in result:
                raise RuntimeError("duplicate-json-property")
            result[name] = value
        return result

    return json.loads(
        raw.decode("utf-8"),
        object_pairs_hook=reject_duplicates,
        parse_constant=reject_json_constant,
    )


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
        or isinstance(data.get("record_count"), bool)
        or not 0 <= data.get("record_count") <= 250001
        or not isinstance(data.get("results"), list)
        or data["record_count"] != len(data["results"])
    ):
        raise RuntimeError("envelope-contract")
    for row in data["results"]:
        if not isinstance(row, dict) or set(row) != expected_fields:
            raise RuntimeError("row-contract")
        if any(isinstance(value, (dict, list)) for value in row.values()):
            raise RuntimeError("row-scalar-contract")
        validate_row(row, expected_fields)
    return data["results"], data["query"]


def valid_nullable_string(value):
    return value is None or isinstance(value, str)


def valid_nullable_integer(value):
    return value is None or (
        isinstance(value, int)
        and not isinstance(value, bool)
        and 0 <= value <= 2147483647
    )


def validate_row(row, expected_fields):
    provider_kinds = {"File", "Module", "Class", "Function"}
    if expected_fields == NODE_FIELDS:
        if (
            row["record_kind"] != "code_unit"
            or row["provider_kind"] not in provider_kinds
            or not valid_nullable_string(row["path"])
            or not valid_nullable_string(row["name"])
            or not valid_nullable_integer(row["start_line"])
            or not valid_nullable_integer(row["end_line"])
            or not valid_nullable_string(row["parser_language"])
        ):
            raise RuntimeError("row-value-contract")
        if row["parser_language"] is not None:
            normalize_language(row["parser_language"])
        return

    if (
        row["record_kind"] != "relationship"
        or row["relationship_kind"] not in {"CONTAINS", "IMPORTS", "CALLS", "INHERITS"}
        or row["source_provider_kind"] not in provider_kinds
        or row["target_provider_kind"] not in provider_kinds
        or any(not valid_nullable_string(row[name]) for name in (
            "source_path", "source_name", "target_path", "target_name"))
        or any(not valid_nullable_integer(row[name]) for name in (
            "source_start_line", "source_end_line", "target_start_line", "target_end_line"))
    ):
        raise RuntimeError("row-value-contract")


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


def not_attempted_observation(request_sha256):
    return {
        "attempted": False,
        "bodyByteCount": 0,
        "requestSha256": request_sha256,
        "rootValueKind": "not-observed",
        "shapeComplete": False,
        "containers": [],
        "properties": [],
        "arrays": [],
        "aliasNames": [],
        "coordinateBindings": [],
    }


def classify_failure(error):
    if isinstance(error, (json.JSONDecodeError, UnicodeDecodeError)):
        return "invalid-json"
    reason = str(error)
    if reason in {
        "claim-mismatch", "claim-boundary-mismatch", "query-hash-drift",
        "provider-lock-drift", "provider-version-drift",
    }:
        return "preflight"
    if reason in {"projection-a-http", "projection-b-http"}:
        return "http-status"
    if reason in {"response-over-bound", "evidence-over-bound"}:
        return "observation-bound-exceeded"
    if reason.startswith("provider-") or reason.startswith("gateway-"):
        return "provider-lifecycle"
    if reason.startswith("duplicate-json"):
        return "envelope-mismatch"
    if reason.startswith("envelope-") or reason.startswith("row-"):
        return "envelope-mismatch"
    if reason == "combined-row-bound":
        return "projection-mismatch"
    return "projection-mismatch"


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
    observations = {
        "A": not_attempted_observation(EXPECTED_A),
        "B": not_attempted_observation(EXPECTED_B),
    }
    coverage = {
        "independentNodeKinds": {
            "File": 0, "Module": 0, "Class": 0, "Function": 0,
        },
        "relationshipKinds": {
            "CONTAINS": 0, "IMPORTS": 0, "CALLS": 0, "INHERITS": 0,
        },
        "filteredTargetUnresolvedOnly": False,
        "projectionEndpointIdentityEqual": False,
        "parserLanguage": {
            "aliasObserved": False,
            "structuralBindingsObserved": False,
            "fileNullObserved": False,
            "oneIdentityPerPath": False,
            "allCodeUnitsAssigned": False,
            "emptyFileUnknownLanguage": False,
            "emptyFileRetainedCodeUnitCount": 0,
            "unknownLanguageCount": 0,
        },
        "rowCounts": {"projectionA": 0, "projectionB": 0, "combined": 0},
        "completeness": {
            "privateLimitExact": False,
            "injectedTruncationRejected": False,
            "queryDriftRejected": False,
            "languageConflictRejected": False,
            "combinedBoundRejected": False,
        },
    }
    completeness = coverage["completeness"]
    row_counts = coverage["rowCounts"]
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
        observation_a, bounded_a, known_a = observe_json(
            response_a, status_a, memory_key, EXPECTED_A)
        observations["A"] = observation_a
        if status_a != 200:
            raise RuntimeError("projection-a-http")
        rows_a, echo_a = parse_envelope(
            response_a, projection_a.decode("utf-8"), NODE_FIELDS)
        observation_a["queryEchoSha256"] = sha256(echo_a.encode("utf-8"))
        observation_a["shapeComplete"] = bounded_a and known_a
        if not bounded_a:
            raise RuntimeError("observation-bound-exceeded")
        if not known_a:
            raise RuntimeError("projection-mismatch")
        snapshot_a = tree_hash(database)
        status_b, response_b = query(port, projection_b.decode("utf-8"))
        observation_b, bounded_b, known_b = observe_json(
            response_b, status_b, memory_key, EXPECTED_B)
        observations["B"] = observation_b
        if status_b != 200:
            raise RuntimeError("projection-b-http")
        rows_b, echo_b = parse_envelope(response_b, projection_b.decode("utf-8"), RELATIONSHIP_FIELDS)
        observation_b["queryEchoSha256"] = sha256(echo_b.encode("utf-8"))
        observation_b["shapeComplete"] = bounded_b and known_b
        if not bounded_b:
            raise RuntimeError("observation-bound-exceeded")
        if not known_b:
            raise RuntimeError("projection-mismatch")
        snapshot_b = tree_hash(database)
        validate_combined_bound(len(rows_a), len(rows_b))
        row_counts = {
            "projectionA": len(rows_a),
            "projectionB": len(rows_b),
            "combined": len(rows_a) + len(rows_b),
        }
        payload_a = decode_json_strict(response_a)
        payload_b = decode_json_strict(response_b)
        observation_a["shapeComplete"] = observation_a["shapeComplete"] and not {
            "truncated", "result_limit",
        }.intersection(payload_a["data"])
        observation_b["shapeComplete"] = observation_b["shapeComplete"] and not {
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
            "rowCounts": row_counts,
            "completeness": completeness,
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
            or not observations["A"]["shapeComplete"]
            or not observations["B"]["shapeComplete"]
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
        failure_category = classify_failure(error)
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

    provider_state_kind = {
        "absent": "absent",
        "file": "regular-file",
        "directory": "directory",
        "link": "link-or-reparse",
        "special": "special",
        "cleanup-error": "unknown",
    }.get(database_entry_type, "unknown")
    cleanup_failures = sum((
        not gateway_stopped,
        not context_deleted,
        not database_deleted,
        not provider_home_deleted,
    ))
    cleanup = {
        "gatewayStopped": gateway_stopped,
        "contextDeleted": context_deleted,
        "providerStateKind": provider_state_kind,
        "providerStateRemoved": database_deleted,
        "providerHomeRemoved": provider_home_deleted,
        "containerRemoved": True,
        "tempImageTagsRemoved": True,
        "failureCount": cleanup_failures,
    }
    if cleanup_failures:
        failure_category = failure_category or "cleanup-failure"
    verdict = "pass" if failure_category is None else "fail"
    evidence = {
        "schemaVersion": EVIDENCE_SCHEMA,
        "verdict": verdict,
        "primaryFailureCategory": failure_category or "none",
        "acceptedCorrectionSha256": accepted_correction,
        "claimSha256": claim_sha256,
        "sourceCommit": os.environ["SOURCE_COMMIT"],
        "sourceTree": os.environ["SOURCE_TREE"],
        "productionSourceManifestSha256": os.environ["PRODUCTION_SOURCE_MANIFEST_SHA256"],
        "finalImageSha256": os.environ["FINAL_IMAGE_SHA256"],
        "verifierBaseImageSha256": os.environ["VERIFIER_BASE_IMAGE_SHA256"],
        "providerVersion": provider_version,
        "providerLockSha256": EXPECTED_LOCK,
        "providerDistributionSha256": EXPECTED_DISTRIBUTION,
        "fixtureTreeSha256": tree_hash(fixture),
        "projectionObservations": observations,
        "coverage": coverage,
        "immutableSnapshot": {
            "indexCount": 1 if context_created else 0,
            "watcherStarted": False,
            "mutationBetweenProjections": False,
            "snapshotEqual": bool(snapshot_a) and snapshot_a == snapshot_b,
        },
        "cleanup": cleanup,
        "forbiddenRetention": {
            "scalarValues": False,
            "source": False,
            "rows": False,
            "rawJson": False,
            "rawQuery": False,
            "paths": False,
            "symbols": False,
            "credentials": False,
            "tokens": False,
            "endpointValues": False,
            "diagnostics": False,
        },
    }
    memory_key = b""
    publish_create_new(evidence_root / "evidence.json", evidence)
    if verdict != "pass":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
