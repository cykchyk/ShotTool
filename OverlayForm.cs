using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ShotTool
{
    /// <summary>
    /// 冻结式选区遮罩：背景是按下热键那一刻的整屏快照，
    /// 单击 = 光标所在显示器全屏，拖动 = 矩形选区。
    /// </summary>
    internal sealed class OverlayForm : Form
    {
        private readonly Bitmap _frozen;
        private readonly Rectangle _virtualScreen;
        private readonly Font _font;

        private Point _dragStart;
        private Rectangle _selection;
        private bool _dragging;
        private Bitmap _result;

        public Bitmap Result
        {
            get { return _result; }
        }

        public OverlayForm(Bitmap frozen, Rectangle virtualScreen)
        {
            _frozen = frozen;
            _virtualScreen = virtualScreen;
            _font = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular, GraphicsUnit.Point);

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Bounds = virtualScreen;
            TopMost = true;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            BackColor = Color.Black;
            Text = "选择截图区域";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ForceWindowRect();
            Activate();
            Native.SetForegroundWindow(Handle);
        }

        /// <summary>仅用于生成 README 预览图：直接摆一个选区，不走鼠标事件。</summary>
        internal void SetDemoSelection(Rectangle clientSelection)
        {
            _dragging = true;
            _selection = clientSelection;
        }

        private void ForceWindowRect()
        {
            Native.SetWindowPos(
                Handle,
                Native.HWND_TOPMOST,
                _virtualScreen.Left,
                _virtualScreen.Top,
                _virtualScreen.Width,
                _virtualScreen.Height,
                Native.SWP_SHOWWINDOW);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_DPICHANGED)
            {
                ForceWindowRect();
                Invalidate();
                return;
            }

            base.WndProc(ref m);
        }

        // ---- 坐标换算：客户区 -> 物理像素 -------------------------------------

        private float ScaleX
        {
            get { return ClientSize.Width <= 0 ? 1f : (float)ClientSize.Width / _virtualScreen.Width; }
        }

        private float ScaleY
        {
            get { return ClientSize.Height <= 0 ? 1f : (float)ClientSize.Height / _virtualScreen.Height; }
        }

        private Point ToPhysical(Point client)
        {
            return new Point(
                _virtualScreen.Left + (int)Math.Round(client.X / ScaleX),
                _virtualScreen.Top + (int)Math.Round(client.Y / ScaleY));
        }

        private Rectangle ToPhysical(Rectangle client)
        {
            Point topLeft = ToPhysical(client.Location);
            return new Rectangle(
                topLeft.X,
                topLeft.Y,
                (int)Math.Round(client.Width / ScaleX),
                (int)Math.Round(client.Height / ScaleY));
        }

        private Rectangle ToClient(Rectangle physical)
        {
            return new Rectangle(
                (int)Math.Round((physical.X - _virtualScreen.Left) * ScaleX),
                (int)Math.Round((physical.Y - _virtualScreen.Top) * ScaleY),
                (int)Math.Round(physical.Width * ScaleX),
                (int)Math.Round(physical.Height * ScaleY));
        }

        // ---- 鼠标 / 键盘 ------------------------------------------------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Cancel();
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStart = e.Location;
                _selection = new Rectangle(e.Location, Size.Empty);
                Invalidate();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                _selection = Normalize(_dragStart, e.Location);
                Invalidate();
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_dragging)
            {
                base.OnMouseUp(e);
                return;
            }

            _dragging = false;

            int dx = Math.Abs(e.X - _dragStart.X);
            int dy = Math.Abs(e.Y - _dragStart.Y);

            // 没有拖动 = 点按：截光标所在那块屏幕
            if (dx <= 4 && dy <= 4)
            {
                Point physical = ToPhysical(e.Location);
                Screen screen = Screen.FromPoint(physical);
                Complete(screen.Bounds);
                return;
            }

            if (_selection.Width < 2 || _selection.Height < 2)
            {
                Cancel();
                return;
            }

            Complete(ToPhysical(_selection));
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Cancel();
                return;
            }

            base.OnKeyDown(e);
        }

        private static Rectangle Normalize(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X);
            int y = Math.Min(a.Y, b.Y);
            int width = Math.Abs(a.X - b.X);
            int height = Math.Abs(a.Y - b.Y);
            return new Rectangle(x, y, width, height);
        }

        // ---- 收尾 --------------------------------------------------------------

        private void Complete(Rectangle physical)
        {
            Rectangle relative = new Rectangle(
                physical.X - _virtualScreen.Left,
                physical.Y - _virtualScreen.Top,
                physical.Width,
                physical.Height);

            relative.Intersect(new Rectangle(0, 0, _frozen.Width, _frozen.Height));

            if (relative.Width < 1 || relative.Height < 1)
            {
                Cancel();
                return;
            }

            _result = _frozen.Clone(relative, PixelFormat.Format32bppArgb);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Cancel()
        {
            _dragging = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // ---- 绘制 --------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            g.DrawImage(_frozen, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));

            using (var dim = new SolidBrush(Color.FromArgb(112, 0, 0, 0)))
            {
                g.FillRectangle(dim, ClientRectangle);
            }

            bool hasSelection = _dragging && _selection.Width > 0 && _selection.Height > 0;
            if (!hasSelection)
            {
                DrawHint(g);
                return;
            }

            Rectangle physical = ToPhysical(_selection);

            // 选区内部还原成原图亮度
            g.DrawImage(
                _frozen,
                _selection,
                new Rectangle(
                    physical.X - _virtualScreen.Left,
                    physical.Y - _virtualScreen.Top,
                    Math.Max(1, physical.Width),
                    Math.Max(1, physical.Height)),
                GraphicsUnit.Pixel);

            using (var pen = new Pen(Color.FromArgb(0, 174, 255), 1f))
            {
                g.DrawRectangle(
                    pen,
                    _selection.X,
                    _selection.Y,
                    Math.Max(1, _selection.Width - 1),
                    Math.Max(1, _selection.Height - 1));
            }

            DrawSizeBadge(g, _selection, physical);
        }

        private void DrawSizeBadge(Graphics g, Rectangle selection, Rectangle physical)
        {
            string text = physical.Width + " × " + physical.Height;
            SizeF size = g.MeasureString(text, _font);

            float x = selection.X + 8f;
            float y = selection.Y + 8f;

            if (y + size.Height + 10f > ClientSize.Height)
            {
                y = selection.Y - size.Height - 10f;
            }

            if (x + size.Width + 12f > ClientSize.Width)
            {
                x = selection.Right - size.Width - 12f;
            }

            var box = new RectangleF(x - 6f, y - 4f, size.Width + 12f, size.Height + 8f);
            using (var back = new SolidBrush(Color.FromArgb(214, 18, 18, 22)))
            {
                g.FillRectangle(back, box);
            }

            using (var fore = new SolidBrush(Color.White))
            {
                g.DrawString(text, _font, fore, x, y);
            }
        }

        private void DrawHint(Graphics g)
        {
            const string text = "单击：截取光标所在屏幕      拖动：框选区域      Esc / 右键：取消";
            SizeF size = g.MeasureString(text, _font);

            Rectangle monitor = ToClient(Screen.FromPoint(Cursor.Position).Bounds);

            float x = monitor.Left + (monitor.Width - size.Width) / 2f;
            float y = monitor.Top + 28f;

            if (x < 0f)
            {
                x = 0f;
            }

            var box = new RectangleF(x - 16f, y - 10f, size.Width + 32f, size.Height + 20f);
            using (var back = new SolidBrush(Color.FromArgb(214, 18, 18, 22)))
            {
                g.FillRectangle(back, box);
            }

            using (var fore = new SolidBrush(Color.White))
            {
                g.DrawString(text, _font, fore, x, y);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _font.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
