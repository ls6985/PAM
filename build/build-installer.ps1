<#
.SYNOPSIS
    构建 PAM 的 Windows 安装程序。

.DESCRIPTION
    先调用仓库根目录的 publish.ps1，把两个工程以自包含方式发布到
    output\installer（内置 .NET 桌面运行时，用户机器无需预装），再用 Inno Setup
    把该目录编译成 output\export\PAM-Setup-<版本>.exe。版本号取自 PAM.App.csproj。
    output\ 整目录被 git 忽略，build（本脚本所在目录）与 docs 随仓库提交。

.PARAMETER Iscc
    ISCC.exe 的完整路径。不传时按 PATH、ISCC_PATH 环境变量与常见安装目录查找。

.PARAMETER FrameworkDependent
    发布不含 .NET 运行时的版本，体积更小，但用户机器需自行安装 .NET 10 桌面运行时。

.PARAMETER SkipPublish
    复用已有的 output\installer 目录，不重新发布。

.PARAMETER RestartTray
    构建结束后从 output\installer 重新拉起后台托盘，保持开发机上的热键可用。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build\build-installer.ps1
#>
[CmdletBinding()]
param(
    [string] $Iscc,
    [string] $Configuration = 'Release',
    [switch] $FrameworkDependent,
    [switch] $SkipPublish,
    [switch] $RestartTray
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$issPath = Join-Path $PSScriptRoot 'PAM.iss'
$outputRoot = Join-Path $root 'output'
$stagePath = Join-Path $outputRoot 'installer'
$exportPath = Join-Path $outputRoot 'export'

function Write-Step([string] $Message) {
    Write-Host "[PAM] $Message" -ForegroundColor Cyan
}

function Get-AssemblyVersion {
    $appProject = Join-Path $root 'PAM.App\PAM.App.csproj'
    $trayProject = Join-Path $root 'PAM.Tray\PAM.Tray.csproj'
    # csproj 无 BOM；PowerShell 5.1 的 Get-Content 会按本地代码页解码中文注释，显式按 UTF-8 读。
    $appVersion = ([xml][IO.File]::ReadAllText($appProject)).SelectSingleNode('/Project/PropertyGroup/Version').InnerText.Trim()
    $trayVersion = ([xml][IO.File]::ReadAllText($trayProject)).SelectSingleNode('/Project/PropertyGroup/Version').InnerText.Trim()

    if ($appVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
        throw "PAM.App.csproj 的 Version 格式无效：$appVersion"
    }
    if ($appVersion -ne $trayVersion) {
        throw "两个工程的 Version 不一致：PAM.App=$appVersion，PAM.Tray=$trayVersion"
    }
    return $appVersion
}

function Resolve-Iscc([string] $Explicit) {
    $candidates = [System.Collections.Generic.List[string]]::new()
    if ($Explicit) { $candidates.Add($Explicit) }
    if ($env:ISCC_PATH) { $candidates.Add($env:ISCC_PATH) }
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) { $candidates.Add($command.Source) }

    foreach ($base in @($env:LOCALAPPDATA,
                        [Environment]::GetEnvironmentVariable('ProgramFiles'),
                        [Environment]::GetEnvironmentVariable('ProgramFiles(x86)'))) {
        if (-not $base) { continue }
        $candidates.Add((Join-Path $base 'Programs\Inno Setup 6\ISCC.exe'))
        $candidates.Add((Join-Path $base 'Inno Setup 6\ISCC.exe'))
        $candidates.Add((Join-Path $base 'Programs\Inno Setup 7\ISCC.exe'))
        $candidates.Add((Join-Path $base 'Inno Setup 7\ISCC.exe'))
    }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    return $null
}

$version = Get-AssemblyVersion
Write-Step "版本 $version"

if (-not $SkipPublish) {
    Write-Step '发布自包含程序文件到 output\installer'
    $publishArgs = @{ Configuration = $Configuration; Output = $stagePath }
    if (-not $FrameworkDependent) { $publishArgs['SelfContained'] = $true }
    if ($RestartTray) { $publishArgs['RestartTray'] = $true }
    & (Join-Path $root 'publish.ps1') @publishArgs
    if (-not (Test-Path -LiteralPath (Join-Path $stagePath 'PAM.App.exe'))) {
        throw "output\installer 里没有 PAM.App.exe，发布未成功。"
    }
} elseif (-not (Test-Path -LiteralPath (Join-Path $stagePath 'PAM.App.exe'))) {
    throw "output\installer 里没有 PAM.App.exe，不能跳过发布。"
}

$isccPath = Resolve-Iscc $Iscc
if (-not $isccPath) {
    throw "未找到 Inno Setup 编译器 ISCC.exe。可先安装：winget install --id JRSoftware.InnoSetup -e --source winget"
}
Write-Step "Inno Setup: $isccPath"

New-Item -ItemType Directory -Path $exportPath -Force | Out-Null
Write-Step '编译安装程序'
& $isccPath "/DMyAppVersion=$version" "/DSourceDir=$stagePath" "/DOutputDir=$exportPath" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC 编译失败（退出码 $LASTEXITCODE）。"
}

$setupPath = Join-Path $exportPath "PAM-Setup-$version.exe"
if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "未生成预期的安装程序：$setupPath"
}

$file = Get-Item -LiteralPath $setupPath
Write-Step ("完成：{0}（{1:N1} MB）" -f $file.Name, ($file.Length / 1MB))
Write-Step "导出目录：$exportPath"
