using System.Globalization;

namespace TriAxisBackupEditor.Localization;

public enum AppLanguage
{
    Portuguese,
    English
}

/// <summary>
/// Tradução do programa. O texto-fonte é o próprio português (<c>Tr("Cancelar")</c>): em português devolve o
/// texto como está, em inglês procura a tradução em <see cref="English"/> e, se não houver, mostra o português.
/// Frases com valores usam <see cref="TrF"/>: <c>TrF($"Programa {n}")</c> procura <c>"Programa {0}"</c>.
/// O log técnico não é traduzido: é para suporte e fica sempre em português.
/// </summary>
public static class Localizer
{
    private static volatile AppLanguage _current = AppLanguage.Portuguese;

    public static AppLanguage Current => _current;

    public static bool IsEnglish => _current == AppLanguage.English;

    public static void SetLanguage(AppLanguage language) => _current = language;

    /// <summary>"pt" / "en" escolhem direto; qualquer outro valor ("auto") segue o idioma do Windows.</summary>
    public static AppLanguage Resolve(string? setting, CultureInfo windowsCulture)
    {
        switch (setting?.Trim().ToLowerInvariant())
        {
            case "pt":
                return AppLanguage.Portuguese;
            case "en":
                return AppLanguage.English;
            default:
                return string.Equals(windowsCulture.TwoLetterISOLanguageName, "pt", StringComparison.OrdinalIgnoreCase)
                    ? AppLanguage.Portuguese
                    : AppLanguage.English;
        }
    }

    public static string Tr(string portuguese) => IsEnglish ? Lookup(portuguese) : portuguese;

    public static string TrF(FormattableString portuguese)
    {
        var format = IsEnglish ? Lookup(portuguese.Format) : portuguese.Format;
        return string.Format(CultureInfo.CurrentCulture, format, portuguese.GetArguments());
    }

    /// <summary>
    /// Para textos vindos do XAML: ignora espaços nas pontas (e os mantém) e quebras de linha \r\n.
    /// Devolve o próprio texto quando não há tradução.
    /// </summary>
    public static string TranslateUiText(string text)
    {
        if (!IsEnglish || string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var trimmedStart = text.Length - text.TrimStart().Length;
        var trimmedEnd = text.Length - text.TrimEnd().Length;
        var core = text.Trim().Replace("\r\n", "\n");
        if (!English.Entries.TryGetValue(core, out var english))
        {
            return text;
        }

        return text[..trimmedStart] + english + text[^trimmedEnd..];
    }

    private static string Lookup(string portuguese) =>
        English.Entries.TryGetValue(portuguese, out var english) ? english : portuguese;
}
