namespace TriAxisBackupEditor.Localization;

/// <summary>Traduções para o inglês: chave = texto em português, valor = texto em inglês.</summary>
internal static partial class English
{
    private static readonly Dictionary<string, string> Map = BuildMap();

    internal static IReadOnlyDictionary<string, string> Entries => Map;

    private static Dictionary<string, string> BuildMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        AddScreens(map);
        AddMessages(map);
        AddMore(map);
        return map;
    }

    private static partial void AddScreens(Dictionary<string, string> map);

    private static partial void AddMessages(Dictionary<string, string> map);

    private static partial void AddMore(Dictionary<string, string> map);
}
