using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	internal interface IGroupSyncMutationBackend {
		string RecoveryPath { get; }
		void Begin(AutomationPlan plan);
		void Execute(AutomationOperation operation);
		void Commit();
		void Complete();
		bool TryRollback(out string error);
	}

	internal static class GroupSyncTransaction {
		internal static AutomationReport Apply(
			AutomationPlan plan,
			IGroupSyncMutationBackend backend) {
			if (plan == null) {
				throw new ArgumentNullException(nameof(plan));
			}
			if (backend == null) {
				throw new ArgumentNullException(nameof(backend));
			}
			if (!plan.IsValid) {
				return new AutomationReport(
					false, Array.Empty<AutomationOperation>(), plan.Diagnostics,
					new[] { "The plan has blocking diagnostics and was not applied." },
					AutomationRollbackStatus.NotRequired, string.Empty);
			}
			if (!plan.HasChanges) {
				return new AutomationReport(
					true, Array.Empty<AutomationOperation>(), plan.Diagnostics,
					Array.Empty<string>(), AutomationRollbackStatus.NotRequired, string.Empty);
			}

			var applied = new List<AutomationOperation>();
			try {
				backend.Begin(plan);
				foreach (var operation in plan.Operations) {
					backend.Execute(operation);
					applied.Add(operation);
				}
				backend.Commit();
				backend.Complete();
				return new AutomationReport(
					true, applied, plan.Diagnostics, Array.Empty<string>(),
					AutomationRollbackStatus.NotRequired, string.Empty);
			} catch (Exception exception) {
				var diagnostics = new List<AutomationDiagnostic>(plan.Diagnostics) {
					new AutomationDiagnostic(
						AutomationDiagnosticCode.ApplyFailed,
						AutomationDiagnosticSeverity.Error,
						"Apply",
						$"Group synchronization failed: {exception.Message}")
				};
				if (backend.TryRollback(out var rollbackError)) {
					return new AutomationReport(
						false, applied, diagnostics, new[] { exception.Message },
						AutomationRollbackStatus.Succeeded, string.Empty);
				}

				diagnostics.Add(new AutomationDiagnostic(
					AutomationDiagnosticCode.RollbackFailed,
					AutomationDiagnosticSeverity.Error,
					"Rollback",
					rollbackError));
				return new AutomationReport(
					false, applied, diagnostics,
					new[] { exception.Message, rollbackError },
					AutomationRollbackStatus.Failed, backend.RecoveryPath);
			}
		}
	}

	internal sealed class UnityGroupSyncMutationBackend : IGroupSyncMutationBackend {
		private readonly AddressableAssetSettings m_settings;
		private GroupSyncRecoverySnapshot m_snapshot;
		private string m_recoveryPath = string.Empty;

		internal UnityGroupSyncMutationBackend(AddressableAssetSettings settings) {
			m_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		}

		public string RecoveryPath => m_recoveryPath;

		public void Begin(AutomationPlan plan) {
			if (m_snapshot != null) {
				throw new InvalidOperationException("This mutation backend has already started a transaction.");
			}
			m_snapshot = CaptureSnapshot(m_settings, plan);
			Directory.CreateDirectory(GroupSyncRecovery.RecoveryDirectory);
			m_recoveryPath = Path.Combine(
				GroupSyncRecovery.RecoveryDirectory,
				$"group-sync-{m_snapshot.operationId}.json");
			SaveSnapshot();
		}

		public void Execute(AutomationOperation operation) {
			if (operation == null) {
				throw new ArgumentNullException(nameof(operation));
			}
			switch (operation.Kind) {
				case AutomationOperationKind.CreateGroup:
					CreateGroup(operation);
					break;
				case AutomationOperationKind.AddBundledAssetGroupSchema:
					AddSchema<BundledAssetGroupSchema>(operation);
					break;
				case AutomationOperationKind.AddContentUpdateGroupSchema:
					AddSchema<ContentUpdateGroupSchema>(operation);
					break;
				case AutomationOperationKind.CreateLabel:
					m_settings.AddLabel(operation.Value, false);
					break;
				case AutomationOperationKind.CreateEntry:
				case AutomationOperationKind.MoveEntry:
					CreateOrMoveEntry(operation);
					break;
				case AutomationOperationKind.SetAddress:
					RequireEntry(operation.AssetGuid).SetAddress(operation.Value, false);
					break;
				case AutomationOperationKind.AddLabel:
					RequireEntry(operation.AssetGuid).SetLabel(operation.Value, true, false, false);
					break;
				case AutomationOperationKind.RemoveLabel:
					RequireEntry(operation.AssetGuid).SetLabel(operation.Value, false, false, false);
					break;
				default:
					throw new InvalidOperationException($"Unsupported group operation '{operation.Kind}'.");
			}
		}

		public void Commit() {
			m_settings.SetDirty(
				AddressableAssetSettings.ModificationEvent.BatchModification,
				null, true, true);
			AssetDatabase.SaveAssets();
		}

		public void Complete() {
			DeleteSnapshot();
		}

		public bool TryRollback(out string error) {
			if (m_snapshot == null) {
				error = "No recovery snapshot was available.";
				return false;
			}
			if (TryRestore(m_settings, m_snapshot, out error)) {
				DeleteSnapshot();
				return true;
			}

			m_snapshot.status = GroupSyncRecoverySnapshot.RequiresRecoveryStatus;
			m_snapshot.lastError = error ?? string.Empty;
			try {
				SaveSnapshot();
			} catch (Exception saveException) {
				error = $"{error} The recovery snapshot also could not be updated: {saveException.Message}";
			}
			return false;
		}

		private void CreateGroup(AutomationOperation operation) {
			if (m_settings.FindGroup(operation.GroupName) != null) {
				throw new InvalidOperationException($"Group '{operation.GroupName}' already exists; re-analyze before Apply.");
			}
			var group = m_settings.CreateGroup(
				operation.GroupName, false, false, false,
				new List<AddressableAssetGroupSchema>());
			if (group == null || !string.Equals(group.Name, operation.GroupName, StringComparison.Ordinal)) {
				throw new InvalidOperationException($"Addressables could not create group '{operation.GroupName}'.");
			}
			var groupSnapshot = m_snapshot.groups.First(item =>
				string.Equals(item.name, operation.GroupName, StringComparison.Ordinal));
			groupSnapshot.createdGuid = group.Guid ?? string.Empty;
			SaveSnapshot();
		}

		private void AddSchema<TSchema>(AutomationOperation operation)
			where TSchema : AddressableAssetGroupSchema {
			var group = RequireGroup(operation.GroupGuid, operation.GroupName);
			if (group.GetSchema<TSchema>() == null && group.AddSchema<TSchema>(false) == null) {
				throw new InvalidOperationException(
					$"Addressables could not add {typeof(TSchema).Name} to '{group.Name}'.");
			}
		}

		private void CreateOrMoveEntry(AutomationOperation operation) {
			var group = RequireGroup(operation.GroupGuid, operation.GroupName);
			if (m_settings.CreateOrMoveEntry(operation.AssetGuid, group, false, false) == null) {
				throw new InvalidOperationException($"Addressables could not create or move entry '{operation.AssetGuid}'.");
			}
		}

		private AddressableAssetEntry RequireEntry(string guid) {
			var entry = m_settings.FindAssetEntry(guid);
			return entry ?? throw new InvalidOperationException($"Addressables entry '{guid}' no longer exists.");
		}

		private AddressableAssetGroup RequireGroup(string guid, string name) {
			var group = FindGroup(m_settings, guid, name);
			return group ?? throw new InvalidOperationException(
				$"Addressables group '{name}' ({guid}) no longer exists.");
		}

		private void SaveSnapshot() {
			var json = JsonUtility.ToJson(m_snapshot, true);
			File.WriteAllText(m_recoveryPath, json, new UTF8Encoding(false));
		}

		private void DeleteSnapshot() {
			if (!string.IsNullOrEmpty(m_recoveryPath) && File.Exists(m_recoveryPath)) {
				File.Delete(m_recoveryPath);
			}
		}

		internal static GroupSyncRecoverySnapshot CaptureSnapshot(
			AddressableAssetSettings settings,
			AutomationPlan plan) {
			var snapshot = new GroupSyncRecoverySnapshot {
				operationId = Guid.NewGuid().ToString("N"),
				createdUtc = DateTime.UtcNow.ToString("O"),
				status = GroupSyncRecoverySnapshot.PendingStatus,
				planHash = plan.PlanHash,
				entries = plan.Operations.Where(item => !string.IsNullOrEmpty(item.AssetGuid))
					.Select(item => item.AssetGuid)
					.Distinct(StringComparer.Ordinal)
					.OrderBy(item => item, StringComparer.Ordinal)
					.Select(guid => CaptureEntry(settings, guid))
					.ToList(),
				groups = plan.Operations
					.Where(item => !string.IsNullOrEmpty(item.GroupName))
					.GroupBy(item => string.IsNullOrEmpty(item.GroupGuid)
						? "name:" + item.GroupName
						: "guid:" + item.GroupGuid, StringComparer.Ordinal)
					.Select(group => CaptureGroup(settings, group.First().GroupGuid, group.First().GroupName))
					.OrderBy(item => item.name, StringComparer.Ordinal)
					.ToList(),
				createdLabels = plan.Operations
					.Where(item => item.Kind == AutomationOperationKind.CreateLabel)
					.Select(item => item.Value)
					.Distinct(StringComparer.Ordinal)
					.OrderBy(item => item, StringComparer.Ordinal)
					.ToList()
			};
			return snapshot;
		}

		private static GroupSyncRecoveryEntry CaptureEntry(
			AddressableAssetSettings settings,
			string guid) {
			var entry = settings.FindAssetEntry(guid);
			return entry == null
				? new GroupSyncRecoveryEntry { guid = guid, existed = false }
				: new GroupSyncRecoveryEntry {
					guid = guid,
					existed = true,
					groupGuid = entry.parentGroup?.Guid ?? string.Empty,
					groupName = entry.parentGroup?.Name ?? string.Empty,
					address = entry.address ?? string.Empty,
					readOnly = entry.ReadOnly,
					labels = entry.labels?.Where(label => label != null)
						.OrderBy(label => label, StringComparer.Ordinal).ToList() ?? new List<string>()
				};
		}

		private static GroupSyncRecoveryGroup CaptureGroup(
			AddressableAssetSettings settings,
			string guid,
			string name) {
			var group = FindGroup(settings, guid, name);
			return new GroupSyncRecoveryGroup {
				guid = group?.Guid ?? guid ?? string.Empty,
				name = group?.Name ?? name ?? string.Empty,
				existed = group != null,
				hadBundledSchema = group?.GetSchema<BundledAssetGroupSchema>() != null,
				hadContentUpdateSchema = group?.GetSchema<ContentUpdateGroupSchema>() != null
			};
		}

		internal static bool TryRestore(
			AddressableAssetSettings settings,
			GroupSyncRecoverySnapshot snapshot,
			out string error) {
			try {
				foreach (var item in snapshot.entries.OrderBy(entry => entry.guid, StringComparer.Ordinal)) {
					var current = settings.FindAssetEntry(item.guid);
					if (!item.existed) {
						current?.parentGroup?.RemoveAssetEntry(current, false);
						continue;
					}
					var group = FindGroup(settings, item.groupGuid, item.groupName);
					if (group == null) {
						throw new InvalidOperationException(
							$"Original group '{item.groupName}' ({item.groupGuid}) is unavailable for entry '{item.guid}'.");
					}
					var restored = settings.CreateOrMoveEntry(item.guid, group, item.readOnly, false);
					if (restored == null) {
						throw new InvalidOperationException($"Entry '{item.guid}' could not be restored.");
					}
					restored.SetAddress(item.address ?? string.Empty, false);
					var desiredLabels = new HashSet<string>(item.labels ?? new List<string>(), StringComparer.Ordinal);
					foreach (var label in restored.labels.ToArray()) {
						if (!desiredLabels.Contains(label)) {
							restored.SetLabel(label, false, false, false);
						}
					}
					foreach (var label in desiredLabels) {
						restored.SetLabel(label, true, true, false);
					}
				}

				foreach (var item in snapshot.groups.Where(group => group.existed)) {
					var group = FindGroup(settings, item.guid, item.name);
					if (group == null) {
						throw new InvalidOperationException($"Original group '{item.name}' is unavailable during rollback.");
					}
					if (!item.hadBundledSchema) {
						group.RemoveSchema<BundledAssetGroupSchema>(false);
					}
					if (!item.hadContentUpdateSchema) {
						group.RemoveSchema<ContentUpdateGroupSchema>(false);
					}
				}

				foreach (var item in snapshot.groups.Where(group => !group.existed)
				         .OrderByDescending(group => group.name, StringComparer.Ordinal)) {
					var group = FindGroup(settings, item.createdGuid, item.name);
					if (group != null) {
						settings.RemoveGroup(group);
					}
				}

				foreach (var label in snapshot.createdLabels.OrderBy(item => item, StringComparer.Ordinal)) {
					settings.RemoveLabel(label, false);
				}
				settings.SetDirty(
					AddressableAssetSettings.ModificationEvent.BatchModification,
					null, true, true);
				AssetDatabase.SaveAssets();
				error = string.Empty;
				return true;
			} catch (Exception exception) {
				error = $"Rollback is incomplete: {exception.Message}";
				return false;
			}
		}

		internal static AddressableAssetGroup FindGroup(
			AddressableAssetSettings settings,
			string guid,
			string name) {
			if (!string.IsNullOrEmpty(guid)) {
				var byGuid = settings.groups.FirstOrDefault(group =>
					group != null && string.Equals(group.Guid, guid, StringComparison.Ordinal));
				if (byGuid != null) {
					return byGuid;
				}
			}
			return string.IsNullOrEmpty(name) ? null : settings.FindGroup(name);
		}
	}

	internal static class GroupSyncRecovery {
		internal static readonly string RecoveryDirectory =
			Path.Combine("Library", "TorProduction.Addressables", "Recovery");

		internal static bool TryFindPending(out string recoveryPath) {
			recoveryPath = string.Empty;
			if (!Directory.Exists(RecoveryDirectory)) {
				return false;
			}
			recoveryPath = Directory.EnumerateFiles(
				RecoveryDirectory, "group-sync-*.json", SearchOption.TopDirectoryOnly)
				.OrderBy(path => path, StringComparer.Ordinal)
				.FirstOrDefault() ?? string.Empty;
			return !string.IsNullOrEmpty(recoveryPath);
		}

		internal static AutomationReport Recover(string recoveryPath = null) {
			if (string.IsNullOrEmpty(recoveryPath) && !TryFindPending(out recoveryPath)) {
				return new AutomationReport(
					true, Array.Empty<AutomationOperation>(), Array.Empty<AutomationDiagnostic>(),
					Array.Empty<string>(), AutomationRollbackStatus.NotRequired, string.Empty);
			}

			try {
				var fullDirectory = Path.GetFullPath(RecoveryDirectory)
					.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
				var fullPath = Path.GetFullPath(recoveryPath);
				if (!fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase) ||
				    !File.Exists(fullPath)) {
					throw new InvalidOperationException("The recovery path is outside the package-owned recovery directory or no longer exists.");
				}
				if (!AddressableAssetSettingsDefaultObject.SettingsExists) {
					throw new InvalidOperationException("Addressables settings are missing; the snapshot cannot be restored.");
				}
				var snapshot = JsonUtility.FromJson<GroupSyncRecoverySnapshot>(File.ReadAllText(fullPath));
				if (snapshot == null || string.IsNullOrEmpty(snapshot.operationId)) {
					throw new InvalidOperationException("The recovery snapshot is corrupt or incompatible.");
				}
				var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
				if (!UnityGroupSyncMutationBackend.TryRestore(settings, snapshot, out var restoreError)) {
					throw new InvalidOperationException(restoreError);
				}
				File.Delete(fullPath);
				return new AutomationReport(
					true, Array.Empty<AutomationOperation>(), Array.Empty<AutomationDiagnostic>(),
					Array.Empty<string>(), AutomationRollbackStatus.Succeeded, string.Empty);
			} catch (Exception exception) {
				return new AutomationReport(
					false, Array.Empty<AutomationOperation>(),
					new[] { new AutomationDiagnostic(
						AutomationDiagnosticCode.RecoveryFailed,
						AutomationDiagnosticSeverity.Error,
						"Recovery",
						exception.Message) },
					new[] { exception.Message }, AutomationRollbackStatus.Failed,
					recoveryPath ?? string.Empty);
			}
		}
	}

	[Serializable]
	internal sealed class GroupSyncRecoverySnapshot {
		internal const string PendingStatus = "Pending";
		internal const string RequiresRecoveryStatus = "RequiresRecovery";
		public string operationId = string.Empty;
		public string createdUtc = string.Empty;
		public string status = PendingStatus;
		public string planHash = string.Empty;
		public string lastError = string.Empty;
		public List<GroupSyncRecoveryEntry> entries = new List<GroupSyncRecoveryEntry>();
		public List<GroupSyncRecoveryGroup> groups = new List<GroupSyncRecoveryGroup>();
		public List<string> createdLabels = new List<string>();
	}

	[Serializable]
	internal sealed class GroupSyncRecoveryEntry {
		public string guid = string.Empty;
		public bool existed;
		public string groupGuid = string.Empty;
		public string groupName = string.Empty;
		public string address = string.Empty;
		public bool readOnly;
		public List<string> labels = new List<string>();
	}

	[Serializable]
	internal sealed class GroupSyncRecoveryGroup {
		public string guid = string.Empty;
		public string createdGuid = string.Empty;
		public string name = string.Empty;
		public bool existed;
		public bool hadBundledSchema;
		public bool hadContentUpdateSchema;
	}
}
