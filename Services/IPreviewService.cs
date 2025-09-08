using System;
using Avalonia.Media.Imaging;

namespace Photobooth.Services;

public interface IPreviewService : IDisposable
{
    event Action<Bitmap?>? FrameReady;
    bool Start();
    void Stop();
    bool IsRunning { get; }
}