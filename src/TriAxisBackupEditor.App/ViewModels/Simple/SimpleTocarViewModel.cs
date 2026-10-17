using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Presets;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.App.ViewModels.Simple;

/// <summary>
/// "Tocar": a pedaleira na tela, ligada ao entrar e desligada ao sair. Os pedais mostram o nome do timbre
/// que cada programa chama (segundo o último backup).
/// </summary>
public sealed class SimpleTocarViewModel : SimplePage
{
    private readonly AppState _state;
    private readonly IDialogService _dialogs;
    private TriAxisDump? _dump;

    public SimpleTocarViewModel(SimpleShellViewModel shell, AppState state, ControlViewModel control, IDialogService dialogs)
        : base(shell)
    {
        _state = state;
        _dialogs = dialogs;
        Control = control;
        RenamePedalCommand = new RelayCommandWithParameter<PedalViewModel>(RenamePedal);
    }

    public override string Title => "Tocar";

    public ControlViewModel Control { get; }

    public bool IsReady => _state.SelectedOutput is not null;

    public ICommand RenamePedalCommand { get; }

    public override void OnEnter()
    {
        _dump = Shell.LatestDump();
        Control.PedalNameProvider = program => PresetNaming.PedalLabel(program, _dump, _state.Settings.PresetNames);
        Control.RefreshPedalNames();
        if (IsReady)
        {
            Control.EnsureActive();
        }

        OnPropertyChanged(nameof(IsReady));
    }

    public override void OnLeave()
    {
        Control.Deactivate();
        Control.PedalNameProvider = null;
        Control.RefreshPedalNames();
    }

    private void RenamePedal(PedalViewModel? pedal)
    {
        if (pedal?.Program is not int program)
        {
            return;
        }

        var preset = PresetNaming.PresetForProgram(program, _dump);
        if (preset is null)
        {
            _dialogs.ShowInfo("Dar nome", "Para dar nomes aos timbres, guarde primeiro a memória do TriAxis (página Backup).");
            return;
        }

        var current = PresetNaming.NameFor(preset.Value, _state.Settings.PresetNames) ?? string.Empty;
        var name = _dialogs.PromptText("Dar nome ao timbre", $"Este pedal chama o timbre {preset.Value:00}. Nome (ex.: Base limpa, Solo):", current);
        if (name is null)
        {
            return;
        }

        _state.Settings.PresetNames[preset.Value - 1] = name.Trim();
        _state.SaveSettings();
        Control.RefreshPedalNames();
    }
}
