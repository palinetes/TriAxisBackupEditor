using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TriAxisBackupEditor.Localization;

namespace TriAxisBackupEditor.Tests;

/// <summary>
/// O inglês não pode ficar para trás do português: toda frase do código (Tr/TrF) e todo texto fixo das telas (XAML)
/// precisa ter tradução, e os {0}, {1}... das frases com valores precisam bater.
/// </summary>
[Collection("Localizer")]
public class LocalizationTests : IDisposable
{
    public LocalizationTests() => Localizer.SetLanguage(AppLanguage.Portuguese);

    public void Dispose() => Localizer.SetLanguage(AppLanguage.Portuguese);

    // ---------- Localizer ----------

    [Theory]
    [InlineData("pt", "en-US", AppLanguage.Portuguese)]
    [InlineData("en", "pt-BR", AppLanguage.English)]
    [InlineData("auto", "pt-BR", AppLanguage.Portuguese)]
    [InlineData("auto", "pt-PT", AppLanguage.Portuguese)]
    [InlineData("auto", "en-GB", AppLanguage.English)]
    [InlineData("auto", "de-DE", AppLanguage.English)]
    [InlineData(null, "es-ES", AppLanguage.English)]
    [InlineData("xx", "pt-BR", AppLanguage.Portuguese)]
    public void Resolve_FollowsSettingThenWindows(string? setting, string windows, AppLanguage expected) =>
        Assert.Equal(expected, Localizer.Resolve(setting, new CultureInfo(windows)));

    [Fact]
    public void Portuguese_ReturnsTheSourceText()
    {
        Assert.Equal("Cancelar", Localizer.Tr("Cancelar"));
        Assert.Equal("Programa 7", Localizer.TrF($"Programa {7}"));
    }

    [Fact]
    public void English_TranslatesAndFormats()
    {
        Localizer.SetLanguage(AppLanguage.English);

        Assert.Equal("Program 7", Localizer.TrF($"Programa {7}"));
        Assert.Equal("Bank 2 of 12: programs 21–30", Localizer.TrF($"Banco {2} de {12}: programas {21}–{30}"));
        Assert.Equal("Texto sem tradução", Localizer.Tr("Texto sem tradução"));
    }

    [Fact]
    public void TranslateUiText_KeepsSurroundingSpacesAndLineBreaks()
    {
        Localizer.SetLanguage(AppLanguage.English);

        Assert.Equal("  Mode: ", Localizer.TranslateUiText("  Modo: "));
        Assert.Equal("Everything on the TriAxis now will be replaced by ", Localizer.TranslateUiText("Tudo o que está no TriAxis agora será substituído por "));
        Assert.Equal("hold SHIFT and press the left\narrow of PRESET", Localizer.TranslateUiText("segure SHIFT e aperte a seta\r\nda esquerda do PRESET"));
        Assert.Equal("", Localizer.TranslateUiText(""));
    }

    [Fact]
    public void RestorePhrase_FollowsTheLanguage()
    {
        Assert.Equal("RESTAURAR", TriAxisBackupEditor.Application.Restore.RestoreConfirmation.RequiredPhrase);
        Localizer.SetLanguage(AppLanguage.English);
        Assert.Equal("RESTORE", TriAxisBackupEditor.Application.Restore.RestoreConfirmation.RequiredPhrase);
    }

    // ---------- chaves de frases com valores: o cálculo precisa bater com o compilador ----------

    [Fact]
    public void FormatKey_MatchesTheCompilerForInterpolatedStrings()
    {
        var n = 3;
        var s = "x";
        FormattableString real = $"A {n} B {(n > 2 ? "y" : "z")} C {n:00} D {s,5} {{literal}} E {n,-4:N0}";
        const string source = "A {n} B {(n > 2 ? \"y\" : \"z\")} C {n:00} D {s,5} {{literal}} E {n,-4:N0}";

        Assert.Equal(real.Format, SourceKeys.CompiledFormat(source));
    }

    // ---------- completude ----------

    [Fact]
    public void EveryTrAndTrFInTheSource_HasAnEnglishTranslation()
    {
        var missing = new List<string>();
        foreach (var (file, line, key) in SourceKeys.FindAll(RepoPaths.SourceFolder))
        {
            if (!English.Entries.ContainsKey(key))
            {
                missing.Add($"{file}:{line}  {key}");
            }
        }

        Assert.True(missing.Count == 0, "Sem tradução em inglês:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void EveryFixedTextInTheScreens_HasAnEnglishTranslation()
    {
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(RepoPaths.AppFolder, "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || Path.GetFileName(file) is "Dark.xaml" or "App.xaml")
            {
                continue;
            }

            foreach (var text in XamlTexts(file))
            {
                var key = text.Trim().Replace("\r\n", "\n");
                if (!English.Entries.ContainsKey(key))
                {
                    missing.Add($"{Path.GetFileName(file)}  {key}");
                }
            }
        }

        Assert.True(missing.Count == 0, "Telas sem tradução em inglês:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void NoDisplayTextInStyleSetters()
    {
        // O tradutor das telas não alcança textos vindos de Setter de estilo (um valor local passaria por cima do gatilho).
        // Textos que mudam por estado devem vir do ViewModel, com Tr().
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(RepoPaths.AppFolder, "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            foreach (var setter in XDocument.Load(file).Descendants().Where(e => e.Name.LocalName == "Setter"))
            {
                var property = (string?)setter.Attribute("Property");
                var value = (string?)setter.Attribute("Value");
                if (property is "Text" or "Content" or "ToolTip" or "Header"
                    && value is not null
                    && Regex.IsMatch(value, "[A-Za-zÀ-ú]{3}")
                    && !value.StartsWith('{'))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {property} = {value}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Texto fixo em Setter de estilo (use uma propriedade do ViewModel com Tr):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void BothManuals_ExistAndAreEmbeddedWithoutCultureSplit()
    {
        // "index.en.html" tem ".en." no nome: sem WithCulture="false" o .NET o põe num assembly de idioma e o F1 em inglês não acha o arquivo.
        Assert.True(File.Exists(Path.Combine(RepoPaths.Root, "docs", "manual", "index.html")));
        Assert.True(File.Exists(Path.Combine(RepoPaths.Root, "docs", "manual", "index.en.html")));
        var csproj = File.ReadAllText(Path.Combine(RepoPaths.AppFolder, "TriAxisBackupEditor.App.csproj"));
        Assert.Contains("WithCulture=\"false\"", csproj);
    }

    [Fact]
    public void Translations_KeepTheSamePlaceholders()
    {
        var problems = new List<string>();
        foreach (var (key, value) in English.Entries)
        {
            if (Placeholders(key) != Placeholders(value))
            {
                problems.Add($"{key}  =>  {value}");
            }
        }

        Assert.True(problems.Count == 0, "Placeholders diferentes:\n" + string.Join("\n", problems));
    }

    // ---------- apoio ----------

    private static string Placeholders(string text) =>
        string.Join(",", Regex.Matches(text, @"(?<!\{)\{(\d+)[,:}]").Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x));

    private static IEnumerable<string> XamlTexts(string file)
    {
        var root = XDocument.Load(file).Root!;
        foreach (var element in root.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes())
            {
                var name = attribute.Name.LocalName;
                var value = attribute.Value;
                if (name is "Text" or "Content" or "Header" or "ToolTip" or "Title" or "Label"
                    && !value.StartsWith('{')
                    && Regex.IsMatch(value, @"[A-Za-zÀ-ú]{2}"))
                {
                    yield return value;
                }
            }

            var own = string.Concat(element.Nodes().OfType<XText>().Select(t => t.Value)).Trim();
            if (own.Length > 0 && Regex.IsMatch(own, @"[A-Za-zÀ-ú]{2}"))
            {
                yield return own;
            }
        }
    }
}

internal static class RepoPaths
{
    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TriAxisBackupEditor.sln")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
        }
    }

    public static string SourceFolder => Path.Combine(Root, "src");

    public static string AppFolder => Path.Combine(Root, "src", "TriAxisBackupEditor.App");
}

/// <summary>Encontra as chamadas Tr("...") e TrF($"...") no código e calcula a chave do dicionário.</summary>
internal static class SourceKeys
{
    public static IEnumerable<(string File, int Line, string Key)> FindAll(string sourceFolder)
    {
        foreach (var file in Directory.EnumerateFiles(sourceFolder, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/obj/") || normalized.Contains("/bin/") || normalized.Contains("/TriAxisBackupEditor.Tests/")
                || normalized.Contains("/TriAxisBackupEditor.Localization/"))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, @"(?<![A-Za-z0-9_.])(TrF|Tr)\("))
            {
                var start = match.Index + match.Length;
                while (start < text.Length && char.IsWhiteSpace(text[start]))
                {
                    start++;
                }

                var interpolated = match.Groups[1].Value == "TrF";
                var prefix = interpolated ? "$\"" : "\"";
                if (string.CompareOrdinal(text, start, prefix, 0, prefix.Length) != 0)
                {
                    continue; // Tr(variavel): sem texto-fonte para conferir
                }

                var inner = ReadLiteral(text, start + prefix.Length, interpolated);
                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                yield return (Path.GetRelativePath(sourceFolder, file), line, interpolated ? CompiledFormat(inner) : Unescape(inner));
            }
        }
    }

    private static string ReadLiteral(string text, int index, bool interpolated)
    {
        var sb = new StringBuilder();
        var i = index;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\\')
            {
                sb.Append(c).Append(text[i + 1]);
                i += 2;
                continue;
            }

            if (interpolated && c == '{' && text[i + 1] != '{')
            {
                var depth = 1;
                sb.Append(c);
                i++;
                while (i < text.Length && depth > 0)
                {
                    var h = text[i];
                    if (h == '"')
                    {
                        sb.Append(h);
                        i++;
                        while (i < text.Length && text[i] != '"')
                        {
                            if (text[i] == '\\')
                            {
                                sb.Append(text[i]);
                                i++;
                            }

                            sb.Append(text[i]);
                            i++;
                        }

                        sb.Append('"');
                        i++;
                        continue;
                    }

                    if (h == '{')
                    {
                        depth++;
                    }
                    else if (h == '}')
                    {
                        depth--;
                    }

                    sb.Append(h);
                    i++;
                }

                continue;
            }

            if (c == '"')
            {
                return sb.ToString();
            }

            sb.Append(c);
            i++;
        }

        throw new InvalidOperationException("Literal sem fim.");
    }

    /// <summary>Texto de um literal interpolado como FormattableString.Format: cada {expressão[,alinhamento][:formato]} vira {i...}.</summary>
    public static string CompiledFormat(string inner)
    {
        var sb = new StringBuilder();
        var index = 0;
        var i = 0;
        while (i < inner.Length)
        {
            var c = inner[i];
            if (c == '\\')
            {
                sb.Append(Unescape(inner.Substring(i, 2)));
                i += 2;
                continue;
            }

            if (c == '{' && i + 1 < inner.Length && inner[i + 1] == '{')
            {
                sb.Append("{{");
                i += 2;
                continue;
            }

            if (c == '}' && i + 1 < inner.Length && inner[i + 1] == '}')
            {
                sb.Append("}}");
                i += 2;
                continue;
            }

            if (c != '{')
            {
                sb.Append(c);
                i++;
                continue;
            }

            var depth = 1;
            var j = i + 1;
            var paren = 0;
            int? colon = null;
            int? comma = null;
            while (j < inner.Length && depth > 0)
            {
                var h = inner[j];
                if (h == '"')
                {
                    j++;
                    while (j < inner.Length && inner[j] != '"')
                    {
                        if (inner[j] == '\\')
                        {
                            j++;
                        }

                        j++;
                    }

                    j++;
                    continue;
                }

                if (h is '(' or '[')
                {
                    paren++;
                }
                else if (h is ')' or ']')
                {
                    paren--;
                }
                else if (h == '{')
                {
                    depth++;
                }
                else if (h == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
                else if (h == ':' && depth == 1 && paren == 0 && colon is null)
                {
                    colon = j;
                }
                else if (h == ',' && depth == 1 && paren == 0 && colon is null && comma is null)
                {
                    comma = j;
                }

                j++;
            }

            var suffix = colon is int cIdx ? inner[cIdx..j] : string.Empty;
            if (comma is int mIdx)
            {
                var alignEnd = colon ?? j;
                var align = inner[mIdx..alignEnd];
                if (Regex.IsMatch(align, @"^,\s*-?\d+\s*$"))
                {
                    suffix = align.Trim() + suffix;
                }
            }

            sb.Append('{').Append(index++).Append(suffix).Append('}');
            i = j + 1;
        }

        return sb.ToString();
    }

    private static string Unescape(string text)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\' || i + 1 >= text.Length)
            {
                sb.Append(text[i]);
                continue;
            }

            i++;
            switch (text[i])
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case '"': sb.Append('"'); break;
                case '\'': sb.Append('\''); break;
                case '\\': sb.Append('\\'); break;
                case 'u': sb.Append((char)Convert.ToInt32(text.Substring(i + 1, 4), 16)); i += 4; break;
                default: sb.Append('\\').Append(text[i]); break;
            }
        }

        return sb.ToString();
    }
}
