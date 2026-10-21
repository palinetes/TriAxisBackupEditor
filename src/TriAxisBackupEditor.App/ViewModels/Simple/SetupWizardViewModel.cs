using System.Windows.Input;
using System.Windows.Threading;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Connection;
using TriAxisBackupEditor.Application.Control;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Midi;

namespace TriAxisBackupEditor.App.ViewModels.Simple;

public enum WizardStep
{
    Device = 1,
    Cables = 2,
    FirstBackup = 3,
    SendTest = 4
}

/// <summary>
/// Assistente de ligação para quem não entende de MIDI: escolher o aparelho, ver os cabos, fazer o primeiro backup
/// (testa o caminho TriAxis → PC) e um teste de envio (testa PC → TriAxis e acerta o "número do TriAxis").
/// </summary>
public sealed class SetupWizardViewModel : SimplePage
{
    private readonly AppState _state;
    private readonly IMidiDeviceService _midi;
    private readonly ConnectionViewModel _connection;
    private readonly ITechLog _log;
    private readonly DispatcherTimer _deviceTimer;
    private WizardStep _step = WizardStep.Device;
    private bool _usesUsbCable = true;
    private IReadOnlyList<MidiDevicePair> _devices = Array.Empty<MidiDevicePair>();
    private MidiDevicePair? _selectedDevice;
    private int _channel;
    private int _testProgram = 2;
    private bool _testSent;
    private string _testMessage = string.Empty;
    private readonly ChannelFinder _finder = new();
    private readonly TriAxisListener _listener;
    private CancellationTokenSource? _listenCts;
    private bool _isListening;

    public SetupWizardViewModel(SimpleShellViewModel shell, AppState state, IMidiDeviceService midi, ConnectionViewModel connection, IDialogService dialogs, ITechLog log)
        : base(shell)
    {
        _state = state;
        _midi = midi;
        _connection = connection;
        _log = log;
        _listener = new TriAxisListener(midi);
        _channel = state.Settings.ControlMidiChannel;

        FirstBackup = shell.NewCaptureCard("Primeiro backup (assistente)");
        FirstBackup.Completed += (_, _) => CommandManager.InvalidateRequerySuggested();

        _deviceTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => RefreshDevices(), Dispatcher.CurrentDispatcher);

        NextCommand = new RelayCommand(Next, CanGoNext);
        BackCommand = new RelayCommand(() => Step = Step - 1, () => Step > WizardStep.Device && !FirstBackup.IsRunning);
        SkipCommand = new RelayCommand(Shell.FinishWizard, () => !FirstBackup.IsRunning);
        SendTestCommand = new RelayCommand(SendTest, () => !_state.IsBusy);
        ListenCommand = new RelayCommand(Listen, () => !_state.IsBusy && !IsListening);
        StopListenCommand = new RelayCommand(() => _listenCts?.Cancel(), () => IsListening);
        YesCommand = new RelayCommand(() => AnswerTest(true), () => TestSent);
        NoCommand = new RelayCommand(() => AnswerTest(false), () => TestSent);
    }

    public override string Title => "Ligar o TriAxis ao computador";

    public override bool CanLeave => !FirstBackup.IsRunning;

    public WizardStep Step
    {
        get => _step;
        private set
        {
            if (SetProperty(ref _step, value))
            {
                OnPropertyChanged(nameof(StepNumber));
                OnPropertyChanged(nameof(NextText));
                if (value == WizardStep.Device)
                {
                    _deviceTimer.Start();
                }
                else
                {
                    _deviceTimer.Stop();
                }

                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int StepNumber => (int)Step;

    public string NextText => Step == WizardStep.SendTest ? "Concluir" : "Próximo";

    // ---- Passo 1: aparelho ----

    public bool UsesUsbCable
    {
        get => _usesUsbCable;
        set
        {
            if (SetProperty(ref _usesUsbCable, value))
            {
                OnPropertyChanged(nameof(UsesAudioInterface));
            }
        }
    }

    public bool UsesAudioInterface
    {
        get => !_usesUsbCable;
        set => UsesUsbCable = !value;
    }

    public IReadOnlyList<MidiDevicePair> Devices
    {
        get => _devices;
        private set
        {
            if (SetProperty(ref _devices, value))
            {
                OnPropertyChanged(nameof(HasNoDevices));
            }
        }
    }

    public bool HasNoDevices => Devices.Count == 0;

    public MidiDevicePair? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    // ---- Passo 3: primeiro backup ----

    public CaptureCardViewModel FirstBackup { get; }

    // ---- Passo 4: teste de envio ----

    /// <summary>"Número do TriAxis" = canal MIDI em que ele recebe (1–16). A palavra "canal" não aparece no modo simples.</summary>
    public int Channel
    {
        get => _channel;
        set => SetProperty(ref _channel, Math.Clamp(value, 1, 16));
    }

    public bool TestSent
    {
        get => _testSent;
        private set
        {
            if (SetProperty(ref _testSent, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string SendTestText => _finder.State switch
    {
        ChannelFinderState.TestingAll => "Testar a ligação",
        ChannelFinderState.Narrowing => "Próximo teste",
        _ => "Testar de novo"
    };

    public string TestMessage
    {
        get => _testMessage;
        private set => SetProperty(ref _testMessage, value);
    }

    public ICommand NextCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand SkipCommand { get; }

    public ICommand SendTestCommand { get; }

    /// <summary>"Escutar o TriAxis": ele mesmo diz o canal quando se aperta a tecla de programa no painel.</summary>
    public ICommand ListenCommand { get; }

    public ICommand StopListenCommand { get; }

    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (SetProperty(ref _isListening, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ICommand YesCommand { get; }

    public ICommand NoCommand { get; }

    public override void OnEnter()
    {
        RefreshDevices();
        if (Step == WizardStep.Device)
        {
            _deviceTimer.Start();
        }
    }

    public override void OnLeave()
    {
        _leaving = true;
        _listenCts?.Cancel();
        _deviceTimer.Stop();
    }

    private void RefreshDevices()
    {
        IReadOnlyList<MidiDevicePair> pairs;
        try
        {
            pairs = DevicePairing.Pair(_midi.GetInputDevices(), _midi.GetOutputDevices());
        }
        catch (Exception ex)
        {
            _log.Error("Assistente: erro ao listar interfaces.", ex);
            return;
        }

        if (pairs.Select(p => p.Name).SequenceEqual(Devices.Select(d => d.Name)))
        {
            return;
        }

        var previous = SelectedDevice?.Name;
        Devices = pairs;
        SelectedDevice = pairs.FirstOrDefault(p => p.Name == previous)
                         ?? pairs.FirstOrDefault(p => p.Input.SameDeviceAs(_state.SelectedInput))
                         ?? (pairs.Count == 1 ? pairs[0] : null);
    }

    private bool CanGoNext() => Step switch
    {
        WizardStep.Device => SelectedDevice is not null,
        WizardStep.Cables => true,
        WizardStep.FirstBackup => FirstBackup.IsSuccess,
        WizardStep.SendTest => true,
        _ => false
    };

    private void Next()
    {
        switch (Step)
        {
            case WizardStep.Device:
                _connection.SelectedInput = SelectedDevice!.Input;
                _connection.SelectedOutput = SelectedDevice.Output;
                _log.Info($"Assistente: aparelho escolhido \"{SelectedDevice.Name}\".");
                Step = WizardStep.Cables;
                break;
            case WizardStep.Cables:
                Step = WizardStep.FirstBackup;
                break;
            case WizardStep.FirstBackup:
                Step = WizardStep.SendTest;
                break;
            case WizardStep.SendTest:
                Finish();
                break;
        }
    }

    /// <summary>Manda a troca de programa nos canais do teste atual (no 1º teste, nos 16 de uma vez).</summary>
    private void SendTest()
    {
        var output = _state.SelectedOutput;
        if (output is null)
        {
            TestMessage = "Volte ao passo 1 e escolha o aparelho.";
            return;
        }

        if (_finder.State is ChannelFinderState.Found or ChannelFinderState.NoResponse)
        {
            _finder.Restart();
        }

        var channels = _finder.CurrentProbe;
        try
        {
            using var port = _midi.OpenOutput(output);
            foreach (var channel in channels)
            {
                port.SendShort(MidiMessages.ProgramChange(channel, _testProgram));
            }

            _log.Info($"Assistente: teste de envio — programa {_testProgram} nos canais {string.Join(",", channels)}.");
            TestMessage = $"Mandei o programa {_testProgram}. O display PROGRAM/CHANNEL do TriAxis mudou para {_testProgram}?";
            TestSent = true;
            _testProgram = _testProgram == 2 ? 1 : 2;
            RaiseTestState();
        }
        catch (MidiException ex)
        {
            TestMessage = "Não consegui usar a interface: " + ex.Message + " Feche outros programas de música e tente de novo.";
        }
    }

    private void AnswerTest(bool changed)
    {
        _finder.Answer(changed);
        TestSent = false;
        switch (_finder.State)
        {
            case ChannelFinderState.Found:
                Channel = _finder.FoundChannel!.Value;
                TestMessage = $"Pronto! Seu TriAxis está no número {Channel}. Clique em Concluir.";
                _log.Info($"Assistente: TriAxis encontrado no canal {Channel}.");
                break;

            case ChannelFinderState.NoResponse:
                TestMessage = "O TriAxis não reagiu a nenhum número, então o problema está na ligação, não no número. Confira:\n" +
                              "• O cabo sai do MIDI OUT da interface e entra no conector MIDI IN do TriAxis?\n" +
                              "• Em cabo USB-MIDI: troque os dois plugues de lugar.\n" +
                              "• Placa de áudio (ex.: RME): o cabo está no conector MIDI da porta escolhida no passo 1?\n" +
                              "• Tem uma pedaleira (ex.: FCB1010) no meio do caminho? Ligue a interface direto no TriAxis.\n" +
                              "Depois clique em Testar de novo.";
                _log.Warn("Assistente: TriAxis não reagiu à troca de programa em nenhum canal.");
                break;

            default:
                TestMessage = _finder.QuestionsAsked == 1
                    ? "Ótimo, a ligação funciona! Agora vou descobrir o número do seu TriAxis: no máximo 4 testes rápidos. Clique em Próximo teste."
                    : "Anotado. Clique em Próximo teste.";
                break;
        }

        RaiseTestState();
    }

    /// <summary>
    /// Escuta o MIDI OUT do TriAxis por 30 s. Quando o usuário aperta a seta de PROGRAM no painel, o TriAxis manda um
    /// Program Change no canal dele: assim o canal certo aparece sem tentativa e erro. Só lê, não envia nada.
    /// </summary>
    private async void Listen()
    {
        var input = _state.SelectedInput;
        if (input is null)
        {
            TestMessage = "Volte ao passo 1 e escolha o aparelho.";
            return;
        }

        _listenCts = new CancellationTokenSource();
        var source = _listenCts;
        if (!_state.TryBeginOperation("Escutando o TriAxis", () => source.Cancel()))
        {
            _listenCts = null;
            return;
        }

        IsListening = true;
        TestSent = false;
        TestMessage = "Estou escutando por 30 segundos. No painel do TriAxis, aperte uma das setas ◀ ▶ embaixo da janela PROGRAM/CHANNEL (ou a seta de PRESET).";
        _log.Info("Assistente: escutando o MIDI OUT do TriAxis (aguardando um Program Change).");
        try
        {
            var message = await _listener.ListenForProgramChangeAsync(input, TimeSpan.FromSeconds(30), source.Token);
            if (message is { } heard)
            {
                Channel = heard.Channel;
                TestMessage = $"Ouvi o TriAxis! Ele mandou a troca de programa no número {heard.Channel}. Já deixei esse número guardado: clique em Concluir.";
                _log.Info($"Assistente: o TriAxis anunciou Program Change (valor {heard.Number}) no canal {heard.Channel}.");
                _finder.Restart();
                RaiseTestState();
            }
            else if (!_leaving)
            {
                TestMessage = "Não ouvi nada. Confira:\n" +
                              "• O cabo do MIDI OUT do TriAxis chega no MIDI IN da interface? (é o mesmo do backup)\n" +
                              "• Aperte as setas de PROGRAM no painel do TriAxis, com ele ligado.\n" +
                              "Se preferir, use o botão Testar a ligação abaixo.";
                _log.Warn("Assistente: nenhum Program Change ouvido do TriAxis.");
            }
        }
        catch (MidiException ex)
        {
            TestMessage = "Não consegui usar a interface: " + ex.Message + " Feche outros programas de música e tente de novo.";
        }
        finally
        {
            IsListening = false;
            _listenCts = null;
            source.Dispose();
            _state.EndOperation();
        }
    }

    private bool _leaving;

    /// <summary>Abre o assistente direto no teste de envio (ex.: "o TriAxis não troca de programa").</summary>
    public void StartAtSendTest()
    {
        Step = WizardStep.SendTest;
        TestMessage = "Vamos descobrir por que o TriAxis não troca de programa. Clique em Testar a ligação e olhe o display PROGRAM/CHANNEL.";
    }

    public bool IsChannelFound => _finder.State == ChannelFinderState.Found;

    private void RaiseTestState()
    {
        OnPropertyChanged(nameof(SendTestText));
        OnPropertyChanged(nameof(IsChannelFound));
        CommandManager.InvalidateRequerySuggested();
    }

    private void Finish()
    {
        _state.Settings.ControlMidiChannel = Channel;
        _state.SaveSettings();
        _deviceTimer.Stop();
        Shell.FinishWizard();
    }
}
