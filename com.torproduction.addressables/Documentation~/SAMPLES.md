# Basic Setup sample

Import **Basic Setup** from the package's Samples tab in Unity Package Manager. Unity copies it to:

```text
Assets/Samples/Tor Production Addressables Toolpack/<package-version>/Basic Setup
```

The sample contains an editor-only `AddressablesAutomationConfig` and one empty scene. Its scene rule demonstrates GUID-based folder ownership, a `Basic Setup Scenes` destination group, relative-path addressing, and the `basic-setup` label. It contains no runtime scripts or sample assembly and does not select itself automatically.

After import:

1. Open **Project Settings > Tor Production > Addressables Automation**.
2. Select the imported configuration asset.
3. Ensure the project has Addressables settings through Unity's official Groups window.
4. Run **Analyze Scenes (No Changes)**.
5. Review the proposed group/schema/entry/address/label operations before any Apply.

Sample import is inert: it does not apply the configuration, create Addressables settings, or edit Build Settings. Remove the copied sample folder explicitly when finished. Removing the UPM package does not remove an imported sample, and removing the sample does not delete unrelated project state.
