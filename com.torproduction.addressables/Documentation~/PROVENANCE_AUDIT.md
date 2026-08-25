# Provenance and licensing audit

Status: factual Phase 7A audit only. This is not legal advice and does not resolve ownership, permission, relicensing, or attribution. Potentially required notices have been preserved.

## Verified repository facts

- The current reachable Git history begins at commit `728f17a84a80ec6922213a16618fd614331b68de` (2026-08-20). Its Git author/committer is Yurii Tor. A rewritten repository history records who committed this baseline; it does not prove who originally authored or owned every imported file.
- `package.json` identifies the package author as Tor Production and the repository as `Yurii-Tor/torproduction.addressables`. This is current product metadata, not evidence of an IP transfer.
- The retained `LICENSE.md` is the MIT text and currently says `Copyright (c) 2020 Stan's Assets`. Phase 7A does not remove, replace, or reinterpret that statement.
- `Third Party Notices.md` explicitly says provenance and attribution are unresolved and must be confirmed before public release.
- Current tracked production C# is substantially changed across Phases 1-6, but rewritten code and current Git authorship do not by themselves establish ownership of its design, retained portions, or earlier sources.
- `Samples~/BasicSetup/Scenes/SampleScene.unity` and its `.meta` retain the initial baseline blobs/GUID. Numerous retained `.meta` files likewise preserve asset identity. Preservation was required for serialized-reference safety and is not a provenance conclusion.
- Retained legacy identity/configuration carriers that require source confirmation include `ConfigsEnum.cs`, `ProjectConfigData.cs`, `ProjectConfigPathsManager.cs`, `ScenesListMapper.cs`, `BuildController.cs`, `TargetPlatform.cs`, `DependencyResolverMenu.cs`, `UpdateGroupsMenu.cs`, `CustomCheckBundleDupeDependencies.cs`, and `AssemblyInfo.cs`. Some are compatibility shims or extensively rewritten; that does not eliminate the need to identify the original source/license of retained portions.
- The initial baseline contained `EditorPlaymodeBuildScript.cs`, which closely followed Unity Addressables' packed Play Mode builder and used a private Addressables path API. Phase 5 deleted it completely and the shipped package now selects Unity's built-in public builder. Its former presence still requires source/license review of repository history, even though it is not in current package content.
- Current-tree and repository-owned-ref checks found no retained former-owner or former-company name, URL, email, namespace, or package identifier. Earlier history was sanitized and an external backup bundle was retained outside this repository. Absence from the current tree is not proof that a former employer or contractor had no ownership interest.
- Exact public-code searches performed during Phase 7A did not identify a public source for the specific legacy identifiers reviewed. Failure to find a public match is not evidence of original authorship or permission.

## Current notices and statements

| Location | Current statement | What it establishes |
| --- | --- | --- |
| `package.json` | Author: Tor Production; license field: MIT | Intended package metadata only |
| `LICENSE.md` | MIT; copyright Stan's Assets, 2020 | A retained copyright/license notice that must not be removed without authority |
| `Third Party Notices.md` | Provenance/attribution unresolved | Release blocker; no complete third-party inventory yet |
| Git history | Current commits attributed to Yurii Tor | Repository commit authorship, not necessarily underlying IP ownership |

No contributor agreement, IP assignment, work-for-hire agreement, employer waiver, contractor release, upstream source inventory, or relicensing permission is currently recorded in the repository.

## Assumptions that must not be used

- Do not assume Tor Production owns code merely because the repository, package name, or current commits use that brand.
- Do not assume the Stan's Assets MIT notice covers every file, grants relicensing authority, or may be replaced.
- Do not assume an employer or contractor has no claim because its branding was removed.
- Do not assume extensive rewrites erase copyright in retained expression, structure, assets, scene data, or compatibility shims.
- Do not assume Unity package code can be copied under the same terms as using Unity's public APIs. Identify the exact upstream file/version and applicable Unity license if historical distribution matters.
- Do not assume a public-code search is a complete provenance search.

## Decisions and evidence required from the owner/legal reviewer

1. Who originally authored each initial-baseline code and asset family, including the retained scene/GUID assets and the listed legacy identity/configuration carriers?
2. Was any work created while the author was employed by, contracted to, or using confidential material from Stan's Assets, a former prospective company, another former employer, a client, or a contractor? Provide the relevant employment/contract/IP-assignment or written release terms.
3. Did Stan's Assets own or license the baseline package? What exact files and versions did its 2020 MIT notice cover, and must that notice remain verbatim in redistributed source/binaries?
4. Did a former prospective company contribute, commission, own, or license any retained code, assets, architecture, documentation, or identifiers? If so, what written redistribution and attribution terms apply after rebranding?
5. For every third-party or upstream source, identify repository/package/file, exact version or commit, incorporated files/portions, applicable license, copyright holder, modification status, and required notice text.
6. For the deleted copied-looking Addressables Play Mode implementation, identify the exact Unity Addressables upstream version/file and confirm whether retaining it in reachable or archived history is permitted or whether an authorized history/legal remediation is required.
7. Are there signed contributor, contractor, work-for-hire, or IP-assignment agreements covering all non-owner contributions? Identify any contributor whose permission is still missing.
8. Does Tor Production have written authority to redistribute and relicense all shipped content? If yes, choose the approved licensing outcome: retain MIT with the existing notice, retain that notice and add a Tor Production modifications notice, adopt another license, use proprietary terms, or use a dual-license structure.
9. What exact copyright line(s), attribution acknowledgements, source offers/links, and third-party notice text must ship in `LICENSE.md`, `Third Party Notices.md`, source files, documentation, archives, and release notes?
10. May the retained `SampleScene.unity`, configuration/scene GUID identities, compatibility shims, and rewritten legacy carriers ship publicly, or must any be replaced after serialized-reference impact is reviewed?

Phase 7 must remain incomplete until the owner records answers/evidence in `ImplementationPlan.md`, the approved notice/license changes are applied without assumption, and the resulting candidate is revalidated.
