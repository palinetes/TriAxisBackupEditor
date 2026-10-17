using TriAxisBackupEditor.Storage;

namespace TriAxisBackupEditor.Application.Restore;

public enum SafeReplaceStep
{
    /// <summary>Mostrando o aviso "isto troca os timbres do seu TriAxis".</summary>
    Warning,

    /// <summary>Aguardando o backup de segurança (obrigatório).</summary>
    SafetyBackup,

    /// <summary>Backup de segurança salvo: o botão "Sim, substituir" pode ser liberado.</summary>
    ReadyToReplace
}

/// <summary>
/// Confirmação em duas etapas do modo simples: (1) aceitar o aviso, (2) fazer o backup de segurança do estado atual.
/// Só depois disso a confirmação para o <see cref="RestoreService"/> é montada — e o serviço continua revalidando tudo.
/// Arquivos fora do formato esperado do TriAxis não entram neste fluxo (ficam para o modo avançado).
/// </summary>
public sealed class SafeReplaceFlow
{
    public SafeReplaceFlow(RestorePlan plan)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
    }

    public RestorePlan Plan { get; }

    public SafeReplaceStep Step { get; private set; } = SafeReplaceStep.Warning;

    public BackupRecord? SafetyBackup { get; private set; }

    /// <summary>No modo simples só passam arquivos íntegros e no formato do TriAxis (sem avisos).</summary>
    public bool IsAllowed => Plan.CanRestore && !Plan.Validation.HasWarnings && Plan.Validation.MatchesTriAxisLayout;

    public string? BlockReason => IsAllowed
        ? null
        : !Plan.CanRestore
            ? "Este arquivo está danificado ou incompleto e não pode ser enviado."
            : "Este arquivo não parece um backup completo do TriAxis. Se tiver certeza, use o modo avançado.";

    public void AcceptWarning()
    {
        EnsureAllowed();
        if (Step == SafeReplaceStep.Warning)
        {
            Step = SafeReplaceStep.SafetyBackup;
        }
    }

    public void RegisterSafetyBackup(BackupRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureAllowed();
        if (Step != SafeReplaceStep.SafetyBackup)
        {
            throw new InvalidOperationException("Aceite o aviso antes de fazer o backup de segurança.");
        }

        if (!File.Exists(record.FilePath))
        {
            throw new InvalidOperationException("O backup de segurança não foi encontrado no disco.");
        }

        SafetyBackup = record;
        Step = SafeReplaceStep.ReadyToReplace;
    }

    /// <summary>Confirmação completa — só existe depois do aviso aceito e do backup de segurança salvo.</summary>
    public RestoreConfirmation CreateConfirmation()
    {
        EnsureAllowed();
        if (Step != SafeReplaceStep.ReadyToReplace)
        {
            throw new RestoreBlockedException("Envio bloqueado: faça primeiro o backup de segurança do estado atual do TriAxis.");
        }

        return new RestoreConfirmation
        {
            OverwriteAcknowledged = true,
            TypedPhrase = RestoreConfirmation.RequiredPhrase,
            WarningsAcknowledged = false
        };
    }

    private void EnsureAllowed()
    {
        if (!IsAllowed)
        {
            throw new RestoreBlockedException(BlockReason!);
        }
    }
}
