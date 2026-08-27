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
$importStdoutPath = Join-Path $temporaryArtifactsPath 'package-validation-import.stdout.txt'
$importStderrPath = Join-Path $temporaryArtifactsPath 'package-validation-import.stderr.txt'
$importProcessPath = Join-Path $temporaryArtifactsPath 'package-validation-import-process.json'
$pvsStdoutPath = Join-Path $temporaryArtifactsPath 'package-validation.stdout.txt'
$pvsStderrPath = Join-Path $temporaryArtifactsPath 'package-validation.stderr.txt'
$pvsProcessPath = Join-Path $temporaryArtifactsPath 'package-validation-process.json'
$directStdoutPath = Join-Path $temporaryArtifactsPath 'find-missing-docs.stdout.txt'
$directStderrPath = Join-Path $temporaryArtifactsPath 'find-missing-docs.stderr.txt'
$directProcessPath = Join-Path $temporaryArtifactsPath 'find-missing-docs-process.json'
$directResponsePath = Join-Path $temporaryArtifactsPath 'find-missing-docs-response.txt'
$directBundlePath = Join-Path $temporaryArtifactsPath 'find-missing-docs-bundle.json'
$classificationPath = Join-Path $temporaryArtifactsPath 'package-validation-classification.json'
$expectedCheckerSha256 = 'c571657558566c4b652a52ef2130a64af462274feca0da234bc9bf6d6ab6729b'
$outcomeStatement = 'All applicable PVS validations passed except the PVS 0.86.0-preview XML-documentation child-process launcher, which failed with the recorded upstream toolchain TypeLoadException. The same bundled FindMissingDocs checker was executed independently and confirmed that no public production APIs lack XML documentation.'

if ($PackageValidationSuiteVersion -ne '0.86.0-preview') {
    throw 'This audited PVS runner supports only com.unity.package-validation-suite@0.86.0-preview.'
}

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

function ConvertTo-CommandText([string]$FilePath, [string[]]$Arguments) {
    $quotedFilePath = '"' + $FilePath.Replace('"', '\"') + '"'
    return ($quotedFilePath + ' ' + ($Arguments -join ' ')).Trim()
}

function Invoke-CapturedProcess(
    [string]$FilePath,
    [string[]]$Arguments,
    [string]$WorkingDirectory,
    [string]$StdoutPath,
    [string]$StderrPath,
    [string]$ProcessRecordPath,
    [System.Collections.IDictionary]$Metadata
) {
    $parameters = @{
        FilePath = $FilePath
        ArgumentList = $Arguments
        WorkingDirectory = $WorkingDirectory
        RedirectStandardOutput = $StdoutPath
        RedirectStandardError = $StderrPath
        PassThru = $true
    }
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        $parameters.WindowStyle = 'Hidden'
    }

    $process = Start-Process @parameters
    # Accessing Handle before waiting preserves ExitCode on Windows PowerShell 5.1.
    $null = $process.Handle
    $process.WaitForExit()
    $process.Refresh()
    $exitCode = $process.ExitCode

    $stdout = [IO.File]::ReadAllText($StdoutPath)
    $stderr = [IO.File]::ReadAllText($StderrPath)
    $record = [ordered]@{
        filePath = $FilePath
        arguments = $Arguments
        renderedCommand = ConvertTo-CommandText $FilePath $Arguments
        workingDirectory = $WorkingDirectory
        exitCode = $exitCode
        stdoutLength = $stdout.Length
        stderrLength = $stderr.Length
        stdoutFile = Split-Path -Leaf $StdoutPath
        stderrFile = Split-Path -Leaf $StderrPath
    }
    foreach ($entry in $Metadata.GetEnumerator()) {
        $record[$entry.Key] = $entry.Value
    }
    [IO.File]::WriteAllText(
        $ProcessRecordPath,
        ($record | ConvertTo-Json -Depth 20) + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))

    return [pscustomobject]@{
        ExitCode = $exitCode
        Stdout = $stdout
        Stderr = $stderr
    }
}

function Get-PvsPackageRoot() {
    $packageCache = Join-Path $temporaryProjectPath 'Library/PackageCache'
    $candidates = @(Get-ChildItem -LiteralPath $packageCache -Directory -Filter 'com.unity.package-validation-suite@*')
    if ($candidates.Count -ne 1) {
        throw "Expected exactly one Package Validation Suite cache entry, found $($candidates.Count)."
    }

    $manifest = Get-Content -Raw -LiteralPath (Join-Path $candidates[0].FullName 'package.json') |
        ConvertFrom-Json
    if ($manifest.name -ne 'com.unity.package-validation-suite' -or
        $manifest.version -ne $PackageValidationSuiteVersion) {
        throw "Unexpected Package Validation Suite cache entry: $($manifest.name)@$($manifest.version)"
    }
    return $candidates[0].FullName
}

function Invoke-DirectXmlDocValidation([string]$PvsPackageRoot) {
    $sourceCheckerPath = Join-Path $PvsPackageRoot 'Bin~/FindMissingDocs/FindMissingDocs.exe'
    $sourceCheckerDirectory = Split-Path -Parent $sourceCheckerPath
    $checkerWorkingDirectory = Join-Path $temporaryRoot 'direct-find-missing-docs'
    $checkerPath = Join-Path $checkerWorkingDirectory 'FindMissingDocs.exe'
    $monoPath = Join-Path (Split-Path -Parent $resolvedUnityPath) 'Data/MonoBleedingEdge/bin/mono.exe'
    $sourcePath = Join-Path $PvsPackageRoot 'Editor/ValidationSuite/ValidationTests/Standards/US0041-APIDocumentationIncluded.cs'
    foreach ($requiredPath in @($sourceCheckerPath, $monoPath, $sourcePath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required PVS XML-documentation toolchain file is missing: $requiredPath"
        }
    }

    $source = Get-Content -Raw -LiteralPath $sourcePath
    if ($source -notmatch 'FindMissingDocs/FindMissingDocs\.exe' -or
        $source -notmatch 'Checks\s*\{\s*get;\s*\}\s*=\s*\{\s*"PVP-20-1"\s*\}') {
        throw 'The installed PVS source does not map PVP-20-1 to the bundled FindMissingDocs checker as expected.'
    }

    Copy-Item -Recurse -LiteralPath $sourceCheckerDirectory -Destination $checkerWorkingDirectory
    $sourceCheckerSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceCheckerPath).Hash.ToLowerInvariant()
    $executedCheckerSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $checkerPath).Hash.ToLowerInvariant()
    if ($sourceCheckerSha256 -ne $expectedCheckerSha256 -or
        $executedCheckerSha256 -ne $expectedCheckerSha256) {
        throw 'The bundled or disposable direct checker does not match the audited PVS 0.86.0-preview checker.'
    }

    $bundleInventory = [ordered]@{
        sourceDirectory = $sourceCheckerDirectory
        executedDirectory = $checkerWorkingDirectory
        sourceCheckerSha256 = $sourceCheckerSha256
        executedCheckerSha256 = $executedCheckerSha256
        files = @(Get-ChildItem -File -LiteralPath $checkerWorkingDirectory |
            Sort-Object Name |
            ForEach-Object {
                [ordered]@{
                    name = $_.Name
                    length = $_.Length
                    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
                }
            })
    }
    [IO.File]::WriteAllText(
        $directBundlePath,
        ($bundleInventory | ConvertTo-Json -Depth 20) + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))

    $excludedPaths = @(Get-ChildItem -Recurse -Directory -Force -LiteralPath $temporaryPackagePath |
        Where-Object {
            $_.Name.EndsWith('~', [StringComparison]::Ordinal) -or
            $_.Name.StartsWith('.', [StringComparison]::Ordinal) -or
            $_.Name -eq 'Tests'
        } |
        ForEach-Object { $_.FullName } |
        Sort-Object -Unique)
    if ($excludedPaths.Count -eq 0) {
        throw 'The direct XML-documentation checker unexpectedly has no package exclusions.'
    }
    $response = '--excluded-paths="' + ($excludedPaths -join ',') + '"'
    [IO.File]::WriteAllText($directResponsePath, $response, [Text.UTF8Encoding]::new($false))

    $arguments = @(
        ('"' + $checkerPath + '"'),
        ('--root-path="' + $temporaryPackagePath + '"'),
        ('--response-file="' + $directResponsePath + '"')
    )
    $filterPath = Join-Path $temporaryPackagePath 'Documentation~/filter.yml'
    if (Test-Path -LiteralPath $filterPath) {
        throw 'An XML-documentation filter is not permitted by the release-readiness gate.'
    }

    $result = Invoke-CapturedProcess `
        -FilePath $monoPath `
        -Arguments $arguments `
        -WorkingDirectory $checkerWorkingDirectory `
        -StdoutPath $directStdoutPath `
        -StderrPath $directStderrPath `
        -ProcessRecordPath $directProcessPath `
        -Metadata ([ordered]@{
            tool = 'FindMissingDocs.exe'
            checkId = 'PVP-20-1'
            pvsVersion = $PackageValidationSuiteVersion
            pvsPackageRoot = $PvsPackageRoot
            sourceCheckerPath = $sourceCheckerPath
            executedCheckerPath = $checkerPath
            sourceCheckerSha256 = $sourceCheckerSha256
            executedCheckerSha256 = $executedCheckerSha256
            monoSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $monoPath).Hash.ToLowerInvariant()
            responseFile = Split-Path -Leaf $directResponsePath
            filterFile = $null
        })

    if ($result.ExitCode -ne 0) {
        throw "Direct bundled FindMissingDocs execution exited with code $($result.ExitCode)."
    }
    if ($result.Stdout.Length -ne 0) {
        throw 'Direct bundled FindMissingDocs execution reported undocumented public production APIs.'
    }
    if ($result.Stderr.Length -ne 0) {
        throw 'Direct bundled FindMissingDocs execution produced stderr output.'
    }

    return [pscustomobject]@{
        SourceCheckerPath = $sourceCheckerPath
        ExecutedCheckerPath = $checkerPath
        SourceCheckerSha256 = $sourceCheckerSha256
        ExecutedCheckerSha256 = $executedCheckerSha256
        ExitCode = $result.ExitCode
        StdoutLength = $result.Stdout.Length
        StderrLength = $result.Stderr.Length
    }
}

function Assert-AllOtherPvsValidationsPassed([string]$Report) {
    $resultMatches = @([regex]::Matches(
        $Report,
        '(?m)^(?<status>Succeeded|Failed|NotRun) - "(?<name>[^"]+)"\s*$'))
    if ($resultMatches.Count -eq 0) {
        throw 'The PVS report contains no validation-result headings.'
    }

    $failed = @($resultMatches | Where-Object { $_.Groups['status'].Value -eq 'Failed' })
    if ($failed.Count -ne 1 -or $failed[0].Groups['name'].Value -ne 'Xmldoc Validation') {
        throw 'The PVS report contains a failure other than the one allowed Xmldoc Validation launcher failure.'
    }
    $notRun = @($resultMatches | Where-Object { $_.Groups['status'].Value -eq 'NotRun' })
    if ($notRun.Count -ne 1 -or $notRun[0].Groups['name'].Value -ne 'Package Diff Evaluation') {
        throw 'The PVS report contains an unexpected NotRun validation.'
    }

    $succeededNames = @($resultMatches |
        Where-Object { $_.Groups['status'].Value -eq 'Succeeded' } |
        ForEach-Object { $_.Groups['name'].Value })
    $requiredSucceeded = @(
        'Assembly Definition Validation',
        'Assets Validation',
        'ChangeLog Validation',
        'Folder Structure Validation',
        'X-ray Validations',
        'Package Lifecycle Validation',
        'Documentation Validation',
        'Manifest Validation',
        'Meta Files Validation',
        'Package Unity Version Validation',
        'Path Length Validation',
        'Required File Type Validation',
        'Samples Validation',
        'Unity Version Validation'
    )
    foreach ($requiredName in $requiredSucceeded) {
        if ($succeededNames -notcontains $requiredName) {
            throw "Required PVS validation did not succeed: $requiredName"
        }
    }

    $manifestExceptionCount = [regex]::Matches(
        $Report,
        '(?m)^\s*ErrorMarkedWithException: In package\.json, "name" needs to start with one of these approved company names:').Count
    if ($manifestExceptionCount -ne 1) {
        throw 'The PVS report does not contain exactly the approved manifest namespace exception.'
    }
}

function Test-KnownXmlDocLauncherFailure(
    [string]$Report,
    [string]$Log,
    [object]$DirectResult
) {
    Assert-AllOtherPvsValidationsPassed $Report

    $inconclusiveText = 'XmlDocValidation test is inconclusive: FindMissingDocs.exe exited with status 1.'
    $typeLoadText = "System.TypeLoadException: Could not load type of field 'Unity.XmlDoc.Filter.FilterYaml:filter' (0) due to: Could not load file or assembly 'Microsoft.DocAsCode.Metadata.ManagedReference.Roslyn, Version=2.56.6.0, Culture=neutral, PublicKeyToken=null' or one of its dependencies."
    $fatalTypeLoadText = '[ERROR] FATAL UNHANDLED EXCEPTION: ' + $typeLoadText

    $errorLines = @($Report -split "`r?`n" | Where-Object { $_ -match '^\s*Error:' })
    if ($errorLines.Count -ne 1 -or $errorLines[0].Trim() -ne ('Error: ' + $inconclusiveText)) {
        throw 'The Xmldoc Validation error is not the exact recognized FindMissingDocs exit-1 launcher failure.'
    }
    if (-not $Report.Contains($typeLoadText) -or -not $Report.Contains($fatalTypeLoadText)) {
        throw 'The PVS report does not contain the exact recognized Roslyn TypeLoadException.'
    }

    $normalizedReport = $Report.Replace('\', '/')
    $normalizedCheckerPath = $DirectResult.SourceCheckerPath.Replace('\', '/')
    if (-not $normalizedReport.Contains($normalizedCheckerPath)) {
        throw 'The PVS launcher failure did not invoke the same bundled checker used by the direct gate.'
    }
    if ($DirectResult.ExitCode -ne 0 -or
        $DirectResult.StdoutLength -ne 0 -or
        $DirectResult.StderrLength -ne 0 -or
        $DirectResult.SourceCheckerSha256 -ne $DirectResult.ExecutedCheckerSha256) {
        throw 'The direct bundled checker result is not clean.'
    }

    $exceptionLines = @($Log -split "`r?`n" | Where-Object {
        $_ -match '^(?:[A-Za-z0-9_.]+Exception|\[ERROR\] FATAL UNHANDLED EXCEPTION):'
    })
    $allowedExceptionLines = @(
        $typeLoadText,
        $fatalTypeLoadText,
        'InvalidOperationException: Package Validation Suite failed. Review the exported report.'
    )
    foreach ($exceptionLine in $exceptionLines) {
        if ($allowedExceptionLines -notcontains $exceptionLine) {
            throw "The PVS log contains a different exception: $exceptionLine"
        }
    }
    foreach ($requiredExceptionLine in $allowedExceptionLines) {
        if ($exceptionLines -notcontains $requiredExceptionLine) {
            throw "The PVS log is missing an expected exact exception line: $requiredExceptionLine"
        }
    }

    return $true
}

function Copy-ValidationArtifacts() {
    if (-not (Test-Path -LiteralPath $temporaryArtifactsPath)) {
        return
    }
    foreach ($artifact in Get-ChildItem -File -LiteralPath $temporaryArtifactsPath) {
        Copy-Item -Force -LiteralPath $artifact.FullName -Destination `
            (Join-Path $resolvedArtifactsPath $artifact.Name)
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

$artifactNames = @(
    'package-validation-import.log',
    'package-validation.log',
    'package-validation.txt',
    'package-validation-import.stdout.txt',
    'package-validation-import.stderr.txt',
    'package-validation-import-process.json',
    'package-validation.stdout.txt',
    'package-validation.stderr.txt',
    'package-validation-process.json',
    'find-missing-docs.stdout.txt',
    'find-missing-docs.stderr.txt',
    'find-missing-docs-process.json',
    'find-missing-docs-response.txt',
    'find-missing-docs-bundle.json',
    'package-validation-classification.json'
)
foreach ($artifactName in $artifactNames) {
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
        '-projectPath', ('"' + $temporaryProjectPath + '"'),
        '-logFile', ('"' + $importLogPath + '"')
    )
    $importResult = Invoke-CapturedProcess `
        -FilePath $resolvedUnityPath `
        -Arguments $importArguments `
        -WorkingDirectory $temporaryRoot `
        -StdoutPath $importStdoutPath `
        -StderrPath $importStderrPath `
        -ProcessRecordPath $importProcessPath `
        -Metadata ([ordered]@{
            phase = 'PVS disposable host import'
            pvsVersion = $PackageValidationSuiteVersion
        })
    if (-not (Test-Path -LiteralPath $importLogPath)) {
        throw 'Unity did not produce a Package Validation Suite import log.'
    }
    if ($importResult.ExitCode -ne 0) {
        throw "Package Validation Suite project import exited with code $($importResult.ExitCode)."
    }
    & (Join-Path $PSScriptRoot 'Assert-NoSamplesTildeMetaWarning.ps1') -LogPath $importLogPath
    $importFailures = Select-String -LiteralPath $importLogPath -Pattern $importFailurePattern
    if ($importFailures) {
        throw 'Package Validation Suite import log contains a compilation/import failure.'
    }
    & (Join-Path $PSScriptRoot 'Assert-InertProject.ps1') -ProjectPath $temporaryProjectPath

    $pvsPackageRoot = Get-PvsPackageRoot
    $directResult = Invoke-DirectXmlDocValidation $pvsPackageRoot

    $arguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', ('"' + $temporaryProjectPath + '"'),
        '-executeMethod', 'TorProduction.Addressables.ReleaseReadiness.PackageValidationRunner.Run',
        '-torPvsReport', ('"' + $reportPath + '"'),
        '-torPackageVersion', $packageManifest.version,
        '-logFile', ('"' + $logPath + '"')
    )
    $pvsResult = Invoke-CapturedProcess `
        -FilePath $resolvedUnityPath `
        -Arguments $arguments `
        -WorkingDirectory $temporaryRoot `
        -StdoutPath $pvsStdoutPath `
        -StderrPath $pvsStderrPath `
        -ProcessRecordPath $pvsProcessPath `
        -Metadata ([ordered]@{
            phase = 'complete PVS execution through Unity'
            pvsVersion = $PackageValidationSuiteVersion
            package = "$($packageManifest.name)@$($packageManifest.version)"
        })

    if (-not (Test-Path -LiteralPath $logPath)) {
        throw 'Unity did not produce a Package Validation Suite log.'
    }
    & (Join-Path $PSScriptRoot 'Assert-NoSamplesTildeMetaWarning.ps1') -LogPath $logPath
    if (-not (Test-Path -LiteralPath $reportPath)) {
        throw 'Package Validation Suite did not produce an exported report.'
    }
    & (Join-Path $PSScriptRoot 'Assert-InertProject.ps1') -ProjectPath $temporaryProjectPath

    $report = Get-Content -Raw -LiteralPath $reportPath
    $log = Get-Content -Raw -LiteralPath $logPath
    $unmodifiedPvsSuitePassed = $pvsResult.ExitCode -eq 0 -and $report -notmatch '(?m)^Failed - '
    $acceptedInfrastructureFallback = $false
    if (-not $unmodifiedPvsSuitePassed) {
        if ($pvsResult.ExitCode -ne 1) {
            throw "Package Validation Suite exited with unexpected code $($pvsResult.ExitCode)."
        }
        $acceptedInfrastructureFallback = Test-KnownXmlDocLauncherFailure `
            -Report $report `
            -Log $log `
            -DirectResult $directResult
    }

    $classification = [ordered]@{
        pvsVersion = $PackageValidationSuiteVersion
        package = "$($packageManifest.name)@$($packageManifest.version)"
        pvsExitCode = $pvsResult.ExitCode
        unmodifiedPvsSuitePassed = $unmodifiedPvsSuitePassed
        acceptedInfrastructureFallback = $acceptedInfrastructureFallback
        allowedFailedValidation = if ($acceptedInfrastructureFallback) { 'Xmldoc Validation' } else { $null }
        checkId = if ($acceptedInfrastructureFallback) { 'PVP-20-1' } else { $null }
        exceptionType = if ($acceptedInfrastructureFallback) { 'System.TypeLoadException' } else { $null }
        missingAssembly = if ($acceptedInfrastructureFallback) { 'Microsoft.DocAsCode.Metadata.ManagedReference.Roslyn, Version=2.56.6.0' } else { $null }
        directCheckerExitCode = $directResult.ExitCode
        directCheckerStdoutLength = $directResult.StdoutLength
        directCheckerStderrLength = $directResult.StderrLength
        statement = if ($acceptedInfrastructureFallback) { $outcomeStatement } else { 'The complete unmodified Package Validation Suite passed.' }
    }
    [IO.File]::WriteAllText(
        $classificationPath,
        ($classification | ConvertTo-Json -Depth 20) + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))

    if ($acceptedInfrastructureFallback) {
        Write-Output $outcomeStatement
    } else {
        Write-Output "Package Validation Suite $PackageValidationSuiteVersion passed for $($packageManifest.name)@$($packageManifest.version)."
    }
} finally {
    Copy-ValidationArtifacts
    Remove-TemporaryRoot
}
