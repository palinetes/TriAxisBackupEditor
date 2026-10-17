using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TriAxisBackupEditor.Midi.WinMm;

/// <summary>Implementação de <see cref="IMidiDeviceService"/> sobre a API MIDI nativa do Windows (winmm).</summary>
[SupportedOSPlatform("windows")]
public sealed class WinMmMidiDeviceService : IMidiDeviceService
{
    public IReadOnlyList<MidiDeviceInfo> GetInputDevices()
    {
        var count = NativeMethods.midiInGetNumDevs();
        var names = new List<string?>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var caps = new NativeMethods.MIDIINCAPS();
            var r = NativeMethods.midiInGetDevCaps((UIntPtr)i, ref caps, (uint)Marshal.SizeOf<NativeMethods.MIDIINCAPS>());
            names.Add(r == NativeMethods.MMSYSERR_NOERROR ? caps.szPname : null);
        }

        return Build(MidiDirection.Input, names);
    }

    public IReadOnlyList<MidiDeviceInfo> GetOutputDevices()
    {
        var count = NativeMethods.midiOutGetNumDevs();
        var names = new List<string?>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var caps = new NativeMethods.MIDIOUTCAPS();
            var r = NativeMethods.midiOutGetDevCaps((UIntPtr)i, ref caps, (uint)Marshal.SizeOf<NativeMethods.MIDIOUTCAPS>());
            names.Add(r == NativeMethods.MMSYSERR_NOERROR ? caps.szPname : null);
        }

        return Build(MidiDirection.Output, names);
    }

    public IMidiInputPort OpenInput(MidiDeviceInfo device)
    {
        var current = Resolve(GetInputDevices(), device);
        return new WinMmMidiInputPort((uint)current.Index, current);
    }

    public IMidiOutputPort OpenOutput(MidiDeviceInfo device)
    {
        var current = Resolve(GetOutputDevices(), device);
        return new WinMmMidiOutputPort((uint)current.Index, current);
    }

    private static MidiDeviceInfo Resolve(IReadOnlyList<MidiDeviceInfo> devices, MidiDeviceInfo wanted)
    {
        // O índice pode ter mudado desde a seleção (plug/unplug): procura pelo nome + ocorrência.
        var match = devices.FirstOrDefault(d => d.SameDeviceAs(wanted));
        if (match is null)
        {
            var kind = wanted.Direction == MidiDirection.Input ? "MIDI IN" : "MIDI OUT";
            throw new MidiException(
                $"A porta {kind} \"{wanted.DisplayName}\" não foi encontrada. Verifique se a interface USB está conectada e clique em \"Atualizar dispositivos\".");
        }

        return match;
    }

    private static IReadOnlyList<MidiDeviceInfo> Build(MidiDirection direction, IReadOnlyList<string?> names)
    {
        var result = new List<MidiDeviceInfo>(names.Count);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < names.Count; i++)
        {
            var name = string.IsNullOrWhiteSpace(names[i]) ? $"Dispositivo {i}" : names[i]!.Trim();
            occurrences.TryGetValue(name, out var n);
            occurrences[name] = n + 1;
            result.Add(new MidiDeviceInfo(direction, i, name, n));
        }

        return result;
    }
}
