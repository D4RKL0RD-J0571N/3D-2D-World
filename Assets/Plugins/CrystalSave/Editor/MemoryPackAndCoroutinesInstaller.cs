#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
    [InitializeOnLoad]
    [DefaultExecutionOrder(-250)]
    sealed partial class MemoryPackAndCoroutinesInstaller
    {
        /*─────────────────────────────  folders / consts  ───────────────────────────*/
        const string MemPkgRoot       = "Packages/com.memorypack.runtime";
        const string MemRuntimeDir    = MemPkgRoot + "/Runtime";
        const string MemEditorDir     = MemPkgRoot + "/Editor";

        // New plugin folder for BouncyCastle
        const string BcPluginDir      = "Assets/Plugins/BouncyCastle";

        const string JsonPackage = "com.unity.nuget.newtonsoft-json";
        const string CoroutinesPackage = "com.unity.editorcoroutines";
        const string UnityAiAssistantPackage = "com.unity.ai.assistant";

        // Unity AI Assistant ships an editor-only, explicitly referenced Unsafe.dll.
        // When MemoryPack also supplies its runtime copy, Unity's duplicate resolver can
        // select the AI copy globally while omitting it from predefined assemblies.
        // The AI package exposes this define constraint so another package can provide
        // the shared assembly instead. This define must be present BEFORE AI Assistant
        // is registered; otherwise its first compile fails before our reload callback
        // gets a chance to add the define.
        const string ExcludeAiAssistantUnsafeDefine = "EXCLUDE_COMPILER_SERVICES_UNSAFE";

        /*─────────────────────────────  NuGet maps  ────────────────────────────────*/
        static readonly (string id,string ver,string zip,string file)[] RuntimeDlls =
        {
            ("MemoryPack.Core",                        "1.21.4", "lib/netstandard2.1/MemoryPack.Core.dll",                         "MemoryPack.Core.dll"),
#if !UNITY_6000_5_OR_NEWER
            // These assemblies are shipped with Unity 6.5+, so we only need them for older versions
            ("System.Collections.Immutable",           "6.0.0",  "lib/netstandard2.0/System.Collections.Immutable.dll",            "System.Collections.Immutable.dll"),
            ("System.Runtime.CompilerServices.Unsafe", "6.0.0",  "lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll",  "System.Runtime.CompilerServices.Unsafe.dll"),
#endif
        };

        static readonly (string id,string ver,string zip,string file,bool roslyn)[] EditorDlls =
        {
            ("MemoryPack.Generator",          "1.21.4", "analyzers/dotnet/cs/MemoryPack.Generator.dll",         "MemoryPack.Generator.dll",          true),
            ("Microsoft.CodeAnalysis.Common", "4.3.1",  "lib/netstandard2.0/Microsoft.CodeAnalysis.dll",       "Microsoft.CodeAnalysis.dll",        false),
            ("Microsoft.CodeAnalysis.CSharp", "4.3.1",  "lib/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll","Microsoft.CodeAnalysis.CSharp.dll", false),
#if !UNITY_6000_5_OR_NEWER
            // This assembly is shipped with Unity 6.5+, so we only need it for older versions
            ("System.Reflection.Metadata",    "6.0.0",  "lib/netstandard2.0/System.Reflection.Metadata.dll",   "System.Reflection.Metadata.dll",    false),
#endif
        };

        // BouncyCastle metadata
        static readonly (string id,string ver,string zip,string file) BouncyCastleDll =
            ("BouncyCastle.Cryptography", "2.6.1",
                "lib/net461/BouncyCastle.Cryptography.dll",
             "BouncyCastle.Cryptography.dll");  // must match assembly name

#pragma warning disable UDR0001 // Editor-only package-manager request cleared on assembly reload and play-mode transitions.
        static AddRequest? coroutinesAddRequest;
        static AddAndRemoveRequest? registryAddRequest;
#pragma warning restore UDR0001

        static bool IsBuildPlayerRunning => BuildPipeline.isBuildingPlayer;

        static MemoryPackAndCoroutinesInstaller()
        {
            EditorApplication.delayCall -= PromptIfNeeded;
#pragma warning disable UDR0001 // Editor-only installer callbacks are cleared by ClearStaticState on reload/play transitions.
            EditorApplication.delayCall += PromptIfNeeded;
            EditorApplication.update -= OnRegistryAddProgress;
            EditorApplication.update -= OnCoroutinesAddProgress;
            AssemblyReloadEvents.beforeAssemblyReload -= ClearStaticState;
            AssemblyReloadEvents.beforeAssemblyReload += ClearStaticState;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#pragma warning restore UDR0001
#if UNITY_2021_3_OR_NEWER
            // modern hook lives in BuildTargetChangeHook below
#else
            EditorUserBuildSettings.activeBuildTargetChanged -= OnBuildTargetChanged;
            EditorUserBuildSettings.activeBuildTargetChanged += OnBuildTargetChanged;
#endif
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ClearStaticState();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                ClearStaticState();
                EditorApplication.delayCall += PromptIfNeeded;
            }
        }

        static void ClearStaticState()
        {
            EditorApplication.delayCall -= PromptIfNeeded;
            EditorApplication.update -= OnRegistryAddProgress;
            EditorApplication.update -= OnCoroutinesAddProgress;
            coroutinesAddRequest = null;
            registryAddRequest = null;
        }

        [DidReloadScripts]
        static void OnScriptsReloaded()
        {
            if (IsBuildPlayerRunning) return;
            EnsureSharedAssemblyConflictDefines();
            RepairRuntimeMetas();
            EnsureUnityShippedAssembliesEnabled();
            PromptIfNeeded();
        }

        static void EnsureSharedAssemblyConflictDefines()
        {
            if (!File.Exists(Path.Combine(MemRuntimeDir, "System.Runtime.CompilerServices.Unsafe.dll")))
                return;

            BuildTargetGroup group =
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            if (group == BuildTargetGroup.Unknown)
                return;

            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
            string symbols = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            var symbolSet = new HashSet<string>(
                symbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);

            if (!symbolSet.Add(ExcludeAiAssistantUnsafeDefine))
                return;

            PlayerSettings.SetScriptingDefineSymbols(
                namedTarget,
                string.Join(";", symbolSet));

            Debug.Log(
                "<color=green>[Crystal Save]</color> Prepared the Unity AI Assistant / MemoryPack " +
                "System.Runtime.CompilerServices.Unsafe handoff. Crystal Save's runtime-enabled " +
                "copy will be shared if AI Assistant is installed now or later.");
        }

        static void RepairRuntimeMetas()
        {
            if (!Directory.Exists(MemRuntimeDir)) return;
            foreach (string dll in Directory.GetFiles(MemRuntimeDir, "*.dll"))
            {
                string meta = dll + ".meta";
                if (!File.Exists(meta)) continue;
                string txt = File.ReadAllText(meta);
                if (txt.Contains("DefaultImporter:") && !txt.Contains("PluginImporter:"))
                {
                    EnsurePluginImporter(meta);
                    Debug.Log($"<color=yellow>[Crystal Save]</color> Repaired meta for {Path.GetFileName(dll)} (DefaultImporter → PluginImporter)");
                }
            }
            DetectConflictingAssemblies();
        }

        /*──────────────  ensure Unity-shipped assemblies are runtime-enabled  ──────────────────*/

        // Assemblies that Crystal Save (via MemoryPack) needs at runtime.
        // On Unity 6.5+ we don't ship our own copies because Unity includes them,
        // but Unity may configure them as editor-only. We need them enabled for all platforms.
        static readonly string[] RuntimeRequiredAssemblies =
        {
            "System.Runtime.CompilerServices.Unsafe.dll",
            "System.Collections.Immutable.dll",
        };

        const string RuntimeEnabledCheckFlag = "CRYSTALSAVE_RUNTIME_ENABLED_CHECK";

        static void EnsureUnityShippedAssembliesEnabled()
        {
            if (SessionState.GetBool(RuntimeEnabledCheckFlag, false)) return;
            SessionState.SetBool(RuntimeEnabledCheckFlag, true);

            bool anyFixed = false;

            foreach (string assemblyName in RuntimeRequiredAssemblies)
            {
                // If Crystal Save ships its own copy (Unity < 6.5), don't enable third-party copies.
                // DetectConflictingAssemblies() disables duplicates; re-enabling them causes CS0234.
                if (File.Exists(Path.Combine(MemRuntimeDir, assemblyName)))
                    continue;

                // Find all copies — skip our own MemoryPack runtime dir (handled by RepairRuntimeMetas)
                var allCopies = new List<string>();
                foreach (string searchRoot in new[] { "Assets", "Packages", "Library/ScriptAssemblies" })
                {
                    if (!Directory.Exists(searchRoot)) continue;
                    allCopies.AddRange(Directory.GetFiles(searchRoot, assemblyName, SearchOption.AllDirectories));
                }

                foreach (string dllPath in allCopies)
                {
                    string normalized = dllPath.Replace('\\', '/');

                    // Skip our own copies — those are handled by RepairRuntimeMetas
                    if (normalized.StartsWith(MemRuntimeDir, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string meta = dllPath + ".meta";
                    if (!File.Exists(meta)) continue;
                    string txt = File.ReadAllText(meta);

                    if (!txt.Contains("PluginImporter:")) continue;

                    // Check if runtime is disabled (Any: enabled: 0) — we need it enabled
                    if (EnableRuntimeInMeta(meta, txt))
                    {
                        anyFixed = true;
                        Debug.Log(
                            $"<color=yellow>[Crystal Save]</color> Enabled runtime for {assemblyName} at " +
                            $"{normalized} — required by MemoryPack serialization.");
                    }
                }
            }

            if (anyFixed)
            {
                Debug.Log(
                    "<color=green>[Crystal Save]</color> Ensured shared .NET assemblies are runtime-enabled. " +
                    "MemoryPack requires these assemblies at runtime for serialization.");
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Checks a PluginImporter .meta for the "Any" platform entry and ensures enabled: 1.
        /// Returns true if the file was modified.
        /// </summary>
        static bool EnableRuntimeInMeta(string metaPath, string txt)
        {
            // Look for the pattern:
            //   - first:
            //       Any:         (with optional trailing space/text)
            //     second:
            //       enabled: 0
            // and change enabled: 0 to enabled: 1
            var match = Regex.Match(txt,
                @"(-\s*first:\s*\n\s*Any:\s*\n\s*second:\s*\n\s*enabled:\s*)0",
                RegexOptions.Multiline);

            if (!match.Success) return false; // Already enabled or not found

            string patched = txt.Substring(0, match.Index)
                           + match.Groups[1].Value + "1"
                           + txt.Substring(match.Index + match.Length);

            File.WriteAllText(metaPath, patched);
            return true;
        }

        /*──────────────────────  duplicate / misconfigured DLL detection  ──────────────────────*/

        static readonly string[] ConflictProneAssemblies =
        {
            "System.Runtime.CompilerServices.Unsafe.dll",
            "System.Collections.Immutable.dll",
            "MemoryPack.Core.dll",
        };

        const string ConflictResolvedFlag = "CRYSTALSAVE_CONFLICT_RESOLVED";

        static void DetectConflictingAssemblies()
        {
            if (SessionState.GetBool(ConflictResolvedFlag, false)) return;

            EnsureSharedAssemblyConflictDefines();
            bool anyResolved = false;

            foreach (string assemblyName in ConflictProneAssemblies)
            {
                // Find all copies across Assets/ and Packages/
                var copies = new List<string>();
                if (Directory.Exists("Assets"))
                    copies.AddRange(Directory.GetFiles("Assets", assemblyName, SearchOption.AllDirectories));
                if (Directory.Exists("Packages"))
                    copies.AddRange(Directory.GetFiles("Packages", assemblyName, SearchOption.AllDirectories));

                if (copies.Count <= 1) continue;

                // Identify our copy and third-party copies
                var thirdParty = new List<string>();
                bool weHaveOurs = false;

                foreach (string dllPath in copies)
                {
                    bool isOurs = dllPath.Replace('\\', '/').StartsWith(MemRuntimeDir, StringComparison.OrdinalIgnoreCase);
                    if (isOurs)
                        weHaveOurs = true;
                    else
                        thirdParty.Add(dllPath);
                }

                if (!weHaveOurs || thirdParty.Count == 0) continue;

                // Disable third-party duplicates so only our runtime-enabled copy is active
                foreach (string dllPath in thirdParty)
                {
                    string normalizedPath = dllPath.Replace('\\', '/');
                    if (assemblyName == "System.Runtime.CompilerServices.Unsafe.dll"
                        && normalizedPath.IndexOf(
                            "/" + UnityAiAssistantPackage + "/",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Do not mutate a Package Manager package. Its importer has
                        // !EXCLUDE_COMPILER_SERVICES_UNSAFE specifically for this handoff.
                        continue;
                    }

                    string meta = dllPath + ".meta";
                    if (!File.Exists(meta)) continue;
                    string txt = File.ReadAllText(meta);

                    if (DisableDllInMeta(meta, txt))
                    {
                        anyResolved = true;
                        Debug.Log(
                            $"<color=yellow>[Crystal Save]</color> Disabled duplicate {assemblyName} at " +
                            $"{dllPath.Replace('\\', '/')} — Crystal Save's copy will be used instead.");
                    }
                }
            }

            SessionState.SetBool(ConflictResolvedFlag, true);

            if (anyResolved)
            {
                Debug.Log(
                    "<color=green>[Crystal Save]</color> Resolved assembly conflicts. " +
                    "Third-party duplicates of shared .NET assemblies have been disabled to prevent compile errors. " +
                    "Crystal Save's copies (enabled for all platforms) will serve both editor and runtime needs.");
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Rewrites a PluginImporter .meta to set enabled: 0 on all platforms.
        /// Returns true if the file was modified.
        /// </summary>
        static bool DisableDllInMeta(string metaPath, string txt)
        {
            if (!txt.Contains("PluginImporter:")) return false;

            int pluginIdx = txt.IndexOf("PluginImporter:", StringComparison.Ordinal);
            string afterPlugin = txt.Substring(pluginIdx);
            if (!afterPlugin.Contains("enabled: 1")) return false;

            // Replace all "enabled: 1" with "enabled: 0" after the PluginImporter section
            string before = txt.Substring(0, pluginIdx);
            string after = afterPlugin.Replace("enabled: 1", "enabled: 0");
            string patched = before + after;

            if (patched == txt) return false;

            File.WriteAllText(metaPath, patched);
            return true;
        }

        [MenuItem("Tools/Crystal Save/Project/Install Dependencies", false, 2000)]
        static void MenuInstallDependencies()
        {
            if (IsBuildPlayerRunning)
            {
                Debug.LogWarning("[Crystal Save] Install Dependencies is blocked while Build Player is running.");
                return;
            }

            ShowConsentDialog(forcePrompt: true);
        }

        [MenuItem("Tools/Crystal Save/Project/Install Dependencies", true)]
        static bool ValidateMenuInstallDependencies() => !IsBuildPlayerRunning && DependenciesMissing;

        const string PromptFlag = "REMEMBERME_DEP_PROMPTED";

        static bool DependenciesMissing
        {
            get
            {
                if (IsBuildPlayerRunning) return false;

                bool memMissing  = !File.Exists(Path.Combine(MemPkgRoot, "package.json"));
                bool coroMissing = !IsEditorCoroutinesInstalled();
                bool needsBc     = NeedsBouncyCastle;
                bool hasPlugin   = File.Exists(Path.Combine(BcPluginDir, BouncyCastleDll.file));
                bool bcMissing   = needsBc && !hasPlugin;
                return memMissing || coroMissing || bcMissing || !ManifestDeclaresPackage(JsonPackage);
            }
        }

        static bool IsEditorCoroutinesInstalled()
        {
            if (File.Exists($"Packages/{CoroutinesPackage}/package.json"))
                return true;

            if (ManifestDeclaresPackage(CoroutinesPackage))
                return true;

            if (PackagesLockContainsPackage(CoroutinesPackage))
                return true;

            return PackageCacheContainsPackage(CoroutinesPackage);
        }

        static bool ManifestDeclaresPackage(string packageId)
        {
            const string manifestPath = "Packages/manifest.json";
            if (!File.Exists(manifestPath)) return false;

            string text = File.ReadAllText(manifestPath);
            return ContainsJsonObjectKey(text, packageId);
        }

        static bool PackagesLockContainsPackage(string packageId)
        {
            const string lockPath = "Packages/packages-lock.json";
            if (!File.Exists(lockPath)) return false;

            string text = File.ReadAllText(lockPath);
            return ContainsJsonObjectKey(text, packageId);
        }

        static bool ContainsJsonObjectKey(string jsonText, string key)
        {
            return Regex.IsMatch(
                jsonText,
                $"\"{Regex.Escape(key)}\"\\s*:",
                RegexOptions.CultureInvariant);
        }

        static bool PackageCacheContainsPackage(string packageId)
        {
            const string packageCacheRoot = "Library/PackageCache";
            if (!Directory.Exists(packageCacheRoot)) return false;

            return Directory
                .EnumerateDirectories(packageCacheRoot, packageId + "@*", SearchOption.TopDirectoryOnly)
                .Any();
        }

        static bool NeedsBouncyCastle =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL
         || EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneLinux64;

        static void PromptIfNeeded()
        {
            if (IsBuildPlayerRunning)                    return;
            if (SessionState.GetBool(PromptFlag, false)) return;
            if (!DependenciesMissing)                     return;
            SessionState.SetBool(PromptFlag, true);
            ShowConsentDialog(forcePrompt: false);
        }

        static void ShowConsentDialog(bool forcePrompt)
        {
            if (IsBuildPlayerRunning) return;

            bool memMissing  = !File.Exists(Path.Combine(MemPkgRoot, "package.json"));
            bool coroMissing = !IsEditorCoroutinesInstalled();
            bool needsBc     = NeedsBouncyCastle;
            bool hasPlugin   = File.Exists(Path.Combine(BcPluginDir, BouncyCastleDll.file));
            bool bcMissing   = needsBc && !hasPlugin;

            bool jsonMissing = !ManifestDeclaresPackage(JsonPackage);
            if (!memMissing && !coroMissing && !bcMissing && !jsonMissing) return;

            string msg = "Crystal Save requires the following free dependencies:\n\n";
            if (memMissing)
                msg += "• MemoryPack 1.21.4 (nuget.org – MIT)\n" +
                       "    – MemoryPack.Core & Generator\n" +
                       "    – System.Collections.Immutable / Unsafe\n";
            if (coroMissing)
                msg += "• Unity Editor Coroutines 1.x (Unity Registry)\n";
            if (bcMissing)
                msg += "• BouncyCastle 2.6.1 (nuget.org – MIT) – will install as a plugin under Assets/Plugins/BouncyCastle for AES-GCM on WebGL & Linux\n";
            if (jsonMissing) msg += "• Unity Newtonsoft JSON 3.2.2 (legacy save bundle reader)\n";
            msg += "\nInstall now?";

            if (EditorUtility.DisplayDialog("Install required dependencies?", msg, "Install", "Cancel"))
            {
                if (memMissing)  InstallMemoryPack();
                if (coroMissing || jsonMissing) InstallRegistryDependencies();
                if (bcMissing)   InstallBouncyCastle();
            }
            else if (!forcePrompt)
            {
                Debug.LogWarning("[Crystal Save] Dependencies not installed. " +
                                 "Run Tools ▸ Crystal Save ▸ Install Dependencies to add them.");
            }
        }

        static void InstallMemoryPack()
        {
            if (IsBuildPlayerRunning) return;

            try
            {
                Directory.CreateDirectory(MemRuntimeDir);
                Directory.CreateDirectory(MemEditorDir);

                foreach (var (id, v, p, f)    in RuntimeDlls)            ExtractNuGet(id, v, p, Path.Combine(MemRuntimeDir,  f), false, isPlugin: true);
                foreach (var (id, v, p, f, r) in EditorDlls)             ExtractNuGet(id, v, p, Path.Combine(MemEditorDir, f), r);

                File.WriteAllText(Path.Combine(MemPkgRoot, "package.json"),
@"{
  ""name"": ""com.memorypack.runtime"",
  ""version"": ""1.21.4"",
  ""displayName"": ""MemoryPack Runtime"",
  ""description"": ""MemoryPack runtime & Roslyn generator (embedded)"",
  ""unity"": ""2022.3"",
  ""author"": { ""name"": ""Cysharp"" }
}");
                EnsureSharedAssemblyConflictDefines();
                Debug.Log("<color=green>[Dependency]</color> MemoryPack 1.21.4 installed.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dependency] MemoryPack install failed:\n" + ex);
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        static void InstallRegistryDependencies()
        {
            // One Package Manager request avoids concurrent package mutations.
            var packages = new List<string>();
            if (!IsEditorCoroutinesInstalled()) packages.Add(CoroutinesPackage);
            if (!ManifestDeclaresPackage(JsonPackage)) packages.Add(JsonPackage + "@3.2.2");
            if (packages.Count == 0 || registryAddRequest != null && !registryAddRequest.IsCompleted) return;
            registryAddRequest = Client.AddAndRemove(packages.ToArray());
            EditorApplication.update -= OnRegistryAddProgress;
            EditorApplication.update += OnRegistryAddProgress;
        }

        static void OnRegistryAddProgress()
        {
            if (registryAddRequest == null || !registryAddRequest.IsCompleted) return;
            EditorApplication.update -= OnRegistryAddProgress;
            if (registryAddRequest.Status == StatusCode.Success) Debug.Log("[Dependency] Registry dependencies installed.");
            else Debug.LogError("[Dependency] Registry dependency installation failed: " + registryAddRequest.Error?.message);
            registryAddRequest = null;
        }

        static void InstallEditorCoroutines()
        {
            if (IsBuildPlayerRunning) return;

            coroutinesAddRequest = Client.Add(CoroutinesPackage);
#pragma warning disable UDR0001 // Editor-only package-manager progress callback is removed by ClearStaticState and after completion.
            EditorApplication.update -= OnCoroutinesAddProgress;
            EditorApplication.update += OnCoroutinesAddProgress;
#pragma warning restore UDR0001
        }

        static void OnCoroutinesAddProgress()
        {
            if (coroutinesAddRequest is null || !coroutinesAddRequest.IsCompleted) return;
            EditorApplication.update -= OnCoroutinesAddProgress;
            if (coroutinesAddRequest.Status == StatusCode.Success)
                Debug.Log("<color=green>[Dependency]</color> Unity Editor Coroutines installed.");
            else
                Debug.LogError($"[Dependency] Failed to add {CoroutinesPackage}: {coroutinesAddRequest.Error.message}");
        }

        static void InstallBouncyCastle()
        {
            if (IsBuildPlayerRunning) return;

            try
            {
                // ── Remove any old embedded package ────────────────────
                const string oldPkg = "Packages/com.bouncycastle.runtime";
                if (Directory.Exists(oldPkg))
                {
                    Directory.Delete(oldPkg, true);
                    var meta = oldPkg + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                    Debug.Log("<color=yellow>[Dependency]</color> Removed old BouncyCastle package from Packages/");
                }

                // ── Create plugin folder ──────────────────────────────
                Directory.CreateDirectory(BcPluginDir);

                // 1) Extract the correctly-named DLL
                string dst = Path.Combine(BcPluginDir, BouncyCastleDll.file);
                ExtractNuGet(BouncyCastleDll.id, BouncyCastleDll.ver, BouncyCastleDll.zip, dst, false, isPlugin: true);

                // 2) Write link.xml to preserve it for IL2CPP/WebGL
                File.WriteAllText(Path.Combine(BcPluginDir, "link.xml"),
@"<linker>
  <assembly fullname=""BouncyCastle.Cryptography"">
    <type fullname=""Org.BouncyCastle.Crypto.*"" preserve=""all"" />
  </assembly>
</linker>");

                Debug.Log("<color=green>[Dependency]</color> BouncyCastle 2.6.1 installed as plugin under Assets/Plugins/BouncyCastle.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dependency] BouncyCastle install failed:\n" + ex);
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        static void ExtractNuGet(string id, string ver, string zipPath, string dst, bool roslynLabel, bool isPlugin = false)
        {
            string nupkg = DownloadNuGet(id, ver);
            using var zip = ZipFile.OpenRead(nupkg);
            var entry = zip.GetEntry(zipPath.Replace('\\','/'))
                        ?? throw new Exception($"Path {zipPath} not found in {id} {ver}");
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            entry.ExtractToFile(dst, true);

            string meta = dst + ".meta";
            if (!File.Exists(meta)) WriteMeta(meta, roslynLabel, isPlugin);
            else if (isPlugin)      EnsurePluginImporter(meta);
            else if (roslynLabel)   EnsureRoslynLabel(meta);
            else if (RepairBareLabels(meta))
                Debug.Log($"<color=yellow>[Crystal Save]</color> Repaired meta labels for {Path.GetFileName(dst)}");
        }

        static string DownloadNuGet(string id, string ver)
        {
            string tmp = Path.Combine(Path.GetTempPath(), $"{id}.{ver}.nupkg");
            if (File.Exists(tmp)) return tmp;

            string url = $"https://www.nuget.org/api/v2/package/{id}/{ver}";
            try
            {
                // Run the blocking download on a worker so the editor thread can keep repainting progress.
                Task downloadTask = Task.Run(() =>
                {
                    using var wc = new WebClient();
                    wc.DownloadFile(url, tmp);
                });

                double start = EditorApplication.timeSinceStartup;
                while (!downloadTask.Wait(100))
                {
                    float pulse = 0.1f + Mathf.PingPong((float)(EditorApplication.timeSinceStartup - start) * 0.35f, 0.8f);
                    EditorUtility.DisplayProgressBar("Installing Dependencies", $"Downloading {id} v{ver}\u2026", pulse);
                }

                downloadTask.GetAwaiter().GetResult(); // propagate any exception
            }
            catch
            {
                if (File.Exists(tmp)) try { File.Delete(tmp); } catch { }
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return tmp;
        }

        static void WriteMeta(string path, bool roslyn, bool isPlugin = false)
        {
            string guid = Guid.NewGuid().ToString("N");
            if (isPlugin)
            {
                WritePluginMeta(path, guid);
            }
            else
            {
                File.WriteAllText(path,
$"fileFormatVersion: 2\nguid: {guid}\n{(roslyn ? "labels:\n- RoslynAnalyzer\n" : "")}DefaultImporter:\n  externalObjects: {{ }}\n  userData: \n  assetBundleName: \n  assetBundleVariant: ");
            }
        }

        static void WritePluginMeta(string path, string guid)
        {
            File.WriteAllText(path,
$@"fileFormatVersion: 2
guid: {guid}
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 1
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any: 
    second:
      enabled: 1
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  userData: 
  assetBundleName: 
  assetBundleVariant: ");
        }

        static void EnsurePluginImporter(string metaPath)
        {
            string txt = File.ReadAllText(metaPath);
            if (txt.Contains("PluginImporter:")) return;

            // Preserve existing GUID
            string guid = Guid.NewGuid().ToString("N");
            var match = System.Text.RegularExpressions.Regex.Match(txt, @"guid:\s*([a-f0-9]+)");
            if (match.Success) guid = match.Groups[1].Value;

            WritePluginMeta(metaPath, guid);
        }

        static void EnsureRoslynLabel(string metaPath)
        {
            string txt = File.ReadAllText(metaPath);
            if (!txt.Contains("RoslynAnalyzer"))
            {
                txt = txt.Replace("labels:", "labels:\n- RoslynAnalyzer");
                File.WriteAllText(metaPath, txt);
            }
        }

        // A bare "labels:" line serializes as null, which Unity rejects with
        // "Expected sequence value for string array" during asset import.
        // Remove it; labelled sequences are left untouched.
        static bool RepairBareLabels(string metaPath)
        {
            string txt = File.ReadAllText(metaPath);
            string repaired = System.Text.RegularExpressions.Regex.Replace(
                txt, @"(?m)^labels:[ \t]*\r?\n(?![ \t]*-)", string.Empty);
            if (repaired == txt) return false;
            File.WriteAllText(metaPath, repaired);
            return true;
        }

        internal static void OnBuildTargetChanged()
        {
            if (IsBuildPlayerRunning) return;

            EnsureSharedAssemblyConflictDefines();
            SessionState.SetBool(PromptFlag, false); // allow re-prompt
            PromptIfNeeded();
        }
    }
}

#if UNITY_2021_3_OR_NEWER
namespace Arawn.CrystalSave.Editor
{
    sealed class BuildTargetChangeHook : UnityEditor.Build.IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;
        public void OnActiveBuildTargetChanged(BuildTarget prev, BuildTarget next) =>
            MemoryPackAndCoroutinesInstaller.OnBuildTargetChanged();
    }
}
#endif
