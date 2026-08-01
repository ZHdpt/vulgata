---
story_key: 4-3-conservative-pre-scan-gate-and-communication-type-detection
title: "Story 4.3: Conservative Pre-Scan Gate and Communication Type Detection"
Status: in-progress
Epic: 4
Story: 3
created: 2026-07-30
execution_level: expedited-mvp
depends_on:
  - 4-1-codegraph-integration-and-code-unit-extraction
  - 4-2-language-and-framework-detection
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-story-4-3-pre-scan-gate.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md
  - docs/agent-workflow/august-15-expedited-mvp-exception.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - specs/002-language-framework-detection/plan.md
---

# Story 4.3: Conservative Pre-Scan Gate and Communication Type Detection

## Story

As a **SystemOwner**,
I want Vulgata to detect communication kinds deterministically after Repository
profiling and persist a conservative automatically approved pre-scan handoff,
so that the August 15 Scan can proceed without treating weak evidence as fact
or waiting for an LLM or Human-in-the-Loop cycle.

## Scope and boundary

- Extend the completed Story 4.1/4.2 Repository profiling path with a
  deterministic post-profile pass and a durable Pre-Scan Handoff.
- Use only bounded Repository-local structural, framework, package-manifest,
  annotation, and configuration evidence. Do not call an LLM.
- The August 15 acceptance path covers prepared RPC and HTTP evidence only.
  Message queue, file/storage, page-navigation, object-flow, Service Topology,
  and general Cross-Repository Resolution remain documented post-deadline
  limitations.
- Preserve three truth levels: confirmed high-confidence communication kinds,
  conservative hints, and conservative warnings. A hint or warning must never
  be promoted to a confirmed code-derived fact.
- Persist and expose `pre_scan_approved` for the later sequential runner. Do not
  implement a Scan Coordinator, queue, Worker Agent dispatch, lease, retry,
  concurrent superstep, or provider failover.
- Do not implement pre-scan LLM questions, manual approval, the seven-day
  lifecycle, `等待回答`, the bell badge, or the full notification surface.
- Do not implement detailed provider/consumer endpoint detection. Story 11.4
  owns the prepared exact RPC/HTTP match; Epic 7 owns the general subsystem.

## Acceptance Criteria

### AC-1: Detect prepared RPC and HTTP communication kinds deterministically

**Given** Code Units, language facts, and framework facts have been persisted for
a Repository
**When** the Story 4.3 post-profile pass runs
**Then** it inspects bounded CodeGraphContext structural evidence, supported
framework facts, package manifests, annotations, and configuration
**And** it detects the prepared demonstration's RPC and HTTP communication kinds
without an LLM, package execution, network lookup, or source-code exfiltration
**And** unsupported communication families are left for their owning
post-deadline stories rather than guessed.

### AC-2: Preserve evidence, provenance, and conservative confidence

**Given** deterministic communication evidence is found
**When** the result is classified
**Then** direct supported framework annotations or matched configuration are
recorded as confirmed high-confidence kinds
**And** import-only or otherwise weak evidence is retained as a conservative hint
**And** material ambiguity is retained as a conservative warning
**And** every item carries a bounded Repository-relative evidence locator and
deterministic provenance
**And** neither hints nor warnings are represented as confirmed code-derived
facts.

### AC-3: Persist an automatically approved pre-scan result

**Given** the deterministic pass completes, including when it produces only
hints or warnings
**When** the pre-scan gate completes
**Then** one durable Repository- and ScanRun-scoped result transitions directly
to `pre_scan_approved`
**And** confirmed kinds, hints, warnings, evidence locators, provenance, and the
schema/version needed by a Pre-Scan Handoff consumer are committed atomically
**And** a retry cannot create a second approved result or duplicate facts for
the same ScanRun.

### AC-4: Publish a Pre-Scan Handoff without dispatch

**Given** an approved result exists
**When** an authorized application service or API requests the pre-scan handoff
**Then** it receives the approved Pre-Scan Handoff needed by Story 11.2
**And** the response contains no absolute host path, source content, secret, or
unbounded diagnostic payload
**And** this story does not create or dispatch a Worker Agent job.

### AC-5: Record bounded Chinese observability

**Given** the pre-scan gate approves a result
**When** the outcome is recorded
**Then** the Scan log records the Simplified-Chinese equivalent of
“Pre-scan approved with conservative warnings — proceeding to the sequential
demo runner”
**And** any exposed warning/evidence summary is bounded and authorized through
the existing Repository visibility rules.

### AC-6: Preserve atomic failure and rerun behavior

**Given** an evidence input is malformed, unsafe, oversized, or unsupported
**When** detection processes that input
**Then** the individual input is isolated as bounded conservative evidence or a
warning without inventing a confirmed fact
**And** a persistence failure leaves no partially approved pre-scan result
**And** a provider-level profiling failure remains governed by Story 4.1/4.2
atomicity and does not publish an approved handoff.

## Context resolutions

1. **The August 15 PRD boundary is the current acceptance overlay.** Although the
   full Story 4.3 wording enumerates RPC, HTTP, message queues, file/storage, and
   page navigation, PRD section 5.16 and the approved July 30 demonstration
   slice explicitly defer every family except the prepared RPC/HTTP path.
2. **Pre-scan hypotheses are not full Cross-Repository Communication
   Detection.** Story 4.3 classifies communication kinds and preserves evidence;
   it does not infer provider/consumer roles, resolve a target Repository, or
   create a REFERENCES edge.
3. **Catalog-shaped seeds do not establish catalog-management ownership.** The
   approved handoff may carry confirmed kinds suitable for later Communication
   Pattern Catalog consumption, but this story must not implement the
   SystemOwner catalog editor, versioning, prompt injection, or Epic 7
   management behavior.
4. **The accepted deadline correction supersedes the earlier question gate.**
   Blocking ambiguity becomes a warning and the result auto-approves. Story 8.7
   preserves the post-deadline LLM/Human-in-the-Loop lifecycle.
5. **Durability is required, but its minimum shape is a planning decision.** The
   current source has no pre-scan result aggregate. The plan must choose the
   smallest additive Core/Infrastructure model that preserves type-safe,
   queryable, atomic, idempotent handoff semantics and must coordinate shared EF
   files with Story 11.1.

## Existing implementation seams

- `RepositoryProfilingCoordinator` currently completes the post-CodeGraphContext
  profiling flow and builds `RepositoryProfileDraft`.
- `IScanRunRepository.PersistCompletedProfileAsync` and `ScanRunRepository`
  atomically persist Code Units, structural records, framework facts, language
  summaries, and candidate outcomes.
- `ScanRun` currently exposes only `Profiling`, `Completed`, `Failed`, and
  `Aborted`; it has no approved pre-scan result.
- `CodeUnitEdge` and `UnresolvedStructuralRelationshipCandidate` preserve
  structural relationships but do not represent communication kinds,
  confidence levels, warnings, or approval.
- `RepositoryProfilingTests` and `FrameworkDetectionTests` are the focused
  deterministic and persistence test seams.

## Implementation guardrails

- Keep Core free of EF Core and infrastructure dependencies.
- Use specific domain service contracts; do not add a generic repository or
  expose `DbSet`, `IQueryable`, or provider-specific response shapes.
- Keep detection closed, deterministic, non-executing, path-contained, and
  bounded by explicit input/evidence limits.
- Reuse already persisted structural/framework facts where sufficient. Read
  additional files only through a Repository-relative, containment-checked
  boundary.
- Normalize communication-kind and confidence values through closed domain
  types; never persist attacker-controlled text as a type discriminator.
- Preserve existing `ManagementAccess` and Repository visibility behavior.
- Any additive migration must pass clean-database apply, rollback, and pending
  model checks and must be serialized with Story 11.1 migration work.
- This story materially changes Scan parsing/classification and gate behavior.
  After independent plan review accepts, the sprint loop must obtain explicit
  planned-change authorization before task generation.

## Testing requirements

- Observe transient RED then GREEN for every included production behavior; no
  failing test may be committed.
- Cover high-confidence RPC/HTTP evidence, weak-hint non-promotion, warning
  auto-approval, unsupported/deferred families, bounded evidence, path
  containment, duplicate suppression, rerun idempotency, and atomic failure.
- Cover Pre-Scan Handoff authorization and redaction.
- If persistence changes, cover clean migration, rollback, and no pending model
  changes.
- Treat the post-profile persistence/API path as integration-triggering for the
  conditional E2E decision.

## Tasks / Subtasks

T001-T036 are complete. The accepted delivery definition, observed RED/GREEN
evidence, combined review history, required E2E, and final verification are
recorded in `specs/003-conservative-pre-scan-gate/workflow-state.json`.

## References

- Story authority: [Source: `docs/bmad/epics.md#Story-43-Conservative-Pre-Scan-Gate--Communication-Type-Detection`]
- Deadline PRD boundary: [Source: `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md#516-August-15-Demonstration-MVP-Boundary`]
- Approved deadline slice: [Source: `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md`]
- Earlier ownership correction: [Source: `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-story-4-3-pre-scan-gate.md`]
- Canonical terminology: [Source: `docs/UBIQUITOUS_LANGUAGE.md`]
- Inherited profiling design: [Source: `specs/002-language-framework-detection/plan.md`]

## Dev Agent Record

### Context reference

- Preparation and activation source commit:
  `e7597082bf6acd4e7dfa05a56d393deae787a3d4`.
- Execution level: `expedited-mvp`.
- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/4-3-conservative-pre-scan-gate-and-communication-type-detection.json`.
- SpecKit feature directory:
  `specs/003-conservative-pre-scan-gate`.
- Current workflow stage: `complete`.
- Latest workflow checkpoint ID:
  `bmad-4-3-remediation-complete-20260731T120412Z`.
- Latest workflow checkpoint SHA:
  pending final completion checkpoint binding.
- The prior completed-story boundary was invalidated by the Epic 4 architecture
  gate. The accepted July 31 correction plan, independent plan review, explicit
  Scan-strategy authorization, and correction-only tasks now define the active
  delivery boundary.

### Agent Model Used

- Root sprint-loop orchestrator
- Story context synthesizer

### Debug Log References

- None.

### Completion Notes List

- July 31 correction: the Epic 4 architecture gate found three reserved
  Story 4.3 defects. The accepted remediation makes framework detection consume
  the verified archive lease, removes public coordinator/adaptor bypasses, and
  makes composite Repository Profile plus Pre-Scan Result persistence the only
  final persistence path. No migration, PRD, dispatch, scheduling, retry,
  Worker Agent tool, or provider-failover change is authorized.
- The user explicitly approved this accepted Scan-strategy correction for task
  generation. All transient RED, combined quality review, clean-database
  two-Repository E2E, migration-neutrality, and Docker gates remain required.
- July 31 remediation implementation is GREEN: archive-only framework tests
  4/4 and their combined boundary 144/144; construction/persistence contracts
  4/4 and their combined boundary 108/108; isolated candidate enumeration 4/4;
  focused remediation coverage 179/179; and the repeatable full suite 624/624.
- Production composition exposed and then corrected an ancestor-Git defect in
  candidate enumeration. The enumerator now treats the committed snapshot as
  a materialized archive, and the clean-database two-Repository E2E passes with
  only external `ICodeGraphProvider` replaced.
- `VulgataDbContext` has no pending model changes, Pre-Scan migration tests pass
  3/3, the Web build has zero errors, and the unchanged Docker command produced
  `vulgata:story-4-3-architecture-remediation` successfully.
- Independent combined quality review 5 returned `ACCEPT` with no unresolved
  Critical/High. It records one nonblocking Medium cleanup: concrete detector
  string overloads remain for legacy reflection tests, while the public Core
  interface and production composition are lease-bound.
- Independent final E2E returned `PASS`: production composition 1/1 and the
  complete Story 4.3 E2E class 2/2, with all 13 implementation hashes unchanged.
- Reviewed candidate `f8f8253d19f0776dea1b24cefd20b9dc4de30d80`
  contains the exact independently accepted implementation, tests, review, and
  verification evidence. Story 4.3 architecture remediation is complete.
- Story context prepared and activated from the accepted August 15 scope.
- Independent plan review accepted the pinned Story 4.3 plan on iteration 3.
- User explicitly approved the Scan strategy; task generation is active.
- Task generation produced T001-T036 and read-only analysis accepted with no
  findings; the first expedited checkpoint is bound.
- T004-T006 are GREEN: the private committed-tree lease rejects mutable or
  unsafe input, remains revision-bound, performs bounded archive handling, and
  releases its repository lock even if best-effort cleanup fails.
- T007-T009 are GREEN: candidates, CodeGraphContext, framework/language
  detection, and source hashing share one verified lease root; five late
  mutation modes fail before final profile persistence and cleanup denial
  cannot mask the profiling outcome.
- T010-T012 are GREEN: immutable closed domain types enforce the non-circular
  profile-to-detector-to-result-to-composite lifecycle without persistence or
  a detector/result dependency on `RepositoryProfileDraft`.
- T013-T016 are GREEN: an embedded closed RPC/HTTP rule catalog drives bounded
  snapshot-only classification; weak evidence remains a Hint, anomalies remain
  Warning/None, false lexical tokens cannot confirm, and retained locators are
  redacted.
- T017-T019 are GREEN: authoritative flags, occurrence totals, and the
  aggregate fingerprint are derived from the full normalized evidence set
  before deterministic sampling; truncated results retain direct Rpc and Http
  samples plus a `BoundExceeded` warning within the 100-row cap.
- T020-T023 are GREEN: the complete Repository Profile and approved Pre-Scan
  Result now commit atomically with exact Profiled Revision identity;
  same-fingerprint replay is idempotent, conflicting replay fails closed, and
  detector/final-save failures expose no partial profile or classification.
- T024-T026 are GREEN: additive migration
  `20260730083207_AddConservativePreScanGate` creates only the approved result
  and classification schema, rolls back to the Story 4.2 baseline, and has no
  pending EF model changes.
- T027-T030 are GREEN: the immutable schema `1.0` Pre-Scan Handoff is composed
  into existing ManagementAccess ScanRun GET routes with pre/post visibility
  checks, exact revision validation, bounded evidence, and no dispatch or
  sensitive internal projection.
- T031-T033 are GREEN: both exact Simplified-Chinese approval outcomes persist
  on ScanRun, revoked visibility remains non-disclosing, and structured logs
  contain only closed identity/state/schema/count/truncation/rule-set fields.
- T034 is partially GREEN: the Web build exited 0 with no analyzer/code-style
  errors, both checkout-byte failures were traced to machine-level
  `core.autocrlf=true`, the framework fixture now pins `core.autocrlf=false`,
  and the complete non-final-image regression suite passed 604/604. The
  environment-bound final-image provider-contract test remains part of the
  immutable-image lane and is not claimed by that 604-test result.
- T035 is GREEN on an isolated PostgreSQL 17 database: migrations applied from
  an empty database through `20260730083207_AddConservativePreScanGate`,
  rolled back to `20260720060442_FrameworkEvidenceAuditCount`, reapplied, and
  reported no pending model changes. Current-boundary profiling, persistence,
  migration, and authorized handoff tests are also GREEN.
- T036 is GREEN: after the recorded transient `archive.ubuntu.com` HTTP 502,
  the unchanged exact command
  `docker build --progress=plain -f docker/Dockerfile -t vulgata:story-4-3 .`
  completed successfully and produced immutable image
  `sha256:63cce7a38d827de131a30e5c401d0aecede30e54992ff6bd9e2778dbbc2edc28`.
  No Dockerfile or proxy workaround was committed.
- T034-T036 now satisfy the Story 4.3 included verification boundary. The
  604-test suite excludes only the separately environment-bound immutable
  CodeGraph provider qualification lane from completed Story 4.2; no failing
  test is committed or represented as deferred Story 4.3 behavior.
- Reviewed candidate
  `64930fce2af96767c95abb848807eadd0419ad3f` is uniquely bound to checkpoint
  `bmad-4-3-review-20260730T092942Z-8516f1`.
- Combined review iteration 1 returned `REVISE`: annotation-leading string
  decoys can be promoted to Confirmed/High, and committed configuration-only
  artifacts are not inspected. Fresh focused regressions failed 2/2 before
  production correction: the decoys produced Rpc and Http while the
  configuration-only snapshot produced neither. Exact leading Java/.NET
  invocation parsing and bounded deterministic committed-configuration
  inspection now make those regressions GREEN 2/2; the full detector and
  profiling boundary is GREEN 130/130 and the Web build exits 0. The recovery
  remains subject to fresh combined review. The reviewer classified
  the Story 4.2 final-image exclusion as truthful and the observed line-ending
  handling as non-defective.
- Combined review iteration 2 confirmed both iteration-1 High findings
  corrected and accepted the current 606/606 non-final-image regression
  evidence, but returned `REVISE` for two additional High defects: the
  committed-snapshot entry-count limit is enforced only after materialization,
  and direct annotation recognition remains vulnerable to multiline lexical
  decoys and foreign-language Code Units. E2E is paused while a fresh TDD
  implementer corrects those findings. Case-distinct Linux paths, the public
  empty-detector constructor seam, and flattened-key-only YAML support remain
  explicit nonblocking Medium findings pending re-review/E2E disposition.
- The iteration-2 High findings now have observed regression REDs: the first
  excess archive destination was created before rejection, and Java/C#
  multiline plus foreign-language annotation decoys falsely confirmed Rpc/Http.
  `MaximumEntries` is now enforced before destination resolution/creation, and
  bounded Java/C# lexical masking plus language gating preserves valid
  parameterized annotations while rejecting those decoys. Root verification is
  GREEN 136/136 for detector/profiling, GREEN 612/612 for the current complete
  non-final-image suite, and the Web build exits 0. The exact uncommitted
  correction boundary is pinned from `f501103657c67325be304a3b92ca779c75c74bbe`
  by diff SHA-256
  `5d504730573a51643950915ede14bccf35c6274f5708999fc23cc3902c45c2bd`
  for the final permitted combined review iteration.
- Combined review iteration 3 independently rehashed the exact correction
  boundary, passed detector/profiling 136/136, and returned `ACCEPT` with no
  unresolved Critical/High. It retained three explicit nonblocking Medium
  limitations: case-distinct Linux paths, the public empty-detector
  compatibility seam, and flattened-key-only YAML/application.yaml coverage.
  The accepted review requires conditional E2E of the actual prepared
  Repository configuration/persistence/handoff path before completion.
- Conditional E2E rejected an incomplete 164-test coverage override and added
  one bounded integration test. Root then rebuilt the current E2E-relevant
  boundary GREEN 165/165. The new test commits all three flattened prepared
  configuration filenames in a temporary Git Repository and exercises the
  private snapshot through Rpc/Http detection, atomic persistence,
  same-fingerprint replay, exact Profiled Revision, and schema `1.0` Approved
  Pre-Scan Handoff. Existing cited tests cover authorization/revocation,
  revision mismatch, migration, zero dispatch, and deferred-family absence.
  Because the E2E test is tracked, review iteration 3 is invalidated and a
  fresh combined re-review of the enlarged test boundary is mandatory.
- Combined review iteration 4 independently reran the enlarged E2E boundary
  GREEN 165/165, rehashed the unchanged product correction and new E2E file,
  and returned `ACCEPT`. Review iteration 3's product acceptance remains valid;
  iteration 4 accepts the complete enlarged boundary and advances to final
  verification.
- Final verification passed the complete current non-final-image regression
  suite GREEN 613/613, the Web build, and the EF no-pending-model gate. The
  exact Docker command
  `docker build --progress=plain -f docker/Dockerfile -t vulgata:story-4-3 .`
  produced immutable image
  `sha256:851adce25ca8b5a799f2d62553aea9589918aa550ea48e23c6bf6c1159594c7a`.
- Superseded by the July 31 architecture remediation: Story 4.3 was previously
  recorded complete with no unresolved Critical/High finding. The previously accepted
  prepared-scope limitations are: case-distinct Linux Repository paths remain
  unsupported; public compatibility construction could substitute the empty
  detector although production DI used `CommunicationKindDetector`; and
  prepared flattened keys are supported for `application.properties`,
  `application.yml`, and `application.yaml`, while general nested YAML remains
  unsupported. The compatibility-construction limitation is no longer accepted
  and is an explicit correction target.

### File List

- `docs/bmad/implementation-artifacts/4-3-conservative-pre-scan-gate-and-communication-type-detection.md`
- `docs/bmad/implementation-artifacts/sprint-status.yaml`
- `docs/bmad/implementation-artifacts/workflow-activations/4-3-conservative-pre-scan-gate-and-communication-type-detection.json`
- `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`
- `src/dotnet/Vulgata.Core/DomainServices/IPreScanCommunicationDetector.cs`
- `src/dotnet/Vulgata.Core/DomainServices/IScanRunRepository.cs`
- `src/dotnet/Vulgata.Core/Entities/CommunicationKindClassification.cs`
- `src/dotnet/Vulgata.Core/Entities/PreScanResult.cs`
- `src/dotnet/Vulgata.Core/Entities/ScanRun.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/CommunicationKindClassificationConfiguration.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/PreScanResultConfiguration.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/ScanRunConfiguration.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260730083207_AddConservativePreScanGate.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260730083207_AddConservativePreScanGate.Designer.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`
- `src/dotnet/Vulgata.Core/DomainServices/ApprovedPreScanHandoff.cs`
- `src/dotnet/Vulgata.Shared/Repositories/ApprovedPreScanHandoffDto.cs`
- `src/dotnet/Vulgata.Web/Data/PreScanHandoffService.cs`
- `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`
- `src/dotnet/Vulgata.Infrastructure/Git/CommittedRepositorySnapshotService.cs`
- `src/dotnet/Vulgata.Infrastructure/Git/GitWorkingCopyService.cs`
- `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`
- `src/dotnet/Vulgata.Infrastructure/CommunicationDetection/Catalog/communication-evidence-rules.v1.json`
- `src/dotnet/Vulgata.Infrastructure/CommunicationDetection/CommunicationEvidenceRuleCatalog.cs`
- `src/dotnet/Vulgata.Infrastructure/CommunicationDetection/CommunicationKindDetector.cs`
- `src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj`
- `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`
- `src/dotnet/Vulgata.Web/Program.cs`
- `tests/Vulgata.Tests/PreScanCommunicationDetectionTests.cs`
- `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
- `tests/Vulgata.Tests/RepositoryApiTests.cs`
- `tests/Vulgata.Tests/RepositoryProfilingTests.cs`
- `tests/Vulgata.Tests/MigrationTests.cs`
- `tests/Vulgata.Tests/PreScanHandoffTests.cs`
- `tests/Vulgata.Tests/Story43EndToEndTests.cs`
- `specs/003-conservative-pre-scan-gate/reviews/quality-review-2.json`
- `specs/003-conservative-pre-scan-gate/reviews/quality-review-3.json`
- `specs/003-conservative-pre-scan-gate/reviews/quality-review-4.json`

## Change Log

- 2026-07-30: Prepared and activated Story 4.3 under the expedited-mvp
  exception; no tasks or implementation generated.
- 2026-07-30: Plan review accepted after correction; paused before task
  generation for mandatory Scan-strategy authorization.
- 2026-07-30: User approved the accepted Scan strategy; task generation
  resumed without waiving later gates.
- 2026-07-30: Tasks and read-only analysis accepted; assembled the first
  expedited accepted-delivery-definition checkpoint.
- 2026-07-30: Bound the accepted-delivery-definition checkpoint at
  `d47a9f1ad56b2a5981e73e33552b8e8a5c624f1d`; foundational TDD may begin.
- 2026-07-30: Completed T004-T006 with observed RED-GREEN evidence; root
  verification passed 12 committed-snapshot tests and 62 profiling regressions.
- 2026-07-30: Completed T007-T009 with observed RED-GREEN evidence; root
  verification passed 66 profiling tests, 15 snapshot/candidate regressions,
  and the Web build with zero errors.
- 2026-07-30: Completed T010-T012 with observed RED-GREEN evidence; root
  verification passed 33 lifecycle tests and 69 profiling/candidate
  regressions.
- 2026-07-30: Completed T013-T016 with observed RED-GREEN evidence; root
  verification passed 49 closed-classification tests and 69 profiling/candidate
  regressions after catalog-bound and lexical false-positive hardening.
- 2026-07-30: Final verification passed the 604-test non-final-image suite,
  isolated PostgreSQL apply/rollback/reapply/model gate, and the unchanged
  Story 4.3 production Docker build after the transient Ubuntu mirror failure
  cleared.
- 2026-07-30: All Story 4.3 implementation tasks are complete; transitioned
  the story from `in-progress` to `review` for the pinned combined review.
- 2026-07-30: Combined review iteration 1 returned `REVISE` with two High
  detector acceptance defects; dispatched a fresh TDD correction without
  advancing to E2E.
- 2026-07-30: Corrected both High findings with observed 2/2 RED, focused 2/2
  GREEN, and independently rebuilt detector/profiling GREEN 130/130; bound the
  permitted recovery checkpoint at
  `f501103657c67325be304a3b92ca779c75c74bbe` for fresh combined review.
- 2026-07-30: Combined review iteration 2 confirmed both prior High fixes but
  returned `REVISE` for pre-materialization entry-count enforcement and
  multiline/foreign-language annotation false confirmations; dispatched a
  fresh bounded TDD correction and kept E2E paused.
- 2026-07-30: Corrected both iteration-2 High findings with observed 0/1 and
  0/3 REDs, focused GREEN, root detector/profiling GREEN 136/136, complete
  non-final-image GREEN 612/612, and Web build 0 errors; pinned the exact
  correction diff for combined review iteration 3 without adding another
  expedited recovery checkpoint.
- 2026-07-30: Combined review iteration 3 accepted the exact hash-pinned
  correction boundary with no unresolved Critical/High and advanced the story
  to conditional E2E without duplicate user approval.
- 2026-07-30: Required conditional E2E passed GREEN 165/165 after adding one
  prepared three-configuration integration test; the tracked test-only change
  invalidated review iteration 3 and triggered a fresh combined re-review.
- 2026-07-30: Combined review iteration 4 accepted the complete enlarged
  product-plus-E2E boundary and advanced the story to final verification.
- 2026-07-30: Final verification passed 613/613 current non-final-image tests,
  the Web build, the EF no-pending-model gate, and the exact Story 4.3 Docker
  build; assembled the provisional completed-story checkpoint and transitioned
  the story from `review` to `done`.
- 2026-07-31: A fresh Epic 4 architecture audit found the concrete framework
  detector still exposed a parallel public string-path/Git snapshot entry
  point contrary to the accepted lease-only plan. The user approved the narrow
  correction; Story 4.3 returned to `in-progress` for RED-GREEN implementation,
  combined review, verification, and a fresh Epic 4 audit.
