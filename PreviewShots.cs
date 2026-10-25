using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace ShotTool
{
    /// <summary>开发用：生成 README 里那两张预览图，不参与正常使用。</summary>
    internal static class PreviewShots
    {
        public static void Run()
        {
            string docs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs");
            Directory.CreateDirectory(docs);

            ShotWindow(Path.Combine(docs, "preview-window.png"));
            ShotOverlay(Path.Combine(docs, "preview-overlay.png"));
        }

        private static void ShotWindow(string path)
        {
            using (var form = new MainForm(AppConfig.Load(), true))
            {
                form.Show();
                Application.DoEvents();

                Rectangle client = form.RectangleToScreen(form.ClientRectangle);
                using (Bitmap bitmap = CaptureService.Capture(client))
                {
                    bitmap.Save(path, ImageFormat.Png);
                }

                form.Close();
            }
        }

        private static void ShotOverlay(string path)
        {
            Rectangle screen = SystemInformation.VirtualScreen;
            var bounds = new Rectangle(Point.Empty, screen.Size);

            using (Bitmap mock = BuildMockDesktop(screen.Size))
            using (var overlay = new OverlayForm(mock, bounds))
            {
                overlay.CreateControl();
                overlay.SetDemoSelection(new Rectangle(126, 92, 806, 522));

                using (var shot = new Bitmap(screen.Width, screen.Height))
                {
                    overlay.DrawToBitmap(shot, new Rectangle(0, 0, screen.Width, screen.Height));
                    shot.Save(path, ImageFormat.Png);
                }
            }
        }

        private static Bitmap BuildMockDesktop(Size size)
        {
            var bitmap = new Bitmap(size.Width, size.Height);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var background = new LinearGradientBrush(
                    new Rectangle(0, 0, size.Width, size.Height),
                    Color.FromArgb(34, 42, 58),
                    Color.FromArgb(16, 20, 28),
                    45f))
                {
                    g.FillRectangle(background, 0, 0, size.Width, size.Height);
                }

                DrawMockWindow(g, new Rectangle(140, 110, 760, 470), "报告.docx", new[]
                {
                    "第三季度总结", "• 收入环比 +12%", "• 新客户 38 家", "• 续费率 91%"
                });

                DrawMockWindow(g, new Rectangle(960, 210, 660, 430), "素材库", new[]
                {
                    "banner_01.png", "banner_02.png", "icon_set.svg"
                });

                DrawMockWindow(g, new Rectangle(320, 660, 880, 300), "终端", new[]
                {
                    "> dotnet build -c Release", "已成功生成。", "0 个警告  0 个错误"
                });
            }

            return bitmap;
        }

        private static void DrawMockWindow(Graphics g, Rectangle bounds, string title, string[] lines)
        {
            using (var body = new SolidBrush(Color.FromArgb(246, 247, 250)))
            {
                g.FillRectangle(body, bounds);
            }

            using (var bar = new SolidBrush(Color.FromArgb(226, 230, 238)))
            {
                g.FillRectangle(bar, bounds.X, bounds.Y, bounds.Width, 34);
            }

            using (var dot = new SolidBrush(Color.FromArgb(196, 202, 214)))
            {
                g.FillEllipse(dot, bounds.X + 14, bounds.Y + 12, 10, 10);
                g.FillEllipse(dot, bounds.X + 30, bounds.Y + 12, 10, 10);
                g.FillEllipse(dot, bounds.X + 46, bounds.Y + 12, 10, 10);
            }

            using (var titleFont = new Font("Microsoft YaHei UI", 10f))
            using (var textFont = new Font("Microsoft YaHei UI", 11f))
            using (var titleBrush = new SolidBrush(Color.FromArgb(90, 98, 112)))
            using (var textBrush = new SolidBrush(Color.FromArgb(58, 66, 80)))
            {
                g.DrawString(title, titleFont, titleBrush, bounds.X + 74, bounds.Y + 9);

                float y = bounds.Y + 62;
                foreach (string line in lines)
                {
                    g.DrawString(line, textFont, textBrush, bounds.X + 28, y);
                    y += 34f;
                }
            }
        }
    }
}
