using System.Globalization;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels.Simple;

public sealed class SimpleHomeViewModel : SimplePage
{
    private static CultureInfo PtBr => CultureInfo.CurrentCulture;
    private readonly AppState _state;
    private string _lastBackupText = string.Empty;
    private bool _hasBackup;

    public SimpleHomeViewModel(SimpleShellViewModel shell, AppState state)
        : base(shell)
    {
        _state = state;
    }

    public override string Title => Tr("Início");

    public string LastBackupText
    {
        get => _lastBackupText;
        private set => SetProperty(ref _lastBackupText, value);
    }

    public bool HasBackup
    {
        get => _hasBackup;
        private set => SetProperty(ref _hasBackup, value);
    }

    public bool IsConnected => _state.HasMidiSelection;

    public override void OnEnter()
    {
        var latest = Shell.LatestBackup();
        HasBackup = latest is not null;
        LastBackupText = latest is null
            ? Tr("Você ainda não tem nenhum backup. Comece guardando a memória do seu TriAxis.")
            : TrF($"Último backup: {Friendly(latest.CreatedAt)}") + (string.IsNullOrWhiteSpace(latest.Notes) ? string.Empty : TrF($" · \"{latest.Notes}\""));
        OnPropertyChanged(nameof(IsConnected));
    }

    public static string Friendly(DateTime when)
    {
        var day = when.Date == DateTime.Today ? Tr("hoje")
            : when.Date == DateTime.Today.AddDays(-1) ? Tr("ontem")
            : when.ToString(Tr("dd/MM/yyyy"), PtBr);
        return TrF($"{day} às {when:HH:mm}");
    }
}
