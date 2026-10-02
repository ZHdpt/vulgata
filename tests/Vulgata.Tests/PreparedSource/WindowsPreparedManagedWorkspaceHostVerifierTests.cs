using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Configuration;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Vulgata.Tests.TestInfrastructure;
using Xunit;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class WindowsPreparedManagedWorkspaceHostVerifierTests
{
    private static readonly TimeSpan ObservationGuard = TimeSpan.FromSeconds(3);

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ObserveAndVerifyAndHoldAsync_ProtectedOwnedFixtureReturnsObservedIdentityAndExclusiveHold()
    {
        Assert.True(OperatingSystem.IsWindows(), "This LocalTools case requires Windows ACL and named-mutex semantics.");

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier effectiveProcessSid = Assert.IsType<SecurityIdentifier>(identity.User);
        await using WindowsHostFixture fixture = await WindowsHostFixture.CreateAsync(
            effectiveProcessSid,
            protectedDacl: true);
        var verifier = new WindowsPreparedManagedWorkspaceHostVerifier(effectiveProcessSid);

        PreparedManagedWorkspaceHostObservation observation = await verifier.ObserveAsync(
                fixture.Grant,
                fixture.SourceStoragePolicy)
            .WaitAsync(ObservationGuard);

        Assert.Equal(PreparedManagedWorkspaceHostStatus.Observed, observation.Status);
        PreparedToolDirectoryObjectIdentity rootIdentity = Assert.IsType<PreparedToolDirectoryObjectIdentity>(
            observation.RootObjectIdentity);
        Assert.False(string.IsNullOrWhiteSpace(observation.DerivedWorkspaceOwnerIdentity));

        var binding = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            fixture.PreparedRootPath,
            rootIdentity,
            observation.DerivedWorkspaceOwnerIdentity!,
            StateEntryObjectIdentity: null);

        PreparedManagedWorkspaceHostHoldResult first = await verifier.VerifyAndHoldAsync(
                binding,
                fixture.SourceStoragePolicy)
            .WaitAsync(ObservationGuard);
        IPreparedManagedWorkspaceHostHold? heldScope = first.Hold;
        try
        {
            Assert.Equal(PreparedManagedWorkspaceHostStatus.Protected, first.Status);
            Assert.NotNull(heldScope);
            Assert.Equal(
                PreparedManagedWorkspaceHostStatus.Protected,
                await heldScope!.CheckCurrentAsync(binding).AsTask().WaitAsync(ObservationGuard));

            PreparedManagedWorkspaceHostHoldResult competing = await verifier.VerifyAndHoldAsync(
                    binding,
                    fixture.SourceStoragePolicy)
                .WaitAsync(ObservationGuard);
            try
            {
                AssertDeniedOrUnavailable(competing.Status);
                Assert.Null(competing.Hold);
            }
            finally
            {
                if (competing.Hold is not null)
                {
                    await competing.Hold.DisposeAsync().AsTask().WaitAsync(ObservationGuard);
                }
            }

            await fixture.AssertFixtureContentsUnchangedAsync();
            fixture.AssertFixtureDaclsUnchanged();
        }
        finally
        {
            if (heldScope is not null)
            {
                await heldScope.DisposeAsync().AsTask().WaitAsync(ObservationGuard);
            }
        }

        PreparedManagedWorkspaceHostHoldResult afterRelease = await verifier.VerifyAndHoldAsync(
                binding,
                fixture.SourceStoragePolicy)
            .WaitAsync(ObservationGuard);
        IPreparedManagedWorkspaceHostHold? releasedScope = afterRelease.Hold;
        try
        {
            Assert.Equal(PreparedManagedWorkspaceHostStatus.Protected, afterRelease.Status);
            Assert.NotNull(releasedScope);
            await fixture.AssertFixtureContentsUnchangedAsync();
            fixture.AssertFixtureDaclsUnchanged();
        }
        finally
        {
            if (releasedScope is not null)
            {
                await releasedScope.DisposeAsync().AsTask().WaitAsync(ObservationGuard);
            }
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ObserveAsync_WrongApprovedPrincipalDoesNotObserveAnOwnedRoot()
    {
        Assert.True(OperatingSystem.IsWindows(), "This LocalTools case requires Windows token semantics.");

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier effectiveProcessSid = Assert.IsType<SecurityIdentifier>(identity.User);
        await using WindowsHostFixture fixture = await WindowsHostFixture.CreateAsync(
            effectiveProcessSid,
            protectedDacl: true);
        SecurityIdentifier wrongPrincipal = new(WellKnownSidType.AuthenticatedUserSid, null);
        Assert.NotEqual(effectiveProcessSid, wrongPrincipal);
        var verifier = new WindowsPreparedManagedWorkspaceHostVerifier(wrongPrincipal);

        PreparedManagedWorkspaceHostObservation observation = await verifier.ObserveAsync(
                fixture.Grant,
                fixture.SourceStoragePolicy)
            .WaitAsync(ObservationGuard);

        AssertDeniedOrUnavailable(observation.Status);
        await fixture.AssertFixtureContentsUnchangedAsync();
        fixture.AssertFixtureDaclsUnchanged();
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ObserveAsync_WritableUnprotectedRootAndMetadataAreUnavailable()
    {
        Assert.True(OperatingSystem.IsWindows(), "This LocalTools case requires Windows ACL semantics.");

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier effectiveProcessSid = Assert.IsType<SecurityIdentifier>(identity.User);
        await using WindowsHostFixture fixture = await WindowsHostFixture.CreateAsync(
            effectiveProcessSid,
            protectedDacl: false);
        var verifier = new WindowsPreparedManagedWorkspaceHostVerifier(effectiveProcessSid);

        PreparedManagedWorkspaceHostObservation observation = await verifier.ObserveAsync(
                fixture.Grant,
                fixture.SourceStoragePolicy)
            .WaitAsync(ObservationGuard);

        AssertDeniedOrUnavailable(observation.Status);
        await fixture.AssertFixtureContentsUnchangedAsync();
        fixture.AssertFixtureDaclsUnchanged();
    }

    private static void AssertDeniedOrUnavailable(PreparedManagedWorkspaceHostStatus status) =>
        Assert.True(
            status is PreparedManagedWorkspaceHostStatus.Denied or PreparedManagedWorkspaceHostStatus.Unavailable,
            $"Expected a closed host result, got {status}.");

    private sealed class WindowsHostFixture : IAsyncDisposable
    {
        private readonly string _repositoryRoot;
        private readonly string _fixtureRoot;
        private readonly DirectorySecurity _originalPreparedRootDacl;
        private readonly DirectorySecurity _originalSourceStorageDacl;
        private readonly DirectorySecurity _originalMetadataDacl;
        private readonly byte[] _preparedMarkerBytes;
        private readonly byte[] _siblingMarkerBytes;
        private readonly string _preparedMarkerPath;
        private readonly string _siblingMarkerPath;
        private readonly string _preparedRootDaclSddl;
        private readonly string _sourceStorageDaclSddl;
        private readonly string _metadataDaclSddl;
        private bool _disposed;

        private WindowsHostFixture(
            string repositoryRoot,
            string fixtureRoot,
            string preparedRootPath,
            string sourceStorageRootPath,
            string metadataRootPath,
            string preparedMarkerPath,
            string siblingMarkerPath,
            DirectorySecurity originalPreparedRootDacl,
            DirectorySecurity originalSourceStorageDacl,
            DirectorySecurity originalMetadataDacl,
            byte[] preparedMarkerBytes,
            byte[] siblingMarkerBytes,
            PreparedSourceApproval approval,
            RetainedSourceCapturePolicy sourceStoragePolicy)
        {
            _repositoryRoot = repositoryRoot;
            _fixtureRoot = fixtureRoot;
            PreparedRootPath = preparedRootPath;
            SourceStorageRootPath = sourceStorageRootPath;
            MetadataRootPath = metadataRootPath;
            _preparedMarkerPath = preparedMarkerPath;
            _siblingMarkerPath = siblingMarkerPath;
            _originalPreparedRootDacl = originalPreparedRootDacl;
            _originalSourceStorageDacl = originalSourceStorageDacl;
            _originalMetadataDacl = originalMetadataDacl;
            _preparedMarkerBytes = preparedMarkerBytes;
            _siblingMarkerBytes = siblingMarkerBytes;
            _preparedRootDaclSddl = GetDacl(preparedRootPath)
                .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
            _sourceStorageDaclSddl = GetDacl(sourceStorageRootPath)
                .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
            _metadataDaclSddl = GetDacl(metadataRootPath)
                .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
            Approval = approval;
            SourceStoragePolicy = sourceStoragePolicy;
            Grant = new PreparedSourceGrantRequest(
                approval,
                preparedRootPath,
                new PreparedSourceGrantLimits(
                    MaximumOperations: 4,
                    MaximumMetadataEntries: 16,
                    MaximumExaminedBytes: 4_096,
                    MaximumReturnedBytes: 4_096,
                    Deadline: TimeSpan.FromSeconds(5)));
        }

        internal string PreparedRootPath { get; }

        internal string SourceStorageRootPath { get; }

        internal string MetadataRootPath { get; }

        internal PreparedSourceApproval Approval { get; }

        internal PreparedSourceGrantRequest Grant { get; }

        internal RetainedSourceCapturePolicy SourceStoragePolicy { get; }

        internal async Task AssertFixtureContentsUnchangedAsync()
        {
            Assert.Equal(_preparedMarkerBytes, await File.ReadAllBytesAsync(_preparedMarkerPath));
            Assert.Equal(_siblingMarkerBytes, await File.ReadAllBytesAsync(_siblingMarkerPath));
        }

        internal void AssertFixtureDaclsUnchanged()
        {
            Assert.Equal(
                _preparedRootDaclSddl,
                GetDacl(PreparedRootPath).GetSecurityDescriptorSddlForm(AccessControlSections.Access));
            Assert.Equal(
                _sourceStorageDaclSddl,
                GetDacl(SourceStorageRootPath).GetSecurityDescriptorSddlForm(AccessControlSections.Access));
            Assert.Equal(
                _metadataDaclSddl,
                GetDacl(MetadataRootPath).GetSecurityDescriptorSddlForm(AccessControlSections.Access));
        }

        internal static async Task<WindowsHostFixture> CreateAsync(
            SecurityIdentifier effectiveProcessSid,
            bool protectedDacl)
        {
            string repositoryRoot = FindRepositoryRoot();
            string testResultsRoot = Path.GetFullPath(Path.Combine(repositoryRoot, "TestResults"));
            AssertNoReparsePoint(repositoryRoot);
            if (Directory.Exists(testResultsRoot))
            {
                AssertNoReparsePoint(testResultsRoot);
            }
            else
            {
                Directory.CreateDirectory(testResultsRoot);
            }

            AssertOwnedPath(repositoryRoot, testResultsRoot);
            AssertNoReparsePoint(testResultsRoot);

            string fixtureRoot = Path.Combine(testResultsRoot, $"windows-host-verifier-{Guid.NewGuid():N}");
            string preparedRoot = Path.Combine(fixtureRoot, "managed-workspace");
            string sourceStorageRoot = Path.Combine(fixtureRoot, "source-storage");
            string siblingRoot = Path.Combine(fixtureRoot, "unrelated-sibling");
            string preparedMarkerPath = Path.Combine(preparedRoot, "fixture-source.marker");
            string siblingMarkerPath = Path.Combine(siblingRoot, "outside-root.marker");
            string metadataRoot = Path.Combine(sourceStorageRoot, ".prepared-codegraph-ownership");
            DirectorySecurity? originalPreparedRootDacl = null;
            DirectorySecurity? originalSourceStorageDacl = null;
            DirectorySecurity? originalMetadataDacl = null;
            try
            {
                Directory.CreateDirectory(preparedRoot);
                Directory.CreateDirectory(sourceStorageRoot);
                Directory.CreateDirectory(siblingRoot);
                originalPreparedRootDacl = GetDacl(preparedRoot);
                originalSourceStorageDacl = GetDacl(sourceStorageRoot);
                var approval = new PreparedSourceApproval(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "windows-host-verifier-test",
                    PreparedSourcePurpose.WorkerSourceRead,
                    $"approval-{Guid.NewGuid():N}",
                    $"root-{Guid.NewGuid():N}",
                    "fixture operator");

                if (protectedDacl)
                {
                    ApplyProtectedApplicationDacl(preparedRoot, effectiveProcessSid);
                    ApplyProtectedApplicationDacl(sourceStorageRoot, effectiveProcessSid);
                }
                else
                {
                    ApplyDeliberatelyWritableDacl(preparedRoot);
                    ApplyDeliberatelyWritableDacl(sourceStorageRoot);
                }

                byte[] preparedMarkerBytes = "owned prepared-root marker\n"u8.ToArray();
                byte[] siblingMarkerBytes = "unrelated sibling marker\n"u8.ToArray();
                await File.WriteAllBytesAsync(preparedMarkerPath, preparedMarkerBytes);
                await File.WriteAllBytesAsync(siblingMarkerPath, siblingMarkerBytes);

                IConfiguration configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SourceStorage:RootPath"] = sourceStorageRoot,
                        ["SourceStorage:MaximumEntries"] = "16",
                        ["SourceStorage:MaximumBlobBytes"] = "4096",
                        ["SourceStorage:MaximumAggregateBlobBytes"] = "8192",
                        ["SourceStorage:CaptureDeadlineSeconds"] = "5",
                        ["SourceStorage:OperationStaleAfterMinutes"] = "5",
                        ["SourceStorage:VerificationBufferBytes"] = "4096",
                        ["SourceStorage:Read:DeadlineSeconds"] = "5",
                    })
                    .Build();
                RetainedSourceCapturePolicy policy = RetainedSourceCapturePolicy.Create(
                    configuration,
                    fixtureRoot,
                    preparedRoot);
                PreparedCodeGraphStoreProvisionStatus provision =
                    await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(policy);
                Assert.Equal(PreparedCodeGraphStoreProvisionStatus.Provisioned, provision);
                originalMetadataDacl = GetDacl(metadataRoot);
                if (protectedDacl)
                {
                    ApplyProtectedApplicationDacl(metadataRoot, effectiveProcessSid);
                }
                else
                {
                    ApplyDeliberatelyWritableDacl(metadataRoot);
                }

                return new WindowsHostFixture(
                    repositoryRoot,
                    fixtureRoot,
                    preparedRoot,
                    sourceStorageRoot,
                    metadataRoot,
                    preparedMarkerPath,
                    siblingMarkerPath,
                    originalPreparedRootDacl,
                    originalSourceStorageDacl,
                    originalMetadataDacl,
                    preparedMarkerBytes,
                    siblingMarkerBytes,
                    approval,
                    policy);
            }
            catch
            {
                try
                {
                    if (originalMetadataDacl is not null && Directory.Exists(metadataRoot))
                    {
                        RestoreDacl(metadataRoot, originalMetadataDacl);
                    }

                    if (originalPreparedRootDacl is not null && Directory.Exists(preparedRoot))
                    {
                        RestoreDacl(preparedRoot, originalPreparedRootDacl);
                    }

                    if (originalSourceStorageDacl is not null && Directory.Exists(sourceStorageRoot))
                    {
                        RestoreDacl(sourceStorageRoot, originalSourceStorageDacl);
                    }
                }
                finally
                {
                    if (Directory.Exists(fixtureRoot))
                    {
                        EnsureOwnedPath(testResultsRoot, fixtureRoot);
                        EnsureNoReparsePoints(fixtureRoot);
                        Directory.Delete(fixtureRoot, recursive: true);
                    }

                }

                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            string testResultsRoot = Path.Combine(_repositoryRoot, "TestResults");
            EnsureOwnedPath(testResultsRoot, _fixtureRoot);
            AssertNoReparsePoint(testResultsRoot);
            try
            {
                RestoreDacl(MetadataRootPath, _originalMetadataDacl);
                RestoreDacl(PreparedRootPath, _originalPreparedRootDacl);
                RestoreDacl(SourceStorageRootPath, _originalSourceStorageDacl);
            }
            finally
            {
                EnsureNoReparsePoints(_fixtureRoot);
                Directory.Delete(_fixtureRoot, recursive: true);
            }

            return ValueTask.CompletedTask;
        }

        private static string FindRepositoryRoot()
        {
            for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
            {
                if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                    File.Exists(Path.Combine(current.FullName, "Directory.Build.props")) &&
                    Directory.Exists(Path.Combine(current.FullName, "src", "dotnet", "Vulgata.Infrastructure")))
                {
                    return Path.GetFullPath(current.FullName);
                }
            }

            throw new DirectoryNotFoundException("Could not locate the assigned Vulgata checkout from the test assembly.");
        }

        private static DirectorySecurity GetDacl(string path) =>
            new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access);

        private static void RestoreDacl(string path, DirectorySecurity original) =>
            new DirectoryInfo(path).SetAccessControl(original);

        private static void ApplyProtectedApplicationDacl(string path, SecurityIdentifier processSid)
        {
            DirectorySecurity security = GetDacl(path);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            AuthorizationRuleCollection rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: true,
                targetType: typeof(SecurityIdentifier));
            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.IdentityReference is SecurityIdentifier sid && !sid.Equals(processSid))
                {
                    security.PurgeAccessRules(sid);
                }
            }

            security.PurgeAccessRules(processSid);
            security.AddAccessRule(new FileSystemAccessRule(
                processSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            new DirectoryInfo(path).SetAccessControl(security);
        }

        private static void ApplyDeliberatelyWritableDacl(string path)
        {
            DirectorySecurity security = GetDacl(path);
            security.SetAccessRuleProtection(isProtected: false, preserveInheritance: true);
            security.SetAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            new DirectoryInfo(path).SetAccessControl(security);
        }

        private static void AssertOwnedPath(string repositoryRoot, string testResultsRoot)
        {
            string expected = Path.GetFullPath(Path.Combine(repositoryRoot, "TestResults"));
            Assert.True(
                string.Equals(testResultsRoot, expected, StringComparison.OrdinalIgnoreCase),
                "The Windows fixture parent must be the assigned checkout's TestResults directory.");
        }

        private static void EnsureOwnedPath(string expectedParent, string candidate)
        {
            string parent = Path.GetFullPath(expectedParent)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullCandidate = Path.GetFullPath(candidate);
            Assert.True(
                fullCandidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                "Refusing fixture cleanup outside its verified parent.");
            Assert.True(
                Path.GetFileName(fullCandidate).StartsWith("windows-host-verifier-", StringComparison.Ordinal),
                "Refusing cleanup of a path without this test's unique fixture marker.");
        }

        private static void AssertNoReparsePoint(string path)
        {
            Assert.False(
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0,
                $"Refusing to use a reparse-point test resource path: {path}");
        }

        private static void EnsureNoReparsePoints(string directory)
        {
            AssertNoReparsePoint(directory);
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                AssertNoReparsePoint(entry);
                if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                {
                    EnsureNoReparsePoints(entry);
                }
            }
        }
    }
}
