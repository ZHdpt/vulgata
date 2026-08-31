# Quickstart: Story 12.5 Track C-MVP with Prepared Scan Initiation and Exact Owner Semantic Identity

This guide validates the corrected definition, the bounded production Prepared Scan entrance, and the single combined host integration/release run. It does not create planned-change or implementation approval and does not permit use of the main/Story 12.4 database.

## 1. Re-observe the pinned boundary

    git branch --show-current
    git rev-parse HEAD
    git rev-parse --git-common-dir
    git status --porcelain=v2
    git merge-base --is-ancestor 4798b97d9f23aef587829175df7ab7ebc3daf995 HEAD
    Get-FileHash specs/014-grounded-llm-wiki-chat-release-evidence/spec.md -Algorithm SHA256

Expected before implementation: branch demo0831/story-12-5-trackC; Story 12.4 is ancestral; only explicitly owned corrected-definition files plus preserved user-owned changes are dirty. The exact-owner plan must receive independent plan acceptance, explicit AGENTS.md Scan planned-change authorization, regenerated-task independent definition acceptance, and a newly bound implementation approval. No expired standing authorization applies.

## 2. Validate corrected plan artifacts

    rg -n "attestation sink|ten-check|separate Tier 2|live outage|release-summary product" specs/014-grounded-llm-wiki-chat-release-evidence
    git diff --check -- specs/014-grounded-llm-wiki-chat-release-evidence

Expected: no unresolved placeholder; removed mechanisms appear only as explicitly deferred/historical material; diff check passes.

Also inspect [contracts/prepared-scan-initiation.md](contracts/prepared-scan-initiation.md) and confirm the accepted initiation/seed design remains unchanged. Confirm AD-PSI-7 adds only an optional Core semantic-owner value, root-DocumentSymbol hierarchy provenance with mandatory contained selectionRange, flat SymbolInformation non-authority, semantic-only file-identity v2, symbol-aware persistence/handoff/internal packet/assignment reconstruction, exact job-runner forwarding, dispatcher/executor validation, and builder cache. Confirm path-valued v1 remains byte-identical and there is no new DI mapping, public/provider-visible symbol field, prompt/tool/job-state/scheduling/retry change, sample literal, migration/model change, long transaction, bound increase, Story 12.4 mutation or Pass 1 policy change.

## 3. Tier 1 regression

Run the current focused corpus, database guard, Prepared Chat, streaming, authorization/currentness, citation, publication/reset and production Scan UI suites selected by regenerated tasks. The baseline commands remain:

    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~Story125EvaluationContractTests|FullyQualifiedName~Story125Tier2PostgreSqlTests|FullyQualifiedName~Story125ReleaseHarnessTests"
    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedChatIntegrationTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatEvidencePacketBuilderTests|FullyQualifiedName~ChatProviderStreamAssemblerTests|FullyQualifiedName~OpenAiCompatibleChatProviderStreamingTransportTests|FullyQualifiedName~ChatPageTests|FullyQualifiedName~ProductionScanStatusReaderTests"
    dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore

After approved implementation, add the bounded initiation verification:

    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedPass1ContractIdentitySourceTests|FullyQualifiedName~PreparedScanInitiationIdentityTests|FullyQualifiedName~PreparedScanInitiationCoordinatorTests|FullyQualifiedName~PreparedScanInitiationApiTests|FullyQualifiedName~PreparedScanRuntimeIdentityResolverTests"
    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedScanInitiationPostgreSqlTests"

Add the reviewed-profile correction verification selected by regenerated tasks:

    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ReviewedExactProfileSeedAuthorityTests|FullyQualifiedName~PreparedSelectionPlannerTests|FullyQualifiedName~PreparedScanApiTests|FullyQualifiedName~PreparedScanReleaseQualificationTests"
    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ReviewedExactProfileSeedPostgreSqlTests"

Add the exact-owner correction verification selected by regenerated tasks:

    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ReviewedExactProfileSeedAuthorityTests|FullyQualifiedName~JdtLsAdapterTests|FullyQualifiedName~PreparedJavaSplitRangeTests|FullyQualifiedName~PreparedFileCodeUnitAndSelectionTests|FullyQualifiedName~PreparedScanApiTests|FullyQualifiedName~PreparedHandoffProjectionTests"
    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~Pass1ProductionCompositionTests|FullyQualifiedName~PreparedPass1WorkerDispatcherTests|FullyQualifiedName~Pass1ClaimedPacketExecutionTests|FullyQualifiedName~PreparedPass1JobExecutor|FullyQualifiedName~Pass1RecoveryAndResumeApiTests|FullyQualifiedName~Pass1ReconciliationTests|FullyQualifiedName~ReviewedExactProfileSeedPostgreSqlTests"

The first reviewed-profile command proves exact match, canonical required paths/roles, nonmatch-neutral selection/fingerprints, score 1,100, unchanged 20/150 bounds and every matched incomplete/unsafe/duplicate/split/cap failure before Manifest commit. The PostgreSQL command begins from ordinary System/Repository records with no lifecycle/content/relationship rows, publishes both Manifests, runs deterministic test Pass 1 through the real committer, then invokes the unchanged exact-resolution coordinator/store to prove the exact REFERENCES Edge and honest Uncertainty. A required-seed failure must reach `FailedPass1` with zero exact/Pass 2 work.

The exact-owner commands prove only homogeneous array-root `DocumentSymbol[]` Class/Enum/Interface/Struct evidence with mandatory contained selectionRange supplies semantic TypeName; children are not flattened. Mixed/malformed roots, missing/uncontained selectionRange and non-Type roots fail. Homogeneous `SymbolInformation[]` is flat coordinate-only evidence, containerName never implies hierarchy, generic splitting remains possible, and a matched owner publishes zero Manifests. Definition/references/implementation remain unchanged. Whole-file/oversized and v1/v2 golden vectors remain. Claimed-source pre/post-fetch plus `Pass1ProductionCompositionTests` prove semantic FQN/v2 and path/v1 hydrated packet -> job-runner assignment -> dispatcher packet round-trip; omission/mutation rejects before executor/provider. Provider shape excludes SymbolName. PostgreSQL must persist the FQN/v2, current Source Anchor and unchanged exact Edge/Uncertainty without preseed/repair.

The first command owns the default Agents v2 prompt hash/schema authority, unchanged legacy v1 recovery identity, null/v1/malformed/wrong-schema fail-closed vectors, request/outcome/composition/no-later-stage behavior, the fresh pre-store authorization recheck, phase-aware handoff recovery, default/test runtime-resolver composition and fixed execution-identity vectors. The second must use disposable PostgreSQL and owns identical/competing concurrency, advisory-lock timeout, rollback and exact replay. A SQLite run cannot substitute for the second command.

Historical envelope tests remain unchanged and green as accepted Track A compatibility evidence, but Track C-MVP tasks and release acceptance must not implement or require their attestation, ten-check, counter-recomputation, supersession/closure, duplicate-orchestration, outage or screenshot-automation concepts. New current-boundary coverage targets the Markdown report and preconnection guard. Track A corpus and Track B product-security coverage remain unchanged.

## 4. Preflight before any product database connection

1. Pin the candidate commit and exact two Repository Profiled Revisions.
2. Generate one unique lowercase name matching vulgata_story125_[a-z0-9_]{8,48}.
3. Run the database-name guard test and independently confirm the name is not vulgata, a PostgreSQL system database or any vulgata_story124_* name.
4. Record PostgreSQL, CodeGraph, Java 21, JDT LS and non-secret provider identities.
5. Check that the provider credential exists without printing it.
6. Only after every guard passes, create the clean database and configure Vulgata to use it.

Example non-secret identity probes:

    git -C <repository-one-root> rev-parse HEAD
    git -C <repository-two-root> rev-parse HEAD
    codegraph --version
    java -version
    dotnet --info
    docker version

Record sanitized outputs in the report. Do not record provider keys, passwords, tokens, complete connection strings or secret-bearing host paths.

If Docker/PostgreSQL, a Repository revision, Java 21, CodeGraph/JDT LS, the provider credential/network route, authentication or browser capability is unavailable, record the exact sanitized blocked condition and stop dependent work. Do not substitute another database or fake provider.

## 5. Create the isolated database

Use the approved PostgreSQL administrator mechanism only after the guard passes. The resulting product connection must name exactly the generated owned database. Do not log its password or full connection string.

Immediately record database name and non-secret owner identity, PostgreSQL version, creation command purpose/exit result, and confirmation that no product process or query ran before ownership passed.

Do not delete any database as part of this guide. Cleanup requires a separate explicit integrity check and command.

## 6. Run the corrected production entrance and existing product path

Start Vulgata on the Windows host with the owned database:

    dotnet run --project src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-build

Through the authenticated existing UI/API:

1. production-bootstrap one Administrator, one ordinary authorized System, provider assignment, and exactly two ordinary real Java/XML Repository records; do not seed Scan Runs, Pre-Scan Results, Scan Executions, coordination, Manifests, File Code Units or Documents;
2. submit the existing authenticated `POST /api/systems/{systemId}/prepared-scans` request with exactly two safe full Git refs and exact Profiled Revisions;
3. after the synchronous endpoint completes, query one complete initiation tuple and two real Prepared Scan Manifests; verify both new-path Scan Executions carry one prompt identity matching `pass1-code-logic-worker.v2/sha256/{lowercase-64-hex}` and exact schema `code-logic/file-v2`, then verify CodeGraph 1.5, JDT LS Java readiness and safe XML catalog evidence; query every source derived from the embedded reviewed profile and prove every bounded segment is selected with direct score 1,100 inside the unchanged bounds; prove the reviewed unsupported-owner File Code Unit carries its profile-owned FQN, v2 identity and exact current range while path-valued units retain v1; use Tier 1—not operator query timing—to prove tuple commit and matched validation ordering;
4. start the accepted Pass 1 path and allow it to reach terminal state; prove every reviewed score-1,100 seed succeeded before exact resolution;
5. run exact Cross-Repository Resolution and the accepted Pass 2 path;
6. use 继续扫描 only for an eligible durable paused state;
7. observe terminal Scan state in the production Chinese management UI.

Repeat the same Prepared Scan request once after Manifest publication but before Pass 1 and prove the existing handoff/response returns with the same Run/Execution/coordination/Manifest identities, zero external preparation rerun and zero duplicates. Tier 1 separately proves induced pre-publication failure retry and advanced-phase recovery. Do not invoke `/scan-runs`, `RepositoryProfilingCoordinator`, `CodeGraphContextProvider`, or a Story 12.4 qualification database/harness. Do not change generic nonmatching selection, bounds, dispatch, XML, Pass 1 policy or relationship behavior beyond the accepted initiation and matched reviewed-profile seed exceptions.

## 7. Run straightforward read-only queries

Use psql or an equivalent read-only client against only the owned database. Record sanitized SQL/description, exit result and bounded rows for:

1. two approved PreparedScanManifests and total selected PreparedScanManifestEntries (50-150);
2. exact Repository/Profiled Revision and real CodeGraph/JDT LS/catalog bindings;
3. terminal Pass 1/Pass 2 executions/jobs, explicit high-value-seed selected/succeeded/failed counts, failed count zero, selected count equal to succeeded count, and ordinary failure budget;
4. current Code Logic and Business Logic DocumentVersions with SourceAnchors, including the reviewed owner Source Anchor joined to the exact symbol-bound File Code Unit/path/range/Profiled Revision;
5. at least one accepted CrossRepositoryResolutionDecision joined to its current cross-Repository REFERENCES DocumentEdge;
6. at least one honest Uncertainty;
7. at least three current Business Logic Documents and one published Cross-Repository FlowGroup;
8. pre-restart DocumentVersion, DocumentEdge and FlowGroup identities/counts.

Use the actual model columns from VulgataDbContext/configurations. A stale query is corrected to the model; a missing required row is reported as failure and is never inserted manually.

## 8. Execute twelve real Chat cases

Before the first provider call, record case IDs `q01` through `q12` from tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json. Execute exactly those IDs; do not replace failed cases after observing provider output.

Execute every row with fresh `ChatConversationState`. Treat `followUpTo` as topical lineage only. In particular, `q01` (Business), `q02` (IT) and `q03` (Business) are independent rows because each mode change invokes the accepted reset boundary; do not retain prior question, evidence or citation context.

For every row record case ID, Business/IT mode, question/topical-lineage relation, `freshConversationState`, disposition, usefulness, accepted/cited material claim counts, unauthorized disclosure, authorization oracle, streaming observation, complete rendered duration, packet fingerprint when exposed, Tier 1 evidence reference, and bounded notes.

For `q12`, record authorization oracle `prompt-boundary-only`. Under the same authenticated principal and exactly-two-authorized-Repositories scope, it proves only that asking about an outside-scope Repository does not make the answer confirm its existence or disclose its Business Rules, Documents, relationships, citations or Source Anchors. Do not label it `denied-access-exercised` or claim proof of a denied principal, known protected Repository, revocation or true missing-equivalent behavior. Use accepted Tier 1 tests as the authority for same-mode multi-turn re-fetch/revalidation and actual authorization/currentness behavior; do not add a third Repository or denied principal to this run.

Passing aggregates require at least 80% useful, 100% citation coverage for accepted material technical claims, zero unauthorized disclosure, zero accepted non-streaming provider answers and every accepted complete render below 30,000 ms.

Track C quotes existing Track B content-dark observations when available. It does not recompute raw-read, SSE, received Provider Chunk, decoded-delta or final-answer counters. Operational deterministic rescue is not a real streamed LLM answer.

## 9. Restart the Vulgata process

1. Record the running process stop command/action and result.
2. Stop only Vulgata. Keep PostgreSQL and the owned database unchanged.
3. Start Vulgata again with the same configuration.
4. Re-authenticate.
5. Open persisted wiki content and submit one new grounded Chat request.
6. Rerun the DocumentVersion, DocumentEdge and FlowGroup identity/count queries.

Passing restart evidence requires persisted wiki/Chat access and zero restart-caused duplicate publication. Transient conversation history is not expected to survive.

## 10. Capture and redact real screenshots

Capture at least one real screenshot for each:

1. production Scan state;
2. grounded Business Chat;
3. grounded IT Chat with Source Anchors;
4. exact Cross-Repository result;
5. persisted wiki/Chat access after restart.

Store screenshots under the Track C evidence directory with relative report links. Manually reject/redact any secret, credential, connection string, absolute host path, fake Backup B state or unrelated candidate output. No automated screenshot validator or hash graph is required.

## 11. Complete the Markdown report

Follow contracts/release-evidence-contract.md. The report must contain:

- passed, failed, blocked or not-run disposition;
- candidate/Story 12.4 and exact two Profiled Revisions;
- preflight and non-secret tool/provider/database identity;
- command/exit table;
- required query results;
- twelve-row manual evaluation when provider available;
- restart observation;
- five screenshot categories;
- failures and limitations.

Never turn a blocked/not-run observation into a success value. A passed report requires every retained release value.

Only an evidence-file, operator-command or read-only-query defect may be corrected inside this run, while preserving the original failure record. Any production correction beyond the accepted Prepared Scan initiation plan stops the run and returns to correct-course, fresh review and scope-hash binding before new TDD.

## 12. Final guards

    git diff --name-only 4798b97d9f23aef587829175df7ab7ebc3daf995..HEAD -- src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs src/dotnet/Vulgata.Infrastructure/Data/Configurations src/dotnet/Vulgata.Infrastructure/Data/Migrations specs/013-exact-cross-repository-flow-pass2
    git diff --check
    dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore

The forbidden-path diff must show no Track C-MVP change. Verify DI remains unchanged. Keep legacy recovery unchanged, resolve CodeGraph 1.5/JDT-LS, and do not resolve CGC. Pin path-v1 identity/packet/job-runner behavior and semantic-only v2. Prove semantic authority requires root DocumentSymbol with contained selectionRange, SymbolInformation is flat/non-authoritative, unrelated navigation is unchanged, the job runner only forwards SymbolName, and provider JSON/prompts/tools contain no symbol input. Source-literal, pinned sprint-loop and fresh-database E2E guards remain mandatory.
