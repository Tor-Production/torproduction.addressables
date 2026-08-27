[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$manifestPath = Join-Path $resolvedPackagePath 'package.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

if ($manifest.name -ne 'com.torproduction.addressables') {
    throw "Unexpected package name: $($manifest.name)"
}
if ($manifest.version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$') {
    throw "Package version is not valid SemVer: $($manifest.version)"
}
if ($manifest.displayName -ne 'Tor Production Addressables Toolpack' -or
    [string]::IsNullOrWhiteSpace($manifest.description) -or
    $manifest.description.Length -lt 50) {
    throw 'Package displayName and a descriptive summary of at least 50 characters are required.'
}

$validationExceptions = Get-Content -Raw -LiteralPath `
    (Join-Path $resolvedPackagePath 'ValidationExceptions.json') | ConvertFrom-Json
if (@($validationExceptions.ErrorExceptions).Count -ne 1 -or
    $validationExceptions.ErrorExceptions[0].ValidationTest -ne 'Manifest Validation' -or
    $validationExceptions.ErrorExceptions[0].PackageVersion -ne $manifest.version -or
    $validationExceptions.ErrorExceptions[0].ExceptionMessage -notmatch 'approved company names' -or
    @($validationExceptions.WarningExceptions).Count -ne 0) {
    throw 'ValidationExceptions.json must contain only the exact version-scoped non-Unity namespace exception.'
}
if ($manifest.unity -ne '6000.0') {
    throw "Unexpected Unity minimum: $($manifest.unity)"
}
if ($manifest.license -ne 'MIT') {
    throw "The manifest license field changed without an approved legal decision: $($manifest.license)"
}
if ($manifest.author.name -ne 'Tor Production') {
    throw "Unexpected package author metadata: $($manifest.author.name)"
}
if ($manifest.repository.type -ne 'git' -or
    $manifest.repository.url -ne 'git+https://github.com/Yurii-Tor/torproduction.addressables.git') {
    throw 'Package repository metadata does not match the configured repository.'
}

$dependencyNames = @($manifest.dependencies.PSObject.Properties.Name)
if ($dependencyNames.Count -ne 1 -or
    $dependencyNames[0] -ne 'com.unity.addressables' -or
    $manifest.dependencies.'com.unity.addressables' -ne '2.7.6') {
    throw 'The package must declare exactly Addressables 2.7.6 as its production dependency.'
}
if ($manifest.PSObject.Properties.Name -contains 'publishConfig' -or
    $manifest.PSObject.Properties.Name -contains 'scripts') {
    throw 'Publication or npm lifecycle configuration is not allowed in the package manifest.'
}

if (@($manifest.samples).Count -ne 1 -or
    $manifest.samples[0].displayName -ne 'Basic Setup' -or
    $manifest.samples[0].path -ne 'Samples~/BasicSetup' -or
    [string]::IsNullOrWhiteSpace($manifest.samples[0].description)) {
    throw 'The manifest must declare exactly the curated Samples~/BasicSetup sample.'
}

$requiredFiles = @(
    'README.md',
    'CHANGELOG.md',
    'LICENSE.md',
    'Third Party Notices.md',
    'ValidationExceptions.json',
    'Editor/TorProduction.Addressables.Editor.asmdef',
    'Tests/Editor/TorProduction.Addressables.Editor.Tests.asmdef',
    'Tests/Runtime/TorProduction.Addressables.PlayMode.Tests.asmdef',
    'Samples~/BasicSetup.meta',
    'Samples~/BasicSetup/Editor.meta',
    'Samples~/BasicSetup/Editor/BasicSetupAddressablesAutomationConfig.asset',
    'Samples~/BasicSetup/Editor/BasicSetupAddressablesAutomationConfig.asset.meta',
    'Samples~/BasicSetup/Scenes.meta',
    'Samples~/BasicSetup/Scenes/SampleScene.unity',
    'Samples~/BasicSetup/Scenes/SampleScene.unity.meta',
    'Documentation~/com.torproduction.addressables.md',
    'Documentation~/INSTALLATION.md',
    'Documentation~/COMPATIBILITY.md',
    'Documentation~/CONFIGURATION.md',
    'Documentation~/GROUP_SYNCHRONIZATION.md',
    'Documentation~/SCENE_SYNCHRONIZATION.md',
    'Documentation~/DEPENDENCY_ANALYSIS.md',
    'Documentation~/BUILD_PIPELINE.md',
    'Documentation~/CLI.md',
    'Documentation~/SAMPLES.md',
    'Documentation~/LIMITATIONS.md',
    'Documentation~/TROUBLESHOOTING.md',
    'Documentation~/CONTRIBUTING.md',
    'Documentation~/RELEASE_PROCESS.md',
    'Documentation~/RELEASE_READINESS.md',
    'Documentation~/API_SURFACE.txt',
    'Documentation~/PHASE_6_BREAKING_CHANGES.md',
    'Documentation~/SAFETY.md'
)
foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedPackagePath $relativePath) -PathType Leaf)) {
        throw "Required package file is missing: $relativePath"
    }
}

if (Test-Path -LiteralPath (Join-Path $resolvedPackagePath 'Documentation~/filter.yml')) {
    throw 'Documentation~/filter.yml is not permitted to suppress public API XML-documentation requirements.'
}

$changelog = Get-Content -Raw -LiteralPath (Join-Path $resolvedPackagePath 'CHANGELOG.md')
$escapedVersion = [regex]::Escape([string]$manifest.version)
if ($changelog -notmatch "(?m)^## \[$escapedVersion\] - \d{4}-\d{2}-\d{2}\s*$") {
    throw "CHANGELOG.md has no dated heading for package version $($manifest.version)."
}

$allowedTopLevel = @(
    'CHANGELOG.md', 'CHANGELOG.md.meta',
    'Documentation~',
    'Editor', 'Editor.meta',
    'LICENSE.md', 'LICENSE.md.meta',
    'README.md', 'README.md.meta',
    'Samples~',
    'Tests', 'Tests.meta',
    'Third Party Notices.md', 'Third Party Notices.md.meta',
    'ValidationExceptions.json', 'ValidationExceptions.json.meta',
    'package.json', 'package.json.meta'
)
foreach ($item in Get-ChildItem -Force -LiteralPath $resolvedPackagePath) {
    if ($allowedTopLevel -notcontains $item.Name) {
        throw "Forbidden or unexpected package-root item: $($item.Name)"
    }
}

if (Test-Path -LiteralPath (Join-Path $resolvedPackagePath 'Samples~.meta')) {
    throw 'The hidden package sample root Samples~ must not have a .meta file; Unity recreates the hidden folder and logs a warning.'
}

$forbiddenDirectoryNames = @('Library', 'Temp', 'Logs', 'artifacts', '.git', '.github')
$forbiddenExtensions = @('.mp4', '.mov', '.avi', '.mkv', '.webm')
foreach ($item in Get-ChildItem -Recurse -Force -LiteralPath $resolvedPackagePath) {
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Package content must not contain links or reparse points: $($item.FullName)"
    }
    $isRootProjectSettings = $item.PSIsContainer -and
        $item.Name -eq 'ProjectSettings' -and
        [String]::Equals($item.Parent.FullName, $resolvedPackagePath, [StringComparison]::OrdinalIgnoreCase)
    if ($item.PSIsContainer -and
        (($forbiddenDirectoryNames -contains $item.Name) -or $isRootProjectSettings)) {
        throw "Forbidden generated/host directory in package: $($item.FullName)"
    }
    if (-not $item.PSIsContainer -and $forbiddenExtensions -contains $item.Extension.ToLowerInvariant()) {
        throw "Video files are not permitted in package content: $($item.FullName)"
    }
}

if (@(Get-ChildItem -Recurse -File -LiteralPath (Join-Path $resolvedPackagePath 'Samples~') -Filter '*.asmdef').Count -ne 0) {
    throw 'The Basic Setup sample must not compile a sample assembly.'
}

$metaGuids = @{}
foreach ($item in Get-ChildItem -Recurse -Force -LiteralPath $resolvedPackagePath) {
    $relativePath = $item.FullName.Substring($resolvedPackagePath.Length).TrimStart('\', '/')
    if ($relativePath -eq 'Documentation~' -or
        $relativePath.StartsWith('Documentation~\') -or
        $relativePath.StartsWith('Documentation~/')) {
        continue
    }
    if ($item.PSIsContainer) {
        $isHiddenSamplesRoot = $relativePath -eq 'Samples~'
        if (-not $isHiddenSamplesRoot -and -not (Test-Path -LiteralPath ($item.FullName + '.meta'))) {
            throw "Unity folder is missing its .meta file: $relativePath"
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
        throw "Unity asset is missing its .meta file: $relativePath"
    }
}

Write-Output "Package manifest/content validation passed: $($manifest.name)@$($manifest.version)"
