namespace VocalisFonoPlay.Audio.Features;

public sealed class AudioFeatures
{
    public double Timestamp { get; init; }
    public double Rms { get; init; }
    public double Energy { get; init; }
    public double Amplitude { get; init; }
    public double Duration { get; init; }
    public bool IsVoiceActive { get; init; }
    public double? F0 { get; init; }
    public double Confidence { get; init; }
}
