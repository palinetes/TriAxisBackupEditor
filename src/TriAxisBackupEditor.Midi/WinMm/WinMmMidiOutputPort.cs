using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TriAxisBackupEditor.Midi.WinMm;

/// <summary>
/// MIDI OUT via winmm. Cada chamada a <see cref="SendRaw"/> é síncrona: espera o driver marcar o buffer
/// como concluído (MHDR_DONE) antes de liberar a memória — assim um bloco nunca é sobrescrito durante o envio.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WinMmMidiOutputPort : IMidiOutputPort
{
    private readonly object _sync = new();
    private IntPtr _handle;
    private bool _disposed;

    public WinMmMidiOutputPort(uint deviceId, MidiDeviceInfo device)
    {
        Device = device;
        var result = NativeMethods.midiOutOpen(out _handle, deviceId, IntPtr.Zero, IntPtr.Zero, NativeMethods.CALLBACK_NULL);
        WinMmErrors.Check(result, "abrir a saída MIDI", device);
    }

    public MidiDeviceInfo Device { get; }

    public void SendRaw(byte[] buffer, int offset, int count, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count <= 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Trecho fora do buffer.");
        }

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var data = Marshal.AllocHGlobal(count);
            var header = Marshal.AllocHGlobal(NativeMethods.MidiHdrSize);
            var canFree = true;
            try
            {
                Marshal.Copy(buffer, offset, data, count);
                var hdr = new NativeMethods.MIDIHDR
                {
                    lpData = data,
                    dwBufferLength = (uint)count,
                    dwBytesRecorded = (uint)count
                };
                Marshal.StructureToPtr(hdr, header, false);

                var size = (uint)NativeMethods.MidiHdrSize;
                WinMmErrors.Check(NativeMethods.midiOutPrepareHeader(_handle, header, size), "preparar envio para", Device);

                try
                {
                    WinMmErrors.Check(NativeMethods.midiOutLongMsg(_handle, header, size), "enviar SysEx para", Device);
                    WaitUntilDone(header, timeout);
                }
                finally
                {
                    canFree = Unprepare(header, size);
                }
            }
            finally
            {
                if (canFree)
                {
                    Marshal.FreeHGlobal(data);
                    Marshal.FreeHGlobal(header);
                }
            }
        }
    }

    public void SendShort(uint message)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            WinMmErrors.Check(NativeMethods.midiOutShortMsg(_handle, message), "enviar mensagem MIDI para", Device);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_handle != IntPtr.Zero)
            {
                // Sem midiOutReset: ele enviaria Note Off em todos os canais. Nada é enviado ao fechar.
                NativeMethods.midiOutClose(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }

    private void WaitUntilDone(IntPtr header, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while ((Marshal.ReadInt32(header, NativeMethods.MidiHdrFlagsOffset) & NativeMethods.MHDR_DONE) == 0)
        {
            if (sw.Elapsed > timeout)
            {
                throw new MidiException(
                    $"A interface \"{Device.DisplayName}\" não concluiu o envio em {timeout.TotalSeconds:0} s. Verifique o cabo/driver.");
            }

            Thread.Sleep(1);
        }
    }

    /// <returns>true se o buffer foi devolvido pelo driver e pode ser liberado.</returns>
    private bool Unprepare(IntPtr header, uint size)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var r = NativeMethods.midiOutUnprepareHeader(_handle, header, size);
            if (r != NativeMethods.MIDIERR_STILLPLAYING)
            {
                return true;
            }

            Thread.Sleep(5);
        }

        // Último recurso após timeout: força a devolução do buffer pelo driver.
        NativeMethods.midiOutReset(_handle);
        return NativeMethods.midiOutUnprepareHeader(_handle, header, size) != NativeMethods.MIDIERR_STILLPLAYING;
    }
}
