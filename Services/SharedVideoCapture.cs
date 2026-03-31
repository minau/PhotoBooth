using System;
using OpenCvSharp;

namespace Photomaton.Services;

/// <summary>
/// Holds a single VideoCapture instance for the whole app lifetime.
/// This avoids expensive camera reopen cycles on Raspberry Pi.
/// </summary>
public sealed class SharedVideoCapture : IDisposable
{
    private readonly object _sync = new();
    private readonly int _deviceIndex;
    private readonly int _w;
    private readonly int _h;
    private readonly int _fps;

    private VideoCapture? _capture;

    public SharedVideoCapture(int deviceIndex = 0, int w = 1280, int h = 720, int fps = 30)
    {
        _deviceIndex = deviceIndex;
        _w = w;
        _h = h;
        _fps = fps;
    }

    public VideoCapture GetOrCreate()
    {
        lock (_sync)
        {
            if (_capture?.IsOpened() == true)
            {
                return _capture;
            }

            _capture?.Dispose();
            _capture = new VideoCapture(_deviceIndex);
            if (!_capture.IsOpened())
            {
                throw new InvalidOperationException($"Failed to open camera device index {_deviceIndex}.");
            }

            _capture.Set(VideoCaptureProperties.FrameWidth, _w);
            _capture.Set(VideoCaptureProperties.FrameHeight, _h);
            _capture.Set(VideoCaptureProperties.Fps, _fps);
            _capture.Set(VideoCaptureProperties.FourCC, FourCC.MJPG);
            _capture.Set(VideoCaptureProperties.BufferSize, 1);

            return _capture;
        }
    }

    public void WarmUp()
    {
        var capture = GetOrCreate();
        using var warmupFrame = new Mat();
        capture.Read(warmupFrame);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
        }
    }
}
