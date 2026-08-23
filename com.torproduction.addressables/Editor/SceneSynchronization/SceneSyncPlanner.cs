using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TorProduction.Addressables.Editor {
	internal sealed class SceneSyncProjectState {
		internal bool SettingsExist;
		internal string SettingsIdentity = string.Empty;
		internal string ConfigGuid = string.Empty;
		internal string ConfigJson = string.Empty;
		internal readonly List<SceneSyncRuleState> Rules = new List<SceneSyncRuleState>();
		internal readonly List<SceneSyncGroupState> Groups = new List<SceneSyncGroupState>();
		internal readonly List<SceneSyncEntryState> Entries = new List<SceneSyncEntryState>();
		internal readonly List<SceneBuildState> BuildScenes = new List<SceneBuildState>();
		internal readonly List<ManagedSceneRecord> ManagedScenes = new List<ManagedSceneRecord>();
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
				Append(builder, ((int)rule.Mode).ToString());
				Append(builder, rule.DestinationGroupGuid);
				Append(builder, rule.DestinationGroupName);
				Append(builder, rule.Category);
				Append(builder, rule.AddressPrefix);
				Append(builder, ((int)rule.AddressPolicy).ToString());
				foreach (var label in rule.RequiredLabels.OrderBy(item => item, StringComparer.Ordinal)) Append(builder, label);
				foreach (var scene in rule.Scenes.OrderBy(item => item.Guid, StringComparer.Ordinal)) {
					Append(builder, scene.Guid);
					Append(builder, scene.Path);
				}
			}
			foreach (var group in Groups.OrderBy(item => item.Guid, StringComparer.Ordinal).ThenBy(item => item.Name, StringComparer.Ordinal)) {
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
				foreach (var label in entry.Labels.OrderBy(item => item, StringComparer.Ordinal)) Append(builder, label);
			}
			foreach (var scene in BuildScenes) {
				Append(builder, scene.Guid);
				Append(builder, scene.Path);
				Append(builder, scene.Enabled ? "1" : "0");
			}
			foreach (var label in Labels.OrderBy(item => item, StringComparer.Ordinal)) Append(builder, label);
			foreach (var diagnostic in Diagnostics.OrderBy(item => item.Location, StringComparer.Ordinal).ThenBy(item => item.Code)) {
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

	internal sealed class SceneSyncRuleState {
		internal int Index;
		internal string SourceFolderPath = string.Empty;
		internal SceneFolderMode Mode;
		internal string DestinationGroupGuid = string.Empty;
		internal string DestinationGroupName = string.Empty;
		internal string Category = string.Empty;
		internal string AddressPrefix = string.Empty;
		internal SceneAddressPolicy AddressPolicy;
		internal string[] RequiredLabels = Array.Empty<string>();
		internal readonly List<SceneAssetState> Scenes = new List<SceneAssetState>();
	}

	internal sealed class SceneAssetState {
		internal string Guid = string.Empty;
		internal string Path = string.Empty;
	}

	internal sealed class SceneSyncGroupState {
		internal string Guid = string.Empty;
		internal string Name = string.Empty;
		internal bool ReadOnly;
		internal bool HasBundledSchema;
		internal bool HasContentUpdateSchema;
		internal bool IsBuildable = true;
	}

	internal sealed class SceneSyncEntryState {
		internal string Guid = string.Empty;
		internal string Path = string.Empty;
		internal string GroupGuid = string.Empty;
		internal string GroupName = string.Empty;
		internal string Address = string.Empty;
		internal string[] Labels = Array.Empty<string>();
	}

	[Serializable]
	internal sealed class SceneBuildState {
		public string guid = string.Empty;
		public string path = string.Empty;
		public bool enabled = true;

		internal string Guid { get => guid; set => guid = value ?? string.Empty; }
		internal string Path { get => path; set => path = value ?? string.Empty; }
		internal bool Enabled { get => enabled; set => enabled = value; }
	}

	internal sealed class SceneSyncMutation {
		internal SceneSyncMutation(
			IEnumerable<SceneBuildState> buildScenes,
			IEnumerable<ManagedSceneRecord> managedScenes) {
			BuildScenes = (buildScenes ?? Array.Empty<SceneBuildState>()).ToArray();
			ManagedScenes = (managedScenes ?? Array.Empty<ManagedSceneRecord>()).ToArray();
		}

		internal SceneBuildState[] BuildScenes { get; }
		internal ManagedSceneRecord[] ManagedScenes { get; }
	}

	internal static class SceneSyncPlanner {
		internal static AutomationPlan Create(SceneSyncProjectState state, AddressablesAutomationConfig config = null) {
			if (state == null) throw new ArgumentNullException(nameof(state));
			var sourceHash = state.ComputeHash();
			var diagnostics = new List<AutomationDiagnostic>(state.Diagnostics);
			var operations = new List<AutomationOperation>();
			if (!state.SettingsExist) {
				diagnostics.Add(Error(AutomationDiagnosticCode.AddressablesSettingsMissing, "Addressables", "Addressables settings do not exist. Analysis did not create them."));
				return Build(sourceHash, operations, diagnostics, config, new SceneSyncMutation(state.BuildScenes, state.ManagedScenes), state.ConfigGuid);
			}

			var groupsByGuid = state.Groups.Where(item => !string.IsNullOrEmpty(item.Guid)).GroupBy(item => item.Guid, StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.First(), StringComparer.Ordinal);
			var groupsByName = state.Groups.Where(item => !string.IsNullOrEmpty(item.Name)).GroupBy(item => item.Name, StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.First(), StringComparer.Ordinal);
			var entries = state.Entries.Where(item => !string.IsNullOrEmpty(item.Guid)).GroupBy(item => item.Guid, StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.First(), StringComparer.Ordinal);
			var records = state.ManagedScenes.Where(item => item != null && !string.IsNullOrEmpty(item.SceneGuid)).GroupBy(item => item.SceneGuid, StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.First(), StringComparer.Ordinal);
			var desired = new Dictionary<string, DesiredScene>(StringComparer.Ordinal);
			var destinations = new Dictionary<string, Destination>(StringComparer.Ordinal);

			foreach (var rule in state.Rules.OrderBy(item => item.Index)) {
				Destination destination = null;
				if (rule.Mode == SceneFolderMode.Addressable) {
					destination = ResolveDestination(rule, groupsByGuid, groupsByName);
					if (destination == null) {
						diagnostics.Add(Error(AutomationDiagnosticCode.DestinationGroupMissing, $"Scenes[{rule.Index}]", "The destination group has neither a resolvable GUID nor a non-empty fallback name."));
						continue;
					}
					destinations[destination.Key] = destination;
					if (destination.Existing?.ReadOnly == true) diagnostics.Add(Error(AutomationDiagnosticCode.DestinationGroupReadOnly, $"Scenes[{rule.Index}]", $"Destination group '{destination.Name}' is read-only."));
					if (destination.Existing != null && destination.Existing.HasBundledSchema && !destination.Existing.IsBuildable) diagnostics.Add(Error(AutomationDiagnosticCode.DestinationGroupNonBuildable, $"Scenes[{rule.Index}]", $"Destination group '{destination.Name}' is not buildable."));
				}

				foreach (var scene in rule.Scenes.OrderBy(item => item.Guid, StringComparer.Ordinal).ThenBy(item => item.Path, StringComparer.Ordinal)) {
					entries.TryGetValue(scene.Guid, out var entry);
					records.TryGetValue(scene.Guid, out var record);
					var candidate = BuildDesired(rule, scene, destination, entry, record);
					if (desired.TryGetValue(scene.Guid, out var other)) {
						if (!other.HasSameOutcome(candidate)) diagnostics.Add(Error(AutomationDiagnosticCode.SceneClaimConflict, scene.Path, $"The scene is claimed incompatibly by Scenes[{other.RuleIndex}] and Scenes[{rule.Index}]."));
						continue;
					}
					if (rule.Mode == SceneFolderMode.LocalBuildSettings && entry != null && (record == null || record.Mode != SceneFolderMode.Addressable)) {
						diagnostics.Add(Error(AutomationDiagnosticCode.SceneClaimConflict, scene.Path, "The local scene is already an unrelated Addressables entry. It was left unchanged."));
					}
					desired.Add(scene.Guid, candidate);
				}
			}

			DetectAddressCollisions(entries.Values, desired, diagnostics);
			if (diagnostics.Any(item => item.Severity == AutomationDiagnosticSeverity.Error)) {
				return Build(sourceHash, operations, diagnostics, config, new SceneSyncMutation(state.BuildScenes, state.ManagedScenes), state.ConfigGuid);
			}

			foreach (var destination in destinations.Values.OrderBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.Guid, StringComparer.Ordinal)) {
				if (destination.Existing == null) operations.Add(new AutomationOperation(AutomationOperationKind.CreateGroup, groupName: destination.Name));
				if (destination.Existing == null || !destination.Existing.HasBundledSchema) operations.Add(new AutomationOperation(AutomationOperationKind.AddBundledAssetGroupSchema, groupGuid: destination.Guid, groupName: destination.Name));
				if (destination.Existing == null || !destination.Existing.HasContentUpdateSchema) operations.Add(new AutomationOperation(AutomationOperationKind.AddContentUpdateGroupSchema, groupGuid: destination.Guid, groupName: destination.Name));
			}

			foreach (var label in desired.Values.SelectMany(item => item.ManagedLabels).Distinct(StringComparer.Ordinal).Where(item => !state.Labels.Contains(item)).OrderBy(item => item, StringComparer.Ordinal)) {
				operations.Add(new AutomationOperation(AutomationOperationKind.CreateLabel, value: label));
			}
			foreach (var item in desired.Values.OrderBy(item => item.RuleIndex).ThenBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Guid, StringComparer.Ordinal)) {
				if (item.Mode == SceneFolderMode.Addressable) {
					if (item.Entry == null) operations.Add(EntryOperation(AutomationOperationKind.CreateEntry, item));
					else if (!DestinationMatches(item.Entry, item.Destination)) operations.Add(EntryOperation(AutomationOperationKind.MoveEntry, item));
					if (item.Entry == null || !string.Equals(item.Entry.Address, item.Address, StringComparison.Ordinal)) operations.Add(new AutomationOperation(AutomationOperationKind.SetAddress, item.Guid, item.Path, item.Destination.Guid, item.Destination.Name, item.Address));
					var currentLabels = new HashSet<string>(item.Entry?.Labels ?? Array.Empty<string>(), StringComparer.Ordinal);
					var formerManaged = new HashSet<string>(item.Record?.ManagedLabels ?? Array.Empty<string>(), StringComparer.Ordinal);
					foreach (var label in formerManaged.Where(label => !item.ManagedLabels.Contains(label) && currentLabels.Contains(label)).OrderBy(label => label, StringComparer.Ordinal)) operations.Add(new AutomationOperation(AutomationOperationKind.RemoveLabel, item.Guid, item.Path, item.Destination.Guid, item.Destination.Name, label));
					foreach (var label in item.ManagedLabels.Where(label => !currentLabels.Contains(label)).OrderBy(label => label, StringComparer.Ordinal)) operations.Add(new AutomationOperation(AutomationOperationKind.AddLabel, item.Guid, item.Path, item.Destination.Guid, item.Destination.Name, label));
				} else if (item.Record?.Mode == SceneFolderMode.Addressable && item.Entry != null) {
					operations.Add(new AutomationOperation(AutomationOperationKind.RemoveEntry, item.Guid, item.Path));
				}
			}
			foreach (var record in records.Values.Where(item => !desired.ContainsKey(item.SceneGuid)).OrderBy(item => item.SceneGuid, StringComparer.Ordinal)) {
				if (record.Mode == SceneFolderMode.Addressable && entries.ContainsKey(record.SceneGuid)) operations.Add(new AutomationOperation(AutomationOperationKind.RemoveEntry, record.SceneGuid, record.LastKnownPath));
			}

			var desiredBuild = BuildDesiredBuildSettings(state, desired.Values);
			if (!BuildSettingsEqual(state.BuildScenes, desiredBuild)) operations.Add(new AutomationOperation(AutomationOperationKind.UpdateBuildSettings));
			var desiredRecords = desired.Values.OrderBy(item => item.Guid, StringComparer.Ordinal).Select(item => new ManagedSceneRecord(item.Guid, item.Path, item.Mode, item.Address, item.Destination?.Guid, item.Destination?.Name, item.ManagedLabels.OrderBy(label => label, StringComparer.Ordinal).ToArray())).ToArray();
			if (!ManagedRecordsEqual(state.ManagedScenes, desiredRecords)) operations.Add(new AutomationOperation(AutomationOperationKind.UpdateManagedScenes));

			return Build(sourceHash, operations, diagnostics, config, new SceneSyncMutation(desiredBuild, desiredRecords), state.ConfigGuid);
		}

		private static DesiredScene BuildDesired(SceneSyncRuleState rule, SceneAssetState scene, Destination destination, SceneSyncEntryState entry, ManagedSceneRecord record) {
			var labels = new HashSet<string>(rule.RequiredLabels ?? Array.Empty<string>(), StringComparer.Ordinal);
			if (!string.IsNullOrWhiteSpace(rule.Category)) labels.Add(rule.Category);
			var address = string.Empty;
			if (rule.Mode == SceneFolderMode.Addressable) {
				address = rule.AddressPolicy == SceneAddressPolicy.PreserveManagedAddress
					? !string.IsNullOrEmpty(record?.ManagedAddress) ? record.ManagedAddress : !string.IsNullOrEmpty(entry?.Address) ? entry.Address : GroupSyncPlanner.GenerateAddress(rule.SourceFolderPath, scene.Path, rule.AddressPrefix)
					: GroupSyncPlanner.GenerateAddress(rule.SourceFolderPath, scene.Path, rule.AddressPrefix);
			}
			return new DesiredScene(rule.Index, scene.Guid, scene.Path, rule.Mode, destination, address, labels, entry, record);
		}

		private static SceneBuildState[] BuildDesiredBuildSettings(SceneSyncProjectState state, IEnumerable<DesiredScene> desired) {
			var desiredArray = desired.ToArray();
			var formerLocal = state.ManagedScenes.Where(item => item != null && item.Mode == SceneFolderMode.LocalBuildSettings).ToArray();
			var formerlyManaged = new HashSet<string>(formerLocal.Select(item => item.SceneGuid), StringComparer.Ordinal);
			var formerlyManagedPaths = new HashSet<string>(formerLocal.Select(item => item.LastKnownPath), StringComparer.Ordinal);
			var claimed = new HashSet<string>(desiredArray.Select(item => item.Guid), StringComparer.Ordinal);
			var result = state.BuildScenes.Where(item =>
				!formerlyManaged.Contains(item.Guid) &&
				!formerlyManagedPaths.Contains(item.Path) &&
				!claimed.Contains(item.Guid)).Select(Clone).ToList();
			result.AddRange(desiredArray.Where(item => item.Mode == SceneFolderMode.LocalBuildSettings).OrderBy(item => item.RuleIndex).ThenBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Guid, StringComparer.Ordinal).Select(item => new SceneBuildState { Guid = item.Guid, Path = item.Path, Enabled = true }));
			return result.ToArray();
		}

		private static SceneBuildState Clone(SceneBuildState item) => new SceneBuildState { Guid = item.Guid, Path = item.Path, Enabled = item.Enabled };

		private static bool BuildSettingsEqual(IReadOnlyList<SceneBuildState> left, IReadOnlyList<SceneBuildState> right) {
			return left.Count == right.Count && left.Select((item, index) => string.Equals(item.Guid, right[index].Guid, StringComparison.Ordinal) && string.Equals(item.Path, right[index].Path, StringComparison.Ordinal) && item.Enabled == right[index].Enabled).All(item => item);
		}

		private static bool ManagedRecordsEqual(IEnumerable<ManagedSceneRecord> left, IEnumerable<ManagedSceneRecord> right) {
			string Serialize(ManagedSceneRecord item) => item == null ? "<null>" : string.Join("|", item.SceneGuid, item.LastKnownPath, (int)item.Mode, item.ManagedAddress, item.DestinationGroupGuid, item.DestinationGroupName, string.Join(",", item.ManagedLabels.OrderBy(label => label, StringComparer.Ordinal)));
			return left.Select(Serialize).OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(right.Select(Serialize).OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal);
		}

		private static void DetectAddressCollisions(IEnumerable<SceneSyncEntryState> entries, IReadOnlyDictionary<string, DesiredScene> desired, ICollection<AutomationDiagnostic> diagnostics) {
			var owners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
			foreach (var item in desired.Values.Where(item => item.Mode == SceneFolderMode.Addressable)) AddOwner(owners, item.Address, item.Guid);
			foreach (var entry in entries.Where(item => !desired.ContainsKey(item.Guid))) AddOwner(owners, entry.Address, entry.Guid);
			foreach (var collision in owners.Where(item => item.Value.Distinct(StringComparer.Ordinal).Count() > 1).OrderBy(item => item.Key, StringComparer.Ordinal)) diagnostics.Add(Error(AutomationDiagnosticCode.AddressCollision, collision.Key, $"Address '{collision.Key}' would be owned by multiple assets: {string.Join(", ", collision.Value.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))}."));
		}

		private static void AddOwner(IDictionary<string, List<string>> owners, string address, string guid) {
			if (string.IsNullOrEmpty(address)) return;
			if (!owners.TryGetValue(address, out var list)) owners.Add(address, list = new List<string>());
			list.Add(guid ?? string.Empty);
		}

		private static Destination ResolveDestination(SceneSyncRuleState rule, IReadOnlyDictionary<string, SceneSyncGroupState> byGuid, IReadOnlyDictionary<string, SceneSyncGroupState> byName) {
			if (!string.IsNullOrEmpty(rule.DestinationGroupGuid) && byGuid.TryGetValue(rule.DestinationGroupGuid, out var guid)) return new Destination(guid.Guid, guid.Name, guid);
			if (!string.IsNullOrWhiteSpace(rule.DestinationGroupName) && byName.TryGetValue(rule.DestinationGroupName, out var name)) return new Destination(name.Guid, name.Name, name);
			return string.IsNullOrWhiteSpace(rule.DestinationGroupName) ? null : new Destination(string.Empty, rule.DestinationGroupName, null);
		}

		private static bool DestinationMatches(SceneSyncEntryState entry, Destination destination) => !string.IsNullOrEmpty(destination.Guid) ? string.Equals(entry.GroupGuid, destination.Guid, StringComparison.Ordinal) : string.Equals(entry.GroupName, destination.Name, StringComparison.Ordinal);
		private static AutomationOperation EntryOperation(AutomationOperationKind kind, DesiredScene scene) => new AutomationOperation(kind, scene.Guid, scene.Path, scene.Destination.Guid, scene.Destination.Name, labels: scene.ManagedLabels);

		private static AutomationPlan Build(string sourceHash, IEnumerable<AutomationOperation> operations, IEnumerable<AutomationDiagnostic> diagnostics, AddressablesAutomationConfig config, SceneSyncMutation mutation, string configGuid) {
			var sortedOperations = operations.OrderBy(item => Rank(item.Kind)).ThenBy(item => item.AssetGuid, StringComparer.Ordinal).ThenBy(item => item.AssetPath, StringComparer.Ordinal).ThenBy(item => item.GroupName, StringComparer.Ordinal).ThenBy(item => item.Value, StringComparer.Ordinal).ToArray();
			var sortedDiagnostics = diagnostics.OrderByDescending(item => item.Severity).ThenBy(item => item.Location, StringComparer.Ordinal).ThenBy(item => item.Code).ThenBy(item => item.Message, StringComparer.Ordinal).ToArray();
			var builder = new StringBuilder(sourceHash);
			foreach (var operation in sortedOperations) builder.Append('|').Append((int)operation.Kind).Append('|').Append(operation.AssetGuid).Append('|').Append(operation.AssetPath).Append('|').Append(operation.GroupGuid).Append('|').Append(operation.GroupName).Append('|').Append(operation.Value);
			return new AutomationPlan(AutomationScope.Scenes, sourceHash, AutomationHash.Compute(builder.ToString()), sortedOperations, sortedDiagnostics, config, mutation, configGuid);
		}

		private static int Rank(AutomationOperationKind kind) {
			switch (kind) {
				case AutomationOperationKind.CreateGroup: return 0;
				case AutomationOperationKind.AddBundledAssetGroupSchema: return 1;
				case AutomationOperationKind.AddContentUpdateGroupSchema: return 2;
				case AutomationOperationKind.CreateLabel: return 3;
				case AutomationOperationKind.UpdateBuildSettings: return 20;
				case AutomationOperationKind.UpdateManagedScenes: return 30;
				default: return 10;
			}
		}

		private static AutomationDiagnostic Error(AutomationDiagnosticCode code, string location, string message) => new AutomationDiagnostic(code, AutomationDiagnosticSeverity.Error, location, message);

		private sealed class Destination {
			internal Destination(string guid, string name, SceneSyncGroupState existing) { Guid = guid ?? string.Empty; Name = name ?? string.Empty; Existing = existing; }
			internal string Guid { get; }
			internal string Name { get; }
			internal SceneSyncGroupState Existing { get; }
			internal string Key => string.IsNullOrEmpty(Guid) ? "name:" + Name : "guid:" + Guid;
		}

		private sealed class DesiredScene {
			internal DesiredScene(int ruleIndex, string guid, string path, SceneFolderMode mode, Destination destination, string address, HashSet<string> labels, SceneSyncEntryState entry, ManagedSceneRecord record) { RuleIndex = ruleIndex; Guid = guid; Path = path; Mode = mode; Destination = destination; Address = address; ManagedLabels = labels; Entry = entry; Record = record; }
			internal int RuleIndex { get; }
			internal string Guid { get; }
			internal string Path { get; }
			internal SceneFolderMode Mode { get; }
			internal Destination Destination { get; }
			internal string Address { get; }
			internal HashSet<string> ManagedLabels { get; }
			internal SceneSyncEntryState Entry { get; }
			internal ManagedSceneRecord Record { get; }
			internal bool HasSameOutcome(DesiredScene other) => Mode == other.Mode && string.Equals(Destination?.Key, other.Destination?.Key, StringComparison.Ordinal) && string.Equals(Address, other.Address, StringComparison.Ordinal) && ManagedLabels.SetEquals(other.ManagedLabels);
		}
	}
}
