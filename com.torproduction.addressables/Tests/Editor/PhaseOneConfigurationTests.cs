using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TorProduction.Addressables.Editor;
using TorProduction.AddressablesToolpack.Editor.Menu;
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Tests {
	internal sealed class PhaseOneConfigurationTests {
		private const string CONFIG_GUID = "0123456789abcdef0123456789abcdef";

		[Test]
		public void ProjectSettingsRead_WhenMissing_IsInert() {
			var backend = new FakeProjectSettingsBackend { Exists = false };

			var result = AddressablesAutomationProjectSettingsStore.Read(backend);

			Assert.That(result.Status, Is.EqualTo(ProjectSettingsReadStatus.Missing));
			Assert.That(backend.ValueReadCount, Is.Zero);
			Assert.That(backend.SaveCount, Is.Zero);
		}

		[Test]
		public void SettingsProviderFactory_IsInertAndUsesProjectSettingsPath() {
			var settingsExisted = System.IO.File.Exists(
				AddressablesAutomationProjectSettingsStore.SettingsPath);
			var settingsContents = settingsExisted
				? System.IO.File.ReadAllText(AddressablesAutomationProjectSettingsStore.SettingsPath)
				: null;

			var provider = AddressablesAutomationSettingsProvider.CreateProvider();

			Assert.That(provider.settingsPath, Is.EqualTo(AddressablesAutomationSettingsProvider.SettingsPath));
			Assert.That(System.IO.File.Exists(AddressablesAutomationProjectSettingsStore.SettingsPath),
				Is.EqualTo(settingsExisted));
			if (settingsExisted) {
				Assert.That(
					System.IO.File.ReadAllText(AddressablesAutomationProjectSettingsStore.SettingsPath),
					Is.EqualTo(settingsContents));
			}
		}

		[Test]
		public void ProjectSettingsSelectAndDetach_WriteOnlyExplicitly() {
			var backend = new FakeProjectSettingsBackend { Exists = false };

			Assert.That(
				AddressablesAutomationProjectSettingsStore.TryPersistSelection(CONFIG_GUID, backend, out var selectError),
				Is.True,
				selectError);
			Assert.That(backend.SaveCount, Is.EqualTo(1));
			Assert.That(backend.SelectedConfigGuid, Is.EqualTo(CONFIG_GUID));
			Assert.That(backend.AutomationEnabled, Is.False);
			Assert.That(backend.AutomaticScopes, Is.EqualTo(AutomationScope.None));

			var read = AddressablesAutomationProjectSettingsStore.Read(backend);
			Assert.That(read.Status, Is.EqualTo(ProjectSettingsReadStatus.Valid));
			Assert.That(backend.SaveCount, Is.EqualTo(1), "Read must not persist or repair settings.");

			Assert.That(
				AddressablesAutomationProjectSettingsStore.TryDetach(backend, out var detachError),
				Is.True,
				detachError);
			Assert.That(backend.SaveCount, Is.EqualTo(2));
			Assert.That(backend.SelectedConfigGuid, Is.Empty);
		}

		[Test]
		public void ProjectSettingsRead_WhenCorrupt_PreservesStoredValues() {
			var backend = CreateValidBackend();
			backend.Magic = "corrupt";
			backend.AutomationEnabled = true;
			backend.AutomaticScopes = AutomationScope.Scenes;

			var result = AddressablesAutomationProjectSettingsStore.Read(backend);

			Assert.That(result.Status, Is.EqualTo(ProjectSettingsReadStatus.Corrupt));
			Assert.That(result.Snapshot.SelectedConfigGuid, Is.EqualTo(CONFIG_GUID));
			Assert.That(result.Snapshot.AutomationEnabled, Is.True);
			Assert.That(result.Snapshot.AutomaticScopes, Is.EqualTo(AutomationScope.Scenes));
			Assert.That(backend.SaveCount, Is.Zero);
		}

		[TestCase(true, AutomationScope.None)]
		[TestCase(false, AutomationScope.Scenes)]
		public void ProjectSettingsRead_WhenOptInStateIsInconsistent_IsCorrupt(
			bool enabled,
			AutomationScope scopes) {
			var backend = CreateValidBackend();
			backend.AutomationEnabled = enabled;
			backend.AutomaticScopes = scopes;

			var result = AddressablesAutomationProjectSettingsStore.Read(backend);

			Assert.That(result.Status, Is.EqualTo(ProjectSettingsReadStatus.Corrupt));
			Assert.That(backend.SaveCount, Is.Zero);
		}

		[TestCase(0, ProjectSettingsReadStatus.MigrationRequired)]
		[TestCase(2, ProjectSettingsReadStatus.UnsupportedSchema)]
		public void ProjectSettingsRead_WhenSchemaDiffers_IsTypedAndInert(
			int schemaVersion,
			ProjectSettingsReadStatus expectedStatus) {
			var backend = CreateValidBackend();
			backend.SchemaVersion = schemaVersion;

			var result = AddressablesAutomationProjectSettingsStore.Read(backend);

			Assert.That(result.Status, Is.EqualTo(expectedStatus));
			Assert.That(backend.SaveCount, Is.Zero);
		}

		[Test]
		public void ProjectSettingsRecovery_WhenCorrupt_BacksUpAndWritesSafeDefaults() {
			var backend = CreateValidBackend();
			backend.Magic = "corrupt";
			backend.AutomationEnabled = true;
			backend.AutomaticScopes = AutomationScope.Scenes;

			Assert.That(
				AddressablesAutomationProjectSettingsStore.TryRecover(
					backend,
					out var recoveryPath,
					out var error),
				Is.True,
				error);
			Assert.That(recoveryPath, Is.EqualTo("Library/FakeRecovery.asset"));
			Assert.That(backend.BackupCount, Is.EqualTo(1));
			Assert.That(backend.SaveCount, Is.EqualTo(1));
			Assert.That(backend.Magic, Is.EqualTo(AddressablesAutomationProjectSettingsStore.ExpectedMagic));
			Assert.That(backend.SelectedConfigGuid, Is.Empty);
			Assert.That(backend.AutomationEnabled, Is.False);
			Assert.That(backend.AutomaticScopes, Is.EqualTo(AutomationScope.None));
		}

		[Test]
		public void ProjectSettingsWrite_WhenSaveFails_RestoresInMemoryValues() {
			var backend = CreateValidBackend();
			backend.ThrowOnSave = true;

			Assert.That(
				AddressablesAutomationProjectSettingsStore.TryPersistSelection(
					"fedcba9876543210fedcba9876543210",
					backend,
					out var error),
				Is.False);
			StringAssert.Contains("Could not save", error);
			Assert.That(backend.SelectedConfigGuid, Is.EqualTo(CONFIG_GUID));
			Assert.That(backend.AutomationEnabled, Is.False);
			Assert.That(backend.AutomaticScopes, Is.EqualTo(AutomationScope.None));
		}

		[Test]
		public void Resolution_WhenSelectedAssetMoves_UsesGuidAndDoesNotSave() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				var backend = CreateValidBackend();
				var resolver = new FakeAssetResolver();
				resolver.Paths[CONFIG_GUID] = "Assets/Editor/Config/Automation.asset";
				resolver.Assets["Assets/Editor/Config/Automation.asset"] = config;
				var addressables = new FakeAddressablesSettingsView { Exists = true };

				var first = AddressablesAutomationContextProvider.Resolve(
					AutomationScope.Groups,
					false,
					backend,
					resolver,
					addressables);
				Assert.That(first.Status, Is.EqualTo(ConfigurationStatus.Ready));

				resolver.Assets.Remove("Assets/Editor/Config/Automation.asset");
				resolver.Paths[CONFIG_GUID] = "Assets/Moved/Editor/Automation.asset";
				resolver.Assets["Assets/Moved/Editor/Automation.asset"] = config;
				var moved = AddressablesAutomationContextProvider.Resolve(
					AutomationScope.Groups,
					false,
					backend,
					resolver,
					addressables);

				Assert.That(moved.Status, Is.EqualTo(ConfigurationStatus.Ready));
				Assert.That(moved.ConfigPath, Is.EqualTo("Assets/Moved/Editor/Automation.asset"));
				Assert.That(moved.Config, Is.SameAs(config));
				Assert.That(backend.SaveCount, Is.Zero);
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		[Test]
		public void Resolution_WhenSelectedAssetIsDeleted_RetainsGuidAndFailsClosed() {
			var backend = CreateValidBackend();
			var resolver = new FakeAssetResolver();

			var result = AddressablesAutomationContextProvider.Resolve(
				AutomationScope.Groups,
				false,
				backend,
				resolver,
				new FakeAddressablesSettingsView { Exists = true });

			Assert.That(result.Status, Is.EqualTo(ConfigurationStatus.SelectedConfigMissing));
			Assert.That(result.ProjectSettings.SelectedConfigGuid, Is.EqualTo(CONFIG_GUID));
			Assert.That(backend.SelectedConfigGuid, Is.EqualTo(CONFIG_GUID));
			Assert.That(backend.SaveCount, Is.Zero);
		}

		[Test]
		public void Resolution_AutomaticScopeRequiresExplicitOptIn() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				var backend = CreateValidBackend();
				var resolver = new FakeAssetResolver();
				resolver.Paths[CONFIG_GUID] = "Assets/Editor/Automation.asset";
				resolver.Assets["Assets/Editor/Automation.asset"] = config;
				var addressables = new FakeAddressablesSettingsView { Exists = true };

				var result = AddressablesAutomationContextProvider.Resolve(
					AutomationScope.Scenes,
					true,
					backend,
					resolver,
					addressables);

				Assert.That(result.Status, Is.EqualTo(ConfigurationStatus.AutomationDisabled));
				Assert.That(result.IsReady, Is.False);
				Assert.That(backend.SaveCount, Is.Zero);
				Assert.That(resolver.QueryCount, Is.Zero, "Disabled automation must not resolve project assets.");
				Assert.That(addressables.QueryCount, Is.Zero, "Disabled automation must not query Addressables.");
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		[Test]
		public void Selection_WhenAssetIsUnresolved_DoesNotSave() {
			var backend = new FakeProjectSettingsBackend { Exists = false };

			Assert.That(
				AddressablesAutomationContextProvider.TrySelectConfig(
					CONFIG_GUID,
					backend,
					new FakeAssetResolver(),
					out var error),
				Is.False);
			StringAssert.Contains("persistent asset", error);
			Assert.That(backend.SaveCount, Is.Zero);
		}

		[Test]
		public void Selection_WhenGuidChanges_ClearsAutomaticOptIn() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				const string replacementGuid = "fedcba9876543210fedcba9876543210";
				var backend = CreateValidBackend();
				backend.AutomationEnabled = true;
				backend.AutomaticScopes = AutomationScope.Scenes;
				var resolver = new FakeAssetResolver();
				resolver.Paths[replacementGuid] = "Assets/Editor/Replacement.asset";
				resolver.Assets["Assets/Editor/Replacement.asset"] = config;

				Assert.That(
					AddressablesAutomationContextProvider.TrySelectConfig(
						replacementGuid,
						backend,
						resolver,
						out var error),
					Is.True,
					error);
				Assert.That(backend.SelectedConfigGuid, Is.EqualTo(replacementGuid));
				Assert.That(backend.AutomationEnabled, Is.False);
				Assert.That(backend.AutomaticScopes, Is.EqualTo(AutomationScope.None));
				Assert.That(backend.SaveCount, Is.EqualTo(1));
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		[Test]
		public void AutomaticOptIn_WhenAddressablesIsMissing_DoesNotSave() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				var backend = CreateValidBackend();
				var resolver = new FakeAssetResolver();
				resolver.Paths[CONFIG_GUID] = "Assets/Editor/Automation.asset";
				resolver.Assets["Assets/Editor/Automation.asset"] = config;

				Assert.That(
					AddressablesAutomationContextProvider.TryApplyAutomaticSceneProcessing(
						true,
						backend,
						resolver,
						new FakeAddressablesSettingsView { Exists = false },
						out var error),
					Is.False);
				StringAssert.Contains("Addressables settings do not exist", error);
				Assert.That(backend.SaveCount, Is.Zero);
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		[Test]
		public void Validator_MissingAddressablesSettings_IsTypedAndInert() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				var addressables = new FakeAddressablesSettingsView { Exists = false };

				var report = AddressablesAutomationValidator.Validate(
					config,
					new FakeAssetResolver(),
					addressables);

				Assert.That(report.IsValid, Is.False);
				Assert.That(
					report.Diagnostics.Select(item => item.Code),
					Does.Contain(ConfigurationDiagnosticCode.AddressablesSettingsMissing));
				Assert.That(addressables.QueryCount, Is.Zero);
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		[Test]
		public void Validator_ReportsInvalidTypesAndOverlapsWithoutChangingConfig() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				config.ReplaceWithCurrentSchema(
					new[] {
						new GroupSyncRule(
							"folder-a",
							Array.Empty<string>(),
							string.Empty,
							"Missing Group",
							string.Empty,
							GroupAddressPolicy.RelativePath,
							ExistingLabelPolicy.PreserveUnrelated,
							Array.Empty<string>(),
							new[] { "Missing.Type, Missing.Assembly" }),
						new GroupSyncRule(
							"folder-b",
							Array.Empty<string>(),
							string.Empty,
							"Missing Group",
							string.Empty,
							GroupAddressPolicy.RelativePath,
							ExistingLabelPolicy.PreserveUnrelated,
							Array.Empty<string>(),
							Array.Empty<string>())
					},
					Array.Empty<SceneFolderRule>());
				var resolver = new FakeAssetResolver();
				resolver.Paths["folder-a"] = "Assets/Content";
				resolver.Paths["folder-b"] = "Assets/Content/Nested";
				resolver.Folders.Add("Assets/Content");
				resolver.Folders.Add("Assets/Content/Nested");
				var before = EditorJsonUtility.ToJson(config);

				var report = AddressablesAutomationValidator.Validate(
					config,
					resolver,
					new FakeAddressablesSettingsView { Exists = true });

				Assert.That(report.IsValid, Is.False);
				Assert.That(
					report.Diagnostics.Select(item => item.Code),
					Does.Contain(ConfigurationDiagnosticCode.TypeFilterUnresolved));
				Assert.That(
					report.Diagnostics.Select(item => item.Code),
					Does.Contain(ConfigurationDiagnosticCode.RuleOverlap));
				Assert.That(EditorJsonUtility.ToJson(config), Is.EqualTo(before));
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		[Test]
		public void Validator_ScopeAndExplicitNestedExclusion_AreRespected() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				config.ReplaceWithCurrentSchema(
					new[] {
						new GroupSyncRule(
							"folder-a",
							new[] { "folder-b" },
							string.Empty,
							"Group A",
							string.Empty,
							GroupAddressPolicy.RelativePath,
							ExistingLabelPolicy.PreserveUnrelated,
							null,
							null),
						new GroupSyncRule(
							"folder-b",
							Array.Empty<string>(),
							string.Empty,
							"Group B",
							string.Empty,
							GroupAddressPolicy.RelativePath,
							ExistingLabelPolicy.PreserveUnrelated,
							Array.Empty<string>(),
							Array.Empty<string>())
					},
					new[] { new SceneFolderRule() });
				var resolver = new FakeAssetResolver();
				resolver.Paths["folder-a"] = "Assets/Content";
				resolver.Paths["folder-b"] = "Assets/Content/Nested";
				resolver.Folders.Add("Assets/Content");
				resolver.Folders.Add("Assets/Content/Nested");

				var groupsOnly = AddressablesAutomationValidator.Validate(
					config,
					resolver,
					new FakeAddressablesSettingsView { Exists = true },
					AutomationScope.Groups);

				Assert.That(groupsOnly.IsValid, Is.True);
				Assert.That(
					groupsOnly.Diagnostics.Any(item =>
						item.Code == ConfigurationDiagnosticCode.RuleOverlap),
					Is.False);
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		private static FakeProjectSettingsBackend CreateValidBackend() {
			return new FakeProjectSettingsBackend {
				Exists = true,
				Magic = AddressablesAutomationProjectSettingsStore.ExpectedMagic,
				SchemaVersion = AddressablesAutomationProjectSettingsStore.CurrentSchemaVersion,
				SelectedConfigGuid = CONFIG_GUID,
				AutomationEnabled = false,
				AutomaticScopes = AutomationScope.None
			};
		}

		private sealed class FakeProjectSettingsBackend : IAddressablesAutomationProjectSettingsBackend {
			private string m_magic;
			private int m_schemaVersion;
			private string m_selectedConfigGuid;
			private bool m_automationEnabled;
			private AutomationScope m_automaticScopes;

			public bool Exists { get; set; }
			public int ValueReadCount { get; private set; }
			public int SaveCount { get; private set; }
			public int BackupCount { get; private set; }
			public bool ThrowOnSave { get; set; }

			public string Magic {
				get { ValueReadCount++; return m_magic; }
				set => m_magic = value;
			}

			public int SchemaVersion {
				get { ValueReadCount++; return m_schemaVersion; }
				set => m_schemaVersion = value;
			}

			public string SelectedConfigGuid {
				get { ValueReadCount++; return m_selectedConfigGuid; }
				set => m_selectedConfigGuid = value;
			}

			public bool AutomationEnabled {
				get { ValueReadCount++; return m_automationEnabled; }
				set => m_automationEnabled = value;
			}

			public AutomationScope AutomaticScopes {
				get { ValueReadCount++; return m_automaticScopes; }
				set => m_automaticScopes = value;
			}

			public bool TryBackup(out string recoveryPath, out string error) {
				BackupCount++;
				recoveryPath = "Library/FakeRecovery.asset";
				error = string.Empty;
				return true;
			}

			public void Save() {
				if (ThrowOnSave) {
					throw new InvalidOperationException("simulated save failure");
				}
				Exists = true;
				SaveCount++;
			}
		}

		private sealed class FakeAssetResolver : IConfigurationAssetResolver {
			internal readonly Dictionary<string, string> Paths = new Dictionary<string, string>();
			internal readonly Dictionary<string, UnityEngine.Object> Assets =
				new Dictionary<string, UnityEngine.Object>();
			internal readonly HashSet<string> Folders = new HashSet<string>();
			internal int QueryCount { get; private set; }

			public string GuidToAssetPath(string guid) {
				QueryCount++;
				return Paths.TryGetValue(guid, out var path) ? path : string.Empty;
			}

			public UnityEngine.Object LoadMainAssetAtPath(string path) {
				QueryCount++;
				return Assets.TryGetValue(path, out var asset) ? asset : null;
			}

			public bool IsValidFolder(string path) {
				QueryCount++;
				return Folders.Contains(path);
			}

			public Type ResolveType(string assemblyQualifiedName) {
				QueryCount++;
				return Type.GetType(assemblyQualifiedName, false);
			}
		}

		private sealed class FakeAddressablesSettingsView : IAddressablesSettingsView {
			internal readonly Dictionary<string, string> GroupsByGuid = new Dictionary<string, string>();
			internal readonly HashSet<string> GroupNames = new HashSet<string>();
			internal readonly HashSet<string> Labels = new HashSet<string>();

			public bool Exists { get; set; }
			public int QueryCount { get; private set; }

			public bool TryGetGroupName(string groupGuid, out string groupName) {
				QueryCount++;
				return GroupsByGuid.TryGetValue(groupGuid, out groupName);
			}

			public bool TryGetGroupGuid(string groupName, out string groupGuid) {
				QueryCount++;
				foreach (var pair in GroupsByGuid) {
					if (pair.Value == groupName) {
						groupGuid = pair.Key;
						return true;
					}
				}

				groupGuid = string.Empty;
				return false;
			}

			public bool HasGroupName(string groupName) {
				QueryCount++;
				return GroupNames.Contains(groupName);
			}

			public bool HasLabel(string label) {
				QueryCount++;
				return Labels.Contains(label);
			}
		}
	}
}
