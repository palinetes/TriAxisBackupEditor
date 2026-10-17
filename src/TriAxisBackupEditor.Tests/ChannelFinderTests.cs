using TriAxisBackupEditor.Application.Control;

namespace TriAxisBackupEditor.Tests;

public class ChannelFinderTests
{
    /// <summary>Simula um TriAxis no canal informado respondendo às perguntas.</summary>
    private static ChannelFinder Run(int triAxisChannel)
    {
        var finder = new ChannelFinder();
        while (finder.State is ChannelFinderState.TestingAll or ChannelFinderState.Narrowing)
        {
            finder.Answer(finder.CurrentProbe.Contains(triAxisChannel));
        }

        return finder;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(16)]
    public void FindsEveryChannel_InAtMostFiveQuestions(int channel)
    {
        var finder = Run(channel);

        Assert.Equal(ChannelFinderState.Found, finder.State);
        Assert.Equal(channel, finder.FoundChannel);
        Assert.True(finder.QuestionsAsked <= 5);
    }

    [Fact]
    public void FirstTest_SendsToAllSixteenChannels()
    {
        Assert.Equal(Enumerable.Range(1, 16), new ChannelFinder().CurrentProbe);
    }

    [Fact]
    public void NoChangeOnAllChannels_MeansConnectionProblem()
    {
        var finder = new ChannelFinder();

        finder.Answer(false);

        Assert.Equal(ChannelFinderState.NoResponse, finder.State);
        Assert.Empty(finder.CurrentProbe);
        Assert.Null(finder.FoundChannel);
    }

    [Fact]
    public void Restart_StartsOver()
    {
        var finder = Run(5);

        finder.Restart();

        Assert.Equal(ChannelFinderState.TestingAll, finder.State);
        Assert.Equal(16, finder.CurrentProbe.Count);
        Assert.Equal(0, finder.QuestionsAsked);
    }
}
