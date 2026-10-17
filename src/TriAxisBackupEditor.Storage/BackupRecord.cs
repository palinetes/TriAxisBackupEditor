namespace TriAxisBackupEditor.Storage;

public static class BackupOrigins
{
    public const string Captured = "Capturado";
    public const string Imported = "Importado";
    public const string Duplicated = "Duplicado";
    public const string Edited = "Editado";
}

public sealed class BackupRecord
{
    public long Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Versão de firmware, quando puder ser detectada (ainda não há mapa confiável para isso).</summary>
    public string? Firmware { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string Origin { get; set; } = string.Empty;

    /// <summary>Primeiros bytes do arquivo em hexadecimal, para identificação rápida.</summary>
    public string HeaderHex { get; set; } = string.Empty;

    public bool FileExists => File.Exists(FilePath);

    public BackupRecord Copy() => (BackupRecord)MemberwiseClone();
}

public interface IBackupRepository
{
    /// <summary>Cria o banco/tabelas se necessário.</summary>
    void Initialize();

    BackupRecord Add(BackupRecord record);

    void Update(BackupRecord record);

    void Delete(long id);

    BackupRecord? GetById(long id);

    BackupRecord? FindByPath(string filePath);

    IReadOnlyList<BackupRecord> GetAll();

    /// <summary>Busca por nome, data (AAAA-MM-DD ou DD/MM/AAAA), observação ou hash.</summary>
    IReadOnlyList<BackupRecord> Search(string term);
}
