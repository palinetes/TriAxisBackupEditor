using System.Globalization;
using System.Text;
using System.Windows.Input;
using TriAxisBackupEditor.App.Mvvm;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Storage;
using TriAxisBackupEditor.SysEx;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.ViewModels;

/// <summary>Um display + botões ◄ ► do painel (Gain, Treble, ...).</summary>
public sealed class KnobViewModel : ObservableObject
{
    private readonly EditorViewModel _owner;

    public KnobViewModel(EditorViewModel owner, PresetParameter parameter, int panelIndex)
    {
        _owner = owner;
        Parameter = parameter;
        // Como no aparelho: as setas dos 5 primeiros parâmetros também são o teclado numérico 0–9.
        KeyLeft = panelIndex < 5 ? (panelIndex * 2).ToString() : string.Empty;
        KeyRight = panelIndex < 5 ? ((panelIndex * 2) + 1).ToString() : string.Empty;
        Label = TriAxisValues.ParameterLabel(parameter);
        DecrementCommand = new RelayCommand(() => Step(-1), () => _owner.HasDump);
        IncrementCommand = new RelayCommand(() => Step(+1), () => _owner.HasDump);
    }

    public PresetParameter Parameter { get; }

    public string Label { get; }

    public string KeyLeft { get; }

    public string KeyRight { get; }

    public string Display => _owner.GetKnobDisplay(Parameter);

    public bool IsModified => _owner.IsParameterModified(Parameter);

    public IReadOnlyList<string> Options => TriAxisValues.KnobDisplay;

    public ICommand DecrementCommand { get; }

    public ICommand IncrementCommand { get; }

    public void Step(int delta) => _owner.StepKnob(Parameter, delta);

    public void SetIndex(int index) => _owner.SetKnob(Parameter, index);

    public void Refresh()
    {
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(IsModified));
    }
}

/// <summary>
/// Editor visual do dump (painel no estilo do TriAxis). Trabalha sobre uma cópia: o arquivo de origem nunca
/// é alterado e o resultado é salvo como um NOVO .syx, que segue para a restauração com confirmação em duas etapas.
/// </summary>
public sealed class EditorViewModel : ObservableObject
{
    private static CultureInfo PtBr => CultureInfo.CurrentCulture;

    private readonly AppState _state;
    private readonly BackupLibraryService _library;
    private readonly IDialogService _dialogs;
    private readonly ITechLog _log;
    private TriAxisDumpEditor? _editor;
    private string _sourcePath = string.Empty;
    private int _currentPreset = 1;
    private int _currentProgram = 1;
    private bool _assignMode;
    private bool _programMapValid;
    private byte[]? _clipboard;
    private int _clipboardFrom;
    private IReadOnlyList<TriAxisPresetView> _presets = Array.Empty<TriAxisPresetView>();
    private TriAxisPresetView? _selectedMatrixPreset;
    private string _changesText = string.Empty;
    private string _statusText = Tr("Abra um backup (.syx) para editar. O arquivo original nunca é alterado.");

    public EditorViewModel(AppState state, BackupLibraryService library, IDialogService dialogs, ITechLog log)
    {
        _state = state;
        _library = library;
        _dialogs = dialogs;
        _log = log;

        Knobs = TriAxisValues.PanelOrder.Select((p, i) => new KnobViewModel(this, p, i)).ToList();

        OpenFileCommand = new RelayCommand(OpenFile);
        OpenLatestCommand = new RelayCommand(OpenLatest);
        PresetPrevCommand = new RelayCommand(() => StepPreset(-1), () => HasDump);
        PresetNextCommand = new RelayCommand(() => StepPreset(+1), () => HasDump);
        ProgramPrevCommand = new RelayCommand(() => StepProgram(-1), () => HasDump);
        ProgramNextCommand = new RelayCommand(() => StepProgram(+1), () => HasDump);
        CopyPresetCommand = new RelayCommand(CopyPreset, () => HasDump);
        PastePresetCommand = new RelayCommand(PastePreset, () => HasDump && _clipboard is not null);
        RevertPresetCommand = new RelayCommand(RevertPreset, () => HasDump && _editor!.IsPresetModified(_currentPreset));
        RevertAllCommand = new RelayCommand(RevertAll, () => IsModified);
        SaveAsNewCommand = new RelayCommand(() => SaveAsNew(), () => IsModified && !_state.IsBusy);
        SendCommand = new RelayCommand(Send, () => HasDump && !_state.IsBusy);
        NextModeCommand = new RelayCommand(NextMode, () => HasDump);
        ToggleLoopCommand = new RelayCommand(() => FxLoop = !FxLoop, () => HasDump);
    }

    /// <summary>Pede para abrir um arquivo na aba Restaurar (fluxo com confirmação em duas etapas).</summary>
    public event EventHandler<string>? SendRequested;

    /// <summary>Um novo arquivo foi salvo na biblioteca.</summary>
    public event EventHandler? LibraryChanged;

    public IReadOnlyList<KnobViewModel> Knobs { get; }

    public bool HasDump => _editor is not null;

    public bool IsModified => _editor?.IsModified == true;

    public string SourcePath
    {
        get => _sourcePath;
        private set => SetProperty(ref _sourcePath, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ChangesText
    {
        get => _changesText;
        private set => SetProperty(ref _changesText, value);
    }

    public string PreliminaryWarning =>
        Tr("Editor baseado no mapa SysEx PRELIMINAR (editor de referência markphilpot/triaxiseditor), ainda não validado em hardware. ") +
        Tr("As edições geram um NOVO arquivo; o backup original nunca é alterado. Antes de confiar no editor, faça o teste de ida e volta do Plano de Testes (seção D).");

    // ---------------- Painel ----------------

    public string PresetDisplay => HasDump ? _currentPreset.ToString("00", PtBr) : "--";

    public string ProgramDisplay => HasDump ? _currentProgram.ToString(PtBr) : "---";

    public int CurrentPreset => _currentPreset;

    public string ProgramInfo
    {
        get
        {
            if (_editor is null)
            {
                return string.Empty;
            }

            if (!_programMapValid)
            {
                return TrF($"Program Change {_currentProgram} · ligação programa → timbre ainda não confirmada neste TriAxis");
            }

            var mapped = _editor.GetProgramPreset(_currentProgram);
            return mapped is int p
                ? TrF($"Program Change {_currentProgram} → preset {p:00}")
                : TrF($"Program Change {_currentProgram} → valor desconhecido");
        }
    }

    /// <summary>A ligação programa → timbre pode ser editada (só quando o mapa do arquivo parece válido).</summary>
    public bool CanAssignPrograms => _programMapValid;

    /// <summary>PGM/CH: com o modo ligado, as setas de PRESET mudam o preset associado ao programa atual.</summary>
    public bool AssignMode
    {
        get => _assignMode;
        set
        {
            if (value && !_programMapValid)
            {
                StatusText = Tr("A ligação programa → timbre deste TriAxis ainda não foi confirmada; por segurança ela não pode ser editada.");
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _assignMode, value))
            {
                StatusText = value
                    ? TrF($"Modo PGM/CH: as setas de PRESET agora escolhem qual preset o Program Change {_currentProgram} chama.")
                    : Tr("Modo PGM/CH desligado: as setas de PRESET navegam entre os presets.");
            }
        }
    }

    public TriAxisMode? Mode
    {
        get => _editor?.GetMode(_currentPreset);
        set
        {
            if (_editor is null || value is null || value == _editor.GetMode(_currentPreset))
            {
                return;
            }

            _editor.SetMode(_currentPreset, value.Value);
            AfterEdit();
        }
    }

    public string ModeText => _editor is null
        ? string.Empty
        : Mode is { } m ? TriAxisValues.ModeLabel(m) : TrF($"Modo desconhecido (0x{_editor.GetPresetByte(_currentPreset, TriAxisValues.ModeOffset):X2})");

    public bool FxLoop { get => GetSwitch(FxSwitch.FxLoop); set => SetSwitch(FxSwitch.FxLoop, value); }

    public bool Switch1 { get => GetSwitch(FxSwitch.Switch1); set => SetSwitch(FxSwitch.Switch1, value); }

    public bool Switch2 { get => GetSwitch(FxSwitch.Switch2); set => SetSwitch(FxSwitch.Switch2, value); }

    public bool Switch3 { get => GetSwitch(FxSwitch.Switch3); set => SetSwitch(FxSwitch.Switch3, value); }

    public bool Switch4 { get => GetSwitch(FxSwitch.Switch4); set => SetSwitch(FxSwitch.Switch4, value); }

    // ---------------- Matriz ----------------

    public IReadOnlyList<TriAxisPresetView> Presets
    {
        get => _presets;
        private set => SetProperty(ref _presets, value);
    }

    public TriAxisPresetView? SelectedMatrixPreset
    {
        get => _selectedMatrixPreset;
        set
        {
            if (SetProperty(ref _selectedMatrixPreset, value) && value is not null && value.Number != _currentPreset)
            {
                GoToPreset(value.Number);
            }
        }
    }

    // ---------------- Comandos ----------------

    public ICommand OpenFileCommand { get; }

    public ICommand OpenLatestCommand { get; }

    public ICommand PresetPrevCommand { get; }

    public ICommand PresetNextCommand { get; }

    public ICommand ProgramPrevCommand { get; }

    public ICommand ProgramNextCommand { get; }

    public ICommand CopyPresetCommand { get; }

    public ICommand PastePresetCommand { get; }

    public ICommand RevertPresetCommand { get; }

    public ICommand RevertAllCommand { get; }

    public ICommand SaveAsNewCommand { get; }

    public ICommand SendCommand { get; }

    /// <summary>Botão MODE do painel: passa para o próximo dos 8 modos.</summary>
    public ICommand NextModeCommand { get; }

    /// <summary>Botão LOOP SW do painel: liga/desliga o FX loop do preset.</summary>
    public ICommand ToggleLoopCommand { get; }

    // ---------------- Nomes das saídas (ex.: ligações com a Simul-Class 2:90) ----------------

    public string FxLoopName => SwitchName(0, Tr("FX LOOP"));

    public string Switch1Name => SwitchName(1, "SW1");

    public string Switch2Name => SwitchName(2, "SW2");

    public string Switch3Name => SwitchName(3, "SW3");

    public string Switch4Name => SwitchName(4, "SW4");

    /// <summary>Estado dos switches do preset atual, com os nomes dados pelo usuário.</summary>
    public string SwitchSummary => _editor is null
        ? string.Empty
        : string.Join("   ·   ", new[]
        {
            TrF($"{FxLoopName}: {OnOff(FxLoop)}"),
            TrF($"{Switch1Name}: {OnOff(Switch1)}"),
            TrF($"{Switch2Name}: {OnOff(Switch2)}"),
            TrF($"{Switch3Name}: {OnOff(Switch3)}"),
            TrF($"{Switch4Name}: {OnOff(Switch4)}")
        });

    public void NotifySwitchNamesChanged()
    {
        OnPropertyChanged(nameof(FxLoopName));
        OnPropertyChanged(nameof(Switch1Name));
        OnPropertyChanged(nameof(Switch2Name));
        OnPropertyChanged(nameof(Switch3Name));
        OnPropertyChanged(nameof(Switch4Name));
        OnPropertyChanged(nameof(SwitchSummary));
    }

    private static string OnOff(bool value) => value ? Tr("ligado") : Tr("desligado");

    private string SwitchName(int index, string fallback)
    {
        var names = _state.Settings.SwitchNames;
        var name = index < names.Count ? names[index] : null;
        return string.IsNullOrWhiteSpace(name) ? fallback : TrF($"{fallback} · {name.Trim()}");
    }

    private void NextMode()
    {
        var modes = Enum.GetValues<TriAxisMode>();
        Mode = Mode is TriAxisMode current ? modes[((int)current + 1) % modes.Length] : modes[0];
    }

    /// <summary>Abre um .syx no editor. Pergunta antes de descartar edições não salvas.</summary>
    public bool Load(string path)
    {
        if (!ConfirmDiscard())
        {
            return false;
        }

        TriAxisFileContent content;
        try
        {
            content = TriAxisDumpFile.Normalize(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Editor", Tr("Não foi possível ler o arquivo: ") + ex.Message);
            return false;
        }

        if (!TriAxisDumpEditor.TryCreate(content.Data, out var editor, out var reason))
        {
            _dialogs.ShowError("Editor", reason + Tr("\n\nO editor só abre dumps completos capturados do TriAxis."));
            return false;
        }

        _editor = editor;
        SourcePath = path;
        _currentPreset = 1;
        _currentProgram = 1;
        AssignMode = false;
        _log.Info($"Editor: aberto \"{path}\" (cópia em memória; o arquivo não será alterado)." + (content.Note is null ? string.Empty : " " + content.Note));
        StatusText = content.WasPadded
            ? TrF($"Editando uma cópia de {Path.GetFileName(path)} (arquivo do editor web: usando a parte válida de {TriAxisDumpLayout.TotalLength} bytes).")
            : TrF($"Editando uma cópia de {Path.GetFileName(path)}. Use ◄ ► (ou a roda do mouse / clique no display) para mudar valores.");
        RefreshAll();
        return true;
    }

    /// <summary>true se não há edições pendentes ou o usuário aceitou descartá-las.</summary>
    public bool ConfirmDiscard() =>
        !IsModified || _dialogs.Confirm("Editor", Tr("Há edições não salvas no editor. Descartar as alterações?"));

    public string GetKnobDisplay(PresetParameter parameter)
    {
        if (_editor is null)
        {
            return "--";
        }

        var index = _editor.GetKnob(_currentPreset, parameter);
        return index is int i ? TriAxisValues.KnobDisplay[i] : "??";
    }

    public bool IsParameterModified(PresetParameter parameter) =>
        _editor is not null && OriginalByte((int)parameter) != _editor.GetPresetByte(_currentPreset, (int)parameter);

    public void StepKnob(PresetParameter parameter, int delta)
    {
        if (_editor is null)
        {
            return;
        }

        var current = _editor.GetKnob(_currentPreset, parameter);
        var next = current is int i ? Math.Clamp(i + delta, 0, TriAxisValues.KnobCount - 1) : 0;
        SetKnob(parameter, next);
    }

    public void SetKnob(PresetParameter parameter, int index)
    {
        if (_editor is null || _editor.GetKnob(_currentPreset, parameter) == index)
        {
            return;
        }

        _editor.SetKnob(_currentPreset, parameter, index);
        AfterEdit();
    }

    private byte OriginalByte(int relative) =>
        _editor!.OriginalBytes()[TriAxisDumpLayout.PresetsOffset + ((_currentPreset - 1) * TriAxisDumpLayout.PresetLength) + relative];

    private bool GetSwitch(FxSwitch flag) => _editor?.GetSwitch(_currentPreset, flag) == true;

    private void SetSwitch(FxSwitch flag, bool on)
    {
        if (_editor is null || _editor.GetSwitch(_currentPreset, flag) == on)
        {
            return;
        }

        _editor.SetSwitch(_currentPreset, flag, on);
        AfterEdit();
    }

    private void StepPreset(int delta)
    {
        if (_editor is null)
        {
            return;
        }

        var next = Wrap(_currentPreset + delta, TriAxisDumpLayout.PresetCount);
        if (AssignMode)
        {
            _editor.SetProgramPreset(_currentProgram, next);
            _currentPreset = next;
            AfterEdit();
            StatusText = TrF($"Program Change {_currentProgram} agora chama o preset {next:00}.");
            return;
        }

        GoToPreset(next);
    }

    private void StepProgram(int delta)
    {
        if (_editor is null)
        {
            return;
        }

        _currentProgram = Wrap(_currentProgram + delta, TriAxisDumpLayout.ProgramCount);
        if (_programMapValid && _editor.GetProgramPreset(_currentProgram) is int mapped)
        {
            _currentPreset = mapped;
        }

        RefreshPanel();
    }

    private void GoToPreset(int preset)
    {
        _currentPreset = preset;
        RefreshPanel();
    }

    private void CopyPreset()
    {
        _clipboard = _editor!.GetPresetBytes(_currentPreset);
        _clipboardFrom = _currentPreset;
        StatusText = TrF($"Preset {_currentPreset:00} copiado. Vá a outro preset e clique em \"Colar\".");
    }

    private void PastePreset()
    {
        if (_clipboard is null)
        {
            return;
        }

        _editor!.SetPresetBytes(_currentPreset, _clipboard);
        AfterEdit();
        StatusText = TrF($"Preset {_clipboardFrom:00} colado sobre o preset {_currentPreset:00}.");
    }

    private void RevertPreset()
    {
        _editor!.RevertPreset(_currentPreset);
        AfterEdit();
        StatusText = TrF($"Preset {_currentPreset:00} voltou ao valor do arquivo original.");
    }

    private void RevertAll()
    {
        if (!_dialogs.Confirm("Editor", Tr("Desfazer TODAS as alterações e voltar ao arquivo original?")))
        {
            return;
        }

        _editor!.RevertAll();
        AfterEdit();
        StatusText = Tr("Todas as alterações foram desfeitas.");
    }

    /// <summary>Salva o dump editado como arquivo novo e passa a editar a partir dele.</summary>
    private BackupRecord? SaveAsNew()
    {
        if (_editor is null || !_editor.IsModified)
        {
            return null;
        }

        var notes = _dialogs.PromptText(Tr("Salvar dump editado"), Tr("Observação do novo arquivo:"), DefaultEditNotes());
        return notes is null ? null : SaveCore(notes);
    }

    /// <summary>
    /// Modo simples: salva as edições como arquivo novo sem perguntar nada e devolve o caminho a enviar
    /// (ou o arquivo atual, se não houve mudança). Null se não há timbres abertos ou a gravação falhou.
    /// </summary>
    public string? PrepareFileForSending()
    {
        if (_editor is null)
        {
            return null;
        }

        return _editor.IsModified ? SaveCore(DefaultEditNotes())?.FilePath : SourcePath;
    }

    public void SelectPreset(int preset)
    {
        if (_editor is not null && preset is >= 1 and <= TriAxisDumpLayout.PresetCount && preset != _currentPreset)
        {
            GoToPreset(preset);
        }
    }

    private string DefaultEditNotes()
    {
        var changes = _editor!.GetChanges();
        var presets = changes.ChangedPresets.Count > 0 ? Tr("timbres ") + string.Join(", ", changes.ChangedPresets.Select(p => p.ToString("00"))) : Tr("mapa de programas");
        return TrF($"Editado ({presets})");
    }

    private BackupRecord? SaveCore(string notes)
    {
        if (_editor is null)
        {
            return null;
        }

        try
        {
            var record = _library.SaveEditedDump(_editor.ToArray(), _state.Settings.BackupFolder, notes, DateTime.Now);
            LibraryChanged?.Invoke(this, EventArgs.Empty);

            var keepPreset = _currentPreset;
            var keepProgram = _currentProgram;
            _editor = null; // as alterações já estão no arquivo novo: recarrega sem perguntar
            Load(record.FilePath);
            _currentPreset = keepPreset;
            _currentProgram = keepProgram;
            RefreshPanel();

            StatusText = TrF($"Salvo como {record.FileName}. O arquivo original não foi alterado.");
            _state.StatusText = Tr("Timbres editados salvos: ") + record.FileName;
            return record;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _dialogs.ShowError(Tr("Salvar dump editado"), ex.Message);
            return null;
        }
    }

    private void Send()
    {
        if (_editor is null)
        {
            return;
        }

        var path = SourcePath;
        if (_editor.IsModified)
        {
            if (!_dialogs.Confirm(Tr("Enviar ao TriAxis"),
                    Tr("As alterações precisam ser salvas como um novo arquivo antes do envio.\n\nSalvar agora e abrir a aba Restaurar?")))
            {
                return;
            }

            var record = SaveAsNew();
            if (record is null)
            {
                return;
            }

            path = record.FilePath;
        }

        SendRequested?.Invoke(this, path);
    }

    private void OpenFile()
    {
        var path = _dialogs.OpenSyxFile(_state.Settings.BackupFolder);
        if (path is not null)
        {
            Load(path);
        }
    }

    private void OpenLatest()
    {
        var latest = _library.Search(null).FirstOrDefault(r => r.FileExists && r.Origin == BackupOrigins.Captured);
        if (latest is null)
        {
            _dialogs.ShowInfo("Editor", Tr("Nenhum backup capturado na biblioteca ainda. Faça um backup primeiro (aba Backup)."));
            return;
        }

        Load(latest.FilePath);
    }

    private void AfterEdit()
    {
        RefreshPanel();
        RefreshMatrixAndChanges();
    }

    private void RefreshAll()
    {
        RefreshMatrixAndChanges();
        RefreshPanel();
        OnPropertyChanged(nameof(HasDump));
    }

    private void RefreshPanel()
    {
        foreach (var knob in Knobs)
        {
            knob.Refresh();
        }

        OnPropertyChanged(nameof(PresetDisplay));
        OnPropertyChanged(nameof(ProgramDisplay));
        OnPropertyChanged(nameof(ProgramInfo));
        OnPropertyChanged(nameof(CurrentPreset));
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(ModeText));
        OnPropertyChanged(nameof(FxLoop));
        OnPropertyChanged(nameof(Switch1));
        OnPropertyChanged(nameof(Switch2));
        OnPropertyChanged(nameof(Switch3));
        OnPropertyChanged(nameof(Switch4));
        OnPropertyChanged(nameof(SwitchSummary));
        OnPropertyChanged(nameof(IsModified));

        var row = Presets.FirstOrDefault(p => p.Number == _currentPreset);
        if (!ReferenceEquals(row, _selectedMatrixPreset))
        {
            _selectedMatrixPreset = row;
            OnPropertyChanged(nameof(SelectedMatrixPreset));
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshMatrixAndChanges()
    {
        if (_editor is null)
        {
            Presets = Array.Empty<TriAxisPresetView>();
            ChangesText = string.Empty;
            return;
        }

        var bytes = _editor.ToArray();
        Presets = TriAxisDumpParser.TryParse(bytes, out var dump, out _) ? dump!.Presets : Array.Empty<TriAxisPresetView>();
        // O mapa só é confiável se parecer um mapa de verdade no arquivo ORIGINAL (não muda com as edições).
        _programMapValid = TriAxisDumpParser.TryParse(_editor.OriginalBytes(), out var original, out _) && original!.ProgramMapLooksValid;
        OnPropertyChanged(nameof(CanAssignPrograms));
        ChangesText = DescribeChanges(_editor.GetChanges());
        OnPropertyChanged(nameof(IsModified));
    }

    private static string DescribeChanges(DumpComparison changes)
    {
        if (changes.Identical)
        {
            return Tr("Nenhuma alteração em relação ao arquivo original.");
        }

        var sb = new StringBuilder();
        sb.AppendLine(TrF($"{changes.DifferenceCount} byte(s) alterado(s):"));
        foreach (var d in changes.Differences)
        {
            sb.AppendLine(DescribeDifference(d));
        }

        return sb.ToString();
    }

    private static string DescribeDifference(ByteDifference d)
    {
        var before = d.A ?? 0;
        var after = d.B ?? 0;
        if (d.Offset >= TriAxisDumpLayout.PresetsOffset && d.Offset < TriAxisDumpLayout.ProgramsOffset)
        {
            var relative = (d.Offset - TriAxisDumpLayout.PresetsOffset) % TriAxisDumpLayout.PresetLength;
            var preset = ((d.Offset - TriAxisDumpLayout.PresetsOffset) / TriAxisDumpLayout.PresetLength) + 1;
            var name = TriAxisDumpLayout.PresetParameterNames[relative];
            string Value(byte b) => relative switch
            {
                < TriAxisValues.ModeOffset => TriAxisValues.KnobToDisplay(b) ?? $"0x{b:X2}",
                TriAxisValues.ModeOffset => TriAxisValues.ModeFromByte(b) is { } m ? TriAxisValues.ModeLabel(m) : $"0x{b:X2}",
                _ => FxText(b)
            };
            return TrF($"Preset {preset:00} · {name}: {Value(before)} → {Value(after)}");
        }

        if (d.Offset >= TriAxisDumpLayout.ProgramsOffset && d.Offset < TriAxisDumpLayout.TrailerOffset)
        {
            var program = d.Offset - TriAxisDumpLayout.ProgramsOffset + 1;
            return TrF($"Program Change {program:000}: preset {before + 1:00} → {after + 1:00}");
        }

        return d.ToString();
    }

    private static string FxText(byte b)
    {
        var parts = new List<string>();
        if ((b & 0x01) != 0) parts.Add("FX");
        if ((b & 0x02) != 0) parts.Add("SW1");
        if ((b & 0x04) != 0) parts.Add("SW2");
        if ((b & 0x08) != 0) parts.Add("SW3");
        if ((b & 0x10) != 0) parts.Add("SW4");
        return parts.Count == 0 ? Tr("nenhum") : string.Join("+", parts);
    }

    private static int Wrap(int value, int count) => ((value - 1 + count) % count) + 1;
}
