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

    public static AppPaths CreateDefault()
    {
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
