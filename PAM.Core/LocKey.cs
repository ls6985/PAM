namespace PAM.Core;

/// <summary>显式本地化资源键；每个成员对应语言表中的一条 {zh, en} 词条。</summary>
public enum LocKey
{
    /// <summary>PowerShell 管理器</summary>
    PowerShellManager,
    /// <summary>自定义命令</summary>
    CustomCommands,
    /// <summary>设置</summary>
    Settings,
    /// <summary>取消</summary>
    Cancel,
    /// <summary>确定</summary>
    OK,
    /// <summary>编辑</summary>
    Edit,
    /// <summary>删除</summary>
    Delete,
    /// <summary>退出</summary>
    Exit,
    /// <summary>状态</summary>
    Status,
    /// <summary>名称</summary>
    Name,
    /// <summary>路径</summary>
    Path,
    /// <summary>备注</summary>
    Note,
    /// <summary>操作</summary>
    Actions,
    /// <summary>别名</summary>
    Alias,
    /// <summary>浏览…</summary>
    Browse,
    /// <summary>保存并生效</summary>
    SaveApply,
    /// <summary>新建别名</summary>
    NewAlias,
    /// <summary>新建命令</summary>
    NewCommand,
    /// <summary>确认删除？</summary>
    ConfirmDelete,
    /// <summary>文件不存在</summary>
    FileNotFound,
    /// <summary>删除（再次点击确认）</summary>
    DeleteClickAgainToConfirm,
    /// <summary>切换启用状态</summary>
    ToggleEnabledState,
    /// <summary>点击启用 / 禁用</summary>
    ClickToEnableDisable,
    /// <summary>已启用</summary>
    Enabled,
    /// <summary>已禁用</summary>
    Disabled,
    /// <summary>显示主界面</summary>
    ShowMainWindow,
    /// <summary>运行中</summary>
    Running,
    /// <summary>未运行</summary>
    NotRunning,
    /// <summary>在 $PROFILE 中集中管理 Set-Alias 快捷启动项，保存后立即在新窗口生效。</summary>
    ManageSetAliasShortcutsInPROFILETheyTakeEffectInNewWindowsRightAfterSaving,
    /// <summary>复制 $PROFILE 路径</summary>
    CopyPROFILEPath,
    /// <summary>搜索别名、路径或备注</summary>
    SearchAliasPathOrNote,
    /// <summary>别名（必填）</summary>
    AliasRequired,
    /// <summary>应用程序路径（必填）</summary>
    ApplicationPathRequired,
    /// <summary>备注（可选）</summary>
    NoteOptional,
    /// <summary>例如 blbl</summary>
    EGBlbl,
    /// <summary>例如：打开哔哩哔哩</summary>
    EGOpenBilibili,
    /// <summary>高级选项：启动参数（可选）</summary>
    AdvancedLaunchArgumentsOptional,
    /// <summary>启动参数</summary>
    LaunchArguments,
    /// <summary>例如：--incognito</summary>
    EGIncognito,
    /// <summary>每次启动该程序都会带上这些参数；在终端里临时传入的参数排在后面。含空格的参数请用英文双引号包起来。</summary>
    TheseArgumentsAreAddedEveryTimeTheAppStartsArgumentsPassedInTheTerminalComeAfterThemWrapValuesContainingSpacesInDoubleQuotes,
    /// <summary>Enter 确定 · Esc 取消</summary>
    EnterToConfirmEscToCancel,
    /// <summary>别名不能为空。</summary>
    AliasCannotBeEmpty,
    /// <summary>别名不能包含空格。</summary>
    AliasCannotContainSpaces,
    /// <summary>该别名已存在，请换一个。</summary>
    ThisAliasAlreadyExistsChooseAnother,
    /// <summary>请选择或填写应用程序路径。</summary>
    SelectOrEnterAnApplicationPath,
    /// <summary>⚠ 文件不存在，仍可保存。</summary>
    FileNotFoundButYouCanStillSave,
    /// <summary>选择要启动的应用程序</summary>
    SelectAnApplicationToLaunch,
    /// <summary>应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*</summary>
    ApplicationsExeExeAllFiles,
    /// <summary>还没有别名</summary>
    NoAliasesYet,
    /// <summary>点击右上角“新建别名”，为常用应用添加一个 PowerShell 快捷命令。</summary>
    ClickNewAliasInTheTopRightToAddAPowerShellShortcutForAnApp,
    /// <summary>没有匹配的别名</summary>
    NoMatchingAliases,
    /// <summary>换个关键字试试，或清空搜索框查看全部别名。</summary>
    TryAnotherKeywordOrClearTheSearchBoxToSeeAllAliases,
    /// <summary>路径已复制</summary>
    PathCopied,
    /// <summary>尚未读取到 $PROFILE 路径，无法复制。</summary>
    PROFILEPathIsNotAvailableYetCannotCopy,
    /// <summary>执行策略受限</summary>
    ExecutionPolicyRestricted,
    /// <summary>尝试修复</summary>
    TryToFix,
    /// <summary>把常用命令收进 $PROFILE 的受控区块：命令名就是终端里的调用入口，保存时只改写该区块，其余配置原样保留。</summary>
    KeepFrequentlyUsedFunctionsAndSnippetsInAManagedPROFILEBlockOnlyThatBlockIsRewritten,
    /// <summary>保存时只重写该区块，区块外的 $PROFILE 内容不会被修改。</summary>
    OnlyThisBlockIsRewrittenTheRestOfPROFILEIsPreserved,
    /// <summary>搜索名称、代码或备注</summary>
    SearchNameCodeOrNote,
    /// <summary>Ctrl+Enter 确定 · Esc 取消</summary>
    CtrlEnterToConfirmEscToCancel,
    /// <summary>命令名称（必填）</summary>
    CommandNameRequired,
    /// <summary>例如 ll 或 Get-Project</summary>
    EGLlOrGetProject,
    /// <summary>例如：列出当前目录并按时间排序</summary>
    EGListTheCurrentDirectoryByTime,
    /// <summary>启用该命令</summary>
    EnableThisCommand,
    /// <summary>命令代码（必填）</summary>
    CommandCodeRequired,
    /// <summary>代码会被原样写入受控区块；禁用的命令保存为注释行，重新启用后完整还原。</summary>
    CodeIsWrittenVerbatimIntoTheManagedBlockDisabledCommandsAreSavedAsCommentsAndRestoredWhenReEnabled,
    /// <summary>命令名称不能为空。</summary>
    CommandNameCannotBeEmpty,
    /// <summary>命令名称不能包含空格。</summary>
    CommandNameCannotContainSpaces,
    /// <summary>命令名称包含 PowerShell 不支持的字符。</summary>
    CommandNameContainsCharactersNotSupportedByPowerShell,
    /// <summary>该名称已存在，请换一个。</summary>
    ThisNameAlreadyExistsChooseAnother,
    /// <summary>{0} 是内置命令，请换一个名称。</summary>
    K0IsReservedForABuiltInCommandChooseAnother,
    /// <summary>该名称是内置命令，请换一个。</summary>
    ThisNameIsABuiltInCommandChooseAnother,
    /// <summary>内置</summary>
    BuiltIn,
    /// <summary>内置命令，随保存自动维护，不可编辑或删除</summary>
    BuiltInCommandMaintainedAutomaticallyOnSaveCannotBeEditedOrDeleted,
    /// <summary>命令代码不能为空。</summary>
    CommandCodeCannotBeEmpty,
    /// <summary>还没有自定义命令</summary>
    NoCustomCommandsYet,
    /// <summary>点击右上角“新建命令”，把常用函数或脚本片段写进 $PROFILE 的受控区块。</summary>
    ClickNewCommandInTheTopRightToAddAFunctionOrSnippetToTheManagedPROFILEBlock,
    /// <summary>没有匹配的命令</summary>
    NoMatchingCommands,
    /// <summary>换个关键字试试，或清空搜索框查看全部命令。</summary>
    TryAnotherKeywordOrClearTheSearchBoxToSeeAllCommands,
    /// <summary>正在启动…</summary>
    Starting,
    /// <summary>正在读取 $PROFILE…</summary>
    ReadingPROFILE,
    /// <summary>无法读取 $PROFILE 路径</summary>
    UnableToReadPROFILEPath,
    /// <summary>已保存 · 共 {0} 个别名</summary>
    Saved0Aliases,
    /// <summary>已保存 · 共 {0} 个自定义命令</summary>
    Saved0CustomCommands,
    /// <summary>有未保存更改 · 共 {0} 个别名</summary>
    UnsavedChanges0Aliases,
    /// <summary>有未保存更改 · 共 {0} 个自定义命令</summary>
    UnsavedChanges0CustomCommands,
    /// <summary>配置文件不存在，点击“保存并生效”时将自动创建。</summary>
    TheProfileDoesNotExistItWillBeCreatedWhenYouClickSaveApply,
    /// <summary>错误：读取 $PROFILE 失败：{0}</summary>
    ErrorFailedToReadPROFILE0,
    /// <summary>错误：无法定位 $PROFILE：{0}</summary>
    ErrorCannotLocatePROFILE0,
    /// <summary>正在写入 $PROFILE…</summary>
    WritingPROFILE,
    /// <summary>已按 pam-data.json 重建 $PROFILE 托管区。</summary>
    RebuiltTheManagedSectionOfPROFILEFromPamDataJson,
    /// <summary>{0} 与 $PROFILE 中已有命令同名，这些别名不会生效。</summary>
    K0ConflictsWithAnExistingCommandInPROFILETheseAliasesWillNotTakeEffect,
    /// <summary>错误：写入失败：{0}</summary>
    ErrorFailedToWrite0,
    /// <summary>错误：检查执行策略失败：{0}</summary>
    ErrorFailedToCheckExecutionPolicy0,
    /// <summary>执行策略为 {0}，$PROFILE 可能无法加载。</summary>
    ExecutionPolicyIs0PROFILEMayNotLoad,
    /// <summary>正在将执行策略设为 RemoteSigned (CurrentUser)…</summary>
    SettingExecutionPolicyToRemoteSignedCurrentUser,
    /// <summary>错误：修复执行策略失败：{0}</summary>
    ErrorFailedToFixExecutionPolicy0,
    /// <summary>执行策略仍为 {0}，请检查系统策略。</summary>
    ExecutionPolicyIsStill0CheckTheSystemPolicy,
    /// <summary>执行策略仍受限，请手动检查系统策略。</summary>
    ExecutionPolicyIsStillRestrictedCheckItManually,
    /// <summary>执行策略已设为 RemoteSigned (CurrentUser)。再次保存即可在新窗口生效。</summary>
    ExecutionPolicySetToRemoteSignedCurrentUserSaveAgainToApplyInANewWindow,
    /// <summary>已写入 · 执行策略受限，未启动新的 PowerShell 窗口。请点击“尝试修复”。</summary>
    SavedExecutionPolicyIsRestrictedNoNewPowerShellWindowWasOpenedClickTryToFix,
    /// <summary>已写入 · 已启动新的 PowerShell 窗口 · 共 {0} 个别名</summary>
    SavedOpenedANewPowerShellWindow0Aliases,
    /// <summary>错误：已写入，但启动 PowerShell 失败：{0}</summary>
    ErrorSavedButFailedToLaunchPowerShell0,
    /// <summary>错误：复制路径失败：{0}</summary>
    ErrorFailedToCopyPath0,
    /// <summary>编辑别名 · {0}</summary>
    EditAlias0,
    /// <summary>编辑命令 · {0}</summary>
    EditCommand0,
    /// <summary>错误：读取自定义命令失败：{0}</summary>
    ErrorFailedToReadCustomCommands0,
    /// <summary>正在写入自定义命令…</summary>
    WritingCustomCommands,
    /// <summary>已创建 $PROFILE 并写入</summary>
    CreatedPROFILEAndSaved,
    /// <summary>已写入</summary>
    Saved,
    /// <summary>已写入 · 共 {0} 个自定义命令 · 执行策略受限，未启动新的 PowerShell 窗口。</summary>
    Saved0CustomCommandsExecutionPolicyRestrictedNoNewWindowWasOpened,
    /// <summary>已创建 $PROFILE 并写入 · 共 {0} 个自定义命令 · 执行策略受限，未启动新的 PowerShell 窗口。</summary>
    CreatedPROFILEAndSaved0CustomCommandsExecutionPolicyRestrictedNoNewWindowWasOpened,
    /// <summary>已写入 · 已启动新的 PowerShell 窗口 · 共 {0} 个自定义命令</summary>
    SavedOpenedANewPowerShellWindow0CustomCommands,
    /// <summary>已创建 $PROFILE 并写入 · 已启动新的 PowerShell 窗口 · 共 {0} 个自定义命令</summary>
    CreatedPROFILEAndSavedOpenedANewPowerShellWindow0CustomCommands,
    /// <summary>错误：写入自定义命令失败：{0}</summary>
    ErrorFailedToWriteCustomCommands0,
    /// <summary>调整外观、终端与全局热键。修改会立即保存，并同步到后台托盘。</summary>
    AdjustAppearanceTerminalAndGlobalHotkeyChangesAreSavedImmediatelyAndSyncedToTheTray,
    /// <summary>外观</summary>
    Appearance,
    /// <summary>主题</summary>
    Theme,
    /// <summary>PAM 界面的明暗风格。</summary>
    LightDarkAppearanceOfThePAMInterface,
    /// <summary>语言</summary>
    Language,
    /// <summary>界面显示语言，切换后立即生效。</summary>
    InterfaceLanguageAppliesImmediately,
    /// <summary>跟随系统</summary>
    FollowSystem,
    /// <summary>浅色</summary>
    Light,
    /// <summary>深色</summary>
    Dark,
    /// <summary>简体中文</summary>
    X,
    /// <summary>English</summary>
    English,
    /// <summary>自动探测（优先 PowerShell 7）</summary>
    AutoDetectPreferPowerShell7,
    /// <summary>PowerShell 7 (pwsh.exe)</summary>
    PowerShell7PwshExe,
    /// <summary>Windows PowerShell 5.1 (powershell.exe)</summary>
    WindowsPowerShell51PowershellExe,
    /// <summary>终端</summary>
    Terminal,
    /// <summary>默认终端</summary>
    DefaultTerminal,
    /// <summary>用于全局热键唤醒的终端、保存后启动的窗口，以及下方“打开配置文件”。</summary>
    UsedByTheGlobalHotkeyByTheWindowOpenedAfterSavingAndByOpenProfileFileBelow,
    /// <summary>未检测到 PowerShell 7，已回退到 Windows PowerShell 5.1。</summary>
    PowerShell7WasNotFoundFallingBackToWindowsPowerShell51,
    /// <summary>打开配置文件</summary>
    OpenProfileFile,
    /// <summary>按上方选择的默认终端打开对应的 $PROFILE；文件不存在时会自动创建。</summary>
    OpensTheMatchingPROFILEForTheSelectedTerminalTheFileIsCreatedIfMissing,
    /// <summary>尚未解析到 $PROFILE 路径</summary>
    PROFILEPathNotResolvedYet,
    /// <summary>全局热键</summary>
    GlobalHotkey,
    /// <summary>打开终端</summary>
    OpenTerminal,
    /// <summary>当前热键</summary>
    CurrentHotkey,
    /// <summary>更改…</summary>
    Change,
    /// <summary>恢复默认</summary>
    RestoreDefault,
    /// <summary>点击“更改”后按下组合键；Esc 取消录制。</summary>
    ClickChangeThenPressAKeyCombinationEscCancels,
    /// <summary>请按下组合键…</summary>
    PressAKeyCombination,
    /// <summary>至少包含一个修饰键（Ctrl / Alt / Shift / Win）。</summary>
    IncludeAtLeastOneModifierCtrlAltShiftWin,
    /// <summary>检测冲突</summary>
    CheckConflicts,
    /// <summary>应用热键</summary>
    ApplyHotkey,
    /// <summary>可用：该组合当前没有被系统或其他软件占用。</summary>
    AvailableNotCurrentlyUsedByWindowsOrOtherApps,
    /// <summary>系统保留：{0}</summary>
    ReservedByWindows0,
    /// <summary>已被占用：{0}</summary>
    AlreadyInUse0,
    /// <summary>当前由 PAM 托盘占用，可继续使用。</summary>
    CurrentlyHeldByThePAMTraySafeToKeep,
    /// <summary>常见软件快捷键提醒：{0}</summary>
    CommonShortcutWarning0,
    /// <summary>尚未检测，点击下方“检测冲突”。</summary>
    NotCheckedYetClickCheckConflictsBelow,
    /// <summary>终端输入</summary>
    TerminalInput,
    /// <summary>打开终端时把中文输入法切到英文模式</summary>
    SwitchTheChineseIMEToEnglishModeWhenATerminalOpens,
    /// <summary>后台托盘检测到新打开的 PowerShell / Windows Terminal 窗口时，先读任务栏输入指示器；只有处于中文模式才模拟一次 Shift 切到英文输入状态。输入法本身不变，也不会换成英文键盘；已是英文模式时一个键都不按。每个窗口只处理一次。</summary>
    WhenANewPowerShellWindowsTerminalWindowOpensTheTrayReadsTheTaskbarInputIndicatorAndTapsShiftOnceOnlyIfTheIMEIsInChineseModeTheInputMethodItselfAndTheKeyboardLayoutStayUnchangedNothingIsPressedWhenItIsAlreadyInEnglishModeEachWindowIsHandledOnce,
    /// <summary>别名启动</summary>
    AliasLaunching,
    /// <summary>独立进程启动（静默日志）</summary>
    LaunchAsASeparateProcessSilenceOutput,
    /// <summary>用别名启动软件时改为独立进程，并把子进程的 stdout/stderr 重定向到 NUL 设备：终端不再刷程序日志，关闭 PowerShell 也不会把软件一起关掉。关闭则恢复原来的直接启动方式。$PROFILE 只在终端启动时加载一次，保存别名后要新开终端窗口才生效。</summary>
    LaunchAliasesInASeparateProcessAndRedirectItsStdoutStderrToTheNULDeviceNoProgramOutputInTheTerminalAndClosingPowerShellWillNotCloseTheAppTurnOffToRestoreTheOriginalDirectLaunchPROFILEIsLoadedOnlyWhenATerminalStartsSoOpenANewTerminalWindowAfterSavingAliases,
    /// <summary>启动时显示别名备注</summary>
    ShowAliasRemarkOnLaunch,
    /// <summary>启动前在终端打印一行该别名的备注；没有备注时不打印。</summary>
    PrintTheAliasRemarkInTheTerminalBeforeLaunchingNothingIsPrintedWhenTheRemarkIsEmpty,
    /// <summary>未找到命令时显示友好提示</summary>
    FriendlyHintForUnknownCommands,
    /// <summary>终端里输入不存在的命令时，用一行简短提示代替 PowerShell 的红色报错，并引导到 pls 或管理器。关闭后恢复默认报错；$PROFILE 只在终端启动时加载，保存后要新开终端窗口才生效。</summary>
    WhenACommandIsNotFoundReplacePowerShellSRedErrorWithOneShortHintPointingToPlsOrTheManagerTurnOffToRestoreTheDefaultErrorPROFILEIsLoadedOnlyWhenATerminalStartsSoOpenANewTerminalWindowAfterSaving,
    /// <summary>后台运行</summary>
    Background,
    /// <summary>开机自动启动后台托盘</summary>
    StartTheTrayAutomaticallyAtSignIn,
    /// <summary>登录 Windows 后自动启动托盘并注册全局热键，无需先打开管理器。移动程序目录后打开一次管理器即可更新启动项路径。</summary>
    StartTheTrayAndRegisterTheGlobalHotkeyWhenYouSignInToWindowsWithoutOpeningTheManagerFirstIfYouMoveTheProgramFolderOpenTheManagerOnceToRefreshTheStartupEntryPath,
    /// <summary>关闭管理器时保留后台托盘</summary>
    KeepTheTrayRunningWhenTheManagerCloses,
    /// <summary>关闭后台托盘后，全局热键立即失效（无法用快捷键唤起终端），托盘菜单也会消失；下次打开管理器或点“立即启动”才会恢复。</summary>
    TurningOffTheBackgroundTrayImmediatelyDisablesTheGlobalHotkeyYouCanNoLongerOpenATerminalWithItAndRemovesTheTrayMenuItComesBackOnlyWhenYouReopenTheManagerOrClickStartNow,
    /// <summary>后台托盘状态</summary>
    TrayStatus,
    /// <summary>立即启动</summary>
    StartNow,
    /// <summary>结束后台运行</summary>
    StopBackground,
    /// <summary>设置文件</summary>
    SettingsFile,
    /// <summary>恢复默认设置</summary>
    RestoreDefaults,
    /// <summary>所有更改都会立即写入 settings.json。</summary>
    AllChangesAreWrittenToSettingsJsonImmediately,
    /// <summary>切换默认终端后已重新读取 $PROFILE，未保存的更改已丢弃。</summary>
    TheNewDefaultTerminalSPROFILEHasBeenLoadedUnsavedChangesWereDiscarded,
    /// <summary>设置已保存并同步到后台托盘。</summary>
    SettingsSavedAndSyncedToTheTray,
    /// <summary>设置已保存，但未找到后台托盘程序，热键将在下次启动托盘后生效。</summary>
    SettingsSavedTheTrayExecutableWasNotFoundSoTheHotkeyWillTakeEffectWhenItIsStarted,
    /// <summary>保存设置失败：{0}</summary>
    FailedToSaveSettings0,
    /// <summary>打开配置文件失败：{0}</summary>
    FailedToOpenTheProfileFile0,
    /// <summary>已创建并打开配置文件：{0}</summary>
    CreatedAndOpenedTheProfileFile0,
    /// <summary>已打开配置文件：{0}</summary>
    OpenedTheProfileFile0,
    /// <summary>热键已应用并同步到后台托盘。</summary>
    HotkeyAppliedAndSyncedToTheTray,
    /// <summary>热键冲突，未应用。</summary>
    HotkeyConflictsNotApplied,
    /// <summary>热键冲突</summary>
    HotkeyConflict,
    /// <summary>仍然使用这个热键吗？</summary>
    UseThisHotkeyAnyway,
    /// <summary>确定要恢复全部默认设置吗？</summary>
    RestoreAllSettingsToTheirDefaults,
    /// <summary>已恢复默认设置。</summary>
    DefaultSettingsRestored,
    /// <summary>后台托盘已启动。</summary>
    TheBackgroundTrayHasBeenStarted,
    /// <summary>已开启开机自动启动：登录 Windows 后会自动运行后台托盘。</summary>
    StartupEnabledTheTrayWillRunAutomaticallyWhenYouSignInToWindows,
    /// <summary>已关闭开机自动启动：下次登录不会自动运行后台托盘。</summary>
    StartupDisabledTheTrayWillNotRunAutomaticallyAtYourNextSignIn,
    /// <summary>设置已保存，但开机启动项未能更新；请检查是否被安全软件或系统策略拦截。</summary>
    SettingsSavedButTheStartupEntryCouldNotBeUpdatedCheckWhetherSecuritySoftwareOrASystemPolicyBlockedIt,
    /// <summary>无法启动后台托盘，请检查 PAM.Tray.exe 是否与管理程序在同一目录。</summary>
    CouldNotStartTheTrayCheckThatPAMTrayExeIsNextToTheManagerExecutable,
    /// <summary>已通知后台托盘退出。</summary>
    TheBackgroundTrayWasAskedToExit,
    /// <summary>热键修改后需要后台托盘重新注册；当前托盘未运行，已为你启动。</summary>
    TheTrayMustReRegisterTheHotkeyItWasNotRunningSoItHasBeenStarted,
    /// <summary>热键不能为空。</summary>
    HotkeyCannotBeEmpty,
    /// <summary>热键必须包含至少一个修饰键和一个普通按键。</summary>
    AHotkeyNeedsAtLeastOneModifierAndOneRegularKey,
    /// <summary>热键只能包含一个普通按键。</summary>
    AHotkeyCanOnlyContainOneRegularKey,
    /// <summary>热键必须包含 Ctrl、Alt、Shift 或 Win 中的至少一个修饰键。</summary>
    IncludeAtLeastOneOfCtrlAltShiftWin,
    /// <summary>热键缺少普通按键。</summary>
    MissingRegularKey,
    /// <summary>无法识别按键“{0}”。</summary>
    UnrecognizedKey0,
    /// <summary>不能把修饰键本身当作热键按键。</summary>
    AModifierKeyCannotBeTheHotkeyKey,
    /// <summary>无效的按键。</summary>
    InvalidKey,
    /// <summary>不支持该按键，请换一个字母、数字或功能键。</summary>
    UnsupportedKeyUseALetterDigitOrFunctionKeyInstead,
    /// <summary>Ctrl+Alt+Del 由 Windows 安全界面独占，任何应用都无法接管。</summary>
    CtrlAltDelIsReservedByTheWindowsSecureScreenAndCannotBeCaptured,
    /// <summary>Ctrl+Shift+Esc 用于打开任务管理器。</summary>
    CtrlShiftEscOpensTaskManager,
    /// <summary>Ctrl+Esc 用于打开开始菜单。</summary>
    CtrlEscOpensTheStartMenu,
    /// <summary>Alt+Tab 用于切换窗口。</summary>
    AltTabSwitchesWindows,
    /// <summary>Alt+Shift+Tab 用于反向切换窗口。</summary>
    AltShiftTabSwitchesWindowsInReverseOrder,
    /// <summary>Alt+F4 用于关闭当前窗口。</summary>
    AltF4ClosesTheCurrentWindow,
    /// <summary>Alt+Esc 用于按窗口顺序切换。</summary>
    AltEscCyclesThroughWindows,
    /// <summary>Win+L 用于锁定计算机。</summary>
    WinLLocksTheComputer,
    /// <summary>Win+Enter 用于启动 Windows 讲述人。</summary>
    WinEnterStartsWindowsNarrator,
    /// <summary>Win+Tab 用于打开任务视图。</summary>
    WinTabOpensTaskView,
    /// <summary>Win+D 用于显示桌面。</summary>
    WinDShowsTheDesktop,
    /// <summary>Win+E 用于打开文件资源管理器。</summary>
    WinEOpensFileExplorer,
    /// <summary>Win+R 用于打开“运行”对话框。</summary>
    WinROpensTheRunDialog,
    /// <summary>Win+I 用于打开 Windows 设置。</summary>
    WinIOpensWindowsSettings,
    /// <summary>Win+S / Win+Q 用于 Windows 搜索。</summary>
    WinSWinQOpensWindowsSearch,
    /// <summary>Win+V 用于打开剪贴板历史。</summary>
    WinVOpensClipboardHistory,
    /// <summary>Win+K 用于打开“连接”面板。</summary>
    WinKOpensTheConnectPane,
    /// <summary>Win+P 用于切换投影模式。</summary>
    WinPSwitchesProjectionModes,
    /// <summary>Win+A 用于打开操作中心。</summary>
    WinAOpensActionCenter,
    /// <summary>Win+G 用于打开 Xbox Game Bar。</summary>
    WinGOpensXboxGameBar,
    /// <summary>Win+U 用于打开轻松使用设置。</summary>
    WinUOpensEaseOfAccessSettings,
    /// <summary>Win+X 用于打开快速链接菜单。</summary>
    WinXOpensTheQuickLinkMenu,
    /// <summary>Win+T 用于浏览任务栏程序。</summary>
    WinTBrowsesTaskbarApps,
    /// <summary>Win+PrintScreen 用于系统截图并保存。</summary>
    WinPrintScreenCapturesAndSavesAScreenshot,
    /// <summary>Win++ / Win+- 用于打开或关闭放大镜。</summary>
    WinWinControlsTheMagnifier,
    /// <summary>Win+Shift+S 由 Windows 截图工具占用。</summary>
    WinShiftSIsUsedByTheWindowsSnippingTool,
    /// <summary>该组合用于 Windows 虚拟桌面管理。</summary>
    ThisCombinationManagesWindowsVirtualDesktops,
    /// <summary>Win+Alt+R 由 Xbox Game Bar 录制功能占用。</summary>
    WinAltRIsUsedByXboxGameBarRecording,
    /// <summary>Ctrl+S 是几乎所有软件的“保存”，注册为全局热键后会被拦截。</summary>
    CtrlSIsSaveInAlmostEveryAppAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+N 是常见的“新建”，注册为全局热键后会被拦截。</summary>
    CtrlNIsACommonNewShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+O 是常见的“打开”，注册为全局热键后会被拦截。</summary>
    CtrlOIsACommonOpenShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+C 是系统级“复制”，注册为全局热键后会被拦截。</summary>
    CtrlCIsTheSystemCopyShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+V 是系统级“粘贴”，注册为全局热键后会被拦截。</summary>
    CtrlVIsTheSystemPasteShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+X 是系统级“剪切”，注册为全局热键后会被拦截。</summary>
    CtrlXIsTheSystemCutShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+A 是常见的“全选”，注册为全局热键后会被拦截。</summary>
    CtrlAIsACommonSelectAllShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+Z 是常见的“撤销”，注册为全局热键后会被拦截。</summary>
    CtrlZIsACommonUndoShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+P 是常见的“打印”，注册为全局热键后会被拦截。</summary>
    CtrlPIsACommonPrintShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+F 是常见的“查找”，注册为全局热键后会被拦截。</summary>
    CtrlFIsACommonFindShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+W 是常见的“关闭标签页”，注册为全局热键后会被拦截。</summary>
    CtrlWIsACommonCloseTabShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+T 是常见的“新建标签页”，注册为全局热键后会被拦截。</summary>
    CtrlTIsACommonNewTabShortcutAGlobalHotkeyWouldInterceptIt,
    /// <summary>Ctrl+Shift+T 是浏览器“恢复关闭的标签页”，注册为全局热键后会被拦截。</summary>
    CtrlShiftTReopensClosedBrowserTabsAGlobalHotkeyWouldInterceptIt,
    /// <summary>QQ / 微信等截图工具常使用 Ctrl+Alt+A。</summary>
    ScreenshotToolsSuchAsQQWeChatOftenUseCtrlAltA,
    /// <summary>部分终端工具或 Linux 子系统使用 Ctrl+Alt+T。</summary>
    SomeTerminalToolsAndWSLLaunchersUseCtrlAltT,
    /// <summary>截图工具与部分 IDE 常使用 Ctrl+Shift+A。</summary>
    ScreenshotToolsAndSomeIDEsOftenUseCtrlShiftA,
    /// <summary>截图或输入法工具常使用 Ctrl+Alt+S。</summary>
    ScreenshotOrInputMethodToolsOftenUseCtrlAltS,
    /// <summary>中文输入法的中英文切换常使用 Ctrl+Space。</summary>
    ChineseInputMethodsOftenUseCtrlSpaceToSwitchLanguages,
    /// <summary>Windows 窗口系统菜单常使用 Alt+Space。</summary>
    AltSpaceOpensTheWindowsWindowSystemMenu,
    /// <summary>该组合已被系统或其他常驻软件占用，请换一个。</summary>
    ThisCombinationIsAlreadyTakenByWindowsOrAnotherRunningAppPickAnotherOne,
    /// <summary>热键被占用</summary>
    HotkeyIsAlreadyInUse,
    /// <summary>确定要退出 {0} 吗？</summary>
    Exit0,
    /// <summary>未找到主界面程序：{0}</summary>
    MainAppNotFound0,
    /// <summary>启动主界面失败：{0}</summary>
    FailedToStartTheMainApp0,
    /// <summary>启动 Windows Terminal 失败：{0}</summary>
    FailedToStartWindowsTerminal0,
    /// <summary>重新加载设置</summary>
    ReloadSettings,
    /// <summary>热键：{0}</summary>
    Hotkey0,
    /// <summary>热键 {0} 注册失败，可能已被系统或其他软件占用。请到“设置”中更换。</summary>
    CouldNotRegisterHotkey0ItMayBeTakenByWindowsOrAnotherAppChangeItInSettings,
    /// <summary>设置已重新加载：热键 {0}</summary>
    SettingsReloadedHotkey0,
    /// <summary>美化</summary>
    Beautify,
    /// <summary>为终端配置 Starship 提示符与 Nerd Font 字体。</summary>
    ConfigureTheStarshipPromptAndNerdFontsForYourTerminal,
    /// <summary>检测中…</summary>
    Detecting,
    /// <summary>未安装</summary>
    NotInstalled,
    /// <summary>已安装 {0}</summary>
    Installed0,
    /// <summary>未检测到 Scoop</summary>
    ScoopNotDetected,
    /// <summary>Scoop 已就绪</summary>
    ScoopIsReady,
    /// <summary>Starship 是跨 shell 的提示符引擎；通过 Scoop 安装后自动可用，无需手动配置 PATH。</summary>
    StarshipIsACrossShellPromptEngineOnceInstalledViaScoopItWorksOutOfTheBoxNoPATHSetupNeeded,
    /// <summary>Starship 官方建议先装好 Nerd Font：请在上方选择字体后再安装。</summary>
    StarshipSDocsRecommendInstallingANerdFontFirstPickAFontAboveBeforeInstalling,
    /// <summary>已安装，无需重复安装；换字体后重新打开终端即可生效。</summary>
    AlreadyInstalledNoNeedToInstallAgainReopenYourTerminalAfterChangingFontsToTakeEffect,
    /// <summary>未选字体或已安装时不可用。</summary>
    DisabledUntilAFontIsSelectedUnavailableWhenStarshipIsAlreadyInstalled,
    /// <summary>一键安装（含 Scoop）</summary>
    OneClickInstallInclScoop,
    /// <summary>重新检测</summary>
    ReDetect,
    /// <summary>正在安装 Scoop 与 Starship，可能需要几分钟…</summary>
    InstallingScoopAndStarshipThisMayTakeAFewMinutes,
    /// <summary>正在安装 Scoop…</summary>
    InstallingScoop,
    /// <summary>Scoop 安装完成。</summary>
    ScoopInstalled,
    /// <summary>Starship 安装完成。</summary>
    StarshipInstalled,
    /// <summary>未检测到 Scoop，请先安装 Scoop。</summary>
    ScoopWasNotDetectedInstallScoopFirst,
    /// <summary>scoop install starship 失败（退出码 {0}）。{1}</summary>
    ScoopInstallStarshipFailedExitCode01,
    /// <summary>Scoop 安装失败（退出码 {0}）。{1}</summary>
    ScoopInstallationFailedExitCode01,
    /// <summary>操作完成。</summary>
    Done,
    /// <summary>操作失败：{0}</summary>
    OperationFailed0,
    /// <summary>配置终端外观、Starship 提示符与 Nerd Font；首次修改终端配置前会自动保存一份原始配置，随时可恢复。</summary>
    ConfigureTerminalAppearanceTheStarshipPromptAndNerdFontsASnapshotOfTheOriginalTerminalConfigIsSavedBeforeTheFirstChangeAndCanBeRestoredAtAnyTime,
    /// <summary>对应 Windows Terminal「默认值 → 外观」的设置，写入 settings.json 的 profiles.defaults；只保存改动过的项，留空或选「终端默认」即交还终端默认。</summary>
    MapsToWindowsTerminalSDefaultsAppearanceAndWritesProfilesDefaultsInSettingsJsonOnlyChangedItemsAreSavedLeaveAFieldEmptyOrPickTerminalDefaultToHandItBackToTheTerminal,
    /// <summary>文本</summary>
    Text,
    /// <summary>字体</summary>
    Font,
    /// <summary>写入 profiles.defaults.font.face；内置 Cascadia Code NF 已随本程序打包，未安装时可一键按当前用户安装。</summary>
    WritesProfilesDefaultsFontFaceCascadiaCodeNFShipsWithThisAppInstallItForTheCurrentUserWithOneClickIfMissing,
    /// <summary>显示效果</summary>
    Effects,
    /// <summary>复古风格终端显示</summary>
    RetroTerminalEffect,
    /// <summary>CRT 扫描线与辉光效果（experimental.retroTerminalEffect）。</summary>
    CRTScanlinesAndGlowExperimentalRetroTerminalEffect,
    /// <summary>启用复古效果</summary>
    EnableRetroEffect,
    /// <summary>光标</summary>
    Cursor,
    /// <summary>光标形状</summary>
    CursorShape,
    /// <summary>对应 cursorShape；选「终端默认」即删除该设置。</summary>
    MapsToCursorShapePickTerminalDefaultToRemoveTheSetting,
    /// <summary>光标颜色</summary>
    CursorColor,
    /// <summary>对应 cursorColor，填 #RGB 或 #RRGGBB；留空使用配色方案里的光标色。</summary>
    MapsToCursorColorEnterRGBOrRRGGBBLeaveEmptyToUseTheColorSchemeSCursorColor,
    /// <summary>例如 #FFFFFF；留空表示不设置</summary>
    ForExampleFFFFFFLeaveEmptyToLeaveItUnset,
    /// <summary>背景图</summary>
    BackgroundImage,
    /// <summary>背景图路径</summary>
    BackgroundImagePath,
    /// <summary>对应 backgroundImage；也支持 desktopWallpaper 与 ms-appdata URI。清空即移除背景图。</summary>
    MapsToBackgroundImageDesktopWallpaperAndMsAppdataURIsAlsoWorkClearTheFieldToRemoveTheImage,
    /// <summary>清空</summary>
    Clear,
    /// <summary>移除背景图设置</summary>
    RemoveTheBackgroundImageSetting,
    /// <summary>背景图透明度</summary>
    BackgroundImageOpacity,
    /// <summary>对应 backgroundImageOpacity（0~1），这里按百分比显示。</summary>
    MapsToBackgroundImageOpacity01ShownHereAsAPercentage,
    /// <summary>窗口</summary>
    Window,
    /// <summary>窗口透明度</summary>
    WindowOpacity,
    /// <summary>对应 opacity（0~100，100 为完全不透明）；需低于 100 才能看出效果。</summary>
    MapsToOpacity0100Where100IsFullyOpaqueSetItBelow100ToSeeTheEffect,
    /// <summary>窗口透明亚克力材质</summary>
    AcrylicTransparency,
    /// <summary>对应 useAcrylic；开启后透明背景带模糊（Windows 11 效果最佳）。</summary>
    MapsToUseAcrylicEnablingItBlursTheTransparentBackgroundBestOnWindows11,
    /// <summary>使用亚克力材质</summary>
    UseAcrylic,
    /// <summary>放弃更改</summary>
    DiscardChanges,
    /// <summary>保存外观设置</summary>
    SaveAppearance,
    /// <summary>恢复终端备份</summary>
    RestoreWindowsTerminalBackupTheFirstTimeThisAppChangesTerminalSettingsFontAppearanceItSavesTheWholeSettingsJsonAsSettingsJsonBakLaterChangesNeverOverwriteItSoRestoringAlwaysReturnsToTheConfigFromBeforeThisAppWasInstalledTheBackupIsKeptAndCanBeRestoredRepeatedly,
    /// <summary>回到本软件第一次修改前的 settings.json；原始备份不会被覆盖或删除，可反复恢复。</summary>
    ReturnToTheSettingsJsonFromBeforeThisAppSFirstChangeTheOriginalBackupIsNeverOverwrittenOrDeletedSoItCanBeRestoredRepeatedly,
    /// <summary>原始配置备份创建于 {0}；恢复时永远回到这份配置。</summary>
    OriginalBackupCreatedAt0RestoringAlwaysReturnsToThisConfig,
    /// <summary>还没有原始配置备份；第一次通过本程序修改终端配置时会自动生成。</summary>
    NoOriginalBackupYetItIsCreatedAutomaticallyTheFirstTimeThisAppChangesTerminalSettings,
    /// <summary>备份文件：{0}</summary>
    BackupFile0,
    /// <summary>已还原到第一次修改前的终端配置；备份保留，可再次还原。</summary>
    RestoredTheWindowsTerminalConfigFromBeforeThisAppSFirstChangeTheOriginalBackupIsKeptAndCanBeRestoredAgain,
    /// <summary>有尚未保存的修改；保存后打开新终端窗口即可看到效果。</summary>
    UnsavedChangesSaveThemThenOpenANewTerminalWindowToSeeTheEffect,
    /// <summary>当前显示的是 settings.json 里已保存的值；改动后点「保存外观设置」。</summary>
    ShowingTheValuesSavedInSettingsJsonClickSaveAppearanceAfterMakingChanges,
    /// <summary>正在保存终端外观设置…</summary>
    SavingTerminalAppearanceSettings,
    /// <summary>没有需要保存的更改。</summary>
    NothingToSave,
    /// <summary>光标颜色要写成 #RGB 或 #RRGGBB；留空表示使用终端默认颜色。</summary>
    CursorColorMustBeRGBOrRRGGBBLeaveEmptyToUseTheTerminalDefault,
    /// <summary>已保存 {0} 项终端外观设置，打开新终端窗口即可看到效果。</summary>
    Saved0TerminalAppearanceSettingSOpenANewTerminalWindowToSeeTheEffect,
    /// <summary>选择终端背景图</summary>
    ChooseATerminalBackgroundImage,
    /// <summary>图片文件 (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|所有文件 (*.*)|*.*</summary>
    ImageFilesPngJpgJpegGifBmpPngJpgJpegGifBmpAllFiles,
    /// <summary>终端默认</summary>
    TerminalDefault,
    /// <summary>竖线 (bar)</summary>
    Bar,
    /// <summary>复古粗块 (vintage)</summary>
    VintageBlock,
    /// <summary>下划线 (underscore)</summary>
    Underscore,
    /// <summary>实心框 (filledBox)</summary>
    FilledBox,
    /// <summary>空心框 (emptyBox)</summary>
    EmptyBox,
    /// <summary>双下划线 (doubleUnderscore)</summary>
    DoubleUnderscore,
    /// <summary>未知的外观设置项。</summary>
    UnknownAppearanceSetting,
    /// <summary>外观设置的值不是合法的 JSON 字面量。</summary>
    TheAppearanceValueIsNotAValidJSONLiteral,
    /// <summary>开关值只能是 true / false。</summary>
    AToggleCanOnlyBeTrueOrFalse,
    /// <summary>光标形状不合法。</summary>
    InvalidCursorShape,
    /// <summary>不支持的光标形状。</summary>
    UnsupportedCursorShape,
    /// <summary>光标颜色要写成 #RGB 或 #RRGGBB。</summary>
    CursorColorMustBeRGBOrRRGGBB,
    /// <summary>背景图路径不能为空。</summary>
    TheBackgroundImagePathCannotBeEmpty,
    /// <summary>背景图透明度必须在 0 ~ 1 之间。</summary>
    BackgroundImageOpacityMustBeBetween0And1,
    /// <summary>窗口透明度必须是 0 ~ 100 的整数。</summary>
    WindowOpacityMustBeAnIntegerBetween0And100,
    /// <summary>settings.json 的目标层级不是对象，无法自动修改，请手动调整。</summary>
    ATargetLevelInSettingsJsonIsNotAnObjectCannotModifyAutomaticallyEditItManually,
    /// <summary>settings.json 的目标属性不是简单值或结构异常，无法自动修改，请手动调整。</summary>
    ATargetPropertyInSettingsJsonIsNotASimpleValueOrHasAnUnexpectedStructureEditItManually,
    /// <summary>外观设置的属性路径为空。</summary>
    TheAppearanceSettingPathIsEmpty,
    /// <summary>Windows Terminal 当前字体：{0}</summary>
    WindowsTerminalCurrentFont0,
    /// <summary>Windows Terminal 当前字体：未设置（跟随 WT 默认）</summary>
    WindowsTerminalCurrentFontNotSetUsingTheWTDefault,
    /// <summary>安装内置字体</summary>
    InstallBundledFont,
    /// <summary>把打包的 Cascadia Code NF 安装为当前用户字体（免管理员权限）。</summary>
    InstallsTheBundledCascadiaCodeNFForTheCurrentUserNoAdminRightsNeeded,
    /// <summary>想要其他 Nerd Font？安装 Scoop 后执行：scoop bucket add nerd-fonts，再安装如 JetBrains-Mono-NF、FiraCode-NF 等包，重启本页即可在下拉中看到。</summary>
    WantOtherNerdFontsWithScoopInstalledRunScoopBucketAddNerdFontsThenInstallEGJetBrainsMonoNFOrFiraCodeNFReopenThisTabAndTheyWillAppearInTheList,
    /// <summary>正在安装内置字体…</summary>
    InstallingTheBundledFont,
    /// <summary>未找到 Windows Terminal 的 settings.json，请先启动一次 Windows Terminal。</summary>
    WindowsTerminalSettingsJsonWasNotFoundStartWindowsTerminalOnceFirst,
    /// <summary>未找到字体文件：{0}</summary>
    FontFileNotFound0,
    /// <summary>只支持 .ttf 字体文件。</summary>
    OnlyTtfFontFilesAreSupported,
    /// <summary>安装字体失败：{0}</summary>
    FailedToInstallTheFont0,
    /// <summary>已为当前用户安装 {0}。</summary>
    Installed0ForTheCurrentUser,
    /// <summary>字体名称不能为空。</summary>
    TheFontNameCannotBeEmpty,
    /// <summary>主题预设</summary>
    ThemePreset,
    /// <summary>标注“需 Nerd Font”的预设要先装好字体，否则提示符会出现乱码方块。</summary>
    PresetsMarkedAsNeedingANerdFontRequireTheFontToBeInstalledFirstOtherwiseThePromptShowsPlaceholderBoxes,
    /// <summary>应用主题</summary>
    ApplyTheme,
    /// <summary>正在应用主题预设…</summary>
    ApplyingTheThemePreset,
    /// <summary>主题已应用：{0}；原 starship.toml 已自动备份。</summary>
    ThemeApplied0TheOriginalStarshipTomlHasBeenBackedUp,
    /// <summary>主题已应用：{0}。该预设需要 Nerd Font，当前未安装内置字体，提示符可能显示乱码方块，建议先安装。</summary>
    ThemeApplied0ThisPresetNeedsANerdFontWhichIsNotInstalledYetThePromptMayShowPlaceholderBoxesInstallTheBundledFontFirst,
    /// <summary>请先选择一个主题预设。</summary>
    SelectAThemePresetFirst,
    /// <summary>未找到内置主题预设：{0}</summary>
    BundledThemePresetNotFound0,
    /// <summary>配置维护</summary>
    ConfigMaintenance,
    /// <summary>已创建</summary>
    Created,
    /// <summary>未创建</summary>
    NotCreated,
    /// <summary>打开 starship.toml</summary>
    OpenStarshipToml,
    /// <summary>恢复上次主题设置</summary>
    RestoreStarshipTomlBackup,
    /// <summary>恢复终端备份</summary>
    RestoreWindowsTerminalBackup,
    /// <summary>正在打开 starship.toml…</summary>
    OpeningStarshipToml,
    /// <summary>恢复上次主题设置</summary>
    RestoringTheStarshipTomlBackup,
    /// <summary>已恢复上一次的主题设置。</summary>
    StarshipTomlHasBeenRestoredFromTheBackup,
    /// <summary>恢复终端备份</summary>
    RestoringTheWindowsTerminalBackup,
    /// <summary>没有可恢复的备份。</summary>
    NoBackupToRestore,
    /// <summary>状态刷新失败：{0}</summary>
    FailedToRefreshTheStatus0,
    /// <summary>settings.json 的顶层不是对象，无法自动修改。</summary>
    TheRootOfSettingsJsonIsNotAnObjectCannotModifyAutomatically,
    /// <summary>settings.json 的 profiles 不是对象（可能是旧版数组结构），无法自动修改，请手动调整。</summary>
    ProfilesInSettingsJsonIsNotAnObjectPossiblyALegacyArrayLayoutCannotModifyAutomaticallyEditItManually,
    /// <summary>settings.json 的 profiles 结构异常，无法自动修改。</summary>
    UnexpectedProfilesStructureInSettingsJsonCannotModifyAutomatically,
    /// <summary>settings.json 的 profiles.defaults 不是对象，无法自动修改。</summary>
    ProfilesDefaultsInSettingsJsonIsNotAnObjectCannotModifyAutomatically,
    /// <summary>settings.json 的 profiles.defaults 结构异常，无法自动修改。</summary>
    UnexpectedProfilesDefaultsStructureInSettingsJsonCannotModifyAutomatically,
    /// <summary>profiles.defaults.font 不是对象，无法自动修改。</summary>
    ProfilesDefaultsFontIsNotAnObjectCannotModifyAutomatically,
    /// <summary>profiles.defaults.font 结构异常，无法自动修改。</summary>
    UnexpectedProfilesDefaultsFontStructureInSettingsJsonCannotModifyAutomatically,
    /// <summary>无法定位 profiles.defaults.font.face，请手动修改字体设置。</summary>
    CannotLocateProfilesDefaultsFontFaceEditTheFontSettingManually,
    /// <summary>settings.json 结构异常，无法自动修改。</summary>
    UnexpectedSettingsJsonStructureCannotModifyAutomatically,
    /// <summary>写入后校验未通过，已保持原配置不变。</summary>
    PostWriteValidationFailedTheOriginalFileWasKeptUnchanged,
    /// <summary>自定义提示符</summary>
    CustomPrompt,
    /// <summary>预览为示意值，实际文字由 Starship 按系统状态生成（切换目录时路径会跟着变）。</summary>
    ThePreviewUsesSampleValuesStarshipGeneratesTheRealTextFromSystemStateThePathFollowsCd,
    /// <summary>插入变量</summary>
    InsertVariable,
    /// <summary>当前颜色</summary>
    CurrentColor,
    /// <summary>空格</summary>
    Space,
    /// <summary>用户名</summary>
    Username,
    /// <summary>主机名</summary>
    Hostname,
    /// <summary>冒号</summary>
    Colon,
    /// <summary>结束符</summary>
    Tail,
    /// <summary>重置为默认</summary>
    ResetToDefaults,
    /// <summary>恢复应用前配置</summary>
    RestorePreApplyConfig,
    /// <summary>应用到 Starship</summary>
    ApplyToStarship,
    /// <summary>starship.toml 尚未创建，应用后会自动生成</summary>
    StarshipTomlDoesNotExistYetApplyingWillCreateIt,
    /// <summary>当前提示符由 PowerShell 管理器接管</summary>
    ThePromptIsCurrentlyManagedByPowerShellManager,
    /// <summary>当前使用原有提示符配置（尚未应用过自定义）</summary>
    UsingTheOriginalPromptCustomPromptNotAppliedYet,
    /// <summary>正在写入自定义提示符…</summary>
    WritingTheCustomPrompt,
    /// <summary>自定义提示符已写入 starship.toml，原配置已单独备份；打开新终端即可看到效果。</summary>
    CustomPromptWrittenToStarshipTomlWithASeparateBackupOfTheOriginalConfigOpenANewTerminalToSeeIt,
    /// <summary>正在恢复应用前的提示符配置…</summary>
    RestoringThePreApplyPromptConfig,
    /// <summary>已恢复应用前的 starship.toml，自定义提示符已移除。</summary>
    RestoredTheOriginalStarshipTomlTheCustomPromptHasBeenRemoved,
    /// <summary>没有可恢复的应用前备份。</summary>
    NoPreApplyBackupToRestore,
    /// <summary>已重置为默认内容，点击“应用到 Starship”才会写入。</summary>
    DefaultsRestoredInTheEditorClickApplyToStarshipToWriteThem,
    /// <summary>提示符前空一行</summary>
    BlankLineBeforePrompt,
    /// <summary>路径分隔符用 /</summary>
    UseAsPathSeparator,
    /// <summary>开启后，每条命令前会多一个空行。</summary>
    WhenEnabledAnEmptyLineIsInsertedBeforeEveryPromptStarshipAddNewline,
    /// <summary>关闭后，路径分隔符跟随系统默认。</summary>
    WhenDisabledPathsUseTheSystemBackslashSeparatorStarshipUseOsPathSep,
    /// <summary>高级选项</summary>
    AdvancedOptions,
    /// <summary>设置终端字体、外观和命令行提示符。第一次修改前会自动备份，随时可以还原；点「刷新状态」重新检测。</summary>
    BeautifyPageIntro,
    /// <summary>点「添加片段」从左到右拼出提示符：每段用下拉选内容（用户名、目录、符号等）和颜色。下面的预览会实时更新；应用后只改提示符这一行，其它设置不动。</summary>
    PromptEditorIntro,
    /// <summary>下拉里能选变量和符号；颜色选预设，或选「自定义…」后填 6 位色号。</summary>
    PromptSegmentHint,
    /// <summary>应用主题前会自动备份，点这里还原上次的配置。</summary>
    TomlBackupHint,
    /// <summary>保存设置</summary>
    SaveSettings,
    /// <summary>正在保存设置…</summary>
    SavingSettings,
    /// <summary>已保存设置：主题「{0}」已应用，{1} 项外观改动已写入；打开新终端窗口即可看到效果。</summary>
    SettingsSavedThemeAndAppearance,
    /// <summary>已保存设置：主题「{0}」已应用；打开新终端窗口即可看到效果。</summary>
    SettingsSavedThemeOnly,
    /// <summary>主题已应用，但外观保存失败：{0}</summary>
    ThemeAppliedButAppearanceFailed,
    /// <summary>只还原还没保存的终端外观改动，不影响主题选择。</summary>
    DiscardAppearanceTooltip,
    /// <summary>应用所选主题，并保存字体与外观改动。</summary>
    SaveSettingsTooltip,
    /// <summary>改完后点上面的「保存设置」。</summary>
    ClickSaveSettingsAbove,
    /// <summary>已保存设置，但没装图标字体，主题里的符号可能显示成方块。</summary>
    SettingsSavedMissingFontWarning,
    /// <summary>自定义…</summary>
    CustomEllipsis,
    /// <summary>刷新状态</summary>
    RefreshStatus,
    /// <summary>重新探测 Starship、Scoop、字体与终端配置状态。</summary>
    RefreshStatusTooltip,
    /// <summary>格式</summary>
    PromptFormatLabel,
    /// <summary>输入任意文字或 $变量</summary>
    CustomSegmentTooltip,
    /// <summary>输入 6 位十六进制色号（即 # 后面的部分）</summary>
    HexColorTooltip,
    /// <summary>删除该片段</summary>
    RemoveSegmentTooltip,
    /// <summary>添加片段</summary>
    AddSegment,
    /// <summary>两种方案二选一，切换后点下方的「保存设置」生效。</summary>
    PromptSchemeHint,
    /// <summary>把当前方案恢复成程序默认，点「保存设置」后才写入。</summary>
    RestoreDefaultsTooltip,
    /// <summary>已恢复默认主题，点「保存设置」才会写入。</summary>
    DefaultThemeRestored,
    /// <summary>已放弃未保存的改动，回到上次保存的状态。</summary>
    ChangesDiscarded,
    /// <summary>请先添加至少一个片段。</summary>
    AddSegmentFirst,
    /// <summary>当前主题：自定义提示符</summary>
    CustomPromptSimpleStatus,
    /// <summary>通用设置</summary>
    CommonSettings,
    /// <summary>主题预设和自定义提示符都会用这两个设置。</summary>
    CommonOptionsScope,
    /// <summary>示例 / 指南</summary>
    CommandExampleGuide,
    /// <summary>查看命令代码的写法示例，可直接复制。</summary>
    CommandExampleGuideTooltip,
    /// <summary>只写要执行的命令即可：保存时会包成以命令名命名的函数，只有在终端里输入该名称时才会执行。</summary>
    CommandBodyHint,
    /// <summary>高级选项：命令参数（可选）</summary>
    AdvancedCommandArgumentsOptional,
    /// <summary>允许在命令名后传入参数</summary>
    AllowArgumentsAfterCommandName,
    /// <summary>固定参数</summary>
    FixedArguments,
    /// <summary>例如：-t 8.8.8.8</summary>
    FixedArgumentsPlaceholder,
    /// <summary>每次调用都会带上这些参数，终端里临时输入的参数排在后面；两者都追加在命令体最后一行的末尾。含空格的参数请用英文双引号包起来。</summary>
    CommandArgumentsHint,
    /// <summary>把下面的命令复制后，点「新建命令」，粘贴到命令代码里，设置好命令名称，保存并生效即可。</summary>
    CommandGuideIntro,
    /// <summary>命令名称就是将来在终端里输入的名字；代码只写要执行的命令，不需要写 function。</summary>
    CommandGuideNameHint,
    /// <summary>复制</summary>
    Copy,
    /// <summary>关闭</summary>
    Close,
    /// <summary>已复制：{0}</summary>
    Copied0,
    /// <summary>复制失败：{0}</summary>
    CopyFailed0,
}

/// <summary>LocKey 对应的 {zh, en} 语言表，以及中文→英文的兜底查表（供动态字符串与 XAML 桥接）。</summary>
internal static class LocTable
{
    public static readonly IReadOnlyDictionary<LocKey, (string Zh, string En)> Data =
        new Dictionary<LocKey, (string Zh, string En)>
        {
            [LocKey.PowerShellManager] = ("PowerShell 管理器", "PowerShell Manager"),
            [LocKey.CustomCommands] = ("自定义命令", "Custom Commands"),
            [LocKey.Settings] = ("设置", "Settings"),
            [LocKey.Cancel] = ("取消", "Cancel"),
            [LocKey.OK] = ("确定", "OK"),
            [LocKey.Edit] = ("编辑", "Edit"),
            [LocKey.Delete] = ("删除", "Delete"),
            [LocKey.Exit] = ("退出", "Exit"),
            [LocKey.Status] = ("状态", "Status"),
            [LocKey.Name] = ("名称", "Name"),
            [LocKey.Path] = ("路径", "Path"),
            [LocKey.Note] = ("备注", "Note"),
            [LocKey.Actions] = ("操作", "Actions"),
            [LocKey.Alias] = ("别名", "Alias"),
            [LocKey.Browse] = ("浏览…", "Browse…"),
            [LocKey.SaveApply] = ("保存并生效", "Save & Apply"),
            [LocKey.NewAlias] = ("新建别名", "New Alias"),
            [LocKey.NewCommand] = ("新建命令", "New Command"),
            [LocKey.ConfirmDelete] = ("确认删除？", "Confirm delete?"),
            [LocKey.FileNotFound] = ("文件不存在", "File not found"),
            [LocKey.DeleteClickAgainToConfirm] = ("删除（再次点击确认）", "Delete (click again to confirm)"),
            [LocKey.ToggleEnabledState] = ("切换启用状态", "Toggle enabled state"),
            [LocKey.ClickToEnableDisable] = ("点击启用 / 禁用", "Click to enable / disable"),
            [LocKey.Enabled] = ("已启用", "Enabled"),
            [LocKey.Disabled] = ("已禁用", "Disabled"),
            [LocKey.ShowMainWindow] = ("显示主界面", "Show Main Window"),
            [LocKey.Running] = ("运行中", "Running"),
            [LocKey.NotRunning] = ("未运行", "Not running"),
            [LocKey.ManageSetAliasShortcutsInPROFILETheyTakeEffectInNewWindowsRightAfterSaving] = ("在 $PROFILE 中集中管理 Set-Alias 快捷启动项，保存后立即在新窗口生效。", "Manage Set-Alias shortcuts in $PROFILE. They take effect in new windows right after saving."),
            [LocKey.CopyPROFILEPath] = ("复制 $PROFILE 路径", "Copy $PROFILE path"),
            [LocKey.SearchAliasPathOrNote] = ("搜索别名、路径或备注", "Search alias, path or note"),
            [LocKey.AliasRequired] = ("别名（必填）", "Alias (required)"),
            [LocKey.ApplicationPathRequired] = ("应用程序路径（必填）", "Application path (required)"),
            [LocKey.NoteOptional] = ("备注（可选）", "Note (optional)"),
            [LocKey.EGBlbl] = ("例如 blbl", "e.g. blbl"),
            [LocKey.EGOpenBilibili] = ("例如：打开哔哩哔哩", "e.g. open Bilibili"),
            [LocKey.AdvancedLaunchArgumentsOptional] = ("高级选项：启动参数（可选）", "Advanced: launch arguments (optional)"),
            [LocKey.LaunchArguments] = ("启动参数", "Launch arguments"),
            [LocKey.EGIncognito] = ("例如：--incognito", "e.g. --incognito"),
            [LocKey.TheseArgumentsAreAddedEveryTimeTheAppStartsArgumentsPassedInTheTerminalComeAfterThemWrapValuesContainingSpacesInDoubleQuotes] = ("每次启动该程序都会带上这些参数；在终端里临时传入的参数排在后面。含空格的参数请用英文双引号包起来。", "These arguments are added every time the app starts; arguments passed in the terminal come after them. Wrap values containing spaces in double quotes."),
            [LocKey.EnterToConfirmEscToCancel] = ("Enter 确定 · Esc 取消", "Enter to confirm · Esc to cancel"),
            [LocKey.AliasCannotBeEmpty] = ("别名不能为空。", "Alias cannot be empty."),
            [LocKey.AliasCannotContainSpaces] = ("别名不能包含空格。", "Alias cannot contain spaces."),
            [LocKey.ThisAliasAlreadyExistsChooseAnother] = ("该别名已存在，请换一个。", "This alias already exists. Choose another."),
            [LocKey.SelectOrEnterAnApplicationPath] = ("请选择或填写应用程序路径。", "Select or enter an application path."),
            [LocKey.FileNotFoundButYouCanStillSave] = ("⚠ 文件不存在，仍可保存。", "⚠ File not found, but you can still save."),
            [LocKey.SelectAnApplicationToLaunch] = ("选择要启动的应用程序", "Select an application to launch"),
            [LocKey.ApplicationsExeExeAllFiles] = ("应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*", "Applications (*.exe)|*.exe|All files (*.*)|*.*"),
            [LocKey.NoAliasesYet] = ("还没有别名", "No aliases yet"),
            [LocKey.ClickNewAliasInTheTopRightToAddAPowerShellShortcutForAnApp] = ("点击右上角“新建别名”，为常用应用添加一个 PowerShell 快捷命令。", "Click \"New Alias\" in the top right to add a PowerShell shortcut for an app."),
            [LocKey.NoMatchingAliases] = ("没有匹配的别名", "No matching aliases"),
            [LocKey.TryAnotherKeywordOrClearTheSearchBoxToSeeAllAliases] = ("换个关键字试试，或清空搜索框查看全部别名。", "Try another keyword, or clear the search box to see all aliases."),
            [LocKey.PathCopied] = ("路径已复制", "Path copied"),
            [LocKey.PROFILEPathIsNotAvailableYetCannotCopy] = ("尚未读取到 $PROFILE 路径，无法复制。", "$PROFILE path is not available yet; cannot copy."),
            [LocKey.ExecutionPolicyRestricted] = ("执行策略受限", "Execution policy restricted"),
            [LocKey.TryToFix] = ("尝试修复", "Try to fix"),
            [LocKey.KeepFrequentlyUsedFunctionsAndSnippetsInAManagedPROFILEBlockOnlyThatBlockIsRewritten] = ("把常用命令收进 $PROFILE 的受控区块：命令名就是终端里的调用入口，保存时只改写该区块，其余配置原样保留。", "Keep frequently used commands in a managed $PROFILE block: the command name is what you type in a terminal. Only that block is rewritten."),
            [LocKey.OnlyThisBlockIsRewrittenTheRestOfPROFILEIsPreserved] = ("保存时只重写该区块，区块外的 $PROFILE 内容不会被修改。", "Only this block is rewritten; the rest of $PROFILE is preserved."),
            [LocKey.SearchNameCodeOrNote] = ("搜索名称、代码或备注", "Search name, code or note"),
            [LocKey.CtrlEnterToConfirmEscToCancel] = ("Ctrl+Enter 确定 · Esc 取消", "Ctrl+Enter to confirm · Esc to cancel"),
            [LocKey.CommandNameRequired] = ("命令名称（必填）", "Command name (required)"),
            [LocKey.EGLlOrGetProject] = ("例如 ll 或 Get-Project", "e.g. ll or Get-Project"),
            [LocKey.EGListTheCurrentDirectoryByTime] = ("例如：列出当前目录并按时间排序", "e.g. list the current directory by time"),
            [LocKey.EnableThisCommand] = ("启用该命令", "Enable this command"),
            [LocKey.CommandCodeRequired] = ("命令代码（必填）", "Command code (required)"),
            [LocKey.CodeIsWrittenVerbatimIntoTheManagedBlockDisabledCommandsAreSavedAsCommentsAndRestoredWhenReEnabled] = ("代码会被原样写入受控区块；禁用的命令保存为注释行，重新启用后完整还原。", "Code is written verbatim into the managed block. Disabled commands are saved as comments and restored when re-enabled."),
            [LocKey.CommandNameCannotBeEmpty] = ("命令名称不能为空。", "Command name cannot be empty."),
            [LocKey.CommandNameCannotContainSpaces] = ("命令名称不能包含空格。", "Command name cannot contain spaces."),
            [LocKey.CommandNameContainsCharactersNotSupportedByPowerShell] = ("命令名称包含 PowerShell 不支持的字符。", "Command name contains characters not supported by PowerShell."),
            [LocKey.ThisNameAlreadyExistsChooseAnother] = ("该名称已存在，请换一个。", "This name already exists. Choose another."),
            [LocKey.K0IsReservedForABuiltInCommandChooseAnother] = ("{0} 是内置命令，请换一个名称。", "\"{0}\" is reserved for a built-in command. Choose another."),
            [LocKey.ThisNameIsABuiltInCommandChooseAnother] = ("该名称是内置命令，请换一个。", "This name is a built-in command. Choose another."),
            [LocKey.BuiltIn] = ("内置", "Built-in"),
            [LocKey.BuiltInCommandMaintainedAutomaticallyOnSaveCannotBeEditedOrDeleted] = ("内置命令，随保存自动维护，不可编辑或删除", "Built-in command, maintained automatically on save; cannot be edited or deleted."),
            [LocKey.CommandCodeCannotBeEmpty] = ("命令代码不能为空。", "Command code cannot be empty."),
            [LocKey.NoCustomCommandsYet] = ("还没有自定义命令", "No custom commands yet"),
            [LocKey.ClickNewCommandInTheTopRightToAddAFunctionOrSnippetToTheManagedPROFILEBlock] = ("点击右上角“新建命令”，把常用函数或脚本片段写进 $PROFILE 的受控区块。", "Click \"New Command\" in the top right to add a function or snippet to the managed $PROFILE block."),
            [LocKey.NoMatchingCommands] = ("没有匹配的命令", "No matching commands"),
            [LocKey.TryAnotherKeywordOrClearTheSearchBoxToSeeAllCommands] = ("换个关键字试试，或清空搜索框查看全部命令。", "Try another keyword, or clear the search box to see all commands."),
            [LocKey.Starting] = ("正在启动…", "Starting…"),
            [LocKey.ReadingPROFILE] = ("正在读取 $PROFILE…", "Reading $PROFILE…"),
            [LocKey.UnableToReadPROFILEPath] = ("无法读取 $PROFILE 路径", "Unable to read $PROFILE path"),
            [LocKey.Saved0Aliases] = ("已保存 · 共 {0} 个别名", "Saved · {0} aliases"),
            [LocKey.Saved0CustomCommands] = ("已保存 · 共 {0} 个自定义命令", "Saved · {0} custom commands"),
            [LocKey.UnsavedChanges0Aliases] = ("有未保存更改 · 共 {0} 个别名", "Unsaved changes · {0} aliases"),
            [LocKey.UnsavedChanges0CustomCommands] = ("有未保存更改 · 共 {0} 个自定义命令", "Unsaved changes · {0} custom commands"),
            [LocKey.TheProfileDoesNotExistItWillBeCreatedWhenYouClickSaveApply] = ("配置文件不存在，点击“保存并生效”时将自动创建。", "The profile does not exist; it will be created when you click \"Save & Apply\"."),
            [LocKey.ErrorFailedToReadPROFILE0] = ("错误：读取 $PROFILE 失败：{0}", "Error: failed to read $PROFILE: {0}"),
            [LocKey.ErrorCannotLocatePROFILE0] = ("错误：无法定位 $PROFILE：{0}", "Error: cannot locate $PROFILE: {0}"),
            [LocKey.WritingPROFILE] = ("正在写入 $PROFILE…", "Writing $PROFILE…"),
            [LocKey.RebuiltTheManagedSectionOfPROFILEFromPamDataJson] = ("已按 pam-data.json 重建 $PROFILE 托管区。", "Rebuilt the managed section of $PROFILE from pam-data.json."),
            [LocKey.K0ConflictsWithAnExistingCommandInPROFILETheseAliasesWillNotTakeEffect] = ("{0} 与 $PROFILE 中已有命令同名，这些别名不会生效。", "{0} conflicts with an existing command in $PROFILE; these aliases will not take effect."),
            [LocKey.ErrorFailedToWrite0] = ("错误：写入失败：{0}", "Error: failed to write: {0}"),
            [LocKey.ErrorFailedToCheckExecutionPolicy0] = ("错误：检查执行策略失败：{0}", "Error: failed to check execution policy: {0}"),
            [LocKey.ExecutionPolicyIs0PROFILEMayNotLoad] = ("执行策略为 {0}，$PROFILE 可能无法加载。", "Execution policy is {0}; $PROFILE may not load."),
            [LocKey.SettingExecutionPolicyToRemoteSignedCurrentUser] = ("正在将执行策略设为 RemoteSigned (CurrentUser)…", "Setting execution policy to RemoteSigned (CurrentUser)…"),
            [LocKey.ErrorFailedToFixExecutionPolicy0] = ("错误：修复执行策略失败：{0}", "Error: failed to fix execution policy: {0}"),
            [LocKey.ExecutionPolicyIsStill0CheckTheSystemPolicy] = ("执行策略仍为 {0}，请检查系统策略。", "Execution policy is still {0}; check the system policy."),
            [LocKey.ExecutionPolicyIsStillRestrictedCheckItManually] = ("执行策略仍受限，请手动检查系统策略。", "Execution policy is still restricted; check it manually."),
            [LocKey.ExecutionPolicySetToRemoteSignedCurrentUserSaveAgainToApplyInANewWindow] = ("执行策略已设为 RemoteSigned (CurrentUser)。再次保存即可在新窗口生效。", "Execution policy set to RemoteSigned (CurrentUser). Save again to apply in a new window."),
            [LocKey.SavedExecutionPolicyIsRestrictedNoNewPowerShellWindowWasOpenedClickTryToFix] = ("已写入 · 执行策略受限，未启动新的 PowerShell 窗口。请点击“尝试修复”。", "Saved · Execution policy is restricted; no new PowerShell window was opened. Click \"Try to fix\"."),
            [LocKey.SavedOpenedANewPowerShellWindow0Aliases] = ("已写入 · 已启动新的 PowerShell 窗口 · 共 {0} 个别名", "Saved · Opened a new PowerShell window · {0} aliases"),
            [LocKey.ErrorSavedButFailedToLaunchPowerShell0] = ("错误：已写入，但启动 PowerShell 失败：{0}", "Error: saved, but failed to launch PowerShell: {0}"),
            [LocKey.ErrorFailedToCopyPath0] = ("错误：复制路径失败：{0}", "Error: failed to copy path: {0}"),
            [LocKey.EditAlias0] = ("编辑别名 · {0}", "Edit alias · {0}"),
            [LocKey.EditCommand0] = ("编辑命令 · {0}", "Edit command · {0}"),
            [LocKey.ErrorFailedToReadCustomCommands0] = ("错误：读取自定义命令失败：{0}", "Error: failed to read custom commands: {0}"),
            [LocKey.WritingCustomCommands] = ("正在写入自定义命令…", "Writing custom commands…"),
            [LocKey.CreatedPROFILEAndSaved] = ("已创建 $PROFILE 并写入", "Created $PROFILE and saved"),
            [LocKey.Saved] = ("已写入", "Saved"),
            [LocKey.Saved0CustomCommandsExecutionPolicyRestrictedNoNewWindowWasOpened] = ("已写入 · 共 {0} 个自定义命令 · 执行策略受限，未启动新的 PowerShell 窗口。", "Saved · {0} custom commands · Execution policy restricted; no new window was opened."),
            [LocKey.CreatedPROFILEAndSaved0CustomCommandsExecutionPolicyRestrictedNoNewWindowWasOpened] = ("已创建 $PROFILE 并写入 · 共 {0} 个自定义命令 · 执行策略受限，未启动新的 PowerShell 窗口。", "Created $PROFILE and saved · {0} custom commands · Execution policy restricted; no new window was opened."),
            [LocKey.SavedOpenedANewPowerShellWindow0CustomCommands] = ("已写入 · 已启动新的 PowerShell 窗口 · 共 {0} 个自定义命令", "Saved · Opened a new PowerShell window · {0} custom commands"),
            [LocKey.CreatedPROFILEAndSavedOpenedANewPowerShellWindow0CustomCommands] = ("已创建 $PROFILE 并写入 · 已启动新的 PowerShell 窗口 · 共 {0} 个自定义命令", "Created $PROFILE and saved · Opened a new PowerShell window · {0} custom commands"),
            [LocKey.ErrorFailedToWriteCustomCommands0] = ("错误：写入自定义命令失败：{0}", "Error: failed to write custom commands: {0}"),
            [LocKey.AdjustAppearanceTerminalAndGlobalHotkeyChangesAreSavedImmediatelyAndSyncedToTheTray] = ("调整外观、终端与全局热键。修改会立即保存，并同步到后台托盘。", "Adjust appearance, terminal and global hotkey. Changes are saved immediately and synced to the tray."),
            [LocKey.Appearance] = ("外观", "Appearance"),
            [LocKey.Theme] = ("主题", "Theme"),
            [LocKey.LightDarkAppearanceOfThePAMInterface] = ("PAM 界面的明暗风格。", "Light / dark appearance of the PAM interface."),
            [LocKey.Language] = ("语言", "Language"),
            [LocKey.InterfaceLanguageAppliesImmediately] = ("界面显示语言，切换后立即生效。", "Interface language; applies immediately."),
            [LocKey.FollowSystem] = ("跟随系统", "Follow system"),
            [LocKey.Light] = ("浅色", "Light"),
            [LocKey.Dark] = ("深色", "Dark"),
            [LocKey.X] = ("简体中文", "简体中文"),
            [LocKey.English] = ("English", "English"),
            [LocKey.AutoDetectPreferPowerShell7] = ("自动探测（优先 PowerShell 7）", "Auto-detect (prefer PowerShell 7)"),
            [LocKey.PowerShell7PwshExe] = ("PowerShell 7 (pwsh.exe)", "PowerShell 7 (pwsh.exe)"),
            [LocKey.WindowsPowerShell51PowershellExe] = ("Windows PowerShell 5.1 (powershell.exe)", "Windows PowerShell 5.1 (powershell.exe)"),
            [LocKey.Terminal] = ("终端", "Terminal"),
            [LocKey.DefaultTerminal] = ("默认终端", "Default terminal"),
            [LocKey.UsedByTheGlobalHotkeyByTheWindowOpenedAfterSavingAndByOpenProfileFileBelow] = ("用于全局热键唤醒的终端、保存后启动的窗口，以及下方“打开配置文件”。", "Used by the global hotkey, by the window opened after saving, and by \"Open profile file\" below."),
            [LocKey.PowerShell7WasNotFoundFallingBackToWindowsPowerShell51] = ("未检测到 PowerShell 7，已回退到 Windows PowerShell 5.1。", "PowerShell 7 was not found; falling back to Windows PowerShell 5.1."),
            [LocKey.OpenProfileFile] = ("打开配置文件", "Open profile file"),
            [LocKey.OpensTheMatchingPROFILEForTheSelectedTerminalTheFileIsCreatedIfMissing] = ("按上方选择的默认终端打开对应的 $PROFILE；文件不存在时会自动创建。", "Opens the matching $PROFILE for the selected terminal. The file is created if missing."),
            [LocKey.PROFILEPathNotResolvedYet] = ("尚未解析到 $PROFILE 路径", "$PROFILE path not resolved yet"),
            [LocKey.GlobalHotkey] = ("全局热键", "Global hotkey"),
            [LocKey.OpenTerminal] = ("打开终端", "Open terminal"),
            [LocKey.CurrentHotkey] = ("当前热键", "Current hotkey"),
            [LocKey.Change] = ("更改…", "Change…"),
            [LocKey.RestoreDefault] = ("恢复默认", "Restore default"),
            [LocKey.ClickChangeThenPressAKeyCombinationEscCancels] = ("点击“更改”后按下组合键；Esc 取消录制。", "Click \"Change\" then press a key combination. Esc cancels."),
            [LocKey.PressAKeyCombination] = ("请按下组合键…", "Press a key combination…"),
            [LocKey.IncludeAtLeastOneModifierCtrlAltShiftWin] = ("至少包含一个修饰键（Ctrl / Alt / Shift / Win）。", "Include at least one modifier (Ctrl / Alt / Shift / Win)."),
            [LocKey.CheckConflicts] = ("检测冲突", "Check conflicts"),
            [LocKey.ApplyHotkey] = ("应用热键", "Apply hotkey"),
            [LocKey.AvailableNotCurrentlyUsedByWindowsOrOtherApps] = ("可用：该组合当前没有被系统或其他软件占用。", "Available: not currently used by Windows or other apps."),
            [LocKey.ReservedByWindows0] = ("系统保留：{0}", "Reserved by Windows: {0}"),
            [LocKey.AlreadyInUse0] = ("已被占用：{0}", "Already in use: {0}"),
            [LocKey.CurrentlyHeldByThePAMTraySafeToKeep] = ("当前由 PAM 托盘占用，可继续使用。", "Currently held by the PAM tray; safe to keep."),
            [LocKey.CommonShortcutWarning0] = ("常见软件快捷键提醒：{0}", "Common shortcut warning: {0}"),
            [LocKey.NotCheckedYetClickCheckConflictsBelow] = ("尚未检测，点击下方“检测冲突”。", "Not checked yet. Click \"Check conflicts\" below."),
            [LocKey.TerminalInput] = ("终端输入", "Terminal input"),
            [LocKey.SwitchTheChineseIMEToEnglishModeWhenATerminalOpens] = ("打开终端时把中文输入法切到英文模式", "Switch the Chinese IME to English mode when a terminal opens"),
            [LocKey.WhenANewPowerShellWindowsTerminalWindowOpensTheTrayReadsTheTaskbarInputIndicatorAndTapsShiftOnceOnlyIfTheIMEIsInChineseModeTheInputMethodItselfAndTheKeyboardLayoutStayUnchangedNothingIsPressedWhenItIsAlreadyInEnglishModeEachWindowIsHandledOnce] = ("后台托盘检测到新打开的 PowerShell / Windows Terminal 窗口时，先读任务栏输入指示器；只有处于中文模式才模拟一次 Shift 切到英文输入状态。输入法本身不变，也不会换成英文键盘；已是英文模式时一个键都不按。每个窗口只处理一次。", "When a new PowerShell / Windows Terminal window opens, the tray reads the taskbar input indicator and taps Shift once only if the IME is in Chinese mode. The input method itself and the keyboard layout stay unchanged; nothing is pressed when it is already in English mode. Each window is handled once."),
            [LocKey.AliasLaunching] = ("别名启动", "Alias launching"),
            [LocKey.LaunchAsASeparateProcessSilenceOutput] = ("独立进程启动（静默日志）", "Launch as a separate process (silence output)"),
            [LocKey.LaunchAliasesInASeparateProcessAndRedirectItsStdoutStderrToTheNULDeviceNoProgramOutputInTheTerminalAndClosingPowerShellWillNotCloseTheAppTurnOffToRestoreTheOriginalDirectLaunchPROFILEIsLoadedOnlyWhenATerminalStartsSoOpenANewTerminalWindowAfterSavingAliases] = ("用别名启动软件时改为独立进程，并把子进程的 stdout/stderr 重定向到 NUL 设备：终端不再刷程序日志，关闭 PowerShell 也不会把软件一起关掉。关闭则恢复原来的直接启动方式。$PROFILE 只在终端启动时加载一次，保存别名后要新开终端窗口才生效。", "Launch aliases in a separate process and redirect its stdout/stderr to the NUL device: no program output in the terminal, and closing PowerShell will not close the app. Turn off to restore the original direct launch. $PROFILE is loaded only when a terminal starts, so open a new terminal window after saving aliases."),
            [LocKey.ShowAliasRemarkOnLaunch] = ("启动时显示别名备注", "Show alias remark on launch"),
            [LocKey.PrintTheAliasRemarkInTheTerminalBeforeLaunchingNothingIsPrintedWhenTheRemarkIsEmpty] = ("启动前在终端打印一行该别名的备注；没有备注时不打印。", "Print the alias remark in the terminal before launching; nothing is printed when the remark is empty."),
            [LocKey.FriendlyHintForUnknownCommands] = ("未找到命令时显示友好提示", "Friendly hint for unknown commands"),
            [LocKey.WhenACommandIsNotFoundReplacePowerShellSRedErrorWithOneShortHintPointingToPlsOrTheManagerTurnOffToRestoreTheDefaultErrorPROFILEIsLoadedOnlyWhenATerminalStartsSoOpenANewTerminalWindowAfterSaving] = ("终端里输入不存在的命令时，用一行简短提示代替 PowerShell 的红色报错，并引导到 pls 或管理器。关闭后恢复默认报错；$PROFILE 只在终端启动时加载，保存后要新开终端窗口才生效。", "When a command is not found, replace PowerShell's red error with one short hint pointing to pls or the manager. Turn off to restore the default error. $PROFILE is loaded only when a terminal starts, so open a new terminal window after saving."),
            [LocKey.Background] = ("后台运行", "Background"),
            [LocKey.StartTheTrayAutomaticallyAtSignIn] = ("开机自动启动后台托盘", "Start the tray automatically at sign-in"),
            [LocKey.StartTheTrayAndRegisterTheGlobalHotkeyWhenYouSignInToWindowsWithoutOpeningTheManagerFirstIfYouMoveTheProgramFolderOpenTheManagerOnceToRefreshTheStartupEntryPath] = ("登录 Windows 后自动启动托盘并注册全局热键，无需先打开管理器。移动程序目录后打开一次管理器即可更新启动项路径。", "Start the tray and register the global hotkey when you sign in to Windows, without opening the manager first. If you move the program folder, open the manager once to refresh the startup entry path."),
            [LocKey.KeepTheTrayRunningWhenTheManagerCloses] = ("关闭管理器时保留后台托盘", "Keep the tray running when the manager closes"),
            [LocKey.TurningOffTheBackgroundTrayImmediatelyDisablesTheGlobalHotkeyYouCanNoLongerOpenATerminalWithItAndRemovesTheTrayMenuItComesBackOnlyWhenYouReopenTheManagerOrClickStartNow] = ("关闭后台托盘后，全局热键立即失效（无法用快捷键唤起终端），托盘菜单也会消失；下次打开管理器或点“立即启动”才会恢复。", "Turning off the background tray immediately disables the global hotkey (you can no longer open a terminal with it) and removes the tray menu. It comes back only when you reopen the manager or click \"Start now\"."),
            [LocKey.TrayStatus] = ("后台托盘状态", "Tray status"),
            [LocKey.StartNow] = ("立即启动", "Start now"),
            [LocKey.StopBackground] = ("结束后台运行", "Stop background"),
            [LocKey.SettingsFile] = ("设置文件", "Settings file"),
            [LocKey.RestoreDefaults] = ("恢复默认设置", "Restore defaults"),
            [LocKey.AllChangesAreWrittenToSettingsJsonImmediately] = ("所有更改都会立即写入 settings.json。", "All changes are written to settings.json immediately."),
            [LocKey.TheNewDefaultTerminalSPROFILEHasBeenLoadedUnsavedChangesWereDiscarded] = ("切换默认终端后已重新读取 $PROFILE，未保存的更改已丢弃。", "The new default terminal's $PROFILE has been loaded; unsaved changes were discarded."),
            [LocKey.SettingsSavedAndSyncedToTheTray] = ("设置已保存并同步到后台托盘。", "Settings saved and synced to the tray."),
            [LocKey.SettingsSavedTheTrayExecutableWasNotFoundSoTheHotkeyWillTakeEffectWhenItIsStarted] = ("设置已保存，但未找到后台托盘程序，热键将在下次启动托盘后生效。", "Settings saved. The tray executable was not found, so the hotkey will take effect when it is started."),
            [LocKey.FailedToSaveSettings0] = ("保存设置失败：{0}", "Failed to save settings: {0}"),
            [LocKey.FailedToOpenTheProfileFile0] = ("打开配置文件失败：{0}", "Failed to open the profile file: {0}"),
            [LocKey.CreatedAndOpenedTheProfileFile0] = ("已创建并打开配置文件：{0}", "Created and opened the profile file: {0}"),
            [LocKey.OpenedTheProfileFile0] = ("已打开配置文件：{0}", "Opened the profile file: {0}"),
            [LocKey.HotkeyAppliedAndSyncedToTheTray] = ("热键已应用并同步到后台托盘。", "Hotkey applied and synced to the tray."),
            [LocKey.HotkeyConflictsNotApplied] = ("热键冲突，未应用。", "Hotkey conflicts; not applied."),
            [LocKey.HotkeyConflict] = ("热键冲突", "Hotkey conflict"),
            [LocKey.UseThisHotkeyAnyway] = ("仍然使用这个热键吗？", "Use this hotkey anyway?"),
            [LocKey.RestoreAllSettingsToTheirDefaults] = ("确定要恢复全部默认设置吗？", "Restore all settings to their defaults?"),
            [LocKey.DefaultSettingsRestored] = ("已恢复默认设置。", "Default settings restored."),
            [LocKey.TheBackgroundTrayHasBeenStarted] = ("后台托盘已启动。", "The background tray has been started."),
            [LocKey.StartupEnabledTheTrayWillRunAutomaticallyWhenYouSignInToWindows] = ("已开启开机自动启动：登录 Windows 后会自动运行后台托盘。", "Startup enabled: the tray will run automatically when you sign in to Windows."),
            [LocKey.StartupDisabledTheTrayWillNotRunAutomaticallyAtYourNextSignIn] = ("已关闭开机自动启动：下次登录不会自动运行后台托盘。", "Startup disabled: the tray will not run automatically at your next sign-in."),
            [LocKey.SettingsSavedButTheStartupEntryCouldNotBeUpdatedCheckWhetherSecuritySoftwareOrASystemPolicyBlockedIt] = ("设置已保存，但开机启动项未能更新；请检查是否被安全软件或系统策略拦截。", "Settings saved, but the startup entry could not be updated. Check whether security software or a system policy blocked it."),
            [LocKey.CouldNotStartTheTrayCheckThatPAMTrayExeIsNextToTheManagerExecutable] = ("无法启动后台托盘，请检查 PAM.Tray.exe 是否与管理程序在同一目录。", "Could not start the tray. Check that PAM.Tray.exe is next to the manager executable."),
            [LocKey.TheBackgroundTrayWasAskedToExit] = ("已通知后台托盘退出。", "The background tray was asked to exit."),
            [LocKey.TheTrayMustReRegisterTheHotkeyItWasNotRunningSoItHasBeenStarted] = ("热键修改后需要后台托盘重新注册；当前托盘未运行，已为你启动。", "The tray must re-register the hotkey. It was not running, so it has been started."),
            [LocKey.HotkeyCannotBeEmpty] = ("热键不能为空。", "Hotkey cannot be empty."),
            [LocKey.AHotkeyNeedsAtLeastOneModifierAndOneRegularKey] = ("热键必须包含至少一个修饰键和一个普通按键。", "A hotkey needs at least one modifier and one regular key."),
            [LocKey.AHotkeyCanOnlyContainOneRegularKey] = ("热键只能包含一个普通按键。", "A hotkey can only contain one regular key."),
            [LocKey.IncludeAtLeastOneOfCtrlAltShiftWin] = ("热键必须包含 Ctrl、Alt、Shift 或 Win 中的至少一个修饰键。", "Include at least one of Ctrl / Alt / Shift / Win."),
            [LocKey.MissingRegularKey] = ("热键缺少普通按键。", "Missing regular key."),
            [LocKey.UnrecognizedKey0] = ("无法识别按键“{0}”。", "Unrecognized key \"{0}\"."),
            [LocKey.AModifierKeyCannotBeTheHotkeyKey] = ("不能把修饰键本身当作热键按键。", "A modifier key cannot be the hotkey key."),
            [LocKey.InvalidKey] = ("无效的按键。", "Invalid key."),
            [LocKey.UnsupportedKeyUseALetterDigitOrFunctionKeyInstead] = ("不支持该按键，请换一个字母、数字或功能键。", "Unsupported key. Use a letter, digit or function key instead."),
            [LocKey.CtrlAltDelIsReservedByTheWindowsSecureScreenAndCannotBeCaptured] = ("Ctrl+Alt+Del 由 Windows 安全界面独占，任何应用都无法接管。", "Ctrl+Alt+Del is reserved by the Windows secure screen and cannot be captured."),
            [LocKey.CtrlShiftEscOpensTaskManager] = ("Ctrl+Shift+Esc 用于打开任务管理器。", "Ctrl+Shift+Esc opens Task Manager."),
            [LocKey.CtrlEscOpensTheStartMenu] = ("Ctrl+Esc 用于打开开始菜单。", "Ctrl+Esc opens the Start menu."),
            [LocKey.AltTabSwitchesWindows] = ("Alt+Tab 用于切换窗口。", "Alt+Tab switches windows."),
            [LocKey.AltShiftTabSwitchesWindowsInReverseOrder] = ("Alt+Shift+Tab 用于反向切换窗口。", "Alt+Shift+Tab switches windows in reverse order."),
            [LocKey.AltF4ClosesTheCurrentWindow] = ("Alt+F4 用于关闭当前窗口。", "Alt+F4 closes the current window."),
            [LocKey.AltEscCyclesThroughWindows] = ("Alt+Esc 用于按窗口顺序切换。", "Alt+Esc cycles through windows."),
            [LocKey.WinLLocksTheComputer] = ("Win+L 用于锁定计算机。", "Win+L locks the computer."),
            [LocKey.WinEnterStartsWindowsNarrator] = ("Win+Enter 用于启动 Windows 讲述人。", "Win+Enter starts Windows Narrator."),
            [LocKey.WinTabOpensTaskView] = ("Win+Tab 用于打开任务视图。", "Win+Tab opens Task View."),
            [LocKey.WinDShowsTheDesktop] = ("Win+D 用于显示桌面。", "Win+D shows the desktop."),
            [LocKey.WinEOpensFileExplorer] = ("Win+E 用于打开文件资源管理器。", "Win+E opens File Explorer."),
            [LocKey.WinROpensTheRunDialog] = ("Win+R 用于打开“运行”对话框。", "Win+R opens the Run dialog."),
            [LocKey.WinIOpensWindowsSettings] = ("Win+I 用于打开 Windows 设置。", "Win+I opens Windows Settings."),
            [LocKey.WinSWinQOpensWindowsSearch] = ("Win+S / Win+Q 用于 Windows 搜索。", "Win+S / Win+Q opens Windows Search."),
            [LocKey.WinVOpensClipboardHistory] = ("Win+V 用于打开剪贴板历史。", "Win+V opens clipboard history."),
            [LocKey.WinKOpensTheConnectPane] = ("Win+K 用于打开“连接”面板。", "Win+K opens the Connect pane."),
            [LocKey.WinPSwitchesProjectionModes] = ("Win+P 用于切换投影模式。", "Win+P switches projection modes."),
            [LocKey.WinAOpensActionCenter] = ("Win+A 用于打开操作中心。", "Win+A opens Action Center."),
            [LocKey.WinGOpensXboxGameBar] = ("Win+G 用于打开 Xbox Game Bar。", "Win+G opens Xbox Game Bar."),
            [LocKey.WinUOpensEaseOfAccessSettings] = ("Win+U 用于打开轻松使用设置。", "Win+U opens Ease of Access settings."),
            [LocKey.WinXOpensTheQuickLinkMenu] = ("Win+X 用于打开快速链接菜单。", "Win+X opens the Quick Link menu."),
            [LocKey.WinTBrowsesTaskbarApps] = ("Win+T 用于浏览任务栏程序。", "Win+T browses taskbar apps."),
            [LocKey.WinPrintScreenCapturesAndSavesAScreenshot] = ("Win+PrintScreen 用于系统截图并保存。", "Win+PrintScreen captures and saves a screenshot."),
            [LocKey.WinWinControlsTheMagnifier] = ("Win++ / Win+- 用于打开或关闭放大镜。", "Win++ / Win+- controls the Magnifier."),
            [LocKey.WinShiftSIsUsedByTheWindowsSnippingTool] = ("Win+Shift+S 由 Windows 截图工具占用。", "Win+Shift+S is used by the Windows snipping tool."),
            [LocKey.ThisCombinationManagesWindowsVirtualDesktops] = ("该组合用于 Windows 虚拟桌面管理。", "This combination manages Windows virtual desktops."),
            [LocKey.WinAltRIsUsedByXboxGameBarRecording] = ("Win+Alt+R 由 Xbox Game Bar 录制功能占用。", "Win+Alt+R is used by Xbox Game Bar recording."),
            [LocKey.CtrlSIsSaveInAlmostEveryAppAGlobalHotkeyWouldInterceptIt] = ("Ctrl+S 是几乎所有软件的“保存”，注册为全局热键后会被拦截。", "Ctrl+S is Save in almost every app; a global hotkey would intercept it."),
            [LocKey.CtrlNIsACommonNewShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+N 是常见的“新建”，注册为全局热键后会被拦截。", "Ctrl+N is a common New shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlOIsACommonOpenShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+O 是常见的“打开”，注册为全局热键后会被拦截。", "Ctrl+O is a common Open shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlCIsTheSystemCopyShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+C 是系统级“复制”，注册为全局热键后会被拦截。", "Ctrl+C is the system Copy shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlVIsTheSystemPasteShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+V 是系统级“粘贴”，注册为全局热键后会被拦截。", "Ctrl+V is the system Paste shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlXIsTheSystemCutShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+X 是系统级“剪切”，注册为全局热键后会被拦截。", "Ctrl+X is the system Cut shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlAIsACommonSelectAllShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+A 是常见的“全选”，注册为全局热键后会被拦截。", "Ctrl+A is a common Select All shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlZIsACommonUndoShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+Z 是常见的“撤销”，注册为全局热键后会被拦截。", "Ctrl+Z is a common Undo shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlPIsACommonPrintShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+P 是常见的“打印”，注册为全局热键后会被拦截。", "Ctrl+P is a common Print shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlFIsACommonFindShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+F 是常见的“查找”，注册为全局热键后会被拦截。", "Ctrl+F is a common Find shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlWIsACommonCloseTabShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+W 是常见的“关闭标签页”，注册为全局热键后会被拦截。", "Ctrl+W is a common Close Tab shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlTIsACommonNewTabShortcutAGlobalHotkeyWouldInterceptIt] = ("Ctrl+T 是常见的“新建标签页”，注册为全局热键后会被拦截。", "Ctrl+T is a common New Tab shortcut; a global hotkey would intercept it."),
            [LocKey.CtrlShiftTReopensClosedBrowserTabsAGlobalHotkeyWouldInterceptIt] = ("Ctrl+Shift+T 是浏览器“恢复关闭的标签页”，注册为全局热键后会被拦截。", "Ctrl+Shift+T reopens closed browser tabs; a global hotkey would intercept it."),
            [LocKey.ScreenshotToolsSuchAsQQWeChatOftenUseCtrlAltA] = ("QQ / 微信等截图工具常使用 Ctrl+Alt+A。", "Screenshot tools such as QQ / WeChat often use Ctrl+Alt+A."),
            [LocKey.SomeTerminalToolsAndWSLLaunchersUseCtrlAltT] = ("部分终端工具或 Linux 子系统使用 Ctrl+Alt+T。", "Some terminal tools and WSL launchers use Ctrl+Alt+T."),
            [LocKey.ScreenshotToolsAndSomeIDEsOftenUseCtrlShiftA] = ("截图工具与部分 IDE 常使用 Ctrl+Shift+A。", "Screenshot tools and some IDEs often use Ctrl+Shift+A."),
            [LocKey.ScreenshotOrInputMethodToolsOftenUseCtrlAltS] = ("截图或输入法工具常使用 Ctrl+Alt+S。", "Screenshot or input-method tools often use Ctrl+Alt+S."),
            [LocKey.ChineseInputMethodsOftenUseCtrlSpaceToSwitchLanguages] = ("中文输入法的中英文切换常使用 Ctrl+Space。", "Chinese input methods often use Ctrl+Space to switch languages."),
            [LocKey.AltSpaceOpensTheWindowsWindowSystemMenu] = ("Windows 窗口系统菜单常使用 Alt+Space。", "Alt+Space opens the Windows window system menu."),
            [LocKey.ThisCombinationIsAlreadyTakenByWindowsOrAnotherRunningAppPickAnotherOne] = ("该组合已被系统或其他常驻软件占用，请换一个。", "This combination is already taken by Windows or another running app. Pick another one."),
            [LocKey.HotkeyIsAlreadyInUse] = ("热键被占用", "Hotkey is already in use"),
            [LocKey.Exit0] = ("确定要退出 {0} 吗？", "Exit {0}?"),
            [LocKey.MainAppNotFound0] = ("未找到主界面程序：{0}", "Main app not found: {0}"),
            [LocKey.FailedToStartTheMainApp0] = ("启动主界面失败：{0}", "Failed to start the main app: {0}"),
            [LocKey.FailedToStartWindowsTerminal0] = ("启动 Windows Terminal 失败：{0}", "Failed to start Windows Terminal: {0}"),
            [LocKey.ReloadSettings] = ("重新加载设置", "Reload settings"),
            [LocKey.Hotkey0] = ("热键：{0}", "Hotkey: {0}"),
            [LocKey.CouldNotRegisterHotkey0ItMayBeTakenByWindowsOrAnotherAppChangeItInSettings] = ("热键 {0} 注册失败，可能已被系统或其他软件占用。请到“设置”中更换。", "Could not register hotkey {0}. It may be taken by Windows or another app. Change it in Settings."),
            [LocKey.SettingsReloadedHotkey0] = ("设置已重新加载：热键 {0}", "Settings reloaded: hotkey {0}"),
            [LocKey.Beautify] = ("美化", "Beautify"),
            [LocKey.ConfigureTheStarshipPromptAndNerdFontsForYourTerminal] = ("为终端配置 Starship 提示符与 Nerd Font 字体。", "Configure the Starship prompt and Nerd Fonts for your terminal."),
            [LocKey.Detecting] = ("检测中…", "Detecting…"),
            [LocKey.NotInstalled] = ("未安装", "Not installed"),
            [LocKey.Installed0] = ("已安装 {0}", "Installed {0}"),
            [LocKey.ScoopNotDetected] = ("未检测到 Scoop", "Scoop not detected"),
            [LocKey.ScoopIsReady] = ("Scoop 已就绪", "Scoop is ready"),
            [LocKey.StarshipIsACrossShellPromptEngineOnceInstalledViaScoopItWorksOutOfTheBoxNoPATHSetupNeeded] = ("Starship 用来美化命令行提示符；装好后就能用，不用手动配置。", "Starship styles the command-line prompt. Once installed, it works without manual setup."),
            [LocKey.StarshipSDocsRecommendInstallingANerdFontFirstPickAFontAboveBeforeInstalling] = ("建议先装好图标字体，否则主题里的符号可能显示成方块。", "Install the icon font first, otherwise theme symbols may show as boxes."),
            [LocKey.AlreadyInstalledNoNeedToInstallAgainReopenYourTerminalAfterChangingFontsToTakeEffect] = ("已经装好了；换字体后重新打开终端生效。", "Already installed. Reopen the terminal after changing the font."),
            [LocKey.DisabledUntilAFontIsSelectedUnavailableWhenStarshipIsAlreadyInstalled] = ("未选字体或已安装时不可用。", "Disabled until a font is selected; unavailable when Starship is already installed."),
            [LocKey.OneClickInstallInclScoop] = ("一键安装（含 Scoop）", "One-click install (incl. Scoop)"),
            [LocKey.ReDetect] = ("重新检测", "Re-detect"),
            [LocKey.InstallingScoopAndStarshipThisMayTakeAFewMinutes] = ("正在安装 Scoop 与 Starship，可能需要几分钟…", "Installing Scoop and Starship. This may take a few minutes…"),
            [LocKey.InstallingScoop] = ("正在安装 Scoop…", "Installing Scoop…"),
            [LocKey.ScoopInstalled] = ("Scoop 安装完成。", "Scoop installed."),
            [LocKey.StarshipInstalled] = ("Starship 安装完成。", "Starship installed."),
            [LocKey.ScoopWasNotDetectedInstallScoopFirst] = ("未检测到 Scoop，请先安装 Scoop。", "Scoop was not detected. Install Scoop first."),
            [LocKey.ScoopInstallStarshipFailedExitCode01] = ("scoop install starship 失败（退出码 {0}）。{1}", "scoop install starship failed (exit code {0}). {1}"),
            [LocKey.ScoopInstallationFailedExitCode01] = ("Scoop 安装失败（退出码 {0}）。{1}", "Scoop installation failed (exit code {0}). {1}"),
            [LocKey.Done] = ("操作完成。", "Done."),
            [LocKey.OperationFailed0] = ("操作失败：{0}", "Operation failed: {0}"),
            [LocKey.ConfigureTerminalAppearanceTheStarshipPromptAndNerdFontsASnapshotOfTheOriginalTerminalConfigIsSavedBeforeTheFirstChangeAndCanBeRestoredAtAnyTime] = ("配置终端外观、Starship 提示符与 Nerd Font；首次修改终端配置前会自动保存一份原始配置，随时可恢复。", "Configure terminal appearance, the Starship prompt and Nerd Fonts. A snapshot of the original terminal config is saved before the first change and can be restored at any time."),
            [LocKey.MapsToWindowsTerminalSDefaultsAppearanceAndWritesProfilesDefaultsInSettingsJsonOnlyChangedItemsAreSavedLeaveAFieldEmptyOrPickTerminalDefaultToHandItBackToTheTerminal] = ("只保存改动过的项目；留空或选「终端默认」就恢复系统默认。", "Only changed items are saved. Leave a field empty or choose Terminal default to restore the system default."),
            [LocKey.Text] = ("文本", "Text"),
            [LocKey.Font] = ("字体", "Font"),
            [LocKey.WritesProfilesDefaultsFontFaceCascadiaCodeNFShipsWithThisAppInstallItForTheCurrentUserWithOneClickIfMissing] = ("内置图标字体已随程序打包，安装时不需要管理员权限。", "The bundled icon font comes with the app and installs without administrator rights."),
            [LocKey.Effects] = ("显示效果", "Effects"),
            [LocKey.RetroTerminalEffect] = ("复古风格终端显示", "Retro terminal effect"),
            [LocKey.CRTScanlinesAndGlowExperimentalRetroTerminalEffect] = ("模拟老式显示器的扫描线和光晕。", "Simulates the scan lines and glow of an old monitor."),
            [LocKey.EnableRetroEffect] = ("启用复古效果", "Enable retro effect"),
            [LocKey.Cursor] = ("光标", "Cursor"),
            [LocKey.CursorShape] = ("光标形状", "Cursor shape"),
            [LocKey.MapsToCursorShapePickTerminalDefaultToRemoveTheSetting] = ("选「终端默认」表示不特别设置。", "Choose Terminal default to leave this unset."),
            [LocKey.CursorColor] = ("光标颜色", "Cursor color"),
            [LocKey.MapsToCursorColorEnterRGBOrRRGGBBLeaveEmptyToUseTheColorSchemeSCursorColor] = ("填 #RGB 或 #RRGGBB；留空跟随系统配色。", "Enter #RGB or #RRGGBB; leave empty to follow the system colors."),
            [LocKey.ForExampleFFFFFFLeaveEmptyToLeaveItUnset] = ("例如 #FFFFFF；留空表示不设置", "For example #FFFFFF; leave empty to leave it unset"),
            [LocKey.BackgroundImage] = ("背景图", "Background image"),
            [LocKey.BackgroundImagePath] = ("背景图路径", "Background image path"),
            [LocKey.MapsToBackgroundImageDesktopWallpaperAndMsAppdataURIsAlsoWorkClearTheFieldToRemoveTheImage] = ("选一张本地图片；清空就取消背景图。", "Pick a local image; clear the field to remove the background."),
            [LocKey.Clear] = ("清空", "Clear"),
            [LocKey.RemoveTheBackgroundImageSetting] = ("移除背景图设置", "Remove the background image setting"),
            [LocKey.BackgroundImageOpacity] = ("背景图透明度", "Background image opacity"),
            [LocKey.MapsToBackgroundImageOpacity01ShownHereAsAPercentage] = ("数值越大，背景图越清晰。", "Higher values make the background image more visible."),
            [LocKey.Window] = ("窗口", "Window"),
            [LocKey.WindowOpacity] = ("窗口透明度", "Window opacity"),
            [LocKey.MapsToOpacity0100Where100IsFullyOpaqueSetItBelow100ToSeeTheEffect] = ("100 是完全不透明，调低才能看出效果。", "100 is fully opaque; lower it to see the effect."),
            [LocKey.AcrylicTransparency] = ("窗口透明亚克力材质", "Acrylic transparency"),
            [LocKey.MapsToUseAcrylicEnablingItBlursTheTransparentBackgroundBestOnWindows11] = ("让透明背景带一点模糊，Windows 11 上效果最好。", "Blurs the transparent background; works best on Windows 11."),
            [LocKey.UseAcrylic] = ("使用亚克力材质", "Use acrylic"),
            [LocKey.DiscardChanges] = ("放弃更改", "Discard changes"),
            [LocKey.SaveAppearance] = ("保存外观设置", "Save appearance"),
            [LocKey.RestoreWindowsTerminalBackupTheFirstTimeThisAppChangesTerminalSettingsFontAppearanceItSavesTheWholeSettingsJsonAsSettingsJsonBakLaterChangesNeverOverwriteItSoRestoringAlwaysReturnsToTheConfigFromBeforeThisAppWasInstalledTheBackupIsKeptAndCanBeRestoredRepeatedly] = ("第一次改终端设置时会备份原始配置，点这里可还原；备份不会被覆盖。", "A snapshot is kept before the first terminal change; click here to restore it. The snapshot is never overwritten."),
            [LocKey.ReturnToTheSettingsJsonFromBeforeThisAppSFirstChangeTheOriginalBackupIsNeverOverwrittenOrDeletedSoItCanBeRestoredRepeatedly] = ("还原到第一次修改前的样子；备份会保留。", "Restore the original settings; the snapshot is kept."),
            [LocKey.OriginalBackupCreatedAt0RestoringAlwaysReturnsToThisConfig] = ("原始配置备份创建于 {0}；恢复时永远回到这份配置。", "Original backup created at {0}. Restoring always returns to this config."),
            [LocKey.NoOriginalBackupYetItIsCreatedAutomaticallyTheFirstTimeThisAppChangesTerminalSettings] = ("还没有原始配置备份；第一次通过本程序修改终端配置时会自动生成。", "No original backup yet. It is created automatically the first time this app changes terminal settings."),
            [LocKey.BackupFile0] = ("备份文件：{0}", "Backup file: {0}"),
            [LocKey.RestoredTheWindowsTerminalConfigFromBeforeThisAppSFirstChangeTheOriginalBackupIsKeptAndCanBeRestoredAgain] = ("已还原到第一次修改前的终端配置；备份保留，可再次还原。", "Original terminal settings restored; the snapshot is kept and can be reused."),
            [LocKey.UnsavedChangesSaveThemThenOpenANewTerminalWindowToSeeTheEffect] = ("有未保存的改动；点「保存设置」后打开新终端窗口生效。", "Unsaved changes. Click Save settings, then open a new terminal window to see the effect."),
            [LocKey.ShowingTheValuesSavedInSettingsJsonClickSaveAppearanceAfterMakingChanges] = ("当前显示的是已保存的设置；改动后点「保存设置」。", "Showing the saved settings. Click Save settings after making changes."),
            [LocKey.SavingTerminalAppearanceSettings] = ("正在保存终端外观设置…", "Saving terminal appearance settings…"),
            [LocKey.NothingToSave] = ("没有需要保存的更改。", "Nothing to save."),
            [LocKey.CursorColorMustBeRGBOrRRGGBBLeaveEmptyToUseTheTerminalDefault] = ("光标颜色要写成 #RGB 或 #RRGGBB；留空表示使用终端默认颜色。", "Cursor color must be #RGB or #RRGGBB; leave empty to use the terminal default."),
            [LocKey.Saved0TerminalAppearanceSettingSOpenANewTerminalWindowToSeeTheEffect] = ("已保存 {0} 项终端外观设置，打开新终端窗口即可看到效果。", "Saved {0} terminal appearance setting(s). Open a new terminal window to see the effect."),
            [LocKey.ChooseATerminalBackgroundImage] = ("选择终端背景图", "Choose a terminal background image"),
            [LocKey.ImageFilesPngJpgJpegGifBmpPngJpgJpegGifBmpAllFiles] = ("图片文件 (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|所有文件 (*.*)|*.*", "Image files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files (*.*)|*.*"),
            [LocKey.TerminalDefault] = ("终端默认", "Terminal default"),
            [LocKey.Bar] = ("竖线 (bar)", "Bar"),
            [LocKey.VintageBlock] = ("复古粗块 (vintage)", "Vintage block"),
            [LocKey.Underscore] = ("下划线 (underscore)", "Underscore"),
            [LocKey.FilledBox] = ("实心框 (filledBox)", "Filled box"),
            [LocKey.EmptyBox] = ("空心框 (emptyBox)", "Empty box"),
            [LocKey.DoubleUnderscore] = ("双下划线 (doubleUnderscore)", "Double underscore"),
            [LocKey.UnknownAppearanceSetting] = ("未知的外观设置项。", "Unknown appearance setting."),
            [LocKey.TheAppearanceValueIsNotAValidJSONLiteral] = ("外观设置的值不是合法的 JSON 字面量。", "The appearance value is not a valid JSON literal."),
            [LocKey.AToggleCanOnlyBeTrueOrFalse] = ("开关值只能是 true / false。", "A toggle can only be true or false."),
            [LocKey.InvalidCursorShape] = ("光标形状不合法。", "Invalid cursor shape."),
            [LocKey.UnsupportedCursorShape] = ("不支持的光标形状。", "Unsupported cursor shape."),
            [LocKey.CursorColorMustBeRGBOrRRGGBB] = ("光标颜色要写成 #RGB 或 #RRGGBB。", "Cursor color must be #RGB or #RRGGBB."),
            [LocKey.TheBackgroundImagePathCannotBeEmpty] = ("背景图路径不能为空。", "The background image path cannot be empty."),
            [LocKey.BackgroundImageOpacityMustBeBetween0And1] = ("背景图透明度必须在 0 ~ 1 之间。", "Background image opacity must be between 0 and 1."),
            [LocKey.WindowOpacityMustBeAnIntegerBetween0And100] = ("窗口透明度必须是 0 ~ 100 的整数。", "Window opacity must be an integer between 0 and 100."),
            [LocKey.ATargetLevelInSettingsJsonIsNotAnObjectCannotModifyAutomaticallyEditItManually] = ("settings.json 的目标层级不是对象，无法自动修改，请手动调整。", "A target level in settings.json is not an object; cannot modify automatically. Edit it manually."),
            [LocKey.ATargetPropertyInSettingsJsonIsNotASimpleValueOrHasAnUnexpectedStructureEditItManually] = ("settings.json 的目标属性不是简单值或结构异常，无法自动修改，请手动调整。", "A target property in settings.json is not a simple value or has an unexpected structure; edit it manually."),
            [LocKey.TheAppearanceSettingPathIsEmpty] = ("外观设置的属性路径为空。", "The appearance setting path is empty."),
            [LocKey.WindowsTerminalCurrentFont0] = ("Windows Terminal 当前字体：{0}", "Windows Terminal current font: {0}"),
            [LocKey.WindowsTerminalCurrentFontNotSetUsingTheWTDefault] = ("Windows Terminal 当前字体：未设置（跟随 WT 默认）", "Windows Terminal current font: not set (using the WT default)"),
            [LocKey.InstallBundledFont] = ("安装内置NF字体", "Install bundled NF font"),
            [LocKey.InstallsTheBundledCascadiaCodeNFForTheCurrentUserNoAdminRightsNeeded] = ("把打包的 Cascadia Code NF 安装为当前用户字体（免管理员权限）。", "Installs the bundled Cascadia Code NF for the current user (no admin rights needed)."),
            [LocKey.WantOtherNerdFontsWithScoopInstalledRunScoopBucketAddNerdFontsThenInstallEGJetBrainsMonoNFOrFiraCodeNFReopenThisTabAndTheyWillAppearInTheList] = ("想用别的图标字体？装好 Scoop 后执行 scoop bucket add nerd-fonts，再安装喜欢的字体，点「刷新状态」就能在下拉里看到。", "Want a different icon font? After installing Scoop, run scoop bucket add nerd-fonts, install a font, then click Refresh to see it in the list."),
            [LocKey.InstallingTheBundledFont] = ("正在安装内置 NF 字体…", "Installing the bundled NF font…"),
            [LocKey.WindowsTerminalSettingsJsonWasNotFoundStartWindowsTerminalOnceFirst] = ("未找到 Windows Terminal 的 settings.json，请先启动一次 Windows Terminal。", "Windows Terminal settings.json was not found. Start Windows Terminal once first."),
            [LocKey.FontFileNotFound0] = ("未找到字体文件：{0}", "Font file not found: {0}"),
            [LocKey.OnlyTtfFontFilesAreSupported] = ("只支持 .ttf 字体文件。", "Only .ttf font files are supported."),
            [LocKey.FailedToInstallTheFont0] = ("安装字体失败：{0}", "Failed to install the font: {0}"),
            [LocKey.Installed0ForTheCurrentUser] = ("已为当前用户安装 {0}。", "Installed {0} for the current user."),
            [LocKey.TheFontNameCannotBeEmpty] = ("字体名称不能为空。", "The font name cannot be empty."),
            [LocKey.ThemePreset] = ("主题预设", "Theme preset"),
            [LocKey.PresetsMarkedAsNeedingANerdFontRequireTheFontToBeInstalledFirstOtherwiseThePromptShowsPlaceholderBoxes] = ("标了「需 Nerd Font」的主题要先装好字体，否则符号会显示成方块。", "Themes marked as needing a Nerd Font require the font to be installed first, or symbols appear as boxes."),
            [LocKey.ApplyTheme] = ("应用主题", "Apply theme"),
            [LocKey.ApplyingTheThemePreset] = ("正在应用主题预设…", "Applying the theme preset…"),
            [LocKey.ThemeApplied0TheOriginalStarshipTomlHasBeenBackedUp] = ("主题已应用：{0}；原 starship.toml 已自动备份。", "Theme applied: {0}. The original starship.toml has been backed up."),
            [LocKey.ThemeApplied0ThisPresetNeedsANerdFontWhichIsNotInstalledYetThePromptMayShowPlaceholderBoxesInstallTheBundledFontFirst] = ("主题已应用：{0}。该预设需要 Nerd Font，当前未安装内置字体，提示符可能显示乱码方块，建议先安装。", "Theme applied: {0}. This preset needs a Nerd Font, which is not installed yet; the prompt may show placeholder boxes. Install the bundled font first."),
            [LocKey.SelectAThemePresetFirst] = ("请先选择一个主题预设。", "Select a theme preset first."),
            [LocKey.BundledThemePresetNotFound0] = ("未找到内置主题预设：{0}", "Bundled theme preset not found: {0}"),
            [LocKey.ConfigMaintenance] = ("配置维护", "Config maintenance"),
            [LocKey.Created] = ("已创建", "Created"),
            [LocKey.NotCreated] = ("未创建", "Not created"),
            [LocKey.OpenStarshipToml] = ("打开 starship.toml", "Open starship.toml"),
            [LocKey.RestoreStarshipTomlBackup] = ("恢复上次主题设置", "Restore last theme settings"),
            [LocKey.RestoreWindowsTerminalBackup] = ("恢复终端备份", "Restore terminal backup"),
            [LocKey.OpeningStarshipToml] = ("正在打开 starship.toml…", "Opening starship.toml…"),
            [LocKey.RestoringTheStarshipTomlBackup] = ("正在恢复主题设置…", "Restoring theme settings…"),
            [LocKey.StarshipTomlHasBeenRestoredFromTheBackup] = ("已恢复上一次的主题设置。", "Previous theme settings restored."),
            [LocKey.RestoringTheWindowsTerminalBackup] = ("正在恢复终端备份…", "Restoring the terminal backup…"),
            [LocKey.NoBackupToRestore] = ("没有可恢复的备份。", "No backup to restore."),
            [LocKey.FailedToRefreshTheStatus0] = ("状态刷新失败：{0}", "Failed to refresh the status: {0}"),
            [LocKey.TheRootOfSettingsJsonIsNotAnObjectCannotModifyAutomatically] = ("settings.json 的顶层不是对象，无法自动修改。", "The root of settings.json is not an object; cannot modify automatically."),
            [LocKey.ProfilesInSettingsJsonIsNotAnObjectPossiblyALegacyArrayLayoutCannotModifyAutomaticallyEditItManually] = ("settings.json 的 profiles 不是对象（可能是旧版数组结构），无法自动修改，请手动调整。", "profiles in settings.json is not an object (possibly a legacy array layout); cannot modify automatically. Edit it manually."),
            [LocKey.UnexpectedProfilesStructureInSettingsJsonCannotModifyAutomatically] = ("settings.json 的 profiles 结构异常，无法自动修改。", "Unexpected profiles structure in settings.json; cannot modify automatically."),
            [LocKey.ProfilesDefaultsInSettingsJsonIsNotAnObjectCannotModifyAutomatically] = ("settings.json 的 profiles.defaults 不是对象，无法自动修改。", "profiles.defaults in settings.json is not an object; cannot modify automatically."),
            [LocKey.UnexpectedProfilesDefaultsStructureInSettingsJsonCannotModifyAutomatically] = ("settings.json 的 profiles.defaults 结构异常，无法自动修改。", "Unexpected profiles.defaults structure in settings.json; cannot modify automatically."),
            [LocKey.ProfilesDefaultsFontIsNotAnObjectCannotModifyAutomatically] = ("profiles.defaults.font 不是对象，无法自动修改。", "profiles.defaults.font is not an object; cannot modify automatically."),
            [LocKey.UnexpectedProfilesDefaultsFontStructureInSettingsJsonCannotModifyAutomatically] = ("profiles.defaults.font 结构异常，无法自动修改。", "Unexpected profiles.defaults.font structure in settings.json; cannot modify automatically."),
            [LocKey.CannotLocateProfilesDefaultsFontFaceEditTheFontSettingManually] = ("无法定位 profiles.defaults.font.face，请手动修改字体设置。", "Cannot locate profiles.defaults.font.face. Edit the font setting manually."),
            [LocKey.UnexpectedSettingsJsonStructureCannotModifyAutomatically] = ("settings.json 结构异常，无法自动修改。", "Unexpected settings.json structure; cannot modify automatically."),
            [LocKey.PostWriteValidationFailedTheOriginalFileWasKeptUnchanged] = ("写入后校验未通过，已保持原配置不变。", "Post-write validation failed; the original file was kept unchanged."),
            [LocKey.CustomPrompt] = ("自定义提示符", "Custom prompt"),
            [LocKey.ThePreviewUsesSampleValuesStarshipGeneratesTheRealTextFromSystemStateThePathFollowsCd] = ("预览是示例文字，实际内容会随目录、时间等自动变化。", "The preview shows sample text; the real values change with the directory, time and so on."),
            [LocKey.InsertVariable] = ("插入变量", "Insert variable"),
            [LocKey.CurrentColor] = ("当前颜色", "Current color"),
            [LocKey.Space] = ("空格", "Space"),
            [LocKey.Username] = ("用户名", "Username"),
            [LocKey.Hostname] = ("主机名", "Hostname"),
            [LocKey.Colon] = ("冒号", "Colon"),
            [LocKey.Tail] = ("结束符", "Tail"),
            [LocKey.ResetToDefaults] = ("重置为默认", "Reset to defaults"),
            [LocKey.RestorePreApplyConfig] = ("恢复应用前配置", "Restore pre-apply config"),
            [LocKey.ApplyToStarship] = ("应用到 Starship", "Apply to Starship"),
            [LocKey.StarshipTomlDoesNotExistYetApplyingWillCreateIt] = ("starship.toml 尚未创建，应用后会自动生成", "starship.toml does not exist yet; applying will create it"),
            [LocKey.ThePromptIsCurrentlyManagedByPowerShellManager] = ("当前提示符由 PowerShell 管理器接管", "The prompt is currently managed by PowerShell Manager"),
            [LocKey.UsingTheOriginalPromptCustomPromptNotAppliedYet] = ("当前使用原有提示符配置（尚未应用过自定义）", "Using the original prompt (custom prompt not applied yet)"),
            [LocKey.WritingTheCustomPrompt] = ("正在写入自定义提示符…", "Writing the custom prompt..."),
            [LocKey.CustomPromptWrittenToStarshipTomlWithASeparateBackupOfTheOriginalConfigOpenANewTerminalToSeeIt] = ("自定义提示符已写入 starship.toml，原配置已单独备份；打开新终端即可看到效果。", "Custom prompt written to starship.toml with a separate backup of the original config. Open a new terminal to see it."),
            [LocKey.RestoringThePreApplyPromptConfig] = ("正在恢复应用前的提示符配置…", "Restoring the pre-apply prompt config..."),
            [LocKey.RestoredTheOriginalStarshipTomlTheCustomPromptHasBeenRemoved] = ("已恢复应用前的 starship.toml，自定义提示符已移除。", "Restored the original starship.toml; the custom prompt has been removed."),
            [LocKey.NoPreApplyBackupToRestore] = ("没有可恢复的应用前备份。", "No pre-apply backup to restore."),
            [LocKey.DefaultsRestoredInTheEditorClickApplyToStarshipToWriteThem] = ("已恢复默认提示符，点「保存设置」才会写入。", "Default prompt restored. Click Save settings to apply."),
            [LocKey.BlankLineBeforePrompt] = ("提示符前空一行", "Blank line before prompt"),
            [LocKey.UseAsPathSeparator] = ("路径分隔符用 /", "Use / as path separator"),
            [LocKey.WhenEnabledAnEmptyLineIsInsertedBeforeEveryPromptStarshipAddNewline] = ("开启后，每条命令前会多一个空行。", "When enabled, a blank line is added before each command."),
            [LocKey.WhenDisabledPathsUseTheSystemBackslashSeparatorStarshipUseOsPathSep] = ("关闭后，路径分隔符跟随系统默认。", "When turned off, paths use the system default separator."),
            [LocKey.AdvancedOptions] = ("高级选项", "Advanced options"),
            [LocKey.BeautifyPageIntro] = ("设置终端字体、外观和命令行提示符。第一次修改前会自动备份，随时可以还原；点「刷新状态」重新检测。", "Configure the terminal font, appearance and prompt. A backup is made before the first change and can be restored at any time; click Refresh to re-detect."),
            [LocKey.PromptEditorIntro] = ("点「添加片段」从左到右拼出提示符：每段用下拉选内容（用户名、目录、符号等）和颜色。下面的预览会实时更新；应用后只改提示符这一行，其它设置不动。", "Click Add segment to build the prompt from left to right: choose the content (user, directory, symbols, etc.) and a color for each segment. The preview updates live; applying changes only the prompt line."),
            [LocKey.PromptSegmentHint] = ("下拉里能选变量和符号；颜色选预设，或选「自定义…」后填 6 位色号。", "Pick variables and symbols from the list; choose a preset color or enter a 6-digit hex value via Custom."),
            [LocKey.TomlBackupHint] = ("应用主题前会自动备份，点这里还原上次的配置。", "A backup is made before applying a theme; click here to restore the previous one."),
            [LocKey.SaveSettings] = ("保存设置", "Save settings"),
            [LocKey.SavingSettings] = ("正在保存设置…", "Saving settings…"),
            [LocKey.SettingsSavedThemeAndAppearance] = ("已保存提示符设置与 {0} 项外观改动；打开新终端窗口即可看到效果。", "Prompt settings and {0} appearance change(s) saved. Open a new terminal window to see the effect."),
            [LocKey.SettingsSavedThemeOnly] = ("已保存提示符设置；打开新终端窗口即可看到效果。", "Prompt settings saved. Open a new terminal window to see the effect."),
            [LocKey.ThemeAppliedButAppearanceFailed] = ("提示符设置已保存，但外观保存失败：{0}", "Prompt settings saved, but appearance could not be saved: {0}"),
            [LocKey.DiscardAppearanceTooltip] = ("回到上次保存的状态，不写入文件。", "Back to the last saved state; nothing is written."),
            [LocKey.SaveSettingsTooltip] = ("按当前方案写入提示符，并保存字体与外观改动。", "Writes the prompt for the selected scheme and saves font and appearance changes."),
            [LocKey.ClickSaveSettingsAbove] = ("改完后点下面的「保存设置」。", "Click Save settings below when you are done."),
            [LocKey.SettingsSavedMissingFontWarning] = ("已保存设置，但没装图标字体，符号可能显示成方块。", "Settings saved, but the icon font is missing; symbols may show as boxes."),
            [LocKey.CustomEllipsis] = ("自定义…", "Custom…"),
            [LocKey.RefreshStatus] = ("刷新状态", "Refresh"),
            [LocKey.RefreshStatusTooltip] = ("重新探测 Starship、Scoop、字体与终端配置状态。", "Re-detect Starship, Scoop, fonts and terminal settings."),
            [LocKey.PromptFormatLabel] = ("格式", "Format"),
            [LocKey.CustomSegmentTooltip] = ("输入任意文字或 $变量", "Enter any text"),
            [LocKey.HexColorTooltip] = ("输入 6 位十六进制色号（即 # 后面的部分）", "Enter 6 hex digits (without #)"),
            [LocKey.RemoveSegmentTooltip] = ("删除该片段", "Remove this segment"),
            [LocKey.AddSegment] = ("添加片段", "Add segment"),
            [LocKey.PromptSchemeHint] = ("两种方案二选一，切换后点下方的「保存设置」生效。", "Choose one of the two schemes; click Save settings below to apply."),
            [LocKey.RestoreDefaultsTooltip] = ("把当前方案恢复成程序默认，点「保存设置」后才写入。", "Restores the current scheme to its defaults. Click Save settings to write."),
            [LocKey.DefaultThemeRestored] = ("已恢复默认主题，点「保存设置」才会写入。", "Default theme restored. Click Save settings to apply."),
            [LocKey.ChangesDiscarded] = ("已放弃未保存的改动，回到上次保存的状态。", "Unsaved changes discarded; back to the last saved state."),
            [LocKey.AddSegmentFirst] = ("请先添加至少一个片段。", "Add at least one segment first."),
            [LocKey.CustomPromptSimpleStatus] = ("当前主题：自定义提示符", "Current theme: custom prompt"),
            [LocKey.CommonSettings] = ("通用设置", "Common settings"),
            [LocKey.CommonOptionsScope] = ("主题预设和自定义提示符都会用这两个设置。", "These two settings apply to both the theme preset and the custom prompt."),
            [LocKey.CommandExampleGuide] = ("示例 / 指南", "Examples / guide"),
            [LocKey.CommandExampleGuideTooltip] = ("查看命令代码的写法示例，可直接复制。", "Show code examples for commands; each can be copied."),
            [LocKey.CommandBodyHint] = ("只写要执行的命令即可：保存时会包成以命令名命名的函数，只有在终端里输入该名称时才会执行。", "Write only the command to run: it is wrapped in a function named after the command and runs only when you type that name in a terminal."),
            [LocKey.AdvancedCommandArgumentsOptional] = ("高级选项：命令参数（可选）", "Advanced: command arguments (optional)"),
            [LocKey.AllowArgumentsAfterCommandName] = ("允许在命令名后传入参数", "Allow arguments after the command name"),
            [LocKey.FixedArguments] = ("固定参数", "Fixed arguments"),
            [LocKey.FixedArgumentsPlaceholder] = ("例如：-t 8.8.8.8", "For example: -t 8.8.8.8"),
            [LocKey.CommandArgumentsHint] = ("每次调用都会带上这些参数，终端里临时输入的参数排在后面；两者都追加在命令体最后一行的末尾。含空格的参数请用英文双引号包起来。", "These arguments are passed on every call, followed by any typed in the terminal. Both are appended to the last line of the command body. Wrap arguments that contain spaces in double quotes."),
            [LocKey.CommandGuideIntro] = ("把下面的命令复制后，点「新建命令」，粘贴到命令代码里，设置好命令名称，保存并生效即可。", "Copy one of the commands below, click New Command, paste it into the command code, set the command name, then save."),
            [LocKey.CommandGuideNameHint] = ("命令名称就是将来在终端里输入的名字；代码只写要执行的命令，不需要写 function。", "The command name is what you will type in a terminal; the code holds only the command to run, no function needed."),
            [LocKey.Copy] = ("复制", "Copy"),
            [LocKey.Close] = ("关闭", "Close"),
            [LocKey.Copied0] = ("已复制：{0}", "Copied: {0}"),
            [LocKey.CopyFailed0] = ("复制失败：{0}", "Copy failed: {0}"),
        };

    public static readonly IReadOnlyDictionary<string, string> ZhToEn = BuildZhToEn();

    private static Dictionary<string, string> BuildZhToEn()
    {
        var d = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (var (key, (zh, en)) in Data) d[zh] = en;
        return d;
    }
}
