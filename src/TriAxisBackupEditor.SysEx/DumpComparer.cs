using static TriAxisBackupEditor.Localization.Localizer;
namespace TriAxisBackupEditor.SysEx;

public sealed record ByteDifference(int Offset, byte? A, byte? B, string Region)
{
    public override string ToString() =>
        TrF($"0x{Offset:X4}  {(A is byte a ? a.ToString("X2") : "--")} → {(B is byte b ? b.ToString("X2") : "--")}  {Region}");
}

public sealed class DumpComparison
{
    public required string HashA { get; init; }
    public required string HashB { get; init; }
    public required int LengthA { get; init; }
    public required int LengthB { get; init; }
    public required int DifferenceCount { get; init; }
    public required IReadOnlyList<ByteDifference> Differences { get; init; }

    /// <summary>Presets (1..90) com pelo menos um byte diferente — só preenchido quando ambos seguem o mapa preliminar.</summary>
    public required IReadOnlyList<int> ChangedPresets { get; init; }

    public required bool UsesTriAxisLayout { get; init; }

    public bool Identical => string.Equals(HashA, HashB, StringComparison.Ordinal) && LengthA == LengthB;

    public bool DifferencesTruncated => DifferenceCount > Differences.Count;
}

public static class DumpComparer
{
    public static DumpComparison Compare(byte[] a, byte[] b, int maxListedDifferences = 2000)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var layout = SysExValidator.Validate(a).MatchesTriAxisLayout && SysExValidator.Validate(b).MatchesTriAxisLayout;
        var differences = new List<ByteDifference>();
        var changedPresets = new SortedSet<int>();
        var count = 0;
        var max = Math.Max(a.Length, b.Length);

        for (var i = 0; i < max; i++)
        {
            byte? va = i < a.Length ? a[i] : null;
            byte? vb = i < b.Length ? b[i] : null;
            if (va == vb)
            {
                continue;
            }

            count++;
            if (layout && i >= TriAxisDumpLayout.PresetsOffset && i < TriAxisDumpLayout.ProgramsOffset)
            {
                changedPresets.Add(((i - TriAxisDumpLayout.PresetsOffset) / TriAxisDumpLayout.PresetLength) + 1);
            }

            if (differences.Count < maxListedDifferences)
            {
                differences.Add(new ByteDifference(i, va, vb, layout ? TriAxisDumpLayout.DescribeOffset(i) : "—"));
            }
        }

        return new DumpComparison
        {
            HashA = SysExHash.Sha256Hex(a),
            HashB = SysExHash.Sha256Hex(b),
            LengthA = a.Length,
            LengthB = b.Length,
            DifferenceCount = count,
            Differences = differences,
            ChangedPresets = changedPresets.ToList(),
            UsesTriAxisLayout = layout
        };
    }
}
