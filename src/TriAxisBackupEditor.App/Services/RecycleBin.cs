using System.Runtime.InteropServices;
using TriAxisBackupEditor.Application.Backup;

namespace TriAxisBackupEditor.App.Services;

/// <summary>Envia arquivos para a Lixeira do Windows (recuperável), nunca apaga definitivamente.</summary>
public sealed class RecycleBin : IFileRecycler
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    public void MoveToRecycleBin(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var operation = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = Path.GetFullPath(path) + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
        };

        var result = SHFileOperation(ref operation);
        if (result != 0 || operation.fAnyOperationsAborted)
        {
            throw new IOException($"Não foi possível enviar \"{path}\" para a Lixeira (código {result}). O arquivo foi mantido.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
