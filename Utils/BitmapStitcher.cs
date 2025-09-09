using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Photobooth.Utils;

public static class BitmapStitcher
{
    private static Image<Bgra32> ToImageSharp(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms);      // -> PNG dans le flux
        ms.Position = 0;
        return Image.Load<Bgra32>(ms);
    }

    /// <summary>
    /// Variante 2×2 (grille) si tu en as besoin.
    /// </summary>
    public static string MakeGrid2x2(
        IReadOnlyList<Bitmap> frames,
        int canvasWidth,
        int canvasHeight,
        int spacing,
        int margin,
        Bgra32 background,
        int jpegQuality,
        string outputPath)
    {
        if (frames == null || frames.Count == 0)
            throw new ArgumentException("Aucune image fournie.", nameof(frames));

        // On prend jusqu'à 4 images
        var imgs = new List<Image<Bgra32>>(4);
        try
        {
            int count = Math.Min(frames.Count, 4);
            for (int i = 0; i < count; i++)
                imgs.Add(ToImageSharp(frames[i]));

            // Calcul des cell sizes
            int innerW = canvasWidth - 2 * margin - spacing;
            int innerH = canvasHeight - 2 * margin - spacing;
            int cellW = innerW / 2;
            int cellH = innerH / 2;

            // Redimensionne chaque image pour rentrer dans sa cellule (Uniform)
            for (int i = 0; i < imgs.Count; i++)
            {
                var img = imgs[i];
                // scale pour tenir dans cellW x cellH tout en conservant ratio
                double scale = Math.Min(cellW / (double)img.Width, cellH / (double)img.Height);
                int w = Math.Max(1, (int)Math.Round(img.Width * scale));
                int h = Math.Max(1, (int)Math.Round(img.Height * scale));
                img.Mutate(ctx => ctx.Resize(new Size(w, h)));
            }

            using var canvas = new Image<Bgra32>(canvasWidth, canvasHeight, background);
            // positions cellules
            Point[] slots = new[]
            {
                new Point(margin, margin),
                new Point(margin + cellW + spacing, margin),
                new Point(margin, margin + cellH + spacing),
                new Point(margin + cellW + spacing, margin + cellH + spacing),
            };

            for (int i = 0; i < imgs.Count; i++)
            {
                var img = imgs[i];
                // centre l'image dans sa cellule
                int cellX = slots[i].X;
                int cellY = slots[i].Y;
                int dx = cellX + (cellW - img.Width) / 2;
                int dy = cellY + (cellH - img.Height) / 2;
                canvas.Mutate(ctx => ctx.DrawImage(img, new Point(dx, dy), 1f));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            canvas.SaveAsJpeg(outputPath, new JpegEncoder { Quality = jpegQuality });
            return outputPath;
        }
        finally
        {
            foreach (var img in imgs) img.Dispose();
        }
    }
}