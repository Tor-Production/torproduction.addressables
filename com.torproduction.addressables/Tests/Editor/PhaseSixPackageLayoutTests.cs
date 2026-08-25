using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting.APIUpdating;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace TorProduction.Addressables.Editor.Tests {
	internal sealed class PhaseSixPackageLayoutTests {
		private const string PackageName = "com.torproduction.addressables";
		private const string ProductionAssemblyName = "TorProduction.Addressables.Editor";
		private const string ImportedSampleRoot =
			"Assets/Samples/Tor Production Addressables Toolpack/0.1.0-preview.1/Basic Setup";

		[Test]
		public void ProductionAssemblyGraph_IsExactlyOneEditorAssembly() {
			var packageRoot = PackageRoot();
			var asmdefs = Directory.GetFiles(packageRoot, "*.asmdef", SearchOption.AllDirectories)
				.Where(path => !IsUnder(path, "Tests"))
				.ToArray();

			Assert.That(asmdefs, Has.Length.EqualTo(1));
			var editorAsmdef = File.ReadAllText(asmdefs[0]);
			Assert.That(ReadJsonString(editorAsmdef, "name"), Is.EqualTo(ProductionAssemblyName));
			Assert.That(ReadJsonString(editorAsmdef, "rootNamespace"),
				Is.EqualTo("TorProduction.Addressables.Editor"));
			Assert.That(ReadJsonStrings(editorAsmdef, "references"), Is.EqualTo(new[] {
				"GUID:9e24947de15b9834991c9d8411ea37cf",
				"GUID:69448af7b92c7f342b298e06a37122aa"
			}));
			Assert.That(ReadJsonStrings(editorAsmdef, "includePlatforms"), Is.EqualTo(new[] { "Editor" }));

			Assert.That(Directory.Exists(Path.Combine(packageRoot, "Runtime")), Is.False);
			Assert.That(Directory.Exists(Path.Combine(packageRoot, "Samples")), Is.False);
			Assert.That(Directory.GetFiles(packageRoot, "*.asmdef", SearchOption.AllDirectories)
				.Any(path => path.IndexOf("Samples~", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);

			var editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
			var production = editorAssemblies.Where(assembly =>
				assembly.name.StartsWith("TorProduction.Addressables", StringComparison.Ordinal) &&
				!assembly.name.EndsWith(".Tests", StringComparison.Ordinal)).ToArray();
			Assert.That(production.Select(item => item.name), Is.EqualTo(new[] { ProductionAssemblyName }));
			Assert.That(production[0].assemblyReferences.Select(item => item.name)
				.Any(name => name.IndexOf("Samples", StringComparison.OrdinalIgnoreCase) >= 0 ||
				             name.EndsWith(".Tests", StringComparison.Ordinal) ||
				             name.IndexOf("Foundation", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
		}

		[Test]
		public void TestsReferenceProductionExplicitly_AndNUnitIsTestOnly() {
			var packageRoot = PackageRoot();
			var testAsmdefPath = Path.Combine(
				packageRoot, "Tests", "Editor", "TorProduction.Addressables.Editor.Tests.asmdef");
			var testAsmdef = File.ReadAllText(testAsmdefPath);
			Assert.That(ReadJsonString(testAsmdef, "name"), Is.EqualTo("TorProduction.Addressables.Editor.Tests"));
			Assert.That(ReadJsonString(testAsmdef, "rootNamespace"),
				Is.EqualTo("TorProduction.Addressables.Editor.Tests"));
			Assert.That(ReadJsonStrings(testAsmdef, "references"), Does.Contain("GUID:6a4270a497015e843be16b899b29c2fb"));
			Assert.That(ReadJsonStrings(testAsmdef, "precompiledReferences"),
				Is.EqualTo(new[] { "nunit.framework.dll" }));

			var assemblyInfo = File.ReadAllText(Path.Combine(packageRoot, "Editor", "AssemblyInfo.cs"));
			Assert.That(Regex.Matches(assemblyInfo, "InternalsVisibleTo").Count, Is.EqualTo(1));
			StringAssert.Contains("TorProduction.Addressables.Editor.Tests", assemblyInfo);

			foreach (var sourcePath in Directory.GetFiles(
				         Path.Combine(packageRoot, "Editor"), "*.cs", SearchOption.AllDirectories)) {
				StringAssert.DoesNotContain("NUnit", File.ReadAllText(sourcePath), sourcePath);
			}
		}

		[Test]
		public void ProductionNamespaces_AreConsistentlyTorProductionAddressablesEditor() {
			var assembly = typeof(AddressablesAutomationConfig).Assembly;
			Assert.That(assembly.GetName().Name, Is.EqualTo(ProductionAssemblyName));
			var invalid = assembly.GetTypes()
				.Where(type => !string.IsNullOrEmpty(type.Namespace))
				.Where(type => !type.Namespace.StartsWith(
					"TorProduction.Addressables.Editor", StringComparison.Ordinal))
				.Select(type => type.FullName)
				.OrderBy(name => name, StringComparer.Ordinal)
				.ToArray();
			Assert.That(invalid, Is.Empty);
		}

		[Test]
		public void RemovedRuntimeAndDeadTypes_HaveNoProductionOrSerializedReferences() {
			var packageRoot = PackageRoot();
			var ignoredMigrationFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
				"LegacyConfigurationMigration.cs",
				"ProjectConfigData.cs"
			};
			var productionFiles = Directory.GetFiles(Path.Combine(packageRoot, "Editor"), "*", SearchOption.AllDirectories)
				.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
				               path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
				               path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
				               path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
				.Where(path => !ignoredMigrationFiles.Contains(Path.GetFileName(path)))
				.ToArray();
			var source = string.Join("\n", productionFiles.Select(File.ReadAllText));
			foreach (var removedIdentifier in new[] {
				"ObjectTemplate", "IObjectTemplate", "ITemplate", "InteractableFactoryId",
				"SerializableDictionary", "SceneField", "ReadOnlyAttribute", "RuntimeExample",
				"EditorExample", "AddressableAssetsConfig", "UpdateGroupSettings", "ScenesListConfig",
				"ProjectSettingsWindow", "UpdateAllNewAssets", "AssetTypes", "ProjectAssetUtil",
				"AddressableMenuUtils", "StansAssets", "PackageSample"
			}) {
				Assert.That(Regex.IsMatch(source, "\\b" + Regex.Escape(removedIdentifier) + "\\b"),
					Is.False, removedIdentifier);
			}

			var serializedSource = string.Join("\n", Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories)
				.Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
				               path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
				               path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
				.Select(File.ReadAllText));
			foreach (var removedGuid in new[] {
				"f39721fd5830c7740aaee0f9c89f76a5", "13993e30313c462185167a9c75246336",
				"50b15bda8048baa4c94e69eb58d3e6e6", "9028e9a5584f3f6469199a8f5c5785bd",
				"e2ccaccc69d55e541b351b67e957a5b7", "adfdc9da12c598242bc975b21a086c01",
				"485de64a98f018044b1d84b3d9a26955", "bb1976c08118e184da01617a660f81b5"
			}) {
				StringAssert.DoesNotContain(removedGuid, serializedSource, removedGuid);
			}
		}

		[Test]
		public void AddressablesAutomationConfig_AssemblyMigrationFixtureLoads() {
			var movedFrom = typeof(AddressablesAutomationConfig)
				.GetCustomAttributes(typeof(MovedFromAttribute), false);
			Assert.That(movedFrom, Has.Length.EqualTo(1));

			var fixture = AssetDatabase.LoadAssetAtPath<AddressablesAutomationConfig>(
				"Packages/com.torproduction.addressables/Tests/Editor/Fixtures/AddressablesAutomationConfigAssemblyMigration.asset");
			Assert.That(fixture, Is.Not.Null);
			Assert.That(fixture.SchemaVersion, Is.EqualTo(AddressablesAutomationConfig.CurrentSchemaVersion));
			Assert.That(fixture.GroupRules.Count, Is.EqualTo(1));
			Assert.That(fixture.GroupRules[0].SourceFolderGuid,
				Is.EqualTo("11111111111111111111111111111111"));
			Assert.That(fixture.GroupRules[0].DestinationGroupName, Is.EqualTo("Migrated Content"));
			Assert.That(fixture.GroupRules[0].RequiredLabels, Is.EqualTo(new[] { "retained" }));
		}

		[Test]
		public void Manifest_DeclaresCuratedBasicSetupSample() {
			var packageRoot = PackageRoot();
			var manifest = File.ReadAllText(Path.Combine(packageRoot, "package.json"));
			StringAssert.Contains("\"displayName\": \"Basic Setup\"", manifest);
			StringAssert.Contains("\"path\": \"Samples~/BasicSetup\"", manifest);
			StringAssert.Contains("explicit Addressables automation workflow", manifest);
			var samplesExcluded = Environment.GetCommandLineArgs().Contains("-torSamplesExcluded");
			Assert.That(Directory.Exists(Path.Combine(packageRoot, "Samples~", "BasicSetup")),
				Is.EqualTo(!samplesExcluded));
			Assert.That(Directory.Exists(Path.Combine(packageRoot, "Samples")), Is.False);
		}

		[Test]
		public void PackageSerializedAssets_HaveResolvableScriptGuids() {
			var packageRoot = PackageRoot();
			var metaGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var metaPath in Directory.GetFiles(packageRoot, "*.meta", SearchOption.AllDirectories)) {
				var match = Regex.Match(File.ReadAllText(metaPath), @"(?m)^guid:\s*(?<guid>[0-9a-f]{32})\s*$");
				if (match.Success) metaGuids.Add(match.Groups["guid"].Value);
			}

			foreach (var assetPath in Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories)
				         .Where(IsSerializedUnityAsset)) {
				foreach (Match match in Regex.Matches(
					         File.ReadAllText(assetPath),
					         @"m_Script:\s*\{[^}]*guid:\s*(?<guid>[0-9a-f]{32})")) {
					var guid = match.Groups["guid"].Value;
					Assert.That(metaGuids.Contains(guid), Is.True,
						$"Missing script GUID {guid} in {assetPath}.");
				}
			}
		}

		[Test]
		public void ImportedBasicSetup_LoadsAndContainsNoMissingScripts() {
			if (!Environment.GetCommandLineArgs().Contains("-torSampleImported")) {
				Assert.Pass("The imported-sample assertions run only in the marked disposable-project lane.");
			}

			var samples = Sample.FindByPackage(PackageName, "0.1.0-preview.1");
			var matchingSamples = samples.Where(item => item.displayName == "Basic Setup").ToArray();
			Assert.That(matchingSamples.Length, Is.EqualTo(1));
			var sample = matchingSamples[0];
			Assert.That(sample.Import(
				Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow), Is.True);
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			var importedRoot = ToAssetPath(sample.importPath);
			Assert.That(importedRoot, Is.EqualTo(ImportedSampleRoot));

			var configPath = importedRoot + "/Editor/BasicSetupAddressablesAutomationConfig.asset";
			var sceneFolderPath = importedRoot + "/Scenes";
			var scenePath = sceneFolderPath + "/SampleScene.unity";
			var config = AssetDatabase.LoadAssetAtPath<AddressablesAutomationConfig>(configPath);
			Assert.That(config, Is.Not.Null);
			Assert.That(config.SchemaVersion, Is.EqualTo(AddressablesAutomationConfig.CurrentSchemaVersion));
			Assert.That(config.SceneRules.Count, Is.EqualTo(1));
			Assert.That(config.SceneRules[0].DestinationGroupName, Is.EqualTo("Basic Setup Scenes"));
			Assert.That(config.SceneRules[0].RequiredLabels, Is.EqualTo(new[] { "basic-setup" }));
			Assert.That(AssetDatabase.AssetPathToGUID(sceneFolderPath),
				Is.EqualTo("4f42b69c201bcba42a0e7d976c56bd93"));
			Assert.That(config.SceneRules[0].SourceFolderGuid,
				Is.EqualTo(AssetDatabase.AssetPathToGUID(sceneFolderPath)));
			Assert.That(AssetDatabase.AssetPathToGUID(configPath),
				Is.EqualTo("bd9739a730454e63ba1e6ad90844123a"));
			Assert.That(AssetDatabase.AssetPathToGUID(scenePath),
				Is.EqualTo("b6072c6f7b9037d4f8bc0963f8916ca2"));
			Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);

			var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
			try {
				var missingCount = scene.GetRootGameObjects()
					.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
				Assert.That(missingCount, Is.Zero);
			} finally {
				EditorSceneManager.CloseScene(scene, true);
			}
		}

		[Test]
		public void PlayerScriptsCompileWithoutPackageRuntimeAssembly() {
			var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
			Assert.That(playerAssemblies.Any(assembly =>
				assembly.name.StartsWith("TorProduction.Addressables", StringComparison.Ordinal) &&
				!assembly.name.EndsWith(".Tests", StringComparison.Ordinal)), Is.False);

			var outputPath = Path.GetFullPath(
				Path.Combine("Library", "TorProduction.Addressables", "Phase6PlayerScripts"));
			if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
			Directory.CreateDirectory(outputPath);
			try {
				var settings = new ScriptCompilationSettings {
					target = BuildTarget.StandaloneWindows64,
					group = BuildTargetGroup.Standalone,
					options = ScriptCompilationOptions.None
				};
				Assert.DoesNotThrow(() => PlayerBuildInterface.CompilePlayerScripts(settings, outputPath));
			} finally {
				if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
			}
		}

		[Test]
		public void PublicApiSurface_MatchesDeterministicSnapshot() {
			var actual = BuildApiSurfaceSnapshot();
			var arguments = Environment.GetCommandLineArgs();
			var markerIndex = Array.IndexOf(arguments, "-torWriteApiSnapshot");
			if (markerIndex >= 0) {
				Assert.That(markerIndex + 1, Is.LessThan(arguments.Length));
				var outputPath = Path.GetFullPath(arguments[markerIndex + 1]);
				Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
				File.WriteAllText(outputPath, actual, new UTF8Encoding(false));
				Assert.Pass("The deterministic API snapshot was written to the explicit artifact path.");
			}

			var expectedPath = Path.Combine(PackageRoot(), "Documentation~", "API_SURFACE.txt");
			Assert.That(File.Exists(expectedPath), Is.True, expectedPath);
			Assert.That(Normalize(File.ReadAllText(expectedPath)), Is.EqualTo(actual));
		}

		private static string BuildApiSurfaceSnapshot() {
			var assembly = typeof(AddressablesAutomationConfig).Assembly;
			var lines = new List<string> {
				"assembly " + assembly.GetName().Name,
				"root-namespace TorProduction.Addressables.Editor",
				"reference Unity.Addressables",
				"reference Unity.Addressables.Editor"
			};
			foreach (var type in assembly.GetExportedTypes()
				         .OrderBy(item => item.FullName, StringComparer.Ordinal)) {
				lines.Add("type " + TypeKind(type) + " " + FormatType(type));
				if (type.IsEnum) {
					foreach (var name in Enum.GetNames(type).OrderBy(item => item, StringComparer.Ordinal)) {
						lines.Add("  value " + name + " = " +
						          Convert.ToInt64(Enum.Parse(type, name), CultureInfo.InvariantCulture));
					}
					continue;
				}

				var members = new List<string>();
				foreach (var constructor in type.GetConstructors(
					         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
					members.Add("constructor " + FormatParameters(constructor.GetParameters()));
				}
				foreach (var field in type.GetFields(
					         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
					var suffix = field.IsLiteral
						? " = " + FormatConstant(field.GetRawConstantValue())
						: string.Empty;
					members.Add("field " + FormatType(field.FieldType) + " " + field.Name + suffix);
				}
				foreach (var property in type.GetProperties(
					         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
					var accessors = (property.GetMethod != null && property.GetMethod.IsPublic ? "get; " : string.Empty) +
					                (property.SetMethod != null && property.SetMethod.IsPublic ? "set; " : string.Empty);
					members.Add("property " + FormatType(property.PropertyType) + " " + property.Name +
					            " { " + accessors + "}");
				}
				foreach (var method in type.GetMethods(
					         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
					         .Where(item => !item.IsSpecialName)) {
					members.Add("method " + FormatType(method.ReturnType) + " " + method.Name +
					            FormatParameters(method.GetParameters()));
				}
				foreach (var member in members.OrderBy(item => item, StringComparer.Ordinal)) {
					lines.Add("  " + member);
				}
			}
			return string.Join("\n", lines) + "\n";
		}

		private static string TypeKind(Type type) {
			if (type.IsEnum) return "enum";
			if (type.IsInterface) return "interface";
			if (type.IsValueType) return "struct";
			if (type.IsAbstract && type.IsSealed) return "static-class";
			return type.IsSealed ? "sealed-class" : "class";
		}

		private static string FormatParameters(IEnumerable<ParameterInfo> parameters) {
			return "(" + string.Join(", ", parameters.Select(parameter => {
				var value = FormatType(parameter.ParameterType) + " " + parameter.Name;
				if (parameter.IsOptional) value += " = " + FormatConstant(parameter.DefaultValue);
				return value;
			})) + ")";
		}

		private static string FormatType(Type type) {
			if (type.IsByRef) return FormatType(type.GetElementType()) + "&";
			if (type.IsArray) return FormatType(type.GetElementType()) + "[]";
			if (!type.IsGenericType) return type.FullName ?? type.Name;
			var definition = type.GetGenericTypeDefinition();
			var name = definition.FullName ?? definition.Name;
			var tick = name.IndexOf('`');
			if (tick >= 0) name = name.Substring(0, tick);
			return name + "<" + string.Join(", ", type.GetGenericArguments().Select(FormatType)) + ">";
		}

		private static string FormatConstant(object value) {
			if (value == null || value == DBNull.Value || value == Missing.Value) return "null";
			if (value is string text) return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
			if (value is char character) return "'" + character + "'";
			if (value is bool boolean) return boolean ? "true" : "false";
			return Convert.ToString(value, CultureInfo.InvariantCulture);
		}

		private static string PackageRoot() {
			var packageInfo = PackageInfo.FindForAssembly(typeof(PhaseSixPackageLayoutTests).Assembly);
			Assert.That(packageInfo, Is.Not.Null);
			Assert.That(packageInfo.name, Is.EqualTo(PackageName));
			return packageInfo.resolvedPath;
		}

		private static bool IsUnder(string path, string directoryName) {
			return path.IndexOf(
				Path.DirectorySeparatorChar + directoryName + Path.DirectorySeparatorChar,
				StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsSerializedUnityAsset(string path) {
			return path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
			       path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
			       path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
		}

		private static string ToAssetPath(string fullPath) {
			var normalizedPath = Path.GetFullPath(fullPath).Replace('\\', '/');
			var normalizedAssets = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
			Assert.That(normalizedPath.StartsWith(
				normalizedAssets + "/", StringComparison.OrdinalIgnoreCase), Is.True, normalizedPath);
			return "Assets/" + normalizedPath.Substring(normalizedAssets.Length + 1);
		}

		private static string ReadJsonString(string json, string propertyName) {
			var match = Regex.Match(json,
				"\\\"" + Regex.Escape(propertyName) + "\\\"\\s*:\\s*\\\"(?<value>[^\\\"]*)\\\"");
			Assert.That(match.Success, Is.True, propertyName);
			return match.Groups["value"].Value;
		}

		private static string[] ReadJsonStrings(string json, string propertyName) {
			var array = Regex.Match(json,
				"\\\"" + Regex.Escape(propertyName) + "\\\"\\s*:\\s*\\[(?<items>.*?)\\]",
				RegexOptions.Singleline);
			Assert.That(array.Success, Is.True, propertyName);
			return Regex.Matches(array.Groups["items"].Value, "\\\"(?<value>[^\\\"]+)\\\"")
				.Cast<Match>().Select(match => match.Groups["value"].Value).ToArray();
		}

		private static string Normalize(string value) => value.Replace("\r\n", "\n");
	}
}
