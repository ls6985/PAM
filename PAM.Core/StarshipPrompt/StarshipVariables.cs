using System.Collections.Generic;
using System.Linq;

namespace PAM.Core;

/// <summary>
/// 美化页「插入变量」下拉用的一项。变量本身由 Starship 在渲染时读取系统状态，
/// 用户不需要（也不应该）手工填写用户名、主机名、当前路径这类动态值。
/// </summary>
public sealed record StarshipVariableDefinition(
    string Variable,
    string NameZh,
    string NameEn,
    string GroupZh,
    string GroupEn,
    string Sample);

/// <summary>
/// 精选 20 个最常用的 Starship 模块变量，全部来自 <c>starship module --list</c>。
/// 名称用中文，按用途分四组；<c>Sample</c> 只用于界面预览，模拟应用后的实际渲染效果：
/// 托管块重写模块段落时按 <c>format = "[$user](fg:…)"</c> 这类裸值渲染，
/// 所以示例值不带装饰（如 hostname 自带 @、directory 是路径本身、character 是 $）。
/// 完整清单有 100+ 个，其余变量仍可在单行 format 编辑器里手写，不受限制。
/// </summary>
public static class StarshipVariables
{
    public static IReadOnlyList<StarshipVariableDefinition> All { get; } =
    [
        // 身份与位置
        new("$username",     "用户名",       "Username",          "身份与位置", "Identity",  System.Environment.UserName),
        new("$hostname",     "主机名",       "Hostname",          "身份与位置", "Identity",  "@" + System.Environment.MachineName),
        new("$directory",    "当前路径",     "Current directory", "身份与位置", "Identity",  "~"),
        new("$character",    "结束符",       "Prompt character",  "身份与位置", "Identity",  "$"),
        new("$shell",        "当前终端",     "Shell",             "身份与位置", "Identity",  "pwsh"),

        // Git
        new("$git_branch",   "Git 分支",     "Git branch",        "Git",       "Git",       "main"),
        new("$git_status",   "Git 状态",     "Git status",        "Git",       "Git",       "!?"),
        new("$git_commit",   "Git 提交",     "Git commit",        "Git",       "Git",       "a1b2c3d"),
        new("$git_state",    "Git 操作",     "Git state",         "Git",       "Git",       "merge"),
        new("$git_metrics",  "Git 增删行",   "Git metrics",       "Git",       "Git",       "+1 ~2"),

        // 运行状态
        new("$cmd_duration", "命令耗时",     "Command duration",  "运行状态",  "Runtime",  "2s"),
        new("$status",       "退出状态",     "Exit status",       "运行状态",  "Runtime",  "✗"),
        new("$time",         "当前时间",     "Time",              "运行状态",  "Runtime",  "19:25"),
        new("$jobs",         "后台任务",     "Background jobs",   "运行状态",  "Runtime",  "1"),
        new("$battery",      "电池电量",     "Battery",           "运行状态",  "Runtime",  "85%"),

        // 开发环境
        new("$python",       "Python 版本",  "Python version",    "开发环境",  "Toolchain", "3.12"),
        new("$nodejs",       "Node.js 版本", "Node.js version",   "开发环境",  "Toolchain", "v20"),
        new("$rust",         "Rust 版本",    "Rust version",      "开发环境",  "Toolchain", "v1.80"),
        new("$golang",       "Go 版本",      "Go version",        "开发环境",  "Toolchain", "v1.22"),
        new("$package",      "包版本",       "Package version",   "开发环境",  "Toolchain", "v1.0.0"),
    ];

    /// <summary>按变量名取预览示例值；清单外的变量保留原样（用户可手写任意 Starship 变量）。</summary>
    public static string SampleOf(string variable)
        => All.FirstOrDefault(item => string.Equals(item.Variable, variable, System.StringComparison.OrdinalIgnoreCase))?.Sample
           ?? variable;

    /// <summary>判断一段文本是不是纯变量引用（如 <c>$username</c>）。</summary>
    public static bool IsVariable(string text)
        => text.Length > 1 && text[0] == '$' && text.Skip(1).All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
