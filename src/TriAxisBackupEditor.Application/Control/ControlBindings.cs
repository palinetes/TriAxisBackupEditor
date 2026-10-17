namespace TriAxisBackupEditor.Application.Control;

/// <summary>Ações do modo controle que podem ser disparadas por teclado ou por uma entrada MIDI.</summary>
public enum ControlAction
{
    Pedal1,
    Pedal2,
    Pedal3,
    Pedal4,
    Pedal5,
    Pedal6,
    Pedal7,
    Pedal8,
    Pedal9,
    Pedal10,
    BankUp,
    BankDown,
    ProgramUp,
    ProgramDown,
    CcButton1,
    CcButton2,
    CcButton3,
    CcButton4
}

public static class ControlActions
{
    public static IReadOnlyList<ControlAction> All { get; } = Enum.GetValues<ControlAction>();

    public static int? PedalNumber(ControlAction action) =>
        action is >= ControlAction.Pedal1 and <= ControlAction.Pedal10 ? action - ControlAction.Pedal1 + 1 : null;

    public static int? CcButtonNumber(ControlAction action) =>
        action is >= ControlAction.CcButton1 and <= ControlAction.CcButton4 ? action - ControlAction.CcButton1 + 1 : null;

    public static string Label(ControlAction action) => action switch
    {
        _ when PedalNumber(action) is int p => $"Pedal {p}",
        _ when CcButtonNumber(action) is int c => $"Botão CC {c}",
        ControlAction.BankUp => "Banco ▲",
        ControlAction.BankDown => "Banco ▼",
        ControlAction.ProgramUp => "Programa +1",
        ControlAction.ProgramDown => "Programa −1",
        _ => action.ToString()
    };
}

public enum MidiTriggerKind
{
    ProgramChange,
    ControlChange,
    Note
}

/// <summary>
/// Mensagem de entrada que dispara uma ação (aprendida com "MIDI learn").
/// CC e notas disparam só no "pressionar" (valor/velocidade &gt; 0), como em pedaleiras que mandam 127/0.
/// </summary>
public sealed record MidiTrigger(MidiTriggerKind Kind, int Channel, int Number)
{
    public bool Matches(ShortMessage message) => message.Channel == Channel && message.Number == Number && Kind switch
    {
        MidiTriggerKind.ProgramChange => message.Kind == MidiMessageKind.ProgramChange,
        MidiTriggerKind.ControlChange => message.Kind == MidiMessageKind.ControlChange && message.Value > 0,
        MidiTriggerKind.Note => message.Kind == MidiMessageKind.NoteOn,
        _ => false
    };

    /// <summary>Cria o gatilho a partir de uma mensagem recebida; null para mensagens que não servem de gatilho.</summary>
    public static MidiTrigger? FromMessage(ShortMessage message) => message.Kind switch
    {
        MidiMessageKind.ProgramChange => new MidiTrigger(MidiTriggerKind.ProgramChange, message.Channel, message.Number),
        MidiMessageKind.ControlChange when message.Value > 0 => new MidiTrigger(MidiTriggerKind.ControlChange, message.Channel, message.Number),
        MidiMessageKind.NoteOn => new MidiTrigger(MidiTriggerKind.Note, message.Channel, message.Number),
        _ => null
    };

    public override string ToString() => Kind switch
    {
        MidiTriggerKind.ProgramChange => $"PC {Number + 1} · canal {Channel}",
        MidiTriggerKind.ControlChange => $"CC {Number} · canal {Channel}",
        _ => $"Nota {Number} · canal {Channel}"
    };
}

/// <summary>Mapa ação ↔ gatilho MIDI. Um gatilho só pode estar ligado a uma ação.</summary>
public sealed class ControlBindingMap
{
    private readonly Dictionary<ControlAction, MidiTrigger> _bindings = new();

    public IReadOnlyDictionary<ControlAction, MidiTrigger> Bindings => _bindings;

    public MidiTrigger? Get(ControlAction action) => _bindings.TryGetValue(action, out var trigger) ? trigger : null;

    /// <summary>Liga o gatilho à ação, removendo-o de qualquer outra ação que o usava.</summary>
    public void Bind(ControlAction action, MidiTrigger trigger)
    {
        foreach (var existing in _bindings.Where(p => p.Value == trigger && p.Key != action).Select(p => p.Key).ToList())
        {
            _bindings.Remove(existing);
        }

        _bindings[action] = trigger;
    }

    public void Clear(ControlAction action) => _bindings.Remove(action);

    public ControlAction? Resolve(ShortMessage message)
    {
        foreach (var (action, trigger) in _bindings)
        {
            if (trigger.Matches(message))
            {
                return action;
            }
        }

        return null;
    }
}
