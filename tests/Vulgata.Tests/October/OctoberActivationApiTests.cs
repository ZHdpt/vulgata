using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October;

public sealed class OctoberActivationApiTests
{
    private const string TargetA = "october-target-a";
    private const string TargetB = "october-target-b";
    private const string FingerprintA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void StartupSelector_AuthorizationOnlyMode_SelectsOnlyBootstrap()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["October:Web:Mode"] = "authorization-only",
            })
            .Build();

        OctoberWebStartupSelection selection = OctoberWebStartupSelector.Resolve(configuration);

        Assert.Equal(
            new OctoberWebStartupSelection(OctoberWebMode.AuthorizationOnly, null, null, null),
            selection);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void StartupSelector_AbsentModeKeepsOrdinaryStartup()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        OctoberWebStartupSelection selection = OctoberWebStartupSelector.Resolve(configuration);

        Assert.Equal(new OctoberWebStartupSelection(OctoberWebMode.Ordinary, null, null, null), selection);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberCertificateIdentityStatus.Authenticated, FingerprintA, TargetA, OctoberGeneratorCertificateStatus.Authorized)]
    [InlineData(OctoberCertificateIdentityStatus.Authenticated, FingerprintA, TargetB, OctoberGeneratorCertificateStatus.Forbidden)]
    [InlineData(OctoberCertificateIdentityStatus.Authenticated, FingerprintB, TargetA, OctoberGeneratorCertificateStatus.Forbidden)]
    [InlineData(OctoberCertificateIdentityStatus.Authenticated, null, TargetA, OctoberGeneratorCertificateStatus.Forbidden)]
    [InlineData(OctoberCertificateIdentityStatus.Unauthenticated, FingerprintA, TargetA, OctoberGeneratorCertificateStatus.Unauthenticated)]
    [InlineData(OctoberCertificateIdentityStatus.AuthorityUnavailable, null, TargetA, OctoberGeneratorCertificateStatus.AuthorityUnavailable)]
    [InlineData(OctoberCertificateIdentityStatus.Cancelled, null, TargetA, OctoberGeneratorCertificateStatus.Cancelled)]
    public async Task CertificateVerifier_UsesAuthenticatedFingerprintAndExactTargetScope(
        OctoberCertificateIdentityStatus identityStatus,
        string? fingerprint,
        string targetId,
        OctoberGeneratorCertificateStatus expectedStatus)
    {
        ControlledCertificateSource source = new(new OctoberCertificateIdentityResult(identityStatus, fingerprint));
        OctoberGeneratorCertificateVerifier verifier = new(source, CreateAllowlist());

        OctoberGeneratorCertificateDecision decision = await verifier.VerifyAsync(
            new DefaultHttpContext(),
            targetId,
            CancellationToken.None);

        Assert.Equal(expectedStatus, decision.Status);
        Assert.Equal(1, source.ReadCount);
        Assert.Equal(CancellationToken.None, source.CancellationToken);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CertificateVerifier_DoesNotTreatRequestHeaderCookieOrBodyAsCertificateProof()
    {
        ControlledCertificateSource source = new(new OctoberCertificateIdentityResult(
            OctoberCertificateIdentityStatus.Unauthenticated,
            FingerprintA));
        OctoberGeneratorCertificateVerifier verifier = new(source, CreateAllowlist());
        DefaultHttpContext context = new();
        context.Request.Headers["X-Client-Certificate-Fingerprint"] = FingerprintA;
        context.Request.Headers.Cookie = $"ClientCertificate={FingerprintA}";
        context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            $$"""{"certificateFingerprint":"{{FingerprintA}}"}"""));

        OctoberGeneratorCertificateDecision decision = await verifier.VerifyAsync(
            context,
            TargetA,
            CancellationToken.None);

        Assert.Equal(OctoberGeneratorCertificateStatus.Unauthenticated, decision.Status);
        Assert.Equal(1, source.ReadCount);
    }

    private static OctoberGeneratorCertificateAllowlist CreateAllowlist() => new(
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [TargetA] = new HashSet<string>([FingerprintA], StringComparer.Ordinal),
            [TargetB] = new HashSet<string>([FingerprintB], StringComparer.Ordinal),
        });

    private sealed class ControlledCertificateSource(OctoberCertificateIdentityResult result)
        : IOctoberAuthenticatedCertificateSource
    {
        public int ReadCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<OctoberCertificateIdentityResult> ReadAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            CancellationToken = cancellationToken;
            return Task.FromResult(result);
        }
    }
}
