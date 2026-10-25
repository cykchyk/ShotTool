using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShotTool
{
    internal sealed class MainForm : Form
    {
        private const int HotkeyId = HotkeyApi.AppHotkeyId;

        private readonly AppConfig _config;
        private readonly bool _smokeMode;

        private NotifyIcon _tray;
        private TextBox _txtFolder;
        private Label _lblPath;
        private TextBox _txtHotkey;
        private Label _lblHotkeyHint;
        private CheckBox _chkTopMost;
        private CheckBox _chkClipboard;
        private Button _btnCapture;
        private Label _lblStatus;

        private HotkeySpec _hotkey;
        private bool _hotkeyRegistered;
        private bool _recording;
        private bool _capturing;
        private bool _reallyExit;
        private bool _trayHintShown;
        private string _lastSavedPath;
        private string _desktopRoot;

        public MainForm(AppConfig config, bool smokeMode)
        {
            _config = config ?? new AppConfig();
            _smokeMode = smokeMode;

            BuildUi();
            ApplyConfig();
            CreateTray();
        }

        // ---- 界面 -------------------------------------------------------------

        private void BuildUi()
        {
            SuspendLayout();

            Text = "截图工具";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            ClientSize = new Size(424, 204);
            BackColor = Color.FromArgb(250, 250, 252);
            Icon = LoadAppIcon();

            var lblFolder = new Label();
            lblFolder.Text = "文件夹名（保存到桌面）";
            lblFolder.Location = new Point(14, 10);
            lblFolder.AutoSize = true;

            _txtFolder = new TextBox();
            _txtFolder.Location = new Point(14, 29);
            _txtFolder.Size = new Size(298, 23);
            _txtFolder.MaxLength = PathRules.MaxLength;
            _txtFolder.BorderStyle = BorderStyle.FixedSingle;
            _txtFolder.TextChanged += FolderTextChanged;
            _txtFolder.Leave += FolderLeave;

            var btnOpen = new Button();
            btnOpen.Text = "打开";
            btnOpen.Location = new Point(320, 28);
            btnOpen.Size = new Size(88, 25);
            btnOpen.Click += delegate { OpenCurrentFolder(); };

            _lblPath = new Label();
            _lblPath.Location = new Point(14, 57);
            _lblPath.Size = new Size(394, 16);
            _lblPath.AutoEllipsis = true;
            _lblPath.ForeColor = Color.DimGray;

            var lblHotkey = new Label();
            lblHotkey.Text = "截图热键";
            lblHotkey.Location = new Point(14, 84);
            lblHotkey.AutoSize = true;

            _txtHotkey = new TextBox();
            _txtHotkey.Location = new Point(14, 102);
            _txtHotkey.Size = new Size(140, 23);
            _txtHotkey.ReadOnly = true;
            _txtHotkey.ShortcutsEnabled = false;
            _txtHotkey.Cursor = Cursors.Hand;
            _txtHotkey.BorderStyle = BorderStyle.FixedSingle;
            _txtHotkey.Enter += HotkeyEnter;
            _txtHotkey.Leave += HotkeyLeave;
            _txtHotkey.KeyDown += HotkeyKeyDown;

            _lblHotkeyHint = new Label();
            _lblHotkeyHint.Location = new Point(162, 106);
            _lblHotkeyHint.Size = new Size(246, 16);
            _lblHotkeyHint.Text = "点这里后按下组合键";
            _lblHotkeyHint.ForeColor = Color.DimGray;

            _chkTopMost = new CheckBox();
            _chkTopMost.Text = "窗口置顶";
            _chkTopMost.Location = new Point(14, 137);
            _chkTopMost.AutoSize = true;
            _chkTopMost.CheckedChanged += delegate
            {
                TopMost = _chkTopMost.Checked;
                _config.TopMost = _chkTopMost.Checked;
                SaveConfig();
            };

            _chkClipboard = new CheckBox();
            _chkClipboard.Text = "截图后复制到剪贴板";
            _chkClipboard.Location = new Point(118, 137);
            _chkClipboard.AutoSize = true;
            _chkClipboard.CheckedChanged += delegate
            {
                _config.CopyToClipboard = _chkClipboard.Checked;
                SaveConfig();
            };

            _btnCapture = new Button();
            _btnCapture.Text = "立即截图";
            _btnCapture.Location = new Point(14, 164);
            _btnCapture.Size = new Size(128, 30);
            _btnCapture.FlatStyle = FlatStyle.Flat;
            _btnCapture.FlatAppearance.BorderSize = 0;
            _btnCapture.BackColor = Color.FromArgb(30, 120, 220);
            _btnCapture.ForeColor = Color.White;
            _btnCapture.Click += delegate { CaptureFlow(); };

            _lblStatus = new Label();
            _lblStatus.Location = new Point(152, 171);
            _lblStatus.Size = new Size(256, 16);
            _lblStatus.AutoEllipsis = true;
            _lblStatus.ForeColor = Color.DimGray;

            Controls.Add(lblFolder);
            Controls.Add(_txtFolder);
            Controls.Add(btnOpen);
            Controls.Add(_lblPath);
            Controls.Add(lblHotkey);
            Controls.Add(_txtHotkey);
            Controls.Add(_lblHotkeyHint);
            Controls.Add(_chkTopMost);
            Controls.Add(_chkClipboard);
            Controls.Add(_btnCapture);
            Controls.Add(_lblStatus);

            ResumeLayout(false);
            PerformLayout();
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                {
                    return icon;
                }
            }
            catch
            {
            }

            return SystemIcons.Application;
        }

        private void ApplyConfig()
        {
            _chkTopMost.Checked = _config.TopMost;
            _chkClipboard.Checked = _config.CopyToClipboard;
            TopMost = _config.TopMost;

            _txtFolder.Text = string.IsNullOrEmpty(_config.FolderName) ? "截图" : _config.FolderName;

            _hotkey = new HotkeySpec(_config.HotkeyModifiers, _config.HotkeyKey);
            if (!_hotkey.IsValid)
            {
                _hotkey = HotkeySpec.Default;
            }

            _txtHotkey.Text = _hotkey.Display;
            RestoreWindowPosition();
        }

        private void RestoreWindowPosition()
        {
            if (_config.WindowX == int.MinValue || _config.WindowY == int.MinValue)
            {
                CenterOnPrimary();
                return;
            }

            var bounds = new Rectangle(_config.WindowX, _config.WindowY, Width, Height);
            bool visible = false;
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(bounds))
                {
                    visible = true;
                    break;
                }
            }

            if (!visible)
            {
                CenterOnPrimary();
                return;
            }

            Location = new Point(_config.WindowX, _config.WindowY);
        }

        private void CenterOnPrimary()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                area.Left + (area.Width - Width) / 2,
                area.Top + (area.Height - Height) / 2);
        }

        private void SaveWindowState()
        {
            if (_smokeMode || WindowState != FormWindowState.Normal)
            {
                return;
            }

            _config.WindowX = Location.X;
            _config.WindowY = Location.Y;
        }

        private void SaveConfig()
        {
            if (_smokeMode)
            {
                return;
            }

            _config.Save();
        }

        // ---- 托盘 -------------------------------------------------------------

        private void CreateTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = Icon;
            _tray.Text = "截图工具";
            _tray.Visible = true;

            var menu = new ContextMenuStrip();
            menu.Items.Add("立即截图", null, delegate { CaptureFlow(); });
            menu.Items.Add("显示主窗口", null, delegate { ShowMainWindow(); });
            menu.Items.Add("打开保存文件夹", null, delegate { OpenCurrentFolder(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { ExitApp(); });

            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowMainWindow(); };
            _tray.BalloonTipClicked += delegate { OpenLastSaved(); };
        }

        private void ShowMainWindow()
        {
            Show();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Activate();
            BringToFront();
        }

        private void ExitApp()
        {
            _reallyExit = true;
            Close();
        }

        private void OpenLastSaved()
        {
            if (string.IsNullOrEmpty(_lastSavedPath))
            {
                return;
            }

            try
            {
                Process.Start("explorer.exe", "/select,\"" + _lastSavedPath + "\"");
            }
            catch (Exception ex)
            {
                ReportError("打开文件位置失败：" + ex.Message);
            }
        }

        // ---- 文件夹 -----------------------------------------------------------

        private string DesktopRoot
        {
            get
            {
                _desktopRoot = DesktopLocator.ResolveIfMissing(_desktopRoot);
                return _desktopRoot;
            }
        }

        private void FolderTextChanged(object sender, EventArgs e)
        {
            UpdateFolderPreview();
        }

        private void FolderLeave(object sender, EventArgs e)
        {
            string name;
            string error;
            if (PathRules.TryNormalize(_txtFolder.Text, out name, out error))
            {
                _config.FolderName = name;
                SaveConfig();
            }
        }

        private void UpdateFolderPreview()
        {
            string name;
            string error;
            if (!PathRules.TryNormalize(_txtFolder.Text, out name, out error))
            {
                _lblPath.Text = error;
                _lblPath.ForeColor = Color.FromArgb(190, 40, 40);
                _btnCapture.Enabled = false;
                return;
            }

            string root = DesktopRoot;
            if (string.IsNullOrEmpty(root))
            {
                _lblPath.Text = "找不到可写的目标目录";
                _lblPath.ForeColor = Color.FromArgb(190, 40, 40);
                _btnCapture.Enabled = false;
                return;
            }

            _lblPath.Text = "将保存到：" + Path.Combine(root, name);
            _lblPath.ForeColor = Color.DimGray;
            _btnCapture.Enabled = true;
        }

        private bool TryGetTargetFolder(out string directory, out string error)
        {
            directory = null;
            string name;
            if (!PathRules.TryNormalize(_txtFolder.Text, out name, out error))
            {
                return false;
            }

            try
            {
                string root = DesktopRoot;
                if (string.IsNullOrEmpty(root))
                {
                    error = "找不到桌面目录，请检查账户配置";
                    return false;
                }

                directory = PathRules.Combine(root, name);

                if (File.Exists(directory))
                {
                    error = "桌面上已有同名文件：" + name;
                    return false;
                }

                Directory.CreateDirectory(directory);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                error = "无法在桌面创建文件夹：访问被拒绝。可能是安全软件拦截，或程序当前所在目录受限。" +
                        "试试把 ShotTool.exe 复制到桌面或 D 盘再运行。";
                return false;
            }
            catch (Exception ex)
            {
                error = "无法创建文件夹：" + ex.Message;
                return false;
            }
        }

        private void OpenCurrentFolder()
        {
            string directory;
            string error;
            if (!TryGetTargetFolder(out directory, out error))
            {
                ReportError(error);
                return;
            }

            try
            {
                Process.Start("explorer.exe", "\"" + directory + "\"");
            }
            catch (Exception ex)
            {
                ReportError("打开文件夹失败：" + ex.Message);
            }
        }

        // ---- 热键 -------------------------------------------------------------

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            RegisterHotkeyAtStartup();
            UpdateFolderPreview();
        }

        private void RegisterHotkeyAtStartup()
        {
            int error;
            if (HotkeyApi.TryRegister(Handle, HotkeyId, _hotkey, out error))
            {
                _hotkeyRegistered = true;
                SetStatus("就绪，热键 " + _hotkey.Display, false);
                return;
            }

            string original = _hotkey.Display;
            foreach (HotkeySpec alternative in HotkeySpec.Fallbacks)
            {
                if (HotkeyApi.TryRegister(Handle, HotkeyId, alternative, out error))
                {
                    // 只改这一次运行使用的键，不覆盖用户保存的设置：
                    // 等占用它的程序退出后，下次启动会自动回到原来那个键
                    _hotkey = alternative;
                    _hotkeyRegistered = true;
                    _txtHotkey.Text = alternative.Display;
                    SetStatus(original + " 已被占用，本次改用 " + alternative.Display, true);
                    return;
                }
            }

            SetStatus(original + " 注册失败，请点输入框重新设置", true);
        }

        private void UnregisterHotkey()
        {
            if (_hotkeyRegistered)
            {
                HotkeyApi.Unregister(Handle, HotkeyId);
                _hotkeyRegistered = false;
            }
        }

        private void HotkeyEnter(object sender, EventArgs e)
        {
            if (_smokeMode || _recording)
            {
                return;
            }

            _recording = true;
            UnregisterHotkey();
            _txtHotkey.Text = "按下组合键…";
            _lblHotkeyHint.Text = "Esc 取消";
            _lblHotkeyHint.ForeColor = Color.DimGray;
        }

        private void HotkeyLeave(object sender, EventArgs e)
        {
            if (_recording)
            {
                EndHotkeyRecording();
            }
        }

        private void EndHotkeyRecording()
        {
            _recording = false;

            if (!_hotkeyRegistered)
            {
                int error;
                _hotkeyRegistered = HotkeyApi.TryRegister(Handle, HotkeyId, _hotkey, out error);
            }

            _txtHotkey.Text = _hotkey.Display;
            _lblHotkeyHint.Text = "点这里后按下组合键";
            _lblHotkeyHint.ForeColor = Color.DimGray;
        }

        private void HotkeyKeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;

            if (!_recording)
            {
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                EndHotkeyRecording();
                return;
            }

            if (HotkeySpec.IsModifierKey(e.KeyCode))
            {
                _txtHotkey.Text = DescribePendingModifiers() + "…";
                return;
            }

            uint modifiers = 0;
            if (e.Control)
            {
                modifiers |= Native.MOD_CONTROL;
            }

            if (e.Alt)
            {
                modifiers |= Native.MOD_ALT;
            }

            if (e.Shift)
            {
                modifiers |= Native.MOD_SHIFT;
            }

            Keys held = Control.ModifierKeys;
            if ((held & Keys.LWin) != 0 || (held & Keys.RWin) != 0)
            {
                modifiers |= Native.MOD_WIN;
            }

            if (modifiers == 0)
            {
                SetStatus("热键至少要有一个 Ctrl / Alt / Shift / Win", true);
                return;
            }

            ApplyHotkey(new HotkeySpec(modifiers, (uint)e.KeyCode));
        }

        private static string DescribePendingModifiers()
        {
            string text = string.Empty;
            Keys held = Control.ModifierKeys;
            if ((held & Keys.Control) != 0)
            {
                text += "Ctrl+";
            }

            if ((held & Keys.Alt) != 0)
            {
                text += "Alt+";
            }

            if ((held & Keys.Shift) != 0)
            {
                text += "Shift+";
            }

            if ((held & Keys.LWin) != 0 || (held & Keys.RWin) != 0)
            {
                text += "Win+";
            }

            return text;
        }

        private void ApplyHotkey(HotkeySpec spec)
        {
            string reservedReason;
            if (HotkeyRules.IsReserved(spec, out reservedReason))
            {
                SetStatus(spec.Display + " 是系统保留组合（" + reservedReason + "），换一个", true);
                EndHotkeyRecording();
                return;
            }

            int error;
            if (!HotkeyApi.Probe(Handle, spec, out error))
            {
                SetStatus(spec.Display + " " + HotkeyApi.DescribeError(error) + "，仍使用 " + _hotkey.Display, true);
                EndHotkeyRecording();
                return;
            }

            UnregisterHotkey();
            if (!HotkeyApi.TryRegister(Handle, HotkeyId, spec, out error))
            {
                // 探测和正式注册之间被别的程序抢走了，恢复旧键
                HotkeyApi.TryRegister(Handle, HotkeyId, _hotkey, out error);
                _hotkeyRegistered = true;
                SetStatus("注册失败：" + HotkeyApi.DescribeError(error) + "，已恢复 " + _hotkey.Display, true);
                EndHotkeyRecording();
                return;
            }

            _hotkey = spec;
            _hotkeyRegistered = true;
            _config.HotkeyModifiers = spec.Modifiers;
            _config.HotkeyKey = spec.VirtualKey;
            SaveConfig();

            string known;
            if (KnownConflicts.TryDescribe(spec, out known))
            {
                SetStatus("热键已改为 " + spec.Display + "（注意：" + known + " 也常用这个键）", false);
            }
            else
            {
                SetStatus("热键已改为 " + spec.Display, false);
            }

            EndHotkeyRecording();
        }

        // ---- 截图 -------------------------------------------------------------

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                // 从消息处理里投递出去，避免在 WndProc 里跑模态窗口
                BeginInvoke(new Action(CaptureFlow));
                return;
            }

            if (Program.ShowWindowMessage != 0 && (uint)m.Msg == Program.ShowWindowMessage)
            {
                ShowMainWindow();
                return;
            }

            base.WndProc(ref m);
        }

        private void CaptureFlow()
        {
            if (_capturing || _recording)
            {
                return;
            }

            string directory;
            string error;
            if (!TryGetTargetFolder(out directory, out error))
            {
                ReportError(error);
                return;
            }

            _capturing = true;
            bool wasVisible = Visible;
            bool hideDuringCapture = wasVisible && _config.HideWindowOnCapture;

            try
            {
                if (hideDuringCapture)
                {
                    Hide();
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(90);
                }

                Rectangle virtualScreen = SystemInformation.VirtualScreen;
                Bitmap frozen;
                try
                {
                    frozen = CaptureService.Capture(virtualScreen);
                }
                catch (Exception ex)
                {
                    ReportError("抓屏失败：" + ex.Message);
                    return;
                }

                try
                {
                    using (var overlay = new OverlayForm(frozen, virtualScreen))
                    {
                        DialogResult result = overlay.ShowDialog();
                        if (result == DialogResult.OK && overlay.Result != null)
                        {
                            using (Bitmap shot = overlay.Result)
                            {
                                SaveShot(shot, directory);
                            }
                        }
                    }
                }
                finally
                {
                    frozen.Dispose();
                }
            }
            finally
            {
                if (hideDuringCapture)
                {
                    Show();
                    Activate();
                }

                _capturing = false;
            }
        }

        private void SaveShot(Bitmap shot, string directory)
        {
            byte[] png;
            try
            {
                png = CaptureService.EncodePng(shot);
            }
            catch (Exception ex)
            {
                ReportError("图片编码失败：" + ex.Message);
                return;
            }

            try
            {
                string path = CaptureService.SavePng(directory, png);

                if (_config.CopyToClipboard)
                {
                    TryCopyImage(shot);
                }

                ReportSaved(path);
            }
            catch (Exception ex)
            {
                ReportError("保存失败：" + ex.Message);
                OfferFallback(png, directory);
            }
        }

        private void OfferFallback(byte[] png, string directory)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "保存失败，请另存为";
                dialog.Filter = "PNG 图片|*.png";
                dialog.FileName = "ScreenShot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";

                try
                {
                    if (Directory.Exists(directory))
                    {
                        dialog.InitialDirectory = directory;
                    }
                }
                catch
                {
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllBytes(dialog.FileName, png);
                        ReportSaved(dialog.FileName);
                        return;
                    }
                    catch (Exception ex)
                    {
                        ReportError("另存为也失败了：" + ex.Message);
                    }
                }

                TryCopyImageFromBytes(png);
            }
        }

        private bool TryCopyImage(Image image)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.SetImage(image);
                    return true;
                }
                catch (ExternalException)
                {
                    System.Threading.Thread.Sleep(40);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private void TryCopyImageFromBytes(byte[] png)
        {
            try
            {
                using (var stream = new MemoryStream(png))
                using (Image image = Image.FromStream(stream))
                {
                    if (TryCopyImage(image))
                    {
                        SetStatus("截图已复制到剪贴板，粘贴即可使用", false);
                    }
                }
            }
            catch
            {
            }
        }

        private void ReportSaved(string path)
        {
            _lastSavedPath = path;
            SetStatus("已保存：" + path, false);

            try
            {
                _tray.ShowBalloonTip(
                    2500,
                    "截图已保存",
                    Path.GetFileName(path) + "\r\n" + Path.GetDirectoryName(path),
                    ToolTipIcon.Info);
            }
            catch
            {
            }
        }

        private void ReportError(string message)
        {
            SetStatus(message, true);

            try
            {
                _tray.ShowBalloonTip(2500, "截图工具", message, ToolTipIcon.Warning);
            }
            catch
            {
            }
        }

        private void SetStatus(string text, bool error)
        {
            _lblStatus.Text = text;
            _lblStatus.ForeColor = error ? Color.FromArgb(190, 40, 40) : Color.DimGray;
        }

        // ---- 生命周期 ---------------------------------------------------------

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_smokeMode)
            {
                RunSmokeTest();
            }
        }

        private void RunSmokeTest()
        {
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 900;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();

                try
                {
                    using (Bitmap bitmap = CaptureService.Capture(Bounds))
                    {
                        bitmap.Save(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke.png"),
                            ImageFormat.Png);
                    }
                }
                catch (Exception ex)
                {
                    File.WriteAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke.err"),
                        ex.ToString());
                }

                _reallyExit = true;
                Close();
            };
            timer.Start();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (!_smokeMode && WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
                Hide();
                ShowTrayHintOnce();
            }
        }

        private void ShowTrayHintOnce()
        {
            if (_trayHintShown || _tray == null)
            {
                return;
            }

            _trayHintShown = true;
            try
            {
                _tray.ShowBalloonTip(2500, "截图工具仍在运行", "已隐藏到托盘，热键依然有效。", ToolTipIcon.Info);
            }
            catch
            {
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowTrayHintOnce();
                return;
            }

            SaveWindowState();
            SaveConfig();
            UnregisterHotkey();

            if (_tray != null)
            {
                _tray.Visible = false;
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                    _tray = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
