[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$CommitSha,

    [string]$WorkflowFile = 'unity_phase_zero.yml'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
    throw "Invalid GitHub repository name: $Repository"
}
if ($CommitSha -notmatch '^[0-9a-f]{40}$') {
    throw "CommitSha must be a lowercase full Git SHA: $CommitSha"
}
if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
    throw 'GH_TOKEN is required to inspect hosted Unity validation.'
}

$runsEndpoint = "repos/$Repository/actions/workflows/$WorkflowFile/runs?event=workflow_dispatch&status=success&per_page=100"
$runsJson = & gh api --method GET $runsEndpoint
if ($LASTEXITCODE -ne 0) {
    throw "Unable to query hosted Unity runs for $WorkflowFile."
}
$runResponse = $runsJson | ConvertFrom-Json
$matchingRuns = @($runResponse.workflow_runs | Where-Object {
    $_.head_sha -eq $CommitSha -and
    $_.event -eq 'workflow_dispatch' -and
    $_.status -eq 'completed' -and
    $_.conclusion -eq 'success'
})
if ($matchingRuns.Count -ne 1) {
    throw "Expected exactly one successful manual Unity compatibility run for $CommitSha; found $($matchingRuns.Count)."
}

$run = $matchingRuns[0]
$jobsJson = & gh api --method GET "repos/$Repository/actions/runs/$($run.id)/jobs?filter=all&per_page=100"
if ($LASTEXITCODE -ne 0) {
    throw "Unable to query jobs for hosted Unity run $($run.id)."
}
$jobResponse = $jobsJson | ConvertFrom-Json
$requiredJobs = @('Addressables 2.7.6', 'Addressables 2.9.1')
foreach ($requiredJob in $requiredJobs) {
    $matches = @($jobResponse.jobs | Where-Object { $_.name -eq $requiredJob })
    if ($matches.Count -ne 1) {
        throw "Expected exactly one '$requiredJob' job in run $($run.id); found $($matches.Count)."
    }
    if ($matches[0].status -ne 'completed' -or $matches[0].conclusion -ne 'success') {
        throw "Hosted Unity job '$requiredJob' did not complete successfully in run $($run.id)."
    }
}

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    $outputLines = @(
        "unity_run_id=$($run.id)",
        "unity_run_url=$($run.html_url)"
    )
    $outputText = ($outputLines -join [Environment]::NewLine) + [Environment]::NewLine
    [IO.File]::AppendAllText($env:GITHUB_OUTPUT, $outputText, [Text.UTF8Encoding]::new($false))
}

Write-Output "Verified hosted Unity compatibility run $($run.id): $($run.html_url)"
Write-Output "Validated commit: $CommitSha"
foreach ($requiredJob in $requiredJobs) {
    $job = @($jobResponse.jobs | Where-Object { $_.name -eq $requiredJob })[0]
    Write-Output "$requiredJob job $($job.id): $($job.html_url)"
}
