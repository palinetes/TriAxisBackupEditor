using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Application.Presets;
using TriAxisBackupEditor.Application.Restore;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Tests;

/// <summary>
/// Formato confirmado num TriAxis real (backup de 2026-10-16): 1126 bytes. E os arquivos do editor web,
/// que guardam esse dump seguido de zeros e de outro F7 (2746 bytes).
/// </summary>
public class RealDumpFormatTests
{
    [Fact]
    public void RealDump_Is1126Bytes_AndMatchesLayoutWithoutWarnings()
    {
        var dump = TestDumps.CreateRealShapedDump();

        var validation = SysExValidator.Validate(dump);

        Assert.Equal(1126, dump.Length);
        Assert.Equal(1126, TriAxisDumpLayout.TotalLength);
        Assert.True(validation.MatchesTriAxisLayout);
        Assert.False(validation.HasWarnings);
        Assert.True(TriAxisDumpParser.TryParse(dump, out _, out _));
    }

    [Fact]
    public void RealDump_ProgramRegion_IsNotTrustedAsProgramMap()
    {
        Assert.True(TriAxisDumpParser.TryParse(TestDumps.CreateRealShapedDump(), out var dump, out _));

        Assert.False(dump!.ProgramMapLooksValid);
        var names = Enumerable.Repeat(string.Empty, 90).ToList();
        names[20] = "Solo";
        Assert.Equal("Programa 1", PresetNaming.PedalLabel(1, dump, names));
        Assert.Null(PresetNaming.PresetForProgram(1, dump));
    }

    [Fact]
    public void WebEditorFile_IsNormalizedToTheRealDump()
    {
        var dump = TestDumps.CreateRealShapedDump();
        var file = TestDumps.PadLikeWebEditor(dump);

        var content = TriAxisDumpFile.Normalize(file);

        Assert.Equal(2746, file.Length);
        Assert.True(SysExValidator.Validate(file).HasErrors); // o arquivo cru nunca é enviado como está
        Assert.True(content.WasPadded);
        Assert.Equal(dump, content.Data);
        Assert.Contains("editor web", content.Note);
    }

    [Fact]
    public void PaddingWithAnyNonZeroByte_IsNotNormalized()
    {
        var file = TestDumps.PadLikeWebEditor(TestDumps.CreateRealShapedDump());
        file[2000] = 0x05;

        var content = TriAxisDumpFile.Normalize(file);

        Assert.False(content.WasPadded);
        Assert.Same(file, content.Data);
    }

    [Fact]
    public void NormalFiles_AreReturnedUntouched()
    {
        var dump = TestDumps.CreateRealShapedDump();

        var content = TriAxisDumpFile.Normalize(dump);

        Assert.False(content.WasPadded);
        Assert.Same(dump, content.Data);
        Assert.Null(content.Note);
    }

    [Fact]
    public async Task Restore_WebEditorFile_SendsOnlyTheRealDump()
    {
        var dump = TestDumps.CreateRealShapedDump();
        var path = Path.Combine(TestDumps.TempFolder(), "triaxis.syx");
        File.WriteAllBytes(path, TestDumps.PadLikeWebEditor(dump));
        var midi = new FakeMidiDeviceService();
        var service = new RestoreService(midi, new TriAxisSysExService(), NullTechLog.Instance);

        var plan = service.Prepare(path);
        var flow = new SafeReplaceFlow(plan);
        var confirmation = new RestoreConfirmation { OverwriteAcknowledged = true, TypedPhrase = RestoreConfirmation.RequiredPhrase };
        var result = await service.RestoreAsync(plan, confirmation, FakeMidiDeviceService.Out, new TransmitOptions(128, 5), null, CancellationToken.None);

        Assert.True(plan.CanRestore);
        Assert.NotNull(plan.Note);
        Assert.True(flow.IsAllowed); // o modo simples também aceita
        Assert.Equal(TransmitStatus.Completed, result.Status);
        Assert.Equal(dump, Assert.Single(midi.OpenedOutputs).AllBytes);
    }

    [Fact]
    public void Import_WebEditorFile_CreatesCleanCopy_AndKeepsOriginal()
    {
        var folder = TestDumps.TempFolder();
        var repo = new SqliteBackupRepository(Path.Combine(folder, "lib.db"));
        repo.Initialize();
        var library = new BackupLibraryService(repo, new TriAxisSysExService(), new NoRecycler(), NullTechLog.Instance);
        var dump = TestDumps.CreateRealShapedDump();
        var source = Path.Combine(folder, "TriAxis_Backup_2026-10-16_20-53-02.syx");
        var padded = TestDumps.PadLikeWebEditor(dump);
        File.WriteAllBytes(source, padded);

        var record = library.Import(source, Path.Combine(folder, "backups"), string.Empty);

        Assert.Equal(1126, record.SizeBytes);
        Assert.Equal(dump, File.ReadAllBytes(record.FilePath));
        Assert.Equal(padded, File.ReadAllBytes(source));
        Assert.Contains("editor web", record.Notes);
    }

    [Fact]
    public void Editor_OpensNormalizedWebEditorFile()
    {
        var content = TriAxisDumpFile.Normalize(TestDumps.PadLikeWebEditor(TestDumps.CreateRealShapedDump()));

        Assert.True(TriAxisDumpEditor.TryCreate(content.Data, out var editor, out _));
        Assert.Equal(1126, editor!.ToArray().Length);
    }

    [Fact]
    public void Compare_TreatsWebEditorFileAndRealDumpAsIdentical()
    {
        var folder = TestDumps.TempFolder();
        var repo = new SqliteBackupRepository(Path.Combine(folder, "lib.db"));
        repo.Initialize();
        var library = new BackupLibraryService(repo, new TriAxisSysExService(), new NoRecycler(), NullTechLog.Instance);
        var dump = TestDumps.CreateRealShapedDump();
        var a = Path.Combine(folder, "a.syx");
        var b = Path.Combine(folder, "b.syx");
        File.WriteAllBytes(a, dump);
        File.WriteAllBytes(b, TestDumps.PadLikeWebEditor(dump));

        var result = library.Compare(new BackupRecord { FilePath = a, FileName = "a" }, new BackupRecord { FilePath = b, FileName = "b" });

        Assert.True(result.Identical);
    }

    private sealed class NoRecycler : IFileRecycler
    {
        public void MoveToRecycleBin(string path) => throw new InvalidOperationException();
    }
}
