using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
    [CreateAssetMenu(fileName = "PrefabFixerConfig", menuName = "Tor Production/Prefab Fixer Config", order = 31)]
    internal class PrefabsFixerConfig : ScriptableObject {
        [SerializeField] private DefaultAsset m_prefabsRootFolder;

        internal DefaultAsset GetRootFolder() {
            return m_prefabsRootFolder;
        }
    }
}
