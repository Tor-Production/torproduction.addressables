[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$ArtifactsPath,

    [string]$PackageValidationSuiteVersion = '0.86.0-preview'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedUnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$repositoryRoot = Split-Path -Parent $resolvedPackagePath
$sourceProjectPath = Join-Path $repositoryRoot 'AddressablesProject'
$resolvedArtifactsPath = [IO.Path]::GetFullPath($ArtifactsPath)
$temporaryBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
$temporaryRoot = Join-Path $temporaryBase ("TorProductionAddressablesPvs-" + [Guid]::NewGuid().ToString('N'))
$temporaryProjectPath = Join-Path $temporaryRoot 'AddressablesProject'
$temporaryPackagePath = Join-Path $temporaryRoot 'com.torproduction.addressables'
$temporaryArtifactsPath = Join-Path $temporaryRoot 'artifacts'
$runnerDirectory = Join-Path $temporaryProjectPath 'Assets/Editor'
$importLogPath = Join-Path $temporaryArtifactsPath 'package-validation-import.log'
$logPath = Join-Path $temporaryArtifactsPath 'package-validation.log'
$reportPath = Join-Path $temporaryArtifactsPath 'package-validation.txt'
$importFailurePattern = 'error CS\d+|Scripts have compiler errors|compilation failed|NullReferenceException|Unhandled Exception|Fatal Error'
$failurePattern = $importFailurePattern + '|DirectoryNotFoundException|Asset import failed'

function Assert-TemporaryPath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith(
        $temporaryBase + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to mutate a path outside the system temporary directory: $fullPath"
    }
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

& (Join-Path $PSScriptRoot 'Validate-PackageManifest.ps1') -PackagePath $resolvedPackagePath
$packageManifest = Get-Content -Raw -LiteralPath (Join-Path $resolvedPackagePath 'package.json') |
    ConvertFrom-Json

New-Item -ItemType Directory -Force -Path `
    $runnerDirectory,
    (Join-Path $temporaryProjectPath 'Packages'),
    (Join-Path $temporaryProjectPath 'ProjectSettings'),
    $temporaryArtifactsPath,
    $resolvedArtifactsPath | Out-Null

foreach ($artifactName in @(
    'package-validation-import.log',
    'package-validation.log',
    'package-validation.txt')) {
    $previousArtifactPath = Join-Path $resolvedArtifactsPath $artifactName
    if (Test-Path -LiteralPath $previousArtifactPath) {
        Remove-Item -Force -LiteralPath $previousArtifactPath
    }
}

try {
    Copy-Item -Recurse -LiteralPath $resolvedPackagePath -Destination $temporaryPackagePath
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PvsRunner.cs') -Destination $runnerDirectory
    Copy-Item -LiteralPath (Join-Path $sourceProjectPath 'Packages/manifest.json') `
        -Destination (Join-Path $temporaryProjectPath 'Packages')
    Copy-Item -LiteralPath `
        (Join-Path $sourceProjectPath 'ProjectSettings/EditorBuildSettings.asset'), `
        (Join-Path $sourceProjectPath 'ProjectSettings/EditorSettings.asset'), `
        (Join-Path $sourceProjectPath 'ProjectSettings/ProjectSettings.asset'), `
        (Join-Path $sourceProjectPath 'ProjectSettings/ProjectVersion.txt'), `
        (Join-Path $sourceProjectPath 'ProjectSettings/VersionControlSettings.asset') `
        -Destination (Join-Path $temporaryProjectPath 'ProjectSettings')

    $manifestPath = Join-Path $temporaryProjectPath 'Packages/manifest.json'
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $manifest.dependencies | Add-Member -NotePropertyName 'com.unity.package-validation-suite' `
        -NotePropertyValue $PackageValidationSuiteVersion -Force
    [IO.File]::WriteAllText(
        $manifestPath,
        ($manifest | ConvertTo-Json -Depth 20) + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))

    $importArguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $temporaryProjectPath,
        '-logFile', $importLogPath
    )
    $startProcessParameters = @{
        FilePath = $resolvedUnityPath
        ArgumentList = $importArguments
        PassThru = $true
    }
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        $startProcessParameters.WindowStyle = 'Hidden'
    }
    $importProcess = Start-Process @startProcessParameters
    Wait-Process -Id $importProcess.Id
    $importProcess.Refresh()
    if (-not (Test-Path -LiteralPath $importLogPath)) {
        throw 'Unity did not produce a Package Validation Suite import log.'
    }
    Copy-Item -Force -LiteralPath $importLogPath -Destination $resolvedArtifactsPath
    if ($importProcess.ExitCode -ne 0) {
        throw "Package Validation Suite project import exited with code $($importProcess.ExitCode). See $resolvedArtifactsPath"
    }
    $importFailures = Select-String -LiteralPath $importLogPath -Pattern $importFailurePattern
    if ($importFailures) {
        throw "Package Validation Suite import log contains a compilation/import failure. See $resolvedArtifactsPath"
    }
    & (Join-Path $PSScriptRoot 'Assert-InertProject.ps1') -ProjectPath $temporaryProjectPath

    $arguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $temporaryProjectPath,
        '-executeMethod', 'TorProduction.Addressables.ReleaseReadiness.PackageValidationRunner.Run',
        '-torPvsReport', $reportPath,
        '-torPackageVersion', $packageManifest.version,
        '-logFile', $logPath
    )
    $startProcessParameters.ArgumentList = $arguments
    $process = Start-Process @startProcessParameters
    Wait-Process -Id $process.Id
    $process.Refresh()

    if (-not (Test-Path -LiteralPath $logPath)) {
        throw 'Unity did not produce a Package Validation Suite log.'
    }
    Copy-Item -Force -LiteralPath $logPath -Destination $resolvedArtifactsPath
    if (Test-Path -LiteralPath $reportPath) {
        Copy-Item -Force -LiteralPath $reportPath -Destination $resolvedArtifactsPath
    }
    if ($process.ExitCode -ne 0) {
        throw "Package Validation Suite exited with code $($process.ExitCode). See $resolvedArtifactsPath"
    }
    if (-not (Test-Path -LiteralPath $reportPath)) {
        throw 'Package Validation Suite did not produce an exported report.'
    }

    & (Join-Path $PSScriptRoot 'Assert-InertProject.ps1') -ProjectPath $temporaryProjectPath
    $failures = Select-String -LiteralPath $logPath -Pattern $failurePattern
    if ($failures) {
        throw "Package Validation Suite log contains a compilation/runtime failure. See $resolvedArtifactsPath"
    }

    Write-Output "Package Validation Suite $PackageValidationSuiteVersion passed for $($packageManifest.name)@$($packageManifest.version)."
} finally {
    Remove-TemporaryRoot
}
