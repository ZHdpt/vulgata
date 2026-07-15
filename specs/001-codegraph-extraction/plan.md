# Implementation Plan: CodeGraph Code Unit Extraction

**Branch**: `dev` | **Date**: 2026-07-15 | **Spec**: [spec.md](spec.md)

## Summary

This revision corrects `PLN-001` through `PLN-004`. CodeGraphContext documents
repository-path indexing, local context/database selection, and read-only
queries. It does not document file-list input, per-file time limits,
machine-readable query output, or endpoint identity after partitioning a
Repository.

The plan therefore proposes one local whole-Repository `cgc` graph build per
`ScanRun`, bounded by one whole-process timeout. Deterministic approximately
1,000-file groups remain only for Vulgata filter/audit records; they never
partition provider input or create provider processes. This preserves
same-Repository structural relationships in a single graph.

This deliberately changes Story/Spec behavior and clarifies PRD §5.4 FR-4.1.
It is a proposed PRD correction, not a silently weakened requirement. An
accepted independent plan review plus explicit user approval are required before
task generation. After approval, provider qualification is the first executable
dependency; provider mapping or structural persistence cannot begin until it
passes and a follow-up plan records an observed parser grammar.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`), with a digest-pinned CPython
3.12.x runtime whose exact patch is recorded in a repository-owned lock.

**Primary Dependencies**: ASP.NET Core Blazor Web App, EF Core 10/Npgsql,
FluentValidation, Fluent UI Blazor, system `git`, and the local
`codegraphcontext==0.5.0` qualification target.

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

**Constraints**: no LLM, MCP, Worker Agent/tool, Scan Coordinator, dispatch,
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
| V. Security & Data Stewardship | Private local Kuzu context/database; no API/MCP/remote backend; safe process arguments and diagnostics. | Pass, gated |
| Docker reproducibility | Digest-pinned base, locked Python/distributions, and non-root `version`/`doctor` are qualification criteria, not assumptions. | Pass, gated |

No Constitution exception is proposed. Without user approval of the correction,
this plan cannot advance to tasks.

## Proposed Scan Strategy and PRD Correction — approval required

### Provider-compatible algorithm

The following is implementation intent only after user approval and provider
qualification.

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
4. The supervisor creates one private context/database and invokes only the
   documented command sequence:

   ```
   cgc context create <run-context> --database kuzudb --db-path <private-db-dir>
   cgc --context <run-context> index <working-copy-dir> --force
   cgc --context <run-context> query "<static read-only structural projection>"
   cgc context delete <run-context>
   ```

   Qualification must prove this composition on the pinned version. Values in
   angle brackets are generated individual arguments, not interpolated shell
   text.
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

### Observable behavior changed or removed

| Current Story/Spec promise | Proposed corrected behavior | Reason and structural effect |
| --- | --- | --- |
| Approximately 1,000-file provider batches. | At-most-1,000-file audit/filter groups only; one provider build for the whole Repository. | `cgc index [PATH]` is the documented input. One graph keeps both endpoints visible. |
| Configurable 30-second per-file timeout. | Remove per-file timer/request; use deployment whole-process timeout, proposed default 1,800 seconds. | A group deadline is not per-file compliance and no candidate boundary is documented. |
| Crash/hang is isolated to one file and eligible siblings continue. | Provider crash/timeout terminates the `ScanRun` with no partial provider-derived data. Filter outcomes already recorded remain auditable. | Full graph correctness is preferred to unsupported partial continuation. |
| Filtered paths are excluded from parse input. | Vulgata excludes them from Code Unit/Edge persistence, but the local provider may inspect the whole private checkout. | The provider has no documented file-subset input. |
| Generic provider fixture permits result mapping. | Qualification must prove a stable projection/parser before an adapter or structural persistence exists. | The CLI publishes no machine-readable result grammar. |

Preserved guarantees are local-only source handling; one Repository per ScanRun;
deterministic filter audit; authorization; safe clone/missing-CLI behavior;
whole-graph same-Repository edges; `FileIdentityHash`; exact
`Exact|Approximate|Heuristic`; `ScanRun`/`ScanRuns`; Chinese UI; and absence of
LLM/MCP/Worker Agent/Coordinator/Cross-Repository Resolution.

### Proposed PRD/source reconciliation

The plan asks the user to approve the following minimal amendment/clarification:

1. PRD FR-4.1 permits one whole-Repository local graph per profile when the
   provider exposes repository-level indexing. Its filter remains an auditable
   Vulgata result filter, not a promise of provider file-subset input.
2. The Story/Spec approximately-1,000-file provider grouping, 30-second
   per-file timeout, and per-file continuation are superseded by audit groups,
   a configured whole-process timeout, and atomic provider-derived failure.
3. Story 4.1 stores structural Code Units with `Language` nullable; Story 4.2
   owns deterministic language/framework detection. This reconciles the Epic
   split with PRD FR-4.1's broad language/framework phrase; it does not remove
   that V1 behavior.

No PRD, BMad story, or specification is edited by this plan. The above is the
explicit post-plan decision required by `AGENTS.md`.

## Provider Qualification Gate — first implementation dependency

Qualification precedes provider-result mapping, Code Unit/Edge persistence, and
the production coordinator. Passing permits a new plan revision/review that
records the actual parser grammar; it does not authorize structural work by
itself.

### Reproducible inputs

- A later Docker change pins its base image by immutable digest. A
  repository-owned version file records CPython 3.12.x, which the final image
  asserts with `python --version`.
- A `requirements.lock` exact-pins and hashes `codegraphcontext==0.5.0`, `kuzu`,
  and every transitive Python package. Installation verifies hashes; scan runtime
  never invokes a resolver/installer.
- Qualification selects the documented embedded local backend with `context
  create <name> --database kuzudb --db-path <path>`. The path is unique,
  private, app-owned, and non-remote.
- The synthetic fixture has eligible symbols with `CALLS`, `IMPORTS`,
  `INHERITS`, and `CONTAINS`, and a relation to a filtered path. It contains no
  proprietary source, credential, or token.

### Exact pass criteria

All criteria must pass in the final non-root image:

1. Lock installation has no drift; `python --version` equals the locked 3.12.x
   value; `cgc version` equals 0.5.0; `cgc doctor` exits zero as `app`; the
   private Kuzu directory is writable.
2. The exact documented context/create/index/query/delete sequence exits zero
   for the whole fixture, within the whole-process limit, without network/API/
   MCP/watcher/bundle/registry/remote-database activity.
3. Bounded query output transforms through a deterministic grammar into every
   owned Code Unit field: normalized relative path, kind, symbol, positive
   ordered range, and provider structural endpoint identity. Vulgata computes
   content hashes itself from local files.
4. The snapshot contains every supported relationship kind with source/target
   metadata, proves all fixture endpoints coexist in the one graph, and yields a
   non-empty stable endpoint identity for the filtered-target candidate.
5. The grammar rejects malformed rows deterministically. The follow-up plan
   records exact static queries, grammar, normalized safe snapshot, commands,
   lock hashes, and image digest before mapping/persistence tasks exist.

### Fail outcome

Any failed criterion, timeout, unstable projection, unavailable field, or
missing filtered-target candidate stops before provider-result mapping or
structural persistence. Return only bounded synthetic evidence, command lines,
image/lock identities, and a failure category for corrective planning. Never
infer an undocumented flag, store schema, delimiter, subset semantic, or endpoint
identity.

## Data and Boundary Design

See [data-model.md](data-model.md) and
[contracts/repository-profiling-api.md](contracts/repository-profiling-api.md).

### Core seams after qualification

- `ScanRun`, `CodeUnit`, `CodeUnitEdge`,
  `UnresolvedStructuralRelationshipCandidate`, and `ScanCandidateOutcome` are
  Core entities/enums. `ScanRun` belongs to exactly one Repository.
- `IScanRunRepository` is aggregate-specific. A narrow structural store writes
  dependents transactionally and exposes no EF types or `IQueryable`.
- `IGitWorkingCopyService` is Core. A future `ICodeGraphCliService` is not
  finalized until qualification/re-plan proves its grammar. Infrastructure owns
  process, qualification, adapter, EF/repositories/migration; Web owns only an
  authorization facade, not a hosted Scan Coordinator.
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
- Scope is only `version`, `doctor`, `context create/delete`, `index`, and
  `query`. `api`, `mcp`, `watch`, bundles, registry, datasources, Neo4j setup,
  and remote backends are prohibited.
- Vulgata consumes a qualified projection, never a provider graph-store schema.
  Failed qualification means corrective planning, not a fallback parser.

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

0. Obtain the post-plan user approval below. Without it, do not generate tasks.
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
| Spec FR-017 | No LLM/MCP/Worker Agent/Coordinator/Documents/Uncertainty/Cross-Repository work. |
| Spec FR-018 and Story AC-1/AC-2 | Not met by documented provider; explicit user correction replaces these promises. |
| PRD §5.4 FR-4.1 | Structural pre-scan remains; provider input/timeout/continuation/filter and Story 4.2 allocation need approval. |
| Constitution IV/E2E matrix | Qualification is test-first; after mapping/UI/auth/API/persistence/migration work, E2E is mandatory. |

## Risks and assumptions

| Risk/assumption | Mitigation/disposition |
| --- | --- |
| No published query output grammar. | Hard-stop qualification and follow-up reviewed plan; no invented parser/JSON flag. |
| Partition can lose same-Repository edges. | Never partition provider input; map one complete graph. |
| Timeout loses all eligible provider work. | Explicit user-approved trade-off; preserves graph correctness and leaves filters auditable. |
| Provider sees filtered local files. | Local-only/private execution; Vulgata persists no structural data for filtered paths; explicit user approval. |
| Docker/Python resolution drift. | Digest/version/hash locks and non-root qualification. |

## Post-plan Authorization Gate

After independent plan acceptance, the root must ask this exact question before
task generation:

> Do you approve the proposed correction for Story 4.1 and PRD §5.4 FR-4.1?
> CodeGraphContext documents repository-level indexing only, so Vulgata will
> build one private local graph for the complete Repository with a configurable
> whole-process timeout (proposed default: 1,800 seconds). Approximately
> 1,000-file groups will be audit/filter groups only; the 30-second per-file
> timeout, per-file crash isolation, and sibling continuation will be removed.
> A provider timeout/crash will fail the whole `ScanRun` with no partial
> provider-derived Code Units/Edges, preserving same-Repository structural
> relationships in one graph. Filtered paths will not persist as Code Units or
> Edges, though the local provider may inspect them because it has no documented
> file-subset option. Story 4.1 keeps `Language` nullable and Story 4.2 performs
> language/framework detection. My intent is first to qualify a pinned local
> provider in a synthetic non-root container; only a passing qualification and a
> fresh reviewed parser plan permit structural persistence work. This changes
> Story/Spec behavior and clarifies PRD FR-4.1; do you approve proceeding to
> task generation on those terms?

The request cites this [plan](plan.md), [specification](spec.md), BMad Story
4.1, and PRD §5.4 FR-4.1/FR-4.9. Approval advances only to task generation; it
does not waive qualification, follow-up plan review, testing, E2E, security, or
verification gates.

## Complexity Tracking

No Constitution exception is required.
