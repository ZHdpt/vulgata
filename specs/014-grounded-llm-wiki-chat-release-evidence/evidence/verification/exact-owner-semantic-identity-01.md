# T048 exact-owner semantic-identity verification

**Candidate:** `e7a4cb10bec4037eb8fe5576423b004a00d7847c`
**Story:** 12.5 Track C exact-owner correction
**Verification date:** 2026-08-31
**Classification:** `BLOCKED_ENVIRONMENT` (the PostgreSQL exact-owner/retryability vectors now pass; required real JDT-LS qualification remains environment-failed)

## Bound artifacts

The candidate specification and plan are unchanged and hash as follows:

| Artifact | SHA-256 |
| --- | --- |
| `specs/014-grounded-llm-wiki-chat-release-evidence/spec.md` | `6da3e251b217ab8dd8190f84c4cf864b994be1e87090558a29e125f5648ec8b7` |
| `specs/014-grounded-llm-wiki-chat-release-evidence/plan.md` | `fd7feea32f1e7a992e0b6e1fdfe0aabdd380968c842bb67bd3ee1794e4373d1d` |
| `specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md` | `527b238ef06d71ca613016c00ff6dc7cca8942ca844e5bdde75d355051aa75f9` |

Story 12.4 commit `4798b97d9f23aef587829175df7ab7ebc3daf995` is ancestral to the candidate. The candidate implementation commit contains the accepted 18-file production manifest only: the two Prepared Pass 1 Agent files; seven Core files; six Infrastructure files; and the two Web files (`PreparedPass1JobRunner` and `PreparedScanPairBuilder`). No `Program.cs`, EF migration, `VulgataDbContext`, model snapshot, Story 12.4, Track A/B, CGC, legacy profiling, or provider-visible contract path was changed by that commit. The working-tree `.env.example` modification is pre-existing/unrelated and was not touched.

## Executed verification

All commands were run from the repository root with `--no-build --no-restore` after a successful Web build. Counts are xUnit totals.

| Boundary | Command/filter | Result |
| --- | --- | --- |
| Build | `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore -p:WarningLevel=0` | PASS, 0 errors; 9 existing NU1507/MSB3277 warnings |
| T043 raw protocol/JDT authority | `JdtLsAdapterTests` authority vector, `ReviewedExactProfileSeedAuthorityTests` matched-owner vector, `PreparedScanBoundaryTests` provenance vector | 3 passed, 0 failed, 0 skipped |
| T044 split/selection | `PreparedJavaSplitRangeTests|PreparedFileCodeUnitAndSelectionTests` | 11 passed, 0 failed, 0 skipped |
| T045 handoff/dispatch/executor/provider shape | `PreparedHandoffProjectionTests|Pass1ProductionCompositionTests|PreparedPass1WorkerDispatcherTests|PreparedPass1JobExecutorStreamingTests|Pass1DeepSeekPromptCompositionTests` | 157 passed, 1 failed, 0 skipped; the only failure was environment composition `jdt_distribution_archive_missing` while constructing the production JDT-LS service, before the assertion |
| T046 PostgreSQL exact-owner/retryability vector | `ReviewedExactProfileSeedPostgreSqlTests.PostPreparedScan_MatchedSemanticOwnerFailsBeforePublication_AndNonmatchingInputRemainsByteNeutralAsync` | 1 passed, 0 failed, 0 skipped with the sanitized compose-fallback administrator connection; HTTP 422 and retryable `(2,2,1,0,0,0,0,0,0,0,0)` tuple |
| Non-database surrounding regression | `PreparedScanInitiationCoordinatorTests|PreparedScanInitiationIdentityTests|PreparedScanBoundaryTests|PreparedXmlSecurityTests|PreparedXmlSplitRangeTests|PreparedCodeGraphReadinessTests|PreparedPass2FlowTests|ExactResolutionContractTests|ChatPipelineCompositionTests|ChatProviderStreamAssemblerTests` | 107 passed, 0 failed, 0 skipped |
| Mixed initiation/scan/chat regression | same non-DB set plus `PreparedScanInitiationApiTests` and `PreparedChatIntegrationTests` | 121 passed, 13 failed, 0 skipped; 11 failures stopped at the required PostgreSQL admin prerequisite and 2 were existing Data Protection key-path access failures |
| Real CodeGraph/JDT qualification | `PreparedScanRealQualificationTests|JdtLsRealQualificationTests` | 5 passed, 4 failed, 0 skipped; 3 `jdt_distribution_archive_missing`, 1 `jdt_request_timeout`, and no assertion reached a prepared release result |
| Workflow | `uv run --offline --cache-dir "$env:TEMP\\vulgata-story125-uv-cache" --with PyYAML .agents/skills/run-bmad-sprint-loop/scripts/validate_workflow.py --repo .` | PASS: Workflow configuration and contracts valid |
| Git hygiene | `git diff --check` | PASS |

The exact claimed-source vector was also rerun with the same sanitized local administrator mapping:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~Pass1ClaimedPacketExecutionTests.CurrentClaim_HydratesServerOwnedPathOrSemanticSymbolNameAfterExactRefetchAsync" --logger "console;verbosity=minimal"
```

It passed **1/1**, 0 skipped. The fixture completed and cleaned its generated database; no connection string, credential, host, database name, or secret was printed or persisted.

## Covered product boundary

The runnable non-database tests cover the accepted raw DocumentSymbol authority, cached-observation and split/selection behavior, exact-owner projection, path-v1 neutrality, semantic-v2 identity/replay contracts, packet/job-runner/dispatcher/executor revalidation, provider-shape exclusion, prepared initiation composition, XML safety, Pass 2 contract behavior, and Track A/B streaming/composition regressions. The T045 failure occurs during environmental JDT-LS service construction; it does not expose a provider-visible `SymbolName` or a semantic-owner assertion failure.

The required no-preseed PostgreSQL vertical remains unproven: initiation replay/concurrency/authorization, exact semantic-owner File Code Unit and Source Anchor publication, exact Edge/Uncertainty outcome, and retry/`FailedPass1` behavior cannot be accepted from SQLite or direct unit tests. The targeted PostgreSQL fail-closed/retryability and claimed-source vectors now run; T049's positive exact-publication qualification remains pending. The real JDT-LS qualification remains unresolved because the configured distribution archive/readiness is unavailable or timed out in this environment. This JDT-LS archive/readiness issue is the remaining blocker for prepared real qualification; the two Data Protection key-path failures are known sandbox-only regression noise and are not exact-owner product failures.

## T048 disposition and handoff

T048 is not technically accepted. The PostgreSQL administrator prerequisite is now resolved for the targeted T046 vectors, which pass, but T049's positive qualification still requires its own fresh isolated database run. The missing/unstable JDT-LS distribution remains the blocker for real prepared qualification. Do not mark T048 complete or begin T049 with a substitute database, run-03 state, fake evidence, or SQLite. Resume with a verified pinned JDT-LS archive/readiness, then rerun the exact T048 qualification commands and record the results before advancing to T049.
