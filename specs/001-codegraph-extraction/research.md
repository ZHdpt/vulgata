# Research: CodeGraph Code Unit Extraction

**Date accessed:** 2026-07-15
**Scope:** official CodeGraphContext documentation cited below, plus the
authorities named in Story 4.1 and the feature specification.

## Decision: use the local CodeGraphContext CLI only behind qualification

**Decision:** The user-approved provider target remains CodeGraphContext `cgc`,
with `codegraphcontext==0.5.1` selected for qualification. Vulgata will not
couple to its graph-store schema, API gateway, or MCP server.

**Rationale:** The official introduction describes a local semantic property
graph built from Tree-sitter and optional SCIP. The graph model documents File,
Class, Function, Module, and structural `CONTAINS`, `IMPORTS`, `CALLS`, and
`INHERITS` relationships. That can support a Vulgata-owned structural snapshot
only if the pinned CLI demonstrably exposes the required stable projection.

**Alternatives considered:** direct graph-store coupling (rejected: makes
provider internals Vulgata persistence); API/MCP server (rejected: out of scope
and exposes a tool/service); extension-only filtering (rejected: cannot prove
same-Repository structural relationships).

**Official evidence:**

- [Introduction](https://codegraphcontext.github.io/) — accessed 2026-07-15;
  describes semantic graph extraction.
- [Code graph model](https://codegraphcontext.github.io/concepts/graph-model/)
  — accessed 2026-07-15; documents paths/ranges for structural nodes and the
  relevant relationship kinds.

## Decision: whole-Repository graph build; audit groups are not provider batches

**Decision:** Propose one private local `cgc index <working-copy>` graph build
per `ScanRun`. Deterministic at-most-1,000-file groups remain only for Vulgata
filter/audit records; they do not select provider input, receive provider
timeouts, or create separate provider processes.

**Rationale:** The CLI reference documents `cgc index [PATH] [--force]`, not a
file-list or batch input. It also does not document a per-file timer, a process
boundary for one file, or an endpoint-identity guarantee after a materialized
subset is indexed. A one-graph strategy is the smallest provider-compatible
choice that does not silently lose a relationship whose endpoints would have
fallen into different groups.

**Alternatives considered:** group deadline multiplied by file count (rejected:
it is not a per-file time limit); bisection/materialized subsets (rejected:
undocumented subset and cross-group identity semantics); unlimited whole process
(rejected: no bounded failure behavior); undocumented CLI flags (rejected:
unsupported provider claim).

**Official evidence:**

- [CLI reference](https://codegraphcontext.github.io/reference/cli/) — accessed
  2026-07-15; documents repository-path `index`, `--force`, `query`, context
  workspaces, and global `--database`, `--db-path`, and `--context` options;
  it publishes no file-list, per-file timeout, or structured query-output
  contract.

## Decision: explicit Kuzu context with locked Python/Docker inputs

**Decision:** Qualification will choose the documented local `kuzudb` backend
through a per-run context and private `--db-path`. A later Docker change must
pin image, CPython, and Python distributions with immutable locks rather than
assume a default backend or a floating package resolver.

**Rationale:** The installation document says CodeGraphContext installs through
`pipx` or `pip`, lists `kuzu` as an embedded driver, and directs users to
select a default backend. The CLI separately documents `context create <name>
--database kuzudb --db-path <path>` and `cgc doctor`. That is enough evidence
for an explicit local context; it is not evidence that Kuzu is the Unix default
or that any unpinned driver version is compatible.

**Lock/qualification mechanism:** A future
`docker/codegraph/requirements.lock` will exact-pin and hash
`codegraphcontext==0.5.1`, `kuzu`, and transitive Python dependencies. A
digest-pinned container base plus a recorded CPython 3.12.x version make the
runtime reproducible. Qualification asserts the lock, `python --version`,
`cgc version`, and `cgc doctor` as the final non-root `app` user.

**Alternatives considered:** existing `codegraph` shim (rejected: no
extraction); runtime installation (rejected: resolver drift); FalkorDB default
assumption (rejected: docs make it conditional); remote backends (rejected: not
local-only single-container scope).

**Official evidence:**

- [Installation](https://codegraphcontext.github.io/getting-started/installation/)
  — accessed 2026-07-15; documents pip/pipx, driver packages, Kuzu embedding,
  backend choice, `cgc version`, and `cgc doctor`.
- [CLI reference](https://codegraphcontext.github.io/reference/cli/) — accessed
  2026-07-15; documents context create/delete and explicit local backend/path
  options.

## Decision: qualification is a hard stop before mapping/persistence

**Decision:** The first executable increment is a non-root container proof on a
synthetic whole-Repository fixture. It must capture the exact static read-only
queries and the pinned CLI's output grammar before a provider adapter, Code Unit
mapping, structural EF entity, migration, or production profiling flow is
implemented.

**Rationale:** The official `query` example establishes a read-only Cypher
command but no machine-readable output schema. The docs also do not prove that
a returned relationship exposes stable endpoints suitable for Vulgata's
Unresolved Structural Relationship Candidate. A generic DTO before proof would
be an invented provider contract.

**Pass criteria:** `cgc` 0.5.1 and Kuzu context work under `app`; indexing the
whole synthetic fixture succeeds within the bounded whole-process limit; a
deterministic parser can obtain path, kind, symbol, range, source/target
endpoint metadata, all supported relationship kinds, and the filtered-target
candidate; and bounded evidence is free of sensitive material. The follow-up
plan must contain the exact commands and grammar observed.

**Fail outcome:** stop before provider-result mapping or structural persistence,
return safe raw evidence and locks for correction, and do not invent JSON,
timeout, subset, or endpoint behavior.

## Decision: correct the unavailable qualification pin to the authorised latest

**Decision:** The qualification command
`python -m pip download --no-deps --dest <generated-private-directory>
codegraphcontext==0.5.0` failed because the configured package index did not
offer that exact distribution. On 2026-07-15 the user explicitly directed
Vulgata to use the latest available version. The observed package-index command
`python -m pip index versions codegraphcontext` returned `0.5.1` as latest, so
the executable qualification target is exact-pinned to
`codegraphcontext==0.5.1`.

**Rationale:** This replaces an unavailable exact pin without changing the
provider family, local-only/private execution, Kuzu context selection,
whole-Repository strategy, 1,800-second whole-process deadline, audit-only
groups, or the hard stop before mapping/persistence.

**Alternatives considered:** retain 0.5.0 (rejected: unavailable from the
configured index); float to an unpinned latest version (rejected: not
reproducible); select a different provider or remote service (rejected: outside
the user authorisation and plan scope).

**Evidence:** `python -m pip index versions codegraphcontext` exited 0 on
2026-07-15 and listed `0.5.1` first. The historical failed 0.5.0 attempt is
preserved in `qualification/qualification-result.json` and
`qualification/evidence/red.md`; it is failure evidence, not an executable
requirement. The official documentation pages observed on the same date remain
labelled 0.5.0, so they establish only a candidate command surface. They do not
prove 0.5.1 output compatibility; the existing non-root qualification hard stop
must prove it before mapping or persistence work.

## Proposed requirement correction requiring user approval

The documented provider cannot meet the Story/Spec's per-file batch/timeout/
continuation guarantees. The plan therefore asks the user to approve a
whole-process timeout (proposed default 1,800 seconds), atomic provider-derived
failure, and audit-only 1,000-file groups. It also asks for explicit PRD FR-4.1
clarification that the provider may locally inspect filtered paths while Vulgata
excludes them from persisted Code Units/Edges, and that Story 4.2 owns
language/framework detection. No requirement text is changed in this research
artifact.

## Binding Story 4.1 data decisions

`FileIdentityHash` is SHA-256 over the first 4,096 source bytes (or all bytes
when shorter); `SourceHash` is the full source-content hash.
`RangeConfidence` is `Exact|Approximate|Heuristic`. The aggregate/table pair is
`ScanRun`/`ScanRuns`. These user-authorised decisions are independent of
 provider qualification and remain binding.
