# Implementation Plan: CodeGraph Code Unit Extraction

**Branch**: `dev` | **Date**: 2026-07-18 | **Spec**: [spec.md](spec.md)
**Story**: `4-1-codegraph-integration-and-code-unit-extraction`
**Provider boundary**: CodeGraphContext `0.5.1`, whole-Repository loopback HTTP JSON

## Summary

Story 4.1 uses one bounded CodeGraphContext `0.5.1` whole-Repository index and
the local `cgc api` loopback gateway as its production provider transport. This
is a technical backfill of the Scan strategy already approved on 2026-07-16;
it does not reopen per-file batching, change the PRD, or add an LLM, MCP tool,
Worker Agent tool, dispatch behavior, or Scan Coordinator.

The production parser uses **two independent read-only `/api/v1/query`
projections**:

1. a Code Unit projection enumerates every provider-observed `File`, `Module`,
   `Class`, and `Function` node, including nodes with no relationships; and
2. a relationship projection enumerates `CONTAINS`, `IMPORTS`, `CALLS`, and
   `INHERITS` separately and joins endpoints by a Vulgata-owned stable endpoint
   identity.

This replaces the current relationship-only discovery assumption, which cannot
return isolated Code Units. Both responses are validated into one owned
`CodeGraphProviderResult`; provider/process/top-level failures remain atomic,
while malformed independent rows are rejected with bounded warnings and do not
discard other valid rows.

The envelope and provider coordinate properties below are **candidate
0.5.1 grammar**, not authority inferred from controlled JSON fixtures or the
public 0.5.0-labelled documentation. Story completion has a mandatory,
separate production provider-contract E2E that runs the exact Projection A and
Projection B texts through the exact-pinned provider installed in an image
derived from the final application image. Only aliases and envelope behavior
observed in that real run may be accepted into the owned grammar. A mismatch,
skip, absent evidence artifact, or non-pass result fails closed before Story
4.1 completion.

The first gate execution failed closed as designed: exact 0.5.1 returned HTTP
200 and one top-level JSON object, but not the candidate
`success:true,error:null,data` envelope. It retained no property names/types
and published no evidence, so it authorizes no grammar guess. After successful
Context deletion, cleanup also found `graph.kuzu` was a regular file and the
directory-only remover raised `NotADirectoryError`. Container and temporary
image tags were removed. No provider rerun has occurred.

The historical `attempt-0003` qualification boundary
`b9b5487b844d1c4ce89e2f03636e8b0c64f99f4dcbf6723a4f1e152ef138853b`
was executed once and is immutable `failed / provider-contract-failure`. Its
runner routed health/query payloads through the wrong evidence channel, omitted
dedicated HTTP lifecycle evidence, discarded structural evidence on failure,
and substituted empty evidence. The user explicitly accepted that as a harness
defect and directed Story 4.1 production completion. It is retained as history,
not as production-blocking authority and not as authority to retry.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`); exact-pinned
`codegraphcontext==0.5.1` in the Linux application image. The current
`docker/codegraph/requirements.lock` SHA-256 is
`10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95`;
its CodeGraphContext distribution SHA-256 is
`3cd79efe6c9525d69ae250c063404b500ef7595b6c63284d841c78a1ecf48658`.

**Primary Dependencies**: ASP.NET Core, EF Core 10/Npgsql,
FluentValidation, Fluent UI Blazor, system `git`, CodeGraphContext `cgc`, and
its private local Kuzu context.

**Storage**: PostgreSQL 17 schema `vulgata`; SQLite integration-test override.
CodeGraphContext's private graph is transient provider state, never Vulgata
persistence truth.

**Production provider transport**: unpublished `127.0.0.1` HTTP gateway in the
application container. Vulgata invokes only `GET /health` and read-only
`POST /api/v1/query`; it does not invoke provider MCP/tool routes, publish the
gateway port, or integrate directly with Kuzu.

**Provider-contract authority**: exact final application image plus
`codegraphcontext==0.5.1`, a credential-free local Repository fixture, the two
exact immutable production projection texts/hashes, and a bounded redacted
`cgc-provider-contract-evidence-v2` pass/fail artifact. Fake HTTP/process
fixtures remain unit/integration-test inputs only.

**Performance/limits**: one configurable 1,800-second operation deadline from
whole-Repository index through both projections; 64 MiB combined raw HTTP body
cap, 4,096-byte maximum row, 250,000 combined row cap, bounded diagnostics, and
no retry/bisection/per-file continuation.

**Scope**: one Repository and one `ScanRun`; audit/progress groups of at most
approximately 1,000 paths never partition provider input.

## Constitution Check

| Gate | Plan response | Status |
| --- | --- | --- |
| I. Layered Project Boundaries | Core owns the provider result contract and domain entities; Infrastructure owns Git, `cgc`, HTTP parsing, EF, and persistence; Shared owns DTO validation; Web owns the authorised facade/UI. | Pass |
| II. Domain Purity & Ubiquitous Language | Uses `ScanRun`, Repository, Code Unit, Code Unit Edge, and Cross-Repository Resolution exactly; no EF/data annotations/generic repository/query leakage in Core. | Pass |
| III. Validation & API Contract Discipline | FluentValidation, plural minimal-API routes, `ProblemDetails`, existing Repository visibility, and Simplified-Chinese UI remain binding. | Pass |
| IV. Test-First Integration Harness | Controlled tests cover parser behavior; a mandatory exact-pinned final-image real-provider contract E2E separately proves the envelope, coordinate aliases, both exact projections, isolated nodes, four relationship kinds, endpoint identity, and filtered-target mapping before completion. | Pass |
| V. Security & Data Stewardship | Source and provider state remain local; loopback is unpublished; output and warnings are bounded; credentials/source are never logged; no provider MCP/tool route is called. | Pass |

No Constitution exception or ADR conflict is identified. Post-design
re-evaluation remains green because the two-projection grammar changes only the
owned provider adapter and its tests; project boundaries and the accepted
whole-Repository strategy are unchanged.

## Official Provider Evidence

- [Code graph model](https://codegraphcontext.github.io/concepts/graph-model/)
  (accessed 2026-07-18) documents the directed property graph, the structural
  `File`, `Module`, `Class`, and `Function` node types, relative paths/names/
  ranges, and `CONTAINS`, `IMPORTS`, `CALLS`, and `INHERITS`.
- [HTTP API reference](https://codegraphcontext.github.io/reference/api/)
  (accessed 2026-07-18) documents `cgc api start`, loopback host/port options,
  `GET /health`, and read-only `POST /api/v1/query`.
- [How ingestion works](https://codegraphcontext.github.io/concepts/how-it-works/)
  (accessed 2026-07-18) documents file discovery, Tree-sitter declaration
  extraction, cross-file resolution, graph node/edge commit, and Cypher query
  serving.

The public site is labelled `0.5.0`; it does not publish a versioned response
envelope, and its coordinate examples do not establish the exact 0.5.1
property names. The envelope and coordinate references below therefore remain
candidate grammar until the mandatory real-provider contract gate observes
them against the exact final image/package pin. Vulgata owns the accepted
aliases, validation, bounds, endpoint identity, and mapping rather than
treating Kuzu tables or an undocumented provider DTO as its domain contract.

## Approved Whole-Repository Production Flow

1. Reuse existing Repository management visibility before creating or exposing
   a `ScanRun`.
2. Clone or update one private working copy. A safe acquisition failure marks
   the run failed and returns `仓库克隆失败：{details}`.
3. Enumerate candidates, compute deterministic audit groups, and persist
   filtered outcomes before provider execution.
4. Create one private provider home, Kuzu database, and named/default Context.
   Run one `cgc index <working-copy> --force`, then start one unpublished
   loopback gateway.
5. After bounded health readiness, send the Code Unit projection and then the
   relationship projection under the same operation deadline and Context.
6. Strictly validate both envelopes and rows in memory. Derive stable endpoint
   identities and map valid Code Units independently of relationships.
7. Resolve relationship endpoints against the complete valid Code Unit map.
   Persist resolved same-Repository Code Unit Edges; persist a target-absent
   relationship as an Unresolved Structural Relationship Candidate only when
   the source Code Unit exists and the target identity is valid.
8. Stop the gateway, delete the Context, then inspect each prevalidated private
   state entry without following links: absence is success, a regular file
   such as `graph.kuzu` is unlinked, a directory is recursively removed without
   link traversal, and a link/reparse/special entry fails cleanup. Atomically
   persist only after successful projection and cleanup.
9. Complete the `ScanRun`, counts, duration, bounded warnings, and human-review
   flag. No Worker Agent dispatch occurs.
10. Before Story completion, execute the separate final-image real-provider
    contract gate defined below. This is not qualification attempt-0003 and
    creates no qualification-ledger record.

Cancellation, timeout, hang, crash, unsuccessful exit, invalid top-level JSON,
limit overflow, query-echo mismatch, or cleanup failure fails provider-derived
results atomically. There is no per-file provider retry, continuation, or
bisection.

## Candidate Owned Projection Grammar

The exact query texts are the production contract candidates and are hashed
over their UTF-8 code-fence content with LF line endings and no trailing LF.
Implementation and the real-provider contract E2E must consume the same
constants; copying or reconstructing a semantically similar query is not
acceptable. Projection A is
`07b13dcb0b95a2ab26da930e9fcbafef7cde27488df99b0634d860ef0767d9a8`;
Projection B is
`40d2da3b9b520b3c4eca3fd7c3bad4f4fb10643736e3a9e9b4ddf62c92adc096`.

Exact 0.5.1 execution showed that the multi-column UNION candidate can stall
inside Kuzu. Both owned projections therefore use one heterogeneous graph
match with closed node/relationship label allowlists.
Completeness is proven by all four node kinds in independent Projection A;
0.5.1's module-level Function nodes make an all-relationship “isolated node”
fixture assertion invalid because `CONTAINS` connects every declaration.
The gate records both hashes, verifies the provider's exact
`data.query` echo where that envelope is observed, and accepts no coordinate
source property merely because a fake response used it.

### Common HTTP envelope

Each request body is `{ "query": <exact projection>, "params": {} }`. A valid
response is one UTF-8 JSON object:

```text
success: true
error: null
data:
  success: true
  query: exact request query
  record_count: non-negative integer
  results: array with exactly record_count rows
```

Missing, duplicate, wrong-typed, or contradictory required envelope fields,
trailing JSON, invalid UTF-8, a non-success status, query mismatch, response
cap overflow, or record-count mismatch is a top-level provider failure. Unknown
envelope fields are ignored only after the required grammar validates, so
additive provider metadata cannot enter Vulgata's contract.

### Projection A: Code Units

One read-only Cypher query is a `UNION ALL` of label-specific matches over
`File`, `Module`, `Class`, and `Function`. It does not match an edge. Every row
has exactly these aliases:

```text
record_kind        "code_unit"
provider_kind      "File" | "Module" | "Class" | "Function"
path               string | null
name               string | null
start_line         integer | null
end_line           integer | null
```

The current candidate query aliases `line_number` to `start_line`. The real
provider gate must observe successful evaluation of each referenced source
property and the six exact returned aliases. If exact 0.5.1 instead exposes
`start_line`, a different end property, or a different envelope, this grammar
and the production parser/query must be corrected together and the gate rerun;
the implementation must not accept both spellings speculatively. Rows are
ordered by provider kind, path, name, start, and end.

Mapping rules:

- `Class` and `Function` require an eligible normalized relative path,
  non-empty name, and positive inclusive ordered range; a directly observed
  provider range maps to `Exact`.
- `File` requires an eligible relative path. Vulgata derives its display name
  from the path when absent and its exact `1..line-count` range from the same
  local bytes used for content hashes, so its range maps to `Exact`.
- `Module` requires a non-empty name. It becomes a persisted Code Unit only
  when it also has an eligible path and a valid range. A pathless or rangeless
  Module remains a valid endpoint tuple for relationship mapping but is not a
  persisted Code Unit. A provider single-line Module coordinate maps to
  `Approximate`; no heuristic range is invented.
- Provider `Method` declarations remain `Function` nodes and map to the
  existing Code Unit type policy without inventing a fifth provider label.
- Nodes from locally filtered paths are not persisted as Code Units.

Because this projection is edge-free, an isolated valid Class or Function is
still enumerated and persisted.

### Projection B: relationships

One independent read-only Cypher query is a `UNION ALL` over the documented
relationship shapes: File-to-Class and File-to-Function containment,
Class-to-Function containment, File-to-Module/File imports,
Function-to-Function calls, and Class-to-Class inheritance. Each row has:

```text
record_kind              "relationship"
relationship_kind        "CONTAINS" | "IMPORTS" | "CALLS" | "INHERITS"
source_provider_kind     provider node kind
source_path              string | null
source_name              string | null
source_start_line        integer | null
source_end_line          integer | null
target_provider_kind     provider node kind
target_path              string | null
target_name              string | null
target_start_line        integer | null
target_end_line          integer | null
```

Rows are ordered by relationship kind and the two endpoint tuples. No
relationship row creates a Code Unit. Both endpoint identities are derived by
the same function used for Projection A and are mapped only after all Code Unit
rows have been validated. Projection B is immutable between build, test, and
execution: its canonical hash must match before request dispatch, the observed
query echo, and the evidence artifact.

### Stable endpoint identity

Normalize kind to its canonical provider value, path to forward-slash
repository-relative form (or empty), name to trimmed provider text (or empty),
and lines to invariant decimal (or empty). The canonical UTF-8 tuple is:

```text
kind "\0" path "\0" name "\0" start_line "\0" end_line
```

The owned identity is `cgc-v1:` followed by lowercase SHA-256 hex of that
tuple. It is deterministic for both projections, contains no source or secret,
and does not depend on a Kuzu internal ID.

Exact duplicate rows are deduplicated. The same identity paired with different
normalized fields is a contract collision and fails the provider result
atomically. A valid relationship with a persisted source but absent target
creates one Unresolved Structural Relationship Candidate carrying the target
`cgc-v1:` identity. If the source is absent or either endpoint tuple cannot
produce a stable identity, the row is malformed and no Edge/candidate is
created.

## Bounded and Malformed-Row Behavior

- The two bodies share a 64 MiB raw-byte cap, a 250,000-row cap, and a
  4,096-byte per-row cap. Crossing any cap is a top-level atomic provider
  failure.
- Required row fields are closed and scalar. Unknown, duplicate, nested,
  source-bearing, wrong-typed, or overlong fields invalidate that row.
- A malformed independent row is skipped and produces a bounded Warning with a
  normalized path when safely available; otherwise it uses `(unknown)`.
- At most 100 warning details are retained/logged. Aggregate rejected counts
  remain accurate after detail sampling stops. No source, raw JSON, absolute
  checkout path, credential, token, or unbounded provider diagnostic is kept.
- A successful pair of envelopes may contain malformed rows; valid independent
  rows still map. A failed/absent second response commits none of the first
  response's provider-derived rows.
- `ParsedFileCount` is the distinct eligible-path count represented by at least
  one valid persisted Code Unit. At 500 or more candidates with zero Code Units,
  `NeedsHumanReview` is true and a warning is recorded.

## Data and Boundary Design

See [data-model.md](data-model.md) and
[contracts/repository-profiling-api.md](contracts/repository-profiling-api.md).

- `ICodeGraphProvider` returns only the owned normalized
  `CodeGraphProviderResult`; Core never sees HTTP, JSON, Cypher, process, or Kuzu
  types.
- Infrastructure owns provider startup, both queries, strict parsing,
  endpoint-key construction, bounded warnings, cleanup, and result mapping.
- The aggregate-specific structural store commits Code Units, Code Unit Edges,
  Unresolved Structural Relationship Candidates, provider outcomes, and
  terminal `ScanRun` state transactionally.
- `IScanStateService` remains the existing Epic 5 seam. No queue, Worker Agent,
  dispatch lifecycle, Document, Uncertainty, or Cross-Repository Resolution is
  introduced.

## Source Layout

```text
src/dotnet/Vulgata.Core/DomainServices/        # owned provider result seam
src/dotnet/Vulgata.Core/Entities/              # ScanRun and structural domain
src/dotnet/Vulgata.Infrastructure/CodeGraph/   # cgc lifecycle, projections, parser
src/dotnet/Vulgata.Infrastructure/Data/        # aggregate persistence
src/dotnet/Vulgata.Shared/                     # API DTOs/validators
src/dotnet/Vulgata.Web/                        # authorised profiling facade/UI
tests/Vulgata.Tests/                           # parser, mapping, persistence, E2E
docker/                                        # exact CodeGraphContext runtime
```

This plan creates no implementation tasks and authorizes no source change by
itself.

## Production Test Design

Tests must be written against the owned grammar and current production seam:

1. A parser RED proves the relationship-only query omits an isolated Function;
   GREEN proves Projection A returns it and Projection B remains independent.
2. Projection coverage includes isolated File, Module, Class, and Function
   rows; File/local Module mapping; pathless Module endpoint-only behavior; all
   four relationship kinds; exact duplicate deduplication; and stable identity
   equality across projections.
3. Mapping tests prove resolved same-Repository Edges, target-absent unresolved
   candidates, source-absent rejection, filtered-target exclusion, unsupported
   kinds, collisions, and no target-null Edge.
4. Parser boundary tests cover invalid UTF-8/JSON, trailing JSON, query mismatch,
   wrong record count, missing/duplicate/unknown/nested fields, row/body/count
   limits, warning sampling, and no raw/source/secret retention.
5. Provider orchestration tests prove exactly one whole-Repository index, one
   gateway, exactly two read-only query projections, one 1,800-second deadline,
   cleanup, and atomic failure when either projection/process/cleanup fails.
6. Persistence/integration tests prove filtered audit durability, transactional
   structural writes, count/human-review rules, migration/SQLite behavior,
   authorization, Chinese errors, and the public API/UI result.
7. The deterministic local-Git E2E proves an authorised start persists an
   isolated Code Unit plus resolved/unresolved relationships without network,
   LLM, MCP, Worker Agent, dispatch, Document, Uncertainty, or
   Cross-Repository Resolution.
8. The mandatory real-provider contract E2E
   `Vulgata.Tests.CodeGraphProviderContractTests.FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract`
   runs separately against a credential-free local Repository fixture in a
   verifier image whose base is the exact final application image. It adds no
   provider package and first proves the final-image identity plus installed
   `codegraphcontext==0.5.1`. It invokes the exact Projection A and B constants,
   verifies the observed envelope and only the coordinate properties actually
   evaluated by 0.5.1, isolated provider nodes, all four relationship kinds,
   one locally filtered target mapped only to an Unresolved Structural
   Relationship Candidate, `cgc-v1` equality for shared endpoint tuples, and
   Projection B request/echo/hash immutability. It emits only the bounded
   redacted evidence schema in the provider contract and fails on skip,
   mismatch, missing evidence, raw/source leakage, or cleanup failure.

Validation commands are listed in [quickstart.md](quickstart.md).

## Requirements Trace

| Authority | Plan disposition |
| --- | --- |
| Story AC-1/FR-003/FR-018 | Production `cgc` 0.5.1, one whole-Repository index, loopback HTTP JSON, two owned read-only projections, one 1,800-second limit. |
| Story AC-2/FR-011 | Top-level/process failure is atomic; malformed independent rows warn and valid rows continue; zero-unit review rule retained. |
| FR-005 | Filtered outcomes persist before provider execution and filtered provider nodes never become Code Units. |
| FR-006–FR-010 | Independent Code Unit enumeration, deterministic endpoint identity, resolved Edge constraints, and unresolved target representation are explicit. |
| FR-013–FR-016 | Existing management visibility, minimal counts, and safe Simplified-Chinese errors are unchanged. |
| FR-017 | No LLM, MCP/Worker Agent tool, dispatch, Document, Uncertainty, language/framework inference, Cross-Repository Resolution, or Scan Coordinator. |
| Approved 2026-07-16 deviation | No per-file provider batch, timeout, retry, bisection, or continuation is reintroduced. |
| Q41-009 / raw P1 | Controlled fixtures are not provider authority. Story completion requires the separate exact-pinned 0.5.1 final-image contract E2E and bounded redacted evidence for both exact production projections. |

## Historical Qualification Disposition

`attempt-0001`, `attempt-0002`, and `attempt-0003` remain immutable historical
harness records. In particular, `attempt-0003`'s terminal category and evidence
must not be edited, reclassified, retried, or cited as successful structural
evidence. Its failure explains why the qualification observer is not used as
the production parser authority. The new provider-contract E2E has its own
test identity and evidence schema, makes no attempt-ledger write, and neither
retries nor repairs attempt-0003.

## Mandatory Real-Provider Contract Gate

The exact command and evidence contract are in [quickstart.md](quickstart.md)
and [repository-profiling-api.md](contracts/repository-profiling-api.md).
Completion requires all of the following:

- the verifier base-image identity equals the just-built final application
  image identity and the installed distribution is exactly
  `codegraphcontext==0.5.1`, with the exact lock/distribution hashes above;
- the credential-free local fixture is indexed once with no network/provider
  credential and both canonical production query hashes match before dispatch;
- the real provider accepts both exact queries and supplies one observed
  envelope/coordinate grammar that the production parser accepts exactly;
- isolated nodes, `CONTAINS|IMPORTS|CALLS|INHERITS`, filtered-target unresolved
  mapping, and Projection A/B `cgc-v1` endpoint equality pass;
- Projection B request hash, provider query-echo hash, and evidence hash are
  identical, and no graph mutation/index/watcher occurs between projections;
- a closed-schema evidence file no larger than 16 KiB contains only hashes,
  versions, counts, booleans, enum/property names, cleanup facts, and the pass
  verdict—never source, rows, raw JSON/query text, paths, symbols, credentials,
  tokens, or diagnostics.

Any non-pass result, missing or over-bound evidence, unrecognized alias or
envelope, exact-query/hash drift, source-bearing evidence, provider/package
drift, or cleanup failure blocks Story completion. The correction path is to
update the owned grammar and production code/tests under fresh review, then
rerun this separate gate. It is never to weaken parsing or retry
attempt-0003.

### Correction iteration 3: safe observation, cleanup, and rerun

Strict parsing remains unchanged and fail-closed. In parallel, a bounded
streaming observer must publish `cgc-provider-contract-evidence-v2` on pass or
fail. It may retain only HTTP status, response byte count/hash, JSON root and
nested value kinds, raw allowlisted names or unknown-name HMAC metadata,
structural counts, immutable query/image/package hashes, cleanup facts, and a
bounded failure category. It
must never retain scalar response values, source, rows, raw JSON/query, paths,
symbols, credentials, tokens, endpoint values, error text, stdout/stderr, or
diagnostics.

The closed evidence bounds are 16 KiB, JSON depth 4, 32 structural containers,
128 unique property observations, 32 array summaries, 64 sorted unique alias
names of at most 64 UTF-8 bytes, and 32 coordinate-binding name pairs. Overflow
sets `shapeComplete=false`, publishes
`primaryFailureCategory=observation-bound-exceeded`, and remains failed.

After cleanup, the runner validates one record, writes a unique same-directory
temporary file, durably flushes, and performs an atomic create-new/no-replace
rename to
`provider-contract/<AcceptedCorrectionSha256>/evidence.json`. Missing,
corrupt, partial, duplicate, over-bound, or forbidden-content evidence fails
the gate. Cleanup facts never replace the primary failure; cleanup failure
turns any otherwise passing result into fail.

Do not rerun under the failed boundary. After this observer/cleanup correction
is independently accepted and implemented, permit exactly one provider
execution. A fail publishes v2 evidence and keeps Story 4.1 blocked; no retry
is permitted until that evidence drives a fresh grammar/query correction and
fresh independent acceptance, after which exactly one further execution is
allowed. The current Projection A/B hashes remain unchanged unless published
evidence proves a query change is required. Attempt-0003 remains untouched.

### Correction iteration 4: immutable claim and secret-safe names

P3-001 is closed by making the accepted correction hash the immutable
execution namespace:

```text
TestResults/codegraph-provider-contract/
  <AcceptedCorrectionSha256>/
    claim.json
    evidence.json
    claim.tmp-* / evidence.tmp-*   # preserved after a crash
```

The correction hash is exactly 64 lowercase hexadecimal characters and
identifies the fresh accepted review boundary for these five artifacts. Before
any image/provider reachability, the runner atomically creates that directory
with no replacement and restrictive permissions and durably syncs its parent.
Directory creation itself consumes the correction. It then create-new writes,
flushes, atomically renames, directory-syncs, rereads, and closed-schema
validates `claim.json`. An existing, empty, partial, invalid, or raced
directory is consumed and blocks before provider reachability; it is never
repaired, reused, deleted, or overwritten.

The immutable claim binds schema version, accepted-correction hash,
`executionOrdinal=1`, both unchanged projection hashes, expected final-image
hash, provider version/lock/distribution hashes, and evidence schema version.
Evidence binds `claimSha256` and is create-new published in the same directory.
A crash after directory creation, claim publication, provider work, cleanup,
or evidence temporary write leaves the correction consumed. Missing final
evidence is a failed execution, not permission to resume. A later accepted
correction uses a different hash/directory and preserves all prior history.

P3-002 is closed by retaining raw property names only when they exactly equal
this case-sensitive ASCII allowlist:

```text
status message error data success query record_count results
record_kind provider_kind path name start_line end_line relationship_kind
source_provider_kind source_path source_name source_start_line source_end_line
target_provider_kind target_path target_name target_start_line target_end_line
```

Before provider reachability, the claimed runner generates a fresh 256-bit
CSPRNG key held only in process memory. Every non-allowlisted decoded property
name is streamed into:

```text
HMAC-SHA256(
  ephemeralKey,
  "vulgata-cgc-unknown-property-v1" || NUL ||
  uint64-big-endian(utf8Length) || NUL || decodedUtf8Name)
```

Evidence retains only lowercase digest, UTF-8 length, JSON value kind,
container ordinal, and first-seen property ordinal. The same unknown name is
correlatable only within one execution. The key/raw name are destroyed after
publication and never persisted, evidenced, logged, included in exceptions, or
sent to test output. Unkeyed name hashes are forbidden. Any unknown name fails
the contract even when safely represented.

Logs use only allowlisted names or
`unknown-property(<digest>,<utf8Length>,<valueKind>,<ordinal>)`; values and raw
unknown names are forbidden. Oversize names are streamed through HMAC without
materialization/logging, retain only full length/digest metadata, and fail.
Tests cover concurrent claim races; crashes before/after each durability
boundary; partial/corrupt claim/evidence; duplicate execution; preserved
history; adversarial path/symbol/source/token property-name canaries; safe
allowlist controls; repeated-name same-run correlation; different-run digest
unlinkability; log/exception canaries; and oversize names.

This correction authorizes no provider run. Only a fresh independent ACCEPT
for iteration 4 may supply `AcceptedCorrectionSha256` and permit one claimed
execution.

## Risks and Remaining Uncertainty

| Risk | Mitigation / disposition |
| --- | --- |
| Public docs are labelled 0.5.0, do not publish the envelope, and differ on coordinate naming. | Treat current envelope/properties as candidates; mandatory exact-pinned final-image real-provider observation ratifies only evidenced aliases and fails closed on drift. |
| A provider release changes labels/properties/envelope. | Exact 0.5.1 pin; any version change requires contract review and updated parser tests. |
| Very large graphs exceed bounded projection limits. | Fail provider-derived results atomically with a safe operator message; do not silently truncate or page with invented snapshot semantics. |
| Module nodes may lack a local path/range. | Preserve their stable endpoint tuple for relationships; persist a Module Code Unit only when path/range meet Code Unit invariants. |
| Two projections could observe different graph state. | The private Context is immutable between completed index and both queries; no watcher/index mutation runs; any process/query failure is atomic. |

No unresolved product clarification, Constitution exception, ADR conflict, or
new product decision remains. The provider grammar is an intentionally
unratified technical contract until the mandatory real-provider gate passes;
that pending execution evidence is a completion gate, not a planning
clarification.

## Complexity Tracking

No Constitution violation requires justification.
