using Godot;

namespace VocalisFonoPlay.Audio.Capture;

public enum CaptureState
{
    Idle,
    Ready,
    Recording,
    Error,
    Unavailable,
}

public class AudioCapture
{
    private readonly AudioEffectCapture _captureEffect;
    private CaptureState _state;

    public AudioCapture(AudioEffectCapture captureEffect)
    {
        _captureEffect = captureEffect;
        _state = CaptureState.Idle;
    }

    public CaptureState State => _state;
    public bool IsAvailable => _captureEffect is not null;

    public void Initialize()
    {
        _state = IsAvailable ? CaptureState.Ready : CaptureState.Unavailable;
    }

    public bool StartCapture()
    {
        if (!IsAvailable)
        {
            _state = CaptureState.Unavailable;
            return false;
        }

        _state = CaptureState.Recording;
        return true;
    }

    public bool TryGetBuffer(int frames, out Vector2[] buffer)
    {
        buffer = Array.Empty<Vector2>();

        if (_state != CaptureState.Recording || frames <= 0 || !_captureEffect.CanGetBuffer(frames))
        {
            return false;
        }

        buffer = _captureEffect.GetBuffer(frames);
        return buffer.Length == frames;
    }

    public void StopCapture()
    {
        if (_state == CaptureState.Recording)
        {
            _state = CaptureState.Ready;
        }
    }

    public void ReportError(string message)
    {
        _state = CaptureState.Error;
        GD.PushWarning($"AudioCapture error: {message}");
    }
}
