namespace TriAxisBackupEditor.Storage;

/// <summary>Todos os dados ficam locais: %LOCALAPPDATA%\TriAxisBackupEditor e Documentos\TriAxis Backups.</summary>
public sealed class AppPaths
{
    public AppPaths(string dataFolder, string defaultBackupFolder)
    {
        DataFolder = dataFolder;
        DefaultBackupFolder = defaultBackupFolder;
    }

    public string DataFolder { get; }

    public string DefaultBackupFolder { get; }

    public string DatabasePath => Path.Combine(DataFolder, "biblioteca.db");

    public string SettingsPath => Path.Combine(DataFolder, "configuracoes.json");

    public string LogsFolder => Path.Combine(DataFolder, "logs");

    /// <summary>Variável de ambiente que troca a pasta de dados (cópia portátil, testes, capturas de tela do manual).</summary>
    public const string DataFolderVariable = "TRIAXIS_DATA_DIR";

    public static AppPaths CreateDefault()
    {
        if (Environment.GetEnvironmentVariable(DataFolderVariable) is { Length: > 0 } custom)
        {
            return new AppPaths(custom, Path.Combine(custom, "Backups"));
        }

        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TriAxisBackupEditor");
        var backups = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TriAxis Backups");
        return new AppPaths(data, backups);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(LogsFolder);
    }
}
