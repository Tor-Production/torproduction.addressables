# Tor Production Addressables — Production UPM Engineering Plan

## Plan status

- Planning: Complete
- Implementation: Phase 7 Preview 3 automated and manual release-quality gates verified; separate explicit publication authorization remains
- Latest completed phase: Phase 6 — package layout and API cleanup complete and verified
- Next incomplete phase: Phase 7 — tests, documentation, CI, and release readiness
- Current active batch: Phase 7 Preview 3 final authorization gate — preserve Preview 2 and Preview 3 drafts/tags and publish only after the owner says exactly `Publish v0.1.0-preview\.3`
- Maintenance starting `main`: `d4516c8f6178b73ec7af3d54ec7cad7f8549e325`
- Source baseline: `ccce9423b7d1f64b76431759052ef5b945e99334`
- Last updated: `2026-08-27`

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

### Phase completion and hosted verification protocol

This protocol is the documented exception to the repository's general commit, tag, and push restriction. It applies only when an entire implementation-plan phase is complete; ordinary commits and intermediate batches do not trigger the paid hosted Unity matrix.

1. Run hosted Unity CI once after each complete implementation-plan phase, not after every commit. A corrective rerun is permitted only when the phase's first hosted run fails or cannot complete and an in-scope correction has been pushed.
2. Before any hosted run, complete all available local validation and update this plan with the phase status, completed and remaining issue IDs, commands and results, manual checks, and blockers.
3. Create the authorized phase-completion commit and push that tested commit to the current remote branch.
4. Confirm GitHub CLI authentication and repository access, discover `.github/workflows/unity_phase_zero.yml`, then dispatch that workflow for the pushed branch with authenticated GitHub CLI, for example `gh workflow run unity_phase_zero.yml --ref <branch>`.
5. Capture the dispatched workflow run ID and URL and watch that exact run through completion with GitHub CLI. Do not infer success from dispatch alone.
6. Require both matrix jobs, Addressables `2.7.6` and `2.9.1`, to complete successfully for the exact pushed phase-completion commit.
7. If either lane fails, cannot start, or cannot be verified, do not create a phase tag. Record the run ID, URL, tested commit, per-lane results, and the exact failure or access blocker in this plan, then stop unless a corrective rerun is authorized by this protocol.
8. After a run, persist its run ID, URL, tested commit, per-lane results, and any failure in a documentation-only verification record commit and push. That evidence-only commit does not require another paid Unity run; the verification record must identify the exact phase-completion commit that was tested.
9. Only after both lanes pass, create the annotated tag `phase-<number>-verified` targeting the exact tested phase-completion commit and push that tag. Never move or reuse a phase-verification tag for a different commit.
10. Never create or push a `v*` tag, create a GitHub Release, publish the package, or enable publication under this protocol. Each of those actions always requires separate explicit release authorization.

**Current hosted-tooling check — 2026-08-22:** GitHub CLI `2.98.0` is installed at `C:\Program Files\GitHub CLI\gh.exe` and authenticated to `github.com` as `Yurii-Tor` with HTTPS Git transport and the required `repo`/`workflow` scopes. CLI repository and workflow discovery succeed for `Yurii-Tor/torproduction.addressables` and active workflow `unity_phase_zero.yml`. The successful hosted run and both matrix jobs were verified through CLI as recorded below. No duplicate paid run, phase tag, `v*` tag, release, or publication was created.

- [x] Phase 0 — Reproducible baseline and compile/install safety (implementation and required hosted matrix complete)
- [x] Phase 1 — Explicit setup and configuration (implementation, manual-verification corrections, hosted matrix, and revised SettingsProvider visual recheck complete)
- [x] Phase 2 — Deterministic group synchronization (implementation, local validation, hosted matrix, evidence record, and verification tag complete)
- [x] Phase 3 — GUID-based scene synchronization (implementation, local validation, hosted matrix, and evidence record complete)
- [x] Phase 4 — Dependency analysis and prefab removal
- [x] Phase 5 — Build pipeline and existing-build Play Mode
- [x] Phase 6 — Package layout and API cleanup
- [ ] Phase 7 — Tests, documentation, CI, and release readiness

### Phase 7 final release batch — 2026-08-26

**Status:** Preview 1 failed the owner's manual clean-project release-quality gate and must not be published. Its draft release was safely removed while its signed tag and the existing `phase-7-verified` evidence tag remain immutable. Preview 2 corrective implementation, complete local validation, the single paid hosted matrix, new verification tag, signed semantic tag, protected draft, exact downloaded-asset verification, and signed-Git consumer verification all pass. The draft remains unpublished pending the owner's manual clean-project test and explicit publication authorization. No registry publication is authorized. OpenUPM and every other registry remain excluded.

**Signing preflight:** Git `2.45.1.windows.1` uses SSH signing with the owner’s ED25519 key `SHA256:MKXGquj6Pe5zW4vuEYEyLpPO0RbGHig/k6w0RHQ9D2E`. The public key is loaded in the Windows SSH agent and registered by GitHub as signing key `1136324`. A disposable annotated tag signed and verified locally with `Good "git" signature`. The repository remote is `git@github.com:Yurii-Tor/torproduction.addressables.git` and GitHub CLI is authenticated as `Yurii-Tor`.

**Bounded history cleanup:** the verified Phase 6 evidence boundary `d9603c64200d8b4ecae653ebc23b04dc9c26df1a` scanned clean. The prohibited former-company text appeared only in the four unverified Phase 7A commits after that boundary. A complete pre-rewrite bundle is retained outside the repository at `C:\Users\morta\Documents\Projects\UnityProjects\tor-production-addressables-phase7a-pre-rewrite-20260826.bundle`. Only that four-commit segment was reconstructed: `dec2eed` → `d7f3bb6`, `67880d8` → `40b18f4`, `3a78b46` → `370a641`, and `2dee18b` → `0a375a6`. The complete range diff showed only the required neutral provenance wording changes. Existing Phase 0–6 tag objects and peeled targets were unchanged. Remote `main` was updated from exact expected SHA `2dee18b1c8ab9d4d8b4e160b1521b3005835d5cb` to `0a375a6cfe97bc5cfdf4fb84b6a68d986def7ccb` through validated `--force-with-lease`; no remote backup ref was created. One contaminated local Codex tree-capture ref was removed after validating its exact object. The resulting scan covered 10 local refs, 15 advertised remote refs, 49 commits, and 925 unique reachable objects, including paths, blob contents, commit metadata/messages, annotated-tag metadata/messages, and ref names; no prohibited match remains. GitHub may retain unreachable server objects until garbage collection, but no repository-owned reachable ref exposes them.

**Release protection:** GitHub environment `release` (ID `20611718886`) requires manual approval by `Yurii-Tor`; its custom deployment policy allows only tag `v0.1.0-preview.1`. Separate environment `release-recovery` (ID `20675850740`) requires the same reviewer and permits only exact branch `main`, because GitHub evaluates deployment policy from the manual event ref rather than the explicitly checked-out tag. No existing environment, ruleset, or branch protection was weakened. Tag pushes use `release`; exact manual recovery uses `release-recovery`. Both paths accept only the fixed preview tag, retain top-level read-only permissions, grant `contents: write` only to the protected release job, and do not run Unity or publish to a registry.

**Exact final candidate and hosted evidence:** candidate `8a80c60e846021403d3382bd71e9ca5e37185832` contains package tree `cb8081dbfcc6a46169d897e02bab4d26cf52ec79`. Local Unity `6000.0.78f1` validation passed development-host EditMode `133/133`, Addressables `2.7.6` path/EditMode `133/133` plus PlayMode `1/1`, Addressables `2.9.1` path/EditMode `133/133`, real sample import/removal on `2.7.6` at `133/133`, and exact `.tgz` install/removal on `2.9.1` at `133/133`; import/removal remained inert. The deterministic archive SHA-256 is `912fc157e597b74d5252881ed8e2e8d287f484111688f2651b8ee302298e9ab3`. PVS retained the fail-closed narrow classification: the unmodified suite exited `1` only for the recorded `PVP-20-1` Roslyn `System.TypeLoadException`, while the hash-identical bundled checker exited `0` with empty stdout/stderr and every other applicable validation passed. The one authorized hosted Unity workflow, run [`32928142216`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32928142216), tested the exact candidate; Addressables `2.7.6` job [`98055049327`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32928142216/job/98055049327) and Addressables `2.9.1` job [`98055049150`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32928142216/job/98055049150) both passed, and independently downloaded XML reports each show `133/133` with zero failed, skipped, or inconclusive. Annotated `phase-7-verified` peels to the candidate. Signed annotated tag object `da33caf04fe178bacb3f5ee60ad9f0c449d32f46` for `v0.1.0-preview.1` peels to the same candidate and GitHub reports its SSH signature as verified.

**Protected release failure and recovery — 2026-08-27:** after the required environment approval, tag-push run [`32928458680`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32928458680) revalidated the signed tag, exact candidate, manifest, GitHub signature, and static release readiness, then failed in `Assert-HostedUnityValidation.ps1` because PowerShell on the Ubuntu runner could not bind the three-argument `File.AppendAllLines` call. Asset construction and draft-release creation were skipped; GitHub still had zero releases. The correction uses the cross-platform `File.AppendAllText(string,string,Encoding)` overload and adds a protected manual recovery entry that runs only from `main`, always checks out `refs/tags/v0.1.0-preview.1`, uses the separately reviewer-gated exact-main `release-recovery` environment, and repeats every original tag/signature/candidate/hosted-run/archive/checksum/release guard. Local execution against GitHub found exactly run `32928142216`, verified its two exact successful jobs, and emitted both expected output lines; release readiness, actionlint `1.7.12`, PowerShell parsing, and `git diff --check` pass. This recovery changes repository CI/evidence only; the signed tag and package tree remain immutable.

The first protected manual recovery, run [`33021858762`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/33021858762), failed at the same line before asset construction because checkout of the immutable signed tag correctly replaced the root worktree with the tag's original helper. No release was created and the tag was unchanged. The corrected recovery keeps the root checkout at the signed tag and performs a second isolated sparse checkout under `.release-recovery` at immutable recovery commit `d573808cef21c39f0689a017a05edb0260b6d13a`, with persisted credentials disabled; only the hosted-validation helper is executed from that pinned checkout. All package validation, archive, notes, and release inputs continue to come from the signed tag.

**Draft release and consumer-install verification:** protected recovery run [`33022702142`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/33022702142) succeeded after approval. It revalidated the signed tag and exact hosted Unity run, rebuilt the archive with the committed checksum, and created draft pre-release ID `377474502` with exactly the `.tgz`, checksum, and release-notes assets. The downloaded GitHub asset is `133385` bytes, has SHA-256 `912fc157e597b74d5252881ed8e2e8d287f484111688f2651b8ee302298e9ab3`, contains exactly one `package/package.json`, and passed disposable Addressables `2.9.1` install/EditMode `133/133`/removal with inert import and removal. The exact signed-tag UPM URL `https://github.com/Yurii-Tor/torproduction.addressables.git?path=/com.torproduction.addressables#v0.1.0-preview.1` independently passed the same `133/133` install/removal gate. The clean-install harness now accepts an explicitly pinned GitHub HTTPS UPM path reference so this check is reproducible; package content remains unchanged from the signed tag.

The owner's failed manual archive attempt used GitHub's automatic whole-repository `torproduction.addressables-0.1.0-preview.1.tar.gz`, whose root contains `.github`, `AddressablesProject`, and the package subdirectory rather than a root UPM manifest. This is not a release-asset defect. The verified attached file `com.torproduction.addressables-0.1.0-preview.1.tgz` was copied beside it for manual use, and the draft release body now prominently distinguishes the supported `.tgz` and Git URL from GitHub's automatic Source code downloads. A body-only draft API edit exposed GitHub's internal `untagged-*` slug as the draft tag name; the draft was explicitly restored to `v0.1.0-preview.1` and exact target `8a80c60e846021403d3382bd71e9ca5e37185832` before publication. The cryptographic Git tag itself was never changed.

### Phase 7 Preview 2 corrective release batch — 2026-08-27

**Failed Preview 1 manual gate and root cause:** installing the exact Preview 1 release `.tgz` in the owner's clean Unity project produced `A meta data file (.meta) exists but its folder 'Packages/com.torproduction.addressables/Samples~' can't be found, and has been created.` Inspection of the exact packaged archive confirmed `package/Samples~.meta`; the same warning is present in prior path, archive, Git-tag, sample-import, PVS, and hosted logs. Unity treats `Samples~` as a hidden package sample root and does not import it as a normal asset folder, so the adjacent root meta is orphaned and causes Unity to recreate the hidden folder and warn. The correction deletes only `com.torproduction.addressables/Samples~.meta` (former GUID `09c7c26f2e2a1d64d9332afe47d33bb0`). The five metadata files inside `Samples~/BasicSetup` remain byte-identical to Preview 1 with GUIDs `5b845519cd81497bb6f04d2de1c78896`, `c375d01d382c4434bc92d5a759950184`, `bd9739a730454e63ba1e6ad90844123a`, `4f42b69c201bcba42a0e7d976c56bd93`, and `b6072c6f7b9037d4f8bc0963f8916ca2`; no sample asset identity changed.

**Preview 1 disposition:** Preview 1 is rejected and will not be published. Before removal, draft release ID `377474502` was revalidated as draft/pre-release, exact tag and target `v0.1.0-preview.1` / `8a80c60e846021403d3382bd71e9ca5e37185832`, and exactly the recorded archive, checksum, and release-notes assets (including archive asset ID `531471190`, size `133385`, SHA-256 `912fc157e597b74d5252881ed8e2e8d287f484111688f2651b8ee302298e9ab3`). Only that draft GitHub Release was deleted. GitHub now has zero releases. Signed tag object `da33caf04fe178bacb3f5ee60ad9f0c449d32f46` still peels to the original candidate and remains signature-verified; the existing `phase-7-verified` tag is also unchanged. Neither tag was deleted, moved, recreated, or force-updated.

**Corrective guards and Preview 2 metadata:** `Assert-NoSamplesTildeMetaWarning.ps1` rejects the exact normalized Unity warning. Static package checks now explicitly require the root `Samples~.meta` to be absent while requiring every retained sample meta and GUID. The clean-install harness intentionally omits the hidden root meta and scans path, archive, signed-Git, sample-import/removal, PlayMode setup/test/cleanup, and package-removal logs. PVS import/main logs and both hosted Unity lanes are also scanned. The detector rejects a known Preview 1 warning log and accepts all twenty current Preview 2 logs. Manifest, changelog, package documentation, release notes, workflow constants, checksum name, and release-readiness validation agree on `0.1.0-preview.2`. GitHub's automatically generated repository source archives are explicitly documented as unsupported UPM tarballs. GitHub environment `release` (ID `20611718886`) still requires owner approval and its only custom deployment policy is exact tag `v0.1.0-preview.2` (policy ID `58356364`); the policy was not broadened to `v*`. There are no npm, OpenUPM, Unity Registry, Asset Store, or other registry publication paths.

**Complete local and hosted Preview 2 evidence:** Unity `6000.0.78f1` development-host EditMode passed `133/133`. Disposable path lanes passed Addressables `2.7.6` EditMode `133/133` plus PlayMode `1/1` and Addressables `2.9.1` EditMode `133/133`; both verified package-import/removal inertness with `Samples~` physically excluded. Real UPM `Basic Setup` sample import/removal passed `133/133` independently on both `2.7.6` and `2.9.1`, preserved the unrelated sentinel and all sample GUIDs, and remained inert. Exact archive install/removal passed `133/133` and inertness independently on both versions. PVS again produced only the narrowly classified `PVP-20-1` Roslyn `System.TypeLoadException` in its unmodified XML-doc launcher; the same bundled checker exited `0` with empty stdout/stderr and every other applicable validation passed. Every Preview 2 log is free of the hidden-sample warning; all non-PVS logs are free of configured compiler/fatal patterns. Candidate `1c756dd2beae7dd34ae1504bb2ec30dd6508d91f` was pushed normally and triggered no workflow. Exactly one paid matrix was dispatched: run [`33036671389`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/33036671389). Addressables `2.7.6` job [`98400727590`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/33036671389/job/98400727590) and Addressables `2.9.1` job [`98400727743`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/33036671389/job/98400727743) passed every required step, including the new warning check. Independently downloaded XML artifacts each report `133/133`, zero failed/skipped/inconclusive, and both logs are warning/compiler/fatal clean. New annotated tag `phase-7-preview-2-verified` peels to that candidate; existing `phase-7-verified` still peels to Preview 1 candidate `8a80c60e846021403d3382bd71e9ca5e37185832`. Signed semantic tag object `216174f9dc143dd301fa261882350f5228b43f8a` for `v0.1.0-preview.2` peels to the Preview 2 candidate and verifies locally and on GitHub with the owner's ED25519 key.

**Protected archive failure and deterministic correction:** protected tag-push run [`33037140748`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/33037140748) validated the tag, signature, exact candidate, package, and hosted matrix, then failed closed before release creation because Ubuntu rebuilt SHA-256 `192e6dc8e0b3dbef2f9e5a54fa341619eb7d0e7acf7f2fe5586100c6eb8f2f48` while the Windows-local evidence had recorded `9df4ceca1d9fd236fd6d262cfef1470a61fa6a536fa95a1e295ca4ad08093139`. GitHub still has zero releases, and neither Preview 2 tag changed. Reproduction proved the long-lived Windows worktree contained mixed CRLF/LF bytes even though `.gitattributes` and every committed Git blob use LF: packing a canonical `git archive` source on Windows produced Ubuntu's exact `192e6d...` archive. Correction commit `4b19ae00ddb6b78ba6376a1f406ff7c4125ff0c8` makes `New-PackageArchive.ps1` copy source into a bounded temporary staging tree, normalize only the repository-declared text extensions to LF at the byte level, package that canonical tree, and verify every extracted file byte against it. Two builds from the mixed Windows tree and one from canonical Git source all reproduce exact SHA-256 `192e6dc8e0b3dbef2f9e5a54fa341619eb7d0e7acf7f2fe5586100c6eb8f2f48`. That corrected exact archive independently passed install/EditMode `133/133`/warning/inertness/removal on Addressables `2.7.6` and `2.9.1`. The correction's ordinary push triggered no workflow. The package source and signed-tag target are unchanged; this is release-infrastructure and checksum evidence only.

**Protected draft and final consumer evidence:** recovery workflow commit `85338777b5dff7ac1b8bc5127ca1f61e2f928738` pins correction commit `4b19ae00ddb6b78ba6376a1f406ff7c4125ff0c8`; its ordinary push triggered no workflow. After owner approval, protected recovery run [`33040437519`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/33040437519), job [`98412555443`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/33040437519/job/98412555443), succeeded. It checked out the signed tag for package and release-note inputs, checked out only the pinned recovery tools/checksum, revalidated the tag/signature/candidate/hosted jobs, reproduced `192e6d...`, and created draft pre-release ID `377569798`. The draft is `draft=true`, `prerelease=true`, `published_at=null`, tag/name `v0.1.0-preview.2`, and exact target `1c756dd2beae7dd34ae1504bb2ec30dd6508d91f`. Its only assets are archive ID `531757685`, `134029` bytes, digest `sha256:192e6dc8e0b3dbef2f9e5a54fa341619eb7d0e7acf7f2fe5586100c6eb8f2f48`; checksum ID `531757687`, `117` bytes, digest `sha256:f8478c4deb6b8edad56acd0a76eacc1017dfe71cabf12e8990b7e5dcd2c17788`; and notes ID `531757684`, `919` bytes, digest `sha256:b59e92fd123311728b3e4a4be28bb868ebce062caf7bee3c949bc74f55b1e5dd`. The downloaded archive is byte-identical to the three canonical local builds, has exactly one `package/package.json`, zero `package/Samples~.meta`, and all five inner sample metas. The exact asset was copied without overwrite to `C:\Users\morta\Downloads\com.torproduction.addressables-0.1.0-preview.2.tgz`; its SHA-256 is `192e6dc8e0b3dbef2f9e5a54fa341619eb7d0e7acf7f2fe5586100c6eb8f2f48`. That exact Downloads file passed disposable Addressables `2.9.1` install/EditMode `133/133`/warning/inertness/removal. The signed-tag URL `https://github.com/Yurii-Tor/torproduction.addressables.git?path=/com.torproduction.addressables#v0.1.0-preview.2` independently passed the same `133/133` gate. All automated release-quality gates are complete.

**Remaining manual publication gate:** in the owner's clean project at `C:\Users\morta\Documents\Projects\UnityProjects\EmptyProjectForTests`, verify the Downloads file's SHA-256, add that exact `.tgz` through Package Manager's **Add package from tarball**, and do not use GitHub's Source code zip/tar.gz. Confirm version `0.1.0-preview.2`, a clean Console with no hidden-sample metadata warning, no automatic Addressables settings/config/Build Settings mutation, successful `Basic Setup` sample import with no missing scripts or changed GUID expectations, and clean sample/package removal without unrelated-state changes. Do not publish until the owner reports that exact manual test passed and explicitly authorizes publication. Even after GitHub publication, npm, OpenUPM, Unity Registry, Asset Store, and every other registry remain unauthorized.

### Phase 7 Preview 3 duplicate-label corrective release batch — 2026-08-27

**Failed Preview 2 manual gate and exact reproduction:** the owner installed the exact unpublished Preview 2 draft asset and found that the shipped Basic Setup scene rule serialized both `m_category: basic-setup` and `m_requiredLabels: [basic-setup]`. Scene planning intentionally treats Category as an implicitly applied Addressables label, so validation saw two effective labels and failed closed with `LabelDuplicate`, leaving the active configuration `InvalidConfig` and blocking Analyze/Apply. This is a package defect, not user error. The exact immutable Downloads archive `C:\Users\morta\Downloads\com.torproduction.addressables-0.1.0-preview.2.tgz` was independently installed into a disposable Addressables `2.9.1` project, the real Basic Setup sample was imported, default Addressables settings were created, the imported config was activated, and validation reproduced `Error:LabelDuplicate:Scenes[0].Labels[1]:Label 'basic-setup' is duplicated in this rule.` The reproduction log is preserved under ignored evidence at `artifacts/phase7-preview3/reproduce-preview2-final/basic-setup-workflow-2.9.1.log`.

**Root cause, correction, and UX:** the sample had modeled the same label twice even though Category already owns the implicit label role. The minimal correction retains Category `basic-setup`, serializes Required Labels as an empty list, and leaves every sample asset, folder, `.meta` file, and GUID unchanged. The Inspector now names the field **Category Label** and explains that it is applied implicitly; Required Labels is described as additional labels. Duplicate Category/Required Labels validation remains fail-closed but reports the error against the visible `Scenes[0].RequiredLabels[index]` field and explicitly identifies the Category Label source instead of exposing an unexplained synthetic `Labels[1]`.

**Why the previous 133-test suite missed it:** the existing shipped-sample test imported and deserialized Basic Setup, checked its schema, paths, GUIDs, scene load, and missing scripts, but asserted the duplicated Required Labels value as expected data. It never created default Addressables settings, activated the imported configuration, invoked configuration validation or Scene Analyze, applied the plan, or checked convergence. Preview 3 adds a focused duplicate-diagnostic test and a disposable clean-project probe that exercises the actual Package Manager sample workflow through activation, expected-only pre-Apply warnings, required group/schema/label/scene operations, successful Apply, a zero-operation second Analyze, sample removal, package removal, and inertness. The authoritative package suite is therefore `134` tests for Preview 3.

**Preview 2 disposition and Preview 3 publication boundary:** Preview 2 is rejected and must not be published. Draft release ID `377569798` remains draft/pre-release and must not be deleted; signed tag object `216174f9dc143dd301fa261882350f5228b43f8a`, semantic tag `v0.1.0-preview.2`, and `phase-7-preview-2-verified` remain immutable and must not be moved, recreated, deleted, or overwritten. Preview 3 is the only active corrective candidate. Its release workflow, documentation, manifest, tests, and checksum target exact `0.1.0-preview.3`; its tag trigger and protected `release` environment policy must allow only exact `v0.1.0-preview.3`, never arbitrary `v*`. No npm, OpenUPM, Unity Registry, Asset Store, or other registry publication is authorized. Preview 3 must stop as a protected draft until the owner manually validates the exact downloaded draft `.tgz` and separately authorizes public publication.

**Canonical repository correction before hosted validation:** the first ordinary Preview 3 implementation push, commit `6242cf62b6b15ffbbc81de84ad1283de43a7a505`, triggered no workflow but GitHub reported that the repository had moved from `Yurii-Tor/torproduction.addressables` to canonical `Tor-Production/torproduction.addressables`. GitHub CLI independently confirms the organization-owned canonical repository. Because that pushed commit still embedded redirecting former-owner URLs in package metadata and current installation instructions, it is superseded before any paid run, tag, or release. Preview 3 package metadata, validators, exported release notes, and current install documentation now use the canonical Tor Production URL; historical evidence links remain as originally recorded.

**Complete local Preview 3 evidence:** PowerShell parsing, tracked JSON/asmdef parsing, Phase 0, manifest/content, release-readiness, actionlint `1.7.12`, and `git diff --check` pass. Development-host Unity `6000.0.78f1` passes `134/134`. Disposable path lanes pass `134/134` and inert removal independently on Addressables `2.7.6` and `2.9.1`, with `Samples~` physically excluded; the `2.7.6` selected built-content PlayMode fixture passes `1/1` with cleanup. Separate source-path lanes on both Addressables versions pass `134/134` plus real Basic Setup import, default Addressables settings creation, configuration activation, expected-only pre-Apply warnings, required group/schema/label/scene-entry planning, successful Apply, zero-operation convergence, exact sample removal, package removal, hidden-sample warning checks, and inertness. After the canonical-owner metadata correction, the deterministic Preview 3 archive SHA-256 is `5f5114372c019c296b7dedd1bc08da4d4b4739eb52ea3587f825752abe167485`; exact final-byte archive installation independently passes the same `134/134` and complete Basic Setup workflow/removal/inertness gate on both versions. Final PVS `0.86.0-preview` exits `1` only for the narrowly recorded `PVP-20-1` Roslyn `TypeLoadException`; the hash-identical bundled checker exits `0` with empty stdout/stderr and every other applicable validation passes. All Preview 3 Unity logs are free of the hidden-sample warning and configured compiler/fatal patterns. Canonical Git-commit archive reproduction remains required immediately before the superseding candidate push; after that, exactly one paid hosted matrix is authorized for the candidate SHA.

**Exact candidate and hosted Preview 3 evidence:** superseding candidate `4db569212015776c26e323c622a466166434637d` was reconstructed from canonical `git archive` bytes and reproduced exact SHA-256 `5f5114372c019c296b7dedd1bc08da4d4b4739eb52ea3587f825752abe167485`. Its normal push advanced canonical `Tor-Production/torproduction.addressables` `main`, triggered no workflow, and left zero hosted runs for the SHA. Exactly one paid matrix was then dispatched: run [`33056059051`](https://github.com/Tor-Production/torproduction.addressables/actions/runs/33056059051), exact head SHA `4db569212015776c26e323c622a466166434637d`, event `workflow_dispatch`, completed `success`. Addressables `2.7.6` job [`98463023367`](https://github.com/Tor-Production/torproduction.addressables/actions/runs/33056059051/job/98463023367) and Addressables `2.9.1` job [`98463023255`](https://github.com/Tor-Production/torproduction.addressables/actions/runs/33056059051/job/98463023255) passed every step, including exact lane selection, Phase 0/package validation, Unity tests, hidden-sample warning guard, inertness, tracked-state verification, and artifact upload. Independently downloaded authoritative XML reports each show `134/134`, zero failed/skipped/inconclusive; both hosted logs are clean of the hidden-sample and configured compiler/fatal patterns. GitHub reports exactly one manual compatibility run for the candidate. All automated gates are complete; only the owner's final manual publication gate remains.

**Immutable tags, protected draft, and final consumer evidence:** annotated verification tag object `9200162b7743b263942ec2c92782385c7c1edba3` for `phase-7-preview-3-verified` peels to exact candidate `4db569212015776c26e323c622a466166434637d` and triggered no workflow. Cryptographically signed annotated tag object `30bcccacd1cb039d2826705e62200fed12079a03` for `v0.1.0-preview.3` peels to the same candidate; local verification reports a good ED25519 signature and GitHub reports `verified=true`, reason `valid`. GitHub environment `release` retains one required reviewer and one custom deployment policy, policy ID `58356364`, updated in place to exact tag `v0.1.0-preview.3` with type `tag`; no wildcard policy exists. Protected tag-push run [`33056556092`](https://github.com/Tor-Production/torproduction.addressables/actions/runs/33056556092), job [`98464679211`](https://github.com/Tor-Production/torproduction.addressables/actions/runs/33056556092/job/98464679211), revalidated the tag/signature/candidate/hosted run, reproduced the checksum, and created draft pre-release ID `377688077`. The draft is `draft=true`, `prerelease=true`, `published_at=null`, tag/name `v0.1.0-preview.3`, and exact target `4db569212015776c26e323c622a466166434637d`. Its unchanged assets are archive ID `532031523`, `134971` bytes, digest `sha256:5f5114372c019c296b7dedd1bc08da4d4b4739eb52ea3587f825752abe167485`; checksum ID `532031527`, `117` bytes, digest `sha256:6eb5d8e792b06d325426675f605fe5ef9ff4d285fb6b1fff46aa13b3f6be8827`; and release-notes ID `532037311`, `1234` bytes, digest `sha256:e1cf27aa49fca1462ddb1029b6614b909cf721be5becbda5a32c916aa7d5240a`. The exact downloaded draft archive is `C:\Users\morta\Downloads\com.torproduction.addressables-0.1.0-preview.3.tgz`, SHA-256 `5f5114372c019c296b7dedd1bc08da4d4b4739eb52ea3587f825752abe167485`; it has one root package manifest, no root `Samples~.meta`, all five required inner sample metas, Category `basic-setup`, and empty Required Labels. That Downloads file and the signed canonical Git URL each independently pass Addressables `2.9.1` install, `134/134`, real Basic Setup activation/Analyze/Apply/convergence, warning checks, exact sample/package removal, and inertness. Preview 2 draft ID `377569798` remains unpublished with its same three assets; no Preview 1, Preview 2, or earlier verification tag changed.

**Owner manual Preview 3 removal result:** the exact draft asset passed the owner's clean-project workflow. Removing `com.torproduction.addressables` also removed `com.unity.addressables` because Addressables was present only as a transitive dependency. Unity correctly retained host-owned `Assets/AddressableAssetsData` and the separately copied Basic Setup sample under `Assets/Samples`; with both packages absent, missing-script references in those retained assets were expected rather than evidence of package-owned destructive behavior. In the disposable project the owner explicitly removed the imported sample and test-only `AddressableAssetsData`, then confirmed unrelated project state remained unchanged. This matches the documented ownership boundary: users remove imported samples separately; AddressableAssetsData, consumer configurations, groups, labels, entries, scenes, and other project state must never be deleted automatically. A project that needs to retain Addressables after removing this package must add `com.unity.addressables` as a direct project dependency first.

**Removal-harness confirmation:** `Test-CleanInstall.ps1` runs the marked `PlayModeFixtureRunner.Cleanup` before removal; that test-only cleanup clears the disposable default settings pointer and deletes only the fixture-created `Assets/AddressableAssetsData`, known fixture asset, packed StreamingAssets data, and ServerData. The Basic Setup workflow probe restores pre-existing Addressables settings and Build Settings or deletes only default settings it created in the disposable project, and restores/removes only its package project-settings record. When `-ImportSample` is active, `Remove-ImportedSample` deletes the copied versioned Basic Setup folder and empty sample parents, verifies the unrelated sentinel hashes, and rechecks inertness before `Test-CleanInstall.ps1` removes `com.torproduction.addressables` from the manifest. Package removal is then compiled and followed by another inertness and sentinel check. These bounded test-fixture cleanups do not authorize production code to delete host-owned data.

**Final publication disposition:** the Preview 3 draft body/manual checklist now states that imported samples require separate removal, a transitive-only Addressables dependency can be removed automatically with this package, users who want to retain Addressables must add it directly before package removal, and `AddressableAssetsData` is host-owned and never deleted automatically. The signed candidate, semantic and verification tags, archive, checksum, attached release assets, hosted evidence, and Preview 2 draft remain unchanged. Every automated and manual Preview 3 release-quality prerequisite is satisfied. The only unsatisfied gate is the deliberately separate publication authorization: do not publish until the owner says exactly `Publish v0.1.0-preview\.3`. npm, OpenUPM, Unity Registry, Asset Store, and every other registry remain unauthorized.

### Phase 7A non-legal release-readiness record — 2026-08-25

**Status:** non-legal/non-publishing Phase 7A work is complete at the authorized local stop boundary and committed in `dec2eedc6e8dde8c5c4a46be970d0a37b05ec82b`; Phase 7 remains incomplete. Final hosted verification, ownership/licensing decisions and resulting notice changes, a real version bump, `phase-7-verified`, any `v*` tag, GitHub Release, registry/OpenUPM publication, and publication enablement are explicitly excluded and were not performed.

**Verified starting state:** after a fresh `git fetch --prune origin`, work resumed on clean `main` at `d9603c64200d8b4ecae653ebc23b04dc9c26df1a`, equal to `origin/main` with `0/0` divergence. Annotated `phase-6-verified` tag object `6f9c7c1794ca4e603d009587c90f4df965605b28` peels to the tested implementation `bf147de69b1bb9f2afb4ca76450027056e4682b4`. Hosted manual run `32689916114` was independently re-read: it tested that exact implementation and both Addressables `2.7.6` and `2.9.1` jobs passed `133/133`. No hosted workflow was dispatched in Phase 7A.

**Phase 7 requirement audit and implementation map:**

| Requirement | Phase 7A implementation/evidence | State |
| --- | --- | --- |
| EditMode and integration coverage | Existing meaningful planner/Unity suites retained; package-layout test narrowly permits the new test-only player assembly; host/path/archive/sample lanes assert exactly `133/133` | Passed locally |
| Selected PlayMode coverage | `Tests/Runtime/ReleaseReadinessPlayModeTests.cs` plus a marked disposable `PlayModeFixtureRunner.cs` build a known Addressable, select Addressables' built-in packed Play Mode builder, load/verify it in PlayMode, assert no package production runtime surface/components, remove fixture/settings, and recheck inertness | `1/1` passed on Addressables `2.7.6` |
| Package path install/removal | Archive-aware `Test-CleanInstall.ps1` retains exact-count/log/inertness checks and bounded temp cleanup | `2.7.6` and `2.9.1` passed; `Samples~` physically absent in both path lanes |
| Sample import/removal | Real UPM import validates config/scene/GUIDs/missing scripts, removes the sample, preserves an unrelated sentinel by SHA-256, removes package, and rechecks inertness | Current package passed on `2.7.6` |
| Package archive | `Validate-PackageManifest.ps1` and `New-PackageArchive.ps1` enforce SemVer/manifest/docs/layout/meta/GUID/prohibited-content rules, compare source/archive file lists, validate extraction, and create SHA-256 | Current archive passed; SHA-256 `e1c7c43c85dc9b2a557b819369ebcfd9f2564819d810e5f7374d5cc011f8cfe4` |
| Archive installation/removal | Clean project uses the produced `.tgz` as its UPM `file:` dependency, runs `133/133`, removes package, and rechecks inertness | Final exact archive passed on `2.9.1` |
| Metadata/version consistency | Package name/version/Unity/dependency/author/repository/sample, current changelog heading, archive filename/embedded manifest, and version-scoped PVS exception are validated together; package version remains `0.1.0-preview.1` | Passed; no real version bump |
| Package Validation Suite | `Run-PackageValidation.ps1` creates a disposable two-pass project with official PVS `0.86.0-preview`, preserves Unity/PVS and direct-checker commands/exits/stdout/stderr, directly gates the hash-identical bundled checker, exports report/log, and asserts inertness | Narrow fallback established: direct checker exits `0` with empty stdout/stderr after public production APIs were documented; Unity/PVS exits `1` only for the exact `PVP-20-1` Roslyn `TypeLoadException`; every other applicable validation succeeds. The unmodified suite is not claimed to pass and no PVS exception/suppression was added. |
| Documentation | Root/package READMEs and `Documentation~` now cover installation/removal, compatibility, configuration, preview/Apply, recovery, groups, scenes, dependencies, builds, CLI, sample use/removal, limitations, troubleshooting, contribution, release process, readiness, and provenance | Implemented and package-validated |
| Provenance/license audit | `Documentation~/PROVENANCE_AUDIT.md` separates repository evidence from owner attestations, verifies the public Stan's Assets template and exact-blob overlap, records all owner answers, and proposes a conservative minimal MIT notice; `LICENSE.md` and `Third Party Notices.md` remain unchanged | Owner decisions recorded; notice edits deferred to separate authorization |
| Required hosted lanes | Manual compatibility workflow retains `6000.0.78f1` plus Addressables `2.7.6`/`2.9.1` and the reviewed stable GameCI pin | Preserved; not dispatched |
| Latest compatibility lane | Separate manual-only experimental workflow pins Unity `6000.0.82f1`, Addressables `2.11.2`, and GameCI `v5.0.0-beta.1` by immutable SHA | Prepared, unverified, no schedule |
| Workflow security/syntax | Obsolete automatic-rebase/PR-assignment workflows removed; semantic-title validation replaced by official `actions/github-script`; official checkout/upload actions moved to Node 24 releases; all third-party actions SHA-pinned; top-level read-only permissions; no ordinary-push/PR/schedule paid Unity trigger or publication permission/path | Static gate and actionlint `1.7.12` pass |
| Release/publication safety | Intended gate order documented; validation scripts create local artifacts only; workflows contain no tag/release/npm/OpenUPM operation and no write permission | Passed; publication intentionally absent |

**Workflow pins and runtime audit:** required workflow uses official `actions/checkout` `v7.0.1` commit `3d3c42e5aac5ba805825da76410c181273ba90b1` and official `actions/upload-artifact` `v7.0.1` commit `043fb46d1a93c77aae656e7c1c64a875d1fc6a0a` (Node 24), plus stable `game-ci/unity-test-runner` `v4.3.1` commit `0ff419b913a3630032cbe0de48a0099b5a9f0ed9`. GameCI's current stable release still declares Node 20, so it is the documented remaining Node-runtime exception in the required paid lane; the available Node 24 `v5.0.0-beta.1` commit `f7d28f891263d875d47ef34370e9e8dd6087e1ef` is confined to the separate manual experimental lane. PR-title validation uses official `actions/github-script` `v9.0.0` commit `3a2844b7e9c422d3c10d287c895573f7108da1b3`. Actionlint `1.7.12` was downloaded from its official release, verified against the release checksum, and accepted all three workflows.

**Exact local evidence:** Windows PowerShell parsing, tracked JSON/asmdef parsing, `git diff --check`, `Validate-PackageManifest.ps1`, `Validate-ReleaseReadiness.ps1`, and actionlint pass. Unity `6000.0.78f1` development-host EditMode passes `133/133`. Disposable path lanes pass `133/133` for Addressables `2.7.6` and `2.9.1`, with package import/removal inert and no configured compiler/import/exception/fatal-pattern hits. The `2.7.6` path lane also passes the single built-content PlayMode test and fixture cleanup. The current-display-name sample lane passes `133/133`, real import/removal, sentinel preservation, and package removal. The current produced archive, SHA-256 `e1c7c43c85dc9b2a557b819369ebcfd9f2564819d810e5f7374d5cc011f8cfe4`, installed on `2.9.1`, passed `133/133`, and removed inertly. Generated projects are deleted; logs/reports/archive/checksum/actionlint stay under ignored `artifacts/` or system temp.

**PVS details:** PVS reports success for Assembly Definition, Assets, ChangeLog, Folder Structure, X-ray, Package Lifecycle, Documentation, Manifest, Meta Files, Package Unity Version, Path Length, Required File Type, Samples, and Unity Version validations; Package Diff is correctly not run because no production version exists. The manifest's Unity-internal company-prefix rule remains the only exact, version-scoped `ValidationExceptions.json` entry. `FindMissingDocs.exe` first identified the undocumented public production API; XML documentation was added only to that surface. A final direct launch copies the unchanged bundled checker directory to a shorter disposable path, proves the PackageCache and executed binaries have identical SHA-256 `c571657558566c4b652a52ef2130a64af462274feca0da234bc9bf6d6ab6729b`, and exits `0` with zero stdout/stderr. Unity's unmodified PVS launch still exits `1` for the exact `PVP-20-1` `System.TypeLoadException` involving `Microsoft.DocAsCode.Metadata.ManagedReference.Roslyn, Version=2.56.6.0`; the script accepts only that shape after the clean direct gate and rejects every different failure. Complete evidence is under ignored `artifacts/phase7a-pvs-xmldoc/final-pvs`.

All applicable PVS validations passed except the PVS 0.86.0-preview XML-documentation child-process launcher, which failed with the recorded upstream toolchain TypeLoadException. The same bundled FindMissingDocs checker was executed independently and confirmed that no public production APIs lack XML documentation.

**Factual provenance summary:** the reachable history begins at corrected commit `728f17a84b9a3d0438e63da5b2182e4404c59fb4` with Yurii Tor as Git author/committer. The audited public Stan's Assets template at commit `b59b0c9c7886d88f85b0449f338eabe9388d3831` is MIT with `Copyright (c) 2020 Stan's Assets`; exact-blob comparison found 24 template-identical initial paths, four current repository paths, and only `LICENSE.md` within current package content. Yurii attests that he alone designed and implemented the product; Stan supplied only that public template; no former prospective company or other human contributor received, commissioned, owned, or contributed the work; Tor Production is his pseudonym/brand; and the retained scene/GUID/configuration/compatibility content may ship. No underlying contract or independent chain-of-title opinion was reviewed. Current `LICENSE.md` and `Third Party Notices.md` remain unchanged in this batch.

**Recorded owner decisions:** continue public MIT distribution; preserve Addressables `2.7.6` as the included minimum and `2.9.1` as a verified compatibility lane; consider `4.0.1` in later deliberate testing; permit retained scene/GUID/configuration/compatibility content to ship; and use the minimal notice wording selected by the audit. The proposed notice result preserves `Copyright (c) 2020 Stan's Assets`, adds `Copyright (c) 2026 Yurii Tor (Tor Production)`, and names the public template/commit once in `Third Party Notices.md`. The owner requested that Codex choose and prepare that minimal wording. No ownership question remains unanswered; optional independent legal review is outside this engineering record.

**Remaining gates:** separately authorize and apply the recorded license/notice edits, then revalidate the resulting exact candidate; separately authorize and pass final hosted required lanes; decide whether the experimental lane is promoted; choose a release version and synchronize changelog/manifest/archive; then separately authorize any Phase 7 tag, `v*` tag, GitHub Release, registry/OpenUPM action. Phase 7 is not complete. The technical implementation is prepared for the final candidate cycle, but this checkout is not yet ready for final hosted verification because the separately excluded notice edits have not been applied and revalidated.

### Phase 7A owner-decision and PVS continuation — 2026-08-25

**Status:** complete at the authorized local boundary in implementation commit `3a78b467cc955e6874ad74067290f375f4e17faf`; Phase 7 remains incomplete. The batch is limited to public production API XML documentation, exact PVS evidence/fallback handling, owner-attestation and template-audit records, static guard hardening, and local revalidation. It stopped before `LICENSE.md`/`Third Party Notices.md` edits, a paid hosted run, version bump, verification or semantic tag, release, or publication.

**Established evidence:** production `package.json` declares only `com.unity.addressables@2.7.6`; PVS `0.86.0-preview` is injected only into a disposable validation-host manifest. The normal Unity/PVS run, direct checker run, exact commands, exit codes, stdout, stderr, hashes, report, classification, and import log are preserved under ignored `artifacts/phase7a-pvs-xmldoc/final-pvs`. Direct `FindMissingDocs.exe` is clean (`0`, empty stdout/stderr); unmodified Unity/PVS is accurately recorded as exit `1` with only the exact known XML child-launcher fault and all other applicable validations successful. A classifier self-test accepts that exact evidence and rejects an extra PVS error, an additional failed validation, a different exception, a different checker path, a failed direct run, and non-empty direct output. Development-host EditMode passed `133/133` with zero failed/skipped/inconclusive and no configured failure-pattern hits. PowerShell, tracked JSON/asmdef, package/manifest, release-readiness, workflow/actionlint, inertness, and diff checks pass. The exact archive at ignored `artifacts/phase7a-pvs-xmldoc/archive/com.torproduction.addressables-0.1.0-preview.1.tgz` has SHA-256 `e1c7c43c85dc9b2a557b819369ebcfd9f2564819d810e5f7374d5cc011f8cfe4`; source/archive contents and metadata match, and its disposable Addressables `2.9.1` install passed `133/133`, removed cleanly, and remained inert.

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

**Next recommended batch at this checkpoint:** after the hosted Phase 0 matrix is green and this batch is reviewed, begin Phase 1 with `CONFIG-002` and the remaining `CONFIG-003` work. That recommendation is fulfilled by the Phase 1 record below; it did not authorize group, scene, dependency, prefab, build, package-layout, or publication work.

### Phase 1 explicit-configuration batch record — 2026-08-21

**Status:** the `CONFIG-002` and remaining `CONFIG-003` implementation scope is complete and locally verified. Work stopped before Phase 2. No Addressables group/entry/label/schema, Build Settings, scene, prefab, dependency, build, package-layout, release, or publication behavior was implemented or enabled. The development host's ignored legacy JSON was left byte-for-byte unchanged, and no new project-settings file or Addressables settings directory was created there.

**Completed issue scope:**

- `CONFIG-002`: added the public editor-only, schema-versioned `AddressablesAutomationConfig` with GUID-backed group and scene-folder rules; added tracked-project `ScriptableSingleton` state containing only the selected config GUID, schema, and scene opt-in flags; resolved the GUID on every read; and added typed missing/corrupt/old/new/deleted/moved/invalid results without read-time repair or saves.
- `CONFIG-002`: added explicit, backup-first schema `0` to `1` migrations and backup-and-reset recovery under `Library/TorProduction.Addressables/Recovery`. Save failures roll in-memory state back and return actionable errors. Newer/unknown schemas remain fail-closed.
- `CONFIG-003`: replaced the active legacy setup surface with `Project Settings > Tor Production > Addressables Automation`. Create, Select, Analyze, Preview Legacy Migration, Create Migrated Configuration, Apply Automatic-Scene Setting, Detach, project-state migration/recovery, and config migration are separate explicit actions. Merely opening or reloading the provider is inert.
- `CONFIG-003`: validation covers supported scopes, schema versions, Addressables-settings existence, persistent main-asset identity, editor-only config placement, `Resources`/Addressables exclusion (including implicit folder entries), source/excluded folders, rule overlap, stable Addressables group GUID plus fallback name, labels, assembly-qualified types, address/label policies, local-scene semantics, and scene-only automatic opt-in. Invalid candidates do not save, and the opt-in Apply control is disabled until the scene context is valid while an existing opt-in can still be turned off.
- `CONFIG-003`: added an explicit read-only legacy preview that parses the three JSON references independently, inspects legacy assets through `SerializedObject` without a Samples dependency, maps exact resolvable group/folder/label/type/scene intent, retains ambiguous or unresolved values so validation fails closed, reports package-owned folders as outside host `Assets`, reports numeric app-state data as intentionally unmapped, and never rewrites/deletes legacy JSON or assets.
- Remaining Phase 0 command gates now consume a typed configuration context. All incomplete group, update-all, dependency, prefab/interactable, build, and automatic scene-reconciliation implementations remain hard-disabled. `UpdateAllNewAssetsController` no longer reaches its destructive legacy sequence.
- Retired the active `ProjectConfigPathsManager`, `ProjectConfigData`, and `ConfigsEnum` APIs while retaining their existing source/meta GUID owners as internal legacy format/DTO/kind identities. The legacy window retains its existing GUID as an inert redirect. Serialized legacy config types and all sample/runtime assets remain unchanged for migration and later owning phases.

**Remaining/deferred issue scope:**

- No Phase 2+ issue was started. `GROUP-001` through `GROUP-006`, `SCENE-001` through `SCENE-004`, `PREFAB-001`/`PREFAB-002`, `DEPS-001`/`DEPS-002`, all build issues, final package layout/API cleanup, and release/publication work remain open.
- `AddressableAssetsConfig`, `UpdateGroupSettings`, `ScenesListConfig`, `ScenesConfig`, the sample `AppStateConfig`, and their serialized assets remain only as legacy migration carriers or later-phase inputs. Removing them now would strand MonoScript GUIDs or implement Phase 2/3/6 work prematurely.
- The public configuration deliberately contains only the rule data whose Phase 1 semantics are defined. Dependency, build, and report defaults will require explicit future schema increments when their owning phases define those contracts.
- Hosted Phase 0 execution, manual Phase 1 persistence/UI checks, and the release/legal gates remain open; therefore the phase checkbox is not marked fully verified and publication remains blocked.

**Implementation commits:**

- `bdcc3d3` — start Phase 1 configuration status.
- `b1de659` — add versioned automation settings.
- `c8c9271` — validate automation configuration.
- `6da6996` — add the explicit automation SettingsProvider.
- `c46f149` — add safe legacy configuration migration and schema recovery.
- `0a3c64f` — gate legacy workflows on validated context.
- `790d451` — reject GUID-ambiguous configuration subassets.

**Commands and verification actually run:**

- Repeated `git status --short`, `git diff --check`, focused/full diffs, history inspection, legacy-reference scans, hardcoded-path scans, workflow/publication scans, host-state checks, and ignored-file hashing. The existing ignored `AddressablesProject/ProjectSettings/ProjectConfig.json` remained SHA-256 `FD8FFB7349A8E90E58310FE563820217BB15DA051483CD7DE17CC77FE1BAD4C9` during the final audit.
- Parsed `package.json`, the host manifest/lock, and all five asmdefs with `ConvertFrom-Json`; all eight JSON files parsed successfully. Parsed `Test-CleanInstall.ps1` with the PowerShell language parser after its final edits.
- Ran `Tools/CI/Validate-PhaseZero.ps1` after the final code changes. It passed package/version pins, minimal dependencies, production assembly boundaries, test references, Unity asset/meta pairing, GUID uniqueness, and the no-publication invariant for Addressables `2.7.6`.
- Ran `Tools/CI/Test-CleanInstall.ps1 -ExcludeSamples` from a new temporary project with no per-project `Library` on Unity `6000.0.78f1` and Addressables `2.7.6`: `33/33` passed, `0` failed/inconclusive/skipped; import and package removal were inert.
- Ran the same clean no-Samples lane with Addressables `2.9.1`: `33/33` passed, `0` failed/inconclusive/skipped; import and package removal were inert.
- Ran `Tools/CI/Test-CleanInstall.ps1` with Samples present on Unity `6000.0.78f1` and Addressables `2.7.6`: `33/33` passed, `0` failed/inconclusive/skipped. The real bundled assets resolved through all three known config GUIDs, exact sample folder/group/label/type/scene-policy mappings were preserved, migration preview was byte-equivalent/inert, and package removal was inert.
- The harness now marks clean-install/no-Samples lanes explicitly and requires exactly `33` discovered and passed tests (`27` Phase 1 cases, `5` Phase 0 cases, and the Addressables required documentation test), with zero failures, inconclusive results, or skips. It preserves XML on a failed Unity exit.
- Final expanded scans of all six EditMode/removal logs found no C# compiler error, compilation failure, `NullReferenceException`, `MissingReferenceException`, serialization/type-load error, unhandled exception, or fatal error. Evidence is under ignored `artifacts/phase1-2.7.6`, `artifacts/phase1-2.9.1`, and `artifacts/phase1-2.7.6-samples`.
- Verified no temporary `TorProductionAddressables-*` project or `Assets/__TorProductionAddressablesTests_*` fixture remained, the host has no `Assets/AddressableAssetsData`, and `ProjectSettings/TorProduction/AddressablesAutomationProjectSettings.asset` was not created by reads/tests.

**Failures and warnings observed:**

- The first core compile exposed namespace shadowing between the new `TorProduction.Addressables.Editor` namespace and Unity's runtime `Addressables` type. The legacy call sites now use an explicit runtime alias; both compatibility lanes passed afterward.
- The first schema-migration test attempted to change a private serialized version through `EditorJsonUtility`, which did not produce the intended old-schema fixture. It now uses `SerializedObject`; the explicit migration test passes.
- The first no-Samples run after adding the bundled-sample test produced `32` passed plus `1` inconclusive and Unity exit code `2` because `Assume.That` is failure-like in command-line test execution. Clean-install and sample exclusion are now explicit command-line markers; clean lanes assert all sample GUIDs are absent and pass, while sample-inclusive lanes require all three assets. The harness also rejects any nonzero inconclusive/skip count.
- Final review found that a config subasset could share its main asset's GUID and make object-field analysis/select target different objects. Candidate validation, selection UI, and schema migration now require the config object itself to be the persistent main asset; the real AssetDatabase lifecycle test covers the rejected subasset case, and all three lanes passed again.
- Passing Unity runs retain the previously observed local licensing-refresh warning and shutdown-time `Curl error 42`; entitlement resolution completed, every final Unity/removal process exited `0`, and no product/import exception pattern was present.

**Checks still requiring Unity/manual/hosted verification:**

- Create/select a real config in a disposable interactive host, restart/domain-reload Unity, and confirm the physical `ScriptableSingleton` selection persists without read-time rewrites. Automated tests cover an actual AssetDatabase create/move/GUID-preservation fixture plus injected deleted-config and save/read backends, but deliberately do not create the production project-settings file.
- Visually exercise the SettingsProvider's disabled/enabled button states, confirmations, embedded diagnostics, legacy preview, and detach/recovery flows. The state predicate and service actions are covered in EditMode; IMGUI presentation itself was not manually inspected.
- Exercise a physically corrupt/older/read-only or VCS-locked project-settings file and a real failed disk save in a disposable project. Injected corrupt/schema/save-failure cases pass, but no user project file was intentionally damaged.
- Run the sample-inclusive fixture on Addressables `2.9.1` if sample migration compatibility on that version is required; the required compatibility lane compiled and passed without Samples, and the exact serialized fixture ran on the declared-minimum `2.7.6` lane.
- Configure the remote and Unity license secrets, then run the SHA-pinned GitHub Actions matrix. Local YAML/actionlint remains unavailable; the new workflow was direct-inspected and has no publication permission/path. The three pre-existing PR utility workflows still use mutable action refs and remain deferred under `CI-001`/Phase 7.
- The machine-wide Unity package cache was warm. Player compilation, PlayMode, Package Validation Suite, packed archive, Git/tag installation, and release checks remain later-phase work and are not claimed as passed.

**Important implementation decisions:**

- Stable group identity uses `AddressableAssetGroup.Guid`; the display name is retained as the recovery/creation hint. Config/folder identity uses Unity asset GUIDs. A selected config must be the persistent main host asset under an `Editor` folder and must not be in `Resources` or be an explicit/implicit Addressables entry.
- The project settings file is not an AssetDatabase asset and has no `.meta`. A newly created config is an AssetDatabase asset and receives a new GUID; every pre-existing `.meta` and GUID remains unchanged.
- Only scene postprocessing can be opted in, and the actual reconciler remains disabled until Phase 3. Automatic group/dependency/build behavior is unsupported.
- Legacy unresolved types and semantically unspecified extra scene folders are retained in the candidate with blocking diagnostics rather than dropped into dangerously broad rules. Numeric app-state data is reported but never migrated. No legacy file or asset is overwritten or deleted.
- The inactive historical body in `ScenesListMapper` remains under `#if false` only until the Phase 3 replacement; restoring its old define is not supported. The later-phase `PrefabsFixerController` hardcoded path is a documented gated exception owned by `PREFAB-001`/`PREFAB-002`, not part of the active configuration system.
- A later editor-assembly rename must add `MovedFrom`/serialization migration for `AddressablesAutomationConfig`; changing the assembly identity without that migration would strand assets. All later schema additions must remain versioned and backup-first.

**Next recommended batch:** after review plus the hosted Phase 0 and manual Phase 1 gates above, begin Phase 2 with `GROUP-001` through `GROUP-006`: implement a read-only deterministic group planner and validation/report contracts first, then a separately confirmed transactional Apply. Do not begin scene reconciliation, dependency/prefab work, builds, package layout, or publication in that batch.

### Phase 1 manual-verification correction record — 2026-08-22

**Status:** the narrow `CONFIG-002`, `CONFIG-003`, and `CI-001` correction scope is implemented and locally verified. Phase 2 was not started. No Addressables content workflow, Build Settings workflow, publication path, or release workflow was added or enabled.

**Manual findings received:**

- Create, Analyze, Detach, explicit configuration persistence, and reopening/reloading after the explicit selection action worked in the interactive host.
- The former `Select` label did not communicate that it persisted the active configuration. Editing the object field before selection changed only pending UI state, so the former `Reload` action restored the saved active value and appeared to detach the pending object.
- There is intentionally no generic Apply action. `Apply Automatic-Scene Setting` remains the separate explicit scene-opt-in action; scene reconciliation itself remains disabled until Phase 3.
- Recovery controls were not shown while stored project state was healthy. This is the intended fail-closed presentation, but the condition was not explained in the healthy UI.
- A normal Windows read-only file attribute did not cause the attempted save-failure case. Hosted CI did not reach Unity because required license secrets were unavailable; a separate run was blocked by the GitHub account billing lock.
- The pinned GameCI test runner emitted its Node 20 migration and `punycode` deprecation warnings when GitHub executed actions using Node 24. These warnings are upstream/tooling concerns, not Unity test failures.

**Completed corrections:**

- `CONFIG-003`: the provider now labels the saved selection as `Active configuration`/`Active asset` and the editable object field as `Pending configuration`/`Pending asset`. Merely changing the object field remains inert and never activates or persists it.
- `CONFIG-003`: `Select` is now `Set Active Configuration`; `Reload` is now `Revert Pending Changes`. Concise help states that the field is pending, Set Active persists the GUID, Revert restores pending UI state from saved project state, and recovery controls appear only for damaged or incompatible stored state.
- `CONFIG-003`: configuration creation buttons now disclose their existing create-and-activate behavior. No generic Apply action was introduced, and the existing scene-specific Apply action remains separate.
- `CONFIG-002`: the production `ScriptableSingleton` save path and rollback behavior were not weakened. The deterministic injected-backend failure test still forces an exception, now also asserting zero successful saves and complete in-memory restoration.
- Narrow `CI-001`: the workflow already passed `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`, and `UNITY_SERIAL` only as masked GitHub secrets to GameCI. A preflight now fails before Unity with names-only guidance unless account credentials plus either license-file or professional-serial material are configured. Static validation requires this preflight and the reviewed stable GameCI SHA, and forbids `ACTIONS_ALLOW_USE_UNSECURE_NODE_VERSION`.

**Windows persistence investigation:**

- The exact physical project-state target is `AddressablesProject/ProjectSettings/TorProduction/AddressablesAutomationProjectSettings.asset` in this repository host, corresponding to `ProjectSettings/TorProduction/AddressablesAutomationProjectSettings.asset` relative to any consuming Unity project. It is not the selected `AddressablesAutomationConfig.asset`, the legacy `ProjectConfig.json`, or an Addressables settings asset.
- Unity's public managed source shows `ScriptableSingleton.Save(true)` delegating to native `InternalEditorUtility.SaveToSerializedFileAndForget`; the native implementation is not published there. A one-test disposable clone probe on Windows with Unity `6000.0.78f1` compared NTFS file identities across explicit saves: each save produced a different identity. After setting the exact existing target to the ordinary `ReadOnly` attribute, the next save still succeeded, produced another identity, and the replacement file no longer carried `ReadOnly`. This locally confirms atomic replacement behavior for the tested editor/platform combination.
- Consequently, an ordinary file attribute is inconclusive as a real failed-write simulation because it protects the replaced file rather than the writable parent-directory replacement operation. No user ACL was changed. A future physical failure check requires a disposable VCS/ACL or filesystem-denial fixture with explicit owner approval; deterministic behavior remains covered by the injected throwing store.

**CI investigation and decision:**

- GameCI's documented personal-license mapping is `UNITY_LICENSE` plus `UNITY_EMAIL` and `UNITY_PASSWORD`; its professional mapping is `UNITY_SERIAL` plus the same account credentials. The preflight accepts those two shapes and never prints a secret value.
- The retained SHA `0ff419b913a3630032cbe0de48a0099b5a9f0ed9` is stable GameCI `v4.3.1` and declares the Node 20 action runtime. Upstream `main` now declares Node 24, but no stable reviewed Node 24 release was selected in this batch. The SHA was not moved to a beta or mutable ref, and no insecure Node override was added.
- Hosted execution remains unverified until repository license secrets are configured and the external GitHub billing lock is cleared. The new preflight will distinguish an absent-secret failure before the longer Unity step without exposing values; it cannot bypass licensing or account billing.

**Implementation commits:**

- `d94a201` — record the manual-verification correction batch as in progress.
- `e245c1b` — clarify active versus pending configuration UX and retain deterministic failed-save coverage.
- `b9ce0cf` — add a non-leaking Unity license-secret preflight while retaining the stable GameCI pin.

**Commands and verification actually run:**

- Read `AGENTS.md` and all `896` pre-correction lines of this plan, inspected the initial Git status/history, and preserved the existing manual-verification host changes.
- Parsed the changed PowerShell scripts with the PowerShell language parser. Exercised the preflight with missing secrets, incomplete license-file credentials, complete license-file credentials, and complete professional-serial credentials; expected failures were clear and both complete shapes passed without printing values.
- Local `actionlint` was not installed, and Python YAML parsing was unavailable (`ModuleNotFoundError: yaml`). The workflow was inspected directly and exercised by the repository's static assertions; no hosted YAML/action execution is claimed.
- Ran `Tools/CI/Validate-PhaseZero.ps1`; static validation passed for the tracked Addressables `2.7.6` lane, including package/version/assembly/meta/publication checks plus the new workflow preflight, stable action SHA, and insecure-Node-override guards.
- Ran `Tools/CI/Test-CleanInstall.ps1 -ExcludeSamples` from fresh temporary projects on Unity `6000.0.78f1` for Addressables `2.7.6` and `2.9.1`. Each lane passed exactly `33/33`, with zero failed/inconclusive/skipped tests; import and package removal remained inert.
- Ran the sample-inclusive Addressables `2.7.6` clean-install lane: exactly `33/33` passed with zero failed/inconclusive/skipped tests; the real legacy sample fixture executed, and import/removal remained inert.
- Ran the disposable Windows `ScriptableSingleton` identity/read-only probe: `1/1` passed and produced the replacement findings above. One temporary probe edit initially had a missing brace and failed compilation; it was corrected, rerun successfully, and the entire verified disposable clone was removed. No probe code or state remains in the repository.
- Repeated focused diffs, `git diff --check`, workflow/secret/Node/publication scans, manifest/asmdef JSON parsing, Unity-result XML checks, log failure-pattern scans, and Unity `.meta` pairing/GUID checks during final audit. Evidence is under ignored `artifacts/phase1-correction-2.7.6`, `artifacts/phase1-correction-2.9.1`, and `artifacts/phase1-correction-2.7.6-samples`.

**Remaining manual/hosted checks and blockers:**

- Reopen the corrected SettingsProvider interactively and visually confirm the active/pending headings, `Set Active Configuration`, `Revert Pending Changes`, explanatory help, and healthy-state recovery note. The exact labels/help and non-mutating provider factory are covered statically/EditMode, but revised IMGUI rendering is not claimed as manually viewed.
- Configure one complete documented Unity credential shape and clear the GitHub account billing lock, then rerun the hosted matrix. The preflight itself has not been executed by GitHub and hosted Unity remains blocked.
- The development Editor remained open with user-created manual-verification Addressables/config/project-state artifacts during this correction batch. They were preserved rather than silently deleted or committed. Close Unity before intentionally archiving/removing those artifacts and restoring the host baseline.

**Next recommended action:** close the development Editor, decide whether to archive or discard the preserved manual-verification host artifacts, visually recheck the corrected SettingsProvider, then configure licensing and clear billing so the hosted matrix can run. Stop at the corrected Phase 1 boundary; do not begin Phase 2 until those gates are reviewed.

### Phase 1 hosted Linux compilation correction record — 2026-08-22

**Status:** the narrow `BUILD-005`/`CI-001` cross-platform Editor compilation correction is complete and verified locally and by the hosted Linux matrix. Phase 2 and the planned Phase 5 build-system redesign were not started.

**Hosted failure and root cause:**

- The Addressables `2.7.6` GitHub Actions lane reached Unity `6000.0.78f1` compilation on Linux and failed before test discovery at `EditorPlaymodeBuildScript.cs(49,73)` with `CS0103: The name 'target' does not exist in the current context`.
- `EditorPlaymodeBuildScript.BuildPath` declared `target` only inside `UNITY_EDITOR_WIN` and `UNITY_EDITOR_OSX` branches. A Linux editor compiled the fallback `throw`, then still compiled the post-conditional reflection call that referenced the undeclared local. The workflow platform was correct; moving CI to Windows would only have hidden the package defect.

**Addressables path semantics and correction:**

- Addressables `2.7.6` maps `BuildTarget.StandaloneWindows64`, `BuildTarget.StandaloneOSX`, and `BuildTarget.StandaloneLinux64` to the `Windows`, `OSX`, and `Linux` platform subfolders respectively. The retained custom script intentionally resolves the editor host's standalone content under `Addressables.LibraryPath + Addressables.StreamingAssetsSubFolder + /<platform>` instead of following `EditorUserBuildSettings.activeBuildTarget` as the public `Addressables.BuildPath` property does.
- Replaced the compile-time partial declaration with one runtime host mapping: `WindowsEditor -> StandaloneWindows64`, `OSXEditor -> StandaloneOSX`, and `LinuxEditor -> StandaloneLinux64`. Unsupported runtime platforms throw an explicit `PlatformNotSupportedException`. There is no suppressed compiler diagnostic, unreachable fallback, or Linux-only source branch.
- Added all three mappings plus the unsupported-platform case to the existing fail-closed EditMode test, preserving the expected `33`-case clean-install count. The custom script's private Addressables reflection and full existing-build replacement remain deferred to `BUILD-005`/Phase 5; this correction changes only host-platform selection.

**Cleanup and verification actually run:**

- After the user closed Unity and explicitly authorized discard, removed only the manual-verification `Assets/AddressableAssetsData`, `Assets/Editor`, related root `.meta` artifacts, and `ProjectSettings/TorProduction` state, then restored the tracked `EditorBuildSettings.asset`. Git was clean before the compilation correction began.
- Ran `Tools/CI/Validate-PhaseZero.ps1`; static validation passed for Addressables `2.7.6`.
- Ran isolated clean-install/EditMode/removal validation with Unity `6000.0.78f1` and Addressables `2.7.6`: exactly `33/33` passed with zero failed/inconclusive/skipped tests; clean import and package removal remained inert.
- Ran the same isolated lane with Addressables `2.9.1`: exactly `33/33` passed with zero failed/inconclusive/skipped tests; clean import and package removal remained inert.
- Scanned all four resulting EditMode/removal logs for compiler errors, compilation failures, unreachable-code diagnostics, import failures, serialization/type-load failures, unhandled exceptions, and fatal errors; no configured pattern was present. Evidence is under ignored `artifacts/hosted-linux-fix-2.7.6` and `artifacts/hosted-linux-fix-2.9.1`.
- Parsed the package/host manifests and all five asmdefs, parsed every CI PowerShell script, verified the existing build-script `.meta` GUID `5301850a6a9277f4fbaa6075ee00a3bc` was unchanged, and confirmed `git diff --exit-code -- AddressablesProject/Assets AddressablesProject/ProjectSettings` after both Unity lanes.

**Hosted check completed:** GitHub Actions run `32554221921` tested exact correction commit `b843fa52846406342cfb624c859b386389bb997a`; both Addressables lanes completed successfully as detailed in the evidence record below. Stop at the remaining Phase 1 visual-verification boundary and do not begin Phase 2 automatically.

### Phase 1 Unity CI cost-control correction record — 2026-08-22

**Status:** the narrow `CI-001` trigger and concurrency correction is complete. Phase 2 was not started, and no validation job, matrix lane, permission, action pin, artifact behavior, or publication path was changed.

- Removed automatic Unity-matrix runs for pull requests and branch pushes. The paid Unity matrix now starts only through `workflow_dispatch` or a pushed version tag matching `v*`.
- Added workflow-level concurrency keyed by workflow and Git ref with `cancel-in-progress: true`, so a newer manual or tag run for the same ref cancels its predecessor.
- Extended `Validate-PhaseZero.ps1` to fail if pull-request/branch triggers return, the manual or `v*` tag trigger is lost, or same-ref cancellation is disabled. The existing license preflight, two Addressables lanes, SHA-pinned actions, read-only permission, inertness checks, tracked-state check, and artifact upload remain unchanged.
- Ran `git diff --check` and `Tools/CI/Validate-PhaseZero.ps1`; both passed. `actionlint` remains unavailable locally, so GitHub's workflow parser and the next intentional manual run remain the hosted syntax/execution checks.

**Completion evidence:** commit `31c14adbe997883cae41aa1994ae095e8934c917` was pushed to `origin/main`. GitHub exposes the updated workflow with only manual and `v*` tag triggers, and the post-push workflow run list contains no run for that ordinary branch push. Do not create a version tag solely to test the workflow and do not begin Phase 2 automatically.

### Phase 1 hosted verification evidence record — 2026-08-22

**Status:** the required hosted Linux correction check passed. This completes the Phase 0 hosted gate and the Phase 1 hosted-matrix gate. Phase 1 remains open only for the separately documented revised SettingsProvider visual recheck; no phase-verification tag was created.

- GitHub Actions run [`32554221921`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32554221921) completed successfully for exact commit `b843fa52846406342cfb624c859b386389bb997a` (`fix: compile editor build path on Linux`).
- Job `96985767565`, `Addressables 2.7.6`, completed successfully. License preflight, compatibility-lane selection, Phase 0 static validation, Unity compilation/EditMode tests, inert-import confirmation, tracked-project-state confirmation, and artifact upload all passed.
- Job `96985767635`, `Addressables 2.9.1`, completed successfully with the same required steps passing.
- The run began under the previous automatic branch-push policy before the cost-control correction was committed. Because it tested the exact correction commit and both required lanes passed, no duplicate paid manual run was dispatched.
- GitHub CLI `2.98.0` confirmed the run SHA, overall conclusion, per-job conclusions, repository access, and active remote workflow. The subsequent CI-policy push `31c14adbe997883cae41aa1994ae095e8934c917` created no Unity workflow run, confirming the new ordinary-branch-push exclusion.

**Next recommended action:** visually recheck the corrected SettingsProvider in the development Editor against the Phase 1 manual-verification checklist, record the result, and only then decide whether Phase 1 is complete. Do not begin Phase 2 automatically.

### Phase 1 SettingsProvider visual verification record — 2026-08-22

**Status:** passed in the development host on Unity `6000.0.78f1`. This closes the final Phase 1 gate. Phase 2 was not started, and no Addressables content, Build Settings, release, or publication operation was enabled or executed.

- Began from a clean tracked worktree with no `Assets/AddressableAssetsData` directory and no `ProjectSettings/TorProduction/AddressablesAutomationProjectSettings.asset`. The ignored legacy `ProjectSettings/ProjectConfig.json` had SHA-256 `FD8FFB7349A8E90E58310FE563820217BB15DA051483CD7DE17CC77FE1BAD4C9`.
- Opened `Tools > Tor Production > Addressables Automation Settings` in the pinned Editor and visually confirmed the corrected `Active configuration`/`Active asset` and `Pending configuration`/`Pending asset` presentation.
- Confirmed `Set Active Configuration` is present and disabled without a pending asset, `Revert Pending Changes` is present and enabled, the pending-versus-active help explains persistence and reversion, and the healthy Lifecycle section explains that recovery controls appear only for damaged or incompatible stored state.
- Confirmed the remaining fail-closed presentation: Analyze, migrated-configuration creation, automatic-scene Apply, and Detach were disabled in the unconfigured host; the content-workflow warning remained visible; and no generic Apply action was present.
- Exercised `Revert Pending Changes` and `Preview Legacy Migration (No Changes)`. Revert preserved the unconfigured state. The preview rendered zero mapped rules for the host's unresolved legacy references and displayed `Legacy preview completed. No files were changed.` No migrated asset was created.
- After both actions, the legacy JSON hash was unchanged, no Tor project-settings asset or Addressables settings directory existed, `git status --short` remained empty, and the current Editor log contained no targeted compiler, compilation, null/missing-reference, unhandled-exception, or fatal-error pattern.
- Ignored screenshots are retained under `artifacts/phase1-settings-visual`, including the clean provider, reverted state, legacy preview, and result-message views. Unity was closed before this record was written.

**Phase decision:** Phase 1 is complete. The required hosted matrix already passed for exact implementation correction commit `b843fa52846406342cfb624c859b386389bb997a`, and the later tracked changes through `886305d5d479930c8967d9351cfafd5d4eada1d1` were CI-policy or documentation-only. Stop here pending review; do not begin Phase 2 automatically.

**Phase-protocol completion:** documentation-only completion record commit `76e2597` was pushed to `origin/main`. Annotated tags `phase-0-verified` and `phase-1-verified` were created and pushed; both peel to exact hosted-tested commit `b843fa52846406342cfb624c859b386389bb997a`. GitHub's post-tag workflow list contained no new Unity run because the paid workflow accepts only manual dispatches and `v*` tags; successful run `32554221921` remained the newest completed verification run. No `v*` tag, GitHub Release, or publication was created.

### Phase 2 deterministic group synchronization implementation record — 2026-08-22

**Status:** `GROUP-001` through `GROUP-006` are implemented, documented, diff-reviewed, locally verified, and hosted-verified. The completion protocol is closed in the hosted evidence record below. No Phase 3+ workflow, automatic group processing, Update All orchestration, build, release, or publication path was enabled.

**Completed issue scope and decisions:**

- `GROUP-001`: replaced the ad-hoc folder window/controller with a read-only group planner shared by UI, public API, and CLI. Null filter/label collections normalize to empty; empty filters include every loadable non-folder main asset; missing settings/folders/config/rules fail closed without mutation.
- `GROUP-002`: existing explicit entries now converge to the configured destination group, generated/preserved address, and label policy. Preserve-unrelated is the default; exact labels remain explicit. A successful state produces an empty second plan.
- `GROUP-003`: active filters use assembly-qualified names and unresolved filters block Apply. The explicit legacy preview retains successfully loaded types after `ReflectionTypeLoadException`, reports loader exceptions, converts only uniquely resolvable simple names, and continues to read the misspelled serialized legacy `m_lables` field without rewriting legacy assets.
- `GROUP-004`: relative-path addresses use normalized source-relative paths, remove only the final extension, and accept an optional validated prefix. Final addresses are checked against other claimed assets and unrelated explicit entries before Apply; duplicate filenames in separate subfolders remain deterministic and distinct.
- `GROUP-005`: the active global Default Group cleanup and filename-only updater were removed while their Unity asset/meta identities were preserved as inert source placeholders. The new path never clears a group wholesale or physically moves source assets; only explicitly claimed entries can move.
- `GROUP-006`: `AutomationPlan` is an immutable, deterministically sorted operation/diagnostic snapshot with source-state and plan hashes. Apply re-analyzes and rejects stale plans, creates/validates groups and `BundledAssetGroupSchema`/`ContentUpdateGroupSchema` before entries, batches explicit dirty/event calls, and writes `Library/TorProduction.Addressables/Recovery/group-sync-<operation-id>.json` before mutation. The default failure policy stops and rolls back through public Addressables APIs. Incomplete rollback retains the snapshot, blocks later Apply, and exposes confirmed UI plus CLI recovery.
- At the original Phase 2 boundary, failed main-asset loads were reported and skipped. The pre-Phase 3 maintenance review supersedes that policy: a failed load is now blocking because skipping an unreadable candidate cannot prove complete convergence.
- Enabled only the manual `Groups` scope in Project Settings and `Tools > Tor Production > Addressables > Synchronize Groups...`. The SettingsProvider resolves Groups independently from unimplemented scene rules. `AddressablesAutomationCli.AnalyzeGroups`, `ApplyGroups`, and `RecoverGroups` wrap the same services and emit structured JSON. `Update All`, automatic scene processing, dependencies, prefab relocation, and builds remain fail-closed.
- Added `Documentation~/GROUP_SYNCHRONIZATION.md`, updated the offline documentation, safety notice, and changelog, and raised the clean-install harness expectation from 33 to 56 EditMode cases. No license, provenance, attribution, package version, release, or publication decision changed.

**Local validation and evidence:**

- Pinned Unity `6000.0.78f1` compiled the final package and ran the development-host Addressables `2.7.6` EditMode suite: `56/56` passed, zero failed/skipped/inconclusive, process exit `0` (`AddressablesProject/Logs/Phase2EditMode-2.7.6-final.xml`).
- The temporary clean-install/removal harness passed on Addressables `2.7.6` and `2.9.1`: each lane compiled and passed `56/56`, confirmed import remained inert before and after package removal, found no targeted compilation/import exception pattern, and deleted its temporary project. Retained ignored artifacts are under `artifacts/phase2-local/final-2.7.6` and `artifacts/phase2-local/final-2.9.1`.
- The 56-case suite includes null/empty inputs, missing settings/folders/groups, invalid filters, duplicate filenames, wrong groups/addresses/labels, preserve/exact label policies, address collisions, folder entries, schema planning, read-only/non-buildable groups, failed loads, incompatible claims, active-config protection, dry-run state hashing, immutable public plan collections, deterministic 2,500-asset planning under the five-second budget, transaction success, forced mid-apply rollback, and incomplete-rollback recovery retention.
- Two integration cases create persisted temporary Addressables settings/assets through the public `2.7.6`/`2.9.1` APIs. One verifies actual group, schema, label, entry, and address creation; the other forces a failure after mutation and verifies the created group/entry are removed, the Default Group entry count is identical, and the recovery file is cleared after successful rollback.
- `Tools/CI/Validate-PhaseZero.ps1 -ExpectedHostAddressablesVersion 2.7.6` passed after restoring the tracked minimum lane. `git diff --check`, the targeted unsafe updater/default-cleanup scan, tracked `AddressablesProject/Assets`/`ProjectSettings` diff, and tracked host manifest/lock diff all passed. The development host's known ignored legacy `ProjectSettings/ProjectConfig.json` remains untouched, so the host itself is intentionally not a valid input to the clean-project-only `Assert-InertProject.ps1`; both fresh temporary lanes passed that assertion instead.
- The complete Phase 2 diff was reviewed for scope boundaries, inert reads, GUID/meta preservation, public Runtime/Editor/Samples/Tests assembly direction, stale-plan coverage, rollback ordering, source-asset immutability, deterministic ordering, and Phase 3+ gating. At this implementation-boundary review, no paid hosted Unity job had been dispatched; the single later dispatch is recorded below.

**Implementation-boundary protocol status:** complete. The focused completion commit, single exact-commit hosted run, evidence-only commit/push, and exact-commit `phase-2-verified` tag are all complete as recorded below. No `v*` tag, GitHub Release, package publication, or other release artifact was created or authorized.

### Phase 2 hosted verification evidence record — 2026-08-22

**Tested completion commit:** focused Phase 2 commit `2bd0ab66ccbba60a55aecd01db37141e9462d999` (`feat: implement deterministic group synchronization`) was pushed to `origin/main`, and `git ls-remote` confirmed `refs/heads/main` at that exact SHA before dispatch.

**Hosted run:** manually dispatched `unity_phase_zero.yml` once for `main`. GitHub Actions run `32591069542` ([run URL](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32591069542)) used event `workflow_dispatch`, reported head SHA `2bd0ab66ccbba60a55aecd01db37141e9462d999`, started `2026-08-22T18:33:03Z`, completed `2026-08-22T18:36:02Z`, and concluded `success`.

- Addressables `2.7.6`: job `97074887730` ([job URL](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32591069542/job/97074887730)) completed successfully in approximately `2m54s`. Checkout, license preflight, lane selection, repository validation, Unity compile/EditMode tests, inert-project assertion, tracked-project-state assertion, and artifact upload all passed.
- Addressables `2.9.1`: job `97074887736` ([job URL](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32591069542/job/97074887736)) completed successfully in approximately `2m52s`. The same required steps all passed.
- GitHub emitted non-failing Node.js 20 deprecation annotations for the intentionally SHA-pinned checkout, artifact, and GameCI actions while executing them on Node.js 24. These annotations did not skip or fail any required step and do not change the successful lane conclusions.
- The post-run manual-dispatch list contained only run `32591069542` for this workflow/branch/SHA, confirming no duplicate paid Phase 2 run was dispatched.

**Phase decision:** every Phase 2 acceptance criterion and both required hosted lanes pass for exact completion commit `2bd0ab66ccbba60a55aecd01db37141e9462d999`.

**Phase-protocol completion:** documentation-only hosted-evidence commit `b0b756bdf2524f9b1057d7f4095820c55e56cbdd` was pushed to `origin/main`. Annotated tag `phase-2-verified` was then created and pushed; its local and remote peeled target is the exact hosted-tested commit `2bd0ab66ccbba60a55aecd01db37141e9462d999`, not the later evidence commit. The post-tag workflow list contained no new run, and manual run `32591069542` remained the sole Phase 2 dispatch. No `v*` tag, GitHub Release, package publication, or other release artifact was created.

### Pre-Phase 3 maintenance and history-sanitization record — 2026-08-23

**Status:** complete and verified locally, from a fresh rewritten clone, and by the hosted compatibility matrix. Starting remote and local `main` both resolved to `d4516c8f6178b73ec7af3d54ec7cad7f8549e325` after fetch/prune. The repository had only `main`; no open pull requests, forks, releases, branch protection, or rulesets; and only the owner collaborator. Existing annotated tags peeled to `b843fa52846406342cfb624c859b386389bb997a` for `phase-0-verified` and `phase-1-verified`, and `2bd0ab66ccbba60a55aecd01db37141e9462d999` for `phase-2-verified`.

**Finding decisions:**

1. The top-level phase state was obsolete documentation; it now distinguishes latest completed Phase 2, next Phase 3, and the active maintenance batch.
2. Missing durable phase progression was a governance defect; `AGENTS.md` now requires advancing the next incomplete phase without reopening completed phases for unrelated cleanup.
3. The lone current-tree former-owner token in `AGENTS.md` was obsolete wording; it is removed and covered by a non-literal static guard.
4. The package repository URL was a metadata defect and now matches `Yurii-Tor/torproduction.addressables`.
5. The paid workflow's Phase 0 display name was inaccurate; only its display name is made phase-neutral, while its file identity and manual/`v*` triggers remain unchanged.
6. Public template READMEs, sample changelog entries, contribution links, placeholder notices, and initialization content were misleading defects and are rewritten now. Assembly names, example code, Samples layout, and final documentation breadth remain planned Phase 6/7 work and those phases remain open.
7. `LICENSE.md` contains an existing Stan's Assets copyright, which may be legally required and is preserved byte-for-byte. Provenance, relicensing, redistribution rights, and the final third-party notice set remain legally blocked pending owner/legal confirmation; no release is authorized.
8. An exception escaping `IGroupSyncMutationBackend.TryRollback` was a safety defect; Apply now returns a structured failed report and recovery path.
9. Snapshot update/deletion exceptions in the Unity rollback backend were safety defects; snapshot writes are atomic and an unfinished cleanup retains recovery evidence and blocks later Apply.
10. Warning-only asset-load failure was inconsistent with fail-closed convergence. It is now a blocking diagnostic; no partial-application exception remains.
11. Missing direct coverage of public stale-plan rejection, a real pending recovery file, rollback-backend exceptions, CLI success/failure, and repeated public analyze/Apply convergence was a maintenance test defect; focused and integration coverage is added in this batch.
12. Missing current-tree reintroduction prevention was a maintenance defect; the existing static validator now scans tracked paths and raw tracked contents case-insensitively while constructing the prohibited token only at runtime.

**Intentional boundaries:** the existing legal attribution is not branding cleanup. Phase 6 owns assembly/runtime/example/sample removal; Phase 7 owns the complete documentation, CI, legal metadata, and release-readiness pass. Neither phase is marked complete. No `v*` tag, release, publication workflow, GitHub Release, or package publication is enabled by this maintenance batch.

**Local maintenance verification:** Windows PowerShell parsing, JSON/asmdef parsing, `git diff --check`, current-tree prohibited-token/path scans, public-template residue scans, host tracked-state checks, Unity `.meta` pairing/GUID checks, and `Tools/CI/Validate-PhaseZero.ps1 -RepositoryRoot .` pass. Isolated Unity `6000.0.78f1` clean-install/EditMode/removal lanes passed on Addressables `2.7.6` and `2.9.1`: each discovered and passed exactly `61/61`, created no configuration/Addressables/Build Settings state on import, remained inert after package removal, and removed its verified temporary project. Evidence is under ignored `artifacts/maintenance-local-2.7.6` and `artifacts/maintenance-local-2.9.1`.

The first `2.7.6` attempt stopped at test compilation because the expanded integration fixture omitted the `UnityEditor.AddressableAssets` namespace import. No tests ran and no source project state changed. The import was added and the necessary lane reran successfully; no paid hosted workflow was dispatched during local maintenance.

**Rewrite and backup evidence:** focused maintenance commit `9bc50cdd0a7fd32d4f7ce676927cabb3d53de8b9` was pushed normally and created no paid run. A fresh sibling mirror was filtered with `git-filter-repo` `2.47.0`; the exact discovered content phrase was replaced by neutral former-owner wording, while defensive callbacks covered paths, commit/tag messages, refs, and author/committer/tagger names and emails. No matching path, ref, message, metadata, namespace, URL/domain, email domain, compact identifier, or legal notice had been discovered. The complete verified pre-rewrite bundle is `C:\Users\morta\Documents\Projects\UnityProjects\tor-production-addressables-pre-rewrite-20260823.bundle`; the original checkout is retained with its push URL disabled.

Immediately before force-push, a fresh remote fetch still showed only `main` at `9bc50cdd0a7fd32d4f7ce676927cabb3d53de8b9` and the three unchanged phase tags, with no open pull requests. Rewritten `main` is `caefbf8511b761662e64e6820666928a41adb36d`. Annotated `phase-0-verified` and `phase-1-verified` now peel to rewritten `7dc1860f60ff649b5e13e1e2e71f09fa546cb140`; annotated `phase-2-verified` peels to rewritten `86a977e254fd243ff171efafc07c10a8980c2771`. The current tree remained exact (`d8343d18f90137466b4e22b36c22dcdfe85bf36d`), all four ref updates succeeded, and no other branch/tag update failed. Rewriting changed commit/tag-object hashes and stripped any old cryptographic signatures; the recreated phase tags are annotated but not claimed to retain prior signatures.

**Fresh-clone verification:** a new normal GitHub clone fetched every branch/tag and independently scanned all repository-owned reachable refs: `623` objects and `374` unique blobs, plus historical paths, commit authors/committers/messages, annotated tag messages/taggers, and ref names. No prohibited token remained. Static validation, package/host JSON and every asmdef parse, Unity `.meta` pairing/GUID uniqueness, no-publication/no-version-tag checks, exact phase-tag mapping, and clean-worktree checks passed. Fresh-clone Addressables `2.7.6` and `2.9.1` clean-install/EditMode/removal lanes each passed exactly `61/61`; import/removal remained inert and tracked host state stayed unchanged.

**Hosted rewritten-baseline evidence:** manual workflow dispatch occurred exactly once for rewritten `main`. Run [`32607348409`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32607348409) used `workflow_dispatch`, tested exact SHA `caefbf8511b761662e64e6820666928a41adb36d`, and completed successfully on `2026-08-23`. Addressables `2.7.6` job [`97114386713`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32607348409/job/97114386713) passed every required step in `3m24s`; Addressables `2.9.1` job [`97114386556`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32607348409/job/97114386556) passed every required step in `2m43s`. Both passed license preflight, lane selection, static validation, Unity compilation/EditMode tests, inertness, tracked-state, and artifact upload. Only the previously documented non-failing upstream Node runtime deprecation annotation remained.

**Guarantee boundary:** every repository-owned reachable branch, tag, ref, historical/current blob, path, and Git metadata field has been sanitized, and the static current-tree guard prevents accidental reintroduction. External clones, forks, caches, backup bundles, pull-request refs, old workflow records/artifacts, and provider-side caches are outside central control and are not claimed sanitized. The backup is intentionally retained and must be protected as sensitive pre-rewrite history. No version tag, GitHub Release, package publication, or repository setting was created or changed.

### Phase 3 GUID-based scene synchronization implementation record — 2026-08-23

**Status:** complete. Phases 0–2 remain complete; their rewritten verification tags retain the exact semantic targets recorded above. Phase 2 was not reopened for unrelated polishing. This batch is limited to `SCENE-001` through `SCENE-004`; Phase 4 work remains excluded.

**Completed scope and decisions:**

- `SCENE-001`: the asset postprocessor performs a case-insensitive `.unity` path check before configuration resolution, coalesces relevant events through `delayCall`, requires the existing explicit automatic-scene opt-in, and suppresses re-entry while the shared public transaction runs. Missing/invalid setup and unrelated imports remain inert.
- `SCENE-002`: scene identity is the Unity GUID. A deterministic full planner covers add, rename, move, delete, duplicate filenames, stale entries, and Addressable/local folder transitions. Managed records retain GUID and last-known path so a deleted local Build Settings row can be removed even after its GUID no longer resolves. Preserve-managed-address retains the prior address; relative-path policy deliberately regenerates it. Explicit claims never clear unrelated Addressables entries or Build Settings rows.
- `SCENE-003`: the active and bundled numeric application-state types/sample were retired. Optional categories and labels are strings; no production assembly references the sample assembly. Legacy application-state GUIDs remain read-only migration inputs and are reported as intentionally unmapped project-owned data.
- `SCENE-004`: configuration schema `2` adds initialized managed-scene records and an explicit schema `0`/`1` migration. Ordered `SceneFolderRule` entries uniformly cover primary and additional folders. Scene Apply reuses the Phase 2 source/plan hash check and recovery transaction, snapshots Addressables state plus complete Build Settings and configuration JSON, updates Build Settings once, dirties the config once, saves at the transaction boundary, and retains the snapshot if rollback or cleanup is incomplete. Configuration assets invalidated by an import are rebound by their stored GUID before stale-plan validation.

The old `ScenesListMapper` mutation body, scene-catalog editor, numeric runtime state, scene catalog types, and bundled numeric-state/catalog assets were removed. `ScenesListConfig` remains only as an obsolete, inert MonoScript-GUID migration carrier with a generic object reference and initialized additional-folder array; removing that carrier before migration support is retired would strand legacy folder intent. Manual Project Settings and CLI operations use the same `AddressablesAutomation.Analyze(..., AutomationScope.Scenes)` plan as automatic processing. Recovery is scope-neutral and blocks all later Apply operations while any group or scene snapshot is pending.

**Local evidence:** static repository validation and JSON/asmdef/meta checks passed. The development-host EditMode suite passed `79/79`. Fresh isolated clean-install/EditMode/removal lanes on Unity `6000.0.78f1` passed `79/79` for Addressables `2.7.6` and `79/79` for `2.9.1`; package import and removal remained inert in both lanes. Focused integration tests verify one-Apply convergence followed by an empty plan, configuration persistence/reload, stale public scene-plan rejection, recovery from an actual pending scene snapshot, preserved unrelated Build Settings, and add/rename/move/delete/mode-transition behavior.

**Hosted Phase 3 evidence:** the exact completion commit is `14b2388ac4953ba4b0cbaa236dc4b5a2723b9331`. Manual workflow dispatch occurred exactly once for that SHA. Run [`32609162009`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32609162009) used `workflow_dispatch` on `main` and completed successfully on `2026-08-23`. Addressables `2.7.6` job [`97119172438`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32609162009/job/97119172438) passed in `2m26s`; Addressables `2.9.1` job [`97119172337`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32609162009/job/97119172337) passed in `2m42s`. Both passed license preflight, lane selection, static validation, Unity compilation/EditMode tests, package-import inertness, tracked-project-state verification, and artifact upload. The authorized annotated `phase-3-verified` tag is created only after this evidence record is pushed and must peel to the exact hosted-tested completion commit above. No version tag, release, publication, or Phase 4 work is included.

### Phase 4 dependency-analysis and prefab-removal implementation record — 2026-08-23

**Status:** complete and verified. The guarded rewritten-history repair and independent fresh-clone validation are complete, and the existing Phase 3 hosted artifacts agree with the recorded `79/79` result in both compatibility lanes. This batch remained limited to `PREFAB-001`, `PREFAB-002`, `DEPS-001`, and `DEPS-002`; Phase 5 has not started.

**Active scope:** remove the project-specific prefab/interactable organizer, migration configuration, and physical asset-moving behavior; replace the duplicate-dependency implementation with a fail-closed Addressables compatibility adapter using only supported public/protected analyzer lifecycle and results; keep analysis immutable by default; require a separate confirmed Fix; report already-explicit entries without moving them; safely create or validate the destination group and required schemas; and disable Fix with an actionable diagnostic for unsupported or unverified Addressables versions.

**Issue progress:** `PREFAB-001` and `PREFAB-002` are implemented by removing the prefab/interactable organizer, its migration configuration, and every production physical-asset move path. `DEPS-001` is implemented with an exact-version-gated subclass of Addressables' built-in duplicate-dependency analyzer that consumes only `RefreshAnalysis` and protected `CheckDupeResults`; the package contains no private Addressables reflection. `DEPS-002` is implemented as an immutable planner, a separately confirmed stale-plan-safe Fix transaction, report-only handling for already-explicit entries, safe group/schema creation, and fail-closed capability diagnostics outside the verified `2.7.6` and `2.9.1` adapters.

**Local evidence:** `Tools/CI/Validate-PhaseZero.ps1 -RepositoryRoot . -ExpectedHostAddressablesVersion 2.7.6`, `git diff --check`, tracked JSON/asmdef parsing, Unity `.meta` pairing/GUID checks, removed-path checks, and targeted scans for private Addressables access and production `AssetDatabase.MoveAsset` passed. The development-host EditMode suite passed `91/91`, including all twelve Phase 4 tests. Fresh isolated clean-install/EditMode/removal lanes on the pinned Unity `6000.0.78f1` passed `91/91` for Addressables `2.7.6` and `91/91` for `2.9.1`; both package imports and removals remained inert, and all four lane logs were clean of the configured compiler, exception, serialization, and fatal-error patterns. Evidence is under ignored `artifacts/phase4-local-pinned-2.7.6` and `artifacts/phase4-local-pinned-2.9.1`.

**Hosted Phase 4 evidence:** the exact implementation commit is `13286cbc0c7f622cba3a157f8713c3f68df00b45`. Manual workflow dispatch occurred exactly once for that SHA. Run [`32625614016`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32625614016) used `workflow_dispatch` on `main` and completed successfully on `2026-08-23`. Addressables `2.7.6` job [`97160393538`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32625614016/job/97160393538) passed in `2m55s`; Addressables `2.9.1` job [`97160393605`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32625614016/job/97160393605) passed in `3m22s`. Both passed license preflight, exact lane selection, static validation, Unity compilation/EditMode tests, package-import inertness, tracked-project-state verification, and artifact upload. Downloaded XML results independently report exactly `91` passed, zero failed, zero skipped, and zero inconclusive in each lane; hosted artifacts remain ignored and uncommitted under `artifacts/phase4-hosted-run-32625614016`. The authorized annotated `phase-4-verified` tag is created only after this documentation-only evidence record is pushed and must peel to the exact hosted-tested implementation commit above. No version tag, release, publication, or Phase 5 work is included.

### Phase 5 build-pipeline implementation record — 2026-08-24

**Status:** complete and verified. Local implementation, both compatibility/install/removal gates, the corrected clean-checkout invariant, the separately authorized hosted compatibility run, and independent hosted XML verification all pass. The exact hosted-tested implementation commit is `a90278572f4ddd4a18a3814495c847bf0f4adafb`; its tree is identical to corrective implementation commit `8d238428d65800215aad427fcd84336ea7baca8a`.

**Issue progress:** `BUILD-001` replaces the content-update-only controller with explicit Full, Content Update, Editor-Compatible, and Multi-Platform requests; Full uses `AddressableAssetSettings.BuildPlayerContent` without reading prior state, while Content Update requires and fingerprints an explicit state file, validates compatibility, and runs `ContentUpdateScript.GatherModifiedEntries` before mutation. `BUILD-002` maps Android, iOS, `StandaloneWindows64`, `StandaloneOSX`, and `StandaloneLinux64` exactly and deterministically without conflating their target groups. `BUILD-003` implements the package-owned persistent job state machine, checked target support/switches, explicit post-reload Resume, cancellation, stop/continue policies, restoration, stale/malformed recovery, and archive/reset behavior. `BUILD-004` preserves every operation report and copies only fresh build-layout artifacts without deleting, renaming, or overwriting Addressables' source. `BUILD-005` removes the copied custom Play Mode builder and private platform-path access, creates deterministic Editor-Compatible receipts, validates target/settings/version/output freshness, and selects Addressables' public built-in `BuildScriptPackedPlayMode` only after confirmation.

**Shared surfaces and recovery:** the menu/window, public editor queue, and `TorProduction.Addressables.Editor.Cli.AddressablesCli.Run` consume the same immutable request, preflight, queue, recovery, result, report, and receipt contracts. UI and CLI expose analyze, all four start kinds, Resume, Cancel, Restore Original Target, Abandon/Reset, receipt validation, and confirmed existing-build selection. Package startup reads only the package-owned current-job slot and opens recovery only when one exists; it never resumes mutation. Reset clears only the three exact legacy package `SessionState` keys. Operation state records the job/kind/stage, full/pending/completed exact queue, original/active target, state input and hash, settings/version/request identities, report/receipt paths, cancellation/failure state, and timestamps. A failed restoration remains actionable at `Library/TorProduction.Addressables/BuildJobs/current.json`.

**Compatibility implementation:** locally installed sources for Addressables `2.7.6` and `2.9.1` were inspected before selecting calls. Both lanes expose the same required public full-build, content-update, restriction-analysis, content-state load/path, data-builder, packed-play, target-support, and target-switch APIs, so no speculative reflection adapter was added. The adapter fails closed outside exact versions `2.7.6` and `2.9.1`. Production scans contain no private Addressables reflection and no reachable `EditorPlaymodeBuildScript`, `ReportUpdater`, or legacy `TargetPlatform` execution path.

**Tests added:** focused pure/simulated coverage proves Full has no state-file dependency; Content Update blocks missing, absent, invalid, incompatible, and restriction-failing input before mutation; all target/editor mappings; unsupported modules; rejected switches; explicit domain-reload continuation; stale/malformed recovery and reset; restoration after success, exception, and cancellation; retained restoration failure; stop/continue policies; deterministic non-conflated ordering; source-preserving layout copies and missing/stale layouts; deterministic receipt freshness/invalidation; Editor-Compatible receipt creation; immutable Analyze; CLI success/blocking failure; inert startup; and absence of private reflection/legacy execution. The isolated integration fixture performs real public full and content-update builds against temporary Addressables settings/content and removes the fixture afterward without building owner content.

**Exact local evidence:** `git diff --check` and `Tools/CI/Validate-PhaseZero.ps1 -RepositoryRoot . -ExpectedHostAddressablesVersion 2.7.6` pass. All tracked JSON/asmdefs parse; Unity asset/meta pairing and GUID uniqueness pass; the staged current-tree and all six repository refs/727 reachable objects pass the exhaustive prohibited-token guard; and production build sources contain no private Addressables reflection or reachable legacy build entry point. The Unity `6000.0.78f1` development-host EditMode suite passed `125/125`, zero failed, skipped, or inconclusive, with no tracked host/Addressables changes. `Tools/CI/Test-CleanInstall.ps1` passed isolated clean-install/EditMode/removal lanes on Addressables `2.7.6` and `2.9.1`; each reports exactly `125/125`, zero failed, skipped, or inconclusive, executes the real full/update integration fixture, remains inert after import and package removal, and deletes its temporary project. Ignored local evidence is under `artifacts/phase5-local-2.7.6` and `artifacts/phase5-local-2.9.1`. The final development-host check found no tracked changes, generated Addressables settings, current package recovery job, or temporary fixture.

**First hosted attempt and narrow correction:** implementation commit `a0ecbf7a61ebfe742ec7a8f184f0e67b1f672f75` was pushed normally and triggered no workflow. Exactly one paid workflow was then manually dispatched: run [`32668602049`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32668602049), event `workflow_dispatch`, exact head SHA `a0ecbf7a61ebfe742ec7a8f184f0e67b1f672f75`. Both Addressables jobs passed checkout, license preflight, and lane selection, then failed the static invariant step before Unity execution because `Editor/BuildProcess/CustomBuildScripts.meta` was orphaned in a clean Linux checkout. Windows local validation had retained the now-empty directory physically and therefore masked the cross-platform clean-clone condition. No XML test results were produced, no tag was created, and no second paid run was automatically dispatched. The narrow Phase 5 correction removes only the obsolete empty `BuildProcess`/`CustomBuildScripts` folder metas and local empty directories left by retiring `EditorPlaymodeBuildScript`; it does not alter production behavior or enter Phase 6 cleanup.

**Hosted Phase 5 evidence:** after separate user authorization, run [`32681584182`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32681584182) used `workflow_dispatch` on `main` with exact head SHA `a90278572f4ddd4a18a3814495c847bf0f4adafb` and completed successfully on `2026-08-24`. Addressables `2.7.6` job [`97299088355`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32681584182/job/97299088355) and Addressables `2.9.1` job [`97299088509`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32681584182/job/97299088509) both passed license preflight, exact lane selection, clean-checkout static validation, Unity `6000.0.78f1` compilation/EditMode tests, package-import inertness, tracked-project-state verification, and artifact upload. Downloaded artifacts were independently verified under ignored `artifacts/phase5-hosted-run-32681584182`: each authoritative XML reports exactly `125` total and passed, zero failed, zero skipped, and zero inconclusive. The annotated `phase-5-verified` tag must target exact hosted-tested commit `a90278572f4ddd4a18a3814495c847bf0f4adafb`, not this later documentation-only evidence commit.

**Scope boundary:** Phase 6 package layout, namespace, assembly, runtime API, and sample cleanup did not begin. No release/publication behavior, `v*` tag, GitHub Release, package publication, or new repository is included.

### Phase 6 package-layout and API-cleanup implementation record — 2026-08-24

**Status:** complete and verified. Local implementation, development-host/player compilation, both package-without-samples lanes, both real UPM sample import/removal lanes, the single authorized hosted compatibility run, and independent hosted XML verification all pass. The exact hosted-tested implementation commit is `bf147de69b1bb9f2afb4ca76450027056e4682b4`. The batch is limited to `API-001` through `API-004`, `PKG-001`, `PKG-002`, `TEST-002`, and the Phase 6-owned template/dead-test portion of `TEST-001`; Phase 7 remains unstarted.

**Assembly and namespace decision:** before this phase, production compiled as runtime `TorProduction.AddressablesToolpack`, editor `TorProduction.AddressablesService.Editor`, and menu `TorProduction.AddressablesToolpack.Editor.Menu`, while package-root `Samples` also compiled as `TorProduction.AddressablesToolpack.Samples`. The Menu boundary existed only for historical internal access and sample dependencies, so it was merged. After this phase, production is exactly one editor-only assembly, `TorProduction.Addressables.Editor`, with root namespace `TorProduction.Addressables.Editor` and only the Addressables runtime/editor assembly references. There is no package Runtime or sample assembly. `TorProduction.Addressables.Editor.Tests` explicitly references production, Addressables runtime/editor, and Unity's test runners; NUnit remains its test-only precompiled reference. Production retains exactly one friend, `TorProduction.Addressables.Editor.Tests`, because integration tests exercise internal planners and adapters without widening implementation types. Production has no dependency on Samples, tests, Foundation, or project-specific runtime types.

**Serialized migration:** `AddressablesAutomationConfig` retained its existing MonoScript GUID and now carries Unity `MovedFrom` metadata for source assembly `TorProduction.AddressablesService.Editor`. `Tests/Editor/Fixtures/AddressablesAutomationConfigAssemblyMigration.asset` is a real serialized schema-3 fixture whose retained group rule, labels, and source/destination GUID data load through the renamed assembly and namespace. Every deleted serialized type's MonoScript GUID was scanned across package and host YAML before deletion; the Phase 6 tests keep the removed-GUID set under assertion. Retained sample scene/folder GUIDs were preserved during their move. New sample configuration and genuinely new folders have new unique GUIDs.

**Runtime/dead API disposition:** `API-001` through `API-004` are complete. The runtime assembly, `ITemplate`, `IObjectTemplate`, `ObjectTemplate`, `InteractableFactoryId`, `SerializableDictionary`, `SceneField` and its drawer, read-only attribute/drawer, RuntimeExample, EditorExample, and unrelated runtime/editor utilities were removed after code/YAML/asmdef reference checks. No compatibility shim was added. Obsolete update-all/config/template carriers and empty placeholder classes were removed; legacy configuration preview continues to recognize historical fields and script GUIDs without retaining those public types. Retained public editor contracts are recorded deterministically in `Documentation~/API_SURFACE.txt`; `Documentation~/PHASE_6_BREAKING_CHANGES.md` maps each removed or renamed public API to its exact removal reason, replacement, or supported migration path. EditMode validation regenerates and compares the API/assembly snapshot so unreviewed drift fails.

**Sample/package disposition:** `PKG-001` and `PKG-002` are complete. The compiled package-root `Samples` tree was replaced by optional `Samples~/BasicSetup`. `package.json` declares display name `Basic Setup`, the concise editor-workflow description, and path `Samples~/BasicSetup`; package version remains `0.1.0-preview.1`. The curated sample contains only a schema-3 `AddressablesAutomationConfig` and the retained scene. It has no script/asmdef, legacy configuration asset, application-state asset, template, or production dependency. The real Unity Package Manager sample API imports it at the standard project path with preserved scene/folder GUIDs and a new configuration GUID. Exact-path sample removal preserves a hashed unrelated asset and meta byte-for-byte; later package removal remains inert.

**Tests and exact local evidence:** `TEST-002` and the Phase 6-owned portion of `TEST-001` are complete; Phase 7's broader test/documentation/CI work remains open. The new package-layout suite asserts the exact production/test graph and references, one necessary friend, editor-only player boundary, Tor Production namespaces, NUnit isolation, absence of stale code namespaces and removed types/GUIDs, migration-fixture load, manifest/sample contents, script-GUID resolution, imported-scene missing-script count, player-script compilation, and deterministic API surface. Unity `6000.0.78f1` development-host EditMode passed `133/133`, zero failed, skipped, or inconclusive, including clean Standalone Windows player-script compilation with no Tor Production player assembly. Fresh disposable Addressables `2.7.6` and `2.9.1` lanes each passed `133/133` with `Samples~` physically absent, package import inertness, and package removal inertness. Separate fresh lanes for both versions each passed `133/133` after real UPM `Basic Setup` import and compilation, exact sample removal, unrelated-state hash preservation, and package removal. All lane logs are clean of the configured compiler/import/exception/fatal patterns.

`git diff --check`, tracked package/host JSON and all asmdef parsing, PowerShell parsing, `Validate-PhaseZero.ps1`, Unity asset/meta pairing, GUID uniqueness, assembly/API snapshot checks, removed type/GUID scans, production NUnit/Foundation/sample dependency scans, stale StansAssets code-namespace scan, and the current-tree prohibited-token guard pass. The final host check found no generated Addressables settings, temporary integration fixture, package recovery job, player output, imported sample, disposable project, host artifact, or tracked host-state change. Ignored evidence is under `artifacts/phase6-*`.

**Hosted Phase 6 evidence:** remote `main` still equaled `e39b34a8f789ffb46ea64776d6960044637d6997` immediately before the candidate push. The normal branch push advanced it to `bf147de69b1bb9f2afb4ca76450027056e4682b4` and triggered no workflow: the inventory remained the same ten runs, latest `32681584182`. Exact-candidate checks before push, immediately after push, and again after propagation all found zero runs, so exactly one manual dispatch was issued. Run [`32689916114`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32689916114) used `workflow_dispatch`, exact head SHA `bf147de69b1bb9f2afb4ca76450027056e4682b4`, and completed successfully on `2026-08-24`. Addressables `2.7.6` job [`97321645273`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32689916114/job/97321645273) passed in `3m04s`; Addressables `2.9.1` job [`97321645395`](https://github.com/Yurii-Tor/torproduction.addressables/actions/runs/32689916114/job/97321645395) passed in `2m56s`. Both passed clean checkout, license preflight, exact lane selection, static validation, Unity compilation/EditMode tests, package-import inertness, tracked-project-state verification, and artifact upload. Downloaded artifacts `9506953830` and `9506951197` were independently verified under ignored `artifacts/phase6-hosted-run-32689916114`: each authoritative XML reports exactly `133` total and passed, zero failed, zero skipped, and zero inconclusive; both hosted logs are clean of the configured failure patterns. The annotated `phase-6-verified` tag is created only after this documentation-only evidence record is pushed and must target the exact hosted-tested implementation commit above, not the evidence commit.

**Scope boundary:** no Phase 7 documentation breadth, CI/PVS/release automation, legal decision, license/notice change, package-version bump, `v*` tag, GitHub Release, publication, release-workflow change, or new repository is included. Existing legal attribution remains untouched. The unresolved ownership/relicensing and required-attribution confirmation remains a Phase 7/legal blocker.

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
  - Latest pinned Unity 6000.0 LTS patch + latest candidate Addressables 2.x as a separate manual/non-blocking experimental lane until explicitly verified and promoted. No recurring paid schedule is enabled without authorization.
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
| `CI-001` Critical | `.github/workflows/*` | No Unity compilation, tests, package validation, or clean-install test | Pinned Unity CI and reusable scripts | Manual dispatch executes both pinned compatibility lanes; any future release-condition trigger requires separate authorization; duplicate runs for the same ref are cancelled |
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
  -PackagePath "$repoRoot\com.torproduction.addressables" `
  -ArtifactsPath "$repoRoot\artifacts\package-validation"

pwsh "$repoRoot\Tools\CI\New-PackageArchive.ps1" `
  -PackagePath "$repoRoot\com.torproduction.addressables" `
  -ArtifactsPath "$repoRoot\artifacts\archive"
```

Inspect the archive for production assemblies, docs, `Samples~`, tests as intended, and absence of `Library`, generated reports, host assets, project settings, credentials, or unrelated vendor packages.

Before release:

- Install from local path.
- Install the packed archive as a local `.tgz` dependency in a disposable project.
- Install from `https://github.com/Tor-Production/torproduction.addressables.git?path=/com.torproduction.addressables#<signed-tag>` only after the signed release tag is separately authorized.
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
