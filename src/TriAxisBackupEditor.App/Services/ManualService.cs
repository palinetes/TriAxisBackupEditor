using System.Diagnostics;

namespace TriAxisBackupEditor.App.Services;

/// <summary>
/// O manual (docs/manual) vai embutido no executável. Ao abrir, é extraído para a pasta de dados local
/// e exibido no navegador padrão — funciona sem internet.
/// </summary>
public static class ManualService
{
    private const string Prefix = "manual/";

    public static string Extract(string targetFolder, string indexFile = "index.html")
    {
        var assembly = typeof(ManualService).Assembly;
        var names = assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)).ToList();
        if (names.Count == 0)
        {
            throw new FileNotFoundException("O manual não foi encontrado dentro do programa.");
        }

        foreach (var name in names)
        {
            var relative = name[Prefix.Length..].Replace('\\', '/');
            var path = Path.Combine(targetFolder, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var source = assembly.GetManifestResourceStream(name)!;
            using var target = File.Create(path);
            source.CopyTo(target);
        }

        return Path.Combine(targetFolder, indexFile);
    }

    public static void Open(string dataFolder)
    {
        var index = Extract(Path.Combine(dataFolder, "manual"), TriAxisBackupEditor.Localization.Localizer.IsEnglish ? "index.en.html" : "index.html");
        Process.Start(new ProcessStartInfo(index) { UseShellExecute = true });
    }
}
