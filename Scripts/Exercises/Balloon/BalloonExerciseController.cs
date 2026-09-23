using VocalisFonoPlay.Biofeedback;
using VocalisFonoPlay.Exercises.Core;

namespace VocalisFonoPlay.Exercises.Balloon;

public sealed class BalloonExerciseController : ExerciseController
{
    private double _targetHeight;

    public double TargetHeight => _targetHeight;

    public override void Start()
    {
        _targetHeight = 0.25d;
    }

    public override void Pause()
    {
        _targetHeight = 0.25d;
    }

    public override void Restart()
    {
        _targetHeight = 0d;
    }

    public override void Finish()
    {
        _targetHeight = 1d;
    }

    public void ApplyBiofeedback(BiofeedbackResult result)
    {
        _targetHeight = Math.Clamp(result.Value, 0d, 1d);
    }
}
