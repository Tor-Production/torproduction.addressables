[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),

    [ValidateSet('2.7.6', '2.9.1', '2.11.2')]
    [string]$ExpectedHostAddressablesVersion = '2.7.6'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$packageRoot = Join-Path $root 'com.torproduction.addressables'
$workflowRoot = Join-Path $root '.github/workflows'
$releaseVersion = '0.1.0-preview.2'
$releaseTag = "v$releaseVersion"
$archiveName = "com.torproduction.addressables-$releaseVersion.tgz"

function Get-NormalizedTextSha256([string]$Path) {
    $text = (Get-Content -Raw -LiteralPath $Path).Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($text)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') })
    } finally {
        $sha.Dispose()
    }
}

& (Join-Path $PSScriptRoot 'Validate-PhaseZero.ps1') `
    -RepositoryRoot $root `
    -ExpectedHostAddressablesVersion $ExpectedHostAddressablesVersion
& (Join-Path $PSScriptRoot 'Validate-PackageManifest.ps1') -PackagePath $packageRoot

$pvsScript = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Run-PackageValidation.ps1')
foreach ($requiredPvsGuard in @(
    'PVP-20-1',
    'Xmldoc Validation',
    'FindMissingDocs.exe exited with status 1.',
    'Unity.XmlDoc.Filter.FilterYaml:filter',
    'Microsoft.DocAsCode.Metadata.ManagedReference.Roslyn, Version=2.56.6.0',
    'c571657558566c4b652a52ef2130a64af462274feca0da234bc9bf6d6ab6729b',
    '$sourceCheckerSha256 -ne $expectedCheckerSha256',
    '$executedCheckerSha256 -ne $expectedCheckerSha256',
    'SourceCheckerSha256 -ne $DirectResult.ExecutedCheckerSha256',
    '$DirectResult.ExitCode -ne 0',
    '$DirectResult.StdoutLength -ne 0',
    '$DirectResult.StderrLength -ne 0',
    '$pvsResult.ExitCode -ne 1',
    '$failed.Count -ne 1',
    'unmodifiedPvsSuitePassed',
    'acceptedInfrastructureFallback'
)) {
    if (-not $pvsScript.Contains($requiredPvsGuard)) {
        throw "The narrow PVS XML-documentation fallback guard is missing: $requiredPvsGuard"
    }
}
if ($pvsScript -match '(?is)ValidationExceptions.*Xmldoc|Xmldoc.*ValidationExceptions') {
    throw 'The PVS runner must not add or recommend an Xmldoc Validation exception.'
}

foreach ($obsoleteWorkflow in @('comment_automatic_rebase.yml', 'pr_assign_creator.yml')) {
    if (Test-Path -LiteralPath (Join-Path $workflowRoot $obsoleteWorkflow)) {
        throw "Obsolete write-capable workflow remains: $obsoleteWorkflow"
    }
}

$workflowFiles = @(Get-ChildItem -File -LiteralPath $workflowRoot -Filter '*.yml')
if ($workflowFiles.Count -ne 4) {
    throw "Unexpected workflow count: $($workflowFiles.Count)"
}
foreach ($workflow in $workflowFiles) {
    $contents = Get-Content -Raw -LiteralPath $workflow.FullName
    if ($contents -notmatch '(?m)^permissions:\s*\r?\n') {
        throw "Workflow has no explicit top-level permissions: $($workflow.Name)"
    }
    if ($contents -match '(?im)^\s{2}schedule:|^\s{2}workflow_run:') {
        throw "Recurring or chained workflow trigger is not authorized: $($workflow.Name)"
    }
    if ($contents -match '(?im)packages:\s*write|id-token:\s*write|npm\s+publish|openupm|softprops/action-gh-release') {
        throw "Registry or broad publication capability is not authorized: $($workflow.Name)"
    }

    foreach ($usesMatch in [regex]::Matches($contents, '(?m)^\s*-?\s*uses:\s*(?<action>[^\s#]+)')) {
        $action = $usesMatch.Groups['action'].Value
        if ($action.StartsWith('./')) {
            continue
        }
        if ($action -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}$') {
            throw "Third-party action is not pinned to an immutable commit SHA in $($workflow.Name): $action"
        }
    }

    if ($contents -match 'game-ci/unity-test-runner@' -and
        ($contents -match '(?m)^\s{2}(push|pull_request|schedule|workflow_run):')) {
        throw "Paid Unity workflow is not manual-only: $($workflow.Name)"
    }
    if ($workflow.Name -ne 'release_github_prerelease.yml' -and
        ($contents -match '(?im)contents:\s*write|gh\s+release')) {
        throw "Only the protected release workflow may write repository contents: $($workflow.Name)"
    }
}

$requiredWorkflow = Get-Content -Raw -LiteralPath (Join-Path $workflowRoot 'unity_phase_zero.yml')
foreach ($requiredText in @(
    'actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1',
    'game-ci/unity-test-runner@0ff419b913a3630032cbe0de48a0099b5a9f0ed9',
    'actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a',
    'Assert-NoSamplesTildeMetaWarning.ps1',
    '2.7.6',
    '2.9.1',
    '6000.0.78f1'
)) {
    if ($requiredWorkflow -notmatch [regex]::Escape($requiredText)) {
        throw "Required compatibility workflow value is missing: $requiredText"
    }
}

$experimentalWorkflow = Get-Content -Raw -LiteralPath (Join-Path $workflowRoot 'unity_latest_experimental.yml')
foreach ($requiredText in @(
    'game-ci/unity-test-runner@f7d28f891263d875d47ef34370e9e8dd6087e1ef',
    '2.11.2',
    '6000.0.82f1'
)) {
    if ($experimentalWorkflow -notmatch [regex]::Escape($requiredText)) {
        throw "Experimental compatibility workflow value is missing: $requiredText"
    }
}

$semanticWorkflow = Get-Content -Raw -LiteralPath (Join-Path $workflowRoot 'pr_title_semantic_validation.yml')
if ($semanticWorkflow -notmatch 'actions/github-script@3a2844b7e9c422d3c10d287c895573f7108da1b3') {
    throw 'The semantic-title workflow must use the reviewed official actions/github-script v9 pin.'
}

$releaseWorkflow = Get-Content -Raw -LiteralPath (Join-Path $workflowRoot 'release_github_prerelease.yml')
foreach ($requiredText in @(
    "EXPECTED_TAG: $releaseTag",
    "EXPECTED_VERSION: $releaseVersion",
    'environment: ${{',
    "'release-recovery' || 'release'",
    'contents: write',
    'actions: read',
    "github.event_name == 'workflow_dispatch'",
    "github.ref == 'refs/heads/main'",
    'ref: refs/tags/v0.1.0-preview.2',
    'ref: d573808cef21c39f0689a017a05edb0260b6d13a',
    'path: .release-recovery',
    'sparse-checkout: Tools/CI/Assert-HostedUnityValidation.ps1',
    'persist-credentials: false',
    './.release-recovery/Tools/CI/Assert-HostedUnityValidation.ps1',
    'Assert-HostedUnityValidation.ps1',
    'New-PackageArchive.ps1',
    'Export-ReleaseNotes.ps1',
    'verification.verified',
    'gh release create',
    '--verify-tag',
    '--draft',
    '--prerelease'
)) {
    if (-not $releaseWorkflow.Contains($requiredText)) {
        throw "Protected release workflow value is missing: $requiredText"
    }
}
if ($releaseWorkflow -notmatch "(?ms)^on:\s*\r?\n\s{2}push:\s*\r?\n\s{4}tags:\s*\r?\n\s{6}-\s*'v\*'\s*\r?\n\s{2}workflow_dispatch:\s*$" -or
    $releaseWorkflow -match '(?m)^\s{2}(pull_request|schedule|workflow_run):') {
    throw 'The GitHub pre-release workflow must trigger only for pushed v* tags or protected manual recovery.'
}
if ($releaseWorkflow -notmatch '(?ms)^permissions:\s*\r?\n\s{2}contents:\s*read\s*\r?\n\s{2}actions:\s*read\s*$') {
    throw 'The release workflow must retain read-only top-level permissions.'
}
if ([regex]::Matches($releaseWorkflow, '(?m)^\s+contents:\s*write\s*$').Count -ne 1) {
    throw 'Exactly one protected release job may receive contents: write.'
}
if ($releaseWorkflow -match 'game-ci/unity-test-runner|unity_phase_zero\.yml\s*@|gh\s+workflow\s+run|npm\s+publish|openupm') {
    throw 'The tag workflow must not run Unity, dispatch another workflow, or publish to a registry.'
}

$releaseNotesExporter = Get-Content -Raw -LiteralPath (Join-Path $root 'Tools/CI/Export-ReleaseNotes.ps1')
foreach ($requiredText in @(
    'Add package from tarball',
    'Source code (zip)',
    'Source code (tar.gz)',
    '?path=/com.torproduction.addressables#v$Version'
)) {
    if (-not $releaseNotesExporter.Contains($requiredText)) {
        throw "Release-note installation warning is missing: $requiredText"
    }
}

$licensePath = Join-Path $packageRoot 'LICENSE.md'
$noticePath = Join-Path $packageRoot 'Third Party Notices.md'
$expectedLicenseSha256 = 'a14f690616e084b1cbae91979075c8e00f7a4bd84a09b311463c84b118ef4a19'
$expectedNoticeSha256 = '97bf61cec1101d091824a5d2563e23620db6da6220a20b8218e4ed94b6b584c3'
if ((Get-NormalizedTextSha256 $licensePath) -ne $expectedLicenseSha256) {
    throw 'LICENSE.md differs from the approved complete MIT text and copyright lines.'
}
if ((Get-NormalizedTextSha256 $noticePath) -ne $expectedNoticeSha256) {
    throw 'Third Party Notices.md differs from the approved minimal template attribution.'
}
if (Test-Path -LiteralPath (Join-Path $packageRoot 'Documentation~/Third Party Notices.md')) {
    throw 'The duplicate Documentation~ third-party notice must not exist.'
}
if (Test-Path -LiteralPath (Join-Path $packageRoot 'Documentation~/PROVENANCE_AUDIT.md')) {
    throw 'Private provenance narrative must not be distributed inside the UPM package.'
}

$changelog = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'CHANGELOG.md')
if ($changelog -notmatch "(?m)^## \[$([regex]::Escape($releaseVersion))\] - 2026-08-27\s*$") {
    throw "CHANGELOG.md must contain the actual $releaseVersion release date."
}
$unreleasedMatch = [regex]::Match(
    $changelog,
    "(?ms)^## \[Unreleased\]\s*\r?\n(?<body>.*?)(?=^## \[$([regex]::Escape($releaseVersion))\])")
if (-not $unreleasedMatch.Success -or -not [string]::IsNullOrWhiteSpace($unreleasedMatch.Groups['body'].Value)) {
    throw 'CHANGELOG.md must retain an empty [Unreleased] section.'
}

$checksumPath = Join-Path $root "Release/$archiveName.sha256"
if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
    throw "Committed release checksum is missing: Release/$archiveName.sha256"
}
$checksumLine = (Get-Content -Raw -LiteralPath $checksumPath).Trim()
if ($checksumLine -notmatch "^[0-9a-f]{64}  $([regex]::Escape($archiveName))$") {
    throw 'Committed release checksum has an invalid filename or SHA-256 format.'
}

Write-Output "Phase 7 release-readiness static validation passed for Addressables $ExpectedHostAddressablesVersion."
