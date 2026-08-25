using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace TorProduction.Addressables.Editor {
	/// <summary>Identifies the independently analyzable Addressables automation areas.</summary>
	[Flags]
	public enum AutomationScope {
		/// <summary>No automation scope.</summary>
		None = 0,
		/// <summary>Addressables group-entry synchronization.</summary>
		Groups = 1 << 0,
		/// <summary>Addressable and local Build Settings scene synchronization.</summary>
		Scenes = 1 << 1,
		/// <summary>Duplicate implicit dependency analysis.</summary>
		Dependencies = 1 << 2,
		/// <summary>All supported automation scopes.</summary>
		All = Groups | Scenes | Dependencies
	}

	/// <summary>Controls how a group rule derives or retains entry addresses.</summary>
	public enum GroupAddressPolicy {
		/// <summary>Generates an address from the asset path relative to the source folder.</summary>
		RelativePath = 0,
		/// <summary>Retains an existing non-empty address and generates one only for a new entry.</summary>
		PreserveExisting = 1
	}

	/// <summary>Controls how synchronization treats labels not owned by a group rule.</summary>
	public enum ExistingLabelPolicy {
		/// <summary>Preserves unrelated labels while enforcing required labels.</summary>
		PreserveUnrelated = 0,
		/// <summary>Removes labels that are not required by the rule.</summary>
		Exact = 1
	}

	/// <summary>Controls how a scene rule derives or retains Addressables addresses.</summary>
	public enum SceneAddressPolicy {
		/// <summary>Generates an address from the scene path relative to the source folder.</summary>
		RelativePath = 0,
		/// <summary>Retains the last package-managed address across scene moves and renames.</summary>
		PreserveManagedAddress = 1
	}

	/// <summary>Defines how scenes claimed by a scene-folder rule are managed.</summary>
	public enum SceneFolderMode {
		/// <summary>No supported scene-management mode has been selected.</summary>
		Unspecified = 0,
		/// <summary>Manages matching scenes as explicit Addressables entries.</summary>
		Addressable = 1,
		/// <summary>Manages matching scenes in Unity Build Settings.</summary>
		LocalBuildSettings = 2
	}

	/// <summary>Defines one deterministic folder-to-Addressables-group synchronization rule.</summary>
	[Serializable]
	public sealed class GroupSyncRule {
		[SerializeField] private string m_sourceFolderGuid = string.Empty;
		[SerializeField] private string[] m_excludedNestedFolderGuids = Array.Empty<string>();
		[SerializeField] private string m_destinationGroupGuid = string.Empty;
		[SerializeField] private string m_destinationGroupName = string.Empty;
		[SerializeField] private string m_addressPrefix = string.Empty;
		[SerializeField] private GroupAddressPolicy m_addressPolicy = GroupAddressPolicy.RelativePath;
		[SerializeField] private ExistingLabelPolicy m_labelPolicy = ExistingLabelPolicy.PreserveUnrelated;
		[SerializeField] private string[] m_requiredLabels = Array.Empty<string>();
		[SerializeField] private string[] m_assemblyQualifiedTypeFilters = Array.Empty<string>();

		/// <summary>Gets the stable GUID of the source folder.</summary>
		public string SourceFolderGuid => m_sourceFolderGuid;
		/// <summary>Gets nested folder GUIDs explicitly excluded from this rule.</summary>
		public IReadOnlyList<string> ExcludedNestedFolderGuids =>
			m_excludedNestedFolderGuids ?? Array.Empty<string>();
		/// <summary>Gets the persistent destination-group asset GUID.</summary>
		public string DestinationGroupGuid => m_destinationGroupGuid;
		/// <summary>Gets the destination-group display-name fallback.</summary>
		public string DestinationGroupName => m_destinationGroupName;
		/// <summary>Gets the optional normalized prefix added to generated addresses.</summary>
		public string AddressPrefix => m_addressPrefix;
		/// <summary>Gets the address convergence policy.</summary>
		public GroupAddressPolicy AddressPolicy => m_addressPolicy;
		/// <summary>Gets the policy for labels not owned by this rule.</summary>
		public ExistingLabelPolicy LabelPolicy => m_labelPolicy;
		/// <summary>Gets the labels every matching entry must contain.</summary>
		public IReadOnlyList<string> RequiredLabels => m_requiredLabels ?? Array.Empty<string>();
		/// <summary>Gets assembly-qualified type names used to filter matching assets.</summary>
		public IReadOnlyList<string> AssemblyQualifiedTypeFilters =>
			m_assemblyQualifiedTypeFilters ?? Array.Empty<string>();

		internal string[] SerializedExcludedNestedFolderGuids => m_excludedNestedFolderGuids;
		internal string[] SerializedRequiredLabels => m_requiredLabels;
		internal string[] SerializedTypeFilters => m_assemblyQualifiedTypeFilters;

		/// <summary>Creates an empty serialized rule for configuration in the Unity Inspector.</summary>
		public GroupSyncRule() { }

		internal GroupSyncRule(
			string sourceFolderGuid,
			string[] excludedNestedFolderGuids,
			string destinationGroupGuid,
			string destinationGroupName,
			string addressPrefix,
			GroupAddressPolicy addressPolicy,
			ExistingLabelPolicy labelPolicy,
			string[] requiredLabels,
			string[] assemblyQualifiedTypeFilters) {
			m_sourceFolderGuid = sourceFolderGuid ?? string.Empty;
			m_excludedNestedFolderGuids = excludedNestedFolderGuids;
			m_destinationGroupGuid = destinationGroupGuid ?? string.Empty;
			m_destinationGroupName = destinationGroupName ?? string.Empty;
			m_addressPrefix = addressPrefix ?? string.Empty;
			m_addressPolicy = addressPolicy;
			m_labelPolicy = labelPolicy;
			m_requiredLabels = requiredLabels;
			m_assemblyQualifiedTypeFilters = assemblyQualifiedTypeFilters;
		}
	}

	/// <summary>Defines how scenes below one source folder are synchronized.</summary>
	[Serializable]
	public sealed class SceneFolderRule {
		[SerializeField] private string m_sourceFolderGuid = string.Empty;
		[SerializeField] private string[] m_excludedNestedFolderGuids = Array.Empty<string>();
		[SerializeField] private SceneFolderMode m_mode;
		[SerializeField] private string m_destinationGroupGuid = string.Empty;
		[SerializeField] private string m_destinationGroupName = string.Empty;
		[SerializeField] private string m_category = string.Empty;
		[SerializeField] private string m_addressPrefix = string.Empty;
		[SerializeField] private SceneAddressPolicy m_addressPolicy = SceneAddressPolicy.RelativePath;
		[SerializeField] private string[] m_requiredLabels = Array.Empty<string>();

		/// <summary>Gets the stable GUID of the source scene folder.</summary>
		public string SourceFolderGuid => m_sourceFolderGuid;
		/// <summary>Gets nested folder GUIDs explicitly excluded from this rule.</summary>
		public IReadOnlyList<string> ExcludedNestedFolderGuids =>
			m_excludedNestedFolderGuids ?? Array.Empty<string>();
		/// <summary>Gets whether scenes are Addressable or managed in Build Settings.</summary>
		public SceneFolderMode Mode => m_mode;
		/// <summary>Gets the persistent destination-group GUID for Addressable scenes.</summary>
		public string DestinationGroupGuid => m_destinationGroupGuid;
		/// <summary>Gets the destination-group display-name fallback.</summary>
		public string DestinationGroupName => m_destinationGroupName;
		/// <summary>Gets the optional category label assigned to matching scenes.</summary>
		public string Category => m_category;
		/// <summary>Gets the optional normalized prefix for generated scene addresses.</summary>
		public string AddressPrefix => m_addressPrefix;
		/// <summary>Gets the scene address convergence policy.</summary>
		public SceneAddressPolicy AddressPolicy => m_addressPolicy;
		/// <summary>Gets additional labels assigned to matching scenes.</summary>
		public IReadOnlyList<string> RequiredLabels => m_requiredLabels ?? Array.Empty<string>();

		internal string[] SerializedExcludedNestedFolderGuids => m_excludedNestedFolderGuids;
		internal string[] SerializedRequiredLabels => m_requiredLabels;

		/// <summary>Creates an empty serialized rule for configuration in the Unity Inspector.</summary>
		public SceneFolderRule() { }

		internal SceneFolderRule(
			string sourceFolderGuid,
			string[] excludedNestedFolderGuids,
			SceneFolderMode mode,
			string destinationGroupGuid,
			string destinationGroupName,
			string category,
			string addressPrefix,
			SceneAddressPolicy addressPolicy,
			string[] requiredLabels) {
			m_sourceFolderGuid = sourceFolderGuid ?? string.Empty;
			m_excludedNestedFolderGuids = excludedNestedFolderGuids;
			m_mode = mode;
			m_destinationGroupGuid = destinationGroupGuid ?? string.Empty;
			m_destinationGroupName = destinationGroupName ?? string.Empty;
			m_category = category ?? string.Empty;
			m_addressPrefix = addressPrefix ?? string.Empty;
			m_addressPolicy = addressPolicy;
			m_requiredLabels = requiredLabels;
		}
	}

	/// <summary>Configures the destination group used by duplicate-dependency fixes.</summary>
	[Serializable]
	public sealed class DependencyAnalysisSettings {
		/// <summary>The default display name for the duplicate-dependency isolation group.</summary>
		public const string DefaultDestinationGroupName = "Duplicate Asset Isolation";

		[SerializeField] private string m_destinationGroupGuid = string.Empty;
		[SerializeField] private string m_destinationGroupName = DefaultDestinationGroupName;

		/// <summary>Gets the persistent destination-group asset GUID.</summary>
		public string DestinationGroupGuid => m_destinationGroupGuid;
		/// <summary>Gets the destination-group display-name fallback.</summary>
		public string DestinationGroupName => m_destinationGroupName;

		/// <summary>Creates settings using the default destination-group name.</summary>
		public DependencyAnalysisSettings() { }

		internal DependencyAnalysisSettings(string destinationGroupGuid, string destinationGroupName) {
			m_destinationGroupGuid = destinationGroupGuid ?? string.Empty;
			m_destinationGroupName = destinationGroupName ?? string.Empty;
		}
	}

	/// <summary>Stores the last package-managed identity and state of one scene.</summary>
	[Serializable]
	public sealed class ManagedSceneRecord {
		[SerializeField] private string m_sceneGuid = string.Empty;
		[SerializeField] private string m_lastKnownPath = string.Empty;
		[SerializeField] private SceneFolderMode m_mode;
		[SerializeField] private string m_managedAddress = string.Empty;
		[SerializeField] private string m_destinationGroupGuid = string.Empty;
		[SerializeField] private string m_destinationGroupName = string.Empty;
		[SerializeField] private string[] m_managedLabels = Array.Empty<string>();

		/// <summary>Gets the stable Unity GUID of the managed scene.</summary>
		public string SceneGuid => m_sceneGuid;
		/// <summary>Gets the scene path observed during the last successful Apply.</summary>
		public string LastKnownPath => m_lastKnownPath;
		/// <summary>Gets the scene-management mode used during the last successful Apply.</summary>
		public SceneFolderMode Mode => m_mode;
		/// <summary>Gets the package-managed Addressables address.</summary>
		public string ManagedAddress => m_managedAddress;
		/// <summary>Gets the persistent destination-group GUID.</summary>
		public string DestinationGroupGuid => m_destinationGroupGuid;
		/// <summary>Gets the destination-group display name.</summary>
		public string DestinationGroupName => m_destinationGroupName;
		/// <summary>Gets the labels last managed by the scene rule.</summary>
		public IReadOnlyList<string> ManagedLabels => m_managedLabels ?? Array.Empty<string>();

		internal ManagedSceneRecord(
			string sceneGuid,
			string lastKnownPath,
			SceneFolderMode mode,
			string managedAddress,
			string destinationGroupGuid,
			string destinationGroupName,
			string[] managedLabels) {
			m_sceneGuid = sceneGuid ?? string.Empty;
			m_lastKnownPath = lastKnownPath ?? string.Empty;
			m_mode = mode;
			m_managedAddress = managedAddress ?? string.Empty;
			m_destinationGroupGuid = destinationGroupGuid ?? string.Empty;
			m_destinationGroupName = destinationGroupName ?? string.Empty;
			m_managedLabels = managedLabels ?? Array.Empty<string>();
		}
	}

	/// <summary>Stores versioned, project-owned rules for Addressables editor automation.</summary>
	[CreateAssetMenu(
		fileName = "AddressablesAutomationConfig",
		menuName = "Tor Production/Addressables Automation Config",
		order = 30)]
	[MovedFrom(true, sourceAssembly: "TorProduction.AddressablesService.Editor")]
	public sealed class AddressablesAutomationConfig : ScriptableObject {
		/// <summary>The configuration schema understood by this package version.</summary>
		public const int CurrentSchemaVersion = 3;

		[SerializeField] private int m_schemaVersion = CurrentSchemaVersion;
		[SerializeField] private GroupSyncRule[] m_groupRules = Array.Empty<GroupSyncRule>();
		[SerializeField] private SceneFolderRule[] m_sceneRules = Array.Empty<SceneFolderRule>();
		[SerializeField] private ManagedSceneRecord[] m_managedScenes = Array.Empty<ManagedSceneRecord>();
		[SerializeField] private DependencyAnalysisSettings m_dependencySettings = new DependencyAnalysisSettings();

		/// <summary>Gets the serialized configuration schema version.</summary>
		public int SchemaVersion => m_schemaVersion;
		/// <summary>Gets the ordered group synchronization rules.</summary>
		public IReadOnlyList<GroupSyncRule> GroupRules => m_groupRules ?? Array.Empty<GroupSyncRule>();
		/// <summary>Gets the ordered scene synchronization rules.</summary>
		public IReadOnlyList<SceneFolderRule> SceneRules => m_sceneRules ?? Array.Empty<SceneFolderRule>();
		/// <summary>Gets the scene identities recorded by successful synchronization.</summary>
		public IReadOnlyList<ManagedSceneRecord> ManagedScenes => m_managedScenes ?? Array.Empty<ManagedSceneRecord>();
		/// <summary>Gets duplicate-dependency analysis and fix settings.</summary>
		public DependencyAnalysisSettings DependencySettings => m_dependencySettings;

		internal GroupSyncRule[] SerializedGroupRules => m_groupRules;
		internal SceneFolderRule[] SerializedSceneRules => m_sceneRules;
		internal ManagedSceneRecord[] SerializedManagedScenes => m_managedScenes;
		internal DependencyAnalysisSettings SerializedDependencySettings => m_dependencySettings;

		internal void ReplaceWithCurrentSchema(
			GroupSyncRule[] groupRules,
			SceneFolderRule[] sceneRules,
			ManagedSceneRecord[] managedScenes = null,
			DependencyAnalysisSettings dependencySettings = null) {
			m_schemaVersion = CurrentSchemaVersion;
			m_groupRules = groupRules ?? Array.Empty<GroupSyncRule>();
			m_sceneRules = sceneRules ?? Array.Empty<SceneFolderRule>();
			m_managedScenes = managedScenes ?? Array.Empty<ManagedSceneRecord>();
			m_dependencySettings = dependencySettings ?? new DependencyAnalysisSettings();
		}

		internal void ReplaceManagedScenes(ManagedSceneRecord[] managedScenes) {
			m_managedScenes = managedScenes ?? Array.Empty<ManagedSceneRecord>();
		}

		internal bool TryMigrateToCurrentSchema(out string error) {
			if (m_schemaVersion == CurrentSchemaVersion) {
				error = "Configuration already uses the current schema.";
				return false;
			}

			if (m_schemaVersion != 0 && m_schemaVersion != 1 && m_schemaVersion != 2) {
				error = $"Configuration schema {m_schemaVersion} has no supported migration to {CurrentSchemaVersion}.";
				return false;
			}

			m_groupRules = m_groupRules ?? Array.Empty<GroupSyncRule>();
			m_sceneRules = m_sceneRules ?? Array.Empty<SceneFolderRule>();
			m_managedScenes = m_managedScenes ?? Array.Empty<ManagedSceneRecord>();
			m_dependencySettings = m_dependencySettings ?? new DependencyAnalysisSettings();
			m_schemaVersion = CurrentSchemaVersion;
			error = string.Empty;
			return true;
		}
	}
}
