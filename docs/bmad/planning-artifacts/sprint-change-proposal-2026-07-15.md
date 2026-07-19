# Sprint Change Proposal — Bounded CodeGraph Qualification Evidence

**Date:** 2026-07-15
**Trigger:** Story 4.1 provider qualification stopped with `fail / unsafe-evidence /
corrective-planning`.

## 1. Issue summary

The user-restored Bookworm qualification image successfully ran as non-root
`app`, installed CodeGraphContext 0.5.1, passed `cgc doctor`, created a private
local Kuzu context, indexed the synthetic whole Repository once, executed one
static read-only query, and cleaned up. The query output exceeded the runner's
8,192-character in-memory capture before the required completion marker could
be observed. The result was intentionally not treated as structural evidence.

The cause is an evidence-capture limit in the test-only qualification harness,
not a provider-installation, Scan, PRD, or production integration failure.

## 2. Impact analysis

| Area | Impact |
| --- | --- |
| Epic 4 / Story 4.1 | Reopen only the provider qualification gate after a reviewed harness correction. |
| Epic 5 and later | Remain blocked; no Code Unit mapping, persistence, Scan Coordinator, Worker Agent, or API/UI work is unlocked. |
| PRD FR-4.1 / FR-4.9 | No change. The required structural evidence remains mandatory. |
| Scan strategy | No change. There remains one private local context, one whole-Repository index, one static query, one whole-process timeout, and no provider batching/retry. |
| Architecture / UX | No change. The correction is test-only and does not alter a runtime component or user flow. |
| Docker / provider pin | No change. Preserve the verified Bookworm image, CodeGraphContext 0.5.1, and hash-verified lock. |

## 3. Options evaluated

### Option A — bounded streaming evidence observer (recommended)

Keep the existing single index and single static query. Replace the
8,192-character *retained-output* limit with a test-only streaming observer:

- enforce a hard total UTF-8 output limit of 64 KiB across stdout and stderr;
- retain no raw provider output after the run;
- stream the output through a SHA-256 accumulator and a bounded,
  qualification-only grammar observer;
- retain only the total byte count, command/output hashes, completion/cleanup
  markers, and normalized predicate facts needed by the gate;
- fail `unsafe-evidence` when output exceeds 64 KiB, contains unsafe data, lacks
  a required marker, or cannot prove the required grammar/predicates.

The observer is not a production parser and must not create a
`CodeGraphParseResult`, Code Unit, Edge, or provider schema contract. It exists
only to decide whether a future reviewed plan may describe an observed grammar.

**Effort:** Low. **Risk:** Low–medium. The hard bound, redaction rule, and
atomic failure behavior remain stronger than the failed 8 KiB buffer.

### Option B — compact static Cypher projection

Keep the 8 KiB retained-output limit but replace the query with a compact
projection that returns one deterministic representative for each relationship
kind and only scalar endpoint/range fields.

**Effort:** Medium. **Risk:** Medium–high. The provider's exact scalar-property
names and aggregation grammar are not yet qualified, so this option risks
inventing a query contract before observing it.

### Option C — accept mechanics-only qualification and defer grammar

Treat non-root command success as sufficient and defer relationship/line-range
grammar to later production work.

**Rejected.** This weakens the Story 4.1 and PRD structural-evidence outcome,
would require a PRD correction, and would let production mapping begin without
the required provider contract.

### Option D — replace CodeGraphContext

Adopt another provider or a custom tree-sitter extractor.

**Rejected for now.** This is a major technology and plan change; it would need
new research, a revised architecture/PRD assessment, and a new provider gate.

## 4. Recommended approach

Adopt **Option A**. It is a direct correction to the qualification harness, not
a Scan-strategy or PRD change. It preserves the user-approved whole-Repository
behavior and prevents raw or unbounded output from being retained.

### Proposed edits

**Plan — Provider Qualification Gate, exact pass criteria 3–4**

Replace the implicit assumption that a small retained buffer is complete with:

> The one static query is observed by a test-only streaming evidence observer.
> It enforces a 64 KiB total UTF-8 output cap, retains no raw provider output
> after the run, and records only SHA-256 identities, byte count, completion and
> cleanup markers, and normalized qualification predicates. Missing or unsafe
> evidence remains an atomic failure. This observer is not a production parser
> and does not define a CodeGraph provider schema.

**Tasks — T007 through T009**

1. T007: replace the 8,192-character retained-output implementation with the
   streaming observer and explicit 64 KiB total cap.
2. T008: require the result writer to accept only hash-safe observer facts and
   reject raw output, missing markers, unsafe text, or output over the cap.
3. T009: add red tests for a marker after 8 KiB, output over 64 KiB, unsafe text,
   missing relationship kinds, missing filtered endpoint, and invalid ranges;
   then run one fresh qualification execution with the unchanged image and
   provider inputs.

No PRD, Epic, architecture, UX, Dockerfile, lock, provider version, agent tool,
or Scan strategy edit is proposed.

## 5. Implementation handoff

**Scope:** Minor, qualification-only; it nevertheless requires a fresh plan
review because the accepted plan explicitly treats qualification failure as a
hard stop.

1. Plan author: amend the qualification evidence contract and task descriptions.
2. Independent plan reviewer: verify that the amendment neither changes Scan
   behavior nor weakens FR-4.1/FR-4.9 evidence requirements.
3. Developer: after approval, implement the red/green test-only harness changes
   and run one fresh synthetic qualification.
4. If that run passes: return to a new reviewed parser plan before any
   production work. If it fails: preserve the safe result and stop again.

## 6. Approval requested

Approve this proposal to amend the Story 4.1 qualification plan and reopen only
the test-only qualification gate. Approval does not authorize provider mapping,
structural persistence, migrations, Git/API/UI/E2E work, Scan Coordinator,
Worker Agents, MCP, LLM, or any production implementation.

## 7. Approval record

**Approved by the user on 2026-07-15:** Option A — bounded streaming evidence
observer. The approval authorizes the plan/task correction and a fresh
qualification attempt only. It preserves one private local context, one
whole-Repository index, one static query, the existing 1,800-second
whole-process limit, and the hard stop before production work.
