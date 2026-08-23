# Content build pipeline

Phase 5 provides four explicit Addressables content build kinds. Open **Tools > Tor Production > Addressables > Build** and run **Analyze / Preflight** before starting a build. The preview identifies the exact `BuildTarget` queue, settings identity, compatibility version, state-file identity when applicable, and every blocking diagnostic. Start re-runs the same validation before creating package job state, switching targets, or invoking Addressables.

## Build kinds

- **Full** runs Addressables' public full player-content build API for one explicit Android, iOS, Windows, macOS, or Linux target. It does not consume or require an existing content-state file.
- **Content Update** requires an explicitly selected `addressables_content_state.bin`. Preflight verifies that the file exists, can be deserialized, matches the current remote-catalog configuration and group identities, and passes Addressables' public content-update restriction analysis before any target switch or build.
- **Editor-Compatible** maps the editor host to `StandaloneWindows64`, `StandaloneOSX`, or `StandaloneLinux64`, then runs a full build. Success creates both a preserved operation receipt and a package-owned latest receipt. The receipt records the exact target, Unity and Addressables versions, settings GUID/hash, output path, and built `settings.json` length/time/hash.
- **Multi-Platform** accepts an explicit subset of Android, iOS, Windows, macOS, and Linux. Preflight checks every exact target and installed Unity module before the first mutation. Ordering is canonical and deterministic, with the already-active exact target first when requested. The default stops and marks the remaining requests skipped after the first failure; continue-on-error is a separate explicit option.

Missing or stale build-layout files are warnings after an otherwise successful build. Fresh Addressables layout files are copied into the unique operation directory; their source files are never renamed, deleted, or overwritten. Operation reports and receipts are preserved under `Library/TorProduction.Addressables/BuildOperations/<job-id>`.

## Target switching and recovery

Before a target switch, the package writes `Library/TorProduction.Addressables/BuildJobs/current.json`. It records the job ID, kind, stage, original and active exact targets, complete/pending/completed queues, request and settings identities, selected state file, operation/report/receipt paths, cancellation, failures, and recovery guidance.

A successful switch returns **Awaiting Resume**. Domain reload and startup never resume mutating work silently. Use one of the explicit recovery actions:

- **Resume** re-runs preflight and continues only if the persisted request, settings, version, and state file remain compatible.
- **Cancel Current Job** cancels before the next synchronous Addressables build stage, skips remaining requests, and restores the original target.
- **Restore Original Target** performs only exact-target restoration.
- **Abandon or Reset Job** archives the package-owned record, including malformed state evidence, and clears the current recovery slot. It clears only this package's three legacy `SessionState` keys; it does not clear Addressables data, `PlayerPrefs`, Build Settings, or project settings.

Restoration failure retains the current job with an actionable diagnostic. Startup offers the recovery window only when the package-owned current job file exists; import and startup are inert otherwise.

## Existing-build Play Mode

Run an Editor-Compatible build, then choose **Existing Build > Validate Receipt**. Selection is blocked when the receipt is missing, malformed, stale, built for another editor OS or exact active target, produced by different Unity/Addressables versions, or no longer matches the current Addressables settings and built `settings.json` fingerprint.

After validation, **Validate and Select Use Existing Build** requires a separate confirmation and selects Addressables' built-in `BuildScriptPackedPlayMode`. The package contains no copied Play Mode builder and no private Addressables reflection.

## Command line

Invoke `TorProduction.Addressables.Editor.Cli.AddressablesCli.Run` with `-executeMethod` and one `-torAction`:

```text
analyze -torKind Full|ContentUpdate|EditorCompatible|MultiPlatform
full-build -torTarget Windows
content-update -torTarget Windows -torStateFile <path>
editor-compatible
multi-platform -torTargets Android,iOS,Windows,macOS,Linux [-torContinueOnError true]
resume
cancel-build-job
restore-target
abandon-build-job   (reset-build-job is an alias)
validate-existing-build
select-existing-build -torConfirmExistingBuild true
```

Analyze is read-only. Start actions emit the structured preflight before invoking the shared engine. Blocking diagnostics and fatal, target-switch, or restoration failures produce a failing Unity process result. `-torReport <path>` copies the package-owned operation report to a caller-selected path without changing Addressables' source layout report.
