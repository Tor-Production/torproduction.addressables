[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'

$resolvedProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$forbiddenPaths = @(
    (Join-Path $resolvedProjectPath 'ProjectSettings/ProjectConfig.json'),
    (Join-Path $resolvedProjectPath 'Assets/AddressableAssetsData')
)

foreach ($forbiddenPath in $forbiddenPaths) {
    if (Test-Path -LiteralPath $forbiddenPath) {
        throw "Package import created forbidden project state: $forbiddenPath"
    }
}

$addressablesSettings = Get-ChildItem -Recurse -File -LiteralPath (Join-Path $resolvedProjectPath 'Assets') `
    -Filter 'AddressableAssetSettings.asset' -ErrorAction SilentlyContinue
if ($addressablesSettings) {
    throw "Package import created Addressables settings: $($addressablesSettings.FullName)"
}

$buildSettingsPath = Join-Path $resolvedProjectPath 'ProjectSettings/EditorBuildSettings.asset'
$buildSettings = Get-Content -Raw -LiteralPath $buildSettingsPath
if ($buildSettings -notmatch '(?m)^\s*m_Scenes:\s*\[\]\s*$') {
    throw "Build Settings are not empty after package import: $buildSettingsPath"
}

Write-Output 'Package import remained inert.'
