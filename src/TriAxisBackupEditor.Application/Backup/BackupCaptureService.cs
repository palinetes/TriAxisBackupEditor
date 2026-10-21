using System.Diagnostics;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.SysEx;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.Application.Backup;

public sealed record CaptureOptions(
    MidiDeviceInfo Input,
    MidiDeviceInfo Output,
    TimeSpan StartTimeout,
    TimeSpan ReceiveTimeout,
    TimeSpan PostDumpIdle,
    byte[]? RequestMessage = null);

public enum CapturePhase
{
    Opening,
    WaitingForDump,
    Receiving,
    Finishing,
    Done
}

public sealed record CaptureProgress(CapturePhase Phase, long BytesReceived, int ExpectedBytes, int CompletedMessages, string Message)
{
    /// <summary>0–100 baseado no tamanho esperado pelo mapa preliminar; nunca chega a 100 antes do F7.</summary>
    public double Percent => Phase == CapturePhase.Done
        ? 100
        : ExpectedBytes <= 0 ? 0 : Math.Min(99, BytesReceived * 100.0 / ExpectedBytes);
}

public enum CaptureStatus
{
    Completed,
    Cancelled,
    NoDataReceived,
    Incomplete,
    Failed
}

public sealed class CaptureResult
{
    public required CaptureStatus Status { get; init; }

    /// <summary>Somente mensagens completas (F0...F7), concatenadas na ordem de chegada, sem alteração.</summary>
    public byte[] Data { get; init; } = Array.Empty<byte>();

    /// <summary>Tudo que chegou, inclusive a mensagem interrompida — usado apenas para o arquivo temporário.</summary>
    public byte[] RawPartialData { get; init; } = Array.Empty<byte>();

    public int MessageCount { get; init; }

    public long BytesReceived { get; init; }

    public int RealTimeBytesDiscarded { get; init; }

    public int StrayBytesDiscarded { get; init; }

    public string Message { get; init; } = string.Empty;

    public bool IsComplete => Status == CaptureStatus.Completed && Data.Length > 0;
}

/// <summary>
/// Recebe o dump do TriAxis. Os fragmentos entregues pelo driver são remontados pelo <see cref="SysExAssembler"/>;
/// o backup só é considerado completo após o F7 e um período de silêncio.
/// </summary>
public sealed class BackupCaptureService
{
    private readonly IMidiDeviceService _midi;
    private readonly ITechLog _log;

    public BackupCaptureService(IMidiDeviceService midi, ITechLog log)
    {
        _midi = midi;
        _log = log;
    }

    public async Task<CaptureResult> CaptureAsync(CaptureOptions options, IProgress<CaptureProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var assembler = new SysExAssembler();
        var completed = new List<byte[]>();
        var raw = new List<byte>();
        var sync = new object();
        var signal = new SemaphoreSlim(0);
        var clock = Stopwatch.StartNew();
        var lastActivity = TimeSpan.Zero;
        long received = 0;
        var started = false;
        var expected = TriAxisDumpLayout.TotalLength;

        void Report(CapturePhase phase, string message)
        {
            int count;
            long bytes;
            lock (sync)
            {
                count = completed.Count;
                bytes = received;
            }

            progress?.Report(new CaptureProgress(phase, bytes, expected, count, message));
        }

        IMidiInputPort? input = null;
        try
        {
            Report(CapturePhase.Opening, TrF($"Abrindo MIDI IN \"{options.Input.DisplayName}\"..."));
            _log.Info($"Backup: abrindo MIDI IN \"{options.Input.DisplayName}\" (MIDI OUT selecionado: \"{options.Output.DisplayName}\").");

            input = _midi.OpenInput(options.Input);
            input.SysExChunkReceived += (_, e) =>
            {
                long total;
                lock (sync)
                {
                    received += e.Data.Length;
                    total = received;
                    raw.AddRange(e.Data);
                    lastActivity = clock.Elapsed;
                    started = true;
                    completed.AddRange(assembler.Feed(e.Data));
                }

                _log.Info($"Backup: recebido fragmento de {e.Data.Length} bytes (total {total}){(e.DriverReportedError ? " — driver sinalizou erro" : string.Empty)}.");
                signal.Release();
            };
            input.ErrorOccurred += (_, e) => _log.Warn("MIDI IN: " + e.Message);

            if (options.RequestMessage is { Length: > 0 } request)
            {
                // Única situação em que o backup envia algo: requisição configurada explicitamente pelo usuário.
                using var output = _midi.OpenOutput(options.Output);
                _log.Info($"Backup: enviando requisição de dump ({request.Length} bytes): {HexFormatter.ToHex(request)}");
                await Task.Run(() => output.SendRaw(request, 0, request.Length, TimeSpan.FromSeconds(5)), cancellationToken).ConfigureAwait(false);
            }

            var waitingMessage = options.RequestMessage is { Length: > 0 }
                ? Tr("Requisição enviada. Aguardando o dump do TriAxis...")
                : Tr("Aguardando o dump. Pressione SHIFT + PRESET DOWN no painel do TriAxis.");
            Report(CapturePhase.WaitingForDump, waitingMessage);

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Finish(CaptureStatus.Cancelled, Tr("Backup cancelado pelo usuário."));
                }

                try
                {
                    await signal.WaitAsync(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return Finish(CaptureStatus.Cancelled, Tr("Backup cancelado pelo usuário."));
                }

                bool hasStarted, inMessage;
                int completedCount;
                TimeSpan idle;
                lock (sync)
                {
                    hasStarted = started;
                    inMessage = assembler.IsInMessage;
                    completedCount = completed.Count;
                    idle = clock.Elapsed - lastActivity;
                }

                if (!hasStarted)
                {
                    if (clock.Elapsed > options.StartTimeout)
                    {
                        return Finish(CaptureStatus.NoDataReceived,
                            TrF($"Nenhum dado recebido em {options.StartTimeout.TotalSeconds:0} s. Verifique o cabo MIDI (TriAxis MIDI OUT → interface MIDI IN) e pressione SHIFT + PRESET DOWN."));
                    }

                    continue;
                }

                if (completedCount > 0 && !inMessage)
                {
                    Report(CapturePhase.Finishing, Tr("F7 recebido. Confirmando fim do dump..."));
                    if (idle >= options.PostDumpIdle)
                    {
                        return Finish(CaptureStatus.Completed, Tr("Dump recebido com sucesso."));
                    }

                    continue;
                }

                Report(CapturePhase.Receiving, Tr("Recebendo dump..."));
                if (idle > options.ReceiveTimeout)
                {
                    return Finish(CaptureStatus.Incomplete,
                        TrF($"A recepção parou há mais de {options.ReceiveTimeout.TotalSeconds:0} s sem o byte final F7. O backup está incompleto."));
                }
            }
        }
        catch (OperationCanceledException)
        {
            return Finish(CaptureStatus.Cancelled, Tr("Backup cancelado pelo usuário."));
        }
        catch (MidiException ex)
        {
            _log.Error("Backup: falha MIDI.", ex);
            return Finish(CaptureStatus.Failed, ex.Message);
        }
        finally
        {
            input?.Dispose();
            _log.Info("Backup: MIDI IN fechado.");
        }

        CaptureResult Finish(CaptureStatus status, string message)
        {
            byte[] data;
            byte[] partial;
            int count;
            long bytes;
            lock (sync)
            {
                data = completed.SelectMany(m => m).ToArray();
                partial = raw.ToArray();
                count = completed.Count;
                bytes = received;
            }

            var result = new CaptureResult
            {
                Status = status,
                Data = status == CaptureStatus.Completed ? data : Array.Empty<byte>(),
                RawPartialData = status == CaptureStatus.Completed ? Array.Empty<byte>() : partial,
                MessageCount = count,
                BytesReceived = bytes,
                RealTimeBytesDiscarded = assembler.RealTimeBytesDiscarded,
                StrayBytesDiscarded = assembler.StrayBytesDiscarded,
                Message = message
            };

            _log.Info($"Backup: fim ({status}). {message} Bytes recebidos: {bytes}; mensagens completas: {count}; " +
                      TrF($"tempo real descartado: {result.RealTimeBytesDiscarded}; bytes fora de SysEx: {result.StrayBytesDiscarded}."));
            if (status == CaptureStatus.Completed)
            {
                _log.Info($"Backup: SHA-256 dos dados recebidos = {SysExHash.Sha256Hex(data)}");
            }

            progress?.Report(new CaptureProgress(CapturePhase.Done, bytes, expected, count, message));
            return result;
        }
    }
}
