# PAM.Tests (单元测试)

`dotnet test` 在项目根目录一键运行，全部通过才算改完。只测 PAM.Core 的纯逻辑，不碰 WPF 界面，也**不会读写真实的 `$PROFILE`、`%AppData%\PAM\settings.json` 和 `%AppData%\PAM\pam-data.json`**（全部在临时目录里跑）。

| 测试文件 | 锁住的行为 |
| --- | --- |
| `CustomCommandServiceTests.cs` | `BuildLines()` 片段生成：多行代码含缩进空行的启用 / 禁用还原、内置命令固定追加、数据里的保留名被跳过、内置代码不会被 `pls` 的别名扫描误判；`TryValidateName()` / `IsBuiltinName()` / `GetBuiltinEntries()` 名称校验与只读条目；`ProfileService.NormalizePath()` 引号处理 |
| `SettingsServiceTests.cs` | `settings.json` 的 camelCase 键名与缩进格式、全部属性读写映射、文件缺失或损坏（乱码、截断、非法 JSON）时回退默认值且不抛异常、手工改坏的枚举值被 `Normalize()` 修正 |
| `AutoStartServiceTests.cs` | 登录启动项 Run 值的构造 / 解析纯逻辑：带引号路径、含空格路径、带启动参数、无引号写法、未闭合引号，以及路径规范化后的大小写无关比较；不读写真实注册表 |
| `PamDataServiceTests.cs` | `pam-data.json` 的 camelCase 键名、中文不做 `\uXXXX` 转义、别名 / 自定义命令往返（含别名的 `arguments` 字段与缺省空串）、损坏 JSON 回退空数据、`Normalize()` 修正空名与缺失字段、`Clone()` 深拷贝、覆盖写入与临时文件清理、默认路径与参数校验 |
| `ProfileRendererTests.cs` | 上下分区边界、用户区逐行保留、旧生成块归拢（含 Command Hint 块）、Starship 块保留在托管区、渲染幂等、外部修改被覆盖、同名避让（不删用户 alias）、冲突名称报告、无别名时省略 Launcher、始终写入 Command Hint 块、旧版托管区（缺 Command Hint 块）被判定为需重建（`ManagedSectionHasCurrentMarkers` / `NeedsRebuild`）、`RenderToProfiles` 向多份 $PROFILE 写入一致托管区、BOM / CRLF / LF 保持、缺失文件创建、`Inspect()` 哈希 |
| `AliasLaunchServiceTests.cs` | `BuildLines()` 片段生成：公共函数与开关读取、explorer / `.lnk` / 兜底路径、`Get-Command` 同名避让、不再 `Remove-Item`、特殊字符转义与备注压平、固定启动参数的分词 / 引号 / 转义与留空回退、空列表、多别名顺序 |
| `CommandHintServiceTests.cs` | `BuildLines()` 片段生成：`CommandNotFoundAction` 注册、开关与语言运行时读取、`CommandScriptBlock` 接管、中英文提示、生成结果稳定；不读写文件 |
| `StarshipServiceTests.cs` | Starship init 区块幂等写入、`starship.toml` 备份与恢复、内置预设资源；应用主题时保留已有自定义提示符块 |
| `StarshipPromptServiceTests.cs` | 托管块写顶层 format 并接管引用到的模块段落、未引用段落 / 注释 / 编码保留、format 往返与首次备份恢复、变量表与预览解析 |
| `WindowsTerminalServiceTests.cs` | `settings.json` 的 JSONC 文本拼接：注释逐字保留、结构逐层补齐、属性设置 / 删除、嵌套同名项不误改、非法值不落盘；**多次写入不覆盖原始备份、恢复后备份保留可反复恢复**；外观 8 项批量写入与回读 |

维护规则：**功能代码没改，就不用动测试代码，也不用读它**——改完 `PAM.Core` 的业务逻辑跑一遍 `dotnet test` 确认没被改坏即可。只有当 `pam-data.json` 字段、托管区格式、编码保留规则、`settings.json` 字段或终端外观属性本身发生变化时，才需要同步调整对应断言。测试不追求覆盖率，只覆盖上表列出的核心行为与边界场景。
