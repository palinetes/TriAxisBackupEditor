using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Tests;

public class TriAxisDumpEditorTests
{
    private static TriAxisDumpEditor CreateEditor(byte[]? dump = null)
    {
        Assert.True(TriAxisDumpEditor.TryCreate(dump ?? TestDumps.CreateTriAxisDump(), out var editor, out var reason), reason);
        return editor!;
    }

    /// <summary>Todos os bytes fora das regiões editáveis (presets e programas) devem permanecer idênticos.</summary>
    private static void AssertOnlyEditableRegionsChanged(byte[] before, byte[] after)
    {
        Assert.Equal(before.Length, after.Length);
        for (var i = 0; i < before.Length; i++)
        {
            if (i >= TriAxisDumpLayout.PresetsOffset && i < TriAxisDumpLayout.TrailerOffset)
            {
                continue;
            }

            Assert.True(before[i] == after[i], $"Byte fora da região editável alterado em 0x{i:X4}.");
        }
    }

    [Fact]
    public void Editor_RefusesFilesOutsideTheMap()
    {
        Assert.False(TriAxisDumpEditor.TryCreate(new byte[] { 0xF0, 0x43, 0x01, 0xF7 }, out var editor, out var reason));
        Assert.Null(editor);
        Assert.Contains("1126", reason);
    }

    [Fact]
    public void Editor_NeverModifiesSourceArray()
    {
        var source = TestDumps.CreateTriAxisDump();
        var snapshot = (byte[])source.Clone();
        var editor = CreateEditor(source);

        editor.SetKnob(1, PresetParameter.Gain, 15);
        editor.SetMode(1, TriAxisMode.Lead2Red);
        editor.SetProgramPreset(5, 90);

        Assert.Equal(snapshot, source);
        Assert.True(editor.IsModified);
    }

    [Fact]
    public void SetKnob_WritesExpectedOffsetOnly()
    {
        var source = TestDumps.CreateTriAxisDump();
        var editor = CreateEditor(source);

        editor.SetKnob(37, PresetParameter.Gain, 11); // 7.0

        var changes = editor.GetChanges();
        var diff = Assert.Single(changes.Differences);
        Assert.Equal(TriAxisDumpLayout.PresetsOffset + (36 * TriAxisDumpLayout.PresetLength) + 8, diff.Offset);
        Assert.Equal((byte)0x0B, diff.B);
        Assert.Equal(11, editor.GetKnob(37, PresetParameter.Gain));
        Assert.Equal("7.0", TriAxisValues.KnobDisplay[11]);
        AssertOnlyEditableRegionsChanged(source, editor.ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public void SetKnob_RejectsValuesOutsideTable(int value)
    {
        var editor = CreateEditor();
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetKnob(1, PresetParameter.Bass, value));
        Assert.False(editor.IsModified);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public void InvalidPresetNumber_Throws(int preset)
    {
        var editor = CreateEditor();
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetKnob(preset, PresetParameter.Bass, 1));
    }

    [Fact]
    public void Modes_RoundTripAllEight()
    {
        var editor = CreateEditor();
        foreach (var mode in Enum.GetValues<TriAxisMode>())
        {
            editor.SetMode(10, mode);
            Assert.Equal(mode, editor.GetMode(10));
        }
    }

    [Fact]
    public void Switches_PreserveUnknownBits()
    {
        var dump = TestDumps.CreateTriAxisDump();
        var fxOffset = TriAxisDumpLayout.PresetsOffset + TriAxisValues.FxOffset;
        dump[fxOffset] = 0x41; // FX loop + bit desconhecido 0x40
        var editor = CreateEditor(dump);

        editor.SetSwitch(1, FxSwitch.Switch3, true);
        editor.SetSwitch(1, FxSwitch.FxLoop, false);

        Assert.Equal((byte)0x48, editor.GetPresetByte(1, TriAxisValues.FxOffset));
        Assert.True(editor.GetSwitch(1, FxSwitch.Switch3));
        Assert.False(editor.GetSwitch(1, FxSwitch.FxLoop));
        Assert.Throws<ArgumentException>(() => editor.SetSwitch(1, FxSwitch.Switch1 | FxSwitch.Switch2, true));
    }

    [Fact]
    public void ProgramMap_EditsSingleByte()
    {
        var source = TestDumps.CreateTriAxisDump();
        var editor = CreateEditor(source);

        editor.SetProgramPreset(128, 17);

        Assert.Equal(17, editor.GetProgramPreset(128));
        var diff = Assert.Single(editor.GetChanges().Differences);
        Assert.Equal(TriAxisDumpLayout.ProgramsOffset + 127, diff.Offset);
        Assert.Equal((byte)16, diff.B);
    }

    [Fact]
    public void UnknownProgramByte_IsReportedAsNull()
    {
        var dump = TestDumps.CreateTriAxisDump();
        dump[TriAxisDumpLayout.ProgramsOffset] = 0x70;

        Assert.Null(CreateEditor(dump).GetProgramPreset(1));
    }

    [Fact]
    public void CopyPasteAndRevert_Work()
    {
        var editor = CreateEditor();
        editor.SetKnob(2, PresetParameter.Treble, 15);

        editor.CopyPreset(2, 3);
        Assert.Equal(editor.GetPresetBytes(2), editor.GetPresetBytes(3));

        var clipboard = editor.GetPresetBytes(2);
        editor.SetPresetBytes(4, clipboard);
        Assert.Equal(clipboard, editor.GetPresetBytes(4));

        editor.RevertPreset(3);
        Assert.False(editor.IsPresetModified(3));
        Assert.True(editor.IsPresetModified(2));

        editor.RevertAll();
        Assert.False(editor.IsModified);
    }

    [Fact]
    public void PastingInvalidBytes_IsRejected()
    {
        var editor = CreateEditor();
        Assert.Throws<ArgumentException>(() => editor.SetPresetBytes(1, new byte[11] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x80, 0 }));
        Assert.Throws<ArgumentException>(() => editor.SetPresetBytes(1, new byte[5]));
    }

    [Fact]
    public void EditedDump_StaysValidAndParsable()
    {
        var editor = CreateEditor();
        var random = new Random(42);
        for (var i = 0; i < 500; i++)
        {
            var preset = random.Next(1, 91);
            editor.SetKnob(preset, (PresetParameter)random.Next(0, 9), random.Next(0, 16));
            editor.SetMode(preset, (TriAxisMode)random.Next(0, 8));
            editor.SetSwitch(preset, (FxSwitch)(1 << random.Next(0, 5)), random.Next(2) == 0);
            editor.SetProgramPreset(random.Next(1, 129), random.Next(1, 91));
        }

        var result = editor.ToArray();
        var validation = SysExValidator.Validate(result);

        Assert.False(validation.HasErrors);
        Assert.True(validation.MatchesTriAxisLayout);
        Assert.True(TriAxisDumpParser.TryParse(result, out var parsed, out _));
        Assert.DoesNotContain(parsed!.Presets, p => p.HasUnknownValues);
        AssertOnlyEditableRegionsChanged(editor.OriginalBytes(), result);
    }

    [Fact]
    public void SaveEditedDump_CreatesNewFileAndRecord()
    {
        var folder = TestDumps.TempFolder();
        var repo = new SqliteBackupRepository(Path.Combine(folder, "lib.db"));
        repo.Initialize();
        var library = new BackupLibraryService(repo, new TriAxisSysExService(), new NoRecycler(), NullTechLog.Instance);
        var editor = CreateEditor();
        editor.SetKnob(1, PresetParameter.Gain, 15);

        var record = library.SaveEditedDump(editor.ToArray(), folder, "Gain 10", new DateTime(2026, 10, 16, 22, 0, 0));

        Assert.Equal("TriAxis_Editado_2026-10-16_22-00-00.syx", record.FileName);
        Assert.Equal(BackupOrigins.Edited, record.Origin);
        Assert.Equal(editor.ToArray(), File.ReadAllBytes(record.FilePath));
        Assert.True(BackupFileNamer.IsEditedName(record.FilePath));
        Assert.Throws<InvalidOperationException>(() => library.SaveEditedDump(new byte[] { 0xF0, 0x01, 0xF7 }, folder, "", DateTime.Now));
    }

    private sealed class NoRecycler : IFileRecycler
    {
        public void MoveToRecycleBin(string path) => throw new InvalidOperationException();
    }
}
