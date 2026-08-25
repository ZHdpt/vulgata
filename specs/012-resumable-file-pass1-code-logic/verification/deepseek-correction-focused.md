# DeepSeek correction focused verification

## Verdict

T081 is **GREEN**. The corrected source-free regression boundary passed 272/272 tests with no failures or skips. The production Web composition resolved the prepared executor and both closed functions after binding the verified JDT-LS archive explicitly.

This verification does not authorize or claim live provider success. It sent no source-derived payload and made no DeepSeek request. T082 remains the next ordered fake-wire gate.

## Commands and results

1. Local-file logging and production composition:

   ```powershell
   $env:AnalysisRuntime__JdtLsDistributionArchivePath = '<approved-jdt-ls-archive>'
   dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --nologo --filter "(FullyQualifiedName~LoggingConfigurationTests.ProductionHost_CriticalLog_WritesConfiguredLocalFileAsync)|(FullyQualifiedName~Pass1DeepSeekPromptCompositionTests.ProductionWebScope_ResolvesPreparedExecutorAndBothClosedFunctionFactoriesAsync)" --logger "console;verbosity=minimal" -p:GenerateMvcTestManifest=false -clp:ErrorsOnly
   ```

   Result: 2 passed, 0 failed, 0 skipped in 4 seconds.

2. Complete T081 focused correction suite:

   ```powershell
   $env:AnalysisRuntime__JdtLsDistributionArchivePath = '<approved-jdt-ls-archive>'
   $env:AnalysisRuntime__JdtLsInstallationRoot = '<approved-jdt-ls-installation>'
   $env:VULGATA_TEST_POSTGRES_ADMIN = '<approved-admin-connection>'
   dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --nologo --filter "(FullyQualifiedName~Pass1DeepSeekContractQualificationTests&FullyQualifiedName!~ApprovedFileCodeUnit_DeepSeekResponse)|(FullyQualifiedName~Pass1DeepSeekPromptCompositionTests)|(FullyQualifiedName~PreparedPass1JobExecutorStreamingTests)|(FullyQualifiedName~Pass1DeepSeekStreamingQualificationTests)|(FullyQualifiedName~Pass1MigrationTests)|(FullyQualifiedName~Pass1DocumentCommitTests)|(FullyQualifiedName~Pass1DocumentPostgreSqlAtomicTests)" --logger "console;verbosity=minimal" -p:GenerateMvcTestManifest=false -clp:ErrorsOnly
   ```

   Result: 272 passed, 0 failed, 0 skipped in 1 minute 17 seconds.

3. Web build:

   ```powershell
   dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore --nologo -clp:ErrorsOnly
   ```

   Result: build succeeded with 0 errors and 9 existing warnings.

## Covered correction seams

- Authoritative strict-schema projection and exact metadata.
- Externalized prompt composition with exactly `navigate_code` and `submit_code_logic`.
- Closed production DI composition for both functions.
- Parser/session acceptance and local rejection mapping.
- Streaming and reasoning continuity, absolute deadlines, turn ceiling, request and response byte limits, and zero SDK retries.
- Source-free handshake contract without executing a live provider request.
- Atomic Document graph commit, exact replay, rejection rollback, and unchanged durable retry authority.
- Zero EF model/migration delta and unchanged v1/v2 coexistence.
- Production Critical logging through the configured local append-only file provider without Windows Event Log permission.

## Host correction disposition

The earlier T081 failure was caused by Windows Event Log access masking startup diagnostics. The user authorized replacing that provider with console plus configured local-file logging. A focused production-host regression proves the Critical entry is written and the logger is disposed cleanly.

The Codex host reports a sandbox user profile, so the application's user-profile archive fallback did not point at the approved archive. The user authorized the explicit path binding; the archive existed and its SHA-256 matched the production-pinned value. No archive copy was required.

## Next gate

Run T082 only: the fake outgoing-wire qualification. Do not begin T083 or any source-bearing provider traffic until T082 is recorded GREEN.
