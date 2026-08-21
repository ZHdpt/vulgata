# Story 12.2 Convergence 09

## Result

**Outcome: converged**

The pinned implementation satisfies the accepted Story 12.2 specification,
plan, tasks, Constitution, and the QR3-001 correction boundary. No task was
appended. `tasks.md` is byte-for-byte unchanged.

## Pinned boundary and artifacts

- Base commit: `29e889badc00db671fcabf80f50918e15381052c`
- Candidate commit: `f518ba23ff4b7cab5f39478f19389be039eb8c8c`
- Candidate diff SHA-256 (`git diff --binary base candidate`):
  `751036e35e005ca49465dfc692c7f1994f9b13c02e02fb4f1935dbc31fb2dcf0`
- Accepted specification SHA-256:
  `c15d03b2b10e4eaa452a59e3b96acafdbb04160de556ca93c28380117a085ad3`
- Accepted plan SHA-256:
  `2a9373661e935918ab6b55a4d83903e4ba92ba7e9fb80534a616d6583cb4b318`
- Canonical-envelope contract SHA-256:
  `d334327a4039ad9e6d9f8859cac0de8cfb8d44dd1b679e721bbaaa0089faa7c9`
- Implementation-evidence SHA-256 at assessment:
  `d7dd28255b346558e4897f6a3278a76f899c615506e26132fe2db1218a81e453`
- `tasks.md` SHA-256 before/after:
  `aab3144ff425d56fa2e4509afde51e6b0c120e5c953d1cb218e87c56c6cecb12`
  / `aab3144ff425d56fa2e4509afde51e6b0c120e5c953d1cb218e87c56c6cecb12`
- Existing task range: `T001`-`T060`, all checked (`[x]`); appended task IDs:
  none.

The candidate is the requested source boundary. The only pre-existing working
tree change observed during this assessment was the orchestrator-owned
`workflow-state.json`; it is outside this convergence write boundary and was
not changed.

## Authority coverage

The raw authorities were read directly: `spec.md`, `plan.md`, `tasks.md`,
`contracts/canonical-envelope.md`, `implementation-evidence.md`,
`docs/bmad/implementation-artifacts/12-2-lightweight-java-xml-pre-scan-and-prepared-scan-manifest.md`,
`docs/bmad/epics.md`, `docs/UBIQUITOUS_LANGUAGE.md`, `AGENTS.md`, and
`.specify/memory/constitution.md`. The raw QR3-001 review was also assessed:
`reviews/quality-review/quality-review-03.json`.

| Inventory | Assessment | Traceable evidence |
|---|---|---|
| FR-001--FR-009 | covered | exact revision acquisition, contained enumeration, closed filtering/audits, raw-byte SHA-256, immutable snapshot revalidation in the completed US1 tasks and implementation evidence |
| FR-010--FR-015 | covered | revision-bound CodeGraph 1.5.0 init/rebuild/status/completeness proof and Java 21/JDT-LS 1.61.0 readiness, including XML-only NotApplicable and hard-failure behavior in T058/T059 evidence |
| FR-016--FR-024 | covered | bounded safe XML projection, closed catalog grammar, prohibited-feature rejection, classification provenance/locator/fingerprint, 256-observation contract, and neutral production-literal qualification |
| FR-025--FR-033 | covered | exact Java/XML split ranges, all-or-nothing oversized-file handling, File Code Unit bounds, direct/expanded scoring, seed/relationship/time bounds, stable repository/pair order, and dual ordinal identity rules |
| FR-034--FR-041 | covered | schema-versioned manifest/context/result/handoff, canonical envelopes, complete durable proof/audit preimages, pair-wide atomic commit, exact replay/conflict, and zero LLM/dispatch authority |
| FR-042--FR-043 | covered | host-runtime pin qualification and boundary/security qualification; no CodeGraphContext, Worker, `navigate_code`, Document, Edge/Uncertainty, Cross-Repository Resolution, Pass 2, Chat, or Backup B behavior |
| SC-001--SC-009 | covered | three-run/reversed-input release qualification, mutation matrix, timing evidence, 50--150 selection/dual ordinals, neutral fixture and XML/security coverage, classification totals, PostgreSQL atomic/replay evidence, and boundary inspection |
| US1/AC-1 | covered | exact scope, filters, hashes, and stale/partial rejection |
| US2/AC-2 | covered | real host CodeGraph/JDT-LS readiness and bounded failure before approval/LLM |
| US3/AC-3 | covered | safe XML and closed communication evidence without final Cross-Repository Edges |
| US4/AC-4 | covered | bounded File Code Units, deterministic business order, and atomic prepared handoff |
| AD-1--AD-9 | covered | plan decisions are represented by the completed production ownership and qualification tasks; no decision is contradicted |
| Constitution I--V | covered | Core/Infrastructure/Shared/Web boundaries, domain purity, FluentValidation/API discipline, integration-first tests, and security/data stewardship remain satisfied |

No finding was classified as `missing`, `partial`, `contradicts`, or
`unrequested`; counts are `0/0/0/0`. No intent or authority defect remains.

## QR3-001 closure

QR3-001 was a HIGH partial on FR-023/FR-034/FR-038 and the canonical ordering
contract: a detector-produced over-bound result appended `BoundExceeded`
without reordering, so store validation rejected a legitimate package. The
pinned correction closes it in the production path:

- `PreparedCommunicationDetector.Detect` retains 255 canonical observations,
  adds one deterministic `BoundExceeded` Warning, and re-applies
  `PreparedScanIdentityBuilders.OrderCommunicationClassifications` to the
  final 256-item set.
- `PreScanResult` has a prepared-only 256 cap while the legacy v1 path remains
  capped at 100; no historical contract or migration is changed.
- `CommunicationKindClassification.CreatePrepared` validates and retains the
  existing `CatalogRuleIdentity`, so persisted and reconstructed classification
  fingerprints have the same canonical preimage.
- The new public detector regression proves exactly 256 retained rows,
  truthful complete totals, one `BoundExceeded` warning, and normative order.
- The PostgreSQL regression proves the detector-produced package initial commit,
  fresh-context exact replay, and externally reordered-package conflict before
  mutation. Checked-in implementation evidence records the isolated PostgreSQL
  17.10 result as 2/2 for the two QR3 tests, with the surrounding suites also
  green.

Therefore all requested closure questions are **closed**: final bounded
ordering, prepared cap 256 versus legacy 100, catalog identity durability,
initial commit, restart `Replay`, and reorder `Conflict`.

## Focused verification

- `dotnet test ... --filter
  "FullyQualifiedName~PreparedCommunicationDetector_OverBoundWarningsRetainExactly256InNormativeOrder|FullyQualifiedName~PostgreSql17_OverBoundDetectorPackageCommitsReplaysAndRejectsExternalReorderAsync"`
  independently passed the detector test (1/1). The PostgreSQL test could not
  be rerun in this shell because `VULGATA_TEST_POSTGRES_ADMIN` was not set; the
  raw implementation evidence from the authorized isolated PostgreSQL 17.10
  run records both QR3 tests passing (2/2), plus the full classification 5/5,
  atomic PostgreSQL 31/31, focused QR1/QR2/T060 9/9, and SQLite replay 5/5.
- Core, Infrastructure, and Web builds are recorded as zero-error in the raw
  implementation evidence; only existing warnings remain.
- `git diff --check` for the pinned base/candidate boundary passed (line-ending
  notices only).

## Report integrity

- Report path: `specs/011-java-xml-prescan-manifest/reviews/convergence/convergence-09.md`
- Report SHA-256 is computed after this write and must be recorded by the
  root orchestrator in workflow evidence.
- No application code, specification, plan, task, workflow state, stage,
  commit, or Git history was edited by this convergence run.
