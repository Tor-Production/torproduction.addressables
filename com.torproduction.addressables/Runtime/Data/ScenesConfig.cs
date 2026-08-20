#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Linq;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Data {
	[CreateAssetMenu(fileName = "ScenesConfig", menuName = "Tor Production/Scenes Config", order = 31)]
	public class ScenesConfig : ScriptableObject {

		[SerializeField] private string[] m_sceneNames; // now it's used just for visualization in the inspector window but consider to change custom inspector  
		[SerializeField] [HideInInspector] private SceneInfo[] m_sceneInfos;
		

		public string[] GetSceneNames() {
			return (string[])m_sceneNames.Clone(); // clone is used to avoid changing the actual array
		}

#if UNITY_EDITOR
		public void SetSceneInfos(SceneInfo[] scenes) {
			m_sceneNames = scenes.Select(s => s.Name).ToArray();
			m_sceneInfos = scenes;
			EditorUtility.SetDirty(this);
		}
		
		public SceneInfo[] GetSceneInfos() {
			return (SceneInfo[])m_sceneInfos.Clone(); // clone is used to avoid changing the actual array
		}

#endif
	}
}
