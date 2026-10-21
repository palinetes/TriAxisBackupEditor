using System.Globalization;
using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Application.Restore;
using TriAxisBackupEditor.Midi;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels.Simple;

public enum SafeSendStep
{
    Blocked,
    Warning,
    SafetyBackup,
    Confirm,
    Sending,
    Done,
    Failed
}

/// <summary>
/// "Mandar para o TriAxis" no modo simples: aviso → backup de segurança obrigatório → "Sim, substituir" → envio →
/// "o display piscou?". As regras ficam em <see cref="SafeReplaceFlow"/>; o RestoreService revalida tudo.
/// </summary>
public sealed class SafeSendViewModel : SimplePage
{
    private static CultureInfo PtBr => CultureInfo.CurrentCulture;

    private readonly AppState _state;
    private readonly RestoreService _restore;
    private readonly ITechLog _log;
    private readonly SimplePage _returnTo;
    private readonly SafeReplaceFlow? _flow;
    private SafeSendStep _step;
    private double _percent;
    private string _message = string.Empty;
    private bool _showNoBlinkHelp;

    public SafeSendViewModel(SimpleShellViewModel shell, AppState state, RestoreService restore, ITechLog log, string filePath, string description, SimplePage returnTo)
        : base(shell)
    {
        _state = state;
        _restore = restore;
        _log = log;
        _returnTo = returnTo;
        Description = description;

        SafetyCard = shell.NewCaptureCard(Tr("Backup de segurança (automático antes de mandar timbres)"));
        SafetyCard.Completed += (_, record) =>
        {
            _flow?.RegisterSafetyBackup(record);
            Step = SafeSendStep.Confirm;
        };

        ContinueCommand = new RelayCommand(Continue, () => Step == SafeSendStep.Warning);
        ReplaceCommand = new AsyncRelayCommand(ReplaceAsync, () => Step is SafeSendStep.Confirm or SafeSendStep.Failed && !_state.IsBusy);
        BackCommand = new RelayCommand(() => Shell.Navigate(_returnTo), () => CanLeave);
        HomeCommand = new RelayCommand(Shell.GoHome, () => CanLeave);
        NoBlinkCommand = new RelayCommand(() => ShowNoBlinkHelp = true);
        RetryCommand = new AsyncRelayCommand(ReplaceAsync, () => Step == SafeSendStep.Done && !_state.IsBusy);

        try
        {
            _flow = new SafeReplaceFlow(restore.Prepare(filePath));
            if (_flow.IsAllowed)
            {
                _step = SafeSendStep.Warning;
            }
            else
            {
                _step = SafeSendStep.Blocked;
                _message = _flow.BlockReason!;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _step = SafeSendStep.Blocked;
            _message = Tr("Não foi possível ler o arquivo: ") + ex.Message;
        }
    }

    public override string Title => Tr("Mandar para o TriAxis");

    public override bool CanLeave => Step != SafeSendStep.Sending && !SafetyCard.IsRunning;

    /// <summary>Ex.: "os timbres editados" ou "o backup de 16/10/2026".</summary>
    public string Description { get; }

    public CaptureCardViewModel SafetyCard { get; }

    public SafeSendStep Step
    {
        get => _step;
        private set
        {
            if (SetProperty(ref _step, value))
            {
                OnPropertyChanged(nameof(StepNumber));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>1 = aviso, 2 = backup de segurança, 3 = substituir (para o indicador de etapas).</summary>
    public int StepNumber => Step switch
    {
        SafeSendStep.Warning => 1,
        SafeSendStep.SafetyBackup => 2,
        _ => 3
    };

    public double Percent
    {
        get => _percent;
        private set => SetProperty(ref _percent, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public bool ShowNoBlinkHelp
    {
        get => _showNoBlinkHelp;
        private set => SetProperty(ref _showNoBlinkHelp, value);
    }

    public string SafetyBackupText => _flow?.SafetyBackup is { } r
        ? TrF($"Estado anterior guardado em {r.CreatedAt.ToString(Tr("dd/MM/yyyy 'às' HH:mm"), PtBr)} (aparece em Backup, se precisar voltar).")
        : string.Empty;

    public ICommand ContinueCommand { get; }

    public ICommand ReplaceCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand HomeCommand { get; }

    public ICommand NoBlinkCommand { get; }

    /// <summary>Reenviar depois de "não piscou" (o backup de segurança já foi feito).</summary>
    public ICommand RetryCommand { get; }

    private void Continue()
    {
        _flow!.AcceptWarning();
        Step = SafeSendStep.SafetyBackup;
    }

    private async Task ReplaceAsync()
    {
        var output = _state.SelectedOutput;
        if (_flow is null || output is null)
        {
            Message = Tr("A ligação com o TriAxis não está configurada.");
            Step = SafeSendStep.Failed;
            return;
        }

        if (!_state.TryBeginOperation(Tr("Mandando timbres para o TriAxis")))
        {
            return;
        }

        Step = SafeSendStep.Sending;
        ShowNoBlinkHelp = false;
        Percent = 0;
        OnPropertyChanged(nameof(SafetyBackupText));
        try
        {
            var progress = new Progress<TransmitProgress>(p => Percent = p.Percent);
            var options = new TransmitOptions(_state.Settings.ChunkSize, _state.Settings.ChunkDelayMs);
            var result = await _restore.RestoreAsync(_flow.Plan, _flow.CreateConfirmation(), output, options, progress, CancellationToken.None);
            if (result.Status == TransmitStatus.Completed)
            {
                Percent = 100;
                Step = SafeSendStep.Done;
                _state.StatusText = Tr("Timbres enviados ao TriAxis.");
            }
            else
            {
                Message = result.Message;
                Step = SafeSendStep.Failed;
            }
        }
        catch (Exception ex) when (ex is RestoreBlockedException or MidiException or IOException)
        {
            _log.Error("Envio (modo simples): falha.", ex);
            Message = ex is MidiException
                ? Tr("A interface parou de responder durante o envio. Não desligue o TriAxis: confira o cabo e tente de novo.")
                : ex.Message;
            Step = SafeSendStep.Failed;
        }
        finally
        {
            _state.EndOperation();
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
