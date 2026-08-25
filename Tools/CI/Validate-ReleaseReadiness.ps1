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

& (Join-Path $PSScriptRoot 'Validate-PhaseZero.ps1') `
    -RepositoryRoot $root `
    -ExpectedHostAddressablesVersion $ExpectedHostAddressablesVersion
& (Join-Path $PSScriptRoot 'Validate-PackageManifest.ps1') -PackagePath $packageRoot

$pvsScript = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Run-PackageValidation.ps1')
foreach ($requiredPvsGuard in @(
    'PVP-20-1',
    'Xmldoc Validation',
    'FindMissingDocs.exe exited with status 1.',
    "Unity.XmlDoc.Filter.FilterYaml:filter",
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
if ($workflowFiles.Count -ne 3) {
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
    if ($contents -match '(?im)contents:\s*write|packages:\s*write|id-token:\s*write|npm\s+publish|openupm|gh\s+release|create-release|softprops/action-gh-release') {
        throw "Publication/write capability is not authorized in workflow: $($workflow.Name)"
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
}

$requiredWorkflow = Get-Content -Raw -LiteralPath (Join-Path $workflowRoot 'unity_phase_zero.yml')
foreach ($requiredText in @(
    'actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1',
    'game-ci/unity-test-runner@0ff419b913a3630032cbe0de48a0099b5a9f0ed9',
    'actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a',
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

$license = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'LICENSE.md')
if ($license -notmatch [regex]::Escape("Copyright (c) 2020 Stan's Assets")) {
    throw 'The unresolved retained copyright notice changed without an approved legal decision.'
}
$notices = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'Third Party Notices.md')
if ($notices -notmatch '(?i)provenance' -or $notices -notmatch '(?i)unresolved') {
    throw 'Third Party Notices must retain the unresolved provenance warning until legal review.'
}

Write-Output "Phase 7A release-readiness static validation passed for Addressables $ExpectedHostAddressablesVersion."
