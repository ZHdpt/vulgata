# Quickstart Validation: Lightweight Java/XML PreScan & Prepared Scan Manifest

This is the post-implementation validation boundary, not task or implementation authorization.

Validation is bound to spec SHA-256 `c15d03b2b10e4eaa452a59e3b96acafdbb04160de556ca93c28380117a085ad3`, checklist SHA-256 `070affb70472c8d264cdf06ec5e06412e0ef06119bb7be388ce385351a5d8150`, accepted decision `story-12-2-definition-review-fifth-correction-authorization-01`, and raw definition-review attempt 4 `REVISE`.

## Prerequisites

- Windows host with .NET 10, approved `@colbymchenry/codegraph@1.5.0`, Java 21, JDT LS `jdt-language-server-1.61.0-202608141332`, and Story 12.1 runtime checksums.
- PostgreSQL 17 available as the only required Docker service.
- Neutral exact-Git fixture containing safe/unsafe Java/XML, every catalog grammar/prohibition/bound, deterministic split/fallback cases, and at least 50 paired eligible units for release-bound validation.

## 1. Reconfirm accepted inputs

```pwsh
git rev-parse HEAD
Get-FileHash specs/011-java-xml-prescan-manifest/spec.md -Algorithm SHA256
Get-FileHash specs/011-java-xml-prescan-manifest/plan.md -Algorithm SHA256
```

The accepted implementation candidate may advance HEAD; the accepted spec/plan hashes must be recorded by the workflow before tasks.

## 2. Focused deterministic/safety suites

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~PreparedScanning'
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~PreparedScanAuditContractTests'
```

Expected: deterministic component suites pass. Task evidence must show this exact order: safe XML public-seam RED → safe projection/range-index GREEN → XML split RED → XML split GREEN consuming `SafeXmlDirectChildRangeSet`. Only then may any US4 planner/manifest/store/coordinator/API consumer or XML-observing qualification run. Separately, `PreparedScanAuditContractTests` must first be RED for canonical outcomes/order/reasons/DetailFingerprint bytes/UUID/replay mutation, then GREEN through the sole `PreparedScanAuditFactory` production owner, followed by the surrounding PreparedScanning suite. No alternate XML range or audit identity constructor exists.

## 3. Canonical-envelope and independent replay proof

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~PreparedCanonicalEnvelope|FullyQualifiedName~PreparedScanAtomicReplayTests'
```

Expected: literal golden vectors pass and task-owned `PreparedScanAtomicReplayTests` executes exact contract replay. Audit replay includes selected/nonselected shapes and mutations of FileCodeUnitIdentityFingerprint, PairSelectionOrdinal, selected association, nullability, stored DetailFingerprint, and same-path multi-segment rows. This suite does not replace the separately mandatory PostgreSQL command in section 5.

## 4. Real Git and analysis qualification

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~ExactGitRevision|FullyQualifiedName~CodeGraphCliQualification|FullyQualifiedName~JdtLsRealQualification|FullyQualifiedName~PreparedAnalysis'
```

Expected: private full-ref acquisition survives ordinary-checkout/ref movement; CodeGraph complete proof is revision-bound and under 30 seconds; Java 21/JDT-LS readiness returns symbols under 60 seconds; XML-only records NotApplicable; malformed/stale/wrong-runtime/path/binding/completeness/readiness failures and timeouts approve nothing. Only after readiness succeeds, unsupported/failed/timed-out typed related-file expansion yields one bounded Warning and identical direct order.

## 5. PostgreSQL migration/atomicity/replay

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~PreparedScanMigrationPostgreSqlTests'
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~PreparedScanAtomicPostgreSqlTests'
```

Expected: migration creates the prepared-only manifest-entry table with non-null principal keys, manifest/coordination binding, optional selected-audit composite FK, and no alternate key over nullable CodeUnit fields; apply/rollback/reapply and no-pending-model checks pass; historical nullable CodeUnits remain readable. `PreparedScanAtomicPostgreSqlTests` reconstructs tags 1-10, rejects entry/FK/preimage/digest mutations including entry-versus-recomputed-CodeUnit fingerprint mismatch, and proves prior atomicity. Record discovered counts as mandatory pre-final evidence.

## 6. Authorization/API contract

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~PreparedScanApi|FullyQualifiedName~PreparedScanAuthorization'
```

Expected: correct 400/401/403/404/409/422/503 ProblemDetails behavior; out-of-scope existence is undisclosed; authorization is checked before Git and immediately before commit; response names both ordinal scopes and exposes stable CodeGraph/Java proof identity hashes plus manifest/pair fingerprints, but no durations, telemetry, source/host/tool output, and starts no Worker.

## 7. Three-run pair release qualification

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~PreparedScanReleaseQualificationTests'
```

On the documented Windows host with PostgreSQL only in Docker, execute three complete paired preparations of unchanged inputs; the third reverses both Repository order and every candidate sequence. Compare every SC-001 output: paths, audits/reasons, file/source hashes, ranges, classifications, scores, selected identities, both ordinal domains, proof identities, prepared-context identity, manifest fingerprints, pair-selection fingerprint, and pair-package fingerprint. All must be byte-identical. Record full PreScan elapsed time separately for each Repository on each run; every Repository must finish below two minutes. Record CodeGraph <30 seconds and JDT-LS <60 seconds as separate sub-timings, not substitutes for full PreScan timing. Also prove the selection golden cases and 50-150/unit bounds.

This suite is ineligible until XML split GREEN and every US4 consumer GREEN are recorded. It must not be scheduled in parallel with safe XML, XML split, audit construction, or consumer implementation.

## 8. Boundary/security inspection

```pwsh
rg -n "CodeGraphContext|navigate_code|WorkerAgent|DocumentVersion|REFERENCES|BackupB" src/dotnet/Vulgata.Core src/dotnet/Vulgata.Infrastructure src/dotnet/Vulgata.Web
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~PreparedScanBoundary|FullyQualifiedName~PreparedXmlSecurity'
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~PreparedScanProductionLiteralQualificationTests'
```

Expected: no new Story 12.2 composition depends on CGC or demo state; no prepared path executes Workers/LLMs or creates jobs/Documents/Edges/Uncertainties. `PreparedScanProductionLiteralQualificationTests` inspects production assemblies, embedded resources/catalogs, and public-seam output against a denylist derived from neutral/release fixture Repository names, canonical paths, endpoint strings, and fixture line-number literals. It writes `artifacts/qualification/story-12.2/production-literal-inspection.json` containing inspected artifact identities, denylist hash, match details, and a zero-match verdict; test/fixture binaries are excluded from inspection.

These are post-GREEN nonbehavioral qualifications. A literal match records exact literal/category/location, then routes to new `PreparedScanProductionLiteralRegressionTests.cs` behavioral RED plus the named production owner; the qualification itself is not edited into GREEN. Prepared-context/handoff projection tests remain behavioral RED/GREEN and must never be classified in this non-RED group.

## 9. Single final verification barrier

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
git diff --check
```

This is the only final verification task and is physically last. Before running it, require: production-literal JSON zero-match artifact; PostgreSQL migration/atomic-replay discovered counts and evidence; workflow/pin plus boundary/security qualification record; three-run release evidence; and all behavioral GREEN evidence. These prerequisite owners may complete in parallel after behavioral GREEN, but none may run after or race this section.

Report build errors separately from warnings, exact test counts, PostgreSQL/tool timing evidence, and any locked cleanup artifact. Application-process restart evidence belongs to later release verification; Story 12.2 acceptance stops at durable prepared state and approved handoff with zero dispatch.

Final quickstart/build/full-test verification is the last task of the correction graph. Its prerequisites include safe XML/XML split/audit/US4 GREEN, production-literal, PostgreSQL migration/atomicity/replay, workflow/security, and three-run release qualification. No task may follow it.
