using VocalisFonoPlay.Audio.Processing;
using Xunit;

namespace VocalisFonoPlay.Tests;

public class AudioProcessorTests
{
    [Fact]
    public void Process_RemovesDcOffset()
    {
        var processor = new AudioProcessor();
        var result = processor.Process(new[] { 0.4f, 0.4f, 0.4f });

        Assert.All(result, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void Process_ClampsSamplesAfterPreprocessing()
    {
        var processor = new AudioProcessor(removeDcOffset: false);
        var result = processor.Process(new[] { 2f, -2f, 0.25f });

        Assert.Equal(new[] { 1f, -1f, 0.25f }, result);
    }

    [Fact]
    public void ComputeWindowRms_ReturnsSignalRmsWithoutDurationScaling()
    {
        var processor = new AudioProcessor(removeDcOffset: false);

        var result = processor.ComputeWindowRms(new[] { 1f, 1f, 1f, 1f });

        Assert.Equal(1d, result);
    }
}
