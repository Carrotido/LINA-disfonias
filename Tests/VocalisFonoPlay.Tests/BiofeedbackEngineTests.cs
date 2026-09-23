using Xunit;
using VocalisFonoPlay.Biofeedback;

namespace VocalisFonoPlay.Tests;

public class BiofeedbackEngineTests
{
    [Fact]
    public void Evaluate_LowRms_ReturnsInactiveResult()
    {
        var engine = new BiofeedbackEngine();

        var result = engine.Evaluate(0.01d, 123.0d);

        Assert.False(result.Active);
        Assert.Equal(0d, result.Value);
    }

    [Fact]
    public void Evaluate_ContinuousSignalBuildsContinuityInsteadOfFollowingMagnitude()
    {
        var engine = new BiofeedbackEngine(targetDurationSeconds: 1.0d);

        var first = engine.Evaluate(0.10d, 0.0d);
        var second = engine.Evaluate(0.20d, 0.5d);

        Assert.True(first.Active);
        Assert.True(second.Active);
        Assert.InRange(second.Value, 0.49d, 0.51d);
        Assert.Equal(1.0d, second.Intensity);
    }

    [Fact]
    public void Evaluate_ShortSilenceGapKeepsContinuousBlowActive()
    {
        var engine = new BiofeedbackEngine(targetDurationSeconds: 1.0d, maximumSilenceGapSeconds: 0.15d);

        engine.Evaluate(0.10d, 0.0d);
        var result = engine.Evaluate(0.04d, 0.1d);

        Assert.True(result.Active);
        Assert.InRange(result.ActiveDuration, 0.09d, 0.11d);
    }

    [Fact]
    public void Evaluate_ThresholdCanBeAdjusted()
    {
        var engine = new BiofeedbackEngine(activationThreshold: 0.20d);

        var quiet = engine.Evaluate(0.10d, 0.0d);
        var active = engine.Evaluate(0.21d, 0.1d);

        Assert.False(quiet.Active);
        Assert.True(active.Active);
    }
}
