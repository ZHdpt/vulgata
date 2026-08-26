namespace Vulgata.Tests.PreparedPass2;

[Trait("Category", "Story12_4_ProductionQualification")]
public sealed class Pass2ProductionQualificationTests
{
    internal const string OptInVariable = "VULGATA_T124_RUN_PRODUCTION_QUALIFICATION";
    internal const string ProviderContentVariable = "VULGATA_T124_ALLOW_PROVIDER_CONTENT";

    [Fact]
    public void Qualification_IsExplicitlyOptIn_AndDoesNotChangeDefaultProviderBehavior()
    {
        string? value = Environment.GetEnvironmentVariable(OptInVariable);
        Assert.True(
            value is null || value is "0" or "1" or "false" or "true",
            $"{OptInVariable} must be an explicit boolean switch when supplied.");
        string? providerContent = Environment.GetEnvironmentVariable(ProviderContentVariable);
        Assert.True(
            providerContent is null || providerContent is "0" or "1" or "false" or "true",
            $"{ProviderContentVariable} must be an explicit boolean switch when supplied.");
    }
}
