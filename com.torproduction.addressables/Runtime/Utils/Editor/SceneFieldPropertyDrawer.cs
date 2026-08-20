#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Common
{
    [CustomPropertyDrawer(typeof(SceneField))]
    public class SceneFieldPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty sceneAsset = property.FindPropertyRelative("m_sceneAsset");
            SerializedProperty sceneName = property.FindPropertyRelative("m_sceneName");
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            if (sceneAsset != null) {
                sceneAsset.objectReferenceValue = EditorGUI.ObjectField(position, sceneAsset.objectReferenceValue,
                    typeof(SceneAsset), false);
                if (sceneAsset.objectReferenceValue != null) {
                    sceneName.stringValue = (sceneAsset.objectReferenceValue as SceneAsset)?.name;
                }
            }
            EditorGUI.EndProperty();
        }
    }
}
#endif
