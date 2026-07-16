# Sprint Change Proposal — CodeGraphContext HTTP JSON Qualification

**Date:** 2026-07-15
**Mode:** Batch, requested directly by the user
**Scope:** Story 4.1 provider qualification only

## 1. Issue summary

The selected provider is **CodeGraphContext 0.5.1**. Its executable is `cgc`.
Earlier architecture/epic material uses the generic term “CodeGraph CLI”; that
is stale terminology, not a second selected provider.

The historical qualification correctly stopped because a rendered `cgc query`
table exceeded an 8,192-character evidence capture before its end marker. The
approved streaming correction removed that size limit but then introduced a
test-only `key=value` record recognizer. CodeGraphContext does not emit that
record: its CLI renders a terminal table. The green synthetic recognizer tests
therefore prove only the invented format, not the provider.

The pinned local package exposes `cgc api start` and `/api/v1/query`. The
gateway returns a JSON envelope, although its nested query payload has no
published versioned schema and gateway Context selection relies on provider
configuration rather than a `--context` flag.

The previously approved whole-Repository `ScanRun` strategy remains the
baseline for Story 4.1. This proposal does not reopen that earlier correction,
does not change PRD behavior, and does not change production Scan behavior.
It changes only the test-only qualification transport and parser candidate.

## 2. Impact analysis

| Area | Impact |
| --- | --- |
| Epic 4 / Story 4.1 | Reopens only the provider qualification plan. No production Story 4.1 work is unlocked. |
| PRD / feature specification | No requirement is weakened or changed. The existing whole-Repository Scan strategy remains intact. |
| Qualification plan | Replace CLI-table evidence with a loopback-only CodeGraphContext HTTP JSON qualification. |
| Task graph / analysis | Invalidate the schema-1.1 `key=value` task graph and regenerate only after a fresh plan review and approval. |
| Test-only harness | Existing observer/writer are not provider-compatible; retain them only as invalidated worktree artifacts until regenerated tasks replace them. |
| Architecture / epics | The generic “CodeGraph” terminology should later be reconciled to “CodeGraphContext `cgc` integration”; no runtime architecture decision changes in this proposal. |
| UX / API / agents | No change. The provider gateway is neither a Vulgata endpoint nor an MCP/Worker Agent tool. Its bundled MCP/tool routes exist only on unpublished container loopback and the qualification never invokes them. |

## 3. Recommended approach

Adopt a **local HTTP JSON qualification transport** while keeping the provider,
image, package pin, whole-Repository index, private Kuzu database, and
1,800-second whole-process limit unchanged.

The replacement synthetic run must:

1. create a generated private `HOME`, named Context, and Kuzu database;
2. set named mode/default Context so `cgc api start` resolves the isolated
   database;
3. index the synthetic Repository once;
4. run the container with `--network none`, bind the provider gateway only to
   `127.0.0.1`, and expose no Docker-published port;
5. make bounded readiness probes that aggregate into one logical health channel,
   then send exactly one read-only JSON query; and
6. stream/discard every provider-process stdout/stderr channel plus the
   aggregate health channel and bounded query-response body under one 64 KiB
   cap, persisting only redacted counts, hashes, and predicates; and
7. never invoke the provider's bundled MCP/tool routes, which stay confined to
   unpublished loopback inside the no-network container.

The observer must parse standard JSON—not invent a provider text format—and
must fail closed unless the pinned source-derived request/response candidate and
one complete result object supply the correlation of relationship kind,
source/target paths, symbols, ranges, and endpoint identities required by Story
4.1. A pass returns to a later parser/integration plan; it does not select HTTP
as the production transport or authorize a production parser/persistence work.

**Scope classification:** Moderate. It changes the provider qualification
lifecycle and invalidates accepted dependent tasks, but does not change PRD
behavior, production Scan behavior, UI, or architecture boundaries.

## 4. Detailed change proposals

### Plan

**Old:** Qualification expects `cgc query` terminal output to contain the
test-defined `vulgata-cgc-structural-record-v1` pipe-separated `key=value`
records.

**New:** [plan.md](../../../specs/001-codegraph-extraction/plan.md) defines a
CodeGraphContext local HTTP gateway lifecycle and
`vulgata-cgc-http-json-v1`/`vulgata-cgc-streaming-evidence-v2` evidence
contracts. It requires private-Context isolation, no-network loopback binding,
one source-derived candidate query, one aggregate readiness-response channel,
all-provider-channel byte accounting,
strict streaming JSON validation, a non-source scalar projection, redacted
schema 1.2 evidence, and cleanup precedence.

**Reason:** The provider emits an HTTP JSON envelope but not the invented text
record. JSON permits correlation validation without table scraping or raw output
retention.

### Tasks and workflow state

**Old:** Tasks T002–T012 and the implementation state treated schema 1.1 and
the synthetic `key=value` observer as the accepted corrected path.

**New:** [tasks.md](../../../specs/001-codegraph-extraction/tasks.md) is an
explicit invalidation record. [workflow-state.json](../../../specs/001-codegraph-extraction/workflow-state.json)
returns the feature to `plan` with the required review, approval, task-author,
and analysis sequence.

**Reason:** A changed plan invalidates dependent task generation and analysis.
No replacement provider execution may use the rejected task graph.

### PRD, architecture, and UX

**PRD:** No edit proposed. The private local provider and whole-Repository
strategy remain compatible with the existing approved correction.

**Architecture and epics:** Record a follow-up documentation correction to call
the selected provider “CodeGraphContext” and its executable “`cgc`”, instead of
the ambiguous generic “CodeGraph CLI.” Do not change runtime components in this
qualification revision.

**UX:** No edit proposed.

## 5. Implementation handoff

1. Independent plan reviewer: evaluate the revised local HTTP lifecycle,
   Context-isolation proof, JSON evidence restrictions, and unchanged PRD/Scan
   strategy.
2. User: after an `ACCEPT`, explicitly approve the changed qualification
   transport under the planned-change authorization gate.
3. Task author: regenerate a clean task graph; do not reuse invalidated T002–T012.
4. Artifact analyst: run fresh cross-artifact analysis.
5. TDD implementer: only then replace the test-only observer/schema/writer and
   execute the single authorized replacement provider gate.

## 6. Approval required after plan review

The revised plan changes a provider qualification transport and parser
candidate. Following independent plan acceptance, obtain explicit approval for
the exact loopback HTTP JSON lifecycle in [plan.md](../../../specs/001-codegraph-extraction/plan.md).
That approval is limited to regenerated qualification work and one replacement
gate. It does not reopen the already approved whole-Repository `ScanRun`
strategy, does not authorize production integration, a Vulgata API endpoint,
Vulgata MCP/Worker Agent tools, direct Kuzu access, Code Unit persistence, migrations,
Git/API/UI/E2E work, a Scan Coordinator, Uncertainty persistence, or
Cross-Repository Resolution. The provider's bundled MCP/tool routes are known
to exist but remain unpublished, loopback-confined, and uninvoked.
