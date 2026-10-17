namespace TriAxisBackupEditor.SysEx;

/// <summary>
/// Mapa PRELIMINAR do dump do TriAxis.
/// Fonte: editor web markphilpot/triaxiseditor (js/triaxis.js, GPL-3.0), também publicado em triaxiseditor.com.
/// Os valores abaixo NÃO foram validados contra um TriAxis real por este projeto: use somente para
/// visualização (leitura). O editor de parâmetros fica desabilitado até a validação descrita em docs/SYSEX.md.
/// </summary>
public static class TriAxisDumpLayout
{
    public const bool IsPreliminary = true;

    public const string Source = "markphilpot/triaxiseditor (js/triaxis.js) — não validado em hardware";

    /// <summary>F0 + 00 00 4A + 02, conforme o editor de referência.</summary>
    public static ReadOnlySpan<byte> ExpectedHeader => new byte[] { 0xF0, 0x00, 0x00, 0x4A, 0x02 };

    public const int HeaderLength = 5;

    public const int PresetCount = 90;
    public const int PresetLength = 11;
    public const int PresetsOffset = HeaderLength;

    public const int ProgramCount = 128;
    public const int ProgramsOffset = PresetsOffset + (PresetCount * PresetLength);

    /// <summary>Região de "controladores" (FX loop / switching / MIDI). O editor de referência a trata como
    /// bloco opaco ("Placeholder") e apenas a preserva.</summary>
    public const int ControllersOffset = ProgramsOffset + ProgramCount;
    public const int ControllersLength = 1622;

    public const int TerminatorOffset = ControllersOffset + ControllersLength;
    public const int TotalLength = TerminatorOffset + 1;

    /// <summary>Nomes dos 11 bytes de cada preset, na ordem do editor de referência.</summary>
    public static IReadOnlyList<string> PresetParameterNames { get; } = new[]
    {
        "Dynamic Voice",
        "Presence",
        "Master",
        "Lead 2 Drive",
        "Lead 1 Drive",
        "Bass",
        "Middle",
        "Treble",
        "Gain",
        "Modo (LEDs)",
        "FX Loop / Switches"
    };

    public static bool HasExpectedHeader(ReadOnlySpan<byte> data) =>
        data.Length >= HeaderLength && data[..HeaderLength].SequenceEqual(ExpectedHeader);

    /// <summary>Descreve em qual região do mapa preliminar um offset cai. Usado na comparação de dumps.</summary>
    public static string DescribeOffset(int offset)
    {
        if (offset < 0 || offset >= TotalLength)
        {
            return "fora do mapa";
        }

        if (offset < HeaderLength)
        {
            return $"Cabeçalho byte {offset}";
        }

        if (offset < ProgramsOffset)
        {
            var relative = offset - PresetsOffset;
            var preset = (relative / PresetLength) + 1;
            var parameter = relative % PresetLength;
            return $"Preset {preset:00} · {PresetParameterNames[parameter]}";
        }

        if (offset < ControllersOffset)
        {
            return $"Programa MIDI {offset - ProgramsOffset + 1:000} → preset";
        }

        if (offset < TerminatorOffset)
        {
            return $"Controladores +{offset - ControllersOffset}";
        }

        return "Terminador F7";
    }
}
