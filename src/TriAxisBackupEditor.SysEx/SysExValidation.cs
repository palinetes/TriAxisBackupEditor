namespace TriAxisBackupEditor.SysEx;

public enum IssueSeverity
{
    Warning,
    Error
}

/// <summary>Um problema encontrado na validação. Erros bloqueiam a transmissão; avisos exigem confirmação extra.</summary>
public sealed record SysExIssue(IssueSeverity Severity, string Code, string Message, int? Offset = null)
{
    public override string ToString()
    {
        var prefix = Severity == IssueSeverity.Error ? "ERRO" : "AVISO";
        return Offset is int o ? $"{prefix} [{Code}] @0x{o:X4}: {Message}" : $"{prefix} [{Code}]: {Message}";
    }
}

/// <summary>Posição de uma mensagem F0 ... F7 completa dentro de um buffer.</summary>
public readonly record struct SysExMessageSpan(int Offset, int Length);

public sealed class SysExValidationResult
{
    public SysExValidationResult(int totalLength, IReadOnlyList<SysExMessageSpan> messages, IReadOnlyList<SysExIssue> issues, bool matchesTriAxisLayout)
    {
        TotalLength = totalLength;
        Messages = messages;
        Issues = issues;
        MatchesTriAxisLayout = matchesTriAxisLayout;
    }

    public int TotalLength { get; }

    public IReadOnlyList<SysExMessageSpan> Messages { get; }

    public IReadOnlyList<SysExIssue> Issues { get; }

    /// <summary>Verdadeiro quando o arquivo bate exatamente com o mapa preliminar (cabeçalho e tamanho).</summary>
    public bool MatchesTriAxisLayout { get; }

    public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);

    public bool HasWarnings => Issues.Any(i => i.Severity == IssueSeverity.Warning);

    /// <summary>Somente dados sem erros estruturais e com pelo menos uma mensagem completa podem ser enviados.</summary>
    public bool IsTransmittable => !HasErrors && Messages.Count > 0;

    public string Summary
    {
        get
        {
            if (HasErrors)
            {
                return "Arquivo inválido: o envio está bloqueado.";
            }

            if (HasWarnings)
            {
                return "Estrutura SysEx válida, com avisos.";
            }

            return MatchesTriAxisLayout
                ? "Dump SysEx válido e compatível com o mapa do TriAxis."
                : "Estrutura SysEx válida.";
        }
    }
}
