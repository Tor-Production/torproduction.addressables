using System;
using System.Linq;
using NUnit.Framework;
using TorProduction.Addressables.Editor;
using TorProduction.AddressablesToolpack.Editor.Menu;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Tests {
	public sealed class PhaseFourDependencyAnalysisTests {
		[Test]
		public void Planner_ImplicitDuplicatesPlanMissingGroupSchemasAndEntries() {
			var state = ReadyState();
			state.Assets.Add(Asset("asset-a", "Assets/Shared/A.asset"));
			state.Assets.Add(Asset("asset-b", "Assets/Shared/B.asset"));

			var plan = DependencyAnalysisPlanner.Create(state);

			Assert.That(plan.IsValid, Is.True, FormatDiagnostics(plan));
			Assert.That(plan.Scope, Is.EqualTo(AutomationScope.Dependencies));
			Assert.That(plan.Operations.Count(item => item.Kind == AutomationOperationKind.CreateGroup), Is.EqualTo(1));
			Assert.That(plan.Operations.Count(item => item.Kind == AutomationOperationKind.AddBundledAssetGroupSchema), Is.EqualTo(1));
			Assert.That(plan.Operations.Count(item => item.Kind == AutomationOperationKind.AddContentUpdateGroupSchema), Is.EqualTo(1));
			Assert.That(plan.Operations.Count(item => item.Kind == AutomationOperationKind.CreateEntry), Is.EqualTo(2));
			Assert.That(plan.Operations.Where(item => item.Kind == AutomationOperationKind.CreateEntry)
				.Select(item => item.GroupName), Is.All.EqualTo("Shared Dependencies"));
		}

		[Test]
		public void Planner_AlreadyExplicitEntriesAreReportOnlyAndNeverMoved() {
			var state = ReadyState();
			var asset = Asset("asset-a", "Assets/Shared/A.asset");
			asset.IsExplicit = true;
			asset.ExplicitGroupGuid = "explicit-guid";
			asset.ExplicitGroupName = "Already Addressable";
			state.Assets.Add(asset);

			var plan = DependencyAnalysisPlanner.Create(state);

			Assert.That(plan.IsValid, Is.True, FormatDiagnostics(plan));
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Any(item =>
				item.Code == AutomationDiagnosticCode.DependencyAlreadyExplicit &&
				item.Severity == AutomationDiagnosticSeverity.Info), Is.True);
		}

		[Test]
		public void Planner_MissingSettingsFailsWithoutMutationPlan() {
			var state = ReadyState();
			state.SettingsExist = false;
			state.Assets.Add(Asset("asset-a", "Assets/Shared/A.asset"));

			var plan = DependencyAnalysisPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Any(item =>
				item.Code == AutomationDiagnosticCode.AddressablesSettingsMissing), Is.True);
		}

		[Test]
		public void Planner_ExistingGroupAddsOnlyMissingSchemasBeforeEntry() {
			var state = ReadyState();
			state.DestinationGroup = new GroupSyncGroupState {
				Guid = "destination-guid",
				Name = "Shared Dependencies",
				HasBundledSchema = false,
				HasContentUpdateSchema = false,
				IsBuildable = true
			};
			state.Assets.Add(Asset("asset-a", "Assets/Shared/A.asset"));

			var plan = DependencyAnalysisPlanner.Create(state);

			Assert.That(plan.IsValid, Is.True, FormatDiagnostics(plan));
			Assert.That(plan.Operations.Any(item => item.Kind == AutomationOperationKind.CreateGroup), Is.False);
			Assert.That(plan.Operations.Select(item => item.Kind), Is.EqualTo(new[] {
				AutomationOperationKind.AddBundledAssetGroupSchema,
				AutomationOperationKind.AddContentUpdateGroupSchema,
				AutomationOperationKind.CreateEntry
			}));
		}

		[Test]
		public void Planner_ReadOnlyDestinationFailsClosed() {
			var state = ReadyState();
			state.DestinationGroup = new GroupSyncGroupState {
				Guid = "destination-guid",
				Name = "Shared Dependencies",
				ReadOnly = true,
				HasBundledSchema = true,
				HasContentUpdateSchema = true,
				IsBuildable = true
			};
			state.Assets.Add(Asset("asset-a", "Assets/Shared/A.asset"));

			var plan = DependencyAnalysisPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Any(item =>
				item.Code == AutomationDiagnosticCode.DestinationGroupReadOnly), Is.True);
		}

		[Test]
		public void UnsupportedVersionDisablesFixWithActionableDiagnostic() {
			Assert.That(AddressablesDuplicateDependencyAdapter.IsVerifiedVersion("2.7.6"), Is.True);
			Assert.That(AddressablesDuplicateDependencyAdapter.IsVerifiedVersion("2.9.1"), Is.True);
			Assert.That(AddressablesDuplicateDependencyAdapter.IsVerifiedVersion("2.8.0"), Is.False);
			var state = ReadyState();
			state.AdapterVersion = "2.8.0";
			state.AdapterSupported = false;
			state.AnalysisSucceeded = false;
			state.AdapterDiagnostic =
				"Addressables 2.8.0 is unverified. Fix is disabled; use a verified version or validate a dedicated adapter.";

			var plan = DependencyAnalysisPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			var diagnostic = plan.Diagnostics.Single(item =>
				item.Code == AutomationDiagnosticCode.DependencyAdapterUnsupported);
			StringAssert.Contains("Fix is disabled", diagnostic.Message);
		}

		[Test]
		public void AnalyzerFailureFailsClosedWithoutOperations() {
			var state = ReadyState();
			state.AnalysisSucceeded = false;
			state.AdapterDiagnostic = "Analyzer build failed before results were available.";
			state.Assets.Add(Asset("asset-a", "Assets/Shared/A.asset"));

			var plan = DependencyAnalysisPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Any(item =>
				item.Code == AutomationDiagnosticCode.DependencyAnalysisFailed), Is.True);
		}

		[Test]
		public void Planner_IsDeterministicAndDoesNotMutateCapturedState() {
			var state = ReadyState();
			state.Assets.Add(Asset("asset-b", "Assets/Shared/B.asset"));
			state.Assets.Add(Asset("asset-a", "Assets/Shared/A.asset"));
			var beforeHash = state.ComputeHash();
			var beforeOrder = state.Assets.Select(item => item.Guid).ToArray();

			var first = DependencyAnalysisPlanner.Create(state);
			var second = DependencyAnalysisPlanner.Create(state);

			Assert.That(state.ComputeHash(), Is.EqualTo(beforeHash));
			Assert.That(state.Assets.Select(item => item.Guid), Is.EqualTo(beforeOrder));
			Assert.That(first.SourceHash, Is.EqualTo(second.SourceHash));
			Assert.That(first.PlanHash, Is.EqualTo(second.PlanHash));
			Assert.That(first.Operations.Select(item => item.AssetGuid),
				Is.EqualTo(second.Operations.Select(item => item.AssetGuid)));
		}

		[Test]
		public void FixRequiresSeparateExplicitConfirmation() {
			var state = ReadyState();
			state.Assets.Add(Asset("asset-a", "Assets/Shared/A.asset"));
			var plan = DependencyAnalysisPlanner.Create(state);

			var report = DependencyResolverController.Fix(plan, false);

			Assert.That(report.Succeeded, Is.False);
			Assert.That(report.Operations, Is.Empty);
			Assert.That(report.Diagnostics.Single().Code,
				Is.EqualTo(AutomationDiagnosticCode.DependencyFixConfirmationRequired));
		}

		[Test]
		public void AdapterDirectFixLifecycleIsDisabled() {
			var adapter = new AddressablesDuplicateDependencyAdapter("2.7.6");

			Assert.That(adapter.CanFix, Is.False);
			Assert.Throws<InvalidOperationException>(() => adapter.FixIssues(null));
		}

		[Test]
		public void SchemaTwoMigrationInitializesDependencySettings() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				var serialized = new SerializedObject(config);
				serialized.FindProperty("m_schemaVersion").intValue = 2;
				serialized.ApplyModifiedPropertiesWithoutUndo();

				Assert.That(config.TryMigrateToCurrentSchema(out var error), Is.True, error);
				Assert.That(config.SchemaVersion, Is.EqualTo(AddressablesAutomationConfig.CurrentSchemaVersion));
				Assert.That(config.DependencySettings, Is.Not.Null);
				Assert.That(config.DependencySettings.DestinationGroupName,
					Is.EqualTo(DependencyAnalysisSettings.DefaultDestinationGroupName));
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		private static DependencyAnalysisProjectState ReadyState() {
			return new DependencyAnalysisProjectState {
				SettingsExist = true,
				SettingsIdentity = "settings-guid",
				DestinationGroupName = "Shared Dependencies",
				AdapterVersion = "2.7.6",
				AdapterSupported = true,
				AnalysisSucceeded = true,
				AdapterDiagnostic = "Addressables 2.7.6 duplicate-dependency analysis is verified."
			};
		}

		private static DependencyAnalysisAssetState Asset(string guid, string path) {
			return new DependencyAnalysisAssetState {
				Guid = guid,
				Path = path,
				ReferencingGroupGuids = new[] { "source-a", "source-b" },
				ReferencingGroupNames = new[] { "Source A", "Source B" }
			};
		}

		private static string FormatDiagnostics(AutomationPlan plan) {
			return string.Join(" | ", plan.Diagnostics.Select(item =>
				$"{item.Severity}:{item.Code}:{item.Location}:{item.Message}"));
		}
	}

	public sealed class PhaseFourDependencyAnalysisIntegrationTests {
		private string m_root;
		private string m_sharedTextureGuid;
		private AddressableAssetSettings m_settings;
		private AddressableAssetSettings m_originalDefaultSettings;
		private AddressablesAutomationConfig m_config;
		private bool m_createdDefaultFolder;

		[SetUp]
		public void SetUp() {
			m_createdDefaultFolder = false;
			m_originalDefaultSettings = AddressableAssetSettingsDefaultObject.SettingsExists
				? AddressableAssetSettingsDefaultObject.GetSettings(false)
				: null;
			m_root = "Assets/__TorProductionPhase4_" + Guid.NewGuid().ToString("N");
			Assert.That(AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(m_root)), Is.Not.Empty);
			Assert.That(AssetDatabase.CreateFolder(m_root, "Content"), Is.Not.Empty);
			Assert.That(AssetDatabase.CreateFolder(m_root, "Editor"), Is.Not.Empty);

			m_settings = AddressableAssetSettings.Create(
				m_root + "/AddressableAssetsData", "AddressableAssetSettings", true, true);
			Assert.That(m_settings, Is.Not.Null);
			if (!AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder)) {
				m_createdDefaultFolder = true;
				Assert.That(AssetDatabase.CreateFolder("Assets", "AddressableAssetsData"), Is.Not.Empty);
			}
			AddressableAssetSettingsDefaultObject.Settings = m_settings;

			var texturePath = m_root + "/Content/SharedTexture.asset";
			var texture = new Texture2D(2, 2);
			texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
			texture.Apply();
			AssetDatabase.CreateAsset(texture, texturePath);
			m_sharedTextureGuid = AssetDatabase.AssetPathToGUID(texturePath);

			var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
			Assert.That(shader, Is.Not.Null, "A built-in texture shader is required for the fixture.");
			var materialA = new Material(shader) { mainTexture = texture };
			var materialB = new Material(shader) { mainTexture = texture };
			var materialAPath = m_root + "/Content/MaterialA.mat";
			var materialBPath = m_root + "/Content/MaterialB.mat";
			AssetDatabase.CreateAsset(materialA, materialAPath);
			AssetDatabase.CreateAsset(materialB, materialBPath);

			var groupA = m_settings.CreateGroup(
				"Phase 4 Source A", false, false, false, null,
				typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
			var groupB = m_settings.CreateGroup(
				"Phase 4 Source B", false, false, false, null,
				typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
			Assert.That(groupA, Is.Not.Null);
			Assert.That(groupB, Is.Not.Null);
			Assert.That(m_settings.CreateOrMoveEntry(
				AssetDatabase.AssetPathToGUID(materialAPath), groupA, false, false), Is.Not.Null);
			Assert.That(m_settings.CreateOrMoveEntry(
				AssetDatabase.AssetPathToGUID(materialBPath), groupB, false, false), Is.Not.Null);

			m_config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			m_config.ReplaceWithCurrentSchema(
				Array.Empty<GroupSyncRule>(),
				Array.Empty<SceneFolderRule>(),
				dependencySettings: new DependencyAnalysisSettings(string.Empty, "Phase 4 Shared Dependencies"));
			AssetDatabase.CreateAsset(m_config, m_root + "/Editor/AutomationConfig.asset");
			m_settings.SetDirty(
				AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		[TearDown]
		public void TearDown() {
			if (m_originalDefaultSettings != null) {
				AddressableAssetSettingsDefaultObject.Settings = m_originalDefaultSettings;
			} else {
				AddressableAssetSettingsDefaultObject.Settings = null;
				EditorBuildSettings.RemoveConfigObject(AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName);
			}
			if (m_createdDefaultFolder &&
			    AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder)) {
				AssetDatabase.DeleteAsset(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
			}
			if (!string.IsNullOrEmpty(m_root) && AssetDatabase.IsValidFolder(m_root)) {
				AssetDatabase.DeleteAsset(m_root);
			}
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		[Test]
		public void BuiltInAnalyzeIsImmutableAndConfirmedFixIsIdempotent() {
			var beforeSettings = EditorJsonUtility.ToJson(m_settings, false);
			var beforeGroupCount = m_settings.groups.Count;
			var beforeExplicitEntry = m_settings.FindAssetEntry(m_sharedTextureGuid, false);

			var first = DependencyResolverController.Analyze(m_config);

			Assert.That(first.IsValid, Is.True, FormatDiagnostics(first));
			Assert.That(first.Operations.Any(item =>
				item.Kind == AutomationOperationKind.CreateEntry &&
				item.AssetGuid == m_sharedTextureGuid), Is.True, FormatDiagnostics(first));
			Assert.That(m_settings.groups.Count, Is.EqualTo(beforeGroupCount));
			Assert.That(m_settings.FindGroup("Phase 4 Shared Dependencies"), Is.Null);
			Assert.That(m_settings.FindAssetEntry(m_sharedTextureGuid, false), Is.SameAs(beforeExplicitEntry));
			Assert.That(EditorJsonUtility.ToJson(m_settings, false), Is.EqualTo(beforeSettings));

			var unconfirmed = DependencyResolverController.Fix(first, false);
			Assert.That(unconfirmed.Succeeded, Is.False);
			Assert.That(m_settings.FindGroup("Phase 4 Shared Dependencies"), Is.Null);

			var fixedReport = DependencyResolverController.Fix(first, true);
			Assert.That(fixedReport.Succeeded, Is.True, string.Join(" | ", fixedReport.Failures));
			var destination = m_settings.FindGroup("Phase 4 Shared Dependencies");
			Assert.That(destination, Is.Not.Null);
			Assert.That(destination.GetSchema<BundledAssetGroupSchema>(), Is.Not.Null);
			Assert.That(destination.GetSchema<ContentUpdateGroupSchema>(), Is.Not.Null);
			Assert.That(m_settings.FindAssetEntry(m_sharedTextureGuid, false).parentGroup, Is.SameAs(destination));

			var second = DependencyResolverController.Analyze(m_config);
			Assert.That(second.IsValid, Is.True, FormatDiagnostics(second));
			Assert.That(second.Operations, Is.Empty, FormatDiagnostics(second));
			var secondFix = DependencyResolverController.Fix(second, true);
			Assert.That(secondFix.Succeeded, Is.True, string.Join(" | ", secondFix.Failures));
			Assert.That(secondFix.Operations, Is.Empty);
		}

		private static string FormatDiagnostics(AutomationPlan plan) {
			return string.Join(" | ", plan.Diagnostics.Select(item =>
				$"{item.Severity}:{item.Code}:{item.Location}:{item.Message}"));
		}
	}
}
