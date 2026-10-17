using TriAxisBackupEditor.Application.Control;
using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Storage;

namespace TriAxisBackupEditor.Tests;

public class MidiMessagesTests
{
    [Theory]
    [InlineData(1, 1, 0x00C0u)]
    [InlineData(1, 11, 0x0AC0u)]
    [InlineData(16, 128, 0x7FCFu)]
    [InlineData(3, 90, 0x59C2u)]
    public void ProgramChange_IsEncodedLikeWinmm(int channel, int program, uint expected)
    {
        Assert.Equal(expected, MidiMessages.ProgramChange(channel, program));
    }

    [Fact]
    public void ControlChange_IsEncoded()
    {
        Assert.Equal(0x7F50B0u, MidiMessages.ControlChange(1, 80, 127));
        Assert.Equal(0x0051B9u, MidiMessages.ControlChange(10, 81, 0));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(17, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 129)]
    public void ProgramChange_RejectsOutOfRange(int channel, int program)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MidiMessages.ProgramChange(channel, program));
    }

    [Fact]
    public void Decode_RecognizesMessageTypes()
    {
        Assert.Equal(new ShortMessage(MidiMessageKind.ProgramChange, 1, 10, 0), MidiMessages.Decode(0x0AC0));
        Assert.Equal(new ShortMessage(MidiMessageKind.ControlChange, 2, 7, 100), MidiMessages.Decode(0x6407B1));
        Assert.Equal(MidiMessageKind.NoteOn, MidiMessages.Decode(0x403C90).Kind);
        Assert.Equal(MidiMessageKind.NoteOff, MidiMessages.Decode(0x003C90).Kind);
        Assert.Equal(MidiMessageKind.Other, MidiMessages.Decode(0xF8).Kind);
    }
}

public class BankNavigatorTests
{
    [Fact]
    public void TenPedalBanks_MapLikeFcb1010()
    {
        var banks = new BankNavigator(10);

        Assert.Equal(13, banks.BankCount);
        Assert.Equal(1, banks.ProgramFor(1));
        Assert.Equal(10, banks.ProgramFor(10));

        banks.BankUp();
        Assert.Equal(11, banks.ProgramFor(1));

        banks.SetBank(12);
        Assert.Equal(121, banks.ProgramFor(1));
        Assert.Equal(128, banks.ProgramFor(8));
        Assert.Null(banks.ProgramFor(9));
    }

    [Fact]
    public void Banks_Wrap()
    {
        var banks = new BankNavigator(5);

        banks.BankDown();
        Assert.Equal(banks.BankCount - 1, banks.Bank);
        banks.BankUp();
        Assert.Equal(0, banks.Bank);
    }

    [Fact]
    public void ShowProgram_SelectsContainingBank()
    {
        var banks = new BankNavigator(4);

        banks.ShowProgram(10);

        Assert.Equal(2, banks.Bank);
        Assert.Equal(2, banks.PedalFor(10));
        Assert.Null(banks.PedalFor(13));
    }

    [Fact]
    public void UnsupportedBankSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BankNavigator(7));
    }
}

public class ControlBindingTests
{
    [Fact]
    public void LearnedCc_TriggersOnlyOnPress()
    {
        var map = new ControlBindingMap();
        var trigger = MidiTrigger.FromMessage(new ShortMessage(MidiMessageKind.ControlChange, 1, 64, 127))!;
        map.Bind(ControlAction.BankUp, trigger);

        Assert.Equal(ControlAction.BankUp, map.Resolve(new ShortMessage(MidiMessageKind.ControlChange, 1, 64, 127)));
        Assert.Null(map.Resolve(new ShortMessage(MidiMessageKind.ControlChange, 1, 64, 0)));
        Assert.Null(map.Resolve(new ShortMessage(MidiMessageKind.ControlChange, 2, 64, 127)));
    }

    [Fact]
    public void ReleaseMessages_CannotBeLearned()
    {
        Assert.Null(MidiTrigger.FromMessage(new ShortMessage(MidiMessageKind.ControlChange, 1, 64, 0)));
        Assert.Null(MidiTrigger.FromMessage(new ShortMessage(MidiMessageKind.NoteOff, 1, 60, 0)));
    }

    [Fact]
    public void Binding_MovesTriggerBetweenActions()
    {
        var map = new ControlBindingMap();
        var trigger = new MidiTrigger(MidiTriggerKind.ProgramChange, 1, 4);

        map.Bind(ControlAction.Pedal1, trigger);
        map.Bind(ControlAction.Pedal2, trigger);

        Assert.Null(map.Get(ControlAction.Pedal1));
        Assert.Equal(ControlAction.Pedal2, map.Resolve(new ShortMessage(MidiMessageKind.ProgramChange, 1, 4, 0)));
    }

    [Fact]
    public void ActionHelpers_Work()
    {
        Assert.Equal(10, ControlActions.PedalNumber(ControlAction.Pedal10));
        Assert.Null(ControlActions.PedalNumber(ControlAction.BankUp));
        Assert.Equal(3, ControlActions.CcButtonNumber(ControlAction.CcButton3));
        Assert.Equal("Pedal 4", ControlActions.Label(ControlAction.Pedal4));
    }
}

public class ControlSurfaceServiceTests
{
    [Fact]
    public void SendBeforeStart_Throws()
    {
        using var service = new ControlSurfaceService(new FakeMidiDeviceService(), NullTechLog.Instance);

        Assert.Throws<InvalidOperationException>(() => service.SendProgramChange(1, 1));
    }

    [Fact]
    public void StartSendStop_UsesOutputPort()
    {
        var midi = new FakeMidiDeviceService();
        var service = new ControlSurfaceService(midi, NullTechLog.Instance);

        service.Start(FakeMidiDeviceService.Out, null);
        service.SendProgramChange(1, 11);
        service.SendControlChange(1, 80, 127);
        service.Stop();

        var port = Assert.Single(midi.OpenedOutputs);
        Assert.Equal(new[] { 0x0AC0u, 0x7F50B0u }, port.ShortMessages);
        Assert.Empty(port.Chunks); // nunca envia SysEx no modo controle
        Assert.True(port.Disposed);
        Assert.False(service.IsActive);
    }

    [Fact]
    public async Task ControlInput_RaisesDecodedMessages()
    {
        var midi = new FakeMidiDeviceService();
        using var service = new ControlSurfaceService(midi, NullTechLog.Instance);
        var received = new List<ShortMessage>();
        service.InputReceived += (_, m) => received.Add(m);

        service.Start(FakeMidiDeviceService.Out, FakeMidiDeviceService.In);
        var input = await midi.InputOpened;
        input.PushShort(0x0AC0);   // PC 11
        input.PushShort(0xF8);     // clock: ignorado
        input.PushShort(0x7F40B0); // CC 64 = 127

        Assert.Equal(2, received.Count);
        Assert.Equal(MidiMessageKind.ProgramChange, received[0].Kind);
        Assert.Equal(64, received[1].Number);

        service.Stop();
        Assert.True(input.Disposed);
    }

    [Fact]
    public void FailingOutput_LeavesServiceInactive()
    {
        var midi = new FakeMidiDeviceService { FailOutputOpen = true };
        using var service = new ControlSurfaceService(midi, NullTechLog.Instance);

        Assert.ThrowsAny<Exception>(() => service.Start(FakeMidiDeviceService.Out, null));
        Assert.False(service.IsActive);
    }

    [Fact]
    public void Settings_NormalizeControlDefaults()
    {
        var settings = new AppSettings { ControlMidiChannel = 40, ControlBankSize = 7 }.Normalize(@"C:\x");

        Assert.Equal(16, settings.ControlMidiChannel);
        Assert.Equal(10, settings.ControlBankSize);
        Assert.Equal(4, settings.ControlCcButtons.Count);
        Assert.Equal(80, settings.ControlCcButtons[0].Controller);
    }
}
