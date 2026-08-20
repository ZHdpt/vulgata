# Story 12.2 Workflow and Security Qualification

- Qualified at: `2026-08-20T15:01:49Z`
- Branch: `demo0831/story-12-2`
- Qualification checkpoint: `24bb73e6ad4cf2c2a48168e6f57ba97ad74eecaf`
- Accepted definition boundary: `e997c182a466694bc5eab405bb68fc7396edee53ce7559f443bcbb2e7beaefb6`
- Definition verdict: `ACCEPT`, `definition-review-05.json`
- Implementation approval: `story-12-2-implementation-approval-01`, `Accepted`

## Ownership normalization

Per nonblocking definition-review finding DR5-001, T048 exclusively owns the
executable `PreparedScanBoundaryTests.cs` qualification. T051 owns this durable
evidence record only. T051 made no test or production-code change, so the two
tasks have no duplicate code-file ownership.

## Executable T048 evidence

Command:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedScanBoundaryTests" -v:q
```

Result: `4 passed, 0 failed, 0 skipped` in one matched test assembly. The build
and test process completed with exit code 0 and zero errors. Existing repository
warnings were emitted separately, including NU1507 package-source mapping,
MSB3277 EF Core assembly-version conflicts, and existing compiler/analyzer
warnings.

The four executable checks prove:

1. `PreparedScanCoordinator` depends only on repository visibility, the closed
   prepared pair builder, the prepared store, and the approved prepared handoff
   source. It has no LLM, Worker, Scan Job Execution, Document, resolution,
   Chat, demo, or CodeGraphContext dependency.
2. The compiled prepared public type graph exposes no Worker/dispatch,
   ScanJobExecution, Document/final Edge/Uncertainty, Cross-Repository
   Resolution, Pass 2, Chat, Backup B, CodeGraphContext, or LLM-provider type.
3. All 28 matching Story 12.2 prepared production source files contain zero
   forbidden invocation, construction, or authority tokens. This includes
   `navigate_code` and the named excluded types.
4. Workflow approval and runtime pins match the accepted boundary: Windows-host
   CodeGraph `@colbymchenry/codegraph-win32-x64@1.5.0`, Java 21, JDT LS
   `jdt-language-server-1.61.0-202608141332`, StreamJsonRpc `2.25.29` over the
   accepted stdio contract, and PostgreSQL `17-alpine` as the pinned Docker
   database service.

## Independent bounded scans and pin checks

The production scan enumerated `src/dotnet/**/*.cs` and selected filenames with
the accepted Story 12.2 prefixes (`PreparedScan`, `PreparedFile`,
`PreparedRevision`, `PreparedCodeGraph`, `PreparedRelated`, `PreparedJava`,
`PreparedCommunication`, `SafeXml`, `XmlEvidence`, `CommunicationCatalog`, and
`CommunicationPatternCatalog`). It scanned 28 files against the exact forbidden
token set and found `0` matches.

The independent runtime-pin command was:

```pwsh
rg -n '@colbymchenry/codegraph-win32-x64@1\.5\.0|jdt-language-server-1\.61\.0-202608141332|JavaMajorVersion|StreamJsonRpc.*2\.25\.29|image: postgres:17-alpine' src/dotnet/Vulgata.Web/Program.cs Directory.Packages.props docker-compose.yml
```

It found the expected CodeGraph, JDT LS, Java, StreamJsonRpc, and PostgreSQL
pins. The workflow-state inspection found `schema_version=3.0`,
`stage=implement`, the bound checkpoint above, and accepted implementation
approval scoped to the accepted boundary. Historical CodeGraphContext,
Worker/Document, Pass 2, Chat, Cross-Repository Resolution, and Backup B modules
elsewhere in the repository were deliberately not treated as Story 12.2
composition; executable dependency/source checks prove that the new prepared
path does not reference them.

## Artifact identities

| Artifact | SHA-256 |
|---|---|
| `specs/011-java-xml-prescan-manifest/spec.md` | `c15d03b2b10e4eaa452a59e3b96acafdbb04160de556ca93c28380117a085ad3` |
| `specs/011-java-xml-prescan-manifest/plan.md` | `2a9373661e935918ab6b55a4d83903e4ba92ba7e9fb80534a616d6583cb4b318` |
| `specs/011-java-xml-prescan-manifest/reviews/definition-review/definition-review-05.json` | `c470944e89a8548714ee8c0c00f2f12f7928218c0420122c5c84705ff90f3cf2` |
| `specs/011-java-xml-prescan-manifest/decisions/implementation-approval-01.md` | `27df5d8fc9ec9b8456ac8fce1972add49f7242fc44063d3cd33ad651a4e20ad5` |
| `tests/Vulgata.Tests/PreparedScanning/PreparedScanBoundaryTests.cs` | `0d7f6556c6d5b998eae6b07b1abc7e874b558afee72e33fe446caa10f4a944ca` |

## Verdict

PASS. No forbidden Story 12.3 or demo behavior was found in the Story 12.2
prepared composition, no runtime/workflow pin drift was found, and no
requirement-backed regression RED/GREEN correction is required. Later
convergence, quality review, conditional E2E, and final verification gates remain
mandatory.
