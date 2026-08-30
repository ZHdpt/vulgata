# T031 Convergence attempt 03: Prepared Scan initiation and Pass 1 identity authority

- Story: 12.5-grounded-llm-wiki-chat-and-august-30-release-evidence
- Feature: specs/014-grounded-llm-wiki-chat-release-evidence
- Attempt: 3 (fresh independent convergence after QR-PSI-002)
- Outcome: converged
- Implementation candidate: 9413558a42b39f8f2a9ce41a717a0b37c9f72a98
- Binding HEAD observed: 9413558a42b39f8f2a9ce41a717a0b37c9f72a98
- Implementation boundary: 9d626259c0d64173fb5269be6690d6fbe455dd58..9413558a42b39f8f2a9ce41a717a0b37c9f72a98
- Story 12.4 accepted dependency: 4798b97d9f23aef587829175df7ab7ebc3daf995 (ancestral)
- Accepted implementation scope: f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1

## Inputs and read-only boundary

This assessment used AGENTS.md, the Constitution, Story 12.5 in docs/bmad/epics.md, the accepted Track C-MVP and Prepared Scan initiation authorities, spec.md, the complete plan.md and tasks.md, the Prepared Scan initiation contract, accepted plan/definition/implementation decisions, T025-T030 implementation and regression evidence, quality review attempts 01 and 02, QR-PSI-001 and QR-PSI-002 correction evidence, and the current source/test boundary.

The worktree and index were clean before this report. No source, test, definition artifact, workflow state, review, database, or Git history was modified by this convergence pass. tasks.md was not appended or otherwise changed.

## Quality-finding disposition

### QR-PSI-001 — closed

Attempt 01 correctly identified that the first implementation claimed hostile Prepared Pass 1 identity ordering without executable proof. The correction added public-seam coordinator vectors for null, legacy-v1, malformed-hash, wrong-schema, and throwing authorities, asserting zero runtime resolution, fresh authorization, initiation-store, and preparation calls. The Agents-owned embedded source now has executable missing, duplicate, empty, invalid-UTF8, and schema-inconsistent resource vectors and exact decoded-text SHA-256 binding. The corrected focused matrix passed 29/29; no new task is required.

### QR-PSI-002 — closed

Attempt 02 found that strict embedded-resource validation occurred in the scoped source constructor. A hostile resource therefore failed during endpoint dependency activation and returned an unhandled HTTP 500 before the coordinator could map it to the accepted sanitized 503 contract.

The minimal correction retains the sole Agents-owned source and strict validation, but captures resource providers in the constructor and defers resource lookup, decoding, schema consistency, and hashing to GetCurrent(). The existing coordinator catches the failure and returns Unavailable; the endpoint maps it to HTTP 503 application/problem+json with the bounded prepared-scan-initiation-unavailable code. The public hostile activation RED observed 500 with zero side effects; the corrected source/coordinator/public boundary passed 41/41. The wider initiation boundary passed 116 with one explicitly environment-dependent concurrency skip and zero failures.

This closes the finding by executable production-composition evidence. It does not reinterpret the earlier 500 as a pass and does not add a second source, fallback, endpoint, or authority.

## Requirement and intent assessment

| Requirement set | Finding classification | Evidence and disposition |
| --- | --- | --- |
| FR-063--FR-067 | none | PreparedScanInitiationCoordinator, PreparedScanInitiationStore, and T029 PostgreSQL/public evidence cover exact System/two-Repository/revision identity, authorization ordering, all-or-nothing tuple creation/replay, server-owned identity binding, and transaction closure before external analysis. |
| FR-068--FR-073 | none | Existing PreparedScanCoordinator/PreparedScanPairBuilder receive the immutable tuple; prepared-v2 result creation, Manifest publication, exact replay, mismatch rejection, phase recovery, legacy exclusion, and retry are covered by T029 evidence and regression. |
| FR-074--FR-076 | none | Default composition binds the Agents-owned v2 identity and accepted CodeGraph 1.5/JDT-LS ports. Hostile identity ordering, no-CGC/legacy profiling, no-later-stage effects, and no-preseed vertical scenarios are executable. QR-PSI-002 also proves public failure mapping during real DI activation. |
| FR-077--FR-078 | none within T025-T033 | Retained blocked/run-02 evidence and the explicit general/single/N-Repository/legacy-consolidation backlog are preserved. Fresh run-03 and final release proof remain outside this accepted boundary. |
| SC-019--SC-025 and US6/AC1--AC9 | none within T025-T033 | T029/T030 cover complete publication, deterministic replay/concurrency, fail-closed mismatch and authorization, retry, composition, transaction visibility, and forbidden-path behavior. The real run is deliberately not claimed here. |
| AD-PSI-1--AD-PSI-5 | none | The bounded existing endpoint entrance, schema-free atomic tuple, PostgreSQL serialization/replay, closed outcomes, and Agents-owned v2 identity decisions are implemented without expanding the accepted architecture. |
| Constitution I--V | none | Core remains dependency-free; Infrastructure owns EF and transactions; Web owns orchestration; API/authorization and source-binding boundaries remain intact; prompt/resource, secret, and later-stage handling remain within established security rules. |

No missing, partial, contradicts, or unrequested finding remains. No convergence task is justified. A request to add an EF/model change, mutate a Story 12.4 contract, restore CGC or historical profiling, redesign general Scan, execute later stages, reuse/fabricate release evidence, or open the closed T034-T045/run-03 lane would contradict the accepted boundary and is not an actionable implementation gap.

## Independent implementation and regression observations

- The corrected candidate is on demo0831/story-12-5-trackC, with the accepted Story 12.4 commit ancestral. The current worktree/index remained clean throughout the read-only assessment.
- The source/coordinator/public hostile-resource and ordering boundary passed 41/41; the broader corrected identity, initiation, PostgreSQL, atomic, store, and runtime-resolver boundary passed 116, with one known environment-dependent concurrency skip and zero failures.
- Track A production Scan status/resume and release contracts passed 121/121. The accepted Track B prepared-chat, currentness, packet, citation, conversation, strict raw-streaming, publication, UI, and integration boundary passed 455/455 after excluding the known diagnostic fixture that demands the explicitly prohibited legacy profiling coordinator.
- The Web build completed with zero errors and only existing package/version warnings. git diff --check is clean apart from line-ending information.
- The unchanged Story 12.2 PreparedScanBoundaryTests package-literal assertion still searches Program.cs although current authority resides in PreparedScanRuntimeIdentityResolver; resolver and production-literal qualifications pass. This pre-existing source-location test defect is outside this candidate and is not counted as either a product or environment failure.
- The local JDT-LS qualification archive remains unavailable. Real runtime qualification is reserved for the separately closed T034-T045/run-03 lane; deterministic/configured evidence does not substitute for that future real run.
- PostgreSQL evidence used only fresh disposable lowercase vulgata_story125_<32 hex> databases. The retained Track C database and fake Backup B state were not contacted or used.
- T034-T045 and run-03 remain closed. This report makes no claim about real CodeGraph/JDT-LS/LLM release execution, restart, screenshots, E2E, or final Story 12.5 completion.

## Checks and pinned artifact hashes

Observed or independently recorded checks:

    check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks: PASS
    focused source/coordinator/API boundary: 41 passed, 0 skipped, 0 failed
    corrected initiation boundary: 116 passed, 1 environment-dependent skip, 0 failed
    Story 12.4 ancestor check: PASS
    implementation-boundary git diff --check: PASS

Pinned SHA-256 values:

    spec.md: 469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0
    plan.md: 6ebb0a525721716bfcbb3dce0d330b4846a6f3cf50c604ceb2bf9b0ed41346a8
    tasks.md: cc73c8e7d43ecb03ec4a8c177c8aeabcacbb8142659029fda40b5a151bf75e22
    contracts/prepared-scan-initiation.md: 2234243212da60fddf94287f4b4946d57d2a600be3161c4d2673b4ae624b7c6d
    evidence/tdd/prepared-pass1-identity-hostile-red-01.md: a7def44e517db9bba80d303280bfea698ddf9a98007195249b31982648795a22
    evidence/tdd/prepared-pass1-identity-activation-red-01.md: 1f1c9a6b5e8af8f64853a6e70f184515e1919a4927e953857789d913f4a6f6db
    implementation candidate: 9413558a42b39f8f2a9ce41a717a0b37c9f72a98

## Finding summary and handoff

- Requirements/acceptance items checked: FR-063--FR-078, SC-019--SC-025, nine US6 acceptance scenarios, AD-PSI-1--AD-PSI-5, and Constitution I--V.
- Findings: 0 missing, 0 partial, 0 contradicts, 0 unrequested.
- Severity: 0 CRITICAL, 0 HIGH, 0 MEDIUM, 0 LOW.
- Appended task IDs: none.
- tasks.md remains byte-for-byte unchanged at SHA-256 cc73c8e7d43ecb03ec4a8c177c8aeabcacbb8142659029fda40b5a151bf75e22.

Converged — the corrected implementation satisfies the accepted spec, plan, and tasks for T025-T030, including executable closure of QR-PSI-001 and QR-PSI-002. The root orchestrator may dispatch the fresh independent quality review attempt 03 and, on ACCEPT, complete T033's final implementation checkpoint. T034-T045/run-03 remain closed and are not implied by this result.
