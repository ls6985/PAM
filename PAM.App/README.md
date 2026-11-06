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
- **自定义命令**：`Views/CustomCommandsView`，管理与别名一致的列表 + 内联编辑条交互，数据源为 `MainViewModel.CustomCommands`（`CustomCommandsViewModel`）。
- **设置**：`Views/SettingsView` 占位页（建设中）。

状态栏在三个页签间共用，切换页签时会同步显示对应页签的统计；`Ctrl+N` / `Ctrl+S` 在“别名管理”与“自定义命令”页签各自生效（自定义命令页的确认键为 `Ctrl+Enter`，回车保留给代码换行）。

## 自定义命令页签

- 数据来自 `$PROFILE` 的 `#region PAM-Custom` 受控区块（格式见根 README），保存时只重写该区块。
- 列表列：名称（等宽字体）、状态胶囊（点击即启用/禁用）、代码预览（第一条有效代码行）、备注、操作（编辑 / 二次确认删除）。
- 内联编辑条：名称（编辑时锁定）、备注、“启用该命令”复选框、多行代码编辑框（等宽、自动换行关闭、纵向滚动）。
- 禁用的行整体半透明显示；搜索框按名称、代码、备注实时过滤。
- “保存并生效”写入受控区块后拉起新的 PowerShell 窗口验证（执行策略受限时仅提示，不拉起）。

## 加载状态

`MainViewModel.IsLoading` 初始为 `true`，`InitializeAsync()` 结束（`finally`）置回 `false`。
`EmptyState` 的可见性由 `EmptyStateVisibilityConverter`（`IsLoading` + `IsListEmpty`）驱动，
列表 `AliasList` 在加载中隐藏，因此启动时不会闪现“还没有别名”空状态文字。

快捷键：`Ctrl+N` 新建，`Ctrl+S` 保存，`Enter`/`Esc` 处理编辑。

## 核心分层

- `MainWindow.xaml.cs`：纯 UI 交互（焦点、状态栏图标映射、空状态文案）。不包含业务校验和读写逻辑。
- `MainViewModel.cs`：绑定属性、数据状态（`ObservableCollection`）、校验逻辑。调用 `PAM.Core` 获取和保存。
- `Converters/`：值转换器（`TextNotEmptyToVisibilityConverter` 等）。