namespace VocalisFonoPlay.Audio.Processing;

public static class RmsCalculator
{
    public static double Calculate(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return 0d;
        }

        double sumOfSquares = 0d;
        foreach (float sample in samples)
        {
            double scaled = sample;
            sumOfSquares += scaled * scaled;
        }

        double meanSquare = sumOfSquares / samples.Length;
        return Math.Sqrt(meanSquare);
    }

    public static double Calculate(short[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return 0d;
        }

        double sumOfSquares = 0d;
        foreach (short sample in samples)
        {
            double normalized = sample / 32768d;
            sumOfSquares += normalized * normalized;
        }

        double meanSquare = sumOfSquares / samples.Length;
        return Math.Sqrt(meanSquare);
    }
}
