using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ShotTool
{
    internal static class Program
    {
        internal static readonly uint ShowWindowMessage =
            Native.RegisterWindowMessage("ShotTool.ShowWindowMessage");

        [STAThread]
        private static void Main(string[] args)
        {
            bool selfTest = HasFlag(args, "--selftest");
            bool smoke = HasFlag(args, "--smoke");

            try
            {
                Native.SetProcessDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch
            {
                // 清单里已经声明过，这里只是双保险
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;

            if (HasFlag(args, "--shots"))
            {
                PreviewShots.Run();
                return;
            }

            if (selfTest)
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }

            if (smoke)
            {
                if (HasFlag(args, "--overlay"))
                {
                    RunOverlaySmoke();
                    return;
                }

                Application.Run(new MainForm(AppConfig.Load(), true));
                return;
            }

            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\ShotTool.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    ActivateRunningInstance();
                    return;
                }

                Application.Run(new MainForm(AppConfig.Load(), false));
            }
        }

        /// <summary>
        /// 已经有实例在跑：唤醒它。如果是另一个位置的副本在跑，先说清楚，
        /// 否则用户会以为「换了目录还是没反应」。
        /// </summary>
        private static void ActivateRunningInstance()
        {
            string other = OtherInstancePath();
            string self = Application.ExecutablePath;

            if (other != null && !string.Equals(other, self, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "截图工具已经在另一个位置运行：\n\n" + other +
                    "\n\n如果那个窗口用不了（例如无法创建文件夹），请到托盘图标上右键退出它，再启动当前这份。",
                    "截图工具",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            Native.PostMessage(Native.HWND_BROADCAST, ShowWindowMessage, IntPtr.Zero, IntPtr.Zero);
        }

        private static string OtherInstancePath()
        {
            try
            {
                int selfId = Process.GetCurrentProcess().Id;
                foreach (Process process in Process.GetProcessesByName("ShotTool"))
                {
                    try
                    {
                        if (process.Id == selfId)
                        {
                            continue;
                        }

                        return process.MainModule.FileName;
                    }
                    catch
                    {
                        // 拿不到路径就算未知，走原来的静默唤醒
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static void RunOverlaySmoke()
        {
            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-overlay.png");
            Rectangle virtualScreen = SystemInformation.VirtualScreen;

            using (Bitmap frozen = CaptureService.Capture(virtualScreen))
            using (var overlay = new OverlayForm(frozen, virtualScreen))
            {
                var timer = new System.Windows.Forms.Timer();
                timer.Interval = 900;
                timer.Tick += delegate
                {
                    timer.Stop();
                    timer.Dispose();

                    try
                    {
                        var area = new Rectangle(0, 0, Math.Min(1600, virtualScreen.Width), Math.Min(900, virtualScreen.Height));
                        using (Bitmap shot = CaptureService.Capture(area))
                        {
                            shot.Save(output, ImageFormat.Png);
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            File.WriteAllText(output + ".err", ex.ToString());
                        }
                        catch
                        {
                        }
                    }

                    overlay.Close();
                };
                timer.Start();
                overlay.ShowDialog();
            }
        }

        private static bool HasFlag(string[] args, string flag)
        {
            if (args == null)
            {
                return false;
            }

            foreach (string arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ReportCrash(e.Exception);
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            ReportCrash(e.ExceptionObject as Exception);
        }

        private static void ReportCrash(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + exception;
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "ShotTool-crash.log"), text);
            }
            catch
            {
            }

            try
            {
                MessageBox.Show(
                    "截图工具遇到问题：" + Environment.NewLine + Environment.NewLine + exception.Message,
                    "截图工具",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
}
