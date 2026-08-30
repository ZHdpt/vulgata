# Story 12.5 exact-profile seed-selection planned-change authorization 03

- Decision ID: `story-12-5-planned-change-authorization-exact-profile-seed-selection-03`
- Gate: AGENTS.md Scan planned-change authorization
- Decision: **accepted**
- Decided at: `2026-08-30T21:56:35.4521804Z`
- Scope SHA-256: `c8aea284ced8fdc564bfc505c3eea73bd96f7aec5a56008f3df7a24e73f0eecc`
- Source checkpoint: `7fc5bd1b8b787d5ff205378abb9e6a464bf00eb5`
- User authorization: `story-12-5-run-03-all-remaining-tasks-approval-02`
- Independent plan review: **ACCEPT**, `plan-review-exact-profile-seeds-01.json`

## Trigger and effects

The accepted plan materially changes a Scan selection strategy and resolves an
architecture conflict: the general planner previously allowed no reviewed
profile to choose seeds, while the fixed exact-resolution release profile
cannot be proven unless all four required source roles survive the existing
selection cap.

The approved approach adds one server-owned read-only
`IReviewedExactProfileSeedAuthority` and sole Infrastructure adapter over the
unchanged embedded `ExactResolutionProfileCatalog`. It activates only for a
complete one-to-one match of exactly two persisted Repository stable
identities, safe full Git refs, and Profiled Revisions. It derives the required
consumer, provider-declaration, provider-implementation, and
unsupported-observation-owner paths and marks every bounded segment with the
closed `ReviewedExactResolutionProfile` evidence value and score 1,100. The
existing 20-per-Repository, 150-pair, deterministic ordering, audit,
fingerprint, Manifest, and Pass 1 `>=400` mandatory-seed behavior remain in
force.

A nonmatching pair receives byte-for-byte neutral generic behavior. A fully
matched but absent, filtered, unsafe, ambiguous, split-invalid, duplicate, or
over-bound seed set rejects before CodeGraph expansion or Manifest publication.
Any required Pass 1 failure uses the existing `FailedPass1` boundary and blocks
exact resolution and Pass 2.

## Implementation intent

Generate dependency-ordered test-first tasks for only the Core closed contract
and evidence value, the Infrastructure catalog adapter, the Web pair-builder
composition and shared `Program.cs` registration, public/composition and
PostgreSQL no-preseed vertical tests, and the fresh guarded release run. Begin
with honest RED at the public authority/planner/builder seams, make the minimal
GREEN, then run convergence, independent quality review, conditional E2E, and
pinned final verification.

The implementation must not add a migration or model change, modify Story 12.4
contracts, alter the exact profile/catalog/resource/parser, accept caller paths
or profiles, hardcode sample values in production, change generic nonmatching
selection, raise bounds, restore CGC or the historical profiling path, change
Pass 1 policy, or reuse or repair run-03 as passed evidence.

## Bound artifacts

- Sprint Change Proposal — `c5b31c6591f4b1400e18133e0b9b58926bedc1ec1a3df960f3243038ab7e9233`
- PRD August overlay — `e14783b0e8b80f7226f6f4875ddf02135d06523e83e3c82d0d9202e3424b571b`
- Epic 12 / Story 12.5 — `b5b90c4d8d82d0a69c7dd74df93312674902f170725bbe0ac71bfc4672db1706`
- August 30 architecture — `6fa9bed2bd90e0abad5333b05381638bcf471088480b00ec94229c96f173878c`
- Specification — `6da3e251b217ab8dd8190f84c4cf864b994be1e87090558a29e125f5648ec8b7`
- Plan — `35766983a07db621efbdef3fa45934b2f66d86514d15deb490b5eca83ca75c43`
- Independent plan review — `31c848cfdc4e5522b57efb4c9673b13f16569d49d2f38891e810cf4cca2fb49a`

This recommended in-scope option was auto-accepted under the user's explicit
instruction to execute all remaining Story 12.5 work without further routine
approval pauses. The authorization advances the accepted plan to task
generation; it does not replace task/definition review, implementation TDD,
convergence, quality, E2E, security, database isolation, or final verification.
