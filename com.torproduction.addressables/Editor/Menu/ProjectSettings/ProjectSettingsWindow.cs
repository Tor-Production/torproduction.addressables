using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	[System.Obsolete("Use Project Settings > Tor Production > Addressables Automation.")]
	public class ProjectSettingsWindow : EditorWindow {
		public static void ShowWindow() {
			SettingsService.OpenProjectSettings(AddressablesAutomationSettingsProvider.SettingsPath);
		}

		private void OnGUI() {
			EditorGUILayout.HelpBox(
				"This legacy window is retired. It no longer reads or saves ProjectConfig.json.",
				MessageType.Info);
			if (GUILayout.Button("Open Addressables Automation Settings")) {
				ShowWindow();
			}
		}
	}
}
