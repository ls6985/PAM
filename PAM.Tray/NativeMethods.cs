using System.Runtime.InteropServices;

namespace PAM.Tray;

/// <summary>
/// 激活主窗口所需的 Win32 API。
/// </summary>
internal static class NativeMethods
{
    /// <summary>SW_SHOW：按当前位置显示窗口。</summary>
    internal const int SwShow = 5;

    /// <summary>SW_RESTORE：若窗口最小化或最大化则还原到原始大小和位置。</summary>
    internal const int SwRestore = 9;

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
}
