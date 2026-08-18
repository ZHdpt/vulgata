using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Tests.DurableScanning;

internal static class DeterministicTestIds
{
    public static Guid Create(string scope, params object[] components)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(components);

        StringBuilder identity = new(scope.Trim());
        foreach (object component in components)
        {
            ArgumentNullException.ThrowIfNull(component);
            string value = component switch
            {
                Guid guid => guid.ToString("D"),
                DateTimeOffset timestamp => timestamp.ToUniversalTime().ToString("O"),
                IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => component.ToString() ?? throw new ArgumentException("An identity component has no value.", nameof(components)),
            };

            identity.Append('\n').Append(value.Trim());
        }

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString()), hash);

        // Use RFC 4122 byte order so the same logical input is stable on every host.
        Span<byte> guidBytes = stackalloc byte[16];
        hash[..16].CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes, bigEndian: true);
    }
}
