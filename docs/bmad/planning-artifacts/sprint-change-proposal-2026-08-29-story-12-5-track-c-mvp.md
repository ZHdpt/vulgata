# Sprint Change Proposal: Story 12.5 Track C-MVP

**Date:** 2026-08-29

**Story:** 12.5 — Grounded LLM-Wiki Chat & August 30 Release Evidence

**Mode:** Expedited batch

**Classification:** Moderate, deadline-driven MVP correction

**Authorization:** `story-12-5-track-c-mvp-overnight-standing-authorization-01`

## 1. Issue Summary

Story 12.5 Track B is accepted at commit
`f292caec427f9844be3c0f65b4cb00a9292080a5`. Track C had accumulated a
second release-proof product around the accepted product: an ECDSA attestation
sink, sealed raw-adapter admission, repeated byte-domain recomputation, a fixed
ten-check schema, immutable supersession graphs, fully hashed cross-links, a
second controlled-provider orchestration run, automated screenshot binding,
and a release-summary feature. Those mechanisms add substantial deadline risk
without increasing the judge-visible value of the real two-Repository Scan,
generated LLM-Wiki, grounded Chat, restart, or screenshots.

The trigger is therefore a deadline-driven MVP review, not a product rollback.
Accepted Track A and Track B source and tests remain authoritative.

## 2. Impact Analysis

### Epic and story impact

- Epic 12 remains achievable. Story 12.5 keeps its user-visible and security
  outcomes but replaces proof automation with one auditable host run.
- Story 12.4 remains an immutable accepted dependency at
  `4798b97d9f23aef587829175df7ab7ebc3daf995`.
- No other story is reordered or made obsolete.

### Artifact impact

- The PRD August 30 overlay gains this approved release-proof simplification.
- Story 12.5 acceptance criteria retain real Scan, real LLM, grounding,
  authorization, streaming, restart, and screenshots while dropping the
  separate attestation/envelope product.
- The August 30 architecture collapses Tier 2 duplication and Tier 3 ceremony
  into one combined host integration and release run.
- Story 12.5 specification, plan, release contract, schema, and tasks return to
  their earliest invalid owners and are regenerated.

### Technical impact

- No EF migration or `VulgataDbContext`/snapshot change is allowed.
- No Story 12.4 contract changes or new Scan behavior are allowed.
- Track B streaming caps, UTF-8/SSE handling, cancellation, authorization,
  currentness, citation validation, Source Anchors, publication ownership, and
  conversation reset remain unchanged.
- A clean lowercase `vulgata_story125_*` PostgreSQL database remains mandatory.

## 3. Recommended Approach

Use an **MVP Review**. Execute one combined host integration and real release
run, record a lightweight preflight and command log, query the database for the
minimum outputs, restart Vulgata once against the same database, manually score
twelve representative questions, and capture/redact five real screenshots.

Effort is medium and schedule risk is materially lower than completing the
discarded proof system. Product/security risk is low because no accepted
runtime invariant is weakened. Evidence becomes simpler and more human-auditable
instead of cryptographically self-verifying.

Rollback is not viable because accepted Track A/B behavior is valuable and
unrelated to the proof-automation problem. A direct adjustment without MVP
scope correction is not viable because it retains the deadline-heavy duplicate
harness and attestation machinery.

## 4. Detailed Change Proposals

### PRD August 30 overlay

**Old:** Tier 2 and Tier 3 imply separate controlled-provider orchestration and
exhaustive same-candidate evidence machinery.

**New:** One combined host integration and real release run may satisfy the
August 30 release boundary. It records candidate/revisions/tool/provider/database
identity and commands, runs the real product, queries required outputs, restarts
once, scores twelve questions, and captures redacted screenshots. Cryptographic
attestation and exhaustive release-envelope automation are post-competition.

### Epic 12 Story 12.5

**Old:** Tiered release evidence with broad recorded schema/prompt/tool/fingerprint
machinery.

**New:** One clean database and one combined real run proving the mandatory Scan,
LLM-Wiki, grounded streaming Chat, minimum outputs, same-database restart,
manual question table, and redacted screenshots.

### August 30 architecture

**Old:** Separate Tier 2 controlled outage/recovery qualification and Tier 3
real-provider evidence, with ceremony suitable for a release-proof subsystem.

**New:** Keep Tier 1 regression; use one combined host integration/release run.
Live outage injection, duplicate orchestration, cryptographic attestation,
counter recomputation, rigid ten-check schema, supersession graph, and automated
screenshot validation move to post-competition backlog.

### Story 12.5 feature artifacts

**Old:** Track C implements an attestation sink and exhaustive schema 2.2
semantic machinery.

**New:** Track C-MVP is evidence execution and minimal harness support only:
database guard/preflight, one real run, straightforward queries, one restart,
manual corpus table, redacted screenshots, and a Markdown report. Existing
Track B runtime observations may be quoted as supporting evidence but are not
recomputed or signed by Track C.

## 5. Explicit Post-Competition Backlog

1. ECDSA P-256 attestation sink and key/fingerprint lifecycle.
2. Sealed raw-adapter admission and forgery qualification.
3. Track C recomputation of transport byte domains and final-answer counters.
4. Fixed ten-check release-envelope enforcement and schema 2.2 semantic vectors.
5. Immutable run supersession and command/observation/artifact closure graphs.
6. Hashing and cross-linking every command, receipt, observation, and screenshot.
7. Separate automated Tier 2 orchestration and live outage injection.
8. Automated candidate-bound screenshot validation.
9. Dedicated release-summary product feature.

## 6. Handoff and Success Criteria

Route the correction through the schema-v3 sprint loop: specification correction,
high-reasoning architecture plan, independent plan review, standing planned-change
decision, regenerated tasks, independent definition review, standing implementation
decision, TDD where executable behavior changes, convergence, quality review,
required E2E, and pinned verification.

Success requires the retained release values in the user authorization, honest
blocked evidence for unavailable external prerequisites, no secret exposure, and
no claim of completion without the technical gates.

## 7. Checklist Disposition

- [x] Trigger, problem, and evidence identified.
- [x] Epic 12 remains viable with Story 12.5 correction only.
- [x] PRD, architecture, story, feature contracts, and task impacts identified.
- [x] UX remains unchanged except manual capture of already-required real views.
- [x] MVP Review selected; rollback rejected; direct adjustment insufficient.
- [x] Post-competition backlog preserved explicitly.
- [x] Moderate Product Owner/Developer/Architect handoff defined.
- [x] User approval recorded by the standing authorization in the initiating prompt.

## 8. Approval Record

The exact bounded proposal is auto-accepted under
`story-12-5-track-c-mvp-overnight-standing-authorization-01` at the decision
timestamp recorded in the Story 12.5 decision artifact. This approval does not
waive independent review, TDD, convergence, quality, E2E, final verification,
database isolation, or secrets protection.
