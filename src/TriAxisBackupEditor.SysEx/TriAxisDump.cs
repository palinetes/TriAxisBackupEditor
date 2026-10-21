using static TriAxisBackupEditor.Localization.Localizer;
namespace TriAxisBackupEditor.SysEx;

/// <summary>Leitura somente-leitura de um preset segundo o mapa preliminar.</summary>
public sealed record TriAxisPresetView(
    int Number,
    string Gain,
    string Treble,
    string Middle,
    string Bass,
    string Lead1Drive,
    string Lead2Drive,
    string Master,
    string Presence,
    string DynamicVoice,
    string Mode,
    bool FxLoop,
    bool Switch1,
    bool Switch2,
    bool Switch3,
    bool Switch4,
    bool HasUnknownValues,
    string RawHex);

public sealed record TriAxisProgramMapping(int Program, int PresetIndex, string Preset);

public sealed class TriAxisDump
{
    public TriAxisDump(IReadOnlyList<TriAxisPresetView> presets, IReadOnlyList<TriAxisProgramMapping> programMap, int trailerLength)
    {
        Presets = presets;
        ProgramMap = programMap;
        TrailerLength = trailerLength;
    }

    public IReadOnlyList<TriAxisPresetView> Presets { get; }

    public IReadOnlyList<TriAxisProgramMapping> ProgramMap { get; }

    public int TrailerLength { get; }

    /// <summary>
    /// O suposto mapa de programas parece real? Num TriAxis de verdade, a região tinha só 2 valores diferentes,
    /// o que não combina com um mapa programa→preset. Só usamos o mapa quando ele tem variedade suficiente.
    /// </summary>
    public bool ProgramMapLooksValid =>
        ProgramMap.Count > 0
        && ProgramMap.All(p => p.PresetIndex < TriAxisDumpLayout.PresetCount)
        && ProgramMap.Select(p => p.PresetIndex).Distinct().Count() >= 10;

    public bool IsPreliminary => TriAxisDumpLayout.IsPreliminary;
}

/// <summary>
/// Interpreta um dump segundo <see cref="TriAxisDumpLayout"/>. Somente leitura; nunca altera os bytes.
/// Valores fora das tabelas conhecidas aparecem como "? (0xNN)" em vez de serem adivinhados.
/// </summary>
public static class TriAxisDumpParser
{
    private const byte FxLoopBit = 0x01;
    private const byte KnownFxBits = 0x1F;

    public static bool TryParse(byte[] data, out TriAxisDump? dump, out string reason)
    {
        dump = null;
        ArgumentNullException.ThrowIfNull(data);

        var validation = SysExValidator.Validate(data);
        if (validation.HasErrors)
        {
            reason = Tr("A estrutura SysEx é inválida.");
            return false;
        }

        if (!validation.MatchesTriAxisLayout)
        {
            reason = TrF($"O arquivo não corresponde ao formato do TriAxis (esperado: 1 mensagem, {TriAxisDumpLayout.TotalLength} bytes, cabeçalho {HexFormatter.ToHex(TriAxisDumpLayout.ExpectedHeader)}).");
            return false;
        }

        var presets = new List<TriAxisPresetView>(TriAxisDumpLayout.PresetCount);
        for (var i = 0; i < TriAxisDumpLayout.PresetCount; i++)
        {
            var bytes = data.AsSpan(TriAxisDumpLayout.PresetsOffset + (i * TriAxisDumpLayout.PresetLength), TriAxisDumpLayout.PresetLength);
            presets.Add(ParsePreset(i + 1, bytes));
        }

        var programs = new List<TriAxisProgramMapping>(TriAxisDumpLayout.ProgramCount);
        for (var p = 0; p < TriAxisDumpLayout.ProgramCount; p++)
        {
            var value = data[TriAxisDumpLayout.ProgramsOffset + p];
            var display = value < TriAxisDumpLayout.PresetCount ? (value + 1).ToString("00") : TrF($"? (0x{value:X2})");
            programs.Add(new TriAxisProgramMapping(p + 1, value, display));
        }

        dump = new TriAxisDump(presets, programs, TriAxisDumpLayout.TrailerLength);
        reason = string.Empty;
        return true;
    }

    private static TriAxisPresetView ParsePreset(int number, ReadOnlySpan<byte> b)
    {
        var unknown = false;

        string Knob(byte value)
        {
            if (TriAxisValues.KnobToDisplay(value) is { } display)
            {
                return display;
            }

            unknown = true;
            return TrF($"? (0x{value:X2})");
        }

        var mode = TriAxisValues.ModeFromByte(b[TriAxisValues.ModeOffset]) is { } m ? TriAxisValues.ModeLabel(m) : null;
        if (mode is null)
        {
            unknown = true;
            mode = TrF($"? (0x{b[9]:X2})");
        }

        var fx = b[10];
        if ((fx & ~KnownFxBits) != 0)
        {
            unknown = true;
        }

        // Ordem dos bytes conforme o editor de referência:
        // 0 DynVoice, 1 Presence, 2 Master, 3 Lead2, 4 Lead1, 5 Bass, 6 Middle, 7 Treble, 8 Gain, 9 Modo, 10 FX.
        return new TriAxisPresetView(
            Number: number,
            Gain: Knob(b[8]),
            Treble: Knob(b[7]),
            Middle: Knob(b[6]),
            Bass: Knob(b[5]),
            Lead1Drive: Knob(b[4]),
            Lead2Drive: Knob(b[3]),
            Master: Knob(b[2]),
            Presence: Knob(b[1]),
            DynamicVoice: Knob(b[0]),
            Mode: mode,
            FxLoop: (fx & FxLoopBit) != 0,
            Switch1: (fx & 0x02) != 0,
            Switch2: (fx & 0x04) != 0,
            Switch3: (fx & 0x08) != 0,
            Switch4: (fx & 0x10) != 0,
            HasUnknownValues: unknown,
            RawHex: HexFormatter.ToHex(b));
    }
}
