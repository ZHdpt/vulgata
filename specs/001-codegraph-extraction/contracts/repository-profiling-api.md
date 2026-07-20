# Repository Profiling API and CodeGraph Provider Contract

## Public HTTP boundary

Existing Repository visibility and `ManagementAccess` rules apply:

```text
POST /api/systems/{systemId}/repositories/{repositoryId}/scan-runs
GET  /api/systems/{systemId}/repositories/{repositoryId}/scan-runs/{scanRunId}

POST /api/repositories/standalone/{repositoryId}/scan-runs
GET  /api/repositories/standalone/{repositoryId}/scan-runs/{scanRunId}
```

- Start has an empty body. The 1,800-second whole-provider timeout is
  deployment-owned, not caller-controlled.
- Denied/hidden access uses the established 403/404 behavior and creates no
  `ScanRun`.
- Successful synchronous completion returns `201 Created` with:

```text
ScanRunSummaryDto
  id: uuid
  repositoryId: uuid
  status: string
  parsedFileCount: int
  totalCandidateFileCount: int
  needsHumanReview: bool
  startedAt: datetime
  completedAt?: datetime
  message?: string
```

- Clone/provider failures return safe Simplified-Chinese `ProblemDetails` and
  retain terminal ScanRun state.
- No DTO exposes source, checkout/provider paths, raw provider payload, query,
  credential, token, Kuzu identity, or process diagnostics.

## Production CodeGraphContext lifecycle

CodeGraphContext is exact-pinned to `0.5.1`; the locked distribution SHA-256
is `3cd79efe6c9525d69ae250c063404b500ef7595b6c63284d841c78a1ecf48658`
inside requirements-lock SHA-256
`10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95`.
One `ScanRun` uses:

```text
cgc context create <private-context> --database kuzudb --db-path <private-db>
cgc context mode named
cgc context default <private-context>
cgc --context <private-context> index <working-copy> --force
cgc api start --host 127.0.0.1 --port <private-port>
GET  http://127.0.0.1:<private-port>/health
POST http://127.0.0.1:<private-port>/api/v1/query   # Code Units
POST http://127.0.0.1:<private-port>/api/v1/query   # relationships
gateway-stop
cgc context delete <private-context>
```

The gateway is production-local and unpublished. Vulgata invokes no
`/api/v1/mcp/*`, `/api/v1/tools*`, index HTTP route, watcher, bundle, registry,
datasource, remote backend, or direct Kuzu API.

Only the private `cgc api start` process receives:

```text
TOOL_RESULT_LIMITS={"execute_cypher_query":250001}
```

Vulgata does not set this variable globally and does not configure another
tool limit. The gateway limit is one row above Vulgata's unchanged 250,000
combined-row ceiling so provider truncation cannot silently satisfy that
ceiling. It does not authorize pagination, retry, or a third query.

One configurable deadline, default 1,800 seconds, covers index through both
query responses. A separate bounded cleanup grace always runs. No per-file
provider process, retry, bisection, or continuation is permitted.

After Context deletion, cleanup validates each generated target under its
private root and inspects without following links: absent is success; a regular
file such as observed `graph.kuzu` is unlinked; a directory is recursively
removed without link/reparse traversal; a link/reparse/special entry is not
followed and fails cleanup.

## Candidate HTTP envelope grammar

Both requests use:

```json
{
  "query": "<exact owned read-only projection>",
  "params": {}
}
```

Each successful response has the required shape:

```json
{
  "status": "ok",
  "message": null,
  "error": null,
  "data": {
    "success": true,
    "query": "<exact request query>",
    "record_count": 1,
    "results": []
  }
}
```

Required properties reject missing, duplicate, null-disallowed, or wrong-typed
values. `data.query` must exactly echo the request.
`data.record_count == data.results.length`. Invalid UTF-8/JSON, trailing JSON,
HTTP failure, a status other than `ok`, non-null message/error, query mismatch, count
mismatch, or a response bound violation fails the whole provider result.
Within `data`, `truncated` and `result_limit` are reserved completeness fields.
A complete response omits both. Presence of either field—including one field
alone, duplicates, wrong types, an inconsistent pair, `truncated: false`, or
any `result_limit` value—fails the whole provider result atomically. These
fields are never ignored as additive metadata. Other unknown envelope
properties may be ignored only after required and reserved fields validate
and never enter the owned result.

This envelope is the closed grammar observed from exact
`codegraphcontext==0.5.1` and confirmed against its pinned API schema/router.
No speculative dual grammar is allowed.

The first exact-provider run returned HTTP 200 and a top-level object but did
not satisfy this shape. It remains failed and authorizes no alternate grammar
because no property/type evidence was published. The first bounded v2
correction then observed the exact root properties and types
`status:string,message:null,data:null,error:string`; this proved the real
envelope and that the candidate query had been rejected.

## Projection A: Code Unit rows

One read-only query uses Kuzu's heterogeneous node match with an explicit
four-label allowlist. This avoids a pinned 0.5.1 Kuzu UNION execution stall
while preserving one immutable graph projection. The exact
canonical text is the UTF-8 code-fence content below with LF line endings and
no trailing LF:

```cypher
MATCH (node)
WHERE label(node) IN ['File', 'Module', 'Class', 'Function']
RETURN 'code_unit' AS record_kind, label(node) AS provider_kind,
       node.path AS path, node.name AS name,
       node.line_number AS start_line, node.end_line AS end_line
ORDER BY provider_kind, path, name, start_line, end_line
```

Canonical SHA-256:
`07b13dcb0b95a2ab26da930e9fcbafef7cde27488df99b0634d860ef0767d9a8`.

The candidate query aliases `line_number` to the owned `start_line` field.
File coordinates are deliberately null and are derived from local source as
specified in the data model. A Module's candidate single line is projected as
an ordered single-line range. The real-provider gate must prove each property
reference evaluates against exact 0.5.1 and that the returned aliases are
exactly those below; it must not accept `line_number` and `start_line` as
interchangeable fallbacks. This query never matches a relationship, so
isolated nodes are included.

Every row has exactly six scalar properties:

```text
record_kind: "code_unit"
provider_kind: "File" | "Module" | "Class" | "Function"
path: string | null
name: string | null
start_line: integer | null
end_line: integer | null
```

## Projection B: relationship rows

One separate read-only query uses one heterogeneous relationship match with
closed relationship and endpoint-label allowlists. This exact canonical UTF-8
text has LF line endings and no trailing LF:

```cypher
MATCH (source)-[relationship]->(target)
WHERE label(relationship) IN ['CONTAINS', 'IMPORTS', 'CALLS', 'INHERITS']
  AND label(source) IN ['File', 'Module', 'Class', 'Function']
  AND label(target) IN ['File', 'Module', 'Class', 'Function']
RETURN 'relationship' AS record_kind, label(relationship) AS relationship_kind,
       label(source) AS source_provider_kind,
       source.path AS source_path, source.name AS source_name,
       source.line_number AS source_start_line, source.end_line AS source_end_line,
       label(target) AS target_provider_kind,
       target.path AS target_path, target.name AS target_name,
       target.line_number AS target_start_line, target.end_line AS target_end_line
ORDER BY relationship_kind,
         source_provider_kind, source_path, source_name, source_start_line, source_end_line,
         target_provider_kind, target_path, target_name, target_start_line, target_end_line
```

Canonical SHA-256:
`40d2da3b9b520b3c4eca3fd7c3bad4f4fb10643736e3a9e9b4ddf62c92adc096`.

Each branch returns exactly:

```text
record_kind: "relationship"
relationship_kind: "CONTAINS" | "IMPORTS" | "CALLS" | "INHERITS"
source_provider_kind: string
source_path: string | null
source_name: string | null
source_start_line: integer | null
source_end_line: integer | null
target_provider_kind: string
target_path: string | null
target_name: string | null
target_start_line: integer | null
target_end_line: integer | null
```

Candidate provider coordinate properties are aliased to the owned
`*_start_line` and `*_end_line` names. The mandatory real-provider gate must
prove every referenced property and exact returned alias; an undocumented
alternative spelling is rejected until the grammar/query/parser are corrected
together. Rows are ordered by relationship kind and endpoint tuples. No row is
permitted to synthesize a Code Unit. File ranges derived from local source and
directly observed Class/Function ranges are `Exact`; a provider single-line
Module range is `Approximate`; no `Heuristic` range is invented by this
adapter.

## Endpoint identity and mapping contract

For both projections:

1. normalize provider kind to `File|Module|Class|Function`;
2. normalize path to forward-slash repository-relative form or empty;
3. trim name or use empty;
4. serialize positive line values using invariant decimal or empty;
5. form `kind + NUL + path + NUL + name + NUL + start + NUL + end`;
6. return `cgc-v1:` plus lowercase SHA-256 hex.

The key is bounded, stable for the immutable graph snapshot, and independent of
Kuzu/provider internal IDs.

- Projection A builds the complete Code Unit identity map first.
- Projection B maps a relationship only after that map is complete.
- persisted source + persisted target -> one resolved Code Unit Edge;
- persisted source + valid absent target identity -> one Unresolved Structural
  Relationship Candidate;
- absent source or invalid endpoint -> malformed row, no Edge/candidate;
- identical duplicates deduplicate;
- identity collision with different tuples -> atomic provider-contract failure.

## Owned Core result contract

```text
CodeGraphProviderResult
  succeeded: bool
  codeUnits: ProviderCodeUnit[]
  relationships: ProviderStructuralRelationship[]
  candidateOutcomes: ProviderCandidateOutcome[]
  parsedFileCount: int
  duration: TimeSpan
  error?: string

ProviderCodeUnit
  relativePath: string
  symbolName: string
  unitType: CodeUnitType
  lineStart: int
  lineEnd: int
  endpointIdentity: string
  rangeConfidence: RangeConfidence

ProviderStructuralRelationship
  sourceEndpointIdentity: string
  targetEndpointIdentity: string
  relationshipKind: StructuralRelationshipKind

ProviderCandidateOutcome
  relativePath: string
  outcome: enum
  boundedReason: string
```

Core sees no HTTP, JSON, Cypher, process, provider Context, or Kuzu type.

## Bounds and failure contract

- 64 MiB combined raw response bytes;
- 250,000 combined rows;
- 4,096 raw bytes per row;
- at most 100 retained/logged warning details, with complete aggregate counts;
- strict UTF-8 and one complete JSON value per response;
- process diagnostics bounded and redacted.

Malformed independent rows are warned and skipped. Valid independent rows
continue. Any process/timeout/HTTP/envelope/limit/query/count/collision/cleanup
failure returns no provider-derived Code Units, relationships, or outcomes.
Previously persisted local filtered audit rows remain.

`ParsedFileCount` counts distinct eligible paths represented by at least one
valid persisted Code Unit. At 500 or more candidates with zero Code Units, the
run completes with `NeedsHumanReview = true` only when both projections
succeeded; a provider failure remains a failed/aborted run.

## Production contract tests

- exact two-query lifecycle and read-only route allow-list;
- all File/Module/Class/Function node kinds from independent Projection A;
- stable endpoint identity equality across projections;
- pathless Module endpoint-only behavior;
- all four supported relationship kinds and unresolved-target mapping;
- malformed rows, duplicate/collision behavior, and warning sampling;
- strict envelope/UTF-8/query echo/count/body/row bounds;
- atomic failure when either query, process, timeout, or cleanup fails;
- no raw/source/secret retention;
- public authorization, Chinese `ProblemDetails`, persistence, migration, and
  local-Git end-to-end coverage.

### Mandatory final-image real-provider contract E2E

The exact completion gate is:

```text
Vulgata.Tests.CodeGraphProviderContractTests.FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract
```

It runs separately from qualification against a verifier image based on the
exact final application image and must:

1. prove equal final/verifier base-image identities and exact installed
   `codegraphcontext==0.5.1` distribution/lock identity without resolving or
   installing any package;
2. index one credential-free local Repository fixture once, with no network,
   credential, retry, watcher, or graph mutation;
3. hash the exact production query constants before dispatch, execute each
   once, and prove Projection B request, observed query echo, and recorded hash
   equality;
4. closed-schema observe the real envelope and coordinate property bindings;
   only successfully evaluated properties/aliases may be accepted;
5. prove all File/Module/Class/Function node kinds from independent
   Projection A, all four
   relationship kinds, identical `cgc-v1` for shared A/B endpoint tuples, and
   one locally filtered CALLS target represented as an Unresolved Structural
   Relationship Candidate after local eligibility reconciliation;
6. clean the gateway, Context, database, and provider home; and
7. emit at most 16 KiB of
   `cgc-provider-contract-evidence-v2` for pass or fail, containing only HTTP
   status, image/package/query/response hashes, byte/row/kind counts, bounded
   raw allowlisted names or unknown-name HMAC metadata, JSON kinds, booleans,
   failure category, and cleanup facts.

The evidence must contain no source, row, raw JSON/query text, fixture/provider
path, filename, symbol, credential, token, endpoint value, or process
diagnostic. Missing/invalid/over-bound evidence, skip, query drift, package or
image drift, unrecognized envelope/property, coverage/identity mismatch,
source-bearing evidence, or cleanup failure blocks Story completion.

Correction means updating this owned grammar plus production query/parser/tests
under review and rerunning this gate. It never means accepting both coordinate
spellings, weakening validation, or invoking/modifying/retrying attempt-0003.

### Exact bounded observation schema

```text
schemaVersion: "cgc-provider-contract-evidence-v2"
verdict: pass | fail
primaryFailureCategory:
  none | preflight | provider-lifecycle | http-status | invalid-json |
  envelope-mismatch | query-rejected | projection-mismatch |
  observation-bound-exceeded | cleanup-failure | publication-failure
acceptedCorrectionSha256, claimSha256,
finalImageSha256, verifierBaseImageSha256
providerVersion, providerLockSha256, providerDistributionSha256
fixtureTreeSha256
projectionObservations:
  A | B:
    attempted, httpStatus?, bodyByteCount, bodySha256?, requestSha256,
    queryEchoSha256?, rootValueKind, shapeComplete
    containers[]: ordinal, parentOrdinal?, safeViaPropertyName?,
                  unknownViaName?, kind, depth
    properties[]:
      containerOrdinal, valueKind, occurrenceCount,
      safeName? |
      unknownName?:
        hmacSha256, utf8Length, containerOrdinal, firstSeenOrdinal
    arrays[]: containerOrdinal and element-kind counts
    aliasNames[]: safeName | unknownName
    coordinateBindings[]: safeName pairs only; unknown names fail
coverage: bounded counts/booleans
immutableSnapshot: booleans/hashes
cleanup:
  gatewayStopped, contextDeleted,
  providerStateKind:
    absent | regular-file | directory | link-or-reparse | special | unknown
  providerStateRemoved, providerHomeRemoved, containerRemoved,
  tempImageTagsRemoved, failureCount
forbiddenRetention:
  scalarValues, source, rows, rawJson, rawQuery, paths, symbols,
  credentials, tokens, endpointValues, diagnostics: all false
```

Bounds: 16 KiB, depth 4, 32 containers, 128 property observations,
32 array summaries, 64 sorted unique aliases of at most 64 UTF-8 bytes, and
32 coordinate bindings. No JSON scalar value is retained. Overflow stops name
collection, keeps only counts/hashes, sets `shapeComplete=false`, publishes
`observation-bound-exceeded`, and remains failed.

Array shape observation retains one representative object container; the
closed parser separately validates every returned row. This prevents repeated
identical row schemas from exhausting the evidence bound.

Raw `safeName` is permitted only for the exact case-sensitive ASCII set:
`status,message,error,data,success,query,record_count,results,record_kind,provider_kind,path,name,start_line,end_line,relationship_kind,source_provider_kind,source_path,source_name,source_start_line,source_end_line,target_provider_kind,target_path,target_name,target_start_line,target_end_line`.

Every other decoded name uses
`HMAC-SHA256(ephemeralKey,
"vulgata-cgc-unknown-property-v1" || NUL || uint64be(utf8Length) || NUL ||
decodedUtf8Name)`. The 256-bit key is generated by CSPRNG per execution,
memory-only, destroyed after publication, and never persisted/logged. Unkeyed
hashes are forbidden. Unknown-name evidence contains only lowercase digest,
length, value kind, container ordinal, and first-seen ordinal and forces fail.
Logs/exceptions/test output follow the same rule and never contain raw unknown
names or scalar values.

### Per-correction claim and publication lifecycle

The immutable directory is:

```text
TestResults/codegraph-provider-contract/<AcceptedCorrectionSha256>/
  claim.json
  evidence.json
  claim.tmp-* / evidence.tmp-*  # preserved on crash
```

Before image/provider reachability: validate the correction hash, atomically
create its directory no-replace with restrictive permissions, sync the parent,
then publish `cgc-provider-contract-claim-v1` by create-new temporary write,
file flush, no-replace rename, directory sync, reread, and validation. Claim
fields bind accepted correction, execution ordinal 1, both projection hashes,
expected final image, provider version/lock/distribution, and evidence schema.
Directory creation consumes the correction.

After cleanup, evidence binds `claimSha256` and uses the same durable
create-new/no-replace protocol inside that directory. Missing, partial,
corrupt, duplicate, over-bound, or forbidden-content evidence fails.
Existing/empty/partial/raced directories are consumed and never repaired,
deleted, reused, or overwritten. A crash after directory creation at any stage
remains consumed; missing evidence is not resume authority. Each later fresh
ACCEPT uses a different correction hash/directory, preserving all history.

No execution occurs before this correction is independently accepted. That
acceptance permits exactly one run. Failure publishes v2 evidence and leaves
Story 4.1 blocked; only a fresh evidence-led grammar/query correction and fresh
acceptance permits one further run. Projection hashes remain unchanged unless
published evidence proves a query change is necessary. Attempt-0003 is never
invoked, modified, or retried.

## Historical qualification evidence

The qualification ledger is not this production contract.
`attempt-0003` boundary SHA-256
`b9b5487b844d1c4ce89e2f03636e8b0c64f99f4dcbf6723a4f1e152ef138853b`
and terminal `failed / provider-contract-failure` remain immutable. The failure
was a runner/evidence-channel defect and supplies no production-blocking
authority, no retry authority, and no successful structural evidence.
