using System.Text.RegularExpressions;
using TriAxisBackupEditor.Midi;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.Application.Connection;

/// <summary>Uma interface MIDI vista como um aparelho só (entrada + saída), como o usuário a enxerga.</summary>
public sealed record MidiDevicePair(string Name, MidiDeviceInfo Input, MidiDeviceInfo Output)
{
    public override string ToString() => Name;
}

/// <summary>
/// Junta as portas de entrada e saída do mesmo aparelho para o modo simples, onde o usuário escolhe
/// "a interface" e não "MIDI IN" e "MIDI OUT" separados.
/// </summary>
public static partial class DevicePairing
{
    public static IReadOnlyList<MidiDevicePair> Pair(IReadOnlyList<MidiDeviceInfo> inputs, IReadOnlyList<MidiDeviceInfo> outputs)
    {
        var pairs = new List<MidiDevicePair>();
        var usedOutputs = new HashSet<MidiDeviceInfo>();

        foreach (var input in inputs)
        {
            // 1º: mesmo nome e mesma ocorrência (caso mais comum: "UM-ONE" / "UM-ONE").
            var output = outputs.FirstOrDefault(o => !usedOutputs.Contains(o) && o.Key == input.Key)
                         // 2º: mesmo nome depois de tirar marcações de direção ("MIDIIN2 (x)" / "MIDIOUT2 (x)", "x In" / "x Out").
                         ?? outputs.FirstOrDefault(o => !usedOutputs.Contains(o) && Normalize(o.Name) == Normalize(input.Name) && Normalize(o.Name).Length > 0);
            if (output is null)
            {
                continue;
            }

            usedOutputs.Add(output);
            pairs.Add(new MidiDevicePair(FriendlyName(input), input, output));
        }

        return pairs;
    }

    /// <summary>Nome sem marcações técnicas de direção, para comparar entrada e saída.</summary>
    public static string Normalize(string name)
    {
        var text = name.ToLowerInvariant();
        text = DirectionPrefix().Replace(text, " ");
        text = DirectionWord().Replace(text, " ");
        text = text.Replace("(", " ").Replace(")", " ");
        return WhiteSpace().Replace(text, " ").Trim();
    }

    private static string FriendlyName(MidiDeviceInfo input)
    {
        var name = input.Name;
        var match = WrappedName().Match(name);
        if (match.Success)
        {
            name = match.Groups["name"].Value.Trim();
        }

        var trimmed = TrailingInputWord().Replace(name, string.Empty).Trim();
        if (trimmed.Length > 0)
        {
            name = trimmed;
        }

        return input.Occurrence == 0 ? name : TrF($"{name} ({input.Occurrence + 1})");
    }

    [GeneratedRegex(@"\bmidi(in|out)\d*\b")]
    private static partial Regex DirectionPrefix();

    [GeneratedRegex(@"\b(in|out|input|output)\b")]
    private static partial Regex DirectionWord();

    [GeneratedRegex(@"^MIDI(IN|OUT)\d*\s*\((?<name>.+)\)$", RegexOptions.IgnoreCase)]
    private static partial Regex WrappedName();

    [GeneratedRegex(@"\s+(in|input)$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingInputWord();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
