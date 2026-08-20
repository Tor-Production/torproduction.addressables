using System;
using System.Linq;

namespace TorProduction.Addressables.Editor {
	internal enum ConfigurationStatus {
		InvalidScope,
		NotConfigured,
		CorruptProjectState,
		ProjectStateMigrationRequired,
		UnsupportedProjectStateSchema,
		SelectedConfigMissing,
		WrongConfigType,
		ConfigOutsideAssets,
		ConfigOutsideEditorFolder,
		ConfigMigrationRequired,
		UnsupportedConfigSchema,
		AddressablesSettingsMissing,
		InvalidConfig,
		AutomationDisabled,
		ScopeDisabled,
		Ready
	}

	internal readonly struct ConfigurationResolution {
		internal ConfigurationResolution(
			ConfigurationStatus status,
			string message,
			ProjectSettingsSnapshot projectSettings,
			string configPath,
			AddressablesAutomationConfig config,
			ConfigurationValidationReport validation) {
			Status = status;
			Message = message ?? string.Empty;
			ProjectSettings = projectSettings;
			ConfigPath = configPath ?? string.Empty;
			Config = config;
			Validation = validation;
		}

		internal ConfigurationStatus Status { get; }
		internal string Message { get; }
		internal ProjectSettingsSnapshot ProjectSettings { get; }
		internal string ConfigPath { get; }
		internal AddressablesAutomationConfig Config { get; }
		internal ConfigurationValidationReport Validation { get; }
		internal bool IsReady => Status == ConfigurationStatus.Ready;
	}

	internal static class AddressablesAutomationContextProvider {
		internal static ConfigurationResolution ResolveManual(AutomationScope scope) {
			return Resolve(
				scope,
				false,
				new UnityProjectSettingsBackend(),
				UnityConfigurationAssetResolver.Instance,
				() => new AddressablesSettingsView());
		}

		internal static ConfigurationResolution ResolveAutomatic(AutomationScope scope) {
			return Resolve(
				scope,
				true,
				new UnityProjectSettingsBackend(),
				UnityConfigurationAssetResolver.Instance,
				() => new AddressablesSettingsView());
		}

		internal static ConfigurationResolution Resolve(
			AutomationScope scope,
			bool requireAutomaticOptIn,
			IAddressablesAutomationProjectSettingsBackend backend,
			IConfigurationAssetResolver resolver,
			IAddressablesSettingsView addressables) {
			return Resolve(
				scope,
				requireAutomaticOptIn,
				backend,
				resolver,
				() => addressables);
		}

		private static ConfigurationResolution Resolve(
			AutomationScope scope,
			bool requireAutomaticOptIn,
			IAddressablesAutomationProjectSettingsBackend backend,
			IConfigurationAssetResolver resolver,
			Func<IAddressablesSettingsView> addressablesFactory) {
			if (scope == AutomationScope.None || (scope & ~AutomationScope.All) != 0) {
				return Disabled(
					ConfigurationStatus.InvalidScope,
					"Select at least one supported automation scope.",
					default);
			}

			var projectSettings = AddressablesAutomationProjectSettingsStore.Read(backend);
			switch (projectSettings.Status) {
				case ProjectSettingsReadStatus.Missing:
					return Disabled(
						ConfigurationStatus.NotConfigured,
						projectSettings.Message,
						projectSettings.Snapshot);
				case ProjectSettingsReadStatus.Corrupt:
					return Disabled(
						ConfigurationStatus.CorruptProjectState,
						projectSettings.Message,
						projectSettings.Snapshot);
				case ProjectSettingsReadStatus.MigrationRequired:
					return Disabled(
						ConfigurationStatus.ProjectStateMigrationRequired,
						projectSettings.Message,
						projectSettings.Snapshot);
				case ProjectSettingsReadStatus.UnsupportedSchema:
					return Disabled(
						ConfigurationStatus.UnsupportedProjectStateSchema,
						projectSettings.Message,
						projectSettings.Snapshot);
			}

			var snapshot = projectSettings.Snapshot;
			if (requireAutomaticOptIn && !snapshot.AutomationEnabled) {
				return Disabled(
					ConfigurationStatus.AutomationDisabled,
					"Automatic Addressables processing is disabled in project settings.",
					snapshot);
			}

			if (requireAutomaticOptIn && (snapshot.AutomaticScopes & scope) != scope) {
				return Disabled(
					ConfigurationStatus.ScopeDisabled,
					$"Automatic processing is not enabled for scope '{scope}'.",
					snapshot);
			}

			if (string.IsNullOrEmpty(snapshot.SelectedConfigGuid)) {
				return Disabled(
					ConfigurationStatus.NotConfigured,
					"No Addressables Automation configuration is selected.",
					snapshot);
			}

			var configPath = resolver.GuidToAssetPath(snapshot.SelectedConfigGuid);
			if (string.IsNullOrEmpty(configPath)) {
				return new ConfigurationResolution(
					ConfigurationStatus.SelectedConfigMissing,
					$"Selected configuration GUID '{snapshot.SelectedConfigGuid}' does not resolve. The GUID was retained so restoring the asset can recover the selection.",
					snapshot,
					string.Empty,
					null,
					null);
			}

			var mainAsset = resolver.LoadMainAssetAtPath(configPath);
			if (!(mainAsset is AddressablesAutomationConfig config)) {
				return new ConfigurationResolution(
					ConfigurationStatus.WrongConfigType,
					$"Selected GUID resolves to '{configPath}', which is not an AddressablesAutomationConfig asset.",
					snapshot,
					configPath,
					null,
					null);
			}

			var normalizedConfigPath = configPath.Replace('\\', '/');
			if (!normalizedConfigPath.StartsWith("Assets/", StringComparison.Ordinal)) {
				return new ConfigurationResolution(
					ConfigurationStatus.ConfigOutsideAssets,
					"Configuration assets must live under the project Assets folder, not inside a package or generated directory.",
					snapshot,
					configPath,
					config,
					null);
			}

			if (!IsEditorOnlyAssetPath(normalizedConfigPath)) {
				return new ConfigurationResolution(
					ConfigurationStatus.ConfigOutsideEditorFolder,
					"Configuration assets must live in an Editor folder so they cannot enter player data.",
					snapshot,
					configPath,
					config,
					null);
			}

			var validation = AddressablesAutomationValidator.Validate(
				config,
				resolver,
				addressablesFactory(),
				scope);
			if (!validation.IsValid) {
				var status = DetermineInvalidStatus(validation);
				var firstError = validation.Diagnostics.First(item =>
					item.Severity == ConfigurationDiagnosticSeverity.Error);
				return new ConfigurationResolution(
					status,
					firstError.Message,
					snapshot,
					configPath,
					config,
					validation);
			}

			return new ConfigurationResolution(
				ConfigurationStatus.Ready,
				string.Empty,
				snapshot,
				configPath,
				config,
				validation);
		}

		internal static bool TrySelectConfig(string configGuid, out string error) {
			return TrySelectConfig(
				configGuid,
				new UnityProjectSettingsBackend(),
				UnityConfigurationAssetResolver.Instance,
				out error);
		}

		internal static bool TrySelectConfig(
			string configGuid,
			IAddressablesAutomationProjectSettingsBackend backend,
			IConfigurationAssetResolver resolver,
			out string error) {
			if (!AddressablesAutomationProjectSettingsStore.IsGuid(configGuid)) {
				error = "Select a persistent AddressablesAutomationConfig asset with a valid Unity GUID.";
				return false;
			}

			var path = resolver.GuidToAssetPath(configGuid)?.Replace('\\', '/');
			if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)) {
				error = "The selected configuration must be a persistent asset under the project Assets folder.";
				return false;
			}

			if (!IsEditorOnlyAssetPath(path)) {
				error = "The selected configuration must live in an Editor folder so it cannot enter player data.";
				return false;
			}

			if (!(resolver.LoadMainAssetAtPath(path) is AddressablesAutomationConfig)) {
				error = $"The selected asset at '{path}' is not an AddressablesAutomationConfig.";
				return false;
			}

			return AddressablesAutomationProjectSettingsStore.TryPersistSelection(
				configGuid,
				backend,
				out error);
		}

		internal static bool TryApplyAutomaticSceneProcessing(
			bool enabled,
			out string error) {
			return TryApplyAutomaticSceneProcessing(
				enabled,
				new UnityProjectSettingsBackend(),
				UnityConfigurationAssetResolver.Instance,
				new AddressablesSettingsView(),
				out error);
		}

		internal static bool TryApplyAutomaticSceneProcessing(
			bool enabled,
			IAddressablesAutomationProjectSettingsBackend backend,
			IConfigurationAssetResolver resolver,
			IAddressablesSettingsView addressables,
			out string error) {
			if (enabled) {
				var resolution = Resolve(
					AutomationScope.Scenes,
					false,
					backend,
					resolver,
					addressables);
				if (!resolution.IsReady) {
					error = resolution.Message;
					return false;
				}
			}

			return AddressablesAutomationProjectSettingsStore.TryWriteValidatedAutomation(
				enabled,
				enabled ? AutomationScope.Scenes : AutomationScope.None,
				backend,
				out error);
		}

		private static bool IsEditorOnlyAssetPath(string path) {
			return path.StartsWith("Assets/Editor/", StringComparison.Ordinal) ||
			       path.IndexOf("/Editor/", StringComparison.Ordinal) >= 0;
		}

		private static ConfigurationStatus DetermineInvalidStatus(
			ConfigurationValidationReport validation) {
			if (validation.Diagnostics.Any(item =>
				    item.Code == ConfigurationDiagnosticCode.ConfigSchemaMigrationRequired)) {
				return ConfigurationStatus.ConfigMigrationRequired;
			}

			if (validation.Diagnostics.Any(item =>
				    item.Code == ConfigurationDiagnosticCode.ConfigSchemaUnsupported)) {
				return ConfigurationStatus.UnsupportedConfigSchema;
			}

			if (validation.Diagnostics.Any(item =>
				    item.Code == ConfigurationDiagnosticCode.AddressablesSettingsMissing)) {
				return ConfigurationStatus.AddressablesSettingsMissing;
			}

			return ConfigurationStatus.InvalidConfig;
		}

		private static ConfigurationResolution Disabled(
			ConfigurationStatus status,
			string message,
			ProjectSettingsSnapshot snapshot) {
			return new ConfigurationResolution(
				status,
				message,
				snapshot,
				string.Empty,
				null,
				null);
		}
	}
}
