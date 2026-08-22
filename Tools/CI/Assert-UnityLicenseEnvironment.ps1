[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$hasEmail = -not [string]::IsNullOrWhiteSpace($env:UNITY_EMAIL)
$hasPassword = -not [string]::IsNullOrWhiteSpace($env:UNITY_PASSWORD)
$hasLicenseFile = -not [string]::IsNullOrWhiteSpace($env:UNITY_LICENSE)
$hasProfessionalSerial = -not [string]::IsNullOrWhiteSpace($env:UNITY_SERIAL)

if (-not $hasEmail -or -not $hasPassword) {
    throw 'Unity CI licensing preflight failed: configure both UNITY_EMAIL and UNITY_PASSWORD repository secrets.'
}

if (-not $hasLicenseFile -and -not $hasProfessionalSerial) {
    throw 'Unity CI licensing preflight failed: configure UNITY_LICENSE for a license-file workflow or UNITY_SERIAL for a professional-license workflow.'
}

Write-Output 'Unity CI licensing preflight passed: required secret names are populated; secret values were not printed.'
