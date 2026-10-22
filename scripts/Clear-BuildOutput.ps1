<#
.SYNOPSIS
    Removes bin and obj directories inside the repository.

.DESCRIPTION
    Deletes build output only: directories whose name matches -DirectoryName and that live inside
    the repository root. The .git, .vs and node_modules subtrees are skipped, reparse points are not
    followed, and output/TestResults are untouched unless requested explicitly.

.EXAMPLE
    ./scripts/Clear-BuildOutput.ps1 -WhatIf
    Shows what would be deleted without deleting anything.

.EXAMPLE
    ./scripts/Clear-BuildOutput.ps1 -Confirm:$false
    Deletes every bin/obj directory without asking for confirmation.

.EXAMPLE
    ./scripts/Clear-BuildOutput.ps1 -DirectoryName bin,obj,TestResults
    Also cleans TestResults.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    # Repository root. Defaults to the repository this script belongs to.
    [string]$Root = '',

    # Directory names to delete.
    [string[]]$DirectoryName = @('bin', 'obj'),

    # Path segments that are never entered.
    [string[]]$ExcludeDirectory = @('.git', '.vs', 'node_modules')
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
    throw "Repository root not found: $Root"
}

$rootFull = (Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
Write-Host "Repository: $rootFull"

if (Get-Process devenv, testhost -ErrorAction SilentlyContinue) {
    Write-Warning 'Visual Studio or testhost is running; locked files will not be deleted.'
}

$targets = @(
    Get-ChildItem -LiteralPath $rootFull -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object {
            $DirectoryName -contains $_.Name -and
            (($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0)
        } |
        Where-Object {
            $relative = $_.FullName.Substring($rootFull.Length).TrimStart('\')
            $segments = $relative -split '\\'
            -not ($segments | Where-Object { $ExcludeDirectory -contains $_ })
        }
)

if ($targets.Count -eq 0) {
    Write-Host 'Nothing to clean: no bin/obj directories found.'
    exit 0
}

$totalBytes = 0.0
foreach ($directory in $targets) {
    $sum = (Get-ChildItem -LiteralPath $directory.FullName -File -Recurse -Force -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum
    if ($null -ne $sum) { $totalBytes += $sum }
}

Write-Host ('Found {0} directories, {1:N1} MB total' -f $targets.Count, ($totalBytes / 1MB))

$removedCount = 0
$failed = @()
foreach ($directory in $targets) {
    if ($PSCmdlet.ShouldProcess($directory.FullName, 'Remove directory')) {
        try {
            Remove-Item -LiteralPath $directory.FullName -Recurse -Force -ErrorAction Stop
            $removedCount++
            Write-Host "Removed: $($directory.FullName)"
        }
        catch {
            $failed += $directory.FullName
            Write-Warning "Could not remove $($directory.FullName): $($_.Exception.Message)"
        }
    }
}

if ($WhatIfPreference) {
    Write-Host ('WhatIf: would remove {0} directories, about {1:N1} MB' -f $targets.Count, ($totalBytes / 1MB))
}
else {
    Write-Host ('Done: removed {0} of {1}, freed about {2:N1} MB' -f $removedCount, $targets.Count, ($totalBytes / 1MB))
}

if ($failed.Count -gt 0) {
    Write-Host 'Not removed:'
    $failed | ForEach-Object { Write-Host "  $_" }
    Write-Host 'Close the programs that hold these files and run the script again.'
    exit 1
}

exit 0
