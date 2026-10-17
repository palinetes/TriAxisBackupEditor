using System.Globalization;

namespace TriAxisBackupEditor.App.ViewModels.Simple;

public sealed class SimpleHomeViewModel : SimplePage
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    private readonly AppState _state;
    private string _lastBackupText = string.Empty;
    private bool _hasBackup;

    public SimpleHomeViewModel(SimpleShellViewModel shell, AppState state)
        : base(shell)
    {
        _state = state;
    }

    public override string Title => "Início";

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
            ? "Você ainda não tem nenhum backup. Comece guardando a memória do seu TriAxis."
            : $"Último backup: {Friendly(latest.CreatedAt)}" + (string.IsNullOrWhiteSpace(latest.Notes) ? string.Empty : $" · \"{latest.Notes}\"");
        OnPropertyChanged(nameof(IsConnected));
    }

    public static string Friendly(DateTime when)
    {
        var day = when.Date == DateTime.Today ? "hoje"
            : when.Date == DateTime.Today.AddDays(-1) ? "ontem"
            : when.ToString("dd/MM/yyyy", PtBr);
        return $"{day} às {when:HH:mm}";
    }
}
