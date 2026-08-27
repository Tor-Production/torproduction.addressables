[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$ArtifactsPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$resolvedArtifactsPath = [IO.Path]::GetFullPath($ArtifactsPath)
$temporaryBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
$temporaryRoot = Join-Path $temporaryBase ("TorProductionAddressablesArchive-" + [Guid]::NewGuid().ToString('N'))
$canonicalPackageRoot = Join-Path $temporaryRoot 'package-source'
$packRoot = Join-Path $temporaryRoot 'pack'
$extractRoot = Join-Path $temporaryRoot 'extract'

$canonicalTextExtensions = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($extension in @(
    '.asmdef',
    '.asset',
    '.cs',
    '.json',
    '.md',
    '.meta',
    '.ps1',
    '.txt',
    '.unity',
    '.yaml',
    '.yml'
)) {
    [void]$canonicalTextExtensions.Add($extension)
}

function Assert-TemporaryPath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith(
        $temporaryBase + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to mutate a path outside the system temporary directory: $fullPath"
    }
}

function ConvertTo-CanonicalLineEndings([string]$Path) {
    [byte[]]$bytes = [IO.File]::ReadAllBytes($Path)
    $normalized = [IO.MemoryStream]::new($bytes.Length)
    try {
        for ($index = 0; $index -lt $bytes.Length; $index++) {
            if ($bytes[$index] -eq 13) {
                if ($index + 1 -lt $bytes.Length -and $bytes[$index + 1] -eq 10) {
                    $index++
                }
                $normalized.WriteByte(10)
                continue
            }
            $normalized.WriteByte($bytes[$index])
        }
        [IO.File]::WriteAllBytes($Path, $normalized.ToArray())
    } finally {
        $normalized.Dispose()
    }
}

& (Join-Path $PSScriptRoot 'Validate-PackageManifest.ps1') -PackagePath $resolvedPackagePath

$manifest = Get-Content -Raw -LiteralPath (Join-Path $resolvedPackagePath 'package.json') | ConvertFrom-Json
$expectedFileName = "$($manifest.name)-$($manifest.version).tgz"
New-Item -ItemType Directory -Force -Path `
    $canonicalPackageRoot, $packRoot, $extractRoot, $resolvedArtifactsPath | Out-Null

try {
    Get-ChildItem -Force -LiteralPath $resolvedPackagePath |
        Copy-Item -Recurse -Force -Destination $canonicalPackageRoot
    Get-ChildItem -Recurse -Force -File -LiteralPath $canonicalPackageRoot |
        Where-Object { $canonicalTextExtensions.Contains($_.Extension) } |
        ForEach-Object { ConvertTo-CanonicalLineEndings $_.FullName }
    & (Join-Path $PSScriptRoot 'Validate-PackageManifest.ps1') -PackagePath $canonicalPackageRoot

    $nodeCommand = Get-Command node -ErrorAction Stop
    $bundledNpmCli = Join-Path (Split-Path -Parent $nodeCommand.Source) 'node_modules/npm/bin/npm-cli.js'
    if (Test-Path -LiteralPath $bundledNpmCli -PathType Leaf) {
        $packJson = & $nodeCommand.Source $bundledNpmCli pack $canonicalPackageRoot --json --pack-destination $packRoot
    } else {
        $npmCommand = Get-Command npm -ErrorAction Stop
        $packJson = & $npmCommand.Source pack $canonicalPackageRoot --json --pack-destination $packRoot
    }
    if ($LASTEXITCODE -ne 0) {
        throw "npm pack failed with exit code $LASTEXITCODE."
    }
    $packResult = $packJson | ConvertFrom-Json
    if (@($packResult).Count -ne 1) {
        throw "npm pack returned an unexpected result count: $(@($packResult).Count)"
    }
    $archiveName = @($packResult)[0].filename
    if ($archiveName -ne $expectedFileName) {
        throw "Archive filename/version mismatch. Expected '$expectedFileName', got '$archiveName'."
    }

    $temporaryArchivePath = Join-Path $packRoot $archiveName
    if (-not (Test-Path -LiteralPath $temporaryArchivePath -PathType Leaf)) {
        throw "npm pack did not create the reported archive: $temporaryArchivePath"
    }

    $archiveEntries = @(& tar -tf $temporaryArchivePath)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to list archive contents: $temporaryArchivePath"
    }
    $archiveFiles = @($archiveEntries |
        Where-Object { $_ -and -not $_.EndsWith('/') } |
        ForEach-Object {
            if (-not $_.StartsWith('package/')) {
                throw "Archive entry is outside the npm package root: $_"
            }
            $_.Substring('package/'.Length).Replace('\', '/')
        } | Sort-Object -Unique)
    $sourceFiles = @(Get-ChildItem -Recurse -Force -File -LiteralPath $resolvedPackagePath |
        ForEach-Object {
            $_.FullName.Substring($resolvedPackagePath.Length).TrimStart('\', '/').Replace('\', '/')
        } | Sort-Object -Unique)
    $difference = @(Compare-Object -ReferenceObject $sourceFiles -DifferenceObject $archiveFiles)
    if ($difference.Count -ne 0) {
        $details = $difference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
        throw "Archive contents differ from package source:`n$($details -join [Environment]::NewLine)"
    }

    & tar -xf $temporaryArchivePath -C $extractRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to extract archive: $temporaryArchivePath"
    }
    $extractedPackagePath = Join-Path $extractRoot 'package'
    & (Join-Path $PSScriptRoot 'Validate-PackageManifest.ps1') -PackagePath $extractedPackagePath
    $extractedManifest = Get-Content -Raw -LiteralPath (Join-Path $extractedPackagePath 'package.json') |
        ConvertFrom-Json
    if ($extractedManifest.name -ne $manifest.name -or $extractedManifest.version -ne $manifest.version) {
        throw 'Extracted archive metadata does not match the source manifest.'
    }
    foreach ($relativePath in $archiveFiles) {
        $canonicalPath = Join-Path $canonicalPackageRoot $relativePath
        $extractedPath = Join-Path $extractedPackagePath $relativePath
        $canonicalHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $canonicalPath).Hash
        $extractedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $extractedPath).Hash
        if ($canonicalHash -ne $extractedHash) {
            throw "Archive file bytes differ from the canonical package source: $relativePath"
        }
    }

    $archivePath = Join-Path $resolvedArtifactsPath $archiveName
    $checksumPath = $archivePath + '.sha256'
    Copy-Item -Force -LiteralPath $temporaryArchivePath -Destination $archivePath
    $checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText(
        $checksumPath,
        "$checksum  $archiveName`n",
        [Text.UTF8Encoding]::new($false))

    Write-Output "Created and validated package archive: $archivePath"
    Write-Output "SHA-256: $checksum"
} finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Assert-TemporaryPath $temporaryRoot
        Remove-Item -Recurse -Force -LiteralPath $temporaryRoot
    }
}
