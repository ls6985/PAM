# PAM.App (WPF 主界面)

使用 WPF-UI (`FluentWindow`) 构建的图形界面，主要负责管理 `$PROFILE` 中的 `Set-Alias`。通过 MVVM 解耦，`MainViewModel` 承载业务状态与校验。

## 功能与快捷键

- 新建、编辑、删除别名；内联编辑条，`Enter` 确认，`Esc` 取消。
- 删除二次确认（点击变“确认删除？”，再点生效）。
- 搜索实时过滤；执行策略受限时警告并可一键修复。
- 路径浏览；文件不存在显示警告图标；长路径中间省略。
- 保存时自动探测并写入 PowerShell 5.1 / 7 两个 `$PROFILE`。

快捷键：`Ctrl+N` 新建，`Ctrl+S` 保存，`Enter`/`Esc` 处理编辑。

## 核心分层

- `MainWindow.xaml.cs`：纯 UI 交互（焦点、状态栏图标映射、空状态文案）。不包含业务校验和读写逻辑。
- `MainViewModel.cs`：绑定属性、数据状态（`ObservableCollection`）、校验逻辑。调用 `PAM.Core` 获取和保存。
- `Converters/`：值转换器（`TextNotEmptyToVisibilityConverter` 等）。