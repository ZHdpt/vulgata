# Implementation Plan: CodeGraph Code Unit Extraction

**Branch**: `dev` | **Date**: 2026-07-16 | **Spec**: [spec.md](spec.md)

## Summary

This revision preserves the previously approved whole-Repository `ScanRun`
strategy and corrects only the qualification transport/evidence path plus the
unavailable provider pin. `codegraphcontext==0.5.0` was not available from the
configured package index during qualification on 2026-07-15. The user then
authorised the latest available version; `python -m pip index versions
codegraphcontext` observed `0.5.1` as latest on the same date.
CodeGraphContext documents repository-path indexing, local context/database
selection, and read-only queries. Its installed 0.5.1 package also exposes a
local HTTP gateway, but that gateway has no `--context` option and declares its
query payload as untyped JSON data. The HTTP gateway is a
**qualification-only local transport** candidate, not the selected Vulgata
production Scan transport or integration contract. It does not document
file-list input, per-file time limits, a versioned structural-result schema, or
endpoint identity after partitioning a Repository.

The public documentation observed on that date still labels its examples as
0.5.0. It is authority for the candidate command surfaces only, not proof that
the unavailable 0.5.0 documentation grammar is unchanged in 0.5.1. The
unchanged qualification hard stop must prove every invoked 0.5.1 behavior.

The plan therefore uses one local whole-Repository `cgc` graph build per
`ScanRun`, bounded by one whole-process timeout. Deterministic approximately
1,000-file groups remain only for Vulgata filter/audit records; they never
partition provider input or create provider processes. This preserves
same-Repository structural relationships in a single graph.

That whole-Repository strategy was approved by the user and is now encoded in
the independently accepted specification (FR-018, acceptance scenario 1.3,
and the provider-failure edge case). It is the executable baseline for this
feature. This
revision does **not** reopen PRD behavior, production Scan behavior, or the
earlier approval. The only new approval boundary after an accepted independent
plan review is the test-only HTTP JSON qualification transport and parser
candidate. After that approval, provider qualification is the first executable
dependency; provider mapping or structural persistence cannot begin until it
passes and a follow-up plan records an observed parser grammar.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`), with a digest-pinned CPython
3.12.x runtime whose exact patch is recorded in a repository-owned lock.

**Primary Dependencies**: ASP.NET Core Blazor Web App, EF Core 10/Npgsql,
FluentValidation, Fluent UI Blazor, system `git`, and the local
`codegraphcontext==0.5.1` qualification target.

**Storage**: PostgreSQL 17 schema `vulgata`, SQLite test override, and a
transient private Kuzu database per provider run. The provider database is never
Vulgata persistence truth.

**Testing**: xUnit, `WebApplicationFactory<Program>`, local bare-Git fixtures,
and a non-root Linux container qualification with synthetic credential-free
source. Full UI/auth/API/persistence/migration E2E is required only after a
qualified provider contract is re-planned.

**Target Platform**: Linux application container, non-root `app` user; Windows
development does not constitute provider qualification.

**Project Type**: Layered Blazor web application.

**Performance Goals**: deterministic filtering/audit grouping for 10,000 source
candidates; one provider graph build for one Repository; finite deployment
whole-process deadline. No per-file service-level timing guarantee is claimed.

**Constraints**: no LLM, Vulgata MCP endpoint/tool, Worker Agent/tool, Scan Coordinator, dispatch,
language/framework detection, Document/Uncertainty persistence, or
Cross-Repository Resolution. Source and provider data stay local; diagnostics
are bounded/redacted; all UI copy is Simplified Chinese.

**Scale/Scope**: one synchronous Repository attempt, private working copy and
provider context/database; no queue, dashboard, SignalR, restart/resume engine,
or partial provider-result continuation.

## Constitution Check

| Gate | Plan response | Status |
| --- | --- | --- |
| I. Layered Project Boundaries | Core owns entities/contracts; Infrastructure owns EF, Git, process, qualification and future adapter; Shared owns DTOs/validators; Web owns authorization/UI. | Pass, provider-gated |
| II. Domain Purity & Ubiquitous Language | Use `ScanRun`/`ScanRuns`, Code Unit, Code Unit Edge, and File Identity Hash. No EF in Core, annotations, generic repository, or query leakage. | Pass |
| III. Validation & API Contract Discipline | Preserve FluentValidation, `ProblemDetails`, `ManagementAccess`, existing visibility semantics, and Chinese-only UI. | Pass |
| IV. Test-First Integration Harness | A synthetic provider qualification test is the first executable increment. Production mapping/persistence waits for its pass and re-plan. | Pass, gated |
| V. Security & Data Stewardship | Private local Kuzu context/database and private provider home; loopback-only HTTP gateway with no published Docker port; no Vulgata MCP/Worker Agent tool or remote backend. The provider's bundled MCP/tool routes remain inside the unpublished no-network container and are never invoked; safe process arguments and diagnostics. | Pass, gated |
| Docker reproducibility | Digest-pinned base, locked Python/distributions, and non-root `version`/`doctor` are qualification criteria, not assumptions. | Pass, gated |

No Constitution exception is proposed. The whole-Repository specification and
the qualification-only HTTP JSON boundary already have their required user
approvals; this regenerated plan still requires independent plan review before
tasks may be regenerated.

## Approved Scan Strategy Baseline and Qualification-Only Approval Boundary

### Provider-compatible algorithm

The following production-adjacent scan strategy is the already approved
baseline for Story 4.1. This revision keeps it unchanged. The only new
post-review approval requested by this plan is the test-only HTTP JSON
qualification transport described later in this document.

1. An authorised SystemOwner selects `开始扫描`. The application uses existing
   Repository visibility before creating/exposing a `ScanRun`.
2. `GitWorkingCopyService` clones or fast-forwards one private working copy with
   `ProcessStartInfo.ArgumentList`, no interactive credentials, and bounded
   sanitized errors. A failure is terminal and preserves
   `仓库克隆失败：{details}`.
3. `ScanCandidateEnumerator` walks the entire checkout by ordinal
   repository-relative path. Generated, vendored, binary, minified JavaScript,
   protobuf/Thrift, and `.gitignore` paths persist as `Filtered`. Eligible paths
   receive a deterministic `AuditGroupOrdinal`, at most 1,000 paths per group.
   An audit group is never provider input, a provider deadline, or a dispatch.
4. The supervisor creates one private context/database and invokes only a
   separately qualified production provider integration. This plan preserves
   the whole-Repository graph requirement but deliberately does **not** select
   the HTTP gateway, its request shape, or a result parser as production Scan
   behavior. A later post-qualification plan/review must make that choice. The
   current qualification-only lifecycle is:

   ```
   cgc context create <run-context> --database kuzudb --db-path <private-db-dir>
   cgc context mode named
   cgc context default <run-context>
   cgc --context <run-context> index <working-copy-dir> --force
   cgc api start --host 127.0.0.1 --port <private-port>
   GET http://127.0.0.1:<private-port>/health
   POST http://127.0.0.1:<private-port>/api/v1/query
   SIGTERM <gateway-pid>; then SIGKILL only after its bounded grace period
   cgc context delete <run-context>
   ```

   The gateway process inherits one generated private `HOME`, uses named-mode
   default Context selection, binds only to container loopback, and receives
   no Docker-published port. The HTTP request is exactly one static read-only
   JSON query; health readiness polling is not a query retry. The provider's
   bundled MCP and tool routes are not invoked. Qualification
   must prove this composition on the pinned version. Values in angle brackets
   are generated individual arguments, not interpolated shell text.
5. One `CodeGraphProcessSupervisor` owns the whole process tree. Its proposed
   deployment setting is `CodeGraph:WholeRepositoryProcessTimeoutSeconds`,
   default **1,800 seconds**. Its clock starts with `index` and ends after the
   projection. Cancellation, timeout, crash, non-zero exit, or unreadable output
   kills the process tree, cleans the private provider directory, marks the
   `ScanRun` Failed/Aborted with a bounded message and `ParsedFileCount = 0`, and
   writes no provider-derived Code Units, Edges, or unresolved candidates. It
   does not retry, bisect, or claim a file-specific result.
6. Only a successful whole graph and qualified projection may enter Vulgata
   validation. Valid records commit atomically. A malformed individual record
   from a completed projection is rejected with a safe file-path warning while
   independently valid records may persist.
7. Endpoint joins happen against the entire graph snapshot. An edge persists
   only when both Code Units are in the same Repository/ScanRun. A validated
   source with non-empty provider structural metadata for a non-persisted target
   becomes one Unresolved Structural Relationship Candidate. No Document,
   Uncertainty, Cross-Repository Resolution, or partial edge is created.

### Accepted specification behavior and provider realization

| Accepted specification requirement | Planned realization | Reason and structural effect |
| --- | --- | --- |
| FR-018: exactly one atomic whole-Repository provider graph build per `ScanRun`. | Invoke one `cgc index <working-copy>` and one structural projection for the complete Repository. | `cgc index [PATH]` is the documented input and one graph keeps both endpoints visible. |
| FR-018: optional deterministic groups of at most approximately 1,000 eligible paths are audit/progress metadata only. | Assign `AuditGroupOrdinal` after ordinal path enumeration; never use a group as provider input, process, request, retry, or deadline. | This preserves deterministic audit/progress without inventing provider partition semantics. |
| FR-018: one configurable whole-process limit, default 1,800 seconds, from indexing through projection. | `CodeGraphProcessSupervisor` owns the complete process tree and applies `CodeGraph:WholeRepositoryProcessTimeoutSeconds` once across index and projection. | The provider documents no per-file input or timer. |
| FR-018: provider cancellation, timeout, hang, crash, unsuccessful exit, or unreadable output fails provider-derived results atomically. | Terminate the process tree, persist bounded terminal `ScanRun` state, retain previously determined local filtering outcomes, and commit no provider-derived Code Units, Edges, or unresolved candidates. | Atomic failure avoids unsupported partial-graph claims and preserves graph correctness. |
| FR-005 and FR-018: filtered outcomes remain auditable while provider input is the whole Repository. | Vulgata excludes filtered paths from Code Unit/Edge persistence, while the local provider may inspect the private checkout. | The provider has no documented file-subset input; source remains organisation-controlled. |
| FR-003/FR-011: consume validated structural results through a Vulgata-owned contract. | Qualification probes a loopback HTTP JSON envelope and private-Context isolation, but does not select a production adapter, result grammar, or structural persistence. | CLI tables are presentation output; the gateway's nested payload is not a published versioned schema. |

Preserved guarantees are local-only source handling; one Repository per ScanRun;
deterministic filter audit; authorization; safe clone/missing-CLI behavior;
whole-graph same-Repository edges; `FileIdentityHash`; exact
`Exact|Approximate|Heuristic`; `ScanRun`/`ScanRuns`; Chinese UI; and absence of
LLM/Vulgata MCP/Worker Agent/Coordinator/Cross-Repository Resolution.

### Accepted Story/specification reconciliation

The accepted specification is the executable authority for these provider-
dependent details. It records the already approved Story 4.1 reconciliation:

1. FR-4.1 is implemented through one whole-Repository local graph per profile
   when the provider exposes repository-level indexing. Its filter remains an
   auditable Vulgata result filter, not a promise of provider file-subset input.
2. FR-018 requires audit-only at-most-approximately-1,000-path groups, one
   configured 1,800-second whole-process timeout, and atomic provider-derived
   failure. It makes no per-file batching, timing, retry, or continuation claim.
3. Story 4.1 stores structural Code Units with `Language` nullable; Story 4.2
   owns deterministic language/framework detection.

This HTTP JSON qualification revision does not request any further PRD change.
If the user later elects to reopen PRD language, that is a separate
change-control action outside this plan revision.

## Provider Qualification Gate — first implementation dependency

Qualification precedes provider-result mapping, Code Unit/Edge persistence, and
the production coordinator. Passing permits a new plan revision/review that
records the actual parser grammar; it does not authorize structural work by
itself.

### Superseding correction — CodeGraphContext local HTTP JSON qualification

The historical Bookworm qualification completed one non-root private Context,
whole-Repository index, static CLI query, and cleanup sequence. Its rendered
table exceeded the old 8,192-character capture before the completion marker was
observed, so it correctly stopped as `unsafe-evidence`. The approved streaming
correction solved that size defect, but its later
`vulgata-cgc-structural-record-v1` implementation incorrectly expected an
invented pipe-separated `key=value` record. CodeGraphContext 0.5.1 does not
emit that record from `cgc query`; its CLI renders presentation output.

This revision keeps **CodeGraphContext 0.5.1** as the provider and `cgc` as its
executable. It replaces only the qualification transport with the provider's
local HTTP gateway, `cgc api start`, whose `/api/v1/query` endpoint returns a
JSON envelope. It does not select a second provider, directly query Kuzu,
expose a Vulgata HTTP endpoint, add a Vulgata MCP or Worker Agent tool, or
implement a production adapter. The provider package's bundled MCP SSE/message
and tool routes remain confined to unpublished container loopback and are never
called. The gateway is a local CodeGraphContext process used only by the
synthetic qualification container.

The change is more than the approved evidence-size fix: it changes the provider
interaction and parser candidate from a CLI table to local HTTP JSON. It
invalidates the accepted streaming-record plan and its dependent task/analysis
claims. A fresh independent plan review and explicit post-review user approval
are required before a replacement provider execution. The historical run does
not consume the one new execution. There is no retry: a `pass` returns to a
separate parser/integration plan and a `fail` returns only redacted evidence for
corrective planning.

### Streaming HTTP evidence protocol — test-only and fixed

The replacement run uses `vulgata-cgc-http-json-v1` together with
`vulgata-cgc-streaming-evidence-v2`. These are qualification-observer
contracts, not a CodeGraphContext result schema or a production parser. They
have the following non-negotiable rules:

1. The test starts the named container with `--network none`, which still permits
   its internal loopback interface but forbids external network traffic. It
   mounts only the read-only synthetic fixture, creates a generated app-owned
   private `HOME` and Kuzu directory with mode `0700`, and passes that exact
   `HOME` to every `cgc`, gateway, and cleanup process. It creates one named
   Context, sets named mode/default Context, then verifies locally that the
   generated config identifies that Context and its generated Kuzu path without
   emitting either value as evidence. The fixture query itself must return only
   fixture-derived structural facts; otherwise Context isolation is unproved.
2. The gateway starts as one child process with
   `cgc api start --host 127.0.0.1 --port <private-port>`. Readiness is exactly
   `GET http://127.0.0.1:<private-port>/health`, whose pinned 0.5.1
   `api/app.py` source returns `{ "status": "ok" }`. The runner polls that URI
   for at most 10 seconds, at 100-millisecond intervals, without sending a
   query. All readiness responses are streamed, discarded, and appended in
   attempt order to one logical `cgc-api-health` channel; they are not
   structural evidence. `healthResponseBytes` and `healthResponseSha256` are
   the aggregate byte count and SHA-256 of that single logical channel, so
   every readiness byte remains inside the same 65,536-byte budget. It sends
   exactly one query after readiness. Cleanup sends `SIGTERM` to
   the recorded gateway process group, waits at most 5 seconds for zero exit,
   then sends `SIGKILL` only if necessary. `gateway-stop` is a runner lifecycle
   action, not a nonexistent `cgc` subcommand; forced kill makes the result fail.
3. Every provider-process stdout/stderr stream—`cgc version`, `doctor`, Context
   create/mode/default/delete, index, and gateway—plus bounded HTTP health- and
   query-response bodies—is framed by runner-generated stream boundaries,
   consumed incrementally, hashed, and discarded. No command output is written to a temporary evidence
   file or buffered as text. All logical channels share one atomic raw-byte
   budget of **65,536 bytes**. Exactly 65,536 bytes is in budget; the 65,537th
   byte is the overflow sentinel. On overflow, the observer includes that byte
   in exactly one logical-channel count/hash, stops observation, terminates the
   gateway/container process tree, and forces private cleanup. It must not issue
   another provider command or HTTP request as recovery.
4. Each logical channel has a raw-byte count and SHA-256; no
   scheduling-dependent merged-stream hash is allowed. `canonicalSummarySha256`
   is the SHA-256 of the UTF-8 bytes of this exact pipe-delimited field order:

   ```text
   version|providerChannelCounts|providerChannelHashes|healthResponseBytes|httpResponseBytes|observedTotalBytes|overflowed|terminationRequested|containerCleanupForced|healthResponseSha256|httpResponseSha256|grammarId|relationshipKinds|predicateCounts|cleanupAttempted|cleanupSucceeded
   ```

   `version` is literal `1.2`; counters use unpadded decimal; booleans use
   lowercase `true` or `false`; provider channel names are lexical and fixed;
   the logical channel set includes exactly one aggregate `cgc-api-health`
   channel and one `cgc-api-query` channel, so readiness polling never creates
   ad hoc per-attempt channel names; `healthResponseBytes` and
   `healthResponseSha256` are aliases for the aggregate health channel;
   `grammarId` is literally
   `vulgata-cgc-http-json-v1,vulgata-cgc-streaming-evidence-v2`; and
   `relationshipKinds` is the comma-joined lexical order of the observed enum
   values.
   `predicateCounts` is the
   comma-joined fixed sequence `valid=<decimal>,invalidOrOversized=<decimal>,`
   `CALLS=<decimal>,CONTAINS=<decimal>,IMPORTS=<decimal>,INHERITS=<decimal>,`
   `allRequiredRelationshipKindsObserved=<boolean>,`
   `allStructuralFactsCorrelatedWithinValidRecords=<boolean>,`
   `hasFilteredTargetCandidate=<boolean>,hasNonEmptyEndpointIdentity=<boolean>,`
   `hasPositiveOrderedRange=<boolean>,hasValidRecordGrammar=<boolean>,`
   `queryCompleted=<boolean>`. The enum names and predicate names are therefore
   lexical, and no field may contain a source, path, symbol, query row, or other
   provider value. This hash is the only combined identity.
5. Health responses are non-structural but are streamed, discarded, and
   aggregated into the single `cgc-api-health` logical channel under the same
   cap. The HTTP query-response body alone is structural evidence. It uses strict
   incremental UTF-8 and a streaming JSON reader; it never calls
   `ReadAsString`, retains a raw response/chunk, or accepts replacement
   decoding. The request shape is source-derived from pinned
   `codegraphcontext/api/schemas.py` (`QueryRequest.query`, `QueryRequest.params`)
   and `api/router.py` (`/api/v1/query` delegates to
   `execute_cypher_query`). The one request body is exactly:

   ```json
   {
     "query": "MATCH (source:Function)-[relationship:CALLS]->(target:Function) RETURN 'CALLS' AS relationship_kind, source.path AS source_path, source.name AS source_symbol, source.line_number AS source_start_line, source.end_line AS source_end_line, source.uid AS source_identity, target.path AS target_path, target.name AS target_symbol, target.line_number AS target_start_line, target.end_line AS target_end_line, target.uid AS target_identity, false AS filtered_target UNION ALL MATCH (source:Class)-[relationship:CONTAINS]->(target:Function) RETURN 'CONTAINS' AS relationship_kind, source.path AS source_path, source.name AS source_symbol, source.line_number AS source_start_line, source.end_line AS source_end_line, source.uid AS source_identity, target.path AS target_path, target.name AS target_symbol, target.line_number AS target_start_line, target.end_line AS target_end_line, target.uid AS target_identity, false AS filtered_target UNION ALL MATCH (source:File)-[relationship:IMPORTS]->(target:Module) RETURN 'IMPORTS' AS relationship_kind, source.path AS source_path, source.name AS source_symbol, relationship.line_number AS source_start_line, relationship.line_number AS source_end_line, source.path AS source_identity, target.path AS target_path, target.name AS target_symbol, target.line_number AS target_start_line, target.line_number AS target_end_line, target.full_import_name AS target_identity, false AS filtered_target UNION ALL MATCH (source:Class)-[relationship:INHERITS]->(target:Class) RETURN 'INHERITS' AS relationship_kind, source.path AS source_path, source.name AS source_symbol, source.line_number AS source_start_line, source.end_line AS source_end_line, source.uid AS source_identity, target.path AS target_path, target.name AS target_symbol, target.line_number AS target_start_line, target.end_line AS target_end_line, target.uid AS target_identity, false AS filtered_target UNION ALL MATCH (source:Function)-[relationship:CALLS]->(target:File) RETURN 'CALLS' AS relationship_kind, source.path AS source_path, source.name AS source_symbol, source.line_number AS source_start_line, source.end_line AS source_end_line, source.uid AS source_identity, target.path AS target_path, target.name AS target_symbol, NULL AS target_start_line, NULL AS target_end_line, target.path AS target_identity, true AS filtered_target ORDER BY relationship_kind, source_identity, target_identity LIMIT 256",
     "params": {}
   }
   ```

   The field names are a **fail-closed qualification candidate** based on the
   pinned 0.5.1 `core/database_kuzu.py` schema: `CALLS` includes
   `Function -> Function` and `Function -> File`; `CONTAINS` includes
   `Class -> Function`; `INHERITS` includes `Class -> Class`; and `IMPORTS` is
   `File -> Module`. `Function`/`Class` expose `uid`, `name`, `path`,
   `line_number`, and `end_line`; `IMPORTS.line_number` supplies the File import
   site range; `Module.line_number` supplies a one-line Module anchor; and
   `File.path`/`Module.full_import_name` are their endpoint identities. These
   are not a claimed production schema. Any unavailable property, syntax
   difference, null identity, or missing fixture relation makes this gate fail
   rather than allowing a new property name or fallback expression.
6. Pinned `api/router.py` wraps the query-handler result in `ApiResponse.data`,
   and `tools/handlers/query_handlers.py` constructs `success`, `query`,
   `record_count`, and `results`. The observer therefore accepts only one JSON
   object with no trailing token, HTTP success status, `status: "ok"`, no
   non-null `error`, and a `data` object containing `success: true`, the exact
   static query string, non-negative integer `record_count`, and a `results`
   array of that length. Optional envelope fields may be absent or null. This
   source-derived response shape is also a candidate: any serialization or
   provider-version difference is `unsafe-evidence`, not a reason to infer a
   new contract.
7. Each `results` object must contain only the twelve scalar aliases above. The
   observer validates one correlated record by relationship type. `CALLS`,
   `CONTAINS`, and `INHERITS` with `filtered_target: false` require two complete
   Code Unit-like endpoint tuples: normalized relative path, non-empty symbol
   and identity, and positive ordered range. `IMPORTS` requires a File import
   site (File path/name identity and the positive relation line as its one-line
   range) plus a Module anchor (path/name/full-import identity and positive
   one-line range). A `filtered_target: true` `CALLS` row may have null target
   ranges only when its target path is the fixture-filtered path; it proves an
   Unresolved Structural Relationship Candidate input, not a resolved Code Unit
   Edge. The observer rejects every other null, unknown/missing/duplicate alias,
   nested or source-bearing field, wrong JSON type, record over 4,096 raw bytes,
   and cross-record fact joining. It immediately discards transient values. The
   test must prove all four kinds and the filtered-target candidate from complete
   individual JSON records.
8. The durable result uses schema version `1.2` and a closed
   `structuralEvidence` object. It may retain only the two grammar IDs, channel
   byte counts/hashes, total/overflow/termination/cleanup facts, lifecycle
   counts, relationship-kind counts, record counts, boolean predicates, and the
   canonical summary hash. It cannot retain a path, symbol, source, endpoint,
   port, raw JSON, raw query, or other provider value. Schema 1.0 and the
   rejected 1.1 attempt remain historical evidence only.
9. Cleanup failure has precedence over every classification. Normal or forced
   cleanup must stop the gateway, delete the named Context, private Kuzu
   database, private provider home, and named container. Any cleanup failure is
   `fail / cleanup-failure / corrective-planning`, even with an otherwise valid
   JSON response.
10. A `pass` requires exactly one, zero-exit lifecycle entry for `cgc-version`,
   `cgc-doctor`, `cgc-context-create`, `cgc-context-mode`,
   `cgc-context-default`, `cgc-index`, `cgc-api-start`, `cgc-api-health`,
   `cgc-api-query`, `gateway-stop`, and `cgc-context-delete`; no duplicate or
   unapproved entry is allowed. It also requires successful cleanup, no
   overflow/termination/forced cleanup, byte conservation across every logical
   provider channel, and every structural predicate. These rules validate only the
   qualification evidence; they are not a production `CodeGraphParseResult`
   parser or Scan behavior.

### Reproducible inputs

- A later Docker change pins its base image by immutable digest. A
  repository-owned version file records CPython 3.12.x, which the final image
  asserts with `python --version`.
- A `requirements.lock` exact-pins and hashes `codegraphcontext==0.5.1`, `kuzu`,
  and every transitive Python package. Installation verifies hashes; scan runtime
  never invokes a resolver/installer.
- Qualification selects the documented embedded local backend with `context
  create <name> --database kuzudb --db-path <path>`, then explicitly sets named
  mode/default Context selection because the local HTTP gateway has no
  `--context` option. The generated provider home and database are unique,
  private, app-owned, and non-remote.
- The synthetic fixture has eligible symbols with `CALLS`, `IMPORTS`,
  `INHERITS`, and `CONTAINS`, and a relation to a filtered path. It contains no
  proprietary source, credential, or token.

### Exact pass criteria

All criteria must pass in the final non-root image:

1. Lock installation has no drift; `python --version` equals the locked 3.12.x
   value; `cgc version` equals 0.5.1; `cgc doctor` exits zero as `app`; the
   private Kuzu directory is writable.
2. The exact Context/create/mode/default/index/api-start/health/query/stop/delete
   lifecycle exits zero for the whole fixture, within the whole-process limit,
   using only container loopback. It performs no external network, call to a
   bundled MCP/tool route, watcher, bundle, registry, datasource, or
   remote-database activity. A pass records
   exactly one approved lifecycle entry and successful cleanup.
3. The observer satisfies the complete HTTP streaming protocol above: strict
   incremental UTF-8/JSON, one inclusive 65,536-byte cap across every provider
   process stream plus the single aggregate `cgc-api-health` channel and the
   bounded `cgc-api-query` response body, separate logical-channel identities,
   no raw retention, and total-byte conservation. A pass has no overflow,
   requested termination, or forced cleanup. Its canonical summary is the fixed
   serialization above, not a merged-pipe hash.
4. The observer proves deterministic JSON projection facts only when they are
   correlated within one complete result object: normalized relative paths,
   relationship kind, non-empty symbols, positive ordered ranges, and provider
   endpoint identities. The record counts include `CALLS`, `IMPORTS`,
   `INHERITS`, and `CONTAINS`, prove all fixture endpoints coexist in the one
   graph, and yield a non-empty endpoint identity for the filtered-target
   candidate. Vulgata computes content hashes itself from local files.
5. The observer fails atomically as `unsafe-evidence` on invalid UTF-8/JSON,
   an unsafe/non-scalar projection, unexpected envelope, response overflow,
   invalid/oversized object, missing Context isolation proof, or an unproved
   structural predicate. Cleanup failure takes precedence. A future plan, not
   this observer, records the observed production parser grammar and integration
   contract before mapping/persistence tasks exist.

### Fail outcome

Any failed criterion, timeout, unstable projection, unavailable field, missing
filtered-target candidate, unsafe JSON/output, missing private-Context proof, or
unproved structural predicate stops before provider-result mapping or structural
persistence. Return only bounded synthetic evidence identities, lifecycle names,
image/lock identities, and a failure category for corrective planning. Never
infer an undocumented API field, store schema, delimiter, subset semantic,
endpoint identity, or production parser contract.

## Data and Boundary Design

See [data-model.md](data-model.md) and
[contracts/repository-profiling-api.md](contracts/repository-profiling-api.md).

### Core seams after qualification

- `ScanRun`, `CodeUnit`, `CodeUnitEdge`,
  `UnresolvedStructuralRelationshipCandidate`, and `ScanCandidateOutcome` are
  Core entities/enums. `ScanRun` belongs to exactly one Repository.
- `IScanRunRepository` is aggregate-specific. A narrow structural store writes
  dependents transactionally and exposes no EF types or `IQueryable`.
- `IGitWorkingCopyService` is Core. A future CodeGraphContext integration
  contract is not finalized until qualification/re-plan evaluates the HTTP JSON
  candidate and selects a production transport. Infrastructure owns process, qualification, adapter,
  EF/repositories/migration; Web owns only an authorization facade, not a
  hosted Scan Coordinator.
- `IScanStateService` remains the Epic 5 no-op seam; no Worker Agent, job,
  dispatch, or scheduling state is added.

### Binding data decisions

Use `ScanRun`/`ScanRuns` (not conceptual `scans`), File Identity Hash
SHA-256(first `min(4096, file length)` bytes), full-file Source Hash, and
`Exact|Approximate|Heuristic` Range Confidence. These user-authorized decisions
remain binding regardless of provider qualification.

## Provider Integration and Deployment

- The current `codegraph` shim is not a provider. Later qualification work
  replaces it with locked `cgc` installation, immutable base/Python locks,
  non-root `version`/`doctor`, and no runtime dependency resolution.
- Kuzu is explicitly selected because the official docs document it as an
  embedded Python driver and document `kuzudb`/`--db-path`; the plan does not
  claim Kuzu is a universal/default backend.
- Qualification scope is only `version`, `doctor`, `context
  create/mode/default/delete`, `index`, one loopback `api start`, one health
  readiness check, one loopback HTTP query, and runner-controlled gateway stop.
  calls to the provider's bundled MCP/tool routes, watchers, bundles, registry,
  datasources, Neo4j setup, remote backends, external networking, and a
  published API port are prohibited. No Vulgata MCP endpoint or Worker Agent
  tool is added.
- This plan does not select a production query transport. A later approved
  CodeGraphContext integration must consume a qualified projection rather than
  Kuzu tables or a provider graph-store schema. Failed qualification means
  corrective planning, not a fallback parser.

## Source Layout

```
src/dotnet/Vulgata.Core/                 # structural entities/contracts
src/dotnet/Vulgata.Infrastructure/       # EF, Git, CodeGraph process/adapter
src/dotnet/Vulgata.Shared/               # profiling DTOs/validators
src/dotnet/Vulgata.Web/                  # API, coordinator, Repository details UI
tests/Vulgata.Tests/                     # qualification and integration/E2E
docker/Dockerfile                        # future locked cgc installation
docker/codegraph/                        # future Python/package locks
specs/001-codegraph-extraction/          # plan artifacts
```

## Implementation Sequence (for later task generation)

0. Complete independent review of this regenerated plan set. The recorded
   whole-Repository and qualification-only approval gates are already satisfied.
1. Implement only reproducible provider qualification: Docker/base/Python locks,
   cgc installation, synthetic fixture, non-root command proof, whole-process
   timeout test, and safe evidence. Do not add mapping, structural EF entities,
   migrations, or profiling UI.
2. On qualification failure, stop for corrective planning. On pass, amend this
   plan/data model/contract with exact parser grammar and obtain fresh plan
   review before structural persistence tasks are generated.
3. Only then write red tests and implement Core, EF, Git, qualified adapter,
   migration, authorized endpoints/UI, and the required E2E.
4. Run focused/full tests, Web build, Docker qualification, and approved E2E.
   Do not add agent tools, dispatch, Coordinator, language/framework detection,
   Documents, Uncertainty, or Cross-Repository Resolution.

## Requirements and seam trace

| Requirement/source | Plan disposition and proof |
| --- | --- |
| Spec FR-001, FR-013, FR-016 | Reuse Repository detail/coordinator visibility; endpoint/UI tests wait for qualified contract. |
| Spec FR-002, FR-015 | Git service/local bare fixture/safe Chinese failure are preserved. |
| Spec FR-003, FR-010, FR-011; PRD FR-4.9 | Whole-graph qualification first; no adapter/mapping/persistence before proof/re-plan. |
| Spec FR-004–FR-009, FR-012 | Target model stays; persistence follows only the qualified contract. |
| Spec FR-005 | Filtered audit persists; provider input filtering is an explicit PRD clarification. |
| Spec FR-017 | No LLM/Vulgata MCP/Worker Agent/Coordinator/Documents/Uncertainty/Cross-Repository work; bundled provider MCP/tool routes are not invoked. |
| Spec FR-018; acceptance scenario 1.3; provider-failure edge case | Satisfied directly: one whole-Repository index/projection, audit-only at-most-approximately-1,000-path groups, one configurable 1,800-second whole-process limit, and atomic provider-derived failure with local filtering outcomes retained. The older Story prose is a superseded provider assumption, not an executable per-file contract. |
| PRD §5.4 FR-4.1 | Structural pre-scan remains; the whole-Repository input/timeout/continuation/filter baseline and Story 4.2 allocation are already approved; only the qualification-only HTTP JSON transport/parser candidate required the later approval gate. |
| Constitution IV/E2E matrix | Qualification is test-first; after mapping/UI/auth/API/persistence/migration work, E2E is mandatory. |

## Risks and assumptions

| Risk/assumption | Mitigation/disposition |
| --- | --- |
| The HTTP gateway envelope is structured but its nested query payload is not a published versioned schema. | Qualification proves a fixed, version-pinned JSON projection and Context isolation; a pass still requires a follow-up parser plan before production mapping. |
| Partition can lose same-Repository edges. | Never partition provider input; map one complete graph. |
| Timeout loses all eligible provider work. | Explicit user-approved trade-off; preserves graph correctness and leaves filters auditable. |
| Provider sees filtered local files. | Local-only/private execution; Vulgata persists no structural data for filtered paths; explicit user approval. |
| Docker/Python resolution drift or unavailable package pin. | Exact lock/version evidence and non-root qualification; `0.5.0` is recorded only as the failed historical pin, while `0.5.1` is the current executable target. |
| HTTP gateway resolves its Context from configuration rather than `--context`. | Use a generated private `HOME`, named mode, and generated default Context; qualification must prove the API observes the indexed synthetic graph and cleanup removes all private state. |
| Public documentation remains labelled 0.5.0 while the configured package index serves 0.5.1. | Treat the docs as a command-surface candidate only; qualification must prove the installed 0.5.1 commands, HTTP JSON projection, Context isolation, and local Kuzu behavior before mapping work. |

## Recorded Authorization Boundary

The previously separate authorization gate for this revision has already been
satisfied. The whole-Repository `ScanRun` baseline was approved first, and the
qualification-only HTTP JSON transport/parser candidate was later approved as
the only new post-review change-control boundary. The following question is
retained as the exact decision record that framed that later approval:

> Do you approve the revised CodeGraphContext qualification correction for Story
> 4.1? CodeGraphContext documents repository-level indexing
> only, so Vulgata will
> build one private local graph for the complete Repository with a configurable
> whole-process timeout (already approved at 1,800 seconds). Approximately
> 1,000-file groups are audit/filter groups only under the already approved
> baseline; the 30-second per-file timeout, per-file crash isolation, and sibling continuation were removed by that earlier approval.
> A provider timeout/crash will fail the whole `ScanRun` with no partial
> provider-derived Code Units/Edges, preserving same-Repository structural
> relationships in one graph. Filtered paths will not persist as Code Units or
> Edges, though the local provider may inspect them because it has no documented
> file-subset option. Story 4.1 keeps `Language` nullable and Story 4.2 performs
> language/framework detection. The CLI's rendered table cannot meet the
> redacted structural-evidence gate, so my intent is to qualify the same pinned
> CodeGraphContext provider through its local loopback HTTP JSON gateway. The
> container will create one generated private `HOME` and named Kuzu Context,
> index the whole synthetic Repository once, start `cgc api` on `127.0.0.1`
> without a published Docker port, send one read-only JSON query, and delete all
> private state. The test will retain only capped counts, hashes, and predicates;
> it will not add a Vulgata endpoint, MCP/Worker Agent tool, production parser,
> direct Kuzu integration, mapping, persistence, or a second provider execution.
> A pass still requires a fresh reviewed parser plan before structural work. This
> materially changes only the provider qualification transport and parser candidate;
> do you approve proceeding to regenerated qualification tasks on those terms?

The request cites this [plan](plan.md), [specification](spec.md), BMad Story
4.1, and PRD §5.4 FR-4.1/FR-4.9. Approval advances only to task generation; it
does not waive qualification, follow-up plan review, testing, E2E, security, or
verification gates.

## Complexity Tracking

No Constitution exception is required.
