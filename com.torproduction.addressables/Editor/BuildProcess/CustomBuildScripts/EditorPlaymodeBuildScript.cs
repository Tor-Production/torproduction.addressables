using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using RuntimeAddressables = UnityEngine.AddressableAssets.Addressables;

namespace TorProduction.AddressablesToolpack.Editor {
	
	/// <summary>
	/// Uses data built by BuildScriptPacked class.  This script just sets up the correct variables and runs.
	/// </summary>
	[CreateAssetMenu(fileName = "EditorPlayMode.asset", menuName = "Addressables/Content Builders/Use Existing Editor Build (requires built groups)")]
	internal class EditorPlaymodeBuildScript : BuildScriptBase {
		/// <inheritdoc />
		public override string Name => "Use Existing Editor Build (requires built groups)";

		private bool m_dataBuilt;

		/// <inheritdoc />
		public override void ClearCachedData() {
			m_dataBuilt = false;
		}

		/// <inheritdoc />
		public override bool IsDataBuilt() {
			return m_dataBuilt;
		}

		public override bool CanBuildData<T>() {
			return typeof(T).IsAssignableFrom(typeof(AddressablesPlayModeBuildResult));
		}

		public string BuildPath {
			get {
				MethodInfo methodInfo = typeof(PlatformMappingService).
					GetMethod("GetAddressablesPlatformPathInternal", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(BuildTarget) }, null);
				var target = GetEditorStandaloneTarget(Application.platform);
				var platformResult = (string)methodInfo.Invoke(null, new object[] { target });
				
				return RuntimeAddressables.LibraryPath + RuntimeAddressables.StreamingAssetsSubFolder + "/" + platformResult;
			}
		}

		internal static BuildTarget GetEditorStandaloneTarget(RuntimePlatform editorPlatform) {
			switch (editorPlatform) {
				case RuntimePlatform.WindowsEditor:
					return BuildTarget.StandaloneWindows64;
				case RuntimePlatform.OSXEditor:
					return BuildTarget.StandaloneOSX;
				case RuntimePlatform.LinuxEditor:
					return BuildTarget.StandaloneLinux64;
				default:
					throw new PlatformNotSupportedException(
						$"Can't retrieve an existing Editor build path for platform '{editorPlatform}'.");
			}
		}

		/// <inheritdoc />
		protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput) {
			var timer = new System.Diagnostics.Stopwatch();
			timer.Start();
			var settingsPath = BuildPath + "/settings.json";
			var buildLogsPath = BuildPath + "/buildLogs.json";
			if (!File.Exists(settingsPath)) {
				IDataBuilderResult resE = new AddressablesPlayModeBuildResult() {
					Error = "Player content must be built before entering play mode with packed data.  This can be done from the Addressables window in the Build->Build Player Content menu command."
				};
				return (TResult)resE;
			}

			var rtd = JsonUtility.FromJson<ResourceManagerRuntimeData>(File.ReadAllText(settingsPath));
			if (rtd == null) {
				IDataBuilderResult resE = new AddressablesPlayModeBuildResult() {
					Error = string.Format("Unable to load initialization data from path {0}.  This can be done from the Addressables window in the Build->Build Player Content menu command.",
						settingsPath)
				};
				return (TResult)resE;
			}

			PackedPlayModeBuildLogs buildLogs = new PackedPlayModeBuildLogs();
			BuildTarget dataBuildTarget = BuildTarget.NoTarget;
			if (!Enum.TryParse(rtd.BuildTarget, out dataBuildTarget)) {
				buildLogs.RuntimeBuildLogs.Add(new PackedPlayModeBuildLogs.RuntimeBuildLog(LogType.Warning,
					$"Unable to parse build target from initialization data: '{rtd.BuildTarget}'."));
			} else if (BuildPipeline.GetBuildTargetGroup(dataBuildTarget) != BuildTargetGroup.Standalone) {
				buildLogs.RuntimeBuildLogs.Add(new PackedPlayModeBuildLogs.RuntimeBuildLog(LogType.Warning,
					$"Asset bundles built with build target {dataBuildTarget} may not be compatible with running in the Editor."));
			}

			if (buildLogs.RuntimeBuildLogs.Count > 0)
				File.WriteAllText(buildLogsPath, JsonUtility.ToJson(buildLogs));

			//TODO: detect if the data that does exist is out of date..
			var runtimeSettingsPath = BuildPath + "/settings.json";
			PlayerPrefs.SetString(RuntimeAddressables.kAddressablesRuntimeDataPath, runtimeSettingsPath);
			PlayerPrefs.SetString(RuntimeAddressables.kAddressablesRuntimeBuildLogPath, buildLogsPath);
			IDataBuilderResult res = new AddressablesPlayModeBuildResult() { OutputPath = settingsPath, Duration = timer.Elapsed.TotalSeconds };
			m_dataBuilt = true;
			return (TResult)res;
		}
	}
}
