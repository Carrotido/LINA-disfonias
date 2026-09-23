namespace VocalisFonoPlay.Biofeedback;

public sealed class BiofeedbackEngine
{
    private bool _wasActive;
    private double _activeDuration;
    private double? _lastTimestamp;

    public BiofeedbackEngine(
        double activationThreshold = 0.08,
        double releaseThreshold = 0.05,
        double targetDurationSeconds = 3.0,
        double maximumSilenceGapSeconds = 0.15)
    {
        ActivationThreshold = activationThreshold;
        ReleaseThreshold = releaseThreshold;
        TargetDurationSeconds = targetDurationSeconds;
        MaximumSilenceGapSeconds = maximumSilenceGapSeconds;
    }

    public double ActivationThreshold { get; set; }
    public double ReleaseThreshold { get; set; }
    public double TargetDurationSeconds { get; set; }
    public double MaximumSilenceGapSeconds { get; set; }

    public BiofeedbackResult Evaluate(double rmsValue, double timestamp)
    {
        double elapsed = GetElapsed(timestamp);
        bool aboveActivationThreshold = rmsValue >= ActivationThreshold;
        bool active = aboveActivationThreshold
            || (_wasActive && (rmsValue >= ReleaseThreshold || elapsed <= MaximumSilenceGapSeconds));

        if (active)
        {
            bool briefReleaseGap = !aboveActivationThreshold && elapsed > MaximumSilenceGapSeconds;
            _activeDuration = _wasActive && !briefReleaseGap
                ? _activeDuration + elapsed
                : 0d;
        }
        else
        {
            _activeDuration = 0d;
        }

        _wasActive = active;
        double continuity = active && TargetDurationSeconds > 0d
            ? Clamp01(_activeDuration / TargetDurationSeconds)
            : 0d;

        return new BiofeedbackResult
        {
            Value = continuity,
            Active = active,
            Intensity = active ? 1d : 0d,
            Confidence = active ? continuity : 0d,
            Timestamp = timestamp,
            ActiveDuration = _activeDuration,
        };
    }

    public void Reset()
    {
        _wasActive = false;
        _activeDuration = 0d;
        _lastTimestamp = null;
    }

    private double GetElapsed(double timestamp)
    {
        double elapsed = _lastTimestamp.HasValue
            ? Math.Max(0d, timestamp - _lastTimestamp.Value)
            : 0d;

        _lastTimestamp = timestamp;
        return elapsed;
    }

    private static double Clamp01(double value)
    {
        if (value < 0d) return 0d;
        if (value > 1d) return 1d;
        return value;
    }
}
