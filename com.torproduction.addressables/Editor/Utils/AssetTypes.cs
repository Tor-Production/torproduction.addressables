using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.U2D;

namespace TorProduction.AddressablesToolpack.Editor {
	public static class AssetTypes {
		public static List<Type> AvailableTypes {
			get {
				if (m_cachedTypes == null) {
					m_cachedTypes = CollectTypes();
				}

				return m_cachedTypes;
			}
		}

		private static List<Type> m_cachedTypes;

		private static List<Type> CollectTypes() {
			return new List<Type>(new[] {
				typeof(GameObject),
				typeof(Texture2D),
				typeof(Sprite),
				typeof(SpriteAtlas),
				typeof(Material),
				typeof(AudioClip),
				typeof(AnimationClip),
				typeof(ScriptableObject)
			}).OrderBy(type => type.FullName, StringComparer.Ordinal).ToList();
		}

		internal static List<Type> GetInheritedTypes<T>() {
			var baseType = typeof(T);
			var allTypes = new List<Type>();
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(item => !item.IsDynamic)) {
				try {
					allTypes.AddRange(assembly.GetTypes());
				} catch (ReflectionTypeLoadException exception) {
					allTypes.AddRange(exception.Types.Where(type => type != null));
				} catch (Exception) {
					// A partially loadable optional assembly must not break the type picker.
				}
			}
			return allTypes.Where(type => type.IsClass && !type.IsAbstract && baseType.IsAssignableFrom(type))
				.GroupBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)
				.Select(group => group.First())
				.OrderBy(type => type.FullName, StringComparer.Ordinal)
				.ToList();
		}
	}
}
