using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ShotTool
{
    internal static class CaptureService
    {
        /// <summary>按物理像素抓取整块虚拟屏幕。</summary>
        public static Bitmap Capture(Rectangle virtualScreen)
        {
            int width = Math.Max(1, virtualScreen.Width);
            int height = Math.Max(1, virtualScreen.Height);

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    virtualScreen.Left,
                    virtualScreen.Top,
                    0,
                    0,
                    new Size(width, height),
                    CopyPixelOperation.SourceCopy);
            }

            return bitmap;
        }

        public static byte[] EncodePng(Bitmap bitmap)
        {
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        /// <summary>先 CreateNew 抢占文件名，避免“判断存在再写”的竞态。</summary>
        public static string SavePng(string directory, byte[] png)
        {
            Directory.CreateDirectory(directory);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            for (int attempt = 0; attempt < 500; attempt++)
            {
                string fileName = attempt == 0
                    ? "ScreenShot_" + stamp + ".png"
                    : "ScreenShot_" + stamp + "_" + attempt + ".png";

                string path = Path.Combine(directory, fileName);
                try
                {
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(png, 0, png.Length);
                    }

                    return path;
                }
                catch (IOException)
                {
                    if (!File.Exists(path))
                    {
                        throw;
                    }
                }
            }

            throw new IOException("无法生成不重复的文件名");
        }
    }
}
