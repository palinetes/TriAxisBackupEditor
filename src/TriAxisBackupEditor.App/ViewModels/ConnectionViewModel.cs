using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.Application.Connection;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Midi;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels;

public enum StatusKind
{
    Idle,
    Ok,
    Warning,
    Error
}

public sealed class ConnectionViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly IMidiDeviceService _midi;
    private readonly ConnectionTestService _tester;
    private readonly ITechLog _log;
    private readonly DispatcherTimer _timer;
    private IReadOnlyList<MidiDeviceInfo> _inputs = Array.Empty<MidiDeviceInfo>();
    private IReadOnlyList<MidiDeviceInfo> _outputs = Array.Empty<MidiDeviceInfo>();
    private bool _updatingLists;
    private string _connectionStatus = Tr("Nenhuma porta selecionada.");
    private StatusKind _connectionStatusKind = StatusKind.Idle;
    private string _testResult = string.Empty;

    public ConnectionViewModel(AppState state, IMidiDeviceService midi, ConnectionTestService tester, ITechLog log)
    {
        _state = state;
        _midi = midi;
        _tester = tester;
        _log = log;

        RefreshCommand = new RelayCommand(() => RefreshDevices(userRequested: true));
        TestCommand = new AsyncRelayCommand(TestAsync, () => !_state.IsBusy && (_state.SelectedInput is not null || _state.SelectedOutput is not null));

        _state.PropertyChanged += OnStateChanged;

        // Atualiza a lista periodicamente para detectar interfaces plugadas/desplugadas (nada é enviado).
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (_, _) => RefreshDevices(userRequested: false), Dispatcher.CurrentDispatcher);
    }

    public IReadOnlyList<MidiDeviceInfo> Inputs
    {
        get => _inputs;
        private set => SetProperty(ref _inputs, value);
    }

    public IReadOnlyList<MidiDeviceInfo> Outputs
    {
        get => _outputs;
        private set => SetProperty(ref _outputs, value);
    }

    public MidiDeviceInfo? SelectedInput
    {
        get => _state.SelectedInput;
        set
        {
            if (_updatingLists)
            {
                return;
            }

            _state.SelectedInput = value;
            _state.Settings.LastInputDeviceKey = value?.Key;
            _state.SaveSettings();
            if (value is not null)
            {
                _log.Info($"MIDI IN selecionado: \"{value.DisplayName}\".");
            }
        }
    }

    public MidiDeviceInfo? SelectedOutput
    {
        get => _state.SelectedOutput;
        set
        {
            if (_updatingLists)
            {
                return;
            }

            _state.SelectedOutput = value;
            _state.Settings.LastOutputDeviceKey = value?.Key;
            _state.SaveSettings();
            if (value is not null)
            {
                _log.Info($"MIDI OUT selecionado: \"{value.DisplayName}\".");
            }
        }
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public StatusKind ConnectionStatusKind
    {
        get => _connectionStatusKind;
        private set => SetProperty(ref _connectionStatusKind, value);
    }

    public string TestResult
    {
        get => _testResult;
        private set => SetProperty(ref _testResult, value);
    }

    public ICommand RefreshCommand { get; }

    public ICommand TestCommand { get; }

    public void Start()
    {
        RefreshDevices(userRequested: false, restoreSaved: true);
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void RefreshDevices(bool userRequested, bool restoreSaved = false)
    {
        IReadOnlyList<MidiDeviceInfo> inputs;
        IReadOnlyList<MidiDeviceInfo> outputs;
        try
        {
            inputs = _midi.GetInputDevices();
            outputs = _midi.GetOutputDevices();
        }
        catch (Exception ex)
        {
            ConnectionStatus = Tr("Erro ao listar dispositivos MIDI: ") + ex.Message;
            ConnectionStatusKind = StatusKind.Error;
            _log.Error("Erro ao listar dispositivos MIDI.", ex);
            return;
        }

        var changed = !SameKeys(inputs, Inputs) || !SameKeys(outputs, Outputs);
        if (changed || userRequested || restoreSaved)
        {
            var wantedIn = restoreSaved ? _state.Settings.LastInputDeviceKey : _state.SelectedInput?.Key;
            var wantedOut = restoreSaved ? _state.Settings.LastOutputDeviceKey : _state.SelectedOutput?.Key;
            var lostIn = _state.SelectedInput is not null && inputs.All(d => d.Key != _state.SelectedInput.Key);
            var lostOut = _state.SelectedOutput is not null && outputs.All(d => d.Key != _state.SelectedOutput.Key);

            _updatingLists = true;
            try
            {
                Inputs = inputs;
                Outputs = outputs;
            }
            finally
            {
                _updatingLists = false;
            }

            _state.SelectedInput = inputs.FirstOrDefault(d => d.Key == wantedIn);
            _state.SelectedOutput = outputs.FirstOrDefault(d => d.Key == wantedOut);
            OnPropertyChanged(nameof(SelectedInput));
            OnPropertyChanged(nameof(SelectedOutput));

            if (changed)
            {
                _log.Info($"Dispositivos MIDI: {inputs.Count} entrada(s) [{string.Join(", ", inputs.Select(d => d.DisplayName))}], " +
                          TrF($"{outputs.Count} saída(s) [{string.Join(", ", outputs.Select(d => d.DisplayName))}]."));
            }

            if (lostIn || lostOut)
            {
                var message = Tr("Interface MIDI desconectada: ") + string.Join(Tr(" e "), new[] { lostIn ? Tr("MIDI IN") : null, lostOut ? Tr("MIDI OUT") : null }.Where(s => s is not null)) +
                              Tr(" não está mais disponível. Reconecte e selecione novamente.");
                _log.Warn(message);
                _state.StatusText = message;
            }
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (Inputs.Count == 0 && Outputs.Count == 0)
        {
            ConnectionStatus = Tr("Nenhuma interface MIDI encontrada. Conecte a interface USB e aguarde a instalação do driver.");
            ConnectionStatusKind = StatusKind.Error;
        }
        else if (_state.SelectedInput is null || _state.SelectedOutput is null)
        {
            ConnectionStatus = Tr("Selecione o MIDI IN e o MIDI OUT conectados ao TriAxis.");
            ConnectionStatusKind = StatusKind.Warning;
        }
        else
        {
            ConnectionStatus = TrF($"Pronto: IN \"{_state.SelectedInput.DisplayName}\" · OUT \"{_state.SelectedOutput.DisplayName}\".");
            ConnectionStatusKind = StatusKind.Ok;
        }
    }

    private async Task TestAsync()
    {
        if (!_state.TryBeginOperation(Tr("Teste de conexão"), () => { }))
        {
            return;
        }

        try
        {
            TestResult = Tr("Testando... abrindo portas e escutando a entrada por 5 segundos. Mude de preset no TriAxis ou pise na pedaleira para ver mensagens chegando.");
            var result = await _tester.TestAsync(_state.SelectedInput, _state.SelectedOutput, TimeSpan.FromSeconds(5), CancellationToken.None);

            var lines = new List<string>
            {
                result.InputOk ? Tr("✔ MIDI IN aberto com sucesso.") : Tr("✖ MIDI IN: ") + result.InputError,
                result.OutputOk ? Tr("✔ MIDI OUT aberto com sucesso (nenhum byte enviado).") : Tr("✖ MIDI OUT: ") + result.OutputError
            };

            if (result.InputOk)
            {
                lines.Add(result.ShortMessages + result.SysExBytes > 0
                    ? TrF($"✔ Recebidas {result.ShortMessages} mensagem(ns) MIDI e {result.SysExBytes} byte(s) de SysEx. Última: {result.LastShortMessage}")
                    : Tr("• Nenhuma mensagem recebida durante a escuta (normal se nada foi tocado/alterado)."));
            }

            TestResult = string.Join(Environment.NewLine, lines);
            _state.StatusText = result.Success ? Tr("Teste de conexão concluído: portas OK.") : Tr("Teste de conexão encontrou problemas. Veja a aba Conexão.");
        }
        finally
        {
            _state.EndOperation();
        }
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.SelectedInput) or nameof(AppState.SelectedOutput))
        {
            UpdateStatus();
        }
    }

    private static bool SameKeys(IReadOnlyList<MidiDeviceInfo> a, IReadOnlyList<MidiDeviceInfo> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Key == p.Second.Key && p.First.Index == p.Second.Index);
}
