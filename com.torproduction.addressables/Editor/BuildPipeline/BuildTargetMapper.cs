using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	internal static class BuildTargetMapper {
		private static readonly ContentBuildPlatform[] s_canonicalOrder = {
			ContentBuildPlatform.Android,
			ContentBuildPlatform.iOS,
			ContentBuildPlatform.Windows,
			ContentBuildPlatform.macOS,
			ContentBuildPlatform.Linux
		};

		internal static bool TryMap(ContentBuildPlatform platform, out BuildTarget target) {
			switch (platform) {
				case ContentBuildPlatform.Android:
					target = BuildTarget.Android;
					return true;
				case ContentBuildPlatform.iOS:
					target = BuildTarget.iOS;
					return true;
				case ContentBuildPlatform.Windows:
					target = BuildTarget.StandaloneWindows64;
					return true;
				case ContentBuildPlatform.macOS:
					target = BuildTarget.StandaloneOSX;
					return true;
				case ContentBuildPlatform.Linux:
					target = BuildTarget.StandaloneLinux64;
					return true;
				default:
					target = BuildTarget.NoTarget;
					return false;
			}
		}

		internal static bool TryMapEditor(RuntimePlatform editorPlatform, out BuildTarget target) {
			switch (editorPlatform) {
				case RuntimePlatform.WindowsEditor:
					target = BuildTarget.StandaloneWindows64;
					return true;
				case RuntimePlatform.OSXEditor:
					target = BuildTarget.StandaloneOSX;
					return true;
				case RuntimePlatform.LinuxEditor:
					target = BuildTarget.StandaloneLinux64;
					return true;
				default:
					target = BuildTarget.NoTarget;
					return false;
			}
		}

		internal static bool TryMap(BuildTarget target, out ContentBuildPlatform platform) {
			foreach (var candidate in s_canonicalOrder) {
				if (TryMap(candidate, out var mapped) && mapped == target) {
					platform = candidate;
					return true;
				}
			}

			platform = default;
			return false;
		}

		internal static IReadOnlyList<BuildTarget> Optimize(
			IEnumerable<ContentBuildPlatform> requested,
			BuildTarget activeTarget) {
			var set = new HashSet<ContentBuildPlatform>(requested ?? Array.Empty<ContentBuildPlatform>());
			var ordered = s_canonicalOrder.Where(set.Contains)
				.Select(platform => {
					TryMap(platform, out var target);
					return target;
				})
				.ToList();

			var activeIndex = ordered.IndexOf(activeTarget);
			if (activeIndex > 0) {
				ordered.RemoveAt(activeIndex);
				ordered.Insert(0, activeTarget);
			}

			return ordered.AsReadOnly();
		}

		internal static string PlatformSubfolder(BuildTarget target) {
			switch (target) {
				case BuildTarget.Android:
					return "Android";
				case BuildTarget.iOS:
					return "iOS";
				case BuildTarget.StandaloneWindows64:
					return "Windows";
				case BuildTarget.StandaloneOSX:
					return "OSX";
				case BuildTarget.StandaloneLinux64:
					return "Linux";
				default:
					throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported Addressables build target.");
			}
		}
	}
}
