using static TriAxisBackupEditor.Localization.Localizer;
namespace TriAxisBackupEditor.SysEx;

/// <summary>
/// Mapa do dump do TriAxis.
/// <para>
/// CONFIRMADO em aparelho real (backup de @palinetes, 2026-10-16): o TriAxis envia UMA mensagem de
/// <b>1126 bytes</b> — cabeçalho F0 00 00 4A 02, 90 presets × 11 bytes, 130 bytes finais e F7.
/// Nos 90 presets reais, todos os bytes de modo estão na tabela conhecida, todos os bits de FX/SW
/// estão dentro de 0x1F e todos os knobs ≤ 0x0F, o que valida a estrutura dos presets.
/// </para>
/// <para>
/// O editor web de referência (markphilpot/triaxiseditor) supunha 2746 bytes, com um bloco
/// "Placeholder" de 1622 bytes; arquivos salvos por ele trazem o dump real seguido de zeros e de
/// outro F7 — veja <see cref="TriAxisDumpFile"/>.
/// </para>
/// <para>
/// AINDA NÃO CONFIRMADO: o significado dos 130 bytes finais. A referência os trata como mapa de
/// 128 programas MIDI + 2 bytes; no aparelho real eles tinham só dois valores diferentes, então o
/// programa os preserva, mas não os edita nem os usa para nomes.
/// </para>
/// </summary>
public static class TriAxisDumpLayout
{
    public const bool IsPreliminary = true;

    public const string Source = "tamanho e presets conferidos num TriAxis real; valores dos knobs vindos de markphilpot/triaxiseditor";

    /// <summary>F0 + 00 00 4A + 02.</summary>
    public static ReadOnlySpan<byte> ExpectedHeader => new byte[] { 0xF0, 0x00, 0x00, 0x4A, 0x02 };

    public const int HeaderLength = 5;

    public const int PresetCount = 90;
    public const int PresetLength = 11;
    public const int PresetsOffset = HeaderLength;

    /// <summary>Início dos 130 bytes finais (a referência os chama de mapa de programas — não confirmado).</summary>
    public const int ProgramsOffset = PresetsOffset + (PresetCount * PresetLength);
    public const int ProgramCount = 128;

    /// <summary>2 bytes depois do suposto mapa de programas (no aparelho real: 01 00). Significado desconhecido.</summary>
    public const int TrailerOffset = ProgramsOffset + ProgramCount;
    public const int TrailerLength = 2;

    public const int TerminatorOffset = TrailerOffset + TrailerLength;

    /// <summary>1126 bytes: tamanho do dump enviado por um TriAxis real.</summary>
    public const int TotalLength = TerminatorOffset + 1;

    /// <summary>Tamanho dos arquivos salvos pelo editor web de referência (dump real + zeros + F7).</summary>
    public const int WebEditorFileLength = 2746;

    /// <summary>Nomes dos 11 bytes de cada preset, na ordem do editor de referência.</summary>
    public static IReadOnlyList<string> PresetParameterNames { get; } = new[]
    {
        Tr("Dynamic Voice"),
        "Presence",
        "Master",
        Tr("Lead 2 Drive"),
        Tr("Lead 1 Drive"),
        "Bass",
        "Middle",
        "Treble",
        "Gain",
        Tr("Modo (LEDs)"),
        Tr("FX Loop / Switches")
    };

    public static bool HasExpectedHeader(ReadOnlySpan<byte> data) =>
        data.Length >= HeaderLength && data[..HeaderLength].SequenceEqual(ExpectedHeader);

    /// <summary>Descreve em qual região do mapa um offset cai. Usado na comparação de dumps.</summary>
    public static string DescribeOffset(int offset)
    {
        if (offset < 0 || offset >= TotalLength)
        {
            return Tr("fora do dump");
        }

        if (offset < HeaderLength)
        {
            return TrF($"Cabeçalho byte {offset}");
        }

        if (offset < ProgramsOffset)
        {
            var relative = offset - PresetsOffset;
            var preset = (relative / PresetLength) + 1;
            var parameter = relative % PresetLength;
            return TrF($"Preset {preset:00} · {PresetParameterNames[parameter]}");
        }

        if (offset < TrailerOffset)
        {
            return TrF($"Final +{offset - ProgramsOffset} (programa {offset - ProgramsOffset + 1:000}? não confirmado)");
        }

        if (offset < TerminatorOffset)
        {
            return TrF($"Final +{offset - ProgramsOffset} (desconhecido)");
        }

        return Tr("Terminador F7");
    }
}

/// <summary>Resultado da leitura de um arquivo .syx do TriAxis.</summary>
public sealed record TriAxisFileContent(byte[] Data, bool WasPadded, string? Note);

/// <summary>
/// Lê arquivos .syx do TriAxis aceitando o formato do editor web de referência (2746 bytes): o dump real de
/// 1126 bytes, seguido de 1619 zeros e de um F7 solto. Nesse caso, e só nesse caso exato, usa os primeiros
/// 1126 bytes. O arquivo em disco nunca é alterado.
/// </summary>
public static class TriAxisDumpFile
{
    public static TriAxisFileContent Normalize(byte[] raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (IsWebEditorPadded(raw))
        {
            return new TriAxisFileContent(
                raw[..TriAxisDumpLayout.TotalLength],
                true,
                TrF($"Arquivo salvo pelo editor web (triaxiseditor.com): {TriAxisDumpLayout.WebEditorFileLength} bytes, dos quais só os primeiros ") +
                TrF($"{TriAxisDumpLayout.TotalLength} são o dump do TriAxis (o resto são zeros). O programa usa só a parte válida."));
        }

        return new TriAxisFileContent(raw, false, null);
    }

    public static bool IsWebEditorPadded(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != TriAxisDumpLayout.WebEditorFileLength
            || raw[TriAxisDumpLayout.TerminatorOffset] != SysExConstants.End
            || raw[^1] != SysExConstants.End
            || !TriAxisDumpLayout.HasExpectedHeader(raw))
        {
            return false;
        }

        var padding = raw[TriAxisDumpLayout.TotalLength..^1];
        if (padding.IndexOfAnyExcept((byte)0) >= 0)
        {
            return false;
        }

        var dump = SysExValidator.Validate(raw[..TriAxisDumpLayout.TotalLength]);
        return !dump.HasErrors && dump.MatchesTriAxisLayout;
    }
}
