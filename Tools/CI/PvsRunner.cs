using System;
using System.IO;
using System.Text;
using UnityEditor.PackageManager.ValidationSuite;
using UnityEngine;

namespace TorProduction.Addressables.ReleaseReadiness {
	public static class PackageValidationRunner {
		private const string PackageName = "com.torproduction.addressables";

		public static void Run() {
			var arguments = Environment.GetCommandLineArgs();
			var reportPath = ReadRequiredArgument(arguments, "-torPvsReport");
			var packageVersion = ReadRequiredArgument(arguments, "-torPackageVersion");
			var succeeded = ValidationSuite.ValidatePackage(
				PackageName,
				packageVersion,
				ValidationType.LocalDevelopment);
			var report = ValidationSuite.GetValidationSuiteReport(PackageName, packageVersion) ??
			             "Package Validation Suite produced no text report.";
			var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
			if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
			File.WriteAllText(reportPath, report, new UTF8Encoding(false));
			Debug.Log(report);
			if (!succeeded) {
				throw new InvalidOperationException(
					"Package Validation Suite failed. Review the exported report.");
			}
		}

		private static string ReadRequiredArgument(string[] arguments, string name) {
			var index = Array.IndexOf(arguments, name);
			if (index < 0 || index + 1 >= arguments.Length ||
			    string.IsNullOrWhiteSpace(arguments[index + 1])) {
				throw new ArgumentException("Missing required command-line argument " + name + ".");
			}
			return arguments[index + 1];
		}
	}
}
