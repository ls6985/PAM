; PowerShell 管理器 (PAM) 安装脚本
; 由 build\build-installer.ps1 调用 ISCC.exe 编译，参数 MyAppVersion / SourceDir / OutputDir 由构建脚本传入。

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\output\installer"
#endif
#ifndef OutputDir
  #define OutputDir "..\output\export"
#endif

#define MyAppName "PowerShell 管理器"
#define MyAppPublisher "white"
#define MyAppURL "https://github.com/ls6985/PAM"
#define MyAppExeName "PAM.App.exe"

[Setup]
; AppId 在所有版本里保持不变，升级安装才会覆盖同一份记录，而不是产生第二个卸载项。
AppId={{6B81FCDA-2C21-4991-885D-FEEB06B0A7CD}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoDescription={#MyAppName} 安装程序
DefaultDirName={localappdata}\Programs\PAM
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=PAM-Setup-{#MyAppVersion}
SetupIconFile=..\PAM.App\Assets\App.ico
UninstallDisplayIcon={app}\PAM.App.exe
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; 只写 HKCU 与当前用户目录，安装与卸载都不申请管理员权限。
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=no
ShowLanguageDialog=no

[Languages]
Name: "chinese"; MessagesFile: "Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "Uninstall-Cleanup.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即启动 PowerShell 管理器"; Flags: nowait postinstall skipifsilent unchecked

[Code]
var
  RemoveUserData: Boolean;

// 升级或卸载时托盘常驻会占住 exe / dll，先结束两个进程再继续。
procedure KillPamProcesses;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /T /IM PAM.App.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /T /IM PAM.Tray.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// ssInstall 在用户确认安装之后、复制文件之前，取消向导不会误关托盘。
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then KillPamProcesses;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Params: String;
  ResultCode: Integer;
begin
  // usUninstall 在用户确认卸载之后、按日志删除文件之前，清理脚本仍在安装目录里。
  if CurUninstallStep = usUninstall then
  begin
    KillPamProcesses;
    // 静默卸载时不弹窗，默认按钮为「否」，保留用户数据。
    RemoveUserData := MsgBox(
      '是否同时删除 PAM 的别名、自定义命令与设置数据？' + #13#10 + #13#10 +
      '选「否」会保留配置，重新安装后可以直接继续使用。' + #13#10 +
      '选「是」会把配置目录改名备份，不会直接抹掉。',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
    Params := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
              ExpandConstant('{app}\Uninstall-Cleanup.ps1') + '"';
    Params := Params + ' -InstallDir "' + ExpandConstant('{app}') + '"';
    if RemoveUserData then
      Params := Params + ' -RemoveUserData';
    Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params,
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
