using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	[CreateAssetMenu(fileName = "AddressableAssetsConfig", menuName = "Tor Production/Addressable Assets Config", order = 31)]
	public class AddressableAssetsConfig : ScriptableObject {
		public UpdateGroupSettings[] m_Settings;
	}
}
