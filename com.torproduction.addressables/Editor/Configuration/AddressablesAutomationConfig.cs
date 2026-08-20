using System;
using System.Collections.Generic;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	[Flags]
	public enum AutomationScope {
		None = 0,
		Groups = 1 << 0,
		Scenes = 1 << 1,
		Dependencies = 1 << 2,
		All = Groups | Scenes | Dependencies
	}

	public enum GroupAddressPolicy {
		RelativePath = 0,
		PreserveExisting = 1
	}

	public enum ExistingLabelPolicy {
		PreserveUnrelated = 0,
		Exact = 1
	}

	public enum SceneAddressPolicy {
		RelativePath = 0,
		PreserveManagedAddress = 1
	}

	public enum SceneFolderMode {
		Unspecified = 0,
		Addressable = 1,
		LocalBuildSettings = 2
	}

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

		public string SourceFolderGuid => m_sourceFolderGuid;
		public IReadOnlyList<string> ExcludedNestedFolderGuids =>
			m_excludedNestedFolderGuids ?? Array.Empty<string>();
		public string DestinationGroupGuid => m_destinationGroupGuid;
		public string DestinationGroupName => m_destinationGroupName;
		public string AddressPrefix => m_addressPrefix;
		public GroupAddressPolicy AddressPolicy => m_addressPolicy;
		public ExistingLabelPolicy LabelPolicy => m_labelPolicy;
		public IReadOnlyList<string> RequiredLabels => m_requiredLabels ?? Array.Empty<string>();
		public IReadOnlyList<string> AssemblyQualifiedTypeFilters =>
			m_assemblyQualifiedTypeFilters ?? Array.Empty<string>();

		internal string[] SerializedExcludedNestedFolderGuids => m_excludedNestedFolderGuids;
		internal string[] SerializedRequiredLabels => m_requiredLabels;
		internal string[] SerializedTypeFilters => m_assemblyQualifiedTypeFilters;

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

		public string SourceFolderGuid => m_sourceFolderGuid;
		public IReadOnlyList<string> ExcludedNestedFolderGuids =>
			m_excludedNestedFolderGuids ?? Array.Empty<string>();
		public SceneFolderMode Mode => m_mode;
		public string DestinationGroupGuid => m_destinationGroupGuid;
		public string DestinationGroupName => m_destinationGroupName;
		public string Category => m_category;
		public string AddressPrefix => m_addressPrefix;
		public SceneAddressPolicy AddressPolicy => m_addressPolicy;
		public IReadOnlyList<string> RequiredLabels => m_requiredLabels ?? Array.Empty<string>();

		internal string[] SerializedExcludedNestedFolderGuids => m_excludedNestedFolderGuids;
		internal string[] SerializedRequiredLabels => m_requiredLabels;

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

	[CreateAssetMenu(
		fileName = "AddressablesAutomationConfig",
		menuName = "Tor Production/Addressables Automation Config",
		order = 30)]
	public sealed class AddressablesAutomationConfig : ScriptableObject {
		public const int CurrentSchemaVersion = 1;

		[SerializeField] private int m_schemaVersion = CurrentSchemaVersion;
		[SerializeField] private GroupSyncRule[] m_groupRules = Array.Empty<GroupSyncRule>();
		[SerializeField] private SceneFolderRule[] m_sceneRules = Array.Empty<SceneFolderRule>();

		public int SchemaVersion => m_schemaVersion;
		public IReadOnlyList<GroupSyncRule> GroupRules => m_groupRules ?? Array.Empty<GroupSyncRule>();
		public IReadOnlyList<SceneFolderRule> SceneRules => m_sceneRules ?? Array.Empty<SceneFolderRule>();

		internal GroupSyncRule[] SerializedGroupRules => m_groupRules;
		internal SceneFolderRule[] SerializedSceneRules => m_sceneRules;

		internal void ReplaceWithCurrentSchema(GroupSyncRule[] groupRules, SceneFolderRule[] sceneRules) {
			m_schemaVersion = CurrentSchemaVersion;
			m_groupRules = groupRules ?? Array.Empty<GroupSyncRule>();
			m_sceneRules = sceneRules ?? Array.Empty<SceneFolderRule>();
		}
	}
}
