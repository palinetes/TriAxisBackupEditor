using System.Globalization;
using System.Text;

namespace TriAxisBackupEditor.SysEx;

public static class BackupFileNamer
{
    public const string Extension = ".syx";
    public const string IncompleteExtension = ".syx.part";

    /// <summary>TriAxis_Backup_AAAA-MM-DD_HH-mm-ss.syx</summary>
    public static string CreateName(DateTime timestamp) =>
        "TriAxis_Backup_" + timestamp.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + Extension;

    /// <summary>Prefixo dos arquivos gerados pelo editor (mapa preliminar).</summary>
    public const string EditedPrefix = "TriAxis_Editado_";

    /// <summary>TriAxis_Editado_AAAA-MM-DD_HH-mm-ss.syx — dump gerado pelo editor a partir de uma cópia.</summary>
    public static string CreateEditedName(DateTime timestamp) =>
        EditedPrefix + timestamp.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + Extension;

    public static bool IsEditedName(string fileName) =>
        Path.GetFileName(fileName).StartsWith(EditedPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>TriAxis_Backup_AAAA-MM-DD_HH-mm-ss_INCOMPLETO.syx.part — nunca é tratado como backup válido.</summary>
    public static string CreateIncompleteName(DateTime timestamp) =>
        "TriAxis_Backup_" + timestamp.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "_INCOMPLETO" + IncompleteExtension;

    /// <summary>Devolve um caminho que ainda não existe, acrescentando _2, _3... Nunca sobrescreve.</summary>
    public static string GetUniquePath(string folder, string fileName)
    {
        var candidate = Path.Combine(folder, fileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var (stem, extension) = SplitExtension(fileName);
        for (var i = 2; i < 10_000; i++)
        {
            candidate = Path.Combine(folder, $"{stem}_{i}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Não foi possível gerar um nome livre para \"{fileName}\" em \"{folder}\".");
    }

    /// <summary>Remove caracteres inválidos e garante a extensão .syx. Mantém acentos e outros caracteres válidos.</summary>
    public static string SanitizeFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name.Trim())
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        var result = sb.ToString().Trim().TrimEnd('.');
        if (result.Length == 0)
        {
            throw new ArgumentException("O nome do arquivo não pode ficar vazio.", nameof(name));
        }

        if (!result.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            result += Extension;
        }

        return result;
    }

    private static (string Stem, string Extension) SplitExtension(string fileName)
    {
        if (fileName.EndsWith(IncompleteExtension, StringComparison.OrdinalIgnoreCase))
        {
            return (fileName[..^IncompleteExtension.Length], IncompleteExtension);
        }

        var ext = Path.GetExtension(fileName);
        return (fileName[..^ext.Length], ext);
    }
}
