using TriAxisBackupEditor.Application.Control;

namespace TriAxisBackupEditor.Tests;

public class TriAxisListenerTests
{
    [Fact]
    public async Task Returns_FirstProgramChange_AndIgnoresOtherMessages()
    {
        var midi = new FakeMidiDeviceService();
        var listener = new TriAxisListener(midi);

        var task = listener.ListenForProgramChangeAsync(FakeMidiDeviceService.In, TimeSpan.FromSeconds(5), CancellationToken.None);
        var port = await midi.InputOpened;
        port.PushShort(MidiMessages.ControlChange(3, 7, 100)); // não é troca de programa
        port.PushShort(MidiMessages.ProgramChange(6, 12));
        port.PushShort(MidiMessages.ProgramChange(2, 40)); // só a primeira conta

        var message = await task;

        Assert.NotNull(message);
        Assert.Equal(6, message!.Value.Channel);
        Assert.Equal(11, message.Value.Number); // valor no cabo: programa 12 = 11
        Assert.True(port.Disposed);
    }

    [Fact]
    public async Task Returns_Null_WhenNothingArrives()
    {
        var listener = new TriAxisListener(new FakeMidiDeviceService());

        var message = await listener.ListenForProgramChangeAsync(FakeMidiDeviceService.In, TimeSpan.FromMilliseconds(80), CancellationToken.None);

        Assert.Null(message);
    }

    [Fact]
    public async Task Returns_Null_WhenCancelled()
    {
        var listener = new TriAxisListener(new FakeMidiDeviceService());
        using var cts = new CancellationTokenSource();

        var task = listener.ListenForProgramChangeAsync(FakeMidiDeviceService.In, TimeSpan.FromSeconds(30), cts.Token);
        cts.Cancel();

        Assert.Null(await task);
    }
}
