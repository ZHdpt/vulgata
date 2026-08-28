# Story 12.5 Track B plan correction checkpoint 03

- Checkpoint ID: `bmad-12-5-plan-20260828T101349Z-725ec9`
- Source review checkpoint: `7e390bf97e9564d542f6ef939349c2204a4043d5`
- Raw verdict: `reviews/plan-review/plan-review-08.json`
- Self-review: PASS / PLAN_READY_FOR_REVIEW

`S125-PR8-001` is corrected without changing the accepted specification. The stream design now enforces these independent byte domains:

- physical raw read: 16,384 implementation cap;
- raw response body: 1,048,576 safety cap;
- SSE line/event: 131,072 safety caps;
- received logical Provider Chunk: 16,384 normative FR-054 pre-JSON cap;
- assembled received provider response: 65,536 normative FR-054 pre-JSON cap;
- decoded answer delta: 16,384 separate defense-in-depth cap;
- final decoded answer: 65,536 separate defense-in-depth cap.

One nonterminal SSE `data` event is one received logical Provider Chunk. Its count includes JSON syntax, escapes, metadata, finish/error/answer content and excludes SSE field framing, terminators, parser-inserted multiline LF, and `[DONE]`. Received caps run before JSON decoding. Exact/+1, escape-heavy, metadata-heavy, precedence, whole-buffer discard, zero-client-output, content-dark observation, and Track C recomputation/tamper vectors are explicit.

Changed plan artifacts: `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/grounded-chat-v2.md`, `contracts/chat-provider-streaming-v1.md`, `contracts/release-evidence-contract.md`, and `contracts/story125-release-evidence.schema.json`. No spec, tasks, production, tests, Story 12.4, EF, or Track C execution change is included.

Validation: modified JSON parses; 60 FR/17 SC traceability remains complete; stale decoded-domain FR-054 wording, ambiguous overflow categories, placeholders, and unresolved markers are absent; `git diff --check` passes.
