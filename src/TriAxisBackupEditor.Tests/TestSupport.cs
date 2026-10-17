using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Tests;

internal static class TestDumps
{
    /// <summary>
    /// Dump com a forma de um TriAxis real (1126 bytes): cabeçalho, 90 presets e região final
    /// (como no aparelho real: <c>14</c>, zeros e <c>01 00</c>), seguida de F7.
    /// </summary>
    public static byte[] CreateRealShapedDump()
    {
        var data = CreateTriAxisDump();
        Array.Clear(data, TriAxisDumpLayout.ProgramsOffset, TriAxisDumpLayout.ProgramCount);
        data[TriAxisDumpLayout.ProgramsOffset] = 0x14;
        data[TriAxisDumpLayout.TrailerOffset] = 0x01;
        data[TriAxisDumpLayout.TrailerOffset + 1] = 0x00;
        return data;
    }

    /// <summary>Arquivo como o salvo pelo editor web: dump real + 1619 zeros + F7 (2746 bytes).</summary>
    public static byte[] PadLikeWebEditor(byte[] dump)
    {
        var file = new byte[TriAxisDumpLayout.WebEditorFileLength];
        dump.CopyTo(file, 0);
        file[^1] = SysExConstants.End;
        return file;
    }

    /// <summary>Dump sintético no formato do TriAxis (1126 bytes, cabeçalho F0 00 00 4A 02), com mapa de programas 1:1.</summary>
    public static byte[] CreateTriAxisDump(byte seed = 0)
    {
        var data = new byte[TriAxisDumpLayout.TotalLength];
        TriAxisDumpLayout.ExpectedHeader.CopyTo(data);
        for (var i = TriAxisDumpLayout.HeaderLength; i < TriAxisDumpLayout.TerminatorOffset; i++)
        {
            data[i] = (byte)((i + seed) % 0x10);
        }

        // Modo e FX de cada preset com valores conhecidos.
        for (var p = 0; p < TriAxisDumpLayout.PresetCount; p++)
        {
            var baseOffset = TriAxisDumpLayout.PresetsOffset + (p * TriAxisDumpLayout.PresetLength);
            data[baseOffset + 9] = 0x7E;
            data[baseOffset + 10] = 0x01;
        }

        for (var p = 0; p < TriAxisDumpLayout.ProgramCount; p++)
        {
            data[TriAxisDumpLayout.ProgramsOffset + p] = (byte)(p % TriAxisDumpLayout.PresetCount);
        }

        data[TriAxisDumpLayout.TrailerOffset] = 0x01;
        data[TriAxisDumpLayout.TrailerOffset + 1] = 0x00;
        data[^1] = SysExConstants.End;
        return data;
    }

    public static string TempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "TriAxisTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

internal sealed class FakeMidiDeviceService : IMidiDeviceService
{
    public static readonly MidiDeviceInfo In = new(MidiDirection.Input, 0, "UM-ONE ÄÖ #1", 0);
    public static readonly MidiDeviceInfo Out = new(MidiDirection.Output, 0, "UM-ONE ÄÖ #1", 0);

    private readonly TaskCompletionSource<FakeInputPort> _inputOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<FakeOutputPort> OpenedOutputs { get; } = new();

    public bool FailOutputOpen { get; set; }

    public Task<FakeInputPort> InputOpened => _inputOpened.Task;

    public IReadOnlyList<MidiDeviceInfo> GetInputDevices() => new[] { In };

    public IReadOnlyList<MidiDeviceInfo> GetOutputDevices() => new[] { Out };

    public IMidiInputPort OpenInput(MidiDeviceInfo device)
    {
        var port = new FakeInputPort(device);
        _inputOpened.TrySetResult(port);
        return port;
    }

    public IMidiOutputPort OpenOutput(MidiDeviceInfo device)
    {
        if (FailOutputOpen)
        {
            throw new MidiException("Porta ocupada.");
        }

        var port = new FakeOutputPort(device);
        OpenedOutputs.Add(port);
        return port;
    }
}

internal sealed class FakeInputPort : IMidiInputPort
{
    public FakeInputPort(MidiDeviceInfo device) => Device = device;

    public MidiDeviceInfo Device { get; }

    public bool Disposed { get; private set; }

    public event EventHandler<MidiSysExChunkEventArgs>? SysExChunkReceived;

    public event EventHandler<MidiShortMessageEventArgs>? ShortMessageReceived;

    public event EventHandler<MidiErrorEventArgs>? ErrorOccurred;

    public void Push(byte[] chunk) => SysExChunkReceived?.Invoke(this, new MidiSysExChunkEventArgs(chunk, false));

    public void PushShort(uint message) => ShortMessageReceived?.Invoke(this, new MidiShortMessageEventArgs(message));

    public void PushError(string message) => ErrorOccurred?.Invoke(this, new MidiErrorEventArgs(message));

    public void Dispose() => Disposed = true;
}

internal sealed class FakeOutputPort : IMidiOutputPort
{
    public FakeOutputPort(MidiDeviceInfo device) => Device = device;

    public MidiDeviceInfo Device { get; }

    public List<byte[]> Chunks { get; } = new();

    public bool Disposed { get; private set; }

    public byte[] AllBytes => Chunks.SelectMany(c => c).ToArray();

    public List<uint> ShortMessages { get; } = new();

    public void SendRaw(byte[] buffer, int offset, int count, TimeSpan timeout) =>
        Chunks.Add(buffer.AsSpan(offset, count).ToArray());

    public void SendShort(uint message) => ShortMessages.Add(message);

    public void Dispose() => Disposed = true;
}
