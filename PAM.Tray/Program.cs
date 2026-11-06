using System.Windows.Forms;

namespace PAM.Tray;

/// <summary>
/// 托盘程序入口。当前为空壳项目，仅保留可运行的最小骨架，暂不实现任何逻辑。
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run();
    }
}
