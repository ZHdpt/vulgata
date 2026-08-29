# Decision: Story 12.5 Track C-MVP planned-change authorization 01

- Authorization ID: `story-12-5-track-c-mvp-overnight-standing-authorization-01`
- Decision ID: `story-12-5-planned-change-authorization-track-c-mvp-01`
- Gate: `planned-change-authorization`
- Scope SHA-256: `2bec1009dbefde3cc8d9452b1579456cb57a9b61f78a063e8ca42d419708ae22`
- Recommended option: `accept the reviewed AD-CMVP-1 through AD-CMVP-8 plan and proceed to task regeneration`
- Decision timestamp: `2026-08-29T23:13:00.8104505+08:00`
- Authorization expiry: `2026-08-30T09:00:00+08:00`
- Status: `accepted`

## Decision packet

The independent plan-only review accepted the complete corrected plan on
attempt 2 after 75/75 focused compatibility tests. The trigger is the approved
PRD August 30 proof-scope reduction and the corresponding architecture change
from a separate attestation/envelope subsystem to evidence execution over the
existing product. The plan adds no Agent tool, changes no Scan strategy, weakens
no security behavior, and plans no production write.

The viable options were to accept the corrected lightweight plan, restore the
deadline-heavy proof subsystem, or stop Story 12.5. The recommended first option
is inside the initiating user boundary, retains judge-visible real release
value, and preserves Track A/B and Story 12.4 unchanged. It was auto-accepted
under the explicit standing authorization.

This decision authorizes task regeneration only. Independent definition review,
the implementation approval gate, honest TDD for any executable delta,
convergence, quality review, E2E, final verification, external-prerequisite
safety, and clean checkpointing remain mandatory.
