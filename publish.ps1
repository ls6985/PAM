<#
.SYNOPSIS
    Build the PAM deliverable: a single folder containing PAM.App.exe and PAM.Tray.exe.

.DESCRIPTION
    Both executables must sit in the same directory because each one looks for the
    other next to itself at runtime. This script publishes the two projects into
    separate, brand new folders outside the repository and then merges them into
    the output folder. Running instances are stopped first so the deliverable
    folder can be replaced.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER Output
    Deliverable folder, relative to this script unless an absolute path is given.
    Defaults to output\publish, the local test build.

.PARAMETER RestartTray
    Start the tray again from the deliverable folder after publishing. Off by
    default; logon startup will also launch it on the next sign-in.

.PARAMETER SelfContained
    Publish for win-x64 with the .NET Desktop Runtime included, so the
    deliverable runs on machines without a system-wide .NET installation.
    Used when building the installer; the default local test build is smaller.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\publish.ps1
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $Output = 'output\publish',
    [switch] $RestartTray,
    [switch] $SelfContained
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$outputPath = if ([System.IO.Path]::IsPathRooted($Output)) {
    $Output
} else {
    Join-Path $root $Output
}
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('pam-publish-' + [Guid]::NewGuid().ToString('N'))

function Write-Step([string] $message) {
    Write-Host "[PAM] $message" -ForegroundColor Cyan
}

function Stop-PamProcesses {
    $running = @(Get-Process -Name 'PAM.App', 'PAM.Tray' -ErrorAction SilentlyContinue)
    if ($running.Count -eq 0) { return }

    Write-Step "Stopping $($running.Count) running PAM process(es)"
    $running | Stop-Process -Force -ErrorAction SilentlyContinue

    for ($i = 0; $i -lt 20; $i++) {
        if (@(Get-Process -Name 'PAM.App', 'PAM.Tray' -ErrorAction SilentlyContinue).Count -eq 0) {
            Start-Sleep -Milliseconds 200
            return
        }
        Start-Sleep -Milliseconds 150
    }
    throw 'PAM processes did not exit; close them manually and run again.'
}

function Remove-Directory([string] $path) {
    # Delete a whole folder tree in one step.
    if (Test-Path -LiteralPath $path) {
        [System.IO.Directory]::Delete($path, $true)
    }
}

function Copy-Tree([string] $source, [string] $destination) {
    Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($source.Length + 1)
        $target = Join-Path $destination $relative
        $parent = Split-Path -Path $target -Parent
        if (-not (Test-Path -LiteralPath $parent)) {
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
        }
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
}

try {
    Stop-PamProcesses
    New-Item -ItemType Directory -Path $work -Force | Out-Null

    $targets = @(
        @{ Project = 'PAM.App'; Out = (Join-Path $work 'app'); Bin = (Join-Path $work 'bin-app') },
        @{ Project = 'PAM.Tray'; Out = (Join-Path $work 'tray'); Bin = (Join-Path $work 'bin-tray') }
    )

    foreach ($target in $targets) {
        Write-Step "Publishing $($target.Project)"
        $baseOutput = $target.Bin.TrimEnd('\', '/') + '/'
        $publishArgs = @(
            (Join-Path $root $target.Project),
            '-c', $Configuration,
            '-o', $target.Out,
            "-p:BaseOutputPath=$baseOutput",
            '-p:PamSkipOutputPush=true',
            '-m:1', '--nologo', '-v', 'minimal', '-tl:off'
        )
        if ($SelfContained) {
            $publishArgs += @('-r', 'win-x64', '--self-contained', 'true')
        }
        & dotnet publish @publishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for $($target.Project) (exit code $LASTEXITCODE)."
        }
    }

    Write-Step "Recreating deliverable folder: $outputPath"
    Remove-Directory $outputPath
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

    foreach ($target in $targets) {
        Copy-Tree -source $target.Out -destination $outputPath
    }

    $required = @(
        'PAM.App.exe', 'PAM.App.dll', 'PAM.App.deps.json', 'PAM.App.runtimeconfig.json',
        'PAM.Tray.exe', 'PAM.Tray.dll', 'PAM.Tray.deps.json', 'PAM.Tray.runtimeconfig.json',
        'PAM.Core.dll', 'Wpf.Ui.dll', 'Wpf.Ui.Abstractions.dll'
    )
    $missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $outputPath $_)) })
    if ($missing.Count -gt 0) {
        throw "Deliverable is missing: $($missing -join ', ')"
    }

    $count = @(Get-ChildItem -LiteralPath $outputPath -Recurse -File).Count
    Write-Step "Done: $count files in $outputPath"

    if ($RestartTray) {
        Start-Process -FilePath (Join-Path $outputPath 'PAM.Tray.exe') -WorkingDirectory $outputPath
        Write-Step 'Background tray restarted.'
    } else {
        Write-Step 'Background tray is stopped. Run output\publish\PAM.Tray.exe to use the hotkey now, or sign in again (logon startup is registered).'
    }
}
finally {
    try { Remove-Directory $work } catch { Write-Warning "Could not remove temporary folder: $work" }
}
