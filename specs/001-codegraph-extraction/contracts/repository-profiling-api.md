# Repository Profiling API and Provider Qualification Contract

The public boundary is contingent on the proposed Story/PRD correction, provider
qualification, and a follow-up reviewed parser plan. Until then, this document
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
- The start body is empty. `perFileTimeoutSeconds` is deliberately removed by
  the proposed correction; deployment-owned whole-process timeout is not a
  caller-controlled API setting.
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

The future qualification test invokes only these documented local CLI surfaces,
using `ProcessStartInfo.ArgumentList` and generated private argument values:

`
cgc version
cgc doctor
cgc context create <run-context> --database kuzudb --db-path <private-db-dir>
cgc --context <run-context> index <working-copy-dir> --force
cgc --context <run-context> query "<static read-only structural projection>"
cgc context delete <run-context>
`

`<static read-only structural projection>` is intentionally not specified in
this revision. The official CLI docs give `query` and an example Cypher query,
but no stable machine-readable output schema. Qualification must capture the
installed 0.5.1 output on a synthetic fixture; a follow-up plan must record:

1. exact query text and arguments;
2. exact bounded output grammar and deterministic parser;
3. normalization for repository-relative path, kind, symbol, positive line
   range, relationship kind, and endpoint identity; and
4. safe normalized fixture snapshot plus lock/image hashes.

Before that work, no source code may claim `ICodeGraphCliService` returns
`CodeGraphParseResult` from the real provider. Tests may use a fake Vulgata
contract for unrelated domain/UI seams, but not as evidence of provider
compatibility.

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
  deleted in `finally`; no `cgc api`, `cgc mcp`, watcher, bundle, registry,
  datasource, remote database, or Neo4j setup command is allowed.
- Repository credentials never appear in DTOs, entities, logs, raw API errors,
  test snapshots, or qualification evidence.
