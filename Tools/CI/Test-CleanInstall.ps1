[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [ValidateSet('2.7.6', '2.9.1')]
    [string]$AddressablesVersion,

    [Parameter(Mandatory = $true)]
    [string]$ArtifactsPath,

    [switch]$ExcludeSamples
)

$ErrorActionPreference = 'Stop'

$resolvedUnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$repositoryRoot = Split-Path -Parent $resolvedPackagePath
$sourceProjectPath = Join-Path $repositoryRoot 'AddressablesProject'
$resolvedArtifactsPath = [IO.Path]::GetFullPath($ArtifactsPath)
$temporaryBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
$temporaryRoot = Join-Path $temporaryBase ("TorProductionAddressables-" + [Guid]::NewGuid().ToString('N'))
$temporaryProjectPath = Join-Path $temporaryRoot 'AddressablesProject'
$temporaryPackagePath = Join-Path $temporaryRoot 'com.torproduction.addressables'
$temporaryArtifactsPath = Join-Path $temporaryRoot 'artifacts'
$logPath = Join-Path $temporaryArtifactsPath "editmode-$AddressablesVersion.log"
$resultsPath = Join-Path $temporaryArtifactsPath "editmode-$AddressablesVersion.xml"
$removalLogPath = Join-Path $temporaryArtifactsPath "removal-$AddressablesVersion.log"

function Assert-TemporaryPath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith(
        $temporaryBase + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to mutate a path outside the system temporary directory: $fullPath"
    }
}

Assert-TemporaryPath $temporaryRoot
New-Item -ItemType Directory -Path `
    (Join-Path $temporaryProjectPath 'Assets'), `
    (Join-Path $temporaryProjectPath 'Packages'), `
    (Join-Path $temporaryProjectPath 'ProjectSettings'), `
    $temporaryArtifactsPath, `
    $resolvedArtifactsPath -Force | Out-Null

foreach ($artifactName in @(
    "editmode-$AddressablesVersion.log",
    "editmode-$AddressablesVersion.xml",
    "removal-$AddressablesVersion.log")) {
    $previousArtifactPath = Join-Path $resolvedArtifactsPath $artifactName
    if (Test-Path -LiteralPath $previousArtifactPath) {
        Remove-Item -Force -LiteralPath $previousArtifactPath
    }
}

try {
    Copy-Item -Recurse -LiteralPath $resolvedPackagePath -Destination $temporaryPackagePath
    Copy-Item -LiteralPath `
        (Join-Path $sourceProjectPath 'Packages/manifest.json'), `
        (Join-Path $sourceProjectPath 'Packages/packages-lock.json') `
        -Destination (Join-Path $temporaryProjectPath 'Packages')
    Copy-Item -LiteralPath `
        (Join-Path $sourceProjectPath 'ProjectSettings/EditorBuildSettings.asset'), `
        (Join-Path $sourceProjectPath 'ProjectSettings/EditorSettings.asset'), `
        (Join-Path $sourceProjectPath 'ProjectSettings/ProjectSettings.asset'), `
        (Join-Path $sourceProjectPath 'ProjectSettings/ProjectVersion.txt'), `
        (Join-Path $sourceProjectPath 'ProjectSettings/VersionControlSettings.asset') `
        -Destination (Join-Path $temporaryProjectPath 'ProjectSettings')

    if ($ExcludeSamples) {
        $samplesPath = Join-Path $temporaryPackagePath 'Samples'
        $samplesMetaPath = Join-Path $temporaryPackagePath 'Samples.meta'
        Assert-TemporaryPath $samplesPath
        Remove-Item -Recurse -Force -LiteralPath $samplesPath
        Remove-Item -Force -LiteralPath $samplesMetaPath
    }

    & (Join-Path $PSScriptRoot 'Set-AddressablesVersion.ps1') `
        -AddressablesVersion $AddressablesVersion `
        -ProjectPath $temporaryProjectPath

    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $temporaryProjectPath,
        '-runTests',
        '-testPlatform', 'EditMode',
        '-testResults', $resultsPath,
        '-logFile', $logPath,
        '-torCleanInstall'
    )
    if ($ExcludeSamples) {
        $arguments += '-torSamplesExcluded'
    }
    $startProcessParameters = @{
        FilePath = $resolvedUnityPath
        ArgumentList = $arguments
        PassThru = $true
    }
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        $startProcessParameters.WindowStyle = 'Hidden'
    }

    $process = Start-Process @startProcessParameters
    Wait-Process -Id $process.Id
    $process.Refresh()

    if (-not (Test-Path -LiteralPath $logPath)) {
        throw 'Unity did not produce a log file.'
    }
    Copy-Item -Force -LiteralPath $logPath -Destination $resolvedArtifactsPath

    if (Test-Path -LiteralPath $resultsPath) {
        Copy-Item -Force -LiteralPath $resultsPath -Destination $resolvedArtifactsPath
    }

    if ($process.ExitCode -ne 0) {
        throw "Unity exited with code $($process.ExitCode). See $resolvedArtifactsPath"
    }
    if (-not (Test-Path -LiteralPath $resultsPath)) {
        throw 'Unity did not produce EditMode test results.'
    }

    [xml]$results = Get-Content -Raw -LiteralPath $resultsPath
    $testRun = $results.'test-run'
    $expectedTestCount = 125
    if ($testRun.result -ne 'Passed' -or
        [int]$testRun.total -ne $expectedTestCount -or
        [int]$testRun.passed -ne $expectedTestCount -or
        [int]$testRun.failed -ne 0 -or
        [int]$testRun.inconclusive -ne 0 -or
        [int]$testRun.skipped -ne 0) {
        throw "Unexpected EditMode result: total=$($testRun.total), passed=$($testRun.passed), failed=$($testRun.failed), inconclusive=$($testRun.inconclusive), skipped=$($testRun.skipped)"
    }

    & (Join-Path $PSScriptRoot 'Assert-InertProject.ps1') -ProjectPath $temporaryProjectPath

    $failurePattern = 'error CS\d+|Scripts have compiler errors|compilation failed|NullReferenceException|Unhandled Exception|Fatal Error'
    $failures = Select-String -LiteralPath $logPath -Pattern $failurePattern
    if ($failures) {
        throw "Unity log contains a compilation/import failure. See $resolvedArtifactsPath"
    }

    $manifestPath = Join-Path $temporaryProjectPath 'Packages/manifest.json'
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $manifest.dependencies.PSObject.Properties.Remove('com.torproduction.addressables')
    $manifest.testables = @()
    [IO.File]::WriteAllText(
        $manifestPath,
        ($manifest | ConvertTo-Json -Depth 20) + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))

    $removalArguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $temporaryProjectPath,
        '-logFile', $removalLogPath
    )
    $startProcessParameters.ArgumentList = $removalArguments
    $removalProcess = Start-Process @startProcessParameters
    Wait-Process -Id $removalProcess.Id
    $removalProcess.Refresh()

    if (-not (Test-Path -LiteralPath $removalLogPath)) {
        throw 'Unity did not produce a package-removal log file.'
    }
    Copy-Item -Force -LiteralPath $removalLogPath -Destination $resolvedArtifactsPath

    if ($removalProcess.ExitCode -ne 0) {
        throw "Unity exited with code $($removalProcess.ExitCode) after package removal. See $resolvedArtifactsPath"
    }

    & (Join-Path $PSScriptRoot 'Assert-InertProject.ps1') -ProjectPath $temporaryProjectPath

    $removalFailures = Select-String -LiteralPath $removalLogPath -Pattern $failurePattern
    if ($removalFailures) {
        throw "Unity log contains a package-removal failure. See $resolvedArtifactsPath"
    }

    Write-Output "Clean-install and removal Addressables $AddressablesVersion passed: total=$($testRun.total), passed=$($testRun.passed)"
} finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Assert-TemporaryPath $temporaryRoot
        Remove-Item -Recurse -Force -LiteralPath $temporaryRoot
    }
}
