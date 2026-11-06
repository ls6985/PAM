using PAM.Core;

namespace PAM.App;

/// <summary>本地化访问的短别名，避免在视图代码里反复写完整类型名。</summary>
public static class Loc
{
    public static LocalizationService Service => LocalizationService.Instance;

    public static string T(string? source) => LocalizationService.Instance.T(source);

    public static string F(string format, params object[] args) => LocalizationService.Instance.F(format, args);
}
