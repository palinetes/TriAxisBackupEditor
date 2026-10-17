using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Midi;

namespace TriAxisBackupEditor.Application.Connection;

public sealed class ConnectionTestResult
{
    public bool InputOk { get; init; }

    public bool OutputOk { get; init; }

    public string? InputError { get; init; }

    public string? OutputError { get; init; }

    public int ShortMessages { get; init; }

    public long SysExBytes { get; init; }

    public string LastShortMessage { get; init; } = string.Empty;

    public bool Success => InputOk && OutputOk;
}

/// <summary>
/// Teste de conexão passivo: abre as portas e escuta a entrada por alguns segundos.
/// Não envia nenhum byte ao TriAxis.
/// </summary>
public sealed class ConnectionTestService
{
    private readonly IMidiDeviceService _midi;
    private readonly ITechLog _log;

    public ConnectionTestService(IMidiDeviceService midi, ITechLog log)
    {
        _midi = midi;
        _log = log;
    }

    public async Task<ConnectionTestResult> TestAsync(MidiDeviceInfo? input, MidiDeviceInfo? output, TimeSpan listenTime, CancellationToken cancellationToken)
    {
        string? outputError = null;
        var outputOk = false;
        if (output is null)
        {
            outputError = "Nenhuma porta MIDI OUT selecionada.";
        }
        else
        {
            try
            {
                using var port = _midi.OpenOutput(output);
                outputOk = true;
                _log.Info($"Teste: MIDI OUT \"{output.DisplayName}\" aberto com sucesso (nada foi enviado).");
            }
            catch (MidiException ex)
            {
                outputError = ex.Message;
                _log.Error("Teste: falha no MIDI OUT.", ex);
            }
        }

        string? inputError = null;
        var inputOk = false;
        var shortCount = 0;
        long sysExBytes = 0;
        var lastShort = string.Empty;

        if (input is null)
        {
            inputError = "Nenhuma porta MIDI IN selecionada.";
        }
        else
        {
            try
            {
                using var port = _midi.OpenInput(input);
                inputOk = true;
                port.ShortMessageReceived += (_, e) =>
                {
                    Interlocked.Increment(ref shortCount);
                    lastShort = e.ToString();
                };
                port.SysExChunkReceived += (_, e) => Interlocked.Add(ref sysExBytes, e.Data.Length);
                _log.Info($"Teste: MIDI IN \"{input.DisplayName}\" aberto. Escutando por {listenTime.TotalSeconds:0} s...");

                try
                {
                    await Task.Delay(listenTime, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Cancelar só encurta a escuta.
                }
            }
            catch (MidiException ex)
            {
                inputError = ex.Message;
                _log.Error("Teste: falha no MIDI IN.", ex);
            }
        }

        _log.Info($"Teste concluído: IN={(inputOk ? "OK" : "falha")}, OUT={(outputOk ? "OK" : "falha")}, mensagens curtas={shortCount}, bytes SysEx={sysExBytes}.");
        return new ConnectionTestResult
        {
            InputOk = inputOk,
            OutputOk = outputOk,
            InputError = inputError,
            OutputError = outputError,
            ShortMessages = shortCount,
            SysExBytes = Interlocked.Read(ref sysExBytes),
            LastShortMessage = lastShort
        };
    }
}
