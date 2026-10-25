using System;
using System.IO;
using Microsoft.Win32;

namespace ShotTool
{
    /// <summary>
    /// 运行时定位“桌面”目录。路径随机器/账户/重定向策略变化，所以不能写死：
    /// 先问外壳，再问注册表，再退回用户目录，最后退到本地应用数据。
    /// 每换一台电脑、每个账户都会重新解析。
    /// </summary>
    internal static class DesktopLocator
    {
        /// <summary>最近一次解析命中的来源，便于在日志里说明路径是怎么来的。</summary>
        public static string LastSource { get; private set; }

        public static string Resolve()
        {
            string path;

            // 1) 外壳 API：OneDrive“备份桌面”、域漫游、组策略重定向都会反映在这里
            path = FromShell(Environment.SpecialFolder.DesktopDirectory, "Shell.DesktopDirectory");
            if (path != null)
            {
                return path;
            }

            // 2) 少数环境下两个枚举值并不一致
            path = FromShell(Environment.SpecialFolder.Desktop, "Shell.Desktop");
            if (path != null)
            {
                return path;
            }

            // 3) 注册表里的用户 Shell Folders（外壳 API 异常时的兜底）
            path = FromRegistry("Desktop");
            if (path != null)
            {
                return path;
            }

            // 4) 常规位置 %USERPROFILE%\Desktop
            path = Probe(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"),
                "UserProfile.Desktop");
            if (path != null)
            {
                return path;
            }

            // 5) 都不可用：退到本地应用数据，至少截图不会丢
            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShotTool",
                "Screenshots");

            try
            {
                Directory.CreateDirectory(fallback);
                LastSource = "LocalAppData.Fallback";
                return fallback;
            }
            catch
            {
                LastSource = "Unavailable";
                return null;
            }
        }

        /// <summary>缓存路径失效（被删、盘符变化、重定向变更）时重新解析。</summary>
        public static string ResolveIfMissing(string cached)
        {
            if (!string.IsNullOrEmpty(cached) && Directory.Exists(cached))
            {
                return cached;
            }

            return Resolve();
        }

        private static string FromShell(Environment.SpecialFolder folder, string source)
        {
            try
            {
                string raw = Environment.GetFolderPath(folder);
                return Probe(raw, source);
            }
            catch
            {
                return null;
            }
        }

        private static string FromRegistry(string valueName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"))
                {
                    if (key == null)
                    {
                        return null;
                    }

                    string raw = key.GetValue(valueName) as string;
                    return Probe(raw, "Registry.ShellFolders");
                }
            }
            catch
            {
                return null;
            }
        }

        private static string Probe(string raw, string source)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                string path = Path.GetFullPath(raw.Trim());
                if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    return null;
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                if (!Directory.Exists(path))
                {
                    return null;
                }

                LastSource = source;
                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
