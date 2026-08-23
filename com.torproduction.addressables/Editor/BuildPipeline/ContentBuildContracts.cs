using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace TorProduction.Addressables.Editor {
	public enum ContentBuildKind {
		Full,
		ContentUpdate,
		EditorCompatible,
		MultiPlatform
	}

	public enum ContentBuildPlatform {
		Android,
		iOS,
		Windows,
		macOS,
		Linux
	}

	public enum ContentBuildFailurePolicy {
		StopOnFirstFailure,
		ContinueOnError
	}

	public enum ContentBuildStatus {
		Success,
		Warning,
		FatalFailure,
		Cancellation,
		Skipped,
		TargetSwitchFailure,
		RestorationFailure,
		AwaitingResume
	}

	public enum ContentBuildDiagnosticSeverity {
		Info,
		Warning,
		Error
	}

	public enum ContentBuildDiagnosticCode {
		InvalidRequest,
		AddressablesSettingsMissing,
		AddressablesVersionUnsupported,
		PlayerDataBuilderMissing,
		TargetUnsupported,
		StateFileRequired,
		StateFileMissing,
		StateFileInvalid,
		StateFileIncompatible,
		ContentUpdateRestriction,
		ExistingJobPending,
		JobStateMissing,
		JobStateInvalid,
		JobStateStale,
		ResumeRequired,
		TargetSwitchFailed,
		BuildFailed,
		BuildCancelled,
		BuildLayoutMissing,
		BuildLayoutStale,
		BuildLayoutCopyFailed,
		ReceiptMissing,
		ReceiptInvalid,
		ReceiptStale,
		ReceiptTargetMismatch,
		ExistingBuildConfirmationRequired,
		ExistingBuildBuilderMissing,
		ExistingBuildSelected,
		RestorationFailed,
		JobAbandoned
	}

	public sealed class ContentBuildRequest {
		private readonly IReadOnlyList<ContentBuildPlatform> m_platforms;

		public ContentBuildRequest(
			ContentBuildKind kind,
			ContentBuildPlatform platform = ContentBuildPlatform.Windows,
			IEnumerable<ContentBuildPlatform> platforms = null,
			string stateFilePath = "",
			ContentBuildFailurePolicy failurePolicy = ContentBuildFailurePolicy.StopOnFirstFailure) {
			Kind = kind;
			Platform = platform;
			m_platforms = Array.AsReadOnly((platforms ?? Array.Empty<ContentBuildPlatform>()).ToArray());
			StateFilePath = stateFilePath ?? string.Empty;
			FailurePolicy = failurePolicy;
		}

		public ContentBuildKind Kind { get; }
		public ContentBuildPlatform Platform { get; }
		public IReadOnlyList<ContentBuildPlatform> Platforms => m_platforms;
		public string StateFilePath { get; }
		public ContentBuildFailurePolicy FailurePolicy { get; }

		public static ContentBuildRequest Full(ContentBuildPlatform platform) =>
			new ContentBuildRequest(ContentBuildKind.Full, platform);

		public static ContentBuildRequest ContentUpdate(
			ContentBuildPlatform platform,
			string stateFilePath) =>
			new ContentBuildRequest(
				ContentBuildKind.ContentUpdate,
				platform,
				stateFilePath: stateFilePath);

		public static ContentBuildRequest EditorCompatible() =>
			new ContentBuildRequest(ContentBuildKind.EditorCompatible);

		public static ContentBuildRequest MultiPlatform(
			IEnumerable<ContentBuildPlatform> platforms,
			ContentBuildFailurePolicy failurePolicy = ContentBuildFailurePolicy.StopOnFirstFailure) =>
			new ContentBuildRequest(
				ContentBuildKind.MultiPlatform,
				platforms: platforms,
				failurePolicy: failurePolicy);
	}

	public sealed class ContentBuildDiagnostic {
		internal ContentBuildDiagnostic(
			ContentBuildDiagnosticCode code,
			ContentBuildDiagnosticSeverity severity,
			string message,
			BuildTarget target = BuildTarget.NoTarget) {
			Code = code;
			Severity = severity;
			Message = message ?? string.Empty;
			Target = target;
		}

		public ContentBuildDiagnosticCode Code { get; }
		public ContentBuildDiagnosticSeverity Severity { get; }
		public string Message { get; }
		public BuildTarget Target { get; }
	}

	public sealed class ContentBuildPreflight {
		private readonly IReadOnlyList<BuildTarget> m_targets;
		private readonly IReadOnlyList<ContentBuildDiagnostic> m_diagnostics;

		internal ContentBuildPreflight(
			ContentBuildRequest request,
			IEnumerable<BuildTarget> targets,
			IEnumerable<ContentBuildDiagnostic> diagnostics,
			string requestHash,
			string settingsGuid,
			string settingsHash,
			string stateFileHash,
			string addressablesVersion) {
			Request = request;
			m_targets = Array.AsReadOnly((targets ?? Array.Empty<BuildTarget>()).ToArray());
			m_diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<ContentBuildDiagnostic>()).ToArray());
			RequestHash = requestHash ?? string.Empty;
			SettingsGuid = settingsGuid ?? string.Empty;
			SettingsHash = settingsHash ?? string.Empty;
			StateFileHash = stateFileHash ?? string.Empty;
			AddressablesVersion = addressablesVersion ?? string.Empty;
		}

		public ContentBuildRequest Request { get; }
		public IReadOnlyList<BuildTarget> Targets => m_targets;
		public IReadOnlyList<ContentBuildDiagnostic> Diagnostics => m_diagnostics;
		public string RequestHash { get; }
		public string SettingsGuid { get; }
		public string SettingsHash { get; }
		public string StateFileHash { get; }
		public string AddressablesVersion { get; }
		public bool IsValid => Request != null &&
		                       m_targets.Count > 0 &&
		                       m_diagnostics.All(item => item.Severity != ContentBuildDiagnosticSeverity.Error);
	}

	public sealed class ContentBuildItemResult {
		internal ContentBuildItemResult(
			BuildTarget target,
			ContentBuildStatus status,
			string message,
			string outputPath = "",
			string layoutPath = "",
			string receiptPath = "") {
			Target = target;
			Status = status;
			Message = message ?? string.Empty;
			OutputPath = outputPath ?? string.Empty;
			LayoutPath = layoutPath ?? string.Empty;
			ReceiptPath = receiptPath ?? string.Empty;
		}

		public BuildTarget Target { get; }
		public ContentBuildStatus Status { get; }
		public string Message { get; }
		public string OutputPath { get; }
		public string LayoutPath { get; }
		public string ReceiptPath { get; }
	}

	public sealed class ContentBuildResult {
		private readonly IReadOnlyList<ContentBuildItemResult> m_items;
		private readonly IReadOnlyList<ContentBuildDiagnostic> m_diagnostics;

		internal ContentBuildResult(
			string jobId,
			ContentBuildStatus status,
			string message,
			IEnumerable<ContentBuildItemResult> items,
			IEnumerable<ContentBuildDiagnostic> diagnostics,
			string reportPath,
			string recoveryPath,
			bool requiresUserAction) {
			JobId = jobId ?? string.Empty;
			Status = status;
			Message = message ?? string.Empty;
			m_items = Array.AsReadOnly((items ?? Array.Empty<ContentBuildItemResult>()).ToArray());
			m_diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<ContentBuildDiagnostic>()).ToArray());
			ReportPath = reportPath ?? string.Empty;
			RecoveryPath = recoveryPath ?? string.Empty;
			RequiresUserAction = requiresUserAction;
		}

		public string JobId { get; }
		public ContentBuildStatus Status { get; }
		public string Message { get; }
		public IReadOnlyList<ContentBuildItemResult> Items => m_items;
		public IReadOnlyList<ContentBuildDiagnostic> Diagnostics => m_diagnostics;
		public string ReportPath { get; }
		public string RecoveryPath { get; }
		public bool RequiresUserAction { get; }
		public bool Succeeded => Status == ContentBuildStatus.Success || Status == ContentBuildStatus.Warning;
	}

	public sealed class ExistingBuildValidation {
		private readonly IReadOnlyList<ContentBuildDiagnostic> m_diagnostics;

		internal ExistingBuildValidation(
			bool isValid,
			string receiptPath,
			BuildTarget target,
			IEnumerable<ContentBuildDiagnostic> diagnostics) {
			IsValid = isValid;
			ReceiptPath = receiptPath ?? string.Empty;
			Target = target;
			m_diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<ContentBuildDiagnostic>()).ToArray());
		}

		public bool IsValid { get; }
		public string ReceiptPath { get; }
		public BuildTarget Target { get; }
		public IReadOnlyList<ContentBuildDiagnostic> Diagnostics => m_diagnostics;
	}

	[Serializable]
	public sealed class ContentBuildReceipt {
		public const int CurrentSchema = 1;
		public int schemaVersion = CurrentSchema;
		public string jobId = string.Empty;
		public string buildKind = string.Empty;
		public string target = string.Empty;
		public string settingsGuid = string.Empty;
		public string settingsHash = string.Empty;
		public string addressablesVersion = string.Empty;
		public string unityVersion = string.Empty;
		public string outputPath = string.Empty;
		public string settingsFilePath = string.Empty;
		public string settingsFileHash = string.Empty;
		public long settingsFileLength;
		public long settingsFileLastWriteUtcTicks;
		public long buildCompletedUtcTicks;
		public long createdUtcTicks;
	}

	public sealed class ContentBuildRecoveryInfo {
		internal ContentBuildRecoveryInfo(
			bool exists,
			bool isValid,
			bool isStale,
			string jobId,
			string stage,
			BuildTarget originalTarget,
			BuildTarget activeTarget,
			IEnumerable<BuildTarget> pendingTargets,
			string message,
			string statePath) {
			Exists = exists;
			IsValid = isValid;
			IsStale = isStale;
			JobId = jobId ?? string.Empty;
			Stage = stage ?? string.Empty;
			OriginalTarget = originalTarget;
			ActiveTarget = activeTarget;
			PendingTargets = Array.AsReadOnly((pendingTargets ?? Array.Empty<BuildTarget>()).ToArray());
			Message = message ?? string.Empty;
			StatePath = statePath ?? string.Empty;
		}

		public bool Exists { get; }
		public bool IsValid { get; }
		public bool IsStale { get; }
		public string JobId { get; }
		public string Stage { get; }
		public BuildTarget OriginalTarget { get; }
		public BuildTarget ActiveTarget { get; }
		public IReadOnlyList<BuildTarget> PendingTargets { get; }
		public string Message { get; }
		public string StatePath { get; }
	}

	internal enum BuildJobStage {
		Prepared,
		SwitchingTarget,
		AwaitingResume,
		Building,
		RestoringOriginalTarget,
		Completed,
		Failed,
		Cancelled,
		RestorationFailed,
		Abandoned
	}

	[Serializable]
	internal sealed class BuildJobItemRecord {
		public string target;
		public string status;
		public string message;
		public string outputPath;
		public string layoutPath;
		public string receiptPath;
	}

	[Serializable]
	internal sealed class BuildJobRecord {
		public const int CurrentSchema = 1;
		public int schemaVersion = CurrentSchema;
		public string jobId;
		public string buildKind;
		public string stage;
		public string failurePolicy;
		public string[] allTargets = Array.Empty<string>();
		public string[] pendingTargets = Array.Empty<string>();
		public BuildJobItemRecord[] completed = Array.Empty<BuildJobItemRecord>();
		public string originalTarget;
		public string activeTarget;
		public string stateFilePath;
		public string stateFileHash;
		public string settingsGuid;
		public string settingsHash;
		public string addressablesVersion;
		public string requestHash;
		public string operationDirectory;
		public string reportPath;
		public string receiptPath;
		public bool cancellationRequested;
		public string failureMessage;
		public string recoveryMessage;
		public long createdUtcTicks;
		public long updatedUtcTicks;

		public bool TryGetStage(out BuildJobStage value) => Enum.TryParse(stage, out value);
		public bool TryGetKind(out ContentBuildKind value) => Enum.TryParse(buildKind, out value);
		public bool TryGetFailurePolicy(out ContentBuildFailurePolicy value) => Enum.TryParse(failurePolicy, out value);

		public bool IsStale(long utcNowTicks, long staleAfterTicks) =>
			updatedUtcTicks <= 0 || utcNowTicks - updatedUtcTicks > staleAfterTicks;
	}
}
