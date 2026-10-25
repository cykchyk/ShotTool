using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class MakeIcon
{
    static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float r = Math.Max(2f, size * 0.22f);
            float w = size - 1f;
            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, r * 2, r * 2, 180, 90);
                path.AddArc(w - r * 2, 0, r * 2, r * 2, 270, 90);
                path.AddArc(w - r * 2, w - r * 2, r * 2, r * 2, 0, 90);
                path.AddArc(0, w - r * 2, r * 2, r * 2, 90, 90);
                path.CloseFigure();
                using (var bg = new SolidBrush(Color.FromArgb(255, 30, 120, 220)))
                {
                    g.FillPath(bg, path);
                }
            }

            float penW = Math.Max(1f, size / 13f);
            using (var pen = new Pen(Color.White, penW))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float m = size * 0.27f;
                float len = size * 0.17f;
                float far = size - m;
                g.DrawLine(pen, m, m, m + len, m);
                g.DrawLine(pen, m, m, m, m + len);
                g.DrawLine(pen, far, m, far - len, m);
                g.DrawLine(pen, far, m, far, m + len);
                g.DrawLine(pen, m, far, m + len, far);
                g.DrawLine(pen, m, far, m, far - len);
                g.DrawLine(pen, far, far, far - len, far);
                g.DrawLine(pen, far, far, far, far - len);
            }
        }
        return bmp;
    }

    [STAThread]
    static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string icoPath = Path.Combine(root, "app.ico");
        string pngPath = Path.Combine(root, "icon-preview.png");

        int[] sizes = new int[] { 16, 24, 32, 48, 64, 256 };
        byte[][] blobs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
        {
            using (var bmp = Render(sizes[i]))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                blobs[i] = ms.ToArray();
                if (sizes[i] == 256)
                {
                    bmp.Save(pngPath, ImageFormat.Png);
                }
            }
        }

        using (var fs = File.Create(icoPath))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int dim = sizes[i] >= 256 ? 0 : sizes[i];
                bw.Write((byte)dim);
                bw.Write((byte)dim);
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write((uint)blobs[i].Length);
                bw.Write((uint)offset);
                offset += blobs[i].Length;
            }
            for (int i = 0; i < blobs.Length; i++)
            {
                bw.Write(blobs[i]);
            }
        }

        Console.WriteLine("ico: " + icoPath + " (" + new FileInfo(icoPath).Length + " bytes)");
        Console.WriteLine("png: " + pngPath);
        return 0;
    }
}
