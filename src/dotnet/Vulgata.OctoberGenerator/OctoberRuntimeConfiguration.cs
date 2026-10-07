using System.Collections.Immutable;
using Vulgata.Infrastructure.Analysis.Java;

namespace Vulgata.OctoberGenerator;

/// <summary>Explicit trusted host inputs. Paths and profile values never come from a model.</summary>
internal sealed record OctoberRuntimeConfiguration(
    ImmutableArray<PreparedSourceRootBinding> PreparedRoots,
    string CodeGraphCommandPath,
    PreparedJavaNavigationProfile JavaNavigation,
    PreparedJdtHostPolicy JavaHostPolicy,
    string LogDirectory)
{
    /// <summary>Lexical validation only: no source, executable, state or log directory is opened.</summary>
    internal bool IsValid()
    {
        if (PreparedRoots.IsDefaultOrEmpty || PreparedRoots.Length > 1024 ||
            PreparedRoots.Any(root => root is null || !Exact(root.TargetId) || root.BatchId == Guid.Empty ||
                root.RepositoryId == Guid.Empty || !Exact(root.RootBindingIdentity) || !Absolute(root.PhysicalRootPath)) ||
            PreparedRoots.Select(root => (root.TargetId, root.BatchId, root.RepositoryId, root.RootBindingIdentity))
                .Distinct().Count() != PreparedRoots.Length ||
            !Absolute(CodeGraphCommandPath) || !Absolute(LogDirectory) || JavaNavigation is null || JavaHostPolicy is null)
            return false;
        if (!Absolute(JavaNavigation.DisposableStateRoot) ||
            JavaNavigation.InitializationTimeout <= TimeSpan.Zero || JavaNavigation.InitializationTimeout > TimeSpan.FromSeconds(20) ||
            JavaNavigation.RequestTimeout <= TimeSpan.Zero || JavaNavigation.RequestTimeout > TimeSpan.FromSeconds(6) ||
            JavaNavigation.ShutdownTimeout <= TimeSpan.Zero || JavaNavigation.ShutdownTimeout > TimeSpan.FromSeconds(15) ||
            JavaNavigation.MaximumResponseBytes is <= 0 or > 2 * 1024 * 1024 ||
            !Paths(JavaHostPolicy.ApprovedExecutableDirectories) || !Paths(JavaHostPolicy.DeclaredRuntimeReadRoots) ||
            !Paths(JavaHostPolicy.FixedChildPathEntries) || !Absolute(JavaHostPolicy.FixedCmdAbsolutePath) ||
            !Absolute(JavaHostPolicy.JavaBinDirectory))
            return false;
        return PreparedRoots.All(root => !Within(root.PhysicalRootPath, JavaNavigation.DisposableStateRoot) &&
            !Within(root.PhysicalRootPath, LogDirectory));
    }

    private static bool Exact(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 4096 && value.Trim() == value && !value.Any(char.IsControl);

    private static bool Absolute(string? path) => Exact(path) && Path.IsPathFullyQualified(path!);

    private static bool Paths(IReadOnlyList<string>? paths) => paths is { Count: > 0 and <= 128 } &&
        paths.All(Absolute) && paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == paths.Count;

    private static bool Within(string root, string candidate)
    {
        string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string child = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        string prefix = Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar;
        return string.Equals(parent, child, StringComparison.OrdinalIgnoreCase) ||
            child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
