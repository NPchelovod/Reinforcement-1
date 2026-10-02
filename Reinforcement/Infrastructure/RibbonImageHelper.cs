using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
namespace Reinforcement
{
    internal static class RibbonImageHelper
    {
        private static readonly ConditionalWeakTable<Image, BitmapImage> Cache = new ConditionalWeakTable<Image, BitmapImage>();
        public static BitmapImage Convert(Image image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            return Cache.GetValue(image, Create);
        }
        private static BitmapImage Create(Image image)
        {
            using (var bitmap = new Bitmap(32, 32))
            using (var memory = new MemoryStream())
            {
                bitmap.SetResolution(96, 96);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.DrawImage(image, 0, 0, 32, 32);
                }
                bitmap.Save(memory, ImageFormat.Png); memory.Position = 0;
                var result = new BitmapImage();
                result.BeginInit(); result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = memory; result.EndInit(); result.Freeze();
                return result;
            }
        }
    }
}
