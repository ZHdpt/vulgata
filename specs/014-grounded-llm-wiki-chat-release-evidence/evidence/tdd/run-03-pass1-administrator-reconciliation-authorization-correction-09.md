# Run 03 Pass 1 Administrator reconciliation authorization correction 09

- Story: 12.5
- Stage: implement / T036 real Pass 1 continuation
- Decision: `story-12-5-run-03-all-remaining-tasks-approval-02`
- Observed at: `2026-08-30T21:17:44.6071663Z`
- Retained pair: `8a5b8a31-bd93-8558-9cb4-efc137db4ada`
- Guarded database: `vulgata_story125_b527027ad0dc4e359070f26498817c3c`

## Observed production failure

After correction 08, the authenticated production activation still left the
terminal pair `Running`. A read-only authorization comparison proved that the
operator is a current Administrator and is intentionally not the System Owner
assigned to the System. The endpoint accepts the established Management Access
policy, but `PreparedPass1JobRunner` performed its final publication recheck by
querying only `SystemOwnerAssignments`. It therefore rejected a valid
Administrator before calling the now-correct reconciliation transaction.

## Honest RED

Added the public production-composition regression
`FullyDrainedPair_AuthenticatedAdministratorWithoutOwnerAssignment_ReconcilesNormally`.
It authenticates a real Administrator without adding a System Owner assignment,
starts Pass 1 through the production endpoint, drains the prepared jobs, and
requires the same `ReadyForResolution` publication and durable `Pass1Outcome`
as a System Owner.

Observed result before the production change:

- expected `ReadyForResolution`;
- actual `Running`;
- 1 test failed, 0 passed.

## Minimal GREEN

The runner's final reconciliation eligibility check now reuses the existing
`IPreparedScanInitiationAuthorizationRechecker`. That production authority
revalidates the Identity user and current Administrator or System Owner role,
System visibility/ownership, and exact two-Repository membership. The runner
also compares the returned user and canonical Repository set with its bound
activation before allowing reconciliation.

The former assignment-only query was removed. This does not grant new access:
Administrator was already an accepted Management Access authority at the
public endpoint, while a System Owner still requires the current role,
assignment, System, and exact Repository pair. Revocation continues to fail
closed before publication.

Synthetic capacity tests that activate the background pump directly were
updated to seed an Identity System Owner matching their already-existing domain
assignment. The conflict-retry assertion now waits for the observable second
call instead of racing the additional durable authorization read.

Verification:

- exact Administrator regression: 1 passed, 0 failed;
- exact conflict-retry regression: 1 passed, 0 failed;
- complete `Pass1ProductionExecutionCapacityTests`: 20 passed, 0 failed;
- `git diff --check`: clean (line-ending notices only).

## Boundary confirmation

- no authorization policy, role, System ownership, or Repository membership
  requirement was weakened;
- no EF migration, `VulgataDbContext`, configuration, or model snapshot changed;
- no Story 12.4 contract or accepted Track A/Track B behavior changed;
- no direct database repair, credential disclosure, source disclosure, or fake
  evidence was used;
- the next action is to restart the guarded host and replay the authenticated
  production activation for the same terminal pair.
