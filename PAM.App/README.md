# PAM.App (WPF 主界面)

使用 WPF-UI (`FluentWindow`) 构建的图形界面，主要负责管理 `$PROFILE` 中的 `Set-Alias`。通过 MVVM 解耦，`MainViewModel` 承载业务状态与校验。

## 功能与快捷键

- 新建、编辑、删除别名；内联编辑条，`Enter` 确认，`Esc` 取消。
- 删除二次确认（点击变“确认删除？”，再点生效）。
- 搜索实时过滤；执行策略受限时警告并可一键修复。
- 路径浏览；文件不存在显示警告图标；长路径中间省略。
- 复制路径按钮：点击页头的复制图标，把 `$PROFILE` 路径写入剪贴板并在状态栏提示“路径已复制”。
- 保存时自动探测并写入 PowerShell 5.1 / 7 两个 `$PROFILE`。

## 多页签导航

顶部使用 WPF-UI 的 `TabView`（`TabItem` 样式在 `Styles.xaml` 的 `NavTabItemStyle`）切换三个页面：

- **别名管理**：搜索框、新建/保存、内联编辑条与别名列表（`MainWindow.xaml` 内的 `AliasTab`）。
- **自定义命令**：`Views/CustomCommandsView` 占位页（建设中）。
- **设置**：`Views/SettingsView` 占位页（建设中）。

状态栏在三个页签间共用；`Ctrl+N` / `Ctrl+S` 仅在“别名管理”页签生效。

## 加载状态

`MainViewModel.IsLoading` 初始为 `true`，`InitializeAsync()` 结束（`finally`）置回 `false`。
`EmptyState` 的可见性由 `EmptyStateVisibilityConverter`（`IsLoading` + `IsListEmpty`）驱动，
列表 `AliasList` 在加载中隐藏，因此启动时不会闪现“还没有别名”空状态文字。

快捷键：`Ctrl+N` 新建，`Ctrl+S` 保存，`Enter`/`Esc` 处理编辑。

## 核心分层

- `MainWindow.xaml.cs`：纯 UI 交互（焦点、状态栏图标映射、空状态文案）。不包含业务校验和读写逻辑。
- `MainViewModel.cs`：绑定属性、数据状态（`ObservableCollection`）、校验逻辑。调用 `PAM.Core` 获取和保存。
- `Converters/`：值转换器（`TextNotEmptyToVisibilityConverter` 等）。