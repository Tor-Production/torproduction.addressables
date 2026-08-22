using System;
using System.Collections.Generic;
using System.Linq;

namespace TorProduction.Addressables.Editor {
	public enum AutomationDiagnosticSeverity {
		Info,
		Warning,
		Error
	}

	public enum AutomationDiagnosticCode {
		InvalidScope,
		ConfigurationInvalid,
		AddressablesSettingsMissing,
		RecoveryRequired,
		SourceFolderMissing,
		TypeFilterUnresolved,
		AssetLoadFailed,
		AssetClaimConflict,
		ConfigurationAssetClaimed,
		DestinationGroupMissing,
		DestinationGroupReadOnly,
		DestinationGroupNonBuildable,
		FolderEntryConflict,
		AddressCollision,
		StalePlan,
		ApplyFailed,
		RollbackFailed,
		RecoveryFailed
	}

	public enum AutomationOperationKind {
		CreateGroup,
		AddBundledAssetGroupSchema,
		AddContentUpdateGroupSchema,
		CreateLabel,
		CreateEntry,
		MoveEntry,
		SetAddress,
		AddLabel,
		RemoveLabel
	}

	public enum AutomationRollbackStatus {
		NotRequired,
		Succeeded,
		Failed
	}

	public sealed class AutomationDiagnostic {
		internal AutomationDiagnostic(
			AutomationDiagnosticCode code,
			AutomationDiagnosticSeverity severity,
			string location,
			string message) {
			Code = code;
			Severity = severity;
			Location = location ?? string.Empty;
			Message = message ?? string.Empty;
		}

		public AutomationDiagnosticCode Code { get; }
		public AutomationDiagnosticSeverity Severity { get; }
		public string Location { get; }
		public string Message { get; }
	}

	public sealed class AutomationOperation {
		private readonly IReadOnlyList<string> m_labels;

		internal AutomationOperation(
			AutomationOperationKind kind,
			string assetGuid = "",
			string assetPath = "",
			string groupGuid = "",
			string groupName = "",
			string value = "",
			IEnumerable<string> labels = null) {
			Kind = kind;
			AssetGuid = assetGuid ?? string.Empty;
			AssetPath = assetPath ?? string.Empty;
			GroupGuid = groupGuid ?? string.Empty;
			GroupName = groupName ?? string.Empty;
			Value = value ?? string.Empty;
			m_labels = Array.AsReadOnly((labels ?? Array.Empty<string>())
				.Where(label => label != null)
				.OrderBy(label => label, StringComparer.Ordinal)
				.ToArray());
		}

		public AutomationOperationKind Kind { get; }
		public string AssetGuid { get; }
		public string AssetPath { get; }
		public string GroupGuid { get; }
		public string GroupName { get; }
		public string Value { get; }
		public IReadOnlyList<string> Labels => m_labels;

		public string Description {
			get {
				switch (Kind) {
					case AutomationOperationKind.CreateGroup:
						return $"Create group '{GroupName}'.";
					case AutomationOperationKind.AddBundledAssetGroupSchema:
						return $"Add BundledAssetGroupSchema to '{GroupName}'.";
					case AutomationOperationKind.AddContentUpdateGroupSchema:
						return $"Add ContentUpdateGroupSchema to '{GroupName}'.";
					case AutomationOperationKind.CreateLabel:
						return $"Create label '{Value}'.";
					case AutomationOperationKind.CreateEntry:
						return $"Create entry for '{AssetPath}' in '{GroupName}'.";
					case AutomationOperationKind.MoveEntry:
						return $"Move '{AssetPath}' to '{GroupName}'.";
					case AutomationOperationKind.SetAddress:
						return $"Set '{AssetPath}' address to '{Value}'.";
					case AutomationOperationKind.AddLabel:
						return $"Add label '{Value}' to '{AssetPath}'.";
					case AutomationOperationKind.RemoveLabel:
						return $"Remove label '{Value}' from '{AssetPath}'.";
					default:
						return Kind.ToString();
				}
			}
		}
	}

	public sealed class AutomationPlan {
		private readonly IReadOnlyList<AutomationOperation> m_operations;
		private readonly IReadOnlyList<AutomationDiagnostic> m_diagnostics;

		internal AutomationPlan(
			AutomationScope scope,
			string sourceHash,
			string planHash,
			IEnumerable<AutomationOperation> operations,
			IEnumerable<AutomationDiagnostic> diagnostics,
			AddressablesAutomationConfig config) {
			Scope = scope;
			SourceHash = sourceHash ?? string.Empty;
			PlanHash = planHash ?? string.Empty;
			m_operations = Array.AsReadOnly((operations ?? Array.Empty<AutomationOperation>()).ToArray());
			m_diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<AutomationDiagnostic>()).ToArray());
			Config = config;
		}

		public AutomationScope Scope { get; }
		public string SourceHash { get; }
		public string PlanHash { get; }
		public IReadOnlyList<AutomationOperation> Operations => m_operations;
		public IReadOnlyList<AutomationDiagnostic> Diagnostics => m_diagnostics;
		public bool IsValid => !m_diagnostics.Any(item => item.Severity == AutomationDiagnosticSeverity.Error);
		public bool HasChanges => m_operations.Count != 0;
		internal AddressablesAutomationConfig Config { get; }
	}

	public sealed class AutomationReport {
		private readonly IReadOnlyList<AutomationOperation> m_operations;
		private readonly IReadOnlyList<AutomationDiagnostic> m_diagnostics;
		private readonly IReadOnlyList<string> m_failures;

		internal AutomationReport(
			bool succeeded,
			IEnumerable<AutomationOperation> operations,
			IEnumerable<AutomationDiagnostic> diagnostics,
			IEnumerable<string> failures,
			AutomationRollbackStatus rollbackStatus,
			string recoveryPath) {
			Succeeded = succeeded;
			m_operations = Array.AsReadOnly((operations ?? Array.Empty<AutomationOperation>()).ToArray());
			m_diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<AutomationDiagnostic>()).ToArray());
			m_failures = Array.AsReadOnly((failures ?? Array.Empty<string>()).ToArray());
			RollbackStatus = rollbackStatus;
			RecoveryPath = recoveryPath ?? string.Empty;
		}

		public bool Succeeded { get; }
		public IReadOnlyList<AutomationOperation> Operations => m_operations;
		public IReadOnlyList<AutomationDiagnostic> Diagnostics => m_diagnostics;
		public IReadOnlyList<string> Failures => m_failures;
		public AutomationRollbackStatus RollbackStatus { get; }
		public string RecoveryPath { get; }
	}
}
