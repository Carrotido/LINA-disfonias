using Xunit;
using VocalisFonoPlay.Audio.Processing;

namespace VocalisFonoPlay.Tests;

public class RmsCalculatorTests
{
    [Fact]
    public void Calculate_EmptyArray_ReturnsZero()
    {
        var result = RmsCalculator.Calculate(Array.Empty<float>());

        Assert.Equal(0d, result);
    }

    [Fact]
    public void Calculate_SineSignal_ReturnsExpectedApproximation()
    {
        var samples = new float[1000];
        for (int i = 0; i < samples.Length; i++)
        {
            double angle = (2 * Math.PI * i) / samples.Length;
            samples[i] = (float)Math.Sin(angle);
        }

        var result = RmsCalculator.Calculate(samples);

        Assert.InRange(result, 0.68, 0.72);
    }
}
