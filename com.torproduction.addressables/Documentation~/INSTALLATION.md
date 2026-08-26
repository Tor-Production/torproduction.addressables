# Installation

## Requirements

- Unity 6, minimum manifest line `6000.0`.
- Addressables `2.7.6` or a separately verified compatible version.
- The package is editor-only; it does not add a production runtime assembly.

The release-readiness matrix and its exact evidence are in [Compatibility](COMPATIBILITY.md). Before the signed preview tag is published, install only from a reviewed local path or produced validation archive.

## Install from disk

In Unity Package Manager choose **+ > Install package from disk...** and select `com.torproduction.addressables/package.json`. For a project manifest, use a path relative to the project's `Packages` directory:

```json
{
  "dependencies": {
    "com.torproduction.addressables": "file:../../com.torproduction.addressables"
  }
}
```

## Install a validation archive

The repository script `Tools/CI/New-PackageArchive.ps1` creates a `.tgz` and SHA-256 file outside the package tree. A local validation project can reference the absolute archive path with a `file:` dependency. This is a release-readiness check, not publication.

## Install from Git

Unity supports a Git dependency with the package subfolder:

```text
https://github.com/Yurii-Tor/torproduction.addressables.git?path=/com.torproduction.addressables#v0.1.0-preview.1
```

Use the signed semantic release tag after its GitHub pre-release is published. Phase-verification tags are engineering evidence and are not public package releases.

## First setup

Installation is inert. Open **Project Settings > Tor Production > Addressables Automation**, then explicitly create or select an editor-only `AddressablesAutomationConfig`. Creating Addressables settings is a separate confirmed action that opens Unity's official Addressables Groups workflow; reading this package's settings never creates them.

Continue with [Configuration](CONFIGURATION.md) and analyze a scope before applying it.

## Remove the package

1. Finish or explicitly recover/abandon any package-owned group, scene, dependency, or build recovery record.
2. Use **Detach** if the project should stop tracking the selected configuration and automatic scene opt-in.
3. Remove imported samples separately in `Assets/Samples/Tor Production Addressables Toolpack/<version>` if they are no longer wanted.
4. Remove the package in Package Manager or delete its manifest dependency.

Removal does not delete consumer configurations, Addressables settings, groups, entries, labels, scenes, Build Settings rows, build output, or imported samples. Those are host-project data and require an explicit owner decision.
