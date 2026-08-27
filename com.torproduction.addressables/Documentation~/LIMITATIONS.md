# Limitations

- This package is editor-only and targets Unity 6. It does not provide a runtime API or runtime component.
- Only Unity `6000.0.78f1` with Addressables `2.7.6` and `2.9.1` has completed the Phase 6 hosted matrix. The prepared `6000.0.82f1` / `2.11.2` lane is unverified until a separately authorized manual run passes.
- Duplicate-dependency Fix is enabled only for exact `2.7.6` and `2.9.1` adapters. Other Addressables versions can report a capability blocker.
- Rules operate on explicit asset entries. Addressable folder entries that implicitly own configured descendants are blocking conflicts rather than being rewritten.
- A failed main-asset load blocks convergence. The tool does not silently skip unreadable candidates.
- Content Update requires an explicit compatible `addressables_content_state.bin`. The package does not guess a prior build or state file.
- Player target modules must already be installed. The package does not install Unity modules or change external toolchains.
- Build stages use synchronous public Addressables APIs; cancellation occurs between stages, not inside a synchronous Addressables build call.
- Existing-build Play Mode selection requires a fresh exact receipt and user confirmation. The package does not ship a custom/copied Play Mode builder.
- The package does not move source assets, reorganize prefabs, map game-specific numeric states, or recreate the removed project-specific migration tool.
- Imported samples, generated Addressables content, consumer configuration assets, and project Addressables settings are host-owned and are not deleted on package removal.
- `0.1.0-preview.3` is distributed only through its signed Git tag and GitHub pre-release. OpenUPM, npm, Unity Registry, Asset Store, and other registry publication are outside this release.
