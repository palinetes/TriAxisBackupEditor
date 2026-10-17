using System.Runtime.InteropServices;
using System.Text;

namespace TriAxisBackupEditor.Midi.WinMm;

/// <summary>P/Invoke da API MIDI do Windows (winmm.dll), disponível do Windows 95 ao Windows 11.</summary>
internal static class NativeMethods
{
    public const int MMSYSERR_NOERROR = 0;
    public const int MMSYSERR_BADDEVICEID = 2;
    public const int MMSYSERR_ALLOCATED = 4;
    public const int MMSYSERR_INVALHANDLE = 5;
    public const int MMSYSERR_NODRIVER = 6;
    public const int MMSYSERR_NOMEM = 7;
    public const int MIDIERR_STILLPLAYING = 65;
    public const int MIDIERR_NODEVICE = 68;

    public const uint CALLBACK_NULL = 0x00000;
    public const uint CALLBACK_FUNCTION = 0x30000;

    public const uint MIM_OPEN = 0x3C1;
    public const uint MIM_CLOSE = 0x3C2;
    public const uint MIM_DATA = 0x3C3;
    public const uint MIM_LONGDATA = 0x3C4;
    public const uint MIM_ERROR = 0x3C5;
    public const uint MIM_LONGERROR = 0x3C6;
    public const uint MIM_MOREDATA = 0x3CC;

    public const int MHDR_DONE = 0x00000001;

    public delegate void MidiInProc(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MIDIINCAPS
    {
        public ushort wMid;
        public ushort wPid;
        public uint vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public uint dwSupport;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MIDIOUTCAPS
    {
        public ushort wMid;
        public ushort wPid;
        public uint vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public ushort wTechnology;
        public ushort wVoices;
        public ushort wNotes;
        public ushort wChannelMask;
        public uint dwSupport;
    }

    /// <summary>MIDIHDR (blittable). Alocado em memória não gerenciada para ter endereço fixo enquanto o driver o usa.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MIDIHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public int dwFlags;
        public IntPtr lpNext;
        public IntPtr reserved;
        public uint dwOffset;
        public IntPtr dwReserved0;
        public IntPtr dwReserved1;
        public IntPtr dwReserved2;
        public IntPtr dwReserved3;
        public IntPtr dwReserved4;
        public IntPtr dwReserved5;
        public IntPtr dwReserved6;
        public IntPtr dwReserved7;
    }

    public static readonly int MidiHdrSize = Marshal.SizeOf<MIDIHDR>();

    public static readonly int MidiHdrFlagsOffset = (int)Marshal.OffsetOf<MIDIHDR>(nameof(MIDIHDR.dwFlags));

    // ---- MIDI IN ----

    [DllImport("winmm.dll")]
    public static extern uint midiInGetNumDevs();

    [DllImport("winmm.dll", EntryPoint = "midiInGetDevCapsW", CharSet = CharSet.Unicode)]
    public static extern int midiInGetDevCaps(UIntPtr uDeviceID, ref MIDIINCAPS caps, uint cbMidiInCaps);

    [DllImport("winmm.dll")]
    public static extern int midiInOpen(out IntPtr lphMidiIn, uint uDeviceID, MidiInProc dwCallback, IntPtr dwInstance, uint dwFlags);

    [DllImport("winmm.dll")]
    public static extern int midiInClose(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    public static extern int midiInStart(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    public static extern int midiInStop(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    public static extern int midiInReset(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    public static extern int midiInPrepareHeader(IntPtr hMidiIn, IntPtr lpMidiInHdr, uint cbMidiInHdr);

    [DllImport("winmm.dll")]
    public static extern int midiInUnprepareHeader(IntPtr hMidiIn, IntPtr lpMidiInHdr, uint cbMidiInHdr);

    [DllImport("winmm.dll")]
    public static extern int midiInAddBuffer(IntPtr hMidiIn, IntPtr lpMidiInHdr, uint cbMidiInHdr);

    [DllImport("winmm.dll", EntryPoint = "midiInGetErrorTextW", CharSet = CharSet.Unicode)]
    public static extern int midiInGetErrorText(int mmrError, StringBuilder pszText, uint cchText);

    // ---- MIDI OUT ----

    [DllImport("winmm.dll")]
    public static extern uint midiOutGetNumDevs();

    [DllImport("winmm.dll", EntryPoint = "midiOutGetDevCapsW", CharSet = CharSet.Unicode)]
    public static extern int midiOutGetDevCaps(UIntPtr uDeviceID, ref MIDIOUTCAPS caps, uint cbMidiOutCaps);

    [DllImport("winmm.dll")]
    public static extern int midiOutOpen(out IntPtr lphMidiOut, uint uDeviceID, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);

    [DllImport("winmm.dll")]
    public static extern int midiOutClose(IntPtr hMidiOut);

    [DllImport("winmm.dll")]
    public static extern int midiOutReset(IntPtr hMidiOut);

    [DllImport("winmm.dll")]
    public static extern int midiOutPrepareHeader(IntPtr hMidiOut, IntPtr lpMidiOutHdr, uint cbMidiOutHdr);

    [DllImport("winmm.dll")]
    public static extern int midiOutUnprepareHeader(IntPtr hMidiOut, IntPtr lpMidiOutHdr, uint cbMidiOutHdr);

    [DllImport("winmm.dll")]
    public static extern int midiOutShortMsg(IntPtr hMidiOut, uint dwMsg);

    [DllImport("winmm.dll")]
    public static extern int midiOutLongMsg(IntPtr hMidiOut, IntPtr lpMidiOutHdr, uint cbMidiOutHdr);

    [DllImport("winmm.dll", EntryPoint = "midiOutGetErrorTextW", CharSet = CharSet.Unicode)]
    public static extern int midiOutGetErrorText(int mmrError, StringBuilder pszText, uint cchText);
}
