using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	[Serializable]
	public class UpdateGroupSettings {
		[SerializeField] private string m_groupName;
		[SerializeField] private DefaultAsset m_assetsFolder;
		[SerializeField] private string[] m_lables;
		[SerializeField] [HideInInspector] private string[] m_typesFilterNames;
		[SerializeField] private bool m_filterByType;

		public string GroupName => m_groupName;
		public DefaultAsset AssetsFolder => m_assetsFolder;
		public string[] Lables => m_lables;
		public Type[] TypesFilter =>  m_typesFilterNames.Select(Type.GetType).ToArray(); // Convert string to Type;
		public string[] TypeFilterNames =>  m_typesFilterNames; // Convert string to Type;
		public bool FilterByType => m_filterByType;
	}
}
