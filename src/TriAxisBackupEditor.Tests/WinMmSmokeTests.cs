using System.Runtime.Versioning;
using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.Midi.WinMm;

namespace TriAxisBackupEditor.Tests;

/// <summary>Chama a API real do Windows (somente leitura): enumerar portas nunca envia dados.</summary>
[SupportedOSPlatform("windows")]
public class WinMmSmokeTests
{
    [Fact]
    public void EnumeratingDevices_DoesNotThrow_AndProducesStableKeys()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new WinMmMidiDeviceService();

        var inputs = service.GetInputDevices();
        var outputs = service.GetOutputDevices();

        Assert.All(inputs, d => Assert.Equal(MidiDirection.Input, d.Direction));
        Assert.All(outputs, d => Assert.Equal(MidiDirection.Output, d.Direction));
        Assert.Equal(inputs.Count, inputs.Select(d => d.Key).Distinct().Count());
        Assert.Equal(outputs.Count, outputs.Select(d => d.Key).Distinct().Count());
    }

    [Fact]
    public void OpeningMissingDevice_GivesActionableMessage()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new WinMmMidiDeviceService();
        var missing = new MidiDeviceInfo(MidiDirection.Input, 99, "Interface Inexistente ÇÃ", 0);

        var ex = Assert.Throws<MidiException>(() => service.OpenInput(missing));

        Assert.Contains("não foi encontrada", ex.Message);
        Assert.Contains("Interface Inexistente ÇÃ", ex.Message);
    }

    [Fact]
    public void DuplicateNames_GetDistinctOccurrences()
    {
        var a = new MidiDeviceInfo(MidiDirection.Input, 0, "USB MIDI", 0);
        var b = new MidiDeviceInfo(MidiDirection.Input, 1, "USB MIDI", 1);

        Assert.NotEqual(a.Key, b.Key);
        Assert.Equal("USB MIDI (2)", b.DisplayName);
        Assert.True(a.SameDeviceAs(a with { Index = 5 }));
    }
}
