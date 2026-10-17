using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Application.Restore;
using TriAxisBackupEditor.Midi;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.App.ViewModels.Simple;

/// <summary>Navegação do modo simples: Início → Meus timbres / Backup / Tocar, assistente e envio seguro.</summary>
public sealed class SimpleShellViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly BackupCaptureService _capture;
    private readonly BackupLibraryService _library;
    private readonly RestoreService _restore;
    private readonly ITechLog _log;
    private SimplePage _currentPage;

    public SimpleShellViewModel(
        AppState state,
        IMidiDeviceService midi,
        BackupCaptureService capture,
        BackupLibraryService library,
        RestoreService restore,
        ConnectionViewModel connection,
        EditorViewModel editor,
        ControlViewModel control,
        IDialogService dialogs,
        ITechLog log)
    {
        _state = state;
        _capture = capture;
        _library = library;
        _restore = restore;
        _log = log;
        Midi = midi;
        Connection = connection;
        Dialogs = dialogs;

        Home = new SimpleHomeViewModel(this, state);
        Timbres = new SimpleTimbresViewModel(this, state, editor, library, dialogs);
        Backup = new SimpleBackupViewModel(this, state, library, dialogs);
        Tocar = new SimpleTocarViewModel(this, state, control, dialogs);
        _currentPage = Home;

        GoHomeCommand = new RelayCommand(GoHome, () => _currentPage != Home && _currentPage.CanLeave);
        GoTimbresCommand = new RelayCommand(() => Navigate(Timbres));
        GoBackupCommand = new RelayCommand(() => Navigate(Backup));
        GoTocarCommand = new RelayCommand(() => Navigate(Tocar));
        OpenWizardCommand = new RelayCommand(OpenWizard, () => !_state.IsBusy || _state.BusyOperation == ControlViewModel.OperationName);
        OpenChannelFinderCommand = new RelayCommand(OpenChannelFinder, () => !_state.IsBusy || _state.BusyOperation == ControlViewModel.OperationName);
    }

    public IMidiDeviceService Midi { get; }

    public ConnectionViewModel Connection { get; }

    public IDialogService Dialogs { get; }

    public SimpleHomeViewModel Home { get; }

    public SimpleTimbresViewModel Timbres { get; }

    public SimpleBackupViewModel Backup { get; }

    public SimpleTocarViewModel Tocar { get; }

    public SimplePage CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(IsHome));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsHome => CurrentPage == Home;

    public ICommand GoHomeCommand { get; }

    public ICommand GoTimbresCommand { get; }

    public ICommand GoBackupCommand { get; }

    public ICommand GoTocarCommand { get; }

    public ICommand OpenWizardCommand { get; }

    public ICommand OpenChannelFinderCommand { get; }

    /// <summary>Chamado quando o usuário entra no modo simples (ou o programa abre nele).</summary>
    public void OnModeEntered()
    {
        if (!_state.Settings.FirstRunCompleted)
        {
            OpenWizard();
        }
        else
        {
            CurrentPage.OnEnter();
        }
    }

    /// <summary>Sai da página atual (ex.: ao trocar para o modo avançado). Falso se ela não pode ser interrompida.</summary>
    public bool TryLeave()
    {
        if (!CurrentPage.CanLeave)
        {
            return false;
        }

        CurrentPage.OnLeave();
        return true;
    }

    public void Navigate(SimplePage page)
    {
        if (page == CurrentPage)
        {
            page.OnEnter();
            return;
        }

        if (!CurrentPage.CanLeave)
        {
            Dialogs.ShowInfo("Aguarde", "Espere a operação atual terminar.");
            return;
        }

        CurrentPage.OnLeave();
        CurrentPage = page;
        page.OnEnter();
    }

    public void GoHome() => Navigate(Home);

    public void OpenWizard() => Navigate(new SetupWizardViewModel(this, _state, Midi, Connection, Dialogs, _log));

    /// <summary>Abre o assistente direto no teste que descobre o número (canal) do TriAxis.</summary>
    public void OpenChannelFinder()
    {
        var wizard = new SetupWizardViewModel(this, _state, Midi, Connection, Dialogs, _log);
        Navigate(wizard);
        wizard.StartAtSendTest();
    }

    public void FinishWizard()
    {
        _state.Settings.FirstRunCompleted = true;
        _state.SaveSettings();
        GoHome();
    }

    public CaptureCardViewModel NewCaptureCard(string notes) => new(_state, _capture, _library, _log, notes);

    /// <summary>Abre o envio seguro (aviso → backup de segurança → substituir) para um arquivo.</summary>
    public void StartSafeSend(string filePath, string description, SimplePage returnTo) =>
        Navigate(new SafeSendViewModel(this, _state, _restore, _log, filePath, description, returnTo));

    /// <summary>Backup mais recente (capturado ou editado) que ainda existe e está no formato do TriAxis.</summary>
    public BackupRecord? LatestBackup() =>
        _library.Search(null).FirstOrDefault(r => r.FileExists && r.SizeBytes == TriAxisDumpLayout.TotalLength);

    public TriAxisDump? LatestDump()
    {
        var latest = LatestBackup();
        if (latest is null)
        {
            return null;
        }

        try
        {
            return TriAxisDumpParser.TryParse(File.ReadAllBytes(latest.FilePath), out var dump, out _) ? dump : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
