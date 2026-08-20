[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),

    [ValidateSet('2.7.6', '2.9.1')]
    [string]$ExpectedHostAddressablesVersion = '2.7.6'
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$packageRoot = Join-Path $root 'com.torproduction.addressables'
$projectRoot = Join-Path $root 'AddressablesProject'
$packageManifest = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'package.json') | ConvertFrom-Json
$hostManifest = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Packages/manifest.json') | ConvertFrom-Json

if ($packageManifest.name -ne 'com.torproduction.addressables') {
    throw "Unexpected package name: $($packageManifest.name)"
}
if ($packageManifest.version -ne '0.1.0-preview.1') {
    throw "Unexpected package version: $($packageManifest.version)"
}
if ($packageManifest.unity -ne '6000.0') {
    throw "Unexpected package Unity minimum: $($packageManifest.unity)"
}
if ($packageManifest.dependencies.'com.unity.addressables' -ne '2.7.6') {
    throw 'The package must retain Addressables 2.7.6 as its minimum dependency.'
}
if ($hostManifest.dependencies.'com.unity.addressables' -ne $ExpectedHostAddressablesVersion) {
    throw "Host Addressables version does not match the lane: $ExpectedHostAddressablesVersion"
}

$expectedHostDependencies = @(
    'com.torproduction.addressables',
    'com.unity.addressables',
    'com.unity.ide.rider',
    'com.unity.test-framework'
) | Sort-Object
$actualHostDependencies = @($hostManifest.dependencies.PSObject.Properties.Name) | Sort-Object
if (Compare-Object $expectedHostDependencies $actualHostDependencies) {
    throw 'The development host manifest contains an unexpected dependency.'
}

$projectVersion = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt')
if ($projectVersion -notmatch '(?m)^m_EditorVersion:\s*6000\.0\.78f1\s*$') {
    throw 'The development project is not pinned to Unity 6000.0.78f1.'
}

$requiredSettings = @(
    'AddressablesProject/ProjectSettings/EditorBuildSettings.asset',
    'AddressablesProject/ProjectSettings/EditorSettings.asset',
    'AddressablesProject/ProjectSettings/ProjectSettings.asset',
    'AddressablesProject/ProjectSettings/ProjectVersion.txt',
    'AddressablesProject/ProjectSettings/VersionControlSettings.asset'
)
foreach ($setting in $requiredSettings) {
    & git -C $root ls-files --error-unmatch -- $setting | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Required project setting is not tracked: $setting"
    }
}

if (Test-Path -LiteralPath (Join-Path $projectRoot 'Assets/MobileDependencyResolver')) {
    throw 'The vendored Mobile Dependency Resolver is still present.'
}

$productionSources = @(
    Get-ChildItem -Recurse -File -LiteralPath (Join-Path $packageRoot 'Runtime') -Filter '*.cs'
    Get-ChildItem -Recurse -File -LiteralPath (Join-Path $packageRoot 'Editor') -Filter '*.cs'
)
foreach ($source in $productionSources) {
    $contents = Get-Content -Raw -LiteralPath $source.FullName
    if ($contents -match 'using\s+NUnit\.Framework') {
        throw "NUnit leaked into production source: $($source.FullName)"
    }
    if ($contents -match 'using\s+StansAssets\.Foundation|\bIdFactory\b') {
        throw "Foundation leaked into production source: $($source.FullName)"
    }
}

$editorSources = Get-ChildItem -Recurse -File -LiteralPath (Join-Path $packageRoot 'Editor') -Filter '*.cs'
foreach ($source in $editorSources) {
    if ((Get-Content -Raw -LiteralPath $source.FullName) -match '\[InitializeOnLoad(Method)?\]') {
        throw "Automatic editor initialization remains: $($source.FullName)"
    }
}

$menuAssembly = Get-Content -Raw -LiteralPath `
    (Join-Path $packageRoot 'Editor/Menu/TorProduction.AddressablesToolpack.Menu.asmdef') | ConvertFrom-Json
if ($menuAssembly.references -contains 'GUID:1824a82c9c6c70540989aa5f5e2b83d5') {
    throw 'The production Menu assembly depends on Samples.'
}

$testAssembly = Get-Content -Raw -LiteralPath `
    (Join-Path $packageRoot 'Tests/Editor/TorProduction.Addressables.Editor.Tests.asmdef') | ConvertFrom-Json
$requiredTestReferences = @(
    'GUID:a5d30c4d8d475d442b6f3f91d04306a1',
    'GUID:6a4270a497015e843be16b899b29c2fb',
    'GUID:d7d6534ed8cfdf5449425dc001ec6d7d',
    'GUID:9e24947de15b9834991c9d8411ea37cf'
)
foreach ($reference in $requiredTestReferences) {
    if ($testAssembly.references -notcontains $reference) {
        throw "The EditMode test assembly is missing production reference $reference"
    }
}

$metaGuids = @{}
$packageItems = Get-ChildItem -Recurse -Force -LiteralPath $packageRoot
foreach ($item in $packageItems) {
    $relativePath = $item.FullName.Substring($packageRoot.Length).TrimStart('\', '/')
    if ($relativePath -eq 'Documentation~' -or $relativePath.StartsWith('Documentation~\') -or `
        $relativePath.StartsWith('Documentation~/')) {
        continue
    }

    if ($item.PSIsContainer) {
        if (-not (Test-Path -LiteralPath ($item.FullName + '.meta'))) {
            throw "Unity folder is missing a .meta file: $relativePath"
        }
        continue
    }

    if ($item.Extension -eq '.meta') {
        $assetPath = $item.FullName.Substring(0, $item.FullName.Length - '.meta'.Length)
        if (-not (Test-Path -LiteralPath $assetPath)) {
            throw "Orphaned Unity .meta file: $relativePath"
        }

        $guidMatch = Select-String -LiteralPath $item.FullName -Pattern '^guid:\s*(\S+)' | Select-Object -First 1
        if ($guidMatch) {
            $guid = $guidMatch.Matches[0].Groups[1].Value
            if ($metaGuids.ContainsKey($guid)) {
                throw "Duplicate Unity GUID $guid in $relativePath and $($metaGuids[$guid])"
            }
            $metaGuids[$guid] = $relativePath
        }
        continue
    }

    if (-not (Test-Path -LiteralPath ($item.FullName + '.meta'))) {
        throw "Unity asset is missing a .meta file: $relativePath"
    }
}

$workflowFiles = Get-ChildItem -File -LiteralPath (Join-Path $root '.github/workflows')
foreach ($workflow in $workflowFiles) {
    if ((Get-Content -Raw -LiteralPath $workflow.FullName) -match '(?im)^\s*[^#].*npm\s+publish|packages:\s*write') {
        throw "A publication-capable workflow remains: $($workflow.Name)"
    }
}

Write-Output "Phase 0 static validation passed for Addressables $ExpectedHostAddressablesVersion."
