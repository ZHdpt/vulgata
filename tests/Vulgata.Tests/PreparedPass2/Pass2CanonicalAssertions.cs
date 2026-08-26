using System.Text;
using System.Text.Json;

namespace Vulgata.Tests.PreparedPass2;

internal static class Pass2CanonicalAssertions
{
    internal static IReadOnlyList<Guid> CanonicalIds(IEnumerable<Guid> values)
    {
        Guid[] ids = values.ToArray();
        return ids.OrderBy(
                id => Convert.ToHexString(id.ToByteArray(bigEndian: true)),
                StringComparer.Ordinal)
            .ToArray();
    }

    internal static string CanonicalText(IEnumerable<string> values) =>
        string.Join("\n", values.Order(StringComparer.Ordinal));

    internal static byte[] CanonicalJson<T>(T value)
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, options));
    }

    internal static void AssertCanonicalIds(IReadOnlyList<Guid> ids)
    {
        Assert.Equal(CanonicalIds(ids), ids);
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
