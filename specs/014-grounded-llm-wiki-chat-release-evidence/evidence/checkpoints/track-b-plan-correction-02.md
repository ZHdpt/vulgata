# Story 12.5 Track B plan correction checkpoint 02

- Checkpoint ID: `bmad-12-5-plan-20260828T095901Z-43a712`
- Source review checkpoint: `bf48acba67bc6634bb3b929aad14eb16a5ed9341`
- Raw verdict: `reviews/plan-review/plan-review-07.json`
- Self-review: PASS / PLAN_READY_FOR_REVIEW

## Review corrections

- `S125-PR7-001`: one `REFERENCES` Edge may carry many valid Story 12.4 decisions. Packet v2.2 binds one deterministic canonical selected decision plus the complete ordered decision count/set fingerprint; Flow Groups retain decision ownership. Currentness revalidates every contributing decision. B2 requires two decisions sharing one Edge with distinct Published Flow Groups.
- `S125-PR7-002`: `IChatAdvisoryContextReader(AuthorizedChatScope)` owns applied `GlobalContext`, `System.Context`, and both selected `Repository.Context` values. Pending/caller context is excluded. Authorization, currentness, NFC/tokenization, fallback scoring, empty behavior, exact 8,192/+1-byte handling, and fingerprint binding are explicit.
- `S125-PR7-003`: `ChatSynthesisAttempt` now binds a separate `RuntimeObservationId`, coordinator `CompletionSummaryId`, and `ValidationOutcome`; transport observations contain no validation field. Track C attestation projects over both immutable sources.

No spec, BMad, Story 12.4, EF, production, test, task, or Track C execution change is included.

## Artifact hashes

- `plan.md`: `b98b5c127db31d445f707ee545febc534f4f6cf97440eb72c187ec65ee9be059`
- `research.md`: `0128b812bc2843bfed8f2885628fc998cdebdd689a410ae27816bd9185d86b00`
- `data-model.md`: `478ad6262131a5a4e5ebcc868fa2783a072727bb6a597ce156588982a3cab124`
- `quickstart.md`: `030827b4838a9dfae5bcedd982c73937c1bb600e17e3a34a2b5cb40eb319aa38`
- `contracts/grounded-chat-v2.md`: `d0ef9254731c1a1e6b4aeb6a073535a2d7aed942bad01ec2a5b60a7f717db165`
- `contracts/chat-provider-streaming-v1.md`: `97843c7cb280fb6beff4a38ac8aab6618b3e7d6055facf6de16530f70f4013c3`
- `contracts/chat-evidence-packet-v2.schema.json`: `7798979a670278bb6267f57712811516c5fd3013ba1f4b402b52ef0bc37f1773`
- `contracts/release-evidence-contract.md`: `d257fa3a2ad4733060c588deb72572f994d9979b82bee67888e19e850b028090`
- `contracts/story125-release-evidence.schema.json`: `dc7825be937a0d4bbcd5baf6d7f28928a45225c9c285024f6a1a3e334065a59f`

Validation: modified JSON parses; 60 FR/17 SC traceability remains complete; stale packet 2.1 identity, one-decision assumptions, telemetry conflation, placeholders, and unresolved markers are absent; `git diff --check` passes.

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
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-plan-correction-02.md`
