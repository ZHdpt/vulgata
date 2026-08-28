# Story 12.5 design correction 01

- Decision ID: `story-12-5-design-correction-01`
- User direction: return Story 12.5 from convergence to the plan phase and correct the design defects exposed by eight non-converged Track B passes
- Source audit task: `01a04783-c4ca-7b13-b94c-5842f2ae2685` (`Audit Story 12.5 design`)
- Reviewed candidate: `29b18f07071c91379ba82c0f3f582abc14efab09`
- Accepted Story 12.4 dependency: `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Decision status: accepted

## Verified reason for rollback

The audit's central diagnosis is confirmed in the pinned repository boundary:

- `ChatEvidenceRetriever` still contains two primary packet paths. A literal Cross-Repository question classifier enters an `ExactRelationshipRow` rebuild path, while other questions return from projection-first retrieval before the bounded relationship projector.
- T021, T055, T065, T070, T075, and T080 repeatedly express the same unclosed cutover postcondition. Aggregate Track B tests increased while no test held the decisive lexical-primary-plus-current-v2-relationship interaction invariant.
- The accepted plan and grounded-chat contract use non-closed selection language such as “approximately twelve” and “top two or three,” and optional versus required relationship/Flow Group support has no complete state table.
- Runtime streaming telemetry and Track C candidate/signature/release-attestation concerns are coupled in the Track B design, increasing the correction surface.
- The plan's FR-057–FR-059 trace rows do not align with their requirement ownership.

These are plan/architecture defects, not another bounded T080 implementation correction. Per the sprint-loop correction contract, plan, tasks, definition review, implementation approval, convergence, quality review, E2E, and verification are invalidated for the affected Track B boundary.

## Authorized correction boundary

The next plan revision must:

1. define one typed retrieval pipeline and an explicit legacy cutover/deletion inventory;
2. retain projection-first lexical evidence as the sole primary packet authority and make the bounded current-v2 relationship projector the only expansion path;
3. publish exact deterministic selection, ranking, seed, candidate-cap, deduplication, and packing rules;
4. define a closed absent/present-valid/present-inconsistent/selected-required relationship and Flow Group state model;
5. introduce independently verifiable Track B sub-boundaries for projection/packet, relationships/currentness, conversation, and streaming/accepted-only reveal;
6. separate neutral runtime streaming telemetry from Track C release attestation while preserving mandatory raw streaming and content-dark evidence;
7. require architectural interaction RED tests before any replacement production write;
8. correct traceability and regenerate the affected Track B tasks instead of appending another convergence task.

Existing production code, tests, Track A evidence, and Story 12.4 integration remain intact as implementation evidence. No production correction is authorized by this decision.

## Exclusions and renewed gates

This decision does not authorize changing the Story 12.4 contract, adding an EF migration, modifying `VulgataDbContext` or its snapshot, starting Track C, or splitting/editing the BMad Story 12.5 scope or accepted specification. If the plan cannot close the defects without a specification/BMad correction, the author must stop and return that conflict to the root.

Because the revised plan changes architecture and materially changes the Chat retrieval tool/dispatch boundary, it requires a fresh independent plan review and fresh planned-change authorization before tasks. Regenerated tasks require a fresh definition review and a new implementation approval before any production write. Prior Track B and conditional Track C implementation approvals cannot be reused across the changed artifact hashes.
