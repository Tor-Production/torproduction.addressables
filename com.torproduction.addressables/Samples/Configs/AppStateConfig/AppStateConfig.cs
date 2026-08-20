using System.Collections.Generic;
using UnityEngine;

namespace TorProduction.AddressablesToolpack {
	[CreateAssetMenu(fileName = "AppStateConfig", menuName = "Tor Production/AppStateConfig", order = 0)]
	public class AppStateConfig : ScriptableObject {

		[SerializeField] private SerializableDictionary<string, AppState> m_appStatesDictionary;

		public AppState GetAppState(string sceneName) {
			if (!string.IsNullOrEmpty(sceneName) && m_appStatesDictionary.Dictionary.TryGetValue(sceneName, out var state)) {
				return state;
			}
			
			Debug.LogWarning($"{nameof(AppStateConfig)} -> {nameof(GetAppState)} : the scene {sceneName} wasn't found in the config asset file. The default value is set");
			return new AppState{StateValue = -1};
		}

#if UNITY_EDITOR
		public Dictionary<string, AppState> GetAppStatesDictionary() => m_appStatesDictionary.Dictionary;
#endif
	}
}
