using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Tests;

public class SqliteBackupRepositoryTests
{
    private static SqliteBackupRepository CreateRepository()
    {
        var repo = new SqliteBackupRepository(Path.Combine(TestDumps.TempFolder(), "test.db"));
        repo.Initialize();
        return repo;
    }

    private static BackupRecord Record(string name, DateTime date, string notes) => new()
    {
        FileName = name,
        FilePath = @"C:\backups\" + name,
        CreatedAt = date,
        SizeBytes = 2746,
        Sha256 = new string('a', 64),
        Notes = notes,
        Origin = BackupOrigins.Captured,
        HeaderHex = "F0 00 00 4A 02"
    };

    [Fact]
    public void AddAndGet_RoundTripsAllFields()
    {
        var repo = CreateRepository();
        var stored = repo.Add(Record("a.syx", new DateTime(2026, 10, 16, 20, 30, 15), "Timbres do show"));

        var loaded = repo.GetById(stored.Id)!;

        Assert.True(stored.Id > 0);
        Assert.Equal("a.syx", loaded.FileName);
        Assert.Equal(new DateTime(2026, 10, 16, 20, 30, 15), loaded.CreatedAt);
        Assert.Equal("Timbres do show", loaded.Notes);
        Assert.Null(loaded.Firmware);
        Assert.Equal("F0 00 00 4A 02", loaded.HeaderHex);
    }

    [Fact]
    public void Search_FindsByNameNotesAndBothDateFormats()
    {
        var repo = CreateRepository();
        repo.Add(Record("show_100%.syx", new DateTime(2026, 10, 16), "Ensaio de domingo"));
        repo.Add(Record("estudio.syx", new DateTime(2025, 1, 5), "Gravação"));

        Assert.Single(repo.Search("domingo"));
        Assert.Single(repo.Search("estudio"));
        Assert.Single(repo.Search("2026-10-16"));
        Assert.Single(repo.Search("16/10/2026"));
        Assert.Single(repo.Search("100%"));
        Assert.Equal(2, repo.Search("").Count);
        Assert.Empty(repo.Search("inexistente"));
    }

    [Fact]
    public void UpdateAndDelete_Work()
    {
        var repo = CreateRepository();
        var stored = repo.Add(Record("a.syx", DateTime.Now, ""));

        stored.Notes = "nova observação";
        repo.Update(stored);
        Assert.Equal("nova observação", repo.GetById(stored.Id)!.Notes);

        repo.Delete(stored.Id);
        Assert.Null(repo.GetById(stored.Id));
    }
}

public class BackupLibraryServiceTests
{
    private sealed class RecordingRecycler : IFileRecycler
    {
        public List<string> Recycled { get; } = new();

        public void MoveToRecycleBin(string path)
        {
            Recycled.Add(path);
            File.Delete(path);
        }
    }

    private static (BackupLibraryService Service, SqliteBackupRepository Repo, RecordingRecycler Recycler, string Folder) Create()
    {
        var folder = TestDumps.TempFolder();
        var repo = new SqliteBackupRepository(Path.Combine(folder, "lib.db"));
        repo.Initialize();
        var recycler = new RecordingRecycler();
        return (new BackupLibraryService(repo, new TriAxisSysExService(), recycler, NullTechLog.Instance), repo, recycler, Path.Combine(folder, "backups"));
    }

    [Fact]
    public void SaveCapturedBackup_WritesExactBytesAndMetadata()
    {
        var (service, repo, _, folder) = Create();
        var dump = TestDumps.CreateTriAxisDump();
        var timestamp = new DateTime(2026, 10, 16, 21, 0, 0);

        var record = service.SaveCapturedBackup(dump, folder, "primeiro backup", timestamp);

        Assert.Equal("TriAxis_Backup_2026-10-16_21-00-00.syx", record.FileName);
        Assert.Equal(dump, File.ReadAllBytes(record.FilePath));
        Assert.Equal(SysExHash.Sha256Hex(dump), record.Sha256);
        Assert.Equal(dump.Length, record.SizeBytes);
        Assert.Equal("primeiro backup", repo.GetById(record.Id)!.Notes);
    }

    [Fact]
    public void SaveCapturedBackup_NeverOverwritesExistingFile()
    {
        var (service, _, _, folder) = Create();
        var timestamp = new DateTime(2026, 10, 16, 21, 0, 0);
        var first = service.SaveCapturedBackup(TestDumps.CreateTriAxisDump(0), folder, "", timestamp);

        var second = service.SaveCapturedBackup(TestDumps.CreateTriAxisDump(1), folder, "", timestamp);

        Assert.NotEqual(first.FilePath, second.FilePath);
        Assert.Equal(TestDumps.CreateTriAxisDump(0), File.ReadAllBytes(first.FilePath));
    }

    [Fact]
    public void SaveCapturedBackup_RejectsInvalidData()
    {
        var (service, _, _, folder) = Create();

        Assert.Throws<InvalidOperationException>(() => service.SaveCapturedBackup(new byte[] { 0xF0, 0x01 }, folder, "", DateTime.Now));
        Assert.False(Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any());
    }

    [Fact]
    public void SaveIncomplete_GoesToSeparateFolderAsPart()
    {
        var (service, repo, _, folder) = Create();

        var path = service.SaveIncomplete(new byte[] { 0xF0, 0x00, 0x00 }, folder, new DateTime(2026, 10, 16, 21, 0, 0));

        Assert.EndsWith("_INCOMPLETO.syx.part", path);
        Assert.Contains(BackupLibraryService.IncompleteFolderName, path);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void DuplicateRenameAndDelete_KeepFilesSafe()
    {
        var (service, repo, recycler, folder) = Create();
        var original = service.SaveCapturedBackup(TestDumps.CreateTriAxisDump(), folder, "orig", DateTime.Now);

        var copy = service.Duplicate(original);
        Assert.Equal(File.ReadAllBytes(original.FilePath), File.ReadAllBytes(copy.FilePath));
        Assert.Contains("_copia", copy.FileName);

        Assert.Throws<IOException>(() => service.Rename(copy, original.FileName));

        var renamed = service.Rename(copy, "Show Ação");
        Assert.Equal("Show Ação.syx", renamed.FileName);
        Assert.True(File.Exists(renamed.FilePath));

        service.Delete(renamed, moveFileToRecycleBin: false);
        Assert.True(File.Exists(renamed.FilePath));
        Assert.Empty(recycler.Recycled);

        service.Delete(original, moveFileToRecycleBin: true);
        Assert.Single(recycler.Recycled);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Compare_DetectsIdenticalAndDifferent()
    {
        var (service, _, _, folder) = Create();
        var a = service.SaveCapturedBackup(TestDumps.CreateTriAxisDump(), folder, "", DateTime.Now);
        var b = service.Duplicate(a);
        var changed = TestDumps.CreateTriAxisDump();
        changed[TriAxisDumpLayout.PresetsOffset + (2 * TriAxisDumpLayout.PresetLength) + 8] = 0x0F;
        var c = service.SaveCapturedBackup(changed, folder, "", DateTime.Now.AddSeconds(1));

        Assert.True(service.Compare(a, b).Identical);

        var diff = service.Compare(a, c);
        Assert.False(diff.Identical);
        Assert.Equal(1, diff.DifferenceCount);
        Assert.Equal(new[] { 3 }, diff.ChangedPresets);
        Assert.Contains("Preset 03", diff.Differences[0].Region);
        Assert.Contains("Gain", diff.Differences[0].Region);
    }

    [Fact]
    public void ExportCsv_WritesHeaderAndEscapedRows()
    {
        var (service, repo, _, folder) = Create();
        service.SaveCapturedBackup(TestDumps.CreateTriAxisDump(), folder, "nota; com \"aspas\"", new DateTime(2026, 10, 16, 21, 0, 0));
        var csv = Path.Combine(folder, "lista.csv");

        service.ExportCsv(repo.GetAll(), csv);

        var lines = File.ReadAllLines(csv);
        Assert.StartsWith("Nome;Data;", lines[0]);
        Assert.Contains("\"nota; com \"\"aspas\"\"\"", lines[1]);
    }
}

public class TriAxisDumpParserTests
{
    [Fact]
    public void Parse_ReadsPresetsAndProgramMap()
    {
        var dump = TestDumps.CreateTriAxisDump();
        var p1 = TriAxisDumpLayout.PresetsOffset;
        dump[p1 + 8] = 0x0F;  // Gain 10
        dump[p1 + 7] = 0x04;  // Treble 3.5
        dump[p1 + 9] = 0x6F;  // Lead 1 Red
        dump[p1 + 10] = 0x0B; // FX + SW1 + SW3

        Assert.True(TriAxisDumpParser.TryParse(dump, out var parsed, out _));

        Assert.Equal(90, parsed!.Presets.Count);
        Assert.Equal(128, parsed.ProgramMap.Count);
        var preset = parsed.Presets[0];
        Assert.Equal("10", preset.Gain);
        Assert.Equal("3.5", preset.Treble);
        Assert.Equal("Lead 1 Red", preset.Mode);
        Assert.True(preset.FxLoop);
        Assert.True(preset.Switch1);
        Assert.False(preset.Switch2);
        Assert.True(preset.Switch3);
        Assert.False(preset.HasUnknownValues);
        Assert.Equal("01", parsed.ProgramMap[0].Preset);
    }

    [Fact]
    public void Parse_UnknownValuesAreFlaggedNotGuessed()
    {
        var dump = TestDumps.CreateTriAxisDump();
        dump[TriAxisDumpLayout.PresetsOffset + 8] = 0x20;
        dump[TriAxisDumpLayout.PresetsOffset + 9] = 0x01;

        Assert.True(TriAxisDumpParser.TryParse(dump, out var parsed, out _));

        Assert.Equal("? (0x20)", parsed!.Presets[0].Gain);
        Assert.Equal("? (0x01)", parsed.Presets[0].Mode);
        Assert.True(parsed.Presets[0].HasUnknownValues);
    }

    [Fact]
    public void Parse_RefusesFilesOutsideTheMap()
    {
        Assert.False(TriAxisDumpParser.TryParse(new byte[] { 0xF0, 0x43, 0x01, 0xF7 }, out var parsed, out var reason));
        Assert.Null(parsed);
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void HexDump_ShowsOffsetHexAndAscii()
    {
        var text = HexFormatter.FormatDump(new byte[] { 0xF0, 0x41, 0x42, 0xF7 });

        Assert.StartsWith("00000000  F0 41 42 F7", text);
        Assert.Contains("|.AB.|", text);
    }

    [Fact]
    public void HexParse_AcceptsCommonFormats()
    {
        Assert.Equal(new byte[] { 0xF0, 0x00, 0x4A, 0xF7 }, HexFormatter.Parse("F0 00 4A F7"));
        Assert.Equal(new byte[] { 0xF0, 0x00, 0x4A, 0xF7 }, HexFormatter.Parse("0xF0,0x00,0x4A,0xF7"));
        Assert.Equal(new byte[] { 0xF0, 0x00, 0x4A, 0xF7 }, HexFormatter.Parse("F0004AF7"));
        Assert.Throws<FormatException>(() => HexFormatter.Parse("F0 ZZ"));
    }
}
