# Story 12.3 T046 Host Runtime Qualification

- Qualified at: `2026-08-21T16:30:58Z` (UTC)
- Source commit under qualification: `cb3fb0f17b2eaf92d93afe71c710cd7b92c1e3bd`
- Branch: `demo0831/story-12-3`
- Architecture: Vulgata, CodeGraph, Java 21, and JDT LS on the Windows host;
  PostgreSQL 17 is the only required Docker service.

## Focused T046 executable qualification

Command (the PostgreSQL connection was supplied through the local
`VULGATA_TEST_ADMIN_CONNECTION` environment variable and is not persisted):

The machine-specific CodeGraph, Java, and JDT-LS locations were supplied
through `VULGATA_T046_CODEGRAPH_COMMAND`, `VULGATA_T046_JAVA_EXECUTABLE`,
`VULGATA_T046_JDT_LS_ROOT`, and `VULGATA_T046_JDT_LS_ARCHIVE`; no location
value is part of this artifact.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~Pass1HostRuntimeQualificationTests" -v:q /clp:ErrorsOnly
```

Result: **2 passed, 0 failed, 0 skipped**. The build/test process exited 0.

The two checks prove:

1. CodeGraph reports `1.5.0`; the pinned package executable, bundled Node
   executable, and npm shim match their accepted SHA-256 values. Java reports
   `21.0.12` LTS. JDT LS archive
   `jdt-language-server-1.61.0-202608141332` matches its accepted SHA-256,
   its host installation and `config_win/config.ini` exist, and the
   production `JdtLsAnalysisSessionFactory` constructs with the pinned
   archive and accepted stdio configuration.
2. `docker-compose.yml` pins `postgres:17-alpine`, the application service is
   profile-gated (`post-deadline-app`), and no CodeGraph or JDT image is
   present. The test opened the configured PostgreSQL service and queried
   `current_setting('server_version_num')`; the major version was `17`.

## Pinned runtime evidence

The exact commands and observed outputs were:

```pwsh
pwsh.exe -NoProfile -File $env:VULGATA_T046_CODEGRAPH_COMMAND --version
# 1.5.0

& $env:VULGATA_T046_JAVA_EXECUTABLE -version
# java version "21.0.12" 2026-07-21 LTS

docker compose config --services
# postgres
```

`docker compose config --services` emitted only Docker-client warnings about
the unreadable per-user Docker config and returned `postgres`; the Docker API
was not contacted by the focused test. The live Npgsql query above is the
authoritative PostgreSQL connectivity/version proof.

Observed SHA-256 values:

| Artifact | SHA-256 |
|---|---|
| CodeGraph `codegraph.js` | `03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03` |
| CodeGraph bundled `node.exe` | `b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3` |
| CodeGraph `npm-shim.js` | `80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877` |
| JDT LS `jdt-language-server-1.61.0-202608141332.tar.gz` | `bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f` |

The accepted package/runtime contract also pins StreamJsonRpc `2.25.29` in
`Directory.Packages.props`; no CodeGraphContext path was used.

## Companion real-tool qualification

These existing post-GREEN tests were rerun against the same source commit
after other JDT activity was idle, with the required normal Windows host
permissions. They were not weakened or edited by T046.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~NavigateCodeRealToolQualificationTests" -v:q /clp:ErrorsOnly
```

Result: **3 passed, 0 failed, 0 skipped** in approximately 32 seconds. This
includes real typed CodeGraph/JDT-LS navigation, bounded Java operations, and
the XML closed outcome.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~PreparedScanRealQualificationTests.PinnedJdtLs_RealReadinessFile_FinishesUnderSixtySeconds" -v:n
```

Result: **1 passed, 0 failed, 0 skipped** in approximately 14 seconds. The
real JDT-LS readiness result was `Succeeded` within the required bound.

An earlier restricted-shell attempt observed Windows EventLog access-denied
startup noise and transient JDT timeouts; those were environmental rerun
conditions, not accepted current-boundary results. The normal-permission
rerun above is the authoritative T046 companion evidence.

No production files, lifecycle state, staging area, or Git history were
modified by T046.
