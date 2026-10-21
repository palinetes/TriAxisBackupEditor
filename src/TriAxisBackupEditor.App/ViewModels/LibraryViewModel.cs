using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels;

public sealed class BackupRow
{
    private static CultureInfo PtBr => CultureInfo.CurrentCulture;

    public BackupRow(BackupRecord record)
    {
        Record = record;
        Exists = record.FileExists;
    }

    public BackupRecord Record { get; }

    public string FileName => Record.FileName;

    public string DateText => Record.CreatedAt.ToString(Tr("dd/MM/yyyy HH:mm:ss"), PtBr);

    public string SizeText => Record.SizeBytes.ToString("N0", PtBr);

    public string Sha256 => Record.Sha256;

    public string ShortHash => Record.Sha256.Length > 16 ? Record.Sha256[..16] + "…" : Record.Sha256;

    public string Notes => Record.Notes;

    public string Origin => Tr(Record.Origin);

    public bool Exists { get; }

    public string StatusText => Exists ? "OK" : Tr("Arquivo ausente");
}

public sealed class LibraryViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly BackupLibraryService _library;
    private readonly IDialogService _dialogs;
    private string _searchText = string.Empty;
    private BackupRow? _selected;
    private IReadOnlyList<BackupRow> _selection = Array.Empty<BackupRow>();
    private string _comparisonText = Tr("Selecione dois backups (Ctrl+clique) e clique em \"Comparar\".");
    private string _countText = string.Empty;

    public LibraryViewModel(AppState state, BackupLibraryService library, IDialogService dialogs)
    {
        _state = state;
        _library = library;
        _dialogs = dialogs;

        RefreshCommand = new RelayCommand(Refresh);
        ImportCommand = new RelayCommand(Import, () => !_state.IsBusy);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        ViewCommand = new RelayCommand(() => ViewRequested?.Invoke(this, Selected!.Record.FilePath), () => Selected?.Exists == true);
        EditCommand = new RelayCommand(() => EditRequested?.Invoke(this, Selected!.Record.FilePath), () => Selected?.Exists == true);
        RestoreCommand = new RelayCommand(() => RestoreRequested?.Invoke(this, Selected!.Record.FilePath), () => !_state.IsBusy && Selected?.Exists == true);
        DuplicateCommand = new RelayCommand(Duplicate, () => !_state.IsBusy && Selected?.Exists == true);
        RenameCommand = new RelayCommand(Rename, () => !_state.IsBusy && Selected?.Exists == true);
        EditNotesCommand = new RelayCommand(EditNotes, () => Selected is not null);
        DeleteCommand = new RelayCommand(Delete, () => !_state.IsBusy && Selected is not null);
        ExportCsvCommand = new RelayCommand(ExportCsv, () => Items.Count > 0);
        CompareCommand = new RelayCommand(Compare, () => _selection.Count == 2 && _selection.All(r => r.Exists));
    }

    public event EventHandler<string>? RestoreRequested;

    public event EventHandler<string>? ViewRequested;

    public event EventHandler<string>? EditRequested;

    public ObservableCollection<BackupRow> Items { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                Refresh();
            }
        }
    }

    public BackupRow? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public string ComparisonText
    {
        get => _comparisonText;
        private set => SetProperty(ref _comparisonText, value);
    }

    public string CountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
    }

    public ICommand RefreshCommand { get; }

    public ICommand ImportCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public ICommand ViewCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand RestoreCommand { get; }

    public ICommand DuplicateCommand { get; }

    public ICommand RenameCommand { get; }

    public ICommand EditNotesCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand ExportCsvCommand { get; }

    public ICommand CompareCommand { get; }

    /// <summary>Chamado pela View quando a seleção múltipla do DataGrid muda.</summary>
    public void SetSelection(IEnumerable<BackupRow> rows)
    {
        _selection = rows.ToList();
        CommandManager.InvalidateRequerySuggested();
    }

    public void Refresh()
    {
        var selectedId = Selected?.Record.Id;
        Items.Clear();
        foreach (var record in _library.Search(SearchText))
        {
            Items.Add(new BackupRow(record));
        }

        Selected = Items.FirstOrDefault(r => r.Record.Id == selectedId);
        CountText = Items.Count == 1 ? Tr("1 backup") : TrF($"{Items.Count} backups");
    }

    private void Import()
    {
        var path = _dialogs.OpenSyxFile(_state.Settings.BackupFolder);
        if (path is null)
        {
            return;
        }

        var notes = _dialogs.PromptText(Tr("Importar backup"), Tr("Observação (opcional):"), string.Empty);
        if (notes is null)
        {
            return;
        }

        try
        {
            var record = _library.Import(path, _state.Settings.BackupFolder, notes);
            Refresh();
            Selected = Items.FirstOrDefault(r => r.Record.Id == record.Id);
            _state.StatusText = Tr("Importado: ") + record.FileName;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(Tr("Importar"), ex.Message);
        }
    }

    private void OpenFolder()
    {
        var path = Selected?.Exists == true ? Selected.Record.FilePath : _state.Settings.BackupFolder;
        Directory.CreateDirectory(_state.Settings.BackupFolder);
        AppState.OpenInExplorer(path);
    }

    private void Duplicate()
    {
        var copy = _library.Duplicate(Selected!.Record);
        Refresh();
        Selected = Items.FirstOrDefault(r => r.Record.Id == copy.Id);
        _state.StatusText = Tr("Duplicado: ") + copy.FileName;
    }

    private void Rename()
    {
        var current = Selected!.Record;
        var newName = _dialogs.PromptText(Tr("Renomear backup"), Tr("Novo nome do arquivo (.syx):"), Path.GetFileNameWithoutExtension(current.FileName));
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        try
        {
            var updated = _library.Rename(current, newName);
            Refresh();
            Selected = Items.FirstOrDefault(r => r.Record.Id == updated.Id);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(Tr("Renomear"), ex.Message);
        }
    }

    private void EditNotes()
    {
        var current = Selected!.Record;
        var notes = _dialogs.PromptText(Tr("Observação"), Tr("Observação deste backup:"), current.Notes);
        if (notes is null)
        {
            return;
        }

        _library.UpdateNotes(current, notes);
        Refresh();
    }

    private void Delete()
    {
        var record = Selected!.Record;
        var answer = _dialogs.AskYesNoCancel(Tr("Excluir backup"),
            TrF($"Excluir \"{record.FileName}\"?\n\n") +
            Tr("Sim — remove da biblioteca e envia o arquivo para a Lixeira do Windows (recuperável).\n") +
            Tr("Não — remove só da biblioteca e mantém o arquivo no disco.\n") +
            Tr("Cancelar — não faz nada."));
        if (answer is null)
        {
            return;
        }

        try
        {
            _library.Delete(record, moveFileToRecycleBin: answer.Value);
            Refresh();
            _state.StatusText = answer.Value ? Tr("Backup enviado para a Lixeira: ") + record.FileName : Tr("Backup removido da biblioteca: ") + record.FileName;
        }
        catch (IOException ex)
        {
            _dialogs.ShowError(Tr("Excluir"), ex.Message);
        }
    }

    private void ExportCsv()
    {
        var path = _dialogs.SaveCsvFile(_state.Settings.BackupFolder, $"TriAxis_Biblioteca_{DateTime.Now:yyyy-MM-dd}.csv");
        if (path is null)
        {
            return;
        }

        _library.ExportCsv(Items.Select(r => r.Record), path);
        _state.StatusText = Tr("Biblioteca exportada: ") + path;
    }

    private void Compare()
    {
        var a = _selection[0].Record;
        var b = _selection[1].Record;
        var result = _library.Compare(a, b);

        var sb = new StringBuilder();
        sb.AppendLine(TrF($"A: {a.FileName}"));
        sb.AppendLine(TrF($"   {result.LengthA} bytes · SHA-256 {result.HashA}"));
        sb.AppendLine(TrF($"B: {b.FileName}"));
        sb.AppendLine(TrF($"   {result.LengthB} bytes · SHA-256 {result.HashB}"));
        sb.AppendLine();

        if (result.Identical)
        {
            sb.AppendLine(Tr("RESULTADO: IDÊNTICOS (mesmo hash SHA-256)."));
        }
        else
        {
            sb.AppendLine(TrF($"RESULTADO: DIFERENTES — {result.DifferenceCount} byte(s) diferem."));
            if (result.UsesTriAxisLayout)
            {
                sb.AppendLine(result.ChangedPresets.Count > 0
                    ? Tr("Presets com diferenças (mapa preliminar, não validado): ") + string.Join(", ", result.ChangedPresets.Select(p => p.ToString("00")))
                    : Tr("Nenhuma diferença na região de presets (mapa preliminar)."));
            }

            sb.AppendLine();
            sb.AppendLine(Tr("Offset  A  → B   Região") + (result.UsesTriAxisLayout ? Tr(" (mapa preliminar)") : string.Empty));
            foreach (var d in result.Differences)
            {
                sb.AppendLine(d.ToString());
            }

            if (result.DifferencesTruncated)
            {
                sb.AppendLine(TrF($"... e mais {result.DifferenceCount - result.Differences.Count} diferenças."));
            }
        }

        ComparisonText = sb.ToString();
    }
}
