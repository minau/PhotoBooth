using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace Photobooth.Utils;

public static class MatConverter
{
    public static void ToBgra(Mat src, Mat dstBgra)
    {
        if (src.Empty()) throw new ArgumentException("Mat is empty");

        // Cas courants
        if (src.Type() == MatType.CV_8UC4)
        {
            // Si c'est RGBA, on passe en BGRA ; sinon on copie
            try
            {
                Cv2.CvtColor(src, dstBgra, ColorConversionCodes.RGBA2BGRA);
            }
            catch
            {
                src.CopyTo(dstBgra);
            }

            return;
        }
        if (src.Type() == MatType.CV_8UC3)
        { 
            Cv2.CvtColor(src, dstBgra, ColorConversionCodes.BGR2BGRA);
            return;
        }

        if (src.Type() == MatType.CV_8UC2)
        {
            // YUV empaqueté (YUY2/UYVY) très courant
            try
            {
                Cv2.CvtColor(src, dstBgra, ColorConversionCodes.YUV2BGRA_YUY2);
                return;
            }
            catch
            {
                // Log here
            }

            try
            {
                Cv2.CvtColor(src, dstBgra, ColorConversionCodes.YUV2BGRA_UYVY);
                return;
            }
            catch
            {
                // Log here
            }
        }

        if (src.Type() == MatType.CV_8UC1)
        {
            Cv2.CvtColor(src, dstBgra, ColorConversionCodes.GRAY2BGRA);
            return;
        }

        // NV12/NV21 (certains backends macOS)
        try
        {
            Cv2.CvtColor(src, dstBgra, ColorConversionCodes.YUV2BGRA_NV12);
            return;
        }
        catch
        {
            // Log here
        }

        try
        {
            Cv2.CvtColor(src, dstBgra, ColorConversionCodes.YUV2BGRA_NV21);
            return;
        }
        catch
        {
            // Log here
        }

        // Dernier recours (si profondeur != 8 bits)
        if (src.Depth() != MatType.CV_8U)
        {
            using var tmp8 = new Mat();
            src.ConvertTo(tmp8, MatType.CV_8U);
            ToBgra(tmp8, dstBgra);
            return;
        }

        // Fallback: copie brute (mieux que planter)
        src.CopyTo(dstBgra);
    }
    
    /// Transforme un Mat (quelconque) en WriteableBitmap Avalonia (BGRA).
    public static WriteableBitmap ToWriteableBitmapAny(Mat src)
    {
        using var matBgra = new Mat();
        ToBgra(src, matBgra);
        Cv2.Flip(matBgra, matBgra, FlipMode.Y);
        //720 * 1280 * CV8U_C4
        var wb = new WriteableBitmap(
            new PixelSize(matBgra.Cols, matBgra.Rows),
            new Vector(96, 96),
            PixelFormat.Bgra8888);

        using (var fb = wb.Lock())
        {
            int srcStride = (int)matBgra.Step();
            int dstStride = fb.RowBytes;
            int rowBytes  = matBgra.Cols * 4;

            // Copie ligne par ligne directement Mat -> framebuffer
            for (int y = 0; y < matBgra.Rows; y++)
            {
                IntPtr srcPtr = matBgra.Data + y * srcStride;
                IntPtr dstPtr = fb.Address     + y * dstStride;
                // Copie rowBytes octets depuis srcPtr vers dstPtr
                unsafe
                {
                    Buffer.MemoryCopy(srcPtr.ToPointer(), dstPtr.ToPointer(), rowBytes, rowBytes);
                }
            }
        }

        return wb;
    }

    public static Mat CropToFourFive(Mat frame)
    {
        var targetRatio = 4.0 / 5.0;
        var currentRatio = frame.Width / (double) frame.Height;
        
        int newWidth = frame.Width;
        int newHeight = frame.Height;

        if (currentRatio > targetRatio)
        {
            // trop large → on coupe sur les côtés
            newWidth = (int)(frame.Height * targetRatio);
        }
        else
        {
            // trop haut → on coupe en haut/bas
            newHeight = (int)(frame.Width / targetRatio);
        }

        int x = (frame.Width - newWidth) / 2;
        int y = (frame.Height - newHeight) / 2;

        return new Mat(frame, new OpenCvSharp.Rect(x, y, newWidth, newHeight));
    }
    
    public static void SaveAsJpeg(this WriteableBitmap wb, string path, int quality = 90)
    {
        if (wb is null) throw new ArgumentNullException(nameof(wb));
        if (quality < 1 || quality > 100) throw new ArgumentOutOfRangeException(nameof(quality));

        int w = wb.PixelSize.Width;
        int h = wb.PixelSize.Height;

        using var img = new Image<Bgra32>(w, h);
        using (var fb = wb.Lock())
        {
            int srcStride = fb.RowBytes;

            for (int y = 0; y < h; y++)
            {
                // destination (ImageSharp 3.x)
                Span<Bgra32> dstRow = img.DangerousGetPixelRowMemory(y).Span;

                // source (Avalonia framebuffer)
                IntPtr srcPtr = fb.Address + y * srcStride;

                // copie sans allocation intermédiaire
                unsafe
                {
                    var srcBytes = new ReadOnlySpan<byte>(srcPtr.ToPointer(), w * 4);
                    var srcBgra  = MemoryMarshal.Cast<byte, Bgra32>(srcBytes);
                    srcBgra.CopyTo(dstRow);
                }
            }
        }

        img.SaveAsJpeg(path, new JpegEncoder { Quality = quality });
    }
}