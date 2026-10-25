using System;
using System.Collections.Generic;
using System.IO;

namespace ShotTool
{
    /// <summary>文件夹名校验：只允许单层名字，挡掉非法字符、保留设备名和路径穿越。</summary>
    internal static class PathRules
    {
        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private static readonly char[] ForbiddenChars = new char[]
        {
            '\\', '/', ':', '*', '?', '"', '<', '>', '|'
        };

        public const int MaxLength = 80;

        public static bool TryNormalize(string raw, out string name, out string error)
        {
            name = (raw ?? string.Empty).Trim();
            error = null;

            if (name.Length == 0)
            {
                error = "文件夹名不能为空";
                return false;
            }

            if (name.Length > MaxLength)
            {
                error = "文件夹名太长了，最多 " + MaxLength + " 个字符";
                return false;
            }

            foreach (char c in name)
            {
                if (c < 32)
                {
                    error = "文件夹名不能包含控制字符";
                    return false;
                }
            }

            if (name.IndexOfAny(ForbiddenChars) >= 0)
            {
                error = "文件夹名不能包含 \\ / : * ? \" < > | 这些字符";
                return false;
            }

            if (name == "." || name == "..")
            {
                error = "文件夹名不能是 . 或 ..";
                return false;
            }

            if (name.EndsWith(".", StringComparison.Ordinal))
            {
                error = "文件夹名不能以 . 结尾";
                return false;
            }

            // 大写点号前的部分才是设备名，例如 "CON.txt" 也是非法的
            int dot = name.IndexOf('.');
            string stem = dot >= 0 ? name.Substring(0, dot) : name;
            if (ReservedNames.Contains(stem.Trim()))
            {
                error = "“" + name + "”是 Windows 保留名称，换一个吧";
                return false;
            }

            return true;
        }

        /// <summary>把名字拼到根目录下，并确认不会落到根目录之外。</summary>
        public static string Combine(string root, string name)
        {
            string full = Path.GetFullPath(Path.Combine(root, name));
            string normalizedRoot = Path.GetFullPath(root);

            if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                normalizedRoot += Path.DirectorySeparatorChar;
            }

            if (!full.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("目标路径超出了桌面目录");
            }

            return full;
        }
    }
}
