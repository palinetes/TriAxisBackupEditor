using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TriAxisBackupEditor.Storage;

public sealed class SqliteBackupRepository : IBackupRepository
{
    private const string Columns = "id, file_name, file_path, created_at, size_bytes, sha256, firmware, notes, origin, header_hex";
    private const string DateFormat = "yyyy-MM-ddTHH:mm:ss";

    private readonly string _connectionString;

    public SqliteBackupRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
    }

    public void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS backups (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                file_name   TEXT    NOT NULL,
                file_path   TEXT    NOT NULL,
                created_at  TEXT    NOT NULL,
                size_bytes  INTEGER NOT NULL,
                sha256      TEXT    NOT NULL,
                firmware    TEXT    NULL,
                notes       TEXT    NOT NULL DEFAULT '',
                origin      TEXT    NOT NULL DEFAULT '',
                header_hex  TEXT    NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS ix_backups_created_at ON backups(created_at);
            CREATE INDEX IF NOT EXISTS ix_backups_sha256 ON backups(sha256);
            PRAGMA user_version = 1;
            """;
        command.ExecuteNonQuery();
    }

    public BackupRecord Add(BackupRecord record)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO backups (file_name, file_path, created_at, size_bytes, sha256, firmware, notes, origin, header_hex)
            VALUES ($name, $path, $created, $size, $sha, $fw, $notes, $origin, $header);
            SELECT last_insert_rowid();
            """;
        Bind(command, record);
        var id = (long)command.ExecuteScalar()!;
        var stored = record.Copy();
        stored.Id = id;
        return stored;
    }

    public void Update(BackupRecord record)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE backups SET file_name = $name, file_path = $path, created_at = $created, size_bytes = $size,
                sha256 = $sha, firmware = $fw, notes = $notes, origin = $origin, header_hex = $header
            WHERE id = $id;
            """;
        Bind(command, record);
        command.Parameters.AddWithValue("$id", record.Id);
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException($"Registro {record.Id} não encontrado na biblioteca.");
        }
    }

    public void Delete(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM backups WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public BackupRecord? GetById(long id) =>
        Query($"SELECT {Columns} FROM backups WHERE id = $id;", c => c.Parameters.AddWithValue("$id", id)).FirstOrDefault();

    public BackupRecord? FindByPath(string filePath) =>
        Query($"SELECT {Columns} FROM backups WHERE file_path = $path COLLATE NOCASE;", c => c.Parameters.AddWithValue("$path", filePath)).FirstOrDefault();

    public IReadOnlyList<BackupRecord> GetAll() =>
        Query($"SELECT {Columns} FROM backups ORDER BY created_at DESC, id DESC;", null);

    public IReadOnlyList<BackupRecord> Search(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return GetAll();
        }

        var text = term.Trim();
        var dateTerm = text;
        if (DateTime.TryParseExact(text, new[] { "dd/MM/yyyy", "d/M/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            dateTerm = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return Query(
            $"""
            SELECT {Columns} FROM backups
            WHERE file_name LIKE $t ESCAPE '\' OR notes LIKE $t ESCAPE '\' OR sha256 LIKE $t ESCAPE '\'
               OR created_at LIKE $d ESCAPE '\'
            ORDER BY created_at DESC, id DESC;
            """,
            c =>
            {
                c.Parameters.AddWithValue("$t", "%" + EscapeLike(text) + "%");
                c.Parameters.AddWithValue("$d", "%" + EscapeLike(dateTerm) + "%");
            });
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void Bind(SqliteCommand command, BackupRecord r)
    {
        command.Parameters.AddWithValue("$name", r.FileName);
        command.Parameters.AddWithValue("$path", r.FilePath);
        command.Parameters.AddWithValue("$created", r.CreatedAt.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$size", r.SizeBytes);
        command.Parameters.AddWithValue("$sha", r.Sha256);
        command.Parameters.AddWithValue("$fw", (object?)r.Firmware ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", r.Notes ?? string.Empty);
        command.Parameters.AddWithValue("$origin", r.Origin ?? string.Empty);
        command.Parameters.AddWithValue("$header", r.HeaderHex ?? string.Empty);
    }

    private IReadOnlyList<BackupRecord> Query(string sql, Action<SqliteCommand>? bind)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind?.Invoke(command);

        var list = new List<BackupRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new BackupRecord
            {
                Id = reader.GetInt64(0),
                FileName = reader.GetString(1),
                FilePath = reader.GetString(2),
                CreatedAt = DateTime.ParseExact(reader.GetString(3), DateFormat, CultureInfo.InvariantCulture),
                SizeBytes = reader.GetInt64(4),
                Sha256 = reader.GetString(5),
                Firmware = reader.IsDBNull(6) ? null : reader.GetString(6),
                Notes = reader.GetString(7),
                Origin = reader.GetString(8),
                HeaderHex = reader.GetString(9)
            });
        }

        return list;
    }
}
