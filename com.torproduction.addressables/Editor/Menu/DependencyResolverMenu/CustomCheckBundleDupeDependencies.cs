using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace TorProduction.Addressables.Editor {
	internal sealed class DuplicateDependencyOccurrence {
		internal DuplicateDependencyOccurrence(
			string assetGuid,
			string assetPath,
			string referencingGroupGuid,
			string referencingGroupName) {
			AssetGuid = assetGuid ?? string.Empty;
			AssetPath = assetPath ?? string.Empty;
			ReferencingGroupGuid = referencingGroupGuid ?? string.Empty;
			ReferencingGroupName = referencingGroupName ?? string.Empty;
		}

		internal string AssetGuid { get; }
		internal string AssetPath { get; }
		internal string ReferencingGroupGuid { get; }
		internal string ReferencingGroupName { get; }
	}

	internal sealed class DuplicateDependencyAdapterResult {
		internal DuplicateDependencyAdapterResult(
			bool supported,
			bool succeeded,
			string version,
			string diagnostic,
			IEnumerable<DuplicateDependencyOccurrence> occurrences) {
			Supported = supported;
			Succeeded = succeeded;
			Version = version ?? string.Empty;
			Diagnostic = diagnostic ?? string.Empty;
			Occurrences = (occurrences ?? Array.Empty<DuplicateDependencyOccurrence>()).ToArray();
		}

		internal bool Supported { get; }
		internal bool Succeeded { get; }
		internal string Version { get; }
		internal string Diagnostic { get; }
		internal IReadOnlyList<DuplicateDependencyOccurrence> Occurrences { get; }
	}

	internal interface IDuplicateDependencyAdapter {
		string Version { get; }
		bool IsVerified { get; }
		string CapabilityDiagnostic { get; }
		DuplicateDependencyAdapterResult Analyze(AddressableAssetSettings settings);
	}

	/// <summary>
	/// Compatibility adapter over the supported Addressables analyzer lifecycle. It deliberately uses only
	/// RefreshAnalysis and the protected CheckDupeResults contract documented by Addressables.
	/// </summary>
	internal sealed class AddressablesDuplicateDependencyAdapter : CheckBundleDupeDependencies,
		IDuplicateDependencyAdapter {
		private static readonly HashSet<string> VerifiedVersions = new HashSet<string>(StringComparer.Ordinal) {
			"2.7.6",
			"2.9.1"
		};

		internal AddressablesDuplicateDependencyAdapter() : this(DetectAddressablesVersion()) { }

		internal AddressablesDuplicateDependencyAdapter(string version) {
			Version = version ?? string.Empty;
		}

		public override bool CanFix => false;
		internal string Version { get; }
		internal bool IsVerified => IsVerifiedVersion(Version);
		internal string CapabilityDiagnostic => IsVerified
			? $"Addressables {Version} duplicate-dependency analysis is verified."
			: $"Addressables {DisplayVersion(Version)} is not a verified duplicate-dependency adapter. " +
			  "Fix is disabled; use Addressables 2.7.6 or 2.9.1, or add and validate a dedicated adapter before fixing.";

		string IDuplicateDependencyAdapter.Version => Version;
		bool IDuplicateDependencyAdapter.IsVerified => IsVerified;
		string IDuplicateDependencyAdapter.CapabilityDiagnostic => CapabilityDiagnostic;

		public override void FixIssues(AddressableAssetSettings settings) {
			throw new InvalidOperationException(
				"This adapter is analyze-only. Use the package's separately confirmed dependency Fix action.");
		}

		public DuplicateDependencyAdapterResult Analyze(AddressableAssetSettings settings) {
			if (settings == null) {
				return Failure("Addressables settings are missing.");
			}
			if (!IsVerified) {
				return new DuplicateDependencyAdapterResult(
					false, false, Version, CapabilityDiagnostic,
					Array.Empty<DuplicateDependencyOccurrence>());
			}

			try {
				var lifecycleResults = RefreshAnalysis(settings) ?? new List<AnalyzeResult>();
				var duplicateResults = CheckDupeResults?.ToArray() ?? Array.Empty<CheckDupeResult>();
				var lifecycleSucceeded = duplicateResults.Length > 0 ||
				                         lifecycleResults.Any(item =>
					                         item != null &&
					                         string.Equals(item.resultName, "No issues found", StringComparison.Ordinal));
				if (!lifecycleSucceeded) {
					var details = string.Join(" | ", lifecycleResults
						.Where(item => item != null && !string.IsNullOrWhiteSpace(item.resultName))
						.Select(item => item.resultName));
					return Failure(string.IsNullOrEmpty(details)
						? "The Addressables analyzer did not produce a successful lifecycle result."
						: $"The Addressables analyzer did not complete successfully: {details}");
				}

				var occurrences = duplicateResults.Select(item => new DuplicateDependencyOccurrence(
					item.DuplicatedGroupGuid.ToString(),
					item.AssetPath,
					item.Group == null ? string.Empty : item.Group.Guid,
					item.Group == null ? string.Empty : item.Group.Name));
				return new DuplicateDependencyAdapterResult(
					true, true, Version, CapabilityDiagnostic, occurrences);
			} catch (Exception exception) {
				return Failure($"Addressables duplicate-dependency analysis failed: {exception.Message}");
			}
		}

		internal static bool IsVerifiedVersion(string version) {
			return !string.IsNullOrEmpty(version) && VerifiedVersions.Contains(version);
		}

		private DuplicateDependencyAdapterResult Failure(string diagnostic) {
			return new DuplicateDependencyAdapterResult(
				IsVerified, false, Version, diagnostic,
				Array.Empty<DuplicateDependencyOccurrence>());
		}

		private static string DetectAddressablesVersion() {
			return PackageInfo.FindForAssembly(typeof(CheckBundleDupeDependencies).Assembly)?.version ?? string.Empty;
		}

		private static string DisplayVersion(string version) {
			return string.IsNullOrEmpty(version) ? "version (unknown)" : version;
		}
	}
}
