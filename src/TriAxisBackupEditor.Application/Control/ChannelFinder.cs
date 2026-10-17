namespace TriAxisBackupEditor.Application.Control;

public enum ChannelFinderState
{
    /// <summary>Primeiro teste: mandar a troca de programa nos 16 canais ao mesmo tempo.</summary>
    TestingAll,

    /// <summary>A ligação funciona; dividindo os canais candidatos ao meio a cada resposta.</summary>
    Narrowing,

    /// <summary>Canal encontrado.</summary>
    Found,

    /// <summary>O TriAxis não reagiu nem com os 16 canais: problema de cabo, porta ou configuração de recepção.</summary>
    NoResponse
}

/// <summary>
/// Descobre em que canal MIDI o TriAxis recebe, só com perguntas "mudou de programa?".
/// 1º teste: todos os 16 canais (separa "ligação com problema" de "canal errado").
/// Depois, busca binária: no máximo 4 perguntas. Trocar de programa não altera nenhum timbre.
/// </summary>
public sealed class ChannelFinder
{
    private List<int> _candidates = Enumerable.Range(1, 16).ToList();

    public ChannelFinderState State { get; private set; } = ChannelFinderState.TestingAll;

    public int? FoundChannel { get; private set; }

    public int QuestionsAsked { get; private set; }

    /// <summary>Canais que devem receber a troca de programa no teste atual.</summary>
    public IReadOnlyList<int> CurrentProbe => State switch
    {
        ChannelFinderState.TestingAll => _candidates,
        ChannelFinderState.Narrowing => _candidates.Take(_candidates.Count / 2).ToList(),
        _ => Array.Empty<int>()
    };

    public IReadOnlyList<int> Candidates => _candidates;

    /// <summary>Resposta do usuário: o display do TriAxis mudou depois do teste atual?</summary>
    public void Answer(bool changed)
    {
        QuestionsAsked++;
        switch (State)
        {
            case ChannelFinderState.TestingAll:
                State = changed ? ChannelFinderState.Narrowing : ChannelFinderState.NoResponse;
                break;

            case ChannelFinderState.Narrowing:
                var half = _candidates.Count / 2;
                _candidates = changed ? _candidates.Take(half).ToList() : _candidates.Skip(half).ToList();
                break;

            default:
                return;
        }

        if (State == ChannelFinderState.Narrowing && _candidates.Count == 1)
        {
            FoundChannel = _candidates[0];
            State = ChannelFinderState.Found;
        }
    }

    public void Restart()
    {
        _candidates = Enumerable.Range(1, 16).ToList();
        State = ChannelFinderState.TestingAll;
        FoundChannel = null;
        QuestionsAsked = 0;
    }
}
