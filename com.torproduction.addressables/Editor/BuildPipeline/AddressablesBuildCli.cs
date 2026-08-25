using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TorProduction.Addressables.Editor;
using UnityEditor;
using UnityEngine;

namespace TorProduction.Addressables.Editor.Cli {
	internal interface IAddressablesBuildCliApi {
		ContentBuildPreflight Analyze(ContentBuildRequest request);
		ContentBuildResult Start(ContentBuildRequest request);
		ContentBuildResult Resume();
		ContentBuildResult Cancel();
		ContentBuildResult Restore();
		ContentBuildResult AbandonReset();
		ExistingBuildValidation ValidateExistingBuild();
		ExistingBuildValidation SelectExistingBuild(bool confirmed);
	}

	internal sealed class AddressablesBuildCliApi : IAddressablesBuildCliApi {
		public ContentBuildPreflight Analyze(ContentBuildRequest request) => AddressablesBuildQueue.Analyze(request);
		public ContentBuildResult Start(ContentBuildRequest request) => AddressablesBuildQueue.Enqueue(request);
		public ContentBuildResult Resume() => AddressablesBuildQueue.Resume();
		public ContentBuildResult Cancel() => AddressablesBuildQueue.Cancel();
		public ContentBuildResult Restore() => AddressablesBuildQueue.RestoreOriginalTarget();
		public ContentBuildResult AbandonReset() => AddressablesBuildQueue.AbandonReset();
		public ExistingBuildValidation ValidateExistingBuild() => AddressablesBuildQueue.ValidateExistingBuild();
		public ExistingBuildValidation SelectExistingBuild(bool confirmed) => AddressablesBuildQueue.SelectExistingBuild(confirmed);
	}

	/// <summary>Exposes batchmode content-build and recovery operations through command-line arguments.</summary>
	public static class AddressablesCli {
		/// <summary>Executes the requested <c>-torAction</c> and throws when the action reports failure.</summary>
		public static void Run() {
			var exitCode = Run(Environment.GetCommandLineArgs(), new AddressablesBuildCliApi(), Debug.Log);
			if (exitCode != 0) {
				throw new InvalidOperationException(
					$"Tor Production Addressables CLI failed with exit code {exitCode}. Review the structured diagnostics above.");
			}
		}

		internal static int Run(
			string[] arguments,
			IAddressablesBuildCliApi api,
			Action<string> writeOutput) {
			try {
				var options = CliOptions.Parse(arguments ?? Array.Empty<string>());
				if (string.IsNullOrEmpty(options.Action)) {
					writeOutput(ErrorJson("Missing -torAction. Use analyze, full-build, content-update, editor-compatible, multi-platform, resume, cancel-build-job, restore-target, abandon-build-job, validate-existing-build, or select-existing-build."));
					return 1;
				}

				switch (options.Action) {
					case "analyze":
						return Analyze(options, api, writeOutput);
					case "full-build":
						return Start(BuildRequest(options, ContentBuildKind.Full), options, api, writeOutput);
					case "content-update":
						return Start(BuildRequest(options, ContentBuildKind.ContentUpdate), options, api, writeOutput);
					case "editor-compatible":
						return Start(ContentBuildRequest.EditorCompatible(), options, api, writeOutput);
					case "multi-platform":
						return Start(BuildRequest(options, ContentBuildKind.MultiPlatform), options, api, writeOutput);
					case "resume":
						return WriteResult(api.Resume(), options, writeOutput);
					case "cancel-build-job":
					case "cancel":
						return WriteResult(api.Cancel(), options, writeOutput);
					case "restore-target":
						return WriteResult(api.Restore(), options, writeOutput);
					case "abandon-build-job":
					case "reset-build-job":
						return WriteResult(api.AbandonReset(), options, writeOutput);
					case "validate-existing-build":
						return WriteValidation(api.ValidateExistingBuild(), writeOutput);
					case "select-existing-build":
						return WriteValidation(api.SelectExistingBuild(options.ConfirmExistingBuild), writeOutput);
					default:
						writeOutput(ErrorJson($"Unknown -torAction '{options.Action}'."));
						return 1;
				}
			} catch (Exception exception) {
				writeOutput(ErrorJson(exception.Message));
				return 1;
			}
		}

		private static int Analyze(CliOptions options, IAddressablesBuildCliApi api, Action<string> writeOutput) {
			if (!TryParseKind(options.Kind, out var kind)) {
				throw new ArgumentException("Analyze requires -torKind Full, ContentUpdate, EditorCompatible, or MultiPlatform.");
			}
			var request = BuildRequest(options, kind);
			var preflight = api.Analyze(request);
			writeOutput(FormatPreflight(preflight));
			return preflight.IsValid ? 0 : 1;
		}

		private static int Start(
			ContentBuildRequest request,
			CliOptions options,
			IAddressablesBuildCliApi api,
			Action<string> writeOutput) {
			var preflight = api.Analyze(request);
			writeOutput(FormatPreflight(preflight));
			if (!preflight.IsValid) return 1;

			var result = api.Start(request);
			return WriteResult(result, options, writeOutput);
		}

		private static int WriteResult(
			ContentBuildResult result,
			CliOptions options,
			Action<string> writeOutput) {
			writeOutput(FormatResult(result));
			if (!string.IsNullOrEmpty(options.ReportPath) &&
			    !string.IsNullOrEmpty(result.ReportPath) && File.Exists(result.ReportPath)) {
				var destination = Path.GetFullPath(options.ReportPath);
				var directory = Path.GetDirectoryName(destination);
				if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
				File.Copy(result.ReportPath, destination, true);
			}
			return result.Status == ContentBuildStatus.FatalFailure ||
			       result.Status == ContentBuildStatus.TargetSwitchFailure ||
			       result.Status == ContentBuildStatus.RestorationFailure
				? 1
				: 0;
		}

		private static int WriteValidation(ExistingBuildValidation validation, Action<string> writeOutput) {
			writeOutput(FormatValidation(validation));
			return validation.IsValid ? 0 : 1;
		}

		private static ContentBuildRequest BuildRequest(CliOptions options, ContentBuildKind kind) {
			switch (kind) {
				case ContentBuildKind.Full:
					return ContentBuildRequest.Full(ParsePlatform(options.Target));
				case ContentBuildKind.ContentUpdate:
					return ContentBuildRequest.ContentUpdate(ParsePlatform(options.Target), options.StateFilePath);
				case ContentBuildKind.EditorCompatible:
					return ContentBuildRequest.EditorCompatible();
				case ContentBuildKind.MultiPlatform:
					if (string.IsNullOrWhiteSpace(options.Targets)) {
						throw new ArgumentException("Multi-Platform requires -torTargets with a comma-separated explicit queue.");
					}
					var platforms = options.Targets.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(ParsePlatform).ToArray();
					return ContentBuildRequest.MultiPlatform(
						platforms,
						options.ContinueOnError
							? ContentBuildFailurePolicy.ContinueOnError
							: ContentBuildFailurePolicy.StopOnFirstFailure);
				default:
					throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported build kind.");
			}
		}

		private static ContentBuildPlatform ParsePlatform(string value) {
			if (string.IsNullOrWhiteSpace(value)) {
				throw new ArgumentException("An explicit -torTarget is required.");
			}
			if (Enum.TryParse(value.Trim(), true, out ContentBuildPlatform platform) &&
			    Enum.IsDefined(typeof(ContentBuildPlatform), platform)) {
				return platform;
			}
			if (Enum.TryParse(value.Trim(), true, out BuildTarget target) &&
			    BuildTargetMapper.TryMap(target, out platform)) {
				return platform;
			}
			throw new ArgumentException(
				$"Target '{value}' is unsupported. Use Android, iOS, Windows/StandaloneWindows64, macOS/StandaloneOSX, or Linux/StandaloneLinux64.");
		}

		private static bool TryParseKind(string value, out ContentBuildKind kind) {
			if (Enum.TryParse(value ?? string.Empty, true, out kind) &&
			    Enum.IsDefined(typeof(ContentBuildKind), kind)) return true;
			var normalized = (value ?? string.Empty).Replace("-", string.Empty);
			return Enum.TryParse(normalized, true, out kind) && Enum.IsDefined(typeof(ContentBuildKind), kind);
		}

		internal static string FormatPreflight(ContentBuildPreflight preflight) =>
			JsonUtility.ToJson(new CliPreflight {
				valid = preflight.IsValid,
				kind = preflight.Request?.Kind.ToString() ?? string.Empty,
				targets = preflight.Targets.Select(item => item.ToString()).ToArray(),
				requestHash = preflight.RequestHash,
				settingsGuid = preflight.SettingsGuid,
				settingsHash = preflight.SettingsHash,
				stateFileHash = preflight.StateFileHash,
				addressablesVersion = preflight.AddressablesVersion,
				diagnostics = preflight.Diagnostics.Select(FormatDiagnostic).ToArray()
			}, true);

		internal static string FormatResult(ContentBuildResult result) =>
			JsonUtility.ToJson(new CliResult {
				jobId = result.JobId,
				status = result.Status.ToString(),
				message = result.Message,
				reportPath = result.ReportPath,
				recoveryPath = result.RecoveryPath,
				requiresUserAction = result.RequiresUserAction,
				items = result.Items.Select(item =>
					$"{item.Target}:{item.Status}:{item.Message}:{item.OutputPath}:{item.LayoutPath}:{item.ReceiptPath}").ToArray(),
				diagnostics = result.Diagnostics.Select(FormatDiagnostic).ToArray()
			}, true);

		internal static string FormatValidation(ExistingBuildValidation validation) =>
			JsonUtility.ToJson(new CliValidation {
				valid = validation.IsValid,
				receiptPath = validation.ReceiptPath,
				target = validation.Target.ToString(),
				diagnostics = validation.Diagnostics.Select(FormatDiagnostic).ToArray()
			}, true);

		private static string FormatDiagnostic(ContentBuildDiagnostic item) =>
			$"{item.Severity}:{item.Code}:{item.Target}:{item.Message}";

		private static string ErrorJson(string message) =>
			JsonUtility.ToJson(new CliError { error = message ?? string.Empty }, true);

		[Serializable]
		private sealed class CliPreflight {
			public bool valid;
			public string kind;
			public string[] targets;
			public string requestHash;
			public string settingsGuid;
			public string settingsHash;
			public string stateFileHash;
			public string addressablesVersion;
			public string[] diagnostics;
		}

		[Serializable]
		private sealed class CliResult {
			public string jobId;
			public string status;
			public string message;
			public string reportPath;
			public string recoveryPath;
			public bool requiresUserAction;
			public string[] items;
			public string[] diagnostics;
		}

		[Serializable]
		private sealed class CliValidation {
			public bool valid;
			public string receiptPath;
			public string target;
			public string[] diagnostics;
		}

		[Serializable]
		private sealed class CliError {
			public string error;
		}

		private sealed class CliOptions {
			internal string Action { get; private set; } = string.Empty;
			internal string Kind { get; private set; } = string.Empty;
			internal string Target { get; private set; } = string.Empty;
			internal string Targets { get; private set; } = string.Empty;
			internal string StateFilePath { get; private set; } = string.Empty;
			internal string ReportPath { get; private set; } = string.Empty;
			internal bool ContinueOnError { get; private set; }
			internal bool ConfirmExistingBuild { get; private set; }

			internal static CliOptions Parse(IReadOnlyList<string> args) {
				var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				for (var index = 0; index < args.Count; index++) {
					var item = args[index];
					if (string.IsNullOrEmpty(item) || !item.StartsWith("-tor", StringComparison.OrdinalIgnoreCase)) continue;
					values[item] = index + 1 < args.Count && !args[index + 1].StartsWith("-tor", StringComparison.OrdinalIgnoreCase)
						? args[++index]
						: "true";
				}

				return new CliOptions {
					Action = Get(values, "-torAction").ToLowerInvariant(),
					Kind = Get(values, "-torKind"),
					Target = Get(values, "-torTarget"),
					Targets = Get(values, "-torTargets"),
					StateFilePath = Get(values, "-torStateFile"),
					ReportPath = Get(values, "-torReport"),
					ContinueOnError = ParseBool(Get(values, "-torContinueOnError")),
					ConfirmExistingBuild = ParseBool(Get(values, "-torConfirmExistingBuild"))
				};
			}

			private static string Get(IReadOnlyDictionary<string, string> values, string key) =>
				values.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

			private static bool ParseBool(string value) =>
				bool.TryParse(value, out var parsed) && parsed;
		}
	}
}
