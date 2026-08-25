# Provenance and licensing audit

Status: factual Phase 7A audit plus the owner's 2026-08-25 attestations. This is not legal advice. The attestations resolve the owner's requested product decisions, but no employment/contract document or independent chain-of-title opinion was reviewed. Potentially required notices remain unchanged in this batch.

## Verified repository facts

- The current reachable Git history begins at commit `728f17a84b9a3d0438e63da5b2182e4404c59fb4` (2026-08-20). Its Git author/committer is Yurii Tor. A rewritten repository history records who committed this baseline; it does not independently prove who originally authored or owned every imported file.
- `package.json` identifies the package author as Tor Production and the repository as `Yurii-Tor/torproduction.addressables`. This is current product metadata, not evidence of an IP transfer.
- The retained `LICENSE.md` is the MIT text and currently says `Copyright (c) 2020 Stan's Assets`. Phase 7A does not remove, replace, or reinterpret that statement.
- `Third Party Notices.md` explicitly says provenance and attribution are unresolved and must be confirmed before public release.
- The public [Stan's Assets Unity Package Sample](https://github.com/StansAssets/Unity-Package-Sample) identifies itself as a GitHub UPM repository template and is distributed under MIT. The audited upstream checkout was commit `b59b0c9c7886d88f85b0449f338eabe9388d3831`; its MIT file carries `Copyright (c) 2020 Stan's Assets`.
- Exact Git-blob comparison found 24 template-identical paths in the rewritten initial baseline. In the current tree, four repository paths remain exact matches: `.github/pull_request_template.md`, the development host's `Assets/Scenes.meta` and `Assets/Scenes/SampleScene.unity.meta`, and package `LICENSE.md`. Within current package content, only `LICENSE.md` is byte-identical to the audited template. Exact identity is a lower bound on template use, not a complete derivation analysis.
- Current tracked production C# is substantially changed across Phases 1-6, but rewritten code and current Git authorship do not by themselves establish ownership of its design, retained portions, or earlier sources.
- `Samples~/BasicSetup/Scenes/SampleScene.unity` and its `.meta` retain the initial baseline blobs/GUID. Numerous retained `.meta` files likewise preserve asset identity. Preservation was required for serialized-reference safety and is not a provenance conclusion.
- Retained legacy identity/configuration carriers that require source confirmation include `ConfigsEnum.cs`, `ProjectConfigData.cs`, `ProjectConfigPathsManager.cs`, `ScenesListMapper.cs`, `BuildController.cs`, `TargetPlatform.cs`, `DependencyResolverMenu.cs`, `UpdateGroupsMenu.cs`, `CustomCheckBundleDupeDependencies.cs`, and `AssemblyInfo.cs`. Some are compatibility shims or extensively rewritten; that does not eliminate the need to identify the original source/license of retained portions.
- The initial baseline contained `EditorPlaymodeBuildScript.cs`, which closely followed Unity Addressables' packed Play Mode builder and used a private Addressables path API. Phase 5 deleted it completely and the shipped package now selects Unity's built-in public builder. Its former presence still requires source/license review of repository history, even though it is not in current package content.
- Current-tree and repository-owned-ref checks found no retained former-owner or former-company name, URL, email, namespace, or package identifier. Earlier history was sanitized and an external backup bundle was retained outside this repository.
- Exact public-code searches performed during Phase 7A did not identify a public source for the specific legacy identifiers reviewed. Failure to find a public match is not evidence of original authorship or permission.

## Owner attestations and product decisions

The owner, Yurii Tor, recorded the following statements on 2026-08-25. They are owner attestations, not independently verified contract evidence:

- Yurii Tor designed and implemented the product alone and claims the entire product idea and implementation. He identified no other human contributor, employer, client, or contractor contribution. Codex was used as software assistance under his direction; it is not recorded as another human contributor.
- Stan's Assets supplied only the public GitHub UPM template identified above; Yurii did not work for Stan's Assets and does not claim Stan authored the product implementation.
- Yurii states that no former prospective company received any part of the idea or implementation or contributed, commissioned, owned, or licensed this work.
- `Tor Production` is Yurii's application pseudonym/brand, not a separate contributor or former owner.
- Yurii confirms the retained sample scene, configuration and scene GUID identities, compatibility shims, and rewritten legacy carriers may ship publicly.
- Yurii chooses continued public distribution under MIT. He requested a minimal notice set, with Addressables `2.7.6` as the minimum and `2.9.1` retained as a verified lane; future compatibility work should include `4.0.1` when that lane is deliberately prepared and tested.

## Current notices and statements

| Location | Current statement | What it establishes |
| --- | --- | --- |
| `package.json` | Author: Tor Production; license field: MIT | Intended package metadata only |
| `LICENSE.md` | MIT; copyright Stan's Assets, 2020 | A retained copyright/license notice that must not be removed without authority |
| `Third Party Notices.md` | Provenance/attribution unresolved | Preserved pending the separately authorized notice edit described below |
| Git history | Current commits attributed to Yurii Tor | Repository commit authorship, not necessarily underlying IP ownership |

No contributor agreement, IP assignment, work-for-hire agreement, employer waiver, contractor release, or independent legal opinion is recorded in the repository. The owner states those instruments are unnecessary because no other human created or received the work. The repository records that statement without representing that any underlying contract was reviewed.

## Assumptions that must not be used

- Do not infer ownership merely from branding or Git authorship; rely on the separately identified owner attestation for the owner's claim.
- Do not assume the Stan's Assets MIT notice covers every file, grants relicensing authority, or may be replaced.
- Do not treat the absence of former-company branding as contract evidence; the no-disclosure/no-contribution account is an owner attestation.
- Do not assume extensive rewrites erase copyright in retained expression, structure, assets, scene data, or compatibility shims.
- Do not assume Unity package code can be copied under the same terms as using Unity's public APIs. Identify the exact upstream file/version and applicable Unity license if historical distribution matters.
- Do not assume a public-code search is a complete provenance search.

## Minimal MIT notice recommendation

The public template and exact-blob evidence support a conservative, minimal result: preserve the Stan's Assets 2020 MIT copyright and permission notice for template-derived material, add the product author's notice, and identify the template once in `Third Party Notices.md`. No per-source-file headers or broader attribution are indicated by the current evidence.

The proposed license header is:

```text
MIT License

Copyright (c) 2020 Stan's Assets
Copyright (c) 2026 Yurii Tor (Tor Production)
```

The proposed third-party entry is: “This repository used Stan's Assets' Unity Package Sample at commit `b59b0c9c7886d88f85b0449f338eabe9388d3831` as a UPM repository template. The template is MIT-licensed, Copyright (c) 2020 Stan's Assets; its notice is retained in `LICENSE.md`.”

These are recorded release-candidate edits, not changes made by Phase 7A. Applying them remains outside this batch's authorization and must be followed by archive/PVS/local validation and the separately authorized hosted verification.

## Remaining review boundary

All ten owner questions have answers recorded above. There is no known missing human-contributor permission and no remaining ownership choice requested from the owner. What remains is procedural:

1. separately authorize applying the proposed `LICENSE.md` and `Third Party Notices.md` text;
2. obtain independent legal review only if the owner wants contract/chain-of-title assurance beyond his recorded attestations;
3. revalidate the exact notice-updated candidate before hosted verification or release.

Phase 7 remains incomplete until the authorized notice edits and final candidate validation are complete. No notice was removed and no license file was changed in this batch.
