# Quickstart: Validate Exact Cross-Repository Resolution

This is the implementation validation guide for Story 11.4. It does not run the final prepared production Repository rehearsal or final Docker build; Story 11.5 owns those gates.

## Prerequisites

- .NET 10 SDK and project dependencies already restored.
- Git CLI available with credential prompts disabled in tests.
- PostgreSQL 17 available for migration/concurrency verification.
- Pinned Vulgata source boundary `67b5eaf45ff3c3348e240e323bed6f80c9e885e0` plus the Story 11.4 implementation under review.
- Two disposable local Git fixtures created by tests, never the production `mbs_account`/`mbs_channel` working directories.
- Runtime fixture has one authorized System, two managed Repositories, exact alias bindings, two ScanRuns/ScanExecutions at Pass1Completed, current Code Logic Document Versions, and Source Anchors.

The reviewed profile artifact [contracts/demo-resolution-profile.v1.json](contracts/demo-resolution-profile.v1.json) contains these immutable ref/commit facts and the observed exact/unsupported selectors:

```text
mbs_account  refs/tags/vulgata/August15th-sample  451b3a99b179b711ae66260651eb6b12db482e0b
mbs_channel  refs/tags/vulgata/August15th-sample  d2e11bacd157195c421065292674ab0c29260081

exact occurrence:
  mbs_account -> mbs_channel
  UpdateChannelSessionOp.java:45, netTools.execute, endpoint argument 1
  consumer raw cust/updateSession
  disgram/in/cust/updateSession.xml:2, trans@name
  provider raw in/cust/updateSession
  UpdateSessionOp.java, type cn.com.yitong.actions.domain.login.UpdateSessionOp

unsupported observation:
  account-file-storage-oss-upload-v1, file-storage
  UploadOcrToOssOp.java:66
  ossClientService.simpleUploadToOSSBase64String
```

The reviewed JSON is the sole selector authority. Do not substitute implementation-time selectors or place database GUIDs, Git URLs, host/WSL/container paths, credentials, or any endpoint not present in the pinned objects in the profile.

## 1. Static boundary inspection

```pwsh
git diff --check
git diff -- specs/007-exact-cross-repo-resolution AGENTS.md
rg -n "mbs_account|mbs_channel" src/dotnet
rg -n "451b3a99b179b711ae66260651eb6b12db482e0b|d2e11bacd157195c421065292674ab0c29260081" src/dotnet
rg -n "IQueryable|DbSet" src/dotnet/Vulgata.Core
```

Expected:

- diff whitespace check succeeds;
- Repository aliases/commits occur only in the embedded profile/tests/docs, not Repository-specific production conditionals;
- Core exposes no EF types;
- no task/code evidence is expected during the planning-only stage.

## 2. Profile and runtime binding contract

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~ExactResolutionProfileTests
```

Required predicates:

- accepted schema/resource loads with stable SHA-256 fingerprint;
- the exact JSON bytes match the reviewed account->channel occurrence and unsupported observation contract;
- unknown/duplicate/trailing/over-depth/oversized fields fail;
- missing/duplicate/extra aliases, same Repository twice, cross-System binding, wrong ScanRun/ScanExecution ownership, wrong profile fingerprint, and insufficient scope fail before Git;
- absolute/UNC/WSL/container paths, Git URLs, persistence IDs in profile, traversal, unsafe refs, broad/unconfigured files, and credentials fail;
- a separate contract fixture may reverse Repository roles; no permanent role field exists;
- a static scan finds zero database IDs, Git URLs, credentials, environment variables, and absolute host/WSL/container paths in the profile.

## 3. Exact Git object acquisition

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~ExactGitRevisionAcquisitionTests
```

Required predicates:

- annotated and lightweight full refs peel to the exact configured commit inside an operation-private exact Git root;
- exact snapshot Profiled Revision equals the full commit even when ordinary managed `HEAD` differs;
- the ordinary managed path, attached branch, `HEAD`, origin, index, and clean status are unchanged after exact acquisition, and a following normal `AcquireAsync` succeeds;
- normal acquisition can run concurrently with two exact leases; cancelling one and disposing the other cleans only their private roots and normal acquisition still succeeds;
- moved/missing/similarly named tag, abbreviated/wrong commit, remote failure, archive/identity failure, cancellation, and cleanup failure expose no usable lease/source/write;
- output/logs/errors do not disclose Git URL, credential, host path, or raw stderr;
- external production Repositories are never mutated.

## 4. Deterministic extraction and matching

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~ExactResolutionExtractionTests|FullyQualifiedName~ExactCrossRepositoryResolverTests"
```

Required positive predicates:

- pinned account `netTools.execute(ctx, "cust/updateSession")` at line 45/argument 1 and pinned channel `trans@name="in/cust/updateSession"` at XML line 2 are decoded by the closed grammars;
- exact provider `UpdateSessionOp.java` type selector maps to one current provider Document Version/Source Anchor;
- `custom-rpc-in-prefix-v1` requires/removes only the provider `in/` prefix and ordinal comparison yields observed `cust/updateSession`;
- direction plus target binding selects exactly one provider;
- mapping selects one current consumer and provider Document Version/Source Anchor;
- the reviewed direction succeeds, and a separate synthetic contract fixture proves the same schema supports the reverse direction;
- same endpoint names in both Repositories do not self-match or reverse-match.

Required negative predicates (all create zero Edges):

- variable/symbol/computed/concatenated/template target;
- unconfigured call symbol/argument/file;
- malformed literal/escape/XML, DTD/external entity, missing/duplicate `name`;
- provider file outside `src/main/resources/disgram/in/`, traversal, symlink/reparse;
- unmatched or differently normalized identifier;
- zero/multiple eligible providers or zero/multiple Document/Source Anchor mappings;
- Pass 1 narrative, LLM output, or System context without exact evidence;
- the legacy global kind-only profile shape is rejected at load time and never becomes a resolution fixture.

The reviewed `account-file-storage-oss-upload-v1` input is the positive unsupported predicate: its exact owner Document selector, line 66 member invocation, and safe locator map to one authorized current Document Version/Source Anchor; it records nonterminal `unresolved`/`unknown_target`, `reason_code=unsupported_kind`, `observation_kind=file_storage`, and zero Edges. It remains eligible for later full-V1 file/storage resolution/reclassification; Story 11.4 never assigns human-terminal `wontfix`. Zero/multiple/stale/unauthorized owner mappings write nothing.

## 5. Persistence, idempotency, authorization, and isolation

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~ExactCrossRepositoryPersistenceTests|FullyQualifiedName~ExactCrossRepositoryAuthorizationTests"
```

Required predicates:

- exact match atomically commits one cross-Repository REFERENCES Edge and one evidence row;
- relational rows retain the Edge/Repository/ScanRun/Document Version/Source Anchor PK/FKs required for ownership, referential integrity, idempotency, and authorized joins; textual evidence fields retain both raw identifiers, normalized identifier, protocol, profile schema/id/version/fingerprint, occurrence, `1.0000` confidence, and safe locators without embedding those internal IDs;
- inexact/ambiguous anchored outcomes commit one Uncertainty using only canonical `cross_repo`, `unknown_target`, or `ambiguous` sub-status and zero Edges;
- the reviewed unsupported observation commits exactly one owner-anchored nonterminal `unresolved`/`unknown_target` Uncertainty with closed reason/kind and zero Edge/evidence rows, and remains eligible for later resolution/reclassification;
- binding/authorization/revision failures write neither Edge nor Uncertainty;
- identical success/warning/unsupported retry returns `Already*` with no duplicates or timestamp mutation;
- drifted replay conflicts and preserves original rows;
- competing current-version change causes rollback; no stale/partial Edge remains;
- lacking either Repository authorization causes zero Git/source/query/write and restricted retrieval exposes only the identity-free, metadata-free marker;
- authorized exact retrieval exposes only opaque stable `relationshipIdentity` plus safe endpoint/profile/Evidence Locator metadata, not Repository/ScanRun/Document Version/Source Anchor PK/FKs;
- profile/textual-evidence/log/exception/diagnostic scans find no serialized internal IDs, plaintext source, credential, Git URL, absolute path, or provider diagnostic; unauthorized returned-model scans find no relationship identity, endpoint, evidence, Repository, or business data;
- direct relational assertions separately prove the required internal PK/FK columns and composite ownership constraints are present.

## 6. Migration lifecycle

Use a uniquely named disposable PostgreSQL database. Never target a shared or production database.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~ExactCrossRepositoryMigrationTests
dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
```

Required migration sequence:

1. clean apply through the Story 11.4 migration;
2. verify two new tables, SourceAnchor alternate key, owner-safe FKs, closed checks, unique fingerprints, and indexes;
3. roll back exactly to `20260801033655_AddSequentialScanRunner`;
4. prove only Story 11.4 schema is removed and all predecessor schema/data remains;
5. reapply and verify identical model;
6. pending-model command exits 0.

## 7. Current-boundary integration

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~ExactCrossRepositoryIntegrationTests
```

Expected sequence:

```text
two authorized explicit bindings
-> first execution Pass1Completed, second not ready: WaitingForPass1 and zero writes
-> both Pass1Completed at exact commits
-> exact committed snapshots
-> pinned consumer dispatch candidate + pinned provider endpoint candidate
-> one exact cross-Repository REFERENCES Edge + evidence
  -> one pinned unsupported observation -> one nonterminal unresolved/unknown_target Uncertainty + zero Edges
  -> authorized trace uses opaque relationshipIdentity plus safe metadata
  -> unauthorized trace returns only RestrictedRelationship
-> identical retry produces no duplicate truth
```

Run a separate synthetic fixture for the reverse direction and run the complete negative matrix. Do not claim final clean-database prepared two-Repository or Docker proof here.

## 8. Build and regression

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
```

Expected: exit 0, no analyzer warnings/errors, no predecessor regression, and no deliberately failing deferred test.

## Handoff to Story 11.5

Story 11.5 must still run:

- one clean PostgreSQL database with the actual prepared Repository management records;
- exact two-Repository profile binding and pinned commit acquisition;
- complete profiling, Pass 1/Pass 2, exact Edge, retrieval/chat/citations path;
- final Docker image build and release verification.

Story 11.4 evidence cannot be substituted for those final gates.
