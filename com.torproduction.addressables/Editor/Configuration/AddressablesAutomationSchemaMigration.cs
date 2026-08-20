using System;
using System.IO;
using System.Text;
using UnityEditor;

namespace TorProduction.Addressables.Editor {
	internal static class AddressablesAutomationSchemaMigration {
		internal static bool TryMigrateConfig(
			AddressablesAutomationConfig config,
			out string recoveryPath,
			out string error) {
			recoveryPath = string.Empty;
			if (config == null) {
				error = "Select a configuration asset to migrate.";
				return false;
			}
			if (!AddressablesAutomationContextProvider.TryValidateConfigCandidate(config, out error)) {
				return false;
			}

			var assetPath = (AssetDatabase.GetAssetPath(config) ?? string.Empty).Replace('\\', '/');
			if (string.IsNullOrEmpty(assetPath) ||
			    (!assetPath.StartsWith("Assets/Editor/", StringComparison.Ordinal) &&
			     assetPath.IndexOf("/Editor/", StringComparison.Ordinal) < 0)) {
				error = "Only persistent configuration assets in an Editor folder can be migrated.";
				return false;
			}

			if (config.SchemaVersion > AddressablesAutomationConfig.CurrentSchemaVersion) {
				error = $"Configuration schema {config.SchemaVersion} is newer than this package supports.";
				return false;
			}
			if (config.SchemaVersion == AddressablesAutomationConfig.CurrentSchemaVersion) {
				error = "Configuration already uses the current schema.";
				return false;
			}

			var before = EditorJsonUtility.ToJson(config, true);
			try {
				Directory.CreateDirectory(AddressablesAutomationProjectSettingsStore.RecoveryDirectory);
				var guid = AssetDatabase.AssetPathToGUID(assetPath);
				recoveryPath = Path.Combine(
					AddressablesAutomationProjectSettingsStore.RecoveryDirectory,
					$"config-schema-{guid}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
				File.WriteAllText(recoveryPath, before, new UTF8Encoding(false));

				if (!config.TryMigrateToCurrentSchema(out error)) {
					return false;
				}

				EditorUtility.SetDirty(config);
				AssetDatabase.SaveAssetIfDirty(config);
				error = string.Empty;
				return true;
			} catch (Exception exception) {
				try {
					EditorJsonUtility.FromJsonOverwrite(before, config);
					EditorUtility.SetDirty(config);
					AssetDatabase.SaveAssetIfDirty(config);
				} catch (Exception restoreException) {
					error = $"Configuration migration failed: {exception.Message}. In-memory restore also failed: {restoreException.Message}";
					return false;
				}

				error = $"Configuration migration failed and was restored: {exception.Message}";
				return false;
			}
		}
	}
}
