using System.Text;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.PreparedNavigation;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T058 RED coverage for the server-owned lease and authorization boundary of
/// the Pass 1 navigate_code facade.  These tests invoke the public navigation
/// port with typed analysis fakes; they do not reach raw LSP, CodeGraph,
/// filesystem, shell, or network APIs.
/// </summary>
public sealed class NavigateCodeLeaseSecurityCorrectionTests
{
    private static readonly Guid RepositoryId =
        Guid.Parse("58000000-0000-4000-8000-000000000001");
    private static readonly Guid ScanRunId =
        Guid.Parse("58000000-0000-4000-8000-000000000002");
    private static readonly Guid JobId =
        Guid.Parse("58000000-0000-4000-8000-000000000003");
    private static readonly Guid FileCodeUnitId =
        Guid.Parse("58000000-0000-4000-8000-000000000004");
    private const string ProfiledRevision =
        "5800000000000000000000000000000000000001";
    private const string SourceHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RuntimeIdentityHash =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string JavaPath = "src/main/java/example/Service.java";
    private const string EvidenceLocator = "src/main/java/example/Service.java#L1-L2";

    [Theory]
    [MemberData(nameof(AuthorizedOperations))]
    public async Task CurrentLease_EachAuthorizedOperation_ReturnsBoundedEvidenceAndOneReceipt(
        PreparedPass1NavigationOperation operation)
    {
        var harness = CreateHarness();

        PreparedPass1NavigationResponse response = await harness.Service.NavigateAsync(
            Request(operation),
            CancellationToken.None);

        Assert.NotEqual(PreparedPass1NavigationOutcome.Unavailable, response.Outcome);
        Assert.NotEmpty(response.Results);
        Assert.Single(harness.ReceiptSink.Receipts);
        Assert.InRange(response.Results.Count, 1, 20);
        Assert.InRange(response.EvidenceBytes, 1, 65_536);
        Assert.All(response.Results, result =>
        {
            Assert.Equal(JavaPath, result.CanonicalPath);
            Assert.Equal(Convert.FromHexString(SourceHash), result.SourceHash.ToArray());
            Assert.Equal(new PreparedPass1LineRange(1, 2), result.LineRange);
        });
    }

    [Fact]
    public async Task Implementations_CurrentLease_ReachesBoundedJdtLsAndReturnsAuthorizedEvidence()
    {
        var harness = CreateHarness();

        PreparedPass1NavigationResponse response = await harness.Service.NavigateAsync(
            Request(PreparedPass1NavigationOperation.Implementations),
            CancellationToken.None);

        Assert.Equal(PreparedPass1NavigationOutcome.SourceEvidence, response.Outcome);
        Assert.Single(response.Results);
        Assert.Equal(1, harness.Java.ImplementationsCalls);
        Assert.Equal(0, harness.Java.DefinitionCalls);
        Assert.Equal(JavaPath, response.Results[0].CanonicalPath);
        Assert.Equal(EvidenceLocator, response.Results[0].EvidenceLocator);
    }

    [Theory]
    [InlineData("expired lease")]
    [InlineData("replaced lease token")]
    [InlineData("paused circuit")]
    [InlineData("probing circuit")]
    [InlineData("completed job")]
    [InlineData("terminal pair")]
    [InlineData("ownership changed")]
    public async Task ServerOwnedMismatch_ReturnsEmptyUnavailableWithoutReceiptOrBudgetMutation(
        string mismatch)
    {
        RequireServerAuthorizationSeam();
        var harness = CreateHarness();
        IReadOnlyDictionary<string, object?> overrides = mismatch switch
        {
            "expired lease" => new Dictionary<string, object?>
            {
                ["leaseExpiresAt"] = DateTimeOffset.UtcNow.AddMinutes(-1),
            },
            "replaced lease token" => new Dictionary<string, object?>
            {
                ["leaseToken"] = Guid.NewGuid(),
            },
            "paused circuit" => new Dictionary<string, object?>
            {
                ["circuitState"] = "Paused",
                ["pairState"] = "Paused",
            },
            "probing circuit" => new Dictionary<string, object?>
            {
                ["circuitState"] = "Probing",
                ["pairState"] = "Probing",
            },
            "completed job" => new Dictionary<string, object?>
            {
                ["jobState"] = "Completed",
            },
            "terminal pair" => new Dictionary<string, object?>
            {
                ["pairPhase"] = "FailedPass1",
                ["phase"] = "FailedPass1",
                ["pairState"] = "Completed",
            },
            "ownership changed" => new Dictionary<string, object?>
            {
                ["authorizationBinding"] = "different-owner",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };

        PreparedPass1NavigationResponse response = await harness.Service.NavigateAsync(
            Request(PreparedPass1NavigationOperation.Definition, overrides: overrides),
            CancellationToken.None);

        Assert.Equal(PreparedPass1NavigationOutcome.Unavailable, response.Outcome);
        Assert.Empty(response.Results);
        Assert.Equal(Guid.Empty, response.ReceiptId);
        Assert.Empty(harness.ReceiptSink.Receipts);
        Assert.Equal(0, harness.ReceiptSink.CommitAttempts);
        Assert.Equal(0, harness.Java.TotalCalls);
        Assert.Equal(0, harness.CodeGraph.TotalCalls);
        Assert.Equal(0, harness.SourceReader.ReadCalls);
    }

    [Fact]
    public void NavigationContracts_RequireServerOwnedAuthorizationBinding()
    {
        RequireServerAuthorizationSeam();
    }

    [Fact]
    public async Task OwnershipChangesBetweenEvidenceAndReceiptCommit_DoesNotReleaseEvidenceOrWriteReceipt()
    {
        var harness = CreateHarness();
        harness.ReceiptSink.ReturnNull = true;

        PreparedPass1NavigationResponse response = await harness.Service.NavigateAsync(
            Request(PreparedPass1NavigationOperation.References),
            CancellationToken.None);

        Assert.Equal(PreparedPass1NavigationOutcome.Unavailable, response.Outcome);
        Assert.Empty(response.Results);
        Assert.Equal(Guid.Empty, response.ReceiptId);
        Assert.Equal(1, harness.ReceiptSink.CommitAttempts);
        Assert.Empty(harness.ReceiptSink.Receipts);
        Assert.Equal(0, harness.ReceiptSink.DurableBudgetMutations);
    }

    [Fact]
    public async Task ForbiddenRead_UsesEmptyNondisclosingOutcomeAndPerformsNoMutation()
    {
        var harness = CreateHarness();
        PreparedPass1NavigationRequestInput request = Request(
            PreparedPass1NavigationOperation.Definition,
            new PreparedPass1NavigationTarget(
                "C:/host/private/Secret.java",
                null,
                new PreparedPass1Position(1, 1),
                null,
                null));

        PreparedPass1NavigationResponse response = await harness.Service.NavigateAsync(
            request,
            CancellationToken.None);

        Assert.Equal(PreparedPass1NavigationOutcome.Unavailable, response.Outcome);
        Assert.Empty(response.Results);
        Assert.Equal(Guid.Empty, response.ReceiptId);
        Assert.Empty(harness.ReceiptSink.Receipts);
        Assert.Equal(0, harness.ReceiptSink.CommitAttempts);
        Assert.Equal(0, harness.Java.TotalCalls);
        Assert.Equal(0, harness.SourceReader.ReadCalls);
    }

    public static IEnumerable<object[]> AuthorizedOperations() =>
        Enum.GetValues<PreparedPass1NavigationOperation>()
            .Select(operation => new object[] { operation });

    private static Harness CreateHarness(bool scopeAvailable = true)
    {
        var scopeSource = new FakeScopeSource(scopeAvailable ? CreateScope() : null);
        var documentSource = new FakeDocumentSource();
        var receiptSink = new FakeReceiptSink();
        var java = new FakeJavaSessionFactory();
        var codeGraph = new FakeCodeGraph();
        var sourceReader = new FakeSourceReader();
        return new Harness(
            new PreparedPass1NavigationService(
                scopeSource,
                documentSource,
                receiptSink,
                java,
                codeGraph,
                sourceReader),
            receiptSink,
            java,
            codeGraph,
            sourceReader);
    }

    private static PreparedPass1NavigationScope CreateScope() =>
        CreateRecord<PreparedPass1NavigationScope>(new Dictionary<string, object?>
            (StringComparer.OrdinalIgnoreCase)
        {
            ["jobId"] = JobId,
            ["repositoryId"] = RepositoryId,
            ["scanRunId"] = ScanRunId,
            ["profiledRevision"] = ProfiledRevision,
            ["sourceFileCodeUnitId"] = FileCodeUnitId,
            ["analysisScope"] = new AnalysisScope(
                RepositoryId,
                ScanRunId,
                ProfiledRevision,
                "5800000000000000000000000000000000000002",
                RuntimeIdentityHash,
                new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds())),
            ["authorizedSources"] = new[]
            {
                new PreparedPass1AuthorizedSource(
                    FileCodeUnitId,
                    JavaPath,
                    SourceHash,
                    new PreparedPass1LineRange(1, 2),
                    new PreparedPass1ByteRange(0, 32),
                    1,
                    EvidenceLocator),
            },
            ["toolAttemptCount"] = 0,
            ["acceptedCallCount"] = 0,
            ["evidenceBytes"] = 0,
            ["observedTargetIdentities"] = Array.Empty<ReadOnlyMemory<byte>>(),
            ["pairExecutionId"] = Guid.Parse("58000000-0000-4000-8000-000000000006"),
            ["jobStateVersion"] = 1L,
            ["pairStateVersion"] = 1L,
            ["leaseToken"] = Guid.Parse("58000000-0000-4000-8000-000000000007"),
            ["leaseExpiresAt"] = DateTimeOffset.UtcNow.AddMinutes(5),
            ["pairState"] = "Running",
            ["circuitState"] = "Closed",
            ["pairPhase"] = "Running",
            ["phase"] = "Running",
            ["jobState"] = "Running",
            ["authorizationBinding"] = "current-owner",
        });

    private static PreparedPass1NavigationRequestInput Request(
        PreparedPass1NavigationOperation operation,
        PreparedPass1NavigationTarget? target = null,
        IReadOnlyDictionary<string, object?>? overrides = null)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["jobId"] = JobId,
            ["toolAttemptOrdinal"] = 1UL,
            ["operation"] = operation,
            ["sourceFileCodeUnitId"] = FileCodeUnitId,
            ["target"] = target ?? DefaultTarget(operation),
            ["requestedDepth"] = 1UL,
            ["parentObservationIdentity"] = null,
            ["pairExecutionId"] = Guid.Parse("58000000-0000-4000-8000-000000000006"),
            ["jobStateVersion"] = 1L,
            ["pairStateVersion"] = 1L,
            ["leaseToken"] = Guid.Parse("58000000-0000-4000-8000-000000000007"),
            ["leaseExpiresAt"] = DateTimeOffset.UtcNow.AddMinutes(5),
            ["pairState"] = "Running",
            ["circuitState"] = "Closed",
            ["pairPhase"] = "Running",
            ["phase"] = "Running",
            ["jobState"] = "Running",
            ["authorizationBinding"] = "current-owner",
        };
        if (overrides is not null)
        {
            foreach ((string key, object? value) in overrides)
            {
                values[key] = value;
            }
        }

        return CreateRecord<PreparedPass1NavigationRequestInput>(values);
    }

    private static void RequireServerAuthorizationSeam()
    {
        string[] required =
        [
            "PairExecutionId",
            "JobStateVersion",
            "PairStateVersion",
            "LeaseToken",
            "LeaseExpiresAt",
        ];
        AssertProperties(typeof(PreparedPass1NavigationScope), required, "scope");
        AssertProperties(typeof(PreparedPass1NavigationRequestInput), required, "request");
        AssertAnyProperty(typeof(PreparedPass1NavigationScope), ["CircuitState", "PairState", "PairCircuitState"], "scope circuit state");
        AssertAnyProperty(typeof(PreparedPass1NavigationScope), ["JobState", "State"], "scope job state");
        AssertAnyProperty(typeof(PreparedPass1NavigationRequestInput), ["CircuitState", "PairState", "PairCircuitState"], "request circuit state");
        AssertAnyProperty(typeof(PreparedPass1NavigationRequestInput), ["JobState", "State"], "request job state");

        Type binding = Assembly.Load("Vulgata.Agents")
            .GetType("Vulgata.Agents.WorkerExecution.PreparedNavigateCodeBinding")
            ?? throw new Xunit.Sdk.XunitException("Prepared navigate_code binding is missing.");
        AssertProperties(binding, required, "hidden binding");
        AssertAnyProperty(binding, ["CircuitState", "PairState", "PairCircuitState"], "hidden binding circuit state");
        AssertAnyProperty(binding, ["JobState", "State"], "hidden binding job state");
    }

    private static void AssertProperties(Type type, IReadOnlyList<string> required, string label)
    {
        string[] names = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)];
        foreach (string property in required)
        {
            Assert.Contains(property, names);
        }
    }

    private static void AssertAnyProperty(Type type, IReadOnlyList<string> candidates, string label)
    {
        string[] names = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)];
        Assert.Contains(names, name => candidates.Contains(name, StringComparer.Ordinal));
    }

    private static T CreateRecord<T>(IReadOnlyDictionary<string, object?> values)
    {
        ConstructorInfo constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(value => value.GetParameters().Length)
            .First();
        object?[] arguments = constructor.GetParameters()
            .Select(parameter => ConvertValue(
                values.TryGetValue(parameter.Name ?? string.Empty, out object? value)
                    ? value
                    : null,
                parameter.ParameterType,
                parameter.Name ?? string.Empty))
            .ToArray();
        return (T)constructor.Invoke(arguments);
    }

    private static object? ConvertValue(object? value, Type targetType, string parameterName)
    {
        Type? nullable = Nullable.GetUnderlyingType(targetType);
        Type effective = nullable ?? targetType;
        if (value is null)
        {
            return nullable is not null || !effective.IsValueType
                ? null
                : Activator.CreateInstance(effective);
        }

        if (targetType.IsInstanceOfType(value) || effective.IsInstanceOfType(value))
        {
            return value;
        }

        if (effective.IsEnum)
        {
            return value is string text
                ? Enum.Parse(effective, text, ignoreCase: true)
                : value;
        }
        if (effective == typeof(Guid))
        {
            return value is Guid guid ? guid : Guid.Parse((string)value);
        }
        if (effective == typeof(DateTimeOffset))
        {
            return value is DateTimeOffset date ? date : DateTimeOffset.Parse((string)value);
        }
        if (effective == typeof(ReadOnlyMemory<byte>))
        {
            return value is ReadOnlyMemory<byte> memory
                ? memory
                : new ReadOnlyMemory<byte>((byte[])value);
        }
        if (effective == typeof(string))
        {
            return value.ToString();
        }
        if (effective.IsArray && value is System.Collections.IEnumerable enumerable)
        {
            Type element = effective.GetElementType()!;
            var items = enumerable.Cast<object?>()
                .Select(item => ConvertValue(item, element, parameterName))
                .ToArray();
            Array array = Array.CreateInstance(element, items.Length);
            for (int index = 0; index < items.Length; index++)
            {
                array.SetValue(items[index], index);
            }
            return array;
        }
        return Convert.ChangeType(value, effective);
    }

    private static PreparedPass1NavigationTarget DefaultTarget(
        PreparedPass1NavigationOperation operation) =>
        operation switch
        {
            PreparedPass1NavigationOperation.Definition or
            PreparedPass1NavigationOperation.References or
            PreparedPass1NavigationOperation.Implementations =>
                new(JavaPath, null, new PreparedPass1Position(1, 1), null, null),
            PreparedPass1NavigationOperation.Callers or
            PreparedPass1NavigationOperation.Callees or
            PreparedPass1NavigationOperation.SymbolSearch or
            PreparedPass1NavigationOperation.RelatedFiles =>
                new(null, "example.Service.run", null, null, null),
            PreparedPass1NavigationOperation.ExactSourceEvidence =>
                new(
                    JavaPath,
                    null,
                    null,
                    new PreparedPass1LineRange(1, 2),
                    Convert.FromHexString(SourceHash)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    private sealed record Harness(
        PreparedPass1NavigationService Service,
        FakeReceiptSink ReceiptSink,
        FakeJavaSessionFactory Java,
        FakeCodeGraph CodeGraph,
        FakeSourceReader SourceReader);

    private sealed class FakeScopeSource(PreparedPass1NavigationScope? scope)
        : IPreparedPass1NavigationScopeSource
    {
        public Task<PreparedPass1NavigationScope?> GetAsync(
            Guid jobId,
            Guid sourceFileCodeUnitId,
            CancellationToken cancellationToken) => Task.FromResult(scope);
    }

    private sealed class FakeDocumentSource : IPreparedPass1CurrentDocumentEvidenceSource
    {
        public Task<PreparedPass1CurrentDocumentEvidence?> FindAsync(
            PreparedPass1NavigationScope scope,
            PreparedPass1AuthorizedSource source,
            PreparedPass1NavigationTarget target,
            CancellationToken cancellationToken) => Task.FromResult<PreparedPass1CurrentDocumentEvidence?>(null);
    }

    private sealed class FakeReceiptSink : IPreparedPass1NavigationReceiptSink
    {
        public List<PreparedPass1NavigationReceiptDraft> Receipts { get; } = [];
        public int CommitAttempts { get; private set; }
        public int DurableBudgetMutations { get; private set; }
        public bool ReturnNull { get; set; }

        public Task<Guid?> CommitAsync(
            PreparedPass1NavigationScope scope,
            PreparedPass1NavigationRequestInput request,
            PreparedPass1NavigationReceiptDraft receipt,
            CancellationToken cancellationToken)
        {
            CommitAttempts++;
            if (ReturnNull)
            {
                return Task.FromResult<Guid?>(null);
            }

            Receipts.Add(receipt);
            DurableBudgetMutations++;
            return Task.FromResult<Guid?>(Guid.Parse("58000000-0000-4000-8000-000000000005"));
        }
    }

    private sealed class FakeJavaSessionFactory : IJavaAnalysisSessionFactory
    {
        public int DefinitionCalls { get; private set; }
        public int ReferencesCalls { get; private set; }
        public int ImplementationsCalls { get; private set; }
        public int TotalCalls => DefinitionCalls + ReferencesCalls + ImplementationsCalls;

        public Task<IJavaAnalysisSession> OpenAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken) =>
            Task.FromResult<IJavaAnalysisSession>(new FakeJavaSession(this));

        private sealed class FakeJavaSession(FakeJavaSessionFactory owner) : IJavaAnalysisSession
        {
            public Task<JavaNavigationResult> GetDocumentSymbolsAsync(
                AnalysisScope scope,
                JavaNavigationRequest request,
                CancellationToken cancellationToken) =>
                Task.FromResult(Success());

            public Task<JavaNavigationResult> GetDefinitionAsync(
                AnalysisScope scope,
                JavaNavigationRequest request,
                CancellationToken cancellationToken)
            {
                owner.DefinitionCalls++;
                return Task.FromResult(Success());
            }

            public Task<JavaNavigationResult> GetReferencesAsync(
                AnalysisScope scope,
                JavaNavigationRequest request,
                CancellationToken cancellationToken)
            {
                owner.ReferencesCalls++;
                return Task.FromResult(Success());
            }

            public Task<JavaNavigationResult> GetImplementationsAsync(
                AnalysisScope scope,
                JavaNavigationRequest request,
                CancellationToken cancellationToken)
            {
                owner.ImplementationsCalls++;
                return Task.FromResult(Success());
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            private static JavaNavigationResult Success() =>
                new(
                    AnalysisOperationStatus.Succeeded,
                    [new AnalysisSymbolEvidence(
                        "example.Service.run",
                        AnalysisSymbolKind.Method,
                        JavaPath,
                        new AnalysisLineRange(1, 2))],
                    null);
        }
    }

    private sealed class FakeCodeGraph : ICodeGraphAnalysisPort
    {
        public int TotalCalls { get; private set; }

        public Task<AnalysisIndexResult> IndexAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AnalysisIndexResult> RebuildAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CodeGraphStatusResult> GetStatusAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SymbolSearchResult> SearchSymbolsAsync(
            AnalysisScope scope,
            AnalysisSymbolQuery query,
            CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult(new SymbolSearchResult(
                AnalysisOperationStatus.Succeeded,
                [new AnalysisSymbolEvidence(
                    "example.Service.run",
                    AnalysisSymbolKind.Method,
                    JavaPath,
                    new AnalysisLineRange(1, 2))],
                null));
        }

        public Task<RelationshipResult> GetCallersAsync(
            AnalysisScope scope,
            AnalysisSymbolRequest request,
            CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult(Relationships());
        }

        public Task<RelationshipResult> GetCalleesAsync(
            AnalysisScope scope,
            AnalysisSymbolRequest request,
            CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult(Relationships());
        }

        public Task<RelatedFilesResult> GetRelatedFilesAsync(
            AnalysisScope scope,
            AnalysisSymbolRequest request,
            CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult(new RelatedFilesResult(
                AnalysisOperationStatus.Succeeded,
                [JavaPath],
                null));
        }

        private static RelationshipResult Relationships() =>
            new(
                AnalysisOperationStatus.Succeeded,
                [new AnalysisRelationshipEvidence(
                    "example.Service.run",
                    "example.Service.run",
                    JavaPath,
                    new AnalysisLineRange(1, 2))],
                null);
    }

    private sealed class FakeSourceReader : IRevisionBoundSourceReader
    {
        public int ReadCalls { get; private set; }

        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            ReadCalls++;
            const string excerpt = "class Service {}\n";
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                RepositoryId,
                ProfiledRevision,
                JavaPath,
                new AnalysisLineRange(1, 2),
                SourceHash,
                excerpt,
                Encoding.UTF8.GetByteCount(excerpt),
                null));
        }
    }
}
