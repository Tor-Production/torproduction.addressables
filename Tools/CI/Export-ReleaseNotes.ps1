[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ChangelogPath,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedChangelog = (Resolve-Path -LiteralPath $ChangelogPath).Path
$changelog = Get-Content -Raw -LiteralPath $resolvedChangelog
$escapedVersion = [regex]::Escape($Version)
$headingMatches = [regex]::Matches(
    $changelog,
    "(?m)^## \[$escapedVersion\] - (?<date>\d{4}-\d{2}-\d{2})\s*$")
if ($headingMatches.Count -ne 1) {
    throw "Expected exactly one dated CHANGELOG heading for $Version; found $($headingMatches.Count)."
}

$heading = $headingMatches[0]
$bodyStart = $heading.Index + $heading.Length
$nextHeadingRegex = [regex]::new('(?m)^## \[')
$nextHeading = $nextHeadingRegex.Match($changelog, $bodyStart)
$bodyEnd = if ($nextHeading.Success) { $nextHeading.Index } else { $changelog.Length }
$body = $changelog.Substring($bodyStart, $bodyEnd - $bodyStart).Trim()
if ([string]::IsNullOrWhiteSpace($body)) {
    throw "CHANGELOG entry for $Version is empty."
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutput
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}
$releaseNotes = "# v$Version`n`n$body`n"
[IO.File]::WriteAllText($resolvedOutput, $releaseNotes, [Text.UTF8Encoding]::new($false))
Write-Output "Exported release notes for $Version ($($heading.Groups['date'].Value)): $resolvedOutput"
