using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PAM.Core;

namespace PAM.Tray;

/// <summary>
/// 渲染托盘图标：用系统自带的 Segoe Fluent Icons 字体画出与主界面标题栏一致的
/// <c>&lt;/&gt;</c> 代码符号（WPF-UI 的 SymbolRegular.Code24 = U+F2F0），不引入图标文件。
/// Windows 10 没有该字体时返回 null，调用方回退为系统默认图标。
/// </summary>
internal static class TrayIconRenderer
{
    private const char CodeGlyph = '\uF2F0';
    private const string GlyphFontName = "Segoe Fluent Icons";

    /// <summary>渲染一个托盘图标。<paramref name="lightForeground" /> 为 true 时画浅色字形（深色主题用）。</summary>
    public static Icon? Create(bool lightForeground)
    {
        // 超采样 4 倍再缩小，小尺寸下字形边缘更平滑。
        const int supersampling = 4;

        try
        {
            var size = SystemInformation.SmallIconSize;
            var side = Math.Max(size.Width, size.Height);
            var canvasSide = side * supersampling;

            using var font = new Font(GlyphFontName, canvasSide * 0.62f, GraphicsUnit.Pixel);

            using var canvas = new Bitmap(canvasSide, canvasSide);
            using (var graphics = Graphics.FromImage(canvas))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                var color = lightForeground
                    ? Color.FromArgb(238, 242, 246)
                    : Color.FromArgb(32, 32, 32);

                using var brush = new SolidBrush(color);
                using var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };

                // 字体字形按行高排布，垂直方向略微上移以补偿空隙，让符号视觉居中。
                var rectangle = new RectangleF(0, -canvasSide * 0.05f, canvasSide, canvasSide);
                graphics.DrawString(CodeGlyph.ToString(), font, brush, rectangle, format);
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
            // 缺少 Segoe Fluent Icons（如 Windows 10）或 GDI+ 异常时回退系统图标。
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
