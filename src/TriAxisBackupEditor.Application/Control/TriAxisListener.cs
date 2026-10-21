using TriAxisBackupEditor.Midi;

namespace TriAxisBackupEditor.Application.Control;

/// <summary>
/// Escuta o MIDI OUT do TriAxis até ele anunciar um Program Change. Segundo o manual, o TriAxis manda um
/// Program Change quando as teclas de PROGRAM do painel são usadas: assim se descobre, só lendo, em que canal
/// ele transmite (e recebe) e qual número de programa vai pelo cabo. Nada é enviado ao TriAxis.
/// </summary>
public sealed class TriAxisListener
{
    private readonly IMidiDeviceService _midi;

    public TriAxisListener(IMidiDeviceService midi) => _midi = midi;

    /// <summary>
    /// Espera o primeiro Program Change. Devolve null se o tempo acabar ou se for cancelado.
    /// Lança <see cref="MidiException"/> se a porta não puder ser aberta.
    /// </summary>
    public async Task<ShortMessage?> ListenForProgramChangeAsync(MidiDeviceInfo input, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var heard = new TaskCompletionSource<ShortMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var port = _midi.OpenInput(input);

        void OnMessage(object? sender, MidiShortMessageEventArgs e)
        {
            var message = MidiMessages.Decode(e.Message);
            if (message.Kind == MidiMessageKind.ProgramChange)
            {
                heard.TrySetResult(message);
            }
        }

        port.ShortMessageReceived += OnMessage;
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            using var registration = timeoutSource.Token.Register(() => heard.TrySetCanceled());
            try
            {
                return await heard.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
        finally
        {
            port.ShortMessageReceived -= OnMessage;
        }
    }
}
