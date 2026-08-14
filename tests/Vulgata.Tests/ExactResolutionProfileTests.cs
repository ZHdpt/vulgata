using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.CrossRepository;

namespace Vulgata.Tests;

public sealed class ExactResolutionProfileTests
{
    private const string ReviewedFingerprint =
        "5d621ca3a09d88305abc2a3661747ef4c9e04b3016f54baced7dc9c113e434bb";

    [Fact]
    public void Load_ReviewedEmbeddedResource_MatchesAcceptedBytesAndFingerprint()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        byte[] accepted = File.ReadAllBytes(FindAcceptedProfilePath());

        Assert.Equal(accepted, profile.CanonicalUtf8Json.ToArray());
        Assert.Equal(ReviewedFingerprint, profile.ProfileFingerprint);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(accepted)).ToLowerInvariant(),
            profile.ProfileFingerprint);
    }

    [Fact]
    public void Load_ReviewedEmbeddedResource_ExposesExactReviewedSelectorsInCanonicalOrder()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();

        Assert.Equal("1.0", profile.SchemaVersion);
        Assert.Equal("august15-exact-cross-repository-v1", profile.ProfileId);
        Assert.Equal("1.0.0", profile.ProfileVersion);

        Assert.Collection(
            profile.Repositories,
            repository => AssertRepository(
                repository,
                "mbs_account",
                "19c60cd3f3976b7697c487c7bafd96142e3a7625"),
            repository => AssertRepository(
                repository,
                "mbs_channel",
                "47f6b627a7598f862873610413eec1e1e7bf46bf"));

        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        Assert.Equal("account-to-channel-cust-update-session-v1", occurrence.OccurrenceId);
        Assert.Equal(ExactResolutionProtocol.CustomRpc, occurrence.Protocol);
        Assert.Equal("mbs_account", occurrence.ConsumerAlias);
        Assert.Equal("mbs_channel", occurrence.ProviderAlias);
        Assert.Equal(ExactResolutionComparison.Ordinal, occurrence.Comparison);
        Assert.Equal("custom-rpc-in-prefix-v1", occurrence.Normalization.Id);
        Assert.Equal(string.Empty, occurrence.Normalization.ConsumerRequiredPrefix);
        Assert.Equal("in/", occurrence.Normalization.ProviderRequiredPrefix);

        Assert.Equal(
            "src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java",
            Assert.Single(occurrence.Consumer.Files));
        Assert.Equal(ExactInvocationForm.SimpleMemberInvocation, occurrence.Consumer.Invocation.Form);
        Assert.Equal("netTools", occurrence.Consumer.Invocation.ReceiverSymbol);
        Assert.Equal("execute", occurrence.Consumer.Invocation.MemberName);
        Assert.Equal(1, occurrence.Consumer.Invocation.EndpointArgumentIndex);
        Assert.Equal(45, occurrence.Consumer.Invocation.Line);
        Assert.Equal(0, occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine);

        Assert.Equal("src/main/resources/disgram/in", occurrence.Provider.Root);
        Assert.Equal(
            "src/main/resources/disgram/in/cust/updateSession.xml",
            Assert.Single(occurrence.Provider.Files));
        Assert.Equal("trans", occurrence.Provider.Declaration.ElementLocalName);
        Assert.Equal("name", occurrence.Provider.Declaration.EndpointAttributeLocalName);
        Assert.Equal(2, occurrence.Provider.Declaration.Line);
        Assert.Equal(0, occurrence.Provider.Declaration.OccurrenceOrdinalOnLine);
        Assert.Equal(
            "src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java",
            occurrence.Provider.ImplementationDocument.RepositoryRelativePath);
        Assert.Equal(ExactSymbolKind.Type, occurrence.Provider.ImplementationDocument.SymbolKind);
        Assert.Equal(
            "cn.com.yitong.actions.domain.login.UpdateSessionOp",
            occurrence.Provider.ImplementationDocument.Symbol);

        UnsupportedObservation observation = Assert.Single(profile.UnsupportedObservations);
        Assert.Equal("account-file-storage-oss-upload-v1", observation.ObservationId);
        Assert.Equal(ExactUnsupportedObservationKind.FileStorage, observation.Kind);
        Assert.Equal("mbs_account", observation.OwnerAlias);
        Assert.Equal(ExactInvocationForm.SimpleMemberInvocation, observation.Selector.Form);
        Assert.Equal("ossClientService", observation.Selector.ReceiverSymbol);
        Assert.Equal("simpleUploadToOSSBase64String", observation.Selector.MemberName);
        Assert.Equal(66, observation.Selector.Line);
        Assert.Equal(0, observation.Selector.OccurrenceOrdinalOnLine);
        Assert.Equal(
            "src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java#line=66;rule=unsupported-member-invocation-v1",
            observation.EvidenceLocator);
    }

    [Fact]
    public void Create_RuntimeRequest_RequiresCompleteDistinctCanonicalAliasBindings()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        Guid systemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        ExactResolutionRuntimeBinding account = Binding(
            "mbs_account",
            "20000000-0000-0000-0000-000000000001");
        ExactResolutionRuntimeBinding channel = Binding(
            "mbs_channel",
            "20000000-0000-0000-0000-000000000002");

        ExactResolutionRuntimeRequest request = ExactResolutionRuntimeRequest.Create(
            systemId,
            profile,
            [channel, account]);

        Assert.Equal(systemId, request.SystemId);
        Assert.Equal(profile.ProfileFingerprint, request.ProfileFingerprint);
        Assert.Collection(
            request.Bindings,
            binding => Assert.Equal("mbs_account", binding.Alias),
            binding => Assert.Equal("mbs_channel", binding.Alias));

        Assert.Throws<ArgumentException>(() => ExactResolutionRuntimeRequest.Create(
            systemId,
            profile,
            [account]));
        Assert.Throws<ArgumentException>(() => ExactResolutionRuntimeRequest.Create(
            systemId,
            profile,
            [account, account]));
        Assert.Throws<ArgumentException>(() => ExactResolutionRuntimeRequest.Create(
            systemId,
            profile,
            [account, Binding("unexpected", "20000000-0000-0000-0000-000000000003")]));
        Assert.Throws<ArgumentException>(() => ExactResolutionRuntimeRequest.Create(
            systemId,
            profile,
            [account, channel with { RepositoryId = account.RepositoryId }]));
    }

    [Fact]
    public void BindForValidation_CrossSystemBinding_IsIndistinguishablePreEvidenceRefusal()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        Guid systemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        ExactResolutionRuntimeBinding account = Binding(
            "mbs_account",
            "20000000-0000-0000-0000-000000000001");
        ExactResolutionRuntimeBinding channel = Binding(
            "mbs_channel",
            "20000000-0000-0000-0000-000000000002");
        ExactResolutionRuntimeBindingEvidence[] evidence =
        [
            Evidence(account, systemId, profile.Repositories[0].ExpectedCommit),
            Evidence(channel, Guid.NewGuid(), profile.Repositories[1].ExpectedCommit),
        ];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ExactResolutionProfileCatalog.BindForValidation(
                systemId,
                profile,
                profile.ProfileId,
                profile.ProfileVersion,
                profile.ProfileFingerprint,
                [account, channel],
                evidence));

        Assert.Equal("精确跨仓库解析请求被拒绝。", exception.Message);
        Assert.DoesNotContain(systemId.ToString(), exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void BindForValidation_CompleteOwnedRevisionEvidence_ReturnsCanonicalRequest()
    {
        MethodInfo? gate = typeof(ExactResolutionProfileCatalog).GetMethod(
            nameof(ExactResolutionProfileCatalog.BindForValidation),
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(gate);
        Assert.True(typeof(ExactResolutionRuntimeBindingEvidence).IsPublic);
        Assert.Contains(
            typeof(ExactResolutionRuntimeBindingEvidence),
            gate.GetParameters().Select(parameter => parameter.ParameterType)
                .SelectMany(type => type.IsGenericType ? type.GetGenericArguments() : [type]));

        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        Guid systemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        ExactResolutionRuntimeBinding account = Binding(
            "mbs_account",
            "20000000-0000-0000-0000-000000000001");
        ExactResolutionRuntimeBinding channel = Binding(
            "mbs_channel",
            "20000000-0000-0000-0000-000000000002");

        ExactResolutionRuntimeRequest request = ExactResolutionProfileCatalog.BindForValidation(
            systemId,
            profile,
            profile.ProfileId,
            profile.ProfileVersion,
            profile.ProfileFingerprint,
            [channel, account],
            [
                Evidence(channel, systemId, profile.Repositories[1].ExpectedCommit),
                Evidence(account, systemId, profile.Repositories[0].ExpectedCommit),
            ]);

        Assert.Collection(
            request.Bindings,
            binding => Assert.Equal("mbs_account", binding.Alias),
            binding => Assert.Equal("mbs_channel", binding.Alias));
    }

    [Fact]
    public void BindForValidation_InvalidBindingProvenanceOrRevision_UsesOneBoundedRefusal()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        Guid systemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        ExactResolutionRuntimeBinding account = Binding(
            "mbs_account",
            "20000000-0000-0000-0000-000000000001");
        ExactResolutionRuntimeBinding channel = Binding(
            "mbs_channel",
            "20000000-0000-0000-0000-000000000002");
        ExactResolutionRuntimeBinding unexpected = Binding(
            "unexpected",
            "20000000-0000-0000-0000-000000000003");
        ExactResolutionRuntimeBindingEvidence accountEvidence = Evidence(
            account,
            systemId,
            profile.Repositories[0].ExpectedCommit);
        ExactResolutionRuntimeBindingEvidence channelEvidence = Evidence(
            channel,
            systemId,
            profile.Repositories[1].ExpectedCommit);
        ExactResolutionRuntimeBindingEvidence extraEvidence = Evidence(
            unexpected,
            systemId,
            profile.Repositories[0].ExpectedCommit);
        var access = new ExactCrossRepositoryAccessProbe(
            systemId,
            account.RepositoryId,
            channel.RepositoryId);

        AssertRefused([account], [accountEvidence, channelEvidence]);
        AssertRefused([account, account], [accountEvidence, channelEvidence]);
        AssertRefused([account, unexpected], [accountEvidence, channelEvidence]);
        AssertRefused(
            [account, channel with { RepositoryId = account.RepositoryId }],
            [accountEvidence, channelEvidence]);
        AssertRefused([account, channel], [accountEvidence]);
        AssertRefused([account, channel], [accountEvidence, accountEvidence]);
        AssertRefused([account, channel], [accountEvidence, channelEvidence, extraEvidence]);
        AssertRefused(
            [account, channel],
            [accountEvidence, channelEvidence with { RepositoryId = account.RepositoryId }]);
        AssertRefused(
            [account, channel],
            [accountEvidence, channelEvidence with { SystemId = Guid.NewGuid() }]);
        AssertRefused(
            [account, channel],
            [accountEvidence, channelEvidence with { StableGitIdentity = "different" }]);
        AssertRefused(
            [account, channel],
            [accountEvidence, channelEvidence with { ScanRunId = Guid.NewGuid() }]);
        AssertRefused(
            [account, channel],
            [accountEvidence, channelEvidence with { ScanExecutionId = Guid.NewGuid() }]);
        AssertRefused(
            [account, channel],
            [accountEvidence, channelEvidence with { SnapshotProfiledRevision = WrongCommit() }]);
        AssertRefused(
            [account, channel],
            [accountEvidence, channelEvidence with { ScanRunProfiledRevision = WrongCommit() }]);
        AssertRefused(
            [account, channel],
            [accountEvidence, channelEvidence with { ScanExecutionProfiledRevision = WrongCommit() }]);
        AssertRefused(
            [account, channel],
            [accountEvidence, channelEvidence with { DocumentVersionProfiledRevision = WrongCommit() }]);
        AssertRefused(
            [account, channel],
            [accountEvidence, channelEvidence with { SourceAnchorProfiledRevision = WrongCommit() }]);
        AssertRefused([account, channel], [accountEvidence, channelEvidence], profileId: "different");
        AssertRefused([account, channel], [accountEvidence, channelEvidence], profileVersion: "9.9.9");
        AssertRefused([account, channel], [accountEvidence, channelEvidence], profileFingerprint: WrongCommit());
        Assert.True(access.Snapshot().IsZero);

        void AssertRefused(
            IReadOnlyCollection<ExactResolutionRuntimeBinding> bindings,
            IReadOnlyCollection<ExactResolutionRuntimeBindingEvidence> evidence,
            string? profileId = null,
            string? profileVersion = null,
            string? profileFingerprint = null)
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                ExactResolutionProfileCatalog.BindForValidation(
                    systemId,
                    profile,
                    profileId ?? profile.ProfileId,
                    profileVersion ?? profile.ProfileVersion,
                    profileFingerprint ?? profile.ProfileFingerprint,
                    bindings,
                    evidence));
            Assert.Equal("精确跨仓库解析请求被拒绝。", exception.Message);
            Assert.Null(exception.InnerException);
            Assert.DoesNotContain("mbs_", exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(systemId.ToString(), exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidProfileBytes))]
    public void ParseForValidation_OpenMalformedOrNoncanonicalResource_ThrowsInvalidDataException(
        byte[] bytes)
    {
        Assert.Throws<InvalidDataException>(() =>
            ExactResolutionProfileCatalog.ParseForValidation(bytes));
    }

    [Fact]
    public void Load_ReviewedEmbeddedResource_ContainsNoDeploymentOrPersistenceSecrets()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        string json = Encoding.UTF8.GetString(profile.CanonicalUtf8Json.Span);

        string[] forbidden =
        [
            "repositoryId",
            "scanRunId",
            "scanExecutionId",
            "documentVersionId",
            "sourceAnchorId",
            "gitUrl",
            "http://",
            "https://",
            "ssh://",
            "${",
            "%",
            "password",
            "credential",
            "token",
            "C:\\\\",
            "\\\\\\\\",
            "/home/",
            "/mnt/",
            "/workspace/",
        ];

        Assert.All(forbidden, value => Assert.DoesNotContain(value, json, StringComparison.OrdinalIgnoreCase));
    }

    public static TheoryData<byte[]> InvalidProfileBytes()
    {
        string accepted = File.ReadAllText(FindAcceptedProfilePath(), Encoding.UTF8)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        TheoryData<byte[]> cases = new();
        cases.Add(Utf8(accepted.Replace(
            "{\n  \"schemaVersion\"",
            "{\n  \"unknown\": true,\n  \"schemaVersion\"",
            StringComparison.Ordinal)));
        cases.Add(Utf8(accepted.Replace(
            "  \"schemaVersion\": \"1.0\",",
            "  \"schemaVersion\": \"1.0\",\n  \"schemaVersion\": \"1.0\",",
            StringComparison.Ordinal)));
        cases.Add(Utf8(accepted + "{}"));
        cases.Add(Utf8(new string('[', 17) + "0" + new string(']', 17)));
        cases.Add(new byte[(256 * 1024) + 1]);
        cases.Add(Utf8(accepted.Replace(
            "  \"schemaVersion\": \"1.0\",\n  \"profileId\": \"august15-exact-cross-repository-v1\",",
            "  \"profileId\": \"august15-exact-cross-repository-v1\",\n  \"schemaVersion\": \"1.0\",",
            StringComparison.Ordinal)));
        cases.Add(Utf8(accepted.Replace(
            "\"alias\": \"mbs_channel\"",
            "\"alias\": \"mbs_account\"",
            StringComparison.Ordinal)));
        cases.Add(Utf8(accepted.Replace(
            "refs/tags/vulgata/August15th-sample",
            "https://example.invalid/repository.git",
            StringComparison.Ordinal)));
        cases.Add(Utf8(accepted.Replace(
            "refs/tags/vulgata/August15th-sample",
            "refs/tags/vulgata/August15th-sample.lock",
            StringComparison.Ordinal)));
        return cases;
    }

    private static void AssertRepository(
        ProfileRepository repository,
        string alias,
        string expectedCommit)
    {
        Assert.Equal(alias, repository.Alias);
        Assert.Equal(alias, repository.StableGitIdentity);
        Assert.Equal("refs/tags/vulgata/August15th-sample", repository.ExpectedRef);
        Assert.Equal(expectedCommit, repository.ExpectedCommit);
    }

    private static ExactResolutionRuntimeBinding Binding(string alias, string repositoryId) => new(
        alias,
        alias,
        Guid.Parse(repositoryId),
        Guid.NewGuid(),
        Guid.NewGuid());

    private static ExactResolutionRuntimeBindingEvidence Evidence(
        ExactResolutionRuntimeBinding binding,
        Guid systemId,
        string revision) => new(
            binding.Alias,
            binding.StableGitIdentity,
            binding.RepositoryId,
            systemId,
            binding.ScanRunId,
            binding.ScanExecutionId,
            revision,
            revision,
            revision,
            revision,
            revision);

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static string WrongCommit() => new('0', 40);

    private static string FindAcceptedProfilePath()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory,
                "specs",
                "007-exact-cross-repo-resolution",
                "contracts",
                "demo-resolution-profile.v1.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new FileNotFoundException("The accepted demo-resolution profile was not found.");
    }
}
