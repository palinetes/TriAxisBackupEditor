using System.Globalization;
using System.Text;

namespace TriAxisBackupEditor.SysEx;

public static class HexFormatter
{
    /// <summary>"F0 00 00 4A 02"</summary>
    public static string ToHex(ReadOnlySpan<byte> data, string separator = " ")
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(data.Length * (2 + separator.Length));
        for (var i = 0; i < data.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(separator);
            }

            sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Visualização clássica: offset, 16 bytes em hexadecimal e coluna ASCII.
    /// <code>00000000  F0 00 00 4A 02 0F 0A 07  05 00 06 07 08 7E 01 ...  |...J........~..|</code>
    /// </summary>
    public static string FormatDump(ReadOnlySpan<byte> data, int bytesPerLine = 16)
    {
        if (bytesPerLine <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesPerLine));
        }

        var sb = new StringBuilder((data.Length / bytesPerLine + 1) * (bytesPerLine * 4 + 16));
        for (var line = 0; line < data.Length; line += bytesPerLine)
        {
            var count = Math.Min(bytesPerLine, data.Length - line);
            sb.Append(line.ToString("X8", CultureInfo.InvariantCulture)).Append("  ");

            for (var i = 0; i < bytesPerLine; i++)
            {
                if (i == bytesPerLine / 2)
                {
                    sb.Append(' ');
                }

                if (i < count)
                {
                    sb.Append(data[line + i].ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
                }
                else
                {
                    sb.Append("   ");
                }
            }

            sb.Append(" |");
            for (var i = 0; i < count; i++)
            {
                var b = data[line + i];
                sb.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');
            }

            sb.Append('|').AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>Aceita "F0 00 4A", "F0,00,4A", "0xF0 0x00", "F0004A". Lança <see cref="FormatException"/>.</summary>
    public static byte[] Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var cleaned = new StringBuilder(text.Length);
        var tokens = text.Split(new[] { ' ', ',', ';', '\t', '\r', '\n', '-' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in tokens)
        {
            var token = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw[2..] : raw;
            if (token.Length % 2 != 0)
            {
                token = "0" + token;
            }

            cleaned.Append(token);
        }

        var hex = cleaned.ToString();
        if (hex.Length == 0)
        {
            return Array.Empty<byte>();
        }

        foreach (var c in hex)
        {
            if (!Uri.IsHexDigit(c))
            {
                throw new FormatException($"Caractere inválido em hexadecimal: '{c}'.");
            }
        }

        return Convert.FromHexString(hex);
    }
}
