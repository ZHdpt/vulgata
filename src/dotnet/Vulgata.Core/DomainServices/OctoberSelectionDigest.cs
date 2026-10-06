using System.Security.Cryptography;
using System.Text.Json;

namespace Vulgata.Core.DomainServices;

public static class OctoberSelectionDigest
{
    public static string Compute(OctoberSetupRequest request, OctoberSelectedAdvisoryContext context)
    {
        ValidateStructure(request, context);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(2);
            writer.WriteStringValue(request.TargetId);
            WriteGuid(writer, request.BatchId);

            writer.WriteStartArray();
            foreach (OctoberRepositorySelection repository in request.Repositories
                         .OrderBy(value => value.RepositoryId.ToString("D"), StringComparer.Ordinal))
            {
                writer.WriteStartArray();
                WriteGuid(writer, repository.RepositoryId);
                WriteOptionalGuid(writer, repository.SystemId);
                writer.WriteStringValue(repository.RootBindingIdentity);
                writer.WriteStringValue(repository.OperatorLabel);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteStartArray();
            foreach (OctoberContextSelection reference in request.ContextSelections
                         .OrderBy(value => value.Scope)
                         .ThenBy(value => value.RecordId.ToString("D"), StringComparer.Ordinal))
            {
                writer.WriteStartArray();
                writer.WriteStringValue(ScopeName(reference.Scope));
                WriteGuid(writer, reference.RecordId);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteStringValue(context.ManagementAuthorityId);
            writer.WriteStartArray();
            foreach (OctoberAdvisoryContextEntry entry in context.Entries
                         .OrderBy(value => value.RepositoryId.ToString("D"), StringComparer.Ordinal)
                         .ThenBy(value => value.Scope))
            {
                writer.WriteStartArray();
                WriteGuid(writer, entry.RepositoryId);
                writer.WriteStringValue(ScopeName(entry.Scope));
                writer.WriteStringValue(ApplicabilityName(entry.Applicability));
                WriteOptionalGuid(writer, entry.OriginRecordId);
                if (entry.Value is null) writer.WriteNullValue();
                else writer.WriteStringValue(entry.Value);
                writer.WriteStringValue("advisory-not-source-proof");
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteStartArray();
            foreach (OctoberContextDifference difference in context.Differences
                         .OrderBy(value => value.RepositoryId.ToString("D"), StringComparer.Ordinal)
                         .ThenBy(value => value.LeftScope)
                         .ThenBy(value => value.RightScope))
            {
                writer.WriteStartArray();
                WriteGuid(writer, difference.RepositoryId);
                writer.WriteStringValue(ScopeName(difference.LeftScope));
                writer.WriteStringValue(ScopeName(difference.RightScope));
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
            writer.WriteEndArray();
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void ValidateStructure(OctoberSetupRequest request, OctoberSelectedAdvisoryContext context)
    {
        if (request is null || context is null || string.IsNullOrWhiteSpace(request.TargetId) ||
            request.BatchId == Guid.Empty || request.Repositories is null or { Count: 0 } ||
            request.ContextSelections is null || string.IsNullOrWhiteSpace(context.ManagementAuthorityId) ||
            context.Repositories.IsDefault || context.Entries.IsDefault || context.Differences.IsDefault)
            throw new ArgumentException("Invalid October selection structure.");

        CheckString(request.TargetId);
        CheckString(context.ManagementAuthorityId);
        HashSet<Guid> repositories = [];
        foreach (OctoberRepositorySelection repository in request.Repositories)
        {
            if (repository is null || repository.RepositoryId == Guid.Empty || repository.SystemId == Guid.Empty ||
                string.IsNullOrWhiteSpace(repository.RootBindingIdentity) ||
                string.IsNullOrWhiteSpace(repository.OperatorLabel) || !repositories.Add(repository.RepositoryId))
                throw new ArgumentException("Invalid October Repository selection.");
            CheckString(repository.RootBindingIdentity);
            CheckString(repository.OperatorLabel);
        }
        HashSet<(OctoberContextScope, Guid)> references = [];
        foreach (OctoberContextSelection reference in request.ContextSelections)
        {
            if (reference is null || !ValidScope(reference.Scope) || reference.RecordId == Guid.Empty ||
                !references.Add((reference.Scope, reference.RecordId)))
                throw new ArgumentException("Invalid October context reference.");
        }
        HashSet<Guid> trustedRepositories = [];
        foreach (OctoberRepositorySelection repository in context.Repositories)
        {
            if (repository is null || repository.RepositoryId == Guid.Empty || repository.SystemId == Guid.Empty ||
                string.IsNullOrWhiteSpace(repository.RootBindingIdentity) ||
                string.IsNullOrWhiteSpace(repository.OperatorLabel) || !trustedRepositories.Add(repository.RepositoryId))
                throw new ArgumentException("Invalid trusted Repository selection.");
            CheckString(repository.RootBindingIdentity);
            CheckString(repository.OperatorLabel);
        }
        HashSet<(Guid, OctoberContextScope)> entries = [];
        foreach (OctoberAdvisoryContextEntry entry in context.Entries)
        {
            if (entry is null || entry.RepositoryId == Guid.Empty || !ValidScope(entry.Scope) ||
                !Enum.IsDefined(entry.Applicability) ||
                entry.EvidenceKind != OctoberContextEvidenceKind.AdvisoryNotSourceProof ||
                !entries.Add((entry.RepositoryId, entry.Scope)))
                throw new ArgumentException("Invalid trusted context entry.");
            if (entry.Value is not null) CheckString(entry.Value);
        }
        HashSet<(Guid, OctoberContextScope, OctoberContextScope)> differences = [];
        foreach (OctoberContextDifference difference in context.Differences)
        {
            if (difference is null || difference.RepositoryId == Guid.Empty ||
                !ValidScope(difference.LeftScope) || !ValidScope(difference.RightScope) ||
                difference.LeftScope >= difference.RightScope ||
                !differences.Add((difference.RepositoryId, difference.LeftScope, difference.RightScope)))
                throw new ArgumentException("Invalid trusted context difference.");
        }
    }

    private static void CheckString(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                    throw new ArgumentException("Invalid UTF-16 selection value.");
            }
            else if (char.IsLowSurrogate(value[index]))
                throw new ArgumentException("Invalid UTF-16 selection value.");
        }
    }

    private static bool ValidScope(OctoberContextScope scope) =>
        scope is OctoberContextScope.Global or OctoberContextScope.System or OctoberContextScope.Repository;

    private static string ScopeName(OctoberContextScope scope) => scope switch
    {
        OctoberContextScope.Global => "global",
        OctoberContextScope.System => "system",
        OctoberContextScope.Repository => "repository",
        _ => throw new ArgumentException("Invalid context scope.")
    };

    private static string ApplicabilityName(OctoberContextApplicability applicability) => applicability switch
    {
        OctoberContextApplicability.Selected => "selected",
        OctoberContextApplicability.NotSelected => "not-selected",
        OctoberContextApplicability.NotApplicable => "not-applicable",
        _ => throw new ArgumentException("Invalid context applicability.")
    };

    private static void WriteGuid(Utf8JsonWriter writer, Guid value) => writer.WriteStringValue(value.ToString("D"));

    private static void WriteOptionalGuid(Utf8JsonWriter writer, Guid? value)
    {
        if (value is Guid id) WriteGuid(writer, id);
        else writer.WriteNullValue();
    }
}
