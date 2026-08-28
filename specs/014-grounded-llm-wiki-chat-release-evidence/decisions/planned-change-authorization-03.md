# Story 12.5 Planned-Change Authorization 03

- **Decision**: Accepted
- **Decision ID**: `story-12-5-planned-change-authorization-03`
- **Scope SHA-256**: `f3057c1106d59d96a804b6a7c8902df653b6c23e4deb7402759c0ffd71a84f16`
- **Requested at**: `2026-08-28T11:18:15.6192481Z`
- **Decided at**: `2026-08-28T11:50:51.6536446Z`
- **User decision**: `Approve planned-change authorization 03`
- **Accepted plan review**: `reviews/plan-review/plan-review-09.json`

## Decision requested

Authorize regeneration of Story 12.5 tasks from the independently accepted
corrected plan. This request does not authorize implementation.

The user accepted this exact decision and scope on 2026-08-28. Task generation
may proceed while the fresh definition-review and implementation-approval gates
remain mandatory.

## Planned-change trigger

The accepted correction materially changes the Chat Agent retrieval and provider
dispatch design. It does not change a Scan strategy and does not weaken or
conflict with the PRD.

The task-generation boundary is bound to:

- specification SHA-256
  `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4`;
- plan SHA-256
  `9b84e94ea2bb564b8a08b521023ab449ae7c5731e52cce057d5e63bad2c02b6a`;
- plan-review-09 SHA-256
  `fabe3fe5baa1f29025479746862c3fda36df4b2e81d02f7f0baa29a519ae015c`;
- reviewed commit `0baaec6a6003b8ff055410ec39c7217d51ab1deb`;
- accepted Story 12.4 dependency commit
  `4798b97d9f23aef587829175df7ab7ebc3daf995`.

## Proposed approach and effects

Regenerated tasks will organize Track B as four dependency-ordered boundaries:

1. B1 creates the typed lexical projection and sole packet-building path.
2. B2 adds current-v2 one-hop relationship expansion without replacing lexical
   primary evidence, preserving every exact decision per Edge and its
   decision-owned Published Flow Groups.
3. B3 adds bounded transient conversation state with re-fetch and revalidation.
4. B4 completes mandatory raw streaming transport, whole-answer validation and
   accepted-only client reveal.

The old question-classifier/exact-row packet-rebuild path, its early return,
legacy relationship authority and duplicate packet assembly are explicit
deletion targets. Applied advisory Context is read through a server-owned,
authorized seam and is canonical-fingerprint bound. Packet 2.2 binds the
selected exact decision plus the complete decision set and Flow Group authority.
Received Provider Chunk and assembled-response limits are enforced on pre-JSON
wire bytes, separately from raw framing and decoded-answer safety limits.
Track B records neutral runtime observations; only Track C may produce signed
release attestation.

## Implementation intent and preserved gates

After authorization, the orchestrator will regenerate tasks, run a fresh
independent definition review, and stop at a new implementation-approval gate.
If implementation is later approved, B1/B2 architectural interaction tests must
show honest RED before the obsolete path is deleted and the minimal typed cutover
is made; B3/B4 follow only after that authority boundary is green.

This request preserves Track A evidence and does not authorize production or test
writes, Track C, an EF migration, `VulgataDbContext` or model-snapshot changes,
Story 12.4 contract mutation, embeddings, vector search, autonomous retrieval,
unrestricted Agent loops, or fake Backup B release evidence.
