namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class CodeGraphImageQualificationTests
{
    [Fact]
    public void QualificationImage_WhenReadFromRepository_RequiresHashLockedCpythonAndExactCgcContract()
    {
        string dockerfilePath = QualificationTestPaths.FromRepositoryRoot("docker", "Dockerfile");
        string pythonVersionPath = QualificationTestPaths.FromRepositoryRoot("docker", "codegraph", "python-version.txt");
        string requirementsLockPath = QualificationTestPaths.FromRepositoryRoot("docker", "codegraph", "requirements.lock");
        string dockerfile = File.ReadAllText(dockerfilePath);

        Assert.True(File.Exists(pythonVersionPath), "Qualification requires a committed CPython 3.12.x lock.");
        Assert.True(File.Exists(requirementsLockPath), "Qualification requires a hash-verified Python lock.");

        string pythonVersion = File.ReadAllText(pythonVersionPath).Trim();
        string requirementsLock = File.ReadAllText(requirementsLockPath);

        Assert.Matches("^3\\.12\\.\\d+$", pythonVersion);
        Assert.Contains(
            "python:3.12-bookworm@sha256:9bed8554e926c07c6f908841d5ee88c33e8df9236b191526bbce81a9062ab43a",
            dockerfile,
            StringComparison.Ordinal);
        Assert.Contains("--require-hashes", dockerfile, StringComparison.Ordinal);
        Assert.Contains("codegraphcontext==0.5.1", requirementsLock, StringComparison.Ordinal);
        Assert.Contains("kuzu==", requirementsLock, StringComparison.Ordinal);
        Assert.Contains("--hash=sha256:", requirementsLock, StringComparison.Ordinal);
        Assert.Contains("USER app", dockerfile, StringComparison.Ordinal);
        Assert.Contains("cgc", dockerfile, StringComparison.Ordinal);
        Assert.DoesNotContain("CLI shim", dockerfile, StringComparison.OrdinalIgnoreCase);
    }
}
