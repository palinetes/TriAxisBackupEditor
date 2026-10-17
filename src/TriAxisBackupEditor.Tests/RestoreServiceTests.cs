using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Application.Restore;
using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Tests;

public class RestoreServiceTests
{
    private readonly FakeMidiDeviceService _midi = new();
    private readonly RestoreService _service;
    private readonly TransmitOptions _options = new(128, 5);

    public RestoreServiceTests()
    {
        _service = new RestoreService(_midi, new TriAxisSysExService(), NullTechLog.Instance);
    }

    private static RestoreConfirmation FullConfirmation(bool warnings = false) => new()
    {
        OverwriteAcknowledged = true,
        TypedPhrase = RestoreConfirmation.RequiredPhrase,
        WarningsAcknowledged = warnings
    };

    private string WriteFile(byte[] data)
    {
        var path = Path.Combine(TestDumps.TempFolder(), "backup.syx");
        File.WriteAllBytes(path, data);
        return path;
    }

    [Fact]
    public async Task Restore_WithoutConfirmation_IsBlockedAndSendsNothing()
    {
        var plan = _service.Prepare(WriteFile(TestDumps.CreateTriAxisDump()));

        await Assert.ThrowsAsync<RestoreBlockedException>(() =>
            _service.RestoreAsync(plan, null, FakeMidiDeviceService.Out, _options, null, CancellationToken.None));

        Assert.Empty(_midi.OpenedOutputs);
    }

    [Fact]
    public async Task Restore_WithOnlyFirstStep_IsBlocked()
    {
        var plan = _service.Prepare(WriteFile(TestDumps.CreateTriAxisDump()));
        var confirmation = new RestoreConfirmation { OverwriteAcknowledged = true };

        await Assert.ThrowsAsync<RestoreBlockedException>(() =>
            _service.RestoreAsync(plan, confirmation, FakeMidiDeviceService.Out, _options, null, CancellationToken.None));

        Assert.Empty(_midi.OpenedOutputs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("restaurar")]
    [InlineData("SIM")]
    public async Task Restore_WithWrongPhrase_IsBlocked(string phrase)
    {
        var plan = _service.Prepare(WriteFile(TestDumps.CreateTriAxisDump()));
        var confirmation = new RestoreConfirmation { OverwriteAcknowledged = true, TypedPhrase = phrase };

        await Assert.ThrowsAsync<RestoreBlockedException>(() =>
            _service.RestoreAsync(plan, confirmation, FakeMidiDeviceService.Out, _options, null, CancellationToken.None));

        Assert.Empty(_midi.OpenedOutputs);
    }

    [Fact]
    public async Task Restore_WithSecondStepOnly_IsBlocked()
    {
        var plan = _service.Prepare(WriteFile(TestDumps.CreateTriAxisDump()));
        var confirmation = new RestoreConfirmation { OverwriteAcknowledged = false, TypedPhrase = RestoreConfirmation.RequiredPhrase };

        await Assert.ThrowsAsync<RestoreBlockedException>(() =>
            _service.RestoreAsync(plan, confirmation, FakeMidiDeviceService.Out, _options, null, CancellationToken.None));
    }

    [Fact]
    public async Task Restore_FileWithWarnings_RequiresWarningAcknowledgement()
    {
        var plan = _service.Prepare(WriteFile(new byte[] { 0xF0, 0x43, 0x10, 0x01, 0xF7 }));
        Assert.True(plan.Validation.HasWarnings);

        await Assert.ThrowsAsync<RestoreBlockedException>(() =>
            _service.RestoreAsync(plan, FullConfirmation(warnings: false), FakeMidiDeviceService.Out, _options, null, CancellationToken.None));

        var result = await _service.RestoreAsync(plan, FullConfirmation(warnings: true), FakeMidiDeviceService.Out, _options, null, CancellationToken.None);
        Assert.Equal(TransmitStatus.Completed, result.Status);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x00, 0x01, 0xF7 })]
    [InlineData(new byte[] { 0xF0, 0x00, 0x00, 0x4A })]
    public async Task Restore_InvalidFile_IsBlockedEvenWithConfirmation(byte[] data)
    {
        var plan = _service.Prepare(WriteFile(data));
        Assert.False(plan.CanRestore);

        await Assert.ThrowsAsync<RestoreBlockedException>(() =>
            _service.RestoreAsync(plan, FullConfirmation(true), FakeMidiDeviceService.Out, _options, null, CancellationToken.None));

        Assert.Empty(_midi.OpenedOutputs);
    }

    [Fact]
    public async Task Restore_FileChangedAfterValidation_IsBlocked()
    {
        var path = WriteFile(TestDumps.CreateTriAxisDump());
        var plan = _service.Prepare(path);
        File.WriteAllBytes(path, TestDumps.CreateTriAxisDump(seed: 3));

        await Assert.ThrowsAsync<RestoreBlockedException>(() =>
            _service.RestoreAsync(plan, FullConfirmation(), FakeMidiDeviceService.Out, _options, null, CancellationToken.None));

        Assert.Empty(_midi.OpenedOutputs);
    }

    [Fact]
    public async Task Restore_Confirmed_SendsExactBytesInChunks()
    {
        var dump = TestDumps.CreateTriAxisDump();
        var plan = _service.Prepare(WriteFile(dump));

        var result = await _service.RestoreAsync(plan, FullConfirmation(), FakeMidiDeviceService.Out, _options, null, CancellationToken.None);

        Assert.Equal(TransmitStatus.Completed, result.Status);
        Assert.Equal(dump.Length, result.BytesSent);
        var port = Assert.Single(_midi.OpenedOutputs);
        Assert.Equal(dump, port.AllBytes);
        Assert.All(port.Chunks, c => Assert.True(c.Length <= 128));
        Assert.Equal((dump.Length + 127) / 128, port.Chunks.Count);
        Assert.True(port.Disposed);
    }

    [Fact]
    public async Task Restore_CancelledBeforeStart_SendsNothing()
    {
        var plan = _service.Prepare(WriteFile(TestDumps.CreateTriAxisDump()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.RestoreAsync(plan, FullConfirmation(), FakeMidiDeviceService.Out, _options, null, cts.Token);

        Assert.Equal(TransmitStatus.CancelledBeforeStart, result.Status);
        Assert.Equal(0, result.BytesSent);
        Assert.Empty(_midi.OpenedOutputs);
    }

    [Fact]
    public void Confirmation_IsCompleteOnlyWithBothSteps()
    {
        var validation = SysExValidator.Validate(TestDumps.CreateTriAxisDump());

        Assert.False(new RestoreConfirmation().IsCompleteFor(validation));
        Assert.False(new RestoreConfirmation { OverwriteAcknowledged = true }.IsCompleteFor(validation));
        Assert.False(new RestoreConfirmation { TypedPhrase = "RESTAURAR" }.IsCompleteFor(validation));
        Assert.True(new RestoreConfirmation { OverwriteAcknowledged = true, TypedPhrase = " RESTAURAR " }.IsCompleteFor(validation));
    }
}

public class SysExTransmitterTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(128)]
    [InlineData(4096)]
    public async Task Chunks_ConcatenateToOriginal(int chunkSize)
    {
        var dump = TestDumps.CreateTriAxisDump();
        var port = new FakeOutputPort(FakeMidiDeviceService.Out);
        var validation = SysExValidator.Validate(dump);

        var status = await SysExTransmitter.SendAsync(port, dump, validation.Messages, new TransmitOptions(chunkSize, 5), null, NullTechLog.Instance, CancellationToken.None);

        Assert.Equal(TransmitStatus.Completed, status);
        Assert.Equal(dump, port.AllBytes);
        Assert.Equal(SysExConstants.Start, port.Chunks[0][0]);
        Assert.Equal(SysExConstants.End, port.Chunks[^1][^1]);
    }

    [Fact]
    public async Task ChunksNeverSpanTwoMessages()
    {
        var data = new byte[] { 0xF0, 0x01, 0x02, 0x03, 0xF7, 0xF0, 0x04, 0x05, 0xF7 };
        var port = new FakeOutputPort(FakeMidiDeviceService.Out);
        var validation = SysExValidator.Validate(data);

        await SysExTransmitter.SendAsync(port, data, validation.Messages, new TransmitOptions(4, 5), null, NullTechLog.Instance, CancellationToken.None);

        Assert.Equal(new[] { 4, 1, 4 }, port.Chunks.Select(c => c.Length));
    }
}
