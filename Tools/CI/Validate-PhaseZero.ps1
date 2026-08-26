[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),

    [ValidateSet('2.7.6', '2.9.1', '2.11.2')]
    [string]$ExpectedHostAddressablesVersion = '2.7.6'
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$packageRoot = Join-Path $root 'com.torproduction.addressables'
$projectRoot = Join-Path $root 'AddressablesProject'

function Test-ContainsFormerOwnerToken([byte[]]$Bytes, [byte[]]$Token) {
    if ($null -eq $Bytes -or $Bytes.Length -lt $Token.Length) {
        return $false
    }
    for ($offset = 0; $offset -le $Bytes.Length - $Token.Length; $offset++) {
        $matched = $true
        for ($index = 0; $index -lt $Token.Length; $index++) {
            $value = $Bytes[$offset + $index]
            if ($value -ge 65 -and $value -le 90) {
                $value += 32
            }
            if ($value -ne $Token[$index]) {
                $matched = $false
                break
            }
        }
        if ($matched) {
            return $true
        }
    }
    return $false
}

$forbiddenTokenCodes = @(87, 104, 105, 109, 115, 121)
$forbiddenToken = -join ($forbiddenTokenCodes | ForEach-Object { [char]$_ })
$forbiddenTokenBytes = [Text.Encoding]::ASCII.GetBytes($forbiddenToken.ToLowerInvariant())
$trackedPaths = @(& git -C $root -c core.quotepath=false ls-files)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to enumerate tracked files for the former-owner guard.'
}
foreach ($trackedPath in $trackedPaths) {
    if ($trackedPath.IndexOf($forbiddenToken, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "A tracked path contains the forbidden former-owner token: $trackedPath"
    }
    $fullTrackedPath = Join-Path $root $trackedPath
    if ((Test-Path -LiteralPath $fullTrackedPath -PathType Leaf) -and
        (Test-ContainsFormerOwnerToken ([IO.File]::ReadAllBytes($fullTrackedPath)) $forbiddenTokenBytes)) {
        throw "A tracked file contains the forbidden former-owner token: $trackedPath"
    }
}

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
if ($packageManifest.repository.url -ne 'git+https://github.com/Yurii-Tor/torproduction.addressables.git') {
    throw 'The package repository URL does not match the configured GitHub repository.'
}
if (@($packageManifest.samples).Count -ne 1 -or
    $packageManifest.samples[0].displayName -ne 'Basic Setup' -or
    $packageManifest.samples[0].path -ne 'Samples~/BasicSetup' -or
    [string]::IsNullOrWhiteSpace($packageManifest.samples[0].description)) {
    throw 'The package must declare exactly the curated Samples~/BasicSetup sample.'
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

if (Test-Path -LiteralPath (Join-Path $packageRoot 'Runtime')) {
    throw 'The editor-only package must not contain a production Runtime directory.'
}
if (Test-Path -LiteralPath (Join-Path $packageRoot 'Samples')) {
    throw 'The compiled legacy Samples directory must not exist.'
}
if (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'Samples~/BasicSetup'))) {
    throw 'The declared BasicSetup sample path is missing.'
}
$productionSources = @(
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
    if ($contents -match 'BindingFlags\.NonPublic|m_ImplicitAssets|GetAddressablesPlatformPathInternal') {
        throw "Private Addressables implementation access remains in production source: $($source.FullName)"
    }
    if ($contents -match 'AssetDatabase\s*\.\s*MoveAsset\s*\(') {
        throw "A production command can still physically move a host asset: $($source.FullName)"
    }
}

$removedPrefabMigrationPaths = @(
    'com.torproduction.addressables/Editor/Menu/InteractablePrefabsPathFixerMenu',
    'com.torproduction.addressables/Editor/Menu/Utils/InteractableTemplateFieldsUpdater.cs'
)
foreach ($relativePath in $removedPrefabMigrationPaths) {
    if (Test-Path -LiteralPath (Join-Path $root $relativePath)) {
        throw "Removed prefab/interactable migration code remains: $relativePath"
    }
}

$editorSources = Get-ChildItem -Recurse -File -LiteralPath (Join-Path $packageRoot 'Editor') -Filter '*.cs'
foreach ($source in $editorSources) {
    $contents = Get-Content -Raw -LiteralPath $source.FullName
    $relativeSource = $source.FullName.Substring($root.Length).TrimStart('\', '/') -replace '\\', '/'
    $recoveryBootstrapPath = 'com.torproduction.addressables/Editor/Menu/BuildMenu/BuildMenu.cs'
    if ($contents -match '\[InitializeOnLoad(Method)?\]' -and $relativeSource -ne $recoveryBootstrapPath) {
        throw "Automatic editor initialization remains: $($source.FullName)"
    }
}

$buildMenuSource = Get-Content -Raw -LiteralPath `
    (Join-Path $packageRoot 'Editor/Menu/BuildMenu/BuildMenu.cs')
if ($buildMenuSource -notmatch 'ShouldOfferRecovery\(ContentBuildRecoveryInfo recovery\)' -or
    $buildMenuSource -notmatch 'if \(ShouldOfferRecovery\(recovery\)\) BuildWorkflowWindow\.OpenRecovery\(\)') {
    throw 'The only allowed startup hook must remain a fail-closed package-job recovery offer.'
}

$removedBuildPaths = @(
    'com.torproduction.addressables/Editor/BuildProcess/CustomBuildScripts/EditorPlaymodeBuildScript.cs',
    'com.torproduction.addressables/Editor/Utils/ReportUpdater.cs'
)
foreach ($relativePath in $removedBuildPaths) {
    if (Test-Path -LiteralPath (Join-Path $root $relativePath)) {
        throw "A removed legacy build execution path remains: $relativePath"
    }
}

$buildPipelineRoot = Join-Path $packageRoot 'Editor/BuildPipeline'
$buildPipelineSources = Get-ChildItem -Recurse -File -LiteralPath $buildPipelineRoot -Filter '*.cs'
foreach ($source in $buildPipelineSources) {
    $contents = Get-Content -Raw -LiteralPath $source.FullName
    if ($contents -match 'System\.Reflection|BindingFlags|Get(Field|Property|Method)\s*\(') {
        throw "Private/reflection-based Addressables access exists in the Phase 5 build pipeline: $($source.FullName)"
    }
}
$allProductionSource = ($productionSources | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
foreach ($legacyIdentifier in @('EditorPlaymodeBuildScript', 'ReportUpdater', 'TargetPlatform')) {
    if ($allProductionSource -match "\b$legacyIdentifier\b") {
        throw "A legacy build identifier remains reachable in production source: $legacyIdentifier"
    }
}
foreach ($requiredBuildCall in @(
    'AddressableAssetSettings.BuildPlayerContent',
    'ContentUpdateScript.BuildContentUpdate',
    'ContentUpdateScript.GatherModifiedEntries',
    'BuildPipeline.IsBuildTargetSupported',
    'SwitchActiveBuildTarget',
    'BuildScriptPackedPlayMode'
)) {
    if ($allProductionSource -notmatch [regex]::Escape($requiredBuildCall)) {
        throw "The supported Phase 5 build integration is missing: $requiredBuildCall"
    }
}

$productionAssemblies = @(Get-ChildItem -Recurse -File -LiteralPath `
    (Join-Path $packageRoot 'Editor') -Filter '*.asmdef')
if ($productionAssemblies.Count -ne 1) {
    throw "Expected exactly one production editor assembly, found $($productionAssemblies.Count)."
}
$editorAssembly = Get-Content -Raw -LiteralPath $productionAssemblies[0].FullName | ConvertFrom-Json
if ($editorAssembly.name -ne 'TorProduction.Addressables.Editor' -or
    $editorAssembly.rootNamespace -ne 'TorProduction.Addressables.Editor' -or
    @($editorAssembly.includePlatforms).Count -ne 1 -or
    $editorAssembly.includePlatforms[0] -ne 'Editor') {
    throw 'The production editor assembly identity/root/platform boundary is invalid.'
}
$expectedEditorReferences = @(
    'GUID:69448af7b92c7f342b298e06a37122aa',
    'GUID:9e24947de15b9834991c9d8411ea37cf'
) | Sort-Object
if (Compare-Object $expectedEditorReferences @($editorAssembly.references | Sort-Object)) {
    throw 'The production editor assembly reference graph changed unexpectedly.'
}
if (@(Get-ChildItem -Recurse -File -LiteralPath (Join-Path $packageRoot 'Samples~') `
        -Filter '*.asmdef').Count -ne 0) {
    throw 'The optional BasicSetup sample must not compile a sample assembly.'
}

$testAssembly = Get-Content -Raw -LiteralPath `
    (Join-Path $packageRoot 'Tests/Editor/TorProduction.Addressables.Editor.Tests.asmdef') | ConvertFrom-Json
$requiredTestReferences = @(
    'GUID:6a4270a497015e843be16b899b29c2fb',
    'GUID:9e24947de15b9834991c9d8411ea37cf',
    'GUID:69448af7b92c7f342b298e06a37122aa',
    'UnityEditor.TestRunner',
    'UnityEngine.TestRunner'
)
foreach ($reference in $requiredTestReferences) {
    if ($testAssembly.references -notcontains $reference) {
        throw "The EditMode test assembly is missing production reference $reference"
    }
}
foreach ($removedReference in @(
    'GUID:a5d30c4d8d475d442b6f3f91d04306a1',
    'GUID:d7d6534ed8cfdf5449425dc001ec6d7d'
)) {
    if ($testAssembly.references -contains $removedReference) {
        throw "The EditMode test assembly retains removed production reference $removedReference"
    }
}
if ($testAssembly.name -ne 'TorProduction.Addressables.Editor.Tests' -or
    $testAssembly.rootNamespace -ne 'TorProduction.Addressables.Editor.Tests' -or
    @($testAssembly.precompiledReferences) -notcontains 'nunit.framework.dll') {
    throw 'The EditMode test assembly identity or NUnit-only test boundary is invalid.'
}
if (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'Documentation~/API_SURFACE.txt')) -or
    -not (Test-Path -LiteralPath (Join-Path $packageRoot 'Documentation~/PHASE_6_BREAKING_CHANGES.md'))) {
    throw 'Phase 6 API snapshot or breaking-change record is missing.'
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
    $workflowContents = Get-Content -Raw -LiteralPath $workflow.FullName
    if ($workflowContents -match '(?im)^\s*[^#].*npm\s+publish|packages:\s*write') {
        throw "A publication-capable workflow remains: $($workflow.Name)"
    }
    if ($workflowContents -match 'ACTIONS_ALLOW_USE_UNSECURE_NODE_VERSION') {
        throw "A workflow suppresses the GitHub Actions Node runtime safety check: $($workflow.Name)"
    }
}

$unityWorkflow = Get-Content -Raw -LiteralPath (Join-Path $root '.github/workflows/unity_phase_zero.yml')
if ($unityWorkflow -notmatch '(?m)^name:\s*Unity compatibility validation\s*$') {
    throw 'The paid Unity workflow must use its phase-neutral display name.'
}
if ($unityWorkflow -match '(?m)^\s{2}pull_request:') {
    throw 'The paid Unity matrix must not run automatically for pull requests.'
}
if ($unityWorkflow -match '(?m)^\s{4}branches:') {
    throw 'The paid Unity matrix must not run automatically for branch pushes.'
}
if ($unityWorkflow -notmatch '(?ms)^on:\s*\r?\n\s{2}workflow_dispatch:\s*$') {
    throw 'The paid Unity workflow must be manual dispatch only.'
}
if ($unityWorkflow -notmatch '(?ms)^concurrency:\s*\r?\n\s{2}group:\s*\$\{\{\s*github\.workflow\s*\}\}-\$\{\{\s*github\.ref\s*\}\}\s*\r?\n\s{2}cancel-in-progress:\s*true\s*$') {
    throw 'The Unity workflow must cancel an in-progress run for the same workflow and ref.'
}
if ($unityWorkflow -notmatch 'Assert-UnityLicenseEnvironment\.ps1') {
    throw 'The Unity workflow is missing its license-secret preflight.'
}
foreach ($secretName in @('UNITY_LICENSE', 'UNITY_EMAIL', 'UNITY_PASSWORD', 'UNITY_SERIAL')) {
    if ($unityWorkflow -notmatch [regex]::Escape("secrets.$secretName")) {
        throw "The Unity workflow does not map the $secretName secret."
    }
}
if ($unityWorkflow -notmatch 'game-ci/unity-test-runner@0ff419b913a3630032cbe0de48a0099b5a9f0ed9') {
    throw 'The Unity workflow must retain the reviewed stable GameCI test-runner pin.'
}

Write-Output "Phase 0 static validation passed for Addressables $ExpectedHostAddressablesVersion."
