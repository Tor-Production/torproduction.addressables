using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TorProduction.Addressables.Editor;
using TorProduction.AddressablesToolpack.Editor;
using TorProduction.AddressablesToolpack.Editor.Menu;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.TestTools;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace TorProduction.AddressablesToolpack.Editor.Tests {
	internal sealed class PhaseZeroSafetyTests {
		[Test]
		public void ConfigurationReads_DoNotCreateOrMutateProjectState() {
			var configExisted = File.Exists("ProjectSettings/ProjectConfig.json");
			var configContents = configExisted ? File.ReadAllText("ProjectSettings/ProjectConfig.json") : null;
			var settingsExisted = File.Exists(AddressablesAutomationProjectSettingsStore.SettingsPath);
			var settingsContents = settingsExisted
				? File.ReadAllText(AddressablesAutomationProjectSettingsStore.SettingsPath)
				: null;
			var addressablesSettings = AddressableAssetSettingsDefaultObject.SettingsExists
				? AddressableAssetSettingsDefaultObject.GetSettings(false)
				: null;
			var buildSettings = EditorBuildSettings.scenes
				.Select(scene => $"{scene.guid}|{scene.path}|{scene.enabled}")
				.ToArray();

			AddressablesAutomationProjectSettingsStore.Read();
			AddressablesAutomationContextProvider.ResolveManual(AutomationScope.All);

			Assert.That(File.Exists("ProjectSettings/ProjectConfig.json"), Is.EqualTo(configExisted));
			if (configExisted) {
				Assert.That(File.ReadAllText("ProjectSettings/ProjectConfig.json"), Is.EqualTo(configContents));
			}
			Assert.That(File.Exists(AddressablesAutomationProjectSettingsStore.SettingsPath), Is.EqualTo(settingsExisted));
			if (settingsExisted) {
				Assert.That(
					File.ReadAllText(AddressablesAutomationProjectSettingsStore.SettingsPath),
					Is.EqualTo(settingsContents));
			}

			Assert.That(
				AddressableAssetSettingsDefaultObject.SettingsExists
					? AddressableAssetSettingsDefaultObject.GetSettings(false)
					: null,
				Is.SameAs(addressablesSettings));
			Assert.That(
				EditorBuildSettings.scenes.Select(scene => $"{scene.guid}|{scene.path}|{scene.enabled}").ToArray(),
				Is.EqualTo(buildSettings));
		}

		[Test]
		[Category("CleanInstall")]
		public void CleanInstall_HasNoGeneratedConfigurationOrAddressablesState() {
			if (!Environment.GetCommandLineArgs().Contains("-torCleanInstall")) {
				Assert.Pass("The clean-install invariants run only in the marked isolated harness.");
			}

			Assert.That(
				File.Exists("ProjectSettings/ProjectConfig.json"),
				Is.False,
				"The isolated project must not contain legacy configuration state.");
			Assert.That(
				File.Exists(AddressablesAutomationProjectSettingsStore.SettingsPath),
				Is.False,
				"The isolated project must not contain generated automation settings.");
			Assert.That(
				AddressableAssetSettingsDefaultObject.SettingsExists,
				Is.False,
				"The isolated project must not contain Addressables settings.");

			Assert.That(Directory.Exists("Assets/AddressableAssetsData"), Is.False);
			Assert.That(EditorBuildSettings.scenes, Is.Empty);
		}

		[Test]
		public void IncompleteAndAutomaticWorkflows_AreFailClosed() {
			Assert.That(AddressablesAutomationWorkflowGate.IncompleteWorkflowsEnabled, Is.False);
			Assert.That(AddressablesAutomationWorkflowGate.AutomaticSceneReconciliationImplemented, Is.False);
			Assert.That(AddressablesAutomationWorkflowGate.CanExecute(AutomationScope.All), Is.False);

			LogAssert.Expect(LogType.Warning, new Regex("^Test workflow:"));
			Assert.That(
				AddressablesAutomationWorkflowGate.TryBegin("Test workflow", AutomationScope.All),
				Is.False);

			var migrationReportPath = Path.Combine(
				UnityEngine.AddressableAssets.Addressables.LibraryPath,
				"UpdatedInteractables.txt");
			var migrationReportExisted = File.Exists(migrationReportPath);
			var migrationReportContents = migrationReportExisted ? File.ReadAllText(migrationReportPath) : null;

			LogAssert.Expect(LogType.Warning, new Regex("^Interactable config migration:"));
			InteractableTemplateFieldsUpdater.UpdateFields();

			Assert.That(File.Exists(migrationReportPath), Is.EqualTo(migrationReportExisted));
			if (migrationReportExisted) {
				Assert.That(File.ReadAllText(migrationReportPath), Is.EqualTo(migrationReportContents));
			}
		}

		[Test]
		public void PackageManifest_DeclaresPinnedPhaseZeroBaseline() {
			var packageInfo = PackageManagerPackageInfo.FindForAssembly(typeof(PhaseZeroSafetyTests).Assembly);
			Assert.That(packageInfo, Is.Not.Null);

			var manifestPath = Path.Combine(packageInfo.resolvedPath, "package.json");
			var manifest = File.ReadAllText(manifestPath);

			Assert.That(ReadManifestString(manifest, "name"), Is.EqualTo("com.torproduction.addressables"));
			Assert.That(ReadManifestString(manifest, "version"), Is.EqualTo("0.1.0-preview.1"));
			Assert.That(ReadManifestString(manifest, "unity"), Is.EqualTo("6000.0"));
			StringAssert.Contains("\"com.unity.addressables\": \"2.7.6\"", manifest);
			StringAssert.DoesNotContain("com.stansassets.foundation", manifest);
		}

		[Test]
		public void ProductionAssemblies_AreReferencedWithoutSamplesOrTests() {
			Assert.That(typeof(InteractableFactoryId).Assembly.GetName().Name, Is.EqualTo("TorProduction.AddressablesToolpack"));
			Assert.That(typeof(AssetTypes).Assembly.GetName().Name, Is.EqualTo("TorProduction.AddressablesService.Editor"));
			Assert.That(typeof(AddressablesAutomationSettingsProvider).Assembly.GetName().Name, Is.EqualTo("TorProduction.AddressablesToolpack.Editor.Menu"));

			var editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
			var productionAssemblyNames = new[] {
				"TorProduction.AddressablesToolpack",
				"TorProduction.AddressablesService.Editor",
				"TorProduction.AddressablesToolpack.Editor.Menu"
			};

			foreach (var assemblyName in productionAssemblyNames) {
				var assembly = editorAssemblies.Single(candidate => candidate.name == assemblyName);
				var references = assembly.assemblyReferences.Select(reference => reference.name).ToArray();
				Assert.That(references, Does.Not.Contain("TorProduction.AddressablesToolpack.Samples"));
				Assert.That(references.Any(reference => reference.EndsWith(".Tests", StringComparison.Ordinal)), Is.False);
			}

			var packageInfo = PackageManagerPackageInfo.FindForAssembly(typeof(PhaseZeroSafetyTests).Assembly);
			var productionSources = Directory.EnumerateFiles(packageInfo.resolvedPath, "*.cs", SearchOption.AllDirectories)
				.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"));
			foreach (var sourcePath in productionSources) {
				StringAssert.DoesNotContain("using NUnit.Framework", File.ReadAllText(sourcePath), sourcePath);
			}
		}

		private static string ReadManifestString(string manifest, string propertyName) {
			var match = Regex.Match(
				manifest,
				$"\\\"{Regex.Escape(propertyName)}\\\"\\s*:\\s*\\\"(?<value>[^\\\"]+)\\\"");
			Assert.That(match.Success, Is.True, $"Manifest property '{propertyName}' is missing.");
			return match.Groups["value"].Value;
		}
	}
}
