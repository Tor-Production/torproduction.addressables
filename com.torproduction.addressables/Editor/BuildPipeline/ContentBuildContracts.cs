using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace TorProduction.Addressables.Editor {
	/// <summary>Identifies the Addressables content-build workflow to execute.</summary>
	public enum ContentBuildKind {
		/// <summary>Creates a new full player-content build.</summary>
		Full,
		/// <summary>Updates a previous build from an explicit content-state file.</summary>
		ContentUpdate,
		/// <summary>Creates content and a freshness receipt for the current editor host.</summary>
		EditorCompatible,
		/// <summary>Executes an explicitly ordered queue of platform builds.</summary>
		MultiPlatform
	}

	/// <summary>Identifies a platform supported by the content-build API.</summary>
	public enum ContentBuildPlatform {
		/// <summary>Android player content.</summary>
		Android,
		/// <summary>iOS player content.</summary>
		iOS,
		/// <summary>64-bit Windows player content.</summary>
		Windows,
		/// <summary>macOS player content.</summary>
		macOS,
		/// <summary>64-bit Linux player content.</summary>
		Linux
	}

	/// <summary>Controls whether a multi-platform queue stops after a failed item.</summary>
	public enum ContentBuildFailurePolicy {
		/// <summary>Stops the queue after its first failed item.</summary>
		StopOnFirstFailure,
		/// <summary>Continues with independent queued items after a failure.</summary>
		ContinueOnError
	}

	/// <summary>Describes the terminal or resumable state of a content-build operation.</summary>
	public enum ContentBuildStatus {
		/// <summary>The operation completed successfully.</summary>
		Success,
		/// <summary>The operation completed with non-fatal diagnostics.</summary>
		Warning,
		/// <summary>The operation failed and cannot continue without a new request.</summary>
		FatalFailure,
		/// <summary>The operation was cancelled.</summary>
		Cancellation,
		/// <summary>The operation was deliberately skipped.</summary>
		Skipped,
		/// <summary>Unity could not switch to the requested build target.</summary>
		TargetSwitchFailure,
		/// <summary>Unity could not restore the original build target.</summary>
		RestorationFailure,
		/// <summary>The persisted build job is waiting for a domain-reload resume.</summary>
		AwaitingResume
	}

	/// <summary>Identifies the severity of a content-build diagnostic.</summary>
	public enum ContentBuildDiagnosticSeverity {
		/// <summary>Informational context that does not block the request.</summary>
		Info,
		/// <summary>A non-fatal condition requiring attention.</summary>
		Warning,
		/// <summary>A condition that blocks or fails the request.</summary>
		Error
	}

	/// <summary>Provides stable machine-readable codes for content-build diagnostics.</summary>
	public enum ContentBuildDiagnosticCode {
		/// <summary>The request is missing required or supported values.</summary>
		InvalidRequest,
		/// <summary>The project has no Addressables settings.</summary>
		AddressablesSettingsMissing,
		/// <summary>The installed Addressables version is not supported by the workflow.</summary>
		AddressablesVersionUnsupported,
		/// <summary>The Addressables player-content data builder is unavailable.</summary>
		PlayerDataBuilderMissing,
		/// <summary>The requested Unity build target is unsupported or its module is missing.</summary>
		TargetUnsupported,
		/// <summary>A content-update request did not specify a state file.</summary>
		StateFileRequired,
		/// <summary>The specified content-state file does not exist.</summary>
		StateFileMissing,
		/// <summary>The specified content-state file could not be read or parsed.</summary>
		StateFileInvalid,
		/// <summary>The content-state file is incompatible with the requested target or project.</summary>
		StateFileIncompatible,
		/// <summary>Addressables content-update restrictions block the request.</summary>
		ContentUpdateRestriction,
		/// <summary>Another persisted build job must be resolved first.</summary>
		ExistingJobPending,
		/// <summary>No persisted job state exists for the requested recovery action.</summary>
		JobStateMissing,
		/// <summary>The persisted build-job state is corrupt or unsupported.</summary>
		JobStateInvalid,
		/// <summary>The persisted build-job state is too old to resume safely.</summary>
		JobStateStale,
		/// <summary>A domain reload occurred and the job requires an explicit resume.</summary>
		ResumeRequired,
		/// <summary>Unity rejected a requested build-target switch.</summary>
		TargetSwitchFailed,
		/// <summary>The Addressables build operation failed.</summary>
		BuildFailed,
		/// <summary>The Addressables build operation was cancelled.</summary>
		BuildCancelled,
		/// <summary>No build-layout artifact was produced.</summary>
		BuildLayoutMissing,
		/// <summary>The available build-layout artifact predates the current build.</summary>
		BuildLayoutStale,
		/// <summary>The build-layout artifact could not be copied to package-owned output.</summary>
		BuildLayoutCopyFailed,
		/// <summary>No editor-compatible build receipt exists.</summary>
		ReceiptMissing,
		/// <summary>The editor-compatible build receipt is corrupt or unsupported.</summary>
		ReceiptInvalid,
		/// <summary>The editor-compatible receipt no longer matches project content.</summary>
		ReceiptStale,
		/// <summary>The receipt target does not match the active editor-compatible target.</summary>
		ReceiptTargetMismatch,
		/// <summary>Selecting the existing-build data builder requires explicit confirmation.</summary>
		ExistingBuildConfirmationRequired,
		/// <summary>The built-in existing-build Play Mode data builder is unavailable.</summary>
		ExistingBuildBuilderMissing,
		/// <summary>The built-in existing-build Play Mode data builder was selected.</summary>
		ExistingBuildSelected,
		/// <summary>The original Unity build target could not be restored.</summary>
		RestorationFailed,
		/// <summary>The persisted build job was explicitly abandoned.</summary>
		JobAbandoned
	}

	/// <summary>Defines an immutable content-build request.</summary>
	public sealed class ContentBuildRequest {
		private readonly IReadOnlyList<ContentBuildPlatform> m_platforms;

		/// <summary>Creates a content-build request with explicit workflow and failure behavior.</summary>
		/// <param name="kind">The build workflow to execute.</param>
		/// <param name="platform">The single target for non-queue workflows.</param>
		/// <param name="platforms">The ordered targets for a multi-platform workflow.</param>
		/// <param name="stateFilePath">The prior content-state file used by a content update.</param>
		/// <param name="failurePolicy">The multi-platform queue failure policy.</param>
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

		/// <summary>Gets the requested build workflow.</summary>
		public ContentBuildKind Kind { get; }
		/// <summary>Gets the single target used by non-queue workflows.</summary>
		public ContentBuildPlatform Platform { get; }
		/// <summary>Gets the immutable ordered targets for a multi-platform request.</summary>
		public IReadOnlyList<ContentBuildPlatform> Platforms => m_platforms;
		/// <summary>Gets the explicit prior content-state path for a content update.</summary>
		public string StateFilePath { get; }
		/// <summary>Gets the failure policy for a multi-platform queue.</summary>
		public ContentBuildFailurePolicy FailurePolicy { get; }

		/// <summary>Creates a full-build request for one platform.</summary>
		/// <param name="platform">The platform to build.</param>
		/// <returns>A full-build request.</returns>
		public static ContentBuildRequest Full(ContentBuildPlatform platform) =>
			new ContentBuildRequest(ContentBuildKind.Full, platform);

		/// <summary>Creates a content-update request using an explicit prior state file.</summary>
		/// <param name="platform">The platform to update.</param>
		/// <param name="stateFilePath">The prior release content-state file.</param>
		/// <returns>A content-update request.</returns>
		public static ContentBuildRequest ContentUpdate(
			ContentBuildPlatform platform,
			string stateFilePath) =>
			new ContentBuildRequest(
				ContentBuildKind.ContentUpdate,
				platform,
				stateFilePath: stateFilePath);

		/// <summary>Creates a full build request compatible with the current editor host.</summary>
		/// <returns>An editor-compatible build request.</returns>
		public static ContentBuildRequest EditorCompatible() =>
			new ContentBuildRequest(ContentBuildKind.EditorCompatible);

		/// <summary>Creates an explicitly ordered multi-platform build request.</summary>
		/// <param name="platforms">The platforms to build.</param>
		/// <param name="failurePolicy">The queue failure policy.</param>
		/// <returns>A multi-platform build request.</returns>
		public static ContentBuildRequest MultiPlatform(
			IEnumerable<ContentBuildPlatform> platforms,
			ContentBuildFailurePolicy failurePolicy = ContentBuildFailurePolicy.StopOnFirstFailure) =>
			new ContentBuildRequest(
				ContentBuildKind.MultiPlatform,
				platforms: platforms,
				failurePolicy: failurePolicy);
	}

	/// <summary>Describes one machine-readable content-build diagnostic.</summary>
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

		/// <summary>Gets the stable diagnostic code.</summary>
		public ContentBuildDiagnosticCode Code { get; }
		/// <summary>Gets the diagnostic severity.</summary>
		public ContentBuildDiagnosticSeverity Severity { get; }
		/// <summary>Gets the actionable diagnostic message.</summary>
		public string Message { get; }
		/// <summary>Gets the affected exact Unity target, or <see cref="BuildTarget.NoTarget"/>.</summary>
		public BuildTarget Target { get; }
	}

	/// <summary>Represents the immutable result of validating a content-build request.</summary>
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

		/// <summary>Gets the validated request.</summary>
		public ContentBuildRequest Request { get; }
		/// <summary>Gets the exact Unity targets in execution order.</summary>
		public IReadOnlyList<BuildTarget> Targets => m_targets;
		/// <summary>Gets the ordered preflight diagnostics.</summary>
		public IReadOnlyList<ContentBuildDiagnostic> Diagnostics => m_diagnostics;
		/// <summary>Gets the deterministic request fingerprint.</summary>
		public string RequestHash { get; }
		/// <summary>Gets the Addressables settings asset GUID captured during analysis.</summary>
		public string SettingsGuid { get; }
		/// <summary>Gets the serialized Addressables settings fingerprint.</summary>
		public string SettingsHash { get; }
		/// <summary>Gets the content-state file fingerprint when one is required.</summary>
		public string StateFileHash { get; }
		/// <summary>Gets the installed Addressables package version used by preflight.</summary>
		public string AddressablesVersion { get; }
		/// <summary>Gets whether the request has targets and no blocking diagnostics.</summary>
		public bool IsValid => Request != null &&
		                       m_targets.Count > 0 &&
		                       m_diagnostics.All(item => item.Severity != ContentBuildDiagnosticSeverity.Error);
	}

	/// <summary>Describes the result of one exact target within a build job.</summary>
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

		/// <summary>Gets the exact Unity build target.</summary>
		public BuildTarget Target { get; }
		/// <summary>Gets the item status.</summary>
		public ContentBuildStatus Status { get; }
		/// <summary>Gets the item result message.</summary>
		public string Message { get; }
		/// <summary>Gets the produced Addressables output path, if any.</summary>
		public string OutputPath { get; }
		/// <summary>Gets the preserved build-layout path, if any.</summary>
		public string LayoutPath { get; }
		/// <summary>Gets the editor-compatible receipt path, if any.</summary>
		public string ReceiptPath { get; }
	}

	/// <summary>Represents the complete result and recovery state of a build job.</summary>
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

		/// <summary>Gets the persistent build-job identifier.</summary>
		public string JobId { get; }
		/// <summary>Gets the overall job status.</summary>
		public ContentBuildStatus Status { get; }
		/// <summary>Gets the overall result message.</summary>
		public string Message { get; }
		/// <summary>Gets immutable per-target results.</summary>
		public IReadOnlyList<ContentBuildItemResult> Items => m_items;
		/// <summary>Gets immutable job diagnostics.</summary>
		public IReadOnlyList<ContentBuildDiagnostic> Diagnostics => m_diagnostics;
		/// <summary>Gets the structured operation-report path.</summary>
		public string ReportPath { get; }
		/// <summary>Gets the persisted recovery-state path, if recovery is required.</summary>
		public string RecoveryPath { get; }
		/// <summary>Gets whether an explicit resume, restore, or abandon action is required.</summary>
		public bool RequiresUserAction { get; }
		/// <summary>Gets whether the job completed without a fatal status.</summary>
		public bool Succeeded => Status == ContentBuildStatus.Success || Status == ContentBuildStatus.Warning;
	}

	/// <summary>Describes whether package-owned editor-compatible content is safe to select.</summary>
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

		/// <summary>Gets whether the existing-build receipt and content are current.</summary>
		public bool IsValid { get; }
		/// <summary>Gets the validated receipt path.</summary>
		public string ReceiptPath { get; }
		/// <summary>Gets the exact target recorded by the receipt.</summary>
		public BuildTarget Target { get; }
		/// <summary>Gets the ordered validation diagnostics.</summary>
		public IReadOnlyList<ContentBuildDiagnostic> Diagnostics => m_diagnostics;
	}

	/// <summary>Stores the fingerprints required to validate an editor-compatible build.</summary>
	[Serializable]
	public sealed class ContentBuildReceipt {
		/// <summary>The schema version written by this package version.</summary>
		public const int CurrentSchema = 1;
		/// <summary>The serialized receipt schema version.</summary>
		public int schemaVersion = CurrentSchema;
		/// <summary>The build job that created the receipt.</summary>
		public string jobId = string.Empty;
		/// <summary>The serialized <see cref="ContentBuildKind"/> name.</summary>
		public string buildKind = string.Empty;
		/// <summary>The serialized exact Unity build target.</summary>
		public string target = string.Empty;
		/// <summary>The Addressables settings asset GUID.</summary>
		public string settingsGuid = string.Empty;
		/// <summary>The Addressables settings fingerprint.</summary>
		public string settingsHash = string.Empty;
		/// <summary>The Addressables package version used for the build.</summary>
		public string addressablesVersion = string.Empty;
		/// <summary>The Unity editor version used for the build.</summary>
		public string unityVersion = string.Empty;
		/// <summary>The absolute Addressables output path.</summary>
		public string outputPath = string.Empty;
		/// <summary>The built settings artifact used for freshness validation.</summary>
		public string settingsFilePath = string.Empty;
		/// <summary>The SHA-256 fingerprint of the built settings artifact.</summary>
		public string settingsFileHash = string.Empty;
		/// <summary>The built settings artifact length in bytes.</summary>
		public long settingsFileLength;
		/// <summary>The built settings artifact's last-write UTC ticks.</summary>
		public long settingsFileLastWriteUtcTicks;
		/// <summary>The UTC ticks when the content build completed.</summary>
		public long buildCompletedUtcTicks;
		/// <summary>The UTC ticks when the receipt was created.</summary>
		public long createdUtcTicks;
	}

	/// <summary>Describes a persisted content-build job that may require recovery.</summary>
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

		/// <summary>Gets whether a persisted current-job file exists.</summary>
		public bool Exists { get; }
		/// <summary>Gets whether the persisted state can be interpreted safely.</summary>
		public bool IsValid { get; }
		/// <summary>Gets whether the persisted state is too old to resume.</summary>
		public bool IsStale { get; }
		/// <summary>Gets the persisted job identifier.</summary>
		public string JobId { get; }
		/// <summary>Gets the serialized build-job stage.</summary>
		public string Stage { get; }
		/// <summary>Gets the target active before the job began.</summary>
		public BuildTarget OriginalTarget { get; }
		/// <summary>Gets the target currently active in Unity.</summary>
		public BuildTarget ActiveTarget { get; }
		/// <summary>Gets the targets that have not completed.</summary>
		public IReadOnlyList<BuildTarget> PendingTargets { get; }
		/// <summary>Gets the recovery guidance or state error.</summary>
		public string Message { get; }
		/// <summary>Gets the persisted current-job state path.</summary>
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
