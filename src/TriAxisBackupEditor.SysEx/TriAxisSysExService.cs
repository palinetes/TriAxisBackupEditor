namespace TriAxisBackupEditor.SysEx;

/// <summary>Fachada da camada SysEx usada pela aplicação. Ponto de extensão para o parser real (docs/SYSEX.md).</summary>
public interface ITriAxisSysExService
{
    SysExValidationResult Validate(byte[] data);

    bool TryParseDump(byte[] data, out TriAxisDump? dump, out string reason);

    string ComputeSha256(byte[] data);

    string FormatHexDump(byte[] data);

    DumpComparison Compare(byte[] a, byte[] b);

    /// <summary>Converte o texto hexadecimal de uma mensagem de requisição e exige uma única mensagem SysEx íntegra.</summary>
    byte[] ParseRequestMessage(string hex);

    SysExAssembler CreateAssembler();
}

public sealed class TriAxisSysExService : ITriAxisSysExService
{
    public SysExValidationResult Validate(byte[] data) => SysExValidator.Validate(data);

    public bool TryParseDump(byte[] data, out TriAxisDump? dump, out string reason) =>
        TriAxisDumpParser.TryParse(data, out dump, out reason);

    public string ComputeSha256(byte[] data) => SysExHash.Sha256Hex(data);

    public string FormatHexDump(byte[] data) => HexFormatter.FormatDump(data);

    public DumpComparison Compare(byte[] a, byte[] b) => DumpComparer.Compare(a, b);

    public byte[] ParseRequestMessage(string hex)
    {
        var bytes = HexFormatter.Parse(hex);
        var validation = SysExValidator.Validate(bytes);
        if (validation.HasErrors || validation.Messages.Count != 1)
        {
            throw new FormatException("A mensagem de requisição deve ser uma única mensagem SysEx completa (F0 ... F7) com dados 00–7F.");
        }

        return bytes;
    }

    public SysExAssembler CreateAssembler() => new();
}
