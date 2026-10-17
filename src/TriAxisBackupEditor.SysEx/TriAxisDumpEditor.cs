namespace TriAxisBackupEditor.SysEx;

/// <summary>
/// Edita uma CÓPIA de um dump real do TriAxis segundo o mapa preliminar.
/// Garantias:
/// <list type="bullet">
/// <item>o array original nunca é modificado;</item>
/// <item>só bytes das regiões de presets (5–994) e de programas (995–1122) são alterados;</item>
/// <item>cabeçalho, bloco de controladores, terminador F7 e bits desconhecidos são preservados bit a bit;</item>
/// <item>todo valor gravado vem das tabelas conhecidas (0x00–0x7F).</item>
/// </list>
/// </summary>
public sealed class TriAxisDumpEditor
{
    private readonly byte[] _original;
    private readonly byte[] _working;

    private TriAxisDumpEditor(byte[] source)
    {
        _original = (byte[])source.Clone();
        _working = (byte[])source.Clone();
    }

    public static bool TryCreate(byte[] dump, out TriAxisDumpEditor? editor, out string reason)
    {
        ArgumentNullException.ThrowIfNull(dump);
        editor = null;

        var validation = SysExValidator.Validate(dump);
        if (validation.HasErrors)
        {
            reason = "O arquivo não é um SysEx válido.";
            return false;
        }

        if (!validation.MatchesTriAxisLayout)
        {
            reason = $"O editor exige um dump completo do TriAxis no formato do mapa ({TriAxisDumpLayout.TotalLength} bytes, cabeçalho {HexFormatter.ToHex(TriAxisDumpLayout.ExpectedHeader)}). " +
                     $"Este arquivo tem {dump.Length} bytes.";
            return false;
        }

        editor = new TriAxisDumpEditor(dump);
        reason = string.Empty;
        return true;
    }

    public bool IsModified => !_working.AsSpan().SequenceEqual(_original);

    /// <summary>Cópia dos bytes atuais (já editados).</summary>
    public byte[] ToArray() => (byte[])_working.Clone();

    /// <summary>Cópia dos bytes originais carregados.</summary>
    public byte[] OriginalBytes() => (byte[])_original.Clone();

    // ---------------- Presets ----------------

    public byte GetPresetByte(int preset, int relativeOffset) => _working[PresetOffset(preset) + CheckRelative(relativeOffset)];

    /// <summary>Índice 0–15 na tabela de knob, ou null se o byte atual for desconhecido.</summary>
    public int? GetKnob(int preset, PresetParameter parameter)
    {
        var value = GetPresetByte(preset, (int)parameter);
        return value < TriAxisValues.KnobCount ? value : null;
    }

    public void SetKnob(int preset, PresetParameter parameter, int knobIndex)
    {
        if (knobIndex < 0 || knobIndex >= TriAxisValues.KnobCount)
        {
            throw new ArgumentOutOfRangeException(nameof(knobIndex), $"Valor de knob deve estar entre 0 e {TriAxisValues.KnobCount - 1}.");
        }

        _working[PresetOffset(preset) + (int)parameter] = (byte)knobIndex;
    }

    public TriAxisMode? GetMode(int preset) => TriAxisValues.ModeFromByte(GetPresetByte(preset, TriAxisValues.ModeOffset));

    public void SetMode(int preset, TriAxisMode mode) =>
        _working[PresetOffset(preset) + TriAxisValues.ModeOffset] = TriAxisValues.ModeToByte(mode);

    public bool GetSwitch(int preset, FxSwitch flag) => (GetPresetByte(preset, TriAxisValues.FxOffset) & (byte)flag) != 0;

    /// <summary>Liga/desliga um bit de FX/SW preservando os demais bits (inclusive desconhecidos).</summary>
    public void SetSwitch(int preset, FxSwitch flag, bool on)
    {
        if (flag == FxSwitch.None || ((byte)flag & ((byte)flag - 1)) != 0)
        {
            throw new ArgumentException("Informe um único switch.", nameof(flag));
        }

        var offset = PresetOffset(preset) + TriAxisValues.FxOffset;
        _working[offset] = on ? (byte)(_working[offset] | (byte)flag) : (byte)(_working[offset] & ~(byte)flag);
    }

    public void CopyPreset(int fromPreset, int toPreset)
    {
        Array.Copy(_working, PresetOffset(fromPreset), _working, PresetOffset(toPreset), TriAxisDumpLayout.PresetLength);
    }

    public byte[] GetPresetBytes(int preset) => _working.AsSpan(PresetOffset(preset), TriAxisDumpLayout.PresetLength).ToArray();

    /// <summary>Cola 11 bytes copiados de outro preset (devem ser bytes de dados 00–7F).</summary>
    public void SetPresetBytes(int preset, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != TriAxisDumpLayout.PresetLength || bytes.Any(b => b > 0x7F))
        {
            throw new ArgumentException("Dados de preset inválidos.", nameof(bytes));
        }

        bytes.CopyTo(_working, PresetOffset(preset));
    }

    public void RevertPreset(int preset)
    {
        var offset = PresetOffset(preset);
        Array.Copy(_original, offset, _working, offset, TriAxisDumpLayout.PresetLength);
    }

    public bool IsPresetModified(int preset)
    {
        var offset = PresetOffset(preset);
        return !_working.AsSpan(offset, TriAxisDumpLayout.PresetLength).SequenceEqual(_original.AsSpan(offset, TriAxisDumpLayout.PresetLength));
    }

    // ---------------- Mapa de programas ----------------

    /// <summary>Preset (1–90) associado ao Program Change (1–128), ou null se o byte for desconhecido.</summary>
    public int? GetProgramPreset(int program)
    {
        var value = _working[ProgramOffset(program)];
        return value < TriAxisDumpLayout.PresetCount ? value + 1 : null;
    }

    public void SetProgramPreset(int program, int preset)
    {
        CheckPreset(preset);
        _working[ProgramOffset(program)] = (byte)(preset - 1);
    }

    public void RevertAll() => Array.Copy(_original, _working, _original.Length);

    public DumpComparison GetChanges() => DumpComparer.Compare(_original, _working);

    // ---------------- Auxiliares ----------------

    private static int PresetOffset(int preset)
    {
        CheckPreset(preset);
        return TriAxisDumpLayout.PresetsOffset + ((preset - 1) * TriAxisDumpLayout.PresetLength);
    }

    private static int ProgramOffset(int program)
    {
        if (program < 1 || program > TriAxisDumpLayout.ProgramCount)
        {
            throw new ArgumentOutOfRangeException(nameof(program), $"Programa deve estar entre 1 e {TriAxisDumpLayout.ProgramCount}.");
        }

        return TriAxisDumpLayout.ProgramsOffset + program - 1;
    }

    private static void CheckPreset(int preset)
    {
        if (preset < 1 || preset > TriAxisDumpLayout.PresetCount)
        {
            throw new ArgumentOutOfRangeException(nameof(preset), $"Preset deve estar entre 1 e {TriAxisDumpLayout.PresetCount}.");
        }
    }

    private static int CheckRelative(int relativeOffset)
    {
        if (relativeOffset < 0 || relativeOffset >= TriAxisDumpLayout.PresetLength)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeOffset));
        }

        return relativeOffset;
    }
}
