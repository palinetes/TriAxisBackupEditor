using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Application.Presets;
using TriAxisBackupEditor.SysEx;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels.Simple;

public sealed class TimbreRow : ObservableObject
{
    private string _label;

    public TimbreRow(int number, string label)
    {
        Number = number;
        _label = label;
    }

    public int Number { get; }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }
}

/// <summary>
/// "Meus timbres": o painel do TriAxis (mesmo EditorViewModel do modo avançado) com a lista dos 90 timbres
/// e nomes dados pelo usuário. Abre sozinho o backup mais recente.
/// </summary>
public sealed class SimpleTimbresViewModel : SimplePage
{
    private readonly AppState _state;
    private readonly BackupLibraryService _library;
    private readonly IDialogService _dialogs;
    private TimbreRow? _selected;
    private bool _syncing;

    public SimpleTimbresViewModel(SimpleShellViewModel shell, AppState state, EditorViewModel editor, BackupLibraryService library, IDialogService dialogs)
        : base(shell)
    {
        _state = state;
        _library = library;
        _dialogs = dialogs;
        Editor = editor;

        for (var i = 1; i <= TriAxisDumpLayout.PresetCount; i++)
        {
            Rows.Add(new TimbreRow(i, PresetNaming.PresetLabel(i, state.Settings.PresetNames)));
        }

        Editor.PropertyChanged += OnEditorChanged;

        RenameCommand = new RelayCommand(Rename, () => Editor.HasDump);
        UndoCommand = new RelayCommand(Undo, () => Editor.IsModified);
        SendCommand = new RelayCommand(Send, () => Editor.HasDump && !_state.IsBusy);
        MakeBackupCommand = new RelayCommand(() => Shell.Navigate(Shell.Backup));
    }

    public override string Title => Tr("Meus timbres");

    public EditorViewModel Editor { get; }

    public string ModifiedText => Editor.IsModified
        ? Tr("Você fez mudanças que ainda não foram para o TriAxis.")
        : Tr("Os timbres na tela são iguais aos do último backup.");

    public ObservableCollection<TimbreRow> Rows { get; } = new();

    public TimbreRow? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value) && value is not null && !_syncing)
            {
                Editor.SelectPreset(value.Number);
            }

            OnPropertyChanged(nameof(CurrentName));
        }
    }

    public string CurrentName => Selected is null ? string.Empty : Selected.Label;

    public bool HasDump => Editor.HasDump;

    public ICommand RenameCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand SendCommand { get; }

    public ICommand MakeBackupCommand { get; }

    public override void OnEnter()
    {
        if (!Editor.HasDump && Shell.LatestBackup() is { } latest)
        {
            Editor.Load(latest.FilePath);
        }

        RefreshNames();
        SyncSelection();
        OnPropertyChanged(nameof(HasDump));
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.IsModified))
        {
            OnPropertyChanged(nameof(ModifiedText));
        }

        if (e.PropertyName is nameof(EditorViewModel.CurrentPreset) or nameof(EditorViewModel.HasDump))
        {
            SyncSelection();
            OnPropertyChanged(nameof(HasDump));
        }
    }

    private void SyncSelection()
    {
        _syncing = true;
        try
        {
            Selected = Editor.HasDump ? Rows.FirstOrDefault(r => r.Number == Editor.CurrentPreset) : null;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RefreshNames()
    {
        foreach (var row in Rows)
        {
            row.Label = PresetNaming.PresetLabel(row.Number, _state.Settings.PresetNames);
        }

        OnPropertyChanged(nameof(CurrentName));
    }

    private void Rename()
    {
        var number = Editor.CurrentPreset;
        var current = PresetNaming.NameFor(number, _state.Settings.PresetNames) ?? string.Empty;
        var name = _dialogs.PromptText(Tr("Dar nome ao timbre"), TrF($"Nome do timbre {number:00} (ex.: Base limpa, Solo):"), current);
        if (name is null)
        {
            return;
        }

        _state.Settings.PresetNames[number - 1] = name.Trim();
        _state.SaveSettings();
        RefreshNames();
    }

    private void Undo()
    {
        Editor.RevertAllCommand.Execute(null);
    }

    private void Send()
    {
        var path = Editor.PrepareFileForSending();
        if (path is null)
        {
            return;
        }

        Shell.StartSafeSend(path, Tr("os timbres que estão na tela"), this);
    }
}
