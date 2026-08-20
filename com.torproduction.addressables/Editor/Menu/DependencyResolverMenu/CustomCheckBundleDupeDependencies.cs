using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	public class CustomCheckBundleDupeDependencies : CheckBundleDupeDependencies {

		/// <summary>
		/// Fix duplicates by moving to a custom group
		/// </summary>
		/// <param name="settings">The current Addressables settings object</param>
		public override void FixIssues(AddressableAssetSettings settings) {
			// analyze first
			CheckForDuplicateDependencies(settings);

			// retrieving internal field value of the base class
			BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.NonPublic;
			FieldInfo field = typeof(CheckBundleDupeDependencies).GetField("m_ImplicitAssets", bindFlags);
			var implicitAssets = (HashSet<GUID>)field.GetValue(this);

			if (implicitAssets.Count == 0)
				return;

			var group = settings.FindGroup(GroupNames.DEPENDENCIES);

			foreach (var asset in implicitAssets)
				settings.CreateOrMoveEntry(asset.ToString(), group, false, false);

			settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
		}

	}
}
