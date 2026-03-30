using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Photobooth.Utils
{
    public static class TemplateRenderer
    {
        public static Uri Simple =>
            new(ConfigLoader.Config.Templates.Simple.Path);

        public static Uri Grid =>
            new(ConfigLoader.Config.Templates.Grid.Path);
        
        public static readonly IReadOnlyList<PhotoSlot> PhotoStripSlots = ConfigLoader.Config.Templates.Grid.Slots.Select(s => new PhotoSlot
            {
                X = s.X,
                Y = s.Y,
                Width = s.Width,
                Height = s.Height
            })
            .ToList();
        
        public static readonly IReadOnlyList<PhotoSlot> PhotoClassicSlots = ConfigLoader.Config.Templates.Simple.Slots.Select(s => new PhotoSlot
            {
                X = s.X,
                Y = s.Y,
                Width = s.Width,
                Height = s.Height
            })
            .ToList();
        
        public sealed class PhotoSlot
        {
            public int X { get; init; }
            public int Y { get; init; }
            public int Width { get; init; }
            public int Height { get; init; }
        }

        public static string BuildFromTemplate(
            Uri templatePath,
            IReadOnlyList<Bitmap> photos,
            IReadOnlyList<PhotoSlot> slots,
            string outputFileName,
            int jpegQuality = 92)
        {
            if (photos.Count == 0)
                throw new ArgumentException("La liste de photos est vide.", nameof(photos));

            if (slots.Count < photos.Count)
                throw new ArgumentException("Il n'y a pas assez de slots pour toutes les photos.", nameof(slots));

            using var stream = AssetLoader.Open(templatePath);
            using var template = Image.Load<Rgba32>(stream);

            for (int i = 0; i < photos.Count; i++)
            {
                using var photo = AvaloniaBitmapToImageSharp(photos[i]);
                using var prepared = CropAndResizeToFill(photo, slots[i].Width, slots[i].Height);

                template.Mutate(ctx =>
                {
                    ctx.DrawImage(prepared, new Point(slots[i].X, slots[i].Y), 1f);
                });
            }

            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string outputDir = Path.Combine(pictures, "Photobooth");
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, outputFileName);
            
            template.SaveAsJpeg(outputPath, new JpegEncoder { Quality = jpegQuality });

            return outputPath;
        }

        private static Image<Rgba32> AvaloniaBitmapToImageSharp(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms);
            ms.Position = 0;
            return Image.Load<Rgba32>(ms);
        }

        private static Image<Rgba32> CropAndResizeToFill(Image<Rgba32> source, int targetWidth, int targetHeight)
        {
            double targetRatio = targetWidth / (double)targetHeight;
            double sourceRatio = source.Width / (double)source.Height;

            int cropWidth;
            int cropHeight;

            if (sourceRatio > targetRatio)
            {
                cropHeight = source.Height;
                cropWidth = (int)Math.Round(cropHeight * targetRatio);
            }
            else
            {
                cropWidth = source.Width;
                cropHeight = (int)Math.Round(cropWidth / targetRatio);
            }

            int cropX = (source.Width - cropWidth) / 2;
            int cropY = (source.Height - cropHeight) / 2;

            return source.Clone(ctx => ctx
                .Crop(new Rectangle(cropX, cropY, cropWidth, cropHeight))
                .Resize(targetWidth, targetHeight));
        }
    }
}