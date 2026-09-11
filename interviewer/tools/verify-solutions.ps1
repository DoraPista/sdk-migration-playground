<#
.SYNOPSIS
  Proves that every interviewer solution turns its exercise green, and shows which tests are red
  in the untouched candidate version.

.DESCRIPTION
  For each solution folder that contains a code/ directory:
    1. Copies the repository (without bin/obj/interviewer) to a scratch workspace next to the repo.
    2. Overlays interviewer/solutions/<category>/<exercise>/code/** onto exercises/<category>/<exercise>/.
    3. Runs `dotnet test` on the exercise's tests folder.
  With -Baseline it runs the candidate (untouched) tests instead, which is expected to show failures.

  The workspace is created next to the repository rather than in %TEMP%, because some Windows
  application-control policies refuse to load freshly built DLLs from the temp folder.

.EXAMPLE
  ./interviewer/tools/verify-solutions.ps1                       # all solutions
  ./interviewer/tools/verify-solutions.ps1 -Filter 07-*          # one category
  ./interviewer/tools/verify-solutions.ps1 -Baseline -Filter 02-01*
#>
param(
    [string]$Filter = "*",
    [switch]$Baseline,
    [switch]$KeepWorkspace,
    [string]$WorkspaceRoot
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$solutions = Join-Path $repo "interviewer\solutions"
if (-not $WorkspaceRoot) { $WorkspaceRoot = Split-Path $repo -Parent }
$workspace = Join-Path $WorkspaceRoot (".gym-verify-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))

Write-Host "Copying repository to $workspace ..."
New-Item -ItemType Directory -Path $workspace | Out-Null
robocopy $repo $workspace /E /NFL /NDL /NJH /NJS /NP /XD bin obj .git .vs interviewer | Out-Null

# From here on, native tools write to stderr routinely; don't let PowerShell treat that as fatal.
$ErrorActionPreference = "Continue"

$results = @()
$exerciseDirs = Get-ChildItem (Join-Path $workspace "exercises") -Directory |
    ForEach-Object { Get-ChildItem $_.FullName -Directory } |
    Where-Object { $_.Name -like $Filter -and (Test-Path (Join-Path $_.FullName "tests")) }

foreach ($exercise in $exerciseDirs) {
    $category = $exercise.Parent.Name
    $solutionCode = Join-Path $solutions "$category\$($exercise.Name)\code"

    if (-not $Baseline) {
        if (-not (Test-Path $solutionCode)) { continue }
        robocopy $solutionCode $exercise.FullName /E /NFL /NDL /NJH /NJS /NP | Out-Null
    }

    $testsDir = Join-Path $exercise.FullName "tests"
    $mode = if ($Baseline) { "candidate" } else { "solution" }
    Write-Host "`n=== $($exercise.Name) ($mode) ===" -ForegroundColor Cyan
    # Windows Smart App Control can refuse to load a freshly built, unsigned test DLL
    # ("An Application Control policy has blocked this file"). The verdict is cached per file hash, so
    # retrying the same binary does not help: rebuild with a new SourceRevisionId (new hash) instead.
    for ($try = 1; $try -le 4; $try++) {
        $identity = if ($try -gt 1) { "-p:SourceRevisionId=gymretry$try" + (Get-Random) } else { "" }
        $output = (cmd /c "dotnet test `"$testsDir`" --nologo $identity 2>&1") -join "`n"
        if ($output -notmatch "Application Control policy has blocked") { break }
        Write-Host "   (blocked by Application Control, rebuilding with a new binary identity $try/4)" -ForegroundColor Yellow
    }
    $summaryLines = $output -split "`n" | Where-Object { $_ -match "(Passed!|Failed!)\s+-" }
    $summary = ($summaryLines -join " ").Trim()
    $buildFailed = ($output -match "Build FAILED") -or ($output -match ": error (CS|MSB|NETSDK|NU)")
    if ($buildFailed) { $summary = "BUILD FAILED" }
    $green = (-not $buildFailed) -and ($output -match "Passed!") -and -not ($output -match "Failed!")
    Write-Host $summary
    if (-not $green) {
        $output -split "`n" | Where-Object { $_ -match "\[FAIL\]|: error |Catastrophic" } | Select-Object -First 15 |
            ForEach-Object { Write-Host "   $($_.Trim())" }
    }
    $results += [pscustomobject]@{ Exercise = $exercise.Name; Green = $green; Summary = $summary }
}

Write-Host "`n==================== Summary ====================" -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String -Width 220 | Write-Host

if (-not $KeepWorkspace) { Remove-Item $workspace -Recurse -Force -ErrorAction SilentlyContinue }
else { Write-Host "Workspace kept at $workspace" }

if (-not $Baseline -and ($results | Where-Object { -not $_.Green })) { exit 1 }
exit 0
