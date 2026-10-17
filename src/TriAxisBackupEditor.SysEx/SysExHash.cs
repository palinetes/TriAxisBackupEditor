using System.Security.Cryptography;

namespace TriAxisBackupEditor.SysEx;

public static class SysExHash
{
    /// <summary>SHA-256 em hexadecimal minúsculo (64 caracteres).</summary>
    public static string Sha256Hex(ReadOnlySpan<byte> data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static string Sha256HexOfFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
