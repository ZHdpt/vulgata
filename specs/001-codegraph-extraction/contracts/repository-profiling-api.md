# Repository Profiling API and Provider Qualification Contract

## Qualification execution capability contract

Provider execution is not implied by test discovery or plan acceptance.
Synthetic tests are identified only by `CodeGraphQualificationSynthetic` and
use fake launchers/in-memory streams. The sole real gate is identified by
`CodeGraphQualificationProvider` plus one exact fully-qualified test name and
requires a sealed `ProviderExecutionLease`; no broad filter may invoke it.

The lease issuer must, before Docker or provider reachability, validate a
process-scoped attempt ID and random token against an unexpired append-only
authorization record, its accepted corrective plan-review identity, token
SHA-256, and exact command identity. It then atomically creates and flushes a
create-new `started.json`. Existing start/terminal state fails closed. The
plaintext token is never committed or retained. Full-suite execution without
the wrapper remains inert.

All documented harness commands compile and omit `--no-build`. The provider
fixture path embedded in the pre-incident assembly is revoked before synthetic
work, so that known stale binary exits before image inspection/build/run.
Pre-authorization preflight has no Docker/provider reachability. Exactly one
newly human-authorised `attempt-0002` is required because `attempt-0001` used
the superseded CLI observer and is permanently failed.

The public boundary is contingent on the already approved Story/PRD baseline,
provider qualification, and a follow-up reviewed parser plan. Until then, this document
defines intended HTTP shape and the *qualification* boundary; it does not
pretend a production `ICodeGraphCliService` result grammar already exists.

## Public HTTP boundary after qualification

The implementation extends existing authorised repository routes with plural
nouns and `ProblemDetails` errors. System-owned and standalone Repository routes
preserve their existing visibility semantics:

```
POST /api/systems/{systemId}/repositories/{repositoryId}/scan-runs
GET  /api/systems/{systemId}/repositories/{repositoryId}/scan-runs/{scanRunId}

POST /api/repositories/standalone/{repositoryId}/scan-runs
GET  /api/repositories/standalone/{repositoryId}/scan-runs/{scanRunId}
```

- Authorization is `ManagementAccess` plus the existing visible-System/
  Repository SystemOwner rule in the coordinator. Denied/hidden access follows
  the established 404/403 behavior and creates no `ScanRun`.
- The start body is empty. FR-018 defines a deployment-owned whole-process
  timeout, defaulting to 1,800 seconds; it is not a caller-controlled API
  setting and there is no per-file timeout field.
- A synchronous successful start returns `201 Created` and a terminal minimal
  `ScanRunSummaryDto`. Missing CLI and clone failure persist terminal state and
  return safe Chinese `ProblemDetails`, never stdout/stderr.

```
ScanRunSummaryDto
  id: uuid
  repositoryId: uuid
  status: string
  parsedFileCount: int
  totalCandidateFileCount: int
  needsHumanReview: bool
  startedAt: datetime
  completedAt?: datetime
  message?: string            // sanitized Simplified-Chinese display text
```

The summary never exposes checkout path, CLI arguments, database/context path,
raw provider payload, source content, credential, or token.

## Qualification command contract — not a production adapter

The future qualification test invokes only these documented local CLI and
loopback HTTP surfaces, using `ProcessStartInfo.ArgumentList` and generated
private argument values:

`
cgc version
cgc doctor
cgc context create <run-context> --database kuzudb --db-path <private-db-dir>
cgc context mode named
cgc context default <run-context>
cgc --context <run-context> index <working-copy-dir> --force
cgc api start --host 127.0.0.1 --port <private-port>
GET http://127.0.0.1:<private-port>/health
POST http://127.0.0.1:<private-port>/api/v1/query
gateway-stop
cgc context delete <run-context>
`

This lifecycle is qualification-only. It preserves the already approved
whole-Repository `ScanRun` baseline, but it does not select the HTTP gateway as
production Scan behavior, does not define a production adapter, and does not
authorize direct Kuzu integration.

Qualification must enforce these accepted constraints:

1. The generated private `HOME`, named Context, and Kuzu path are created once
  per run. Because `cgc api start` has no `--context` option, the test must
  set named mode plus default Context before the whole-Repository index and
  must prove the gateway resolves that isolated Context.
2. Readiness is exactly aggregate polling of
  `GET http://127.0.0.1:<private-port>/health` for the bounded startup window.
  Every readiness response is streamed, discarded, and appended in attempt
  order to one logical `cgc-api-health` channel inside the shared observer
  byte budget. Readiness polling is not a query retry.
3. After readiness, the runner sends exactly one static read-only
  `POST /api/v1/query` request. Its bounded response body is the single
  `cgc-api-query` logical channel.
4. The query-response body is validated with strict incremental UTF-8 and a
  streaming JSON reader. Qualification must not call `ReadAsString`, retain a
  raw response or chunk, accept replacement decoding, or infer undocumented
  fields. The accepted observer grammar IDs are
  `vulgata-cgc-http-json-v1` and `vulgata-cgc-streaming-evidence-v2`.
5. Durable evidence is schema `1.2` redacted `structuralEvidence` only: logical
  channel byte counts and hashes, lifecycle counts, relationship-kind counts,
  boolean predicates, cleanup facts, and the canonical summary hash. It must
  not retain a path, symbol, source fragment, endpoint value, raw JSON, raw
  query, or other provider value.
6. Cleanup precedence is strict. `gateway-stop` is a runner lifecycle action
  that sends `SIGTERM` to the recorded gateway process group, waits its bounded
  grace period, and uses `SIGKILL` only if needed. Cleanup must then delete the
  named Context, private Kuzu database, private provider home, and named
  container. Any cleanup failure is a failed qualification even if the JSON
  response was otherwise valid.

Qualification must capture the installed 0.5.1 behavior on a synthetic
fixture; a follow-up plan must record:

1. exact query text and arguments;
2. exact bounded HTTP envelope grammar and deterministic parser;
3. normalization for repository-relative path, kind, symbol, positive line
   range, relationship kind, and endpoint identity; and
4. safe normalized fixture snapshot plus lock/image hashes.

Before that work, no source code may claim `ICodeGraphCliService` returns
`CodeGraphParseResult` from the real provider, that the local HTTP gateway is
the production Scan transport, or that the accepted qualification envelope is
already a production parser contract. Tests may use a fake Vulgata contract for
unrelated domain/UI seams, but not as evidence of provider compatibility.

The exact executable provider pin is `codegraphcontext==0.5.1`, selected under
the user's latest-available-version direction after the configured package
index did not provide 0.5.0. This correction keeps the public API contingent on
the same local, whole-Repository qualification and follow-up plan; it neither
changes a caller-visible API field nor authorises a production adapter. Since
the observed public CLI documentation is labelled 0.5.0, it is not an
assurance of 0.5.1 output compatibility; the qualification contract remains
the required proof.

## Requirements for the future owned result contract

After qualification and re-planning, the owned result contract must carry only
validated normalized data:

`
CodeGraphParseResult
  codeUnits: ProviderCodeUnit[]
  relationships: ProviderStructuralRelationship[]
  candidateOutcomes: ProviderCandidateOutcome[]
  duration: TimeSpan
  parsedFileCount: int

ProviderCodeUnit
  relativePath, symbolName, providerKind, lineStart, lineEnd,
  providerEndpointIdentity, rangeConfidence

ProviderStructuralRelationship
  sourceEndpointIdentity, targetEndpointIdentity, relationshipKind

ProviderCandidateOutcome
  relativePath, outcome, boundedReason
`

Validation requires normalized relative path, non-empty structural fields,
positive ordered lines, supported unit/edge mapping, and bounded safe reason.
Invalid rows are audit warnings/outcomes, not EF entities. A provider process
failure has no per-file result contract: it is terminal `ScanRun` state with no
partial provider-derived Code Units, Edges, or candidates.

## Security contract

- No shell interpolation; process output is bounded/redacted and retained only
  as safe operator evidence.
- Provider contexts/databases are local, private, unique per `ScanRun`, and
  deleted in `finally`. Qualification may start only the loopback-only
  `cgc api` gateway with no Docker-published port so it can perform aggregate
  health polling and one bounded JSON query; it must never invoke bundled
  MCP/tool routes, a Vulgata endpoint/tool, watcher, bundle, registry,
  datasource, remote database, direct Kuzu integration, or a Neo4j setup
  command.
- Repository credentials never appear in DTOs, entities, logs, raw API errors,
  test snapshots, or qualification evidence.
