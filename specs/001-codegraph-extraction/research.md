# Research: CodeGraph Code Unit Extraction

**Date accessed:** 2026-07-15
**Scope:** official CodeGraphContext documentation cited below, plus the
authorities named in Story 4.1 and the feature specification.

## Decision: qualify CodeGraphContext through its local HTTP JSON gateway

**Decision:** The user-approved provider target remains CodeGraphContext `cgc`,
with `codegraphcontext==0.5.1` selected for qualification. The qualification
uses `cgc api start` as a loopback-only JSON transport inside one disposable,
non-root `--network none` container. This selects no production Scan transport:
Vulgata will not expose the gateway, couple to its internal graph-store schema,
or invoke its bundled MCP/tool routes. A passing qualification returns to a
separate production parser/integration plan and review.

**Rationale:** The official introduction describes a local semantic property
graph built from Tree-sitter and optional SCIP. The graph model documents File,
Class, Function, Module, and structural `CONTAINS`, `IMPORTS`, `CALLS`, and
`INHERITS` relationships. The pinned 0.5.1 package exposes `/api/v1/query` as a
JSON envelope over a read-only query handler, whereas `cgc query` emits a
terminal presentation table and no documented machine-readable grammar. The
qualification-only gateway therefore provides a feasible fail-closed candidate
for validating correlated structural facts without treating CLI rendering or
the provider's Kuzu schema as Vulgata's owned contract.

**Alternatives considered:** CLI-table parsing (rejected: presentation output
exceeded the original capture and does not emit the later test-invented
`key=value` grammar); direct graph-store coupling (rejected: makes provider
internals Vulgata persistence); production HTTP-gateway coupling (deferred:
qualification cannot select production Scan behavior); provider bundled
MCP/tool routes (rejected and not invoked: Story 4.1 adds no Vulgata MCP or
Worker Agent tool); extension-only filtering (rejected: cannot prove
same-Repository structural relationships).

**Official evidence:**

- [Introduction](https://codegraphcontext.github.io/) — accessed 2026-07-15;
  describes semantic graph extraction.
- [Code graph model](https://codegraphcontext.github.io/concepts/graph-model/)
  — accessed 2026-07-15; documents paths/ranges for structural nodes and the
  relevant relationship kinds.

The exact 0.5.1 request/envelope and relationship projections are
qualification candidates derived from the pinned installed package sources
identified in [plan.md](plan.md), not guarantees inferred from the public 0.5.0
documentation.

## Decision: whole-Repository graph build; audit groups are not provider batches

**Decision:** Use one private local `cgc index <working-copy>` graph build
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
synthetic whole-Repository fixture. It creates one private named Kuzu Context,
indexes once, starts the provider gateway on unpublished container loopback,
aggregates bounded `/health` readiness responses, submits exactly one static
read-only `/api/v1/query` request, stops the gateway, deletes the Context, and
cleans all private state before a provider adapter, Code Unit mapping,
structural EF entity, migration, or production profiling flow is implemented.

**Rationale:** The public CLI material establishes a read-only Cypher surface
but no machine-readable CLI output schema. The pinned 0.5.1 gateway supplies a
JSON candidate, but neither its envelope nor its nested query result is a
published versioned production contract. The qualification must therefore use
strict incremental UTF-8/JSON validation and prove all required facts inside
complete individual result objects. A generic DTO before proof would still be
an invented provider contract.

**Pass criteria:** `cgc` 0.5.1 and the isolated Kuzu Context work under `app`;
one whole-fixture index and one HTTP query finish within the bounded
whole-process limit; all provider stdout/stderr plus the aggregate readiness
and query-response channels are incrementally hashed and discarded under one
inclusive 65,536-byte cap; the strict candidate grammar proves correlated
path, kind, symbol, range and endpoint metadata for `CALLS`, `CONTAINS`,
`IMPORTS`, and `INHERITS`, including the filtered-target candidate; no bundled
MCP/tool route is invoked; and cleanup succeeds. The follow-up plan must record
the observed production parser grammar before mapping or persistence work.

**Fail outcome:** stop before provider-result mapping or structural persistence,
return only redacted counts, hashes, predicates, lifecycle/image/lock
identities, and a failure category for correction, and do not infer a JSON
field, production transport, timeout, subset, or endpoint behavior.

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

## Accepted specification baseline and qualification gate

The accepted specification no longer makes the older Story's unsupported
per-file batch, timeout, or continuation promises. FR-018 now requires one
whole-process timeout (default 1,800 seconds), atomic provider-derived failure,
and audit-only at-most-approximately-1,000-path groups, together with the
FR-4.1 clarification that the provider may locally inspect filtered paths while
Vulgata excludes them from persisted Code Units/Edges, and that Story 4.2 owns
language/framework detection. The remaining active approval boundary was only
the qualification-only local HTTP JSON transport/parser candidate. No
requirement text is changed in this regenerated research artifact.

## Binding Story 4.1 data decisions

`FileIdentityHash` is SHA-256 over the first 4,096 source bytes (or all bytes
when shorter); `SourceHash` is the full source-content hash.
`RangeConfidence` is `Exact|Approximate|Heuristic`. The aggregate/table pair is
`ScanRun`/`ScanRuns`. These user-authorised decisions are independent of
 provider qualification and remain binding.
