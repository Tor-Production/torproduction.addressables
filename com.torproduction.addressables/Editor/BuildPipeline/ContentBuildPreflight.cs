using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	internal sealed class ContentStateValidationResult {
		internal ContentStateValidationResult(
			bool isValid,
			string fileHash,
			IEnumerable<ContentBuildDiagnostic> diagnostics) {
			IsValid = isValid;
			FileHash = fileHash ?? string.Empty;
			Diagnostics = (diagnostics ?? Array.Empty<ContentBuildDiagnostic>()).ToArray();
		}

		internal bool IsValid { get; }
		internal string FileHash { get; }
		internal IReadOnlyList<ContentBuildDiagnostic> Diagnostics { get; }
	}

	internal sealed class BuildExecutionOutcome {
		internal BuildExecutionOutcome(
			bool succeeded,
			string message,
			string outputPath = "",
			string contentStatePath = "") {
			Succeeded = succeeded;
			Message = message ?? string.Empty;
			OutputPath = outputPath ?? string.Empty;
			ContentStatePath = contentStatePath ?? string.Empty;
		}

		internal bool Succeeded { get; }
		internal string Message { get; }
		internal string OutputPath { get; }
		internal string ContentStatePath { get; }
	}

	internal sealed class BuildLayoutCaptureResult {
		internal BuildLayoutCaptureResult(
			ContentBuildStatus status,
			string message,
			string copiedPath = "") {
			Status = status;
			Message = message ?? string.Empty;
			CopiedPath = copiedPath ?? string.Empty;
		}

		internal ContentBuildStatus Status { get; }
		internal string Message { get; }
		internal string CopiedPath { get; }
	}

	internal sealed class BuildReceiptCreationResult {
		internal BuildReceiptCreationResult(bool succeeded, string message, string receiptPath = "") {
			Succeeded = succeeded;
			Message = message ?? string.Empty;
			ReceiptPath = receiptPath ?? string.Empty;
		}

		internal bool Succeeded { get; }
		internal string Message { get; }
		internal string ReceiptPath { get; }
	}

	internal interface IContentBuildPreflightEnvironment {
		BuildTarget ActiveTarget { get; }
		RuntimePlatform EditorPlatform { get; }
		bool SettingsExist { get; }
		bool PlayerDataBuilderValid { get; }
		string SettingsGuid { get; }
		string SettingsHash { get; }
		string AddressablesVersion { get; }
		bool FileExists(string path);
		bool IsTargetSupported(BuildTargetGroup group, BuildTarget target);
		ContentStateValidationResult ValidateContentState(string path, BuildTarget target);
		IReadOnlyList<ContentBuildDiagnostic> CheckContentUpdateRestrictions(string path, BuildTarget target);
	}

	internal interface IContentBuildBackend : IContentBuildPreflightEnvironment {
		long UtcNowTicks { get; }
		bool IsCancellationRequested { get; }
		bool SwitchActiveTarget(BuildTargetGroup group, BuildTarget target);
		BuildExecutionOutcome BuildFull(BuildTarget target);
		BuildExecutionOutcome BuildContentUpdate(BuildTarget target, string stateFilePath);
		BuildLayoutCaptureResult CaptureBuildLayout(string operationDirectory, long buildStartedUtcTicks, BuildTarget target);
		BuildReceiptCreationResult CreateEditorCompatibleReceipt(
			BuildJobRecord record,
			BuildExecutionOutcome outcome,
			BuildTarget target);
		void WriteOperationReport(BuildJobRecord record);
	}

	internal interface IBuildJobStore {
		string CurrentPath { get; }
		bool Exists { get; }
		string CreateOperationDirectory(string jobId);
		bool TryLoad(out BuildJobRecord record, out string error);
		void Save(BuildJobRecord record);
		void DeleteCurrent();
		string Archive(BuildJobRecord record);
		string ArchiveInvalidCurrent(string reason);
		void ClearLegacySessionState();
	}

	internal static class ContentBuildPreflightService {
		internal static ContentBuildPreflight Analyze(
			ContentBuildRequest request,
			IContentBuildPreflightEnvironment environment) {
			var diagnostics = new List<ContentBuildDiagnostic>();
			var targets = new List<BuildTarget>();
			var stateFileHash = string.Empty;

			if (request == null) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.InvalidRequest,
					"A non-null content build request is required."));
				return Result(null, targets, diagnostics, environment, stateFileHash);
			}

			if (!Enum.IsDefined(typeof(ContentBuildKind), request.Kind) ||
			    !Enum.IsDefined(typeof(ContentBuildFailurePolicy), request.FailurePolicy)) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.InvalidRequest,
					"The build kind or failure policy is not supported."));
			}

			if (!environment.SettingsExist) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.AddressablesSettingsMissing,
					"Addressables settings do not exist. Create and configure them before building."));
			}
			if (!IsSupportedVersion(environment.AddressablesVersion)) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.AddressablesVersionUnsupported,
					$"Addressables {environment.AddressablesVersion} is not a verified build adapter. Supported versions are 2.7.6 and 2.9.1."));
			}
			if (environment.SettingsExist && !environment.PlayerDataBuilderValid) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.PlayerDataBuilderMissing,
					"The active Addressables player data builder is missing or cannot produce player content."));
			}
			if (environment.SettingsExist &&
			    (string.IsNullOrEmpty(environment.SettingsGuid) || string.IsNullOrEmpty(environment.SettingsHash))) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.AddressablesSettingsMissing,
					"Addressables settings must be a persistent asset with a deterministic GUID and dependency hash before building."));
			}

			BuildTargets(request, environment, targets, diagnostics);
			foreach (var target in targets) {
				var group = BuildPipeline.GetBuildTargetGroup(target);
				if (group == BuildTargetGroup.Unknown || !environment.IsTargetSupported(group, target)) {
					diagnostics.Add(Error(
					ContentBuildDiagnosticCode.TargetUnsupported,
					$"Build target '{target}' is not supported by this Unity installation. Install its platform module before starting the queue.",
					target));
				}
			}

			if (request.Kind == ContentBuildKind.ContentUpdate) {
				var statePath = NormalizePath(request.StateFilePath);
				if (string.IsNullOrEmpty(statePath)) {
					diagnostics.Add(Error(
						ContentBuildDiagnosticCode.StateFileRequired,
						"Content Update requires an explicitly selected addressables_content_state.bin file."));
				} else if (!environment.FileExists(statePath)) {
					diagnostics.Add(Error(
						ContentBuildDiagnosticCode.StateFileMissing,
						$"The selected content-state file does not exist: '{statePath}'."));
				} else if (targets.Count == 1) {
					var validation = environment.ValidateContentState(statePath, targets[0]);
					diagnostics.AddRange(validation.Diagnostics);
					if (validation.IsValid) {
						stateFileHash = validation.FileHash;
						diagnostics.AddRange(environment.CheckContentUpdateRestrictions(statePath, targets[0]));
					}
				}
			} else if (!string.IsNullOrWhiteSpace(request.StateFilePath)) {
				diagnostics.Add(new ContentBuildDiagnostic(
					ContentBuildDiagnosticCode.InvalidRequest,
					ContentBuildDiagnosticSeverity.Warning,
					"The state-file input is ignored because only Content Update consumes previous content state."));
			}

			return Result(request, targets, diagnostics, environment, stateFileHash);
		}

		private static void BuildTargets(
			ContentBuildRequest request,
			IContentBuildPreflightEnvironment environment,
			ICollection<BuildTarget> targets,
			ICollection<ContentBuildDiagnostic> diagnostics) {
			switch (request.Kind) {
				case ContentBuildKind.Full:
				case ContentBuildKind.ContentUpdate:
					if (!Enum.IsDefined(typeof(ContentBuildPlatform), request.Platform) ||
					    !BuildTargetMapper.TryMap(request.Platform, out var target)) {
						diagnostics.Add(Error(
							ContentBuildDiagnosticCode.InvalidRequest,
							$"Platform '{request.Platform}' is not supported."));
						return;
					}
					targets.Add(target);
					return;
				case ContentBuildKind.EditorCompatible:
					if (!BuildTargetMapper.TryMapEditor(environment.EditorPlatform, out var editorTarget)) {
						diagnostics.Add(Error(
							ContentBuildDiagnosticCode.TargetUnsupported,
							$"Editor host platform '{environment.EditorPlatform}' has no supported standalone target mapping."));
						return;
					}
					targets.Add(editorTarget);
					return;
				case ContentBuildKind.MultiPlatform:
					if (request.Platforms == null || request.Platforms.Count == 0) {
						diagnostics.Add(Error(
							ContentBuildDiagnosticCode.InvalidRequest,
							"Multi-Platform requires at least one explicit platform request."));
						return;
					}

					var invalid = request.Platforms.Where(platform =>
						!Enum.IsDefined(typeof(ContentBuildPlatform), platform)).ToArray();
					if (invalid.Length > 0) {
						diagnostics.Add(Error(
							ContentBuildDiagnosticCode.InvalidRequest,
							"The Multi-Platform queue contains an unsupported platform value."));
						return;
					}
					if (request.Platforms.Distinct().Count() != request.Platforms.Count) {
						diagnostics.Add(Error(
							ContentBuildDiagnosticCode.InvalidRequest,
							"The Multi-Platform queue contains a duplicate exact target."));
						return;
					}

					foreach (var item in BuildTargetMapper.Optimize(request.Platforms, environment.ActiveTarget)) {
						targets.Add(item);
					}
					return;
				default:
					diagnostics.Add(Error(
						ContentBuildDiagnosticCode.InvalidRequest,
						$"Build kind '{request.Kind}' is not supported."));
					return;
			}
		}

		private static ContentBuildPreflight Result(
			ContentBuildRequest request,
			IEnumerable<BuildTarget> targets,
			IEnumerable<ContentBuildDiagnostic> diagnostics,
			IContentBuildPreflightEnvironment environment,
			string stateFileHash) {
			var targetArray = targets.ToArray();
			var normalizedStatePath = request == null ? string.Empty : NormalizePath(request.StateFilePath);
			var requestHash = AutomationHash.Compute(string.Join("|", new[] {
				request == null ? "null" : request.Kind.ToString(),
				request == null ? string.Empty : request.FailurePolicy.ToString(),
				string.Join(",", targetArray.Select(item => item.ToString()).OrderBy(item => item, StringComparer.Ordinal)),
				normalizedStatePath,
				stateFileHash ?? string.Empty,
				environment.SettingsGuid ?? string.Empty,
				environment.SettingsHash ?? string.Empty,
				environment.AddressablesVersion ?? string.Empty
			}));
			return new ContentBuildPreflight(
				request,
				targetArray,
				diagnostics,
				requestHash,
				environment.SettingsGuid,
				environment.SettingsHash,
				stateFileHash,
				environment.AddressablesVersion);
		}

		private static string NormalizePath(string path) {
			if (string.IsNullOrWhiteSpace(path)) return string.Empty;
			try {
				return Path.GetFullPath(path).Replace('\\', '/');
			} catch {
				return path.Trim().Replace('\\', '/');
			}
		}

		private static bool IsSupportedVersion(string version) =>
			string.Equals(version, "2.7.6", StringComparison.Ordinal) ||
			string.Equals(version, "2.9.1", StringComparison.Ordinal);

		private static ContentBuildDiagnostic Error(
			ContentBuildDiagnosticCode code,
			string message,
			BuildTarget target = BuildTarget.NoTarget) =>
			new ContentBuildDiagnostic(code, ContentBuildDiagnosticSeverity.Error, message, target);
	}
}
