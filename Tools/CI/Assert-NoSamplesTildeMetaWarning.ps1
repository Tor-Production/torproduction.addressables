[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$LogPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$warningPattern = "A meta data file \(\.meta\) exists but its folder 'Packages/com\.torproduction\.addressables/Samples/?~' can't be found, and has been created\."

foreach ($path in $LogPath) {
    $resolvedPath = (Resolve-Path -LiteralPath $path).Path
    $normalizedLog = (Get-Content -Raw -LiteralPath $resolvedPath).Replace('\', '/')
    if ($normalizedLog -match $warningPattern) {
        throw "Unity recreated the intentionally hidden Samples~ package folder because a root Samples~.meta was present: $resolvedPath"
    }
}

Write-Output "Samples~ root metadata warning check passed for $($LogPath.Count) log(s)."
