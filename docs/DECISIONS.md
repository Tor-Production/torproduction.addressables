# Verified current decisions

This register contains only decisions confirmed by released source, tests, package configuration, workflows, or final release evidence. The archived [ImplementationPlan.md](../ImplementationPlan.md) may explain historical rationale but is not sufficient evidence that a planned decision shipped.

## DEC-001 — The product is editor-only

The package ships one production editor assembly and no production runtime assembly. Runtime code under `com.torproduction.addressables/Tests/Runtime/` is PlayMode test coverage, not a player-facing package API.

Evidence: [editor assembly definition](../com.torproduction.addressables/Editor/TorProduction.Addressables.Editor.asmdef), [deterministic API surface](../com.torproduction.addressables/Documentation~/API_SURFACE.txt), [package-layout tests](../com.torproduction.addressables/Tests/Editor/PhaseSixPackageLayoutTests.cs), and [package manifest](../com.torproduction.addressables/package.json).

## DEC-002 — Reads are inert and mutations are explicit

Unconfigured import and configuration reads must not create Addressables or project state. Supported mutations pass through explicit analysis and application, reject stale plans, and expose failure or recovery rather than silently continuing.

Evidence: [public automation entry points](../com.torproduction.addressables/Editor/GroupSynchronization/AddressablesAutomation.cs), [automation contracts](../com.torproduction.addressables/Editor/GroupSynchronization/AutomationContracts.cs), [configuration tests](../com.torproduction.addressables/Tests/Editor/PhaseOneConfigurationTests.cs), [safety tests](../com.torproduction.addressables/Tests/Editor/PhaseZeroSafetyTests.cs), and the hosted workflow's inertness gate in [unity_phase_zero.yml](../.github/workflows/unity_phase_zero.yml).

## DEC-003 — Configuration and scene identity are GUID-backed

The released editor configuration uses `AddressablesAutomationConfig`, `GroupSyncRule`, and `SceneFolderRule`. Folder, group, asset, and managed-scene identity is GUID-backed; planners generate deterministic operations and treat incompatible claims as diagnostics.

Evidence: [configuration model](../com.torproduction.addressables/Editor/Configuration/AddressablesAutomationConfig.cs), [automation contracts](../com.torproduction.addressables/Editor/GroupSynchronization/AutomationContracts.cs), [group synchronization tests](../com.torproduction.addressables/Tests/Editor/PhaseTwoGroupSynchronizationTests.cs), and [scene synchronization tests](../com.torproduction.addressables/Tests/Editor/PhaseThreeSceneSynchronizationTests.cs).

## DEC-004 — Convergence is scoped and recoverable

Group and scene operations target explicitly configured ownership, preserve unrelated state, validate source hashes, and use recovery or rollback records around mutation. Physical relocation of source assets is not part of the released core workflow.

Evidence: [group planner](../com.torproduction.addressables/Editor/GroupSynchronization/GroupSyncPlanner.cs), [group transaction](../com.torproduction.addressables/Editor/GroupSynchronization/GroupSyncTransaction.cs), [scene planner](../com.torproduction.addressables/Editor/SceneSynchronization/SceneSyncPlanner.cs), [group tests](../com.torproduction.addressables/Tests/Editor/PhaseTwoGroupSynchronizationTests.cs), and [scene tests](../com.torproduction.addressables/Tests/Editor/PhaseThreeSceneSynchronizationTests.cs).

## DEC-005 — The released build API has four explicit kinds

The public editor API exposes `Full`, `ContentUpdate`, `EditorCompatible`, and `MultiPlatform` requests. Preflight, persistent recovery, target restoration, receipts, and existing-build validation are part of the same released build surface.

Evidence: [build contracts](../com.torproduction.addressables/Editor/BuildPipeline/ContentBuildContracts.cs), [build job engine](../com.torproduction.addressables/Editor/BuildPipeline/ContentBuildJobEngine.cs), [build pipeline tests](../com.torproduction.addressables/Tests/Editor/PhaseFiveBuildPipelineTests.cs), and [deterministic API surface](../com.torproduction.addressables/Documentation~/API_SURFACE.txt).

## DEC-006 — Compatibility claims are exact and evidence-bound

The production dependency is exact Addressables `2.7.6`. Required verification covers `2.7.6` and `2.9.1` on Unity `6000.0.78f1`. The `6000.0.82f1` / `2.11.2` lane is manual and experimental, so its existence does not expand the supported release claim.

Evidence: [package manifest](../com.torproduction.addressables/package.json), [required compatibility workflow](../.github/workflows/unity_phase_zero.yml), [experimental workflow](../.github/workflows/unity_latest_experimental.yml), and [Preview 3 checksum](../Release/com.torproduction.addressables-0.1.0-preview.3.tgz.sha256).

## Historical or superseded proposals

Do not promote planned distribution through OpenUPM, conditional or unresolved licensing language, active phase-completion protocols, proposed APIs, or unverified behavior from the historical plan. If a future task implements one of those ideas, it must update this register using current source and verification evidence.
