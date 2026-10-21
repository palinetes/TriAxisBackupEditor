using TriAxisBackupEditor.SysEx;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.Application.Presets;

/// <summary>
/// Nomes dos timbres dados pelo usuário (o TriAxis não guarda nomes: ficam só no computador).
/// Índice 0 = preset 1.
/// </summary>
public static class PresetNaming
{
    public static string? NameFor(int preset, IReadOnlyList<string> names) =>
        preset >= 1 && preset <= names.Count && !string.IsNullOrWhiteSpace(names[preset - 1]) ? names[preset - 1].Trim() : null;

    /// <summary>"Timbre 07" ou "07 · Base limpa".</summary>
    public static string PresetLabel(int preset, IReadOnlyList<string> names) =>
        NameFor(preset, names) is { } name ? TrF($"{preset:00} · {name}") : TrF($"Timbre {preset:00}");

    /// <summary>Preset (1–90) chamado por um programa (1–128), segundo o mapa do dump; null se desconhecido.</summary>
    public static int? PresetForProgram(int program, TriAxisDump? dump)
    {
        // Só usa o mapa quando ele parece um mapa de verdade (num TriAxis real a região não parecia).
        if (dump is null || !dump.ProgramMapLooksValid || program < 1 || program > dump.ProgramMap.Count)
        {
            return null;
        }

        var index = dump.ProgramMap[program - 1].PresetIndex;
        return index < TriAxisDumpLayout.PresetCount ? index + 1 : null;
    }

    /// <summary>Texto do pedal no modo Tocar: nome do timbre quando conhecido; senão "Programa N".</summary>
    public static string PedalLabel(int program, TriAxisDump? dump, IReadOnlyList<string> names)
    {
        var preset = PresetForProgram(program, dump);
        if (preset is null)
        {
            return TrF($"Programa {program}");
        }

        return NameFor(preset.Value, names) ?? TrF($"Timbre {preset.Value:00}");
    }
}
