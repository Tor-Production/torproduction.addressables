using System.Collections.Generic;
using TorProduction.Addressables.Editor;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal sealed class BuildMenuSelection {
		internal ContentBuildKind Kind = ContentBuildKind.Full;
		internal ContentBuildPlatform Target = ContentBuildPlatform.Windows;
		internal string StateFilePath = string.Empty;
		internal bool Android;
		internal bool iOS;
		internal bool Windows = true;
		internal bool macOS;
		internal bool Linux;
		internal bool ContinueOnError;

		internal ContentBuildRequest CreateRequest() {
			switch (Kind) {
				case ContentBuildKind.Full:
					return ContentBuildRequest.Full(Target);
				case ContentBuildKind.ContentUpdate:
					return ContentBuildRequest.ContentUpdate(Target, StateFilePath);
				case ContentBuildKind.EditorCompatible:
					return ContentBuildRequest.EditorCompatible();
				case ContentBuildKind.MultiPlatform:
					var platforms = new List<ContentBuildPlatform>();
					if (Android) platforms.Add(ContentBuildPlatform.Android);
					if (iOS) platforms.Add(ContentBuildPlatform.iOS);
					if (Windows) platforms.Add(ContentBuildPlatform.Windows);
					if (macOS) platforms.Add(ContentBuildPlatform.macOS);
					if (Linux) platforms.Add(ContentBuildPlatform.Linux);
					return ContentBuildRequest.MultiPlatform(
						platforms,
						ContinueOnError
							? ContentBuildFailurePolicy.ContinueOnError
							: ContentBuildFailurePolicy.StopOnFirstFailure);
				default:
					return new ContentBuildRequest(Kind, Target);
			}
		}
	}
}
