# T032 quality-review input: Prepared Scan initiation

This packet is verdict-neutral. The independent reviewer must inspect raw authorities, source, tests and evidence and produce its own `ACCEPT`, `REVISE`, or `BLOCKED` verdict.

## Pinned boundary

- Story: `12.5-grounded-llm-wiki-chat-and-august-30-release-evidence`
- Feature: `specs/014-grounded-llm-wiki-chat-release-evidence`
- Base: `9d626259c0d64173fb5269be6690d6fbe455dd58`
- Implementation candidate: `25b3d13ae46601c407327487471c749ca6ce85e9`
- Workflow binding: `b8d2c084ee04acd92423142e161b6984195899ff`
- Accepted scope: `f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1`
- Spec SHA-256: `469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0`
- Plan SHA-256: `6ebb0a525721716bfcbb3dce0d330b4846a6f3cf50c604ceb2bf9b0ed41346a8`
- Tasks SHA-256: `6117b0b8eeea9e139868e60274a4b3ba1b25d38312555a06fce225f73cc9968e`
- Convergence attempt-02 report SHA-256: `063b2fbbd5acd447173b3fda948f3708f3402de09817e5eb57796afa7b88b053`

## Owned implementation and test seams

- Core: `PreparedPass1ContractIdentity` and `IPreparedPass1ContractIdentitySource`.
- Agents: `EmbeddedPreparedPass1ContractIdentitySource` only.
- Infrastructure: `PreparedScanInitiationStore`, `PreparedScanStore`, and the prepared-v2 create-or-upgrade branch in `PreparedScanRepository`.
- Web/composition: `PreparedScanInitiationAuthorizationRechecker`, `PreparedScanInitiationCoordinator`, and the single scoped DI registration in `Program.cs`.
- Tests: `PreparedPass1ContractIdentitySourceTests`, `PreparedScanInitiationCoordinatorTests`, and public PostgreSQL `PreparedScanInitiationApiTests` fixture/assertion corrections.
- Evidence/task state: T025 RED, T029/T030 verification and T025-T030 task completion only.

The exact review diff is `git diff 9d626259c0d64173fb5269be6690d6fbe455dd58..25b3d13ae46601c407327487471c749ca6ce85e9`.

## Required review questions

1. Does the server-owned v2 prompt/schema identity fail closed before authorization, durable creation and external analysis, while leaving the v1 recovery source unchanged?
2. Does the rechecker preserve missing-equivalent authorization and ownership behavior without Web EF/DbContext access?
3. Are exact replay, mismatch rejection, concurrency, PostgreSQL retry strategy, ambiguous commit acknowledgement and microsecond timestamp normalization safe and deterministic?
4. Does publication correctly create a prepared-v2 `PreScanResult` for the no-preseed path and preserve the legacy compatibility-upgrade path without partial/cross-paired visibility?
5. Are external Git/CodeGraph/JDT-LS/XML operations outside database transactions, with final authorization/currentness retained before publication?
6. Does default composition use current CodeGraph 1.5/JDT-LS/safe XML and avoid CGC, the historical profiling coordinator, old-v1 satisfaction, later Worker/Pass 1/Pass 2/Document/Edge/Uncertainty/Chat/LLM work, and all prohibited scope?
7. Are public RED/GREEN tests and surrounding Track A/B regressions sufficient for correctness, security, architecture quality and regression risk?

## Evidence matrix

- T025 honest RED: `1 passed / 3 expected RED`; final identity/coordinator GREEN `18/18`.
- Corrected public replay test `1/1`; ready-runtime/transient retry `2/2`.
- Combined identity/coordinator/runtime/public API/PostgreSQL/atomic/store after QR-PSI-001 correction: `111 passed, 1 environment-dependent skip, 0 failed`.
- Track A Scan status/resume and release contracts: `116/116`.
- Track B retrieval/currentness/packet/citation/conversation/strict streaming/publication/UI/integration: `423/423` after the two default Data Protection cases were rerun with normal host access.
- Deterministic Prepared Scan/Story 12.2 applicable suites: `70/70`.
- Web build: `0 errors`; existing package/version warnings only.
- Official workflow validator: valid. `git diff --check`: clean.
- Independent convergence attempt 02: `converged`, zero findings, no appended tasks.

## Quality attempt-01 correction

The immutable attempt-01 verdict is
`reviews/quality-review/prepared-scan-initiation-quality-review-01.json`
(`REVISE`, finding `QR-PSI-001`). The correction adds executable coordinator
vectors for null, v1, malformed-hash, wrong-schema and throwing authorities,
proving zero runtime, fresh-authorization, store and preparation calls. It adds
executable embedded-resource vectors for missing, duplicate, empty, invalid
UTF-8 and schema-inconsistent content plus exact decoded-text SHA-256 binding.
The matrix passed `29/29`; no public contract, prompt or Scan behavior changed.
Attempt 02 must judge the corrected candidate independently and write a new raw
verdict without modifying attempt 01.

## Environment and exclusion facts

- Disposable PostgreSQL verification used fresh lowercase `vulgata_story125_<32 hex>` databases; no retained Track C database or secret was contacted or persisted.
- The local JDT-LS distribution archive is absent, so real-runtime qualification is reserved for the separately unauthorized run-03 lane.
- One unchanged Story 12.2 test still searches `Program.cs` for a CodeGraph package literal whose current authority is in `PreparedScanRuntimeIdentityResolver`; current resolver tests pass. Judge whether this is genuinely in the accepted T025-T030 boundary rather than assuming either disposition.
- T034-T045 and all run-03 external work remain closed.
- No EF migration, DbContext/model snapshot, Story 12.4 contract, Agent tool, general Scan strategy, CGC registration, legacy coordinator restoration, single/arbitrary-N entrance, or Track A/B product behavior is authorized.

The reviewer alone owns `reviews/quality-review/prepared-scan-initiation-quality-review-01.json`; the root owns workflow state, staging and commits.
