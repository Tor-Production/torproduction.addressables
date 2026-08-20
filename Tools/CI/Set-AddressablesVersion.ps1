[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('2.7.6', '2.9.1')]
    [string]$AddressablesVersion,

    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'

$resolvedProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$manifestPath = Join-Path $resolvedProjectPath 'Packages/manifest.json'
$lockPath = Join-Path $resolvedProjectPath 'Packages/packages-lock.json'

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($null -eq $manifest.dependencies.'com.unity.addressables') {
    throw "The project manifest does not declare com.unity.addressables: $manifestPath"
}

$manifest.dependencies.'com.unity.addressables' = $AddressablesVersion
$manifestJson = $manifest | ConvertTo-Json -Depth 20
[IO.File]::WriteAllText(
    $manifestPath,
    $manifestJson + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))

if (Test-Path -LiteralPath $lockPath) {
    Remove-Item -Force -LiteralPath $lockPath
}

Write-Output "Selected Addressables $AddressablesVersion for $resolvedProjectPath"
