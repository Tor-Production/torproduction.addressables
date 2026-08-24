using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TorProduction.Addressables.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TorProduction.Addressables.Editor.Tests {
	public sealed class PhaseThreeSceneSynchronizationPlannerTests {
		[TearDown]
		public void TearDown() => ScenePostprocessCoordinator.ResetForTests();

		[Test]
		public void Planner_AddAddressableScenePlansEntryAddressLabelsAndOwnership() {
			var state = ReadyState();
			var rule = AddressableRule();
			rule.RequiredLabels = new[] { "managed" };
			rule.Category = "gameplay";
			rule.Scenes.Add(Scene("scene-a", "Assets/Scenes/Level.unity"));
			state.Rules.Add(rule);

			var plan = SceneSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.True, Diagnostics(plan));
			Assert.That(plan.Operations.Select(item => item.Kind), Does.Contain(AutomationOperationKind.CreateEntry));
			Assert.That(plan.Operations.Single(item => item.Kind == AutomationOperationKind.SetAddress).Value, Is.EqualTo("Level"));
			Assert.That(plan.Operations.Count(item => item.Kind == AutomationOperationKind.CreateLabel), Is.EqualTo(2));
			Assert.That(plan.Operations.Select(item => item.Kind), Does.Contain(AutomationOperationKind.UpdateManagedScenes));
			Assert.That(plan.SceneMutation.ManagedScenes.Single().SceneGuid, Is.EqualTo("scene-a"));
		}

		[Test]
		public void Planner_DuplicateSceneNamesRemainDistinctByGuidAndRelativePath() {
			var state = ReadyState();
			var rule = AddressableRule();
			rule.Scenes.Add(Scene("scene-a", "Assets/Scenes/One/Main.unity"));
			rule.Scenes.Add(Scene("scene-b", "Assets/Scenes/Two/Main.unity"));
			state.Rules.Add(rule);

			var plan = SceneSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.True, Diagnostics(plan));
			Assert.That(plan.Operations.Where(item => item.Kind == AutomationOperationKind.SetAddress).Select(item => item.Value), Is.EquivalentTo(new[] { "One/Main", "Two/Main" }));
		}

		[Test]
		public void Planner_RenamePreservesManagedAddressAndUpdatesLastKnownPath() {
			var state = ReadyState();
			var rule = AddressableRule(SceneAddressPolicy.PreserveManagedAddress);
			rule.Scenes.Add(Scene("scene-a", "Assets/Scenes/Renamed.unity"));
			state.Rules.Add(rule);
			state.Entries.Add(Entry("scene-a", "Assets/Scenes/Renamed.unity", "stable/address"));
			state.ManagedScenes.Add(Record("scene-a", "Assets/Scenes/Original.unity", SceneFolderMode.Addressable, "stable/address"));

			var plan = SceneSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.True, Diagnostics(plan));
			Assert.That(plan.Operations.Any(item => item.Kind == AutomationOperationKind.SetAddress), Is.False);
			Assert.That(plan.Operations.Select(item => item.Kind), Is.EqualTo(new[] { AutomationOperationKind.UpdateManagedScenes }));
			Assert.That(plan.SceneMutation.ManagedScenes.Single().LastKnownPath, Is.EqualTo("Assets/Scenes/Renamed.unity"));
		}

		[Test]
		public void Planner_RelativePolicyRegeneratesAddressAfterRename() {
			var state = ReadyState();
			var rule = AddressableRule(SceneAddressPolicy.RelativePath);
			rule.Scenes.Add(Scene("scene-a", "Assets/Scenes/Renamed.unity"));
			state.Rules.Add(rule);
			state.Entries.Add(Entry("scene-a", "Assets/Scenes/Renamed.unity", "Original"));
			state.ManagedScenes.Add(Record("scene-a", "Assets/Scenes/Original.unity", SceneFolderMode.Addressable, "Original"));

			var plan = SceneSyncPlanner.Create(state);

			Assert.That(plan.Operations.Single(item => item.Kind == AutomationOperationKind.SetAddress).Value, Is.EqualTo("Renamed"));
		}

		[Test]
		public void Planner_LocalScenesUseConfiguredOrderAndPreserveUnrelatedBuildScenes() {
			var state = ReadyState();
			state.BuildScenes.Add(Build("unrelated", "Assets/Bootstrap.unity", false));
			state.BuildScenes.Add(Build("scene-b", "Assets/Local/B.unity", true));
			var rule = LocalRule();
			rule.Scenes.Add(Scene("scene-b", "Assets/Local/B.unity"));
			rule.Scenes.Add(Scene("scene-a", "Assets/Local/A.unity"));
			state.Rules.Add(rule);

			var plan = SceneSyncPlanner.Create(state);

			Assert.That(plan.SceneMutation.BuildScenes.Select(item => item.Guid), Is.EqualTo(new[] { "unrelated", "scene-a", "scene-b" }));
			Assert.That(plan.SceneMutation.BuildScenes[0].Enabled, Is.False);
			Assert.That(plan.Operations.Select(item => item.Kind), Does.Contain(AutomationOperationKind.UpdateBuildSettings));
		}

		[Test]
		public void Planner_AddressableToLocalTransitionRemovesOnlyManagedEntry() {
			var state = ReadyState();
			var rule = LocalRule();
			rule.Scenes.Add(Scene("scene-a", "Assets/Local/A.unity"));
			state.Rules.Add(rule);
			state.Entries.Add(Entry("scene-a", "Assets/Local/A.unity", "A"));
			state.ManagedScenes.Add(Record("scene-a", "Assets/Scenes/A.unity", SceneFolderMode.Addressable, "A"));

			var plan = SceneSyncPlanner.Create(state);

			Assert.That(plan.Operations.Select(item => item.Kind), Does.Contain(AutomationOperationKind.RemoveEntry));
			Assert.That(plan.SceneMutation.BuildScenes.Select(item => item.Guid), Does.Contain("scene-a"));
		}

		[Test]
		public void Planner_LocalToAddressableTransitionRemovesManagedBuildScene() {
			var state = ReadyState();
			var rule = AddressableRule();
			rule.Scenes.Add(Scene("scene-a", "Assets/Scenes/A.unity"));
			state.Rules.Add(rule);
			state.BuildScenes.Add(Build("scene-a", "Assets/Local/A.unity", true));
			state.ManagedScenes.Add(Record("scene-a", "Assets/Local/A.unity", SceneFolderMode.LocalBuildSettings));

			var plan = SceneSyncPlanner.Create(state);

			Assert.That(plan.Operations.Select(item => item.Kind), Does.Contain(AutomationOperationKind.CreateEntry));
			Assert.That(plan.Operations.Select(item => item.Kind), Does.Contain(AutomationOperationKind.UpdateBuildSettings));
			Assert.That(plan.SceneMutation.BuildScenes, Is.Empty);
		}

		[Test]
		public void Planner_DeletedManagedScenesAreRemovedWithoutTouchingUnrelatedState() {
			var state = ReadyState();
			state.Entries.Add(Entry("addressable", "Assets/Gone.unity", "gone"));
			state.Entries.Add(Entry("unrelated", "Assets/Other.unity", "other"));
			state.BuildScenes.Add(Build(string.Empty, "Assets/LocalGone.unity", true));
			state.BuildScenes.Add(Build("build-unrelated", "Assets/Bootstrap.unity", true));
			state.ManagedScenes.Add(Record("addressable", "Assets/Gone.unity", SceneFolderMode.Addressable, "gone"));
			state.ManagedScenes.Add(Record("local", "Assets/LocalGone.unity", SceneFolderMode.LocalBuildSettings));

			var plan = SceneSyncPlanner.Create(state);

			Assert.That(plan.Operations.Count(item => item.Kind == AutomationOperationKind.RemoveEntry), Is.EqualTo(1));
			Assert.That(plan.Operations.Single(item => item.Kind == AutomationOperationKind.RemoveEntry).AssetGuid, Is.EqualTo("addressable"));
			Assert.That(plan.SceneMutation.BuildScenes.Select(item => item.Guid), Is.EqualTo(new[] { "build-unrelated" }));
			Assert.That(plan.SceneMutation.ManagedScenes, Is.Empty);
		}

		[Test]
		public void Planner_ConflictingOverlappingClaimsFailClosed() {
			var state = ReadyState();
			var addressable = AddressableRule();
			addressable.Scenes.Add(Scene("scene-a", "Assets/Scenes/A.unity"));
			var local = LocalRule();
			local.Index = 1;
			local.Scenes.Add(Scene("scene-a", "Assets/Scenes/A.unity"));
			state.Rules.Add(addressable);
			state.Rules.Add(local);

			var plan = SceneSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.SceneClaimConflict), Is.True);
		}

		[Test]
		public void Planner_AddressCollisionWithUnrelatedEntryFailsClosed() {
			var state = ReadyState();
			var rule = AddressableRule();
			rule.Scenes.Add(Scene("scene-a", "Assets/Scenes/A.unity"));
			state.Rules.Add(rule);
			state.Entries.Add(new SceneSyncEntryState { Guid = "other", Path = "Assets/Other.unity", GroupGuid = "target-guid", GroupName = "Target", Address = "A" });

			var plan = SceneSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.AddressCollision), Is.True);
		}

		[Test]
		public void Planner_IsDeterministicAcrossInputOrdering() {
			var first = ReadyState();
			var firstRule = AddressableRule();
			firstRule.Scenes.Add(Scene("b", "Assets/Scenes/B.unity"));
			firstRule.Scenes.Add(Scene("a", "Assets/Scenes/A.unity"));
			first.Rules.Add(firstRule);
			var second = ReadyState();
			var secondRule = AddressableRule();
			secondRule.Scenes.Add(Scene("a", "Assets/Scenes/A.unity"));
			secondRule.Scenes.Add(Scene("b", "Assets/Scenes/B.unity"));
			second.Rules.Add(secondRule);

			var firstPlan = SceneSyncPlanner.Create(first);
			var secondPlan = SceneSyncPlanner.Create(second);

			Assert.That(secondPlan.SourceHash, Is.EqualTo(firstPlan.SourceHash));
			Assert.That(secondPlan.PlanHash, Is.EqualTo(firstPlan.PlanHash));
			Assert.That(secondPlan.Operations.Select(item => item.Description), Is.EqualTo(firstPlan.Operations.Select(item => item.Description)));
		}

		[Test]
		public void PostprocessCoordinator_FiltersCoalescesAndSuppressesRecursion() {
			var scheduled = new List<Action>();
			var reconciliations = 0;
			Assert.That(ScenePostprocessCoordinator.Notify(new[] { "Assets/Icon.png" }, scheduled.Add, () => reconciliations++, null), Is.False);
			Assert.That(ScenePostprocessCoordinator.Notify(new[] { "Assets/A.unity" }, scheduled.Add, () => {
				reconciliations++;
				ScenePostprocessCoordinator.Notify(new[] { "Assets/B.unity" }, scheduled.Add, () => reconciliations++, null);
			}, null), Is.True);
			Assert.That(ScenePostprocessCoordinator.Notify(new[] { "Assets/C.UNITY" }, scheduled.Add, () => reconciliations++, null), Is.True);
			Assert.That(scheduled, Has.Count.EqualTo(1));

			scheduled[0]();

			Assert.That(reconciliations, Is.EqualTo(1));
			Assert.That(scheduled, Has.Count.EqualTo(1));
		}

		[Test]
		public void SceneCli_ReportsSuccessAndThrowsForBlockingAnalysisOrFailedApply() {
			var valid = new AutomationPlan(AutomationScope.Scenes, "source", "plan", Array.Empty<AutomationOperation>(), Array.Empty<AutomationDiagnostic>(), null, new SceneSyncMutation(Array.Empty<SceneBuildState>(), Array.Empty<ManagedSceneRecord>()));
			var invalid = new AutomationPlan(AutomationScope.Scenes, "source", "plan", Array.Empty<AutomationOperation>(), new[] { new AutomationDiagnostic(AutomationDiagnosticCode.ConfigurationInvalid, AutomationDiagnosticSeverity.Error, "Config", "invalid") }, null);
			var output = new List<string>();

			Assert.DoesNotThrow(() => AddressablesAutomationCli.RunAnalyzeScenes(() => valid, output.Add));
			Assert.Throws<InvalidOperationException>(() => AddressablesAutomationCli.RunAnalyzeScenes(() => invalid, output.Add));
			Assert.Throws<InvalidOperationException>(() => AddressablesAutomationCli.RunApplyScenes(() => valid, _ => new AutomationReport(false, Array.Empty<AutomationOperation>(), Array.Empty<AutomationDiagnostic>(), new[] { "failed" }, AutomationRollbackStatus.NotRequired, string.Empty), output.Add));
			Assert.That(output, Is.Not.Empty);
		}

		[Test]
		public void ConfigurationSchemaOneMigratesManagedSceneCollectionExplicitly() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				var serialized = new SerializedObject(config);
				serialized.FindProperty("m_schemaVersion").intValue = 1;
				serialized.FindProperty("m_managedScenes").arraySize = 0;
				serialized.ApplyModifiedPropertiesWithoutUndo();

				Assert.That(config.TryMigrateToCurrentSchema(out var error), Is.True, error);
				Assert.That(config.SchemaVersion, Is.EqualTo(AddressablesAutomationConfig.CurrentSchemaVersion));
				Assert.That(config.ManagedScenes, Is.Empty);
			} finally { UnityEngine.Object.DestroyImmediate(config); }
		}

		[Test]
		public void PublicAnalyze_MissingSceneConfigurationFailsWithoutMutation() {
			var plan = AddressablesAutomation.Analyze(null, AutomationScope.Scenes);

			Assert.That(plan.Scope, Is.EqualTo(AutomationScope.Scenes));
			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.ConfigurationInvalid), Is.True);
		}

		private static SceneSyncProjectState ReadyState() {
			var state = new SceneSyncProjectState { SettingsExist = true, SettingsIdentity = "settings", ConfigJson = "{}" };
			state.Groups.Add(new SceneSyncGroupState { Guid = "target-guid", Name = "Target", HasBundledSchema = true, HasContentUpdateSchema = true, IsBuildable = true });
			return state;
		}

		private static SceneSyncRuleState AddressableRule(SceneAddressPolicy policy = SceneAddressPolicy.RelativePath) => new SceneSyncRuleState { Index = 0, SourceFolderPath = "Assets/Scenes", Mode = SceneFolderMode.Addressable, DestinationGroupGuid = "target-guid", DestinationGroupName = "Target", AddressPolicy = policy, RequiredLabels = Array.Empty<string>() };
		private static SceneSyncRuleState LocalRule() => new SceneSyncRuleState { Index = 0, SourceFolderPath = "Assets/Local", Mode = SceneFolderMode.LocalBuildSettings, AddressPolicy = SceneAddressPolicy.RelativePath, RequiredLabels = Array.Empty<string>() };
		private static SceneAssetState Scene(string guid, string path) => new SceneAssetState { Guid = guid, Path = path };
		private static SceneSyncEntryState Entry(string guid, string path, string address) => new SceneSyncEntryState { Guid = guid, Path = path, GroupGuid = "target-guid", GroupName = "Target", Address = address };
		private static SceneBuildState Build(string guid, string path, bool enabled) => new SceneBuildState { Guid = guid, Path = path, Enabled = enabled };
		private static ManagedSceneRecord Record(string guid, string path, SceneFolderMode mode, string address = "") => new ManagedSceneRecord(guid, path, mode, address, mode == SceneFolderMode.Addressable ? "target-guid" : string.Empty, mode == SceneFolderMode.Addressable ? "Target" : string.Empty, Array.Empty<string>());
		private static string Diagnostics(AutomationPlan plan) => string.Join(" | ", plan.Diagnostics.Select(item => $"{item.Severity}:{item.Code}:{item.Location}:{item.Message}"));
	}

	public sealed class PhaseThreeSceneSynchronizationIntegrationTests {
		private string m_root;
		private string m_addressableScenePath;
		private string m_localScenePath;
		private string m_addressableGuid;
		private string m_localGuid;
		private AddressableAssetSettings m_settings;
		private AddressableAssetSettings m_originalDefaultSettings;
		private EditorBuildSettingsScene[] m_originalBuildScenes;
		private bool m_createdDefaultFolder;

		[SetUp]
		public void SetUp() {
			m_originalBuildScenes = EditorBuildSettings.scenes;
			m_originalDefaultSettings = AddressableAssetSettingsDefaultObject.SettingsExists ? AddressableAssetSettingsDefaultObject.GetSettings(false) : null;
			m_root = "Assets/__TorProductionPhase3_" + Guid.NewGuid().ToString("N");
			Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(m_root)), Is.Not.Empty);
			Assert.That(AssetDatabase.CreateFolder(m_root, "Addressable"), Is.Not.Empty);
			Assert.That(AssetDatabase.CreateFolder(m_root, "Local"), Is.Not.Empty);
			Assert.That(AssetDatabase.CreateFolder(m_root, "Editor"), Is.Not.Empty);
			m_addressableScenePath = m_root + "/Addressable/Level.unity";
			m_localScenePath = m_root + "/Local/Menu.unity";
			CreateScene(m_addressableScenePath);
			CreateScene(m_localScenePath);
			m_addressableGuid = AssetDatabase.AssetPathToGUID(m_addressableScenePath);
			m_localGuid = AssetDatabase.AssetPathToGUID(m_localScenePath);
			m_settings = AddressableAssetSettings.Create(m_root + "/AddressableAssetsData", "AddressableAssetSettings", true, true);
			if (!AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder)) {
				m_createdDefaultFolder = true;
				Assert.That(AssetDatabase.CreateFolder("Assets", "AddressableAssetsData"), Is.Not.Empty);
			}
			AddressableAssetSettingsDefaultObject.Settings = m_settings;
			EditorBuildSettings.scenes = m_originalBuildScenes.Concat(new[] { new EditorBuildSettingsScene(m_root + "/Unrelated.unity", false) }).ToArray();
		}

		[TearDown]
		public void TearDown() {
			EditorBuildSettings.scenes = m_originalBuildScenes ?? Array.Empty<EditorBuildSettingsScene>();
			if (m_originalDefaultSettings != null) AddressableAssetSettingsDefaultObject.Settings = m_originalDefaultSettings;
			else {
				AddressableAssetSettingsDefaultObject.Settings = null;
				EditorBuildSettings.RemoveConfigObject(AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName);
			}
			if (m_createdDefaultFolder && AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder)) AssetDatabase.DeleteAsset(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
			if (!string.IsNullOrEmpty(m_root) && AssetDatabase.IsValidFolder(m_root)) AssetDatabase.DeleteAsset(m_root);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		[Test]
		public void PublicApply_ReconcilesAddRenameMoveDeletePersistsAndConverges() {
			var config = CreateConfig();
			var first = AddressablesAutomation.Analyze(config, AutomationScope.Scenes);
			var firstReport = AddressablesAutomation.Apply(first);
			Assert.That(first.IsValid, Is.True, Diagnostics(first));
			Assert.That(firstReport.Succeeded, Is.True, string.Join(" | ", firstReport.Failures));
			Assert.That(m_settings.FindAssetEntry(m_addressableGuid), Is.Not.Null);
			Assert.That(EditorBuildSettings.scenes.Any(item => item.path == m_localScenePath), Is.True);
			Assert.That(config.ManagedScenes.Count, Is.EqualTo(2));
			var originalAddress = m_settings.FindAssetEntry(m_addressableGuid).address;

			var configPath = AssetDatabase.GetAssetPath(config);
			AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceUpdate);
			config = AssetDatabase.LoadAssetAtPath<AddressablesAutomationConfig>(configPath);
			Assert.That(config.ManagedScenes.Select(item => item.SceneGuid), Is.EquivalentTo(new[] { m_addressableGuid, m_localGuid }));
			Assert.That(AddressablesAutomation.Analyze(config, AutomationScope.Scenes).Operations, Is.Empty);

			var renamed = m_root + "/Addressable/Renamed.unity";
			Assert.That(AssetDatabase.MoveAsset(m_addressableScenePath, renamed), Is.Empty);
			m_addressableScenePath = renamed;
			var renameReport = AddressablesAutomation.Apply(AddressablesAutomation.Analyze(config, AutomationScope.Scenes));
			Assert.That(renameReport.Succeeded, Is.True, string.Join(" | ", renameReport.Failures));
			Assert.That(m_settings.FindAssetEntry(m_addressableGuid).address, Is.EqualTo(originalAddress));

			var movedToLocal = m_root + "/Local/Renamed.unity";
			Assert.That(AssetDatabase.MoveAsset(m_addressableScenePath, movedToLocal), Is.Empty);
			m_addressableScenePath = movedToLocal;
			var transitionReport = AddressablesAutomation.Apply(AddressablesAutomation.Analyze(config, AutomationScope.Scenes));
			Assert.That(transitionReport.Succeeded, Is.True, string.Join(" | ", transitionReport.Failures));
			Assert.That(m_settings.FindAssetEntry(m_addressableGuid), Is.Null);
			Assert.That(EditorBuildSettings.scenes.Any(item => item.path == movedToLocal), Is.True);

			Assert.That(AssetDatabase.DeleteAsset(movedToLocal), Is.True);
			var deleteReport = AddressablesAutomation.Apply(AddressablesAutomation.Analyze(config, AutomationScope.Scenes));
			Assert.That(deleteReport.Succeeded, Is.True, string.Join(" | ", deleteReport.Failures));
			Assert.That(EditorBuildSettings.scenes.Any(item => item.path == movedToLocal), Is.False);
			Assert.That(config.ManagedScenes.Any(item => item.SceneGuid == m_addressableGuid), Is.False);
			Assert.That(AddressablesAutomation.Analyze(config, AutomationScope.Scenes).Operations, Is.Empty);
		}

		[Test]
		public void PublicApply_RejectsStaleScenePlanBeforeMutation() {
			var config = CreateConfig();
			var plan = AddressablesAutomation.Analyze(config, AutomationScope.Scenes);
			CreateScene(m_root + "/Addressable/AddedAfterPreview.unity");

			var report = AddressablesAutomation.Apply(plan);

			Assert.That(report.Succeeded, Is.False);
			Assert.That(
				report.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.StalePlan),
				Is.True,
				Diagnostics(plan) + " => " + string.Join(" | ", report.Diagnostics.Select(item => $"{item.Severity}:{item.Code}:{item.Location}:{item.Message}")));
			Assert.That(m_settings.FindGroup("Managed Scenes"), Is.Null);
		}

		[Test]
		public void PublicRecovery_RestoresActualPendingSceneSnapshot() {
			var config = CreateConfig();
			var desiredBuild = new[] { new SceneBuildState { Guid = m_localGuid, Path = m_localScenePath, Enabled = true } };
			var desiredRecords = new[] { new ManagedSceneRecord(m_localGuid, m_localScenePath, SceneFolderMode.LocalBuildSettings, string.Empty, string.Empty, string.Empty, Array.Empty<string>()) };
			var plan = new AutomationPlan(AutomationScope.Scenes, "source", "plan", new[] { new AutomationOperation(AutomationOperationKind.UpdateBuildSettings), new AutomationOperation(AutomationOperationKind.UpdateManagedScenes) }, Array.Empty<AutomationDiagnostic>(), config, new SceneSyncMutation(desiredBuild, desiredRecords));
			var backend = new UnityGroupSyncMutationBackend(m_settings);
			backend.Begin(plan);
			foreach (var operation in plan.Operations) backend.Execute(operation);
			backend.Commit();
			Assert.That(File.Exists(backend.RecoveryPath), Is.True);
			Assert.That(config.ManagedScenes.Count, Is.EqualTo(1));

			var report = AddressablesAutomation.Recover();

			Assert.That(report.Succeeded, Is.True, string.Join(" | ", report.Failures));
			Assert.That(report.RollbackStatus, Is.EqualTo(AutomationRollbackStatus.Succeeded));
			Assert.That(config.ManagedScenes, Is.Empty);
			Assert.That(EditorBuildSettings.scenes.Select(item => item.path), Is.EqualTo(m_originalBuildScenes.Select(item => item.path).Concat(new[] { m_root + "/Unrelated.unity" })));
			Assert.That(File.Exists(backend.RecoveryPath), Is.False);
		}

		private AddressablesAutomationConfig CreateConfig() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			config.ReplaceWithCurrentSchema(Array.Empty<GroupSyncRule>(), new[] {
				new SceneFolderRule(AssetDatabase.AssetPathToGUID(m_root + "/Addressable"), Array.Empty<string>(), SceneFolderMode.Addressable, string.Empty, "Managed Scenes", "gameplay", "scenes", SceneAddressPolicy.PreserveManagedAddress, new[] { "managed-scene" }),
				new SceneFolderRule(AssetDatabase.AssetPathToGUID(m_root + "/Local"), Array.Empty<string>(), SceneFolderMode.LocalBuildSettings, string.Empty, string.Empty, "menu", string.Empty, SceneAddressPolicy.RelativePath, Array.Empty<string>())
			});
			AssetDatabase.CreateAsset(config, m_root + "/Editor/AutomationConfig.asset");
			AssetDatabase.SaveAssets();
			return config;
		}

		private static void CreateScene(string path) {
			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
		}

		private static string Diagnostics(AutomationPlan plan) => string.Join(" | ", plan.Diagnostics.Select(item => $"{item.Severity}:{item.Code}:{item.Location}:{item.Message}"));
	}
}
