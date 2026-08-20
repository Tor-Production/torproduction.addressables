# Temporary safety notice

This notice applies to `0.1.0-preview.1` while the production workflows are being rebuilt.

- Installing or importing the package does not create configuration files, Addressables settings, groups, labels, or Build Settings entries.
- Automatic scene synchronization is disabled.
- Group updates, Update All, dependency fixing, prefab relocation, and Addressables build commands remain visible but disabled.
- The legacy project-settings window reads configuration without creating defaults. It writes `ProjectSettings/ProjectConfig.json` only after the user explicitly selects all three legacy config assets and clicks **Save**.
- The package is not release-ready. Publishing workflows remain disabled until the later release-readiness phase and the legal checks recorded in `ImplementationPlan.md` are complete.

Do not invoke legacy controller methods through reflection or custom editor code. Their safe analyze/apply replacements are outside this implementation batch.
