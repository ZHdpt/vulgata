# Retained CL references and connected rooted Business flows

User direction (2026-10-13): implement evidence-backed edges and connected business-flow
grouping from retained CL, preserve existing publications, test and independently
review, then pause before another paid scan. No workflow skills, rescan, provider
call, retained-database mutation, schema migration or consumer implementation change
is part of this development step. Assigned branch is oct14/business at fc15147c.

## Bounded supported meaning

The first opt-in profile is `october-retained-evidence-v2`. It recognizes explicit,
source-qualified nested-flow reference mechanisms (flowEmbedOp / IFlowTool.execute)
and exact nonexpression logical path literals. Candidate targets must be an
unambiguous retained .flw artifact in the same Repository and root binding. A name,
role alias, contextual hint or common identifier cannot create an edge. Opposing,
advisory, ambiguous, unsupported-mechanism and missing-target cases remain explicit
Unresolved/NotEvaluated outcomes, never negative proof of a relationship.

A token ending in `.flw` must match the exact relative source path. An
extensionless logical slash path maps only to
`src/main/resources/flow/{logicalPath}.flw`. No basename/suffix search or expression
evaluation is allowed. Multiple retained Documents or conflicting exact origins
at that path remain ambiguous.

A derived target endpoint represents the exact retained flow artifact and its
accepted source claims. It does not invent a provider registration or callable
entrypoint declaration. Supported REFERENCES edges mean a static declared resource
reference, with an owner-added limitation that runtime mapping, registration and
successful invocation remain unqualified. Do not claim cross-repository dispatch:
retained SOFA caller declarations lack qualified matching provider/binding evidence
under this bounded profile. They remain visible gaps; no aliasing by service name.

Candidate endpoints are a deterministic in-memory projection, not edits to CL or
its stored observations. Model witnesses must pin the exact inquiry, source and
target versions, the independently reproduced binding token, and exact accepted
SourceSupported caller, target-artifact and connecting assertions. Advisory context
cannot supply proof. Every claimed assertion remains checked at owner acceptance;
structural grounding alone is not a guarantee of semantic truth.

## Compatibility and implementation boundary

Null/absent ResolutionPolicyVersion means exact v1 behavior. Preserve legacy bytes,
digests, group IDs, replay and publication reads; reject unknown/conflicting values.
Nullable omitted fields bind the opt-in profile through existing-CL begin/snapshot,
resolution corpus/acceptance, flow input and finalization selection. Existing schema
already stores PolicyVersion and fingerprint-derived identities; no migration.
Filter selected-policy rows AND their preflight budgets/dependencies/edges/unknowns.
Independent publication reload must use the same profile. Append new outcomes;
never reinterpret or remove existing outcomes/publication membership.

Use one bounded inquiry per candidate pair with explicit omissions under existing
finite limits (32 targets per observation and 64 inquiries per corpus). Resolution
LLM requests receive compact exact endpoint evidence, not
the full retained corpus, while owner authorization and dependency ledgers retain
all contributors. Preserve reservation/usage, fencing, cancellation and settlement.

Use the existing directed traversal from meaningful selected entry roots to build
multi-document groups, including cycles/limits/frontiers. Do not merge disconnected
roots, infer order from graph reachability, or make every CL document a business
entry. Pin v2 profile into new group identity, including an empty-edge group; retain
exact old identities for v1. Synthesis receives connected member evidence and
relationship qualifications. Overlapping entry-point views may remain distinct.

Owned files: Core resolution policy/contracts and flow planner; Agents resolution
packet/executor/prompt; Infrastructure resolution acceptance, selected flow-input
loading, existing-CL snapshots and flow publication; Generator Business settings/
composition/continuation only; affected Business tests and feature evidence. Core
has one author, Infrastructure/schema has root ownership, Generator a separate
owner, tests a separate owner. No shared Web or other lane implementation edits.

## Verification and stop

Independent RED/GREEN: legacy serialization/digest identity; exact endpoint evidence;
advisory/name-only/ambiguous/reversed/opposed rejection; bounded packets with full
owner authority; multi-document/cycle/frontier grouping; mixed v1/v2 persistence and
preflight, old replay/read preservation; exact Generator profile propagation.
Run through eng/test.ps1, then focused independent review. Inspect actual retained
CL through read-only owner APIs for candidate readiness, separating this from
controlled witnesses and real provider/storage qualification. Do not manufacture
or seed accepted retained knowledge. No paid scan or retained write before a later
execution decision. Reader-scaling work and broad automatic root discovery are
separate; this step does not claim full corpus or cross-repository completeness.

## Continuation handoff

The opt-in Business setting is `resolutionPolicyVersion` with value
`october-retained-evidence-v2`. Use it only with the implemented existing-CL
continuation (`outputStage=cl-and-bl` plus the exact original
`existingClPublicationId`); the original CL is neither regenerated nor replaced.
Absent/null retains the previous policy and bytes. Unknown values or disagreement
between admission, snapshot, corpus, loader and finalization fail closed. A fresh
run identity and normal launch/settlement authority remain required; do not reuse
a consumed marker, alter an old receipt or resume a completed run with changed
settings. This implementation does not change any retained runtime configuration.

Before a later paid attempt, include the newly eligible resolution attempts in the
request/deadline budget as well as synthesis. New-policy resolution reserves the
exact sent system/user input estimate plus its output ceiling, without treating
the combined reservation as the output limit. The existing two-attempt/120-second
resolution bounds remain. Resolution packets are capped at 1 MiB with no silent
truncation; full authorization and dependency records remain owner-held.

No new migration, provider tool, import/publication route or automatic root
discovery is introduced. The separate large-publication reader-cap issue is not
fixed here. A later run must qualify useful connected content and its ordinary
Business reading; candidate discovery alone is not accepted relationship proof.

## Verification evidence (2026-10-13)

The independent reviewer accepted the scoped plan and final source boundary.
Review found and closed three concrete defects: multi-origin Documents hiding a
competing target, omission of the 32-target observation limit, and output-only
reservation for new-policy resolution. Their regressions failed before each
correction; no acceptance rule or legacy fixture was weakened to obtain GREEN.

Executed through `pwsh -NoProfile -File eng/test.ps1`:

| Boundary | Result | Evidence under TestResults |
| --- | --- | --- |
| Initial v2 behavior RED | 17 selected, 1 passed, 16 failed on unimplemented behavior | `isolation-665c6953c2474b9cbaa38d4125a78741/results.trx` |
| Initial v2 GREEN | 26/26, no skips | `isolation-4bab751a62514385b1d66c4e654d7103/results.trx` |
| Ambiguity/limit review RED | 28 selected, 26 passed, 2 expected failures | `isolation-888f9edcbec8415cbb660e7a1f90140f/results.trx` |
| Affected Knowledge/Business/continuation suite | 830/830, no skips | `isolation-ad5b249cdba04061a52139545e97fdcc/results.trx` |
| Combined reservation RED | 3 selected, 1 passed, 2 expected failures | `isolation-69683719edde4e58906760eca633fce0/results.trx` |
| Final resolution/execution/finalization regressions after reservation fix | 180/180, no skips, including all 31 v2 cases | `isolation-493c75b06c8348738a667cb249da05d5/results.trx` |

Controlled coverage includes exact witness acceptance and rejection, directed
diamond/cycle/frontier grouping, complete contributor authority behind small
packets, selected-policy preflight and replay, mixed v1/v2 SQLite persistence,
actual multi-document BL publication and canonical Business reading, rejection of
a mismatched publication profile, and unchanged old publication bytes. Final
provider-free executor verification passed after the reservation correction;
independent review accepted the corrected code with no remaining blocker.

Offline candidate inspection used the previously authenticated owner export of
the exact 163-CL corpus `0ea50f38-2441-4ec0-b31c-313acd0c5f55` from publication
`fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`. After the ambiguity/limit corrections it
produced 11 candidate pairs and 344 explicit observation dispositions; largest
serialized endpoint packet was 68,711 UTF-8 bytes. Nine pairs originate from seven
of the prior 22 roots. For example, `authMethods` references retained password,
gesture-password and IFAA artifacts, with downstream retained references available
for directed traversal. These are candidate facts, not accepted model witnesses,
runtime invocation proof or a new publication. Export and preview remain ignored
local evidence under `TestResults/business-override-readiness/`.

PostgreSQL execution of the new policy, paid/provider usefulness, and ordinary-user
reading of a newly generated retained publication remain unexecuted. No migration,
retained data mutation, CL regeneration, paid request, host restart, merge or push
was performed for this change. The next action is a separately confirmed execution
boundary; the present task stops after reviewed implementation and tests.
