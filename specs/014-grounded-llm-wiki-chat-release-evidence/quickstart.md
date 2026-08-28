# Quickstart: Story 12.5 Validation and Release Evidence

This is a validation/run guide, not planned-change authorization or implementation approval. Track A's accepted behavior/evidence is preserved and is not reimplemented or re-reviewed unless the corrected Track B work changes it. After fresh independent plan acceptance, the workflow must pause before regenerated tasks for fresh authorization of the material `IChatEvidenceRetriever` Agent-tool/runtime-contract changes; after task/definition acceptance it must obtain a new Track B implementation approval. Track C remains closed until corrected Track B is converged, quality-accepted, conditionally E2E-decided, verified and checkpoint-bound.

## 1. Re-observe the boundary

From the Story 12.5 worktree:

```pwsh
git branch --show-current
git rev-parse HEAD
git status --short
git diff --name-only 0d835c5f65f70a3c6a9561c6ff600069bb93e832...HEAD
```

Required observations:

- branch is `demo0831/story-12-5`;
- no Story 12.4 worktree path, temporary directory, qualification database, dirty file, index state, or unintegrated commit is used;
- before Track B/C, the accepted Story 12.4 final commit is present and recorded;
- `VulgataDbContext`, configurations, migrations, and model snapshot are absent from the Story 12.5 delta.

## 2. Validate plan artifacts

Parse the three JSON schemas with any draft-2020-12 validator and verify no unresolved marker exists:

```pwsh
$markers = @('NEEDS' + ' CLARIFICATION', '[FEA' + 'TURE]', '[DA' + 'TE]', '[###', 'ACTION' + ' REQUIRED', 'REMOVE' + ' IF UNUSED')
$markers | ForEach-Object { rg -n --fixed-strings $_ specs/014-grounded-llm-wiki-chat-release-evidence/plan.md specs/014-grounded-llm-wiki-chat-release-evidence/research.md specs/014-grounded-llm-wiki-chat-release-evidence/data-model.md specs/014-grounded-llm-wiki-chat-release-evidence/quickstart.md specs/014-grounded-llm-wiki-chat-release-evidence/contracts }
git diff --check -- specs/014-grounded-llm-wiki-chat-release-evidence
```

Expected: no placeholder matches and no whitespace errors.

## 3. Preserved Track A regression validation

Run production Scan-status, UI, corpus, and evidence-contract regressions to prove the plan correction did not alter accepted Track A behavior:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~ProductionScanStatusReaderTests|FullyQualifiedName~ProductionScanPageTests|FullyQualifiedName~Story125EvaluationContractTests|FullyQualifiedName~Story125ReleaseHarnessTests|FullyQualifiedName~ManualResumeApiTests"
```

Expected:

- every persisted Scan state maps to one bounded Chinese presentation;
- retry count and earliest retry, pause/failure reason, completed/total counts, and bounded failed-file summary come from persisted rows;
- only authorized recoverable paused state exposes `继续扫描`;
- hidden/missing state is indistinguishable;
- fake `DemoScanUiState` never reaches the production status reader or accepted evidence;
- corpus has 12-20 complete cases and no desired answer;
- invalid database names fail before a product connection.

## 4. Track B entry check

Do not run Track B implementation/qualification until all are true:

1. Story 12.4 final checkpoint is accepted.
2. Its final commit is integrated into this branch.
3. The integrated entities expose exact-match decision, exact current `DocumentEdge`, complete/published Flow Group, current Business Logic Document, and complete GENERATED_FROM authority without changing Story 12.4.
4. Fresh planned-change authorization, regenerated tasks/definition acceptance, and a new Track B implementation approval are bound to the revised artifact hashes.

If accepted names or shapes differ from the candidate files, adapt Story 12.5 reads to the accepted contract. If the required invariant is absent, stop and return `BLOCKED_STORY_12_4_CONTRACT`; do not edit the dependency contract.

## 5. Track B focused validation

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.cs --filter "FullyQualifiedName~ChatContractTests|FullyQualifiedName~ChatEvidencePacketBuilderTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatEvidenceCurrentnessTests|FullyQualifiedName~ChatConversationStateTests|FullyQualifiedName~ChatProviderStreamAssemblerTests|FullyQualifiedName~OpenAiCompatibleChatProviderStreamingTransportTests|FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~ChatAgentResultValidatorTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatValidatedRevealTests|FullyQualifiedName~ChatPageTests"
```

Expected:

- first observe the B1 architectural RED against the old literal-question/exact-row rebuild path; after cutover, structural inspection proves no classifier, legacy Chat authority query, alternate final-packet path, or duplicate packet-builder call remains;
- `AuthorizedChatSnapshot -> ChatLexicalPrimaryProjection -> ChatRelationshipExpansionProjection -> ChatEvidenceAssembly` is the only retrieval composition;
- Cross-Repository-literal and equivalent neutral wording preserve identical lexical primary Document/section identities, and adding/removing/staling relationship fixtures never changes those primary identities;
- exact constants pass: highest-ranked 256 distinct candidates retained after scoring (257+ is ranked, not refused), structured token equality and duplicate-conflict vectors, integer score vector `50/40/30/25/20/5/3/1` with each component applied at most once and 25 bound only to the active mode's term field, five Documents, twelve primaries or exhaustion, four sections per Document, first `min(3,count)` seeds, two additions per seed, 20 total sections, 32 anchors/relationships, ten Flow Group authorities, 12 flow steps;
- repeated identical inputs yield the same ordered evidence and packet fingerprint;
- legacy-only exact evidence is rejected, accepted v2 decision + current directed Edge is accepted;
- every relationship and Flow Group `absent`/`present-valid`/`present-inconsistent`/`selected-required` state vector passes: optional invalid facts do not poison unrelated lexical evidence, selected-required mutation refuses the whole assembly with no substitution;
- only current REFERENCES/GENERATED_FROM and Published complete Flow Groups expand one hop in exact canonical order;
- exact and +1 Document/section/anchor/relationship/flow/UTF-8 bounds pass/fail correctly;
- initial question plus two follow-ups re-fetch every row and use prior answers only as context;
- primary and the single permitted syntax repair both use the dedicated provider stream; no non-streaming provider result is accepted; neutral Track B runtime telemetry contains no candidate/run/case/signature/export field;
- decoded answer deltas of 16,384 bytes/final answers of 65,536 pass and +1 fails; separate raw-read/body and SSE line/event safety caps pass exact/+1 vectors;
- arbitrary physical reads split wire UTF-8, CR/LF, SSE fields, JSON tokens/escapes, decoded deltas, terminal metadata, and post-terminal reads without conflating counters;
- keepalives do not reset ten-second inactivity; cancellation, disconnect, terminal/framing failure, deadline, and overflow discard the buffer;
- semantic failure never repairs, no provider fragment reaches the client, and accepted Unicode reveal is immediate for reduced motion/low remaining budget;
- all accepted answer flows share one 29-second maximum deadline.

## 6. Build and full Tier 1

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
git diff --check
```

Record build errors separately from warnings. Tier 1 is not accepted with a failing test or an unreviewed shared-file/migration delta.

## 7. Tier 2 disposable PostgreSQL run

Generate a unique lowercase database beginning `vulgata_story125_`. The harness must validate the exact name and reject both the main development database and Story 12.4 qualification database before opening a product connection.

Use PostgreSQL 17, neutral Java/XML fixtures, real Git/CodeGraph/JDT LS, and a controlled provider. Exercise:

```text
clean database -> current migrations -> real analysis -> controlled provider outage
-> persisted retry/pause -> stop Vulgata process -> restart same candidate/database
-> restore provider -> authorized manual resume -> completion -> duplicate queries
```

Expected evidence: redacted exact commands plus hashes and exit codes, provider model/non-secret configuration identity, versions, durations, persisted pre/post state, explicit Scan/relationship/restart query receipts, no duplicate Document/Edge/publication, and database ownership/cleanup receipts. A database-name preflight failure must precede every product read/write.

## 8. Tier 3 real release run

Pin one clean Story 12.5 candidate plus the integrated accepted Story 12.4 commit. Run Vulgata, CodeGraph, Java 21, and JDT LS on the host; PostgreSQL 17 is the only required Docker service. Use two real sample Repositories at exact Profiled Revisions and 50-150 automatically selected File Code Units.

Accept only when the manifest proves:

- every high-value seed succeeded;
- ordinary failures stayed within the recorded budget;
- >=1 exact Cross-Repository REFERENCES Edge;
- >=1 honest Uncertainty;
- >=3 Business Logic Documents including >=1 Cross-Repository flow;
- all 12-20 fixed corpus cases ran through the declared provider disposition, >=80% were useful;
- 100% of accepted material technical claims were cited;
- zero unauthorized evidence/existence disclosures;
- every accepted provider answer used streaming, zero non-streaming provider answers were accepted, complete render finished in <30 seconds, and stream observations were content-dark;
- process restart against the same database preserved authenticated wiki/Chat access with no duplicate publication or data loss.

Before accepting those values, authenticate every Track C attestation against its immutable sealed-admission runtime observation, same-process preflight and exact candidate/run/case/request/adapter/provider/`stream=true` request/header/read/EOF binding, then recompute raw/SSE/delta/final counters and all existing aggregates. Reject a perfectly consistent SDK fake-chunk receipt solely when authorized adapter-to-sink provenance is absent. Continue requiring `ordinaryFailureCount <= ordinaryFailureBudget`, complete corpus coverage, exact half-up usefulness, and `nonStreamingAcceptedAnswerCount == 0`.

## 9. Screenshot and manifest review

Capture only real candidate-bound views. At least one screenshot is mandatory in each category: persisted Scan state, grounded Business answer/citations, grounded IT answer/Source Anchors, restart result, and release summary. Store relative paths and SHA-256 hashes in the passed-only release receipt.

Reject any screenshot containing fake Backup B state, secrets, connection strings, credentials, absolute host paths, another candidate, or unredacted diagnostics. Validate every run envelope against `contracts/story125-release-evidence.schema.json`: non-passed runs must have an honest status-conditioned result and null release receipt; a passed run must contain every command/exit-code, claim receipt, packet/result artifact, database-ownership receipt, and all five screenshot categories.

A passed schema `story125-release-evidence/2.2` envelope must also contain exactly these checks: Tier 1 `tier1-contract-ranking-relationship`, `tier1-conversation-synthesis-security`, `tier1-production-ui-corpus-evidence`, `tier1-build-full-tests`; Tier 2 `tier2-postgresql-restart-resume`; Tier 3 `tier3-candidate-tool-database-preflight`, `tier3-real-scan`, `tier3-grounded-chat-corpus`, `tier3-restart-continuity`, `tier3-screenshot-release-integrity`. Reject every omitted, duplicate, unknown, mis-tiered, or non-passed check. Resolve every check command ID to a zero-exit command in this envelope and every check artifact to identical path/hash/media type on a same-envelope command or attempted observation.

Run positive semantic vectors for sealed raw-adapter admission plus signed Track C attestation, exact/+1 per-domain limits, split wire/SSE/JSON/delta/terminal reads, one valid repair, one non-invoked case, exact aggregate agreement, and all ten checks. Run adversarial vectors for direct caller submission, altered runtime/attestation equality, foreign/unsigned provenance, the aggregate-consistent SDK fake-chunk forgery, omitted/duplicate/reordered attempts, domain/deadline/terminal/outcome/repair/validation mismatch, content-bearing artifacts, and all prior quality/coverage/check failures.

When a run fails or is blocked, preserve its commands, attempted observations, artifacts, and bounded reason. A correction creates a new run directory and `runId`, sets `supersedesRunId`, and never overwrites the earlier envelope.

## 10. Final artifact integrity

```pwsh
git status --short
git diff --name-only
Get-FileHash specs/014-grounded-llm-wiki-chat-release-evidence/plan.md,specs/014-grounded-llm-wiki-chat-release-evidence/research.md,specs/014-grounded-llm-wiki-chat-release-evidence/data-model.md,specs/014-grounded-llm-wiki-chat-release-evidence/quickstart.md -Algorithm SHA256
```

Final review must confirm one candidate, one integrated accepted dependency commit, one disposable `vulgata_story125_*` database per run, real production UI state, and explicit failures/limitations. Tests or fake state alone cannot establish release completion.
