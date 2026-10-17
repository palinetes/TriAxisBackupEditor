namespace TriAxisBackupEditor.SysEx;

/// <summary>Os 9 parâmetros de "knob" do preset, com o valor sendo o byte relativo dentro dos 11 bytes (mapa preliminar).</summary>
public enum PresetParameter
{
    DynamicVoice = 0,
    Presence = 1,
    Master = 2,
    Lead2Drive = 3,
    Lead1Drive = 4,
    Bass = 5,
    Middle = 6,
    Treble = 7,
    Gain = 8
}

/// <summary>Os 8 modos do TriAxis (LEDs RHY/LD1/LD2).</summary>
public enum TriAxisMode
{
    RhythmGreen,
    RhythmYellow,
    Lead1Green,
    Lead1Yellow,
    Lead1Red,
    Lead2Green,
    Lead2Yellow,
    Lead2Red
}

[Flags]
public enum FxSwitch : byte
{
    None = 0,
    FxLoop = 0x01,
    Switch1 = 0x02,
    Switch2 = 0x04,
    Switch3 = 0x08,
    Switch4 = 0x10
}

/// <summary>Tabelas de valores do mapa preliminar (editor de referência markphilpot/triaxiseditor).</summary>
public static class TriAxisValues
{
    public const int ModeOffset = 9;
    public const int FxOffset = 10;

    /// <summary>16 posições do display: índice = byte gravado no dump.</summary>
    public static IReadOnlyList<string> KnobDisplay { get; } = new[]
    {
        "0.0", "1.0", "2.0", "3.0", "3.5", "4.0", "4.5", "5.0",
        "5.5", "6.0", "6.5", "7.0", "7.5", "8.0", "9.0", "10"
    };

    public static int KnobCount => KnobDisplay.Count;

    /// <summary>Ordem dos knobs no painel frontal, da esquerda para a direita.</summary>
    public static IReadOnlyList<PresetParameter> PanelOrder { get; } = new[]
    {
        PresetParameter.Gain,
        PresetParameter.Treble,
        PresetParameter.Middle,
        PresetParameter.Bass,
        PresetParameter.Lead1Drive,
        PresetParameter.Lead2Drive,
        PresetParameter.Master,
        PresetParameter.Presence,
        PresetParameter.DynamicVoice
    };

    private static readonly Dictionary<TriAxisMode, byte> ModeBytes = new()
    {
        [TriAxisMode.RhythmGreen] = 0x7E,
        [TriAxisMode.RhythmYellow] = 0x7D,
        [TriAxisMode.Lead1Green] = 0x7B,
        [TriAxisMode.Lead1Yellow] = 0x77,
        [TriAxisMode.Lead1Red] = 0x6F,
        [TriAxisMode.Lead2Green] = 0x5F,
        [TriAxisMode.Lead2Yellow] = 0x3F,
        [TriAxisMode.Lead2Red] = 0x7F
    };

    private static readonly Dictionary<byte, TriAxisMode> ModesByByte = ModeBytes.ToDictionary(p => p.Value, p => p.Key);

    public static string ParameterLabel(PresetParameter parameter) => parameter switch
    {
        PresetParameter.Gain => "GAIN",
        PresetParameter.Treble => "TREBLE",
        PresetParameter.Middle => "MIDDLE",
        PresetParameter.Bass => "BASS",
        PresetParameter.Lead1Drive => "LEAD 1 DRIVE",
        PresetParameter.Lead2Drive => "LEAD 2 DRIVE",
        PresetParameter.Master => "MASTER",
        PresetParameter.Presence => "PRESENCE",
        PresetParameter.DynamicVoice => "DYNAMIC VOICE",
        _ => parameter.ToString()
    };

    public static string ModeLabel(TriAxisMode mode) => mode switch
    {
        TriAxisMode.RhythmGreen => "Rhythm Green",
        TriAxisMode.RhythmYellow => "Rhythm Yellow",
        TriAxisMode.Lead1Green => "Lead 1 Green",
        TriAxisMode.Lead1Yellow => "Lead 1 Yellow",
        TriAxisMode.Lead1Red => "Lead 1 Red",
        TriAxisMode.Lead2Green => "Lead 2 Green",
        TriAxisMode.Lead2Yellow => "Lead 2 Yellow",
        TriAxisMode.Lead2Red => "Lead 2 Red",
        _ => mode.ToString()
    };

    public static byte ModeToByte(TriAxisMode mode) => ModeBytes[mode];

    public static TriAxisMode? ModeFromByte(byte value) => ModesByByte.TryGetValue(value, out var mode) ? mode : null;

    /// <summary>"8.0" para valores conhecidos; null se o byte estiver fora da tabela.</summary>
    public static string? KnobToDisplay(byte value) => value < KnobDisplay.Count ? KnobDisplay[value] : null;
}
