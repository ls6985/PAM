using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Win32;
using PAM.Core;

namespace PAM.Tray;

/// <summary>
/// 渲染托盘图标：用 GDI+ 直接画出与主界面标题栏一致的 <c>&lt;/&gt;</c> 代码符号，不依赖任何图标字体。
/// 旧实现拿系统的 Segoe Fluent Icons 去画 WPF-UI 的 U+F2F0 码位，但那是两套码位互不兼容的图标字体，
/// Segoe Fluent Icons 里根本没有 U+F2F0；GDI+ 不抛异常而是静默画出 .notdef（空白方框），
/// 托盘上就只剩一个小方块，代码里"字体缺失时回退"的 catch 也永远不会触发。
/// </summary>
internal static class TrayIconRenderer
{
    /// <summary>渲染一个托盘图标。<paramref name="lightForeground" /> 为 true 时画浅色线条（深色主题用）。</summary>
    public static Icon? Create(bool lightForeground)
    {
        // 超采样 4 倍再缩小，小尺寸下线条边缘更平滑。
        const int supersampling = 4;

        try
        {
            var size = SystemInformation.SmallIconSize;
            var side = Math.Max(size.Width, size.Height);
            var canvasSide = side * supersampling;
            var u = (float)canvasSide;

            using var canvas = new Bitmap(canvasSide, canvasSide);
            using (var graphics = Graphics.FromImage(canvas))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                var color = lightForeground
                    ? Color.FromArgb(238, 242, 246)
                    : Color.FromArgb(32, 32, 32);

                // 圆头圆角折线：16x16 缩略图下比字形更清晰，也不受系统字体版本影响。
                using var pen = new Pen(color, u * 0.09f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round,
                };

                // "<"
                graphics.DrawLines(pen, new[]
                {
                    new PointF(u * 0.34f, u * 0.22f),
                    new PointF(u * 0.13f, u * 0.50f),
                    new PointF(u * 0.34f, u * 0.78f),
                });
                // "/"
                graphics.DrawLine(pen, u * 0.41f, u * 0.82f, u * 0.59f, u * 0.18f);
                // ">"
                graphics.DrawLines(pen, new[]
                {
                    new PointF(u * 0.66f, u * 0.22f),
                    new PointF(u * 0.87f, u * 0.50f),
                    new PointF(u * 0.66f, u * 0.78f),
                });
            }

            using var scaled = new Bitmap(side, side);
            using (var graphics = Graphics.FromImage(scaled))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(canvas, new Rectangle(0, 0, side, side));
            }

            var handle = scaled.GetHicon();
            try
            {
                // Icon.FromHandle 不拥有句柄，Clone 出独立副本后再销毁原始句柄。
                return Icon.FromHandle(handle).Clone() as Icon;
            }
            finally
            {
                NativeMethods.DestroyIcon(handle);
            }
        }
        catch
        {
            // GDI+ / HICON 异常时回退系统图标；正常路径不再依赖任何字体。
            return null;
        }
    }

    /// <summary>按主题设置决定字形颜色：深色主题用浅色字形；跟随系统时读系统的应用主题。</summary>
    public static bool ShouldUseLightGlyph(string? theme)
    {
        return theme switch
        {
            AppSettings.ThemeDark => true,
            AppSettings.ThemeLight => false,
            _ => !IsSystemUsingLightAppsTheme(),
        };
    }

    private static bool IsSystemUsingLightAppsTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not 0;
        }
        catch
        {
            return true;
        }
    }
}
