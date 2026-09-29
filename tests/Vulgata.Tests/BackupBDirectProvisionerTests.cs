using Vulgata.Tests.TestInfrastructure;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.BackupB;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Git.BackupB;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

/// <summary>
/// Backup B provisioner focused input validation tests.
/// </summary>
public sealed class BackupBDirectProvisionerTests
{
    #region Input Validation Tests

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ProvisionAsync_NullRequest_ThrowsArgumentNullException()
    {
        var provisioner = CreateProvisioner();
        BackupBProvisioningRequest? request = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provisioner.ProvisionAsync(request!, CancellationToken.None));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBProvisioningRequest_Validate_EmptySystemName_ThrowsArgumentException()
    {
        var request = new BackupBProvisioningRequest
        {
            SystemName = "",
            SystemDescription = "Test",
            Repositories = new Dictionary<string, BackupBRepositoryInput>
            {
                ["repo1"] = CreateValidRepositoryInput()
            }
        };

        var ex = Assert.Throws<ArgumentException>(() => request.Validate());
        Assert.Contains("System name", ex.Message);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBProvisioningRequest_Validate_EmptyRepositories_ThrowsArgumentException()
    {
        var request = new BackupBProvisioningRequest
        {
            SystemName = "Test System",
            SystemDescription = "Test",
            Repositories = new Dictionary<string, BackupBRepositoryInput>()
        };

        var ex = Assert.Throws<ArgumentException>(() => request.Validate());
        Assert.Contains("repository", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBRepositoryInput_Validate_EmptyName_ThrowsArgumentException()
    {
        var input = new BackupBRepositoryInput
        {
            Name = "",
            Description = "Test repo",
            CloneUrl = "https://example.com/repo.git",
            LocalGitPath = @"C:\repos\test",
            ExpectedCommit = new string('a', 40),
            ManifestEntries = new List<BackupBManifestEntry> { CreateValidManifestEntry("repo1") }
        };

        var ex = Assert.Throws<ArgumentException>(() => input.Validate("repo1"));
        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBRepositoryInput_Validate_EmptyCloneUrl_ThrowsArgumentException()
    {
        var input = new BackupBRepositoryInput
        {
            Name = "Test Repo",
            Description = "Test repo",
            CloneUrl = "",
            LocalGitPath = @"C:\repos\test",
            ExpectedCommit = new string('a', 40),
            ManifestEntries = new List<BackupBManifestEntry> { CreateValidManifestEntry("repo1") }
        };

        var ex = Assert.Throws<ArgumentException>(() => input.Validate("repo1"));
        Assert.Contains("Clone URL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBRepositoryInput_Validate_EmptyLocalGitPath_ThrowsArgumentException()
    {
        var input = new BackupBRepositoryInput
        {
            Name = "Test Repo",
            Description = "Test repo",
            CloneUrl = "https://example.com/repo.git",
            LocalGitPath = "",
            ExpectedCommit = new string('a', 40),
            ManifestEntries = new List<BackupBManifestEntry> { CreateValidManifestEntry("repo1") }
        };

        var ex = Assert.Throws<ArgumentException>(() => input.Validate("repo1"));
        Assert.Contains("Local git path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBRepositoryInput_Validate_EmptyCommit_ThrowsArgumentException()
    {
        var input = new BackupBRepositoryInput
        {
            Name = "Test Repo",
            Description = "Test repo",
            CloneUrl = "https://example.com/repo.git",
            LocalGitPath = @"C:\repos\test",
            ExpectedCommit = "",
            ManifestEntries = new List<BackupBManifestEntry> { CreateValidManifestEntry("repo1") }
        };

        var ex = Assert.Throws<ArgumentException>(() => input.Validate("repo1"));
        Assert.Contains("commit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBRepositoryInput_Validate_EmptyManifestEntries_ThrowsArgumentException()
    {
        var input = new BackupBRepositoryInput
        {
            Name = "Test Repo",
            Description = "Test repo",
            CloneUrl = "https://example.com/repo.git",
            LocalGitPath = @"C:\repos\test",
            ExpectedCommit = new string('a', 40),
            ManifestEntries = new List<BackupBManifestEntry>()
        };

        var ex = Assert.Throws<ArgumentException>(() => input.Validate("repo1"));
        Assert.Contains("manifest", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Manifest Entry Validation Tests

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBManifestEntry_Validate_EmptyFilePath_ThrowsArgumentException()
    {
        var entry = new BackupBManifestEntry
        {
            RepositoryAlias = "repo1",
            ExactCommit = new string('a', 40),
            FilePath = "",
            CodeUnits = new List<BackupBCodeUnitEntry> { CreateValidCodeUnitEntry() }
        };

        var ex = Assert.Throws<ArgumentException>(() => entry.Validate());
        Assert.Contains("File path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBManifestEntry_Validate_LeadingSlash_ThrowsArgumentException()
    {
        var entry = new BackupBManifestEntry
        {
            RepositoryAlias = "repo1",
            ExactCommit = new string('a', 40),
            FilePath = "/src/test.cs",
            CodeUnits = new List<BackupBCodeUnitEntry> { CreateValidCodeUnitEntry() }
        };

        var ex = Assert.Throws<ArgumentException>(() => entry.Validate());
        Assert.Contains("repository-relative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBManifestEntry_Validate_DirectoryTraversal_ThrowsArgumentException()
    {
        var entry = new BackupBManifestEntry
        {
            RepositoryAlias = "repo1",
            ExactCommit = new string('a', 40),
            FilePath = "../src/test.cs",
            CodeUnits = new List<BackupBCodeUnitEntry> { CreateValidCodeUnitEntry() }
        };

        var ex = Assert.Throws<ArgumentException>(() => entry.Validate());
        Assert.Contains("traversal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBManifestEntry_Validate_InvalidCommitLength_ThrowsArgumentException()
    {
        var entry = new BackupBManifestEntry
        {
            RepositoryAlias = "repo1",
            ExactCommit = "short",
            FilePath = "src/test.cs",
            CodeUnits = new List<BackupBCodeUnitEntry> { CreateValidCodeUnitEntry() }
        };

        var ex = Assert.Throws<ArgumentException>(() => entry.Validate());
        Assert.Contains("commit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Code Unit Entry Validation Tests

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBCodeUnitEntry_Validate_EmptySymbolName_ThrowsArgumentException()
    {
        var entry = new BackupBCodeUnitEntry
        {
            SymbolName = "",
            UnitType = "Method",
            Language = "C#",
            LineStart = 1,
            LineEnd = 10
        };

        var ex = Assert.Throws<ArgumentException>(() => entry.Validate());
        Assert.Contains("Symbol name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBCodeUnitEntry_Validate_LineEndBeforeStart_ThrowsArgumentException()
    {
        var entry = new BackupBCodeUnitEntry
        {
            SymbolName = "TestMethod",
            UnitType = "Method",
            Language = "C#",
            LineStart = 10,
            LineEnd = 5
        };

        var ex = Assert.Throws<ArgumentException>(() => entry.Validate());
        Assert.Contains("line start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBCodeUnitEntry_Validate_LineStartZero_ThrowsArgumentException()
    {
        var entry = new BackupBCodeUnitEntry
        {
            SymbolName = "TestMethod",
            UnitType = "Method",
            Language = "C#",
            LineStart = 0,
            LineEnd = 10
        };

        var ex = Assert.Throws<ArgumentException>(() => entry.Validate());
        Assert.Contains("Line start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BackupBCodeUnitEntry_Validate_ValidEntry_DoesNotThrow()
    {
        var entry = new BackupBCodeUnitEntry
        {
            SymbolName = "TestMethod",
            UnitType = "Method",
            Language = "C#",
            LineStart = 1,
            LineEnd = 10
        };

        var exception = Record.Exception(() => entry.Validate());
        Assert.Null(exception);
    }

    #endregion

    #region Hash Computation Tests

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ComputeSha256_EmptyString_ReturnsCorrectHash()
    {
        var input = "";
        var expectedHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        var actualHash = ComputeSha256(input);

        Assert.Equal(expectedHash, actualHash);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ComputeSha256_SimpleString_ReturnsCorrectHash()
    {
        var input = "hello";
        var expectedHash = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

        var actualHash = ComputeSha256(input);

        Assert.Equal(expectedHash, actualHash);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ComputeSha256_Consistency_SameInputSameOutput()
    {
        var input = "test content for hash consistency";

        var hash1 = ComputeSha256(input);
        var hash2 = ComputeSha256(input);

        Assert.Equal(hash1, hash2);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ComputeSha256_ReturnsLowercaseHex()
    {
        var input = "test";
        var hash = ComputeSha256(input);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    #endregion

    #region Helper Methods

    private static BackupBDirectProvisioner CreateProvisioner()
    {
        var systemRepo = new StubSystemRepository();
        var repoRepo = new StubRepositoryRepository();
        var scanRunRepo = new StubScanRunRepository();
        var scanExecutionStore = new StubScanExecutionStore();

        return new BackupBDirectProvisioner(
            systemRepo,
            repoRepo,
            scanRunRepo,
            scanExecutionStore);
    }

    private static BackupBRepositoryInput CreateValidRepositoryInput()
    {
        return new BackupBRepositoryInput
        {
            Name = "Test Repo",
            Description = "A test repository",
            CloneUrl = "https://example.com/repo.git",
            LocalGitPath = @"C:\repos\test",
            ExpectedCommit = new string('a', 40),
            ManifestEntries = new List<BackupBManifestEntry>
            {
                CreateValidManifestEntry("repo1")
            }
        };
    }

    private static BackupBManifestEntry CreateValidManifestEntry(string alias)
    {
        return new BackupBManifestEntry
        {
            RepositoryAlias = alias,
            ExactCommit = new string('a', 40),
            FilePath = "src/test.cs",
            CodeUnits = new List<BackupBCodeUnitEntry>
            {
                CreateValidCodeUnitEntry()
            }
        };
    }

    private static BackupBCodeUnitEntry CreateValidCodeUnitEntry()
    {
        return new BackupBCodeUnitEntry
        {
            SymbolName = "TestMethod",
            UnitType = "Method",
            Language = "C#",
            LineStart = 1,
            LineEnd = 10
        };
    }

    private static string ComputeSha256(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion

    #region Stub Implementations

    private sealed class StubSystemRepository : ISystemRepository
    {
        private readonly List<SystemEntity> _systems = new();

        public Task<IReadOnlyList<SystemEntity>> ListVisibleAsync(SystemOwnerFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SystemEntity>>(_systems);

        public Task<SystemEntity?> GetVisibleByIdAsync(Guid id, SystemOwnerFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<SystemEntity?>(_systems.FirstOrDefault(s => s.Id == id));

        public Task<SystemEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<SystemEntity?>(_systems.FirstOrDefault(s => s.Id == id));

        public Task<bool> NameExistsAsync(string name, Guid? excludeSystemId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_systems.Any(s => s.Name == name && s.Id != excludeSystemId));

        public Task<SystemEntity> AddAsync(SystemEntity system, CancellationToken cancellationToken = default)
        {
            _systems.Add(system);
            return Task.FromResult(system);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<SystemDeleteResult> DeleteIfNoDependenciesAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SystemDependencyCounts> GetDependencyCountsAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SystemOwnerAssignmentWriteResult> AssignOwnerAsync(Guid systemId, string userId, DateTimeOffset now, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> RemoveOwnerAsync(Guid systemId, string userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SystemOwnerAssignmentSummary>> ListOwnersAsync(Guid systemId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> CountOwnerAssignmentsByUserAsync(string userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubRepositoryRepository : IRepositoryRepository
    {
        private readonly List<Repository> _repositories = new();

        public Task<IReadOnlyList<Repository>> ListBySystemAsync(Guid systemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Repository>>(_repositories.Where(r => r.SystemId == systemId).ToList());

        public Task<IReadOnlyList<Repository>> ListStandaloneAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Repository?> GetByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_repositories.FirstOrDefault(r => r.Id == repositoryId));

        public Task<Repository?> GetBySystemAndIdAsync(Guid systemId, Guid repositoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_repositories.FirstOrDefault(r => r.Id == repositoryId && r.SystemId == systemId));

        public Task<Repository?> GetStandaloneByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> NameExistsAsync(Guid systemId, string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> StandaloneNameExistsAsync(string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Repository> AddAsync(Repository repository, CancellationToken cancellationToken = default)
        {
            _repositories.Add(repository);
            return Task.FromResult(repository);
        }

        public void Remove(Repository repository) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubScanRunRepository : IScanRunRepository
    {
        private readonly List<ScanRun> _scanRuns = new();

        public Task<ScanRun?> GetAsync(Guid repositoryId, Guid scanRunId, CancellationToken cancellationToken = default)
            => Task.FromResult(_scanRuns.FirstOrDefault(s => s.Id == scanRunId));

        public Task<ApprovedPreScanHandoff?> GetApprovedPreScanHandoffAsync(Guid repositoryId, Guid scanRunId, CancellationToken cancellationToken = default)
            => Task.FromResult<ApprovedPreScanHandoff?>(null);

        public Task AddAsync(ScanRun scanRun, CancellationToken cancellationToken = default)
        {
            _scanRuns.Add(scanRun);
            return Task.CompletedTask;
        }

        public void AddCandidateOutcomes(IEnumerable<ScanCandidateOutcome> outcomes) { }

        public void AddStructuralRecords(
            IEnumerable<CodeUnit> codeUnits,
            IEnumerable<CodeUnitEdge> edges,
            IEnumerable<UnresolvedStructuralRelationshipCandidate> unresolvedRelationships) { }

        public Task<PreScanResult> PersistCompletedProfileAsync(RepositoryProfileCommitDraft draft, CancellationToken cancellationToken = default)
        {
            // Return a minimal valid PreScanResult matching the draft's scan run
            var scanRun = draft.Profile.ScanRun;
            var preScanResult = PreScanResult.CreateApproved(
                scanRun.RepositoryId,
                scanRun.Id,
                scanRun.ProfiledRevision!,
                DateTimeOffset.UtcNow,
                new string('b', 64),
                "预扫描已批准——继续进入顺序演示运行器",
                ConfirmedCommunicationKinds.None,
                0,
                0,
                0,
                evidenceTruncated: false,
                Array.Empty<CommunicationKindClassification>());

            return Task.FromResult(preScanResult);
        }

        public Task RecoverFailedFinalCommitAsync(Guid repositoryId, Guid scanRunId, string sanitizedMessage, DateTimeOffset failedAt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RecoverAbortedFinalCommitAsync(Guid repositoryId, Guid scanRunId, string sanitizedMessage, DateTimeOffset abortedAt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubScanExecutionStore : IScanExecutionStore
    {
        private readonly List<ScanExecution> _executions = new();

        public Task<ScanExecution?> GetAsync(Guid scanExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_executions.FirstOrDefault(e => e.Id == scanExecutionId));

        public Task<ScanExecution?> FindActiveAsync(Guid repositoryId, Guid scanRunId, CancellationToken cancellationToken = default)
            => Task.FromResult<ScanExecution?>(null);

        public Task PersistAsync(ScanExecution execution, CancellationToken cancellationToken = default)
        {
            _executions.Add(execution);
            return Task.CompletedTask;
        }

        public Task<ScanExecutionStateView> ReadStateAsync(Guid scanExecutionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    #endregion
}
