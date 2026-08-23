using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TorProduction.Addressables.Editor {
	internal sealed class GroupSyncProjectState {
		internal bool SettingsExist;
		internal string SettingsIdentity = string.Empty;
		internal string ConfigGuid = string.Empty;
		internal string ConfigJson = string.Empty;
		internal readonly List<GroupSyncRuleState> Rules = new List<GroupSyncRuleState>();
		internal readonly List<GroupSyncGroupState> Groups = new List<GroupSyncGroupState>();
		internal readonly List<GroupSyncEntryState> Entries = new List<GroupSyncEntryState>();
		internal readonly HashSet<string> Labels = new HashSet<string>(StringComparer.Ordinal);
		internal readonly List<AutomationDiagnostic> Diagnostics = new List<AutomationDiagnostic>();

		internal string ComputeHash() {
			var builder = new StringBuilder();
			Append(builder, SettingsExist ? "1" : "0");
			Append(builder, SettingsIdentity);
			Append(builder, ConfigGuid);
			Append(builder, ConfigJson);
			foreach (var rule in Rules.OrderBy(item => item.Index)) {
				Append(builder, rule.Index.ToString());
				Append(builder, rule.SourceFolderPath);
				Append(builder, rule.DestinationGroupGuid);
				Append(builder, rule.DestinationGroupName);
				Append(builder, rule.AddressPrefix);
				Append(builder, ((int)rule.AddressPolicy).ToString());
				Append(builder, ((int)rule.LabelPolicy).ToString());
				foreach (var label in (rule.RequiredLabels ?? Array.Empty<string>()).OrderBy(item => item, StringComparer.Ordinal)) {
					Append(builder, label);
				}
				foreach (var typeName in (rule.TypeFilterNames ?? Array.Empty<string>()).OrderBy(item => item, StringComparer.Ordinal)) {
					Append(builder, typeName);
				}
				foreach (var asset in rule.Assets.OrderBy(item => item.Guid, StringComparer.Ordinal)
				         .ThenBy(item => item.Path, StringComparer.Ordinal)) {
					Append(builder, asset.Guid);
					Append(builder, asset.Path);
					Append(builder, asset.AssetType?.AssemblyQualifiedName);
					Append(builder, asset.LoadError);
				}
			}
			foreach (var group in Groups.OrderBy(item => item.Guid, StringComparer.Ordinal)
			         .ThenBy(item => item.Name, StringComparer.Ordinal)) {
				Append(builder, group.Guid);
				Append(builder, group.Name);
				Append(builder, group.ReadOnly ? "1" : "0");
				Append(builder, group.HasBundledSchema ? "1" : "0");
				Append(builder, group.HasContentUpdateSchema ? "1" : "0");
				Append(builder, group.IsBuildable ? "1" : "0");
			}
			foreach (var entry in Entries.OrderBy(item => item.Guid, StringComparer.Ordinal)) {
				Append(builder, entry.Guid);
				Append(builder, entry.Path);
				Append(builder, entry.GroupGuid);
				Append(builder, entry.GroupName);
				Append(builder, entry.Address);
				Append(builder, entry.IsFolder ? "1" : "0");
				foreach (var label in entry.Labels.OrderBy(item => item, StringComparer.Ordinal)) {
					Append(builder, label);
				}
			}
			foreach (var label in Labels.OrderBy(item => item, StringComparer.Ordinal)) {
				Append(builder, label);
			}
			foreach (var diagnostic in Diagnostics.OrderBy(item => item.Location, StringComparer.Ordinal)
			         .ThenBy(item => item.Code)) {
				Append(builder, diagnostic.Code.ToString());
				Append(builder, diagnostic.Severity.ToString());
				Append(builder, diagnostic.Location);
				Append(builder, diagnostic.Message);
			}
			return AutomationHash.Compute(builder.ToString());
		}

		private static void Append(StringBuilder builder, string value) {
			value = value ?? string.Empty;
			builder.Append(value.Length).Append(':').Append(value).Append('|');
		}
	}

	internal sealed class GroupSyncRuleState {
		internal int Index;
		internal string SourceFolderPath = string.Empty;
		internal string DestinationGroupGuid = string.Empty;
		internal string DestinationGroupName = string.Empty;
		internal string AddressPrefix = string.Empty;
		internal GroupAddressPolicy AddressPolicy;
		internal ExistingLabelPolicy LabelPolicy;
		internal string[] RequiredLabels = Array.Empty<string>();
		internal string[] TypeFilterNames = Array.Empty<string>();
		internal Type[] ResolvedTypes = Array.Empty<Type>();
		internal readonly List<GroupSyncAssetState> Assets = new List<GroupSyncAssetState>();
	}

	internal sealed class GroupSyncAssetState {
		internal string Guid = string.Empty;
		internal string Path = string.Empty;
		internal Type AssetType;
		internal string LoadError = string.Empty;
	}

	internal sealed class GroupSyncGroupState {
		internal string Guid = string.Empty;
		internal string Name = string.Empty;
		internal bool ReadOnly;
		internal bool HasBundledSchema;
		internal bool HasContentUpdateSchema;
		internal bool IsBuildable = true;
	}

	internal sealed class GroupSyncEntryState {
		internal string Guid = string.Empty;
		internal string Path = string.Empty;
		internal string GroupGuid = string.Empty;
		internal string GroupName = string.Empty;
		internal string Address = string.Empty;
		internal bool IsFolder;
		internal string[] Labels = Array.Empty<string>();
	}

	internal static class GroupSyncPlanner {
		internal static AutomationPlan Create(
			GroupSyncProjectState state,
			AddressablesAutomationConfig config = null) {
			if (state == null) {
				throw new ArgumentNullException(nameof(state));
			}

			var diagnostics = new List<AutomationDiagnostic>(state.Diagnostics);
			var operations = new List<AutomationOperation>();
			var sourceHash = state.ComputeHash();
			if (!state.SettingsExist) {
				diagnostics.Add(Error(
					AutomationDiagnosticCode.AddressablesSettingsMissing,
					"Addressables",
					"Addressables settings do not exist. Analysis did not create them."));
				return Build(sourceHash, operations, diagnostics, config, state.ConfigGuid);
			}

			var groupsByGuid = state.Groups
				.Where(group => !string.IsNullOrEmpty(group.Guid))
				.GroupBy(group => group.Guid, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
			var groupsByName = state.Groups
				.Where(group => !string.IsNullOrEmpty(group.Name))
				.GroupBy(group => group.Name, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
			var entriesByGuid = state.Entries
				.Where(entry => !string.IsNullOrEmpty(entry.Guid))
				.GroupBy(entry => entry.Guid, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
			var claims = new Dictionary<string, DesiredEntry>(StringComparer.Ordinal);
			var destinationGroups = new Dictionary<string, DestinationGroup>(StringComparer.Ordinal);

			foreach (var rule in state.Rules.OrderBy(item => item.Index)) {
				var location = $"Groups[{rule.Index}]";
				if (string.IsNullOrEmpty(rule.SourceFolderPath)) {
					diagnostics.Add(Error(
						AutomationDiagnosticCode.SourceFolderMissing,
						location,
						"The configured source folder does not resolve."));
					continue;
				}

				var destination = ResolveDestination(rule, groupsByGuid, groupsByName);
				if (destination == null) {
					diagnostics.Add(Error(
						AutomationDiagnosticCode.DestinationGroupMissing,
						location,
						"The destination group has neither a resolvable GUID nor a non-empty fallback name."));
					continue;
				}
				destinationGroups[destination.Key] = destination;
				if (destination.Existing?.ReadOnly == true) {
					diagnostics.Add(Error(
						AutomationDiagnosticCode.DestinationGroupReadOnly,
						location,
						$"Destination group '{destination.Name}' is read-only."));
				}
				if (destination.Existing != null && destination.Existing.HasBundledSchema &&
				    !destination.Existing.IsBuildable) {
					diagnostics.Add(Error(
						AutomationDiagnosticCode.DestinationGroupNonBuildable,
						location,
						$"Destination group '{destination.Name}' has an invalid bundled build/load path configuration."));
				}

				foreach (var asset in rule.Assets.OrderBy(item => item.Guid, StringComparer.Ordinal)
				         .ThenBy(item => item.Path, StringComparer.Ordinal)) {
					if (!string.IsNullOrEmpty(asset.LoadError) || asset.AssetType == null) {
						diagnostics.Add(new AutomationDiagnostic(
							AutomationDiagnosticCode.AssetLoadFailed,
							AutomationDiagnosticSeverity.Error,
							asset.Path,
							string.IsNullOrEmpty(asset.LoadError)
								? "The main asset could not be loaded. Apply is blocked because complete convergence cannot be proven."
								: asset.LoadError));
						continue;
					}
					if ((rule.ResolvedTypes?.Length ?? 0) != 0 &&
					    !rule.ResolvedTypes.Any(type => type != null && type.IsAssignableFrom(asset.AssetType))) {
						continue;
					}
					if (string.Equals(asset.Guid, state.ConfigGuid, StringComparison.Ordinal)) {
						diagnostics.Add(Error(
							AutomationDiagnosticCode.ConfigurationAssetClaimed,
							asset.Path,
							"A group rule claims the active configuration asset. Move the config outside the source folder or exclude its folder."));
						continue;
					}

					entriesByGuid.TryGetValue(asset.Guid, out var existing);
					var desired = BuildDesired(rule, asset, destination, existing);
					if (claims.TryGetValue(asset.Guid, out var other)) {
						if (!other.HasSameOutcome(desired)) {
							diagnostics.Add(Error(
								AutomationDiagnosticCode.AssetClaimConflict,
								asset.Path,
								$"The asset is claimed incompatibly by Groups[{other.RuleIndex}] and Groups[{rule.Index}]."));
						}
						continue;
					}
					claims.Add(asset.Guid, desired);
				}
			}

			DetectFolderEntryConflicts(state.Entries, claims.Values, diagnostics);
			DetectAddressCollisions(state.Entries, claims, diagnostics);
			if (diagnostics.Any(item => item.Severity == AutomationDiagnosticSeverity.Error)) {
				return Build(sourceHash, operations, diagnostics, config, state.ConfigGuid);
			}

			foreach (var destination in destinationGroups.Values.OrderBy(item => item.Name, StringComparer.Ordinal)
			         .ThenBy(item => item.Guid, StringComparer.Ordinal)) {
				if (destination.Existing == null) {
					operations.Add(new AutomationOperation(
						AutomationOperationKind.CreateGroup,
						groupName: destination.Name));
				}
				if (destination.Existing == null || !destination.Existing.HasBundledSchema) {
					operations.Add(new AutomationOperation(
						AutomationOperationKind.AddBundledAssetGroupSchema,
						groupGuid: destination.Guid,
						groupName: destination.Name));
				}
				if (destination.Existing == null || !destination.Existing.HasContentUpdateSchema) {
					operations.Add(new AutomationOperation(
						AutomationOperationKind.AddContentUpdateGroupSchema,
						groupGuid: destination.Guid,
						groupName: destination.Name));
				}
			}

			foreach (var label in claims.Values.SelectMany(item => item.DesiredLabels)
			         .Distinct(StringComparer.Ordinal)
			         .Where(label => !state.Labels.Contains(label))
			         .OrderBy(label => label, StringComparer.Ordinal)) {
				operations.Add(new AutomationOperation(AutomationOperationKind.CreateLabel, value: label));
			}

			foreach (var desired in claims.Values.OrderBy(item => item.Guid, StringComparer.Ordinal)
			         .ThenBy(item => item.Path, StringComparer.Ordinal)) {
				var existing = desired.Existing;
				if (existing == null) {
					operations.Add(EntryOperation(AutomationOperationKind.CreateEntry, desired));
				} else if (!destinationMatches(existing, desired.Destination)) {
					operations.Add(EntryOperation(AutomationOperationKind.MoveEntry, desired));
				}
				if (existing == null || !string.Equals(existing.Address, desired.Address, StringComparison.Ordinal)) {
					operations.Add(new AutomationOperation(
						AutomationOperationKind.SetAddress,
						desired.Guid, desired.Path,
						desired.Destination.Guid, desired.Destination.Name,
						desired.Address));
				}

				var currentLabels = new HashSet<string>(
					existing?.Labels ?? Array.Empty<string>(), StringComparer.Ordinal);
				foreach (var label in desired.DesiredLabels.Where(label => !currentLabels.Contains(label))
				         .OrderBy(label => label, StringComparer.Ordinal)) {
					operations.Add(new AutomationOperation(
						AutomationOperationKind.AddLabel,
						desired.Guid, desired.Path,
						desired.Destination.Guid, desired.Destination.Name,
						label));
				}
				foreach (var label in currentLabels.Where(label => !desired.DesiredLabels.Contains(label))
				         .OrderBy(label => label, StringComparer.Ordinal)) {
					operations.Add(new AutomationOperation(
						AutomationOperationKind.RemoveLabel,
						desired.Guid, desired.Path,
						desired.Destination.Guid, desired.Destination.Name,
						label));
				}
			}

			return Build(sourceHash, operations, diagnostics, config, state.ConfigGuid);
		}

		private static bool destinationMatches(GroupSyncEntryState entry, DestinationGroup destination) {
			return !string.IsNullOrEmpty(destination.Guid)
				? string.Equals(entry.GroupGuid, destination.Guid, StringComparison.Ordinal)
				: string.Equals(entry.GroupName, destination.Name, StringComparison.Ordinal);
		}

		private static AutomationOperation EntryOperation(
			AutomationOperationKind kind,
			DesiredEntry desired) {
			return new AutomationOperation(
				kind,
				desired.Guid,
				desired.Path,
				desired.Destination.Guid,
				desired.Destination.Name,
				labels: desired.DesiredLabels);
		}

		private static DesiredEntry BuildDesired(
			GroupSyncRuleState rule,
			GroupSyncAssetState asset,
			DestinationGroup destination,
			GroupSyncEntryState existing) {
			var address = rule.AddressPolicy == GroupAddressPolicy.PreserveExisting &&
			              existing != null && !string.IsNullOrEmpty(existing.Address)
				? existing.Address
				: GenerateAddress(rule.SourceFolderPath, asset.Path, rule.AddressPrefix);
			var labels = new HashSet<string>(StringComparer.Ordinal);
			if (rule.LabelPolicy == ExistingLabelPolicy.PreserveUnrelated && existing != null) {
				labels.UnionWith(existing.Labels ?? Array.Empty<string>());
			}
			labels.UnionWith(rule.RequiredLabels ?? Array.Empty<string>());
			return new DesiredEntry(
				rule.Index, asset.Guid, asset.Path, destination, address, labels, existing);
		}

		internal static string GenerateAddress(string sourceFolder, string assetPath, string prefix) {
			var normalizedFolder = (sourceFolder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
			var normalizedPath = (assetPath ?? string.Empty).Replace('\\', '/');
			var relative = normalizedPath.StartsWith(normalizedFolder + "/", StringComparison.Ordinal)
				? normalizedPath.Substring(normalizedFolder.Length + 1)
				: normalizedPath;
			var extension = Path.GetExtension(relative);
			if (!string.IsNullOrEmpty(extension)) {
				relative = relative.Substring(0, relative.Length - extension.Length);
			}
			return string.IsNullOrEmpty(prefix) ? relative : prefix + "/" + relative;
		}

		private static DestinationGroup ResolveDestination(
			GroupSyncRuleState rule,
			IReadOnlyDictionary<string, GroupSyncGroupState> groupsByGuid,
			IReadOnlyDictionary<string, GroupSyncGroupState> groupsByName) {
			if (!string.IsNullOrEmpty(rule.DestinationGroupGuid) &&
			    groupsByGuid.TryGetValue(rule.DestinationGroupGuid, out var byGuid)) {
				return new DestinationGroup(byGuid.Guid, byGuid.Name, byGuid);
			}
			if (!string.IsNullOrWhiteSpace(rule.DestinationGroupName) &&
			    groupsByName.TryGetValue(rule.DestinationGroupName, out var byName)) {
				return new DestinationGroup(byName.Guid, byName.Name, byName);
			}
			return string.IsNullOrWhiteSpace(rule.DestinationGroupName)
				? null
				: new DestinationGroup(string.Empty, rule.DestinationGroupName, null);
		}

		private static void DetectFolderEntryConflicts(
			IEnumerable<GroupSyncEntryState> entries,
			IEnumerable<DesiredEntry> desiredEntries,
			ICollection<AutomationDiagnostic> diagnostics) {
			var folders = entries.Where(entry => entry.IsFolder)
				.OrderBy(entry => entry.Path, StringComparer.Ordinal)
				.ToArray();
			foreach (var folder in folders) {
				var target = desiredEntries.FirstOrDefault(item =>
					item.Path.StartsWith(folder.Path.TrimEnd('/') + "/", StringComparison.Ordinal));
				if (target == null) {
					continue;
				}
				diagnostics.Add(Error(
					AutomationDiagnosticCode.FolderEntryConflict,
					folder.Path,
					$"Addressable folder entry '{folder.Path}' implicitly owns '{target.Path}'. Remove or narrow the folder entry before Apply."));
			}
		}

		private static void DetectAddressCollisions(
			IEnumerable<GroupSyncEntryState> entries,
			IReadOnlyDictionary<string, DesiredEntry> claims,
			ICollection<AutomationDiagnostic> diagnostics) {
			var owners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
			foreach (var desired in claims.Values) {
				AddOwner(owners, desired.Address, desired.Guid);
			}
			foreach (var entry in entries.Where(item => !item.IsFolder && !claims.ContainsKey(item.Guid))) {
				AddOwner(owners, entry.Address, entry.Guid);
			}
			foreach (var collision in owners.Where(item => item.Value.Distinct(StringComparer.Ordinal).Count() > 1)
			         .OrderBy(item => item.Key, StringComparer.Ordinal)) {
				diagnostics.Add(Error(
					AutomationDiagnosticCode.AddressCollision,
					collision.Key,
					$"Address '{collision.Key}' would be owned by multiple assets: {string.Join(", ", collision.Value.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))}."));
			}
		}

		private static void AddOwner(
			IDictionary<string, List<string>> owners,
			string address,
			string guid) {
			address = address ?? string.Empty;
			if (string.IsNullOrEmpty(address)) {
				return;
			}
			if (!owners.TryGetValue(address, out var list)) {
				list = new List<string>();
				owners.Add(address, list);
			}
			list.Add(guid ?? string.Empty);
		}

		private static AutomationPlan Build(
			string sourceHash,
			IEnumerable<AutomationOperation> operations,
			IEnumerable<AutomationDiagnostic> diagnostics,
			AddressablesAutomationConfig config,
			string configGuid) {
			var sortedOperations = operations.OrderBy(OperationRank)
				.ThenBy(item => item.AssetGuid, StringComparer.Ordinal)
				.ThenBy(item => item.AssetPath, StringComparer.Ordinal)
				.ThenBy(item => item.GroupName, StringComparer.Ordinal)
				.ThenBy(item => item.Value, StringComparer.Ordinal)
				.ToArray();
			var sortedDiagnostics = diagnostics
				.OrderByDescending(item => item.Severity)
				.ThenBy(item => item.Location, StringComparer.Ordinal)
				.ThenBy(item => item.Code)
				.ThenBy(item => item.Message, StringComparer.Ordinal)
				.ToArray();
			var planText = new StringBuilder(sourceHash);
			foreach (var operation in sortedOperations) {
				planText.Append('|').Append((int)operation.Kind)
					.Append('|').Append(operation.AssetGuid)
					.Append('|').Append(operation.AssetPath)
					.Append('|').Append(operation.GroupGuid)
					.Append('|').Append(operation.GroupName)
					.Append('|').Append(operation.Value);
			}
			return new AutomationPlan(
				AutomationScope.Groups,
				sourceHash,
				AutomationHash.Compute(planText.ToString()),
				sortedOperations,
				sortedDiagnostics,
				config,
				configGuid: configGuid);
		}

		private static int OperationRank(AutomationOperation operation) {
			switch (operation.Kind) {
				case AutomationOperationKind.CreateGroup: return 0;
				case AutomationOperationKind.AddBundledAssetGroupSchema: return 1;
				case AutomationOperationKind.AddContentUpdateGroupSchema: return 2;
				case AutomationOperationKind.CreateLabel: return 3;
				default: return 10;
			}
		}

		private static AutomationDiagnostic Error(
			AutomationDiagnosticCode code,
			string location,
			string message) {
			return new AutomationDiagnostic(code, AutomationDiagnosticSeverity.Error, location, message);
		}

		private sealed class DestinationGroup {
			internal DestinationGroup(string guid, string name, GroupSyncGroupState existing) {
				Guid = guid ?? string.Empty;
				Name = name ?? string.Empty;
				Existing = existing;
			}

			internal string Guid { get; }
			internal string Name { get; }
			internal GroupSyncGroupState Existing { get; }
			internal string Key => string.IsNullOrEmpty(Guid) ? "name:" + Name : "guid:" + Guid;
		}

		private sealed class DesiredEntry {
			internal DesiredEntry(
				int ruleIndex,
				string guid,
				string path,
				DestinationGroup destination,
				string address,
				HashSet<string> desiredLabels,
				GroupSyncEntryState existing) {
				RuleIndex = ruleIndex;
				Guid = guid;
				Path = path;
				Destination = destination;
				Address = address;
				DesiredLabels = desiredLabels;
				Existing = existing;
			}

			internal int RuleIndex { get; }
			internal string Guid { get; }
			internal string Path { get; }
			internal DestinationGroup Destination { get; }
			internal string Address { get; }
			internal HashSet<string> DesiredLabels { get; }
			internal GroupSyncEntryState Existing { get; }

			internal bool HasSameOutcome(DesiredEntry other) {
				return string.Equals(Destination.Key, other.Destination.Key, StringComparison.Ordinal) &&
				       string.Equals(Address, other.Address, StringComparison.Ordinal) &&
				       DesiredLabels.SetEquals(other.DesiredLabels);
			}
		}
	}

	internal static class AutomationHash {
		internal static string Compute(string value) {
			using (var algorithm = SHA256.Create()) {
				var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
				var builder = new StringBuilder(bytes.Length * 2);
				foreach (var item in bytes) {
					builder.Append(item.ToString("x2"));
				}
				return builder.ToString();
			}
		}
	}
}
