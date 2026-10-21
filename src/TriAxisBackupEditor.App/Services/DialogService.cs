using System.Windows;
using Microsoft.Win32;
using TriAxisBackupEditor.App.Views;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.Services;

public enum RestoreDialogOutcome
{
    Cancelled,
    Confirmed,
    SafetyBackupRequested
}

public sealed class RestoreDialogContext
{
    public required string FileName { get; init; }
    public required int SizeBytes { get; init; }
    public required string Sha256 { get; init; }
    public required string OutputDevice { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public string? SafetyBackupPath { get; init; }
    public bool CanMakeSafetyBackup { get; init; }
}

public sealed record RestoreDialogResult(RestoreDialogOutcome Outcome, bool OverwriteAcknowledged, string TypedPhrase, bool WarningsAcknowledged);

public interface IDialogService
{
    string? OpenSyxFile(string? initialFolder);

    string? ChooseFolder(string? initialFolder);

    string? SaveCsvFile(string? initialFolder, string defaultName);

    void ShowInfo(string title, string message);

    void ShowWarning(string title, string message);

    void ShowError(string title, string message);

    bool Confirm(string title, string message);

    /// <summary>true = Sim, false = Não, null = Cancelar.</summary>
    bool? AskYesNoCancel(string title, string message);

    string? PromptText(string title, string label, string initialValue);

    RestoreDialogResult ShowRestoreConfirmation(RestoreDialogContext context);
}

public sealed class DialogService : IDialogService
{
    private static Window? Owner =>
        System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? System.Windows.Application.Current?.MainWindow;

    public string? OpenSyxFile(string? initialFolder)
    {
        var dialog = new OpenFileDialog
        {
            Title = Tr("Selecionar arquivo SysEx"),
            Filter = Tr("Arquivos SysEx (*.syx)|*.syx|Todos os arquivos (*.*)|*.*"),
            CheckFileExists = true,
            InitialDirectory = ExistingFolder(initialFolder)
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ChooseFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = Tr("Escolher pasta de backups"),
            InitialDirectory = ExistingFolder(initialFolder)
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? SaveCsvFile(string? initialFolder, string defaultName)
    {
        var dialog = new SaveFileDialog
        {
            Title = Tr("Exportar biblioteca"),
            Filter = Tr("CSV (*.csv)|*.csv"),
            FileName = defaultName,
            OverwritePrompt = true,
            InitialDirectory = ExistingFolder(initialFolder)
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void ShowInfo(string title, string message) => Show(title, message, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string title, string message) => Show(title, message, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string title, string message) => Show(title, message, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string title, string message) =>
        Show(title, message, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    public bool? AskYesNoCancel(string title, string message) =>
        Show(title, message, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        };

    public string? PromptText(string title, string label, string initialValue)
    {
        var window = new TextInputWindow(title, label, initialValue) { Owner = Owner };
        return window.ShowDialog() == true ? window.Value : null;
    }

    public RestoreDialogResult ShowRestoreConfirmation(RestoreDialogContext context)
    {
        var window = new RestoreConfirmWindow(context) { Owner = Owner };
        window.ShowDialog();
        return window.Result;
    }

    private static MessageBoxResult Show(string title, string message, MessageBoxButton buttons, MessageBoxImage image, MessageBoxResult defaultResult = MessageBoxResult.OK)
    {
        var owner = Owner;
        return owner is null
            ? MessageBox.Show(message, title, buttons, image, defaultResult)
            : MessageBox.Show(owner, message, title, buttons, image, defaultResult);
    }

    private static string? ExistingFolder(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) ? folder : null;
}
