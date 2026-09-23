using VocalisFonoPlay.Audio.Processing;

namespace VocalisFonoPlay.Audio.Features;

public sealed class FeatureExtractor
{
    private readonly int _sampleRate;

    public FeatureExtractor(int sampleRate = 48000)
    {
        _sampleRate = sampleRate;
    }

    public AudioFeatures Extract(float[] samples, double timestamp)
    {
        if (samples == null || samples.Length == 0)
        {
            return new AudioFeatures
            {
                Timestamp = timestamp,
                Rms = 0d,
                Energy = 0d,
                Amplitude = 0d,
                Duration = 0d,
                IsVoiceActive = false,
                F0 = null,
                Confidence = 0d,
            };
        }

        double rms = RmsCalculator.Calculate(samples);
        double energy = rms * rms;
        double amplitude = 0d;
        foreach (float sample in samples)
        {
            amplitude = Math.Max(amplitude, Math.Abs(sample));
        }
        bool voiceActive = rms > 0.02d;

        return new AudioFeatures
        {
            Timestamp = timestamp,
            Rms = rms,
            Energy = energy,
            Amplitude = amplitude,
            Duration = samples.Length / (double)_sampleRate,
            IsVoiceActive = voiceActive,
            F0 = null,
            Confidence = voiceActive ? 0.75d : 0.0d,
        };
    }
}
