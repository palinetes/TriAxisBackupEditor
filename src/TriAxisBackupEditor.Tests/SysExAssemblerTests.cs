using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Tests;

public class SysExAssemblerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(128)]
    [InlineData(1000)]
    [InlineData(2745)]
    [InlineData(4096)]
    public void FragmentedDump_IsReassembledByteForByte(int fragmentSize)
    {
        var dump = TestDumps.CreateTriAxisDump();
        var assembler = new SysExAssembler();
        var completed = new List<byte[]>();

        for (var offset = 0; offset < dump.Length; offset += fragmentSize)
        {
            var count = Math.Min(fragmentSize, dump.Length - offset);
            completed.AddRange(assembler.Feed(dump.AsSpan(offset, count)));
        }

        var message = Assert.Single(completed);
        Assert.Equal(dump, message);
        Assert.False(assembler.IsInMessage);
    }

    [Fact]
    public void IrregularFragments_AreReassembled()
    {
        var dump = TestDumps.CreateTriAxisDump();
        var assembler = new SysExAssembler();
        var random = new Random(1234);
        var completed = new List<byte[]>();

        var offset = 0;
        while (offset < dump.Length)
        {
            var count = Math.Min(random.Next(1, 300), dump.Length - offset);
            completed.AddRange(assembler.Feed(dump.AsSpan(offset, count)));
            offset += count;
        }

        Assert.Equal(dump, Assert.Single(completed));
    }

    [Fact]
    public void MessageIsNotCompleteUntilF7()
    {
        var dump = TestDumps.CreateTriAxisDump();
        var assembler = new SysExAssembler();

        var first = assembler.Feed(dump.AsSpan(0, dump.Length - 1));

        Assert.Empty(first);
        Assert.True(assembler.IsInMessage);
        Assert.Equal(dump.Length - 1, assembler.CurrentLength);
        Assert.Equal(dump.AsSpan(0, dump.Length - 1).ToArray(), assembler.GetPartialMessage());

        var second = assembler.Feed(new byte[] { SysExConstants.End });
        Assert.Equal(dump, Assert.Single(second));
    }

    [Fact]
    public void RealTimeBytesInterleaved_AreDiscarded()
    {
        var assembler = new SysExAssembler();

        var result = assembler.Feed(new byte[] { 0xF8, 0xF0, 0x00, 0xFE, 0x4A, 0xF8, 0x01, 0xF7, 0xFE });

        Assert.Equal(new byte[] { 0xF0, 0x00, 0x4A, 0x01, 0xF7 }, Assert.Single(result));
        Assert.Equal(4, assembler.RealTimeBytesDiscarded);
    }

    [Fact]
    public void StatusByteInsideMessage_AbortsIt()
    {
        var assembler = new SysExAssembler();

        var result = assembler.Feed(new byte[] { 0xF0, 0x01, 0x02, 0x90, 0x40, 0x7F });

        Assert.Empty(result);
        Assert.Equal(1, assembler.AbortedMessages);
        Assert.Equal(new byte[] { 0xF0, 0x01, 0x02 }, assembler.LastAbortedMessage);
        Assert.False(assembler.IsInMessage);
    }

    [Fact]
    public void BytesOutsideSysEx_AreIgnored()
    {
        var assembler = new SysExAssembler();

        var result = assembler.Feed(new byte[] { 0x01, 0x02, 0xF0, 0x05, 0xF7, 0x03 });

        Assert.Equal(new byte[] { 0xF0, 0x05, 0xF7 }, Assert.Single(result));
        Assert.Equal(3, assembler.StrayBytesDiscarded);
    }

    [Fact]
    public void SeveralMessagesInOneChunk_AreAllReturned()
    {
        var assembler = new SysExAssembler();

        var result = assembler.Feed(new byte[] { 0xF0, 0x01, 0xF7, 0xF0, 0x02, 0xF7 });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void OversizedMessage_IsAborted()
    {
        var assembler = new SysExAssembler(maxMessageLength: 10);
        var data = new byte[20];
        data[0] = 0xF0;

        assembler.Feed(data);

        Assert.Equal(1, assembler.AbortedMessages);
        Assert.False(assembler.IsInMessage);
    }
}
