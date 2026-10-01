using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Configuration;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Vulgata.Tests.October;

public sealed class OctoberProviderBindingTests
{
    private const string KeyPurpose = "Vulgata.LlmProviders.ApiKey";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_CompleteDistinctWorkerAndChatProfiles_BindsBothAtomically()
    {
        const string workerCiphertext = "synthetic-worker-ciphertext";
        const string chatCiphertext = "synthetic-chat-ciphertext";
        const string workerPlaintext = "synthetic-worker-api-key";
        const string chatPlaintext = "synthetic-chat-api-key";
        OctoberProviderProfile worker = new(
            "worker-primary",
            ApiType.Responses,
            "https://worker.example.test/v1",
            "worker-model-v1",
            "credential://worker-primary");
        OctoberProviderProfile chat = new(
            "chat-primary",
            ApiType.ChatCompletions,
            "https://chat.example.test/v1",
            "chat-model-v1",
            "credential://chat-primary");
        OctoberProviderConfiguration configuration = new(
            "owned-host-document-v1",
            "only-source",
            worker.Name,
            chat.Name,
            [worker, chat],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [worker.CredentialReference] = workerCiphertext,
                [chat.CredentialReference] = chatCiphertext,
            });
        ControlledSecretEncryptionService encryption = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [workerCiphertext] = workerPlaintext,
            [chatCiphertext] = chatPlaintext,
        });
        OctoberProviderBinder binder = new(encryption);

        OctoberProviderBindingResult result = await binder.BindAsync(configuration);

        Assert.Equal(OctoberProviderBindingStatus.Bound, result.Status);
        OctoberBoundProviderConfiguration binding = Assert.IsType<OctoberBoundProviderConfiguration>(result.Binding);
        Assert.Equal("owned-host-document-v1", binding.SourceIdentity);
        Assert.Equal("only-source", binding.Precedence);
        Assert.Equal(worker, binding.Worker);
        Assert.Equal(chat, binding.Chat);

        Assert.Equal(2, encryption.Decryptions.Count);
        Assert.Contains((KeyPurpose, workerCiphertext), encryption.Decryptions);
        Assert.Contains((KeyPurpose, chatCiphertext), encryption.Decryptions);
        Assert.DoesNotContain(workerPlaintext, binding.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(chatPlaintext, binding.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(workerPlaintext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(chatPlaintext, result.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("shared-profile")]
    [InlineData("shared-credential-reference")]
    public async Task BindAsync_SharedSelectionDecryptsOnceAndIgnoresUnusedCredentials(string sharing)
    {
        const string sharedCiphertext = "synthetic-shared-ciphertext";
        const string unselectedCiphertext = "synthetic-unselected-ciphertext";
        const string unusedCiphertext = "synthetic-unused-ciphertext";
        const string sharedPlaintext = "synthetic-shared-api-key";
        OctoberProviderProfile worker = new(
            "worker-primary",
            ApiType.Responses,
            "https://worker.example.test/v1",
            "worker-model-v1",
            sharing == "shared-credential-reference"
                ? "credential://shared"
                : "credential://worker-primary");
        OctoberProviderProfile chat = new(
            "chat-primary",
            ApiType.ChatCompletions,
            "https://chat.example.test/v1",
            "chat-model-v1",
            sharing == "shared-credential-reference"
                ? "credential://shared"
                : "credential://chat-primary");
        OctoberProviderProfile unused = new(
            "unused-profile",
            ApiType.Messages,
            "https://unused.example.test/v1",
            "unused-model-v1",
            "credential://unused");
        bool sharesProfile = sharing == "shared-profile";
        Dictionary<string, string> protectedCredentials = new(StringComparer.Ordinal)
        {
            [worker.CredentialReference] = sharedCiphertext,
            [unused.CredentialReference] = unusedCiphertext,
        };
        if (!string.Equals(worker.CredentialReference, chat.CredentialReference, StringComparison.Ordinal))
            protectedCredentials[chat.CredentialReference] = sharesProfile
                ? unselectedCiphertext
                : sharedCiphertext;
        OctoberProviderConfiguration configuration = new(
            "owned-host-document-v1",
            "only-source",
            worker.Name,
            sharesProfile ? worker.Name : chat.Name,
            [worker, chat, unused],
            protectedCredentials);
        ControlledSecretEncryptionService encryption = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [sharedCiphertext] = sharedPlaintext,
            [unselectedCiphertext] = "synthetic-unselected-api-key",
            [unusedCiphertext] = "synthetic-unused-api-key",
        });

        OctoberProviderBindingResult result = await new OctoberProviderBinder(encryption)
            .BindAsync(configuration);

        Assert.Equal(OctoberProviderBindingStatus.Bound, result.Status);
        OctoberBoundProviderConfiguration binding = Assert.IsType<OctoberBoundProviderConfiguration>(result.Binding);
        Assert.Equal(worker, binding.Worker);
        Assert.Equal(sharesProfile ? worker : chat, binding.Chat);
        Assert.Equal((KeyPurpose, sharedCiphertext), Assert.Single(encryption.Decryptions));
        Assert.DoesNotContain(encryption.Decryptions, call => call.Ciphertext == unselectedCiphertext);
        Assert.DoesNotContain(encryption.Decryptions, call => call.Ciphertext == unusedCiphertext);
        Assert.DoesNotContain(sharedPlaintext, binding.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(sharedPlaintext, result.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_CopiesMutableConfigurationInputs_BindsOriginalSelection()
    {
        const string workerCiphertext = "synthetic-original-worker-ciphertext";
        const string chatCiphertext = "synthetic-original-chat-ciphertext";
        OctoberProviderProfile worker = new(
            "worker-primary",
            ApiType.Responses,
            "https://worker.example.test/v1",
            "worker-model-v1",
            "credential://worker-primary");
        OctoberProviderProfile chat = new(
            "chat-primary",
            ApiType.ChatCompletions,
            "https://chat.example.test/v1",
            "chat-model-v1",
            "credential://chat-primary");
        List<OctoberProviderProfile> profiles = [worker, chat];
        Dictionary<string, string> protectedCredentials = new(StringComparer.Ordinal)
        {
            [worker.CredentialReference] = workerCiphertext,
            [chat.CredentialReference] = chatCiphertext,
        };
        OctoberProviderConfiguration configuration = new(
            "owned-host-document-v1",
            "only-source",
            worker.Name,
            chat.Name,
            profiles,
            protectedCredentials);

        profiles.Clear();
        protectedCredentials.Clear();
        ControlledSecretEncryptionService encryption = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [workerCiphertext] = "synthetic-worker-api-key",
            [chatCiphertext] = "synthetic-chat-api-key",
        });

        OctoberProviderBindingResult result = await new OctoberProviderBinder(encryption)
            .BindAsync(configuration);

        Assert.Equal(OctoberProviderBindingStatus.Bound, result.Status);
        OctoberBoundProviderConfiguration binding = Assert.IsType<OctoberBoundProviderConfiguration>(result.Binding);
        Assert.Equal("owned-host-document-v1", binding.SourceIdentity);
        Assert.Equal(worker, binding.Worker);
        Assert.Equal(chat, binding.Chat);
        Assert.Equal(2, encryption.Decryptions.Count);
        Assert.Contains((KeyPurpose, workerCiphertext), encryption.Decryptions);
        Assert.Contains((KeyPurpose, chatCiphertext), encryption.Decryptions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_UsesExistingDataProtectionPurposeForBothProviderKeys()
    {
        const string workerPlaintext = "synthetic-worker-api-key";
        const string chatPlaintext = "synthetic-chat-api-key";
        using ServiceProvider provider = CreateProtectionProvider();
        SecretEncryptionService encryption = new(provider.GetRequiredService<IDataProtectionProvider>());
        string workerCiphertext = await encryption.EncryptAsync(SecretPurposes.LlmApiKey, workerPlaintext);
        string chatCiphertext = await encryption.EncryptAsync(SecretPurposes.LlmApiKey, chatPlaintext);
        OctoberProviderConfiguration configuration = CreateCompleteConfiguration(
            workerCiphertext, chatCiphertext);

        OctoberProviderBindingResult result = await new OctoberProviderBinder(encryption)
            .BindAsync(configuration);

        Assert.Equal(OctoberProviderBindingStatus.Bound, result.Status);
        OctoberBoundProviderConfiguration binding = Assert.IsType<OctoberBoundProviderConfiguration>(result.Binding);
        Assert.Equal("worker-primary", binding.Worker.Name);
        Assert.Equal("chat-primary", binding.Chat.Name);
        Assert.Equal(nameof(OctoberProviderConfiguration), configuration.ToString());
        Assert.Equal(nameof(OctoberProviderProfile), binding.Worker.ToString());
        Assert.Equal(nameof(OctoberProviderProfile), binding.Chat.ToString());
        Assert.DoesNotContain(workerPlaintext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(chatPlaintext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(workerCiphertext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(chatCiphertext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("credential://worker-primary", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("credential://chat-primary", result.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("wrong-purpose")]
    [InlineData("wrong-ephemeral-key-set")]
    public async Task BindAsync_IncompatibleDataProtection_ReturnsUnavailableWithoutDisclosure(
        string incompatibility)
    {
        const string workerPlaintext = "synthetic-worker-api-key";
        const string chatPlaintext = "synthetic-chat-api-key";
        using ServiceProvider sourceProvider = CreateProtectionProvider();
        using ServiceProvider? alternateProvider = incompatibility == "wrong-ephemeral-key-set"
            ? CreateProtectionProvider()
            : null;
        SecretEncryptionService sourceEncryption = new(
            sourceProvider.GetRequiredService<IDataProtectionProvider>());
        IDataProtectionProvider bindingProtection = (alternateProvider ?? sourceProvider)
            .GetRequiredService<IDataProtectionProvider>();
        SecretEncryptionService bindingEncryption = new(bindingProtection);
        string protectionPurpose = incompatibility == "wrong-purpose"
            ? SecretPurposes.DatabaseConnection
            : SecretPurposes.LlmApiKey;
        string workerCiphertext = await sourceEncryption.EncryptAsync(protectionPurpose, workerPlaintext);
        string chatCiphertext = await sourceEncryption.EncryptAsync(protectionPurpose, chatPlaintext);
        OctoberProviderConfiguration configuration = CreateCompleteConfiguration(
            workerCiphertext, chatCiphertext);

        OctoberProviderBindingResult result = await new OctoberProviderBinder(bindingEncryption)
            .BindAsync(configuration);

        Assert.Equal(OctoberProviderBindingStatus.CredentialUnavailable, result.Status);
        Assert.Null(result.Binding);
        Assert.DoesNotContain(workerPlaintext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(chatPlaintext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(workerCiphertext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(chatCiphertext, result.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("empty-source")]
    [InlineData("whitespace-source")]
    [InlineData("wrong-precedence")]
    [InlineData("empty-worker-role")]
    [InlineData("whitespace-chat-role")]
    [InlineData("missing-worker-profile")]
    [InlineData("worker-profile-case-mismatch")]
    [InlineData("missing-chat-profile")]
    [InlineData("chat-profile-case-mismatch")]
    [InlineData("blank-selected-profile-name")]
    [InlineData("duplicate-selected-profile-name")]
    [InlineData("duplicate-unused-profile-name")]
    [InlineData("blank-worker-model")]
    [InlineData("blank-chat-model")]
    [InlineData("blank-worker-credential-reference")]
    [InlineData("blank-chat-credential-reference")]
    [InlineData("missing-selected-protected-entry")]
    [InlineData("blank-selected-protected-entry")]
    [InlineData("unknown-api-type")]
    [InlineData("combined-api-type")]
    [InlineData("blank-endpoint")]
    [InlineData("non-http-endpoint")]
    [InlineData("relative-endpoint")]
    [InlineData("endpoint-userinfo")]
    [InlineData("endpoint-fragment")]
    public async Task BindAsync_InvalidSelectedConfiguration_IsRejectedBeforeDecrypting(
        string invalidCase)
    {
        OctoberProviderConfiguration configuration = CreateInvalidConfiguration(invalidCase);
        ControlledSecretEncryptionService encryption = CreateEncryption();
        OctoberProviderBinder binder = new(encryption);

        OctoberProviderBindingResult result = await binder.BindAsync(configuration);

        Assert.Equal(OctoberProviderBindingStatus.InvalidConfiguration, result.Status);
        Assert.Null(result.Binding);
        Assert.Empty(encryption.Decryptions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("blank-plaintext")]
    [InlineData("decryption-exception")]
    public async Task BindAsync_SelectedCredentialFailure_ReturnsSanitizedUnavailableWithoutPartialBinding(
        string failureCase)
    {
        const string workerCiphertext = "synthetic-worker-ciphertext";
        const string chatCiphertext = "synthetic-chat-ciphertext";
        const string workerPlaintext = "synthetic-worker-api-key";
        const string chatPlaintext = "synthetic-chat-api-key";
        const string exceptionDetail = "synthetic-protected-input-exception-detail";
        OctoberProviderConfiguration configuration = CreateCompleteConfiguration();
        ControlledSecretEncryptionService encryption = new(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [workerCiphertext] = workerPlaintext,
                [chatCiphertext] = chatPlaintext,
            },
            failingCiphertext: chatCiphertext,
            blankPlaintext: failureCase == "blank-plaintext",
            failureDetail: failureCase == "decryption-exception" ? exceptionDetail : null);

        OctoberProviderBindingResult result = await new OctoberProviderBinder(encryption)
            .BindAsync(configuration);

        Assert.Equal(OctoberProviderBindingStatus.CredentialUnavailable, result.Status);
        Assert.Null(result.Binding);
        Assert.NotEmpty(encryption.Decryptions);
        Assert.All(encryption.Decryptions, call => Assert.Equal(KeyPurpose, call.Purpose));
        Assert.DoesNotContain(workerPlaintext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(chatPlaintext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(workerCiphertext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(chatCiphertext, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(exceptionDetail, result.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("before-first-decryption")]
    [InlineData("after-first-decryption")]
    public async Task BindAsync_CallerCancellationStopsFurtherCredentialDecryption(
        string cancellationPoint)
    {
        using CancellationTokenSource cancellation = new();
        bool cancelAfterFirstDecryption = cancellationPoint == "after-first-decryption";
        if (!cancelAfterFirstDecryption)
            cancellation.Cancel();

        ControlledSecretEncryptionService encryption = CreateEncryption(
            cancelAfterFirstDecryption ? cancellation : null);
        OctoberProviderBindingResult result = await new OctoberProviderBinder(encryption)
            .BindAsync(CreateCompleteConfiguration(), cancellation.Token);

        Assert.Equal(OctoberProviderBindingStatus.Cancelled, result.Status);
        Assert.Null(result.Binding);
        Assert.Equal(cancelAfterFirstDecryption ? 1 : 0, encryption.Decryptions.Count);
        Assert.All(encryption.Decryptions, call => Assert.Equal(KeyPurpose, call.Purpose));
    }

    private static OctoberProviderConfiguration CreateCompleteConfiguration(
        string workerCiphertext = "synthetic-worker-ciphertext",
        string chatCiphertext = "synthetic-chat-ciphertext")
    {
        OctoberProviderProfile worker = new(
            "worker-primary",
            ApiType.Responses,
            "https://worker.example.test/v1",
            "worker-model-v1",
            "credential://worker-primary");
        OctoberProviderProfile chat = new(
            "chat-primary",
            ApiType.ChatCompletions,
            "https://chat.example.test/v1",
            "chat-model-v1",
            "credential://chat-primary");
        return new OctoberProviderConfiguration(
            "owned-host-document-v1",
            "only-source",
            worker.Name,
            chat.Name,
            [worker, chat],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [worker.CredentialReference] = workerCiphertext,
                [chat.CredentialReference] = chatCiphertext,
            });
    }

    private static ServiceProvider CreateProtectionProvider()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        return services.BuildServiceProvider();
    }

    private static OctoberProviderConfiguration CreateInvalidConfiguration(string invalidCase)
    {
        OctoberProviderProfile worker = new(
            "worker-primary",
            ApiType.Responses,
            "https://worker.example.test/v1",
            "worker-model-v1",
            "credential://worker-primary");
        OctoberProviderProfile chat = new(
            "chat-primary",
            ApiType.ChatCompletions,
            "https://chat.example.test/v1",
            "chat-model-v1",
            "credential://chat-primary");
        List<OctoberProviderProfile> profiles = [worker, chat];
        Dictionary<string, string> protectedCredentials = new(StringComparer.Ordinal)
        {
            [worker.CredentialReference] = "synthetic-worker-ciphertext",
            [chat.CredentialReference] = "synthetic-chat-ciphertext",
        };
        string sourceIdentity = "owned-host-document-v1";
        string precedence = "only-source";
        string workerSelection = worker.Name;
        string chatSelection = chat.Name;

        switch (invalidCase)
        {
            case "empty-source":
                sourceIdentity = string.Empty;
                break;
            case "whitespace-source":
                sourceIdentity = "  ";
                break;
            case "wrong-precedence":
                precedence = "source-then-fallback";
                break;
            case "empty-worker-role":
                workerSelection = string.Empty;
                break;
            case "whitespace-chat-role":
                chatSelection = " ";
                break;
            case "missing-worker-profile":
                workerSelection = "worker-missing";
                break;
            case "worker-profile-case-mismatch":
                workerSelection = "WORKER-primary";
                break;
            case "missing-chat-profile":
                chatSelection = "chat-missing";
                break;
            case "chat-profile-case-mismatch":
                chatSelection = "CHAT-primary";
                break;
            case "blank-selected-profile-name":
                profiles[0] = worker with { Name = string.Empty };
                break;
            case "duplicate-selected-profile-name":
                profiles.Add(worker with { Model = "duplicate-worker-model" });
                break;
            case "duplicate-unused-profile-name":
                OctoberProviderProfile unused = worker with
                {
                    Name = "unused-profile",
                    CredentialReference = "credential://unused",
                };
                profiles.Add(unused);
                profiles.Add(unused with { Model = "duplicate-unused-model" });
                break;
            case "blank-worker-model":
                profiles[0] = worker with { Model = " " };
                break;
            case "blank-chat-model":
                profiles[1] = chat with { Model = string.Empty };
                break;
            case "blank-worker-credential-reference":
                profiles[0] = worker with { CredentialReference = string.Empty };
                break;
            case "blank-chat-credential-reference":
                profiles[1] = chat with { CredentialReference = " " };
                break;
            case "missing-selected-protected-entry":
                profiles[1] = chat with { CredentialReference = "credential://missing" };
                break;
            case "blank-selected-protected-entry":
                protectedCredentials[chat.CredentialReference] = string.Empty;
                break;
            case "unknown-api-type":
                profiles[0] = worker with { ApiType = (ApiType)8 };
                break;
            case "combined-api-type":
                profiles[0] = worker with { ApiType = ApiType.Responses | ApiType.Messages };
                break;
            case "blank-endpoint":
                profiles[0] = worker with { Endpoint = string.Empty };
                break;
            case "non-http-endpoint":
                profiles[0] = worker with { Endpoint = "ftp://worker.example.test/v1" };
                break;
            case "relative-endpoint":
                profiles[0] = worker with { Endpoint = "/v1" };
                break;
            case "endpoint-userinfo":
                profiles[0] = worker with { Endpoint = "https://user:password@worker.example.test/v1" };
                break;
            case "endpoint-fragment":
                profiles[0] = worker with { Endpoint = "https://worker.example.test/v1#fragment" };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidCase));
        }

        return new OctoberProviderConfiguration(
            sourceIdentity,
            precedence,
            workerSelection,
            chatSelection,
            profiles,
            protectedCredentials);
    }

    private static ControlledSecretEncryptionService CreateEncryption(
        CancellationTokenSource? cancelAfterFirstDecryption = null) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["synthetic-worker-ciphertext"] = "synthetic-worker-api-key",
            ["synthetic-chat-ciphertext"] = "synthetic-chat-api-key",
        }, cancelAfterFirstDecryption: cancelAfterFirstDecryption);

    private sealed class ControlledSecretEncryptionService(
        IReadOnlyDictionary<string, string> plaintextByCiphertext,
        string? failingCiphertext = null,
        bool blankPlaintext = false,
        string? failureDetail = null,
        CancellationTokenSource? cancelAfterFirstDecryption = null) : ISecretEncryptionService
    {
        public List<(string Purpose, string Ciphertext)> Decryptions { get; } = [];

        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The provider-binding tracer only decrypts selected credentials.");

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default)
        {
            Decryptions.Add((purpose, cipher));
            if (Decryptions.Count == 1)
                cancelAfterFirstDecryption?.Cancel();
            if (string.Equals(cipher, failingCiphertext, StringComparison.Ordinal))
            {
                return failureDetail is not null
                    ? Task.FromException<string>(new InvalidOperationException(failureDetail))
                    : Task.FromResult(blankPlaintext ? string.Empty : plaintextByCiphertext[cipher]);
            }

            return Task.FromResult(plaintextByCiphertext.TryGetValue(cipher, out string? plaintext)
                ? plaintext
                : string.Empty);
        }
    }
}
