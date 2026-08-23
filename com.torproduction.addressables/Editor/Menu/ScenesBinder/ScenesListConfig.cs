using UnityEditor;
using UnityEngine;
using System;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	[Obsolete("Legacy migration carrier only. Use AddressablesAutomationConfig scene rules.")]
	public sealed class ScenesListConfig : ScriptableObject {
		public DefaultAsset m_ScenesLocation;
		public DefaultAsset m_UIScenesLocation;
		public UnityEngine.Object m_ScenesConfig;
		public DefaultAsset[] m_OtherSceneFolders = Array.Empty<DefaultAsset>();
	}
}
