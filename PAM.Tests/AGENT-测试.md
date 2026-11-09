# PAM.Tests (单元测试)

`dotnet test` 在项目根目录一键运行，全部通过才算改完。只测 PAM.Core 的纯逻辑，不碰 WPF 界面，也**不会读写真实的 `$PROFILE` 和 `%AppData%\PAM\settings.json`**（全部在临时目录里跑）。

| 测试文件 | 锁住的行为 |
| --- | --- |
| `ProfileServiceTests.cs` | 受控区块只追加 / 整体替换 / 清空移除；区块与别名互不破坏；多行代码含缩进空行的启用禁用还原；单引号、双引号、无引号路径解析与写回转义（`` ` ``、`$`、`"`）；UTF-8 BOM、无 BOM 升级、ANSI 保留；CRLF 与 LF 各自的换行风格保留；配置文件缺失时自动建父目录；空文件、超长路径、特殊字符、非法路径参数；**三条内置命令固定写入、手工同名条目被覆盖、保留名校验、老配置补齐、内置代码不会被 `pls` 的别名扫描误判** |
| `SettingsServiceTests.cs` | `settings.json` 的 camelCase 键名与缩进格式、全部属性读写映射、文件缺失或损坏（乱码、截断、非法 JSON）时回退默认值且不抛异常、手工改坏的枚举值被 `Normalize()` 修正 |
| `AliasLaunchServiceTests.cs` | 别名启动器区块幂等写入、清空时整体移除、不破坏 PAM-Custom / `Set-Alias` / 编码 / 换行 / 转义 |
| `StarshipServiceTests.cs` | Starship init 区块幂等写入、`starship.toml` 备份与恢复、内置预设资源；应用主题时保留已有自定义提示符块 |
| `StarshipPromptServiceTests.cs` | 托管块写顶层 format 并接管引用到的模块段落、未引用段落 / 注释 / 编码保留、format 往返与首次备份恢复、变量表与预览解析 |
| `WindowsTerminalServiceTests.cs` | `settings.json` 的 JSONC 文本拼接：注释逐字保留、结构逐层补齐、属性设置 / 删除、嵌套同名项不误改、非法值不落盘；**多次写入不覆盖原始备份、恢复后备份保留可反复恢复**；外观 8 项批量写入与回读 |

维护规则：**功能代码没改，就不用动测试代码，也不用读它**——改完 `PAM.Core` 的业务逻辑跑一遍 `dotnet test` 确认没被改坏即可。只有当 `$PROFILE` 区块格式、编码保留规则、`settings.json` 字段或终端外观属性本身发生变化时，才需要同步调整对应断言。测试不追求覆盖率，只覆盖上表列出的核心行为与边界场景。
