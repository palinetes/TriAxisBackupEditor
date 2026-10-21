using static TriAxisBackupEditor.Localization.Localizer;
namespace TriAxisBackupEditor.Application.Control;

public enum MidiMessageKind
{
    Other,
    NoteOff,
    NoteOn,
    ControlChange,
    ProgramChange
}

/// <summary>Mensagem MIDI curta decodificada. Canal 1–16; Number/Value 0–127.</summary>
public readonly record struct ShortMessage(MidiMessageKind Kind, int Channel, int Number, int Value)
{
    public override string ToString() => Kind switch
    {
        MidiMessageKind.ProgramChange => TrF($"Program Change {Number + 1} (canal {Channel})"),
        MidiMessageKind.ControlChange => TrF($"CC {Number} = {Value} (canal {Channel})"),
        MidiMessageKind.NoteOn => TrF($"Note On {Number} vel {Value} (canal {Channel})"),
        MidiMessageKind.NoteOff => TrF($"Note Off {Number} (canal {Channel})"),
        _ => Tr("Outra mensagem")
    };
}

/// <summary>Monta e decodifica mensagens MIDI curtas no formato do winmm (status no byte menos significativo).</summary>
public static class MidiMessages
{
    /// <param name="channel">1–16</param>
    /// <param name="program">1–128 (como aparece no TriAxis e na pedaleira)</param>
    public static uint ProgramChange(int channel, int program)
    {
        CheckChannel(channel);
        if (program is < 1 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(program), "Programa deve estar entre 1 e 128.");
        }

        return (uint)(0xC0 | (channel - 1)) | ((uint)(program - 1) << 8);
    }

    /// <param name="channel">1–16</param>
    /// <param name="controller">0–127</param>
    /// <param name="value">0–127</param>
    public static uint ControlChange(int channel, int controller, int value)
    {
        CheckChannel(channel);
        if (controller is < 0 or > 127)
        {
            throw new ArgumentOutOfRangeException(nameof(controller), "CC deve estar entre 0 e 127.");
        }

        if (value is < 0 or > 127)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Valor deve estar entre 0 e 127.");
        }

        return (uint)(0xB0 | (channel - 1)) | ((uint)controller << 8) | ((uint)value << 16);
    }

    public static ShortMessage Decode(uint message)
    {
        var status = (int)(message & 0xFF);
        var data1 = (int)((message >> 8) & 0x7F);
        var data2 = (int)((message >> 16) & 0x7F);
        var channel = (status & 0x0F) + 1;

        return (status & 0xF0) switch
        {
            0x80 => new ShortMessage(MidiMessageKind.NoteOff, channel, data1, data2),
            0x90 when data2 == 0 => new ShortMessage(MidiMessageKind.NoteOff, channel, data1, 0),
            0x90 => new ShortMessage(MidiMessageKind.NoteOn, channel, data1, data2),
            0xB0 => new ShortMessage(MidiMessageKind.ControlChange, channel, data1, data2),
            0xC0 => new ShortMessage(MidiMessageKind.ProgramChange, channel, data1, 0),
            _ => new ShortMessage(MidiMessageKind.Other, channel, data1, data2)
        };
    }

    private static void CheckChannel(int channel)
    {
        if (channel is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), "Canal MIDI deve estar entre 1 e 16.");
        }
    }
}
