# OCT-SCAN research and decisions

Date: 2026-10-06. Static source inspection at `fad7f11c9d6bc9ed3da1b371b24beb9f5c09db5d`; read-only research agent supplied an independent source mapping. No tool/provider/database runtime was executed.

## Current cross-lane adoption — 2026-10-06, second pass

- Verified Business `contracts/knowledge-contract.md` SHA-256 `F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36` in the explicitly supplied `f081` feature directory; inspected supporting plan/model/fixtures/quickstart read-only. Adopt F1/F2/F5 with no remaining semantic objection against that SHA. No counterpart edits.
- **F1 decision**: serialized commit submission under the shared per-attempt guard is the ordering boundary. Completion seal proves analysis only; Scan owns eligibility/fence/monotonic deadlines, Business checks transaction entry and final fresh authority/eligibility before guarded submission. Already-submitted commits settle exactly even after cancellation. Temporary absence while a write might finish cannot prove rollback; freeze waits. Publication follows the same phase-eligible rule.
- **F2 decision**: every Business provider attempt/retry uses Scan's process-local shared cap/request-rate/budget/deadline/cancellation/accounting scope. No outer orchestration permit around child calls; cap=1 remains usable. Actual/unknown usage settles once and permit release waits for settled/confirmed-disposed transport. Unknown usage retains conservative reservation or blocks further constrained calls. No Business pool/limiter or cross-process Web coordination.
- **Rationale**: These owner rules address the recorded seal→expiry→entry counterexample, commit-ack uncertainty, queued/hard-stop/rate races and capacity/accounting leakage without a new receipt system or competing consumer API. Earlier seal-only and commit-visibility shorthand below are superseded.
- **Checkpoint**: initial owner contract/fixture commit → minimal consumer compile/conformance probes → accepted exact CP-K1/C0 → separately authorized dependent production. No completed-consumer prerequisite and no compiled checkpoint claimed by planning concurrence. F5 retains explicit existing-policy selection and truthful CL-only success without B1/B2.

## Historical first cross-lane correction pass — superseded holds, 2026-10-06

- **F5 decision**: Require `outputStage=cl-only|cl-and-bl` in the existing scan-policy file. CL-only uses Business B0 acceptance and fixed CL publication/read ports with exact operation recovery; B1/B2 remain S3. Stage-labelled output prevents CL success from implying BL completion. Missing/unknown stage rejects; no default, new configuration framework, SQL write or automatic upgrade.
- **Source**: Read-only inspection of the explicitly supplied Business counterpart `C:/Users/13728/.codex/worktrees/f081/vulgata/specs/20-1-Oct14-business/contracts/knowledge-contract.md`, Fixed publication: B0 CL-only and a separate immutable B2 CL+BL selection are already permitted. This observation does not claim adoption of Business's pending F1/F2 revision.
- **F1 hold**: Earlier local common-gate/commit-order design below is historical and not canonical agreement. Business v1 instead linearizes at an immutable completion seal. The seal → drain-expiry fence → transaction-start counterexample and all still-settling writes must be resolved by the owner before Scan implementation.
- **F2 hold**: The earlier callback is a consumer requirement, not an agreed interface. Shared live concurrency/provider admission, budget reservation/accounting, cancellation and permit cleanup are necessary; copied numeric budgets alone cannot enforce them. Await the stable Business revision for adoption/objection, with no consumer type fork.
- **Alternatives rejected**: Requiring B1/B2 for early CL, reporting finalization success without BL, automatically choosing a weaker stage, or settling F1/F2 independently in Scan.

## Scope and authority

- **Decision**: Use the October delivery plan, integration plan and Scan brief as the first-deliverable boundary, retaining compatible constitution/PRD/security/testing constraints.
- **Rationale**: The user explicitly selects a new direct-SpecKit feature and stops before tasks. Older task matrices and full 18.5 execution are not prerequisites.
- **Alternatives rejected**: Resuming governed stories or claiming generic product completion would misrepresent the authorized scope.
- **Sources**: `docs/bmad/planning-artifacts/october-delivery-plan-2026-09-30.md`, `october-integration-plan-2026-10-06.md`, `october-delivery/scan-batch-feature.md`, `october-delivery/business-knowledge-feature.md`; October proposal authority-delta table; architecture AD-2/6/7/8/14/23/32 and constitution 2.0.0.

## Generator and real execution

- **Decision**: Generate owns a live session and a new OCT-specific bounded MAF executor, consuming the selected provider configuration and Business types.
- **Rationale**: `Vulgata.OctoberGenerator/Program.cs` only parses prepare/select; `OctoberGeneratorModule.ExecuteAsync` disposes its `IOctoberOperatorSession` on return. `OctoberProviderBinder` validates configuration/decryptability but does not create an execution client. `PreparedSourceBinding` already maps exact target/batch/Repository/root identity.
- **Alternatives rejected**: Calling Select then using its disposed context; routing through `PreparedPass1WorkerDispatcher`, whose contracts require legacy pair/ScanRun/Git identity; dummy Worker output.
- **Reusable mechanism**: `Vulgata.Agents/WorkerExecution/PreparedPass1JobExecutor.cs` constructs a real `ChatClient.AsAIAgent` with `FunctionInvokingChatClient`, bounded iterations, serial tools and controlled retry/logging. Copy the pattern, not its semantic packet contract. Installed MAF versions come from `Directory.Packages.props`; no version update or external API research was needed.

## Tool lifetime and containment

- **Decision**: One process-local Repository owner, one preparation, serialized bounded native session replacement on the returned descriptor, dedicated JDT windows and short source contexts.
- **Rationale**: `NativeCodeGraphSession.PrepareAsync` rejects existing `.codegraph`; `PreparedCodeGraphConnection.OpenSession` allows one session and disposal leaves the index. `NativeCodeGraphMcpClient` caps two minutes/ten calls and directly reads paths for excerpt comparison. `PreparedJavaNavigationContextFactory.OpenDedicatedAsync` is the qualified dedicated entry; Web's `PreparedJavaWorkerToolComposer` calls guarded `OpenAsync` instead. Ordinary source and tool workspace grants cap at 30 seconds. `PreparedSourceReadContext` continuation cursors are context-local/single-use and lack positive owned-state exclusion.
- **Alternatives rejected**: Unlimited leases, global cap changes, per-Worker index initialization/deletion, restart adoption, blanket name exclusion, post-return filtering, reconstructing native commands or treating source observations as OS isolation.
- **Bounded unresolved runtime question**: Whether useful JDT startup fits 30 seconds is settled by SC1, not assumed. The reviewed candidate fallback is a Generate-only 120-second dedicated profile with current checks and finite run budget. Implementation/runtime cannot proceed with an unqualified profile. This is a qualification dependency, not an unspecified design choice.

## Original Knowledge ownership and stop research — F1 ordering superseded above

- **Decision**: Business supplies compiling shared contracts and the sole persistent writer. Scan supplies trusted completion and a common attempt gate, then freezes only resolved accepted handles and terminal coverage.
- **Rationale**: Model `Complete` is not terminal authority; prechecking a fence outside the transaction permits late writes. Lost storage replies must resolve by operation identity, not repeated inference. A graceful stop is a partial terminal corpus, while hard interruption is not success.
- **Alternatives rejected**: Local schema/receipt system, raw row import, latest-batch fallback, Scan-owned publication or a second Business Worker pool.
- **Remaining dependency**: C0 commit and concrete owner-selected type names are not yet supplied. Required semantics are fully enumerated in `contracts/scan-boundaries.md`; absent C0 blocks implementation, not completion of this planning-only request.

## Evidence policy

- **Decision**: Tool fixture first, one real file second, connected batch third, final combined qualification under integration owner.
- **Rationale**: Isolated historical native passes, SQLite fixtures and JSON validity cannot prove current combined tools, PostgreSQL atomicity or useful generated content.
- **Alternatives rejected**: Running live source/provider qualification during planning; repeating unchanged historical suites; turning deferred scope into a new queue.
