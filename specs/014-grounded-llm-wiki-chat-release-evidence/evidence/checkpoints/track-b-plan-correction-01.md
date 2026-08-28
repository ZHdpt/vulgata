# Story 12.5 Track B plan correction checkpoint 01

- Checkpoint ID: `bmad-12-5-plan-20260828T093259Z-cd2355`
- Baseline rollback checkpoint: `73c317f10917236e7a4ac0ad55f4966abd18024e`
- Design decision: `story-12-5-design-correction-01`
- Self-review: PASS / PLAN_READY_FOR_REVIEW
- Next stage: independent plan-only review

## Corrected architecture

- One typed retrieval path owns authorized snapshot, lexical primary projection, current-v2 relationship expansion, and sole packet assembly.
- The cutover explicitly deletes the literal question classifier, exact-row/provenance rebuild, projection-only early return, duplicate packet path, legacy Chat authority reads, and direct hidden projector construction.
- Exact deterministic ranking, top-256-after-ranking, twelve-primary target, `min(3,count)` seeds, deduplication, complete-unit packing, UTF-8 bounds, and canonical fingerprint rules replace approximate prose.
- Relationship and Flow Group support use closed `absent`, `present-valid`, `present-inconsistent`, and `selected-required` states.
- Track B is verified through B1 projection/packet, B2 relationships/currentness, B3 conversation, and B4 raw streaming/accepted-only reveal boundaries, each beginning with a named architectural interaction RED.
- Track B retains mandatory strict raw streaming and neutral content-dark runtime observations; Track C alone adds candidate/case/process/signature/export attestation.
- FR-056 through FR-060 traceability is corrected without changing the accepted specification, BMad story, Story 12.4 contract, or persistence model.

## Artifact hashes

- `plan.md`: `a74b4cef7fd2d553a26b45692b25b9b81876ca1b2b88d6d80d453a43d5966aec`
- `research.md`: `714ad2ef8adaa34eb7e42b78a248a9a8e16ba74acf94ace1e29a08959fb839a7`
- `data-model.md`: `81cd843538e72c49c606191d83485b4e4760782e982e109be58eef1e7377bbb2`
- `quickstart.md`: `4f5696057046fdec343bad9f1e27e739c44dbb1ad7a4a5c1cf26f0b24c9568ae`
- `contracts/grounded-chat-v2.md`: `1985baaa6f1314e1caf892575bd5e61e2a3d11b845ea72af294e22e62fa70325`
- `contracts/chat-provider-streaming-v1.md`: `fd62547b11477d628004a33cb68112b8c75cf942d03f4b07a14799ccb70bbba6`
- `contracts/chat-evidence-packet-v2.schema.json`: `ac3b73dccff4523bcce02b530cb717b8392581852dd3c0d8e9fe81ab62c85d22`
- `contracts/release-evidence-contract.md`: `839fed2d45df76143d2e2f82363b737339f2e6400d62b4e776740f67644b00e7`
- `contracts/story125-release-evidence.schema.json`: `138923ff5377c1b535cfc2f466bcbfdfd9fbdeadf57702e6119deda1582030ad`

Validation: all modified JSON parses; 60 FR and 17 SC mappings are present; placeholder scan and `git diff --check` pass. Full Python JSON Schema meta-validation was unavailable because `jsonschema` is not installed; independent review must treat schema coherence as an explicit check.

## Owned-path manifest

- `specs/014-grounded-llm-wiki-chat-release-evidence/plan.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/research.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/data-model.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/quickstart.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/grounded-chat-v2.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/chat-provider-streaming-v1.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/chat-evidence-packet-v2.schema.json`
- `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/release-evidence-contract.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/story125-release-evidence.schema.json`
- `specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json`
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-plan-correction-01.md`

Explicit exclusions: spec, tasks, production code, tests, sprint status, Story 12.4 artifacts, EF files, and Track C execution/evidence.
