using System.Globalization;
using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.App.ViewModels;

public sealed class DumpViewerViewModel : ObservableObject
{
    public const string EditorDisabledMessage = "Visualização somente leitura. Para editar, use a aba Editor (mapa preliminar, não validado em hardware; as edições geram um novo arquivo).";

    private const int MaxHexBytes = 256 * 1024;
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly AppState _state;
    private readonly ITriAxisSysExService _sysEx;
    private readonly IDialogService _dialogs;
    private string _filePath = string.Empty;
    private string _sizeText = string.Empty;
    private string _hashText = string.Empty;
    private string _headerText = string.Empty;
    private string _validationSummary = "Abra um arquivo .syx para visualizar.";
    private StatusKind _validationKind = StatusKind.Idle;
    private IReadOnlyList<string> _issues = Array.Empty<string>();
    private string _hexText = string.Empty;
    private IReadOnlyList<TriAxisPresetView> _presets = Array.Empty<TriAxisPresetView>();
    private IReadOnlyList<TriAxisProgramMapping> _programs = Array.Empty<TriAxisProgramMapping>();
    private bool _hasParsedDump;
    private string _parseMessage = string.Empty;

    public DumpViewerViewModel(AppState state, ITriAxisSysExService sysEx, IDialogService dialogs)
    {
        _state = state;
        _sysEx = sysEx;
        _dialogs = dialogs;
        OpenFileCommand = new RelayCommand(OpenFile);
        OpenInEditorCommand = new RelayCommand(() => EditRequested?.Invoke(this, FilePath), () => HasParsedDump);
    }

    public string FilePath { get => _filePath; private set => SetProperty(ref _filePath, value); }

    public string SizeText { get => _sizeText; private set => SetProperty(ref _sizeText, value); }

    public string HashText { get => _hashText; private set => SetProperty(ref _hashText, value); }

    public string HeaderText { get => _headerText; private set => SetProperty(ref _headerText, value); }

    public string ValidationSummary { get => _validationSummary; private set => SetProperty(ref _validationSummary, value); }

    public StatusKind ValidationKind { get => _validationKind; private set => SetProperty(ref _validationKind, value); }

    public IReadOnlyList<string> Issues { get => _issues; private set => SetProperty(ref _issues, value); }

    public string HexText { get => _hexText; private set => SetProperty(ref _hexText, value); }

    public IReadOnlyList<TriAxisPresetView> Presets { get => _presets; private set => SetProperty(ref _presets, value); }

    public IReadOnlyList<TriAxisProgramMapping> Programs { get => _programs; private set => SetProperty(ref _programs, value); }

    public bool HasParsedDump { get => _hasParsedDump; private set => SetProperty(ref _hasParsedDump, value); }

    public string ParseMessage { get => _parseMessage; private set => SetProperty(ref _parseMessage, value); }

    public string EditorMessage => EditorDisabledMessage;

    public string MapSourceText => $"Mapa preliminar — fonte: {TriAxisDumpLayout.Source}. Somente leitura.";

    public ICommand OpenFileCommand { get; }

    public ICommand OpenInEditorCommand { get; }

    public event EventHandler<string>? EditRequested;

    public void Load(string path)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Visualizar dump", "Não foi possível ler o arquivo: " + ex.Message);
            return;
        }

        FilePath = path;
        SizeText = data.Length.ToString("N0", PtBr) + " bytes";
        HashText = _sysEx.ComputeSha256(data);
        HeaderText = HexFormatter.ToHex(data.AsSpan(0, Math.Min(16, data.Length)));

        var validation = _sysEx.Validate(data);
        ValidationSummary = validation.Summary;
        ValidationKind = validation.HasErrors ? StatusKind.Error : validation.HasWarnings ? StatusKind.Warning : StatusKind.Ok;
        Issues = validation.Issues.Select(i => i.ToString()).ToList();

        HexText = data.Length <= MaxHexBytes
            ? _sysEx.FormatHexDump(data)
            : _sysEx.FormatHexDump(data[..MaxHexBytes]) + $"\n... exibindo os primeiros {MaxHexBytes:N0} bytes de {data.Length:N0}.";

        if (_sysEx.TryParseDump(data, out var dump, out var reason))
        {
            Presets = dump!.Presets;
            Programs = dump.ProgramMap;
            HasParsedDump = true;
            var unknown = dump.Presets.Count(p => p.HasUnknownValues);
            ParseMessage = $"{dump.Presets.Count} presets e {dump.ProgramMap.Count} programas MIDI interpretados pelo mapa preliminar." +
                           (unknown > 0 ? $" {unknown} preset(s) têm valores fora das tabelas conhecidas (marcados com ?)." : string.Empty);
        }
        else
        {
            Presets = Array.Empty<TriAxisPresetView>();
            Programs = Array.Empty<TriAxisProgramMapping>();
            HasParsedDump = false;
            ParseMessage = "Estrutura de presets não identificada com segurança: exibindo apenas o hex dump. " + reason;
        }
    }

    private void OpenFile()
    {
        var path = _dialogs.OpenSyxFile(_state.Settings.BackupFolder);
        if (path is not null)
        {
            Load(path);
        }
    }
}
