<#
.SYNOPSIS
    PAM 卸载清理：结束运行实例、移除登录启动项、剥离两份 $PROFILE 的托管区。

.DESCRIPTION
    由 Inno Setup 卸载程序在删除安装目录之前调用，也可以手动运行。
    $PROFILE 的用户区内容原样保留，改动前为每个文件生成一份带时间戳的备份。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Uninstall-Cleanup.ps1 -InstallDir "$env:LOCALAPPDATA\Programs\PAM"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $InstallDir,

    [switch] $RemoveUserData
)

$ErrorActionPreference = 'Stop'
$script:Log = [System.Collections.Generic.List[string]]::new()

function Write-Log([string] $Message) {
    $script:Log.Add("[$(Get-Date -Format 'HH:mm:ss')] $Message")
}

function Stop-PamProcesses {
    foreach ($name in @('PAM.App', 'PAM.Tray')) {
        Get-Process -Name $name -ErrorAction SilentlyContinue |
            Stop-Process -Force -ErrorAction SilentlyContinue
    }
    Write-Log '已结束运行中的 PAM.App / PAM.Tray。'
}

# 与 MainViewModel.GetManagedProfilePaths() 保持同一批路径：两个 shell 各解析一次，
# 再补上文档目录里的常见文件名，覆盖 $PROFILE 不存在或 shell 不在 PATH 的情况。
function Get-ProfilePaths {
    $found = [System.Collections.Generic.List[string]]::new()

    foreach ($shell in @('powershell.exe', 'pwsh.exe')) {
        $command = Get-Command $shell -ErrorAction SilentlyContinue
        if (-not $command) { continue }
        try {
            $output = & $command.Source -NoProfile -NonInteractive -Command '$PROFILE' 2>$null
            $line = @($output | Where-Object { $_ -is [string] -and $_.Trim() -match '\.ps1$' } |
                              Select-Object -Last 1)
            if ($line.Count -gt 0) {
                $path = $line[0].Trim().Trim('"')
                if ([System.IO.Path]::IsPathRooted($path)) { $found.Add($path) }
            }
        } catch {
            Write-Log "解析 $shell 的 `$PROFILE 失败：$($_.Exception.Message)"
        }
    }

    $documents = [Environment]::GetFolderPath('MyDocuments')
    if (-not [string]::IsNullOrWhiteSpace($documents)) {
        $found.Add((Join-Path $documents 'WindowsPowerShell\Microsoft.PowerShell_profile.ps1'))
        $found.Add((Join-Path $documents 'PowerShell\Microsoft.PowerShell_profile.ps1'))
    }

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $unique = [System.Collections.Generic.List[string]]::new()
    foreach ($path in $found) {
        if ($seen.Add($path)) { $unique.Add($path) }
    }
    return $unique.ToArray()
}

# 保留原文件的 BOM 与编码：Windows PowerShell 5.1 用 BOM 判断含中文的 $PROFILE 编码。
function Get-TextInfo([string] $Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $false

    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $hasBom = $true
        $encoding = [System.Text.UTF8Encoding]::new($true)
    } elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        $hasBom = $true
        $encoding = [System.Text.UnicodeEncoding]::new($false, $true)
    } elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        $hasBom = $true
        $encoding = [System.Text.UnicodeEncoding]::new($true, $true)
    } else {
        $encoding = [System.Text.UTF8Encoding]::new($false)
    }

    $text = $encoding.GetString($bytes)
    if ($hasBom -and $text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) {
        $text = $text.Substring(1)
    }

    return [pscustomobject]@{ Text = $text; Encoding = $encoding }
}

# 托管区从「# >>> PAM Managed >>>」一直延伸到文件末尾；用户区里残留的旧版
# 生成块（别名启动器、命令提示、Starship、自定义命令区）也一并剔除。
function Remove-PamBlocks([string[]] $Lines) {
    $managedIndex = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i].TrimStart() -match '^#\s*>>>\s*PAM Managed\s*>>>') {
            $managedIndex = $i
            break
        }
    }
    $upper = if ($managedIndex -ge 0) { $managedIndex } else { $Lines.Count }

    $userLines = [System.Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $upper; $i++) { $userLines.Add($Lines[$i]) }

    $kept = [System.Collections.Generic.List[string]]::new()
    $inGenerated = $false
    $inCustomRegion = $false
    foreach ($line in $userLines) {
        $trimmed = $line.TrimStart()
        if ($inCustomRegion) {
            if ($trimmed -match '^#endregion\b') { $inCustomRegion = $false }
            continue
        }
        if ($inGenerated) {
            if ($trimmed -match '^#\s*<<<\s*PAM\b') { $inGenerated = $false }
            continue
        }
        if ($trimmed -match '^#region\s+PAM-Custom\b') { $inCustomRegion = $true; continue }
        if ($trimmed -match '^#\s*>>>\s*PAM\b') { $inGenerated = $true; continue }
        $kept.Add($line)
    }

    while ($kept.Count -gt 0 -and [string]::IsNullOrWhiteSpace($kept[$kept.Count - 1])) {
        $kept.RemoveAt($kept.Count - 1)
    }
    return $kept.ToArray()
}

function Reset-Profile([string] $Path, [string] $Stamp) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return }

    $info = Get-TextInfo $Path
    $text = $info.Text
    $newLine = if ($text.Contains("`r`n")) { "`r`n" } elseif ($text.Contains("`r")) { "`r" } else { "`n" }
    $lines = @($text -split "`r`n|`n|`r")
    $kept = @(Remove-PamBlocks $lines)
    $newText = if ($kept.Count -gt 0) { ($kept -join $newLine) + $newLine } else { '' }

    if ($newText -ceq $text) {
        Write-Log "无需改动：$Path"
        return
    }

    $backup = "$Path.pam-uninstall-$Stamp.bak"
    [System.IO.File]::Copy($Path, $backup, $true)
    [System.IO.File]::WriteAllText($Path, $newText, $info.Encoding)
    Write-Log "已移除托管区：$Path"
    Write-Log "原文件备份：$backup"
}

# 只清理指向本次安装目录的启动项，避免误删另一份开发构建留下的记录。
function Remove-LogonEntry([string] $InstallDirectory) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $approvedKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
    $value = $null
    try {
        $value = (Get-ItemProperty -LiteralPath $runKey -Name 'PAM.Tray' -ErrorAction Stop).'PAM.Tray'
    } catch {
        return
    }
    if ([string]::IsNullOrWhiteSpace($value)) { return }

    $exePath = $value.Trim()
    if ($exePath.StartsWith('"')) {
        $closing = $exePath.IndexOf('"', 1)
        $exePath = if ($closing -gt 0) { $exePath.Substring(1, $closing - 1) } else { '' }
    } else {
        $space = $exePath.IndexOf(' ')
        if ($space -gt 0) { $exePath = $exePath.Substring(0, $space) }
    }

    if ($exePath -and $exePath.StartsWith($InstallDirectory.TrimEnd('\'),
                                          [System.StringComparison]::OrdinalIgnoreCase)) {
        Remove-ItemProperty -LiteralPath $runKey -Name 'PAM.Tray' -ErrorAction SilentlyContinue
        Remove-ItemProperty -LiteralPath $approvedKey -Name 'PAM.Tray' -ErrorAction SilentlyContinue
        Write-Log '已删除 HKCU 登录启动项 PAM.Tray。'
    } else {
        Write-Log '启动项指向其它安装位置，已保留。'
    }
}

# 配置目录改名备份而不是直接删除，用户日后仍能找回别名与设置。
function Backup-UserData([string] $Stamp) {
    $dataDir = Join-Path $env:APPDATA 'PAM'
    if (-not (Test-Path -LiteralPath $dataDir -PathType Container)) { return }

    $backup = "$dataDir-uninstalled-$Stamp"
    $suffix = 0
    while (Test-Path -LiteralPath $backup) {
        $suffix++
        $backup = "$dataDir-uninstalled-$Stamp-$suffix"
    }
    Move-Item -LiteralPath $dataDir -Destination $backup -Force
    Write-Log "配置目录已备份到：$backup"
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
try {
    Stop-PamProcesses
    Remove-LogonEntry $InstallDir
    foreach ($profilePath in (Get-ProfilePaths)) {
        Reset-Profile $profilePath $stamp
    }
    if ($RemoveUserData) { Backup-UserData $stamp }
    Write-Log '清理完成。'
} catch {
    Write-Log "清理过程出错：$($_.Exception.Message)"
} finally {
    try {
        [System.IO.File]::WriteAllLines(
            (Join-Path $env:TEMP 'PAM-Uninstall.log'),
            $script:Log,
            [System.Text.UTF8Encoding]::new($true))
    } catch { }
}
