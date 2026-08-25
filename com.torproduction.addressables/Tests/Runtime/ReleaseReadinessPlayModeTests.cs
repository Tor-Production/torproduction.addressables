using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine;
using UnityEngine.TestTools;
using UnityAddressables = UnityEngine.AddressableAssets.Addressables;

namespace TorProduction.Addressables.PlayMode.Tests {
	public sealed class ReleaseReadinessPlayModeTests {
		private const string KnownAddress = "tor-production/release-readiness-known-asset";

		[UnityTest]
		public IEnumerator BuiltInPackedPlayMode_LoadsKnownAsset_AndPackageRemainsRuntimeInert() {
			if (!Environment.GetCommandLineArgs().Contains("-torReleaseReadinessPlayMode")) {
				Assert.Ignore("This integration test runs only in the marked disposable-project lane.");
			}
			AssertNoProductionRuntimeSurface();
			var handle = UnityAddressables.LoadAssetAsync<TextAsset>(KnownAddress);
			yield return handle;
			try {
				Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded),
					handle.OperationException?.ToString());
				Assert.That(handle.Result, Is.Not.Null);
				Assert.That(handle.Result.text,
					Is.EqualTo("Tor Production Addressables PlayMode fixture\n"));
			} finally {
				if (handle.IsValid()) UnityAddressables.Release(handle);
			}
			AssertNoProductionRuntimeSurface();
		}

		private static void AssertNoProductionRuntimeSurface() {
			var unexpectedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Select(assembly => assembly.GetName().Name)
				.Where(name => name != null &&
				               name.StartsWith("TorProduction.Addressables", StringComparison.Ordinal) &&
				               name.IndexOf(".Editor", StringComparison.Ordinal) < 0 &&
				               name.IndexOf(".Tests", StringComparison.Ordinal) < 0)
				.OrderBy(name => name, StringComparer.Ordinal)
				.ToArray();
			Assert.That(unexpectedAssemblies, Is.Empty,
				"The editor-only package must not load a production runtime assembly.");

			var unexpectedComponents = Resources.FindObjectsOfTypeAll<Component>()
				.Where(component => component != null &&
				                    component.GetType().Assembly.GetName().Name ==
				                    "TorProduction.Addressables.Editor")
				.Select(component => component.GetType().FullName)
				.OrderBy(name => name, StringComparer.Ordinal)
				.ToArray();
			Assert.That(unexpectedComponents, Is.Empty,
				"Installing the package must not create package-owned runtime components.");
		}
	}
}
