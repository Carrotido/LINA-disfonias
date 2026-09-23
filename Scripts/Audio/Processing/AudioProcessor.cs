namespace VocalisFonoPlay.Audio.Processing;

public sealed class AudioProcessor
{
    private readonly bool _removeDcOffset;

    public AudioProcessor(bool removeDcOffset = true)
    {
        _removeDcOffset = removeDcOffset;
    }

    public float[] Process(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return Array.Empty<float>();
        }

        var processed = new float[samples.Length];
        double mean = 0d;
        if (_removeDcOffset)
        {
            foreach (float sample in samples)
            {
                mean += sample;
            }

            mean /= samples.Length;
        }

        for (int i = 0; i < samples.Length; i++)
        {
            processed[i] = Math.Clamp((float)(samples[i] - mean), -1f, 1f);
        }

        return processed;
    }

    public double ComputeWindowRms(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return 0d;
        }

        return RmsCalculator.Calculate(samples);
    }
}
