using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.Localization;
using TriAxisBackupEditor.Storage;
using SimpleShell = TriAxisBackupEditor.App.ViewModels.Simple.SimpleShellViewModel;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    // Ordem das abas em MainWindow.xaml.
    public const int TabConnection = 0;
    public const int TabControl = 1;
    public const int TabBackup = 2;
    public const int TabRestore = 3;
    public const int TabLibrary = 4;
    public const int TabEditor = 5;
    public const int TabViewer = 6;
    public const int TabSettings = 7;

    private int _selectedTab;

    public MainViewModel(
        AppState state,
        ConnectionViewModel connection,
        ControlViewModel control,
        BackupViewModel backup,
        RestoreViewModel restore,
        LibraryViewModel library,
        EditorViewModel editor,
        DumpViewerViewModel viewer,
        SettingsViewModel settings,
        LogViewModel log,
        SimpleShell simple)
    {
        State = state;
        Simple = simple;
        Connection = connection;
        Control = control;
        Backup = backup;
        Restore = restore;
        Library = library;
        Editor = editor;
        Viewer = viewer;
        Settings = settings;
        Log = log;

        backup.BackupSaved += (_, _) => library.Refresh();
        editor.LibraryChanged += (_, _) => library.Refresh();
        library.RestoreRequested += (_, path) => OpenInRestore(path);
        editor.SendRequested += (_, path) => OpenInRestore(path);
        library.ViewRequested += (_, path) =>
        {
            viewer.Load(path);
            SelectedTab = TabViewer;
        };
        library.EditRequested += (_, path) => OpenInEditor(path);
        viewer.EditRequested += (_, path) => OpenInEditor(path);
        settings.SettingsSaved += (_, _) =>
        {
            editor.NotifySwitchNamesChanged();
            backup.NotifySettingsChanged();
            restore.NotifySettingsChanged();
        };
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.SelectedInput) or nameof(AppState.SelectedOutput))
            {
                OnPropertyChanged(nameof(IsConnected));
            }
        };
    }

    // ---------------- Modo simples / avançado ----------------

    public SimpleShell Simple { get; }

    public bool IsSimpleMode
    {
        get => State.Settings.UiMode == AppSettings.SimpleMode;
        set => SetMode(value);
    }

    public bool IsAdvancedMode
    {
        get => !IsSimpleMode;
        set => SetMode(!value);
    }

    /// <summary>Selo do topo no modo simples: "TriAxis ligado" ou "Configurar ligação".</summary>
    public bool IsConnected => State.HasMidiSelection;

    /// <summary>Chamado na abertura do programa, depois que a janela aparece.</summary>
    public void Start()
    {
        if (IsSimpleMode)
        {
            Simple.OnModeEntered();
        }
    }

    private void SetMode(bool simple)
    {
        if (simple == IsSimpleMode)
        {
            return;
        }

        if (State.IsBusy && State.BusyOperation != ControlViewModel.OperationName)
        {
            State.StatusText = TrF($"Aguarde: {State.BusyOperationDisplay} em andamento.");
            RaiseModeChanged();
            return;
        }

        if (simple)
        {
            Control.Deactivate();
        }
        else if (!Simple.TryLeave())
        {
            State.StatusText = Tr("Aguarde a operação atual terminar para trocar de modo.");
            RaiseModeChanged();
            return;
        }

        State.Settings.UiMode = simple ? AppSettings.SimpleMode : AppSettings.AdvancedMode;
        State.SaveSettings();
        RaiseModeChanged();

        if (simple)
        {
            Simple.OnModeEntered();
        }
        else
        {
            Library.Refresh();
        }
    }

    private void RaiseModeChanged()
    {
        OnPropertyChanged(nameof(IsSimpleMode));
        OnPropertyChanged(nameof(IsAdvancedMode));
    }

    public string Title => Tr("TriAxis Backup & Editor 1.0 — por @palinetes");

    /// <summary>Abre o manual do usuário (embutido no programa) no navegador padrão. Também pela tecla F1.</summary>
    public System.Windows.Input.ICommand OpenManualCommand => _openManualCommand ??= new RelayCommand(() =>
    {
        Services.ManualService.Open(State.Paths.DataFolder);
        State.StatusText = Tr("Manual aberto no navegador.");
    });

    private System.Windows.Input.ICommand? _openManualCommand;

    /// <summary>Idioma da interface: o texto da tela é trocado no início do programa, então mudar pede para reiniciar.</summary>
    public System.Windows.Input.ICommand SetLanguageCommand => _setLanguageCommand ??= new RelayCommandWithParameter<string>(SetLanguage);

    private System.Windows.Input.ICommand? _setLanguageCommand;

    public bool IsEnglishUi => Localizer.IsEnglish;

    public bool IsPortugueseUi => !Localizer.IsEnglish;

    private void SetLanguage(string? code)
    {
        var wantsEnglish = code == "en";
        if (wantsEnglish == Localizer.IsEnglish)
        {
            return;
        }

        if (State.IsBusy)
        {
            State.StatusText = TrF($"Aguarde: {State.BusyOperationDisplay} em andamento.");
            return;
        }

        State.Settings.Language = wantsEnglish ? "en" : "pt";
        State.SaveSettings();

        // As frases já mostradas só mudam ao reabrir: oferece reiniciar agora.
        var restartText = wantsEnglish
            ? "Idioma alterado para English. Para aplicar, o programa precisa abrir de novo. Reiniciar agora?"
            : "Language changed to Português. To apply it, the program has to start again. Restart now?";
        var restart = Simple.Dialogs.Confirm(wantsEnglish ? "Language / Idioma" : "Idioma / Language", restartText);
        if (restart)
        {
            TriAxisBackupEditor.App.App.RestartRequested = true;
            System.Windows.Application.Current.Shutdown();
        }
    }

    public AppState State { get; }

    public ConnectionViewModel Connection { get; }

    public ControlViewModel Control { get; }

    public BackupViewModel Backup { get; }

    public RestoreViewModel Restore { get; }

    public LibraryViewModel Library { get; }

    public EditorViewModel Editor { get; }

    public DumpViewerViewModel Viewer { get; }

    public SettingsViewModel Settings { get; }

    public LogViewModel Log { get; }

    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value) && value == TabSettings)
            {
                Settings.Reload();
            }
        }
    }

    private void OpenInRestore(string path)
    {
        Restore.LoadFile(path);
        SelectedTab = TabRestore;
    }

    private void OpenInEditor(string path)
    {
        if (Editor.Load(path))
        {
            SelectedTab = TabEditor;
        }
    }
}
