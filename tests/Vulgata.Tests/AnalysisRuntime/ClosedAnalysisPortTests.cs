using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.AnalysisRuntime;

public sealed class ClosedAnalysisPortTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ClosedPorts_ExposeOnlyApprovedAsyncOperationsWithCancellationAndBoundedScope()
    {
        AssertAsyncPort(
            typeof(ICodeGraphAnalysisPort),
            "IndexAsync",
            "RebuildAsync",
            "GetStatusAsync",
            "SearchSymbolsAsync",
            "GetCallersAsync",
            "GetCalleesAsync",
            "GetRelatedFilesAsync");
        AssertAsyncPort(
            typeof(IJavaAnalysisSessionFactory),
            "OpenAsync");
        AssertAsyncPort(
            typeof(IJavaAnalysisSession),
            "GetDocumentSymbolsAsync",
            "GetDefinitionAsync",
            "GetReferencesAsync",
            "GetImplementationsAsync");
        AssertAsyncPort(typeof(IRevisionBoundSourceReader), "ReadAsync");
        AssertAsyncPort(typeof(IAnalysisRuntimeHealthProbe), "CheckConfiguredIntegrityAsync");

        AssertScopeBinding();
        foreach (Type portType in Ports)
        {
            foreach (MethodInfo method in portType.GetMethods())
            {
                Assert.Contains(
                    method.GetParameters(),
                    parameter => parameter.ParameterType == typeof(AnalysisScope));
                Assert.Contains(
                    method.GetParameters(),
                    parameter => parameter.ParameterType == typeof(CancellationToken));
                AssertFiniteOperationBound(method);
                AssertNoForbiddenPortTypes(method);
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CodeGraphPort_IsClosedToOnlyApprovedTypedCapabilities()
    {
        Type portType = typeof(ICodeGraphAnalysisPort);
        Assert.Equal(
            new[]
            {
                "GetCalleesAsync",
                "GetCallersAsync",
                "GetRelatedFilesAsync",
                "GetStatusAsync",
                "IndexAsync",
                "RebuildAsync",
                "SearchSymbolsAsync",
            },
            portType.GetMethods().Select(method => method.Name).Order(StringComparer.Ordinal).ToArray());

        AssertNoForbiddenPortMembers(portType);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JavaSession_IsClosedToOnlyBoundedNavigationCapabilities()
    {
        Type portType = typeof(IJavaAnalysisSession);
        Assert.Equal(
            new[]
            {
                "GetDefinitionAsync",
                "GetDocumentSymbolsAsync",
                "GetImplementationsAsync",
                "GetReferencesAsync",
            },
            portType.GetMethods().Select(method => method.Name).Order(StringComparer.Ordinal).ToArray());

        AssertNoForbiddenPortMembers(portType);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void SourceReader_RequiresRevisionBoundExactRangeAndFiniteByteCap()
    {
        MethodInfo method = typeof(IRevisionBoundSourceReader).GetMethod("ReadAsync")!;
        string[] names = method
            .GetParameters()
            .SelectMany(parameter => new[] { parameter.Name ?? string.Empty }
                .Concat(parameter.ParameterType.GetProperties().Select(property => property.Name)))
            .ToArray();

        Assert.Contains(names, name => name.Equals("RelativePath", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.Contains("Range", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.Contains("Expected", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name =>
            name.Contains("Byte", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Limit", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Cap", StringComparison.OrdinalIgnoreCase));
        AssertNoForbiddenPortMembers(typeof(IRevisionBoundSourceReader));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void HealthProbe_ExposesBoundedIntegrityResultWithoutStartupToolLeakage()
    {
        MethodInfo method = typeof(IAnalysisRuntimeHealthProbe)
            .GetMethod("CheckConfiguredIntegrityAsync")!;
        Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
        AssertNoForbiddenPortMembers(typeof(IAnalysisRuntimeHealthProbe));

        Type resultType = method.ReturnType.GetGenericArguments()[0];
        AssertImmutableModel(resultType);
        Assert.Contains(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "Status" or "FailureCode" or "Diagnostic");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ClosedResults_AreImmutableBoundedAndSanitized()
    {
        foreach (Type resultType in Ports
            .SelectMany(port => port.GetMethods())
            .Select(method => UnwrapTask(method.ReturnType))
            .Where(type => type != typeof(IJavaAnalysisSession)))
        {
            AssertImmutableModel(resultType);
            AssertNoForbiddenTypeNames(resultType);
            AssertNoRawDiagnosticOrUnboundedMembers(resultType);
        }

        Type[] closedFailureEnums = typeof(ICodeGraphAnalysisPort).Assembly
            .GetTypes()
            .Where(type => type.IsEnum &&
                type.Namespace == typeof(ICodeGraphAnalysisPort).Namespace)
            .Where(type =>
            {
                string[] names = Enum.GetNames(type);
                return names.Contains("NotReady") &&
                    names.Contains("Rejected") &&
                    names.Contains("TimedOut") &&
                    names.Contains("Cancelled") &&
                    names.Contains("MalformedResponse") &&
                    names.Contains("IntegrityMismatch") &&
                    names.Contains("WorkspaceConflict") &&
                    names.Contains("ProcessFailed") &&
                    names.Contains("CleanupFailed") &&
                    names.Contains("Unavailable");
            })
            .ToArray();
        Assert.NotEmpty(closedFailureEnums);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PublicAnalysisSurface_DoesNotExposeRawProtocolShellNetworkStoreOrFilesystemAccess()
    {
        foreach (Type portType in Ports)
        {
            AssertNoForbiddenPortMembers(portType);
        }

        foreach (Type type in typeof(ICodeGraphAnalysisPort).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(ICodeGraphAnalysisPort).Namespace &&
                (type.Name.Contains("Analysis", StringComparison.Ordinal) ||
                    type.Name.Contains("Evidence", StringComparison.Ordinal) ||
                    type.Name.Contains("Result", StringComparison.Ordinal))))
        {
            AssertNoForbiddenTypeNames(type);
        }
    }

    private static readonly Type[] Ports =
    [
        typeof(ICodeGraphAnalysisPort),
        typeof(IJavaAnalysisSessionFactory),
        typeof(IJavaAnalysisSession),
        typeof(IRevisionBoundSourceReader),
        typeof(IAnalysisRuntimeHealthProbe),
    ];

    private static void AssertScopeBinding()
    {
        Type scopeType = typeof(AnalysisScope);
        Assert.True(scopeType.IsClass && scopeType.IsSealed);
        AssertImmutableModel(scopeType);
        foreach (string propertyName in new[]
        {
            "RepositoryId",
            "ScanRunId",
            "ProfiledRevision",
            "WorkingCopyBindingToken",
            "RuntimeIdentityHash",
            "Deadline",
        })
        {
            Assert.NotNull(scopeType.GetProperty(propertyName));
        }

        Assert.DoesNotContain(
            scopeType.GetProperties(),
            property => property.Name.Contains("WorkspacePath", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("WorkingCopyRoot", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("RepositoryIds", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertAsyncPort(Type portType, params string[] expectedMethodNames)
    {
        Assert.True(portType.IsInterface, $"{portType.Name} must be a Core-owned interface.");
        MethodInfo[] methods = portType.GetMethods();
        Assert.Equal(
            expectedMethodNames.Order(StringComparer.Ordinal),
            methods.Select(method => method.Name).Order(StringComparer.Ordinal).ToArray());

        foreach (MethodInfo method in methods)
        {
            Assert.EndsWith("Async", method.Name, StringComparison.Ordinal);
            Assert.True(
                method.ReturnType.IsGenericType &&
                method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
                $"{portType.Name}.{method.Name} must return Task<T>.");
        }
    }

    private static void AssertFiniteOperationBound(MethodInfo method)
    {
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(AnalysisScope) ||
                parameter.ParameterType.GetProperties().Any(property =>
                    property.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("Byte", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("Deadline", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("Timeout", StringComparison.OrdinalIgnoreCase)) ||
                parameter.Name?.Contains("limit", StringComparison.OrdinalIgnoreCase) == true ||
                parameter.Name?.Contains("bytes", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static Type UnwrapTask(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)
            ? type.GetGenericArguments()[0]
            : type;

    private static void AssertImmutableModel(Type modelType)
    {
        if (modelType.IsInterface || modelType.IsEnum || modelType.IsPrimitive)
        {
            return;
        }

        Assert.True(modelType.IsClass && modelType.IsSealed, $"{modelType.Name} must be sealed.");
        foreach (PropertyInfo property in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            MethodInfo? setter = property.SetMethod;
            Assert.True(
                setter is null || setter.IsPrivate ||
                setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)),
                $"{modelType.Name}.{property.Name} must be immutable or init-only.");
            Assert.DoesNotContain("IList", property.PropertyType.FullName ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("List", property.PropertyType.FullName ?? string.Empty, StringComparison.Ordinal);
        }
    }

    private static void AssertNoForbiddenPortMembers(Type portType)
    {
        foreach (MethodInfo method in portType.GetMethods())
        {
            AssertNoForbiddenPortTypes(method);
            AssertNoRawDiagnosticOrUnboundedMembers(method.ReturnType);
        }
    }

    private static void AssertNoForbiddenPortTypes(MethodInfo method)
    {
        foreach (ParameterInfo parameter in method.GetParameters())
        {
            AssertNoForbiddenTypeNames(parameter.ParameterType);
        }

        AssertNoForbiddenTypeNames(method.ReturnType);
    }

    private static void AssertNoForbiddenTypeNames(Type type) =>
        AssertNoForbiddenTypeNames(type, new HashSet<Type>());

    private static void AssertNoForbiddenTypeNames(Type type, ISet<Type> visited)
    {
        if (!visited.Add(type))
        {
            return;
        }

        string fullName = type.FullName ?? type.Name;
        foreach (string forbidden in new[]
        {
            "System.Diagnostics.Process",
            "System.IO.Stream",
            "System.IO.FileInfo",
            "System.IO.DirectoryInfo",
            "System.Net.",
            "System.Uri",
            "StreamJsonRpc",
            "JsonRpc",
            "LanguageServerProtocol",
            "EntityFrameworkCore",
            "IQueryable",
            "DbSet",
            "HttpClient",
            "Socket",
            "Pipe",
        })
        {
            Assert.DoesNotContain(forbidden, fullName, StringComparison.OrdinalIgnoreCase);
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotContain(
                property.Name,
                new[] { "RawMessage", "RawJson", "Stdout", "Stderr", "Command", "Arguments", "Environment", "Process", "FileHandle", "GraphQuery" },
                StringComparer.OrdinalIgnoreCase);
            AssertNoForbiddenTypeNames(property.PropertyType, visited);
        }

        foreach (Type argument in type.IsArray
            ? [type.GetElementType()!]
            : type.IsGenericType
                ? type.GetGenericArguments()
                : [])
        {
            AssertNoForbiddenTypeNames(argument, visited);
        }
    }

    private static void AssertNoRawDiagnosticOrUnboundedMembers(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotContain("AbsolutePath", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WholeFile", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Unbounded", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Raw", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Stdout", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Stderr", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
