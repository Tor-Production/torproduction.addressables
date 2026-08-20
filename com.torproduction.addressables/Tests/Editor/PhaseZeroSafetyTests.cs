using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
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
		private string m_testDirectory;

		[SetUp]
		public void SetUp() {
			m_testDirectory = Path.GetFullPath(Path.Combine(
				"Library",
				"TorProduction.Addressables.Tests",
				Guid.NewGuid().ToString("N")));
			Directory.CreateDirectory(m_testDirectory);
		}

		[TearDown]
		public void TearDown() {
			if (!Directory.Exists(m_testDirectory)) {
				return;
			}

			foreach (var file in Directory.EnumerateFiles(m_testDirectory)) {
				File.Delete(file);
			}

			Directory.Delete(m_testDirectory);
		}

		[Test]
		public void MissingConfigurationRead_IsInert() {
			var missingPath = Path.Combine(m_testDirectory, "missing-project-config.json");

			foreach (ConfigsEnum configType in Enum.GetValues(typeof(ConfigsEnum))) {
				Assert.That(
					ProjectConfigPathsManager.TryGetConfigPath(configType, missingPath, out var configPath),
					Is.False);
				Assert.That(configPath, Is.Empty);
			}

			Assert.That(File.Exists(missingPath), Is.False, "A configuration read must not create a file.");
		}

		[Test]
		public void InvalidConfigurationRead_DoesNotRewriteInput() {
			var configPath = Path.Combine(m_testDirectory, "invalid-project-config.json");
			const string invalidConfiguration =
				"{\"m_ScenesListConfigGUID\":\"missing\",\"m_AddressableAssetsConfigGUID\":\"missing\",\"m_AppStatesConfigGUID\":\"missing\"}";
			File.WriteAllText(configPath, invalidConfiguration);

			foreach (ConfigsEnum configType in Enum.GetValues(typeof(ConfigsEnum))) {
				Assert.That(
					ProjectConfigPathsManager.TryGetConfigPath(configType, configPath, out var resolvedPath),
					Is.False);
				Assert.That(resolvedPath, Is.Empty);
			}

			Assert.That(File.ReadAllText(configPath), Is.EqualTo(invalidConfiguration));
		}

		[Test]
		public void ConfigurationReads_DoNotMutateAddressablesOrBuildSettings() {
			var configExisted = File.Exists("ProjectSettings/ProjectConfig.json");
			var configContents = configExisted ? File.ReadAllText("ProjectSettings/ProjectConfig.json") : null;
			var addressablesSettings = AddressableAssetSettingsDefaultObject.GetSettings(false);
			var buildSettings = EditorBuildSettings.scenes
				.Select(scene => $"{scene.guid}|{scene.path}|{scene.enabled}")
				.ToArray();

			foreach (ConfigsEnum configType in Enum.GetValues(typeof(ConfigsEnum))) {
				ProjectConfigPathsManager.GetConfigPath(configType);
			}

			Assert.That(File.Exists("ProjectSettings/ProjectConfig.json"), Is.EqualTo(configExisted));
			if (configExisted) {
				Assert.That(File.ReadAllText("ProjectSettings/ProjectConfig.json"), Is.EqualTo(configContents));
			}

			Assert.That(AddressableAssetSettingsDefaultObject.GetSettings(false), Is.SameAs(addressablesSettings));
			Assert.That(
				EditorBuildSettings.scenes.Select(scene => $"{scene.guid}|{scene.path}|{scene.enabled}").ToArray(),
				Is.EqualTo(buildSettings));
		}

		[Test]
		[Category("CleanInstall")]
		public void CleanInstall_HasNoGeneratedConfigurationOrAddressablesState() {
			Assume.That(
				File.Exists("ProjectSettings/ProjectConfig.json"),
				Is.False,
				"This assertion runs only in the isolated clean-install lane.");
			Assume.That(
				AddressableAssetSettingsDefaultObject.GetSettings(false),
				Is.Null,
				"This assertion runs only in the isolated clean-install lane.");

			Assert.That(Directory.Exists("Assets/AddressableAssetsData"), Is.False);
			Assert.That(EditorBuildSettings.scenes, Is.Empty);
		}

		[Test]
		public void IncompleteAndAutomaticWorkflows_AreFailClosed() {
			Assert.That(PhaseZeroWorkflowGate.IncompleteWorkflowsEnabled, Is.False);
			Assert.That(PhaseZeroWorkflowGate.AutomaticSceneProcessingEnabled, Is.False);

			LogAssert.Expect(LogType.Warning, $"Test workflow: {PhaseZeroWorkflowGate.DisabledReason}");
			Assert.That(PhaseZeroWorkflowGate.TryBegin("Test workflow"), Is.False);
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
			Assert.That(typeof(ProjectConfigPathsManager).Assembly.GetName().Name, Is.EqualTo("TorProduction.AddressablesToolpack.Editor.Menu"));

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
