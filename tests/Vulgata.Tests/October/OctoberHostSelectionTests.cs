using System.Diagnostics;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Configuration;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
using Xunit.Abstractions;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.October;

public sealed class OctoberHostSelectionTests(ITestOutputHelper output)
{
    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorPrepare_WithWebStopped_CommitsOwnedBatch()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);
        OctoberSetupRequest parsedRequest = Assert.IsType<OctoberSetupRequest>(
            JsonSerializer.Deserialize<OctoberSetupRequest>(requestJson));
        Assert.Equal(fixture.TargetId, parsedRequest.TargetId);
        Assert.Equal(fixture.BatchId, parsedRequest.BatchId);
        Assert.Empty(parsedRequest.SelectionDigest);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOwnedProviderDocumentBindsAsync();
        await fixture.AssertOctoberSchemaAbsentAsync();
        using Process process = fixture.StartGenerator(requestJson);
        int processId = process.Id;
        output.WriteLine($"generator-process-id={processId}");
        (int exitCode, string standardOutput, string standardError) = await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.True(exitCode == 0, "The actual generator prepare command should commit the authorized owned batch.");
        Assert.True(
            childOutput.Contains("Prepared", StringComparison.Ordinal) &&
            childOutput.Contains(fixture.BatchId.ToString("D"), StringComparison.OrdinalIgnoreCase),
            "The successful command should report Prepared and its exact batch ID.");
        bool reportsBoundProviderConfiguration = childOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Contains("provider", StringComparison.OrdinalIgnoreCase) &&
                         line.Contains("bound", StringComparison.OrdinalIgnoreCase));
        Assert.True(reportsBoundProviderConfiguration,
            "The successful command should identify provider configuration as bound without exposing provider details.");

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        IOctoberLocalAuthority authority = scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult authentication = await authority.AuthenticateAsync(CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
        await using IOctoberOperatorSession session = Assert.IsAssignableFrom<IOctoberOperatorSession>(
            authentication.Session);
        OctoberAuthoritySelectionResult current = await authority.AuthorizeSelectionAsync(
            session,
            new OctoberAuthoritySelectionRequest(fixture.TargetId, fixture.BatchId, PrepareRequest: null),
            CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, current.Status);
        OctoberAuthorizedSelection freshAuthority = Assert.IsType<OctoberAuthorizedSelection>(current.Authority);

        await using OctoberDbContext october = fixture.CreateOctoberContext();
        IOctoberBatchSelector selector = new OctoberBatchSelector(
            october, fixture.TargetId, fixture.CanonicalDatabaseIdentity);
        OctoberSelectionResult selected = await selector.BindAsync(
            new OctoberStartupSelection(fixture.TargetId, fixture.BatchId, freshAuthority),
            CancellationToken.None);
        Assert.Equal(OctoberSelectionStatus.Selected, selected.Status);
        IOctoberHistoricalBoundSetupData bound =
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(selected.BoundSetupData);
        Assert.Equal(fixture.TargetId, bound.Snapshot.Request.TargetId);
        Assert.Equal(fixture.BatchId, bound.Snapshot.Request.BatchId);
        Assert.Equal(fixture.CanonicalDatabaseIdentity, bound.Snapshot.Authority.CanonicalDatabaseIdentity);
        Assert.Equal(fixture.SubjectId, bound.Snapshot.Authority.SubjectId);
        Assert.Equal(fixture.AuthorityId, bound.Snapshot.Authority.SelectedContext!.ManagementAuthorityId);
        Assert.Equal(new[] { fixture.RepositoryId }, bound.Snapshot.Request.Repositories
            .Select(repository => repository.RepositoryId));
        Assert.Matches("^[0-9a-f]{64}$", bound.Snapshot.Request.SelectionDigest);
        Assert.Equal(bound.Snapshot.Request.SelectionDigest, bound.Snapshot.Authority.SelectionDigest);
        Assert.Equal(TimeSpan.Zero, bound.Snapshot.SelectedAtUtc.Offset);
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorSelect_WithWebStopped_ReturnsExistingBatchWithoutChangingCommittedState()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOctoberSchemaAbsentAsync();

        using Process prepare = fixture.StartGenerator("prepare", requestJson);
        output.WriteLine($"prepare-process-id={prepare.Id}");
        (int prepareExitCode, string prepareOutput, string prepareError) =
            await fixture.WaitForGeneratorAsync(prepare);
        output.WriteLine($"prepare-exit-code={prepareExitCode}");
        Assert.DoesNotContain(fixture.OperatorPassword, prepareOutput + prepareError, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, prepareOutput + prepareError, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, prepareOutput + prepareError, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, prepareOutput + prepareError, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, prepareOutput + prepareError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, prepareExitCode);

        OctoberBoundSetupSnapshot committedBeforeSelect = await fixture.ReadBoundSnapshotAsync();
        var persistedBeforeSelect = await fixture.CaptureOctoberStateAsync();

        using Process select = fixture.StartGenerator("select", requestJson: null);
        output.WriteLine($"select-process-id={select.Id}");
        (int selectExitCode, string selectOutput, string selectError) =
            await fixture.WaitForGeneratorAsync(select);
        output.WriteLine($"select-exit-code={selectExitCode}");
        Assert.DoesNotContain(fixture.OperatorPassword, selectOutput + selectError, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, selectOutput + selectError, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, selectOutput + selectError, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, selectOutput + selectError, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, selectOutput + selectError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, selectExitCode);
        Assert.True(
            selectOutput.Contains("Selected", StringComparison.Ordinal) &&
            selectOutput.Contains(fixture.BatchId.ToString("D"), StringComparison.OrdinalIgnoreCase),
            "The successful command should report Selected and its exact batch ID.");

        OctoberBoundSetupSnapshot committedAfterSelect = await fixture.ReadBoundSnapshotAsync();
        var persistedAfterSelect = await fixture.CaptureOctoberStateAsync();

        Assert.Equal(committedBeforeSelect.Request.TargetId, committedAfterSelect.Request.TargetId);
        Assert.Equal(committedBeforeSelect.Request.BatchId, committedAfterSelect.Request.BatchId);
        Assert.Equal(committedBeforeSelect.Request.SelectionDigest, committedAfterSelect.Request.SelectionDigest);
        Assert.Equal(
            committedBeforeSelect.Request.Repositories.ToArray(),
            committedAfterSelect.Request.Repositories.ToArray());
        Assert.Equal(
            committedBeforeSelect.Request.ContextSelections.ToArray(),
            committedAfterSelect.Request.ContextSelections.ToArray());
        Assert.Equal(committedBeforeSelect.Authority.TargetId, committedAfterSelect.Authority.TargetId);
        Assert.Equal(committedBeforeSelect.Authority.BatchId, committedAfterSelect.Authority.BatchId);
        Assert.Equal(committedBeforeSelect.Authority.SubjectId, committedAfterSelect.Authority.SubjectId);
        Assert.Equal(
            committedBeforeSelect.Authority.CanonicalDatabaseIdentity,
            committedAfterSelect.Authority.CanonicalDatabaseIdentity);
        Assert.Equal(
            committedBeforeSelect.Authority.SelectionDigest,
            committedAfterSelect.Authority.SelectionDigest);
        Assert.Equal(
            committedBeforeSelect.Authority.SelectedContext!.ManagementAuthorityId,
            committedAfterSelect.Authority.SelectedContext!.ManagementAuthorityId);
        Assert.Equal(
            committedBeforeSelect.Authority.SelectedContext.Repositories.ToArray(),
            committedAfterSelect.Authority.SelectedContext.Repositories.ToArray());
        Assert.Equal(
            committedBeforeSelect.Authority.SelectedContext.Entries.ToArray(),
            committedAfterSelect.Authority.SelectedContext.Entries.ToArray());
        Assert.Equal(
            committedBeforeSelect.Authority.SelectedContext.Differences.ToArray(),
            committedAfterSelect.Authority.SelectedContext.Differences.ToArray());
        Assert.Equal(committedBeforeSelect.SelectedAtUtc, committedAfterSelect.SelectedAtUtc);

        Assert.Equal(persistedBeforeSelect.AppliedMigrations, persistedAfterSelect.AppliedMigrations);
        Assert.Equal(persistedBeforeSelect.SchemaDefinition, persistedAfterSelect.SchemaDefinition);
        Assert.Equal(persistedBeforeSelect.RowCounts, persistedAfterSelect.RowCounts);
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorPrepare_MalformedProviderDoesNotReachOperatorFactorValidationOrOctoberMigration()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOctoberSchemaAbsentAsync();

        (int failedLoginBaseline, string invalidRecoveryCode) =
            await fixture.EnableTwoFactorAndProveFailedFactorIncrementsCounterAsync();
        output.WriteLine($"failed-factor-counter-baseline={failedLoginBaseline}");
        fixture.WriteMalformedProviderDocument();
        output.WriteLine("provider-document=malformed-owned-json");

        using Process process = fixture.StartGenerator("prepare", requestJson);
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidRecoveryCode, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);

        await fixture.AssertOctoberSchemaAbsentAsync();
        int failedLoginCountAfterRejection = await fixture.ReadOperatorAccessFailedCountAsync();
        Assert.Equal(failedLoginBaseline, failedLoginCountAfterRejection);
        Assert.Contains("InvalidConfiguration", childOutput, StringComparison.Ordinal);
        output.WriteLine("provider-rejection-failed-factor-counter-unchanged=true");
        output.WriteLine("provider-rejection-october-schema=absent");
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorPrepare_UnusableSelectedProviderCredentialReportsCredentialUnavailableBeforeOperatorFactorValidationOrOctoberMigration()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOwnedProviderDocumentBindsAsync();
        await fixture.AssertOctoberSchemaAbsentAsync();

        (int failedFactorBaseline, string invalidRecoveryCode) =
            await fixture.EnableTwoFactorAndProveFailedFactorIncrementsCounterAsync();
        output.WriteLine($"failed-factor-counter-baseline={failedFactorBaseline}");
        fixture.WriteUnusableSelectedProviderCredential();
        output.WriteLine("provider-document=complete-owned-document-with-unusable-worker-ciphertext");

        using Process process = fixture.StartGenerator("prepare", requestJson);
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidRecoveryCode, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);

        await fixture.AssertOctoberSchemaAbsentAsync();
        int failedFactorCountAfterRejection = await fixture.ReadOperatorAccessFailedCountAsync();
        Assert.Equal(failedFactorBaseline, failedFactorCountAfterRejection);
        Assert.Contains("CredentialUnavailable", childOutput, StringComparison.Ordinal);
        output.WriteLine("provider-rejection-failed-factor-counter-unchanged=true");
        output.WriteLine("provider-rejection-october-schema=absent");
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorPrepare_UnavailableOwnedIdentityConnectionReportsAuthorityUnavailableBeforeOctoberMigration()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOwnedProviderDocumentBindsAsync();
        await fixture.AssertOctoberSchemaAbsentAsync();
        string unavailableIdentityConnection = fixture.CreateUnavailableIdentityConnectionString();

        using Process process = fixture.StartGenerator(
            "prepare", requestJson, identityConnectionStringOverride: unavailableIdentityConnection);
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(unavailableIdentityConnection, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);

        await fixture.AssertOctoberSchemaAbsentAsync();
        Assert.Contains("AuthorityUnavailable", childOutput, StringComparison.Ordinal);
        output.WriteLine("unavailable-owned-identity-connection-october-schema=absent");
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorPrepare_MismatchedOwnedIdentityDescriptorFailsClosedBeforeOctoberMigration()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOwnedProviderDocumentBindsAsync();
        await fixture.AssertOctoberSchemaAbsentAsync();
        string mismatchedIdentity = fixture.CreateMismatchedCanonicalDatabaseIdentity();
        output.WriteLine("identity-descriptor=synthetic-mismatch-on-same-owned-identity-connection");

        using Process process = fixture.StartGenerator(
            "prepare", requestJson,
            identityConnectionStringOverride: fixture.ConnectionString,
            identityCanonicalDatabaseIdentityOverride: mismatchedIdentity);
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(mismatchedIdentity, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        await fixture.AssertOctoberSchemaAbsentAsync();
        Assert.NotEqual(0, exitCode);
        output.WriteLine("mismatched-owned-identity-descriptor-october-schema=absent");
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorPrepare_MismatchedOwnedTargetDescriptorReportsTargetUnavailableBeforeOctoberMigration()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOwnedProviderDocumentBindsAsync();
        await fixture.AssertOctoberSchemaAbsentAsync();
        string mismatchedTargetIdentity = fixture.CreateMismatchedCanonicalDatabaseIdentity();
        output.WriteLine("target-descriptor=synthetic-database-name-mismatch-on-owned-data-connection");

        using Process process = fixture.StartGenerator(
            "prepare", requestJson,
            identityConnectionStringOverride: fixture.ConnectionString,
            targetCanonicalDatabaseIdentityOverride: mismatchedTargetIdentity);
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.CanonicalDatabaseIdentity, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(mismatchedTargetIdentity, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);

        await fixture.AssertOctoberSchemaAbsentAsync();
        Assert.Contains("TargetUnavailable", childOutput, StringComparison.Ordinal);
        output.WriteLine("mismatched-target-descriptor-october-schema=absent");
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorPrepare_MissingSelectedWorkerProfileReportsInvalidConfigurationBeforeOperatorFactorValidationOrOctoberMigration()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOwnedProviderDocumentBindsAsync();
        await fixture.AssertOctoberSchemaAbsentAsync();

        (int failedFactorBaseline, string invalidRecoveryCode) =
            await fixture.EnableTwoFactorAndProveFailedFactorIncrementsCounterAsync();
        output.WriteLine($"failed-factor-counter-baseline={failedFactorBaseline}");
        fixture.WriteMissingSelectedWorkerProfile();
        output.WriteLine("provider-document=complete-owned-document-with-missing-selected-worker-profile");

        using Process process = fixture.StartGenerator("prepare", requestJson);
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidRecoveryCode, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);

        await fixture.AssertOctoberSchemaAbsentAsync();
        int failedFactorCountAfterRejection = await fixture.ReadOperatorAccessFailedCountAsync();
        Assert.Equal(failedFactorBaseline, failedFactorCountAfterRejection);
        Assert.Contains("InvalidConfiguration", childOutput, StringComparison.Ordinal);
        output.WriteLine("invalid-role-reference-failed-factor-counter-unchanged=true");
        output.WriteLine("invalid-role-reference-october-schema=absent");
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorUnsupportedModeReportsInvalidConfigurationWithoutOctoberMigration()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOwnedProviderDocumentBindsAsync();
        await fixture.AssertOctoberSchemaAbsentAsync();

        using Process process = fixture.StartGenerator("unsupported-mode", requestJson);
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);

        await fixture.AssertOctoberSchemaAbsentAsync();
        Assert.Contains("InvalidConfiguration", childOutput, StringComparison.Ordinal);
        output.WriteLine("unsupported-mode-october-schema=absent");
    }

    [Trait("Suite", "E2E")]
    [SuiteTheory("E2E")]
    [InlineData("missing-authority-id")]
    [InlineData("missing-protection-setting")]
    [InlineData("unsupported-credential-source")]
    [InlineData("malformed-setup-json")]
    [InlineData("non-exact-authority-id")]
    public async Task GeneratorPrepare_InvalidStartupConfigurationPredicatesReportInvalidConfigurationBeforeOctoberMigration(
        string invalidCase)
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOwnedProviderDocumentBindsAsync();
        await fixture.AssertOctoberSchemaAbsentAsync();
        output.WriteLine($"startup-configuration-case={invalidCase}");

        using Process process = fixture.StartGenerator(
            "prepare", requestJson, invalidStartupConfigurationCase: invalidCase);
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);

        await fixture.AssertOctoberSchemaAbsentAsync();
        Assert.Contains("InvalidConfiguration", childOutput, StringComparison.Ordinal);
        output.WriteLine("invalid-startup-configuration-october-schema=absent");
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorPrepare_DuplicateProviderDocumentPropertyIsRejectedBeforeOperatorFactorValidationOrOctoberMigration()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOctoberSchemaAbsentAsync();

        (int failedFactorBaseline, string invalidRecoveryCode) =
            await fixture.EnableTwoFactorAndProveFailedFactorIncrementsCounterAsync();
        output.WriteLine($"failed-factor-counter-baseline={failedFactorBaseline}");
        fixture.WriteDuplicateRootProviderProperty();
        output.WriteLine("provider-document=duplicate-root-sourceIdentity");

        using Process process = fixture.StartGenerator("prepare", requestJson);
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidRecoveryCode, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);

        await fixture.AssertOctoberSchemaAbsentAsync();
        int failedFactorCountAfterRejection = await fixture.ReadOperatorAccessFailedCountAsync();
        Assert.Equal(failedFactorBaseline, failedFactorCountAfterRejection);
        output.WriteLine("duplicate-provider-rejection-failed-factor-counter-unchanged=true");
        output.WriteLine("duplicate-provider-rejection-october-schema=absent");
    }

    [Trait("Suite", "E2E")]
    [SuiteTheory("E2E")]
    [InlineData("unknown-profile-property")]
    [InlineData("unknown-api-wire-value")]
    [InlineData("competing-provider-source")]
    [InlineData("malformed-utf8")]
    public async Task GeneratorPrepare_InvalidProviderDocumentPredicatesAreRejectedBeforeOperatorFactorValidationOrOctoberMigration(
        string invalidCase)
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOctoberSchemaAbsentAsync();

        (int failedFactorBaseline, string invalidRecoveryCode) =
            await fixture.EnableTwoFactorAndProveFailedFactorIncrementsCounterAsync();
        output.WriteLine($"failed-factor-counter-baseline={failedFactorBaseline}");
        if (invalidCase != "competing-provider-source")
            fixture.WriteInvalidProviderDocument(invalidCase);
        output.WriteLine($"provider-document-case={invalidCase}");

        using Process process = fixture.StartGenerator(
            "prepare", requestJson, includeCompetingProviderSource: invalidCase == "competing-provider-source");
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidRecoveryCode, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);

        await fixture.AssertOctoberSchemaAbsentAsync();
        int failedFactorCountAfterRejection = await fixture.ReadOperatorAccessFailedCountAsync();
        Assert.Equal(failedFactorBaseline, failedFactorCountAfterRejection);
        output.WriteLine("invalid-provider-rejection-failed-factor-counter-unchanged=true");
        output.WriteLine("invalid-provider-rejection-october-schema=absent");
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorPrepare_InvalidProtectedOperatorPasswordIsSanitizedBeforeOctoberMigration()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOctoberSchemaAbsentAsync();
        string wrongOperatorPassword = fixture.WriteWrongOperatorPassword();
        output.WriteLine("operator-credential-case=synthetic-wrong-protected-password");

        using Process process = fixture.StartGenerator("prepare", requestJson);
        output.WriteLine($"generator-process-id={process.Id}");
        (int exitCode, string standardOutput, string standardError) =
            await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(wrongOperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, exitCode);

        await fixture.AssertOctoberSchemaAbsentAsync();
        Assert.Contains("Denied", childOutput, StringComparison.Ordinal);
        output.WriteLine("invalid-operator-password-october-schema=absent");
    }

    private sealed class GeneratorPrepareFixture : IAsyncDisposable
    {
        private const string DatabasePrefix = "vulgata_story184_generator_";
        private const string DataProtectionApplicationName = "Vulgata.Tests.Story184.GeneratorPrepare";
        private readonly string _administratorConnectionString;
        private readonly ITestOutputHelper _output;
        private readonly DataProtectionProviderRegistration _protection;
        private readonly string _databaseName;
        private readonly string _runId;
        private bool _disposed;

        private GeneratorPrepareFixture(
            ITestOutputHelper output,
            string runId,
            string administratorConnectionString,
            string databaseName,
            string connectionString,
            string canonicalDatabaseIdentity,
            string ownedRoot,
            ServiceProvider services,
            DataProtectionProviderRegistration protection,
            string subjectId,
            string operatorUserName,
            string operatorPassword,
            Guid systemId,
            Guid repositoryId,
            Guid globalContextId,
            Guid batchId,
            string targetId)
        {
            _output = output;
            _runId = runId;
            _administratorConnectionString = administratorConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
            CanonicalDatabaseIdentity = canonicalDatabaseIdentity;
            OwnedRoot = ownedRoot;
            Services = services;
            _protection = protection;
            SubjectId = subjectId;
            OperatorUserName = operatorUserName;
            OperatorPassword = operatorPassword;
            SystemId = systemId;
            RepositoryId = repositoryId;
            GlobalContextId = globalContextId;
            BatchId = batchId;
            TargetId = targetId;
            AuthorityId = $"generator-tracer-authority-{runId}";
            ProviderApiKeyWorker = $"synthetic-worker-key-{runId}";
            ProviderApiKeyChat = $"synthetic-chat-key-{runId}";
        }

        internal string ConnectionString { get; }
        internal string CanonicalDatabaseIdentity { get; }
        internal string OwnedRoot { get; }
        internal ServiceProvider Services { get; }
        internal string SubjectId { get; }
        internal string OperatorUserName { get; }
        internal string OperatorPassword { get; }
        internal Guid SystemId { get; }
        internal Guid RepositoryId { get; }
        internal Guid GlobalContextId { get; }
        internal Guid BatchId { get; }
        internal string TargetId { get; }
        internal string AuthorityId { get; }
        internal string ProviderApiKeyWorker { get; }
        internal string ProviderApiKeyChat { get; }

        internal static async Task<GeneratorPrepareFixture> CreateAsync(ITestOutputHelper output)
        {
            string runId = Environment.GetEnvironmentVariable("VULGATA_TEST_RESOURCE_RUN") ?? string.Empty;
            Assert.True(Guid.TryParseExact(runId, "N", out _),
                "The E2E launcher must provide its exact owned PostgreSQL run identifier.");
            string administrator = TestResourceScope.RequireAdministrator();
            TestResourceScope.ValidateAdministrator(administrator);
            NpgsqlConnectionStringBuilder adminBuilder = new(administrator)
            {
                Pooling = false,
                Timeout = 5,
                CommandTimeout = 15,
            };
            Assert.Equal("127.0.0.1", adminBuilder.Host);

            string databaseName = DatabasePrefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            string? ownedRoot = null;
            ServiceProvider? services = null;
            DataProtectionProviderRegistration? protection = null;
        string? targetConnection = null;
            bool databaseCreated = false;
            try
            {
                await using (NpgsqlConnection connection = new(adminBuilder.ConnectionString))
                {
                    await connection.OpenAsync();
                    int major = Convert.ToInt32(await new NpgsqlCommand(
                        "SELECT current_setting('server_version_num')::integer;", connection)
                        .ExecuteScalarAsync(), CultureInfo.InvariantCulture) / 10000;
                    Assert.Equal(17, major);
                    await using NpgsqlCommand exists = new(
                        "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);", connection);
                    exists.Parameters.AddWithValue("name", databaseName);
                    Assert.False((bool)(await exists.ExecuteScalarAsync() ?? true));
                    await using NpgsqlCommand create = new(
                        $"CREATE DATABASE \"{databaseName}\";", connection);
                    await create.ExecuteNonQueryAsync();
                    databaseCreated = true;
                    TestResourceScope.RecordCreated(adminBuilder.ConnectionString, databaseName);
                }

                NpgsqlConnectionStringBuilder targetBuilder = new(adminBuilder.ConnectionString)
                {
                    Database = databaseName,
                    Pooling = false,
                    Timeout = 5,
                    CommandTimeout = 30,
                };
                targetConnection = targetBuilder.ConnectionString;
                TestResourceScope.ValidateOwnedTarget(targetConnection);
                string canonicalIdentity =
                    $"postgres://{adminBuilder.Host}:{adminBuilder.Port.ToString(CultureInfo.InvariantCulture)}/{databaseName}";
                ownedRoot = CreateOwnedRoot(runId);
                string keyRingPath = Path.Combine(ownedRoot, "keys");
                Directory.CreateDirectory(keyRingPath);
                string subjectId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                string operatorUserName = CreateOperatorUserName(subjectId);
                string operatorPassword = $"Valid1!Generator-{Guid.NewGuid():N}";
                Guid batchId = Guid.NewGuid();
                string targetId = $"generator-target-{runId}";
                string authorityId = $"generator-tracer-authority-{runId}";
                ImmutableDictionary<string, string> allowedTargets =
                    ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal)
                        .Add(targetId, canonicalIdentity);
                OctoberAuthorityConfiguration authorityConfiguration = new(
                    authorityId,
                    targetId,
                    batchId,
                    canonicalIdentity,
                    targetConnection,
                    targetConnection,
                    canonicalIdentity,
                    allowedTargets,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromHours(1));

                protection = CreateDataProtectionProvider(keyRingPath);
                string credentialFile = Path.Combine(ownedRoot, "operator.credentials.protected");
                string providerFile = Path.Combine(ownedRoot, "providers.json");
                string cliHome = Path.Combine(ownedRoot, "dotnet-cli");
                Directory.CreateDirectory(cliHome);
                WriteProtectedInputs(
                    protection.Provider,
                    credentialFile,
                    providerFile,
                    operatorUserName,
                    operatorPassword,
                    runId);

                IOctoberOperatorCredentialSource credentialSource =
                    new OctoberProtectedFileCredentialSource(credentialFile, protection.Provider);
                ServiceCollection registrations = new();
                registrations.AddLogging();
                registrations.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                    .SetApplicationName(DataProtectionApplicationName);
                OctoberHeadlessAuthorityRegistration.AddOctoberLocalAuthority(
                    registrations, authorityConfiguration, credentialSource, TimeProvider.System);
                registrations.AddDbContext<OctoberDbContext>(options =>
                    options.UseNpgsql(targetConnection, npgsql =>
                        npgsql.MigrationsHistoryTable("__OctoberMigrationsHistory", "october")));
                services = registrations.BuildServiceProvider(validateScopes: true);
                (Guid systemId, Guid repositoryId, Guid globalContextId) =
                    await MigrateAuthoritySchemasAndSeedAsync(services, subjectId, operatorPassword);

                await using (NpgsqlConnection target = new(targetConnection))
                {
                    TestResourceScope.ValidateOwnedTarget(targetConnection);
                    await target.OpenAsync();
                    await using NpgsqlCommand octoberSchema = new(
                        "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'october');", target);
                    Assert.False((bool)(await octoberSchema.ExecuteScalarAsync() ?? true),
                        "The supported Identity/domain fixture must leave October unmigrated before generator prepare.");
                }

                GeneratorPrepareFixture fixture = new(
                    output, runId, adminBuilder.ConnectionString, databaseName, targetConnection,
                    canonicalIdentity, ownedRoot, services, protection, subjectId, operatorUserName, operatorPassword,
                    systemId, repositoryId, globalContextId, batchId, targetId);
                services = null;
                ownedRoot = null;
                protection = null;
                databaseCreated = false;

                output.WriteLine($"resource-run={runId}");
                output.WriteLine($"runner-container=vulgata-test-{runId}");
                output.WriteLine($"runner-volume=vulgata-test-data-{runId}");
                output.WriteLine($"owned-database={databaseName}");
                output.WriteLine($"owned-temp-root={fixture.OwnedRoot}");
                return fixture;
            }
            catch
            {
                if (services is not null)
                    await services.DisposeAsync();
                if (protection is not null)
                    await protection.Services.DisposeAsync();
                if (databaseCreated)
                {
                    string cleanupTarget = targetConnection ?? new NpgsqlConnectionStringBuilder(
                        adminBuilder.ConnectionString) { Database = databaseName, Pooling = false }.ConnectionString;
                    await DropOwnedDatabaseAsync(adminBuilder.ConnectionString, databaseName, cleanupTarget);
                }
                if (ownedRoot is not null)
                    DeleteOwnedRoot(ownedRoot, runId);
                throw;
            }
        }

        internal OctoberSetupRequest CreatePrepareRequest() => new(
            TargetId,
            BatchId,
            [new OctoberRepositorySelection(RepositoryId, SystemId,
                "owned-generator-root-v1", "Generator prepare tracer")],
            [
                new OctoberContextSelection(OctoberContextScope.Global, GlobalContextId),
                new OctoberContextSelection(OctoberContextScope.System, SystemId),
                new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId),
            ],
            string.Empty);

        internal async Task AssertOwnedOperatorCanPrepareAsync(OctoberSetupRequest request)
        {
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            IOctoberLocalAuthority authority = scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
            OctoberAuthenticationResult authentication = await authority.AuthenticateAsync(CancellationToken.None);
            Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
            await using IOctoberOperatorSession session = Assert.IsAssignableFrom<IOctoberOperatorSession>(
                authentication.Session);
            Assert.Equal(SubjectId, session.SubjectId);
            Assert.Equal(AuthorityId, session.ManagementAuthorityId);

            OctoberAuthoritySelectionResult selection = await authority.AuthorizeSelectionAsync(
                session,
                new OctoberAuthoritySelectionRequest(TargetId, BatchId, request),
                CancellationToken.None);
            Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, selection.Status);
            Assert.Equal(TargetId, Assert.IsType<OctoberSetupRequest>(selection.Request).TargetId);
            Assert.Equal(BatchId, selection.Authority!.BatchId);
        }

        internal async Task<(int FailedLoginCount, string InvalidRecoveryCode)>
            EnableTwoFactorAndProveFailedFactorIncrementsCounterAsync()
        {
            int failedLoginCountBefore;
            string invalidRecoveryCode = "not-a-valid-identity-recovery-code-" + _runId;
            await using (AsyncServiceScope setupScope = Services.CreateAsyncScope())
            {
                TestResourceScope.ValidateOwnedTarget(ConnectionString);
                UserManager<ApplicationUser> users = setupScope.ServiceProvider
                    .GetRequiredService<UserManager<ApplicationUser>>();
                ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(
                    await users.FindByIdAsync(SubjectId));
                IdentityResult enabled = await users.SetTwoFactorEnabledAsync(user, true);
                Assert.True(enabled.Succeeded, string.Join("; ", enabled.Errors.Select(error => error.Code)));
                IEnumerable<string>? recoveryCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(
                    user, number: 1);
                Assert.Single(recoveryCodes ?? Array.Empty<string>());
                Assert.DoesNotContain(invalidRecoveryCode, recoveryCodes ?? Array.Empty<string>());
                failedLoginCountBefore = await users.GetAccessFailedCountAsync(user);
                Assert.Equal(0, failedLoginCountBefore);

                WriteProtectedOperatorCredentials(
                    _protection.Provider,
                    Path.Combine(OwnedRoot, "operator.credentials.protected"),
                    OperatorUserName,
                    OperatorPassword,
                    authenticatorCode: null,
                    recoveryCode: invalidRecoveryCode);
            }

            await using (AsyncServiceScope operationScope = Services.CreateAsyncScope())
            {
                IOctoberLocalAuthority authority = operationScope.ServiceProvider
                    .GetRequiredService<IOctoberLocalAuthority>();
                OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);
                Assert.Equal(OctoberAuthenticationStatus.Denied, result.Status);
                Assert.Null(result.Session);
            }

            int failedLoginCountAfter = await ReadOperatorAccessFailedCountAsync();
            Assert.Equal(failedLoginCountBefore + 1, failedLoginCountAfter);
            _output.WriteLine($"verified-failed-factor-counter={failedLoginCountBefore}->{failedLoginCountAfter}");
            return (failedLoginCountAfter, invalidRecoveryCode);
        }

        internal async Task<int> ReadOperatorAccessFailedCountAsync()
        {
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(await users.FindByIdAsync(SubjectId));
            return await users.GetAccessFailedCountAsync(user);
        }

        internal void WriteMalformedProviderDocument() =>
            File.WriteAllText(Path.Combine(OwnedRoot, "providers.json"), "{ malformed provider document");

        internal async Task AssertOwnedProviderDocumentBindsAsync()
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(OwnedRoot, "providers.json")));
            JsonElement root = document.RootElement;
            OctoberProviderProfile[] profiles = root.GetProperty("profiles").EnumerateArray()
                .Select(profile => new OctoberProviderProfile(
                    profile.GetProperty("name").GetString()!,
                    profile.GetProperty("apiType").GetString() switch
                    {
                        "chat-completions" => ApiType.ChatCompletions,
                        "responses" => ApiType.Responses,
                        "messages" => ApiType.Messages,
                        _ => throw new InvalidOperationException("The owned provider fixture API type is invalid."),
                    },
                    profile.GetProperty("endpoint").GetString()!,
                    profile.GetProperty("model").GetString()!,
                    profile.GetProperty("credentialReference").GetString()!))
                .ToArray();
            Dictionary<string, string> protectedCredentials = root.GetProperty("protectedCredentials")
                .EnumerateObject()
                .ToDictionary(entry => entry.Name, entry => entry.Value.GetString()!, StringComparer.Ordinal);
            OctoberProviderConfiguration configuration = new(
                root.GetProperty("sourceIdentity").GetString()!,
                root.GetProperty("precedence").GetString()!,
                root.GetProperty("workerProfile").GetString()!,
                root.GetProperty("chatProfile").GetString()!,
                profiles,
                protectedCredentials);

            OctoberProviderBindingResult result = await new OctoberProviderBinder(
                new SecretEncryptionService(_protection.Provider)).BindAsync(configuration);

            Assert.Equal(OctoberProviderBindingStatus.Bound, result.Status);
            Assert.IsType<OctoberBoundProviderConfiguration>(result.Binding);
        }

        internal void WriteUnusableSelectedProviderCredential()
        {
            string providerFile = Path.Combine(OwnedRoot, "providers.json");
            JsonObject provider = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(providerFile)));
            JsonObject protectedCredentials = Assert.IsType<JsonObject>(provider["protectedCredentials"]);
            Assert.False(string.IsNullOrWhiteSpace(
                protectedCredentials["worker-credential"]?.GetValue<string>()));
            protectedCredentials["worker-credential"] = "synthetically-unusable-protected-worker-ciphertext";
            File.WriteAllText(providerFile, provider.ToJsonString());
        }

        internal void WriteMissingSelectedWorkerProfile()
        {
            string providerFile = Path.Combine(OwnedRoot, "providers.json");
            JsonObject provider = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(providerFile)));
            JsonArray profiles = Assert.IsType<JsonArray>(provider["profiles"]);
            string currentWorkerProfile = Assert.IsType<string>(provider["workerProfile"]?.GetValue<string>());
            Assert.Contains(profiles, profile =>
                string.Equals(profile?["name"]?.GetValue<string>(), currentWorkerProfile, StringComparison.Ordinal));

            string missingWorkerProfile = $"{currentWorkerProfile}-not-present";
            Assert.DoesNotContain(profiles, profile =>
                string.Equals(profile?["name"]?.GetValue<string>(), missingWorkerProfile, StringComparison.Ordinal));
            provider["workerProfile"] = missingWorkerProfile;
            File.WriteAllText(providerFile, provider.ToJsonString());
        }

        internal string CreateUnavailableIdentityConnectionString()
        {
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
            NpgsqlConnectionStringBuilder ownedIdentity = new(ConnectionString);
            NpgsqlConnectionStringBuilder identityConnection = new(ConnectionString)
            {
                Password = $"synthetic-unavailable-identity-password-{_runId}",
                Pooling = false,
            };
            Assert.Equal(ownedIdentity.Host, identityConnection.Host);
            Assert.Equal(ownedIdentity.Port, identityConnection.Port);
            Assert.Equal(ownedIdentity.Database, identityConnection.Database);
            Assert.Equal(ownedIdentity.Username, identityConnection.Username);
            Assert.NotEqual(ownedIdentity.Password, identityConnection.Password);
            return identityConnection.ConnectionString;
        }

        internal string CreateMismatchedCanonicalDatabaseIdentity()
        {
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
            NpgsqlConnectionStringBuilder descriptor = new(ConnectionString);
            string declaredIdentity = string.Create(CultureInfo.InvariantCulture,
                $"postgres://{descriptor.Host}:{descriptor.Port}/{descriptor.Database}");
            Assert.Equal(CanonicalDatabaseIdentity, declaredIdentity);

            return string.Create(CultureInfo.InvariantCulture,
                $"postgres://{descriptor.Host}:{descriptor.Port}/{descriptor.Database}-mismatch");
        }

        internal string WriteWrongOperatorPassword()
        {
            string wrongPassword = $"Wrong1!Generator-{Guid.NewGuid():N}";
            WriteProtectedOperatorCredentials(
                _protection.Provider,
                Path.Combine(OwnedRoot, "operator.credentials.protected"),
                OperatorUserName,
                wrongPassword,
                authenticatorCode: null,
                recoveryCode: null);
            return wrongPassword;
        }

        internal void WriteDuplicateRootProviderProperty()
        {
            string providerFile = Path.Combine(OwnedRoot, "providers.json");
            string providerDocument = File.ReadAllText(providerFile);
            int rootObjectEnd = providerDocument.LastIndexOf('}');
            Assert.True(rootObjectEnd >= 0, "The fixture must start from its complete valid provider document.");
            string duplicateProperty = ",\"sourceIdentity\":\"duplicate-owned-source\"";
            File.WriteAllText(providerFile,
                providerDocument[..rootObjectEnd] + duplicateProperty + providerDocument[rootObjectEnd..]);
        }

        internal void WriteInvalidProviderDocument(string invalidCase)
        {
            string providerFile = Path.Combine(OwnedRoot, "providers.json");
            string providerDocument = File.ReadAllText(providerFile);
            if (invalidCase == "malformed-utf8")
            {
                byte[] original = File.ReadAllBytes(providerFile);
                byte[] marker = Encoding.UTF8.GetBytes("\"sourceIdentity\":\"owned-generator-provider-");
                int markerIndex = original.AsSpan().IndexOf(marker);
                Assert.True(markerIndex >= 0, "The valid source identity must be present in the fixture document.");
                int invalidByteIndex = markerIndex + marker.Length;
                byte[] malformed = new byte[original.Length + 1];
                Buffer.BlockCopy(original, 0, malformed, 0, invalidByteIndex);
                malformed[invalidByteIndex] = 0xC3;
                Buffer.BlockCopy(original, invalidByteIndex, malformed, invalidByteIndex + 1,
                    original.Length - invalidByteIndex);
                File.WriteAllBytes(providerFile, malformed);
                return;
            }

            string mutated = invalidCase switch
            {
                "unknown-profile-property" => providerDocument.Replace(
                    "\"name\":\"worker-profile\"",
                    "\"name\":\"worker-profile\",\"unknownProperty\":true",
                    StringComparison.Ordinal),
                "unknown-api-wire-value" => ReplaceFirst(
                    providerDocument,
                    "\"apiType\":\"chat-completions\"",
                    "\"apiType\":\"unknown-wire-value\""),
                _ => throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase,
                    "Unknown invalid provider document case."),
            };

            Assert.NotEqual(providerDocument, mutated);
            File.WriteAllText(providerFile, mutated);
        }

        private static string ReplaceFirst(string value, string expected, string replacement)
        {
            int index = value.IndexOf(expected, StringComparison.Ordinal);
            Assert.True(index >= 0, "The expected provider field must be present in the valid fixture document.");
            return value[..index] + replacement + value[(index + expected.Length)..];
        }

        internal async Task AssertOctoberSchemaAbsentAsync()
        {
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
            await using NpgsqlConnection target = new(ConnectionString);
            await target.OpenAsync();
            await using NpgsqlCommand command = new(
                "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'october');", target);
            Assert.False((bool)(await command.ExecuteScalarAsync() ?? true));
        }

        internal async Task<OctoberBoundSetupSnapshot> ReadBoundSnapshotAsync()
        {
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            IOctoberLocalAuthority authority = scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
            OctoberAuthenticationResult authentication = await authority.AuthenticateAsync(CancellationToken.None);
            Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
            await using IOctoberOperatorSession session = Assert.IsAssignableFrom<IOctoberOperatorSession>(
                authentication.Session);
            OctoberAuthoritySelectionResult current = await authority.AuthorizeSelectionAsync(
                session,
                new OctoberAuthoritySelectionRequest(TargetId, BatchId, PrepareRequest: null),
                CancellationToken.None);
            Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, current.Status);
            OctoberAuthorizedSelection freshAuthority = Assert.IsType<OctoberAuthorizedSelection>(current.Authority);

            await using OctoberDbContext october = CreateOctoberContext();
            IOctoberBatchSelector selector = new OctoberBatchSelector(
                october, TargetId, CanonicalDatabaseIdentity);
            OctoberSelectionResult selected = await selector.BindAsync(
                new OctoberStartupSelection(TargetId, BatchId, freshAuthority),
                CancellationToken.None);
            Assert.Equal(OctoberSelectionStatus.Selected, selected.Status);
            IOctoberHistoricalBoundSetupData bound =
                Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(selected.BoundSetupData);
            return bound.Snapshot;
        }

        internal async Task<(string[] AppliedMigrations, string[] SchemaDefinition, int[] RowCounts)>
            CaptureOctoberStateAsync()
        {
            await using OctoberDbContext october = CreateOctoberContext();
            string[] appliedMigrations = (await october.Database.GetAppliedMigrationsAsync()).ToArray();
            int[] rowCounts =
            [
                await october.Batches.CountAsync(),
                await october.RepositorySelections.CountAsync(),
                await october.ContextReferences.CountAsync(),
                await october.AdvisoryEntries.CountAsync(),
                await october.ContextDifferences.CountAsync(),
                await october.PreparedSourceApprovals.CountAsync(),
            ];

            const string schemaQuery = """
                SELECT 'table|' || table_name || '|' || table_type
                FROM information_schema.tables
                WHERE table_schema = 'october'
                UNION ALL
                SELECT 'column|' || table_name || '|' || column_name || '|' || data_type || '|' ||
                    udt_name || '|' || is_nullable
                FROM information_schema.columns
                WHERE table_schema = 'october'
                UNION ALL
                SELECT 'constraint|' || table_name || '|' || constraint_name || '|' || constraint_type
                FROM information_schema.table_constraints
                WHERE constraint_schema = 'october'
                UNION ALL
                SELECT 'index|' || tablename || '|' || indexname || '|' || indexdef
                FROM pg_indexes
                WHERE schemaname = 'october'
                ORDER BY 1;
                """;
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
            await using NpgsqlConnection connection = new(ConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = new(schemaQuery, connection);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
            List<string> schemaDefinition = [];
            while (await reader.ReadAsync())
                schemaDefinition.Add(reader.GetString(0));

            return (appliedMigrations, schemaDefinition.ToArray(), rowCounts);
        }

        internal OctoberDbContext CreateOctoberContext()
        {
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
            return new OctoberDbContext(new DbContextOptionsBuilder<OctoberDbContext>()
                .UseNpgsql(ConnectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__OctoberMigrationsHistory", "october"))
                .Options);
        }

        internal Process StartGenerator(string requestJson)
            => StartGenerator("prepare", requestJson);

        internal Process StartGenerator(
            string mode,
            string? requestJson,
            bool includeCompetingProviderSource = false,
            string? identityConnectionStringOverride = null,
            string? identityCanonicalDatabaseIdentityOverride = null,
            string? targetCanonicalDatabaseIdentityOverride = null,
            string? invalidStartupConfigurationCase = null)
        {
            string repositoryRoot = FindRepositoryRoot();
            string project = Path.Combine(repositoryRoot,
                "src", "dotnet", "Vulgata.OctoberGenerator", "Vulgata.OctoberGenerator.csproj");
            ProcessStartInfo startInfo = new("dotnet")
            {
                WorkingDirectory = OwnedRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            string? path = Environment.GetEnvironmentVariable("PATH");
            string? systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            string? temp = Environment.GetEnvironmentVariable("TEMP");
            string? tmp = Environment.GetEnvironmentVariable("TMP");
            string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            startInfo.Environment.Clear();
            if (!string.IsNullOrWhiteSpace(path)) startInfo.Environment["PATH"] = path;
            if (!string.IsNullOrWhiteSpace(systemRoot)) startInfo.Environment["SystemRoot"] = systemRoot;
            if (!string.IsNullOrWhiteSpace(temp)) startInfo.Environment["TEMP"] = temp;
            if (!string.IsNullOrWhiteSpace(tmp)) startInfo.Environment["TMP"] = tmp;
            if (!string.IsNullOrWhiteSpace(dotnetRoot)) startInfo.Environment["DOTNET_ROOT"] = dotnetRoot;
            startInfo.Environment["DOTNET_CLI_HOME"] = Path.Combine(OwnedRoot, "dotnet-cli");
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
            startInfo.Environment["DOTNET_NOLOGO"] = "1";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            startInfo.Environment["ConnectionStrings__DefaultConnection"] = ConnectionString;
            startInfo.Environment["DataProtection__KeyRingPath"] = Path.Combine(OwnedRoot, "keys");
            startInfo.Environment["DataProtection__ApplicationName"] = DataProtectionApplicationName;
            startInfo.Environment["October__Mode"] = mode;
            startInfo.Environment["October__TargetId"] = TargetId;
            startInfo.Environment["October__BatchId"] = BatchId.ToString("D", CultureInfo.InvariantCulture);
            if (requestJson is null)
            {
                Assert.False(startInfo.Environment.ContainsKey("October__SetupRequestJson"));
            }
            else
            {
                startInfo.Environment["October__SetupRequestJson"] = requestJson;
            }
            startInfo.Environment["October__Authority__Id"] = AuthorityId;
            startInfo.Environment["October__Targets__0__TargetId"] = TargetId;
            startInfo.Environment["October__Targets__0__CanonicalDatabaseIdentity"] =
                targetCanonicalDatabaseIdentityOverride ?? CanonicalDatabaseIdentity;
            startInfo.Environment["October__Operator__CredentialSource"] = "protected-file";
            startInfo.Environment["October__Operator__CredentialFile"] =
                Path.Combine(OwnedRoot, "operator.credentials.protected");
            startInfo.Environment["October__Providers__ConfigurationFile"] =
                Path.Combine(OwnedRoot, "providers.json");
            if (includeCompetingProviderSource)
                startInfo.Environment["October__Providers__SourceIdentity"] = "competing-flattened-source";
            if (identityConnectionStringOverride is not null)
            {
                startInfo.Environment["October__Identity__ConnectionName"] = "GeneratorIdentity";
                startInfo.Environment["ConnectionStrings__GeneratorIdentity"] = identityConnectionStringOverride;
                startInfo.Environment["October__Identity__CanonicalDatabaseIdentity"] =
                    identityCanonicalDatabaseIdentityOverride ?? CanonicalDatabaseIdentity;
            }
            switch (invalidStartupConfigurationCase)
            {
                case null:
                    break;
                case "missing-authority-id":
                    startInfo.Environment.Remove("October__Authority__Id");
                    break;
                case "missing-protection-setting":
                    startInfo.Environment.Remove("DataProtection__KeyRingPath");
                    break;
                case "unsupported-credential-source":
                    startInfo.Environment["October__Operator__CredentialSource"] =
                        "synthetic-unsupported-source";
                    break;
                case "malformed-setup-json":
                    startInfo.Environment["October__SetupRequestJson"] = "{";
                    break;
                case "non-exact-authority-id":
                    startInfo.Environment["October__Authority__Id"] = $" {AuthorityId}";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidStartupConfigurationCase));
            }

            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(project);
            startInfo.ArgumentList.Add("--no-build");
            startInfo.ArgumentList.Add("--no-restore");
            Assert.Equal(Path.GetFullPath(OwnedRoot), Path.GetFullPath(startInfo.WorkingDirectory));
            Assert.DoesNotContain(OperatorPassword, string.Join(' ', startInfo.ArgumentList), StringComparison.Ordinal);
            Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The owned generator child did not start.");
            return process;
        }

        internal async Task<(int ExitCode, string StandardOutput, string StandardError)> WaitForGeneratorAsync(
            Process process)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                }

                throw new TimeoutException("The owned generator child exceeded its bounded startup deadline.");
            }

            return (process.ExitCode, await stdoutTask, await stderrTask);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            await Services.DisposeAsync();
            await _protection.Services.DisposeAsync();
            await DropOwnedDatabaseAsync(_administratorConnectionString, _databaseName, ConnectionString);
            DeleteOwnedRoot(OwnedRoot, _runId);
            _output.WriteLine($"verified-database-cleanup={_databaseName}");
            _output.WriteLine($"verified-temp-cleanup={_runId}");
        }

        private static DataProtectionProviderRegistration CreateDataProtectionProvider(string keyRingPath)
        {
            ServiceCollection services = new();
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                .SetApplicationName(DataProtectionApplicationName);
            ServiceProvider provider = services.BuildServiceProvider();
            return new DataProtectionProviderRegistration(provider,
                provider.GetRequiredService<IDataProtectionProvider>());
        }

        private static void WriteProtectedOperatorCredentials(
            IDataProtectionProvider protection,
            string credentialFile,
            string userName,
            string password,
            string? authenticatorCode,
            string? recoveryCode)
        {
            string credentials = JsonSerializer.Serialize(new
            {
                userName,
                password,
                authenticatorCode,
                recoveryCode,
            });
            string protectedCredentials = protection
                .CreateProtector("october-operator-credentials-v1")
                .Protect(credentials);
            File.WriteAllText(credentialFile, protectedCredentials);
        }

        private static void WriteProtectedInputs(
            IDataProtectionProvider protection,
            string credentialFile,
            string providerFile,
            string userName,
            string password,
            string runId)
        {
            string credentials = JsonSerializer.Serialize(new
            {
                userName,
                password,
                authenticatorCode = (string?)null,
                recoveryCode = (string?)null,
            });
            string protectedCredentials = protection
                .CreateProtector("october-operator-credentials-v1")
                .Protect(credentials);
            File.WriteAllText(credentialFile, protectedCredentials);

            string workerKey = $"synthetic-worker-key-{runId}";
            string chatKey = $"synthetic-chat-key-{runId}";
            string workerCiphertext = protection.CreateProtector(SecretPurposes.LlmApiKey).Protect(workerKey);
            string chatCiphertext = protection.CreateProtector(SecretPurposes.LlmApiKey).Protect(chatKey);
            string providerDocument = JsonSerializer.Serialize(new
            {
                sourceIdentity = $"owned-generator-provider-{runId}",
                precedence = "only-source",
                workerProfile = "worker-profile",
                chatProfile = "chat-profile",
                profiles = new[]
                {
                    new
                    {
                        name = "worker-profile",
                        apiType = "chat-completions",
                        endpoint = "https://worker.invalid/v1",
                        model = "controlled-worker-model",
                        credentialReference = "worker-credential",
                    },
                    new
                    {
                        name = "chat-profile",
                        apiType = "chat-completions",
                        endpoint = "https://chat.invalid/v1",
                        model = "controlled-chat-model",
                        credentialReference = "chat-credential",
                    },
                },
                protectedCredentials = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["worker-credential"] = workerCiphertext,
                    ["chat-credential"] = chatCiphertext,
                },
            });
            File.WriteAllText(providerFile, providerDocument);
        }

        private static async Task<(Guid SystemId, Guid RepositoryId, Guid GlobalContextId)> MigrateAuthoritySchemasAndSeedAsync(
            ServiceProvider services,
            string subjectId,
            string password)
        {
            await using (AsyncServiceScope scope = services.CreateAsyncScope())
            {
                TestResourceScope.ValidateOwnedTarget(
                    scope.ServiceProvider.GetRequiredService<OctoberAuthorityConfiguration>().DataConnectionString);
                ApplicationDbContext identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await identity.Database.MigrateAsync();
                RoleManager<IdentityRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                IdentityResult role = await roles.CreateAsync(new IdentityRole(RoleNames.SystemOwner));
                Assert.True(role.Succeeded, string.Join(";", role.Errors.Select(error => error.Code)));
                UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                ApplicationUser user = new()
                {
                    Id = subjectId,
                    UserName = CreateOperatorUserName(subjectId),
                    Email = CreateOperatorUserName(subjectId),
                    EmailConfirmed = true,
                    LockoutEnabled = true,
                };
                IdentityResult created = await users.CreateAsync(user, password);
                Assert.True(created.Succeeded, string.Join(";", created.Errors.Select(error => error.Code)));
                IdentityResult assignment = await users.AddToRoleAsync(user, RoleNames.SystemOwner);
                Assert.True(assignment.Succeeded, string.Join(";", assignment.Errors.Select(error => error.Code)));
            }

            await using AsyncServiceScope managementScope = services.CreateAsyncScope();
            TestResourceScope.ValidateOwnedTarget(
                managementScope.ServiceProvider.GetRequiredService<OctoberAuthorityConfiguration>().DataConnectionString);
            await using VulgataDbContext management = await managementScope.ServiceProvider
                .GetRequiredService<IDbContextFactory<VulgataDbContext>>()
                .CreateDbContextAsync();
            await management.Database.MigrateAsync();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            SystemEntity system = new("Generator tracer system", "Owned test data", "System advisory context", now);
            Repository repository = system.AddRepository(
                "Generator tracer repository",
                "https://example.invalid/generator-tracer.git",
                "Owned test data",
                "Repository advisory context",
                now);
            GlobalContext global = new("Global advisory context", now);
            management.Systems.Add(system);
            management.Repositories.Add(repository);
            management.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(system.Id, subjectId, now));
            management.GlobalContexts.Add(global);
            await management.SaveChangesAsync();
            return (system.Id, repository.Id, global.Id);
        }

        private static string CreateOperatorUserName(string subjectId) =>
            $"generator-tracer-{subjectId}@example.invalid";

        private static async Task DropOwnedDatabaseAsync(
            string administrator,
            string databaseName,
            string targetConnection)
        {
            Assert.StartsWith(DatabasePrefix, databaseName, StringComparison.Ordinal);
            Assert.Matches("^vulgata_story184_generator_[0-9a-f]{32}$", databaseName);
            TestResourceScope.ValidateOwnedTarget(targetConnection);
            NpgsqlConnectionStringBuilder builder = new(administrator) { Pooling = false, Timeout = 5 };
            TestResourceScope.ValidateAdministrator(builder.ConnectionString);
            await using NpgsqlConnection connection = new(builder.ConnectionString);
            await connection.OpenAsync();
            await using (NpgsqlCommand terminate = new(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid();",
                connection))
            {
                terminate.Parameters.AddWithValue("name", databaseName);
                await terminate.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand drop = new(
                $"DROP DATABASE IF EXISTS \"{databaseName}\";", connection))
            {
                await drop.ExecuteNonQueryAsync();
            }

            await using NpgsqlCommand exists = new(
                "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);", connection);
            exists.Parameters.AddWithValue("name", databaseName);
            Assert.False((bool)(await exists.ExecuteScalarAsync() ?? true),
                "The recorded test-created database must be absent after cleanup.");
        }

        private static string CreateOwnedRoot(string runId)
        {
            string parent = Path.Combine(Path.GetTempPath(), "vulgata-tests");
            Directory.CreateDirectory(parent);
            string root = Path.Combine(parent, "story184-generator-" + runId + "-" + Guid.NewGuid().ToString("N"));
            string resolvedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
            string resolvedRoot = Path.GetFullPath(root);
            Assert.StartsWith(resolvedParent + Path.DirectorySeparatorChar, resolvedRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            Directory.CreateDirectory(resolvedRoot);
            return resolvedRoot;
        }

        private static void DeleteOwnedRoot(string root, string runId)
        {
            string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                Path.Combine(Path.GetTempPath(), "vulgata-tests")));
            string resolved = Path.GetFullPath(root);
            string prefix = "story184-generator-" + runId + "-";
            string leaf = Path.GetFileName(resolved);
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            Assert.StartsWith(parent + Path.DirectorySeparatorChar, resolved, comparison);
            Assert.StartsWith(prefix, leaf, StringComparison.Ordinal);
            if (Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
            Assert.False(Directory.Exists(resolved), "The exact owned temporary root must be removed.");
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("The assigned repository root was not found.");
        }
    }

    private sealed record DataProtectionProviderRegistration(
        ServiceProvider Services,
        IDataProtectionProvider Provider);
}
