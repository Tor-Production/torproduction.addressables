using TorProduction.Addressables.Editor;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class BuildController {
		internal static ContentBuildPreflight Analyze(ContentBuildRequest request) =>
			AddressablesBuildQueue.Analyze(request);

		internal static ContentBuildResult Start(ContentBuildRequest request) =>
			AddressablesBuildQueue.Enqueue(request);

		internal static ContentBuildResult Resume() => AddressablesBuildQueue.Resume();
		internal static ContentBuildResult Cancel() => AddressablesBuildQueue.Cancel();
		internal static ContentBuildResult Restore() => AddressablesBuildQueue.RestoreOriginalTarget();
		internal static ContentBuildResult AbandonReset() => AddressablesBuildQueue.AbandonReset();
		internal static ContentBuildRecoveryInfo InspectRecovery() => AddressablesBuildQueue.InspectRecovery();
		internal static ExistingBuildValidation ValidateExistingBuild() => AddressablesBuildQueue.ValidateExistingBuild();
		internal static ExistingBuildValidation SelectExistingBuild(bool confirmed) =>
			AddressablesBuildQueue.SelectExistingBuild(confirmed);
	}
}
