using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Core.DomainServices;

public sealed record CanonicalField(ushort Tag, CanonicalValue Value);

public abstract record CanonicalValue
{
    private CanonicalValue()
    {
    }

    internal sealed record NullCanonicalValue : CanonicalValue;
    internal sealed record BooleanCanonicalValue(bool Value) : CanonicalValue;
    internal sealed record Int64CanonicalValue(long Value) : CanonicalValue;
    internal sealed record UInt64CanonicalValue(ulong Value) : CanonicalValue;
    internal sealed record UuidCanonicalValue(Guid Value) : CanonicalValue;
    internal sealed record EnumCanonicalValue(string Value) : CanonicalValue;
    internal sealed record StringCanonicalValue(string Value) : CanonicalValue;
    internal sealed record BytesCanonicalValue(byte[] Value) : CanonicalValue;
    internal sealed record ListCanonicalValue(IReadOnlyList<CanonicalValue> Value) : CanonicalValue;
    internal sealed record ObjectCanonicalValue(IReadOnlyList<CanonicalField> Value) : CanonicalValue;
}

/// <summary>Writes and validates the versioned Story 12.2 canonical byte grammar.</summary>
public static class CanonicalEnvelope
{
    private static readonly byte[] _magic = [0x56, 0x47, 0x43, 0x45, 0x01];
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private static readonly HashSet<string> _domains = new(StringComparer.Ordinal)
    {
        "vulgata/story-12.2/file-code-unit-identity/1.0",
        "vulgata/story-12.2/codegraph-completeness-proof/1.0",
        "vulgata/story-12.2/java-readiness/1.0",
        "vulgata/story-12.2/prepared-scan-context-snapshot/1.0",
        "vulgata/story-12.2/communication-classification/1.0",
        "vulgata/story-12.2/prepared-scan-audit/1.0",
        "vulgata/story-12.2/pair-selection/1.0",
        "vulgata/story-12.2/prepared-scan-manifest/1.0",
        "vulgata/story-12.2/prepared-scan-pair-package/1.0",
        "vulgata/story-12.2/deterministic-uuid/1.0",
        "vulgata/story-12.5/prepared-scan-initiation-execution/1.0",
    };

    public static CanonicalValue Null() => new CanonicalValue.NullCanonicalValue();

    public static CanonicalValue Boolean(bool value) => new CanonicalValue.BooleanCanonicalValue(value);

    public static CanonicalValue Int64(long value) => new CanonicalValue.Int64CanonicalValue(value);

    public static CanonicalValue UInt64(ulong value) => new CanonicalValue.UInt64CanonicalValue(value);

    public static CanonicalValue Uuid(Guid value) => new CanonicalValue.UuidCanonicalValue(value);

    public static CanonicalValue Enum(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!value.All(character => character is >= (char)0x21 and <= (char)0x7e))
        {
            throw new ArgumentException("Canonical enum members must be nonempty printable ASCII.", nameof(value));
        }

        return new CanonicalValue.EnumCanonicalValue(value);
    }

    public static CanonicalValue String(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = _strictUtf8.GetByteCount(value);
        return new CanonicalValue.StringCanonicalValue(value);
    }

    public static CanonicalValue Bytes(ReadOnlySpan<byte> value) =>
        new CanonicalValue.BytesCanonicalValue(value.ToArray());

    public static CanonicalValue List(IEnumerable<CanonicalValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        CanonicalValue[] copy = values.ToArray();
        if (copy.Any(value => value is null))
        {
            throw new ArgumentException("Canonical lists cannot contain null references.", nameof(values));
        }

        return new CanonicalValue.ListCanonicalValue(Array.AsReadOnly(copy));
    }

    public static CanonicalValue Object(IEnumerable<CanonicalField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        CanonicalField[] copy = fields.ToArray();
        ushort previous = 0;
        foreach (CanonicalField field in copy)
        {
            if (field.Tag == 0 || field.Tag <= previous || field.Value is null)
            {
                throw new ArgumentException("Canonical object tags must be nonzero, unique, and strictly ascending.", nameof(fields));
            }

            previous = field.Tag;
        }

        return new CanonicalValue.ObjectCanonicalValue(Array.AsReadOnly(copy));
    }

    public static byte[] Create(string domain, CanonicalValue root)
    {
        if (root is not CanonicalValue.ObjectCanonicalValue)
        {
            throw new ArgumentException("A canonical envelope root must be an object.", nameof(root));
        }

        using var stream = new MemoryStream();
        stream.Write(_magic);
        WriteValue(stream, String(RequireDomain(domain)));
        WriteValue(stream, root);
        return stream.ToArray();
    }

    public static byte[] Create(string domain, IEnumerable<CanonicalField> fields) =>
        Create(domain, Object(fields));

    public static bool Validate(ReadOnlySpan<byte> envelope)
    {
        try
        {
            int offset = 0;
            RequireBytes(envelope, ref offset, _magic);
            CanonicalValue domain = ReadValue(envelope, ref offset);
            if (domain is not CanonicalValue.StringCanonicalValue domainString)
            {
                return false;
            }

            _ = RequireDomain(domainString.Value);
            if (ReadValue(envelope, ref offset) is not CanonicalValue.ObjectCanonicalValue)
            {
                return false;
            }

            return offset == envelope.Length;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static Guid DeriveUuid(string kind, ReadOnlySpan<byte> artifactFingerprint)
    {
        if (artifactFingerprint.Length != 32)
        {
            throw new ArgumentException("A SHA-256 fingerprint is required.", nameof(artifactFingerprint));
        }

        byte[] envelope = Create(
            "vulgata/story-12.2/deterministic-uuid/1.0",
            [
                new CanonicalField(1, Enum(kind)),
                new CanonicalField(2, Bytes(artifactFingerprint)),
            ]);
        byte[] uuidBytes = SHA256.HashData(envelope)[..16];
        uuidBytes[6] = (byte)((uuidBytes[6] & 0x0f) | 0x80);
        uuidBytes[8] = (byte)((uuidBytes[8] & 0x3f) | 0x80);
        return new Guid(uuidBytes, bigEndian: true);
    }

    private static void WriteValue(Stream stream, CanonicalValue value)
    {
        switch (value)
        {
            case CanonicalValue.NullCanonicalValue:
                stream.WriteByte(0x00);
                break;
            case CanonicalValue.BooleanCanonicalValue boolean:
                stream.WriteByte(0x01);
                stream.WriteByte(boolean.Value ? (byte)1 : (byte)0);
                break;
            case CanonicalValue.Int64CanonicalValue signed:
                stream.WriteByte(0x02);
                WriteInt64(stream, signed.Value);
                break;
            case CanonicalValue.UInt64CanonicalValue unsigned:
                stream.WriteByte(0x03);
                WriteUInt64(stream, unsigned.Value);
                break;
            case CanonicalValue.UuidCanonicalValue uuid:
                stream.WriteByte(0x04);
                Span<byte> uuidBytes = stackalloc byte[16];
                _ = uuid.Value.TryWriteBytes(uuidBytes, bigEndian: true, out _);
                stream.Write(uuidBytes);
                break;
            case CanonicalValue.EnumCanonicalValue member:
                stream.WriteByte(0x05);
                WriteLengthPrefixed(stream, Encoding.ASCII.GetBytes(member.Value));
                break;
            case CanonicalValue.StringCanonicalValue text:
                stream.WriteByte(0x06);
                WriteLengthPrefixed(stream, _strictUtf8.GetBytes(text.Value));
                break;
            case CanonicalValue.BytesCanonicalValue bytes:
                stream.WriteByte(0x07);
                WriteLengthPrefixed(stream, bytes.Value);
                break;
            case CanonicalValue.ListCanonicalValue list:
                stream.WriteByte(0x08);
                WriteUInt32(stream, checked((uint)list.Value.Count));
                foreach (CanonicalValue item in list.Value)
                {
                    WriteValue(stream, item);
                }

                break;
            case CanonicalValue.ObjectCanonicalValue @object:
                stream.WriteByte(0x09);
                WriteUInt32(stream, checked((uint)@object.Value.Count));
                foreach (CanonicalField field in @object.Value)
                {
                    WriteUInt16(stream, field.Tag);
                    WriteValue(stream, field.Value);
                }

                break;
            default:
                throw new ArgumentException("Unknown canonical value.", nameof(value));
        }
    }

    private static CanonicalValue ReadValue(ReadOnlySpan<byte> source, ref int offset)
    {
        byte tag = ReadByte(source, ref offset);
        return tag switch
        {
            0x00 => Null(),
            0x01 => ReadBoolean(source, ref offset),
            0x02 => Int64(unchecked((long)ReadUInt64(source, ref offset))),
            0x03 => UInt64(ReadUInt64(source, ref offset)),
            0x04 => ReadUuid(source, ref offset),
            0x05 => Enum(ReadAscii(source, ref offset)),
            0x06 => String(ReadUtf8(source, ref offset)),
            0x07 => Bytes(ReadLengthPrefixed(source, ref offset)),
            0x08 => ReadList(source, ref offset),
            0x09 => ReadObject(source, ref offset),
            _ => throw new ArgumentException("Unknown canonical value tag."),
        };
    }

    private static CanonicalValue ReadBoolean(ReadOnlySpan<byte> source, ref int offset) =>
        ReadByte(source, ref offset) switch
        {
            0 => Boolean(false),
            1 => Boolean(true),
            _ => throw new ArgumentException("Invalid canonical Boolean."),
        };

    private static CanonicalValue ReadUuid(ReadOnlySpan<byte> source, ref int offset)
    {
        ReadOnlySpan<byte> bytes = ReadExact(source, ref offset, 16);
        return Uuid(new Guid(bytes, bigEndian: true));
    }

    private static CanonicalValue ReadList(ReadOnlySpan<byte> source, ref int offset)
    {
        uint count = ReadUInt32(source, ref offset);
        var values = new List<CanonicalValue>(RequireIntCount(count));
        for (uint index = 0; index < count; index++)
        {
            values.Add(ReadValue(source, ref offset));
        }

        return List(values);
    }

    private static CanonicalValue ReadObject(ReadOnlySpan<byte> source, ref int offset)
    {
        uint count = ReadUInt32(source, ref offset);
        var fields = new List<CanonicalField>(RequireIntCount(count));
        for (uint index = 0; index < count; index++)
        {
            ushort fieldTag = ReadUInt16(source, ref offset);
            fields.Add(new CanonicalField(fieldTag, ReadValue(source, ref offset)));
        }

        return Object(fields);
    }

    private static string ReadAscii(ReadOnlySpan<byte> source, ref int offset)
    {
        ReadOnlySpan<byte> bytes = ReadLengthPrefixed(source, ref offset);
        if (bytes.IsEmpty || ContainsNonPrintableAscii(bytes))
        {
            throw new ArgumentException("Invalid canonical ASCII enum member.");
        }

        return Encoding.ASCII.GetString(bytes);
    }

    private static string ReadUtf8(ReadOnlySpan<byte> source, ref int offset) =>
        _strictUtf8.GetString(ReadLengthPrefixed(source, ref offset));

    private static bool ContainsNonPrintableAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value is < 0x21 or > 0x7e)
            {
                return true;
            }
        }

        return false;
    }

    private static ReadOnlySpan<byte> ReadLengthPrefixed(ReadOnlySpan<byte> source, ref int offset)
    {
        uint length = ReadUInt32(source, ref offset);
        return ReadExact(source, ref offset, RequireIntCount(length));
    }

    private static ReadOnlySpan<byte> ReadExact(ReadOnlySpan<byte> source, ref int offset, int length)
    {
        if (length < 0 || offset > source.Length - length)
        {
            throw new ArgumentException("Truncated canonical envelope.");
        }

        ReadOnlySpan<byte> result = source.Slice(offset, length);
        offset += length;
        return result;
    }

    private static byte ReadByte(ReadOnlySpan<byte> source, ref int offset) =>
        ReadExact(source, ref offset, 1)[0];

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, ref int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(ReadExact(source, ref offset, sizeof(ushort)));

    private static uint ReadUInt32(ReadOnlySpan<byte> source, ref int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(ReadExact(source, ref offset, sizeof(uint)));

    private static ulong ReadUInt64(ReadOnlySpan<byte> source, ref int offset) =>
        BinaryPrimitives.ReadUInt64BigEndian(ReadExact(source, ref offset, sizeof(ulong)));

    private static void RequireBytes(ReadOnlySpan<byte> source, ref int offset, ReadOnlySpan<byte> expected)
    {
        if (!ReadExact(source, ref offset, expected.Length).SequenceEqual(expected))
        {
            throw new ArgumentException("Invalid canonical envelope magic or version.");
        }
    }

    private static int RequireIntCount(uint value) => value > int.MaxValue
        ? throw new ArgumentException("Canonical count exceeds the implementation bound.")
        : (int)value;

    private static string RequireDomain(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        if (!_domains.Contains(domain))
        {
            throw new ArgumentException("Unknown canonical envelope domain.", nameof(domain));
        }

        return domain;
    }

    private static void WriteLengthPrefixed(Stream stream, ReadOnlySpan<byte> value)
    {
        WriteUInt32(stream, checked((uint)value.Length));
        stream.Write(value);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt64(Stream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
