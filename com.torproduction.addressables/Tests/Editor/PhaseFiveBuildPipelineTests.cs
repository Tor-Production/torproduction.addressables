using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TorProduction.Addressables.Editor.Cli;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace TorProduction.Addressables.Editor.Tests {
	internal sealed class PhaseFiveBuildPipelineTests {
		private string m_temporaryRoot;

		[SetUp]
		public void SetUp() {
			m_temporaryRoot = Path.Combine(
				"Library",
				"TorProduction.Addressables.Tests",
				Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(m_temporaryRoot);
		}

		[TearDown]
		public void TearDown() {
			if (Directory.Exists(m_temporaryRoot)) Directory.Delete(m_temporaryRoot, true);
		}

		[Test]
		public void FullBuild_DoesNotReadOrRequireContentState() {
			var backend = new FakeBackend { ActiveTarget = BuildTarget.StandaloneWindows64 };
			var store = new FakeStore(m_temporaryRoot);
			var engine = new ContentBuildJobEngine(backend, store);

			var preflight = engine.Analyze(ContentBuildRequest.Full(ContentBuildPlatform.Windows));
			var result = engine.Start(ContentBuildRequest.Full(ContentBuildPlatform.Windows));

			Assert.That(preflight.IsValid, Is.True);
			Assert.That(backend.StateValidationCalls, Is.Zero);
			Assert.That(backend.RestrictionCalls, Is.Zero);
			Assert.That(backend.FullBuildCalls, Is.EqualTo(1));
			Assert.That(result.Status, Is.EqualTo(ContentBuildStatus.Success));
			Assert.That(store.Exists, Is.False);
		}

		[Test]
		public void ContentUpdate_RejectsMissingPathBeforeMutation() {
			var backend = new FakeBackend();
			var store = new FakeStore(m_temporaryRoot);
			var result = new ContentBuildJobEngine(backend, store).Start(
				ContentBuildRequest.ContentUpdate(ContentBuildPlatform.Windows, string.Empty));

			AssertBlockedBeforeMutation(result, backend, store, ContentBuildDiagnosticCode.StateFileRequired);
		}

		[Test]
		public void ContentUpdate_RejectsAbsentFileBeforeMutation() {
			var backend = new FakeBackend { FileExistsResult = false };
			var store = new FakeStore(m_temporaryRoot);
			var result = new ContentBuildJobEngine(backend, store).Start(
				ContentBuildRequest.ContentUpdate(ContentBuildPlatform.Windows, "missing.bin"));

			AssertBlockedBeforeMutation(result, backend, store, ContentBuildDiagnosticCode.StateFileMissing);
		}

		[TestCase(ContentBuildDiagnosticCode.StateFileInvalid)]
		[TestCase(ContentBuildDiagnosticCode.StateFileIncompatible)]
		public void ContentUpdate_RejectsInvalidOrIncompatibleStateBeforeMutation(
			ContentBuildDiagnosticCode diagnosticCode) {
			var backend = new FakeBackend {
				StateValidation = new ContentStateValidationResult(
					false,
					string.Empty,
					new[] { Error(diagnosticCode, "Invalid or incompatible state.") })
			};
			var store = new FakeStore(m_temporaryRoot);
			var result = new ContentBuildJobEngine(backend, store).Start(
				ContentBuildRequest.ContentUpdate(ContentBuildPlatform.Android, "state.bin"));

			AssertBlockedBeforeMutation(result, backend, store, diagnosticCode);
			Assert.That(backend.RestrictionCalls, Is.Zero);
		}

		[Test]
		public void ContentUpdate_RestrictionFailureBlocksBeforeMutation() {
			var backend = new FakeBackend {
				RestrictionDiagnostics = new[] {
					Error(ContentBuildDiagnosticCode.ContentUpdateRestriction, "Static-content restriction.")
				}
			};
			var store = new FakeStore(m_temporaryRoot);
			var result = new ContentBuildJobEngine(backend, store).Start(
				ContentBuildRequest.ContentUpdate(ContentBuildPlatform.Windows, "state.bin"));

			AssertBlockedBeforeMutation(result, backend, store, ContentBuildDiagnosticCode.ContentUpdateRestriction);
			Assert.That(backend.RestrictionCalls, Is.EqualTo(1));
		}

		[TestCase(ContentBuildPlatform.Android, BuildTarget.Android)]
		[TestCase(ContentBuildPlatform.iOS, BuildTarget.iOS)]
		[TestCase(ContentBuildPlatform.Windows, BuildTarget.StandaloneWindows64)]
		[TestCase(ContentBuildPlatform.macOS, BuildTarget.StandaloneOSX)]
		[TestCase(ContentBuildPlatform.Linux, BuildTarget.StandaloneLinux64)]
		public void PlatformMappings_AreExact(ContentBuildPlatform platform, BuildTarget expected) {
			Assert.That(BuildTargetMapper.TryMap(platform, out var actual), Is.True);
			Assert.That(actual, Is.EqualTo(expected));
		}

		[TestCase(RuntimePlatform.WindowsEditor, BuildTarget.StandaloneWindows64)]
		[TestCase(RuntimePlatform.OSXEditor, BuildTarget.StandaloneOSX)]
		[TestCase(RuntimePlatform.LinuxEditor, BuildTarget.StandaloneLinux64)]
		public void EditorMappings_AreExact(RuntimePlatform editor, BuildTarget expected) {
			Assert.That(BuildTargetMapper.TryMapEditor(editor, out var actual), Is.True);
			Assert.That(actual, Is.EqualTo(expected));
		}

		[Test]
		public void UnsupportedModule_BlocksEveryRequestedPlatformBeforeFirstMutation() {
			var backend = new FakeBackend();
			backend.UnsupportedTargets.Add(BuildTarget.Android);
			var store = new FakeStore(m_temporaryRoot);
			var request = ContentBuildRequest.MultiPlatform(new[] {
				ContentBuildPlatform.Windows,
				ContentBuildPlatform.Android
			});

			var result = new ContentBuildJobEngine(backend, store).Start(request);

			AssertBlockedBeforeMutation(result, backend, store, ContentBuildDiagnosticCode.TargetUnsupported);
			Assert.That(backend.SupportChecks, Is.EqualTo(2));
		}

		[Test]
		public void RejectedTargetSwitch_IsReportedAndOriginalTargetIsRetained() {
			var backend = new FakeBackend { SwitchResult = false };
			var store = new FakeStore(m_temporaryRoot);

			var result = new ContentBuildJobEngine(backend, store).Start(
				ContentBuildRequest.Full(ContentBuildPlatform.Android));

			Assert.That(result.Status, Is.EqualTo(ContentBuildStatus.TargetSwitchFailure));
			Assert.That(result.Items.Single().Status, Is.EqualTo(ContentBuildStatus.TargetSwitchFailure));
			Assert.That(backend.ActiveTarget, Is.EqualTo(BuildTarget.StandaloneWindows64));
			Assert.That(store.Exists, Is.False);
		}

		[Test]
		public void DomainReload_RequiresExplicitResumeBeforeBuild() {
			var backend = new FakeBackend();
			var store = new FakeStore(m_temporaryRoot);
			var firstEngine = new ContentBuildJobEngine(backend, store);

			var switched = firstEngine.Start(ContentBuildRequest.Full(ContentBuildPlatform.Android));
			var recovery = new ContentBuildJobEngine(backend, store).InspectRecovery();
			var resumed = new ContentBuildJobEngine(backend, store).Resume();

			Assert.That(switched.Status, Is.EqualTo(ContentBuildStatus.AwaitingResume));
			Assert.That(switched.RequiresUserAction, Is.True);
			Assert.That(backend.FullBuildCalls, Is.EqualTo(1), "Only explicit Resume may invoke the build.");
			Assert.That(recovery.Exists, Is.True);
			Assert.That(recovery.Stage, Is.EqualTo(BuildJobStage.AwaitingResume.ToString()));
			Assert.That(resumed.Status, Is.EqualTo(ContentBuildStatus.Success));
			Assert.That(backend.ActiveTarget, Is.EqualTo(BuildTarget.StandaloneWindows64));
			Assert.That(store.Exists, Is.False);
		}

		[Test]
		public void Recovery_IdentifiesStaleAndIncompleteStateWithoutMutation() {
			var backend = new FakeBackend();
			var store = new FakeStore(m_temporaryRoot) {
				Record = ValidRecord(backend.UtcNowTicks - ContentBuildJobEngine.StaleAfterTicks - 1)
			};
			var recovery = new ContentBuildJobEngine(backend, store).InspectRecovery();

			Assert.That(recovery.Exists, Is.True);
			Assert.That(recovery.IsValid, Is.True);
			Assert.That(recovery.IsStale, Is.True);
			Assert.That(backend.MutationCount, Is.Zero);
			Assert.That(store.SaveCalls, Is.Zero);

			store.Record = new BuildJobRecord();
			var incomplete = new ContentBuildJobEngine(backend, store).InspectRecovery();
			Assert.That(incomplete.Exists, Is.True);
			Assert.That(incomplete.IsValid, Is.False);
			Assert.That(incomplete.IsStale, Is.True);
			Assert.That(backend.MutationCount, Is.Zero);

			var reset = new ContentBuildJobEngine(backend, store).AbandonReset();
			Assert.That(reset.Status, Is.EqualTo(ContentBuildStatus.Warning));
			Assert.That(store.Exists, Is.False);
			Assert.That(store.InvalidArchiveCalls, Is.EqualTo(1));
		}

		[TestCase(false, ContentBuildStatus.Success)]
		[TestCase(true, ContentBuildStatus.FatalFailure)]
		public void OriginalTarget_IsRestoredAfterSuccessOrBuildException(
			bool throwDuringBuild,
			ContentBuildStatus expectedStatus) {
			var backend = new FakeBackend { ThrowDuringBuild = throwDuringBuild };
			var store = new FakeStore(m_temporaryRoot);
			var engine = new ContentBuildJobEngine(backend, store);
			Assert.That(engine.Start(ContentBuildRequest.Full(ContentBuildPlatform.Android)).Status,
				Is.EqualTo(ContentBuildStatus.AwaitingResume));

			var result = engine.Resume();

			Assert.That(result.Status, Is.EqualTo(expectedStatus));
			Assert.That(backend.ActiveTarget, Is.EqualTo(BuildTarget.StandaloneWindows64));
			Assert.That(store.Exists, Is.False);
		}

		[Test]
		public void Cancellation_RestoresOriginalTargetAndSkipsPendingRequests() {
			var backend = new FakeBackend();
			var store = new FakeStore(m_temporaryRoot);
			var engine = new ContentBuildJobEngine(backend, store);
			Assert.That(engine.Start(ContentBuildRequest.MultiPlatform(new[] {
				ContentBuildPlatform.Android,
				ContentBuildPlatform.Linux
			})).Status, Is.EqualTo(ContentBuildStatus.AwaitingResume));

			var result = engine.RequestCancellation();

			Assert.That(result.Status, Is.EqualTo(ContentBuildStatus.Cancellation));
			Assert.That(result.Items.Any(item => item.Status == ContentBuildStatus.Cancellation), Is.True);
			Assert.That(result.Items.Any(item => item.Status == ContentBuildStatus.Skipped), Is.True);
			Assert.That(backend.ActiveTarget, Is.EqualTo(BuildTarget.StandaloneWindows64));
			Assert.That(store.Exists, Is.False);
		}

		[Test]
		public void RestorationFailure_RetainsActionableRecoveryState() {
			var backend = new FakeBackend {
				SwitchOverride = target => target != BuildTarget.StandaloneWindows64
			};
			var store = new FakeStore(m_temporaryRoot);
			var engine = new ContentBuildJobEngine(backend, store);
			Assert.That(engine.Start(ContentBuildRequest.Full(ContentBuildPlatform.Android)).Status,
				Is.EqualTo(ContentBuildStatus.AwaitingResume));

			var result = engine.Resume();

			Assert.That(result.Status, Is.EqualTo(ContentBuildStatus.RestorationFailure));
			Assert.That(result.RequiresUserAction, Is.True);
			Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ContentBuildDiagnosticCode.RestorationFailed));
			Assert.That(store.Exists, Is.True);
			Assert.That(store.Record.stage, Is.EqualTo(BuildJobStage.RestorationFailed.ToString()));
			StringAssert.Contains("Retry Restore Original Target", store.Record.recoveryMessage);
		}

		[Test]
		public void MultiPlatform_DefaultStopsAndExplicitContinueProcessesLaterTarget() {
			var stopBackend = new FakeBackend();
			stopBackend.BuildOutcomes[BuildTarget.StandaloneWindows64] = new BuildExecutionOutcome(false, "failure");
			var stopped = new ContentBuildJobEngine(stopBackend, new FakeStore(m_temporaryRoot)).Start(
				ContentBuildRequest.MultiPlatform(new[] { ContentBuildPlatform.Windows, ContentBuildPlatform.Linux }));

			Assert.That(stopped.Status, Is.EqualTo(ContentBuildStatus.FatalFailure));
			Assert.That(stopped.Items.Select(item => item.Status),
				Is.EqualTo(new[] { ContentBuildStatus.FatalFailure, ContentBuildStatus.Skipped }));
			Assert.That(stopBackend.FullBuildCalls, Is.EqualTo(1));

			var continueBackend = new FakeBackend();
			continueBackend.BuildOutcomes[BuildTarget.StandaloneWindows64] = new BuildExecutionOutcome(false, "failure");
			var continueStore = new FakeStore(m_temporaryRoot);
			var continueEngine = new ContentBuildJobEngine(continueBackend, continueStore);
			var waiting = continueEngine.Start(ContentBuildRequest.MultiPlatform(
				new[] { ContentBuildPlatform.Windows, ContentBuildPlatform.Linux },
				ContentBuildFailurePolicy.ContinueOnError));
			Assert.That(waiting.Status, Is.EqualTo(ContentBuildStatus.AwaitingResume));
			var continued = continueEngine.Resume();

			Assert.That(continued.Status, Is.EqualTo(ContentBuildStatus.FatalFailure));
			Assert.That(continued.Items.Count, Is.EqualTo(2));
			Assert.That(continued.Items.Any(item => item.Status == ContentBuildStatus.Skipped), Is.False);
			Assert.That(continueBackend.FullBuildCalls, Is.EqualTo(2));
			Assert.That(continueBackend.ActiveTarget, Is.EqualTo(BuildTarget.StandaloneWindows64));
		}

		[Test]
		public void QueueOptimization_IsDeterministicAndDoesNotConflateStandaloneTargets() {
			var requested = new[] {
				ContentBuildPlatform.Linux,
				ContentBuildPlatform.Windows,
				ContentBuildPlatform.macOS,
				ContentBuildPlatform.Android,
				ContentBuildPlatform.iOS
			};

			var first = BuildTargetMapper.Optimize(requested, BuildTarget.StandaloneOSX);
			var second = BuildTargetMapper.Optimize(requested.Reverse(), BuildTarget.StandaloneOSX);

			Assert.That(first, Is.EqualTo(second));
			Assert.That(first, Is.EqualTo(new[] {
				BuildTarget.StandaloneOSX,
				BuildTarget.Android,
				BuildTarget.iOS,
				BuildTarget.StandaloneWindows64,
				BuildTarget.StandaloneLinux64
			}));
			Assert.That(first.Distinct().Count(), Is.EqualTo(5));
		}

		[Test]
		public void BuildLayoutCapture_CopiesFreshSourceWithoutDeletingOrOverwritingIt() {
			var source = Path.Combine(m_temporaryRoot, "buildlayout.json");
			var operation = Path.Combine(m_temporaryRoot, "operation");
			Directory.CreateDirectory(operation);
			File.WriteAllText(source, "source-layout");
			var started = File.GetLastWriteTimeUtc(source).Ticks - TimeSpan.FromSeconds(1).Ticks;

			var result = BuildLayoutArtifactService.Capture(
				new[] { source }, operation, started, BuildTarget.StandaloneWindows64);

			Assert.That(result.Status, Is.EqualTo(ContentBuildStatus.Success));
			Assert.That(File.Exists(source), Is.True);
			Assert.That(File.ReadAllText(source), Is.EqualTo("source-layout"));
			Assert.That(File.Exists(result.CopiedPath), Is.True);
			Assert.That(File.ReadAllText(result.CopiedPath), Is.EqualTo("source-layout"));
			var second = BuildLayoutArtifactService.Capture(
				new[] { source }, operation, started, BuildTarget.StandaloneWindows64);
			Assert.That(second.Status, Is.EqualTo(ContentBuildStatus.FatalFailure));
			Assert.That(File.ReadAllText(result.CopiedPath), Is.EqualTo("source-layout"));
		}

		[Test]
		public void BuildLayoutCapture_MissingAndStaleSourcesAreWarnings() {
			var operation = Path.Combine(m_temporaryRoot, "operation");
			Directory.CreateDirectory(operation);
			var missing = BuildLayoutArtifactService.Capture(
				new[] { Path.Combine(m_temporaryRoot, "missing.json") },
				operation,
				DateTime.UtcNow.Ticks,
				BuildTarget.Android);
			Assert.That(missing.Status, Is.EqualTo(ContentBuildStatus.Warning));

			var staleSource = Path.Combine(m_temporaryRoot, "stale.txt");
			File.WriteAllText(staleSource, "old");
			var stale = BuildLayoutArtifactService.Capture(
				new[] { staleSource },
				operation,
				File.GetLastWriteTimeUtc(staleSource).Ticks + 1,
				BuildTarget.Android);
			Assert.That(stale.Status, Is.EqualTo(ContentBuildStatus.Warning));
			Assert.That(Directory.GetFiles(operation), Is.Empty);
		}

		[Test]
		public void ExistingBuildReceipt_FreshnessIsDeterministicAndInvalidatesOnChange() {
			var ticks = DateTime.UtcNow.Ticks;
			var receipt = ValidReceipt(ticks);
			var context = ReceiptContext(ticks, "settings-hash", "artifact-hash");

			var valid = BuildReceiptValidator.Validate(receipt, "receipt.json", context);
			Assert.That(valid.IsValid, Is.True);

			var changedSettings = BuildReceiptValidator.Validate(
				receipt, "receipt.json", ReceiptContext(ticks, "changed", "artifact-hash"));
			Assert.That(changedSettings.IsValid, Is.False);
			Assert.That(changedSettings.Diagnostics.Any(item => item.Code == ContentBuildDiagnosticCode.ReceiptStale), Is.True);

			var changedArtifact = BuildReceiptValidator.Validate(
				receipt, "receipt.json", ReceiptContext(ticks, "settings-hash", "changed"));
			Assert.That(changedArtifact.IsValid, Is.False);
			Assert.That(changedArtifact.Diagnostics.Any(item => item.Code == ContentBuildDiagnosticCode.ReceiptStale), Is.True);

			receipt.target = BuildTarget.Android.ToString();
			var incompatible = BuildReceiptValidator.Validate(receipt, "receipt.json", context);
			Assert.That(incompatible.IsValid, Is.False);
			Assert.That(incompatible.Diagnostics.Any(item => item.Code == ContentBuildDiagnosticCode.ReceiptTargetMismatch), Is.True);
		}

		[Test]
		public void EditorCompatibleBuild_CreatesReceiptForExactEditorTarget() {
			var backend = new FakeBackend {
				EditorPlatform = RuntimePlatform.WindowsEditor,
				ActiveTarget = BuildTarget.StandaloneWindows64
			};
			var result = new ContentBuildJobEngine(backend, new FakeStore(m_temporaryRoot)).Start(
				ContentBuildRequest.EditorCompatible());

			Assert.That(result.Status, Is.EqualTo(ContentBuildStatus.Success));
			Assert.That(result.Items.Single().Target, Is.EqualTo(BuildTarget.StandaloneWindows64));
			Assert.That(result.Items.Single().ReceiptPath, Is.EqualTo("receipt.json"));
			Assert.That(backend.ReceiptCalls, Is.EqualTo(1));
		}

		[Test]
		public void Analyze_IsImmutableAndDoesNotCreateRecoveryState() {
			var backend = new FakeBackend();
			var store = new FakeStore(m_temporaryRoot);
			var engine = new ContentBuildJobEngine(backend, store);

			var first = engine.Analyze(ContentBuildRequest.Full(ContentBuildPlatform.Windows));
			var second = engine.Analyze(ContentBuildRequest.Full(ContentBuildPlatform.Windows));

			Assert.That(first.IsValid, Is.True);
			Assert.That(first.RequestHash, Is.EqualTo(second.RequestHash));
			Assert.That(backend.MutationCount, Is.Zero);
			Assert.That(store.SaveCalls, Is.Zero);
			Assert.That(store.ClearLegacyCalls, Is.Zero);
			Assert.That(store.Exists, Is.False);
		}

		[Test]
		public void Cli_UsesSharedPreflightAndReturnsFailureForBlockingDiagnostics() {
			var api = new FakeCliApi { PreflightValid = false };
			var output = new List<string>();
			var failureCode = AddressablesCli.Run(
				new[] { "Unity", "-torAction", "full-build", "-torTarget", "Windows" },
				api,
				output.Add);

			Assert.That(failureCode, Is.EqualTo(1));
			Assert.That(api.AnalyzeCalls, Is.EqualTo(1));
			Assert.That(api.StartCalls, Is.Zero);
			StringAssert.Contains("Blocking preflight", output.Single());
		}

		[Test]
		public void Cli_SuccessPreviewsThenStartsSameRequest() {
			var api = new FakeCliApi { PreflightValid = true };
			var output = new List<string>();
			var exitCode = AddressablesCli.Run(
				new[] { "Unity", "-torAction", "full-build", "-torTarget", "Windows" },
				api,
				output.Add);

			Assert.That(exitCode, Is.Zero);
			Assert.That(api.AnalyzeCalls, Is.EqualTo(1));
			Assert.That(api.StartCalls, Is.EqualTo(1));
			Assert.That(api.LastAnalyzed.Kind, Is.EqualTo(ContentBuildKind.Full));
			Assert.That(api.LastStarted.Kind, Is.EqualTo(api.LastAnalyzed.Kind));
			Assert.That(api.LastStarted.Platform, Is.EqualTo(api.LastAnalyzed.Platform));
			Assert.That(output.Count, Is.EqualTo(2));
		}

		[Test]
		public void ImportBootstrap_IsInertWhenNoRecoveryJobExists() {
			var recovery = new ContentBuildRecoveryInfo(
				false,
				true,
				false,
				string.Empty,
				string.Empty,
				BuildTarget.NoTarget,
				BuildTarget.StandaloneWindows64,
				Array.Empty<BuildTarget>(),
				"No job.",
				"current.json");

			Assert.That(TorProduction.AddressablesToolpack.Editor.Menu.BuildRecoveryBootstrap.ShouldOfferRecovery(recovery), Is.False);
		}

		[Test]
		public void ProductionSources_ContainNoPrivateAddressablesReflectionOrLegacyExecutionPath() {
			var packageInfo = PackageManagerPackageInfo.FindForAssembly(typeof(PhaseFiveBuildPipelineTests).Assembly);
			var editorRoot = Path.Combine(packageInfo.resolvedPath, "Editor");
			var sourcePaths = Directory.EnumerateFiles(editorRoot, "*.cs", SearchOption.AllDirectories).ToArray();
			var source = string.Join("\n", sourcePaths.Select(File.ReadAllText));

			StringAssert.DoesNotContain("EditorPlaymodeBuildScript", source);
			StringAssert.DoesNotContain("ReportUpdater", source);
			StringAssert.DoesNotContain("TargetPlatform", source);
			StringAssert.DoesNotContain("GetField(\"", source);
			StringAssert.DoesNotContain("GetProperty(\"", source);
			StringAssert.DoesNotContain("BindingFlags.NonPublic", source);
			StringAssert.Contains("AddressableAssetSettings.BuildPlayerContent", source);
			StringAssert.Contains("ContentUpdateScript.BuildContentUpdate", source);
		}

		private static void AssertBlockedBeforeMutation(
			ContentBuildResult result,
			FakeBackend backend,
			FakeStore store,
			ContentBuildDiagnosticCode expectedCode) {
			Assert.That(result.Status, Is.EqualTo(ContentBuildStatus.FatalFailure));
			Assert.That(result.Diagnostics.Any(item => item.Code == expectedCode), Is.True);
			Assert.That(backend.MutationCount, Is.Zero);
			Assert.That(store.SaveCalls, Is.Zero);
			Assert.That(store.Exists, Is.False);
		}

		private static ContentBuildDiagnostic Error(ContentBuildDiagnosticCode code, string message) =>
			new ContentBuildDiagnostic(code, ContentBuildDiagnosticSeverity.Error, message);

		private static BuildJobRecord ValidRecord(long updatedTicks) => new BuildJobRecord {
			jobId = "job",
			buildKind = ContentBuildKind.Full.ToString(),
			stage = BuildJobStage.AwaitingResume.ToString(),
			failurePolicy = ContentBuildFailurePolicy.StopOnFirstFailure.ToString(),
			allTargets = new[] { BuildTarget.Android.ToString() },
			pendingTargets = new[] { BuildTarget.Android.ToString() },
			completed = Array.Empty<BuildJobItemRecord>(),
			originalTarget = BuildTarget.StandaloneWindows64.ToString(),
			activeTarget = BuildTarget.Android.ToString(),
			operationDirectory = "operation",
			reportPath = "report.json",
			settingsGuid = "settings-guid",
			settingsHash = "settings-hash",
			addressablesVersion = "2.7.6",
			requestHash = "request-hash",
			createdUtcTicks = updatedTicks,
			updatedUtcTicks = updatedTicks
		};

		private static ContentBuildReceipt ValidReceipt(long ticks) => new ContentBuildReceipt {
			jobId = "job",
			buildKind = ContentBuildKind.EditorCompatible.ToString(),
			target = BuildTarget.StandaloneWindows64.ToString(),
			settingsGuid = "settings-guid",
			settingsHash = "settings-hash",
			addressablesVersion = "2.7.6",
			unityVersion = "6000.0.78f1",
			outputPath = "output",
			settingsFilePath = "settings.json",
			settingsFileHash = "artifact-hash",
			settingsFileLength = 10,
			settingsFileLastWriteUtcTicks = ticks - 2,
			buildCompletedUtcTicks = ticks - 1,
			createdUtcTicks = ticks
		};

		private static BuildReceiptValidationContext ReceiptContext(
			long ticks,
			string settingsHash,
			string artifactHash) => new BuildReceiptValidationContext(
			BuildTarget.StandaloneWindows64,
			RuntimePlatform.WindowsEditor,
			"settings-guid",
			settingsHash,
			"2.7.6",
			"6000.0.78f1",
			_ => true,
			_ => 10,
			_ => ticks - 2,
			_ => artifactHash);

		private sealed class FakeBackend : IContentBuildBackend {
			public BuildTarget ActiveTarget { get; set; } = BuildTarget.StandaloneWindows64;
			public RuntimePlatform EditorPlatform { get; set; } = RuntimePlatform.WindowsEditor;
			public bool SettingsExist { get; set; } = true;
			public bool PlayerDataBuilderValid { get; set; } = true;
			public string SettingsGuid { get; set; } = "settings-guid";
			public string SettingsHash { get; set; } = "settings-hash";
			public string AddressablesVersion { get; set; } = "2.7.6";
			public long UtcNowTicks { get; set; } = DateTime.UtcNow.Ticks;
			public bool IsCancellationRequested { get; set; }
			public bool FileExistsResult { get; set; } = true;
			public bool SwitchResult { get; set; } = true;
			public bool ThrowDuringBuild { get; set; }
			public Func<BuildTarget, bool> SwitchOverride { get; set; }
			public HashSet<BuildTarget> UnsupportedTargets { get; } = new HashSet<BuildTarget>();
			public Dictionary<BuildTarget, BuildExecutionOutcome> BuildOutcomes { get; } =
				new Dictionary<BuildTarget, BuildExecutionOutcome>();
			public ContentStateValidationResult StateValidation { get; set; } =
				new ContentStateValidationResult(true, "state-hash", Array.Empty<ContentBuildDiagnostic>());
			public IReadOnlyList<ContentBuildDiagnostic> RestrictionDiagnostics { get; set; } =
				Array.Empty<ContentBuildDiagnostic>();
			public int SupportChecks { get; private set; }
			public int StateValidationCalls { get; private set; }
			public int RestrictionCalls { get; private set; }
			public int SwitchCalls { get; private set; }
			public int FullBuildCalls { get; private set; }
			public int ContentUpdateBuildCalls { get; private set; }
			public int ReceiptCalls { get; private set; }
			public int MutationCount => SwitchCalls + FullBuildCalls + ContentUpdateBuildCalls;

			public bool FileExists(string path) => FileExistsResult;

			public bool IsTargetSupported(BuildTargetGroup group, BuildTarget target) {
				SupportChecks++;
				return !UnsupportedTargets.Contains(target);
			}

			public ContentStateValidationResult ValidateContentState(string path, BuildTarget target) {
				StateValidationCalls++;
				return StateValidation;
			}

			public IReadOnlyList<ContentBuildDiagnostic> CheckContentUpdateRestrictions(string path, BuildTarget target) {
				RestrictionCalls++;
				return RestrictionDiagnostics;
			}

			public bool SwitchActiveTarget(BuildTargetGroup group, BuildTarget target) {
				SwitchCalls++;
				var accepted = SwitchOverride != null ? SwitchOverride(target) : SwitchResult;
				if (accepted) ActiveTarget = target;
				return accepted;
			}

			public BuildExecutionOutcome BuildFull(BuildTarget target) {
				FullBuildCalls++;
				if (ThrowDuringBuild) throw new InvalidOperationException("simulated build exception");
				return BuildOutcomes.TryGetValue(target, out var outcome)
					? outcome
					: new BuildExecutionOutcome(true, "success", "output", "state");
			}

			public BuildExecutionOutcome BuildContentUpdate(BuildTarget target, string stateFilePath) {
				ContentUpdateBuildCalls++;
				return new BuildExecutionOutcome(true, "success", "output", "state");
			}

			public BuildLayoutCaptureResult CaptureBuildLayout(
				string operationDirectory,
				long buildStartedUtcTicks,
				BuildTarget target) => new BuildLayoutCaptureResult(ContentBuildStatus.Success, "captured", "layout");

			public BuildReceiptCreationResult CreateEditorCompatibleReceipt(
				BuildJobRecord record,
				BuildExecutionOutcome outcome,
				BuildTarget target) {
				ReceiptCalls++;
				return new BuildReceiptCreationResult(true, "receipt", "receipt.json");
			}

			public void WriteOperationReport(BuildJobRecord record) { }
		}

		private sealed class FakeStore : IBuildJobStore {
			private readonly string m_root;

			internal FakeStore(string root) {
				m_root = root;
			}

			internal BuildJobRecord Record { get; set; }
			internal int SaveCalls { get; private set; }
			internal int ClearLegacyCalls { get; private set; }
			internal int InvalidArchiveCalls { get; private set; }
			public string CurrentPath => Path.Combine(m_root, "current.json");
			public bool Exists => Record != null;

			public string CreateOperationDirectory(string jobId) {
				var path = Path.Combine(m_root, jobId);
				Directory.CreateDirectory(path);
				return path;
			}

			public bool TryLoad(out BuildJobRecord record, out string error) {
				record = Record;
				error = record == null ? "missing" : string.Empty;
				return record != null;
			}

			public void Save(BuildJobRecord record) {
				SaveCalls++;
				Record = record;
			}

			public void DeleteCurrent() => Record = null;

			public string Archive(BuildJobRecord record) => Path.Combine(m_root, record.jobId + "-archive.json");

			public string ArchiveInvalidCurrent(string reason) {
				InvalidArchiveCalls++;
				return Path.Combine(m_root, "invalid-archive.json");
			}

			public void ClearLegacySessionState() => ClearLegacyCalls++;
		}

		private sealed class FakeCliApi : IAddressablesBuildCliApi {
			internal bool PreflightValid { get; set; }
			internal int AnalyzeCalls { get; private set; }
			internal int StartCalls { get; private set; }
			internal ContentBuildRequest LastAnalyzed { get; private set; }
			internal ContentBuildRequest LastStarted { get; private set; }

			public ContentBuildPreflight Analyze(ContentBuildRequest request) {
				AnalyzeCalls++;
				LastAnalyzed = request;
				return new ContentBuildPreflight(
					request,
					new[] { BuildTarget.StandaloneWindows64 },
					PreflightValid
						? Array.Empty<ContentBuildDiagnostic>()
						: new[] { Error(ContentBuildDiagnosticCode.InvalidRequest, "Blocking preflight") },
					"request-hash",
					"settings-guid",
					"settings-hash",
					string.Empty,
					"2.7.6");
			}

			public ContentBuildResult Start(ContentBuildRequest request) {
				StartCalls++;
				LastStarted = request;
				return new ContentBuildResult(
					"job",
					ContentBuildStatus.Success,
					"success",
					Array.Empty<ContentBuildItemResult>(),
					Array.Empty<ContentBuildDiagnostic>(),
					string.Empty,
					string.Empty,
					false);
			}

			public ContentBuildResult Resume() => Start(ContentBuildRequest.Full(ContentBuildPlatform.Windows));
			public ContentBuildResult Cancel() => Resume();
			public ContentBuildResult Restore() => Resume();
			public ContentBuildResult AbandonReset() => Resume();
			public ExistingBuildValidation ValidateExistingBuild() => Validation();
			public ExistingBuildValidation SelectExistingBuild(bool confirmed) => Validation();

			private static ExistingBuildValidation Validation() => new ExistingBuildValidation(
				true,
				"receipt.json",
				BuildTarget.StandaloneWindows64,
				Array.Empty<ContentBuildDiagnostic>());
		}
	}

	internal sealed class PhaseFiveAddressablesBuildIntegrationTests {
		private string m_root;
		private AddressableAssetSettings m_settings;
		private AddressableAssetSettings m_originalDefaultSettings;
		private bool m_createdDefaultFolder;
		private bool m_runIntegration;

		[SetUp]
		public void SetUp() {
			m_runIntegration = Environment.GetCommandLineArgs().Contains("-torCleanInstall");
			if (!m_runIntegration) return;

			m_originalDefaultSettings = AddressableAssetSettingsDefaultObject.SettingsExists
				? AddressableAssetSettingsDefaultObject.GetSettings(false)
				: null;
			m_root = "Assets/__TorProductionPhase5Build_" + Guid.NewGuid().ToString("N");
			Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(m_root)), Is.Not.Empty);
			Assert.That(AssetDatabase.CreateFolder(m_root, "Content"), Is.Not.Empty);
			m_settings = AddressableAssetSettings.Create(
				m_root + "/AddressableAssetsData", "AddressableAssetSettings", true, true);
			Assert.That(m_settings, Is.Not.Null);

			if (!AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder)) {
				m_createdDefaultFolder = true;
				Assert.That(AssetDatabase.CreateFolder("Assets", "AddressableAssetsData"), Is.Not.Empty);
			}
			AddressableAssetSettingsDefaultObject.Settings = m_settings;
			m_settings.BuildRemoteCatalog = true;
			m_settings.OverridePlayerVersion = "phase-five-integration";

			var assetPath = m_root + "/Content/Fixture.asset";
			AssetDatabase.CreateAsset(new TextAsset("phase-five-build"), assetPath);
			Assert.That(m_settings.CreateOrMoveEntry(
				AssetDatabase.AssetPathToGUID(assetPath), m_settings.DefaultGroup, false, false), Is.Not.Null);
			m_settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		[TearDown]
		public void TearDown() {
			if (!m_runIntegration) return;
			if (m_settings != null) {
				try {
					AddressableAssetSettings.CleanPlayerContent();
				} catch (Exception exception) {
					Debug.LogWarning("Phase 5 integration cleanup warning: " + exception.Message);
				}
			}

			if (m_originalDefaultSettings != null) {
				AddressableAssetSettingsDefaultObject.Settings = m_originalDefaultSettings;
			} else {
				AddressableAssetSettingsDefaultObject.Settings = null;
				EditorBuildSettings.RemoveConfigObject(AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName);
			}
			if (m_createdDefaultFolder &&
			    AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder)) {
				AssetDatabase.DeleteAsset(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
			}
			if (!string.IsNullOrEmpty(m_root) && AssetDatabase.IsValidFolder(m_root)) {
				AssetDatabase.DeleteAsset(m_root);
			}
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		[Test]
		[Category("CleanInstall")]
		[Timeout(300000)]
		public void PublicFullAndContentUpdateApis_BuildAnIsolatedFixture() {
			if (!m_runIntegration) {
				Assert.Pass("Real Addressables builds run only inside the isolated clean-install harness.");
			}
			var backend = new UnityContentBuildBackend();
			var target = backend.ActiveTarget;
			Assert.That(backend.IsTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target), Is.True);
			Assert.That(backend.PlayerDataBuilderValid, Is.True);

			var full = backend.BuildFull(target);
			Assert.That(full.Succeeded, Is.True, full.Message);
			Assert.That(full.ContentStatePath, Is.Not.Empty);
			Assert.That(File.Exists(full.ContentStatePath), Is.True, full.ContentStatePath);

			var stateValidation = backend.ValidateContentState(full.ContentStatePath, target);
			Assert.That(stateValidation.IsValid, Is.True,
				string.Join(" | ", stateValidation.Diagnostics.Select(item => item.Message)));
			Assert.That(
				backend.CheckContentUpdateRestrictions(full.ContentStatePath, target)
					.Any(item => item.Severity == ContentBuildDiagnosticSeverity.Error),
				Is.False);

			var update = backend.BuildContentUpdate(target, full.ContentStatePath);
			Assert.That(update.Succeeded, Is.True, update.Message);
		}
	}
}
