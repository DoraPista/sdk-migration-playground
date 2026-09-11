<#
.SYNOPSIS
    Makes a candidate-facing copy of the gym with all interviewer material removed.

.DESCRIPTION
    Copies the repository to a new folder, leaving out interviewer/, build output and any REVIEW.md a
    previous candidate wrote, then verifies that nothing spoiler-shaped is left behind.

.EXAMPLE
    ./interviewer/tools/make-candidate-copy.ps1 -Destination C:\temp\gym-for-candidate
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Destination,

    # Overwrite the destination if it already exists.
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')

if (Test-Path $Destination) {
    if (-not $Force) {
        throw "$Destination already exists. Pass -Force to overwrite it."
    }

    Remove-Item -Recurse -Force $Destination
}

Write-Host "Copying $repo -> $Destination ..."
New-Item -ItemType Directory -Path $Destination | Out-Null

$excluded = @('interviewer', 'bin', 'obj', '.git', '.vs', 'TestResults')

Get-ChildItem -Path $repo -Recurse -Force | Where-Object {
    $relative = $_.FullName.Substring($repo.Path.Length).TrimStart('\')
    $parts = $relative -split '\\'
    -not ($parts | Where-Object { $excluded -contains $_ }) -and
    $_.Name -ne 'REVIEW.md' -and
    -not $relative.StartsWith('.gym-verify')
} | ForEach-Object {
    $relative = $_.FullName.Substring($repo.Path.Length).TrimStart('\')
    $target = Join-Path $Destination $relative
    if ($_.PSIsContainer) {
        if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target | Out-Null }
    }
    else {
        $parent = Split-Path $target -Parent
        if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }
        Copy-Item $_.FullName -Destination $target
    }
}

# The copy must not contain the interviewer folder, or any reference that would lead a candidate to it.
$leftovers = @()
if (Test-Path (Join-Path $Destination 'interviewer')) {
    $leftovers += 'the interviewer/ folder is still present'
}

$referencing = Get-ChildItem -Path $Destination -Recurse -File -Include *.md, *.cs, *.ps1 |
    Select-String -Pattern 'interviewer/solutions|SOLUTION\.md|DISCUSSION\.md' -List |
    ForEach-Object { $_.Path.Substring($Destination.Length).TrimStart('\') }

foreach ($file in $referencing) {
    $leftovers += "$file references interviewer material"
}

if ($leftovers.Count -gt 0) {
    Write-Warning 'The copy still refers to interviewer material:'
    $leftovers | ForEach-Object { Write-Warning "  $_" }
    Write-Warning 'Links to interviewer/ from candidate docs are expected (README and INTERVIEW_MODE point at it);'
    Write-Warning 'check that none of them quote an answer, then delete or reword as you prefer.'
}
else {
    Write-Host 'No interviewer material found in the copy.' -ForegroundColor Green
}

$exercises = (Get-ChildItem -Path (Join-Path $Destination 'exercises') -Directory |
    ForEach-Object { Get-ChildItem $_.FullName -Directory }).Count
Write-Host "Done: $exercises exercise folders copied to $Destination"
