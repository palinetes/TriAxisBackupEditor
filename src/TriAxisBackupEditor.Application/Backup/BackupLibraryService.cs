using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Application.Backup;

/// <summary>Envia arquivos para a Lixeira (implementado na camada de UI/Windows).</summary>
public interface IFileRecycler
{
    void MoveToRecycleBin(string path);
}

/// <summary>Salvar capturas e gerenciar a biblioteca. O conteúdo de um .syx nunca é alterado depois de gravado.</summary>
public sealed class BackupLibraryService
{
    public const string IncompleteFolderName = "Incompletos";

    private readonly IBackupRepository _repository;
    private readonly ITriAxisSysExService _sysEx;
    private readonly IFileRecycler _recycler;
    private readonly ITechLog _log;

    public BackupLibraryService(IBackupRepository repository, ITriAxisSysExService sysEx, IFileRecycler recycler, ITechLog log)
    {
        _repository = repository;
        _sysEx = sysEx;
        _recycler = recycler;
        _log = log;
    }

    /// <summary>
    /// Grava o dump recebido num arquivo novo, relê do disco e confere o SHA-256 antes de registrar no banco.
    /// </summary>
    public BackupRecord SaveCapturedBackup(byte[] data, string folder, string notes, DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(data);

        var validation = _sysEx.Validate(data);
        if (validation.HasErrors)
        {
            throw new InvalidOperationException("Os dados recebidos não formam um SysEx válido; nada foi salvo na biblioteca.");
        }

        var hash = _sysEx.ComputeSha256(data);
        var path = BackupFileStore.WriteNew(folder, BackupFileNamer.CreateName(timestamp), data);
        VerifyWrittenFile(path, hash);

        var record = _repository.Add(new BackupRecord
        {
            FileName = Path.GetFileName(path),
            FilePath = path,
            CreatedAt = timestamp,
            SizeBytes = data.Length,
            Sha256 = hash,
            Firmware = null,
            Notes = notes?.Trim() ?? string.Empty,
            Origin = BackupOrigins.Captured,
            HeaderHex = HeaderOf(data)
        });

        _log.Info($"Backup salvo: \"{path}\" ({data.Length} bytes, SHA-256 {hash}).");
        return record;
    }

    /// <summary>
    /// Salva um dump produzido pelo editor como arquivo NOVO (TriAxis_Editado_...syx). Exige que o resultado
    /// continue íntegro e no formato do mapa; o arquivo de origem nunca é alterado.
    /// </summary>
    public BackupRecord SaveEditedDump(byte[] data, string folder, string notes, DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(data);

        var validation = _sysEx.Validate(data);
        if (validation.HasErrors || !validation.MatchesTriAxisLayout)
        {
            throw new InvalidOperationException("O dump editado não passou na validação; nada foi salvo.");
        }

        var hash = _sysEx.ComputeSha256(data);
        var path = BackupFileStore.WriteNew(folder, BackupFileNamer.CreateEditedName(timestamp), data);
        VerifyWrittenFile(path, hash);

        var record = _repository.Add(new BackupRecord
        {
            FileName = Path.GetFileName(path),
            FilePath = path,
            CreatedAt = timestamp,
            SizeBytes = data.Length,
            Sha256 = hash,
            Notes = notes?.Trim() ?? string.Empty,
            Origin = BackupOrigins.Edited,
            HeaderHex = HeaderOf(data)
        });

        _log.Info($"Dump editado salvo: \"{path}\" ({data.Length} bytes, SHA-256 {hash}). {notes}");
        return record;
    }

    /// <summary>Salva dados de uma recepção interrompida como .syx.part em "Incompletos". Não entra na biblioteca.</summary>
    public string SaveIncomplete(byte[] data, string folder, DateTime timestamp)
    {
        var target = Path.Combine(folder, IncompleteFolderName);
        var path = BackupFileStore.WriteNew(target, BackupFileNamer.CreateIncompleteName(timestamp), data);
        _log.Warn($"Recepção incompleta salva como arquivo temporário: \"{path}\" ({data.Length} bytes, SHA-256 {_sysEx.ComputeSha256(data)}).");
        return path;
    }

    public IReadOnlyList<BackupRecord> Search(string? term) =>
        string.IsNullOrWhiteSpace(term) ? _repository.GetAll() : _repository.Search(term);

    /// <summary>Importa um .syx existente. Arquivos fora da pasta de backups são copiados para ela.</summary>
    public BackupRecord Import(string sourcePath, string backupFolder, string notes)
    {
        var data = File.ReadAllBytes(sourcePath);
        var validation = _sysEx.Validate(data);
        if (validation.HasErrors)
        {
            throw new InvalidOperationException(
                "O arquivo não é um SysEx válido e não foi importado:\n" + string.Join("\n", validation.Issues.Select(i => "• " + i.Message)));
        }

        var existing = _repository.FindByPath(Path.GetFullPath(sourcePath));
        if (existing is not null)
        {
            throw new InvalidOperationException("Este arquivo já está na biblioteca.");
        }

        var sourceFolder = Path.GetFullPath(Path.GetDirectoryName(sourcePath) ?? ".");
        var inBackupFolder = string.Equals(sourceFolder.TrimEnd('\\'), Path.GetFullPath(backupFolder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        var path = inBackupFolder ? Path.GetFullPath(sourcePath) : BackupFileStore.CopyInto(sourcePath, backupFolder);

        var hash = _sysEx.ComputeSha256(data);
        VerifyWrittenFile(path, hash);

        var record = _repository.Add(new BackupRecord
        {
            FileName = Path.GetFileName(path),
            FilePath = path,
            CreatedAt = File.GetLastWriteTime(sourcePath),
            SizeBytes = data.Length,
            Sha256 = hash,
            Notes = notes?.Trim() ?? string.Empty,
            Origin = BackupOrigins.Imported,
            HeaderHex = HeaderOf(data)
        });

        _log.Info($"Importado: \"{sourcePath}\" → \"{path}\" (SHA-256 {hash}).");
        return record;
    }

    public BackupRecord Duplicate(BackupRecord source)
    {
        EnsureFile(source);
        var path = BackupFileStore.Duplicate(source.FilePath);
        VerifyWrittenFile(path, source.Sha256);

        var copy = source.Copy();
        copy.Id = 0;
        copy.FileName = Path.GetFileName(path);
        copy.FilePath = path;
        copy.CreatedAt = DateTime.Now;
        copy.Origin = BackupOrigins.Duplicated;
        copy.Notes = string.IsNullOrWhiteSpace(source.Notes) ? $"Cópia de {source.FileName}" : source.Notes;

        var stored = _repository.Add(copy);
        _log.Info($"Duplicado: \"{source.FilePath}\" → \"{path}\".");
        return stored;
    }

    public BackupRecord Rename(BackupRecord record, string newFileName)
    {
        EnsureFile(record);
        var newPath = BackupFileStore.Rename(record.FilePath, newFileName);
        var updated = record.Copy();
        updated.FilePath = newPath;
        updated.FileName = Path.GetFileName(newPath);
        _repository.Update(updated);
        _log.Info($"Renomeado: \"{record.FilePath}\" → \"{newPath}\".");
        return updated;
    }

    public BackupRecord UpdateNotes(BackupRecord record, string notes)
    {
        var updated = record.Copy();
        updated.Notes = notes?.Trim() ?? string.Empty;
        _repository.Update(updated);
        return updated;
    }

    /// <summary>Remove da biblioteca e, se pedido, envia o arquivo para a Lixeira (nunca apaga definitivamente).</summary>
    public void Delete(BackupRecord record, bool moveFileToRecycleBin)
    {
        if (moveFileToRecycleBin && File.Exists(record.FilePath))
        {
            _recycler.MoveToRecycleBin(record.FilePath);
            _log.Info($"Arquivo enviado para a Lixeira: \"{record.FilePath}\".");
        }

        _repository.Delete(record.Id);
        _log.Info($"Removido da biblioteca: \"{record.FileName}\" (id {record.Id}).");
    }

    public void ExportCsv(IEnumerable<BackupRecord> records, string path)
    {
        CsvExporter.Export(records, path);
        _log.Info($"Biblioteca exportada para CSV: \"{path}\".");
    }

    public DumpComparison Compare(BackupRecord a, BackupRecord b)
    {
        EnsureFile(a);
        EnsureFile(b);
        var comparison = _sysEx.Compare(File.ReadAllBytes(a.FilePath), File.ReadAllBytes(b.FilePath));
        _log.Info($"Comparação: \"{a.FileName}\" × \"{b.FileName}\": {(comparison.Identical ? "idênticos" : $"{comparison.DifferenceCount} bytes diferentes")}.");
        return comparison;
    }

    private static void EnsureFile(BackupRecord record)
    {
        if (!File.Exists(record.FilePath))
        {
            throw new FileNotFoundException($"O arquivo \"{record.FilePath}\" não existe mais. Ele foi movido ou apagado fora do programa?", record.FilePath);
        }
    }

    private static void VerifyWrittenFile(string path, string expectedHash)
    {
        var actual = SysExHash.Sha256HexOfFile(path);
        if (!string.Equals(actual, expectedHash, StringComparison.Ordinal))
        {
            throw new IOException($"Falha na verificação: o arquivo gravado \"{path}\" não confere com os dados (SHA-256 diferente). Verifique o disco.");
        }
    }

    private static string HeaderOf(byte[] data) => HexFormatter.ToHex(data.AsSpan(0, Math.Min(8, data.Length)));
}
