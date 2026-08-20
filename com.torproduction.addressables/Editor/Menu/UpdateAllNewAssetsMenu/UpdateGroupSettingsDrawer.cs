using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	[CustomPropertyDrawer(typeof(UpdateGroupSettings))]
	public class UpdateGroupSettingsDrawer : PropertyDrawer {
		private List<int> m_selectedTypes = new List<int>();
		private List<string[]> m_availableTypeOptions = new List<string[]>();
		private string[] m_typeOptions = AddressableMenuUtils.GetTypesOptions();

		// Static dictionary to track foldout states (if needed)
		private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			// Check the value of m_filterByType
			SerializedProperty filterByTypeProp = property.FindPropertyRelative("m_filterByType");

			EditorGUI.BeginProperty(position, label, property);

			// Generate a unique key for the current property (e.g., using the property's path)
			string propertyKey = property.propertyPath;

			// Check and initialize foldout state
			if (!foldoutStates.ContainsKey(propertyKey)) {
				foldoutStates[propertyKey] = true; // Default to expanded
			}

			// Draw the foldout
			foldoutStates[propertyKey] = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), foldoutStates[propertyKey], label, true);

			if (!foldoutStates[propertyKey]) {
				return;
			}

			// Draw the property title
			EditorGUI.LabelField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), label);

			position.x += 10;

			// Adjust the position for the rest of the properties
			float singleLineHeight = EditorGUIUtility.singleLineHeight;
			position.y += EditorGUIUtility.singleLineHeight;

			// Calculate rects
			var groupNameRect = new Rect(position.x, position.y, position.width, singleLineHeight);
			position.y += singleLineHeight;
			var assetsFolderRect = new Rect(position.x, position.y, position.width, singleLineHeight);
			position.y += singleLineHeight;

			SerializedProperty labelsProp = property.FindPropertyRelative("m_lables");
			float labelsHeight = EditorGUI.GetPropertyHeight(labelsProp, new GUIContent("Labels"), true);
			var labelsRect = new Rect(position.x, position.y, position.width, labelsHeight);
			position.y += labelsHeight;
			var filterByTypeRect = new Rect(position.x, position.y, position.width, singleLineHeight);
			position.y += singleLineHeight;
			var typesFilterRect = new Rect(position.x, position.y, position.width, singleLineHeight);

			EditorGUI.PropertyField(groupNameRect, property.FindPropertyRelative("m_groupName"), new GUIContent("Group Name"));
			EditorGUI.PropertyField(assetsFolderRect, property.FindPropertyRelative("m_assetsFolder"), new GUIContent("Assets Folder"));

			EditorGUI.PropertyField(labelsRect, labelsProp, new GUIContent("Labels"), true);

			EditorGUI.PropertyField(filterByTypeRect, filterByTypeProp, new GUIContent("Filter By Type"));

			if (filterByTypeProp.boolValue) {
				DrawTypeDropdowns(typesFilterRect, property.FindPropertyRelative("m_typesFilterNames"));
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			string propertyKey = property.propertyPath;

			// Check and initialize foldout state
			if (!foldoutStates.ContainsKey(propertyKey)) {
				foldoutStates[propertyKey] = true; // Default to expanded
			}

			// If the foldout is collapsed, return the height for just the foldout.
			if (!foldoutStates[propertyKey]) {
				return EditorGUIUtility.singleLineHeight;
			}

			// Start with the default height for the property.
			float totalHeight = EditorGUIUtility.singleLineHeight;

			totalHeight += EditorGUIUtility.singleLineHeight * 4; // 4 single-line properties

			// Add the dynamic height of the labels array.
			SerializedProperty labelsProp = property.FindPropertyRelative("m_lables");
			totalHeight += EditorGUI.GetPropertyHeight(labelsProp, true);

			SerializedProperty filterByTypeProp = property.FindPropertyRelative("m_filterByType");
			SerializedProperty typesFilterNamesProp = property.FindPropertyRelative("m_typesFilterNames");
			//totalHeight += EditorGUIUtility.singleLineHeight *
			if (filterByTypeProp.boolValue) {
				// Height for each dropdown in m_selectedTypes
				float dropdownHeight = EditorGUIUtility.singleLineHeight + 2f;  // Height + vertical spacing
				totalHeight += dropdownHeight * typesFilterNamesProp.arraySize;

				// Height for the "Add Type" button
				totalHeight += 20f; // Button height
			}
			
			return totalHeight;
		}

		private void UpdateAvailableTypeOptions() {
			// Resize the list to match the count of selected types
			m_availableTypeOptions = new List<string[]>(m_selectedTypes.Count);

			HashSet<int> usedIndices = new HashSet<int>(m_selectedTypes);

			for (int i = 0; i < m_selectedTypes.Count; i++) {
				var optionsList = new List<string>();

				// Add only those options that are not selected yet, or the current selected option
				for (int j = 0; j < m_typeOptions.Length; j++) {
					if (!usedIndices.Contains(j) || m_selectedTypes[i] == j) {
						optionsList.Add(m_typeOptions[j]);
					}
				}

				// Ensure the list is long enough to accommodate the current index
				while (m_availableTypeOptions.Count <= i) {
					m_availableTypeOptions.Add(new string[0]);
				}

				m_availableTypeOptions[i] = optionsList.ToArray();
			}
		}

		private void DrawTypeDropdowns(Rect position, SerializedProperty typesFilterProperty) {
			float singleLineHeight = EditorGUIUtility.singleLineHeight;
			float verticalSpacing = 2f; // Space between controls
			float buttonHeight = 20f;   // Height of the "Add Type" button

			// Ensure m_selectedTypes is in sync with the serialized property
			SyncSelectedTypesWithProperty(typesFilterProperty);

			// Label
			EditorGUI.LabelField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), "Filters:", EditorStyles.boldLabel);

			float currentY = position.y + singleLineHeight + verticalSpacing; // Start below the label

			// Draw each type dropdown
			for (int i = 0; i < m_selectedTypes.Count; i++) {
				Rect dropdownRect = new Rect(position.x, currentY, position.width - 60, singleLineHeight);

				// Dropdown for type selection
				string[] currentOptions = m_availableTypeOptions[i];
				int currentIndex = Array.IndexOf(currentOptions, m_typeOptions[m_selectedTypes[i]]);
				int newIndex = EditorGUI.Popup(dropdownRect, $"Asset Type {i + 1}", currentIndex, currentOptions);

				// Update type selection
				if (newIndex != currentIndex) {
					m_selectedTypes[i] = Array.IndexOf(m_typeOptions, currentOptions[newIndex]);;
					UpdateAvailableTypeOptions();
					UpdateSerializedProperty(typesFilterProperty);
				}

				// Delete button
				if (GUI.Button(new Rect(position.x + position.width - 60, currentY, 60, singleLineHeight), "Delete")) {
					m_selectedTypes.RemoveAt(i);
					UpdateAvailableTypeOptions();
					UpdateSerializedProperty(typesFilterProperty);
					i--;
				}

				currentY += singleLineHeight + verticalSpacing;
			}

			// Add button position should be updated based on currentY
			if (GUI.Button(new Rect(position.x, currentY, position.width, buttonHeight), "Add Type")) {
				AddNewTypeSelection();
				UpdateSerializedProperty(typesFilterProperty);
				UpdateAvailableTypeOptions();
			}
		}

		private void SyncSelectedTypesWithProperty(SerializedProperty typesFilterProp) {
			m_selectedTypes.Clear();
			for (int i = 0; i < typesFilterProp.arraySize; i++) {
				SerializedProperty elementProp = typesFilterProp.GetArrayElementAtIndex(i);

				if (elementProp.propertyType == SerializedPropertyType.String) {
					string typeName = elementProp.stringValue;
					int typeIndex = Array.IndexOf(m_typeOptions, typeName);
					if (typeIndex >= 0) {
						m_selectedTypes.Add(typeIndex);
					}
				} else {
					Debug.LogError("Expected string type in the serialized property array.");
				}
			}

			UpdateAvailableTypeOptions();
		}

		private void UpdateSerializedProperty(SerializedProperty typesFilterProp) {
			typesFilterProp.arraySize = m_selectedTypes.Count;
			for (int i = 0; i < m_selectedTypes.Count; i++) {
				int typeIndex = m_selectedTypes[i];
				if (typeIndex >= 0 && typeIndex < m_typeOptions.Length) {
					string typeName = m_typeOptions[typeIndex];
					typesFilterProp.GetArrayElementAtIndex(i).stringValue = typeName;
				} else {
					Debug.LogError("Invalid type index: " + typeIndex);
				}
			}

			typesFilterProp.serializedObject.ApplyModifiedProperties();
		}

		private void AddNewTypeSelection() {
			var usedIndices = new HashSet<int>(m_selectedTypes);
			var firstAvailableIndex = Enumerable.Range(0, m_typeOptions.Length)
				.FirstOrDefault(index => !usedIndices.Contains(index));

			if (!usedIndices.Contains(firstAvailableIndex) || usedIndices.Count == 0) {
				m_selectedTypes.Add(firstAvailableIndex);
				UpdateAvailableTypeOptions();
			}
		}
	}
}
