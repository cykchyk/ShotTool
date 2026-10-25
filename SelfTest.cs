using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ShotTool
{
    /// <summary>无界面自检：不弹窗、不写用户配置，结果落在 %TEMP%\ShotTool-selftest.log。</summary>
    internal static class SelfTest
    {
        private static readonly List<string> Lines = new List<string>();
        private static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log");

        private static bool _failed;

        public static int Run()
        {
            LogLine("START " + DateTime.Now.ToString("HH:mm:ss"));

            try
            {
                RunCore();
            }
            catch (Exception ex)
            {
                _failed = true;
                LogLine("FATAL " + ex);
            }

            LogLine(_failed ? "RESULT: FAIL" : "RESULT: PASS");

            try
            {
                Console.WriteLine(string.Join(Environment.NewLine, Lines.ToArray()));
            }
            catch
            {
            }

            return _failed ? 1 : 0;
        }

        private static void RunCore()
        {
            LogLine("TEMP=" + Path.GetTempPath());
            LogLine("BASE=" + AppDomain.CurrentDomain.BaseDirectory);

            Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);

            string tempRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest-work");
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
            catch
            {
            }

            Directory.CreateDirectory(tempRoot);
            Environment.SetEnvironmentVariable("SHOTTOOL_CONFIG_DIR", Path.Combine(tempRoot, "config"));

            LogLine("STEP desktop");
            TestDesktop();
            TestDesktopWritable();
            LogLine("STEP pathrules");
            TestPathRules();
            LogLine("STEP config");
            TestConfig(tempRoot);
            LogLine("STEP capture");
            TestCapture(tempRoot);
            LogLine("STEP hotkey");
            TestHotkey();
            LogLine("STEP done");
        }

        private static void LogLine(string line)
        {
            Lines.Add(line);
            try
            {
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static void TestDesktop()
        {
            string desktop = DesktopLocator.Resolve();
            Check(
                "桌面目录可解析",
                !string.IsNullOrEmpty(desktop) && Directory.Exists(desktop),
                desktop + "  <- " + DesktopLocator.LastSource);
        }

        private static void TestDesktopWritable()
        {
            string root = DesktopLocator.Resolve();
            string probe = Path.Combine(root, "ShotTool-写入测试");
            try
            {
                Directory.CreateDirectory(probe);
                File.WriteAllText(Path.Combine(probe, "probe.txt"), "ok");
                Check("桌面可写入（会创建同名文件夹）", true, probe);
            }
            catch (Exception ex)
            {
                Warn(
                    "桌面写入被拒绝 —— 可能是安全软件拦截，或程序运行在受限目录；换个目录再试",
                    ex.GetType().Name + " 0x" + ex.HResult.ToString("X8"));
            }
            finally
            {
                try
                {
                    Directory.Delete(probe, true);
                }
                catch
                {
                }
            }
        }

        private static void Warn(string name, string detail)
        {
            string line = "WARN  " + name;
            if (!string.IsNullOrEmpty(detail))
            {
                line += "   [" + detail + "]";
            }

            LogLine(line);
        }

        private static void TestPathRules()
        {
            string name;
            string error;

            Check("空名被拒", !PathRules.TryNormalize("   ", out name, out error));
            Check("斜杠被拒", !PathRules.TryNormalize("a/b", out name, out error));
            Check("反斜杠被拒", !PathRules.TryNormalize("a\\b", out name, out error));
            Check("双点被拒", !PathRules.TryNormalize("..", out name, out error));
            Check("保留名被拒", !PathRules.TryNormalize("CON", out name, out error));
            Check("带扩展保留名被拒", !PathRules.TryNormalize("NUL.txt", out name, out error));
            Check("结尾点被拒", !PathRules.TryNormalize("abc.", out name, out error));
            Check("问号被拒", !PathRules.TryNormalize("a?b", out name, out error));
            Check(
                "中文名通过",
                PathRules.TryNormalize("  截图 测试  ", out name, out error) && name == "截图 测试",
                error);
            Check("英文名通过", PathRules.TryNormalize("Shots", out name, out error));

            string desktop = DesktopLocator.Resolve();
            try
            {
                string combined = PathRules.Combine(desktop, "ShotToolTest");
                Check(
                    "路径拼接留在桌面内",
                    combined.StartsWith(desktop, StringComparison.OrdinalIgnoreCase),
                    combined);
            }
            catch (Exception ex)
            {
                Check("路径拼接留在桌面内", false, ex.Message);
            }
        }

        private static void TestConfig(string tempRoot)
        {
            var config = new AppConfig();
            config.FolderName = "测试目录";
            config.HotkeyModifiers = Native.MOD_ALT;
            config.HotkeyKey = 0x58;
            config.TopMost = false;
            config.Save();

            AppConfig loaded = AppConfig.Load();
            Check(
                "配置保存/读取",
                loaded.FolderName == "测试目录" &&
                loaded.HotkeyModifiers == Native.MOD_ALT &&
                loaded.HotkeyKey == 0x58 &&
                loaded.TopMost == false,
                loaded.FolderName);

            Check(
                "配置文件在临时目录",
                File.Exists(Path.Combine(tempRoot, "config", "config.json")),
                AppConfig.ConfigPath);
        }

        private static void TestCapture(string tempRoot)
        {
            Rectangle virtualScreen = SystemInformation.VirtualScreen;

            using (Bitmap bitmap = CaptureService.Capture(virtualScreen))
            {
                Check(
                    "抓屏尺寸与虚拟屏幕一致",
                    bitmap.Width == virtualScreen.Width && bitmap.Height == virtualScreen.Height,
                    bitmap.Width + "x" + bitmap.Height + " / " + virtualScreen.Width + "x" + virtualScreen.Height);

                byte[] png = CaptureService.EncodePng(bitmap);
                Check(
                    "PNG 编码有效",
                    png.Length > 100 && png[0] == 0x89 && png[1] == 0x50 && png[2] == 0x4E && png[3] == 0x47,
                    png.Length + " bytes");

                string directory = Path.Combine(tempRoot, "输出");
                string first = CaptureService.SavePng(directory, png);
                Check("PNG 落盘", File.Exists(first), first);

                using (Image reloaded = Image.FromFile(first))
                {
                    Check(
                        "PNG 可回读",
                        reloaded.Width == bitmap.Width && reloaded.Height == bitmap.Height,
                        reloaded.Width + "x" + reloaded.Height);
                }

                string second = CaptureService.SavePng(directory, png);
                Check("同毫秒不覆盖", !string.Equals(first, second, StringComparison.OrdinalIgnoreCase), second);
            }
        }

        private static void TestHotkey()
        {
            var spec = new HotkeySpec(
                Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT,
                (uint)Keys.F11);

            using (var form = new Form())
            {
                IntPtr handle = form.Handle;
                int error;

                bool registered = HotkeyApi.TryRegister(handle, HotkeyApi.AppHotkeyId, spec, out error);
                Check("热键注册", registered, HotkeyApi.DescribeError(error) + " (" + spec.Display + ")");

                if (registered)
                {
                    int probeError;
                    bool free = HotkeyApi.Probe(handle, spec, out probeError);
                    Check(
                        "重复注册被识别为冲突",
                        !free && probeError == Native.ERROR_HOTKEY_ALREADY_REGISTERED,
                        "win32=" + probeError);

                    HotkeyApi.Unregister(handle, HotkeyApi.AppHotkeyId);

                    int afterError;
                    bool freeAgain = HotkeyApi.Probe(handle, spec, out afterError);
                    Check("注销后探测为空闲", freeAgain, "win32=" + afterError);
                }
            }

            string reason;
            Check(
                "Alt+Tab 判为系统保留",
                HotkeyRules.IsReserved(new HotkeySpec(Native.MOD_ALT, (uint)Keys.Tab), out reason),
                reason);

            Check(
                "Ctrl+Alt+S 不在保留表",
                !HotkeyRules.IsReserved(
                    new HotkeySpec(Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.S),
                    out reason));

            Check(
                "热键显示文本",
                new HotkeySpec(Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.S).Display == "Ctrl+Alt+S",
                new HotkeySpec(Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.S).Display);

            Check(
                "功能键显示文本",
                new HotkeySpec(Native.MOD_CONTROL, (uint)Keys.F1).Display == "Ctrl+F1",
                new HotkeySpec(Native.MOD_CONTROL, (uint)Keys.F1).Display);
        }

        private static void Check(string name, bool passed, string detail = null)
        {
            if (!passed)
            {
                _failed = true;
            }

            string line = (passed ? "PASS  " : "FAIL  ") + name;
            if (!string.IsNullOrEmpty(detail))
            {
                line += "   [" + detail + "]";
            }

            LogLine(line);
        }
    }
}
