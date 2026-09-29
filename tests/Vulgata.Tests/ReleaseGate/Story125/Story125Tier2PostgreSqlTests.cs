using Vulgata.Tests.TestInfrastructure;
using System.Reflection;

namespace Vulgata.Tests.ReleaseGate;

public sealed class Story125Tier2PostgreSqlTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Story125DatabaseNameValidator_GeneratesUniqueLowercaseNamesAndRejectsProtectedTargets()
    {
        Type? harnessType = typeof(Story125Tier2PostgreSqlTests).Assembly.GetType(
            "Vulgata.Tests.ReleaseGate.Story125ReleaseHarness");
        Assert.NotNull(harnessType);

        MethodInfo? createRunIdentity = harnessType!.GetMethod(
            "CreateRunIdentity",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        Assert.NotNull(createRunIdentity);

        MethodInfo? isOwnedDatabaseName = harnessType.GetMethod(
            "IsOwnedDatabaseName",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string)],
            modifiers: null);
        Assert.NotNull(isOwnedDatabaseName);

        object firstIdentity = createRunIdentity!.Invoke(null, null) ??
            throw new InvalidOperationException("The Story 12.5 run identity was not created.");
        object secondIdentity = createRunIdentity.Invoke(null, null) ??
            throw new InvalidOperationException("The Story 12.5 run identity was not created.");
        string firstName = ReadDatabaseName(firstIdentity);
        string secondName = ReadDatabaseName(secondIdentity);

        Assert.NotEqual(firstName, secondName);
        Assert.Matches("^vulgata_story125_[a-z0-9_]{8,48}$", firstName);
        Assert.Matches("^vulgata_story125_[a-z0-9_]{8,48}$", secondName);
        Assert.Equal(firstName.ToLowerInvariant(), firstName);
        Assert.Equal(secondName.ToLowerInvariant(), secondName);
        Assert.True(IsOwned(isOwnedDatabaseName, firstName));
        Assert.True(IsOwned(isOwnedDatabaseName, secondName));

        string[] protectedOrMalformedNames =
        [
            "postgres",
            "template0",
            "template1",
            "vulgata",
            "vulgata_story124_00000000000000000000000000000000",
            "vulgata_story125_",
            "vulgata_story125_not-allowed",
            "VULGATA_STORY125_00000000000000000000000000000000",
        ];
        Assert.All(
            protectedOrMalformedNames,
            name => Assert.False(IsOwned(isOwnedDatabaseName, name), name));
    }

    private static string ReadDatabaseName(object runIdentity)
    {
        PropertyInfo? databaseName = runIdentity.GetType().GetProperty(
            "DatabaseName",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(databaseName);
        return Assert.IsType<string>(databaseName!.GetValue(runIdentity));
    }

    private static bool IsOwned(MethodInfo validator, string databaseName) =>
        Assert.IsType<bool>(validator.Invoke(null, [databaseName]));
}
