using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D;
using TorProduction.AddressablesToolpack.Data;

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

		private static List<Type> m_cachedTypes = null;

		private static List<Type> CollectTypes() {
			var types = new List<Type>(new[] {
				// simple types
				typeof(GameObject),
				typeof(Texture2D),
				typeof(Sprite),
				typeof(SpriteAtlas),
				typeof(Material),
				typeof(IObjectTemplate),
				// typeof(IFeedbackMetaTemplate),
				// typeof(IFeedBackProperty),
				// typeof(IBehaviourPropertyTemplate),
				// typeof(IScenesMapping),
				// typeof(ISceneMapID)
			});

			types.AddRange(GetInheritedTypes<IObjectTemplate>());
			// types.AddRange(GetInheritedTypes<IFeedbackMetaTemplate>());
			// types.AddRange(GetInheritedTypes<IFeedBackProperty>());
			// types.AddRange(GetInheritedTypes<IBehaviourPropertyTemplate>());
			// types.AddRange(GetInheritedTypes<IScenesMapping>());
			// types.AddRange(GetInheritedTypes<ISceneMapID>());

			return types;
		}

		internal static List<Type> GetInheritedTypes<T>() {
			// Get the type of the base class
			var baseType = typeof(T);

			// Get all assembly types
			var allTypes = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(assembly => assembly.GetTypes());

			// Filter types to find subclasses of the base type
			var subclassTypes = allTypes
				.Where(type => type.IsClass && !type.IsAbstract && baseType.IsAssignableFrom(type))
				.ToList();

			return subclassTypes;
		}
	}
}
