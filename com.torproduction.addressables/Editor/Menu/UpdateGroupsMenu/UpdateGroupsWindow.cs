using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal class UpdateGroupsWindow : EditorWindow {
		private DefaultAsset m_folderAsset = default;
		private int m_selectedGroupIndex;
		private string[] m_groupOptions;
		private bool m_filterByType;
		private List<int> m_selectedTypes;
		private List<string[]> m_availableTypeOptions;
		private string[] m_typeOptions;
		private List<string> m_allLabels;
		private List<int> m_selectedLabelsIndices;
		private List<string[]> m_availableLabelOptions;

		public void OnEnable() {
			m_groupOptions = AddressableMenuUtils.GetAllGroupNames();
			m_typeOptions = AddressableMenuUtils.GetTypesOptions();
			m_selectedTypes = new List<int>();
			m_filterByType = false;
			m_selectedGroupIndex = 1;
			m_folderAsset = default;
			m_selectedLabelsIndices = new List<int>();
		}

		public static void ShowWindow() {
			GetWindow<UpdateGroupsWindow>("Update groups");
		}

		private void OnGUI() {
			// Assets folder component
			GUILayout.Label("Update Addressable groups from an Assets Folder", EditorStyles.boldLabel);
			m_folderAsset = (DefaultAsset)EditorGUILayout.ObjectField("Folders Path", m_folderAsset, typeof(DefaultAsset), false);
			if (m_folderAsset != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(m_folderAsset))) {
				EditorGUILayout.HelpBox("Please select a folder.", MessageType.Warning);
				m_folderAsset = null;
			}

			// Addressable Groups component
			m_selectedGroupIndex = EditorGUILayout.Popup("Addressable Groups", m_selectedGroupIndex, m_groupOptions);

			// Label management component
			if (m_allLabels == null || m_allLabels.Count == 0) {
				m_allLabels = AddressableMenuUtils.GetAllLabels();
			}
			DrawLabelDropdowns();
			
			// Use filter flag component
			m_filterByType = EditorGUILayout.Toggle("Filter by Type", m_filterByType);

			// Filter component
			if (m_filterByType) {
				DrawTypeDropdowns();
			}

			// Update button
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Update", GUILayout.Height(40))) {
				var config = new UpdateGroupsConfig() {
					FolderAsset = m_folderAsset,
					GroupName = m_groupOptions[m_selectedGroupIndex],
					TypesFilter = m_filterByType ? m_selectedTypes.Select(typeIndex => m_typeOptions[typeIndex]).ToArray() : null,
					Lables = m_selectedLabelsIndices.Select(lableIndex => m_allLabels[lableIndex]).ToArray()
				};

				UpdateGroupsController.UpdateGroups(config);
			}
		}

		private void DrawTypeDropdowns() {
			EditorGUILayout.LabelField("Filters:", EditorStyles.boldLabel);

			for (int i = 0; i < m_selectedTypes.Count; i++) {
				EditorGUILayout.BeginHorizontal();
				string[] currentOptions = m_availableTypeOptions[i];
				int currentIndex = Array.IndexOf(currentOptions, m_typeOptions[m_selectedTypes[i]]);
				int newIndex = EditorGUILayout.Popup($"Asset Type {i + 1}", currentIndex, currentOptions);

				if (newIndex != currentIndex) {
					m_selectedTypes[i] = Array.IndexOf(m_typeOptions, currentOptions[newIndex]);
					UpdateAvailableTypeOptions(); // Update options as selection changes
				}

				if (GUILayout.Button("Delete", GUILayout.Width(60))) {
					m_selectedTypes.RemoveAt(i);
					UpdateAvailableTypeOptions(); // Update options after deletion
					i--;
				}

				EditorGUILayout.EndHorizontal();
			}

			if (GUILayout.Button("Add Type")) {
				var usedIndices = new HashSet<int>(m_selectedTypes);
				var firstAvailableIndex = Enumerable.Range(0, m_typeOptions.Length)
					.FirstOrDefault(index => !usedIndices.Contains(index));

				// Check if an available index is found. Assuming 0 is always a valid index if no other types are used.
				if (!usedIndices.Contains(firstAvailableIndex) || usedIndices.Count == 0) {
					m_selectedTypes.Add(firstAvailableIndex);
					UpdateAvailableTypeOptions(); // Update options after adding a new type
				}
			}
		}
		private void UpdateAvailableLabelOptions() {
			m_availableLabelOptions = new List<string[]>(m_selectedLabelsIndices.Count);
			HashSet<int> usedIndices = new HashSet<int>(m_selectedLabelsIndices);

			for (int i = 0; i < m_selectedLabelsIndices.Count; ++i) {
				var optionsList = new List<string>();
				for (int j = 0; j < m_allLabels.Count; ++j) {
					if (!usedIndices.Contains(j) || m_selectedLabelsIndices[i] == j) {
						optionsList.Add(m_allLabels[j]);
					}
				}

				m_availableLabelOptions.Add(optionsList.ToArray());
			}
		}
		
		private void DrawLabelDropdowns() {
			EditorGUILayout.LabelField("Labels:", EditorStyles.boldLabel);

			for (int i = 0; i < m_selectedLabelsIndices.Count; i++) {
				EditorGUILayout.BeginHorizontal();
				string[] currentOptions = m_availableLabelOptions[i];
				int currentIndex = Array.IndexOf(currentOptions, m_allLabels[m_selectedLabelsIndices[i]]);
				int newIndex = EditorGUILayout.Popup($"Label {i + 1}", currentIndex, currentOptions);

				if (newIndex != currentIndex) {
					m_selectedLabelsIndices[i] = m_allLabels.IndexOf(currentOptions[newIndex]);
					UpdateAvailableLabelOptions(); // Update options as selection changes
				}

				if (GUILayout.Button("Delete", GUILayout.Width(60))) {
					m_selectedLabelsIndices.RemoveAt(i);
					UpdateAvailableLabelOptions(); // Update options after deletion
					i--;
				}

				EditorGUILayout.EndHorizontal();
			}

			if (GUILayout.Button("Add Label")) {
				var usedIndices = new HashSet<int>(m_selectedLabelsIndices);
				var firstAvailableIndex = Enumerable.Range(0, m_allLabels.Count)
					.FirstOrDefault(index => !usedIndices.Contains(index));

				// Check if an available index is found.
				// Assuming 0 is always a valid index if no other labels are used.
				if (!usedIndices.Contains(firstAvailableIndex) || usedIndices.Count == 0) {
					m_selectedLabelsIndices.Add(firstAvailableIndex);
					UpdateAvailableLabelOptions(); // Update options after adding a new label
				}
			}
		}

		private void UpdateAvailableTypeOptions() {
			m_availableTypeOptions = new List<string[]>(m_selectedTypes.Count);
			HashSet<int> usedIndices = new HashSet<int>(m_selectedTypes);

			for (int i = 0; i < m_selectedTypes.Count; i++) {
				var optionsList = new List<string>();
				for (int j = 0; j < m_typeOptions.Length; j++) {
					if (!usedIndices.Contains(j) || m_selectedTypes[i] == j) {
						optionsList.Add(m_typeOptions[j]);
					}
				}

				m_availableTypeOptions.Add(optionsList.ToArray());
			}
		}
	}
}
