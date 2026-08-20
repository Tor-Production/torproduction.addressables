using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Reflection;
using UnityEngine.AddressableAssets;
using RuntimeAddressables = UnityEngine.AddressableAssets.Addressables;
using TorProduction.AddressablesToolpack.Data;
using TorProduction.Addressables.Editor;

// This is a temporary script with is used only when we need to update fields in the
// existing components so I would keep it in project until the whole transition is done.
// Expectedly some of the project API are removed so I have to comment some code between commits

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal class InteractableTemplateFieldsUpdater {
		private const string MENU_PATH = "Tools/Tor Production/Update Interactable Configs To Addressable";

		[MenuItem(MENU_PATH, priority = 200)]
		public static void UpdateFields() {
			if (!AddressablesAutomationWorkflowGate.TryBegin("Interactable config migration", AutomationScope.All)) {
				return;
			}

			// File to store the updated asset paths
			string reportPath = Path.Combine(RuntimeAddressables.LibraryPath, "UpdatedInteractables.txt");

			// Start the file with the current date and time
			File.AppendAllText(reportPath, $"\n\n\tUpdate Session: {DateTime.Now}\n");

			var totalChanged = 0;
			
			
			// Get the type of the class where the field is defined
			Type type = typeof(ObjectTemplate); // Replace with the actual class name

			// Get the field
			FieldInfo fieldInfo = type.GetField("m_characterPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
					
			if (fieldInfo == null) {
				// Handle the case where the field is not found
				Debug.LogError($"{nameof(InteractableTemplateFieldsUpdater)} -> {nameof(UpdateFields)} : " +
				               $"m_characterPrefab  field not found. Maybe the property name was changed but not resolved in reflection string parameter");
				
				File.AppendAllText(reportPath, $"The \"m_characterPrefab\" field is not found. Finished without any action\n");

				return;
			}

			// Find all assets of type Interactable
			string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
			foreach (string guid in guids) {
				var configPath = AssetDatabase.GUIDToAssetPath(guid);
				var scriptableObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(configPath);

				if (scriptableObject is IObjectTemplate interactable) {
					// Update the AssetReferenceGameObject field
					
					
					var prefabValue = (GameObject)fieldInfo.GetValue(interactable);
					
					// TODO: study and revise the edge case here
					// if (prefabValue != null && interactable.CharacterPrefabReference.editorAsset != prefabValue) {
					// 	string assetPath = AssetDatabase.GetAssetPath(prefabValue);
					// 	
					// 	var prefabGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefabValue));
					// 	var reference = new AssetReferenceGameObject(prefabGUID);
					// 	var characterPrefabField = interactable.GetType().GetField("m_characterPrefabReference", BindingFlags.NonPublic | BindingFlags.Instance);
					// 	if (characterPrefabField != null) {
					// 		characterPrefabField.SetValue(interactable, reference);
					// 	} else {
					// 		// Handle the case where the field is not found
					// 		Debug.LogError($"{nameof(InteractableTemplateFieldsUpdater)} -> {nameof(UpdateFields)} : " +
					// 		               $"m_characterPrefabReference  field not found. Maybe the property name was changed but not resolved in reflection string parameter");
					// 	}
					// 	
					// 	// Append the updated asset path to the file
					// 	File.AppendAllText(reportPath, $"{assetPath}\n\t\u2514\u2500\u2500for {configPath}\n");
					//
					// 	++totalChanged;
					// 	// Save the changes
					// 	EditorUtility.SetDirty(scriptableObject);
					// }

				}
			}

			File.AppendAllText(reportPath, $"Total number of changed files: {totalChanged}\n");

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log("Interactables updated. Updated asset paths saved to " + reportPath);
		}

		[MenuItem(MENU_PATH, true)]
		private static bool ValidateUpdateFields() => AddressablesAutomationWorkflowGate.CanExecute(AutomationScope.All);
	}
}
