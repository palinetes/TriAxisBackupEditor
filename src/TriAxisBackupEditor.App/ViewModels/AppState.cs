using System.Diagnostics;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.Storage;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels;

/// <summary>Estado compartilhado entre as abas: portas selecionadas, configurações e operação em andamento.</summary>
public sealed class AppState : ObservableObject
{
    private MidiDeviceInfo? _selectedInput;
    private MidiDeviceInfo? _selectedOutput;
    private bool _isBusy;
    private string _busyOperation = string.Empty;
    private string _statusText = Tr("Pronto. Nenhum dado é enviado ao TriAxis sem uma ação sua.");
    private AppSettings _settings;

    public AppState(AppSettings settings, SettingsStore settingsStore, AppPaths paths)
    {
        _settings = settings;
        SettingsStore = settingsStore;
        Paths = paths;
    }

    public SettingsStore SettingsStore { get; }

    public AppPaths Paths { get; }

    public AppSettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }

    public MidiDeviceInfo? SelectedInput
    {
        get => _selectedInput;
        set
        {
            if (SetProperty(ref _selectedInput, value))
            {
                OnPropertyChanged(nameof(HasMidiSelection));
            }
        }
    }

    public MidiDeviceInfo? SelectedOutput
    {
        get => _selectedOutput;
        set
        {
            if (SetProperty(ref _selectedOutput, value))
            {
                OnPropertyChanged(nameof(HasMidiSelection));
            }
        }
    }

    public bool HasMidiSelection => SelectedInput is not null && SelectedOutput is not null;

    /// <summary>Backup, restauração ou teste em andamento: as outras operações MIDI ficam bloqueadas.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string BusyOperation
    {
        get => _busyOperation;
        private set
        {
            if (SetProperty(ref _busyOperation, value))
            {
                OnPropertyChanged(nameof(BusyOperationDisplay));
            }
        }
    }

    /// <summary>Nome da operação no idioma da tela (<see cref="BusyOperation"/> guarda o texto-fonte para comparações).</summary>
    public string BusyOperationDisplay => Tr(_busyOperation);

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <param name="interrupt">
    /// Para operações que só leem ou esperam (backup, teste): como cancelá-las. Elas podem ser interrompidas
    /// ao fechar o programa. Envios ao TriAxis passam null e bloqueiam o fechamento até terminar.
    /// </param>
    public bool TryBeginOperation(string name, Action? interrupt = null)
    {
        if (IsBusy)
        {
            return false;
        }

        BusyOperation = name;
        _interrupt = interrupt;
        IsBusy = true;
        return true;
    }

    public void EndOperation()
    {
        IsBusy = false;
        BusyOperation = string.Empty;
        _interrupt = null;
    }

    /// <summary>A operação atual pode ser cancelada sem risco para o TriAxis.</summary>
    public bool CanInterrupt => IsBusy && _interrupt is not null;

    public void Interrupt() => _interrupt?.Invoke();

    private Action? _interrupt;

    public void SaveSettings()
    {
        try
        {
            SettingsStore.Save(Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = Tr("Não foi possível salvar as configurações: ") + ex.Message;
        }
    }

    public static void OpenInExplorer(string path)
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        else if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        else
        {
            throw new DirectoryNotFoundException(TrF($"\"{path}\" não existe."));
        }
    }
}
