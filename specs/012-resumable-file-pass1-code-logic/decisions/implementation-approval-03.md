# Story 12.3 Claim-Authorization Compatibility Approval 03

- Decision ID: `story-12-3-implementation-approval-03`
- Status: Accepted
- Decided at: `2026-08-22T11:15:30Z`
- Scope SHA-256: `d17688241da9288d0302128cfc4f6d0a990a90cdc1b73ba9f6bd965a81e6ffe4`
- Specification SHA-256: `7dba4ddf1d808dd6327468920d8ae2e0bf8130881a95c10166858c407e2f98a1`
- Plan SHA-256: `9535a4b31c16cb927a11ef4e6c71d6b3398536e53dedda38c4dd70ece3b0226f`
- Tasks SHA-256: `872afdbdab9ad5acec75c19b94a0f9701a1647cc33a19d97d3a8385a75d66c2d`
- Accepted definition verdict SHA-256: `1439a9ab31e76814af5a9bcf14b420089e641d3c8b3bdcf09c885c02b89fb19c`

Scope construction: SHA-256 of the UTF-8, LF-joined, ordinal path-sorted
`<repository-relative-path><TAB><lowercase-file-sha256>` manifest for the
current Story 12.3 specification, complete corrected plan set, tasks, and
independent `definition-review-10` ACCEPT verdict.

## Decision requested

Authorize the narrow T053 compatibility correction already required by the
accepted runtime contract: add server-only `JobStateVersion` and
`PairStateVersion` fields to `PreparedPass1ClaimResult`, populate them from the
same `ClaimNextAsync` transaction after lease acquisition, use safe defaults
for non-claimed outcomes, and consume that exact authorization during claimed
File Code Unit hydration and post-source-I/O revalidation.

The trigger is an implementation-time ownership defect: the approved plan,
data model, and runtime contract already require both captured versions, but
the previous T053 task path list omitted the existing claim contract and store
projection files. Without this additive projection, a claimed-packet source
cannot distinguish a post-claim job/pair version change from a valid claim
without inventing version values.

Effects and implementation intent are bounded to the in-memory server-only
claim result and T053 hydration path. The correction adds no entity, row,
column, migration, state transition, retry, lease revocation, persistence
semantic, provider-visible field, API field, Document field, or log field. It
preserves Story 12.1 lease/atomic replay behavior and leaves T055 executor,
dispatcher, committer, failure, and reconciliation composition untouched.

Affected definition material: `tasks.md` T053, the T052/T053 ownership-map row,
and correction note T053-SCOPE-001. Normative authority remains `plan.md`
claimed-packet design, `data-model.md` Runtime-only claim authorization, and
`contracts/pass1-runtime-contracts.md` Claims/packet-source clauses. The PRD
clauses remain FR-4.3, FR-4.4, FR-4.9, FR-4.12 and NFR-2.3; no PRD requirement
is changed or weakened.

This authorization only refreshes the implementation hash after the accepted
task correction. It does not waive T052/T053 RED/GREEN proof, remaining T054-
T064 work, convergence, fresh quality review, E2E, security,
migration-no-delta, PostgreSQL/Windows-host qualification, pinned final
verification, checkpoint, or root-owned commit gates.

## Decision

The user explicitly approved this exact refreshed implementation scope on
2026-08-22. No excluded scope or downstream gate was waived.
