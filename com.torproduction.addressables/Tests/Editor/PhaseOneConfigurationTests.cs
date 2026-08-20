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
		private const string LEGACY_ADDRESSABLES_GUID = "11111111111111111111111111111111";
		private const string LEGACY_SCENES_GUID = "22222222222222222222222222222222";
		private const string LEGACY_APP_STATES_GUID = "33333333333333333333333333333333";
		private const string SAMPLE_ADDRESSABLES_CONFIG_GUID = "2cccb57831ce0b142bd410986607fd0e";
		private const string SAMPLE_SCENES_CONFIG_GUID = "82222a4673d24384d9b8554ccb08996f";
		private const string SAMPLE_APP_STATES_CONFIG_GUID = "a20111eb8a0c7cf4396ff7e9e9e84b1c";
		private const string SAMPLE_SCENES_FOLDER_GUID = "4f42b69c201bcba42a0e7d976c56bd93";

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

			var ready = new ConfigurationResolution(
				ConfigurationStatus.Ready,
				string.Empty,
				new ProjectSettingsSnapshot(1, CONFIG_GUID, false, AutomationScope.None),
				"Assets/Editor/Automation.asset",
				null,
				null);
			var invalidButEnabled = new ConfigurationResolution(
				ConfigurationStatus.InvalidConfig,
				"Invalid config",
				new ProjectSettingsSnapshot(1, CONFIG_GUID, true, AutomationScope.Scenes),
				"Assets/Editor/Automation.asset",
				null,
				null);
			Assert.That(AddressablesAutomationSettingsProvider.CanApplyAutomaticSceneSetting(ready, true), Is.True);
			Assert.That(AddressablesAutomationSettingsProvider.CanApplyAutomaticSceneSetting(ready, false), Is.False);
			Assert.That(AddressablesAutomationSettingsProvider.CanApplyAutomaticSceneSetting(invalidButEnabled, true), Is.False);
			Assert.That(AddressablesAutomationSettingsProvider.CanApplyAutomaticSceneSetting(invalidButEnabled, false), Is.True);
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
		public void ProjectSettingsMigration_PreservesGuidAndDisablesAutomation() {
			var backend = CreateValidBackend();
			backend.SchemaVersion = 0;
			backend.AutomationEnabled = true;
			backend.AutomaticScopes = AutomationScope.Scenes;

			Assert.That(
				AddressablesAutomationProjectSettingsStore.TryMigrate(
					backend, out var recoveryPath, out var error),
				Is.True,
				error);
			Assert.That(recoveryPath, Is.EqualTo("Library/FakeRecovery.asset"));
			Assert.That(backend.BackupCount, Is.EqualTo(1));
			Assert.That(backend.SaveCount, Is.EqualTo(1));
			Assert.That(backend.SchemaVersion,
				Is.EqualTo(AddressablesAutomationProjectSettingsStore.CurrentSchemaVersion));
			Assert.That(backend.SelectedConfigGuid, Is.EqualTo(CONFIG_GUID));
			Assert.That(backend.AutomationEnabled, Is.False);
			Assert.That(backend.AutomaticScopes, Is.EqualTo(AutomationScope.None));
		}

		[Test]
		public void ConfigurationSchemaMigration_NormalizesCollectionsExplicitly() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				var serialized = new SerializedObject(config);
				serialized.FindProperty("m_schemaVersion").intValue = 0;
				serialized.ApplyModifiedPropertiesWithoutUndo();

				Assert.That(config.TryMigrateToCurrentSchema(out var error), Is.True, error);
				Assert.That(config.SchemaVersion, Is.EqualTo(AddressablesAutomationConfig.CurrentSchemaVersion));
				Assert.That(config.GroupRules, Is.Empty);
				Assert.That(config.SceneRules, Is.Empty);
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
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
		public void AssetDatabaseMove_PreservesSelectedConfigurationGuid() {
			var root = "Assets/__TorProductionAddressablesTests_" + Guid.NewGuid().ToString("N");
			Assert.That(root.StartsWith("Assets/__TorProductionAddressablesTests_", StringComparison.Ordinal), Is.True);
			Assert.That(AssetDatabase.IsValidFolder(root), Is.False);
			try {
				Assert.That(
					AssetDatabase.CreateFolder("Assets", root.Substring("Assets/".Length)),
					Is.Not.EqualTo(string.Empty));
				Assert.That(AssetDatabase.CreateFolder(root, "Editor"), Is.Not.EqualTo(string.Empty));
				var originalPath = root + "/Editor/Automation.asset";
				var movedPath = root + "/Editor/MovedAutomation.asset";
				var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
				AssetDatabase.CreateAsset(config, originalPath);
				var subassetConfig = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
				AssetDatabase.AddObjectToAsset(subassetConfig, originalPath);
				AssetDatabase.SaveAssets();
				var guid = AssetDatabase.AssetPathToGUID(originalPath);
				var backend = new FakeProjectSettingsBackend { Exists = false };

				Assert.That(
					AddressablesAutomationContextProvider.TryValidateConfigCandidate(
						subassetConfig, out var subassetError),
					Is.False);
				StringAssert.Contains("subassets", subassetError);

				Assert.That(
					AddressablesAutomationContextProvider.TrySelectConfig(
						guid, backend, UnityConfigurationAssetResolver.Instance, out var selectError),
					Is.True,
					selectError);
				Assert.That(AssetDatabase.MoveAsset(originalPath, movedPath), Is.Empty);

				var resolution = AddressablesAutomationContextProvider.Resolve(
					AutomationScope.Groups,
					false,
					backend,
					UnityConfigurationAssetResolver.Instance,
					new FakeAddressablesSettingsView { Exists = true });

				Assert.That(AssetDatabase.AssetPathToGUID(movedPath), Is.EqualTo(guid));
				Assert.That(resolution.Status, Is.EqualTo(ConfigurationStatus.Ready));
				Assert.That(resolution.ConfigPath, Is.EqualTo(movedPath));
				Assert.That(backend.SaveCount, Is.EqualTo(1), "Only explicit selection may save project state.");
			} finally {
				if (AssetDatabase.IsValidFolder(root)) {
					Assert.That(AssetDatabase.DeleteAsset(root), Is.True);
				}
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
		public void Selection_WhenConfigIsInResources_DoesNotSave() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				var backend = new FakeProjectSettingsBackend { Exists = false };
				var resolver = new FakeAssetResolver();
				resolver.Paths[CONFIG_GUID] = "Assets/Editor/Resources/Automation.asset";
				resolver.Assets["Assets/Editor/Resources/Automation.asset"] = config;

				Assert.That(
					AddressablesAutomationContextProvider.TrySelectConfig(
						CONFIG_GUID, backend, resolver, out var error),
					Is.False);
				StringAssert.Contains("Resources", error);
				Assert.That(backend.SaveCount, Is.Zero);
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		[Test]
		public void Resolution_WhenConfigItselfIsAddressable_FailsClosed() {
			var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
			try {
				var backend = CreateValidBackend();
				var resolver = new FakeAssetResolver();
				resolver.Paths[CONFIG_GUID] = "Assets/Editor/Automation.asset";
				resolver.Assets["Assets/Editor/Automation.asset"] = config;
				var addressables = new FakeAddressablesSettingsView { Exists = true };
				addressables.AssetEntries.Add(CONFIG_GUID);

				var result = AddressablesAutomationContextProvider.Resolve(
					AutomationScope.Groups, false, backend, resolver, addressables);

				Assert.That(result.Status, Is.EqualTo(ConfigurationStatus.InvalidConfig));
				Assert.That(result.Validation.Diagnostics.Any(item =>
					item.Code == ConfigurationDiagnosticCode.ConfigurationIsAddressable), Is.True);
				Assert.That(backend.SaveCount, Is.Zero);

				Assert.That(
					AddressablesAutomationContextProvider.TrySelectConfig(
						CONFIG_GUID, backend, resolver, addressables, out var selectError),
					Is.False);
				StringAssert.Contains("Addressables entry", selectError);
				Assert.That(backend.SaveCount, Is.Zero);
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
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
							"missing-group-guid",
							string.Empty,
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
				Assert.That(
					report.Diagnostics.Any(item =>
						item.Code == ConfigurationDiagnosticCode.DestinationGroupNotFound &&
						item.Severity == ConfigurationDiagnosticSeverity.Error),
					Is.True);
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
					new[] {
						new SceneFolderRule(
							"folder-c",
							Array.Empty<string>(),
							SceneFolderMode.LocalBuildSettings,
							string.Empty,
							string.Empty,
							string.Empty,
							"local-prefix",
							SceneAddressPolicy.PreserveManagedAddress,
							Array.Empty<string>())
					});
				var resolver = new FakeAssetResolver();
				resolver.Paths["folder-a"] = "Assets/Content";
				resolver.Paths["folder-b"] = "Assets/Content/Nested";
				resolver.Paths["folder-c"] = "Assets/Scenes";
				resolver.Folders.Add("Assets/Content");
				resolver.Folders.Add("Assets/Content/Nested");
				resolver.Folders.Add("Assets/Scenes");

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

				var scenesOnly = AddressablesAutomationValidator.Validate(
					config,
					resolver,
					new FakeAddressablesSettingsView { Exists = true },
					AutomationScope.Scenes);
				Assert.That(scenesOnly.IsValid, Is.False);
				Assert.That(
					scenesOnly.Diagnostics.Select(item => item.Code),
					Does.Contain(ConfigurationDiagnosticCode.AddressPolicyInvalid));
				Assert.That(
					scenesOnly.Diagnostics.Select(item => item.Code),
					Does.Contain(ConfigurationDiagnosticCode.AddressPrefixInvalid));
			} finally {
				UnityEngine.Object.DestroyImmediate(config);
			}
		}

		[Test]
		public void LegacyPreview_MapsResolvableValuesAndRetainsUnsafeIntentWithoutMutation() {
			var legacyAddressables = ScriptableObject.CreateInstance<LegacyAddressablesFixture>();
			var legacyScenes = ScriptableObject.CreateInstance<LegacyScenesFixture>();
			var appStates = ScriptableObject.CreateInstance<LegacyObjectFixture>();
			var groupFolder = ScriptableObject.CreateInstance<LegacyObjectFixture>();
			var worldFolder = ScriptableObject.CreateInstance<LegacyObjectFixture>();
			var uiFolder = ScriptableObject.CreateInstance<LegacyObjectFixture>();
			var additionalFolder = ScriptableObject.CreateInstance<LegacyObjectFixture>();
			var sceneCatalog = ScriptableObject.CreateInstance<LegacyObjectFixture>();
			try {
				legacyAddressables.m_Settings = new[] {
					new LegacyGroupFixture {
						m_groupName = "SceneMapsGroup",
						m_assetsFolder = groupFolder,
						m_lables = new[] { "content", "maps" },
						m_typesFilterNames = new[] { "String" },
						m_filterByType = true
					}
				};
				legacyScenes.m_ScenesLocation = worldFolder;
				legacyScenes.m_UIScenesLocation = uiFolder;
				legacyScenes.m_OtherSceneFolders = new UnityEngine.Object[] { additionalFolder };
				legacyScenes.m_ScenesConfig = sceneCatalog;

				var environment = new FakeLegacyMigrationEnvironment();
				environment.AddAsset(
					LEGACY_ADDRESSABLES_GUID, "Packages/com.test/LegacyAddressables.asset",
					legacyAddressables, LegacyConfigurationMigration.AddressableAssetsConfigScriptGuid);
				environment.AddAsset(
					LEGACY_SCENES_GUID, "Packages/com.test/LegacyScenes.asset",
					legacyScenes, LegacyConfigurationMigration.ScenesConfigScriptGuid);
				environment.AddAsset(
					LEGACY_APP_STATES_GUID, "Assets/Legacy/AppStates.asset", appStates, "app-script");
				environment.AddFolder(groupFolder, "Assets/Content/Maps", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
				environment.AddFolder(worldFolder, "Assets/Scenes/World", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
				environment.AddFolder(uiFolder, "Assets/Scenes/UI", "cccccccccccccccccccccccccccccccc");
				environment.AddFolder(
					additionalFolder, "Packages/com.test/Samples/Scenes", "dddddddddddddddddddddddddddddddd");
				environment.AssetPaths[sceneCatalog] = "Packages/com.test/ScenesConfig.asset";
				environment.GroupGuids["SceneMapsGroup"] = "stable-group-id";
				environment.GroupGuids[GroupNames.SCENES] = "stable-scenes-id";
				environment.TypeMatches["String"] = new[] { typeof(string) };

				var addressablesBefore = EditorJsonUtility.ToJson(legacyAddressables);
				var scenesBefore = EditorJsonUtility.ToJson(legacyScenes);
				var json =
					$"{{\"m_ScenesListConfigGUID\":\"{LEGACY_SCENES_GUID}\"," +
					$"\"m_AddressableAssetsConfigGUID\":\"{LEGACY_ADDRESSABLES_GUID}\"," +
					$"\"m_AppStatesConfigGUID\":\"{LEGACY_APP_STATES_GUID}\"}}";

				var preview = LegacyConfigurationMigration.Preview(json, environment);

				Assert.That(preview.GroupRules.Length, Is.EqualTo(1));
				Assert.That(preview.GroupRules[0].SourceFolderGuid,
					Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
				Assert.That(preview.GroupRules[0].DestinationGroupName, Is.EqualTo("SceneMapsGroup"));
				Assert.That(preview.GroupRules[0].DestinationGroupGuid, Is.EqualTo("stable-group-id"));
				Assert.That(preview.GroupRules[0].RequiredLabels.ToArray(),
					Is.EqualTo(new[] { "content", "maps" }));
				Assert.That(preview.GroupRules[0].AssemblyQualifiedTypeFilters.Single(),
					Is.EqualTo(typeof(string).AssemblyQualifiedName));
				Assert.That(preview.SceneRules.Length, Is.EqualTo(3));
				Assert.That(preview.SceneRules[0].Mode, Is.EqualTo(SceneFolderMode.Addressable));
				Assert.That(preview.SceneRules[0].DestinationGroupName, Is.EqualTo(GroupNames.SCENES));
				Assert.That(preview.SceneRules[0].AddressPolicy,
					Is.EqualTo(SceneAddressPolicy.PreserveManagedAddress));
				Assert.That(preview.SceneRules[1].Mode, Is.EqualTo(SceneFolderMode.LocalBuildSettings));
				Assert.That(preview.SceneRules[2].Mode, Is.EqualTo(SceneFolderMode.Unspecified));
				Assert.That(preview.HasBlockingErrors, Is.True);
				Assert.That(preview.Diagnostics.Any(item =>
					item.Code == LegacyMigrationDiagnosticCode.AdditionalSceneModeRequired), Is.True);
				Assert.That(preview.Diagnostics.Any(item =>
					item.Code == LegacyMigrationDiagnosticCode.SourceFolderOutsideAssets), Is.True);
				Assert.That(preview.Diagnostics.Any(item =>
					item.Code == LegacyMigrationDiagnosticCode.AppStatesIntentionallyIgnored), Is.True);
				Assert.That(EditorJsonUtility.ToJson(legacyAddressables), Is.EqualTo(addressablesBefore));
				Assert.That(EditorJsonUtility.ToJson(legacyScenes), Is.EqualTo(scenesBefore));
			} finally {
				UnityEngine.Object.DestroyImmediate(legacyAddressables);
				UnityEngine.Object.DestroyImmediate(legacyScenes);
				UnityEngine.Object.DestroyImmediate(appStates);
				UnityEngine.Object.DestroyImmediate(groupFolder);
				UnityEngine.Object.DestroyImmediate(worldFolder);
				UnityEngine.Object.DestroyImmediate(uiFolder);
				UnityEngine.Object.DestroyImmediate(additionalFolder);
				UnityEngine.Object.DestroyImmediate(sceneCatalog);
			}
		}

		[Test]
		public void LegacyPreview_WhenOneJsonFieldIsMalformed_PreservesValidSiblings() {
			var legacyAddressables = ScriptableObject.CreateInstance<LegacyAddressablesFixture>();
			var folder = ScriptableObject.CreateInstance<LegacyObjectFixture>();
			var appStates = ScriptableObject.CreateInstance<LegacyObjectFixture>();
			try {
				legacyAddressables.m_Settings = new[] {
					new LegacyGroupFixture {
						m_groupName = "PreservedGroup",
						m_assetsFolder = folder,
						m_lables = Array.Empty<string>(),
						m_typesFilterNames = new[] { "Ambiguous" },
						m_filterByType = true
					}
				};
				var environment = new FakeLegacyMigrationEnvironment();
				environment.AddAsset(
					LEGACY_ADDRESSABLES_GUID, "Assets/Legacy/Addressables.asset",
					legacyAddressables, LegacyConfigurationMigration.AddressableAssetsConfigScriptGuid);
				environment.AddAsset(
					LEGACY_APP_STATES_GUID, "Assets/Legacy/AppStates.asset", appStates, "app-script");
				environment.AddFolder(folder, "Assets/Content", "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
				environment.TypeMatches["Ambiguous"] = new[] { typeof(string), typeof(int) };
				var json =
					$"{{\"m_ScenesListConfigGUID\":42," +
					$"\"m_AddressableAssetsConfigGUID\":\"{LEGACY_ADDRESSABLES_GUID}\"," +
					$"\"m_AppStatesConfigGUID\":\"{LEGACY_APP_STATES_GUID}\"}}";

				var preview = LegacyConfigurationMigration.Preview(json, environment);

				Assert.That(preview.ScenesConfigGuid, Is.Empty);
				Assert.That(preview.AddressableAssetsConfigGuid, Is.EqualTo(LEGACY_ADDRESSABLES_GUID));
				Assert.That(preview.AppStatesConfigGuid, Is.EqualTo(LEGACY_APP_STATES_GUID));
				Assert.That(preview.GroupRules.Length, Is.EqualTo(1));
				Assert.That(preview.GroupRules[0].AssemblyQualifiedTypeFilters.Single(),
					Is.EqualTo("Ambiguous"));
				Assert.That(preview.Diagnostics.Any(item =>
					item.Code == LegacyMigrationDiagnosticCode.ReferenceMalformed &&
					item.Kind == LegacyConfigurationKind.Scenes), Is.True);
				Assert.That(preview.Diagnostics.Any(item =>
					item.Code == LegacyMigrationDiagnosticCode.TypeFilterAmbiguous), Is.True);
				Assert.That(preview.Diagnostics.Any(item =>
					item.Code == LegacyMigrationDiagnosticCode.AppStatesIntentionallyIgnored), Is.True);
			} finally {
				UnityEngine.Object.DestroyImmediate(legacyAddressables);
				UnityEngine.Object.DestroyImmediate(folder);
				UnityEngine.Object.DestroyImmediate(appStates);
			}
		}

		[Test]
		public void LegacyPreview_WhenBundledSamplesArePresent_PreservesTheirExactValues() {
			var addressablesPath = AssetDatabase.GUIDToAssetPath(SAMPLE_ADDRESSABLES_CONFIG_GUID);
			var scenesPath = AssetDatabase.GUIDToAssetPath(SAMPLE_SCENES_CONFIG_GUID);
			var appStatesPath = AssetDatabase.GUIDToAssetPath(SAMPLE_APP_STATES_CONFIG_GUID);
			var samplesExcluded = Environment.GetCommandLineArgs().Contains("-torSamplesExcluded");
			if (samplesExcluded) {
				Assert.That(addressablesPath, Is.Empty);
				Assert.That(scenesPath, Is.Empty);
				Assert.That(appStatesPath, Is.Empty);
				Assert.Pass("The marked clean no-Samples lane omitted every legacy sample migration fixture.");
			}

			Assert.That(addressablesPath, Is.Not.Empty, "The sample-inclusive lane must import the legacy Addressables config.");
			Assert.That(scenesPath, Is.Not.Empty, "The sample-inclusive lane must import the legacy scenes config.");
			Assert.That(appStatesPath, Is.Not.Empty, "The sample-inclusive lane must import the legacy app-state config.");
			var addressablesAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(addressablesPath);
			var scenesAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(scenesPath);
			var addressablesBefore = EditorJsonUtility.ToJson(addressablesAsset);
			var scenesBefore = EditorJsonUtility.ToJson(scenesAsset);
			var json =
				$"{{\"m_ScenesListConfigGUID\":\"{SAMPLE_SCENES_CONFIG_GUID}\"," +
				$"\"m_AddressableAssetsConfigGUID\":\"{SAMPLE_ADDRESSABLES_CONFIG_GUID}\"," +
				$"\"m_AppStatesConfigGUID\":\"{SAMPLE_APP_STATES_CONFIG_GUID}\"}}";

			var preview = LegacyConfigurationMigration.Preview(
				json,
				new UnityLegacyMigrationEnvironment());

			Assert.That(preview.GroupRules.Length, Is.EqualTo(1));
			Assert.That(preview.GroupRules[0].SourceFolderGuid, Is.EqualTo(SAMPLE_SCENES_FOLDER_GUID));
			Assert.That(preview.GroupRules[0].DestinationGroupGuid, Is.Empty);
			Assert.That(preview.GroupRules[0].DestinationGroupName, Is.EqualTo("SceneMapsGroup"));
			Assert.That(preview.GroupRules[0].RequiredLabels, Is.Empty);
			Assert.That(preview.GroupRules[0].AssemblyQualifiedTypeFilters, Is.Empty);
			Assert.That(preview.SceneRules.Length, Is.EqualTo(1));
			Assert.That(preview.SceneRules[0].SourceFolderGuid, Is.EqualTo(SAMPLE_SCENES_FOLDER_GUID));
			Assert.That(preview.SceneRules[0].Mode, Is.EqualTo(SceneFolderMode.Addressable));
			Assert.That(preview.SceneRules[0].DestinationGroupGuid, Is.Empty);
			Assert.That(preview.SceneRules[0].DestinationGroupName, Is.EqualTo("ScenesGroup"));
			Assert.That(preview.SceneRules[0].AddressPolicy, Is.EqualTo(SceneAddressPolicy.PreserveManagedAddress));
			Assert.That(preview.SceneRules[0].RequiredLabels, Is.Empty);
			Assert.That(preview.Diagnostics.Any(item =>
				item.Code == LegacyMigrationDiagnosticCode.SourceFolderOutsideAssets), Is.True);
			Assert.That(preview.Diagnostics.Any(item =>
				item.Code == LegacyMigrationDiagnosticCode.AppStatesIntentionallyIgnored), Is.True);
			Assert.That(EditorJsonUtility.ToJson(addressablesAsset), Is.EqualTo(addressablesBefore));
			Assert.That(EditorJsonUtility.ToJson(scenesAsset), Is.EqualTo(scenesBefore));
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
			internal readonly HashSet<string> AssetEntries = new HashSet<string>();

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

			public bool HasAssetEntry(string assetGuid) {
				QueryCount++;
				return AssetEntries.Contains(assetGuid);
			}
		}

		[Serializable]
		private sealed class LegacyGroupFixture {
			public string m_groupName;
			public UnityEngine.Object m_assetsFolder;
			public string[] m_lables;
			public string[] m_typesFilterNames;
			public bool m_filterByType;
		}

		private sealed class LegacyAddressablesFixture : ScriptableObject {
			public LegacyGroupFixture[] m_Settings;
		}

		private sealed class LegacyScenesFixture : ScriptableObject {
			public UnityEngine.Object m_ScenesLocation;
			public UnityEngine.Object m_UIScenesLocation;
			public UnityEngine.Object m_ScenesConfig;
			public UnityEngine.Object[] m_OtherSceneFolders;
		}

		private sealed class LegacyObjectFixture : ScriptableObject { }

		private sealed class FakeLegacyMigrationEnvironment : ILegacyMigrationEnvironment {
			internal readonly Dictionary<string, string> Paths = new Dictionary<string, string>();
			internal readonly Dictionary<string, UnityEngine.Object> Assets =
				new Dictionary<string, UnityEngine.Object>();
			internal readonly Dictionary<UnityEngine.Object, string> AssetPaths =
				new Dictionary<UnityEngine.Object, string>();
			internal readonly Dictionary<string, string> GuidsByPath = new Dictionary<string, string>();
			internal readonly HashSet<string> Folders = new HashSet<string>();
			internal readonly Dictionary<ScriptableObject, string> ScriptGuids =
				new Dictionary<ScriptableObject, string>();
			internal readonly Dictionary<string, string> GroupGuids = new Dictionary<string, string>();
			internal readonly Dictionary<string, Type[]> TypeMatches = new Dictionary<string, Type[]>();

			internal void AddAsset(
				string guid,
				string path,
				ScriptableObject asset,
				string scriptGuid) {
				Paths[guid] = path;
				Assets[path] = asset;
				AssetPaths[asset] = path;
				GuidsByPath[path] = guid;
				ScriptGuids[asset] = scriptGuid;
			}

			internal void AddFolder(UnityEngine.Object folder, string path, string guid) {
				AssetPaths[folder] = path;
				GuidsByPath[path] = guid;
				Folders.Add(path);
			}

			public string GuidToAssetPath(string guid) {
				return Paths.TryGetValue(guid, out var path) ? path : string.Empty;
			}

			public UnityEngine.Object LoadMainAssetAtPath(string path) {
				return Assets.TryGetValue(path, out var asset) ? asset : null;
			}

			public string GetAssetPath(UnityEngine.Object asset) {
				return asset != null && AssetPaths.TryGetValue(asset, out var path) ? path : string.Empty;
			}

			public string AssetPathToGuid(string path) {
				return GuidsByPath.TryGetValue(path, out var guid) ? guid : string.Empty;
			}

			public bool IsValidFolder(string path) {
				return Folders.Contains(path);
			}

			public string GetMonoScriptGuid(ScriptableObject asset) {
				return ScriptGuids.TryGetValue(asset, out var guid) ? guid : string.Empty;
			}

			public bool TryGetGroupGuid(string groupName, out string groupGuid) {
				return GroupGuids.TryGetValue(groupName, out groupGuid);
			}

			public LegacyTypeLookupResult FindTypes(string legacyName) {
				return new LegacyTypeLookupResult(
					TypeMatches.TryGetValue(legacyName, out var matches) ? matches : Array.Empty<Type>(),
					Array.Empty<string>());
			}
		}
	}
}
