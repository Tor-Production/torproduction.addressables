using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class PrefabsFixerMenu {
		[MenuItem("Tools/Tor Production/Addressables/Fix Prefab Paths")]
		internal static void FixPrefabPathsButtonClick() {
			Debug.Log($"{nameof(PrefabsFixerMenu)} -> {nameof(FixPrefabPathsButtonClick)} : Start changing paths of used interactable prefabs");
			PrefabsFixerController.FixPrefabPaths();
		}
	}
}
