// Services/LinuxOpenCvPreviewService.cs
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
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
        private WriteableBitmap? _frontBuffer;
        private CancellationTokenSource? _cts;
        private readonly object _latestFrameLock = new();
        private Mat? _latestFrame;
        private int _isFrameReady;
        private int _uiPublishPending;

        public event Action<Bitmap?>? FrameReady;
        public bool IsRunning => _cts is { IsCancellationRequested: false };

        public LinuxOpenCvPreviewService(int deviceIndex = 0, int w = 1280, int h = 720, int fps = 30)
        { _deviceIndex = deviceIndex; _w = w; _h = h; _fps = fps; }

        public bool Start()
        {
            if (IsRunning)
            {
                Log("Start ignored: already running.");
                return true;
            }

            _cap = new VideoCapture(_deviceIndex);
            if (!_cap.IsOpened())
            {
                Log($"Failed to open camera device index {_deviceIndex}.");
                return false;
            }

            _cap.Set(VideoCaptureProperties.FrameWidth, _w);
            _cap.Set(VideoCaptureProperties.FrameHeight, _h);
            _cap.Set(VideoCaptureProperties.Fps, _fps);
            _cap.Set(VideoCaptureProperties.FourCC, FourCC.MJPG); // réduit la charge CPU
            _cap.Set(VideoCaptureProperties.Buffersize, 1); // limite la latence et la pression CPU

            Log($"Camera started (device={_deviceIndex}, {_w}x{_h}@{_fps}, fourcc=MJPG).");

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => CaptureLoop(_cts.Token), _cts.Token);
            _ = Task.Run(() => RenderLoop(_cts.Token), _cts.Token);
            return true;
        }

        private void CaptureLoop(CancellationToken ct)
        {
            using var matBgr = new Mat();
            var emptyReads = 0;

            while (!ct.IsCancellationRequested)
            {
                if (!(_cap?.Read(matBgr) ?? false) || matBgr.Empty())
                {
                    emptyReads++;
                    if (emptyReads % 120 == 0)
                    {
                        Log($"Capture returned empty frame {emptyReads} times.");
                    }
                    Thread.Sleep(5);
                    continue;
                }

                emptyReads = 0;

                lock (_latestFrameLock)
                {
                    _latestFrame?.Dispose();
                    _latestFrame = matBgr.Clone();
                    Volatile.Write(ref _isFrameReady, 1);
                }
            }

            Log("Capture loop stopped.");
        }

        private void RenderLoop(CancellationToken ct)
        {
            using var frame = new Mat();
            var sw = Stopwatch.StartNew();
            var processedFrames = 0;

            while (!ct.IsCancellationRequested)
            {
                if (Volatile.Read(ref _isFrameReady) == 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                lock (_latestFrameLock)
                {
                    if (_latestFrame is null || _latestFrame.Empty())
                    {
                        Volatile.Write(ref _isFrameReady, 0);
                        continue;
                    }

                    _latestFrame.CopyTo(frame);
                    Volatile.Write(ref _isFrameReady, 0);
                }

                if (frame.Type() != MatType.CV_8UC3)
                {
                    Log($"Unexpected frame type: {frame.Type()} (expected CV_8UC3). Frame skipped.");
                    continue;
                }

                using var cropped = MatConverter.CropToFourFive(frame);
                using var bgra = new Mat();
                MatConverter.ToBgra(cropped, bgra);
                Cv2.Flip(bgra, bgra, FlipMode.Y);

                if (Interlocked.CompareExchange(ref _uiPublishPending, 1, 0) != 0)
                {
                    continue;
                }

                var srcStride = (int)bgra.Step();
                var totalBytes = srcStride * bgra.Rows;
                var pooledBuffer = ArrayPool<byte>.Shared.Rent(totalBytes);

                try
                {
                    Marshal.Copy(bgra.Data, pooledBuffer, 0, totalBytes);
                }
                catch (Exception ex)
                {
                    ArrayPool<byte>.Shared.Return(pooledBuffer);
                    Interlocked.Exchange(ref _uiPublishPending, 0);
                    Log($"Failed to copy BGRA frame: {ex.Message}");
                    continue;
                }

                var width = bgra.Cols;
                var height = bgra.Rows;
                var stride = srcStride;

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        if (_frontBuffer is null || _frontBuffer.PixelSize.Width != width || _frontBuffer.PixelSize.Height != height)
                        {
                            _frontBuffer = MatConverter.CreateBgraBitmap(width, height);
                            Log($"Allocated preview bitmap {_frontBuffer.PixelSize.Width}x{_frontBuffer.PixelSize.Height}.");
                        }

                        MatConverter.CopyBgraBytesToBitmap(pooledBuffer, stride, width, height, _frontBuffer);
                        FrameReady?.Invoke(_frontBuffer);
                    }
                    catch (Exception ex)
                    {
                        Log($"UI frame publish failed: {ex.Message}");
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(pooledBuffer);
                        Interlocked.Exchange(ref _uiPublishPending, 0);
                    }
                });

                processedFrames++;
                if (sw.ElapsedMilliseconds >= 5000)
                {
                    var fps = processedFrames / (sw.ElapsedMilliseconds / 1000.0);
                    Log($"Preview pipeline FPS: {fps:F1}");
                    sw.Restart();
                    processedFrames = 0;
                }
            }

            Log("Render loop stopped.");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cap?.Release();
            _cap?.Dispose();
            _cap = null;

            lock (_latestFrameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
                Volatile.Write(ref _isFrameReady, 0);
            }

            _frontBuffer = null;
            Interlocked.Exchange(ref _uiPublishPending, 0);

            Log("Preview service stopped.");
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }

        private static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.UtcNow:O}] [LinuxOpenCvPreviewService] {message}");
        }
    }
}
