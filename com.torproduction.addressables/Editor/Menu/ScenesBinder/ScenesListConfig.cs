using UnityEditor;
using UnityEngine;
using TorProduction.AddressablesToolpack.Data;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	[CreateAssetMenu(fileName = "ScenesListConfig", menuName = "Tor Production/Scenes Binder Config", order = 31)]
	public class ScenesListConfig : ScriptableObject {
		public DefaultAsset m_ScenesLocation;
		public DefaultAsset m_UIScenesLocation;
		public ScenesConfig m_ScenesConfig;
		public DefaultAsset[] m_OtherSceneFolders;
	}
}
