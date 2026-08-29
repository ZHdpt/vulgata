# Story 12.5 Planned-Change Authorization 04

- Decision ID: `story-12-5-planned-change-authorization-04`
- User direction: approve task regeneration and implementation.
- Accepted plan review: `plan-review-10` at checkpoint commit `d5c43e1ae719f8237e2024fda738c0da57a45977`.
- Accepted specification SHA-256: `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4`.
- Accepted plan SHA-256: `fad42dcd3a3f393809e95500ea6d6c7607ce4a16787d8b16d054b415ce533318`.
- Story 12.4 dependency commit: `4798b97d9f23aef587829175df7ab7ebc3daf995`.

## Planned-change trigger and authorized scope

The accepted plan materially changes the Chat Agent/tool and provider-publication architecture. Under `AGENTS.md`, this requires explicit planned-change authorization before tasks. The user authorizes Sol/high task regeneration for only the accepted Story 12.5 boundary:

1. preserve and mutation-guard the existing single typed lexical projection -> optional relationship projection -> one packet builder topology and the server-owned advisory Context reader;
2. retain lexical projection as the sole primary evidence authority;
3. retain current-v2 relationship projection, including many accepted decisions sharing one Edge and decision-owned Published Flow Groups, as the sole optional expansion authority;
4. retain evidence-packet 2.2 and content-dark, release-neutral Track B raw-streaming telemetry;
5. replace the ambiguous B4 tail with one Web-scoped publication owner, one preallocated composite candidate, and one atomic `AcceptedPublicationCommit` that publishes answer, Conversation State, completion summary, and accepted-only reveal together;
6. retain the separate Track C schema-2.2 attestation and actual complete-render verification after B1-B4 and B-INT are accepted.

No EF migration, `VulgataDbContext`, model snapshot, Story 12.4 contract mutation, vector/embedding retrieval, autonomous retrieval loop, unrestricted Agent tool loop, fake Backup B release evidence, or unvalidated client output is authorized.

Task authoring must use `gpt-5.6-sol` with high reasoning as directed by the user. Model selection does not waive self-review, independent definition review, TDD, convergence, quality review, E2E, or verification.

## Conditional implementation authorization

The same user instruction preauthorizes implementation of the regenerated task set only after a fresh independent definition review accepts the complete specification, corrected plan set, and regenerated tasks without changing this scope. At that point the root may durably bind `story-12-5-implementation-approval-03` to the accepted artifact hashes without asking again.

This preauthorization cannot be reused if review changes the specification, plan, architecture, task scope, Story 12.4 contract, persistence boundary, or Track B/Track C ordering. No test or production write is permitted before definition acceptance and that final hash binding. Track C remains dependency-ordered behind accepted B1-B4 and B-INT even after implementation approval becomes effective.
