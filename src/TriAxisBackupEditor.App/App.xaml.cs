using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.App.ViewModels;
using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Application.Connection;
using TriAxisBackupEditor.Application.Control;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Application.Restore;
using TriAxisBackupEditor.Midi.WinMm;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App;

/// <summary>Raiz de composição. Nada é enviado ao TriAxis na inicialização: apenas lista as portas MIDI.</summary>
public partial class App : System.Windows.Application
{
    private ITechLog? _log;
    private ConnectionViewModel? _connection;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplyCulture(new CultureInfo("pt-BR"), overrideXmlLanguage: false);

        var dialogs = new DialogService();
        try
        {
            var paths = AppPaths.CreateDefault();
            paths.EnsureCreated();

            var settingsStore = new SettingsStore(paths);
            var settings = settingsStore.Load();
            ApplyLanguage(settings);
            var log = new FileTechLog(paths.LogsFolder, settings.LoggingEnabled);
            _log = log;
            log.Info($"Iniciando TriAxis Backup & Editor {typeof(App).Assembly.GetName().Version} em {Environment.OSVersion}.");
            if (settingsStore.LastLoadError is { } loadError)
            {
                log.Warn(loadError);
            }

            InstallErrorHandlers(log, dialogs);

            var repository = new SqliteBackupRepository(paths.DatabasePath);
            repository.Initialize();

            var sysEx = new TriAxisSysExService();
            var midi = new WinMmMidiDeviceService();
            var state = new AppState(settings, settingsStore, paths);

            var library = new BackupLibraryService(repository, sysEx, new RecycleBin(), log);
            var capture = new BackupCaptureService(midi, log);
            var restore = new RestoreService(midi, sysEx, log);
            var tester = new ConnectionTestService(midi, log);
            var controlSurface = new ControlSurfaceService(midi, log);

            var logVm = new LogViewModel(log);
            _connection = new ConnectionViewModel(state, midi, tester, log);
            var backupVm = new BackupViewModel(state, capture, library, sysEx, dialogs, log);
            var restoreVm = new RestoreViewModel(state, restore, backupVm, dialogs, log);
            var libraryVm = new LibraryViewModel(state, library, dialogs);
            var viewerVm = new DumpViewerViewModel(state, sysEx, dialogs);
            var settingsVm = new SettingsViewModel(state, sysEx, dialogs, log);
            var editorVm = new EditorViewModel(state, library, dialogs, log);
            var controlVm = new ControlViewModel(state, controlSurface, _connection, dialogs, log);

            var simpleShell = new TriAxisBackupEditor.App.ViewModels.Simple.SimpleShellViewModel(state, midi, capture, library, restore, _connection, editorVm, controlVm, dialogs, log);
            var main = new MainViewModel(state, _connection, controlVm, backupVm, restoreVm, libraryVm, editorVm, viewerVm, settingsVm, logVm, simpleShell);

            libraryVm.Refresh();
            _connection.Start();

            var window = new global::TriAxisBackupEditor.App.Views.MainWindow { DataContext = main };
            MainWindow = window;
            window.Show();
            main.Start();
        }
        catch (Exception ex)
        {
            _log?.Error("Falha ao iniciar.", ex);
            dialogs.ShowError(Tr("TriAxis Backup & Editor"), Tr("Não foi possível iniciar o programa:\n\n") + ex.Message);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _connection?.Stop();
        _log?.Info("Programa encerrado.");
        base.OnExit(e);
        if (RestartRequested && Environment.ProcessPath is { } exe)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        }
    }

    /// <summary>Ao fechar, abre o programa de novo (usado ao trocar de idioma).</summary>
    public static bool RestartRequested { get; set; }

    /// <summary>Define o idioma (configuração ou idioma do Windows), a cultura dos números/datas e o tradutor das telas.</summary>
    private static void ApplyLanguage(AppSettings settings)
    {
        var language = Resolve(settings.Language, CultureInfo.InstalledUICulture);
        SetLanguage(language);
        ApplyCulture(new CultureInfo(language == TriAxisBackupEditor.Localization.AppLanguage.English ? "en-US" : "pt-BR"), overrideXmlLanguage: true);
    }

    private static void ApplyCulture(CultureInfo culture, bool overrideXmlLanguage)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        if (overrideXmlLanguage)
        {
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
        }
    }

    private void InstallErrorHandlers(ITechLog log, IDialogService dialogs)
    {
        AsyncRelayCommand.UnhandledError = ex =>
        {
            log.Error("Erro em comando.", ex);
            dialogs.ShowError(Tr("Erro"), ex.Message);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            log.Error("Erro não tratado na interface.", args.Exception);
            dialogs.ShowError(Tr("Erro inesperado"), args.Exception.Message + Tr("\n\nOs detalhes foram registrados no log."));
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            log.Error("Erro em tarefa de fundo.", args.Exception);
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                log.Error("Erro fatal.", ex);
            }
        };
    }
}
