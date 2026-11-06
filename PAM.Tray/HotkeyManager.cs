using System.Runtime.InteropServices;

namespace PAM.Tray;

/// <summary>
/// 全局热键管理器。通过 <see cref="NativeWindow"/> 创建一个隐藏的消息窗口，
/// 使用 Win32 <c>RegisterHotKey</c> 注册全局热键并拦截 <c>WM_HOTKEY</c> 消息。
/// </summary>
internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    /// <summary>WM_HOTKEY：全局热键被按下时系统发送的消息。</summary>
    private const int WmHotkey = 0x0312;

    /// <summary>热键 ID，取值范围必须在 0x0000 ~ 0xBFFF 之间。</summary>
    private const int HotkeyId = 0x1000;

    private bool _registered;
    private bool _disposed;

    /// <summary>全局热键被触发时触发。</summary>
    public event EventHandler? Pressed;

    /// <summary>
    /// 创建消息窗口句柄。必须在拥有消息循环的线程上构造本实例。
    /// </summary>
    public HotkeyManager()
    {
        CreateHandle(new CreateParams
        {
            Caption = "PAM.Tray.HotkeyWindow",
            Style = 0,
            ExStyle = 0,
        });
    }

    /// <summary>
    /// 注册全局热键。
    /// </summary>
    /// <returns>注册成功返回 <c>true</c>；热键已被其他进程占用或调用失败返回 <c>false</c>。</returns>
    public bool TryRegister(HotkeyModifiers modifiers, Keys key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_registered)
        {
            return true;
        }

        // HotkeyModifiers 的取值与 Win32 MOD_* 标志一致，可直接转换。
        uint fsModifiers = (uint)(modifiers & (HotkeyModifiers.Alt | HotkeyModifiers.Control |
                                              HotkeyModifiers.Shift | HotkeyModifiers.Win));
        uint vk = (uint)(key & Keys.KeyCode);

        _registered = RegisterHotKey(Handle, HotkeyId, fsModifiers, vk);
        return _registered;
    }

    /// <inheritdoc />
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Handle != IntPtr.Zero)
        {
            if (_registered)
            {
                UnregisterHotKey(Handle, HotkeyId);
                _registered = false;
            }

            DestroyHandle();
        }

        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

/// <summary>
/// 全局热键修饰键，取值与 Win32 <c>MOD_*</c> 标志一致。
/// </summary>
[Flags]
internal enum HotkeyModifiers : uint
{
    None = 0x0000,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}
