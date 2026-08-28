# Story 12.5 Track B B1+B2 implementation checkpoint 01

- Checkpoint ID: `bmad-12-5-implement-20260828T140229Z-b1b2`
- Starting commit: `47a335fe08e7b8c82681a4a75622cf5b6b94f0de`
- Approved boundary: Track B implementation approval 02, tasks T020-T039
- Dependency: Story 12.4 completion commit `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Planned-change authority: `story-12-5-planned-change-authorization-03`

## Delivered boundary

- One typed retrieval path preserves deterministic lexical primaries and calls the packet builder exactly once.
- The literal Cross-Repository classifier, projection-only early return, duplicate packet path, legacy exact-evidence authority, and retriever-owned exact/provenance rebuild are absent.
- Current-v2 REFERENCES and GENERATED_FROM expansion supports multiple accepted decisions per Edge, canonical selected decision, complete decision count/set fingerprint, and decision-owned Published Flow Groups.
- Retrieval and render-time currentness recompute the same canonical decision-set authority and fail closed on selected-required mutation.
- Packet 2.2 binds titles, headings, descriptions, exact Edge/decision/set authority, Flow Groups, steps, advisory Context, and bounded conversation state.
- Complete-unit packing removes unreachable optional relationship support as a unit and never truncates required evidence.
- Applied Global/System/exact-two-Repository Context is read server-side after authorization, is bounded to 8,192 UTF-8 bytes, and is composed through scoped ports.
- Production DI injects `IChatRelationshipProjector` and `IChatAdvisoryContextReader`; no caller advisory-text boundary was added.

## TDD evidence

- Structural RED observed while two packet paths/concrete projection remained; GREEN now proves one builder, no obsolete classifier/rows/early return, and no concrete projector name in the retriever.
- Shared-Edge RED observed when two current exact decisions and their Published Flow Groups produced no packet; GREEN preserves one Edge, count 2, set fingerprint, and both groups.
- Decision-set mutation RED observed as `Current`; GREEN returns `MissingOrStale` after recomputation.
- Seed-order RED observed because GUID ordering replaced lexical rank; GREEN preserves first occurrence before `min(3,count)`.
- Packet complete-unit RED observed when dropping an optional section left its relationship/Flow Group/step/Document/anchor; GREEN removes the whole unreachable unit.
- Two stale legacy assertions were corrected to the accepted plan: more than 20 eligible sections and a sixth lower-ranked optional Document are bounded/excluded, not whole-packet failures.

## Verification

- Pinned B1+B2 regression: **248 passed, 0 failed, 0 skipped**.
- Retrieval/currentness focused suite: **89 passed, 0 failed**.
- Composition/coordinator/UI suite: **63 passed, 0 failed**.
- Packet builder: **22 passed, 0 failed**.
- Candidate extractor/currentness surroundings: **60 passed, 0 failed**.
- Web/test project builds: **0 errors**.
- `git diff --check`: clean; Git reported informational LF-to-CRLF conversion notices only.

Known build warnings are pre-existing package-source reachability (`NU1507`/`NU1900`) and Microsoft.EntityFrameworkCore version-conflict (`MSB3277`) warnings. They are not product test failures. No EF migration, `VulgataDbContext`, model snapshot, Story 12.4 contract, project file, or central Agents DI file changed.

Track B3 T040-T044 is the next implementation boundary. This checkpoint does not activate Track C; the accepted conditional Track C approval remains gated on the completed and accepted full Track B checkpoint.
