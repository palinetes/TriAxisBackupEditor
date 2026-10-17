using System.Collections.ObjectModel;
using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Storage;

namespace TriAxisBackupEditor.App.ViewModels.Simple;

public sealed class SimpleBackupRow
{
    public SimpleBackupRow(BackupRecord record)
    {
        Record = record;
    }

    public BackupRecord Record { get; }

    public string Title => string.IsNullOrWhiteSpace(Record.Notes) ? "Backup" : Record.Notes;

    public string When => SimpleHomeViewModel.Friendly(Record.CreatedAt);

    public bool IsEdited => Record.Origin == BackupOrigins.Edited;
}

/// <summary>Guardar a memória do TriAxis e devolver um backup, sem jargão.</summary>
public sealed class SimpleBackupViewModel : SimplePage
{
    private readonly AppState _state;
    private readonly BackupLibraryService _library;
    private readonly IDialogService _dialogs;
    private CaptureCardViewModel? _capture;
    private SimpleBackupRow? _selected;

    public SimpleBackupViewModel(SimpleShellViewModel shell, AppState state, BackupLibraryService library, IDialogService dialogs)
        : base(shell)
    {
        _state = state;
        _library = library;
        _dialogs = dialogs;

        NewBackupCommand = new RelayCommand(StartNewBackup, () => !_state.IsBusy);
        RestoreCommand = new RelayCommand(Restore, () => Selected is not null && !_state.IsBusy);
        RenameCommand = new RelayCommand(Rename, () => Selected is not null);
        DeleteCommand = new RelayCommand(Delete, () => Selected is not null && !_state.IsBusy);
        OpenFolderCommand = new RelayCommand(() =>
        {
            Directory.CreateDirectory(_state.Settings.BackupFolder);
            AppState.OpenInExplorer(_state.Settings.BackupFolder);
        });
    }

    public override string Title => "Backup";

    public override bool CanLeave => Capture?.IsRunning != true;

    public ObservableCollection<SimpleBackupRow> Rows { get; } = new();

    public SimpleBackupRow? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    /// <summary>Cartão "Guardar a memória" visível depois de clicar no botão.</summary>
    public CaptureCardViewModel? Capture
    {
        get => _capture;
        private set => SetProperty(ref _capture, value);
    }

    public bool IsEmpty => Rows.Count == 0;

    public ICommand NewBackupCommand { get; }

    public ICommand RestoreCommand { get; }

    public ICommand RenameCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public override void OnEnter()
    {
        Capture = null;
        Refresh();
    }

    private void Refresh()
    {
        var selectedId = Selected?.Record.Id;
        Rows.Clear();
        foreach (var record in _library.Search(null).Where(r => r.FileExists))
        {
            Rows.Add(new SimpleBackupRow(record));
        }

        Selected = Rows.FirstOrDefault(r => r.Record.Id == selectedId);
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void StartNewBackup()
    {
        var notes = _dialogs.PromptText("Guardar a memória do TriAxis", "Dê um nome para este backup (opcional):", $"Backup de {DateTime.Now:dd/MM/yyyy}");
        if (notes is null)
        {
            return;
        }

        var card = Shell.NewCaptureCard(notes);
        card.Completed += (_, _) => Refresh();
        Capture = card;
        card.StartCommand.Execute(null);
    }

    private void Restore()
    {
        var row = Selected!;
        Shell.StartSafeSend(row.Record.FilePath, $"o backup \"{row.Title}\" ({row.When})", this);
    }

    private void Rename()
    {
        var row = Selected!;
        var text = _dialogs.PromptText("Renomear backup", "Nome deste backup:", row.Title);
        if (text is null)
        {
            return;
        }

        _library.UpdateNotes(row.Record, text);
        Refresh();
    }

    private void Delete()
    {
        var row = Selected!;
        if (!_dialogs.Confirm("Apagar backup", $"Mandar \"{row.Title}\" ({row.When}) para a Lixeira do Windows?\n\nDá para recuperar depois pela Lixeira."))
        {
            return;
        }

        try
        {
            _library.Delete(row.Record, moveFileToRecycleBin: true);
        }
        catch (IOException ex)
        {
            _dialogs.ShowError("Apagar backup", ex.Message);
        }

        Refresh();
    }
}
