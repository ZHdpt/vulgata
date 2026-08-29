# Quickstart: Story 12.5 Track C-MVP

This guide validates the corrected definition and describes the single combined host integration/release run. It does not create approval, authorize production changes, or permit use of the main/Story 12.4 database.

## 1. Re-observe the pinned boundary

    git branch --show-current
    git rev-parse HEAD
    git rev-parse --git-common-dir
    git status --porcelain=v2
    git merge-base --is-ancestor 4798b97d9f23aef587829175df7ab7ebc3daf995 HEAD
    Get-FileHash specs/014-grounded-llm-wiki-chat-release-evidence/spec.md -Algorithm SHA256

Expected before implementation: branch demo0831/story-12-5-trackC; Story 12.4 is ancestral; only explicitly owned corrected-definition files are dirty.

## 2. Validate corrected plan artifacts

    rg -n "attestation sink|ten-check|separate Tier 2|live outage|release-summary product" specs/014-grounded-llm-wiki-chat-release-evidence
    git diff --check -- specs/014-grounded-llm-wiki-chat-release-evidence

Expected: no unresolved placeholder; removed mechanisms appear only as explicitly deferred/historical material; diff check passes.

## 3. Tier 1 regression

Run the current focused corpus, database guard, Prepared Chat, streaming, authorization/currentness, citation, publication/reset and production Scan UI suites selected by regenerated tasks. The baseline commands remain:

    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~Story125EvaluationContractTests|FullyQualifiedName~Story125Tier2PostgreSqlTests|FullyQualifiedName~Story125ReleaseHarnessTests"
    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedChatIntegrationTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatEvidencePacketBuilderTests|FullyQualifiedName~ChatProviderStreamAssemblerTests|FullyQualifiedName~OpenAiCompatibleChatProviderStreamingTransportTests|FullyQualifiedName~ChatPageTests|FullyQualifiedName~ProductionScanStatusReaderTests"
    dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore

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

## 6. Run the existing product path

Start Vulgata on the Windows host with the owned database:

    dotnet run --project src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-build

Through the authenticated existing UI/API:

1. select one authorized System and exactly two real Java/XML Repositories;
2. prepare both Repositories at the recorded Profiled Revisions;
3. verify real CodeGraph index and JDT LS Java readiness plus safe XML catalog evidence;
4. start the accepted Pass 1 path and allow it to reach terminal state;
5. run exact Cross-Repository Resolution and the accepted Pass 2 path;
6. use 继续扫描 only for an eligible durable paused state;
7. observe terminal Scan state in the production Chinese management UI.

Do not invoke or copy a Story 12.4 qualification database/harness. Do not change selection, scheduling, retry, dispatch, XML or relationship behavior.

## 7. Run straightforward read-only queries

Use psql or an equivalent read-only client against only the owned database. Record sanitized SQL/description, exit result and bounded rows for:

1. two approved PreparedScanManifests and total selected PreparedScanManifestEntries (50-150);
2. exact Repository/Profiled Revision and real CodeGraph/JDT LS/catalog bindings;
3. terminal Pass 1/Pass 2 executions/jobs, explicit high-value-seed selected/succeeded/failed counts, failed count zero, selected count equal to succeeded count, and ordinary failure budget;
4. current Code Logic and Business Logic DocumentVersions with SourceAnchors;
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

Only an evidence-file, operator-command or read-only-query defect may be corrected inside this run, while preserving the original failure record. If any correction requires production source, stop: the no-production implementation approval is invalid, and work returns to plan, fresh downstream review and scope-hash rebinding before any TDD task may start.

## 12. Final guards

    git diff --name-only 4798b97d9f23aef587829175df7ab7ebc3daf995..HEAD -- src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs src/dotnet/Vulgata.Infrastructure/Data/Migrations specs/013-exact-cross-repository-flow-pass2
    git diff --check
    dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore

The forbidden-path diff must show no Track C-MVP change. Then run the pinned sprint-loop verification and required E2E gate. Human preauthorization does not replace technical acceptance.
