using System.Globalization;
using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Application.Restore;
using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.App.ViewModels;

public sealed class RestoreViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly AppState _state;
    private readonly RestoreService _restore;
    private readonly BackupViewModel _backup;
    private readonly IDialogService _dialogs;
    private readonly ITechLog _log;
    private CancellationTokenSource? _cts;
    private RestorePlan? _plan;
    private string _filePath = string.Empty;
    private IReadOnlyList<string> _issues = Array.Empty<string>();
    private bool _isSending;
    private double _progressPercent;
    private string _progressText = string.Empty;
    private string _statusText = "Selecione um arquivo .syx para restaurar.";
    private StatusKind _statusKind = StatusKind.Idle;
    private bool _showDisplayCheck;

    public RestoreViewModel(AppState state, RestoreService restore, BackupViewModel backup, IDialogService dialogs, ITechLog log)
    {
        _state = state;
        _restore = restore;
        _backup = backup;
        _dialogs = dialogs;
        _log = log;

        SelectFileCommand = new RelayCommand(SelectFile, () => !_state.IsBusy);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => !_state.IsBusy && _plan?.CanRestore == true && _state.SelectedOutput is not null);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsSending);
    }

    public string FilePath
    {
        get => _filePath;
        private set => SetProperty(ref _filePath, value);
    }

    public bool HasFile => _plan is not null;

    public string SizeText => _plan is null ? string.Empty : _plan.Data.Length.ToString("N0", PtBr) + " bytes";

    public string HashText => _plan?.Sha256 ?? string.Empty;

    public string HeaderText => _plan is null ? string.Empty : HexFormatter.ToHex(_plan.Data.AsSpan(0, Math.Min(16, _plan.Data.Length)));

    public string MessagesText => _plan is null ? string.Empty : _plan.Validation.Messages.Count.ToString(PtBr);

    public string ValidationSummary => _plan?.Validation.Summary ?? string.Empty;

    public StatusKind ValidationKind => _plan is null
        ? StatusKind.Idle
        : _plan.Validation.HasErrors ? StatusKind.Error : _plan.Validation.HasWarnings ? StatusKind.Warning : StatusKind.Ok;

    public IReadOnlyList<string> Issues
    {
        get => _issues;
        private set => SetProperty(ref _issues, value);
    }

    public int ChunkSize
    {
        get => _state.Settings.ChunkSize;
        set
        {
            var clamped = Math.Clamp(value, AppSettings.MinChunkSize, AppSettings.MaxChunkSize);
            if (_state.Settings.ChunkSize != clamped)
            {
                _state.Settings.ChunkSize = clamped;
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public int ChunkDelayMs
    {
        get => _state.Settings.ChunkDelayMs;
        set
        {
            var clamped = Math.Clamp(value, AppSettings.MinChunkDelayMs, AppSettings.MaxChunkDelayMs);
            if (_state.Settings.ChunkDelayMs != clamped)
            {
                _state.Settings.ChunkDelayMs = clamped;
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public int MinChunkDelay => AppSettings.MinChunkDelayMs;

    public int MaxChunkDelay => AppSettings.MaxChunkDelayMs;

    public bool IsSending
    {
        get => _isSending;
        private set => SetProperty(ref _isSending, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
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

    /// <summary>Lembrete exibido após o envio: o display do TriAxis deve piscar.</summary>
    public bool ShowDisplayCheck
    {
        get => _showDisplayCheck;
        private set => SetProperty(ref _showDisplayCheck, value);
    }

    public ICommand SelectFileCommand { get; }

    public ICommand RestoreCommand { get; }

    public ICommand CancelCommand { get; }

    public void NotifySettingsChanged()
    {
        OnPropertyChanged(nameof(ChunkSize));
        OnPropertyChanged(nameof(ChunkDelayMs));
    }

    /// <summary>Carrega e valida um arquivo (também usado pela Biblioteca). Nada é enviado aqui.</summary>
    public void LoadFile(string path)
    {
        ShowDisplayCheck = false;
        ProgressPercent = 0;
        ProgressText = string.Empty;
        try
        {
            _plan = _restore.Prepare(path);
            FilePath = _plan.FilePath;
            Issues = _plan.Validation.Issues.Select(i => i.ToString()).ToList();
            if (_plan.CanRestore)
            {
                SetStatus(_plan.Validation.HasWarnings
                        ? "Arquivo válido, com avisos. A restauração exigirá que você reconheça os avisos."
                        : "Arquivo válido. Pronto para restaurar.",
                    _plan.Validation.HasWarnings ? StatusKind.Warning : StatusKind.Ok);
            }
            else
            {
                SetStatus("Arquivo inválido: a restauração está bloqueada. Veja os problemas abaixo.", StatusKind.Error);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _plan = null;
            FilePath = path;
            Issues = Array.Empty<string>();
            SetStatus("Não foi possível ler o arquivo: " + ex.Message, StatusKind.Error);
        }

        RaiseFileProperties();
    }

    private void SelectFile()
    {
        var path = _dialogs.OpenSyxFile(_state.Settings.BackupFolder);
        if (path is not null)
        {
            LoadFile(path);
        }
    }

    private async Task RestoreAsync()
    {
        if (_plan is null)
        {
            return;
        }

        var output = _state.SelectedOutput;
        if (output is null)
        {
            _dialogs.ShowWarning("Restaurar", "Selecione o MIDI OUT conectado ao MIDI IN do TriAxis na aba Conexão.");
            return;
        }

        // Revalida o arquivo imediatamente antes de pedir a confirmação.
        LoadFile(_plan.FilePath);
        if (_plan is not { CanRestore: true } plan)
        {
            return;
        }

        var confirmation = await AskConfirmationAsync(plan, output);
        if (confirmation is null)
        {
            SetStatus("Restauração cancelada. Nada foi enviado ao TriAxis.", StatusKind.Idle);
            return;
        }

        if (!_state.TryBeginOperation("Restauração"))
        {
            return;
        }

        _cts = new CancellationTokenSource();
        IsSending = true;
        ShowDisplayCheck = false;
        ProgressPercent = 0;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            SetStatus("Enviando ao TriAxis... não desligue o equipamento nem desconecte o cabo.", StatusKind.Warning);
            var progress = new Progress<TransmitProgress>(p =>
            {
                ProgressPercent = p.Percent;
                ProgressText = $"{p.BytesSent.ToString("N0", PtBr)} de {p.TotalBytes.ToString("N0", PtBr)} bytes";
            });

            var result = await _restore.RestoreAsync(plan, confirmation, output, new TransmitOptions(ChunkSize, ChunkDelayMs), progress, _cts.Token);

            if (result.Status == TransmitStatus.Completed)
            {
                SetStatus(result.Message, StatusKind.Ok);
                ShowDisplayCheck = true;
                _state.StatusText = "Restauração enviada: " + plan.FileName;
                _dialogs.ShowInfo("Envio concluído",
                    $"{plan.FileName} foi enviado ({result.BytesSent.ToString("N0", PtBr)} bytes).\n\n" +
                    "Confira agora se o display do TriAxis piscou, confirmando o recebimento.\n" +
                    "Se não piscou: verifique o cabo (interface MIDI OUT → TriAxis MIDI IN) e tente com um intervalo maior entre blocos.");
            }
            else
            {
                SetStatus(result.Message, StatusKind.Warning);
            }
        }
        catch (RestoreBlockedException ex)
        {
            SetStatus(ex.Message, StatusKind.Error);
            _dialogs.ShowError("Restauração bloqueada", ex.Message);
        }
        catch (MidiException ex)
        {
            SetStatus("Falha MIDI: " + ex.Message, StatusKind.Error);
            _log.Error("Restauração: falha MIDI.", ex);
            _dialogs.ShowError("Falha na restauração",
                ex.Message + "\n\nO envio pode ter sido parcial. Não desligue o TriAxis: corrija a conexão e repita a restauração completa.");
        }
        finally
        {
            IsSending = false;
            _cts.Dispose();
            _cts = null;
            _state.EndOperation();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task<RestoreConfirmation?> AskConfirmationAsync(RestorePlan plan, MidiDeviceInfo output)
    {
        string? safetyBackupPath = null;
        while (true)
        {
            var context = new RestoreDialogContext
            {
                FileName = plan.FileName,
                SizeBytes = plan.Data.Length,
                Sha256 = plan.Sha256,
                OutputDevice = output.DisplayName,
                Warnings = WarningsFor(plan),
                SafetyBackupPath = safetyBackupPath,
                CanMakeSafetyBackup = _state.HasMidiSelection
            };

            var answer = _dialogs.ShowRestoreConfirmation(context);
            switch (answer.Outcome)
            {
                case RestoreDialogOutcome.SafetyBackupRequested:
                    var record = await _backup.RunBackupAsync($"Backup de segurança antes de restaurar {plan.FileName}");
                    if (record is not null)
                    {
                        safetyBackupPath = record.FilePath;
                    }

                    continue;

                case RestoreDialogOutcome.Confirmed:
                    return new RestoreConfirmation
                    {
                        OverwriteAcknowledged = answer.OverwriteAcknowledged,
                        TypedPhrase = answer.TypedPhrase,
                        WarningsAcknowledged = answer.WarningsAcknowledged
                    };

                default:
                    return null;
            }
        }
    }

    private static List<string> WarningsFor(RestorePlan plan)
    {
        var warnings = plan.Validation.Issues.Where(i => i.Severity == IssueSeverity.Warning).Select(i => i.Message).ToList();
        if (BackupFileNamer.IsEditedName(plan.FilePath))
        {
            warnings.Add("Este arquivo foi gerado pelo Editor a partir do mapa SysEx PRELIMINAR (não validado em hardware). " +
                         "Tenha à mão o backup original para voltar atrás se algo não soar como esperado.");
        }

        return warnings;
    }

    private void RaiseFileProperties()
    {
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(HashText));
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(MessagesText));
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(ValidationKind));
        CommandManager.InvalidateRequerySuggested();
    }

    private void SetStatus(string text, StatusKind kind)
    {
        StatusText = text;
        StatusKind = kind;
    }
}
