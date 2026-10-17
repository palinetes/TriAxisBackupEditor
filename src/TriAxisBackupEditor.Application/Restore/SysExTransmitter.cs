using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Application.Restore;

public sealed record TransmitOptions(int ChunkSize, int ChunkDelayMs)
{
    public TimeSpan ChunkTimeout { get; init; } = TimeSpan.FromSeconds(10);
}

public sealed record TransmitProgress(int BytesSent, int TotalBytes, int MessageIndex, int MessageCount)
{
    public double Percent => TotalBytes == 0 ? 0 : BytesSent * 100.0 / TotalBytes;
}

public enum TransmitStatus
{
    Completed,
    CancelledBeforeStart,
    CancelledBetweenMessages
}

/// <summary>
/// Envia SysEx em blocos com intervalo entre eles, para não estourar o buffer de interfaces antigas.
/// Uma mensagem iniciada é sempre enviada até o F7: cancelar no meio deixaria o TriAxis com dados parciais.
/// O cancelamento vale antes do início e entre mensagens.
/// </summary>
public static class SysExTransmitter
{
    public static async Task<TransmitStatus> SendAsync(
        IMidiOutputPort port,
        byte[] data,
        IReadOnlyList<SysExMessageSpan> messages,
        TransmitOptions options,
        IProgress<TransmitProgress>? progress,
        ITechLog log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(data);
        if (options.ChunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "O tamanho do bloco deve ser positivo.");
        }

        var total = messages.Sum(m => m.Length);
        var sent = 0;

        for (var m = 0; m < messages.Count; m++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                log.Warn($"Restauração cancelada {(m == 0 ? "antes do envio (nada foi enviado)" : $"após {m} de {messages.Count} mensagens completas")}.");
                return m == 0 ? TransmitStatus.CancelledBeforeStart : TransmitStatus.CancelledBetweenMessages;
            }

            var span = messages[m];
            var chunks = 0;
            for (var offset = span.Offset; offset < span.Offset + span.Length; offset += options.ChunkSize)
            {
                var count = Math.Min(options.ChunkSize, span.Offset + span.Length - offset);
                port.SendRaw(data, offset, count, options.ChunkTimeout);
                sent += count;
                chunks++;
                progress?.Report(new TransmitProgress(sent, total, m + 1, messages.Count));

                if (sent < total)
                {
                    // Intencionalmente sem CancellationToken: a mensagem em andamento termina.
                    await Task.Delay(options.ChunkDelayMs, CancellationToken.None).ConfigureAwait(false);
                }
            }

            log.Info($"Restauração: mensagem {m + 1}/{messages.Count} enviada ({span.Length} bytes em {chunks} blocos de até {options.ChunkSize} bytes, intervalo {options.ChunkDelayMs} ms).");
        }

        return TransmitStatus.Completed;
    }
}
