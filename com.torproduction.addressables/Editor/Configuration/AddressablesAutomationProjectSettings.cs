using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	internal enum ProjectSettingsReadStatus {
		Missing,
		Valid,
		Corrupt,
		MigrationRequired,
		UnsupportedSchema
	}

	internal readonly struct ProjectSettingsSnapshot {
		internal ProjectSettingsSnapshot(
			int schemaVersion,
			string selectedConfigGuid,
			bool automationEnabled,
			AutomationScope automaticScopes) {
			SchemaVersion = schemaVersion;
			SelectedConfigGuid = selectedConfigGuid ?? string.Empty;
			AutomationEnabled = automationEnabled;
			AutomaticScopes = automaticScopes;
		}

		internal int SchemaVersion { get; }
		internal string SelectedConfigGuid { get; }
		internal bool AutomationEnabled { get; }
		internal AutomationScope AutomaticScopes { get; }
	}

	internal readonly struct ProjectSettingsReadResult {
		internal ProjectSettingsReadResult(
			ProjectSettingsReadStatus status,
			ProjectSettingsSnapshot snapshot,
			string message) {
			Status = status;
			Snapshot = snapshot;
			Message = message ?? string.Empty;
		}

		internal ProjectSettingsReadStatus Status { get; }
		internal ProjectSettingsSnapshot Snapshot { get; }
		internal string Message { get; }
	}

	internal interface IAddressablesAutomationProjectSettingsBackend {
		bool Exists { get; }
		string Magic { get; set; }
		int SchemaVersion { get; set; }
		string SelectedConfigGuid { get; set; }
		bool AutomationEnabled { get; set; }
		AutomationScope AutomaticScopes { get; set; }
		bool TryBackup(out string recoveryPath, out string error);
		void Save();
	}

	[FilePath(
		AddressablesAutomationProjectSettingsStore.SettingsPath,
		FilePathAttribute.Location.ProjectFolder)]
	internal sealed class AddressablesAutomationProjectSettings :
		ScriptableSingleton<AddressablesAutomationProjectSettings> {
		[SerializeField] private string m_magic;
		[SerializeField] private int m_schemaVersion;
		[SerializeField] private string m_selectedConfigGuid;
		[SerializeField] private bool m_automationEnabled;
		[SerializeField] private AutomationScope m_automaticScopes;

		internal string Magic {
			get => m_magic;
			set => m_magic = value;
		}

		internal int SchemaVersion {
			get => m_schemaVersion;
			set => m_schemaVersion = value;
		}

		internal string SelectedConfigGuid {
			get => m_selectedConfigGuid;
			set => m_selectedConfigGuid = value;
		}

		internal bool AutomationEnabled {
			get => m_automationEnabled;
			set => m_automationEnabled = value;
		}

		internal AutomationScope AutomaticScopes {
			get => m_automaticScopes;
			set => m_automaticScopes = value;
		}

		internal void SaveExplicitly() {
			Save(true);
		}
	}

	internal sealed class UnityProjectSettingsBackend : IAddressablesAutomationProjectSettingsBackend {
		public bool Exists => File.Exists(AddressablesAutomationProjectSettingsStore.SettingsPath);

		public string Magic {
			get => AddressablesAutomationProjectSettings.instance.Magic;
			set => AddressablesAutomationProjectSettings.instance.Magic = value;
		}

		public int SchemaVersion {
			get => AddressablesAutomationProjectSettings.instance.SchemaVersion;
			set => AddressablesAutomationProjectSettings.instance.SchemaVersion = value;
		}

		public string SelectedConfigGuid {
			get => AddressablesAutomationProjectSettings.instance.SelectedConfigGuid;
			set => AddressablesAutomationProjectSettings.instance.SelectedConfigGuid = value;
		}

		public bool AutomationEnabled {
			get => AddressablesAutomationProjectSettings.instance.AutomationEnabled;
			set => AddressablesAutomationProjectSettings.instance.AutomationEnabled = value;
		}

		public AutomationScope AutomaticScopes {
			get => AddressablesAutomationProjectSettings.instance.AutomaticScopes;
			set => AddressablesAutomationProjectSettings.instance.AutomaticScopes = value;
		}

		public bool TryBackup(out string recoveryPath, out string error) {
			recoveryPath = string.Empty;
			error = string.Empty;
			if (!Exists) {
				return true;
			}

			try {
				Directory.CreateDirectory(AddressablesAutomationProjectSettingsStore.RecoveryDirectory);
				recoveryPath = Path.Combine(
					AddressablesAutomationProjectSettingsStore.RecoveryDirectory,
					$"project-settings-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.asset");
				File.Copy(AddressablesAutomationProjectSettingsStore.SettingsPath, recoveryPath, false);
				return true;
			} catch (Exception exception) {
				error = $"Could not back up the existing project settings: {exception.Message}";
				recoveryPath = string.Empty;
				return false;
			}
		}

		public void Save() {
			Directory.CreateDirectory(Path.GetDirectoryName(
				AddressablesAutomationProjectSettingsStore.SettingsPath) ?? "ProjectSettings");
			AddressablesAutomationProjectSettings.instance.SaveExplicitly();
		}
	}

	internal static class AddressablesAutomationProjectSettingsStore {
		internal const int CurrentSchemaVersion = 1;
		internal const AutomationScope SupportedAutomaticScopes = AutomationScope.Scenes;
		internal const string SettingsPath =
			"ProjectSettings/TorProduction/AddressablesAutomationProjectSettings.asset";
		internal const string ExpectedMagic = "TorProduction.AddressablesAutomationProjectSettings";
		internal const string RecoveryDirectory = "Library/TorProduction.Addressables/Recovery";

		private static readonly Regex s_guidPattern = new Regex(
			"^[0-9a-fA-F]{32}$",
			RegexOptions.CultureInvariant);

		internal static ProjectSettingsReadResult Read() {
			return Read(new UnityProjectSettingsBackend());
		}

		internal static ProjectSettingsReadResult Read(
			IAddressablesAutomationProjectSettingsBackend backend) {
			if (!backend.Exists) {
				return new ProjectSettingsReadResult(
					ProjectSettingsReadStatus.Missing,
					default,
					"No Addressables Automation project settings have been saved.");
			}

			try {
				var snapshot = new ProjectSettingsSnapshot(
					backend.SchemaVersion,
					backend.SelectedConfigGuid,
					backend.AutomationEnabled,
					backend.AutomaticScopes);

				if (!string.Equals(backend.Magic, ExpectedMagic, StringComparison.Ordinal)) {
					return new ProjectSettingsReadResult(
						ProjectSettingsReadStatus.Corrupt,
						snapshot,
						"The project settings signature is missing or invalid. Use explicit recovery; the file was not rewritten.");
				}

				if (snapshot.SchemaVersion < CurrentSchemaVersion) {
					return new ProjectSettingsReadResult(
						ProjectSettingsReadStatus.MigrationRequired,
						snapshot,
						$"Project settings schema {snapshot.SchemaVersion} requires an explicit migration to {CurrentSchemaVersion}.");
				}

				if (snapshot.SchemaVersion > CurrentSchemaVersion) {
					return new ProjectSettingsReadResult(
						ProjectSettingsReadStatus.UnsupportedSchema,
						snapshot,
						$"Project settings schema {snapshot.SchemaVersion} is newer than supported schema {CurrentSchemaVersion}.");
				}

				if (!string.IsNullOrEmpty(snapshot.SelectedConfigGuid) &&
				    !s_guidPattern.IsMatch(snapshot.SelectedConfigGuid)) {
					return new ProjectSettingsReadResult(
						ProjectSettingsReadStatus.Corrupt,
						snapshot,
						"The selected configuration GUID is malformed. The stored value was retained.");
				}

				if ((snapshot.AutomaticScopes & ~SupportedAutomaticScopes) != 0) {
					return new ProjectSettingsReadResult(
						ProjectSettingsReadStatus.Corrupt,
						snapshot,
						"Project settings contain unsupported automatic-scope flags. The stored value was retained.");
				}

				if (string.IsNullOrEmpty(snapshot.SelectedConfigGuid) &&
				    (snapshot.AutomationEnabled || snapshot.AutomaticScopes != AutomationScope.None)) {
					return new ProjectSettingsReadResult(
						ProjectSettingsReadStatus.Corrupt,
						snapshot,
						"Automatic processing cannot be enabled without a selected configuration. The stored values were retained.");
				}

				if (snapshot.AutomationEnabled !=
				    (snapshot.AutomaticScopes != AutomationScope.None)) {
					return new ProjectSettingsReadResult(
						ProjectSettingsReadStatus.Corrupt,
						snapshot,
						"Automatic opt-in and scope values are inconsistent. The stored values were retained.");
				}

				return new ProjectSettingsReadResult(
					ProjectSettingsReadStatus.Valid,
					snapshot,
					string.Empty);
			} catch (Exception exception) {
				return new ProjectSettingsReadResult(
					ProjectSettingsReadStatus.Corrupt,
					default,
					$"Project settings could not be read: {exception.Message}");
			}
		}

		internal static bool TryPersistSelection(
			string configGuid,
			IAddressablesAutomationProjectSettingsBackend backend,
			out string error) {
			if (string.IsNullOrEmpty(configGuid) || !s_guidPattern.IsMatch(configGuid)) {
				error = "Select a persistent configuration asset with a valid Unity GUID.";
				return false;
			}

			var current = Read(backend);
			if (current.Status != ProjectSettingsReadStatus.Missing &&
			    current.Status != ProjectSettingsReadStatus.Valid) {
				error = current.Message;
				return false;
			}

			var selectionChanged = !string.Equals(
				current.Snapshot.SelectedConfigGuid,
				configGuid,
				StringComparison.OrdinalIgnoreCase);
			return TryMutateAndSave(
				backend,
				() => {
					backend.Magic = ExpectedMagic;
					backend.SchemaVersion = CurrentSchemaVersion;
					backend.SelectedConfigGuid = configGuid.ToLowerInvariant();
					if (current.Status == ProjectSettingsReadStatus.Missing || selectionChanged) {
						backend.AutomationEnabled = false;
						backend.AutomaticScopes = AutomationScope.None;
					}
				},
				"save the selected configuration",
				out error);
		}

		internal static bool TryWriteValidatedAutomation(
			bool enabled,
			AutomationScope scopes,
			IAddressablesAutomationProjectSettingsBackend backend,
			out string error) {
			var current = Read(backend);
			if (current.Status != ProjectSettingsReadStatus.Valid) {
				error = current.Message;
				return false;
			}

			if (string.IsNullOrEmpty(current.Snapshot.SelectedConfigGuid)) {
				error = "Select a configuration before applying automation settings.";
				return false;
			}

			if ((scopes & ~SupportedAutomaticScopes) != 0 ||
			    (enabled && scopes == AutomationScope.None)) {
				error = "Only explicitly opted-in scene postprocessing is supported.";
				return false;
			}

			return TryMutateAndSave(
				backend,
				() => {
					backend.AutomationEnabled = enabled;
					backend.AutomaticScopes = enabled ? scopes : AutomationScope.None;
				},
				"save automatic-processing settings",
				out error);
		}

		internal static bool TryDetach(out string error) {
			return TryDetach(new UnityProjectSettingsBackend(), out error);
		}

		internal static bool TryDetach(
			IAddressablesAutomationProjectSettingsBackend backend,
			out string error) {
			var current = Read(backend);
			if (current.Status == ProjectSettingsReadStatus.Missing) {
				error = string.Empty;
				return true;
			}

			if (current.Status != ProjectSettingsReadStatus.Valid) {
				error = current.Message;
				return false;
			}

			return TryMutateAndSave(
				backend,
				() => {
					backend.SelectedConfigGuid = string.Empty;
					backend.AutomationEnabled = false;
					backend.AutomaticScopes = AutomationScope.None;
				},
				"detach the selected configuration",
				out error);
		}

		internal static bool TryRecover(out string recoveryPath, out string error) {
			return TryRecover(new UnityProjectSettingsBackend(), out recoveryPath, out error);
		}

		internal static bool TryRecover(
			IAddressablesAutomationProjectSettingsBackend backend,
			out string recoveryPath,
			out string error) {
			var current = Read(backend);
			if (current.Status == ProjectSettingsReadStatus.Missing) {
				recoveryPath = string.Empty;
				error = "There is no saved project state to recover.";
				return false;
			}

			if (current.Status == ProjectSettingsReadStatus.Valid) {
				recoveryPath = string.Empty;
				error = "Project state is valid. Use Detach instead of recovery.";
				return false;
			}

			if (!backend.TryBackup(out recoveryPath, out error)) {
				return false;
			}

			if (TryMutateAndSave(
				    backend,
				    () => {
					    backend.Magic = ExpectedMagic;
					    backend.SchemaVersion = CurrentSchemaVersion;
					    backend.SelectedConfigGuid = string.Empty;
					    backend.AutomationEnabled = false;
					    backend.AutomaticScopes = AutomationScope.None;
				    },
				    "recover project settings",
				    out error)) {
				return true;
			}

			return false;
		}

		internal static bool IsGuid(string value) {
			return !string.IsNullOrEmpty(value) && s_guidPattern.IsMatch(value);
		}

		private static bool TryMutateAndSave(
			IAddressablesAutomationProjectSettingsBackend backend,
			Action mutation,
			string operation,
			out string error) {
			var previous = default(BackendState);
			var capturedPrevious = false;
			try {
				previous = new BackendState(backend);
				capturedPrevious = true;
				mutation();
				backend.Save();
				error = string.Empty;
				return true;
			} catch (Exception exception) {
				try {
					if (capturedPrevious) {
						previous.Restore(backend);
					}
				} catch (Exception restoreException) {
					error = $"Could not {operation}: {exception.Message}. In-memory rollback also failed: {restoreException.Message}";
					return false;
				}

				error = $"Could not {operation}: {exception.Message}";
				return false;
			}
		}

		private readonly struct BackendState {
			internal BackendState(IAddressablesAutomationProjectSettingsBackend backend) {
				Magic = backend.Magic;
				SchemaVersion = backend.SchemaVersion;
				SelectedConfigGuid = backend.SelectedConfigGuid;
				AutomationEnabled = backend.AutomationEnabled;
				AutomaticScopes = backend.AutomaticScopes;
			}

			private string Magic { get; }
			private int SchemaVersion { get; }
			private string SelectedConfigGuid { get; }
			private bool AutomationEnabled { get; }
			private AutomationScope AutomaticScopes { get; }

			internal void Restore(IAddressablesAutomationProjectSettingsBackend backend) {
				backend.Magic = Magic;
				backend.SchemaVersion = SchemaVersion;
				backend.SelectedConfigGuid = SelectedConfigGuid;
				backend.AutomationEnabled = AutomationEnabled;
				backend.AutomaticScopes = AutomaticScopes;
			}
		}
	}
}
