namespace Vulgata.Tests.CodeGraphQualification;

internal static class QualificationTestPaths
{
    internal static string Attempt0002RepositoryPath =>
        FromRepositoryRoot(
            "tests",
            "Vulgata.Tests",
            "CodeGraphQualification",
            "ProviderFixtures",
            "Attempt0002Repository");

    internal static string RepositoryRoot
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Repository root could not be found.");
        }
    }

    internal static string FromRepositoryRoot(params string[] segments) =>
        Path.Combine([RepositoryRoot, .. segments]);
}
