using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TorProduction.Addressables.Editor;
using TorProduction.AddressablesToolpack.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Tests {
	public sealed class PhaseTwoGroupSynchronizationTests {
		[Test]
		public void Planner_NullFiltersAndLabelsTreatsAllLoadableAssetsAsCandidates() {
			var state = ReadyState();
			var rule = Rule();
			rule.RequiredLabels = null;
			rule.TypeFilterNames = null;
			rule.ResolvedTypes = null;
			rule.Assets.Add(Asset("asset-a", "Assets/Content/A.asset"));
			state.Rules.Add(rule);

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.True);
			Assert.That(plan.Operations.Select(item => item.Kind), Does.Contain(AutomationOperationKind.CreateEntry));
			Assert.That(plan.Operations.Single(item => item.Kind == AutomationOperationKind.SetAddress).Value, Is.EqualTo("A"));
		}

		[Test]
		public void Planner_MissingSettingsFailsWithoutOperations() {
			var state = ReadyState();
			state.SettingsExist = false;
			state.Rules.Add(Rule());

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.AddressablesSettingsMissing), Is.True);
		}

		[Test]
		public void Planner_MissingFolderFailsWithoutOperations() {
			var state = ReadyState();
			var rule = Rule();
			rule.SourceFolderPath = string.Empty;
			state.Rules.Add(rule);

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.SourceFolderMissing), Is.True);
		}

		[Test]
		public void Planner_UnresolvedTypeFailsClosed() {
			var state = ReadyState();
			state.Diagnostics.Add(new AutomationDiagnostic(
				AutomationDiagnosticCode.TypeFilterUnresolved,
				AutomationDiagnosticSeverity.Error,
				"Groups[0].Types[0]",
				"Missing type"));
			var rule = Rule();
			rule.TypeFilterNames = new[] { "Missing.Type, Missing.Assembly" };
			rule.ResolvedTypes = Array.Empty<Type>();
			rule.Assets.Add(Asset("asset-a", "Assets/Content/A.asset"));
			state.Rules.Add(rule);

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
		}

		[Test]
		public void Planner_RelativeAddressesMakeDuplicateFilenamesUnique() {
			var state = ReadyState();
			var rule = Rule();
			rule.Assets.Add(Asset("asset-a", "Assets/Content/One/Icon.png"));
			rule.Assets.Add(Asset("asset-b", "Assets/Content/Two/Icon.png"));
			state.Rules.Add(rule);

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.True);
			Assert.That(
				plan.Operations.Where(item => item.Kind == AutomationOperationKind.SetAddress)
					.Select(item => item.Value),
				Is.EquivalentTo(new[] { "One/Icon", "Two/Icon" }));
		}

		[Test]
		public void Planner_ConvergesWrongGroupAddressAndLabelsThenBecomesEmpty() {
			var state = ReadyState();
			state.Groups.Add(new GroupSyncGroupState {
				Guid = "wrong-guid", Name = "Wrong", HasBundledSchema = true,
				HasContentUpdateSchema = true, IsBuildable = true
			});
			state.Labels.Add("required");
			state.Labels.Add("unrelated");
			var rule = Rule();
			rule.RequiredLabels = new[] { "required" };
			rule.Assets.Add(Asset("asset-a", "Assets/Content/Sub/A.asset"));
			state.Rules.Add(rule);
			state.Entries.Add(new GroupSyncEntryState {
				Guid = "asset-a", Path = "Assets/Content/Sub/A.asset",
				GroupGuid = "wrong-guid", GroupName = "Wrong", Address = "legacy",
				Labels = new[] { "unrelated" }
			});

			var first = GroupSyncPlanner.Create(state);

			Assert.That(first.Operations.Select(item => item.Kind), Does.Contain(AutomationOperationKind.MoveEntry));
			Assert.That(first.Operations.Select(item => item.Kind), Does.Contain(AutomationOperationKind.SetAddress));
			Assert.That(first.Operations.Any(item => item.Kind == AutomationOperationKind.AddLabel && item.Value == "required"), Is.True);
			Assert.That(first.Operations.Any(item => item.Kind == AutomationOperationKind.RemoveLabel && item.Value == "unrelated"), Is.False);

			state.Entries.Clear();
			state.Entries.Add(new GroupSyncEntryState {
				Guid = "asset-a", Path = "Assets/Content/Sub/A.asset",
				GroupGuid = "target-guid", GroupName = "Target", Address = "Sub/A",
				Labels = new[] { "required", "unrelated" }
			});
			var second = GroupSyncPlanner.Create(state);

			Assert.That(second.IsValid, Is.True);
			Assert.That(second.Operations, Is.Empty);
		}

		[Test]
		public void Planner_ExactLabelPolicyRemovesUnrelatedLabels() {
			var state = ReadyState();
			state.Labels.UnionWith(new[] { "required", "unrelated" });
			var rule = Rule();
			rule.LabelPolicy = ExistingLabelPolicy.Exact;
			rule.RequiredLabels = new[] { "required" };
			rule.Assets.Add(Asset("asset-a", "Assets/Content/A.asset"));
			state.Rules.Add(rule);
			state.Entries.Add(new GroupSyncEntryState {
				Guid = "asset-a", Path = "Assets/Content/A.asset",
				GroupGuid = "target-guid", GroupName = "Target", Address = "A",
				Labels = new[] { "required", "unrelated" }
			});

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.Operations.Single().Kind, Is.EqualTo(AutomationOperationKind.RemoveLabel));
			Assert.That(plan.Operations.Single().Value, Is.EqualTo("unrelated"));
		}

		[Test]
		public void Planner_AddressCollisionWithUnrelatedEntryBlocksApply() {
			var state = ReadyState();
			var rule = Rule();
			rule.Assets.Add(Asset("asset-a", "Assets/Content/A.asset"));
			state.Rules.Add(rule);
			state.Entries.Add(new GroupSyncEntryState {
				Guid = "other", Path = "Assets/Other.asset",
				GroupGuid = "target-guid", GroupName = "Target", Address = "A"
			});

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.AddressCollision), Is.True);
		}

		[Test]
		public void Planner_AddressableFolderEntryBlocksDescendantOperations() {
			var state = ReadyState();
			var rule = Rule();
			rule.Assets.Add(Asset("asset-a", "Assets/Content/Sub/A.asset"));
			state.Rules.Add(rule);
			state.Entries.Add(new GroupSyncEntryState {
				Guid = "folder-guid", Path = "Assets/Content", IsFolder = true,
				GroupGuid = "target-guid", GroupName = "Target", Address = "Content"
			});

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.FolderEntryConflict), Is.True);
		}

		[Test]
		public void Planner_MissingGroupAndSchemasAreExplicitOperations() {
			var state = ReadyState();
			state.Groups.Clear();
			var rule = Rule();
			rule.DestinationGroupGuid = string.Empty;
			rule.Assets.Add(Asset("asset-a", "Assets/Content/A.asset"));
			state.Rules.Add(rule);

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.True);
			Assert.That(plan.Operations.Take(3).Select(item => item.Kind), Is.EqualTo(new[] {
				AutomationOperationKind.CreateGroup,
				AutomationOperationKind.AddBundledAssetGroupSchema,
				AutomationOperationKind.AddContentUpdateGroupSchema
			}));
		}

		[Test]
		public void Planner_ReadOnlyOrNonBuildableGroupFailsPreflight() {
			foreach (var group in new[] {
			         new GroupSyncGroupState { Guid = "target-guid", Name = "Target", ReadOnly = true, HasBundledSchema = true, HasContentUpdateSchema = true, IsBuildable = true },
			         new GroupSyncGroupState { Guid = "target-guid", Name = "Target", ReadOnly = false, HasBundledSchema = true, HasContentUpdateSchema = true, IsBuildable = false }
			     }) {
				var state = ReadyState();
				state.Groups.Clear();
				state.Groups.Add(group);
				var rule = Rule();
				rule.Assets.Add(Asset("asset-a", "Assets/Content/A.asset"));
				state.Rules.Add(rule);

				var plan = GroupSyncPlanner.Create(state);

				Assert.That(plan.IsValid, Is.False);
				Assert.That(plan.Operations, Is.Empty);
			}
		}

		[Test]
		public void Planner_FailedAssetLoadBlocksApply() {
			var state = ReadyState();
			var rule = Rule();
			rule.Assets.Add(new GroupSyncAssetState {
				Guid = "broken", Path = "Assets/Content/Broken.asset", LoadError = "forced load failure"
			});
			state.Rules.Add(rule);

			var before = state.ComputeHash();
			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Operations, Is.Empty);
			Assert.That(plan.Diagnostics.Single().Code, Is.EqualTo(AutomationDiagnosticCode.AssetLoadFailed));
			Assert.That(plan.Diagnostics.Single().Severity, Is.EqualTo(AutomationDiagnosticSeverity.Error));
			Assert.That(state.ComputeHash(), Is.EqualTo(before), "Dry-run planning must not mutate captured state.");
		}

		[Test]
		public void Planner_IncompatibleDuplicateClaimFailsPreflight() {
			var state = ReadyState();
			var first = Rule();
			first.Assets.Add(Asset("asset-a", "Assets/Content/A.asset"));
			var second = Rule();
			second.Index = 1;
			second.SourceFolderPath = "Assets";
			second.AddressPrefix = "other";
			second.Assets.Add(Asset("asset-a", "Assets/Content/A.asset"));
			state.Rules.Add(first);
			state.Rules.Add(second);

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.AssetClaimConflict), Is.True);
		}

		[Test]
		public void Planner_RejectsActiveConfigurationAssetClaim() {
			var state = ReadyState();
			state.ConfigGuid = "config-guid";
			var rule = Rule();
			rule.Assets.Add(Asset("config-guid", "Assets/Content/Config.asset"));
			state.Rules.Add(rule);

			var plan = GroupSyncPlanner.Create(state);

			Assert.That(plan.IsValid, Is.False);
			Assert.That(plan.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.ConfigurationAssetClaimed), Is.True);
		}

		[Test]
		public void AddressGenerator_NormalizesPrefixAndRemovesOnlyFinalExtension() {
			Assert.That(
				GroupSyncPlanner.GenerateAddress(
					"Assets\\Content", "Assets/Content/Sub/archive.tar.gz", "catalog"),
				Is.EqualTo("catalog/Sub/archive.tar"));
		}

		[Test]
		public void Transaction_ForcedMidApplyFailureRestoresSnapshot() {
			var backend = new FakeMutationBackend { FailAtExecution = 2 };
			var plan = TransactionPlan(3);

			var report = GroupSyncTransaction.Apply(plan, backend);

			Assert.That(report.Succeeded, Is.False);
			Assert.That(report.RollbackStatus, Is.EqualTo(AutomationRollbackStatus.Succeeded));
			Assert.That(backend.State, Is.EqualTo(0));
			Assert.That(backend.BeginCount, Is.EqualTo(1));
			Assert.That(backend.CommitCount, Is.Zero);
		}

		[Test]
		public void Transaction_IncompleteRollbackRetainsRecoveryPath() {
			var backend = new FakeMutationBackend {
				FailAtExecution = 1,
				RollbackFails = true,
				Path = "Library/TorProduction.Addressables/Recovery/group-sync-test.json"
			};

			var report = GroupSyncTransaction.Apply(TransactionPlan(2), backend);

			Assert.That(report.Succeeded, Is.False);
			Assert.That(report.RollbackStatus, Is.EqualTo(AutomationRollbackStatus.Failed));
			Assert.That(report.RecoveryPath, Is.EqualTo(backend.Path));
			Assert.That(report.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.RollbackFailed), Is.True);
		}

		[Test]
		public void Transaction_RollbackBackendExceptionReturnsStructuredFailure() {
			var backend = new FakeMutationBackend {
				FailAtExecution = 1,
				RollbackThrows = true,
				Path = "Library/TorProduction.Addressables/Recovery/group-sync-throw.json"
			};

			var report = GroupSyncTransaction.Apply(TransactionPlan(2), backend);

			Assert.That(report.Succeeded, Is.False);
			Assert.That(report.RollbackStatus, Is.EqualTo(AutomationRollbackStatus.Failed));
			Assert.That(report.RecoveryPath, Is.EqualTo(backend.Path));
			Assert.That(report.Diagnostics.Any(item =>
				item.Code == AutomationDiagnosticCode.RollbackFailed &&
				item.Message.Contains("threw unexpectedly")), Is.True);
			Assert.That(report.Failures.Count, Is.EqualTo(2));
		}

		[Test]
		public void PublicApply_RejectsStalePlanWithoutThrowing() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				var plan = new AutomationPlan(
					AutomationScope.Groups, "old-source", "old-plan",
					new[] { new AutomationOperation(AutomationOperationKind.CreateLabel, value: "managed") },
					Array.Empty<AutomationDiagnostic>(), config);

				var report = AddressablesAutomation.Apply(plan);

				Assert.That(report.Succeeded, Is.False);
				Assert.That(report.Operations, Is.Empty);
				Assert.That(report.Diagnostics.Any(item => item.Code == AutomationDiagnosticCode.StalePlan), Is.True);
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		[Test]
		public void CliAnalyzeAndApply_ReportSuccessAndThrowOnFailure() {
			var output = new List<string>();
			var valid = TransactionPlan(0);
			var success = new AutomationReport(
				true, Array.Empty<AutomationOperation>(), Array.Empty<AutomationDiagnostic>(),
				Array.Empty<string>(), AutomationRollbackStatus.NotRequired, string.Empty);
			Assert.DoesNotThrow(() => AddressablesAutomationCli.RunAnalyzeGroups(() => valid, output.Add));
			Assert.DoesNotThrow(() => AddressablesAutomationCli.RunApplyGroups(() => valid, _ => success, output.Add));
			Assert.That(output.Count, Is.EqualTo(3));

			var invalid = new AutomationPlan(
				AutomationScope.Groups, "invalid", "invalid",
				Array.Empty<AutomationOperation>(),
				new[] { new AutomationDiagnostic(
					AutomationDiagnosticCode.ConfigurationInvalid,
					AutomationDiagnosticSeverity.Error,
					"CLI", "forced CLI failure") }, null);
			Assert.Throws<InvalidOperationException>(() =>
				AddressablesAutomationCli.RunAnalyzeGroups(() => invalid, _ => { }));
			Assert.Throws<InvalidOperationException>(() =>
				AddressablesAutomationCli.RunApplyGroups(() => invalid, _ => success, _ => { }));
		}

		[Test]
		public void Transaction_SuccessCommitsAndCompletesOnce() {
			var backend = new FakeMutationBackend();

			var report = GroupSyncTransaction.Apply(TransactionPlan(4), backend);

			Assert.That(report.Succeeded, Is.True);
			Assert.That(backend.State, Is.EqualTo(4));
			Assert.That(backend.BeginCount, Is.EqualTo(1));
			Assert.That(backend.CommitCount, Is.EqualTo(1));
			Assert.That(backend.CompleteCount, Is.EqualTo(1));
		}

		[Test]
		public void Planner_LargeFixtureIsDeterministicAndCompletesWithinBudget() {
			var state = ReadyState();
			var rule = Rule();
			for (var index = 2499; index >= 0; index--) {
				rule.Assets.Add(Asset($"asset-{index:D4}", $"Assets/Content/Folder{index % 25:D2}/Asset{index:D4}.asset"));
			}
			state.Rules.Add(rule);
			var stopwatch = Stopwatch.StartNew();

			var first = GroupSyncPlanner.Create(state);
			var second = GroupSyncPlanner.Create(state);

			stopwatch.Stop();
			Assert.That(first.IsValid, Is.True);
			Assert.That(first.PlanHash, Is.EqualTo(second.PlanHash));
			Assert.That(first.Operations.Select(item => item.Description),
				Is.EqualTo(second.Operations.Select(item => item.Description)));
			Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
		}

		[Test]
		public void AssetTypeEnumeration_DoesNotDependOnProjectSpecificRuntimeTypes() {
			Assert.DoesNotThrow(() => AssetTypes.GetInheritedTypes<ScriptableObject>());
			Assert.That(AssetTypes.AvailableTypes, Does.Contain(typeof(ScriptableObject)));
		}

		[Test]
		public void PublicPlanCollections_AreReadOnlySnapshots() {
			var plan = TransactionPlan(1);
			var operations = (IList<AutomationOperation>)plan.Operations;
			var diagnostics = (IList<AutomationDiagnostic>)plan.Diagnostics;

			Assert.Throws<NotSupportedException>(() => operations.Clear());
			Assert.Throws<NotSupportedException>(() => diagnostics.Add(new AutomationDiagnostic(
				AutomationDiagnosticCode.ApplyFailed,
				AutomationDiagnosticSeverity.Error,
				"test", "test")));
		}

		private static GroupSyncProjectState ReadyState() {
			var state = new GroupSyncProjectState {
				SettingsExist = true,
				SettingsIdentity = "settings-guid",
				ConfigJson = "{}"
			};
			state.Groups.Add(new GroupSyncGroupState {
				Guid = "target-guid",
				Name = "Target",
				HasBundledSchema = true,
				HasContentUpdateSchema = true,
				IsBuildable = true
			});
			return state;
		}

		private static GroupSyncRuleState Rule() {
			return new GroupSyncRuleState {
				Index = 0,
				SourceFolderPath = "Assets/Content",
				DestinationGroupGuid = "target-guid",
				DestinationGroupName = "Target",
				AddressPolicy = GroupAddressPolicy.RelativePath,
				LabelPolicy = ExistingLabelPolicy.PreserveUnrelated,
				RequiredLabels = Array.Empty<string>(),
				TypeFilterNames = Array.Empty<string>(),
				ResolvedTypes = Array.Empty<Type>()
			};
		}

		private static GroupSyncAssetState Asset(string guid, string path) {
			return new GroupSyncAssetState { Guid = guid, Path = path, AssetType = typeof(Texture2D) };
		}

		private static AutomationPlan TransactionPlan(int count) {
			var operations = Enumerable.Range(0, count)
				.Select(index => new AutomationOperation(
					AutomationOperationKind.CreateLabel, value: "label-" + index))
				.ToArray();
			return new AutomationPlan(
				AutomationScope.Groups, "source", "plan", operations,
				Array.Empty<AutomationDiagnostic>(), null);
		}

		private sealed class FakeMutationBackend : IGroupSyncMutationBackend {
			private int m_snapshot;
			private int m_executions;
			internal int FailAtExecution = -1;
			internal bool RollbackFails;
			internal bool RollbackThrows;
			internal string Path = "Library/FakeRecovery.json";
			internal int State;
			internal int BeginCount;
			internal int CommitCount;
			internal int CompleteCount;

			public string RecoveryPath => Path;

			public void Begin(AutomationPlan plan) {
				BeginCount++;
				m_snapshot = State;
			}

			public void Execute(AutomationOperation operation) {
				m_executions++;
				State++;
				if (m_executions == FailAtExecution) {
					throw new InvalidOperationException("forced mid-apply failure");
				}
			}

			public void Commit() {
				CommitCount++;
			}

			public void Complete() {
				CompleteCount++;
			}

			public bool TryRollback(out string error) {
				if (RollbackThrows) {
					throw new InvalidOperationException("forced rollback exception");
				}
				if (RollbackFails) {
					error = "forced rollback failure";
					return false;
				}
				State = m_snapshot;
				error = string.Empty;
				return true;
			}
		}
	}

	public sealed class PhaseTwoGroupSynchronizationIntegrationTests {
		private string m_root;
		private string m_assetGuid;
		private AddressableAssetSettings m_settings;
		private AddressableAssetSettings m_originalDefaultSettings;
		private bool m_createdDefaultFolder;

		[SetUp]
		public void SetUp() {
			m_createdDefaultFolder = false;
			m_originalDefaultSettings = AddressableAssetSettingsDefaultObject.SettingsExists
				? AddressableAssetSettingsDefaultObject.GetSettings(false)
				: null;
			m_root = "Assets/__TorProductionPhase2_" + Guid.NewGuid().ToString("N");
			Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(m_root)), Is.Not.Empty);
			Assert.That(AssetDatabase.CreateFolder(m_root, "Content"), Is.Not.Empty);
			Assert.That(AssetDatabase.CreateFolder(m_root, "Editor"), Is.Not.Empty);
			var assetPath = m_root + "/Content/Fixture.asset";
			AssetDatabase.CreateAsset(new TextAsset("phase-2"), assetPath);
			m_assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
			m_settings = AddressableAssetSettings.Create(
				m_root + "/AddressableAssetsData", "AddressableAssetSettings", true, true);
			Assert.That(m_settings, Is.Not.Null);
			if (!AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder)) {
				m_createdDefaultFolder = true;
				Assert.That(AssetDatabase.CreateFolder("Assets", "AddressableAssetsData"), Is.Not.Empty);
			}
			AddressableAssetSettingsDefaultObject.Settings = m_settings;
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
		public void UnityBackend_CreatesGroupSchemasEntryAddressAndLabel() {
			var plan = IntegrationPlan();
			var backend = new UnityGroupSyncMutationBackend(m_settings);

			var report = GroupSyncTransaction.Apply(plan, backend);

			Assert.That(report.Succeeded, Is.True, string.Join(" | ", report.Failures));
			var group = m_settings.FindGroup("Managed Content");
			Assert.That(group, Is.Not.Null);
			Assert.That(group.GetSchema<BundledAssetGroupSchema>(), Is.Not.Null);
			Assert.That(group.GetSchema<ContentUpdateGroupSchema>(), Is.Not.Null);
			var entry = m_settings.FindAssetEntry(m_assetGuid);
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.parentGroup, Is.SameAs(group));
			Assert.That(entry.address, Is.EqualTo("content/fixture"));
			Assert.That(entry.labels, Does.Contain("managed"));
			Assert.That(File.Exists(backend.RecoveryPath), Is.False);
		}

		[Test]
		public void UnityBackend_ForcedMidApplyFailureRestoresAddressablesState() {
			var defaultEntryCount = m_settings.DefaultGroup.entries.Count;
			var unityBackend = new UnityGroupSyncMutationBackend(m_settings);
			var backend = new FailingBackend(unityBackend, 5);

			var report = GroupSyncTransaction.Apply(IntegrationPlan(), backend);

			Assert.That(report.Succeeded, Is.False);
			Assert.That(report.RollbackStatus, Is.EqualTo(AutomationRollbackStatus.Succeeded));
			Assert.That(m_settings.FindGroup("Managed Content"), Is.Null);
			Assert.That(m_settings.FindAssetEntry(m_assetGuid), Is.Null);
			Assert.That(m_settings.DefaultGroup.entries.Count, Is.EqualTo(defaultEntryCount));
			Assert.That(File.Exists(unityBackend.RecoveryPath), Is.False);
		}

		[Test]
		public void PublicAnalyzeAndApply_RepeatedRunConvergesAtIntegrationBoundary() {
			var config = CreateConfig();

			var firstPlan = AddressablesAutomation.Analyze(config, AutomationScope.Groups);
			var firstReport = AddressablesAutomation.Apply(firstPlan);
			var secondPlan = AddressablesAutomation.Analyze(config, AutomationScope.Groups);
			var secondReport = AddressablesAutomation.Apply(secondPlan);

			Assert.That(firstPlan.IsValid, Is.True, FormatDiagnostics(firstPlan));
			Assert.That(firstPlan.HasChanges, Is.True);
			Assert.That(firstReport.Succeeded, Is.True, string.Join(" | ", firstReport.Failures));
			Assert.That(secondPlan.IsValid, Is.True, FormatDiagnostics(secondPlan));
			Assert.That(secondPlan.Operations, Is.Empty);
			Assert.That(secondReport.Succeeded, Is.True);
			Assert.That(secondReport.Operations, Is.Empty);
		}

		[Test]
		public void PublicRecovery_RestoresActualPendingRecoveryFile() {
			var backend = new UnityGroupSyncMutationBackend(m_settings);
			var plan = IntegrationPlan();
			backend.Begin(plan);
			backend.Execute(plan.Operations[0]);
			Assert.That(File.Exists(backend.RecoveryPath), Is.True);
			Assert.That(m_settings.FindGroup("Managed Content"), Is.Not.Null);

			var report = AddressablesAutomation.Recover();

			Assert.That(report.Succeeded, Is.True, string.Join(" | ", report.Failures));
			Assert.That(report.RollbackStatus, Is.EqualTo(AutomationRollbackStatus.Succeeded));
			Assert.That(m_settings.FindGroup("Managed Content"), Is.Null);
			Assert.That(File.Exists(backend.RecoveryPath), Is.False);
		}

		private AddressablesAutomationConfig CreateConfig() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			config.ReplaceWithCurrentSchema(
				new[] { new GroupSyncRule(
					AssetDatabase.AssetPathToGUID(m_root + "/Content"),
					Array.Empty<string>(), string.Empty, "Managed Content", "content",
					GroupAddressPolicy.RelativePath, ExistingLabelPolicy.PreserveUnrelated,
					new[] { "managed" }, Array.Empty<string>()) },
				Array.Empty<SceneFolderRule>());
			AssetDatabase.CreateAsset(config, m_root + "/Editor/AutomationConfig.asset");
			AssetDatabase.SaveAssets();
			return config;
		}

		private static string FormatDiagnostics(AutomationPlan plan) {
			return string.Join(" | ", plan.Diagnostics.Select(item =>
				$"{item.Severity}:{item.Code}:{item.Location}:{item.Message}"));
		}

		private AutomationPlan IntegrationPlan() {
			return new AutomationPlan(
				AutomationScope.Groups,
				"integration-source",
				"integration-plan-" + Guid.NewGuid().ToString("N"),
				new[] {
					new AutomationOperation(AutomationOperationKind.CreateGroup, groupName: "Managed Content"),
					new AutomationOperation(AutomationOperationKind.AddBundledAssetGroupSchema, groupName: "Managed Content"),
					new AutomationOperation(AutomationOperationKind.AddContentUpdateGroupSchema, groupName: "Managed Content"),
					new AutomationOperation(AutomationOperationKind.CreateLabel, value: "managed"),
					new AutomationOperation(AutomationOperationKind.CreateEntry, m_assetGuid, m_root + "/Content/Fixture.asset", groupName: "Managed Content"),
					new AutomationOperation(AutomationOperationKind.SetAddress, m_assetGuid, m_root + "/Content/Fixture.asset", groupName: "Managed Content", value: "content/fixture"),
					new AutomationOperation(AutomationOperationKind.AddLabel, m_assetGuid, m_root + "/Content/Fixture.asset", groupName: "Managed Content", value: "managed")
				},
				Array.Empty<AutomationDiagnostic>(),
				null);
		}

		private sealed class FailingBackend : IGroupSyncMutationBackend {
			private readonly IGroupSyncMutationBackend m_inner;
			private readonly int m_failAfter;
			private int m_count;

			internal FailingBackend(IGroupSyncMutationBackend inner, int failAfter) {
				m_inner = inner;
				m_failAfter = failAfter;
			}

			public string RecoveryPath => m_inner.RecoveryPath;
			public void Begin(AutomationPlan plan) => m_inner.Begin(plan);

			public void Execute(AutomationOperation operation) {
				m_inner.Execute(operation);
				m_count++;
				if (m_count == m_failAfter) {
					throw new InvalidOperationException("forced Unity backend failure");
				}
			}

			public void Commit() => m_inner.Commit();
			public void Complete() => m_inner.Complete();
			public bool TryRollback(out string error) => m_inner.TryRollback(out error);
		}
	}
}
