using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TorProduction.Addressables.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace TorProduction.Addressables.CI {
	public static class BasicSetupWorkflowProbe {
		private const string PackageName = "com.torproduction.addressables";
		private const string SampleName = "Basic Setup";
		private const string GroupName = "Basic Setup Scenes";
		private const string CategoryLabel = "basic-setup";

		public static void Run() {
			var version = Argument("-torExpectedPackageVersion");
			var originalSettings = AddressableAssetSettingsDefaultObject.SettingsExists
				? AddressableAssetSettingsDefaultObject.GetSettings(false)
				: null;
			var originalBuildScenes = EditorBuildSettings.scenes;
			var defaultFolderExisted = AssetDatabase.IsValidFolder(
				AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
			var projectSettingsPath = "ProjectSettings/TorProduction/AddressablesAutomationProjectSettings.asset";
			var projectSettingsExisted = File.Exists(projectSettingsPath);
			var projectSettingsBytes = projectSettingsExisted
				? File.ReadAllBytes(projectSettingsPath)
				: null;

			try {
				var samples = Sample.FindByPackage(PackageName, version)
					.Where(item => item.displayName == SampleName)
					.ToArray();
				Require(samples.Length == 1, $"Expected one '{SampleName}' sample for {version}.");
				Require(samples[0].Import(
					Sample.ImportOptions.OverridePreviousImports |
					Sample.ImportOptions.HideImportWindow), "Basic Setup sample import failed.");
				AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

				var importedRoot = ToAssetPath(samples[0].importPath);
				var configPath = importedRoot + "/Editor/BasicSetupAddressablesAutomationConfig.asset";
				var scenePath = importedRoot + "/Scenes/SampleScene.unity";
				var config = AssetDatabase.LoadAssetAtPath<AddressablesAutomationConfig>(configPath);
				Require(config != null, $"Imported configuration is missing: {configPath}");
				Require(config.SceneRules.Count == 1, "Basic Setup must contain exactly one scene rule.");

				var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
				Require(settings != null, "Creating default Addressables settings failed.");
				Activate(configPath);

				var validation = AddressablesAutomationValidator.Validate(config, AutomationScope.Scenes);
				var validationText = Diagnostics(validation.Diagnostics);
				Require(validation.IsValid, "Basic Setup validation failed: " + validationText);
				Require(validation.Diagnostics.All(item =>
					item.Severity == ConfigurationDiagnosticSeverity.Warning &&
					(item.Code == ConfigurationDiagnosticCode.DestinationGroupNotFound ||
					 item.Code == ConfigurationDiagnosticCode.LabelNotFound)),
					"Basic Setup produced an unexpected pre-Apply diagnostic: " + validationText);

				var first = AddressablesAutomation.Analyze(config, AutomationScope.Scenes);
				Require(first.IsValid, "Scene Analyze failed: " + PlanDiagnostics(first));
				RequireOperation(first, AutomationOperationKind.CreateGroup,
					item => item.GroupName == GroupName, "destination group");
				RequireOperation(first, AutomationOperationKind.AddBundledAssetGroupSchema,
					item => item.GroupName == GroupName, "BundledAssetGroupSchema");
				RequireOperation(first, AutomationOperationKind.AddContentUpdateGroupSchema,
					item => item.GroupName == GroupName, "ContentUpdateGroupSchema");
				RequireOperation(first, AutomationOperationKind.CreateLabel,
					item => item.Value == CategoryLabel, "category label");
				RequireOperation(first, AutomationOperationKind.CreateEntry,
					item => Normalize(item.AssetPath) == scenePath, "sample scene entry");
				RequireOperation(first, AutomationOperationKind.AddLabel,
					item => item.Value == CategoryLabel && Normalize(item.AssetPath) == scenePath,
					"sample scene category label");

				var report = AddressablesAutomation.Apply(first);
				Require(report.Succeeded, "Scene Apply failed: " + string.Join(" | ", report.Failures));
				var group = settings.FindGroup(GroupName);
				Require(group != null, "Scene Apply did not create the sample group.");
				Require(group.GetSchema<BundledAssetGroupSchema>() != null,
					"Sample group is missing BundledAssetGroupSchema.");
				Require(group.GetSchema<ContentUpdateGroupSchema>() != null,
					"Sample group is missing ContentUpdateGroupSchema.");
				var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(scenePath));
				Require(entry != null && entry.parentGroup == group,
					"Sample scene entry is missing from the expected group.");
				Require(entry.labels.Contains(CategoryLabel),
					"Sample scene entry is missing its category label.");

				var second = AddressablesAutomation.Analyze(config, AutomationScope.Scenes);
				Require(second.IsValid, "Second Scene Analyze failed: " + PlanDiagnostics(second));
				Require(second.Operations.Count == 0,
					"Second Scene Analyze did not converge: " +
					string.Join(" | ", second.Operations.Select(item => item.Description)));
				Debug.Log("Basic Setup activation, validation, Analyze, Apply, and convergence passed.");
			} finally {
				EditorBuildSettings.scenes = originalBuildScenes ?? Array.Empty<EditorBuildSettingsScene>();
				if (originalSettings != null) {
					AddressableAssetSettingsDefaultObject.Settings = originalSettings;
				} else {
					if (AddressableAssetSettingsDefaultObject.SettingsExists ||
					    AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder)) {
						AddressableAssetSettingsDefaultObject.Settings = null;
					}
					EditorBuildSettings.RemoveConfigObject(
						AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName);
				}
				if (!defaultFolderExisted && AssetDatabase.IsValidFolder(
					    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder)) {
					AssetDatabase.DeleteAsset(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
				}
				RestoreProjectSettings(projectSettingsPath, projectSettingsExisted, projectSettingsBytes);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			}
		}

		private static void Activate(string configPath) {
			var assembly = typeof(AddressablesAutomationConfig).Assembly;
			var storeType = assembly.GetType(
				"TorProduction.Addressables.Editor.AddressablesAutomationProjectSettingsStore", true);
			var backendType = assembly.GetType(
				"TorProduction.Addressables.Editor.UnityProjectSettingsBackend", true);
			var method = storeType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
				.Single(item => item.Name == "TryPersistSelection" && item.GetParameters().Length == 3);
			var arguments = new object[] {
				AssetDatabase.AssetPathToGUID(configPath),
				Activator.CreateInstance(backendType, true),
				null
			};
			var succeeded = (bool)method.Invoke(null, arguments);
			Require(succeeded, "Activating Basic Setup failed: " + (arguments[2] ?? "unknown error"));
		}

		private static void RequireOperation(
			AutomationPlan plan,
			AutomationOperationKind kind,
			Func<AutomationOperation, bool> predicate,
			string description) {
			Require(plan.Operations.Any(item => item.Kind == kind && predicate(item)),
				$"Scene Analyze did not propose the required {description} operation.");
		}

		private static string Argument(string name) {
			var arguments = Environment.GetCommandLineArgs();
			var index = Array.IndexOf(arguments, name);
			if (index < 0 || index + 1 >= arguments.Length) {
				throw new ArgumentException($"Missing required argument {name}.");
			}
			return arguments[index + 1];
		}

		private static void RestoreProjectSettings(string path, bool existed, byte[] bytes) {
			if (existed) {
				Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "ProjectSettings");
				File.WriteAllBytes(path, bytes);
				return;
			}
			if (File.Exists(path)) File.Delete(path);
			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory) &&
			    Directory.GetFileSystemEntries(directory).Length == 0) {
				Directory.Delete(directory);
			}
		}

		private static string Diagnostics(System.Collections.Generic.IEnumerable<ConfigurationDiagnostic> items) {
			return string.Join(" | ", items.Select(item =>
				$"{item.Severity}:{item.Code}:{item.Location}:{item.Message}"));
		}

		private static string PlanDiagnostics(AutomationPlan plan) {
			return string.Join(" | ", plan.Diagnostics.Select(item =>
				$"{item.Severity}:{item.Code}:{item.Location}:{item.Message}"));
		}

		private static string Normalize(string path) {
			return (path ?? string.Empty).Replace('\\', '/');
		}

		private static string ToAssetPath(string path) {
			var normalized = Normalize(path);
			if (!Path.IsPathRooted(path)) return normalized;
			var projectRoot = Normalize(Directory.GetParent(Application.dataPath).FullName).TrimEnd('/');
			Require(normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase),
				"Imported sample path is outside the disposable project: " + normalized);
			return normalized.Substring(projectRoot.Length + 1);
		}

		private static void Require(bool condition, string message) {
			if (!condition) throw new InvalidOperationException(message);
		}
	}
}
