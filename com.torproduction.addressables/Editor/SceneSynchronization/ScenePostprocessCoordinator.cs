using System;
using System.Collections.Generic;
using System.Linq;

namespace TorProduction.Addressables.Editor {
	internal static class ScenePostprocessCoordinator {
		private static bool s_scheduled;
		private static bool s_executing;

		internal static bool Notify(
			IEnumerable<string> changedPaths,
			Action<Action> schedule,
			Action reconcile,
			Action<Exception> reportFailure) {
			if (changedPaths == null || !changedPaths.Any(IsScenePath)) return false;
			if (s_scheduled || s_executing) return true;
			if (schedule == null) throw new ArgumentNullException(nameof(schedule));
			if (reconcile == null) throw new ArgumentNullException(nameof(reconcile));
			s_scheduled = true;
			schedule(() => {
				s_scheduled = false;
				if (s_executing) return;
				s_executing = true;
				try { reconcile(); }
				catch (Exception exception) { reportFailure?.Invoke(exception); }
				finally { s_executing = false; }
			});
			return true;
		}

		internal static bool IsScenePath(string path) => !string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

		internal static void ResetForTests() {
			s_scheduled = false;
			s_executing = false;
		}
	}
}
