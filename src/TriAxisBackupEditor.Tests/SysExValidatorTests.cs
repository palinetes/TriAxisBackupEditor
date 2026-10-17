using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Tests;

public class SysExValidatorTests
{
    [Fact]
    public void ValidTriAxisDump_IsTransmittableAndMatchesLayout()
    {
        var result = SysExValidator.Validate(TestDumps.CreateTriAxisDump());

        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.True(result.IsTransmittable);
        Assert.True(result.MatchesTriAxisLayout);
        Assert.Single(result.Messages);
        Assert.Equal(new SysExMessageSpan(0, TriAxisDumpLayout.TotalLength), result.Messages[0]);
    }

    [Fact]
    public void EmptyFile_IsBlocked()
    {
        var result = SysExValidator.Validate(Array.Empty<byte>());

        Assert.False(result.IsTransmittable);
        Assert.Contains(result.Issues, i => i.Code == "VAZIO" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void MissingF0_IsBlocked()
    {
        var result = SysExValidator.Validate(new byte[] { 0x00, 0x00, 0x4A, 0x02, 0xF7 });

        Assert.False(result.IsTransmittable);
        Assert.Contains(result.Issues, i => i.Code == "SEM_F0");
    }

    [Fact]
    public void MissingF7_IsBlocked()
    {
        var dump = TestDumps.CreateTriAxisDump();
        var truncated = dump.AsSpan(0, dump.Length - 1).ToArray();

        var result = SysExValidator.Validate(truncated);

        Assert.False(result.IsTransmittable);
        Assert.Contains(result.Issues, i => i.Code == "SEM_F7");
    }

    [Fact]
    public void StatusByteInsideMessage_IsBlocked()
    {
        var dump = TestDumps.CreateTriAxisDump();
        dump[100] = 0x90;

        var result = SysExValidator.Validate(dump);

        Assert.False(result.IsTransmittable);
        Assert.Contains(result.Issues, i => i.Code == "STATUS_INVALIDO" && i.Offset == 100);
    }

    [Fact]
    public void RealTimeByteInsideFile_IsBlocked()
    {
        var dump = TestDumps.CreateTriAxisDump();
        dump[50] = 0xF8;

        Assert.False(SysExValidator.Validate(dump).IsTransmittable);
    }

    [Fact]
    public void TruncatedMessageFollowedByAnother_IsBlocked()
    {
        var data = new byte[] { 0xF0, 0x01, 0x02, 0xF0, 0x03, 0xF7 };

        var result = SysExValidator.Validate(data);

        Assert.False(result.IsTransmittable);
        Assert.Contains(result.Issues, i => i.Code == "F0_SEM_F7");
    }

    [Fact]
    public void UnknownHeaderAndSize_AreWarningsNotErrors()
    {
        var data = new byte[] { 0xF0, 0x43, 0x10, 0x01, 0xF7 };

        var result = SysExValidator.Validate(data);

        Assert.True(result.IsTransmittable);
        Assert.True(result.HasWarnings);
        Assert.False(result.MatchesTriAxisLayout);
        Assert.Contains(result.Issues, i => i.Code == "CABECALHO");
        Assert.Contains(result.Issues, i => i.Code == "TAMANHO");
    }

    [Fact]
    public void MultipleMessages_AreDetected()
    {
        var data = new byte[] { 0xF0, 0x00, 0x00, 0x4A, 0x02, 0x01, 0xF7, 0xF0, 0x00, 0x00, 0x4A, 0x02, 0x02, 0xF7 };

        var result = SysExValidator.Validate(data);

        Assert.Equal(2, result.Messages.Count);
        Assert.Contains(result.Issues, i => i.Code == "MULTIPLAS_MSG");
    }

    [Fact]
    public void GarbageBetweenMessages_IsBlocked()
    {
        var data = new byte[] { 0xF0, 0x01, 0xF7, 0x05, 0xF0, 0x02, 0xF7 };

        Assert.False(SysExValidator.Validate(data).IsTransmittable);
    }
}
