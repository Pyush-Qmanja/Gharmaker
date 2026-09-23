using System.Security.Cryptography;

namespace Platform.Shared.Common;

/// <summary>
/// Creates primary keys for every entity in the platform.
/// Keys are UUID version 7 (RFC 9562): time-ordered and globally unique; the
/// key is also the Firestore document id. Random bits mean it never reveals a count.
/// </summary>
public static class IdGenerator
{
    /// <summary>
    /// Generates a new UUID v7 using the current UTC time.
    /// </summary>
    /// <returns>A new, time-ordered <see cref="Guid"/>.</returns>
    public static Guid NewId() => NewId(DateTimeOffset.UtcNow);

    /// <summary>
    /// Generates a new UUID v7 for the given timestamp. Exposed so tests can
    /// produce deterministic ordering.
    /// </summary>
    /// <param name="timestamp">The instant encoded into the first 48 bits.</param>
    /// <returns>A new, time-ordered <see cref="Guid"/>.</returns>
    public static Guid NewId(DateTimeOffset timestamp)
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);

        // Bytes 0-5: 48-bit big-endian Unix timestamp in milliseconds.
        long unixMs = timestamp.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(unixMs >> 40);
        bytes[1] = (byte)(unixMs >> 32);
        bytes[2] = (byte)(unixMs >> 24);
        bytes[3] = (byte)(unixMs >> 16);
        bytes[4] = (byte)(unixMs >> 8);
        bytes[5] = (byte)unixMs;

        // Version 7 in the high nibble of byte 6, RFC variant in byte 8.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>
    /// Derives a stable key from a name (UUID version 5 style, SHA-1 based):
    /// the same name always gives the same id. Used for records that exist
    /// once per combination, such as the stock balance of one SKU in one
    /// warehouse, so they can be read and written by key without a query.
    /// </summary>
    /// <param name="name">Unique name, e.g. <c>stock_balance:&lt;warehouse&gt;:&lt;sku&gt;</c>.</param>
    /// <returns>The derived <see cref="Guid"/>.</returns>
    public static Guid FromName(string name)
    {
        byte[] hash = SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(name));
        Span<byte> bytes = hash.AsSpan(0, 16);

        // Version 5 in the high nibble of byte 6, RFC variant in byte 8.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }
}
