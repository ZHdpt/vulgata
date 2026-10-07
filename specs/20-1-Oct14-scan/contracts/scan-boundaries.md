# OCT-SCAN boundary requirements

**Checkpoint C0 status**: T001–T004 bounded preparation complete. Scan acknowledged the exact initial compiled owner contract/fixture after 69/69 filtered SelfContained cases passed (19 new Scan + 50 imported Business), with no blocking supported-conformance objection. Historical evidence and fixture limits are recorded in `../conformance-c0.md`. S1 subsequently imported integration's accepted common CP-K1 `12d92d80339ea622cd26c593ebdb74da59401f91` under the direct user instruction to implement T005–T012. S1 T005–T012 is implemented and qualified, including combined native JDT settlement, as recorded in `../verification-s1.md`. That S1 boundary changed no shared contract or schema. The later S2 authorization and owner checkpoint are recorded next.

## Current S2 owner checkpoint

The later direct user instruction authorizes S2 T013–T024. The latest consumed
approved checkpoint is `d72d1b80fd13a5ba21e425cffcc2d9cd89103316`, merged on Scan as
`256fdbf15a28e3bc0d7a2b96618b797edc6f23f2`. It includes reviewed supporting-input
implementation `0ab10a77` and corrected fresh admission `7958a5e3`; the earlier
admission hold is lifted for controlled composition. This grants no runtime resources.

- Supporting content uses owner `Contributor.SourceOrigin`; metadata uses
  `ContributorKind.SourceMetadata` and `MetadataOrigin`. The host retains every
  actual input, including uncited results, before final digest/completion. Use owner
  `ComputeMetadataResultDigest`, `ContentOrigins`, `NormalizeContributors` and
  `ValidateInputs`; metadata never becomes a source-content anchor.
- `PreparedVersion` is the existing approval identity as a root-session provenance
  label, checked with its complete approved binding. File digest and identity prove
  content; the label is neither a snapshot nor permission. The 128 content-origin
  and 128 metadata-observation ceilings cannot enlarge original tool/input grants.
- `IOctoberGenerateAdmissionStore` runs after source-free preflight and before any
  native/source/Worker activity. Its dedicated `IOctoberGenerateAdmissionAuthority`
  rechecks the held operator. `OctoberGenerateAdmissionPolicy` derives the sole
  `(BatchId, BeginGenerate=5, BatchId)` key and payload digest; the exact guard uses
  `CommitAttempt.AttemptId = RunId`. A receipt permits no restart or second dispatch.
- Mandatory `IOctoberCorpusFreezeAuthority` proves closed terminal inventory and
  settled writes. Exact freeze/publication operations retain their own guard
  identities. CL-only needs no Resolution/Synthesis service or provider call.

Business owns these contracts, persistence, policy and migrations. Scan's complete
ledger and full Generate composition remain under T021/T022 verification; imported
owner behavior and component tests are not the real T024 milestone. See the owner
[input contract](../../20-1-Oct14-business/contracts/supporting-inputs-contract.md),
[admission contract](../../20-1-Oct14-business/contracts/generate-admission-contract.md)
and [current Scan evidence](../verification-s2.md).

## Imported initial contract — bounded preparation

Authorized integration checkpoint: `3bcd815b09cfbac7418d6644a2a75816e0788ec0`, containing Business candidate `07c3fe4463e3228a6982fd106b58148ca87974bd` and handoff `7a189c34aec625202f74846d4907ded787ccbdca`. This lane preserved its planning in `23ca0883` and merged the exact integration commit at `5d98ee54`. Both the supplied integration checkpoint and `fad7f11` are ancestors. No reset, history rewrite or other-checkout edit occurred.

Imported owner Markdown contract SHA-256: `719C5AF610E5A84EEA99CA03A6487015DEE79A052D94B0E0902185BA363D4568`. The earlier `F449...` below identifies the historical planning review, not the imported compiled boundary. The compiled candidate makes the accepted submission timing concrete: `Func<CancellationToken, SubmittedCommit>` synchronously invokes commit before returning its settlement task; callback invocation alone is not submission. No deferred Task.Run/async preamble/queued action may move the commit outside the guard.

| Required meaning | Actual imported owner symbols |
| --- | --- |
| Exact binding and prepared provenance | `Vulgata.Core.DomainServices.OctoberKnowledge.KnowledgeBinding`, `PublicationBinding`, `PreparedOrigin`, `DocumentVersionReference`, `DocumentHandle`, `OperationKey` |
| Whole-file proposal/analysis proof | `CompletedFileProposal`, `CompletedFileProof`, `CompletedFileSubmission`, `IOctoberFileAcceptanceAuthority`, `AcceptedFile`, `FileCoverage` |
| F1 entry/submission/settlement | `IOctoberCommitSubmissionGuard.CheckBeforeTransactionAsync` / `SubmitAsync`, `CommitAttempt`, `SubmittedCommit`, `CommitGuardResult`, `CommitEligibilityStatus`, `CommitSubmissionStatus`, `CommitSettlement` |
| Acceptance/lookup/freeze/CL publication | `IOctoberKnowledgeWriter.AcceptCompletedFileAsync`, `LookupOperationAsync`, `FreezeCorpusAsync`, `PublishSelectionAsync`; `CorpusFreezeRequest`, `CorpusHandle`, `PublicationRequest`, `KnowledgeWriteResult` |
| F2 per-attempt host scope | `IOctoberExecutionScope.AcquireProviderAttemptAsync`, `ProviderAttemptRequest`, `ProviderAdmission`, `IOctoberProviderAttemptLease.CheckBeforeSendAsync` / `SettleAsync` / `DisposeAsync`, `ProviderAttemptUsage` |
| S3 finalization consumer boundary | `IOctoberBusinessFinalizer.FinalizeAsync`, `FinalizationRequest`, `BusinessFinalizationResult`, `BusinessFinalizationStatus` (Published, PublishedPartial, NoContent, Failed, Denied, AuthorityUnavailable, HardStopped, CommitIndeterminate) |
| Exact ordinary reads/authority | `IOctoberKnowledgeReader`, `IOctoberKnowledgeDisclosurePolicy`, `ReadSelectionRequest`, `ReadSelection`, `KnowledgeSearchRequest`, `ExactReadRequest`, `RelatedReadRequest` and owner page/status records |
| Shared transport validation | `Vulgata.Shared.OctoberKnowledge` selection/search/read/related DTOs and their FluentValidation validators in `KnowledgeDtos.cs` / `KnowledgeRequestValidators.cs` |
| Controlled fixture | `tests/Vulgata.Tests/October/Knowledge/OctoberKnowledgeFixture.cs`: labelled synthetic data plus `ControlledCommitGuard`, `ControlledKnowledgeWriter`, `ControlledExecutionScope`, `ControlledKnowledgeReader`, `ControlledDisclosurePolicy` and `ControlledFinalizer`; these are owner-provided test models, not production services |

Consume these names directly. In particular, use `IOctoberProviderAttemptLease` rather than inventing a Scan provider permit contract; the future host supplies its implementation. `ProviderAttemptRequest.BusinessWorkKind` describes Business Resolution/Synthesis; file Workers share future host accounting without inventing a parallel Knowledge DTO. `FinalizationRequest` carries the same execution-scope instance and exact corpus/digest; explicit owner finalization status maps to the lane's planned CLI outcome, never inferred from a non-null publication alone.

Scope ends at initial controlled consumer conformance. Real transactional writer, producer validation/Identity authority, durable lookup, freeze/publication enforcement, actual host guard/scheduler/rate/transport lifetime, tool integration and ordinary Web composition are later production prerequisites. No T005/native/provider/database/startup work is authorized here. Fixture-script coverage must not be presented as those implementations or full CP-K1 acceptance by all lanes.

**Cross-lane concurrence (2026-10-06): Scan adopts F1/F2/F5 against Business's canonical `contracts/knowledge-contract.md`, SHA-256 `F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36`, verified in `C:/Users/13728/.codex/worktrees/f081/vulgata/specs/20-1-Oct14-business`.** This is planning-semantic agreement, not compiled conformance or implementation approval. Business owns the shared types. CP-K1 and local C0 denote the same checkpoint; no competing contract/gate is introduced. Earlier unresolved review entries remain historical.

These are requirements to reconcile with OCT-BUSINESS, not a new API catalogue or schema. Actual type names and the integration commit are supplied by Business before consumer implementation. Do not create local DTO substitutes. The shared fixture contains CL, BL, relationship, Uncertainty and a disallowed contributor and is labelled fixture-only.

## Knowledge acceptance and exact reconciliation

Input semantics: exact target/batch/Repository/root-version/file binding from trusted setup; operation identity; immutable whole-file proposal/fingerprint; units/CL/anchors/observations/coverage/advisory contributors; and separate application-trusted admission/completion authority. Scan owns admission and trusted terminal facts; Business validates semantic identities and the atomic write.

Completion is bound to the exact proposal and attempt, valid provider termination, drained tools, complete required coverage and verified source. Model JSON cannot issue the capability. **F1 adopted: the immutable completion seal proves analysis only, not write permission.** Scan owns separate irreversible attempt eligibility/fence state, monotonic drain/run deadlines and hard-stop signal. Business checks capability, live authority and eligibility before transaction entry. Immediately before commit submission the writer and Scan use the same process-local per-attempt guard to recheck current fence, monotonic deadlines, hard stop and fresh authority, then mark/submit that exact transaction as CommitInFlight. Hold the guard through submission with no unguarded await/work after the final checks. The guard serializes effective fencing against submission; it is not a durable receipt or proof of database commit.

For `seal → drain expires/fence → transaction starts`, reject at entry: no transaction begins. If staging began before expiry, the final guard rejects submission and rolls back staged rows. Check monotonic time even if the expiry timer callback is delayed. A commit already submitted before fencing may physically commit afterward: cancellation reaches its I/O but does not prove rollback. Track and settle it by exact operation lookup under fresh disclosure authority; freeze remains blocked until committed or confirmed rolled-back/never-submitted. Preserve committed history. Apply the same guard/eligibility rules to new PublishSelection commits with their own operation identity and phase eligibility; ordinary readers require no generator execution scope.

Use Business's statuses, including Committed/AlreadyCommitted, Conflict, InvalidProposal, NotCompleted, StaleOrFenced, Denied/AuthorityUnavailable, CancelledBeforeCommit, StorageUnavailable, CommitIndeterminate and LogUnavailable, without local status-type forks. Exact lookup uses the same operation identity and digest and rechecks disclosure authority. Unavailable lookup or a momentarily absent row while the original write can still commit is CommitIndeterminate, never definitive absence. Only confirmed rollback/never-submitted plus still-current attempt eligibility permits replay of the same key/payload; every new submission rechecks F1. No replacement write, freeze, success claim or model regeneration while the original operation remains unresolved.

## Terminal outcomes, corpus freeze and finalization

Freeze input includes every admitted file's terminal disposition and exact accepted handles, plus selected/excluded/supporting/unsupported coverage and reasons. No running/pending/indeterminate writes are permitted. Root/batch mismatch or foreign handle rejects. The owner validates membership and returns immutable corpus identity; Scan cannot fabricate it from arbitrary database rows.

First graceful stop closes new file admission but is not itself a fence: active attempts may finish inside drain/run eligibility. Drain expiry fences those file attempts against new transaction/commit submission; it does not cancel a separately permitted finalization phase or create more budget. Hard stop fences attempts, cancels owned I/O and bars new provider work/publication/finalization. Observed authority loss/unavailability denies disclosure and submission, fences/cancels affected work, and bars publication/finalization under that lost authority. Already-submitted commits always require exact settlement; stopped-gap requires confirmed noncommit, not cancellation alone. Preserve existing committed history. Once all writes settle, CL-only publishes the fixed CL set; CL-and-BL finalizes it within the original remaining scope. Business owns semantics and cannot expand source scope or schedule another Scan.

**F2 adopted — Scan-owned process-local execution scope**: Every Business inquiry/matching/synthesis provider attempt, including each retry, acquires the owner-published semantic port (proposed `IOctoberExecutionScope.AcquireProviderAttemptAsync`). Scan creates/binds it to run, finalization operation, purpose/phase, selected provider, the one cap shared with file Workers, host request/rate admission, remaining request/usage/time budgets, absolute monotonic deadline and hard-stop cancellation. Business may narrow bounds, never switch profile/scope, reset counters or extend deadlines. Numeric per-call limits only further restrict it. No independent Business pool, limiter or cross-process Web permit service; no outer finalization orchestration permit is held while waiting for child calls, so cap=1 can progress.

Acquire within the remaining deadline; reserve request/usage budgets and recheck eligibility/rate admission on grant and immediately before provider send. Rate-delayed calls stay queued within the original deadline; denied/expired grants send nothing. Link I/O to deadline/hard-stop; cancel active attempts and deny queued/new/retry calls on hard stop, lost authority or run expiry. First graceful stop permits active-file drain and the separately allowed Business phase within original budgets. CL-only performs zero Business provider calls. Late responses cannot newly accept/publish under F1.

Settle each dispatched attempt exactly once with correlation, request count, actual reported usage or explicit unknown, waits/duration and terminal outcome. Unknown usage is not zero: retain a conservative reservation or block further usage-constrained calls if allowance cannot be established. Release/dispose a permit only after the attempt/transport settles or disposal is confirmed. Unconfirmed cancellation remains owned outstanding/cleanup failure and stops dispatch; do not reclaim capacity early. Settlement/cleanup on every outcome must not double-charge or double-release. No persistence uncertainty or accepted result justifies a new model attempt. Missing/incompatible scope stops dispatch. Business defines shared types; Scan supplies their host implementation only after authorization/checkpoint.

Required F2 probes: cap=1 with two concurrent inquiries and inquiry+synthesis, no outer-permit deadlock; one active and one queued at hard stop; cancellation between grant/send; queued/rate-denied calls send nothing; retry acquires anew without renewed budgets/rate allowance; unknown-usage reservation; and error/unconfirmed-cancellation settlement without early release. F1/F2 semantic objections are resolved against the pinned canonical SHA; compiled conformance, production and runtime qualification remain future prerequisites.

Finalization results distinguish confirmed exact publication, no-content, partial/failed and indeterminate. Identical replay/lookup must recover an ambiguous result without repeated synthesis. Publication/read membership is Business-owned, not a Scan list of guessed IDs.

## Shared reads and ordinary Web

Business publishes exact batch/publication/Document Version/section handles and bounded list/search/content/related reads with provenance, coverage and unknowns. Enforce live user/purpose/mode and all source/advisory contributors at every disclosure, including metadata and denial-safe counts. Business Mode exposes BL only. Missing/mismatched selection fails closed without previous/latest fallback.

Explore supplies the exact reader destination; integration composes ordinary Web with frozen DefaultConnection. Scan's operator session, credentials and selector are not reader authorization. Web never runs October migrations for this feature.

## Generator command and source/tool contract

Proposed command selection: extend the existing environment-backed `October:Mode` parser with `generate` (`October__Mode=generate` in the child-process environment). Preserve existing `October:TargetId`, `October:BatchId`, authority/connection/provider document selectors; add `October:ScanPolicyFile` for the explicit policy file. In that same policy, require `outputStage` equal to `cl-only` or `cl-and-bl`; missing/unknown values fail before dispatch. Pin it for the execution; there is no default, interactive stage switch, second selector or configuration framework. Preserve prepare/select behavior. These selectors are implemented and controlled-tested; full Generate execution wiring remains incomplete.

### F5 — early CL-only publication route

For S2 explicitly use `outputStage: cl-only`. Route: bind → atomic fresh-run admission → required tools → declared inventory/admission → real whole-file Worker → Business acceptance and exact acknowledgement reconciliation → settled terminal coverage/frozen exact set → Business's `PublishSelectionAsync` responsibility for a CL-only fixed publication → exact owner-returned publication/Document handles. Its input contains only that set, coverage/provenance, expected corpus identity and stable publication operation key; empty relationship/BL selections assert no matching was performed. Do not resolve or invoke B1/B2 services, require their implementation, run their providers, issue SQL or synthesize publication IDs. Shared compiled contracts may describe the later stages; their production implementations are not S2 prerequisites.

The proposed command result reports `requestedStage`, `completedStage`, execution/coverage status, exact batch/publication binding and returned Document Version/section handles, plus `matching=not-requested` and `businessLogic=not-requested` for CL-only. These are host report fields referencing owner types, not a new Knowledge DTO. Report completion only after publication is confirmed; lost acknowledgement uses exact operation lookup/replay without re-analysis or re-publication under a fresh key. Indeterminate lookup returns exit 4; accepted CL alone is not published success. No accepted files or no CL Documents (including only valid zero-unit manifests) returns exit 5 with truthful coverage, not a usable wiki claim.

An ordinary permitted IT reader opens the exact handle through Business reads and Explore's destination. Business Mode yields its existing safe empty/unavailable behavior for CL-only content, never CL disclosure or fabricated BL. S2 needs B0 acceptance/CL publication/exact reads and the reader composition; B1 matching and B2 BL are not dependencies.

For S3 explicitly use `outputStage: cl-and-bl`; after the same settle/freeze path invoke Business matching/BL finalization. Missing B1/B2 fails this requested stage without pretending the CL-only stage fulfilled it or silently downgrading. Any later publication is a separate immutable identity; the earlier CL publication and handles remain exact. This selector adds no restart/reuse/resume command and does not automatically promote reader selection to a later publication.

Policy requires explicit selection/ranking, Worker cap, file/run/model/tool/usage ceilings, retry limit and graceful-stop drain. Output carries sanitized correlation, exact publication when confirmed, coverage, outcomes, waits/timing/usage and gaps; stdout/logs never include roots, credentials, raw source or hidden reasoning. Generate exit codes: 0 confirmed complete selected-scope publication for the explicitly requested stage; 1 configuration/authority/tool/provider/store/finalization failure; 2 confirmed partial publication (including graceful stop); 3 interruption/hard cancellation; 4 indeterminate acceptance/publication; 5 no-content. Exit 0 with `completedStage=cl-only` does not claim matching/BL or S3 completion. Prepare/select retain their existing exit contract. A partial stop is never a full-completion exit.

Worker tools expose only closed relative read/list/search, normalized JDT definition/reference, and bound native CodeGraph discovery/explore. Host fixes roots/commands/credentials. Pre-use and pre-disclosure checks are mandatory. Source paging remains scoped to one context; positive owned-state exclusion applies before count/search traversal. Tools report budget/expiry/limitations without inventing evidence. Native sessions only renew inside the current owner/process and finite budget after confirmed disposal.

## C0 acceptance observations

After separate authorization, Business first publishes an initial compiling contract plus labelled fixture commit; integration imports that hash. Minimal consumer compile/conformance probes against it validate CP-K1/C0 before dependent production is enabled. Completed consumers are not prerequisites for those probes; no circular gate. Once objections are resolved and the exact checkpoint accepted, separately authorized dependent production may proceed. Neither the pinned Markdown SHA nor this concurrence proves a compiled checkpoint, creates probe code or authorizes tasks/runtime. Dialogue-specific schema agreement does not gate B0.

The shared fixture/probes must demonstrate: valid/zero-unit whole file; invalid/failed drafts invisible; forged completion denied; seal→expiry→entry creates no transaction; expiry during staging submits no commit, even with delayed timer callback; already-submitted commit followed by hard-stop/revocation resolves committed or confirmed noncommit/indeterminate with fresh disclosure checks; temporary row absence cannot release freeze; identical replay/conflict and no provider recovery; pending/foreign handle freeze rejection; wrong selection no fallback; denied contributor and BL-only reads; all F2 cap=1/queued/rate/deadline/hard-stop/unknown-usage/cleanup probes above. Repeat submission/settlement rules for publication. PostgreSQL qualification later proves actual transactional behavior beyond controlled probes.

F5 fixture additions: select CL-only with matching/BL services absent or call-count sentinels that must stay at zero; publish one accepted file through B0 and open the exact returned handle as ordinary IT; verify Business exclusion; reject missing/unknown stage; publication acknowledgement loss performs lookup without Worker rerun; acceptance without confirmed publication is not success; zero-unit/no-Document output is no-content; CL-and-BL without B1/B2 fails rather than downgrades; later separate CL+BL selection leaves the earlier CL handles unchanged. These are fixture descriptions only, not generated tests.
