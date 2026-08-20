using UnityEditor;
using TorProduction.AddressablesToolpack.Data;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	[CustomEditor(typeof(ScenesConfig))]
	public class MainMenuConfigEditor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			ScenesConfig config = (ScenesConfig)target;

			// Display the array in a disabled group to make it read-only
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.LabelField("Scene Names", EditorStyles.boldLabel);
			SerializedProperty scenes = serializedObject.FindProperty("m_sceneNames");
			EditorGUILayout.PropertyField(scenes, true);
			EditorGUI.EndDisabledGroup();

			// Apply any properties that have been modified
			serializedObject.ApplyModifiedProperties();
		}
	}
}
