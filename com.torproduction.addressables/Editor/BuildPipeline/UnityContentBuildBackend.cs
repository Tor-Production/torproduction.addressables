using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.AddressableAssets.Initialization;
using RuntimeAddressables = UnityEngine.AddressableAssets.Addressables;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace TorProduction.Addressables.Editor {
	internal sealed class UnityContentBuildBackend : IContentBuildBackend {
		internal const string PackageRoot = "Library/TorProduction.Addressables";
		internal const string LatestEditorReceiptRelativePath =
			PackageRoot + "/BuildReceipts/editor-compatible.json";

		private AddressableAssetSettings Settings =>
			AddressableAssetSettingsDefaultObject.SettingsExists
				? AddressableAssetSettingsDefaultObject.GetSettings(false)
				: null;

		public BuildTarget ActiveTarget => EditorUserBuildSettings.activeBuildTarget;
		public RuntimePlatform EditorPlatform => Application.platform;
		public bool SettingsExist => Settings != null;
		public bool PlayerDataBuilderValid => Settings?.ActivePlayerDataBuilder != null &&
		                                      Settings.ActivePlayerDataBuilder.CanBuildData<AddressablesPlayerBuildResult>();
		public string SettingsGuid => ContentBuildIdentity.SettingsGuid(Settings);
		public string SettingsHash => ContentBuildIdentity.SettingsHash(Settings);
		public string AddressablesVersion => GetAddressablesVersion();
		public long UtcNowTicks => DateTime.UtcNow.Ticks;
		public bool IsCancellationRequested => false;

		public bool FileExists(string path) => File.Exists(path);

		public bool IsTargetSupported(BuildTargetGroup group, BuildTarget target) =>
			UnityEditor.BuildPipeline.IsBuildTargetSupported(group, target);

		public bool SwitchActiveTarget(BuildTargetGroup group, BuildTarget target) =>
			EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

		public ContentStateValidationResult ValidateContentState(string path, BuildTarget target) {
			var diagnostics = new List<ContentBuildDiagnostic>();
			if (Settings == null) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.AddressablesSettingsMissing,
					"Addressables settings disappeared during state-file validation.", target));
				return new ContentStateValidationResult(false, string.Empty, diagnostics);
			}

			AddressablesContentState state;
			try {
				state = ContentUpdateScript.LoadContentState(path);
			} catch (Exception exception) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.StateFileInvalid,
					$"The selected content-state file could not be deserialized: {exception.Message}", target));
				return new ContentStateValidationResult(false, string.Empty, diagnostics);
			}

			if (state == null) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.StateFileInvalid,
					"The selected file is not a supported Addressables content-state file.", target));
				return new ContentStateValidationResult(false, string.Empty, diagnostics);
			}

			if (string.IsNullOrEmpty(state.playerVersion) || string.IsNullOrEmpty(state.editorVersion) ||
			    state.cachedInfos == null || state.cachedBundles == null) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.StateFileInvalid,
					"The selected content-state file is incomplete (version or cached-state fields are missing).", target));
			}

			var currentRemotePath = Settings.BuildRemoteCatalog
				? Settings.RemoteCatalogLoadPath.GetValue(Settings)
				: string.Empty;
			if (!Settings.BuildRemoteCatalog || string.IsNullOrEmpty(state.remoteCatalogLoadPath) ||
			    !string.Equals(state.remoteCatalogLoadPath, currentRemotePath, StringComparison.Ordinal)) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.StateFileIncompatible,
					"The selected state requires the current settings to enable Build Remote Catalog with the exact same Remote Catalog Load Path.",
					target));
			}

			var groupGuids = new HashSet<string>(
				(Settings.groups ?? new List<AddressableAssetGroup>())
				.Where(group => group != null)
				.Select(group => group.Guid),
				StringComparer.Ordinal);
			var missingGroups = (state.cachedInfos ?? Array.Empty<CachedAssetState>())
				.Where(item => item != null && !string.IsNullOrEmpty(item.groupGuid) && !groupGuids.Contains(item.groupGuid))
				.Select(item => item.groupGuid)
				.Distinct(StringComparer.Ordinal)
				.OrderBy(item => item, StringComparer.Ordinal)
				.ToArray();
			if (missingGroups.Length > 0) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.StateFileIncompatible,
					$"The selected state references {missingGroups.Length} group GUID(s) that are absent from the current Addressables settings.",
					target));
			}

			if (!string.Equals(state.editorVersion, Application.unityVersion, StringComparison.Ordinal)) {
				diagnostics.Add(new ContentBuildDiagnostic(
					ContentBuildDiagnosticCode.StateFileIncompatible,
					ContentBuildDiagnosticSeverity.Warning,
					$"The state was created by Unity {state.editorVersion}; the current editor is {Application.unityVersion}. Addressables permits this but warns that content may be incompatible.",
					target));
			}

			diagnostics.Add(new ContentBuildDiagnostic(
				ContentBuildDiagnosticCode.StateFileIncompatible,
				ContentBuildDiagnosticSeverity.Warning,
				$"Addressables content-state files do not encode an exact BuildTarget. This request will use the explicitly preflighted target '{target}'.",
				target));

			string hash;
			try {
				hash = ContentBuildIdentity.FileHash(path);
			} catch (Exception exception) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.StateFileInvalid,
					$"The selected content-state file could not be fingerprinted: {exception.Message}", target));
				hash = string.Empty;
			}

			return new ContentStateValidationResult(
				diagnostics.All(item => item.Severity != ContentBuildDiagnosticSeverity.Error),
				hash,
				diagnostics);
		}

		public IReadOnlyList<ContentBuildDiagnostic> CheckContentUpdateRestrictions(string path, BuildTarget target) {
			try {
				var modified = ContentUpdateScript.GatherModifiedEntries(Settings, path);
				if (modified == null) {
					return new[] { Error(
						ContentBuildDiagnosticCode.ContentUpdateRestriction,
						"Addressables could not complete its content-update restriction analysis.", target) };
				}
				if (modified.Count == 0) return Array.Empty<ContentBuildDiagnostic>();

				var paths = modified.Select(item => item?.AssetPath ?? item?.guid ?? "<unknown>")
					.OrderBy(item => item, StringComparer.Ordinal)
					.Take(10);
				return new[] { Error(
					ContentBuildDiagnosticCode.ContentUpdateRestriction,
					$"Addressables found {modified.Count} modified entry or dependency restriction(s): {string.Join(", ", paths)}. Resolve them before starting the update.",
					target) };
			} catch (Exception exception) {
				return new[] { Error(
					ContentBuildDiagnosticCode.ContentUpdateRestriction,
					$"Addressables content-update restriction analysis failed: {exception.Message}", target) };
			}
		}

		public BuildExecutionOutcome BuildFull(BuildTarget target) {
			if (ActiveTarget != target) {
				return new BuildExecutionOutcome(false, $"Full build expected active target '{target}', but Unity reports '{ActiveTarget}'.");
			}
			AddressablesRuntimeProperties.ClearCachedPropertyValues();
			AddressableAssetSettings.BuildPlayerContent(out var result);
			return ConvertResult(result, false);
		}

		public BuildExecutionOutcome BuildContentUpdate(BuildTarget target, string stateFilePath) {
			if (ActiveTarget != target) {
				return new BuildExecutionOutcome(false, $"Content Update expected active target '{target}', but Unity reports '{ActiveTarget}'.");
			}
			AddressablesRuntimeProperties.ClearCachedPropertyValues();
			var result = ContentUpdateScript.BuildContentUpdate(Settings, stateFilePath);
			return ConvertResult(result, true);
		}

		public BuildLayoutCaptureResult CaptureBuildLayout(
			string operationDirectory,
			long buildStartedUtcTicks,
			BuildTarget target) {
			var candidates = new[] {
				AbsolutePath(RuntimeAddressables.LibraryPath + "buildlayout.json"),
				AbsolutePath(RuntimeAddressables.LibraryPath + "buildlayout.txt")
			};
			return BuildLayoutArtifactService.Capture(
				candidates,
				operationDirectory,
				buildStartedUtcTicks,
				target);
		}

		public BuildReceiptCreationResult CreateEditorCompatibleReceipt(
			BuildJobRecord record,
			BuildExecutionOutcome outcome,
			BuildTarget target) {
			return BuildReceiptService.Create(record, outcome, target, this);
		}

		public void WriteOperationReport(BuildJobRecord record) {
			BuildOperationReportWriter.Write(record);
		}

		internal static string GetAddressablesVersion() {
			var package = PackageManagerPackageInfo.FindForAssembly(typeof(AddressableAssetSettings).Assembly);
			return package?.version ?? string.Empty;
		}

		internal static string AbsolutePath(string path) {
			if (string.IsNullOrEmpty(path)) return string.Empty;
			return Path.GetFullPath(path).Replace('\\', '/');
		}

		private BuildExecutionOutcome ConvertResult(AddressablesPlayerBuildResult result, bool contentUpdate) {
			if (result == null) {
				return new BuildExecutionOutcome(false, "Addressables returned no build result.");
			}
			if (!string.IsNullOrEmpty(result.Error)) {
				return new BuildExecutionOutcome(false, result.Error);
			}

			var outputPath = AbsolutePath(RuntimeAddressables.BuildPath);
			var statePath = string.Empty;
			try {
				statePath = AbsolutePath(ContentUpdateScript.GetContentStateDataPath(false, Settings));
			} catch (Exception exception) {
				return new BuildExecutionOutcome(
					false,
					$"Addressables built content but the generated content-state path could not be resolved: {exception.Message}",
					outputPath);
			}
			return new BuildExecutionOutcome(
				true,
				contentUpdate ? "Addressables Content Update completed." : "Addressables full content build completed.",
				outputPath,
				statePath);
		}

		private static ContentBuildDiagnostic Error(
			ContentBuildDiagnosticCode code,
			string message,
			BuildTarget target) =>
			new ContentBuildDiagnostic(code, ContentBuildDiagnosticSeverity.Error, message, target);
	}

	internal static class BuildLayoutArtifactService {
		internal static BuildLayoutCaptureResult Capture(
			IEnumerable<string> candidates,
			string operationDirectory,
			long buildStartedUtcTicks,
			BuildTarget target) {
			var existing = candidates.Where(File.Exists).ToArray();
			if (existing.Length == 0) {
				return new BuildLayoutCaptureResult(
					ContentBuildStatus.Warning,
					"The Addressables build succeeded, but no build-layout source artifact exists. Enable Addressables Debug Build Layout to capture it.");
			}

			var fresh = existing.Where(path => File.GetLastWriteTimeUtc(path).Ticks >= buildStartedUtcTicks).ToArray();
			if (fresh.Length == 0) {
				return new BuildLayoutCaptureResult(
					ContentBuildStatus.Warning,
					"The Addressables build succeeded, but every available build-layout source predates this operation and was not copied.");
			}

			try {
				var copied = new List<string>();
				foreach (var source in fresh) {
					var extension = Path.GetExtension(source);
					var destination = Path.Combine(operationDirectory, $"buildlayout-{target}{extension}");
					if (File.Exists(destination)) {
						return new BuildLayoutCaptureResult(
							ContentBuildStatus.FatalFailure,
							$"Refusing to overwrite the package-owned layout copy '{destination}'.");
					}
					File.Copy(source, destination, false);
					copied.Add(destination);
				}
				return new BuildLayoutCaptureResult(
					ContentBuildStatus.Success,
					"Fresh Addressables build-layout artifact(s) were copied without changing their source.",
					string.Join(";", copied));
			} catch (Exception exception) {
				return new BuildLayoutCaptureResult(
					ContentBuildStatus.FatalFailure,
					$"The build succeeded, but copying its layout artifact failed: {exception.Message}");
			}
		}
	}

	internal sealed class UnityBuildJobStore : IBuildJobStore {
		private static readonly string s_root = UnityContentBuildBackend.AbsolutePath(UnityContentBuildBackend.PackageRoot);
		private static readonly string s_jobsRoot = Path.Combine(s_root, "BuildJobs");
		private static readonly string s_operationsRoot = Path.Combine(s_root, "BuildOperations");

		public string CurrentPath => Path.Combine(s_jobsRoot, "current.json");
		public bool Exists => File.Exists(CurrentPath);

		public string CreateOperationDirectory(string jobId) {
			var path = Path.Combine(s_operationsRoot, jobId);
			if (Directory.Exists(path)) {
				throw new IOException($"Operation directory already exists: {path}");
			}
			Directory.CreateDirectory(path);
			return path;
		}

		public bool TryLoad(out BuildJobRecord record, out string error) {
			record = null;
			error = string.Empty;
			try {
				if (!File.Exists(CurrentPath)) {
					error = "The current job file does not exist.";
					return false;
				}
				record = JsonUtility.FromJson<BuildJobRecord>(File.ReadAllText(CurrentPath));
				if (record == null) {
					error = "The current job JSON could not be parsed.";
					return false;
				}
				return true;
			} catch (Exception exception) {
				error = exception.Message;
				return false;
			}
		}

		public void Save(BuildJobRecord record) {
			Directory.CreateDirectory(s_jobsRoot);
			AtomicWrite(CurrentPath, JsonUtility.ToJson(record, true));
		}

		public void DeleteCurrent() {
			if (File.Exists(CurrentPath)) File.Delete(CurrentPath);
		}

		public string Archive(BuildJobRecord record) {
			var archiveRoot = Path.Combine(s_jobsRoot, "Archive");
			Directory.CreateDirectory(archiveRoot);
			var path = Path.Combine(archiveRoot, record.jobId + "-abandoned.json");
			if (File.Exists(path)) {
				throw new IOException($"Refusing to overwrite archived build-job evidence: {path}");
			}
			File.WriteAllText(path, JsonUtility.ToJson(record, true), new UTF8Encoding(false));
			return path;
		}

		public string ArchiveInvalidCurrent(string reason) {
			if (!File.Exists(CurrentPath)) {
				throw new FileNotFoundException("The invalid current job no longer exists.", CurrentPath);
			}
			var archiveRoot = Path.Combine(s_jobsRoot, "Archive");
			Directory.CreateDirectory(archiveRoot);
			var path = Path.Combine(
				archiveRoot,
				$"invalid-{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}.json");
			if (File.Exists(path)) throw new IOException($"Refusing to overwrite invalid job evidence: {path}");
			var evidence = new InvalidBuildJobArchive {
				reason = reason ?? string.Empty,
				archivedUtcTicks = DateTime.UtcNow.Ticks,
				rawContents = File.ReadAllText(CurrentPath)
			};
			File.WriteAllText(path, JsonUtility.ToJson(evidence, true), new UTF8Encoding(false));
			return path;
		}

		public void ClearLegacySessionState() {
			SessionState.EraseBool("BuildAddressables");
			SessionState.EraseIntArray("PlatformsToBuild");
			SessionState.EraseString("InitialPlatform");
		}

		internal static void AtomicWrite(string path, string contents) {
			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
			var temporaryPath = path + ".tmp";
			File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));
			if (File.Exists(path)) {
				var backupPath = path + ".bak";
				try {
					File.Replace(temporaryPath, path, backupPath);
					if (File.Exists(backupPath)) File.Delete(backupPath);
				} catch (PlatformNotSupportedException) {
					File.Delete(path);
					File.Move(temporaryPath, path);
				}
			} else {
				File.Move(temporaryPath, path);
			}
		}
	}

	[Serializable]
	internal sealed class InvalidBuildJobArchive {
		public string reason;
		public long archivedUtcTicks;
		public string rawContents;
	}

	internal static class ContentBuildIdentity {
		internal static string SettingsGuid(AddressableAssetSettings settings) {
			if (settings == null) return string.Empty;
			var path = AssetDatabase.GetAssetPath(settings);
			return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
		}

		internal static string SettingsHash(AddressableAssetSettings settings) {
			if (settings == null) return string.Empty;
			var settingsPath = AssetDatabase.GetAssetPath(settings);
			if (string.IsNullOrEmpty(settingsPath)) return string.Empty;

			var paths = new HashSet<string>(AssetDatabase.GetDependencies(settingsPath, true), StringComparer.Ordinal);
			paths.Add(settingsPath);
			foreach (var group in settings.groups ?? new List<AddressableAssetGroup>()) {
				var path = group == null ? string.Empty : AssetDatabase.GetAssetPath(group);
				if (!string.IsNullOrEmpty(path)) paths.Add(path);
			}
			foreach (var builder in settings.DataBuilders ?? new List<ScriptableObject>()) {
				var path = builder == null ? string.Empty : AssetDatabase.GetAssetPath(builder);
				if (!string.IsNullOrEmpty(path)) paths.Add(path);
			}

			var values = paths.OrderBy(item => item, StringComparer.Ordinal).Select(path =>
				$"{path}|{AssetDatabase.AssetPathToGUID(path)}|{AssetDatabase.GetAssetDependencyHash(path)}");
			return AutomationHash.Compute(string.Join("\n", values));
		}

		internal static string FileHash(string path) {
			using (var stream = File.OpenRead(path))
			using (var algorithm = SHA256.Create()) {
				var bytes = algorithm.ComputeHash(stream);
				var builder = new StringBuilder(bytes.Length * 2);
				foreach (var item in bytes) builder.Append(item.ToString("x2"));
				return builder.ToString();
			}
		}
	}

	[Serializable]
	internal sealed class BuildOperationReportDto {
		public int schemaVersion;
		public string jobId;
		public string buildKind;
		public string stage;
		public string failurePolicy;
		public string[] pendingTargets;
		public BuildJobItemRecord[] completed;
		public string originalTarget;
		public string activeTarget;
		public string stateFilePath;
		public string stateFileHash;
		public string settingsGuid;
		public string settingsHash;
		public string addressablesVersion;
		public string requestHash;
		public string reportPath;
		public string receiptPath;
		public bool cancellationRequested;
		public string failureMessage;
		public string recoveryMessage;
		public long createdUtcTicks;
		public long updatedUtcTicks;
	}

	internal static class BuildOperationReportWriter {
		internal static void Write(BuildJobRecord record) {
			if (record == null || string.IsNullOrEmpty(record.reportPath)) return;
			var report = new BuildOperationReportDto {
				schemaVersion = record.schemaVersion,
				jobId = record.jobId,
				buildKind = record.buildKind,
				stage = record.stage,
				failurePolicy = record.failurePolicy,
				pendingTargets = record.pendingTargets ?? Array.Empty<string>(),
				completed = record.completed ?? Array.Empty<BuildJobItemRecord>(),
				originalTarget = record.originalTarget,
				activeTarget = record.activeTarget,
				stateFilePath = record.stateFilePath,
				stateFileHash = record.stateFileHash,
				settingsGuid = record.settingsGuid,
				settingsHash = record.settingsHash,
				addressablesVersion = record.addressablesVersion,
				requestHash = record.requestHash,
				reportPath = record.reportPath,
				receiptPath = record.receiptPath,
				cancellationRequested = record.cancellationRequested,
				failureMessage = record.failureMessage,
				recoveryMessage = record.recoveryMessage,
				createdUtcTicks = record.createdUtcTicks,
				updatedUtcTicks = record.updatedUtcTicks
			};
			UnityBuildJobStore.AtomicWrite(record.reportPath, JsonUtility.ToJson(report, true));
		}
	}
}
