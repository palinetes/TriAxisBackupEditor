namespace TriAxisBackupEditor.SysEx;

/// <summary>
/// Valida um buffer (arquivo .syx ou captura) antes de qualquer transmissão.
/// Erros = estrutura quebrada (bloqueia). Avisos = estrutura íntegra, mas diferente do mapa esperado.
/// </summary>
public static class SysExValidator
{
    private const int MaxIssuesPerCode = 10;

    public static SysExValidationResult Validate(ReadOnlySpan<byte> data)
    {
        var issues = new List<SysExIssue>();
        var messages = new List<SysExMessageSpan>();
        var counts = new Dictionary<string, int>();

        void Add(IssueSeverity severity, string code, string message, int? offset = null)
        {
            counts.TryGetValue(code, out var n);
            counts[code] = n + 1;
            if (n < MaxIssuesPerCode)
            {
                issues.Add(new SysExIssue(severity, code, message, offset));
            }
            else if (n == MaxIssuesPerCode)
            {
                issues.Add(new SysExIssue(severity, code, "Mais ocorrências omitidas."));
            }
        }

        if (data.Length == 0)
        {
            Add(IssueSeverity.Error, "VAZIO", "O arquivo está vazio (0 bytes).");
            return new SysExValidationResult(0, messages, issues, false);
        }

        if (data[0] != SysExConstants.Start)
        {
            Add(IssueSeverity.Error, "SEM_F0", $"O arquivo não começa com F0 (primeiro byte: {data[0]:X2}).", 0);
        }

        if (data[^1] != SysExConstants.End)
        {
            Add(IssueSeverity.Error, "SEM_F7", $"O arquivo não termina com F7 (último byte: {data[^1]:X2}). O dump provavelmente está incompleto.", data.Length - 1);
        }

        var inMessage = false;
        var messageStart = 0;

        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];

            if (!inMessage)
            {
                if (b == SysExConstants.Start)
                {
                    inMessage = true;
                    messageStart = i;
                }
                else if (i != 0)
                {
                    // O caso i == 0 já foi reportado como SEM_F0.
                    Add(IssueSeverity.Error, "FORA_SYSEX", $"Byte {b:X2} fora de uma mensagem SysEx.", i);
                }

                continue;
            }

            if (b == SysExConstants.End)
            {
                var length = i - messageStart + 1;
                if (length <= 2)
                {
                    Add(IssueSeverity.Error, "MSG_VAZIA", "Mensagem SysEx sem conteúdo (F0 F7).", messageStart);
                }
                else
                {
                    messages.Add(new SysExMessageSpan(messageStart, length));
                }

                inMessage = false;
            }
            else if (b == SysExConstants.Start)
            {
                Add(IssueSeverity.Error, "F0_SEM_F7", "Nova mensagem F0 antes do F7 da anterior: mensagem truncada.", i);
                messageStart = i;
            }
            else if (SysExConstants.IsRealTime(b))
            {
                Add(IssueSeverity.Error, "TEMPO_REAL", $"Byte de tempo real {b:X2} dentro do SysEx. Arquivo .syx não deveria conter isso.", i);
            }
            else if (SysExConstants.IsStatus(b))
            {
                Add(IssueSeverity.Error, "STATUS_INVALIDO", $"Byte de status {b:X2} dentro do SysEx (dados devem ser 00–7F).", i);
            }
        }

        if (inMessage && data[^1] == SysExConstants.End)
        {
            // Não deve acontecer: o laço fecha a mensagem ao ver F7. Mantido como proteção.
            Add(IssueSeverity.Error, "SEM_F7", "Mensagem final sem F7.", messageStart);
        }

        var matchesLayout = false;
        if (!issues.Any(x => x.Severity == IssueSeverity.Error) && messages.Count > 0)
        {
            if (messages.Count > 1)
            {
                Add(IssueSeverity.Warning, "MULTIPLAS_MSG",
                    $"O arquivo contém {messages.Count} mensagens SysEx. O TriAxis envia uma única mensagem.");
            }

            if (!TriAxisDumpLayout.HasExpectedHeader(data))
            {
                Add(IssueSeverity.Warning, "CABECALHO",
                    $"Cabeçalho {HexFormatter.ToHex(data[..Math.Min(5, data.Length)])} diferente do esperado ({HexFormatter.ToHex(TriAxisDumpLayout.ExpectedHeader)}). Pode não ser um dump de TriAxis.");
            }

            if (data.Length != TriAxisDumpLayout.TotalLength)
            {
                Add(IssueSeverity.Warning, "TAMANHO",
                    $"Tamanho {data.Length} bytes diferente do dump de um TriAxis ({TriAxisDumpLayout.TotalLength} bytes).");
            }

            matchesLayout = messages.Count == 1
                            && data.Length == TriAxisDumpLayout.TotalLength
                            && TriAxisDumpLayout.HasExpectedHeader(data);
        }

        return new SysExValidationResult(data.Length, messages, issues, matchesLayout);
    }
}
