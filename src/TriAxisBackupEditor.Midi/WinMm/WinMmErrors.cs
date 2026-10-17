using System.Text;

namespace TriAxisBackupEditor.Midi.WinMm;

internal static class WinMmErrors
{
    public static void Check(int result, string operation, MidiDeviceInfo device)
    {
        if (result != NativeMethods.MMSYSERR_NOERROR)
        {
            throw Create(result, operation, device);
        }
    }

    public static MidiException Create(int result, string operation, MidiDeviceInfo device) =>
        new($"Falha ao {operation} \"{device.DisplayName}\": {Describe(result, device.Direction)}", result);

    public static string Describe(int result, MidiDirection direction)
    {
        var friendly = result switch
        {
            NativeMethods.MMSYSERR_ALLOCATED =>
                "a porta já está em uso por outro programa. Feche DAWs, MIDI-OX, editores de G-Major etc. e tente de novo.",
            NativeMethods.MMSYSERR_BADDEVICEID =>
                "a porta não existe mais. A interface foi desconectada? Clique em \"Atualizar dispositivos\".",
            NativeMethods.MIDIERR_NODEVICE =>
                "a porta não foi encontrada. A interface foi desconectada? Reconecte e clique em \"Atualizar dispositivos\".",
            NativeMethods.MMSYSERR_NODRIVER =>
                "o driver da interface não está instalado ou parou de responder. Reinstale o driver ou troque de porta USB.",
            NativeMethods.MMSYSERR_NOMEM =>
                "memória insuficiente no driver MIDI.",
            NativeMethods.MMSYSERR_INVALHANDLE =>
                "a porta foi fechada inesperadamente (possível desconexão).",
            _ => null
        };

        var system = SystemText(result, direction);
        return friendly is null ? $"{system} (código {result})" : $"{friendly} (código {result}: {system})";
    }

    private static string SystemText(int result, MidiDirection direction)
    {
        var sb = new StringBuilder(256);
        var r = direction == MidiDirection.Input
            ? NativeMethods.midiInGetErrorText(result, sb, (uint)sb.Capacity)
            : NativeMethods.midiOutGetErrorText(result, sb, (uint)sb.Capacity);
        return r == NativeMethods.MMSYSERR_NOERROR && sb.Length > 0 ? sb.ToString() : "erro desconhecido";
    }
}
