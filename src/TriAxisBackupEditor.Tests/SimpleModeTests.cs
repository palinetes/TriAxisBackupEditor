using TriAxisBackupEditor.Application.Connection;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Application.Presets;
using TriAxisBackupEditor.Application.Restore;
using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Tests;

public class DevicePairingTests
{
    private static MidiDeviceInfo In(int i, string name, int occ = 0) => new(MidiDirection.Input, i, name, occ);

    private static MidiDeviceInfo Out(int i, string name, int occ = 0) => new(MidiDirection.Output, i, name, occ);

    [Fact]
    public void SameName_IsPaired()
    {
        var pairs = DevicePairing.Pair(new[] { In(0, "UM-ONE") }, new[] { Out(0, "Microsoft GS Wavetable Synth"), Out(1, "UM-ONE") });

        var pair = Assert.Single(pairs);
        Assert.Equal("UM-ONE", pair.Name);
        Assert.Equal(1, pair.Output.Index);
    }

    [Fact]
    public void WindowsMultiPortNames_ArePairedWithFriendlyName()
    {
        var pairs = DevicePairing.Pair(new[] { In(0, "MIDIIN2 (USB MIDI Interface)") }, new[] { Out(0, "MIDIOUT2 (USB MIDI Interface)") });

        Assert.Equal("USB MIDI Interface", Assert.Single(pairs).Name);
    }

    [Fact]
    public void InOutWords_AreIgnored()
    {
        var pairs = DevicePairing.Pair(new[] { In(0, "Babyface Midi Port 1"), In(1, "Cable In") }, new[] { Out(0, "Babyface Midi Port 1"), Out(1, "Cable Out") });

        Assert.Equal(new[] { "Babyface Midi Port 1", "Cable" }, pairs.Select(p => p.Name));
    }

    [Fact]
    public void SoftwareSynthWithoutInput_IsNotOffered()
    {
        Assert.Empty(DevicePairing.Pair(Array.Empty<MidiDeviceInfo>(), new[] { Out(0, "Microsoft GS Wavetable Synth") }));
    }

    [Fact]
    public void DuplicateNames_KeepTheirOccurrence()
    {
        var pairs = DevicePairing.Pair(
            new[] { In(0, "USB MIDI", 0), In(1, "USB MIDI", 1) },
            new[] { Out(0, "USB MIDI", 0), Out(1, "USB MIDI", 1) });

        Assert.Equal(new[] { "USB MIDI", "USB MIDI (2)" }, pairs.Select(p => p.Name));
        Assert.Equal(1, pairs[1].Output.Occurrence);
    }
}

public class SafeReplaceFlowTests
{
    private static RestorePlan Plan(byte[] data)
    {
        var path = Path.Combine(TestDumps.TempFolder(), "x.syx");
        File.WriteAllBytes(path, data);
        return new RestoreService(new FakeMidiDeviceService(), new TriAxisSysExService(), NullTechLog.Instance).Prepare(path);
    }

    private static BackupRecord SavedRecord()
    {
        var path = Path.Combine(TestDumps.TempFolder(), "seguranca.syx");
        File.WriteAllBytes(path, TestDumps.CreateTriAxisDump());
        return new BackupRecord { FilePath = path, FileName = "seguranca.syx" };
    }

    [Fact]
    public void Confirmation_RequiresWarningAndSafetyBackup()
    {
        var flow = new SafeReplaceFlow(Plan(TestDumps.CreateTriAxisDump()));

        Assert.Throws<RestoreBlockedException>(() => flow.CreateConfirmation());
        Assert.Throws<InvalidOperationException>(() => flow.RegisterSafetyBackup(SavedRecord()));

        flow.AcceptWarning();
        Assert.Equal(SafeReplaceStep.SafetyBackup, flow.Step);
        Assert.Throws<RestoreBlockedException>(() => flow.CreateConfirmation());

        flow.RegisterSafetyBackup(SavedRecord());
        var confirmation = flow.CreateConfirmation();

        Assert.True(confirmation.IsCompleteFor(flow.Plan.Validation));
        Assert.NotNull(flow.SafetyBackup);
    }

    [Fact]
    public void MissingSafetyFile_IsRejected()
    {
        var flow = new SafeReplaceFlow(Plan(TestDumps.CreateTriAxisDump()));
        flow.AcceptWarning();

        Assert.Throws<InvalidOperationException>(() => flow.RegisterSafetyBackup(new BackupRecord { FilePath = @"C:\nao\existe.syx" }));
        Assert.Equal(SafeReplaceStep.SafetyBackup, flow.Step);
    }

    [Fact]
    public void FilesWithWarnings_StayInAdvancedMode()
    {
        var flow = new SafeReplaceFlow(Plan(new byte[] { 0xF0, 0x43, 0x10, 0x01, 0xF7 }));

        Assert.False(flow.IsAllowed);
        Assert.Contains("modo avançado", flow.BlockReason);
        Assert.Throws<RestoreBlockedException>(() => flow.AcceptWarning());
    }

    [Fact]
    public void InvalidFiles_AreBlocked()
    {
        var flow = new SafeReplaceFlow(Plan(new byte[] { 0xF0, 0x00, 0x00 }));

        Assert.False(flow.IsAllowed);
        Assert.Contains("danificado", flow.BlockReason);
    }

    [Fact]
    public async Task FullFlow_SendsThroughRestoreService()
    {
        var midi = new FakeMidiDeviceService();
        var service = new RestoreService(midi, new TriAxisSysExService(), NullTechLog.Instance);
        var dump = TestDumps.CreateTriAxisDump();
        var path = Path.Combine(TestDumps.TempFolder(), "editado.syx");
        File.WriteAllBytes(path, dump);
        var flow = new SafeReplaceFlow(service.Prepare(path));

        flow.AcceptWarning();
        flow.RegisterSafetyBackup(SavedRecord());
        var result = await service.RestoreAsync(flow.Plan, flow.CreateConfirmation(), FakeMidiDeviceService.Out, new TransmitOptions(128, 5), null, CancellationToken.None);

        Assert.Equal(TransmitStatus.Completed, result.Status);
        Assert.Equal(dump, Assert.Single(midi.OpenedOutputs).AllBytes);
    }
}

public class PresetNamingTests
{
    private static readonly List<string> Names = Enumerable.Range(1, 90).Select(i => i == 3 ? "Base limpa" : string.Empty).ToList();

    [Fact]
    public void Labels_UseNamesWhenPresent()
    {
        Assert.Equal("03 · Base limpa", PresetNaming.PresetLabel(3, Names));
        Assert.Equal("Timbre 04", PresetNaming.PresetLabel(4, Names));
        Assert.Null(PresetNaming.NameFor(91, Names));
    }

    [Fact]
    public void PedalLabel_FollowsProgramMap()
    {
        var bytes = TestDumps.CreateTriAxisDump();
        bytes[TriAxisDumpLayout.ProgramsOffset + 9] = 2; // programa 10 → preset 3
        Assert.True(TriAxisDumpParser.TryParse(bytes, out var dump, out _));

        Assert.Equal("Base limpa", PresetNaming.PedalLabel(10, dump, Names));
        Assert.Equal("Timbre 01", PresetNaming.PedalLabel(1, dump, Names));
        Assert.Equal("Programa 5", PresetNaming.PedalLabel(5, null, Names));
    }

    [Fact]
    public void UnknownProgramByte_FallsBackToProgramNumber()
    {
        var bytes = TestDumps.CreateTriAxisDump();
        bytes[TriAxisDumpLayout.ProgramsOffset] = 0x70;
        Assert.True(TriAxisDumpParser.TryParse(bytes, out var dump, out _));

        Assert.Equal("Programa 1", PresetNaming.PedalLabel(1, dump, Names));
    }

    [Fact]
    public void Settings_NormalizeUiDefaults()
    {
        var settings = new AppSettings { UiMode = "xyz", PresetNames = new List<string> { " Solo " } }.Normalize(@"C:\x");

        Assert.Equal(AppSettings.SimpleMode, settings.UiMode);
        Assert.Equal(90, settings.PresetNames.Count);
        Assert.Equal("Solo", settings.PresetNames[0]);
        Assert.False(settings.FirstRunCompleted);
    }
}
