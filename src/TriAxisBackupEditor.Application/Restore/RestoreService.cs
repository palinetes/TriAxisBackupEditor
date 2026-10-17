using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Application.Restore;

/// <summary>Arquivo lido e validado, pronto para a confirmação do usuário.</summary>
public sealed class RestorePlan
{
    internal RestorePlan(string filePath, byte[] data, string sha256, SysExValidationResult validation)
    {
        FilePath = filePath;
        Data = data;
        Sha256 = sha256;
        Validation = validation;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public byte[] Data { get; }

    public string Sha256 { get; }

    public SysExValidationResult Validation { get; }

    public bool CanRestore => Validation.IsTransmittable;
}

/// <summary>
/// Confirmação em duas etapas:
/// 1) marcar que entende que a memória atual do TriAxis será substituída;
/// 2) digitar a palavra RESTAURAR.
/// Se o arquivo tiver avisos (cabeçalho/tamanho fora do mapa), é preciso também reconhecê-los.
/// </summary>
public sealed class RestoreConfirmation
{
    public const string RequiredPhrase = "RESTAURAR";

    public bool OverwriteAcknowledged { get; init; }

    public string TypedPhrase { get; init; } = string.Empty;

    public bool WarningsAcknowledged { get; init; }

    public bool IsCompleteFor(SysExValidationResult validation) =>
        OverwriteAcknowledged
        && string.Equals(TypedPhrase?.Trim(), RequiredPhrase, StringComparison.Ordinal)
        && (!validation.HasWarnings || WarningsAcknowledged);
}

public sealed class RestoreBlockedException : InvalidOperationException
{
    public RestoreBlockedException(string message)
        : base(message)
    {
    }
}

public sealed record RestoreResult(TransmitStatus Status, int BytesSent, string Message);

public sealed class RestoreService
{
    private readonly IMidiDeviceService _midi;
    private readonly ITriAxisSysExService _sysEx;
    private readonly ITechLog _log;

    public RestoreService(IMidiDeviceService midi, ITriAxisSysExService sysEx, ITechLog log)
    {
        _midi = midi;
        _sysEx = sysEx;
        _log = log;
    }

    public RestorePlan Prepare(string filePath)
    {
        var full = Path.GetFullPath(filePath);
        var data = File.ReadAllBytes(full);
        var validation = _sysEx.Validate(data);
        var hash = _sysEx.ComputeSha256(data);
        _log.Info($"Restauração: arquivo \"{full}\" validado ({data.Length} bytes, SHA-256 {hash}). {validation.Summary}");
        foreach (var issue in validation.Issues)
        {
            _log.Warn("Restauração: " + issue);
        }

        return new RestorePlan(full, data, hash, validation);
    }

    /// <summary>
    /// Envia o arquivo ao TriAxis. Bloqueia (exceção) se a confirmação estiver incompleta, se o arquivo for inválido
    /// ou se ele tiver mudado no disco desde a validação.
    /// </summary>
    public async Task<RestoreResult> RestoreAsync(
        RestorePlan plan,
        RestoreConfirmation? confirmation,
        MidiDeviceInfo? output,
        TransmitOptions options,
        IProgress<TransmitProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (confirmation is null || !confirmation.IsCompleteFor(plan.Validation))
        {
            _log.Warn("Restauração bloqueada: confirmação em duas etapas não concluída.");
            throw new RestoreBlockedException("Restauração bloqueada: a confirmação em duas etapas não foi concluída.");
        }

        if (!plan.CanRestore)
        {
            _log.Warn("Restauração bloqueada: arquivo inválido.");
            throw new RestoreBlockedException("Restauração bloqueada: o arquivo não passou na validação SysEx.");
        }

        if (output is null)
        {
            throw new RestoreBlockedException("Selecione a porta MIDI OUT conectada ao MIDI IN do TriAxis.");
        }

        // Relê o arquivo: o que vai para o TriAxis é exatamente o que foi validado e confirmado.
        var current = File.ReadAllBytes(plan.FilePath);
        var currentHash = _sysEx.ComputeSha256(current);
        if (!string.Equals(currentHash, plan.Sha256, StringComparison.Ordinal))
        {
            _log.Warn($"Restauração bloqueada: \"{plan.FilePath}\" mudou após a validação (SHA-256 {currentHash}).");
            throw new RestoreBlockedException("Restauração bloqueada: o arquivo foi alterado depois da validação. Selecione-o novamente.");
        }

        var revalidation = _sysEx.Validate(current);
        if (!revalidation.IsTransmittable)
        {
            throw new RestoreBlockedException("Restauração bloqueada: o arquivo não passou na validação SysEx.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new RestoreResult(TransmitStatus.CancelledBeforeStart, 0, "Restauração cancelada. Nada foi enviado.");
        }

        _log.Info($"Restauração: abrindo MIDI OUT \"{output.DisplayName}\". Enviando \"{plan.FilePath}\" ({current.Length} bytes, SHA-256 {currentHash}).");
        var sent = 0;
        using var port = _midi.OpenOutput(output);
        var status = await Task.Run(
            () => SysExTransmitter.SendAsync(port, current, revalidation.Messages, options, new SyncProgress(p =>
            {
                Interlocked.Exchange(ref sent, p.BytesSent);
                progress?.Report(p);
            }), _log, cancellationToken),
            CancellationToken.None).ConfigureAwait(false);

        var message = status switch
        {
            TransmitStatus.Completed => "Envio concluído. Confira se o display do TriAxis piscou, confirmando o recebimento.",
            TransmitStatus.CancelledBeforeStart => "Restauração cancelada. Nada foi enviado.",
            _ => "Restauração interrompida entre mensagens. Confira o TriAxis e repita a restauração completa."
        };

        _log.Info($"Restauração: {status}. {sent} de {current.Length} bytes enviados.");
        return new RestoreResult(status, sent, message);
    }

    /// <summary>IProgress síncrono (o Progress&lt;T&gt; padrão posta no contexto e chegaria fora de ordem).</summary>
    private sealed class SyncProgress : IProgress<TransmitProgress>
    {
        private readonly Action<TransmitProgress> _handler;

        public SyncProgress(Action<TransmitProgress> handler) => _handler = handler;

        public void Report(TransmitProgress value) => _handler(value);
    }
}
