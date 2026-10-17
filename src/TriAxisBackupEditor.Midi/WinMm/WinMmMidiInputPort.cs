using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TriAxisBackupEditor.Midi.WinMm;

/// <summary>
/// MIDI IN via winmm com buffers de SysEx. Mensagens maiores que um buffer chegam em vários MIM_LONGDATA:
/// os fragmentos são entregues em ordem e remontados pela camada SysEx (SysExAssembler).
/// O callback do driver só copia os bytes e enfileira; eventos e devolução de buffers ao driver
/// acontecem numa thread própria (a documentação do winmm proíbe chamadas MIDI dentro do callback).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WinMmMidiInputPort : IMidiInputPort
{
    private const int BufferCount = 16;
    private const int BufferSize = 4096;

    private readonly NativeMethods.MidiInProc _callback; // referência mantida viva enquanto a porta existir
    private readonly List<IntPtr> _headers = new();
    private readonly BlockingCollection<InputItem> _queue = new();
    private readonly object _sync = new();
    private readonly Thread _worker;
    private IntPtr _handle;
    private bool _closing;
    private bool _disposed;

    public WinMmMidiInputPort(uint deviceId, MidiDeviceInfo device)
    {
        Device = device;
        _callback = OnMidiIn;
        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "MIDI IN " + device.DisplayName };

        var result = NativeMethods.midiInOpen(out _handle, deviceId, _callback, IntPtr.Zero, NativeMethods.CALLBACK_FUNCTION);
        WinMmErrors.Check(result, "abrir a entrada MIDI", device);

        try
        {
            for (var i = 0; i < BufferCount; i++)
            {
                AddNewBuffer();
            }

            _worker.Start();
            WinMmErrors.Check(NativeMethods.midiInStart(_handle), "iniciar a entrada MIDI", device);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public MidiDeviceInfo Device { get; }

    public event EventHandler<MidiSysExChunkEventArgs>? SysExChunkReceived;

    public event EventHandler<MidiShortMessageEventArgs>? ShortMessageReceived;

    public event EventHandler<MidiErrorEventArgs>? ErrorOccurred;

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _closing = true;
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.midiInStop(_handle);
            NativeMethods.midiInReset(_handle); // devolve todos os buffers pendentes (marcados como DONE)
        }

        var leaked = false;
        foreach (var header in _headers)
        {
            if (_handle != IntPtr.Zero && !UnprepareWithRetry(header))
            {
                // O driver ainda pode escrever neste buffer: vazar é mais seguro do que liberar.
                leaked = true;
                continue;
            }

            FreeHeader(header);
        }

        _headers.Clear();

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.midiInClose(_handle);
            _handle = IntPtr.Zero;
        }

        _queue.CompleteAdding();
        if (_worker.IsAlive && Thread.CurrentThread != _worker)
        {
            _worker.Join(TimeSpan.FromSeconds(2));
        }

        if (leaked)
        {
            ErrorOccurred?.Invoke(this, new MidiErrorEventArgs("Alguns buffers MIDI não puderam ser liberados pelo driver."));
        }

        GC.KeepAlive(_callback);
    }

    private void AddNewBuffer()
    {
        var data = Marshal.AllocHGlobal(BufferSize);
        var header = Marshal.AllocHGlobal(NativeMethods.MidiHdrSize);
        var hdr = new NativeMethods.MIDIHDR { lpData = data, dwBufferLength = BufferSize };
        Marshal.StructureToPtr(hdr, header, false);

        var result = NativeMethods.midiInPrepareHeader(_handle, header, (uint)NativeMethods.MidiHdrSize);
        if (result != NativeMethods.MMSYSERR_NOERROR)
        {
            Marshal.FreeHGlobal(data);
            Marshal.FreeHGlobal(header);
            throw WinMmErrors.Create(result, "preparar buffer de SysEx em", Device);
        }

        _headers.Add(header);
        result = NativeMethods.midiInAddBuffer(_handle, header, (uint)NativeMethods.MidiHdrSize);
        if (result != NativeMethods.MMSYSERR_NOERROR)
        {
            throw WinMmErrors.Create(result, "registrar buffer de SysEx em", Device);
        }
    }

    private bool UnprepareWithRetry(IntPtr header)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var r = NativeMethods.midiInUnprepareHeader(_handle, header, (uint)NativeMethods.MidiHdrSize);
            if (r != NativeMethods.MIDIERR_STILLPLAYING)
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    private static void FreeHeader(IntPtr header)
    {
        var hdr = Marshal.PtrToStructure<NativeMethods.MIDIHDR>(header);
        if (hdr.lpData != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(hdr.lpData);
        }

        Marshal.FreeHGlobal(header);
    }

    // Executado na thread do driver: nada de chamadas winmm nem exceções escapando.
    private void OnMidiIn(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
    {
        try
        {
            switch (wMsg)
            {
                case NativeMethods.MIM_DATA:
                case NativeMethods.MIM_MOREDATA:
                    _queue.TryAdd(InputItem.Short((uint)dwParam1.ToInt64()));
                    break;

                case NativeMethods.MIM_LONGDATA:
                case NativeMethods.MIM_LONGERROR:
                {
                    var hdr = Marshal.PtrToStructure<NativeMethods.MIDIHDR>(dwParam1);
                    var length = (int)Math.Min(hdr.dwBytesRecorded, hdr.dwBufferLength);
                    var bytes = new byte[length];
                    if (length > 0)
                    {
                        Marshal.Copy(hdr.lpData, bytes, 0, length);
                    }

                    _queue.TryAdd(InputItem.Long(dwParam1, bytes, wMsg == NativeMethods.MIM_LONGERROR));
                    break;
                }

                case NativeMethods.MIM_ERROR:
                    _queue.TryAdd(InputItem.Error($"Mensagem MIDI inválida recebida ({(uint)dwParam1.ToInt64():X6})."));
                    break;

                case NativeMethods.MIM_CLOSE:
                    _queue.TryAdd(InputItem.Closed());
                    break;
            }
        }
        catch
        {
            // Fila já encerrada (porta fechando) ou falha de cópia: nunca propagar para o driver.
        }
    }

    private void WorkerLoop()
    {
        foreach (var item in _queue.GetConsumingEnumerable())
        {
            try
            {
                switch (item.Kind)
                {
                    case InputKind.Long:
                        if (item.Data.Length > 0)
                        {
                            SysExChunkReceived?.Invoke(this, new MidiSysExChunkEventArgs(item.Data, item.IsError));
                        }

                        RequeueBuffer(item.Header);
                        break;

                    case InputKind.Short:
                        ShortMessageReceived?.Invoke(this, new MidiShortMessageEventArgs(item.ShortMessage));
                        break;

                    case InputKind.Error:
                        ErrorOccurred?.Invoke(this, new MidiErrorEventArgs(item.Text));
                        break;

                    case InputKind.Closed:
                        bool closing;
                        lock (_sync)
                        {
                            closing = _closing;
                        }

                        if (!closing)
                        {
                            ErrorOccurred?.Invoke(this, new MidiErrorEventArgs("A entrada MIDI foi fechada pelo driver (interface desconectada?)."));
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    ErrorOccurred?.Invoke(this, new MidiErrorEventArgs("Erro ao processar dados MIDI: " + ex.Message));
                }
                catch
                {
                    // Assinante com defeito não pode derrubar a thread de recepção.
                }
            }
        }
    }

    private void RequeueBuffer(IntPtr header)
    {
        int result;
        lock (_sync)
        {
            if (_closing)
            {
                return;
            }

            result = NativeMethods.midiInAddBuffer(_handle, header, (uint)NativeMethods.MidiHdrSize);
        }

        if (result != NativeMethods.MMSYSERR_NOERROR)
        {
            ErrorOccurred?.Invoke(this, new MidiErrorEventArgs(
                "Não foi possível devolver o buffer de SysEx ao driver: " + WinMmErrors.Describe(result, MidiDirection.Input)));
        }
    }

    private enum InputKind
    {
        Short,
        Long,
        Error,
        Closed
    }

    private readonly record struct InputItem(InputKind Kind, uint ShortMessage, IntPtr Header, byte[] Data, bool IsError, string Text)
    {
        public static InputItem Short(uint message) => new(InputKind.Short, message, IntPtr.Zero, Array.Empty<byte>(), false, string.Empty);

        public static InputItem Long(IntPtr header, byte[] data, bool isError) => new(InputKind.Long, 0, header, data, isError, string.Empty);

        public static InputItem Error(string text) => new(InputKind.Error, 0, IntPtr.Zero, Array.Empty<byte>(), true, text);

        public static InputItem Closed() => new(InputKind.Closed, 0, IntPtr.Zero, Array.Empty<byte>(), false, string.Empty);
    }
}
