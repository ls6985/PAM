namespace PAM.Tray;

/// <summary>
/// 托盘可执行文件的入口壳：只做 WinForms 初始化，实际生命周期交给 <see cref="TrayApplication" />。
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var app = new TrayApplication();
        app.Run();
    }
}
