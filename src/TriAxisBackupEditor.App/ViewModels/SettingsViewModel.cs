using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly ITriAxisSysExService _sysEx;
    private readonly IDialogService _dialogs;
    private readonly ITechLog _log;
    private AppSettings _edit;
    private string _message = string.Empty;

    public SettingsViewModel(AppState state, ITriAxisSysExService sysEx, IDialogService dialogs, ITechLog log)
    {
        _state = state;
        _sysEx = sysEx;
        _dialogs = dialogs;
        _log = log;
        _edit = state.Settings.Clone();

        SaveCommand = new RelayCommand(Save, () => !_state.IsBusy);
        ResetCommand = new RelayCommand(Reset, () => !_state.IsBusy);
        Apply290NamesCommand = new RelayCommand(Apply290Names);
        ChooseFolderCommand = new RelayCommand(ChooseFolder);
        OpenDataFolderCommand = new RelayCommand(() => AppState.OpenInExplorer(_state.Paths.DataFolder));
    }

    /// <summary>Disparado depois de salvar, para as outras abas atualizarem.</summary>
    public event EventHandler? SettingsSaved;

    public AppSettings Edit
    {
        get => _edit;
        private set => SetProperty(ref _edit, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string DataFolder => _state.Paths.DataFolder;

    public string DatabasePath => _state.Paths.DatabasePath;

    public string LogsFolder => _state.Paths.LogsFolder;

    public ICommand SaveCommand { get; }

    public ICommand ResetCommand { get; }

    /// <summary>Preenche os nomes das saídas com as três entradas "Voicing Switch" da Simul-Class 2:90 (SW1–SW3).</summary>
    public ICommand Apply290NamesCommand { get; }

    public ICommand ChooseFolderCommand { get; }

    public ICommand OpenDataFolderCommand { get; }

    public void Reload()
    {
        Edit = _state.Settings.Clone();
        Message = string.Empty;
    }

    private void Save()
    {
        if (Edit.DumpRequestEnabled)
        {
            try
            {
                _sysEx.ParseRequestMessage(Edit.DumpRequestHex);
            }
            catch (FormatException ex)
            {
                _dialogs.ShowError(Tr("Requisição de dump"), ex.Message);
                return;
            }

            if (!_state.Settings.DumpRequestEnabled &&
                !_dialogs.Confirm(Tr("Requisição de dump"),
                    Tr("Não existe comando de requisição de dump documentado para o TriAxis neste projeto.\n") +
                    Tr("Com esta opção, a mensagem informada será ENVIADA ao TriAxis no início de cada backup.\n\n") +
                    Tr("Use apenas uma mensagem de fonte confiável. Ativar mesmo assim?")))
            {
                return;
            }
        }

        var saved = Edit.Clone();
        saved.Normalize(_state.Paths.DefaultBackupFolder);
        // Campos editados em outras abas (Conexão e Modo controle) são mantidos como estão agora.
        var current = _state.Settings;
        saved.LastInputDeviceKey = current.LastInputDeviceKey;
        saved.LastOutputDeviceKey = current.LastOutputDeviceKey;
        saved.ControlMidiChannel = current.ControlMidiChannel;
        saved.ControlBankSize = current.ControlBankSize;
        saved.ControlInputDeviceKey = current.ControlInputDeviceKey;
        saved.ControlForwardProgramChanges = current.ControlForwardProgramChanges;
        saved.ControlBindings = current.ControlBindings;
        saved.ControlCcButtons = current.ControlCcButtons;
        _state.Settings = saved;
        _state.SaveSettings();
        _log.FileLoggingEnabled = saved.LoggingEnabled;
        _log.Info($"Configurações salvas: blocos {saved.ChunkSize} bytes, intervalo {saved.ChunkDelayMs} ms, início {saved.StartTimeoutSeconds} s, " +
                  TrF($"recepção {saved.ReceiveTimeoutSeconds} s, log em arquivo {(saved.LoggingEnabled ? Tr("ligado") : Tr("desligado"))}, ") +
                  TrF($"requisição {(saved.DumpRequestEnabled ? Tr("ATIVADA") : Tr("desativada"))}."));
        Edit = saved.Clone();
        Message = Tr("Configurações salvas.");
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    private void Apply290Names()
    {
        // Pelo manual da 2:90: as três entradas de voicing são Deep, 1/2 Drive e Modern, e o manual do TriAxis manda ligar
        // os switches 1, 2 e 3 nelas. A ordem é a sugerida; só o usuário sabe como ligou os cabos, por isso é um botão.
        var copy = Edit.Clone();
        copy.SwitchNames[1] = Tr("2:90 Deep");
        copy.SwitchNames[2] = Tr("2:90 1/2 Drive");
        copy.SwitchNames[3] = Tr("2:90 Modern");
        Edit = copy;
        Message = Tr("Nomes da 2:90 preenchidos (SW1 Deep, SW2 1/2 Drive, SW3 Modern). Se os seus cabos estiverem em outra ordem, ajuste os nomes. Clique em Salvar para aplicar.");
    }

    private void Reset()
    {
        var defaults = new AppSettings { BackupFolder = Edit.BackupFolder }.Normalize(_state.Paths.DefaultBackupFolder);
        Edit = defaults;
        Message = Tr("Valores padrão carregados. Clique em Salvar para aplicar.");
    }

    private void ChooseFolder()
    {
        var folder = _dialogs.ChooseFolder(Edit.BackupFolder);
        if (folder is not null)
        {
            Edit.BackupFolder = folder;
            var refreshed = Edit;
            Edit = refreshed.Clone();
        }
    }
}

public sealed class LogViewModel : ObservableObject
{
    private const int MaxEntries = 3000;
    private readonly ITechLog _log;
    private readonly Dispatcher _dispatcher;

    public LogViewModel(ITechLog log)
    {
        _log = log;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _log.EntryWritten += OnEntry;

        ClearCommand = new RelayCommand(() => Entries.Clear());
        CopyCommand = new RelayCommand(() => Clipboard.SetText(string.Join(Environment.NewLine, Entries)), () => Entries.Count > 0);
        OpenFolderCommand = new RelayCommand(() =>
        {
            Directory.CreateDirectory(_log.LogFolder);
            AppState.OpenInExplorer(_log.LogFolder);
        });
    }

    public ObservableCollection<string> Entries { get; } = new();

    public string LogFolder => _log.LogFolder;

    public ICommand ClearCommand { get; }

    public ICommand CopyCommand { get; }

    public ICommand OpenFolderCommand { get; }

    private void OnEntry(object? sender, LogEntry entry)
    {
        _dispatcher.BeginInvoke(() =>
        {
            Entries.Add(entry.ToString());
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
            }
        });
    }
}
