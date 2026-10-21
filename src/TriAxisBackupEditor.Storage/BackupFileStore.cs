using System.Globalization;
using System.Text;
using TriAxisBackupEditor.SysEx;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.Storage;

/// <summary>Operações de arquivo da biblioteca. Nenhuma delas sobrescreve um arquivo existente.</summary>
public static class BackupFileStore
{
    /// <summary>Grava bytes num arquivo novo (FileMode.CreateNew) e força a escrita em disco.</summary>
    public static string WriteNew(string folder, string fileName, byte[] data)
    {
        Directory.CreateDirectory(folder);
        var path = BackupFileNamer.GetUniquePath(folder, fileName);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(data, 0, data.Length);
            stream.Flush(flushToDisk: true);
        }

        return path;
    }

    public static string Duplicate(string sourcePath)
    {
        var folder = Path.GetDirectoryName(sourcePath) ?? throw new IOException(Tr("Caminho inválido."));
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        var target = BackupFileNamer.GetUniquePath(folder, stem + "_copia" + BackupFileNamer.Extension);
        File.Copy(sourcePath, target, overwrite: false);
        return target;
    }

    /// <summary>Copia um arquivo externo para a pasta de backups, mantendo o nome (com sufixo se já existir).</summary>
    public static string CopyInto(string sourcePath, string folder)
    {
        Directory.CreateDirectory(folder);
        var target = BackupFileNamer.GetUniquePath(folder, BackupFileNamer.SanitizeFileName(Path.GetFileName(sourcePath)));
        File.Copy(sourcePath, target, overwrite: false);
        return target;
    }

    public static string Rename(string path, string newFileName)
    {
        var folder = Path.GetDirectoryName(path) ?? throw new IOException(Tr("Caminho inválido."));
        var target = Path.Combine(folder, BackupFileNamer.SanitizeFileName(newFileName));
        if (string.Equals(target, path, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(target, path, StringComparison.Ordinal))
            {
                // Mudança só de maiúsculas/minúsculas.
                File.Move(path, target);
            }

            return target;
        }

        if (File.Exists(target))
        {
            throw new IOException(TrF($"Já existe um arquivo chamado \"{Path.GetFileName(target)}\". Escolha outro nome."));
        }

        File.Move(path, target, overwrite: false);
        return target;
    }
}

public static class CsvExporter
{
    public static void Export(IEnumerable<BackupRecord> records, string path)
    {
        // Ponto e vírgula + BOM: abre direto no Excel em português.
        var sb = new StringBuilder();
        sb.AppendLine(Tr("Nome;Data;Tamanho (bytes);SHA-256;Firmware;Origem;Observação;Caminho"));
        foreach (var r in records)
        {
            sb.AppendJoin(';',
                Escape(r.FileName),
                Escape(r.CreatedAt.ToString(Tr("dd/MM/yyyy HH:mm:ss"), CultureInfo.InvariantCulture)),
                r.SizeBytes.ToString(CultureInfo.InvariantCulture),
                Escape(r.Sha256),
                Escape(r.Firmware ?? string.Empty),
                Escape(r.Origin),
                Escape(r.Notes),
                Escape(r.FilePath));
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny(new[] { ';', '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
