using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.CrossRepository;

internal static class ExactEndpointNormalizer
{
    internal static string Normalize(string rawIdentifier, string requiredPrefix)
    {
        ArgumentNullException.ThrowIfNull(rawIdentifier);
        ArgumentNullException.ThrowIfNull(requiredPrefix);
        string normalized = rawIdentifier.Normalize(NormalizationForm.FormC)
            .Trim(' ', '\t', '\r', '\n');
        if (requiredPrefix.Length > 0)
        {
            if (!normalized.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The endpoint identifier does not satisfy the configured normalization rule.");
            }

            normalized = normalized[requiredPrefix.Length..];
            if (normalized.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The endpoint identifier does not satisfy the configured normalization rule.");
            }
        }

        if (normalized.Length == 0 ||
            Encoding.UTF8.GetByteCount(normalized) > ExactResolutionLimits.MaxEndpointUtf8Bytes ||
            normalized.Any(char.IsControl) ||
            normalized.Contains('?', StringComparison.Ordinal) ||
            normalized.Contains('#', StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The endpoint identifier does not satisfy the configured normalization rule.");
        }

        return normalized;
    }
}
