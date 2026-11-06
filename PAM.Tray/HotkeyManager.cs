using System.Runtime.InteropServices;

namespace PAM.Tray;

/// <summary>
/// 全局热键管理器。通过 <see cref="NativeWindow"/> 创建一个隐藏的消息窗口，
/// 使用 Win32 <c>RegisterHotKey</c> 注册全局热键并拦截 <c>WM_HOTKEY</c> 消息。
/// 支持重复调用 <see cref="TryRegister"/>：设置变更后先注销旧组合再注册新组合。
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

    /// <summary>当前是否成功注册了全局热键。</summary>
    public bool IsRegistered => _registered;

    /// <summary>
    /// 注册（或改注册）全局热键。参数直接来自 <c>PAM.Core.HotkeyChord</c>。
    /// </summary>
    /// <returns>注册成功返回 <c>true</c>；热键已被其他进程占用或调用失败返回 <c>false</c>。</returns>
    public bool TryRegister(uint fsModifiers, uint virtualKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Unregister();
        _registered = RegisterHotKey(Handle, HotkeyId, fsModifiers, virtualKey);
        return _registered;
    }

    /// <summary>注销当前热键，释放组合以便重新注册。</summary>
    public void Unregister()
    {
        if (!_registered || Handle == IntPtr.Zero) return;

        UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
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
        if (_disposed) return;
        _disposed = true;

        if (Handle != IntPtr.Zero)
        {
            Unregister();
            DestroyHandle();
        }

        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
