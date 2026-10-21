using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Control;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.Storage;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels;

public sealed class PedalViewModel : ObservableObject
{
    private readonly ControlViewModel _owner;

    public PedalViewModel(ControlViewModel owner, int number)
    {
        _owner = owner;
        Number = number;
        PressCommand = new RelayCommand(() => _owner.Execute(ControlAction.Pedal1 + (Number - 1)), () => _owner.IsActive && Program is not null);
    }

    public int Number { get; }

    public int? Program => _owner.ProgramForPedal(Number);

    public string ProgramText => Program is int p ? TrF($"PROGRAMA {p}") : "—";

    /// <summary>Nome do timbre chamado por este pedal (modo simples); senão "Programa N".</summary>
    public string DisplayName => Program is int p ? _owner.PedalNameProvider?.Invoke(p) ?? TrF($"Programa {p}") : "—";

    public bool IsLit => Program is int p && p == _owner.CurrentProgram;

    public string KeyHint => Number == 10 ? Tr("tecla 0") : TrF($"tecla {Number}");

    public ICommand PressCommand { get; }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Program));
        OnPropertyChanged(nameof(ProgramText));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(IsLit));
    }
}

public sealed class CcButtonViewModel : ObservableObject
{
    private readonly ControlViewModel _owner;
    private readonly CcButtonSetting _setting;
    private bool _isOn;

    public CcButtonViewModel(ControlViewModel owner, int index, CcButtonSetting setting)
    {
        _owner = owner;
        _setting = setting;
        Index = index;
        ToggleCommand = new RelayCommand(() => _owner.Execute(ControlAction.CcButton1 + (Index - 1)), () => _owner.IsActive);
    }

    public int Index { get; }

    public string Label
    {
        get => _setting.Label;
        set
        {
            _setting.Label = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayLabel));
            _owner.SaveSettings();
        }
    }

    /// <summary>Nome no idioma da tela (os nomes de fábrica "Botão 1..4" são traduzidos; nomes do usuário ficam como estão).</summary>
    public string DisplayLabel => Tr(_setting.Label);

    public int Controller
    {
        get => _setting.Controller;
        set
        {
            _setting.Controller = Math.Clamp(value, 0, 127);
            OnPropertyChanged();
            _owner.SaveSettings();
        }
    }

    public bool IsOn
    {
        get => _isOn;
        set => SetProperty(ref _isOn, value);
    }

    public ICommand ToggleCommand { get; }
}

public sealed class BindingViewModel : ObservableObject
{
    private readonly ControlViewModel _owner;
    private bool _isLearning;

    public BindingViewModel(ControlViewModel owner, ControlAction action)
    {
        _owner = owner;
        Action = action;
        Label = ControlActions.Label(action);
        LearnCommand = new RelayCommand(() => _owner.StartLearning(this), () => _owner.IsActive && _owner.HasControlInput);
        ClearCommand = new RelayCommand(() => _owner.ClearBinding(this), () => TriggerText != "—");
    }

    public ControlAction Action { get; }

    public string Label { get; }

    public string TriggerText => _owner.TriggerFor(Action)?.ToString() ?? "—";

    public bool IsLearning
    {
        get => _isLearning;
        set
        {
            if (SetProperty(ref _isLearning, value))
            {
                OnPropertyChanged(nameof(LearnText));
            }
        }
    }

    public string LearnText => IsLearning ? Tr("Aguardando...") : Tr("Aprender");

    public ICommand LearnCommand { get; }

    public ICommand ClearCommand { get; }

    public void Refresh() => OnPropertyChanged(nameof(TriggerText));
}

public sealed record InputOption(MidiDeviceInfo? Device, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Modo controle: o programa vira uma pedaleira MIDI para o TriAxis. Envia apenas Program Change e Control Change
/// (seleção de programas / acionamentos). Nada é gravado na memória do TriAxis.
/// </summary>
public sealed class ControlViewModel : ObservableObject
{
    public const string OperationName = "Modo controle";

    private readonly AppState _state;
    private readonly ControlSurfaceService _service;
    private readonly ConnectionViewModel _connection;
    private readonly IDialogService _dialogs;
    private readonly ITechLog _log;
    private readonly Dispatcher _dispatcher;
    private readonly ControlBindingMap _bindings = new();
    private BankNavigator _banks;
    private bool _isActive;
    private int? _currentProgram;
    private IReadOnlyList<InputOption> _inputOptions = Array.Empty<InputOption>();
    private InputOption? _selectedInput;
    private BindingViewModel? _learning;
    private string _statusText = Tr("Ative o modo controle para usar o programa como pedaleira MIDI do TriAxis.");
    private StatusKind _statusKind = StatusKind.Idle;
    private string _lastInput = "—";
    private string _lastSent = "—";

    public ControlViewModel(AppState state, ControlSurfaceService service, ConnectionViewModel connection, IDialogService dialogs, ITechLog log)
    {
        _state = state;
        _service = service;
        _connection = connection;
        _dialogs = dialogs;
        _log = log;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _banks = new BankNavigator(state.Settings.ControlBankSize);

        LoadBindings();
        CcButtons = state.Settings.ControlCcButtons.Take(4).Select((s, i) => new CcButtonViewModel(this, i + 1, s)).ToList();
        Bindings = ControlActions.All.Select(a => new BindingViewModel(this, a)).ToList();
        BuildPedals();
        RefreshInputOptions();

        ToggleActiveCommand = new RelayCommand(ToggleActive, () => IsActive || (!_state.IsBusy && _state.SelectedOutput is not null));
        BankUpCommand = new RelayCommand(() => Execute(ControlAction.BankUp), () => IsActive);
        BankDownCommand = new RelayCommand(() => Execute(ControlAction.BankDown), () => IsActive);
        ProgramUpCommand = new RelayCommand(() => Execute(ControlAction.ProgramUp), () => IsActive);
        ProgramDownCommand = new RelayCommand(() => Execute(ControlAction.ProgramDown), () => IsActive);
        RefreshInputsCommand = new RelayCommand(RefreshInputOptions, () => !IsActive);

        _service.InputReceived += (_, message) => _dispatcher.BeginInvoke(() => OnInput(message));
        _service.ErrorOccurred += (_, error) => _dispatcher.BeginInvoke(() => SetStatus(Tr("Entrada de controle: ") + error, StatusKind.Warning));
    }

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(ActiveButtonText));
                OnPropertyChanged(nameof(CanEditSetup));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ActiveButtonText => IsActive ? Tr("Desativar modo controle") : Tr("Ativar modo controle");

    /// <summary>Canal, entrada e tamanho de banco só mudam com o modo desligado.</summary>
    public bool CanEditSetup => !IsActive;

    public IReadOnlyList<int> Channels { get; } = Enumerable.Range(1, 16).ToList();

    public IReadOnlyList<int> BankSizes => BankNavigator.SupportedBankSizes;

    public int Channel
    {
        get => _state.Settings.ControlMidiChannel;
        set
        {
            _state.Settings.ControlMidiChannel = Math.Clamp(value, 1, 16);
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public int BankSize
    {
        get => _banks.BankSize;
        set
        {
            if (value == _banks.BankSize || !BankNavigator.SupportedBankSizes.Contains(value))
            {
                return;
            }

            _banks = new BankNavigator(value);
            _state.Settings.ControlBankSize = value;
            SaveSettings();
            BuildPedals();
            RefreshPanel();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<InputOption> InputOptions
    {
        get => _inputOptions;
        private set => SetProperty(ref _inputOptions, value);
    }

    public InputOption? SelectedInput
    {
        get => _selectedInput;
        set
        {
            if (SetProperty(ref _selectedInput, value))
            {
                _state.Settings.ControlInputDeviceKey = value?.Device?.Key;
                SaveSettings();
                OnPropertyChanged(nameof(HasControlInput));
            }
        }
    }

    public bool HasControlInput => _selectedInput?.Device is not null;

    public bool ForwardProgramChanges
    {
        get => _state.Settings.ControlForwardProgramChanges;
        set
        {
            _state.Settings.ControlForwardProgramChanges = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public int? CurrentProgram => _currentProgram;

    public string BankDisplay => _banks.Bank.ToString();

    public string ProgramDisplay => _currentProgram?.ToString() ?? "---";

    public string BankRangeText
    {
        get
        {
            var first = _banks.ProgramFor(1);
            var last = Enumerable.Range(1, _banks.BankSize).Select(p => _banks.ProgramFor(p)).LastOrDefault(p => p is not null);
            return TrF($"Banco {_banks.Bank} de {_banks.BankCount - 1}: programas {first}–{last}");
        }
    }

    public ObservableCollection<PedalViewModel> Pedals { get; } = new();

    /// <summary>Colunas da grade de pedais (5 por linha, como a FCB1010).</summary>
    public int PedalColumns => Math.Min(5, _banks.BankSize);

    public IReadOnlyList<CcButtonViewModel> CcButtons { get; }

    public IReadOnlyList<BindingViewModel> Bindings { get; }

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

    public string LastInput
    {
        get => _lastInput;
        private set => SetProperty(ref _lastInput, value);
    }

    public string LastSent
    {
        get => _lastSent;
        private set => SetProperty(ref _lastSent, value);
    }

    public ICommand ToggleActiveCommand { get; }

    public ICommand BankUpCommand { get; }

    public ICommand BankDownCommand { get; }

    public ICommand ProgramUpCommand { get; }

    public ICommand ProgramDownCommand { get; }

    public ICommand RefreshInputsCommand { get; }

    public int? ProgramForPedal(int pedal) => pedal <= _banks.BankSize ? _banks.ProgramFor(pedal) : null;

    public MidiTrigger? TriggerFor(ControlAction action) => _bindings.Get(action);

    /// <summary>Atalhos: 1–9 e 0 = pedais 1–10; ↑/PgUp = banco +; ↓/PgDn = banco −; → / ← = programa ±1.</summary>
    public bool HandleKey(Key key)
    {
        if (!IsActive)
        {
            return false;
        }

        ControlAction? action = key switch
        {
            >= Key.D1 and <= Key.D9 => ControlAction.Pedal1 + (key - Key.D1),
            Key.D0 => ControlAction.Pedal10,
            >= Key.NumPad1 and <= Key.NumPad9 => ControlAction.Pedal1 + (key - Key.NumPad1),
            Key.NumPad0 => ControlAction.Pedal10,
            Key.Up or Key.PageUp => ControlAction.BankUp,
            Key.Down or Key.PageDown => ControlAction.BankDown,
            Key.Right => ControlAction.ProgramUp,
            Key.Left => ControlAction.ProgramDown,
            _ => null
        };

        if (action is null)
        {
            return false;
        }

        Execute(action.Value);
        return true;
    }

    public void Execute(ControlAction action)
    {
        if (!IsActive)
        {
            return;
        }

        try
        {
            if (ControlActions.PedalNumber(action) is int pedal)
            {
                if (pedal <= _banks.BankSize && _banks.ProgramFor(pedal) is int program)
                {
                    SendProgram(program, TrF($"pedal {pedal}"));
                }

                return;
            }

            if (ControlActions.CcButtonNumber(action) is int index && index <= CcButtons.Count)
            {
                var button = CcButtons[index - 1];
                var on = !button.IsOn;
                _service.SendControlChange(Channel, button.Controller, on ? 127 : 0);
                button.IsOn = on;
                LastSent = TrF($"CC {button.Controller} = {(on ? 127 : 0)} (canal {Channel}) — {button.Label}");
                return;
            }

            switch (action)
            {
                case ControlAction.BankUp:
                    _banks.BankUp();
                    RefreshPanel();
                    break;
                case ControlAction.BankDown:
                    _banks.BankDown();
                    RefreshPanel();
                    break;
                case ControlAction.ProgramUp:
                    SendProgram(_currentProgram is int up ? (up % BankNavigator.MaxProgram) + 1 : 1, Tr("programa +1"));
                    break;
                case ControlAction.ProgramDown:
                    SendProgram(_currentProgram is int down ? ((down + BankNavigator.MaxProgram - 2) % BankNavigator.MaxProgram) + 1 : 1, Tr("programa −1"));
                    break;
            }
        }
        catch (MidiException ex)
        {
            _log.Error("Modo controle: falha ao enviar.", ex);
            Deactivate();
            SetStatus(Tr("Falha ao enviar (a interface foi desconectada?). Modo controle desativado: ") + ex.Message, StatusKind.Error);
        }
    }

    public void StartLearning(BindingViewModel binding)
    {
        if (_learning is not null)
        {
            _learning.IsLearning = false;
        }

        _learning = binding;
        binding.IsLearning = true;
        SetStatus(TrF($"Aprendendo \"{binding.Label}\": pise no pedal / mexa no controle da entrada MIDI agora."), StatusKind.Warning);
    }

    public void ClearBinding(BindingViewModel binding)
    {
        _bindings.Clear(binding.Action);
        binding.Refresh();
        SaveBindings();
    }

    public void SaveSettings() => _state.SaveSettings();

    /// <summary>Desliga o modo controle e libera as portas (também chamado ao fechar o programa).</summary>
    /// <summary>Modo simples: nome a mostrar em cada pedal a partir do número do programa.</summary>
    public Func<int, string?>? PedalNameProvider { get; set; }

    public void RefreshPedalNames()
    {
        foreach (var pedal in Pedals)
        {
            pedal.Refresh();
        }
    }

    /// <summary>Liga o modo controle se ainda não estiver ligado (o modo simples liga ao entrar em "Tocar").</summary>
    public void EnsureActive()
    {
        if (!IsActive && ToggleActiveCommand.CanExecute(null))
        {
            ToggleActiveCommand.Execute(null);
        }
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        _service.Stop();
        IsActive = false;
        if (_learning is not null)
        {
            _learning.IsLearning = false;
            _learning = null;
        }

        _state.EndOperation();
        SetStatus(Tr("Modo controle desativado. Portas MIDI liberadas para backup e restauração."), StatusKind.Idle);
        _state.StatusText = Tr("Modo controle desativado.");
    }

    private void ToggleActive()
    {
        if (IsActive)
        {
            Deactivate();
            return;
        }

        var output = _state.SelectedOutput;
        if (output is null)
        {
            _dialogs.ShowWarning(Tr("Modo controle"), Tr("Selecione o MIDI OUT conectado ao MIDI IN do TriAxis na aba Conexão."));
            return;
        }

        if (!_state.TryBeginOperation(OperationName, () => _service.Stop()))
        {
            return;
        }

        try
        {
            _service.Start(output, _selectedInput?.Device);
            IsActive = true;
            SetStatus(TrF($"Modo controle ATIVO no canal {Channel}. Use os pedais, o teclado (1–0, ↑ ↓, ← →) ou a entrada MIDI."), StatusKind.Ok);
            _state.StatusText = Tr("Modo controle ativo: backup e restauração ficam bloqueados enquanto ele estiver ligado.");
        }
        catch (MidiException ex)
        {
            _state.EndOperation();
            SetStatus(ex.Message, StatusKind.Error);
            _dialogs.ShowError(Tr("Modo controle"), ex.Message);
        }

        RefreshPanel();
    }

    private void SendProgram(int program, string source)
    {
        _service.SendProgramChange(Channel, program);
        _currentProgram = program;
        if (_banks.PedalFor(program) is null)
        {
            _banks.ShowProgram(program);
        }

        LastSent = TrF($"Program Change {program} (canal {Channel}) — {source}");
        RefreshPanel();
    }

    private void OnInput(ShortMessage message)
    {
        if (!IsActive)
        {
            return;
        }

        LastInput = message.ToString();

        if (_learning is not null)
        {
            var trigger = MidiTrigger.FromMessage(message);
            if (trigger is null)
            {
                return; // ex.: soltar o pedal (CC 0) não serve de gatilho
            }

            _bindings.Bind(_learning.Action, trigger);
            SaveBindings();
            foreach (var b in Bindings)
            {
                b.Refresh();
            }

            SetStatus(TrF($"\"{_learning.Label}\" agora responde a {trigger}."), StatusKind.Ok);
            _learning.IsLearning = false;
            _learning = null;
            return;
        }

        if (_bindings.Resolve(message) is ControlAction action)
        {
            Execute(action);
        }
        else if (ForwardProgramChanges && message.Kind == MidiMessageKind.ProgramChange)
        {
            try
            {
                SendProgram(message.Number + 1, Tr("repassado da entrada"));
            }
            catch (MidiException ex)
            {
                _log.Error("Modo controle: falha ao repassar.", ex);
                Deactivate();
                SetStatus(Tr("Falha ao enviar: ") + ex.Message, StatusKind.Error);
            }
        }
    }

    private void RefreshInputOptions()
    {
        var options = new List<InputOption> { new(null, Tr("(nenhuma — só tela e teclado)")) };
        options.AddRange(_connection.Inputs.Select(d => new InputOption(d, d.DisplayName)));
        InputOptions = options;
        SelectedInput = options.FirstOrDefault(o => o.Device?.Key == _state.Settings.ControlInputDeviceKey) ?? options[0];
    }

    private void BuildPedals()
    {
        Pedals.Clear();
        // Como na FCB1010: a fileira de cima tem os números maiores (6–10), a de baixo 1–5.
        var rows = Enumerable.Range(1, _banks.BankSize).Chunk(PedalColumns).Reverse();
        foreach (var number in rows.SelectMany(r => r))
        {
            Pedals.Add(new PedalViewModel(this, number));
        }

        OnPropertyChanged(nameof(PedalColumns));
    }

    private void RefreshPanel()
    {
        foreach (var pedal in Pedals)
        {
            pedal.Refresh();
        }

        OnPropertyChanged(nameof(BankDisplay));
        OnPropertyChanged(nameof(ProgramDisplay));
        OnPropertyChanged(nameof(BankRangeText));
        OnPropertyChanged(nameof(CurrentProgram));
        CommandManager.InvalidateRequerySuggested();
    }

    private void LoadBindings()
    {
        foreach (var s in _state.Settings.ControlBindings)
        {
            if (Enum.TryParse<ControlAction>(s.Action, out var action)
                && Enum.TryParse<MidiTriggerKind>(s.Kind, out var kind)
                && s.Channel is >= 1 and <= 16 && s.Number is >= 0 and <= 127)
            {
                _bindings.Bind(action, new MidiTrigger(kind, s.Channel, s.Number));
            }
        }
    }

    private void SaveBindings()
    {
        _state.Settings.ControlBindings = _bindings.Bindings
            .Select(p => new ControlBindingSetting { Action = p.Key.ToString(), Kind = p.Value.Kind.ToString(), Channel = p.Value.Channel, Number = p.Value.Number })
            .ToList();
        SaveSettings();
    }

    private void SetStatus(string text, StatusKind kind)
    {
        StatusText = text;
        StatusKind = kind;
    }
}
