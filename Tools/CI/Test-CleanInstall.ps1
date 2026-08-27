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

    [string]$PackageArchivePath,

    [string]$PackageGitUrl,

    [ValidateRange(1, 10000)]
    [int]$ExpectedEditModeTestCount = 133,

    [switch]$ExcludeSamples,

    [switch]$ImportSample,

    [switch]$RunPlayMode
)

$ErrorActionPreference = 'Stop'

$resolvedUnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$resolvedPackageArchivePath = if ([string]::IsNullOrWhiteSpace($PackageArchivePath)) {
    $null
} else {
    (Resolve-Path -LiteralPath $PackageArchivePath).Path
}
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
$playModeLogPath = Join-Path $temporaryArtifactsPath "playmode-$AddressablesVersion.log"
$playModeResultsPath = Join-Path $temporaryArtifactsPath "playmode-$AddressablesVersion.xml"
$playModePrepareLogPath = Join-Path $temporaryArtifactsPath "playmode-prepare-$AddressablesVersion.log"
$playModeCleanupLogPath = Join-Path $temporaryArtifactsPath "playmode-cleanup-$AddressablesVersion.log"
$removalLogPath = Join-Path $temporaryArtifactsPath "removal-$AddressablesVersion.log"
$sampleRemovalLogPath = Join-Path $temporaryArtifactsPath "sample-removal-$AddressablesVersion.log"
$importedSampleRoot = Join-Path $temporaryProjectPath `
    'Assets/Samples/Tor Production Addressables Toolpack/0.1.0-preview.1/Basic Setup'
$unrelatedSentinelPath = Join-Path $temporaryProjectPath 'Assets/PhaseSixUnrelatedState.txt'
$unrelatedSentinelMetaPath = $unrelatedSentinelPath + '.meta'

if ($ExcludeSamples -and $ImportSample) {
    throw 'ExcludeSamples and ImportSample are mutually exclusive.'
}
if ($ExcludeSamples -and $resolvedPackageArchivePath) {
    throw 'ExcludeSamples is available only for package-path validation, not immutable archive validation.'
}
if ($resolvedPackageArchivePath -and -not [string]::IsNullOrWhiteSpace($PackageGitUrl)) {
    throw 'PackageArchivePath and PackageGitUrl are mutually exclusive.'
}
if ($ExcludeSamples -and -not [string]::IsNullOrWhiteSpace($PackageGitUrl)) {
    throw 'ExcludeSamples is available only for package-path validation, not immutable Git-tag validation.'
}
if (-not [string]::IsNullOrWhiteSpace($PackageGitUrl) -and
    $PackageGitUrl -notmatch '^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\.git\?path=/[^#]+#[A-Za-z0-9_.-]+$') {
    throw "PackageGitUrl must be a pinned GitHub HTTPS UPM path reference: $PackageGitUrl"
}

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

if ($ImportSample) {
    [IO.File]::WriteAllText(
        $unrelatedSentinelPath,
        "Unrelated disposable-project state must survive sample and package removal.`n",
        [Text.UTF8Encoding]::new($false))
}

function Remove-TemporaryRoot() {
    if (-not (Test-Path -LiteralPath $temporaryRoot)) {
        return
    }
    Assert-TemporaryPath $temporaryRoot
    for ($attempt = 1; $attempt -le 15; $attempt++) {
        try {
            Remove-Item -Recurse -Force -LiteralPath $temporaryRoot -ErrorAction Stop
            return
        } catch {
            if ($attempt -eq 15) {
                throw
            }
            Start-Sleep -Milliseconds 1000
        }
    }
}

function Assert-UnrelatedSentinel([string]$ExpectedAssetHash, [string]$ExpectedMetaHash) {
    foreach ($path in @($unrelatedSentinelPath, $unrelatedSentinelMetaPath)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Sample or package removal deleted unrelated project state: $path"
        }
    }

    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $unrelatedSentinelPath).Hash -ne $ExpectedAssetHash -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath $unrelatedSentinelMetaPath).Hash -ne $ExpectedMetaHash) {
        throw 'Sample or package removal modified unrelated project state.'
    }
}

function Remove-ImportedSample() {
    Assert-TemporaryPath $importedSampleRoot
    if (-not (Test-Path -LiteralPath $importedSampleRoot)) {
        throw "Imported BasicSetup sample is missing before removal: $importedSampleRoot"
    }

    Remove-Item -Recurse -Force -LiteralPath $importedSampleRoot
    $importedSampleMetaPath = $importedSampleRoot + '.meta'
    if (Test-Path -LiteralPath $importedSampleMetaPath) {
        Remove-Item -Force -LiteralPath $importedSampleMetaPath
    }

    $samplesRoot = Join-Path $temporaryProjectPath 'Assets/Samples'
    $parent = Split-Path -Parent $importedSampleRoot
    while ($parent.StartsWith($samplesRoot, [StringComparison]::OrdinalIgnoreCase)) {
        if (-not (Test-Path -LiteralPath $parent)) {
            $parent = Split-Path -Parent $parent
            continue
        }
        if (@(Get-ChildItem -Force -LiteralPath $parent).Count -ne 0) {
            break
        }

        Assert-TemporaryPath $parent
        Remove-Item -Force -LiteralPath $parent
        $parentMetaPath = $parent + '.meta'
        if (Test-Path -LiteralPath $parentMetaPath) {
            Remove-Item -Force -LiteralPath $parentMetaPath
        }
        if ([String]::Equals($parent, $samplesRoot, [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $parent = Split-Path -Parent $parent
    }
}

foreach ($artifactName in @(
    "editmode-$AddressablesVersion.log",
    "editmode-$AddressablesVersion.xml",
    "playmode-$AddressablesVersion.log",
    "playmode-$AddressablesVersion.xml",
    "playmode-prepare-$AddressablesVersion.log",
    "playmode-cleanup-$AddressablesVersion.log",
    "sample-removal-$AddressablesVersion.log",
    "removal-$AddressablesVersion.log")) {
    $previousArtifactPath = Join-Path $resolvedArtifactsPath $artifactName
    if (Test-Path -LiteralPath $previousArtifactPath) {
        Remove-Item -Force -LiteralPath $previousArtifactPath
    }
}

try {
    if ($resolvedPackageArchivePath) {
        $temporaryArchivePath = Join-Path $temporaryRoot (Split-Path -Leaf $resolvedPackageArchivePath)
        Copy-Item -LiteralPath $resolvedPackageArchivePath -Destination $temporaryArchivePath
    } elseif ([string]::IsNullOrWhiteSpace($PackageGitUrl)) {
        Copy-Item -Recurse -LiteralPath $resolvedPackagePath -Destination $temporaryPackagePath
    }
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

    if ($resolvedPackageArchivePath -or -not [string]::IsNullOrWhiteSpace($PackageGitUrl)) {
        $manifestPath = Join-Path $temporaryProjectPath 'Packages/manifest.json'
        $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
        $packageReference = if ($resolvedPackageArchivePath) {
            'file:' + $temporaryArchivePath.Replace('\', '/')
        } else {
            $PackageGitUrl
        }
        $manifest.dependencies.'com.torproduction.addressables' = $packageReference
        [IO.File]::WriteAllText(
            $manifestPath,
            ($manifest | ConvertTo-Json -Depth 20) + [Environment]::NewLine,
            [Text.UTF8Encoding]::new($false))
    }

    if ($ExcludeSamples) {
        $samplesPath = Join-Path $temporaryPackagePath 'Samples~'
        $samplesMetaPath = Join-Path $temporaryPackagePath 'Samples~.meta'
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
    if ($ImportSample) {
        $arguments += '-torSampleImported'
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
    if ($testRun.result -ne 'Passed' -or
        [int]$testRun.total -ne $ExpectedEditModeTestCount -or
        [int]$testRun.passed -ne $ExpectedEditModeTestCount -or
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

    if ($RunPlayMode) {
        $fixtureRunnerDirectory = Join-Path $temporaryProjectPath 'Assets/Editor'
        New-Item -ItemType Directory -Force -Path $fixtureRunnerDirectory | Out-Null
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PlayModeFixtureRunner.cs') `
            -Destination $fixtureRunnerDirectory

        $playModePrepareArguments = @(
            '-batchmode',
            '-nographics',
            '-quit',
            '-projectPath', $temporaryProjectPath,
            '-executeMethod', 'TorProduction.Addressables.ReleaseReadiness.PlayModeFixtureRunner.Prepare',
            '-torReleaseReadinessPlayMode',
            '-logFile', $playModePrepareLogPath
        )
        $startProcessParameters.ArgumentList = $playModePrepareArguments
        $playModePrepareProcess = Start-Process @startProcessParameters
        Wait-Process -Id $playModePrepareProcess.Id
        $playModePrepareProcess.Refresh()
        if (-not (Test-Path -LiteralPath $playModePrepareLogPath)) {
            throw 'Unity did not produce a PlayMode fixture-setup log file.'
        }
        Copy-Item -Force -LiteralPath $playModePrepareLogPath -Destination $resolvedArtifactsPath
        if ($playModePrepareProcess.ExitCode -ne 0) {
            throw "Unity exited with code $($playModePrepareProcess.ExitCode) during PlayMode fixture setup. See $resolvedArtifactsPath"
        }
        $playModePrepareFailures = Select-String -LiteralPath $playModePrepareLogPath -Pattern $failurePattern
        if ($playModePrepareFailures) {
            throw "Unity log contains a PlayMode fixture-setup failure. See $resolvedArtifactsPath"
        }

        $playModeArguments = @(
            '-batchmode',
            '-nographics',
            '-projectPath', $temporaryProjectPath,
            '-runTests',
            '-testPlatform', 'PlayMode',
            '-testFilter', 'TorProduction.Addressables.PlayMode.Tests.ReleaseReadinessPlayModeTests',
            '-testResults', $playModeResultsPath,
            '-torReleaseReadinessPlayMode',
            '-logFile', $playModeLogPath
        )
        $startProcessParameters.ArgumentList = $playModeArguments
        $playModeProcess = Start-Process @startProcessParameters
        Wait-Process -Id $playModeProcess.Id
        $playModeProcess.Refresh()

        if (-not (Test-Path -LiteralPath $playModeLogPath)) {
            throw 'Unity did not produce a PlayMode log file.'
        }
        Copy-Item -Force -LiteralPath $playModeLogPath -Destination $resolvedArtifactsPath
        if (Test-Path -LiteralPath $playModeResultsPath) {
            Copy-Item -Force -LiteralPath $playModeResultsPath -Destination $resolvedArtifactsPath
        }
        if ($playModeProcess.ExitCode -ne 0) {
            throw "Unity exited with code $($playModeProcess.ExitCode) during PlayMode tests. See $resolvedArtifactsPath"
        }
        if (-not (Test-Path -LiteralPath $playModeResultsPath)) {
            throw 'Unity did not produce PlayMode test results.'
        }

        [xml]$playModeResults = Get-Content -Raw -LiteralPath $playModeResultsPath
        $playModeRun = $playModeResults.'test-run'
        if ($playModeRun.result -ne 'Passed' -or
            [int]$playModeRun.total -ne 1 -or
            [int]$playModeRun.passed -ne 1 -or
            [int]$playModeRun.failed -ne 0 -or
            [int]$playModeRun.inconclusive -ne 0 -or
            [int]$playModeRun.skipped -ne 0) {
            throw "Unexpected PlayMode result: total=$($playModeRun.total), passed=$($playModeRun.passed), failed=$($playModeRun.failed), inconclusive=$($playModeRun.inconclusive), skipped=$($playModeRun.skipped)"
        }
        $playModeFailures = Select-String -LiteralPath $playModeLogPath -Pattern $failurePattern
        if ($playModeFailures) {
            throw "Unity log contains a PlayMode compilation/runtime failure. See $resolvedArtifactsPath"
        }

        $playModeCleanupArguments = @(
            '-batchmode',
            '-nographics',
            '-quit',
            '-projectPath', $temporaryProjectPath,
            '-executeMethod', 'TorProduction.Addressables.ReleaseReadiness.PlayModeFixtureRunner.Cleanup',
            '-torReleaseReadinessPlayMode',
            '-logFile', $playModeCleanupLogPath
        )
        $startProcessParameters.ArgumentList = $playModeCleanupArguments
        $playModeCleanupProcess = Start-Process @startProcessParameters
        Wait-Process -Id $playModeCleanupProcess.Id
        $playModeCleanupProcess.Refresh()
        if (-not (Test-Path -LiteralPath $playModeCleanupLogPath)) {
            throw 'Unity did not produce a PlayMode fixture-cleanup log file.'
        }
        Copy-Item -Force -LiteralPath $playModeCleanupLogPath -Destination $resolvedArtifactsPath
        if ($playModeCleanupProcess.ExitCode -ne 0) {
            throw "Unity exited with code $($playModeCleanupProcess.ExitCode) during PlayMode fixture cleanup. See $resolvedArtifactsPath"
        }
        $playModeCleanupFailures = Select-String -LiteralPath $playModeCleanupLogPath -Pattern $failurePattern
        if ($playModeCleanupFailures) {
            throw "Unity log contains a PlayMode fixture-cleanup failure. See $resolvedArtifactsPath"
        }
        & (Join-Path $PSScriptRoot 'Assert-InertProject.ps1') -ProjectPath $temporaryProjectPath
    }

    if ($ImportSample) {
        $unrelatedSentinelHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $unrelatedSentinelPath).Hash
        $unrelatedSentinelMetaHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $unrelatedSentinelMetaPath).Hash
        Remove-ImportedSample

        $sampleRemovalArguments = @(
            '-batchmode',
            '-nographics',
            '-quit',
            '-projectPath', $temporaryProjectPath,
            '-logFile', $sampleRemovalLogPath
        )
        $startProcessParameters.ArgumentList = $sampleRemovalArguments
        $sampleRemovalProcess = Start-Process @startProcessParameters
        Wait-Process -Id $sampleRemovalProcess.Id
        $sampleRemovalProcess.Refresh()

        if (-not (Test-Path -LiteralPath $sampleRemovalLogPath)) {
            throw 'Unity did not produce a sample-removal log file.'
        }
        Copy-Item -Force -LiteralPath $sampleRemovalLogPath -Destination $resolvedArtifactsPath
        if ($sampleRemovalProcess.ExitCode -ne 0) {
            throw "Unity exited with code $($sampleRemovalProcess.ExitCode) after sample removal. See $resolvedArtifactsPath"
        }
        if (Test-Path -LiteralPath $importedSampleRoot) {
            throw 'Imported BasicSetup sample remains after explicit removal.'
        }
        Assert-UnrelatedSentinel $unrelatedSentinelHash $unrelatedSentinelMetaHash
        & (Join-Path $PSScriptRoot 'Assert-InertProject.ps1') -ProjectPath $temporaryProjectPath
        $sampleRemovalFailures = Select-String -LiteralPath $sampleRemovalLogPath -Pattern $failurePattern
        if ($sampleRemovalFailures) {
            throw "Unity log contains a sample-removal failure. See $resolvedArtifactsPath"
        }
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

    if ($ImportSample) {
        Assert-UnrelatedSentinel $unrelatedSentinelHash $unrelatedSentinelMetaHash
    }

    $removalFailures = Select-String -LiteralPath $removalLogPath -Pattern $failurePattern
    if ($removalFailures) {
        throw "Unity log contains a package-removal failure. See $resolvedArtifactsPath"
    }

    $sampleResult = if ($ImportSample) { ', sample import/removal passed' } elseif ($ExcludeSamples) { ', Samples~ absent' } else { '' }
    $playModeResult = if ($RunPlayMode) { ', PlayMode 1/1 passed' } else { '' }
    $installKind = if ($resolvedPackageArchivePath) {
        'archive'
    } elseif (-not [string]::IsNullOrWhiteSpace($PackageGitUrl)) {
        'Git tag'
    } else {
        'path'
    }
    Write-Output "Clean-install and removal ($installKind) Addressables $AddressablesVersion passed: total=$($testRun.total), passed=$($testRun.passed)$playModeResult$sampleResult"
} finally {
    Remove-TemporaryRoot
}
