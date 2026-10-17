namespace TriAxisBackupEditor.SysEx;

/// <summary>Bytes de status relevantes para System Exclusive.</summary>
public static class SysExConstants
{
    /// <summary>Início de SysEx.</summary>
    public const byte Start = 0xF0;

    /// <summary>Fim de SysEx (EOX).</summary>
    public const byte End = 0xF7;

    /// <summary>Bytes 0xF8..0xFF são mensagens de tempo real (clock, active sensing...).
    /// Pela especificação MIDI podem aparecer intercalados em um SysEx e não fazem parte dele.</summary>
    public static bool IsRealTime(byte value) => value >= 0xF8;

    /// <summary>Qualquer byte com o bit 7 ligado é um byte de status.</summary>
    public static bool IsStatus(byte value) => value >= 0x80;
}
