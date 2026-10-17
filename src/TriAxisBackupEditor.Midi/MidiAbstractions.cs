namespace TriAxisBackupEditor.Midi;

public enum MidiDirection
{
    Input,
    Output
}

/// <summary>
/// Identifica uma porta MIDI. O índice do Windows muda quando interfaces são plugadas/desplugadas,
/// por isso a porta é reaberta pelo par (Nome, Ocorrência) — Ocorrência distingue portas com o mesmo nome.
/// </summary>
public sealed record MidiDeviceInfo(MidiDirection Direction, int Index, string Name, int Occurrence)
{
    public string DisplayName => Occurrence == 0 ? Name : $"{Name} ({Occurrence + 1})";

    /// <summary>Chave estável para salvar a seleção nas configurações.</summary>
    public string Key => $"{Name}#{Occurrence}";

    public bool SameDeviceAs(MidiDeviceInfo? other) =>
        other is not null && other.Direction == Direction && other.Key == Key;

    public override string ToString() => DisplayName;
}

public sealed class MidiSysExChunkEventArgs : EventArgs
{
    public MidiSysExChunkEventArgs(byte[] data, bool driverReportedError)
    {
        Data = data;
        DriverReportedError = driverReportedError;
    }

    /// <summary>Bytes crus entregues pelo driver. Pode ser um fragmento de uma mensagem maior.</summary>
    public byte[] Data { get; }

    /// <summary>MIM_LONGERROR: o driver sinalizou SysEx inválido/incompleto.</summary>
    public bool DriverReportedError { get; }
}

public sealed class MidiShortMessageEventArgs : EventArgs
{
    public MidiShortMessageEventArgs(uint message) => Message = message;

    public uint Message { get; }

    public byte Status => (byte)(Message & 0xFF);

    public override string ToString() =>
        $"{Message & 0xFF:X2} {(Message >> 8) & 0xFF:X2} {(Message >> 16) & 0xFF:X2}";
}

public sealed class MidiErrorEventArgs : EventArgs
{
    public MidiErrorEventArgs(string message) => Message = message;

    public string Message { get; }
}

public class MidiException : Exception
{
    public MidiException(string message, int? resultCode = null, Exception? inner = null)
        : base(message, inner)
    {
        ResultCode = resultCode;
    }

    public int? ResultCode { get; }
}

/// <summary>Enumeração e abertura de portas MIDI. Abrir uma porta nunca envia bytes ao equipamento.</summary>
public interface IMidiDeviceService
{
    IReadOnlyList<MidiDeviceInfo> GetInputDevices();

    IReadOnlyList<MidiDeviceInfo> GetOutputDevices();

    /// <summary>Abre e inicia a recepção. Lança <see cref="MidiException"/> com mensagem acionável.</summary>
    IMidiInputPort OpenInput(MidiDeviceInfo device);

    IMidiOutputPort OpenOutput(MidiDeviceInfo device);
}

public interface IMidiInputPort : IDisposable
{
    MidiDeviceInfo Device { get; }

    /// <summary>Disparado em thread de fundo, na ordem de chegada.</summary>
    event EventHandler<MidiSysExChunkEventArgs>? SysExChunkReceived;

    event EventHandler<MidiShortMessageEventArgs>? ShortMessageReceived;

    event EventHandler<MidiErrorEventArgs>? ErrorOccurred;
}

public interface IMidiOutputPort : IDisposable
{
    MidiDeviceInfo Device { get; }

    /// <summary>
    /// Envia bytes crus como mensagem longa (midiOutLongMsg) e só retorna quando o driver concluiu o envio.
    /// Pode ser um trecho de um SysEx maior: a divisão em blocos é responsabilidade do chamador.
    /// </summary>
    void SendRaw(byte[] buffer, int offset, int count, TimeSpan timeout);

    /// <summary>Envia uma mensagem curta (Program Change, Control Change...) empacotada como no winmm:
    /// status no byte 0, dado 1 no byte 1, dado 2 no byte 2.</summary>
    void SendShort(uint message);
}
