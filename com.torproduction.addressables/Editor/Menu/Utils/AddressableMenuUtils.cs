using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	public class AddressableMenuUtils { 
		internal static List<string> GetAllLabels() { // Method to fetch all labels from addressable settings
			var settings = AddressableAssetSettingsDefaultObject.Settings;
			return settings != null ? new List<string>(settings.GetLabels()) : new List<string>();
		}
		
		internal static string[] GetTypesOptions() {
			var types = AssetTypes.AvailableTypes;
			return types.Select(t => t.Name).ToArray();
		}

		internal static string[] GetAllGroupNames() {
			var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
			if (settings != null && settings.groups != null) {
				return settings.groups.Select(group => group.Name).ToArray();
			}

			return new string[0];
		}

	}
}
