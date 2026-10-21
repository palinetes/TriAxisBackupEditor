using System.Globalization;
using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.SysEx;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels;

public sealed class DumpViewerViewModel : ObservableObject
{
    public const string EditorDisabledMessage = "Visualização somente leitura. Para editar, use a aba Editor (mapa preliminar, não validado em hardware; as edições geram um novo arquivo).";

    private const int MaxHexBytes = 256 * 1024;
    private static CultureInfo PtBr => CultureInfo.CurrentCulture;

    private readonly AppState _state;
    private readonly ITriAxisSysExService _sysEx;
    private readonly IDialogService _dialogs;
    private string _filePath = string.Empty;
    private string _sizeText = string.Empty;
    private string _hashText = string.Empty;
    private string _headerText = string.Empty;
    private string _validationSummary = Tr("Abra um arquivo .syx para visualizar.");
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

    public string EditorMessage => Tr(EditorDisabledMessage);

    public string MapSourceText => TrF($"Mapa preliminar — fonte: {Tr(TriAxisDumpLayout.Source)}. Somente leitura.");

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
            _dialogs.ShowError(Tr("Visualizar dump"), Tr("Não foi possível ler o arquivo: ") + ex.Message);
            return;
        }

        FilePath = path;
        SizeText = data.Length.ToString("N0", PtBr) + Tr(" bytes");
        HashText = _sysEx.ComputeSha256(data);
        HeaderText = HexFormatter.ToHex(data.AsSpan(0, Math.Min(16, data.Length)));

        // Arquivos do editor web: valida e interpreta a parte válida; o hex continua mostrando o arquivo inteiro.
        var content = TriAxisDumpFile.Normalize(data);
        var validation = _sysEx.Validate(content.Data);
        ValidationSummary = content.WasPadded ? Tr("Arquivo do editor web: a parte válida (dump do TriAxis) está íntegra.") : validation.Summary;
        ValidationKind = validation.HasErrors ? StatusKind.Error : validation.HasWarnings ? StatusKind.Warning : StatusKind.Ok;
        var issues = validation.Issues.Select(i => i.ToString()).ToList();
        if (content.Note is not null)
        {
            issues.Insert(0, content.Note);
        }

        Issues = issues;

        HexText = data.Length <= MaxHexBytes
            ? _sysEx.FormatHexDump(data)
            : _sysEx.FormatHexDump(data[..MaxHexBytes]) + TrF($"\n... exibindo os primeiros {MaxHexBytes:N0} bytes de {data.Length:N0}.");

        if (_sysEx.TryParseDump(content.Data, out var dump, out var reason))
        {
            Presets = dump!.Presets;
            Programs = dump.ProgramMap;
            HasParsedDump = true;
            var unknown = dump.Presets.Count(p => p.HasUnknownValues);
            ParseMessage = TrF($"{dump.Presets.Count} presets interpretados.") +
                           (unknown > 0 ? TrF($" {unknown} preset(s) têm valores fora das tabelas conhecidas (marcados com ?).") : string.Empty) +
                           (dump.ProgramMapLooksValid ? string.Empty : Tr(" A aba Programas MIDI mostra os bytes crus: neste arquivo eles não parecem um mapa programa → timbre (em estudo)."));
        }
        else
        {
            Presets = Array.Empty<TriAxisPresetView>();
            Programs = Array.Empty<TriAxisProgramMapping>();
            HasParsedDump = false;
            ParseMessage = Tr("Estrutura de presets não identificada com segurança: exibindo apenas o hex dump. ") + reason;
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
