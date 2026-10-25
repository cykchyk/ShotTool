using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ShotTool
{
    /// <summary>一个热键 = 修饰键组合 + 虚拟键码。</summary>
    internal sealed class HotkeySpec : IEquatable<HotkeySpec>
    {
        public uint Modifiers { get; private set; }
        public uint VirtualKey { get; private set; }

        public HotkeySpec(uint modifiers, uint virtualKey)
        {
            Modifiers = modifiers;
            VirtualKey = virtualKey;
        }

        public static HotkeySpec Default
        {
            get { return new HotkeySpec(Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.S); }
        }

        /// <summary>被占用时依次尝试的备选，都保持“两键、左手可按”的原则。</summary>
        public static HotkeySpec[] Fallbacks
        {
            get
            {
                return new HotkeySpec[]
                {
                    new HotkeySpec(Native.MOD_ALT, (uint)Keys.S),
                    new HotkeySpec(Native.MOD_CONTROL | Native.MOD_SHIFT, (uint)Keys.S),
                    new HotkeySpec(Native.MOD_ALT, (uint)Keys.X)
                };
            }
        }

        public bool IsValid
        {
            get { return Modifiers != 0 && VirtualKey != 0 && !IsModifierKey((Keys)VirtualKey); }
        }

        public static bool IsModifierKey(Keys key)
        {
            switch (key)
            {
                case Keys.ShiftKey:
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                case Keys.ControlKey:
                case Keys.LControlKey:
                case Keys.RControlKey:
                case Keys.Menu:
                case Keys.LMenu:
                case Keys.RMenu:
                case Keys.LWin:
                case Keys.RWin:
                    return true;
                default:
                    return false;
            }
        }

        public string Display
        {
            get
            {
                var builder = new StringBuilder();
                if ((Modifiers & Native.MOD_CONTROL) != 0) builder.Append("Ctrl+");
                if ((Modifiers & Native.MOD_ALT) != 0) builder.Append("Alt+");
                if ((Modifiers & Native.MOD_SHIFT) != 0) builder.Append("Shift+");
                if ((Modifiers & Native.MOD_WIN) != 0) builder.Append("Win+");
                builder.Append(KeyName(VirtualKey));
                return builder.ToString();
            }
        }

        public static string KeyName(uint virtualKey)
        {
            Keys key = (Keys)virtualKey;

            if (virtualKey >= (uint)Keys.A && virtualKey <= (uint)Keys.Z)
            {
                return ((char)virtualKey).ToString();
            }

            if (virtualKey >= (uint)Keys.D0 && virtualKey <= (uint)Keys.D9)
            {
                return ((char)virtualKey).ToString();
            }

            if (key >= Keys.F1 && key <= Keys.F24)
            {
                return "F" + (virtualKey - (uint)Keys.F1 + 1);
            }

            switch (key)
            {
                case Keys.OemSemicolon: return ";";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                case Keys.Oemtilde: return "`";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemPipe: return "\\";
                case Keys.OemQuotes: return "'";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.Space: return "Space";
                case Keys.Tab: return "Tab";
                case Keys.Escape: return "Esc";
                case Keys.Enter: return "Enter";
                case Keys.Back: return "Backspace";
                case Keys.Delete: return "Delete";
                case Keys.Insert: return "Insert";
                case Keys.Home: return "Home";
                case Keys.End: return "End";
                case Keys.PageUp: return "PageUp";
                case Keys.PageDown: return "PageDown";
                case Keys.Up: return "Up";
                case Keys.Down: return "Down";
                case Keys.Left: return "Left";
                case Keys.Right: return "Right";
                default: return key.ToString();
            }
        }

        public bool Equals(HotkeySpec other)
        {
            return other != null && other.Modifiers == Modifiers && other.VirtualKey == VirtualKey;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as HotkeySpec);
        }

        public override int GetHashCode()
        {
            return (int)(Modifiers * 397 ^ VirtualKey);
        }

        public override string ToString()
        {
            return Display;
        }
    }

    /// <summary>系统保留 / 必然被抢先的组合，直接拒绝。</summary>
    internal static class HotkeyRules
    {
        private static readonly Dictionary<string, string> Reserved = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { Key(Native.MOD_ALT, Keys.Tab), "切换窗口" },
            { Key(Native.MOD_ALT, Keys.F4), "关闭窗口" },
            { Key(Native.MOD_ALT, Keys.Space), "窗口菜单" },
            { Key(Native.MOD_ALT, Keys.Escape), "切换窗口" },
            { Key(Native.MOD_CONTROL, Keys.Escape), "开始菜单" },
            { Key(Native.MOD_CONTROL | Native.MOD_ALT, Keys.Delete), "安全选项" },
            { Key(Native.MOD_CONTROL | Native.MOD_ALT, Keys.Tab), "切换窗口" },
            { Key(Native.MOD_WIN, Keys.L), "锁定屏幕" },
            { Key(Native.MOD_WIN, Keys.D), "显示桌面" },
            { Key(Native.MOD_WIN, Keys.E), "资源管理器" },
            { Key(Native.MOD_WIN, Keys.R), "运行" },
            { Key(Native.MOD_WIN, Keys.S), "搜索" },
            { Key(Native.MOD_WIN, Keys.A), "操作中心" },
            { Key(Native.MOD_WIN, Keys.Tab), "任务视图" },
            { Key(Native.MOD_WIN, Keys.I), "设置" },
            { Key(Native.MOD_WIN, Keys.P), "投影" },
            { Key(Native.MOD_WIN, Keys.V), "剪贴板历史" }
        };

        private static string Key(uint modifiers, Keys key)
        {
            return modifiers.ToString() + ":" + ((uint)key).ToString();
        }

        public static bool IsReserved(HotkeySpec spec, out string reason)
        {
            return Reserved.TryGetValue(spec.Modifiers.ToString() + ":" + spec.VirtualKey.ToString(), out reason);
        }
    }

    /// <summary>常见软件的默认截图 / 覆盖层热键，只提示不拦截。</summary>
    internal static class KnownConflicts
    {
        private static readonly Dictionary<string, string> Known = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { Entry(Native.MOD_CONTROL | Native.MOD_ALT, Keys.A), "QQ 截图" },
            { Entry(Native.MOD_ALT, Keys.A), "微信截图" },
            { Entry(Native.MOD_CONTROL | Native.MOD_SHIFT, Keys.A), "钉钉 / 飞书截图" },
            { Entry(0, Keys.F1), "Snipaste" },
            { Entry(Native.MOD_WIN | Native.MOD_SHIFT, Keys.S), "Windows 系统截图" },
            { Entry(Native.MOD_WIN, Keys.G), "Xbox Game Bar" },
            { Entry(Native.MOD_ALT, Keys.Z), "NVIDIA 覆盖层" },
            { Entry(Native.MOD_CONTROL, Keys.Space), "输入法切换" },
            { Entry(Native.MOD_SHIFT, Keys.Space), "输入法切换" }
        };

        private static string Entry(uint modifiers, Keys key)
        {
            return modifiers.ToString() + ":" + ((uint)key).ToString();
        }

        public static bool TryDescribe(HotkeySpec spec, out string description)
        {
            return Known.TryGetValue(spec.Modifiers.ToString() + ":" + spec.VirtualKey.ToString(), out description);
        }
    }

    /// <summary>RegisterHotKey 的薄封装，顺带做“试注册”冲突探测。</summary>
    internal static class HotkeyApi
    {
        public const int AppHotkeyId = 0x5A01;
        private const int ProbeHotkeyId = 0x5A02;

        public static bool TryRegister(IntPtr window, int id, HotkeySpec spec, out int error)
        {
            error = 0;
            if (Native.RegisterHotKey(window, id, spec.Modifiers | Native.MOD_NOREPEAT, spec.VirtualKey))
            {
                return true;
            }

            error = Marshal.GetLastWin32Error();
            return false;
        }

        /// <summary>
        /// 用临时 id 试注册，成功立刻注销。这样调用方的旧热键始终没被碰过，
        /// 探测失败时不会出现“新键没设上、旧键也没了”的空窗。
        /// </summary>
        public static bool Probe(IntPtr window, HotkeySpec spec, out int error)
        {
            if (TryRegister(window, ProbeHotkeyId, spec, out error))
            {
                Native.UnregisterHotKey(window, ProbeHotkeyId);
                return true;
            }

            return false;
        }

        public static void Unregister(IntPtr window, int id)
        {
            Native.UnregisterHotKey(window, id);
        }

        public static string DescribeError(int code)
        {
            if (code == 0)
            {
                return "成功";
            }

            if (code == Native.ERROR_HOTKEY_ALREADY_REGISTERED)
            {
                return "已被其他程序占用";
            }

            return "注册失败（Win32 " + code + "）";
        }
    }
}
