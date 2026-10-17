using TriAxisBackupEditor.Application.Backup;
using TriAxisBackupEditor.Application.Logging;

namespace TriAxisBackupEditor.Tests;

public class BackupCaptureServiceTests
{
    private readonly FakeMidiDeviceService _midi = new();
    private readonly BackupCaptureService _service;

    public BackupCaptureServiceTests()
    {
        _service = new BackupCaptureService(_midi, NullTechLog.Instance);
    }

    private static CaptureOptions Options(byte[]? request = null) => new(
        FakeMidiDeviceService.In,
        FakeMidiDeviceService.Out,
        StartTimeout: TimeSpan.FromSeconds(2),
        ReceiveTimeout: TimeSpan.FromMilliseconds(400),
        PostDumpIdle: TimeSpan.FromMilliseconds(200),
        RequestMessage: request);

    [Fact]
    public async Task FragmentedDump_IsCapturedExactly()
    {
        var dump = TestDumps.CreateTriAxisDump();
        var capture = _service.CaptureAsync(Options(), null, CancellationToken.None);
        var port = await _midi.InputOpened;

        for (var offset = 0; offset < dump.Length; offset += 300)
        {
            port.Push(dump.AsSpan(offset, Math.Min(300, dump.Length - offset)).ToArray());
            await Task.Delay(5);
        }

        var result = await capture;

        Assert.Equal(CaptureStatus.Completed, result.Status);
        Assert.True(result.IsComplete);
        Assert.Equal(dump, result.Data);
        Assert.Equal(1, result.MessageCount);
        Assert.True(port.Disposed);
        Assert.Empty(_midi.OpenedOutputs); // sem requisição configurada nada é enviado
    }

    [Fact]
    public async Task NoData_TimesOutWithoutSaving()
    {
        var result = await _service.CaptureAsync(Options() with { StartTimeout = TimeSpan.FromMilliseconds(300) }, null, CancellationToken.None);

        Assert.Equal(CaptureStatus.NoDataReceived, result.Status);
        Assert.Empty(result.Data);
        Assert.Empty(result.RawPartialData);
    }

    [Fact]
    public async Task InterruptedReception_IsIncompleteAndKeepsRawBytes()
    {
        var dump = TestDumps.CreateTriAxisDump();
        var capture = _service.CaptureAsync(Options(), null, CancellationToken.None);
        var port = await _midi.InputOpened;

        port.Push(dump.AsSpan(0, 1000).ToArray());
        var result = await capture;

        Assert.Equal(CaptureStatus.Incomplete, result.Status);
        Assert.False(result.IsComplete);
        Assert.Empty(result.Data);
        Assert.Equal(dump.AsSpan(0, 1000).ToArray(), result.RawPartialData);
    }

    [Fact]
    public async Task Cancel_DuringReception_ReturnsCancelledWithPartialData()
    {
        var dump = TestDumps.CreateTriAxisDump();
        using var cts = new CancellationTokenSource();
        var capture = _service.CaptureAsync(Options() with { ReceiveTimeout = TimeSpan.FromSeconds(30) }, null, cts.Token);
        var port = await _midi.InputOpened;

        port.Push(dump.AsSpan(0, 500).ToArray());
        await Task.Delay(50);
        cts.Cancel();
        var result = await capture;

        Assert.Equal(CaptureStatus.Cancelled, result.Status);
        Assert.Empty(result.Data);
        Assert.Equal(500, result.RawPartialData.Length);
    }

    [Fact]
    public async Task ConfiguredRequest_IsSentBeforeWaiting()
    {
        var request = new byte[] { 0xF0, 0x00, 0x00, 0x4A, 0x7F, 0xF7 };
        var dump = TestDumps.CreateTriAxisDump();
        var capture = _service.CaptureAsync(Options(request), null, CancellationToken.None);
        var port = await _midi.InputOpened;

        await Task.Delay(50);
        port.Push(dump);
        var result = await capture;

        Assert.Equal(CaptureStatus.Completed, result.Status);
        var output = Assert.Single(_midi.OpenedOutputs);
        Assert.Equal(request, output.AllBytes);
    }

    [Fact]
    public async Task ProgressReportsBytesReceived()
    {
        var dump = TestDumps.CreateTriAxisDump();
        var reports = new List<CaptureProgress>();
        var progress = new SyncProgress<CaptureProgress>(reports.Add);
        var capture = _service.CaptureAsync(Options(), progress, CancellationToken.None);
        var port = await _midi.InputOpened;

        port.Push(dump);
        await capture;

        Assert.Contains(reports, r => r.Phase == CapturePhase.WaitingForDump);
        var last = reports[^1];
        Assert.Equal(CapturePhase.Done, last.Phase);
        Assert.Equal(dump.Length, last.BytesReceived);
        Assert.Equal(100, last.Percent);
    }

    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _action;
        private readonly object _sync = new();

        public SyncProgress(Action<T> action) => _action = action;

        public void Report(T value)
        {
            lock (_sync)
            {
                _action(value);
            }
        }
    }
}
