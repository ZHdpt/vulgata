# T031 Convergence attempt 02: Prepared Scan initiation and Pass 1 identity authority

- Story: `12-5-grounded-llm-wiki-chat-and-august-30-release-evidence`
- Feature: `specs/014-grounded-llm-wiki-chat-release-evidence`
- Attempt: `2` (fresh independent convergence after `QR-PSI-001`)
- Outcome: `converged`
- Implementation candidate: `25b3d13ae46601c407327487471c749ca6ce85e9`
- Binding HEAD observed: `b8d2c084ee04acd92423142e161b6984195899ff`
- Implementation boundary: `9d626259c0d64173fb5269be6690d6fbe455dd58..25b3d13ae46601c407327487471c749ca6ce85e9`
- Story 12.4 accepted dependency: `4798b97d9f23aef587829175df7ab7ebc3daf995` (ancestral to binding HEAD)
- Accepted implementation scope: `f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1`

## Inputs and boundary

This read-only assessment used `AGENTS.md`, `.specify/memory/constitution.md`,
Story 12.5 in `docs/bmad/epics.md`, the accepted Track C-MVP and Prepared Scan
initiation proposals, `spec.md`, the complete `plan.md`, `tasks.md`, the
Prepared Scan initiation contract, accepted plan/definition/implementation
decisions, the prior convergence report, the raw quality-review verdict,
`QR-PSI-001` RED/GREEN evidence, T029/T030 verification, and the complete
current source/test boundary.

No source, test, definition artifact, workflow state, review, database, or Git
history was modified by this convergence pass. `tasks.md` was not appended or
otherwise changed.

## QR-PSI-001 disposition

The prior quality finding was that hostile Prepared Pass 1 identity ordering was
claimed without executable proof. The corrected T025/T029 boundary now proves,
at public seams, that null, legacy-v1, malformed-hash, wrong-schema, and
throwing identity authorities return `Unavailable` before runtime resolution,
fresh authorization, initiation-store access, or pair preparation. The
Agents-owned source seam independently rejects missing, duplicate, empty,
invalid-UTF8, and schema-inconsistent embedded resources and binds the exact
decoded UTF-8 text hash.

The focused hostile/ordering run passed `29/29`. The corrected verification
records the surrounding initiation filter as `111 passed, 1 explicitly
environment-dependent skip, 0 failed` and the Track A/B regression as `116/116`
and `423/423` respectively. The finding is therefore closed by executable
evidence rather than by reinterpretation of the earlier report.

## Intent assessment

| Requirement set | Finding classification | Evidence |
| --- | --- | --- |
| FR-063--FR-067 | none | `PreparedScanInitiationCoordinator`, `PreparedScanInitiationStore`, and T029 PostgreSQL/public evidence cover exact System/two-Repository/revision identity, authorization ordering, all-or-nothing tuple creation/replay, advisory serialization, and transaction closure before external analysis. |
| FR-068--FR-073 | none | Existing `PreparedScanCoordinator`/`PreparedScanPairBuilder` receive the immutable tuple; prepared-v2 result creation, Manifest publication, exact replay, mismatch rejection, phase recovery, legacy rejection, and retry are covered by T029 evidence. |
| FR-074--FR-076 | none | Default composition binds the Agents-owned v2 identity, accepted CodeGraph 1.5/JDT-LS ports and materialized Core authorization seams. Hostile identity ordering, no-CGC/legacy profiling, no-later-stage effects, and the no-preseed vertical scenarios are executable and recorded. |
| FR-077--FR-078 | none | T030 preserves the retained blocked database and keeps run-03 and general/single/N-Repository/legacy-consolidation work outside this candidate. |
| SC-019--SC-025 and US6/AC1--AC9 | none | T029/T030 evidence covers complete publication, deterministic replay/concurrency, fail-closed mismatch and authorization, retry, composition, transaction visibility, and forbidden-path behavior. |
| Constitution I--V | none | Core remains dependency-free; Infrastructure owns EF and transactions; Web owns orchestration; input/API and authorization boundaries remain intact; prompts/secrets/source handling stay within established security constraints. |

No `missing`, `partial`, `contradicts`, or `unrequested` finding remains. No
convergence task is justified. In particular, a requirement for EF/model
changes, Story 12.4 mutation, CGC or historical profiling, general Scan
redesign, later-stage execution, fake evidence, or run-03 work would contradict
the accepted boundary and is not an actionable implementation gap.

## Independent observations and exclusions

- Branch is `demo0831/story-12-5-trackC`; the pre-write worktree and index were
  clean. The only post-observation write from this pass is this report.
- The unchanged Story 12.2 `PreparedScanBoundaryTests` package-literal
  assertion remains a source-location test defect outside this candidate. The
  current runtime resolver and production-literal qualification are the
  accepted authority and pass; this is not silently counted as a product pass.
- The local JDT-LS qualification archive remains absent in exploratory assets.
  That is an external/run-03 prerequisite, not a T025-T033 accepted-boundary
  gap. The extracted runtime and deterministic/configured evidence remain
  separate from any future real release claim.
- PostgreSQL evidence uses only fresh disposable lowercase
  `vulgata_story125_<32 hex>` databases. No retained Track C database or fake
  Backup B state was used.
- T034-T045 and run-03 remain closed; this convergence result makes no claim
  about real release Scan, Pass 1/2, LLM, restart, screenshot, E2E, or final
  Story 12.5 completion.

## Checks and pinned artifact hashes

Observed commands/results:

```text
.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks: PASS
dotnet test ... --filter "PreparedPass1ContractIdentitySourceTests|PreparedScanInitiationCoordinatorTests": 29 passed, 0 skipped, 0 failed
git diff --check 9d626259c0d64173fb5269be6690d6fbe455dd58..25b3d13ae46601c407327487471c749ca6ce85e9: PASS
git merge-base --is-ancestor 4798b97d9f23aef587829175df7ab7ebc3daf995 b8d2c084ee04acd92423142e161b6984195899ff: PASS
```

Pinned SHA-256 values:

```text
spec.md: 469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0
plan.md: 6ebb0a525721716bfcbb3dce0d330b4846a6f3cf50c604ceb2bf9b0ed41346a8
tasks.md: 6117b0b8eeea9e139868e60274a4b3ba1b25d38312555a06fce225f73cc9968e
contracts/prepared-scan-initiation.md: 2234243212da60fddf94287f4b4946d57d2a600be3161c4d2673b4ae624b7c6d
evidence/tdd/prepared-pass1-identity-hostile-red-01.md: a7def44e517db9bba80d303280bfea698ddf9a98007195249b31982648795a22
evidence/verification/prepared-scan-initiation-identity-authority-01.md: fb4ddb0b92cca5d1f78689a9e567194f3570a925e2644aace06eb75dba454a0b
evidence/verification/prepared-scan-initiation-regression-01.md: 0543f685c874e00852575d8e2843c5e0dd9dd3466e69a255711b816a50905896
```

## Finding summary and handoff

- Requirements/acceptance items checked: FR-063--FR-078, SC-019--SC-025,
  nine US6 acceptance scenarios, AD-PSI-1--AD-PSI-5, and Constitution I--V.
- Findings: `0 missing`, `0 partial`, `0 contradicts`, `0 unrequested`.
- Severity: `0 CRITICAL`, `0 HIGH`, `0 MEDIUM`, `0 LOW`.
- Appended task IDs: none.
- `tasks.md` remains byte-for-byte unchanged at SHA-256
  `6117b0b8eeea9e139868e60274a4b3ba1b25d38312555a06fce225f73cc9968e`.

**Converged — the implementation satisfies the accepted spec, plan, and tasks
for T025-T030, including the corrected QR-PSI-001 hostile identity proof.** The
root orchestrator may complete T031's verdict-neutral quality-review input and
dispatch T032. T034-T045/run-03 remain closed and are not implied by this
result.
