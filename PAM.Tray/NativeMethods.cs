using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PAM.Tray;

/// <summary>
/// 托盘用到的 Win32 API：Windows Terminal 窗口的创建、枚举、显示与跨进程标记。
/// </summary>
internal static class NativeMethods
{
    /// <summary>SW_HIDE：进程启动时窗口即隐藏，窗口句柄仍然存在。</summary>
    internal const int SwHide = 0;

    /// <summary>SW_SHOW：按当前位置显示窗口。</summary>
    internal const int SwShow = 5;

    /// <summary>SW_RESTORE：若窗口最小化或最大化则还原到原始大小和位置。</summary>
    internal const int SwRestore = 9;

    /// <summary>STARTF_USESHOWWINDOW：让 CreateProcess 采用 STARTUPINFO.wShowWindow。</summary>
    internal const int StartfUseShowWindow = 0x00000001;

    /// <summary>WM_CLOSE：请求关闭窗口。</summary>
    internal const int WmClose = 0x0010;

    /// <summary>Windows Terminal 宿主窗口类名。</summary>
    internal const string CascadiaHostingClass = "CASCADIA_HOSTING_WINDOW_CLASS";

    /// <summary>CreateProcess 的窗口与标准句柄配置，字段顺序与原生 STARTUPINFOW 一致。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct StartupInfo
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    /// <summary>CreateProcess 返回的进程与线程标识。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);

    /// <summary>销毁 GDI 图标句柄（TrayIconRenderer 用完 Icon.FromHandle 的句柄后调用）。</summary>
    [DllImport("user32.dll")]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProp(IntPtr hWnd, string name, IntPtr value);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetProp(IntPtr hWnd, string name);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateProcess(
        string? applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        int creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);

    /// <summary>枚举当前所有 Windows Terminal 宿主窗口。</summary>
    internal static List<IntPtr> FindTerminalWindows()
    {
        var result = new List<IntPtr>();
        EnumWindows((hWnd, _) =>
        {
            if (IsWindow(hWnd)
                && string.Equals(ReadClassName(hWnd), CascadiaHostingClass, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(hWnd);
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>按跨进程窗口属性找回托盘上一次持有的终端窗口。</summary>
    internal static IntPtr FindMarkedWindow(string propertyName)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindow(hWnd)) return true;
            if (!string.Equals(ReadClassName(hWnd), CascadiaHostingClass, StringComparison.OrdinalIgnoreCase)) return true;
            if (GetProp(hWnd, propertyName) == IntPtr.Zero) return true;

            found = hWnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>给窗口打上跨进程标记；标记不随标签切换或标题变化而丢失。</summary>
    internal static void MarkWindow(IntPtr hWnd, string propertyName)
        => SetProp(hWnd, propertyName, (IntPtr)1);

    private static string ReadClassName(IntPtr hWnd)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(hWnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : string.Empty;
    }
}
