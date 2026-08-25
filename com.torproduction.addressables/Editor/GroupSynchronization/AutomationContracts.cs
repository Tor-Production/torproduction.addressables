using System;
using System.Collections.Generic;
using System.Linq;

namespace TorProduction.Addressables.Editor {
	/// <summary>Identifies the severity of an automation plan or Apply diagnostic.</summary>
	public enum AutomationDiagnosticSeverity {
		/// <summary>Informational context that does not block execution.</summary>
		Info,
		/// <summary>A non-blocking condition requiring attention.</summary>
		Warning,
		/// <summary>A condition that blocks or fails execution.</summary>
		Error
	}

	/// <summary>Provides stable machine-readable automation diagnostic codes.</summary>
	public enum AutomationDiagnosticCode {
		/// <summary>The requested automation scope is unsupported.</summary>
		InvalidScope,
		/// <summary>The selected configuration is missing or invalid.</summary>
		ConfigurationInvalid,
		/// <summary>The project has no Addressables settings.</summary>
		AddressablesSettingsMissing,
		/// <summary>A prior incomplete Apply must be recovered first.</summary>
		RecoveryRequired,
		/// <summary>A configured source folder no longer exists.</summary>
		SourceFolderMissing,
		/// <summary>A configured asset type filter cannot be resolved.</summary>
		TypeFilterUnresolved,
		/// <summary>An asset claimed by a rule could not be loaded.</summary>
		AssetLoadFailed,
		/// <summary>Multiple rules claim an asset incompatibly.</summary>
		AssetClaimConflict,
		/// <summary>A rule would include the automation configuration itself.</summary>
		ConfigurationAssetClaimed,
		/// <summary>A required destination group cannot be resolved or planned.</summary>
		DestinationGroupMissing,
		/// <summary>A destination group is read-only.</summary>
		DestinationGroupReadOnly,
		/// <summary>A destination group lacks the required build schema.</summary>
		DestinationGroupNonBuildable,
		/// <summary>An Addressable folder entry conflicts with explicit descendant management.</summary>
		FolderEntryConflict,
		/// <summary>Two planned entries would use the same generated address.</summary>
		AddressCollision,
		/// <summary>The installed Addressables version has a verified dependency adapter.</summary>
		DependencyAdapterVerified,
		/// <summary>No verified dependency adapter exists for the installed Addressables version.</summary>
		DependencyAdapterUnsupported,
		/// <summary>The built-in duplicate-dependency analysis failed.</summary>
		DependencyAnalysisFailed,
		/// <summary>A duplicate dependency is already an explicit Addressables entry.</summary>
		DependencyAlreadyExplicit,
		/// <summary>A duplicate-dependency fix requires explicit confirmation.</summary>
		DependencyFixConfirmationRequired,
		/// <summary>Multiple scene rules claim a scene incompatibly.</summary>
		SceneClaimConflict,
		/// <summary>A scene claimed by a rule could not be loaded.</summary>
		SceneLoadFailed,
		/// <summary>Project or configuration state changed after analysis.</summary>
		StalePlan,
		/// <summary>An operation failed during Apply.</summary>
		ApplyFailed,
		/// <summary>Automatic rollback did not fully restore the snapshot.</summary>
		RollbackFailed,
		/// <summary>Explicit recovery did not fully restore the snapshot.</summary>
		RecoveryFailed
	}

	/// <summary>Identifies a deterministic project mutation proposed by an automation plan.</summary>
	public enum AutomationOperationKind {
		/// <summary>Creates an Addressables group.</summary>
		CreateGroup,
		/// <summary>Adds a bundled-asset schema to a group.</summary>
		AddBundledAssetGroupSchema,
		/// <summary>Adds a content-update schema to a group.</summary>
		AddContentUpdateGroupSchema,
		/// <summary>Creates an Addressables label.</summary>
		CreateLabel,
		/// <summary>Creates an explicit Addressables entry.</summary>
		CreateEntry,
		/// <summary>Moves an existing entry to another group.</summary>
		MoveEntry,
		/// <summary>Sets an entry's Addressables address.</summary>
		SetAddress,
		/// <summary>Adds a label to an entry.</summary>
		AddLabel,
		/// <summary>Removes a package-managed label from an entry.</summary>
		RemoveLabel,
		/// <summary>Removes a package-managed Addressables entry.</summary>
		RemoveEntry,
		/// <summary>Reconciles package-managed local scenes in Build Settings.</summary>
		UpdateBuildSettings,
		/// <summary>Persists the managed-scene identity records.</summary>
		UpdateManagedScenes
	}

	/// <summary>Describes whether Apply rollback was needed and completed.</summary>
	public enum AutomationRollbackStatus {
		/// <summary>No mutation required rollback.</summary>
		NotRequired,
		/// <summary>The pre-Apply snapshot was fully restored.</summary>
		Succeeded,
		/// <summary>Rollback was incomplete and recovery remains required.</summary>
		Failed
	}

	/// <summary>Describes one automation analysis, Apply, or recovery finding.</summary>
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

		/// <summary>Gets the stable diagnostic code.</summary>
		public AutomationDiagnosticCode Code { get; }
		/// <summary>Gets the diagnostic severity.</summary>
		public AutomationDiagnosticSeverity Severity { get; }
		/// <summary>Gets the affected rule, asset, or workflow location.</summary>
		public string Location { get; }
		/// <summary>Gets the actionable diagnostic message.</summary>
		public string Message { get; }
	}

	/// <summary>Describes one deterministic project mutation proposed by Analyze.</summary>
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

		/// <summary>Gets the operation kind.</summary>
		public AutomationOperationKind Kind { get; }
		/// <summary>Gets the affected asset GUID, when applicable.</summary>
		public string AssetGuid { get; }
		/// <summary>Gets the affected asset path, when applicable.</summary>
		public string AssetPath { get; }
		/// <summary>Gets the persistent destination-group GUID, when applicable.</summary>
		public string GroupGuid { get; }
		/// <summary>Gets the destination-group display name, when applicable.</summary>
		public string GroupName { get; }
		/// <summary>Gets the operation-specific address, label, or serialized value.</summary>
		public string Value { get; }
		/// <summary>Gets the immutable labels associated with the operation.</summary>
		public IReadOnlyList<string> Labels => m_labels;

		/// <summary>Gets a concise human-readable description of the proposed mutation.</summary>
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
					case AutomationOperationKind.RemoveEntry:
						return $"Remove package-managed scene entry '{AssetPath}'.";
					case AutomationOperationKind.UpdateBuildSettings:
						return "Reconcile package-managed local scenes in Build Settings.";
					case AutomationOperationKind.UpdateManagedScenes:
						return "Persist managed scene identities and last-known paths.";
					default:
						return Kind.ToString();
				}
			}
		}
	}

	/// <summary>Represents an immutable, deterministic and previewable automation plan.</summary>
	public sealed class AutomationPlan {
		private readonly IReadOnlyList<AutomationOperation> m_operations;
		private readonly IReadOnlyList<AutomationDiagnostic> m_diagnostics;

		internal AutomationPlan(
			AutomationScope scope,
			string sourceHash,
			string planHash,
			IEnumerable<AutomationOperation> operations,
			IEnumerable<AutomationDiagnostic> diagnostics,
			AddressablesAutomationConfig config,
			SceneSyncMutation sceneMutation = null,
			string configGuid = "") {
			Scope = scope;
			SourceHash = sourceHash ?? string.Empty;
			PlanHash = planHash ?? string.Empty;
			m_operations = Array.AsReadOnly((operations ?? Array.Empty<AutomationOperation>()).ToArray());
			m_diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<AutomationDiagnostic>()).ToArray());
			Config = config;
			SceneMutation = sceneMutation;
			ConfigGuid = configGuid ?? string.Empty;
		}

		/// <summary>Gets the single automation scope represented by the plan.</summary>
		public AutomationScope Scope { get; }
		/// <summary>Gets the deterministic fingerprint of analyzed project and configuration state.</summary>
		public string SourceHash { get; }
		/// <summary>Gets the deterministic fingerprint of the complete ordered plan.</summary>
		public string PlanHash { get; }
		/// <summary>Gets the ordered immutable proposed operations.</summary>
		public IReadOnlyList<AutomationOperation> Operations => m_operations;
		/// <summary>Gets the ordered immutable analysis diagnostics.</summary>
		public IReadOnlyList<AutomationDiagnostic> Diagnostics => m_diagnostics;
		/// <summary>Gets whether the plan contains no blocking diagnostic.</summary>
		public bool IsValid => !m_diagnostics.Any(item => item.Severity == AutomationDiagnosticSeverity.Error);
		/// <summary>Gets whether Apply would perform at least one project mutation.</summary>
		public bool HasChanges => m_operations.Count != 0;
		internal AddressablesAutomationConfig Config { get; private set; }
		internal SceneSyncMutation SceneMutation { get; }
		internal string ConfigGuid { get; }
		internal void BindConfig(AddressablesAutomationConfig config) => Config = config;
	}

	/// <summary>Describes the mutations, diagnostics, failures, and rollback state from Apply or recovery.</summary>
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

		/// <summary>Gets whether the requested operation completed safely.</summary>
		public bool Succeeded { get; }
		/// <summary>Gets the operations completed before the report was produced.</summary>
		public IReadOnlyList<AutomationOperation> Operations => m_operations;
		/// <summary>Gets the ordered operation diagnostics.</summary>
		public IReadOnlyList<AutomationDiagnostic> Diagnostics => m_diagnostics;
		/// <summary>Gets human-readable failure descriptions.</summary>
		public IReadOnlyList<string> Failures => m_failures;
		/// <summary>Gets whether rollback was required and completed.</summary>
		public AutomationRollbackStatus RollbackStatus { get; }
		/// <summary>Gets the retained recovery snapshot path, if any.</summary>
		public string RecoveryPath { get; }
	}
}
