using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	internal readonly struct BuildReceiptValidationContext {
		internal BuildReceiptValidationContext(
			BuildTarget activeTarget,
			RuntimePlatform editorPlatform,
			string settingsGuid,
			string settingsHash,
			string addressablesVersion,
			string unityVersion,
			Func<string, bool> fileExists,
			Func<string, long> fileLength,
			Func<string, long> lastWriteUtcTicks,
			Func<string, string> fileHash) {
			ActiveTarget = activeTarget;
			EditorPlatform = editorPlatform;
			SettingsGuid = settingsGuid ?? string.Empty;
			SettingsHash = settingsHash ?? string.Empty;
			AddressablesVersion = addressablesVersion ?? string.Empty;
			UnityVersion = unityVersion ?? string.Empty;
			FileExists = fileExists;
			FileLength = fileLength;
			LastWriteUtcTicks = lastWriteUtcTicks;
			FileHash = fileHash;
		}

		internal BuildTarget ActiveTarget { get; }
		internal RuntimePlatform EditorPlatform { get; }
		internal string SettingsGuid { get; }
		internal string SettingsHash { get; }
		internal string AddressablesVersion { get; }
		internal string UnityVersion { get; }
		internal Func<string, bool> FileExists { get; }
		internal Func<string, long> FileLength { get; }
		internal Func<string, long> LastWriteUtcTicks { get; }
		internal Func<string, string> FileHash { get; }
	}

	internal static class BuildReceiptValidator {
		internal static ExistingBuildValidation Validate(
			ContentBuildReceipt receipt,
			string receiptPath,
			BuildReceiptValidationContext context) {
			var diagnostics = new List<ContentBuildDiagnostic>();
			if (receipt == null || receipt.schemaVersion != ContentBuildReceipt.CurrentSchema) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.ReceiptInvalid,
					"The editor-compatible receipt is missing or uses an unsupported schema."));
				return Result(false, receiptPath, BuildTarget.NoTarget, diagnostics);
			}

			if (!Enum.TryParse(receipt.target, out BuildTarget target) || target == BuildTarget.NoTarget ||
			    !string.Equals(receipt.buildKind, ContentBuildKind.EditorCompatible.ToString(), StringComparison.Ordinal)) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.ReceiptInvalid,
					"The receipt does not describe an exact Editor-Compatible build target."));
				return Result(false, receiptPath, BuildTarget.NoTarget, diagnostics);
			}

			if (!BuildTargetMapper.TryMapEditor(context.EditorPlatform, out var expectedEditorTarget) ||
			    expectedEditorTarget != target) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.ReceiptTargetMismatch,
					$"Receipt target '{target}' is incompatible with editor host '{context.EditorPlatform}'. Build Editor-Compatible content on this editor OS.",
					target));
			}
			if (context.ActiveTarget != target) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.ReceiptTargetMismatch,
					$"Addressables' built-in Use Existing Build builder resolves the active target path. Switch Unity's active target to exact receipt target '{target}', then validate again.",
					target));
			}

			if (!string.Equals(receipt.settingsGuid, context.SettingsGuid, StringComparison.Ordinal) ||
			    !string.Equals(receipt.settingsHash, context.SettingsHash, StringComparison.Ordinal)) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.ReceiptStale,
					"Addressables settings or one of their serialized dependencies changed after the Editor-Compatible build.",
					target));
			}
			if (!string.Equals(receipt.addressablesVersion, context.AddressablesVersion, StringComparison.Ordinal) ||
			    !string.Equals(receipt.unityVersion, context.UnityVersion, StringComparison.Ordinal)) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.ReceiptStale,
					"The Unity or Addressables version differs from the version recorded by the Editor-Compatible build.",
					target));
			}

			if (string.IsNullOrEmpty(receipt.settingsFilePath) ||
			    context.FileExists == null || !context.FileExists(receipt.settingsFilePath)) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.ReceiptStale,
					$"The receipt's built settings file is missing: '{receipt.settingsFilePath}'.",
					target));
			} else {
				try {
					var lengthMatches = context.FileLength != null &&
					                    context.FileLength(receipt.settingsFilePath) == receipt.settingsFileLength;
					var timeMatches = context.LastWriteUtcTicks != null &&
					                  context.LastWriteUtcTicks(receipt.settingsFilePath) == receipt.settingsFileLastWriteUtcTicks;
					var hashMatches = context.FileHash != null &&
					                  string.Equals(context.FileHash(receipt.settingsFilePath), receipt.settingsFileHash, StringComparison.Ordinal);
					if (!lengthMatches || !timeMatches || !hashMatches) {
						diagnostics.Add(Error(
							ContentBuildDiagnosticCode.ReceiptStale,
							"The built settings artifact changed after the receipt was created.",
							target));
					}
				} catch (Exception exception) {
					diagnostics.Add(Error(
						ContentBuildDiagnosticCode.ReceiptStale,
						$"The built settings artifact could not be fingerprinted: {exception.Message}",
						target));
				}
			}

			if (receipt.createdUtcTicks <= 0 || receipt.buildCompletedUtcTicks <= 0 ||
			    receipt.createdUtcTicks < receipt.buildCompletedUtcTicks ||
			    receipt.settingsFileLastWriteUtcTicks > receipt.createdUtcTicks) {
				diagnostics.Add(Error(
					ContentBuildDiagnosticCode.ReceiptInvalid,
					"Receipt freshness timestamps are incomplete or inconsistent.",
					target));
			}

			return Result(
				diagnostics.All(item => item.Severity != ContentBuildDiagnosticSeverity.Error),
				receiptPath,
				target,
				diagnostics);
		}

		private static ExistingBuildValidation Result(
			bool valid,
			string receiptPath,
			BuildTarget target,
			IEnumerable<ContentBuildDiagnostic> diagnostics) =>
			new ExistingBuildValidation(valid, receiptPath, target, diagnostics);

		private static ContentBuildDiagnostic Error(
			ContentBuildDiagnosticCode code,
			string message,
			BuildTarget target = BuildTarget.NoTarget) =>
			new ContentBuildDiagnostic(code, ContentBuildDiagnosticSeverity.Error, message, target);
	}

	internal static class BuildReceiptService {
		internal static BuildReceiptCreationResult Create(
			BuildJobRecord record,
			BuildExecutionOutcome outcome,
			BuildTarget target,
			UnityContentBuildBackend backend) {
			var settingsPath = Path.Combine(outcome.OutputPath ?? string.Empty, "settings.json");
			if (!File.Exists(settingsPath)) {
				return new BuildReceiptCreationResult(
					false,
					$"Editor-Compatible content built, but its settings artifact is missing at '{settingsPath}'.");
			}

			try {
				var now = backend.UtcNowTicks;
				var settingsInfo = new FileInfo(settingsPath);
				var receipt = new ContentBuildReceipt {
					jobId = record.jobId,
					buildKind = ContentBuildKind.EditorCompatible.ToString(),
					target = target.ToString(),
					settingsGuid = record.settingsGuid,
					settingsHash = record.settingsHash,
					addressablesVersion = record.addressablesVersion,
					unityVersion = Application.unityVersion,
					outputPath = UnityContentBuildBackend.AbsolutePath(outcome.OutputPath),
					settingsFilePath = UnityContentBuildBackend.AbsolutePath(settingsPath),
					settingsFileHash = ContentBuildIdentity.FileHash(settingsPath),
					settingsFileLength = settingsInfo.Length,
					settingsFileLastWriteUtcTicks = settingsInfo.LastWriteTimeUtc.Ticks,
					buildCompletedUtcTicks = now,
					createdUtcTicks = now
				};

				var operationReceipt = Path.Combine(record.operationDirectory, "editor-compatible-receipt.json");
				if (File.Exists(operationReceipt)) {
					return new BuildReceiptCreationResult(
						false,
						$"Refusing to overwrite operation receipt '{operationReceipt}'.");
				}
				File.WriteAllText(operationReceipt, JsonUtility.ToJson(receipt, true), new UTF8Encoding(false));

				var latestReceipt = UnityContentBuildBackend.AbsolutePath(
					UnityContentBuildBackend.LatestEditorReceiptRelativePath);
				UnityBuildJobStore.AtomicWrite(latestReceipt, JsonUtility.ToJson(receipt, true));
				return new BuildReceiptCreationResult(
					true,
					"Editor-Compatible receipt created.",
					operationReceipt);
			} catch (Exception exception) {
				return new BuildReceiptCreationResult(
					false,
					$"Editor-Compatible receipt creation failed: {exception.Message}");
			}
		}

		internal static ExistingBuildValidation ValidateCurrent() {
			var backend = new UnityContentBuildBackend();
			var receiptPath = UnityContentBuildBackend.AbsolutePath(
				UnityContentBuildBackend.LatestEditorReceiptRelativePath);
			if (!File.Exists(receiptPath)) {
				return new ExistingBuildValidation(
					false,
					receiptPath,
					BuildTarget.NoTarget,
					new[] { new ContentBuildDiagnostic(
						ContentBuildDiagnosticCode.ReceiptMissing,
						ContentBuildDiagnosticSeverity.Error,
						$"No package-owned Editor-Compatible receipt exists at '{receiptPath}'. Run an Editor-Compatible build first.") });
			}

			ContentBuildReceipt receipt;
			try {
				receipt = JsonUtility.FromJson<ContentBuildReceipt>(File.ReadAllText(receiptPath));
			} catch (Exception exception) {
				return new ExistingBuildValidation(
					false,
					receiptPath,
					BuildTarget.NoTarget,
					new[] { new ContentBuildDiagnostic(
						ContentBuildDiagnosticCode.ReceiptInvalid,
						ContentBuildDiagnosticSeverity.Error,
						$"The package-owned receipt could not be read: {exception.Message}") });
			}

			return BuildReceiptValidator.Validate(
				receipt,
				receiptPath,
				new BuildReceiptValidationContext(
					backend.ActiveTarget,
					backend.EditorPlatform,
					backend.SettingsGuid,
					backend.SettingsHash,
					backend.AddressablesVersion,
					Application.unityVersion,
					File.Exists,
					path => new FileInfo(path).Length,
					path => File.GetLastWriteTimeUtc(path).Ticks,
					ContentBuildIdentity.FileHash));
		}
	}

	internal static class ExistingBuildPlayModeService {
		internal static ExistingBuildValidation Validate() => BuildReceiptService.ValidateCurrent();

		internal static ExistingBuildValidation Select(bool explicitlyConfirmed) {
			var validation = Validate();
			if (!validation.IsValid) return validation;
			if (!explicitlyConfirmed) {
				return new ExistingBuildValidation(
					false,
					validation.ReceiptPath,
					validation.Target,
					new[] { new ContentBuildDiagnostic(
						ContentBuildDiagnosticCode.ExistingBuildConfirmationRequired,
						ContentBuildDiagnosticSeverity.Error,
						"Explicit confirmation is required before changing Addressables' active Play Mode data builder.",
						validation.Target) });
			}

			var settings = AddressableAssetSettingsDefaultObject.SettingsExists
				? AddressableAssetSettingsDefaultObject.GetSettings(false)
				: null;
			if (settings == null) return validation;
			var index = settings.DataBuilders.FindIndex(item => item is BuildScriptPackedPlayMode);
			if (index < 0) {
				return new ExistingBuildValidation(
					false,
					validation.ReceiptPath,
					validation.Target,
					new[] { new ContentBuildDiagnostic(
						ContentBuildDiagnosticCode.ExistingBuildBuilderMissing,
						ContentBuildDiagnosticSeverity.Error,
						"Addressables settings do not contain the built-in Use Existing Build (requires built groups) data builder.",
						validation.Target) });
			}

			settings.ActivePlayModeDataBuilderIndex = index;
			EditorUtility.SetDirty(settings);
			AssetDatabase.SaveAssets();
			return new ExistingBuildValidation(
				true,
				validation.ReceiptPath,
				validation.Target,
				new[] { new ContentBuildDiagnostic(
					ContentBuildDiagnosticCode.ExistingBuildSelected,
					ContentBuildDiagnosticSeverity.Info,
					"Addressables' built-in Use Existing Build (requires built groups) data builder is now selected.",
					validation.Target) });
		}
	}

	/// <summary>Provides the public, recoverable entry points for Addressables content builds.</summary>
	public static class AddressablesBuildQueue {
		/// <summary>Analyzes a request without starting a build or changing project state.</summary>
		/// <param name="request">The request to validate.</param>
		/// <returns>An immutable preflight result.</returns>
		public static ContentBuildPreflight Analyze(ContentBuildRequest request) => CreateEngine().Analyze(request);
		/// <summary>Starts a request after repeating its fail-closed preflight checks.</summary>
		/// <param name="request">The request to execute.</param>
		/// <returns>The build result or persisted resume state.</returns>
		public static ContentBuildResult Enqueue(ContentBuildRequest request) => CreateEngine().Start(request);
		/// <summary>Resumes a persisted build job after a domain reload or target switch.</summary>
		/// <returns>The updated build result.</returns>
		public static ContentBuildResult Resume() => CreateEngine().Resume();
		/// <summary>Requests cancellation of the current package-owned build job.</summary>
		/// <returns>The updated build result.</returns>
		public static ContentBuildResult Cancel() => CreateEngine().RequestCancellation();
		/// <summary>Attempts to restore the target active before the current job began.</summary>
		/// <returns>The restoration result.</returns>
		public static ContentBuildResult RestoreOriginalTarget() => CreateEngine().RestoreOriginalTarget();
		/// <summary>Explicitly abandons the current job and archives its recovery record.</summary>
		/// <returns>The abandon/reset result.</returns>
		public static ContentBuildResult AbandonReset() => CreateEngine().AbandonReset();
		/// <summary>Inspects current package-owned recovery state without mutating it.</summary>
		/// <returns>The current recovery-state description.</returns>
		public static ContentBuildRecoveryInfo InspectRecovery() => CreateEngine().InspectRecovery();
		/// <summary>Validates the latest editor-compatible build receipt and artifacts.</summary>
		/// <returns>The existing-build validation result.</returns>
		public static ExistingBuildValidation ValidateExistingBuild() => ExistingBuildPlayModeService.Validate();
		/// <summary>Validates and explicitly selects Addressables' built-in existing-build Play Mode data builder.</summary>
		/// <param name="explicitlyConfirmed">Whether the caller explicitly confirmed the project-state change.</param>
		/// <returns>The selection result and diagnostics.</returns>
		public static ExistingBuildValidation SelectExistingBuild(bool explicitlyConfirmed) =>
			ExistingBuildPlayModeService.Select(explicitlyConfirmed);

		private static ContentBuildJobEngine CreateEngine() =>
			new ContentBuildJobEngine(new UnityContentBuildBackend(), new UnityBuildJobStore());
	}
}
