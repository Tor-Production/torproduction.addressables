# Documentation context architecture

## Context-debt audit

The pre-migration audit at base commit `82f4c143f9ebd20608d3cfc70f6781c057c9df87` found reorganization required:

- 28 tracked Markdown files totaling 268,051 bytes;
- 4,538 bytes of automatically discovered `AGENTS.md` instructions;
- a 193,726-byte `ImplementationPlan.md` required before every code change;
- 198,264 bytes of routine mandatory context before task-specific source inspection;
- nine Markdown documents referencing the plan;
- five current-looking assertions that made the plan mandatory or authoritative;
- current Phase 7/Preview 3 state mixed with baseline assessments, active-phase checklists, planned decisions, and execution logs.

The historical plan exceeded the audit's 50 KiB reorganization threshold and contained intentionally dated statements that conflicted with its final status. Repeated current-state summaries also made authority difficult to determine without broad rescans.

## Migration provenance

`ImplementationPlan.md` remains at its original path for inbound links and historical traceability. Before this migration it was Git blob `1d4b1258ca87dc9fa5f035a8a66ed03556f2db00` and 193,726 bytes at the base commit above. The migration prepends only a supersession banner; its original body is retained.

The released package subtree, Unity host, workflows, release artifacts, checksum files, tags, GitHub Releases, and publication state are outside the documentation migration and must remain unchanged.

## Routing model

| Question | Current authority |
| --- | --- |
| Stable repository rules and mandatory routing | [AGENTS.md](../AGENTS.md) |
| Version, lifecycle, compatibility, active work, and release boundaries | [PROJECT_STATE.md](PROJECT_STATE.md) |
| Implemented decisions | [DECISIONS.md](DECISIONS.md) plus linked released source/tests |
| Development and validation | [DEVELOPMENT.md](DEVELOPMENT.md) plus `Tools/CI/` and workflows |
| Release policy and evidence | [RELEASES.md](RELEASES.md), Git refs, workflows, and committed checksums |
| Provenance, license notice, and attribution decision | [PROVENANCE_AUDIT.md](../PROVENANCE_AUDIT.md) and released legal files |
| Package-user behavior | [Package documentation index](../com.torproduction.addressables/Documentation~/com.torproduction.addressables.md) |
| Historical phase, batch, rationale, and execution evidence | Archived [ImplementationPlan.md](../ImplementationPlan.md), on demand |

Routine future work loads `AGENTS.md` and `PROJECT_STATE.md`, then the single task-specific route. Source, tests, manifests, workflows, Git refs, and committed artifacts remain stronger evidence than narrative summaries.

## Pre-migration Markdown inventory

| Path | Audience and classification | Disposition |
| --- | --- | --- |
| `.agents/CODEX_WORKFLOW.md` | Internal Codex setup and fallback procedure | Retain unchanged; load only for plugin setup/fallback |
| `.github/pull_request_template.md` | Development and review procedure | Retain unchanged |
| `AGENTS.md` | Mandatory stable instructions | Rewrite as concise rules and routing |
| `CONTRIBUTING.md` | Repository contributor entry point | Route to current state and development procedure |
| `ImplementationPlan.md` | Historical decisions, phase records, and release evidence | Retain body with supersession banner; on demand only |
| `PROVENANCE_AUDIT.md` | Current provenance and notice decision | Clarify current authority |
| `README.md` | Repository landing page | Replace stale plan authority with concise routing |
| `com.torproduction.addressables/CHANGELOG.md` | Shipped user and release history | Retain unchanged as package/release evidence |
| `com.torproduction.addressables/Documentation~/BUILD_PIPELINE.md` | Shipped user workflow | Retain unchanged |
| `com.torproduction.addressables/Documentation~/CLI.md` | Shipped user workflow | Retain unchanged |
| `com.torproduction.addressables/Documentation~/COMPATIBILITY.md` | Shipped compatibility snapshot | Retain unchanged; live status routes through project state |
| `com.torproduction.addressables/Documentation~/CONFIGURATION.md` | Shipped user workflow | Retain unchanged |
| `com.torproduction.addressables/Documentation~/CONTRIBUTING.md` | Shipped package-contributor guidance | Retain unchanged as released snapshot |
| `com.torproduction.addressables/Documentation~/DEPENDENCY_ANALYSIS.md` | Shipped user workflow | Retain unchanged |
| `com.torproduction.addressables/Documentation~/GROUP_SYNCHRONIZATION.md` | Shipped user workflow | Retain unchanged |
| `com.torproduction.addressables/Documentation~/INSTALLATION.md` | Shipped user workflow | Retain unchanged |
| `com.torproduction.addressables/Documentation~/LIMITATIONS.md` | Shipped user limitations | Retain unchanged |
| `com.torproduction.addressables/Documentation~/PHASE_6_BREAKING_CHANGES.md` | Shipped historical compatibility record | Retain unchanged |
| `com.torproduction.addressables/Documentation~/RELEASE_PROCESS.md` | Shipped release-procedure snapshot | Retain unchanged; not live repository status |
| `com.torproduction.addressables/Documentation~/RELEASE_READINESS.md` | Shipped release-evidence snapshot | Retain unchanged; not live repository authority |
| `com.torproduction.addressables/Documentation~/SAFETY.md` | Shipped safety contract | Retain unchanged |
| `com.torproduction.addressables/Documentation~/SAMPLES.md` | Shipped user workflow | Retain unchanged |
| `com.torproduction.addressables/Documentation~/SCENE_SYNCHRONIZATION.md` | Shipped user workflow | Retain unchanged |
| `com.torproduction.addressables/Documentation~/TROUBLESHOOTING.md` | Shipped user support | Retain unchanged |
| `com.torproduction.addressables/Documentation~/com.torproduction.addressables.md` | Shipped package documentation index | Retain unchanged |
| `com.torproduction.addressables/LICENSE.md` | Distributed legal text | Retain unchanged; current legal artifact |
| `com.torproduction.addressables/README.md` | Shipped package landing page | Retain unchanged |
| `com.torproduction.addressables/Third Party Notices.md` | Distributed attribution | Retain unchanged; current legal artifact |

## Intentionally retained duplication

- Version and compatibility data appears in the manifest, changelog, released package docs, workflows, and concise project state because those artifacts serve different audiences. The manifest and verification evidence control exact claims.
- Stable safety boundaries appear in both `AGENTS.md` and shipped user documentation because one governs repository work and the other governs package operation.
- Copyright and attribution appear in the license, third-party notice, and provenance audit because distribution and engineering review require different forms of the same legal evidence.
- Historical source-of-truth wording remains inside the preserved plan body and frozen package documentation. Their archive/snapshot status is explicit; rewriting them would damage provenance or change released package content.

## Deferred reorganization

- Do not split the historical plan by phase or rewrite its dated body.
- Do not revise shipped package documentation outside a separately authorized package release task.
- Do not move release evidence, alter checksums, or change Git/GitHub release objects.
- Do not introduce an ADR directory until a new independently reviewable decision needs one; use the verified current register for existing released invariants.
- Do not adopt a code-index service unless later repository navigation demonstrates recurring structural cost that local search cannot address.

## Post-migration audit

After migration, 34 tracked Markdown files total 289,081 bytes. `AGENTS.md` is 3,885 bytes and `docs/PROJECT_STATE.md` is 2,631 bytes, for 6,516 bytes of routine mandatory context. This is below the 12 KiB watch threshold and keeps `AGENTS.md` below 6 KiB. The larger repository total reflects the new indexed documentation; historical evidence is no longer routine context. No exact token-saving claim is made from byte measurements.
