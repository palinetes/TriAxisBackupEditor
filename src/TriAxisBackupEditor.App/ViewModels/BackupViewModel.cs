using System.Globalization;
using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.App.ViewModels;

public sealed class BackupViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly AppState _state;
    private readonly BackupCaptureService _capture;
    private readonly BackupLibraryService _library;
    private readonly ITriAxisSysExService _sysEx;
    private readonly IDialogService _dialogs;
    private readonly ITechLog _log;
    private CancellationTokenSource? _cts;
    private string _notes = string.Empty;
    private bool _isCapturing;
    private double _progressPercent;
    private string _bytesText = "0 bytes";
    private string _statusText = "Pronto para fazer backup.";
    private StatusKind _statusKind = StatusKind.Idle;
    private string _instruction = string.Empty;
    private string? _lastFilePath;
    private string _lastHash = string.Empty;

    public BackupViewModel(AppState state, BackupCaptureService capture, BackupLibraryService library, ITriAxisSysExService sysEx, IDialogService dialogs, ITechLog log)
    {
        _state = state;
        _capture = capture;
        _library = library;
        _sysEx = sysEx;
        _dialogs = dialogs;
        _log = log;

        StartCommand = new AsyncRelayCommand(() => RunBackupAsync(null), () => !_state.IsBusy && _state.HasMidiSelection);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsCapturing);
        ChooseFolderCommand = new RelayCommand(ChooseFolder, () => !_state.IsBusy);
        OpenFolderCommand = new RelayCommand(() => AppState.OpenInExplorer(EnsureFolder()));
        OpenLastFileCommand = new RelayCommand(() => AppState.OpenInExplorer(_lastFilePath!), () => _lastFilePath is not null);
    }

    /// <summary>Disparado depois que um backup completo foi salvo e registrado.</summary>
    public event EventHandler<BackupRecord>? BackupSaved;

    public string BackupFolder => _state.Settings.BackupFolder;

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        private set => SetProperty(ref _isCapturing, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string BytesText
    {
        get => _bytesText;
        private set => SetProperty(ref _bytesText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public StatusKind StatusKind
    {
        get => _statusKind;
        private set => SetProperty(ref _statusKind, value);
    }

    /// <summary>Instrução grande exibida enquanto aguarda o dump.</summary>
    public string Instruction
    {
        get => _instruction;
        private set => SetProperty(ref _instruction, value);
    }

    public string? LastFilePath
    {
        get => _lastFilePath;
        private set => SetProperty(ref _lastFilePath, value);
    }

    public string LastHash
    {
        get => _lastHash;
        private set => SetProperty(ref _lastHash, value);
    }

    public string ExpectedSizeText => $"Tamanho esperado (mapa preliminar): {TriAxisDumpLayout.TotalLength.ToString("N0", PtBr)} bytes";

    public ICommand StartCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand ChooseFolderCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public ICommand OpenLastFileCommand { get; }

    public void NotifySettingsChanged() => OnPropertyChanged(nameof(BackupFolder));

    /// <summary>Executa o backup completo. Também usado pela restauração para o backup de segurança.</summary>
    public async Task<BackupRecord?> RunBackupAsync(string? notesOverride)
    {
        if (_state.SelectedInput is null || _state.SelectedOutput is null)
        {
            _dialogs.ShowWarning("Backup", "Selecione o MIDI IN e o MIDI OUT na aba Conexão antes de fazer o backup.");
            return null;
        }

        byte[]? request = null;
        if (_state.Settings.DumpRequestEnabled)
        {
            try
            {
                request = _sysEx.ParseRequestMessage(_state.Settings.DumpRequestHex);
            }
            catch (FormatException ex)
            {
                _dialogs.ShowError("Requisição de dump inválida", ex.Message + "\n\nCorrija ou desative a requisição na aba Configurações.");
                return null;
            }
        }

        string folder;
        try
        {
            folder = EnsureFolder();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _dialogs.ShowError("Pasta de backups", $"Não foi possível usar a pasta \"{BackupFolder}\": {ex.Message}\nEscolha outra pasta.");
            return null;
        }

        if (!_state.TryBeginOperation("Backup", () => _cts?.Cancel()))
        {
            return null;
        }

        _cts = new CancellationTokenSource();
        IsCapturing = true;
        ProgressPercent = 0;
        BytesText = "0 bytes";
        LastFilePath = null;
        LastHash = string.Empty;
        SetStatus("Preparando...", StatusKind.Idle);
        Instruction = request is null
            ? "Pressione SHIFT + PRESET DOWN no painel do TriAxis"
            : "Requisição enviada — aguardando o TriAxis";
        CommandManager.InvalidateRequerySuggested();

        try
        {
            var options = new CaptureOptions(
                _state.SelectedInput,
                _state.SelectedOutput,
                TimeSpan.FromSeconds(_state.Settings.StartTimeoutSeconds),
                TimeSpan.FromSeconds(_state.Settings.ReceiveTimeoutSeconds),
                TimeSpan.FromMilliseconds(_state.Settings.PostDumpIdleMilliseconds),
                request);

            var progress = new Progress<CaptureProgress>(p =>
            {
                ProgressPercent = p.Percent;
                BytesText = p.BytesReceived.ToString("N0", PtBr) + " bytes recebidos";
                if (p.Phase == CapturePhase.Done)
                {
                    return; // o resultado final é mostrado por HandleResult
                }

                SetStatus(p.Message, StatusKind.Idle);
                if (p.Phase is CapturePhase.Receiving or CapturePhase.Finishing)
                {
                    Instruction = "Recebendo dump... não desligue o TriAxis";
                }
            });

            var result = await _capture.CaptureAsync(options, progress, _cts.Token);
            Instruction = string.Empty;
            return HandleResult(result, folder, notesOverride ?? Notes);
        }
        finally
        {
            IsCapturing = false;
            Instruction = string.Empty;
            _cts.Dispose();
            _cts = null;
            _state.EndOperation();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private BackupRecord? HandleResult(CaptureResult result, string folder, string notes)
    {
        var timestamp = DateTime.Now;
        switch (result.Status)
        {
            case CaptureStatus.Completed:
            {
                BackupRecord record;
                try
                {
                    record = _library.SaveCapturedBackup(result.Data, folder, notes, timestamp);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // O dump recebido não pode se perder: tenta a pasta de dados local do programa.
                    var fallback = Path.Combine(_state.Paths.DataFolder, "Recuperacao");
                    _log.Error($"Falha ao gravar em \"{folder}\". Tentando \"{fallback}\".", ex);
                    record = _library.SaveCapturedBackup(result.Data, fallback, notes, timestamp);
                    _dialogs.ShowWarning("Backup salvo em pasta alternativa",
                        $"Não foi possível gravar em \"{folder}\" ({ex.Message}).\nO backup foi salvo em:\n{record.FilePath}");
                }

                ProgressPercent = 100;
                LastFilePath = record.FilePath;
                LastHash = record.Sha256;
                Notes = string.Empty;

                var validation = _sysEx.Validate(result.Data);
                var warnings = validation.Issues.Where(i => i.Severity == IssueSeverity.Warning).Select(i => "• " + i.Message).ToList();
                var discarded = result.RealTimeBytesDiscarded + result.StrayBytesDiscarded;

                SetStatus($"Backup salvo: {record.FileName} ({record.SizeBytes.ToString("N0", PtBr)} bytes).", warnings.Count > 0 ? StatusKind.Warning : StatusKind.Ok);
                _state.StatusText = "Backup concluído e verificado: " + record.FileName;
                BackupSaved?.Invoke(this, record);

                if (warnings.Count > 0 || discarded > 0)
                {
                    var text = "O backup foi salvo exatamente como recebido, mas há observações:\n\n" + string.Join("\n", warnings);
                    if (discarded > 0)
                    {
                        text += $"\n• {discarded} byte(s) fora do SysEx (clock/active sensing/outras mensagens) foram ignorados na captura.";
                    }

                    _dialogs.ShowWarning("Backup salvo com observações", text);
                }

                return record;
            }

            case CaptureStatus.Cancelled or CaptureStatus.Incomplete when result.RawPartialData.Length > 0:
            {
                var path = _library.SaveIncomplete(result.RawPartialData, folder, timestamp);
                LastFilePath = path;
                SetStatus($"BACKUP INCOMPLETO: {result.Message}", StatusKind.Error);
                _state.StatusText = "Backup incompleto — salvo apenas como arquivo temporário.";
                _dialogs.ShowWarning("Backup incompleto",
                    $"{result.Message}\n\nForam recebidos {result.BytesReceived.ToString("N0", PtBr)} bytes, sem o final F7.\n" +
                    $"Eles foram salvos SOMENTE como arquivo temporário (não entra na biblioteca e não pode ser restaurado):\n{path}\n\n" +
                    "Tente novamente. Se repetir, aumente o tempo limite de recepção ou troque o cabo/interface.");
                return null;
            }

            case CaptureStatus.Cancelled:
                SetStatus("Backup cancelado. Nenhum dado foi recebido.", StatusKind.Warning);
                return null;

            case CaptureStatus.NoDataReceived:
                SetStatus(result.Message, StatusKind.Error);
                _dialogs.ShowWarning("Nenhum dado recebido",
                    result.Message + "\n\nVerifique:\n• O cabo sai do MIDI OUT do TriAxis e entra no MIDI IN da interface.\n" +
                    "• A porta MIDI IN selecionada é a da interface correta.\n• Nenhum outro programa está usando a interface.");
                return null;

            default:
                SetStatus("Falha: " + result.Message, StatusKind.Error);
                _dialogs.ShowError("Falha no backup", result.Message);
                return null;
        }
    }

    private void ChooseFolder()
    {
        var folder = _dialogs.ChooseFolder(BackupFolder);
        if (folder is null)
        {
            return;
        }

        _state.Settings.BackupFolder = folder;
        _state.SaveSettings();
        _log.Info($"Pasta de backups alterada para \"{folder}\".");
        OnPropertyChanged(nameof(BackupFolder));
    }

    private string EnsureFolder()
    {
        var folder = _state.Settings.BackupFolder;
        Directory.CreateDirectory(folder);
        return folder;
    }

    private void SetStatus(string text, StatusKind kind)
    {
        StatusText = text;
        StatusKind = kind;
    }
}
