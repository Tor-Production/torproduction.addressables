using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace TorProduction.Addressables.Editor {
	internal sealed class ContentBuildJobEngine {
		internal static readonly long StaleAfterTicks = TimeSpan.FromHours(24).Ticks;

		private readonly IContentBuildBackend m_backend;
		private readonly IBuildJobStore m_store;

		internal ContentBuildJobEngine(IContentBuildBackend backend, IBuildJobStore store) {
			m_backend = backend ?? throw new ArgumentNullException(nameof(backend));
			m_store = store ?? throw new ArgumentNullException(nameof(store));
		}

		internal ContentBuildPreflight Analyze(ContentBuildRequest request) =>
			ContentBuildPreflightService.Analyze(request, m_backend);

		internal ContentBuildResult Start(ContentBuildRequest request) {
			if (m_store.Exists) {
				return Failure(
					ContentBuildDiagnosticCode.ExistingJobPending,
					$"A package-owned build job already exists at '{m_store.CurrentPath}'. Resume, restore, or abandon it before starting another build.",
					recoveryPath: m_store.CurrentPath);
			}

			var preflight = Analyze(request);
			if (!preflight.IsValid) {
				return new ContentBuildResult(
					string.Empty,
					ContentBuildStatus.FatalFailure,
					"Build preflight failed before any build mutation or target switch.",
					Array.Empty<ContentBuildItemResult>(),
					preflight.Diagnostics,
					string.Empty,
					string.Empty,
					false);
			}

			m_store.ClearLegacySessionState();
			var now = m_backend.UtcNowTicks;
			var jobId = Guid.NewGuid().ToString("N");
			string operationDirectory;
			try {
				operationDirectory = m_store.CreateOperationDirectory(jobId);
			} catch (Exception exception) {
				return Failure(
					ContentBuildDiagnosticCode.JobStateInvalid,
					$"Unable to create the package-owned operation directory: {exception.Message}");
			}

			var record = new BuildJobRecord {
				jobId = jobId,
				buildKind = request.Kind.ToString(),
				stage = BuildJobStage.Prepared.ToString(),
				failurePolicy = request.FailurePolicy.ToString(),
				allTargets = preflight.Targets.Select(item => item.ToString()).ToArray(),
				pendingTargets = preflight.Targets.Select(item => item.ToString()).ToArray(),
				completed = Array.Empty<BuildJobItemRecord>(),
				originalTarget = m_backend.ActiveTarget.ToString(),
				activeTarget = m_backend.ActiveTarget.ToString(),
				stateFilePath = NormalizePath(request.StateFilePath),
				stateFileHash = preflight.StateFileHash,
				settingsGuid = preflight.SettingsGuid,
				settingsHash = preflight.SettingsHash,
				addressablesVersion = preflight.AddressablesVersion,
				requestHash = preflight.RequestHash,
				operationDirectory = operationDirectory,
				reportPath = Path.Combine(operationDirectory, "build-report.json"),
				createdUtcTicks = now,
				updatedUtcTicks = now
			};

			Persist(record);
			return Advance(record, false);
		}

		internal ContentBuildResult Resume() {
			if (!TryLoad(out var record, out var failure)) return failure;

			if (!Revalidate(record, out var diagnostics)) {
				record.failureMessage = "Resume preflight failed. The job was retained without additional mutation.";
				record.recoveryMessage = string.Join(" | ", diagnostics.Select(item => item.Message));
				Persist(record);
				return new ContentBuildResult(
					record.jobId,
					ContentBuildStatus.FatalFailure,
					record.failureMessage,
					Items(record),
					diagnostics,
					record.reportPath,
					m_store.CurrentPath,
					true);
			}

			return Advance(record, true);
		}

		internal ContentBuildResult RequestCancellation() {
			if (!TryLoad(out var record, out var failure)) return failure;
			record.cancellationRequested = true;
			record.recoveryMessage = "Cancellation was explicitly requested. The original target must be restored before the job is cleared.";
			Persist(record);
			return Advance(record, true);
		}

		internal ContentBuildResult RestoreOriginalTarget() {
			if (!TryLoad(out var record, out var failure)) return failure;
			return Restore(record);
		}

		internal ContentBuildResult AbandonReset() {
			m_store.ClearLegacySessionState();
			if (!m_store.Exists) {
				return new ContentBuildResult(
					string.Empty,
					ContentBuildStatus.Success,
					"No package-owned build job exists. Legacy package SessionState keys were cleared.",
					Array.Empty<ContentBuildItemResult>(),
					Array.Empty<ContentBuildDiagnostic>(),
					string.Empty,
					string.Empty,
					false);
			}

			var loaded = m_store.TryLoad(out var record, out var loadError);
			var validationError = string.Empty;
			if (!loaded || !TryValidateRecord(record, out validationError)) {
				var reason = string.IsNullOrEmpty(loadError) ? validationError : loadError;
				try {
					var invalidArchive = m_store.ArchiveInvalidCurrent(reason);
					m_store.DeleteCurrent();
					return new ContentBuildResult(
						string.Empty,
						ContentBuildStatus.Warning,
						"The invalid package-owned job was archived and the recovery slot was reset.",
						Array.Empty<ContentBuildItemResult>(),
						new[] { new ContentBuildDiagnostic(
							ContentBuildDiagnosticCode.JobAbandoned,
							ContentBuildDiagnosticSeverity.Warning,
							$"Invalid job evidence was preserved at '{invalidArchive}'. Reason: {reason}") },
						string.Empty,
						invalidArchive,
						false);
				} catch (Exception exception) {
					return Failure(
						ContentBuildDiagnosticCode.JobStateInvalid,
						$"The invalid job could not be archived and remains recoverable at '{m_store.CurrentPath}': {exception.Message}",
						recoveryPath: m_store.CurrentPath);
				}
			}
			record.stage = BuildJobStage.Abandoned.ToString();
			record.activeTarget = m_backend.ActiveTarget.ToString();
			record.recoveryMessage =
				$"The job was explicitly abandoned while the active target was '{record.activeTarget}'. " +
				$"Its original target was '{record.originalTarget}'. This archived record preserves the incomplete-restoration evidence.";
			record.updatedUtcTicks = m_backend.UtcNowTicks;
			Persist(record);
			string archivePath;
			try {
				archivePath = m_store.Archive(record);
				m_store.DeleteCurrent();
			} catch (Exception exception) {
				record.recoveryMessage =
					$"The job could not be archived and remains at '{m_store.CurrentPath}': {exception.Message}";
				Persist(record);
				return Failure(
					ContentBuildDiagnosticCode.JobStateInvalid,
					record.recoveryMessage,
					recoveryPath: m_store.CurrentPath);
			}
			return new ContentBuildResult(
				record.jobId,
				ContentBuildStatus.Warning,
				record.recoveryMessage,
				Items(record),
				new[] { new ContentBuildDiagnostic(
					ContentBuildDiagnosticCode.JobAbandoned,
					ContentBuildDiagnosticSeverity.Warning,
					$"The abandoned job record is retained at '{archivePath}'.") },
				record.reportPath,
				archivePath,
				false);
		}

		internal ContentBuildRecoveryInfo InspectRecovery() {
			if (!m_store.Exists) {
				return new ContentBuildRecoveryInfo(
					false, true, false, string.Empty, string.Empty,
					BuildTarget.NoTarget, m_backend.ActiveTarget,
					Array.Empty<BuildTarget>(), "No package-owned build job exists.", m_store.CurrentPath);
			}

			var loaded = m_store.TryLoad(out var record, out var error);
			var validationError = string.Empty;
			var recordValid = loaded && TryValidateRecord(record, out validationError);
			if (!recordValid) {
				return new ContentBuildRecoveryInfo(
					true, false, true, string.Empty, "Invalid",
					BuildTarget.NoTarget, m_backend.ActiveTarget,
					Array.Empty<BuildTarget>(),
					string.IsNullOrEmpty(error) ? validationError : error,
					m_store.CurrentPath);
			}

			var stale = record.IsStale(m_backend.UtcNowTicks, StaleAfterTicks);
			return new ContentBuildRecoveryInfo(
				true,
				true,
				stale,
				record.jobId,
				record.stage,
				ParseTarget(record.originalTarget),
				m_backend.ActiveTarget,
				record.pendingTargets.Select(ParseTarget),
				stale
					? "The package-owned build job is stale. Resume will re-run preflight; Restore and Abandon/Reset remain available."
					: "A package-owned build job is incomplete. Choose Resume, Restore Original Target, or Abandon/Reset explicitly.",
				m_store.CurrentPath);
		}

		private ContentBuildResult Advance(BuildJobRecord record, bool explicitResume) {
			if (record.cancellationRequested || m_backend.IsCancellationRequested) {
				return CancelAndRestore(record);
			}

			if (!record.TryGetStage(out var stage)) {
				return RetainedFailure(record, "The persisted job stage is invalid.");
			}

			if (stage == BuildJobStage.RestoringOriginalTarget || stage == BuildJobStage.RestorationFailed) {
				return Restore(record);
			}
			if (stage == BuildJobStage.Completed || stage == BuildJobStage.Failed ||
			    stage == BuildJobStage.Cancelled || stage == BuildJobStage.Abandoned) {
				return RetainedFailure(record, $"Job stage '{stage}' cannot be resumed.");
			}

			if (record.pendingTargets == null || record.pendingTargets.Length == 0) {
				return Restore(record);
			}

			var target = ParseTarget(record.pendingTargets[0]);
			if (target == BuildTarget.NoTarget) {
				return RetainedFailure(record, "The next persisted exact build target is invalid.");
			}

			if (m_backend.ActiveTarget != target) {
				if (stage == BuildJobStage.AwaitingResume && explicitResume) {
					return RetainedFailure(
						record,
						$"Resume expected active target '{target}', but Unity reports '{m_backend.ActiveTarget}'. Restore or abandon the job before continuing.");
				}

				record.stage = BuildJobStage.SwitchingTarget.ToString();
				record.activeTarget = m_backend.ActiveTarget.ToString();
				record.recoveryMessage = $"Switching to exact target '{target}'. Resume is required after any domain reload.";
				Persist(record);
				try {
					var group = UnityEditor.BuildPipeline.GetBuildTargetGroup(target);
					if (!m_backend.SwitchActiveTarget(group, target)) {
						return HandleSwitchFailure(record, target, "Unity rejected SwitchActiveBuildTarget.");
					}
				} catch (Exception exception) {
					return HandleSwitchFailure(record, target, $"Target switch threw: {exception.Message}");
				}

				record.activeTarget = m_backend.ActiveTarget.ToString();
				record.stage = BuildJobStage.AwaitingResume.ToString();
				record.recoveryMessage =
					$"Target switch to '{target}' completed. Resume explicitly to start the build; no build was auto-resumed after reload.";
				Persist(record);
				return ResultFromRecord(
					record,
					ContentBuildStatus.AwaitingResume,
					record.recoveryMessage,
					new[] { new ContentBuildDiagnostic(
						ContentBuildDiagnosticCode.ResumeRequired,
						ContentBuildDiagnosticSeverity.Info,
						record.recoveryMessage,
						target) },
					true);
			}

			if (stage == BuildJobStage.AwaitingResume && !explicitResume) {
				return ResultFromRecord(
					record,
					ContentBuildStatus.AwaitingResume,
					"Resume explicitly before the build stage.",
					new[] { new ContentBuildDiagnostic(
						ContentBuildDiagnosticCode.ResumeRequired,
						ContentBuildDiagnosticSeverity.Info,
						"Resume explicitly before the build stage.",
						target) },
					true);
			}

			return ExecuteCurrent(record, target);
		}

		private ContentBuildResult ExecuteCurrent(BuildJobRecord record, BuildTarget target) {
			record.stage = BuildJobStage.Building.ToString();
			record.activeTarget = m_backend.ActiveTarget.ToString();
			record.recoveryMessage = $"Building exact target '{target}'.";
			var started = m_backend.UtcNowTicks;
			Persist(record);

			if (record.cancellationRequested || m_backend.IsCancellationRequested) {
				return CancelAndRestore(record);
			}

			BuildExecutionOutcome outcome;
			try {
				if (record.TryGetKind(out var kind) && kind == ContentBuildKind.ContentUpdate) {
					outcome = m_backend.BuildContentUpdate(target, record.stateFilePath);
				} else {
					outcome = m_backend.BuildFull(target);
				}
			} catch (OperationCanceledException exception) {
				record.failureMessage = exception.Message;
				return CancelAndRestore(record);
			} catch (Exception exception) {
				outcome = new BuildExecutionOutcome(false, $"Build threw: {exception.Message}");
			}

			var status = outcome.Succeeded ? ContentBuildStatus.Success : ContentBuildStatus.FatalFailure;
			var message = outcome.Message;
			var layoutPath = string.Empty;
			var receiptPath = string.Empty;

			if (outcome.Succeeded) {
				var layout = m_backend.CaptureBuildLayout(record.operationDirectory, started, target);
				layoutPath = layout.CopiedPath;
				if (layout.Status == ContentBuildStatus.Warning) {
					status = ContentBuildStatus.Warning;
					message = Append(message, layout.Message);
				} else if (layout.Status == ContentBuildStatus.FatalFailure) {
					status = ContentBuildStatus.FatalFailure;
					message = Append(message, layout.Message);
				}

				if (status != ContentBuildStatus.FatalFailure &&
				    record.TryGetKind(out var kind) && kind == ContentBuildKind.EditorCompatible) {
					var receipt = m_backend.CreateEditorCompatibleReceipt(record, outcome, target);
					receiptPath = receipt.ReceiptPath;
					record.receiptPath = receiptPath;
					if (!receipt.Succeeded) {
						status = ContentBuildStatus.FatalFailure;
						message = Append(message, receipt.Message);
					}
				}
			}

			var item = new BuildJobItemRecord {
				target = target.ToString(),
				status = status.ToString(),
				message = message,
				outputPath = outcome.OutputPath,
				layoutPath = layoutPath,
				receiptPath = receiptPath
			};
			AppendCompleted(record, item);
			RemovePending(record, target);
			record.failureMessage = status == ContentBuildStatus.FatalFailure ? message : record.failureMessage;
			record.stage = BuildJobStage.Prepared.ToString();
			Persist(record);

			if (status == ContentBuildStatus.FatalFailure &&
			    (!record.TryGetFailurePolicy(out var policy) || policy == ContentBuildFailurePolicy.StopOnFirstFailure)) {
				SkipRemaining(record, $"Skipped because '{target}' failed and the default policy stops on the first failure.");
			}

			if (record.pendingTargets.Length == 0) return Restore(record);
			return Advance(record, false);
		}

		private ContentBuildResult HandleSwitchFailure(BuildJobRecord record, BuildTarget target, string message) {
			AppendCompleted(record, new BuildJobItemRecord {
				target = target.ToString(),
				status = ContentBuildStatus.TargetSwitchFailure.ToString(),
				message = message
			});
			RemovePending(record, target);
			record.failureMessage = message;
			record.stage = BuildJobStage.Prepared.ToString();
			Persist(record);

			if (!record.TryGetFailurePolicy(out var policy) || policy == ContentBuildFailurePolicy.StopOnFirstFailure) {
				SkipRemaining(record, $"Skipped because switching to '{target}' failed and the default policy stops on the first failure.");
			}
			if (record.pendingTargets.Length == 0) return Restore(record);
			return Advance(record, false);
		}

		private ContentBuildResult CancelAndRestore(BuildJobRecord record) {
			if (record.pendingTargets != null && record.pendingTargets.Length > 0) {
				var current = ParseTarget(record.pendingTargets[0]);
				AppendCompleted(record, new BuildJobItemRecord {
					target = current.ToString(),
					status = ContentBuildStatus.Cancellation.ToString(),
					message = "The request was cancelled before the next synchronous build stage."
				});
				RemovePending(record, current);
			}
			SkipRemaining(record, "Skipped after explicit cancellation.");
			record.failureMessage = "Build job cancelled.";
			record.stage = BuildJobStage.Cancelled.ToString();
			Persist(record);
			return Restore(record);
		}

		private ContentBuildResult Restore(BuildJobRecord record) {
			var originalTarget = ParseTarget(record.originalTarget);
			if (originalTarget == BuildTarget.NoTarget) {
				return RestorationFailure(record, "The persisted original target is invalid and cannot be restored automatically.");
			}

			if (m_backend.ActiveTarget != originalTarget) {
				record.stage = BuildJobStage.RestoringOriginalTarget.ToString();
				record.activeTarget = m_backend.ActiveTarget.ToString();
				record.recoveryMessage = $"Restoring original exact target '{originalTarget}'.";
				Persist(record);
				try {
					var group = UnityEditor.BuildPipeline.GetBuildTargetGroup(originalTarget);
					if (!m_backend.SwitchActiveTarget(group, originalTarget)) {
						return RestorationFailure(record, $"Unity rejected restoration to '{originalTarget}'.");
					}
				} catch (Exception exception) {
					return RestorationFailure(record, $"Restoration to '{originalTarget}' threw: {exception.Message}");
				}

				if (m_backend.ActiveTarget != originalTarget) {
					record.activeTarget = m_backend.ActiveTarget.ToString();
					record.recoveryMessage =
						$"Unity accepted restoration to '{originalTarget}', but the active target has not confirmed it yet. Use Restore Original Target after reload.";
					Persist(record);
					return ResultFromRecord(
						record,
						ContentBuildStatus.AwaitingResume,
						record.recoveryMessage,
						new[] { new ContentBuildDiagnostic(
							ContentBuildDiagnosticCode.ResumeRequired,
							ContentBuildDiagnosticSeverity.Warning,
							record.recoveryMessage,
							originalTarget) },
						true);
				}
			}

			record.activeTarget = originalTarget.ToString();
			var finalStatus = DetermineFinalStatus(record);
			record.stage = finalStatus == ContentBuildStatus.Cancellation
				? BuildJobStage.Cancelled.ToString()
				: finalStatus == ContentBuildStatus.FatalFailure || finalStatus == ContentBuildStatus.TargetSwitchFailure
					? BuildJobStage.Failed.ToString()
					: BuildJobStage.Completed.ToString();
			record.recoveryMessage = string.Empty;
			Persist(record);
			var result = ResultFromRecord(
				record,
				finalStatus,
				FinalMessage(finalStatus),
				Array.Empty<ContentBuildDiagnostic>(),
				false,
				string.Empty);
			m_store.DeleteCurrent();
			return result;
		}

		private ContentBuildResult RestorationFailure(BuildJobRecord record, string message) {
			record.stage = BuildJobStage.RestorationFailed.ToString();
			record.activeTarget = m_backend.ActiveTarget.ToString();
			record.failureMessage = Append(record.failureMessage, message);
			record.recoveryMessage =
				message + $" The job remains at '{m_store.CurrentPath}'. Retry Restore Original Target or abandon it explicitly.";
			Persist(record);
			return ResultFromRecord(
				record,
				ContentBuildStatus.RestorationFailure,
				record.recoveryMessage,
				new[] { new ContentBuildDiagnostic(
					ContentBuildDiagnosticCode.RestorationFailed,
					ContentBuildDiagnosticSeverity.Error,
					record.recoveryMessage,
					ParseTarget(record.originalTarget)) },
				true);
		}

		private bool Revalidate(BuildJobRecord record, out IReadOnlyList<ContentBuildDiagnostic> diagnostics) {
			var list = new List<ContentBuildDiagnostic>();
			if (!TryRecreateRequest(record, out var request, out var error)) {
				list.Add(new ContentBuildDiagnostic(
					ContentBuildDiagnosticCode.JobStateInvalid,
					ContentBuildDiagnosticSeverity.Error,
					error));
				diagnostics = list;
				return false;
			}

			var preflight = Analyze(request);
			list.AddRange(preflight.Diagnostics);
			if (record.IsStale(m_backend.UtcNowTicks, StaleAfterTicks)) {
				list.Add(new ContentBuildDiagnostic(
					ContentBuildDiagnosticCode.JobStateStale,
					ContentBuildDiagnosticSeverity.Warning,
					"The job was stale; explicit Resume re-ran the complete preflight before continuing."));
			}
			if (!preflight.IsValid ||
			    !string.Equals(preflight.RequestHash, record.requestHash, StringComparison.Ordinal) ||
			    !string.Equals(preflight.SettingsGuid, record.settingsGuid, StringComparison.Ordinal) ||
			    !string.Equals(preflight.SettingsHash, record.settingsHash, StringComparison.Ordinal) ||
			    !string.Equals(preflight.StateFileHash, record.stateFileHash, StringComparison.Ordinal) ||
			    !string.Equals(preflight.AddressablesVersion, record.addressablesVersion, StringComparison.Ordinal)) {
				list.Add(new ContentBuildDiagnostic(
					ContentBuildDiagnosticCode.JobStateInvalid,
					ContentBuildDiagnosticSeverity.Error,
					"The persisted request, Addressables settings, package version, or state file changed. The job was retained without mutation."));
			}

			diagnostics = list;
			return list.All(item => item.Severity != ContentBuildDiagnosticSeverity.Error);
		}

		private static bool TryRecreateRequest(BuildJobRecord record, out ContentBuildRequest request, out string error) {
			request = null;
			error = string.Empty;
			if (!record.TryGetKind(out var kind) || !record.TryGetFailurePolicy(out var policy)) {
				error = "The persisted build kind or failure policy is invalid.";
				return false;
			}

			var allTargets = (record.allTargets ?? Array.Empty<string>()).Select(ParseTarget).ToArray();
			if (allTargets.Length == 0 || allTargets.Any(item => item == BuildTarget.NoTarget)) {
				error = "The persisted exact target queue is missing or invalid.";
				return false;
			}

			if (kind == ContentBuildKind.EditorCompatible) {
				request = ContentBuildRequest.EditorCompatible();
				return true;
			}

			if (kind == ContentBuildKind.MultiPlatform) {
				var platforms = new List<ContentBuildPlatform>();
				foreach (var target in allTargets) {
					if (!BuildTargetMapper.TryMap(target, out var platform)) {
						error = $"Persisted target '{target}' cannot be mapped to a supported platform request.";
						return false;
					}
					platforms.Add(platform);
				}
				request = ContentBuildRequest.MultiPlatform(platforms, policy);
				return true;
			}

			if (!BuildTargetMapper.TryMap(allTargets[0], out var singlePlatform)) {
				error = $"Persisted target '{allTargets[0]}' cannot be mapped to a supported platform request.";
				return false;
			}
			request = kind == ContentBuildKind.ContentUpdate
				? new ContentBuildRequest(kind, singlePlatform, stateFilePath: record.stateFilePath, failurePolicy: policy)
				: new ContentBuildRequest(kind, singlePlatform, failurePolicy: policy);
			return true;
		}

		private bool TryLoad(out BuildJobRecord record, out ContentBuildResult failure) {
			failure = null;
			if (!m_store.Exists) {
				record = null;
				failure = Failure(
					ContentBuildDiagnosticCode.JobStateMissing,
					"No package-owned build job exists to resume, restore, cancel, or abandon.");
				return false;
			}
			var loaded = m_store.TryLoad(out record, out var error);
			var validationError = string.Empty;
			var recordValid = loaded && TryValidateRecord(record, out validationError);
			if (!recordValid) {
				failure = Failure(
					ContentBuildDiagnosticCode.JobStateInvalid,
					$"The package-owned job state is invalid: {(string.IsNullOrEmpty(error) ? validationError : error)}",
					recoveryPath: m_store.CurrentPath);
				return false;
			}
			return true;
		}

		private static bool TryValidateRecord(BuildJobRecord record, out string error) {
			if (record == null) {
				error = "The job record is null.";
				return false;
			}
			if (record.schemaVersion != BuildJobRecord.CurrentSchema || string.IsNullOrEmpty(record.jobId) ||
			    string.IsNullOrEmpty(record.stage) || string.IsNullOrEmpty(record.originalTarget) ||
			    string.IsNullOrEmpty(record.activeTarget) || string.IsNullOrEmpty(record.reportPath) ||
			    string.IsNullOrEmpty(record.operationDirectory) || string.IsNullOrEmpty(record.settingsGuid) ||
			    string.IsNullOrEmpty(record.settingsHash) || string.IsNullOrEmpty(record.addressablesVersion) ||
			    string.IsNullOrEmpty(record.requestHash)) {
				error = "The job schema or required identity fields are missing.";
				return false;
			}
			if (!record.TryGetStage(out _) || !record.TryGetKind(out var kind) || !record.TryGetFailurePolicy(out _) ||
			    ParseTarget(record.originalTarget) == BuildTarget.NoTarget ||
			    ParseTarget(record.activeTarget) == BuildTarget.NoTarget) {
				error = "The job contains an unknown enum value.";
				return false;
			}
			var allTargets = record.allTargets ?? Array.Empty<string>();
			var pendingTargets = record.pendingTargets ?? Array.Empty<string>();
			var completed = record.completed ?? Array.Empty<BuildJobItemRecord>();
			if (allTargets.Length == 0 || allTargets.Select(ParseTarget).Any(item => item == BuildTarget.NoTarget) ||
			    allTargets.Distinct(StringComparer.Ordinal).Count() != allTargets.Length ||
			    pendingTargets.Select(ParseTarget).Any(item => item == BuildTarget.NoTarget) ||
			    pendingTargets.Any(item => !allTargets.Contains(item, StringComparer.Ordinal)) ||
			    completed.Any(item => item == null || ParseTarget(item.target) == BuildTarget.NoTarget ||
			                           !Enum.TryParse(item.status, out ContentBuildStatus _))) {
				error = "The job contains a missing, duplicate, or invalid exact-target queue/result entry.";
				return false;
			}
			if (kind == ContentBuildKind.ContentUpdate &&
			    (string.IsNullOrEmpty(record.stateFilePath) || string.IsNullOrEmpty(record.stateFileHash))) {
				error = "The Content Update job is missing its explicit state-file identity.";
				return false;
			}
			error = string.Empty;
			return true;
		}

		private void Persist(BuildJobRecord record) {
			record.updatedUtcTicks = m_backend.UtcNowTicks;
			m_store.Save(record);
			m_backend.WriteOperationReport(record);
		}

		private ContentBuildResult RetainedFailure(BuildJobRecord record, string message) {
			record.failureMessage = Append(record.failureMessage, message);
			record.recoveryMessage = message;
			Persist(record);
			return ResultFromRecord(
				record,
				ContentBuildStatus.FatalFailure,
				message,
				new[] { new ContentBuildDiagnostic(
					ContentBuildDiagnosticCode.JobStateInvalid,
					ContentBuildDiagnosticSeverity.Error,
					message) },
				true);
		}

		private static void AppendCompleted(BuildJobRecord record, BuildJobItemRecord item) {
			var completed = new List<BuildJobItemRecord>(record.completed ?? Array.Empty<BuildJobItemRecord>()) { item };
			record.completed = completed.ToArray();
		}

		private static void RemovePending(BuildJobRecord record, BuildTarget target) {
			var pending = new List<string>(record.pendingTargets ?? Array.Empty<string>());
			var index = pending.FindIndex(item => ParseTarget(item) == target);
			if (index >= 0) pending.RemoveAt(index);
			record.pendingTargets = pending.ToArray();
		}

		private void SkipRemaining(BuildJobRecord record, string message) {
			foreach (var pending in record.pendingTargets ?? Array.Empty<string>()) {
				AppendCompleted(record, new BuildJobItemRecord {
					target = pending,
					status = ContentBuildStatus.Skipped.ToString(),
					message = message
				});
			}
			record.pendingTargets = Array.Empty<string>();
			Persist(record);
		}

		private static ContentBuildStatus DetermineFinalStatus(BuildJobRecord record) {
			var statuses = (record.completed ?? Array.Empty<BuildJobItemRecord>())
				.Select(item => ParseStatus(item.status)).ToArray();
			if (statuses.Contains(ContentBuildStatus.Cancellation)) return ContentBuildStatus.Cancellation;
			if (statuses.Contains(ContentBuildStatus.TargetSwitchFailure)) return ContentBuildStatus.TargetSwitchFailure;
			if (statuses.Contains(ContentBuildStatus.FatalFailure)) return ContentBuildStatus.FatalFailure;
			if (statuses.Contains(ContentBuildStatus.Warning)) return ContentBuildStatus.Warning;
			return ContentBuildStatus.Success;
		}

		private ContentBuildResult ResultFromRecord(
			BuildJobRecord record,
			ContentBuildStatus status,
			string message,
			IEnumerable<ContentBuildDiagnostic> diagnostics,
			bool requiresAction,
			string recoveryPath = null) =>
			new ContentBuildResult(
				record.jobId,
				status,
				message,
				Items(record),
				diagnostics,
				record.reportPath,
				recoveryPath ?? m_store.CurrentPath,
				requiresAction);

		private static IReadOnlyList<ContentBuildItemResult> Items(BuildJobRecord record) =>
			(record.completed ?? Array.Empty<BuildJobItemRecord>()).Select(item =>
				new ContentBuildItemResult(
					ParseTarget(item.target),
					ParseStatus(item.status),
					item.message,
					item.outputPath,
					item.layoutPath,
					item.receiptPath)).ToArray();

		private ContentBuildResult Failure(
			ContentBuildDiagnosticCode code,
			string message,
			string recoveryPath = "") =>
			new ContentBuildResult(
				string.Empty,
				ContentBuildStatus.FatalFailure,
				message,
				Array.Empty<ContentBuildItemResult>(),
				new[] { new ContentBuildDiagnostic(code, ContentBuildDiagnosticSeverity.Error, message) },
				string.Empty,
				recoveryPath,
				!string.IsNullOrEmpty(recoveryPath));

		private static BuildTarget ParseTarget(string value) =>
			Enum.TryParse(value, out BuildTarget target) ? target : BuildTarget.NoTarget;

		private static ContentBuildStatus ParseStatus(string value) =>
			Enum.TryParse(value, out ContentBuildStatus status) ? status : ContentBuildStatus.FatalFailure;

		private static string FinalMessage(ContentBuildStatus status) {
			switch (status) {
				case ContentBuildStatus.Success:
					return "The build job completed and the original exact target was restored.";
				case ContentBuildStatus.Warning:
					return "The build job completed with warnings and the original exact target was restored.";
				case ContentBuildStatus.Cancellation:
					return "The build job was cancelled and the original exact target was restored.";
				default:
					return "The build job failed and the original exact target was restored.";
			}
		}

		private static string Append(string first, string second) {
			if (string.IsNullOrWhiteSpace(first)) return second ?? string.Empty;
			if (string.IsNullOrWhiteSpace(second)) return first;
			return first + " " + second;
		}

		private static string NormalizePath(string path) {
			if (string.IsNullOrWhiteSpace(path)) return string.Empty;
			try {
				return Path.GetFullPath(path).Replace('\\', '/');
			} catch {
				return path.Trim().Replace('\\', '/');
			}
		}
	}
}
