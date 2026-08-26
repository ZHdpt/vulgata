# Quickstart: Exact Cross-Repository Flow & Pass 2

This is the implementation and qualification route implied by the accepted design. It is not authorization to generate tasks or edit production. Run all PostgreSQL checks against a disposable dedicated database; never point migration/atomicity qualification at ordinary `vulgata` data.

## 1. Definition and schema gates

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~ExactCrossRepositoryResolutionContractTests|FullyQualifiedName~Pass2BusinessLogicSchemaTests"
```

Expected:

- `business-logic/flow-v1` and both strict function declarations are closed and projectable by the same production strict-schema machinery characterized in Story 12.3;
- the only declared functions are `retrieve_code_logic` and `submit_business_logic`;
- every material output object requires admitted evidence handles and no provider-authored Document/provenance list exists;
- submission binding fixes the exact resolution decision plus its reusable document Edge, pair-specific Flow Group, aggregated provider-evidence fingerprint, consumer-entry stable key, and cross-scan Logical Document stable key/schema;
- schema/binding/ordinal/string/UTF-8/size mutations fail before graph persistence or a follow-up provider send;
- prompt text is one externalized embedded resource and its identity participates in the execution/job fingerprint.

## 2. Exact candidate extraction and resolution

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~CurrentCommunicationCandidateTests|FullyQualifiedName~ExactCrossRepositoryResolutionV2Tests"
```

Expected:

- current Pass 1 communication/deferred evidence and exact Source Anchors produce Consumers only through the closed RPC/HTTP grammar;
- current matched XML configuration is reread at the exact Profiled Revision and safe-catalog re-evaluation produces Providers;
- free-form prose, LLM output, raw graph proximity, optional direct analysis, kind-only classification, MQ/file/page/unsupported evidence produce no Provider;
- exact `(protocol, normalized endpoint identifier)` plus one distinct current provider XML Document Version creates one durable exact-match decision and creates or reuses the directed document-level REFERENCES Edge;
- duplicate locators/classifications/anchors for that same provider owner are canonically aggregated, retain their complete evidence fingerprint, and cannot manufacture `multiple_providers`;
- zero/multiple-distinct-provider/conflicting/stale/unsafe/unsupported/protocol-mismatch cases create honest Uncertainties and no guessed Edge;
- two endpoints and two consumer entries in one consumer Document targeting the same provider Document each retain separate decisions while sharing exactly one source/target/type Edge;
- both Repository directions and at least ten randomized input/evidence-multiplicity/completion orders yield byte-identical canonical decisions; two distinct provider Documents remain ambiguous.

## 3. PostgreSQL resolution transaction and replay

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~ExactCrossRepositoryResolutionPostgreSqlTests"
```

Expected:

- one complete decision batch, including every normalized provider-evidence contribution attached to its owning decision and the deduplicated Edge set, is visible or none is;
- two endpoint decisions and two entry decisions for one source/target pair persist against exactly one Edge under the existing tuple uniqueness constraint;
- a valid pre-existing same-tuple REFERENCES Edge with a non-v2/noncanonical ID is reused after full binding validation rather than duplicated or rewritten;
- exact concurrent/lost-ack replay reconstructs the same decision/Edge/Uncertainty/contribution graph and writes zero rows;
- missing, extra, changed or reordered contribution rows conflict even when a stored aggregate digest is unchanged;
- changed evidence, currentness, authorization, revision, endpoint, catalog or fingerprint conflicts without partial mutation;
- only a newer fully validated exact decision with matching stable consumer entry/protocol/endpoint may link and resolve a prior decision-owned Uncertainty; the old decision remains immutable;
- legacy one-to-one exact-resolution evidence and Uncertainty shapes remain readable, are not backfilled, and never become v2 candidates implicitly.

## 4. Endpoint-centered Flow Group planning

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~EndpointCenteredFlowPlannerTests|FullyQualifiedName~Pass2StartPostgreSqlTests"
```

Expected:

- each pair-specific Flow Group identity contains current pair/evidence bindings, while a separate `business-flow-logical-key/1.0` key contains only System, consumer/provider Repositories, protocol, normalized endpoint and the cross-version consumer-entry stable key;
- changing only pair/Scan Run/Profiled Revision/current Document/anchor coordinates preserves the Logical Document stable key when source semantic entry and endpoint are unchanged; changing endpoint or semantic entry produces a new key;
- each exact-match decision seeds its own Flow Group even when another decision shares its Edge;
- required membership is consumer Document, exact decision and its Cross-Repository Edge, provider XML Document, and one uniquely referenced provider implementation Document;
- optional evidence follows at most one current validated same-Repository REFERENCES hop; cycles/duplicates collapse deterministically;
- exact and plus-one cases prove two Repositories, eight Documents, sixteen steps, ten groups and one hop;
- incomplete/over-bound candidates retain bounded reasons and receive no job; later-than-ten candidates receive content-dark omission rows;
- atomic start creates all groups/traces/omissions/jobs/budgets or none, and exact replay writes nothing.

## 5. Closed retrieval service

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass2RetrieveCodeLogicTests|FullyQualifiedName~Pass2RetrievalPostgreSqlTests"
```

Expected:

- Search, Sections, SourceAnchors, SourceExcerpt and RelatedDocuments operate only inside the server-owned pair/System/Profiled Revisions;
- exact and plus-one cases prove eight reserved calls, ten results, four additional Documents, four sections per Document and 65,536 returned UTF-8 bytes;
- reservation precedes I/O, crash/lost receipt consumes a call, exact call replay does not double-charge, and a later duplicate request charges results/bytes consistently;
- stale, superseded, unauthorized, cross-System, revision/hash/range mismatch, second-hop and hidden content return indistinguishable closed refusal;
- receipts/results persist only identities/fingerprints/counts and no query/source/section/endpoint/provider content;
- retrieval suggestions cannot change the exact provider, Edge, Flow Group key, membership or trace.

## 6. Strict streaming Worker protocol

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreparedPass2StrictWireTests|FullyQualifiedName~PreparedPass2JobExecutorStreamingTests"
```

Expected:

- fake OpenAI-compatible wire proves streaming inactivity/nonresetting absolute caps, request/raw/assembled bounds, zero SDK retry, disabled-thinking/coercion policy and sanitized failures through reused Story 12.3 helpers;
- ordinary turns declare exactly two functions; retrieve-only batches preflight wholly and callbacks run serially only after acceptance;
- request nine is named submission-only, request ten is the sole local correction, and no request eleven is sent;
- mixed/unknown/empty/duplicate/over-budget batches invoke zero callbacks;
- accepted candidates are <=65,536 UTF-8 bytes and initial packets <=131,072 UTF-8 bytes;
- transcript reconstruction never leaks historical reasoning/tool arguments/provider payload and retrieval budget survives retry/restart.

## 7. Validation, provenance, atomic commit, and replay

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass2BusinessLogicValidationTests|FullyQualifiedName~Pass2BusinessLogicCommitPostgreSqlTests"
```

Expected:

- every claim handle maps to an admitted current group/retrieval fact and the per-section/global used Document sets are derived locally;
- every and only actually used distinct Code Logic Document Version receives one GENERATED_FROM Edge;
- grouped/eligible/returned-but-uncited Documents receive none, while every cited additional Document is revalidated immediately before commit;
- changed decision/provider/endpoint/Edge/membership, substitution of another decision sharing the Edge, unsupported relationship, stale or unauthorized handle, revision/hash drift, uncited material, or incomplete provenance writes no Document/Edge/completion state;
- injected failure at every boundary exposes the complete immutable Document/sections/index/provenance/group/job/current-pointer graph or none;
- exact concurrent/lost-ack/restart replay returns one existing Document Version with zero mutation; changed replay conflicts;
- a later scan pair for the same semantic flow uses the existing consumer-owned Logical Document, appends/supersedes through one Version Chain, and advances its concurrency-protected current pointer;
- concurrent cross-pair creation/publication yields one Logical Document slot; losers reload/revalidate the winner's pointer before any successor append;
- changed endpoint/entry creates a distinct Logical Document and leaves the prior chain unchanged; previous version bodies/provenance remain immutable.

## 8. Execution, API, recovery, and mixed outcomes

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass2ExecutionTests|FullyQualifiedName~Pass2ExecutionApiTests|FullyQualifiedName~Pass2ProviderFailurePolicyTests"
```

Expected:

- API behavior is 401/403/ownership-filtered 404 with ProblemDetails conflicts and sanitized status/count/warning responses;
- activation is a wake-up only, claims are serial in canonical group order, and one Flow Group has at most one active job;
- one durable job-local retry, lease expiry, acknowledgement loss, host restart and manual resume preserve identities, budgets and published Documents;
- provider circuit/probes/pause reuse Story 12.3 policy and make no source-bearing probe;
- mixed success/failure/incomplete/bound-excluded/omitted runs account for every candidate, retain successful Documents, create no failed-group Document, and never rewrite resolution facts;
- no complete groups produces zero Business Logic Documents and truthful warning completion.

## 9. Migration lifecycle

Create a disposable database from an explicit administrative connection, apply a unique temporary database name, and delete only that verified disposable database after qualification.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass2MigrationTests|FullyQualifiedName~Pass2PostgreSqlConstraintTests"
```

Expected:

- one migration applies to fresh and legacy Story 12.1-12.3 fixtures;
- legacy one-to-one exact evidence, Uncertainties, Documents, attached Flow Groups and prepared-demo Pass 2 rows remain readable without inferred decision backfill;
- v2 prepublication Flow Groups allow a null Business Logic Document only in valid v2 states, persist a unique exact-decision FK plus deliberately reusable Edge FK, and retain both execution-instance and cross-scan stable identity fields;
- the existing unique `(SourceDocumentVersion, TargetDocumentVersion, References)` Edge index remains unchanged and permits many decision FKs to one Edge;
- the existing unique `(Repository, BusinessLogic, StableKey)` Logical Document index and current-pointer concurrency token prevent duplicate semantic slots without rekeying legacy rows;
- all FKs, unique keys, state-shape checks and legacy/v2 mutual-exclusion constraints hold under competing writers;
- rollback/reapply succeeds for clean and legacy-only fixtures with no v2 prepublication rows;
- EF reports no pending model changes.

## 10. Production composition and exact two-Repository qualification

Run only after fake-wire, schema, migration, PostgreSQL, and source-free provider capability gates pass. Use the approved disposable database, two approved Repository roots and exact revisions, and the configured provider boundary. Stop and report environment/configuration/network/provider failures without weakening contracts or changing the host.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass2ProductionBoundaryQualificationTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass2ProductionExecutionQualificationTests"
```

Expected:

- the production DI graph connects authenticated activation to candidate extraction, resolution, planner, durable jobs, strict two-function Worker, retrieval, validator, committer and terminal accounting;
- a source-free strict capability gate uses the exact production declarations/wire and retains only sanitized hashes/sizes/flags;
- one approved Story 12.3 handoff produces at least one exact Custom RPC Cross-Repository REFERENCES Edge, one complete Flow Group, one live-provider Business Logic Document Version, and complete all-and-only-used GENERATED_FROM provenance;
- every relationship claim traces through a used current Code Logic Document Version to its Source Anchors;
- the same deterministic resolution/group result is obtained with direct Pass 2 JDT-LS/CodeGraph disabled;
- no provider payload, source, secret, endpoint text, host path, prompt body, tool argument, or database connection is retained in qualification evidence.

## 11. Regression and final barrier

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~ExactCrossRepository|FullyQualifiedName~Pass2"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~DurableScanning|FullyQualifiedName~PreparedScanning|FullyQualifiedName~KnowledgeStore|FullyQualifiedName~AnalysisRuntime"
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
git diff --check
```

Report focused and full results separately. Report a zero-error build separately from warnings. A known unrelated baseline failure must be named exactly and cannot be presented as Story 12.4 success. Do not start Story 12.5 Chat/UI/release-evidence work from this quickstart.
