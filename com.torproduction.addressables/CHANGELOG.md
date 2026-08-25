# Change Log

All notable changes to this project are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/) and the package uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

No post-preview product changes are currently recorded. Phase 7 release-readiness work does not authorize publication.

## [0.1.0-preview.1] - 2026-08-25

This preview version remains unpublished while the ownership, licensing, attribution, and final hosted-verification gates are unresolved.

### Added

- Deterministic, read-only group planning through `AddressablesAutomation.Analyze`.
- Confirmed transactional Apply with stale-plan rejection, recovery snapshots, rollback, and manual recovery.
- Relative-path addresses, assembly-qualified type filters, convergent group/address/label operations, missing group/schema planning, and sorted reports.
- Project Settings, editor-window, and CLI dry-run/apply surfaces.
- GUID-based scene synchronization for Addressable and local Build Settings modes, with managed ownership records, deterministic transitions, and opt-in deferred postprocessing.
- A static current-tree guard that rejects reintroduction of former-owner identifiers in tracked paths or contents.
- Analyze-only duplicate-dependency reporting with exact-version capability diagnostics and a separately confirmed transactional Fix.
- Full, Content Update, Editor-Compatible, and deterministic Multi-Platform Addressables content-build workflows with shared preflight, persistent recovery, reports, cancellation, and CLI entry points.
- Package-owned Editor-Compatible receipts and confirmed selection of Addressables' built-in Use Existing Build Play Mode builder.

### Changed

- Group synchronization operates only on explicit rule-owned entries and preserves unrelated labels by default.
- Legacy unique simple type names and the serialized `Lables` field migrate through explicit preview; ambiguous types remain blocking diagnostics.
- Asset-load failures now block Apply because complete convergence cannot be proven.
- Repository and package documentation now describe the implemented product and its unreleased status.
- Numeric application-state mapping and its bundled sample were retired in favor of generic scene categories and labels.
- Configuration schema 3 adds a generic duplicate-dependency destination group. Existing schema 0–2 assets require the explicit backup-first migration.

### Fixed

- Removed the active global Default Group cleanup and the filename-only, create-only group updater.
- Addressable folder entries, duplicate generated addresses, read-only/non-buildable groups, failed loads, and incompatible asset claims fail safely before mutation.
- Rollback-backend and recovery-snapshot cleanup failures return structured reports while retaining recovery evidence.
- Corrected the package repository metadata and made the Unity workflow display name phase-neutral.
- Scene rename, move, delete, duplicate-name, stale-plan, rollback, and recursion cases now converge through the shared transaction boundary.
- Removed the project-specific prefab/interactable organizer, migration configuration, physical asset-moving command, and private Addressables analyzer/path reflection.
- Replaced the legacy content-update-only build controller, target-group queue, destructive report updater, and copied existing-build Play Mode script with supported public Addressables APIs and exact-target recovery.
