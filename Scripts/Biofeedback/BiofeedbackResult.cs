namespace VocalisFonoPlay.Biofeedback;

public sealed class BiofeedbackResult
{
    public double Value { get; init; }
    public bool Active { get; init; }
    public double Intensity { get; init; }
    public double Confidence { get; init; }
    public double Timestamp { get; init; }
    public double ActiveDuration { get; init; }
}
