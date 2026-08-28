# Story 12.5 Track B Implementation Approval 02

- **Decision**: Pending user approval
- **Decision ID**: `story-12-5-implementation-approval-track-b-02`
- **Scope SHA-256**: `c6afc73bad816bad6a7a511fb2bf6557f30e81ae0c6576f3ee4783abfed75db8`
- **Requested at**: `2026-08-28T12:16:34.9825775Z`
- **Accepted definition review**: `reviews/definition-review/definition-review-06.json`

## Exact decision requested

Authorize Story 12.5 Track B production and behavioral-test implementation for
tasks T020-T053 only, using the accepted corrected definition and the sprint
loop's TDD discipline.

The recommended option is to approve this exact scope. The alternative is to
hold implementation at the accepted-definition checkpoint. Any requested scope
or architecture change returns to the appropriate definition owner and cannot
reuse this approval.

## Bound definition

- specification SHA-256:
  `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4`;
- plan SHA-256:
  `9b84e94ea2bb564b8a08b521023ab449ae7c5731e52cce057d5e63bad2c02b6a`;
- tasks SHA-256:
  `15756545cd4e4357e99a2dc5ee18d660ac8e4b6c782b5bdafcd191b821aa2f6f`;
- definition-review-06 SHA-256:
  `2bf9ada95e15875ccd7f070990cda89267cd84183f58609eb492b3faf7035e67`;
- planned-change authorization-03 scope SHA-256:
  `f3057c1106d59d96a804b6a7c8902df653b6c23e4deb7402759c0ffd71a84f16`;
- accepted Story 12.4 dependency commit:
  `4798b97d9f23aef587829175df7ab7ebc3daf995`.

## Authorized implementation intent

If approved, Track B proceeds in dependency order:

1. B1 T020-T033: architectural REDs, four typed retrieval handoffs, sole lexical
   primary and packet path, obsolete dual-path deletion, packet 2.2 candidate
   extraction, and server-owned applied advisory Context.
2. B2 T034-T039: current-v2 one-hop relationship expansion, preserving all exact
   decisions per Edge and decision-owned Published Flow Groups without replacing
   lexical primaries.
3. B3 T040-T044: bounded transient conversation, re-fetch/revalidation, and
   complete claim/citation validation.
4. B4 T045-T053: strict raw provider streaming, pre-JSON received-byte limits,
   whole-answer barrier, accepted-only Unicode-safe reveal, and neutral runtime
   observations.

Every behavior change requires an honest public-seam RED, minimal GREEN, and
focused plus surrounding regression. Shared `Program.cs`, Agents DI, project and
endpoint files retain one coordinated owner.

## Excluded and remaining gates

This decision does not authorize Track C T054-T062, Tier 2/Tier 3 release runs,
the production sealed-attestation sink, real-LLM release evidence, or screenshots.
It also does not authorize an EF migration, `VulgataDbContext` or snapshot change,
Story 12.4 contract mutation, embeddings, vector search, autonomous retrieval,
unrestricted Agent loops, or fake Backup B release evidence.

After Track B implementation, convergence, independent quality review,
conditional E2E, pinned verification, and a Track B checkpoint remain mandatory.
Track C requires a separate approval after Track B acceptance.
