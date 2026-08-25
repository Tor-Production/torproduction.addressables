# Command-line usage

Run Unity in batch mode with the package installed. Every command writes structured JSON to the Unity log and throws for blocking diagnostics or fatal failure so automation receives a non-zero process result.

## Groups and scenes

Read-only analysis:

```text
Unity -batchmode -quit -projectPath <project> -executeMethod TorProduction.Addressables.Editor.AddressablesAutomationCli.AnalyzeGroups
Unity -batchmode -quit -projectPath <project> -executeMethod TorProduction.Addressables.Editor.AddressablesAutomationCli.AnalyzeScenes
```

Explicit mutations and recovery:

```text
... -executeMethod TorProduction.Addressables.Editor.AddressablesAutomationCli.ApplyGroups
... -executeMethod TorProduction.Addressables.Editor.AddressablesAutomationCli.ApplyScenes
... -executeMethod TorProduction.Addressables.Editor.AddressablesAutomationCli.RecoverGroups
... -executeMethod TorProduction.Addressables.Editor.AddressablesAutomationCli.RecoverScenes
```

Apply analyzes current state again. It fails instead of applying an invalid or stale plan.

## Content builds

Use `-executeMethod TorProduction.Addressables.Editor.Cli.AddressablesCli.Run` and exactly one `-torAction`:

```text
-torAction analyze -torKind Full -torTarget Windows
-torAction analyze -torKind ContentUpdate -torTarget Windows -torStateFile <path>
-torAction analyze -torKind EditorCompatible
-torAction analyze -torKind MultiPlatform -torTargets Android,iOS,Windows
-torAction full-build -torTarget Windows
-torAction content-update -torTarget Windows -torStateFile <path>
-torAction editor-compatible
-torAction multi-platform -torTargets Android,iOS,Windows [-torContinueOnError true]
-torAction resume
-torAction cancel-build-job
-torAction restore-target
-torAction abandon-build-job
-torAction validate-existing-build
-torAction select-existing-build -torConfirmExistingBuild true
```

Targets accept `Android`, `iOS`, `Windows`/`StandaloneWindows64`, `macOS`/`StandaloneOSX`, and `Linux`/`StandaloneLinux64`. `-torReport <path>` copies the package-owned operation report to a caller-selected location. Analyze is read-only. Build actions run the same preflight first; target switches never resume silently across a domain reload.

Duplicate-dependency Analyze/Fix remains an interactive Project Settings workflow because Fix requires a reviewed analyzer result and explicit confirmation.
