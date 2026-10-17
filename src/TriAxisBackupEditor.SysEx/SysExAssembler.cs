namespace TriAxisBackupEditor.SysEx;

/// <summary>
/// Remonta mensagens SysEx a partir de fragmentos de tamanho arbitrário, como os entregues pelo driver MIDI
/// (MIM_LONGDATA chega em pedaços do tamanho do buffer). Não assume que uma mensagem chega em um único evento.
/// Não é thread-safe: o chamador serializa as chamadas a <see cref="Feed"/>.
/// </summary>
public sealed class SysExAssembler
{
    /// <summary>Limite de proteção contra fluxo infinito sem F7.</summary>
    public const int DefaultMaxMessageLength = 1024 * 1024;

    private readonly List<byte> _current = new(4096);
    private readonly int _maxMessageLength;

    public SysExAssembler(int maxMessageLength = DefaultMaxMessageLength)
    {
        _maxMessageLength = maxMessageLength;
    }

    public bool IsInMessage { get; private set; }

    /// <summary>Bytes acumulados da mensagem em andamento (ainda sem F7).</summary>
    public int CurrentLength => _current.Count;

    public long TotalBytesFed { get; private set; }

    /// <summary>Bytes F8–FF intercalados descartados (não pertencem ao SysEx pela especificação MIDI).</summary>
    public int RealTimeBytesDiscarded { get; private set; }

    /// <summary>Bytes fora de qualquer mensagem SysEx, ignorados.</summary>
    public int StrayBytesDiscarded { get; private set; }

    public int AbortedMessages { get; private set; }

    /// <summary>Última mensagem interrompida (sem F7), para salvar como arquivo temporário.</summary>
    public byte[]? LastAbortedMessage { get; private set; }

    /// <summary>Alimenta um fragmento e devolve as mensagens completadas por ele (F0 ... F7 inclusive).</summary>
    public IReadOnlyList<byte[]> Feed(ReadOnlySpan<byte> chunk)
    {
        List<byte[]>? completed = null;
        TotalBytesFed += chunk.Length;

        foreach (var b in chunk)
        {
            if (SysExConstants.IsRealTime(b))
            {
                RealTimeBytesDiscarded++;
                continue;
            }

            if (!IsInMessage)
            {
                if (b == SysExConstants.Start)
                {
                    _current.Clear();
                    _current.Add(b);
                    IsInMessage = true;
                }
                else
                {
                    StrayBytesDiscarded++;
                }

                continue;
            }

            if (b == SysExConstants.End)
            {
                _current.Add(b);
                (completed ??= new List<byte[]>()).Add(_current.ToArray());
                _current.Clear();
                IsInMessage = false;
            }
            else if (b == SysExConstants.Start)
            {
                Abort();
                _current.Add(b);
                IsInMessage = true;
            }
            else if (SysExConstants.IsStatus(b))
            {
                Abort();
                StrayBytesDiscarded++;
            }
            else
            {
                _current.Add(b);
                if (_current.Count > _maxMessageLength)
                {
                    Abort();
                }
            }
        }

        return (IReadOnlyList<byte[]>?)completed ?? Array.Empty<byte[]>();
    }

    /// <summary>Cópia da mensagem em andamento (incompleta).</summary>
    public byte[] GetPartialMessage() => _current.ToArray();

    public void Reset()
    {
        _current.Clear();
        IsInMessage = false;
        TotalBytesFed = 0;
        RealTimeBytesDiscarded = 0;
        StrayBytesDiscarded = 0;
        AbortedMessages = 0;
        LastAbortedMessage = null;
    }

    private void Abort()
    {
        if (_current.Count > 0)
        {
            LastAbortedMessage = _current.ToArray();
            AbortedMessages++;
        }

        _current.Clear();
        IsInMessage = false;
    }
}
