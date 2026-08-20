# Tor Production Addressables — Production UPM Engineering Plan

## Plan status

- Planning: Complete
- Implementation: In progress
- Current phase: Phase 0 — implementation complete; hosted CI verification pending
- Current batch: First implementation batch — complete
- Source baseline: `ccce9423b7d1f64b76431759052ef5b945e99334`
- Last updated: `2026-08-20`

This document is the implementation source of truth. Every implementation context must read it before changing code. Work must proceed incrementally by phase and issue ID. After completing a batch, update this status and the relevant issue/phase progress before committing.

## A. Executive assessment

### Intended product

An editor-first, project-agnostic UPM package for configuring, analyzing, synchronizing, building, and reporting Addressables workflows. Installation must be inert until explicitly configured, all destructive changes must be previewable, and no workflow may assume game-specific folders, types, states, or scene conventions.

### Audit basis

- No `AGENTS.md` or additional repository instruction files were found.
- Git was clean on `main` at source baseline `ccce9423b7d1f64b76431759052ef5b945e99334`; the repository had two commits, no configured remotes, and a `package.json` repository URL that could not yet be verified against a remote.
- The development project opened from an ignored Unity `6000.0.78f1` `ProjectVersion.txt` and cached Addressables `2.9.1`, but no `ProjectSettings` were tracked.
- Existing cached compilation succeeded for that particular generated project.
- Existing Unity logs confirmed repeated installation/import `NullReferenceException` failures from `com.torproduction.addressables/Editor/Menu/ScenesBinder/ScenesListMapper.cs:13`.
- No complete user-facing workflow qualified as production-ready at the audit baseline.

### Maturity and safety

Current maturity is prototype/pre-alpha despite version `0.0.1-preview`. It is not presently safe to install into an unrelated project or run against valuable Addressables settings.

Several failures are deterministic:

- An unconfigured import reaches a null scene configuration before filtering relevant assets.
- An unfiltered group update iterates a null `TypesFilter`.
- Prefab organization reflects fields from an interface and reaches placeholder `return null` paths.
- `ObjectTemplate.SetId` always throws.
- The macOS Editor platform dictionary has the wrong key.
- Every nominal Addressables build invokes the content-update API and therefore assumes a prior content-state file.

### Five highest risks

1. **Installation side effects:** package import writes `ProjectSettings/ProjectConfig.json` and throws before setup exists.
2. **Destructive orchestration:** “Update All New Assets” empties the Default Group before validating later operations and has no rollback.
3. **Build integrity:** full-build menus actually perform content updates; target switching and restoration are not exception-safe.
4. **Product-boundary contamination:** editor production code depends on sample/game-specific `AppState`, template, interactable, and numeric-state concepts.
5. **No reproducible safety net:** the Unity version and Project Settings are ignored, the UPM minimum is false, tests are templates, and CI does not compile or validate the package.

## B. Current-state subsystem matrix

| Subsystem | Intended responsibility | Current state | Confirmed problems | Risk | Recommended disposition |
| --- | --- | --- | --- | --- | --- |
| Repository baseline | Reproducible package development | Cached Unity 6 project works locally | No remote; only two commits; Project Settings and `ProjectVersion.txt` ignored | High | Replace baseline |
| Development manifest | Minimal host for compilation and testing | Addressables 2.9.1 plus many unrelated services, XR, Ads, Purchasing, Timeline, and vendored dependency resolver | Dependency noise hides accidental references and makes cloning unpredictable | High | Replace |
| UPM manifest | Declare package identity and compatibility | Tor branding but legacy dependencies and `unity: 2019.3` | Addressables 2.6.0 itself targets substantially newer Unity; Foundation is needed only by unused game-specific ID code | Critical | Redesign |
| Package initialization | Be inert before opt-in | Global `AssetPostprocessor` executes during import | Confirmed null dereference and Project Settings write on clean installation | Critical | Replace |
| Project configuration | Select and validate project-local automation rules | Three hardcoded config paths stored in JSON | Scenes and Addressables defaults are duplicated; paths are game-specific; missing/corrupt data can discard valid references | Critical | Replace |
| Settings UI | Clear setup and validation workflow | Custom `EditorWindow` | Loads/mutates configuration during `OnEnable`; partial validation; null dereferences possible | High | Replace with `SettingsProvider` |
| Group synchronization | Converge folder contents, group, labels, and addresses | Basic folder scan and entry creation | Null failures, ambiguous type resolution, filename-only addresses, no convergence for existing entries, no schema validation | Critical | Redesign |
| Update All orchestration | Safely combine automation steps | Fixed sequence of unrelated operations | Default Group cleanup happens first; no preflight, dry run, stop policy, recovery, or transactional report | Critical | Replace |
| Scene synchronization | Manage Addressable and local scenes | Automatic incremental postprocessor | Name-based identity, broken rename/delete handling, numeric game states, sample assembly dependency, stale entries, no recursion guard | Critical | Redesign |
| Prefab organization | Optionally organize referenced prefabs | Interactable-specific incomplete migration | Interface reflection cannot find fields; two placeholder methods return null; unsafe physical moves | Critical | Remove from core |
| Duplicate dependencies | Analyze/fix duplicated implicit bundle dependencies | Subclass of Addressables analyzer | Reflects private `m_ImplicitAssets`; assumes destination group; no analyze-only report or version gate | High | Replace with adapter |
| Full/content builds | Build Addressables content deliberately | All menu paths call `BuildContentUpdate` | Full build is absent; prior state file is always required | Critical | Replace |
| Multi-platform builds | Queue targets and restore editor state | SessionState queue with target-group comparisons | macOS key defect; standalone targets conflated; switch result ignored; failure can leave wrong target or stale queue | Critical | Replace |
| Existing-build Play Mode | Use previously built editor-compatible data | Forked packed-play builder | Private reflection, Linux failure, stale-data TODO, unsafe cache ownership | High | Replace with built-in builder plus validation |
| Reports | Explain planned and applied work | Timestamped text and moved build-layout file | Nondeterministic, partial, overwrites/deletes prior reports, missing failure structure | High | Replace |
| Runtime data model | Only runtime contracts genuinely required | Templates, app states, scene catalog, dictionary, ID helper, generic drawers | Mostly project-specific, unused, unfinished, or editor-related; `SetId` throws | High | Remove or internalize |
| Assemblies | Enforce Runtime/Editor/Samples/Tests boundaries | Six assemblies | Production Menu assembly depends on Samples; stale StansAssets names and `InternalsVisibleTo`; production source imports NUnit | Critical | Redesign |
| Samples | Optional examples | `Samples` is compiled and shipped as a dependency | Not `Samples~`; no package manifest sample declaration; contains game-specific app-state assets | High | Replace |
| Tests | Protect behavior and compatibility | Duplicate template tests | Do not reference production assemblies; one platform assertion is wrong on Android | Critical | Replace |
| Documentation | Explain installation and safe operation | Mostly package-template content | Stale names, URLs, notices, examples, and ownership | High | Replace |
| CI/release | Validate and publish reproducibly | PR utility workflows and placeholder npm workflow | No Unity compile/tests/PVS; literal package placeholder; unsafe release triggers; old/unpinned actions | Critical | Replace |

Some operations can succeed only when hidden assumptions hold: pre-created Addressables settings, named groups and schemas, non-null arrays, unique filenames and scene names, resolvable simple type names, installed build modules, and a valid prior content-state file.

## C. Product-boundary decisions

### Architectural decisions

| Decision | Preferred option | Alternatives and trade-offs |
| --- | --- | --- |
| Package responsibility | Editor automation for Addressables configuration, group/scene convergence, dependency analysis, builds, reports, and CLI/CI integration | Adding game lifecycle or content semantics would couple the package to individual projects |
| Runtime API | No Runtime assembly in v1 unless a later workflow proves runtime code is necessary | Keeping `ScenesConfig` or templates preserves compatibility but creates an unnecessary player dependency and public API burden |
| Project-specific types | Remove `ObjectTemplate`, `IObjectTemplate`, `ITemplate`, `AppState`, `AppStateConfig`, `InteractableFactoryId`, template examples, and interactable migration code | Obsolete shims would reduce immediate breakage but complicate Unity serialization and preserve the wrong boundary |
| Configuration | Public editor-only `AddressablesAutomationConfig : ScriptableObject`, selected by GUID from tracked project settings | JSON-only settings avoid missing-script assets but provide worse Unity object selection and extensibility; path-only references break on moves |
| Project settings storage | `ProjectSettings/TorProduction/AddressablesAutomationProjectSettings.asset` via `ScriptableSingleton`, containing only selected config GUID, schema version, and opt-in flags | `EditorPrefs` is machine-local; package-folder assets are immutable and pollute installation; read-time file generation is unacceptable |
| Setup surface | `Project Settings > Tor Production > Addressables Automation` using `SettingsProvider` | A standalone wizard may later supplement first-run help, but should not be the source of truth |
| Automatic behavior | Off by default; manual Analyze and Apply always available; scene postprocessing explicitly enabled per project | Always-on automation is convenient but unsafe during installation, package upgrades, large imports, and incomplete configuration |
| Group address policy | Normalized path relative to the rule’s source folder, extension removed, optional configured prefix | Filename-only is readable but not unique; GUID addresses are stable but opaque; preserving every existing address permits policy drift |
| Existing entry policy | Enforce configured group, address, and required labels; preserve unrelated labels by default | Exact-label mode remains an explicit rule option because it is destructive |
| Scene identity/address | Scene GUID is identity; preserve the last managed address across rename/move unless a rule explicitly requests regeneration | Name identity cannot support duplicate names; path-only addresses unexpectedly change external references |
| Scene semantics | Generic folder rules with `Addressable` or `LocalBuildSettings` mode and optional string category/labels | Numeric `AppState` mappings belong in the consuming game. Games can derive their own mappings from categories or labels |
| Prefab relocation | Remove from the core package and document as project-specific migration tooling | A generic opt-in mover would still carry collision, shared-reference, ownership, and rollback risks disproportionate to Addressables automation |
| Duplicate dependencies | Analyze using supported `CheckBundleDupeDependencies` APIs; fix only through a version-gated adapter | Private reflection can expose more internals but will silently break across Addressables versions |
| Build workflows | Four explicit kinds: `Full`, `ContentUpdate`, `EditorCompatible`, and `MultiPlatform` | A single “Build” command hides incompatible preconditions and produced artifacts |
| Distribution | Signed semantic Git tags using `?path=/com.torproduction.addressables`, then OpenUPM after API stabilization | Git is simple and supports private use but has weaker discovery/version resolution; OpenUPM is public and discoverable but adds a third-party index; a scoped npm registry adds credentials and operations work |
| License | MIT under Tor Production, conditional on confirmed rights to retained code and preservation of legally required attribution | Proprietary licensing conflicts with the recommended public/OpenUPM path; dual licensing needs separate legal and release design |

Unity documents Addressables 2.7.6 as the released Addressables package for Unity 6000.0. The observed 2.9.1 compile should initially be a tested compatibility combination, not an unsupported blanket claim. [Unity 6000 Addressables version information](https://docs.unity3d.com/ja/6000.0/Manual/com.unity.addressables.html).

The official Addressables workflow distinguishes a new/full build from “Update a Previous Build”; only the latter requires a preserved `addressables_content_state.bin`. [Addressables build workflow](https://docs.unity3d.com/kr/Packages/com.unity.addressables%401.21/manual/get-started-build-addressables.html), [content-update overview](https://docs.unity3d.com/kr/Packages/com.unity.addressables%401.21/manual/content-update-builds-overview.html).

### Planned editor API and types

Public under `TorProduction.Addressables.Editor`:

- `AddressablesAutomationConfig`: serialized project rule set with `schemaVersion`, group rules, scene rules, dependency settings, and build/report defaults.
- `GroupSyncRule`: source-folder GUID, destination group identity, optional address prefix, assembly-qualified type filters, required labels, and label/address policies.
- `SceneFolderRule`: folder GUID, `Addressable` or `LocalBuildSettings` mode, destination group, optional category/labels, and address policy.
- `AutomationScope`: `Groups`, `Scenes`, `Dependencies`, or combined safe presets.
- `AutomationPlan`: immutable, sorted proposed operations plus source settings hash.
- `AutomationReport`: structured diagnostics, warnings, operations, failures, rollback status, and deterministic result ordering.
- `ContentBuildKind`, `ContentBuildRequest`, and `ContentBuildReceipt`.

Execution entry points:

- `AddressablesAutomation.Analyze(config, scope)` performs no project mutation.
- `AddressablesAutomation.Apply(plan)` rejects stale plans, snapshots affected state, applies through public APIs, and returns a report.
- `AddressablesBuildQueue.Enqueue(request)` validates the entire queue before switching targets.
- CLI methods wrap the same services; menus and UI contain no independent business logic.

All implementation services, compatibility adapters, recovery snapshots, AssetDatabase plumbing, and postprocessors remain internal.

### Configuration behavior

- Merely importing assemblies or reading settings never creates files, Addressables settings, groups, schemas, labels, or Build Settings entries.
- Missing project settings means “not configured”; missing selected config means “configuration reference is unresolved.” Both disable automation without exceptions.
- Selecting or creating a config is explicit. Default creation path is proposed as `Assets/Editor/TorProduction/AddressablesAutomationConfig.asset`.
- Folder and config references use GUIDs. Group rules retain both stable group GUID and display name so a missing/recreated group can be diagnosed.
- Every rule validates independently. One missing folder or group does not discard valid rules or rewrite their GUIDs.
- Existing `ProjectConfig.json` and old config assets receive a one-shot, explicit migration preview. Migration never deletes or rewrites legacy assets and reports values it cannot map.
- Configuration assets, Addressables settings, and Build Settings are excluded from Unity player data unless explicitly required by the host application.

### Group synchronization policy

- Normalize null filters and labels to empty collections.
- Empty filters mean all loadable non-folder assets.
- Type filters store assembly-qualified names. Legacy simple names migrate only when exactly one matching type is found.
- Catch `ReflectionTypeLoadException`; retain successfully loaded types and report loader exceptions as diagnostics.
- Reject missing source folders, overlapping rules that claim the same asset incompatibly, unresolved types, duplicate generated addresses, Addressable folder-entry conflicts, and non-buildable groups before Apply.
- Operate on explicit asset entries, not whole-folder entries. An existing Addressable folder entry that owns descendants blocks Apply until the user resolves it.
- Missing groups or `BundledAssetGroupSchema`/`ContentUpdateGroupSchema` produce proposed creation operations; they are created only during confirmed Apply.
- Existing entries are converged even if currently in another group.
- All proposed operations are sorted by asset GUID/path and applied in one batch. Dirty/save calls occur once per affected settings object.
- Dry run and Apply share the same planner. Apply verifies a plan hash so project changes after preview force re-analysis.

### Safe orchestration and recovery

- “Update All” first builds a complete plan for enabled, implemented scopes.
- Default Group contents are never cleared wholesale. Only entries owned by explicit rules may move.
- Apply order: create/validate groups and schemas, group synchronization, scene synchronization, optional dependency fix, then reports.
- The default failure policy is stop and rollback. “Continue independent operations” is an explicit advanced option and is reflected in the report.
- Unity Undo is offered only for ordinary configuration-object edits. Addressables and Build Settings operations do not promise Undo because nested assets and API-driven moves are not reliably covered.
- Before Apply, store a recovery snapshot under `Library/TorProduction.Addressables/Recovery/<operation-id>.json`: entry GUID/group/address/labels, created groups/schemas, managed Build Settings scenes, and managed scene records.
- On failure, restore through public APIs. If rollback is incomplete, retain the snapshot, disable further Apply operations, and present a Recover action.
- No core operation physically moves user assets.

### Scene synchronization model

- Replace `ScenesListConfig` with ordered `SceneFolderRule` entries. The currently unused “additional scene folders” become ordinary rules.
- Folder overlap is invalid unless one rule explicitly excludes the nested folder.
- Scan only `.unity` assets in configured folders and reconcile against managed GUID records.
- Addressable scenes are placed in the configured group and excluded from the package-managed local Build Settings set.
- Local scenes are added to Build Settings with deterministic configured-folder/path order and removed only if previously managed by this package.
- Moves into, out of, or between rule folders become explicit transition operations.
- Renames preserve the existing managed address by default. Duplicate scene names are valid because identity is GUID-based.
- Deleted scenes are removed using the stored GUID/last-known path record; loading a deleted `SceneAsset` is not required.
- Unrelated Build Settings scenes and unrelated Addressables entries are never removed.
- Automatic synchronization, when opted in, first filters changed paths to `.unity`, coalesces work through `delayCall`, uses a re-entry guard, and runs the same planner as the manual command.
- Application-state mapping is removed. Optional string categories and Addressables labels are generic extension points owned by the consuming project.

### Prefab decision

Remove `PrefabsFixerConfig`, `PrefabsFixerController`, `PrefabsFixerMenu`, and `InteractableTemplateFieldsUpdater` from the production package.

Rationale:

- The feature is tied to interactable templates rather than Addressables.
- It reflects private fields from `IObjectTemplate`, which cannot succeed.
- `HandleAmbiguousPrefabs` and `LoadAllInteractables` return null.
- Its core migration logic is commented out.
- It can physically move shared assets, ignores `AssetDatabase.MoveAsset` errors, uses unsafe prefix comparisons, and has no unambiguous destination policy.

Any future prefab organizer should be a separate, explicitly invoked migration package with a reference graph, collision policy, user-confirmed destinations, and recovery journal.

## Execution progress

- [ ] Phase 0 — Reproducible baseline and compile/install safety (implementation complete; hosted CI verification pending)
- [ ] Phase 1 — Explicit setup and configuration
- [ ] Phase 2 — Deterministic group synchronization
- [ ] Phase 3 — GUID-based scene synchronization
- [ ] Phase 4 — Dependency analysis and prefab removal
- [ ] Phase 5 — Build pipeline and existing-build Play Mode
- [ ] Phase 6 — Package layout and API cleanup
- [ ] Phase 7 — Tests, documentation, CI, and release readiness

### First implementation batch record — 2026-08-20

**Status:** Section H implementation is complete and locally verified. Work stopped at the first-batch boundary. No package was published, no publication workflow was enabled, and the existing ignored `ProjectSettings/ProjectConfig.json` in the development checkout was left untouched.

**Completed issue scope:**

- `BASE-001`, `BASE-002`, and `BASE-003`: tracked the Unity `6000.0.78f1` baseline and minimum Project Settings, set the package minimum to Unity `6000.0` plus Addressables `2.7.6`, established the separate `2.9.1` compatibility lane, minimized the development manifest, removed Foundation, and removed the unrelated vendored resolver.
- `CONFIG-001`: configuration reads are inert, invalid/missing configuration is not rewritten, automatic scene processing is fail-closed, and import no longer initializes build processing.
- Narrow `CONFIG-003`: incomplete group, update-all, dependency, prefab/interactable migration, and build commands are visible but disabled; their invoked handlers also fail closed. The legacy settings window can save only after all three existing references are valid.
- Narrow `PKG-001`: production source no longer imports NUnit, the production Menu assembly no longer references Samples, and clean-install compilation succeeds with the `Samples` directory physically absent. Final `Samples~` conversion remains deferred.
- `TEST-001` and `TEST-002`: removed template/runtime-example tests and replaced them with seven package EditMode safety tests covering missing/invalid configuration, project-state immutability, clean installation, fail-closed workflows, manifest pins, and assembly boundaries.
- Narrow `CI-001`: added reusable static/inert/clean-install scripts and a SHA-pinned GitHub Actions matrix for Unity `6000.0.78f1` with Addressables `2.7.6` and `2.9.1`.
- Publication-disabled slice of `CI-002`: deleted `release_publish_to_npm.yml`; no archive, tag, registry, or package publication path replaced it.
- Added `Documentation~/SAFETY.md` describing the intentionally disabled workflows and Phase 0 limitations.

**Remaining/deferred issue scope:**

- Full `CONFIG-003` remains open with `CONFIG-002` for the Phase 1 SettingsProvider, GUID-backed project settings, validation, and migration work.
- Full `PKG-001` remains open for the planned `Samples~` layout and manifest declaration.
- Full `CI-001` remains open for hosted execution evidence, Package Validation Suite, player/PlayMode coverage, sample import, archive validation, and later release gates.
- Full `CI-002` remains open and legally blocked: a protected archive/tag release workflow must not be added or enabled until ownership, relicensing, and required attribution are confirmed.
- All other issues explicitly excluded by Section H remain unstarted.

**Implementation commits:**

- `3a2e77e` — track reproducible Unity baseline.
- `d77cb04` — minimize Unity dependency baseline.
- `7b597be` — make package import inert.
- `5bd329c` — add Phase 0 safety coverage.
- `7d323a9` — add pinned Phase 0 validation and remove npm publication.
- `719a3e6` — gate the remaining legacy interactable migration entry point and harden its regression fixture.

**Commands and verification actually run:**

- Repeated `git status --short`, `git diff --check`, baseline/history inspection, focused diffs, manifest/asmdef inspection, workflow scans, and the Section F `rg` audits.
- Parsed `package.json`, the host manifest/lock, and every package asmdef with `ConvertFrom-Json`; parsed all four CI PowerShell scripts with the PowerShell language parser.
- Ran `Tools/CI/Validate-PhaseZero.ps1` against the tracked `2.7.6` host. It passed package identity/version pins, minimal host dependencies, tracked settings, production dependency boundaries, required test references, package `.meta` pairing, GUID uniqueness, and the no-publication invariant.
- Made a fresh local clone, ran `Set-AddressablesVersion.ps1` for `2.9.1`, then ran `Validate-PhaseZero.ps1 -ExpectedHostAddressablesVersion 2.9.1`. It passed; only the expected temporary host-manifest modification and lock deletion occurred before verified cleanup.
- Ran `Test-CleanInstall.ps1 -ExcludeSamples` on Unity `6000.0.78f1` for Addressables `2.7.6` and `2.9.1`. Each isolated project started without a `Library`, compiled without Samples, passed `8/8` EditMode tests (`7` package tests plus the Addressables documentation stub), created no config/Addressables settings/Build Settings entries, then removed the package and compiled again without changing those states. Final logs contained no C# compiler errors, script-compilation failures, null/unhandled exceptions, or fatal errors. Generated evidence is under ignored `artifacts/clean-install-2.7.6` and `artifacts/clean-install-2.9.1`.
- Confirmed the temporary clean-install projects and local CI clone were removed only after their resolved paths were checked to be below the system temporary directory. The user's already-open development Editor was not closed or used for batchmode.

**Failures and warnings observed:**

- The pre-fix import reproduced the baseline `ScenesListMapper` null-reference failure. The final isolated imports did not reproduce it.
- The first authored test compile exposed unsupported `Assert.Multiple` usage in the bundled NUnit version and an ambiguous `PackageInfo` type; both were corrected before the first passing result.
- The final menu audit found one direct legacy interactable migration menu that had not inherited the update-all gate. Its new regression initially exposed a missing direct `Unity.Addressables` test-assembly reference. The command and direct handler are now gated, the reference is explicit, and both compatibility lanes passed again.
- Passing Unity runs logged a local licensing refresh warning (`Access token is unavailable`) and package-removal runs logged `Curl error 42`; Unity returned exit code `0`, produced passing results, and completed inert removal. These environment diagnostics must not be mistaken for a hosted licensing check.
- Local YAML/actionlint validation was unavailable: PyYAML and the Node `yaml` module were absent, `actionlint` was not installed, and Docker Desktop was not running. The workflow was inspected directly, all new action references are immutable SHAs, and the workflow itself remains unexecuted externally.

**Checks still requiring hosted or later-phase verification:**

- Configure the repository remote and appropriate Unity licensing secrets, then run the new GitHub Actions matrix on a pull request. Do not mark Phase 0 fully verified until both hosted lanes pass and retain their artifacts.
- The isolated projects had empty per-project `Library` directories, but the machine-wide Unity package cache was warm and was not destructively purged.
- Player compilation, PlayMode tests, Package Validation Suite, sample import, archive/install-from-tag checks, and manual visual confirmation of disabled menu presentation remain later-phase or hosted checks; none is claimed as passed here.
- Section F's broad stale-branding, private-reflection, project-specific type, and placeholder scans still report known later-phase files. Production NUnit and Foundation dependency scans are clean; the remaining hits stay deferred under the issue IDs already assigned in Sections D and E.

**Important implementation decisions:** configuration getters never repair or write state; all incomplete legacy automation is hard-disabled behind one gate; the package retains Addressables `2.7.6` as its declared minimum while `2.9.1` is compatibility-only; no existing Unity asset GUID was regenerated; and publication remains impossible from repository workflows.

**Next recommended batch:** after the hosted Phase 0 matrix is green and this batch is reviewed, begin Phase 1 with `CONFIG-002` and the remaining `CONFIG-003` work. Do not start group, scene, dependency, prefab, build, package-layout, or publication phases as part of that configuration batch.

## D. Prioritized roadmap

### Phase 0 — Reproducible baseline and compile/install safety

**Objective:** make cloning predictable, make package import inert, and establish a trustworthy failing/passing baseline without redesigning workflows.

**Dependencies:** owner defaults recorded in section G; Unity license available for CI.

**Likely files/classes:**

- `.gitignore`, `AddressablesProject/Packages/manifest.json`, `packages-lock.json`, tracked minimum `ProjectSettings`, and `ProjectVersion.txt`
- `com.torproduction.addressables/package.json`
- `ScenesListMapper`, `ProjectConfigPathsManager`, all asmdefs, and existing tests
- `.github/workflows/*`

**Tasks:**

- Pin development Unity to `6000.0.78f1`; track `ProjectVersion.txt`, `EditorBuildSettings.asset`, version-control/editor serialization settings, and only settings required to open and test predictably.
- Reduce the host manifest to the local package, Addressables, Test Framework, IDE integration if desired, and required built-in modules.
- Remove the unrelated vendored Mobile Dependency Resolver after a clean reference search and compile.
- Change the package baseline to Unity `6000.0`, Addressables `2.7.6`, and a valid preview SemVer such as `0.1.0-preview.1`.
- Remove the Foundation dependency after isolating/removing the unused `InteractableFactoryId` reference.
- Make `ScenesListMapper` return immediately unless explicit settings and valid configuration exist; do not write settings from a getter.
- Disable menu execution for known incomplete operations with explanatory validation messages.
- Remove NUnit usage from production assembly source.
- Add a clean-install EditMode test asserting no Addressables settings, config file, or Build Settings mutation occurs.

**Migration:** do not create settings automatically. Existing generated `ProjectConfig.json` is left untouched and ignored until the explicit migration phase.

**Validation/tests:** JSON parsing, clean clone compile, package import with no Addressables settings, import with empty Build Settings, domain reload, and repository-diff assertion.

**Definition of done:** a fresh clone opens and compiles on the pinned editor; importing the package into a clean Unity project logs no exceptions and creates no project assets/settings.

**Risks/rollback:** pruning host dependencies may expose accidental assembly references. Make dependency pruning a standalone commit so it can be reverted without reverting install guards.

**Commit boundaries:**

1. Track reproducible Unity baseline.
2. Minimize host and package dependencies.
3. Make import/config reads inert.
4. Replace template tests with baseline safety tests.
5. Add compile/EditMode CI without publication.

### Phase 1 — Explicit setup and configuration

**Objective:** replace hardcoded paths and partial config assets with one validated, project-local configuration system.

**Dependencies:** Phase 0 install safety and test runner.

**Likely files/classes:**

- Replace `ProjectConfigPathsManager`, `ProjectConfigData`, `ConfigsEnum`, and `ProjectSettingsWindow`
- Replace `AddressableAssetsConfig`, `ScenesListConfig`, and related custom drawers
- Add `AddressablesAutomationConfig`, project settings state, validator, `SettingsProvider`, and legacy migration analyzer

**Tasks:**

- Implement GUID-based selected config state with no write-on-read behavior.
- Add explicit Create, Select, Analyze, Migrate Legacy, and Detach actions.
- Validate Addressables settings existence, every folder/rule/group/type/label policy, rule overlap, and automation opt-in.
- Keep Addressables settings creation separate and explicitly confirmed.
- Preserve all valid legacy references when one reference is missing.
- Version configuration data and implement non-destructive migrations.
- Store reports and recovery data under `Library`, not the package.

**Migration:** preview old JSON/config data; map resolvable group and folder rules; ignore numeric app-state mappings with a clear report; never delete old assets.

**Tests/validation:** persistence across domain reload/restart, moved config asset, missing/deleted config, corrupt project state, partial legacy references, schema-version migration, and package removal/detach procedure.

**Definition of done:** every menu and postprocessor receives a validated configuration context or a typed disabled result; no hardcoded project paths remain.

**Risks/rollback:** Unity serialization changes can strand config assets. Preserve `.meta` files and keep migrations versioned and reversible.

**Commit boundaries:** storage model; SettingsProvider; validator; migration preview; setup documentation.

### Phase 2 — Deterministic group synchronization

**Objective:** implement convergent, previewable folder-to-group synchronization.

**Dependencies:** Phase 1 configuration and reporting contracts.

**Likely files/classes:**

- Replace `UpdateGroupsController`, `UpdateGroupsWindow`, `UpdateGroupSettings`, `UpdateGroupsReport`
- Replace `AssetTypes`, `GroupNames`, and relevant `ProjectAssetUtil` behavior
- Add group planner, type resolver, address generator, applier, snapshot, and fixtures

**Tasks:**

- Separate scan, validation, plan, apply, rollback, and reporting.
- Resolve filters by assembly-qualified name and catch partial assembly load failures.
- Generate relative-path addresses and reject collisions.
- Converge existing entries’ group, address, and labels.
- Treat Addressable folder entries as conflicts rather than creating ambiguous descendants.
- Plan missing groups/schemas and create only after confirmation.
- Remove global Default Group cleanup.
- Batch AssetDatabase/Addressables dirty operations and sort reports.
- Expose dry-run in UI and CLI.

**Migration:** convert uniquely resolvable simple type names and misspelled legacy `Lables`; report ambiguous types.

**Tests/validation:** empty/null filters and labels, missing folders/settings/groups, invalid types, duplicate filenames, wrong groups, labels, address collisions, folder entries, group schema creation, failed asset loads, dry-run immutability, rollback, and large-fixture performance.

**Definition of done:** repeated Apply produces an empty plan; failed preflight produces no changes; a forced mid-apply failure restores the snapshot.

**Risks/rollback:** group removal or schema restoration can be version-sensitive. Use public APIs and keep created-object identities in the recovery record.

**Commit boundaries:** pure planner; type/address policies; applier/rollback; UI/CLI; integration tests.

### Phase 3 — GUID-based scene synchronization

**Objective:** synchronize Addressable world scenes and local bootstrap/UI scenes without game-state assumptions.

**Dependencies:** Phase 1 settings and Phase 2 plan/apply/recovery framework.

**Likely files/classes:**

- Replace `ScenesListMapper`, `ScenesListConfig`, `MainMenuConfigEditor`
- Retire `ScenesConfig`, `SceneInfo`, `AppState`, and `AppStateConfig`
- Add scene rule planner, managed scene record, postprocessor gate, and Build Settings adapter

**Tasks:**

- Implement deterministic full reconciliation by scene GUID.
- Support imported, moved, renamed, deleted, and folder-transitioned scenes.
- Preserve managed addresses on rename.
- Keep unrelated Build Settings and Addressables entries intact.
- Handle duplicate names and null/new configurations.
- Mark every modified Addressables/config object dirty once.
- Add `.unity` fast filtering, deferred coalescing, recursion protection, and opt-in postprocessing.
- Replace additional-folder special cases with uniform rules.
- Remove numeric application states; retain only optional generic categories/labels.

**Migration:** map old world/UI/additional folders where their GUIDs resolve. Preserve existing scene entry addresses. Report app-state data as intentionally unmigrated project-owned information.

**Tests/validation:** add, move, rename, delete, duplicate names, transitions between modes, overlapping rules, stale entries, deterministic ordering, postprocessor recursion, missing configuration, and unrelated Build Settings preservation.

**Definition of done:** manual and automatic runs use the same plan; every tested transition converges in one Apply and a second run is empty.

**Risks/rollback:** scene deletion lacks a loadable object. Managed GUID/last-path records must be committed before relying on delete reconciliation.

**Commit boundaries:** data model/planner; Addressables adapter; Build Settings adapter; postprocessor; migration/tests.

### Phase 4 — Dependency analysis and prefab removal

**Objective:** retain safe duplicated-dependency analysis while removing project-specific asset migration.

**Dependencies:** Phase 2 reporting/group services and compatibility matrix.

**Likely files/classes:**

- Replace `CustomCheckBundleDupeDependencies`, `DependencyResolverController`, and menu
- Remove prefab/interactable migration classes and related configuration
- Add Addressables-version adapters and feature capability diagnostics

**Tasks:**

- Use the built-in analyzer’s public lifecycle and documented protected duplicate results, not private fields.
- Default to analyze-only; show implicit duplicate assets and proposed destination group.
- Fix only after explicit confirmation.
- Create/validate the destination group with bundled/content-update schemas.
- Treat already-explicit Addressable assets as report-only, not move candidates.
- Disable Fix with an actionable message on unverified Addressables versions.
- Remove prefab organizer and all physical-move code from the package.

**Migration:** leave prefab assets untouched; document how consumers can copy the old project-specific tool before upgrading if necessary.

**Tests/validation:** duplicate implicit dependencies, explicit entries, missing settings/group, schema creation, analyze immutability, fix idempotence, and unsupported adapter behavior.

**Definition of done:** no private Addressables reflection remains and no core command moves physical assets.

**Risks/rollback:** analyzer result shapes can change. Compile separate adapters with version definitions and fail closed.

**Commit boundaries:** prefab removal; analyze adapter; explicit fix mode; compatibility tests.

### Phase 5 — Build pipeline and existing-build Play Mode

**Objective:** provide explicit, resumable, exception-safe content build workflows.

**Dependencies:** validated settings, compatibility adapters, deterministic report model.

**Likely files/classes:**

- Replace `BuildController`, `BuildMenu`, `TargetPlatform`, and `ReportUpdater`
- Remove `EditorPlaymodeBuildScript`
- Add build request/queue state machine, target mapper, receipt validator, recovery bootstrap, and CLI

**Tasks:**

- `Full`: call the public full-content build API; no prior state-file requirement.
- `ContentUpdate`: require an explicitly selected, existing, compatible state file and run content-update restriction checks.
- `EditorCompatible`: perform a full build for the host editor’s standalone platform, generate a receipt, validate freshness, and optionally select Addressables’ built-in “Use Existing Build” data builder after confirmation.
- `MultiPlatform`: queue explicit requests for Android, iOS, Windows, macOS, and Linux; preflight all installed build support first.
- Compare exact `BuildTarget`, not only target group.
- Check `BuildPipeline.IsBuildTargetSupported` and `SwitchActiveBuildTarget` results.
- Persist job ID, stage, queue, original target, and report paths across domain reload.
- Use `try/finally` restoration; stop on first failure by default.
- Detect stale state after restart and offer Resume, Restore Target, or Abandon.
- Add progress and cancellation between synchronous Addressables build stages.
- Copy build-layout artifacts; never delete or rename Addressables’ source report.
- Distinguish warnings, fatal errors, cancellations, rollback failures, and skipped requests.
- Remove private platform-path reflection and cache only package-owned build receipts.

**Migration:** clear only legacy SessionState keys owned by this package; do not touch Addressables or unrelated PlayerPrefs keys.

**Tests/validation:** full versus update preconditions, absent/incompatible state file, platform mappings, unavailable modules, rejected switch, domain reload continuation, interrupted recovery, restore-on-exception, stop/continue policy, deterministic queue optimization, stale reports, and existing-build receipt freshness.

**Definition of done:** every exit path restores or explicitly records the original build target; full builds work without a prior content-state file; update builds cannot start with an invalid state.

**Risks/rollback:** Unity target switches trigger reloads. Keep state-machine changes isolated and include a manual Reset Build Job command.

**Commit boundaries:** request/preflight model; full/update implementations; target state machine; reports; existing-build validation; CLI/tests.

### Phase 6 — Package layout and API cleanup

**Objective:** establish final assemblies, namespaces, samples, and public surface.

**Dependencies:** replacement workflows no longer reference old runtime/sample types.

**Likely files:**

- Runtime, Editor, Menu, Samples, and Tests asmdefs and `AssemblyInfo.cs`
- Current `Runtime/Data`, `Runtime/Utils`, `EditorExample`, and `RuntimeExample`
- `Samples` and package manifest `samples` metadata

**Tasks:**

- Collapse production into an editor assembly plus narrowly justified editor subassemblies.
- Remove the Runtime assembly if no runtime contracts remain.
- Remove stale `InternalsVisibleTo` declarations and StansAssets namespaces.
- Rename assemblies/root namespaces consistently to `TorProduction.Addressables.*`.
- Ensure test assemblies reference production explicitly and NUnit never leaks into production.
- Move curated examples to `Samples~/BasicSetup` and declare them in `package.json`; Unity’s package guidance requires this layout and manifest metadata. [Unity package samples](https://docs.unity3d.com/ja/2023.2/Manual/cus-samples.html).
- Preserve existing `.meta` GUIDs during retained file moves and validate all serialized references after moving.
- Remove dead examples, read-only drawer, `SceneField`, templates, app states, ID generator, and `SerializableDictionary` unless a remaining editor workflow proves a requirement.
- If `SerializableDictionary` remains, make initialization deterministic, define duplicate-key behavior, expose read-only access, and add serialization tests.

**Migration:** publish a breaking-change table from every removed public type to “removed/project-owned” or its editor replacement. Do not add runtime shims.

**Tests/validation:** assembly graph check, player compilation with the package installed, sample import, no sample dependency from production, API surface snapshot, and missing-script scan.

**Definition of done:** package production assemblies have no dependency on samples, tests, Foundation, or project-specific types.

**Risks/rollback:** moving serialized scripts can affect Unity references. Move source and `.meta` together, compile after each assembly boundary commit, and defer deletion until references are zero.

**Commit boundaries:** namespace/asmdefs; runtime removal; sample conversion; dead-code removal; public API snapshot.

### Phase 7 — Tests, documentation, CI, and release readiness

**Objective:** prove package safety and publish through controlled, reproducible releases.

**Dependencies:** stable configuration and public editor API.

**Likely files:**

- `Tests/Editor`, a dedicated integration fixture project, and `Tools/CI`
- root/package README, `Documentation~`, CHANGELOG, LICENSE, Third Party Notices, contribution guide
- `.github/workflows/*`

**Tasks:**

- Replace all template tests with pure planner tests and Unity integration tests.
- Add an Addressables compatibility matrix:
  - Unity `6000.0.78f1` + Addressables `2.7.6` as the minimum lane.
  - Unity `6000.0.78f1` + Addressables `2.9.1` as the currently observed compatibility lane.
  - Latest pinned Unity 6000.0 LTS patch + latest explicitly verified Addressables 2.x as a scheduled/non-blocking lane until promoted.
- Use explicit version files; never use a floating “latest” in release-gating CI.
- Add clean-project installation, compilation, EditMode, selected PlayMode, package validation, sample import, archive-content, and release metadata checks.
- Replace template docs with installation, setup, configuration, menu/CLI, builds, content updates, samples, compatibility, limitations, troubleshooting, API, changelog, notices, license, and contribution documentation.
- Replace placeholder npm publishing with tag-driven GitHub releases. Require manual environment approval, signed/tagged version agreement, changelog entry, clean package archive, and all required lanes passing.
- Add OpenUPM metadata only after API stabilization and public-repository confirmation.
- Retain MIT only after legal confirmation of Tor Production ownership/relicensing rights and necessary prior attribution.

**Tests/validation:** full CI from clean caches, release dry run, install from local path/archive/Git tag, and sample import into a clean project.

**Definition of done:** a new contributor can clone, open, test, pack, and install using documented commands; a release cannot publish if version, changelog, tests, validation, or legal metadata fail.

**Risks/rollback:** release automation is externally destructive. Start with archive-only dry runs; enable tag publication in a separate reviewed commit and protected environment.

**Commit boundaries:** test fixtures; CI scripts; required workflows; documentation; legal metadata; release dry run; publication enablement.

## E. Detailed issue backlog

### Confirmed defects

| ID / severity | Affected files | Root cause and user-visible consequence | Recommended fix and dependencies | Acceptance criteria and required tests |
| --- | --- | --- | --- | --- |
| `BASE-001` Critical | `.gitignore`, `AddressablesProject/ProjectSettings` | Development version/settings are ignored; a clone cannot predictably open or reproduce cached success | Track the pinned version and minimum settings; Phase 0 | Clean clone opens and compiles; clean-tree check after batch compile |
| `BASE-002` Critical | `package.json`, host manifest/lock | `unity: 2019.3` conflicts with Addressables 2.6+; host uses 2.9.1 | Set Unity 6000.0 and Addressables 2.7.6 minimum; test 2.9.1 separately | Manifest validation and both compatibility lanes compile |
| `BASE-003` High | Host manifest and vendored Mobile Dependency Resolver | Unrelated packages can satisfy accidental references and slow/perturb resolution | Remove after reference scan; Phase 0 | Minimal host resolves and all tests compile from empty Library |
| `CONFIG-001` Critical | `ScenesListMapper`, `ProjectConfigPathsManager` | Import invokes config lookup, writes JSON, and dereferences missing config | Inert getter and opt-in gate | Clean install produces no exceptions or project mutations |
| `CONFIG-002` Critical | `ProjectConfigPathsManager`, `ProjectConfigData` | Hardcoded game paths; duplicated Scenes/Addressables default; corrupt state can replace valid values | GUID selection with versioned project settings | Move/delete/corrupt/recover tests preserve unaffected rules |
| `CONFIG-003` High | `ProjectSettingsWindow` | Loads during `OnEnable`, validates only scenes, and can save null references | Replace with validated SettingsProvider | Every invalid state disables Apply and gives one actionable diagnostic |
| `GROUP-001` Critical | `UpdateGroupsController`, `UpdateGroupsWindow` | Null filters/labels/settings/groups and invalid selected indices are not handled | Planner validation and normalized collections | Null/empty/missing-input tests never throw or mutate |
| `GROUP-002` High | `UpdateGroupsController` | Existing entries are marked complete regardless of wrong group/address/labels | Convergent comparison and explicit operations | Wrong entries converge; second plan is empty |
| `GROUP-003` High | `AssetTypes`, `UpdateGroupSettings`, controller | Scans `GetTypes()` without loader handling and stores ambiguous simple names | Assembly-qualified filters and safe resolver | Ambiguous/missing/partial-load cases are reported |
| `GROUP-004` High | `UpdateGroupsController` | Filename-only addresses collide across folders | Relative-path policy plus collision preflight | Duplicate filenames get unique deterministic addresses |
| `GROUP-005` Critical | `CleanUpDefaultGroup`, `UpdateAllNewAssetsController` | Every Default Group entry is removed before validating subsequent work | Delete global cleanup; move only rule-owned entries | Failed preflight leaves Default Group byte-for-byte equivalent |
| `GROUP-006` Critical | `UpdateAllNewAssetsController` | No preflight, dry run, status propagation, transaction, or recovery; incomplete operations continue | Unified plan/apply/snapshot framework | Forced failure rolls back and reports partial/rollback state |
| `SCENE-001` Critical | `ScenesListMapper` | Global postprocessor processes unrelated imports and assumes configs | Opt-in `.unity` filter, deferred reconcile, recursion guard | Unrelated imports do no work; clean project remains unchanged |
| `SCENE-002` Critical | `ScenesListMapper`, `ProjectAssetUtil` | Name/path identity makes duplicate names, rename, move, and delete ambiguous | GUID-managed records and full reconciliation | All requested scene transition tests converge |
| `SCENE-003` High | `ScenesListMapper`, sample `AppStateConfig` | Hardcoded numeric state values and production dependency on sample assembly | Remove state mapping; generic category/labels | No production-to-sample assembly reference; no numeric states |
| `SCENE-004` High | `ScenesConfig`, `ScenesListConfig` | Null arrays, unused additional folders, stale entries, incomplete dirty marking | Uniform rules, initialized lists, deterministic planner | New config works; all changed objects persist after reload |
| `PREFAB-001` Critical | `PrefabsFixerController`, `InteractableTemplateFieldsUpdater` | Interface reflection cannot find private fields; core methods return null/commented out | Remove from core | No prefab/interactable command or dependency remains |
| `PREFAB-002` High | Prefab fixer files | Prefix false positives, ignored move errors, collision ambiguity, physical movement without recovery | Remove; document separate migration option | Package contains no `AssetDatabase.MoveAsset` production path |
| `DEPS-001` High | `CustomCheckBundleDupeDependencies` | Reflects private `m_ImplicitAssets` and assumes internals | Public/protected API adapter with version gating | Reflection scan finds no private Addressables access |
| `DEPS-002` High | Dependency resolver controller/menu | Missing settings/group/schema checks and no analyze-only mode | Planner, report, explicit fix, schema creation | Analyze is immutable; fix is confirmed and idempotent |
| `BUILD-001` Critical | `BuildController` | All builds call `ContentUpdateScript.BuildContentUpdate` | Implement explicit build kinds | Full build succeeds without state; update rejects absent state |
| `BUILD-002` Critical | `BuildController`, `TargetPlatform` | macOS dictionary uses `EditorWindows`; target groups conflate standalone platforms | Exact mapping for Android/iOS/Win/macOS/Linux | Pure mapping tests cover every supported target |
| `BUILD-003` Critical | `BuildController` | Switch return ignored; no finally restore; stale SessionState; queue continues after errors | Persistent state machine and recovery | Simulated switch/build exceptions restore or retain recovery state |
| `BUILD-004` High | `ReportUpdater` | Build-layout source is assumed, destination deleted, and stale data may be renamed | Copy validated artifacts into operation directory | Missing/stale layout is warning; previous reports remain |
| `BUILD-005` High | `EditorPlaymodeBuildScript` | Private path reflection, Linux exception, stale cache, copied upstream implementation | Use built-in packed-play builder plus package receipt | Win/mac/Linux mapping; stale receipt blocks existing-build mode |
| `API-001` High | Runtime templates, app states, ID utility | Project-specific types form unnecessary public runtime API and retain Foundation | Clean pre-1.0 removal | Player compile has no package Runtime assembly/Foundation dependency |
| `API-002` Critical | `ObjectTemplate` | Auto-properties are not serialized and `SetId` throws | Remove as project-specific | No references or serialized sample assets depend on it |
| `API-003` Medium | `SerializableDictionary` | Mutable exposure, null initialization, and unclear duplicate-key behavior | Remove; or redesign only if proven necessary | If retained: duplicate/null/round-trip tests and read-only API |
| `API-004` Medium | `SceneField`, read-only drawer, example classes | Unrelated/dead utilities; drawer does not restore prior GUI enabled state | Remove from core | Reference scan zero; player/editor compile passes |
| `PKG-001` Critical | Menu/Samples asmdefs, `Samples` | Production Menu assembly depends on Samples | Break dependency; use `Samples~` | Assembly graph test and sample-uninstalled compile pass |
| `PKG-002` High | asmdefs and `AssemblyInfo.cs` | Stale assembly names, namespaces, and mismatched friend assemblies | Rename consistently; remove unnecessary IVT | API/assembly snapshot contains only Tor Production names |
| `TEST-001` Critical | `Tests/Editor`, `Tests/Runtime` | Template tests do not reference product and duplicate examples | Replace with meaningful EditMode/integration tests | No constant-only/template tests remain |
| `TEST-002` High | `RuntimeExampleTest` | Android is included while test asserts WindowsPlayer; runtime test asm references editor runner | Delete/replace and correct test boundaries | Required lanes pass on editor and player compilation |
| `DOC-001` High | READMEs, Documentation, changelog, notices | Stale StansAssets/template content and inaccurate installation guidance | Rewrite complete documentation set | Link/name/template scan clean; docs match UI/CLI |
| `DOC-002` High | `LICENSE.md`, Third Party Notices | Existing copyright differs from requested ownership; notices are placeholders | Legal confirmation and accurate attribution | Release job verifies approved license/notices hashes |
| `CI-001` Critical | `.github/workflows/*` | No Unity compilation, tests, package validation, or clean-install test | Pinned Unity CI and reusable scripts | Every PR runs required compatibility lanes |
| `CI-002` Critical | `release_publish_to_npm.yml` | Literal package placeholder, old action/runtime, broad release triggers | Remove; add protected tag/archive workflow | No publish on edited release; dry-run artifact is verified first |
| `DIST-001` Medium | `package.json`, no configured remote | Repository URL exists in metadata but Git remote and tag policy are absent | Configure/verify remote, protected tags, Git-path documentation | Install from signed tag with package subfolder succeeds |

### Suspected compatibility risks requiring verification

| ID / severity | Risk | Required verification |
| --- | --- | --- |
| `COMPAT-001` High | `CheckBundleDupeDependencies` public/protected members or result shapes may differ across Addressables 2.x | Compile and behavior-test each adapter against 2.7.6 and 2.9.1; use documented analyzer API where available |
| `COMPAT-002` High | Addressables build result, build-layout paths, profile APIs, and content-state validation may vary | Inspect official package source/API for each supported version and run actual full/update builds |
| `COMPAT-003` Medium | Addressables 2.9.1 compiles in the cached Unity 6000 project but is not the version Unity lists as released for 6000.0 | Keep it a compatibility lane until Unity documentation and complete builds/tests confirm support |
| `COMPAT-004` Medium | SettingsProvider, ScriptableSingleton serialization, and batchmode behavior can change across editor patches | Run persistence and clean-install tests on both pinned Unity 6000 lanes |
| `COMPAT-005` Medium | Build support availability differs by CI host and installed modules | Query public support APIs, declare runner modules, and skip only explicitly non-required scheduled targets |

Implementation must re-check these official sources at the start of compatibility work:

- [Unity 6000 released Addressables version](https://docs.unity3d.com/ja/6000.0/Manual/com.unity.addressables.html)
- [Package manifest fields](https://docs.unity3d.com/cn/2023.2/Manual/upm-manifestPkg.html)
- [Git dependencies and package subfolders](https://docs.unity3d.com/ja/current/Manual/upm-git.html)
- [Addressables content-update lifecycle](https://docs.unity3d.com/kr/Packages/com.unity.addressables%401.21/manual/content-update-builds-overview.html)
- [Duplicate-dependency analyzer API](https://docs.unity3d.com/ja/Packages/com.unity.addressables%401.20/api/UnityEditor.AddressableAssets.Build.AnalyzeRules.CheckBundleDupeDependencies.html)
- Current Unity Test Framework command-line and Package Validation Suite documentation for the pinned versions

## F. Verification strategy

These are post-implementation commands and procedures; none were run as mutations during the planning audit.

### Static checks

```powershell
$repoRoot = (Resolve-Path '.').Path
git -C $repoRoot status --short
git -C $repoRoot diff --check
rg -n "StansAssets|PackageSample|NotImplementedException|m_ImplicitAssets|GetField\(|using NUnit\.Framework" "$repoRoot\com.torproduction.addressables"
rg -n "Assets/Modules|AddressableAssetsData/Configs|StateValue|Interactable" "$repoRoot\com.torproduction.addressables"
```

Expected: clean worktree before/after verification; no stale namespace, private Addressables reflection, game path, numeric state, production NUnit, or placeholder implementation hits.

### JSON and manifest validation

```powershell
Get-Content -Raw "$repoRoot\com.torproduction.addressables\package.json" | ConvertFrom-Json | Out-Null
Get-Content -Raw "$repoRoot\AddressablesProject\Packages\manifest.json" | ConvertFrom-Json | Out-Null
Get-Content -Raw "$repoRoot\AddressablesProject\Packages\packages-lock.json" | ConvertFrom-Json | Out-Null
pwsh "$repoRoot\Tools\CI\Validate-PackageManifest.ps1" -PackagePath "$repoRoot\com.torproduction.addressables"
```

The validator must check SemVer, package name, Unity minimum, dependency range, repository URL, sample paths, required docs, assembly boundaries, forbidden package contents, and version/changelog agreement.

### Compilation

```powershell
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.0.78f1\Editor\Unity.exe'
& $unityExe -batchmode -nographics -quit `
  -projectPath "$repoRoot\AddressablesProject" `
  -logFile "$repoRoot\artifacts\compile.log"
```

Fail on compiler errors, package import exceptions, serialization errors, or unexpected mutation outside `Library`, `Logs`, `Temp`, and configured artifact output.

### EditMode tests

```powershell
& $unityExe -batchmode -nographics `
  -projectPath "$repoRoot\AddressablesProject" `
  -runTests -testPlatform EditMode `
  -testResults "$repoRoot\artifacts\editmode-results.xml" `
  -logFile "$repoRoot\artifacts\editmode.log"
```

Most coverage belongs here:

- Pure C# planners: addresses, type resolution, scene transitions, queue ordering, platform mapping, report ordering.
- Unity integration: temporary assets, Addressables settings/groups/schemas, Build Settings, GUID persistence, rollback, dependency analysis.

Fixtures use `Assets/__TorProductionAddressablesTests/<test-guid>` and delete only that verified root through `AssetDatabase.DeleteAsset`. Setup snapshots global Addressables settings and Build Settings; teardown restores them even after assertion failure.

### PlayMode tests

```powershell
& $unityExe -batchmode -nographics `
  -projectPath "$repoRoot\AddressablesProject" `
  -runTests -testPlatform PlayMode `
  -testResults "$repoRoot\artifacts\playmode-results.xml" `
  -logFile "$repoRoot\artifacts\playmode.log"
```

PlayMode is justified only for one dedicated integration fixture that builds editor-compatible content, selects the built-in existing-build mode, and loads a known Addressable test asset. Runtime template/state tests are removed because v1 is editor-only.

### Clean-project installation

```powershell
pwsh "$repoRoot\Tools\CI\Test-CleanInstall.ps1" `
  -UnityPath $unityExe `
  -PackagePath "$repoRoot\com.torproduction.addressables" `
  -AddressablesVersion '2.7.6' `
  -ArtifactsPath "$repoRoot\artifacts\clean-install-2.7.6"
```

Repeat with `2.9.1`. The helper creates an isolated temporary project and asserts:

- Package resolution and editor compilation succeed.
- No `Assets/AddressableAssetsData` or package config is created.
- Build Settings remain unchanged.
- No postprocessor/config exception is logged.
- Package removal leaves Addressables and Build Settings unchanged.
- Only the temporary project is deleted during cleanup.

### Addressables build procedures

The planned CLI shares production services with the UI:

```powershell
& $unityExe -batchmode -nographics -quit `
  -projectPath "$repoRoot\AddressablesProject" `
  -executeMethod TorProduction.Addressables.Editor.Cli.AddressablesCli.Run `
  -torAction full-build `
  -torTarget StandaloneWindows64 `
  -torReport "$repoRoot\artifacts\full-windows.json" `
  -logFile "$repoRoot\artifacts\full-windows.log"
```

```powershell
& $unityExe -batchmode -nographics -quit `
  -projectPath "$repoRoot\AddressablesProject" `
  -executeMethod TorProduction.Addressables.Editor.Cli.AddressablesCli.Run `
  -torAction content-update `
  -torTarget StandaloneWindows64 `
  -torStateFile "$repoRoot\artifacts\released-windows\addressables_content_state.bin" `
  -torReport "$repoRoot\artifacts\update-windows.json" `
  -logFile "$repoRoot\artifacts\update-windows.log"
```

```powershell
& $unityExe -batchmode -nographics -quit `
  -projectPath "$repoRoot\AddressablesProject" `
  -executeMethod TorProduction.Addressables.Editor.Cli.AddressablesCli.Run `
  -torAction multi-platform `
  -torTargets 'StandaloneWindows64,Android,iOS' `
  -torReport "$repoRoot\artifacts\multi-platform.json" `
  -logFile "$repoRoot\artifacts\multi-platform.log"
```

Manual validation additionally verifies:

1. Missing state blocks only content updates.
2. Full build generates a new state file and receipt.
3. Content update uses the preserved release state, not a stale latest file.
4. Missing platform support fails preflight before any target switch.
5. Cancellation/failure restores the original target.
6. Reopening after interruption offers recovery without silently resuming stale work.
7. Editor-compatible build works with Addressables’ built-in “Use Existing Build” mode and rejects stale receipts.

### Package validation and distribution

```powershell
pwsh "$repoRoot\Tools\CI\Run-PackageValidation.ps1" `
  -UnityPath $unityExe `
  -ProjectPath "$repoRoot\AddressablesProject" `
  -PackagePath "$repoRoot\com.torproduction.addressables"

npm pack "$repoRoot\com.torproduction.addressables" `
  --pack-destination "$repoRoot\artifacts"
```

Inspect the archive for production assemblies, docs, `Samples~`, tests as intended, and absence of `Library`, generated reports, host assets, project settings, credentials, or unrelated vendor packages.

Before release:

- Install from local path.
- Install the packed archive through a temporary registry fixture.
- Install from `https://github.com/Yurii-Tor/tor-production-addressables.git?path=/com.torproduction.addressables#<signed-tag>`.
- Verify the tag, `package.json` version, changelog heading, archive version, and release name are identical.
- Run all required Unity/Addressables matrix lanes from clean caches.

## G. Questions for the owner

The interactive questions returned no selections, so the recommended choices below are applied as explicit planning defaults. Changing one requires revising the affected phases before implementation.

1. **Support baseline**

   - Why it matters: determines manifest minimums, Addressables adapters, CI size, and APIs that may be used.
   - Choices: Unity 6 LTS only; Unity 2022.3 plus Unity 6; Unity 2023.1+.
   - **Applied recommendation:** Unity 6.0 LTS, Addressables 2.7.6 minimum, with 2.9.1 as a separately verified compatibility lane.

2. **Pre-1.0 API compatibility**

   - Why it matters: retaining project-specific types would preserve the current assembly and serialization boundary.
   - Choices: clean pre-1.0 break; one-release obsolete shims; preserve existing source API.
   - **Applied recommendation:** clean break with explicit migration reporting and no runtime compatibility shims.

3. **Distribution**

   - Why it matters: determines repository visibility, credentials, release workflow, and installation documentation.
   - Choices: Git tags then OpenUPM; private Git only; scoped npm registry.
   - **Applied recommendation:** signed Git tags using the package subfolder, then OpenUPM after stabilization.

4. **License**

   - Why it matters: the existing MIT file names Stan’s Assets while the requested owner is Tor Production; publication cannot assume relicensing rights.
   - Choices: Tor Production MIT; proprietary; dual license.
   - **Applied recommendation:** Tor Production MIT, with release blocked until ownership/relicensing and required attribution are confirmed.

## H. Suggested first implementation batch

### Batch objective

Make the repository reproducible, make package installation non-destructive, and establish a credible safety-test baseline. Do not redesign group, scene, dependency, prefab, or build behavior yet.

### Included issues

- `BASE-001`: track Unity version and minimum development Project Settings.
- `BASE-002`: correct Unity/Addressables manifest baseline.
- `BASE-003`: minimize host dependencies and remove unrelated vendored resolver after proof.
- `CONFIG-001`: make package import and config reads inert.
- Narrow part of `CONFIG-003`: disable commands when setup is absent or invalid.
- Narrow part of `PKG-001`: remove production NUnit leakage and prevent baseline compile from relying on samples where possible without moving the final layout yet.
- `TEST-001`/`TEST-002`: replace template tests with clean-install, missing-config, manifest, and assembly-reference smoke coverage.
- Narrow part of `CI-001`: pinned compilation and EditMode workflows for Addressables 2.7.6 and 2.9.1.
- Disable `CI-002` publication; do not replace it with a live publishing workflow yet.
- Add a temporary safety notice documenting which workflows remain disabled/incomplete.

### Excluded issues

- Full SettingsProvider/configuration redesign and legacy migration.
- Group convergence, new address policy, schemas, and rollback engine.
- Scene GUID reconciliation and application-state removal.
- Duplicate-dependency fixing.
- Prefab migration redesign or physical asset movement.
- Full/content-update/multi-platform build replacement.
- Runtime API and final assembly deletion/moves.
- `Samples~` conversion.
- Final documentation, OpenUPM, and publication enablement.

### Batch definition of done

- A fresh clone opens on Unity `6000.0.78f1`.
- The package resolves against Addressables 2.7.6 and 2.9.1 test lanes.
- Installing into a clean project creates no config, Addressables settings, groups, labels, or Build Settings entries.
- No automatic postprocessor exception occurs.
- Incomplete commands are visibly disabled rather than failing partway.
- Baseline EditMode tests and clean-install tests pass from empty caches.
- The working tree remains clean after batchmode verification, excluding documented ignored artifacts.
- Publication remains impossible until later release-readiness phases.
