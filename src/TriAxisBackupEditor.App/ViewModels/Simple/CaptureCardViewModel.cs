using System.Globalization;
using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.Storage;

namespace TriAxisBackupEditor.App.ViewModels.Simple;

public enum CaptureCardState
{
    Ready,
    Waiting,
    Receiving,
    Success,
    Failed
}

/// <summary>
/// "Guardar a memória do TriAxis" em linguagem simples: instrução grande (SHIFT + PRESET ▼), progresso e,
/// se der errado, dicas do que conferir. Usado no assistente, na página Backup e no backup de segurança do envio.
/// </summary>
public sealed class CaptureCardViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly AppState _state;
    private readonly BackupCaptureService _capture;
    private readonly BackupLibraryService _library;
    private readonly ITechLog _log;
    private readonly string _notes;
    private CancellationTokenSource? _cts;
    private CaptureCardState _cardState = CaptureCardState.Ready;
    private double _percent;
    private string _title = "Pronto para guardar a memória do TriAxis";
    private string _detail = string.Empty;
    private string _hint = string.Empty;

    public CaptureCardViewModel(AppState state, BackupCaptureService capture, BackupLibraryService library, ITechLog log, string notes)
    {
        _state = state;
        _capture = capture;
        _library = library;
        _log = log;
        _notes = notes;
        StartCommand = new AsyncRelayCommand(async () => await RunAsync(), () => !IsRunning && !_state.IsBusy);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsRunning);
    }

    /// <summary>Disparado quando a memória foi recebida e salva.</summary>
    public event EventHandler<BackupRecord>? Completed;

    public CaptureCardState CardState
    {
        get => _cardState;
        private set
        {
            if (SetProperty(ref _cardState, value))
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsSuccess));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(ShowInstruction));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsRunning => CardState is CaptureCardState.Waiting or CaptureCardState.Receiving;

    public bool IsSuccess => CardState == CaptureCardState.Success;

    public bool IsFailed => CardState == CaptureCardState.Failed;

    public bool IsReady => CardState is CaptureCardState.Ready or CaptureCardState.Failed;

    public bool ShowInstruction => CardState == CaptureCardState.Waiting;

    public double Percent
    {
        get => _percent;
        private set => SetProperty(ref _percent, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    public string Hint
    {
        get => _hint;
        private set => SetProperty(ref _hint, value);
    }

    public BackupRecord? Result { get; private set; }

    public ICommand StartCommand { get; }

    public ICommand CancelCommand { get; }

    public async Task<BackupRecord?> RunAsync()
    {
        if (_state.SelectedInput is null || _state.SelectedOutput is null)
        {
            Fail("A ligação com o TriAxis ainda não foi configurada", "Use \"Configurar ligação\" no topo da tela.");
            return null;
        }

        if (!_state.TryBeginOperation("Guardando a memória do TriAxis", () => _cts?.Cancel()))
        {
            return null;
        }

        _cts = new CancellationTokenSource();
        Result = null;
        Percent = 0;
        Hint = string.Empty;
        Title = "Agora, no TriAxis: SHIFT + PRESET ▼";
        CardState = CaptureCardState.Waiting;
        StartWaitingClock();

        try
        {
            var options = new CaptureOptions(
                _state.SelectedInput,
                _state.SelectedOutput,
                TimeSpan.FromSeconds(_state.Settings.StartTimeoutSeconds),
                TimeSpan.FromSeconds(_state.Settings.ReceiveTimeoutSeconds),
                TimeSpan.FromMilliseconds(_state.Settings.PostDumpIdleMilliseconds));

            var progress = new Progress<CaptureProgress>(p =>
            {
                if (p.Phase is CapturePhase.Receiving or CapturePhase.Finishing && CardState == CaptureCardState.Waiting)
                {
                    CardState = CaptureCardState.Receiving;
                    Title = "Recebendo os timbres do TriAxis...";
                    Detail = "Não desligue o TriAxis.";
                }

                Percent = p.Percent;
            });

            var result = await _capture.CaptureAsync(options, progress, _cts.Token);
            return Handle(result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or MidiException)
        {
            _log.Error("Backup (modo simples): falha.", ex);
            Fail("Não foi possível guardar a memória", "Feche outros programas de música (DAW, editores) e tente de novo. Detalhe: " + ex.Message);
            return null;
        }
        finally
        {
            _waitClock?.Stop();
            _cts.Dispose();
            _cts = null;
            _state.EndOperation();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private BackupRecord? Handle(CaptureResult result)
    {
        switch (result.Status)
        {
            case CaptureStatus.Completed:
            {
                BackupRecord record;
                try
                {
                    record = _library.SaveCapturedBackup(result.Data, _state.Settings.BackupFolder, _notes, DateTime.Now);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    var fallback = Path.Combine(_state.Paths.DataFolder, "Recuperacao");
                    _log.Error($"Falha ao gravar na pasta de backups; usando \"{fallback}\".", ex);
                    record = _library.SaveCapturedBackup(result.Data, fallback, _notes, DateTime.Now);
                }

                Result = record;
                Percent = 100;
                Title = "Pronto! Memória do TriAxis guardada";
                Detail = $"Salvo em {record.CreatedAt.ToString("dd/MM/yyyy 'às' HH:mm", PtBr)}.";
                CardState = CaptureCardState.Success;
                _state.StatusText = "Backup guardado: " + record.FileName;
                Completed?.Invoke(this, record);
                return record;
            }

            case CaptureStatus.NoDataReceived:
                Fail("Não chegou nada do TriAxis",
                    "Confira:\n• O cabo está no conector MIDI OUT do TriAxis?\n• Usa um cabo USB-MIDI? Troque os dois plugues de lugar no TriAxis e tente de novo.\n• Segure SHIFT e então aperte PRESET ▼.");
                return null;

            case CaptureStatus.Cancelled when result.RawPartialData.Length == 0:
                CardState = CaptureCardState.Ready;
                Title = "Cancelado";
                Detail = string.Empty;
                return null;

            case CaptureStatus.Cancelled:
            case CaptureStatus.Incomplete:
                if (result.RawPartialData.Length > 0)
                {
                    _library.SaveIncomplete(result.RawPartialData, _state.Settings.BackupFolder, DateTime.Now);
                }

                Fail("A transmissão parou no meio", "Nada foi alterado no TriAxis. Tente de novo; se repetir, troque o cabo ou a porta USB.");
                return null;

            default:
                Fail("Não foi possível usar a interface", "Feche outros programas de música e tente de novo. Detalhe: " + result.Message);
                return null;
        }
    }

    /// <summary>Segundos sem receber nada até mostrar a lista do que conferir (a espera continua).</summary>
    private const int EarlyHintSeconds = 15;

    private System.Windows.Threading.DispatcherTimer? _waitClock;

    /// <summary>Contagem enquanto espera o dump e, depois de alguns segundos, dicas do que conferir.</summary>
    private void StartWaitingClock()
    {
        var started = DateTime.Now;
        var limit = _state.Settings.StartTimeoutSeconds;
        Detail = $"Segure SHIFT e aperte PRESET ▼ (a seta da esquerda, embaixo do display PRESET). Esperando... 0 s de {limit} s";

        _waitClock?.Stop();
        _waitClock = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _waitClock.Tick += (_, _) =>
        {
            if (CardState != CaptureCardState.Waiting)
            {
                _waitClock?.Stop();
                return;
            }

            var seconds = (int)(DateTime.Now - started).TotalSeconds;
            Detail = $"Segure SHIFT e aperte PRESET ▼ (a seta da esquerda, embaixo do display PRESET). Esperando... {seconds} s de {limit} s";
            if (seconds >= EarlyHintSeconds && string.IsNullOrEmpty(Hint))
            {
                Hint = "Ainda não chegou nada do TriAxis. Confira, sem fechar esta tela:\n" +
                       "• O cabo sai do conector MIDI OUT do TriAxis e entra no MIDI IN da interface?\n" +
                       "• Em cabo USB-MIDI: troque os dois plugues de lugar no TriAxis.\n" +
                       "• Placa de áudio (ex.: RME): o cabo está no conector MIDI que o programa mostra?\n" +
                       "• Segure SHIFT primeiro e, sem soltar, aperte a seta ◄ do PRESET.\n" +
                       "Se quiser parar, clique em Cancelar: nada é alterado no TriAxis.";
            }
        };
        _waitClock.Start();
    }

    private void Fail(string title, string hint)
    {
        Title = title;
        Detail = string.Empty;
        Hint = hint;
        CardState = CaptureCardState.Failed;
    }
}
