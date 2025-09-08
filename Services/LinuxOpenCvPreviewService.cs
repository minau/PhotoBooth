// Services/LinuxOpenCvPreviewService.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia;
using Avalonia.Threading;
using OpenCvSharp;
using Photobooth.Services;
using Photobooth.Utils;

namespace Photomaton.Services
{
    public sealed class LinuxOpenCvPreviewService : IPreviewService
    {
        private readonly int _deviceIndex, _w, _h, _fps;
        private VideoCapture? _cap;
        private WriteableBitmap? _wb;
        private CancellationTokenSource? _cts;

        public event Action<Bitmap?>? FrameReady;
        public bool IsRunning => _cts is { IsCancellationRequested: false };

        public LinuxOpenCvPreviewService(int deviceIndex = 0, int w = 1280, int h = 720, int fps = 30)
        { _deviceIndex = deviceIndex; _w = w; _h = h; _fps = fps; }

        public bool Start()
        {
            _cap = new VideoCapture(_deviceIndex);
            if (!_cap.IsOpened()) return false;

            _cap.Set(VideoCaptureProperties.FrameWidth, _w);
            _cap.Set(VideoCaptureProperties.FrameHeight, _h);
            _cap.Set(VideoCaptureProperties.Fps, _fps);
            _cap.Set(VideoCaptureProperties.FourCC, FourCC.MJPG); // réduit la charge CPU

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => Loop(_cts.Token));
            return true;
        }

        private void Loop(CancellationToken ct)
        {
            using var matBgr  = new Mat();

            while (!ct.IsCancellationRequested)
            {
                if (!(_cap?.Read(matBgr) ?? false) || matBgr.Empty()) continue;

                if (matBgr.Type() != MatType.CV_8UC3)
                {
                    throw new ArgumentException($"Mat must be CV_8UC3 (BGR), got {matBgr.Type()}");
                }
                
                var bitMap = MatConverter.ToWriteableBitmapAny(matBgr);
                
                _wb = bitMap;
                Dispatcher.UIThread.Post(() => FrameReady?.Invoke(_wb));
            }
        }

        public void Stop() => _cts?.Cancel();
        public void Dispose() => Stop();
    }
}
