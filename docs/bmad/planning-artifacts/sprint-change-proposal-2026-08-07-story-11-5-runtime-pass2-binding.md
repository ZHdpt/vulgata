# Sprint Change Proposal: Story 11.5 Runtime Pass 2 Binding

**Date:** 2026-08-07
**Trigger:** The Story 11.5 release harness proved that the production Pass 2
composition cannot execute the accepted `mbs_account` -> `mbs_channel` path
using product-created identities.
**Mode:** Incremental
**Approval:** Approved by the user on 2026-08-07 after incremental review of
all four edits and the consolidated proposal.
**Scope classification:** Moderate direct adjustment contained within Stories
11.3 and 11.5.
**Execution level:** `expedited-mvp` under the August 15 delivery exception.

## 1. Issue Summary

Story 11.5 implementation is complete and focused GREEN through T050. During
T051-T054 release-harness work, the harness reached a production predecessor
gap: Pass 2 is registered against one embedded `demo-business-flow.v1.json`
singleton whose fixed System, Repository, Scan, Worker job, Code Unit, Logical
Document, and Document Version identities describe an unrelated order/inventory
example.

The final release path must create the demonstration System and the
`mbs_account` and `mbs_channel` Repositories through product services. Those
entities generate their own database identities. The exact resolution service
correctly persists the accepted directed relationship, but it does not produce
a runtime `ResolvedPreparedPass2Flow` bound to those product-created identities.
The existing Pass 2 resolver, runner, and committer require equality with the
embedded fixed flow, so the required product-service-only path cannot proceed.

Working around the mismatch with fixture SQL, direct `DbSet` insertion,
reflection, fabricated identifiers, or a second test-only flow would violate
the accepted release contract and conceal a production gap. The correct repair
is a bounded runtime prepared-flow planner that binds the already accepted
exact profile and persisted evidence to actual current product identities.

## 2. Evidence and Impact Analysis

### Concrete evidence

- `Program.cs` loads `PreparedPass2FlowCatalog` and registers its fixed
  `Flow` as the production singleton.
- `demo-business-flow.v1.json` contains fixed order/inventory identities and
  locators, not the accepted `mbs_account`/`mbs_channel` path.
- `System` and `Repository` constructors generate identities; no product API
  creates the fixed manifest identities.
- exact Cross-Repository Resolution returns `ExactResolutionOutcome`, while
  `PreparedPass2FlowResolver.Resolve` requires the caller to already provide
  all fixed flow identities.
- `SequentialScanRunner` and `WorkerAgentResultCommitter` validate against the
  fixed singleton, preventing a product-created demo flow from reaching Pass 2.
- The accepted exact-resolution profile already supplies stable semantic
  selectors, pinned refs and commits, direction, occurrence, consumer
  invocation, and provider declaration/implementation evidence. These are the
  appropriate runtime-binding authority.
- The fast release-harness contract, ownership, cleanup, environment guard,
  provider self-test, and evidence tests are GREEN 7/7, but the executable
  PostgreSQL/product/Playwright path remains intentionally unrun and T051-T054
  remain unchecked.

### Correct-course checklist results

| Item | Result |
| --- | --- |
| Trigger and evidence | Done — the product-service-only release path cannot construct the fixed Pass 2 identities. |
| Current epic impact | Epic 11 remains viable; Story 11.3 acceptance and Story 11.5 planning need correction. |
| Future epic impact | No scope or sequence change; Epic 5 retains ownership of general tracing and clustering. |
| PRD impact | None — FR-4.5, FR-6.2, and the August 15 MVP boundary remain unchanged. |
| Architecture impact | Feature-level clarification only; it aligns with the July graph-first prepared-flow architecture. |
| UX impact | None. |
| Data impact | No EF model, migration, snapshot, schema, or backfill change. |
| Direct adjustment | Viable — medium effort and medium risk. |
| Rollback | Rejected — it would discard valid chat work and still leave the required release proof impossible. |
| MVP reduction | Rejected — Pass 2 and the two-Repository cited answer are explicit deadline acceptance. |
| Handoff | Root orchestrator owns artifact integration and commits; bounded TDD implementation follows independent review and authorization. |

## 3. Recommended Approach

Use a direct, test-first runtime-binding correction:

1. After exact Cross-Repository Resolution, invoke a bounded Infrastructure
   `PreparedDemoPass2FlowPlanner`.
2. Perform one authorized relational read over current Pass 1 Documents,
   Document Versions, Sections, Source Anchors, and the accepted exact directed
   `REFERENCES` Edge.
3. Reuse the existing exact `mbs_account`/`mbs_channel` profile selectors,
   direction, evidence, refs, and full commits. Do not introduce another
   configuration or general matching algorithm.
4. Require exactly one current match for each required semantic element. Fail
   closed on missing, duplicate, stale, unauthorized, wrong-profile,
   wrong-direction, wrong-ref, or wrong-commit evidence.
5. Produce a server-owned `ResolvedPreparedPass2Flow` using the actual
   product-created System, Repository, Scan execution, Worker job, Code Unit,
   Logical Document, Document Version, Source Anchor, and relationship
   identities.
6. Anchor the Business Logic Document to the consumer Repository while fixing
   its authorized support to exactly the two prepared Repositories and every
   Code Logic Document Version actually used.
7. Make the runner and committer validate the runtime flow and deterministic
   support fingerprint instead of the embedded fixed singleton.
8. Remove the fixed order/inventory manifest as production authority. It may
   remain only where explicitly useful as a non-production test fixture.

This is a deadline-only prepared planner. It does not implement general flow
clustering, Scan scheduling, concurrency, retry, failover, or autonomous scope
selection.

## 4. Approved Artifact Edits

### Edit 1 — Story 11.3 acceptance

Replace the second Story 11.3 acceptance block with:

> **Given** Pass 1 committed current Code Logic Document Versions for both
> product-created demo Repositories and exact resolution committed the accepted
> directed relationship
> **When** the bounded demo Pass 2 planner prepares the single flow
> **Then** it shall bind the actual System, Repository, Scan execution, Worker
> job, Code Unit, Logical Document, Document Version, Source Anchor, and exact
> relationship identities into a server-owned `ResolvedPreparedPass2Flow`
> **And** the flow shall be anchored to the consumer Repository while its fixed
> authorized support contains exactly the two prepared Repositories
> **And** no embedded demo GUID, fixture SQL, `DbSet`, reflection, or general
> clustering identity may be used
> **And** the sequential runner shall dispatch exactly one schema-validated
> Pass 2 job and persist one Business Logic Document with `GENERATED_FROM`
> provenance to every Code Logic Document Version actually used
> **And** bounded refinement, cross-verification waves, partial-failure
> continuation, and general flow clustering shall remain deferred.

### Edit 2 — Feature architecture and research

Record the runtime-binding decision in `plan.md` and `research.md`:

- Infrastructure owns the bounded relational planner after exact resolution.
- Existing exact profile selectors and persisted evidence are authoritative.
- Runtime identities come exclusively from current product-created records.
- Every required selector must resolve uniquely and pass authorization,
  currentness, profile, direction, ref, and commit checks.
- Consumer Repository ownership and exactly-two-Repository support are fixed.
- The fixed order/inventory manifest is not production authority.
- Runner and committer validate the runtime flow/support fingerprint.
- No EF model or migration changes occur.
- Epic 5 retains general tracing and clustering ownership.

### Edit 3 — SpecKit requirements and tasks

Add:

- **FR-023:** After the accepted exact Cross-Repository relationship is
  committed, the final path MUST bind the current authorized Pass 1 evidence
  and actual product-created identities into one server-owned runtime prepared
  Pass 2 flow. It MUST fail closed rather than use embedded or fabricated
  database identities.
- **SC-010:** The prepared-flow tests and final run resolve exactly one
  consumer-owned flow supported by exactly the two accepted Repositories; all
  missing, duplicate, stale, unauthorized, wrong-profile, wrong-direction,
  wrong-ref, and wrong-commit variants produce zero Pass 2 dispatches and zero
  generated Documents.

Insert a production TDD quartet after T050:

- **T051:** Write RED tests for runtime planner, runner, and committer behavior
  using product-created identities and all fail-closed variants.
- **T052:** Capture the observed RED against the absent planner/fixed singleton.
- **T053:** Implement the Core contract adjustments, Infrastructure planner,
  runner/committer validation, and production registration without migration.
- **T054:** Run focused GREEN, predecessor regressions, production composition,
  and pending-model verification.

Renumber only unfinished work:

- existing release-harness T051-T054 become T055-T058;
- existing final-verification T055-T061 become T059-T065;
- completed T001-T050 and their checkboxes remain unchanged.

Update the release sequence, traceability table, dependencies, filters, and
quickstart so runtime prepared-flow binding occurs after exact relationship
creation and before Pass 2 dispatch.

### Edit 4 — Delivery and workflow

- Treat this as a Moderate direct adjustment within Stories 11.3 and 11.5.
- Keep Story 11.5 `in-progress`; create no new epic or story.
- Leave PRD, global architecture, UX, database model, and migrations unchanged.
- Correct artifacts first, obtain independent plan acceptance, then request the
  planned-change authorization required for a material Scan preparation and
  dispatch change.
- After authorization, regenerate/review tasks as required, implement T051-T054
  with transient RED-GREEN TDD, and resume T055-T065 release verification.

## 5. Scope and Guardrails

### Included

- Story 11.3 acceptance clarification in `docs/bmad/epics.md`.
- Story 11.5 `spec.md`, `plan.md`, `research.md`, `tasks.md`, `quickstart.md`,
  and `contracts/release-verification-contract.md` corrections.
- Bounded runtime prepared-flow contracts and Infrastructure implementation.
- Minimal runner, committer, and production-composition changes needed to
  consume the runtime flow.
- Focused tests, predecessor regression, security/static checks, pending-model
  check, and the existing final release gates.

### Excluded

- PRD, global architecture, UX, and unrelated epic/story changes.
- EF entity/configuration/model/migration/snapshot changes.
- Fixture SQL, direct `DbSet` provisioning, reflection, fabricated identifiers,
  test-only production bypasses, or a second exact-resolution profile.
- General flow discovery/clustering, Scan scheduling/concurrency/retry,
  partial-failure continuation, or other deferred Epic 5-10 behavior.
- Weakening authorization, currentness, provenance, citation, cleanup, Docker,
  Identity, Playwright, or two-Repository final-run requirements.
- Staging or committing by implementation/review agents; root remains the sole
  commit authority.

## 6. Handoff and Sequence

| Step | Owner | Outcome |
| --- | --- | --- |
| Proposal approval | User | Consolidated correction is explicitly approved or returned for edits. |
| Artifact correction | Root/spec-plan workflow | Accepted Story and SpecKit artifacts incorporate all four edits. |
| Independent plan review | Fresh plan reviewer | Corrected plan is accepted for feasibility, architecture, compliance, security, and testability. |
| Planned-change authorization | User | Explicit approval is recorded for the bounded Scan preparation/dispatch change; no later gate is waived. |
| Corrected task review | Task workflow/reviewer | Dependency-ordered T051-T065 work agrees with the accepted plan. |
| Runtime binding TDD | Bounded implementer | T051-T054 reach observed RED then focused and surrounding GREEN. |
| Release harness completion | Release-gate implementer | T055-T058 provide the executable sole-owner final path. |
| Final verification | Root/orchestrator | T059-T065 run sequentially against one unchanged candidate with cleanup. |
| Combined quality review | Independent reviewer | The implementation boundary is accepted before the reviewed-candidate checkpoint. |

The integration owner remains the sole authority for EF migration and model
snapshot changes. This proposal requires none; any model delta stops execution.

## 7. Success Criteria

- Corrected Story and SpecKit artifacts pass consistency and independent plan
  review.
- Planned-change authorization is explicitly recorded before task generation
  or production implementation proceeds.
- Runtime planner tests demonstrate actual product-created identity binding and
  every specified fail-closed variant.
- Exactly one consumer-owned runtime prepared flow uses exactly the two accepted
  Repositories and every actual Code Logic Document Version required by the
  accepted exact evidence.
- Runner and committer accept the valid runtime flow and reject identity,
  support, profile, direction, ref, commit, authorization, and currentness
  mismatches before Pass 2 persistence.
- No fixture SQL, `DbSet`, reflection, embedded production database identity,
  general clustering, EF model, migration, or snapshot change is introduced.
- All Story 11.5 focused, predecessor, security, migration, Release build,
  regression, Docker, health, Identity, Playwright, citation, revocation, and
  cleanup gates pass against one unchanged candidate.
- No unresolved RED is committed and known limitations remain truthful.

## 8. Approval and Remaining Gate

The user explicitly approved this complete Sprint Change Proposal on
2026-08-07. That approval authorizes artifact correction and independent plan
review only. Because the proposal materially changes bounded Scan preparation
and dispatch behavior, production task generation and implementation remain
paused until the subsequent planned-change authorization required by
`AGENTS.md` is separately granted.
