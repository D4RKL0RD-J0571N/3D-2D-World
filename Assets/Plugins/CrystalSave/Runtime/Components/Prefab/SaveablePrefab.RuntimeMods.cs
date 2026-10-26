#if MEMORYPACK && ARAWN_REMEMBERME
// SaveablePrefab.RuntimeMods.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Globalization;
using System.Reflection;
using System;
#if UNITY_EDITOR
using UnityEditorInternal;
#endif

namespace Arawn.CrystalSave.Runtime
{
	public partial class SaveablePrefab
	{
        private static readonly HashSet<SaveablePrefab> PendingComponentBlobOwnershipRefreshes = new();
        private static bool componentBlobOwnershipInitializationHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetComponentBlobOwnershipRouting()
        {
            SaveManager.Initialized -= HandleSaveManagerInitializedForComponentBlobOwnership;
            PendingComponentBlobOwnershipRefreshes.Clear();
            componentBlobOwnershipInitializationHooked = false;
        }

        /// <summary>
        /// Returns the nearest SaveablePrefab boundary for an ISaveable helper.
        /// A nested prefab whose blob tracking is disabled still forms a boundary,
        /// preventing an outer prefab from capturing or restoring the nested helper.
        /// </summary>
        internal static SaveablePrefab FindNearestComponentBlobOwner(ISaveable helper)
        {
            if (!(helper is Component component) || component == null)
                return null;

            return component.GetComponentInParent<SaveablePrefab>(true);
        }

        private bool OwnsComponentBlob(ISaveable helper)
        {
            ComponentManager manager = ComponentManager.Instance;
            if (manager != null)
                return manager.IsComponentBlobOwnedBy(helper, this);

            return EffectiveTrackComponentBlobs &&
                   FindNearestComponentBlobOwner(helper) == this;
        }

        private static bool CanParticipateInComponentBlob(ISaveable helper)
        {
            if (helper == null ||
                helper is MonoBehaviour helperBehaviour && helperBehaviour == null)
            {
                return false;
            }

            return helper is not SaveableComponent saveableComponent ||
                   !saveableComponent.RuntimeSaveRegistrationSuppressed;
        }

        private void RequestComponentBlobOwnershipRefresh()
        {
            if (!Application.isPlaying)
                return;

            componentBlobOwnershipRefreshPending = true;

            SaveManager manager = SaveManager.Instance;
            ComponentManager componentManager = manager != null ? manager.ComponentManager : null;
            if (componentManager == null)
            {
                QueueComponentBlobOwnershipRefreshUntilInitialized(this);
                return;
            }

            RemovePendingComponentBlobOwnershipInitialization(this);
            componentManager.RequestPrefabComponentOwnershipRefresh(this);
        }

        private static void QueueComponentBlobOwnershipRefreshUntilInitialized(SaveablePrefab prefab)
        {
            if (prefab == null)
                return;

            PendingComponentBlobOwnershipRefreshes.RemoveWhere(item => item == null);
            PendingComponentBlobOwnershipRefreshes.Add(prefab);

            if (componentBlobOwnershipInitializationHooked)
                return;

            SaveManager.Initialized += HandleSaveManagerInitializedForComponentBlobOwnership;
            componentBlobOwnershipInitializationHooked = true;
        }

        private static void HandleSaveManagerInitializedForComponentBlobOwnership(SaveManager manager)
        {
            SaveManager.Initialized -= HandleSaveManagerInitializedForComponentBlobOwnership;
            componentBlobOwnershipInitializationHooked = false;

            if (PendingComponentBlobOwnershipRefreshes.Count == 0)
                return;

            var pending = PendingComponentBlobOwnershipRefreshes.ToArray();
            PendingComponentBlobOwnershipRefreshes.Clear();
            foreach (SaveablePrefab prefab in pending)
            {
                if (prefab != null)
                    prefab.RequestComponentBlobOwnershipRefresh();
            }
        }

        private static void RemovePendingComponentBlobOwnershipInitialization(SaveablePrefab prefab)
        {
            if (prefab != null)
                PendingComponentBlobOwnershipRefreshes.Remove(prefab);

            PendingComponentBlobOwnershipRefreshes.RemoveWhere(item => item == null);
            if (PendingComponentBlobOwnershipRefreshes.Count != 0 ||
                !componentBlobOwnershipInitializationHooked)
            {
                return;
            }

            SaveManager.Initialized -= HandleSaveManagerInitializedForComponentBlobOwnership;
            componentBlobOwnershipInitializationHooked = false;
        }

        internal void CompleteComponentBlobOwnershipRefresh()
        {
            componentBlobOwnershipRefreshPending = false;
            componentBlobOwnershipEffectiveOverrideActive = false;
            RemovePendingComponentBlobOwnershipInitialization(this);
        }

        internal void BeginApplyingComponentBlobOwnershipRefresh()
        {
            componentBlobOwnershipEffectiveOverrideActive = false;
        }

        public byte[] CaptureRuntimeModifications()
        {
            Logger.Log($"[SaveablePrefab] CaptureRuntimeModifications starting for '{gameObject.name}'", LogCategory.SaveablePrefab, LogLevel.Info);
            
            var mods = new RuntimeModificationData();
            bool optimize = SaveManager.Instance?.SaveSettings?.optimizeRuntimeCapture ?? false;

	    /* 0️⃣ root name/tag/layer ------------------------------------ */
	    string origName  = originalPrefabAsset ? originalPrefabAsset.name  : initialName;
	    string origTag   = originalPrefabAsset ? originalPrefabAsset.tag   : initialTag;
	    int    origLayer = originalPrefabAsset ? originalPrefabAsset.layer : initialLayer;

	    bool nameDiff  = gameObject.name != origName;
	    bool tagDiff   = gameObject.tag  != origTag;
	    bool layerDiff = gameObject.layer != origLayer;

            RootStateOverride rootState = null;

            if (nameDiff)
            {
                rootState ??= new RootStateOverride();
                rootState.Name = gameObject.name;
            }

            if (tagDiff)
            {
                rootState ??= new RootStateOverride();
                rootState.Tag = gameObject.tag;
            }

            if (layerDiff)
            {
                rootState ??= new RootStateOverride();
                rootState.Layer = gameObject.layer;
            }

            bool activeDiff = gameObject.activeSelf != initialActiveState;
            if (activeDiff)
            {
                rootState ??= new RootStateOverride();
                rootState.ActiveSelf = gameObject.activeSelf;
            }

            if (rootState != null)
            {
                mods.RootState = rootState;
            }

            // Early return optimization - but always process component blobs if enabled
            // to prevent load inconsistencies when skipSavingWhenUnchanged is active
            if (optimize &&
                mods.RootState == null &&
                !trackSkinnedMeshOverrides &&
                !trackBlendshapeOverrides &&
                !trackTextureOverrides &&
                !trackMaterialOverrides &&
                !trackAddedComponents &&
                !trackParticleSnapshots &&
                !EffectiveTrackComponentBlobs &&
                !trackChildStateOverrides &&
                !trackChildTransformOverrides)
            {
                return null;
            }

	    /* 1️⃣ runtime-added components ──────────────────────────────── */
	    // NOTE: trackAddedComponents is checked alone (not && isAddedAtRuntime)
	    // to allow Scenario 1 (prefab variations) where the prefab was placed
	    // in the scene at edit-time but components are added at runtime.
	    if (trackAddedComponents)
	    {
	        var assetTypes = originalPrefabAsset
	            ? new HashSet<Type>(originalPrefabAsset
	                .GetComponents<Component>()
	                .Where(c => c != null)
	                .Select(c => c.GetType()))
	            : new HashSet<Type>();

	        Logger.Log($"[CaptureRuntimeMods] trackAddedComponents=true, originalPrefabAsset={(originalPrefabAsset ? originalPrefabAsset.name : "NULL")}, assetTypes.Count={assetTypes.Count}",
	                   LogCategory.SaveablePrefab, LogLevel.Info);

	        foreach (var c in GetComponents<Component>())
	        {
	            if (c == null) continue;
	            bool isNew = !assetTypes.Contains(c.GetType()) && IsRuntimeAdded(c);
	            Logger.Log($"[CaptureRuntimeMods] Component {c.GetType().Name}: isNew={isNew}",
	                       LogCategory.SaveablePrefab, LogLevel.Info);
	            if (!isNew) continue;

	            mods.AddedComponents.Add(new ComponentModification
	            {
	                ComponentTypeName = c.GetType().Name,
	                SerializedData    = SerializeComponent(c)
	            });
	        }
	        Logger.Log($"[CaptureRuntimeMods] Total AddedComponents captured: {mods.AddedComponents.Count}",
	                   LogCategory.SaveablePrefab, LogLevel.Info);
	    }

	    /* 2️⃣ meshes / materials / particles (single hierarchy walk) */
	    var stack = new Stack<(Transform tr, string path)>();
	    stack.Push((transform, string.Empty));

	    while (stack.Count > 0)
	    {
	        var (tr, path) = stack.Pop();

	        // mesh swaps ───────────────────────────────────────────────
	        // Note: We capture even when originalPrefabAsset is null (procedural objects)
	        // The CaptureMesh helper handles the comparison and will save all meshes as new
	        if (trackSkinnedMeshOverrides &&
	            tr.TryGetComponent(out SkinnedMeshRenderer s) &&
	            s.sharedMesh)
	        {
	            CaptureMesh(s.sharedMesh, path, typeof(SkinnedMeshRenderer));
	        }
	        else if (trackSkinnedMeshOverrides &&
	                tr.TryGetComponent(out MeshFilter mf) && mf.sharedMesh)
	        {
	            CaptureMesh(mf.sharedMesh, path, typeof(MeshFilter));
	        }

                if (trackBlendshapeOverrides &&
                    tr.TryGetComponent(out SkinnedMeshRenderer smrForBlend) &&
                    smrForBlend.sharedMesh)
                {
                    CaptureBlendshapes(smrForBlend, path);
                }

                // texture changes ──────────────────────────────────────────
                // Note: We capture even when originalPrefabAsset is null (procedural objects)
                if (trackTextureOverrides &&
                    tr.TryGetComponent(out Renderer rForTextures))
                {
                    CaptureTextures(rForTextures, path);
                }

                // material swaps ───────────────────────────────────────────
                // Note: We capture even when originalPrefabAsset is null (procedural objects)
                if (trackMaterialOverrides &&
                    tr.TryGetComponent(out Renderer r))
                {
                    CaptureMat(r, path);
                }

	        // particle snapshots ───────────────────────────────────────
	        if (trackParticleSnapshots &&
	            tr.TryGetComponent(out ParticleSystem ps) &&
	            (ps.time >= 0.01f || ps.isPlaying))
	        {
	            mods.ParticleSnapshots.Add(new ParticleSystemSnapshot
	            {
	                Path       = path,
	                Time       = ps.time,
	                WasPlaying = ps.isPlaying
	            });
	        }

	        // descend
	        for (int i = 0; i < tr.childCount; ++i)
	        {
	            var ch = tr.GetChild(i);
	            string p = string.IsNullOrEmpty(path) ? ch.name : $"{path}/{ch.name}";
	            stack.Push((ch, p));
	        }
	    }

            /* 3️⃣ helper-blob snapshot */
            // Always capture component blobs when trackComponentBlobs is enabled
            // The optimization flag should not prevent this as it can cause load inconsistencies
            if (EffectiveTrackComponentBlobs)
            {
                var capturedBlobSuffixes = new HashSet<string>(StringComparer.Ordinal);
                ComponentManager componentManager = ComponentManager.Instance;
                ISaveable[] helpers;
                if (componentManager == null ||
                    !componentManager.TryGetCollectionBlobHelpers(this, out helpers))
                {
                    helpers = GetComponentsInChildren<ISaveable>(
                        includeInactive: true);
                }

                foreach (var helper in helpers)
                {
                    if (!CanParticipateInComponentBlob(helper))
                        continue;

                    componentManager?.ObservePrefabBlobHelper(helper);
                    if (!OwnsComponentBlob(helper))
                        continue;

                    using var captureOutcome = ComponentOperationScope.Capture(helper.UniqueIdentifier, "Prefab blob " + UniqueID);
                    byte[] bytes;
                    if (helper is SaveableComponent saveableComponent)
                    {
                        bytes = SaveableComponent.InvokeSaveDataWithRoute(
                            saveableComponent,
                            SaveableComponent.SaveDataCallRoute.PrefabComponentBlob,
                            this);
                    }
                    else
                    {
                        bytes = helper.SaveData();
                    }

                    if (bytes == null || bytes.Length == 0) continue;

                    string helperID = helper.UniqueIdentifier;
                    int separatorIndex = helperID.IndexOf('_');
                    string suffix = separatorIndex >= 0
                        ? helperID.Substring(separatorIndex + 1)
                        : helperID;

                    if (!capturedBlobSuffixes.Add(suffix))
                    {
                        Logger.Log(
                            $"SaveablePrefab: Duplicate component blob suffix '{suffix}' while capturing '{name}'. " +
                            "Only one blob can restore to a given Component ID; regenerate one duplicated Component ID.",
                            LogCategory.SaveablePrefab,
                            LogLevel.Warning);
                    }

                    mods.ComponentBlobs.Add(new ComponentBlob(suffix, bytes));
                }
            }

            /* 4⃩ gather live children & removals via observers */
            ReconcileRemovedChildren();
            if (!(optimize && !trackChildStateOverrides && !trackChildTransformOverrides))
            {
                Logger.Log($"[SaveablePrefab] Processing child states for '{gameObject.name}', trackChildStateOverrides={trackChildStateOverrides}", LogCategory.SaveablePrefab, LogLevel.Info);
                
                int childCount = 0;
                foreach (var path in SaveablePrefabUtil.GetAllDescendants(transform))
                {
                    if (string.IsNullOrEmpty(path)) continue;

                    Transform tr = transform.Find(path);
                    if (!tr) continue;
                    if (tr.GetComponent<SaveablePrefab>() != null) continue;

                    // Defensive: ensure the GameObject hasn't been destroyed mid-iteration
                    try
                    {
                        if (tr.gameObject == null) continue;
                        if (trackChildStateOverrides) TrackChild(tr, path);
                        
                        mods.ChildStates.Add(new ChildStateOverride
                        {
                            Guid            = tr.name,
                            Path            = path,
                            Exists          = true,
                            ActiveWhenSaved = tr.gameObject.activeSelf,
                            Tag             = tr.tag,
                            Layer           = tr.gameObject.layer,
                            Position        = trackChildTransformOverrides ? tr.localPosition : (Vector3?)null,
                            Rotation        = trackChildTransformOverrides ? tr.localRotation : (Quaternion?)null,
                            Scale           = trackChildTransformOverrides ? tr.localScale : (Vector3?)null
                        });
                        childCount++;
                    }
                    catch (MissingReferenceException)
                    {
                        // GameObject was destroyed mid-iteration, skip it
                        Logger.Log($"[SaveablePrefab] MissingReferenceException for child at path '{path}'", LogCategory.SaveablePrefab, LogLevel.Warning);
                        continue;
                    }
                    catch (System.Exception ex)
                    {
                        ComponentOperationScope.Record(ex);
                        // Continue collecting independent children; the owning operation rejects incomplete data.
                        Logger.Log($"[SaveablePrefab] Exception processing child at path '{path}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveablePrefab, LogLevel.Error);
                        continue;
                    }
                }
                
                Logger.Log($"[SaveablePrefab] Processed {childCount} child states for '{gameObject.name}'", LogCategory.SaveablePrefab, LogLevel.Info);

                // Keep tombstones across captures (including rejected saves). Only a different
                // live child at that path supersedes a removal; Destroy is deferred by Unity.
                foreach (var deletedPath in _removedPaths.ToArray())
                {
                    var liveChild = transform.Find(deletedPath);
                    removedPathSources.TryGetValue(deletedPath, out var removedChild);
                    if (liveChild && !ReferenceEquals(liveChild, removedChild))
                    {
                        _removedPaths.Remove(deletedPath);
                        removedPathSources.Remove(deletedPath);
                        continue;
                    }
                    mods.ChildStates.Add(new ChildStateOverride
                    {
                        Guid   = null,
                        Path   = deletedPath,
                        Exists = false
                    });
                }
            }


            /* 🔌 turn child-state tracking off when no related toggle is on */
            if (!trackChildStateOverrides && !trackChildTransformOverrides)
            {
                mods.ChildStates.Clear();   // discard gathered overrides
            }

	    /* 6️⃣ early-out */
            bool empty =
                mods.AddedComponents.Count     == 0 &&
                mods.MeshOverrides.Count       == 0 &&
                mods.MaterialOverrides.Count   == 0 &&
                mods.BlendshapeOverrides.Count == 0 &&
                mods.TextureOverrides.Count    == 0 &&
                mods.ParticleSnapshots.Count   == 0 &&
                mods.ChildStates.Count         == 0 &&
                mods.ComponentBlobs.Count      == 0 &&
                mods.RootState == null;

            Logger.Log($"[SaveablePrefab] CaptureRuntimeModifications complete for '{gameObject.name}', empty={empty}", LogCategory.SaveablePrefab, LogLevel.Info);

	    return empty ? null : SaveDataSerializer.Instance.Serialize(mods);

	    /* ───────────── local capture helpers ───────────── */
	    void CaptureMesh(Mesh live, string p, Type rendererType)
	    {
	        Mesh orig = null;
	        if (rendererType == typeof(SkinnedMeshRenderer))
	            orig = FindInOriginal<SkinnedMeshRenderer>(p)?.sharedMesh;
	        else if (rendererType == typeof(MeshFilter))
	            orig = FindInOriginal<MeshFilter>(p)?.sharedMesh;

	        if (live == orig) return;

	        var mo = new MeshOverride { Path = p, MeshName = SavedAssetReferenceResolver.GetSaveKey(live) };
	#if UNITY_EDITOR
	#if UNITY_6000_0_OR_NEWER
	        if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(live, out var g, out _))
	            mo.MeshGUID = g;
	#else
	        if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(live, out var g, out long _))
	            mo.MeshGUID = g;
	#endif
	#endif
	        mods.MeshOverrides.Add(mo);
	    }

            void CaptureMat(Renderer rend, string p)
            {
                var r0   = FindInOriginal<Renderer>(p);
                var live = rend.sharedMaterials;
                var orig = r0 ? r0.sharedMaterials : null;

                for (int i = 0; i < live.Length; ++i)
                {
                    var lm = live[i];
                    var om = (orig != null && i < orig.Length) ? orig[i] : null;
                    if (lm == om) continue;

                    var mmo = new MaterialOverride
                    {
                        Path         = p,
                        SlotIndex    = i,
                        MaterialName = SavedAssetReferenceResolver.GetSaveKey(lm)
                    };
        #if UNITY_EDITOR
        #if UNITY_6000_0_OR_NEWER
                    if (lm && UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(lm, out var g, out _))
                        mmo.MaterialGUID = g;
        #else
                    if (lm && UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(lm, out var g, out long _))
                        mmo.MaterialGUID = g;
        #endif
        #endif
                    mods.MaterialOverrides.Add(mmo);
                }
            }

            void CaptureBlendshapes(SkinnedMeshRenderer liveSmr, string p)
            {
                Mesh liveMesh = liveSmr.sharedMesh;
                if (!liveMesh || liveMesh.blendShapeCount == 0) return;

                List<BlendshapeWeight> overrides = null;
                SkinnedMeshRenderer origSmr = originalPrefabAsset ? FindInOriginal<SkinnedMeshRenderer>(p) : null;
                Mesh origMesh = origSmr ? origSmr.sharedMesh : null;

                int count = liveMesh.blendShapeCount;
                for (int i = 0; i < count; ++i)
                {
                    float liveWeight = liveSmr.GetBlendShapeWeight(i);
                    float baseline = 0f;

                    if (origSmr && origMesh && origMesh.blendShapeCount > i)
                    {
                        baseline = origSmr.GetBlendShapeWeight(i);
                    }

                    if (Mathf.Abs(liveWeight - baseline) <= 0.01f)
                        continue;

                    overrides ??= new List<BlendshapeWeight>();
                    overrides.Add(new BlendshapeWeight
                    {
                        Index = i,
                        Weight = liveWeight
                    });
                }

                if (overrides == null || overrides.Count == 0) return;

                mods.BlendshapeOverrides.Add(new BlendshapeOverride
                {
                    Path = p,
                    Weights = overrides
                });
            }

            void CaptureTextures(Renderer liveRenderer, string p)
            {
                Logger.Log($"[SaveablePrefab] CaptureTextures called for renderer '{liveRenderer.name}' at path '{p}'", LogCategory.SaveablePrefab, LogLevel.Info);
                
                Renderer origRenderer = FindInOriginal<Renderer>(p);
                
                // CRITICAL FIX: Use materials (instances) for live renderer to capture runtime changes
                // But use sharedMaterials for original prefab to ensure we get the asset baseline
                var liveMaterials = liveRenderer.materials;  // ✅ Gets instances if they exist
                var origMaterials = origRenderer ? origRenderer.sharedMaterials : null;  // ✅ MUST use sharedMaterials for baseline!

                Logger.Log($"[SaveablePrefab] Texture capture: Live uses materials (instances), Orig uses sharedMaterials (assets) - Live count: {liveMaterials.Length}, Orig count: {(origMaterials != null ? origMaterials.Length : 0)}", LogCategory.SaveablePrefab, LogLevel.Info);

                // Common texture property names to check
                string[] textureProperties = new string[]
                {
                    "_MainTex",
                    "_BumpMap",
                    "_MetallicGlossMap",
                    "_OcclusionMap",
                    "_EmissionMap",
                    "_ParallaxMap",
                    "_DetailAlbedoMap",
                    "_DetailNormalMap"
                };

                for (int matIdx = 0; matIdx < liveMaterials.Length; ++matIdx)
                {
                    Material liveMat = liveMaterials[matIdx];
                    if (!liveMat) continue;

                    Material origMat = (origMaterials != null && matIdx < origMaterials.Length) 
                                       ? origMaterials[matIdx] 
                                       : null;

                Logger.Log($"[SaveablePrefab] Texture capture: Checking material slot {matIdx}", LogCategory.SaveablePrefab, LogLevel.Info);
                Logger.Log($"[SaveablePrefab] Texture capture:   Live Mat: '{liveMat.name}' (ID: {UnityObjectHelper.GetUniqueId(liveMat)})", LogCategory.SaveablePrefab, LogLevel.Info);
                Logger.Log($"[SaveablePrefab] Texture capture:   Orig Mat: '{(origMat ? origMat.name : "null")}' (ID: {(origMat ? UnityObjectHelper.GetUniqueId(origMat) : 0)})", LogCategory.SaveablePrefab, LogLevel.Info);
                Logger.Log($"[SaveablePrefab] Texture capture:   Same reference? {(liveMat == origMat)}", LogCategory.SaveablePrefab, LogLevel.Info);                    // CRITICAL FIX: If live and orig materials are the same reference,
                    // it means the prefab asset has material instances (corrupted prefab).
                    // We CANNOT capture valid texture changes in this case because:
                    // 1. Both liveMat and origMat point to the same object
                    // 2. When you change the texture at runtime, it changes in BOTH
                    // 3. So we always see the "changed" texture as the "original"
                    // 4. We must skip this material and let RememberMaterial handle it
                    bool isInstanceMaterial = liveMat.name.EndsWith(" (Instance)");
                    bool cannotCompare = (liveMat == origMat);
                    
                    if (cannotCompare)
                    {
                        Logger.Log($"[SaveablePrefab] Texture capture: Live and Orig materials are SAME REFERENCE (ID: {UnityObjectHelper.GetUniqueId(liveMat)})! This indicates a corrupted prefab asset with material instances. Skipping this material - use RememberMaterial component to save texture changes on material instances.", LogCategory.SaveablePrefab, LogLevel.Warning);
                        continue;  // Skip this material entirely - cannot capture valid data
                    }
                    else if (isInstanceMaterial)
                    {
                        Logger.Log($"[SaveablePrefab] Texture capture: Material is instance - will capture ALL texture changes", LogCategory.SaveablePrefab, LogLevel.Info);
                    }

                    List<TextureProperty> changedTextures = null;

                    foreach (string propName in textureProperties)
                    {
                    if (!liveMat.HasProperty(propName)) continue;

                    Texture liveTex = liveMat.GetTexture(propName);
                    Texture origTex = origMat ? origMat.GetTexture(propName) : null;

                    Logger.Log($"[SaveablePrefab] Texture capture:   Property '{propName}': Live='{(liveTex ? liveTex.name : "null")}' (ID:{(liveTex ? UnityObjectHelper.GetUniqueIdSafe(liveTex) : 0)}), Orig='{(origTex ? origTex.name : "null")}' (ID:{(origTex ? UnityObjectHelper.GetUniqueIdSafe(origTex) : 0)}), Same? {(liveTex == origTex)}", LogCategory.SaveablePrefab, LogLevel.Info);                        // Capture if:
                        // 1. Material is an instance (always capture instance changes), OR
                        // 2. Textures are different (normal comparison)
                        // Note: cannotCompare case is now skipped entirely earlier in the loop
                        bool shouldCapture = isInstanceMaterial || (liveTex != origTex);

                        if (!shouldCapture)
                        {
                            Logger.Log($"[SaveablePrefab] Texture capture:   → Skipping '{propName}' - textures are identical", LogCategory.SaveablePrefab, LogLevel.Info);
                            continue;
                        }

                        // Only log null textures if we're capturing them
                        if (!liveTex)
                        {
                            Logger.Log($"[SaveablePrefab] Texture capture:   → Skipping '{propName}' - live texture is null", LogCategory.SaveablePrefab, LogLevel.Info);
                            continue;
                        }

                        Logger.Log($"[SaveablePrefab] Texture capture:   → CAPTURING '{propName}' - {(isInstanceMaterial ? "instance material" : "textures differ")}!", LogCategory.SaveablePrefab, LogLevel.Info);

                        // Capture the changed texture
                        changedTextures ??= new List<TextureProperty>();
                        
                    var texProp = new TextureProperty
                    {
                        PropertyName = propName,
                        TextureName = liveTex ? SavedAssetReferenceResolver.GetSaveKey(liveTex) : string.Empty,
                        TextureInstanceID = liveTex ? UnityObjectHelper.GetUniqueIdSafe(liveTex) : 0
                    };

#if UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
                        if (liveTex && UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(liveTex, out var g, out _))
                        {
                            texProp.TextureGUID = g;
                            Logger.Log($"[SaveablePrefab] Texture capture: Captured GUID '{g}' for texture '{liveTex.name}'", LogCategory.SaveablePrefab, LogLevel.Info);
                        }
#else
                        if (liveTex && UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(liveTex, out var g, out long _))
                        {
                            texProp.TextureGUID = g;
                            Logger.Log($"[SaveablePrefab] Texture capture: Captured GUID '{g}' for texture '{liveTex.name}'", LogCategory.SaveablePrefab, LogLevel.Info);
                        }
#endif
#endif
                        changedTextures.Add(texProp);
                    }

                    if (changedTextures != null && changedTextures.Count > 0)
                    {
                        Logger.Log($"[SaveablePrefab] Texture capture: Captured {changedTextures.Count} texture changes for material slot {matIdx} at path '{p}'", LogCategory.SaveablePrefab, LogLevel.Info);
                        mods.TextureOverrides.Add(new TextureOverride
                        {
                            Path = p,
                            MaterialSlot = matIdx,
                            Textures = changedTextures
                        });
                    }
                }
            }
        }

	public void ApplyRuntimeModifications(byte[] bytes)
	{
	    if (bytes == null || bytes.Length == 0) return;
            using var restoreOutcome = new ComponentOperationScope(false, UniqueID, "Prefab modifications", restoration: ComponentManager.Instance?.Restoration);

	    if (Logger.ShouldLog(LogCategory.SaveablePrefab, LogLevel.Info)) Logger.Log($"[SaveablePrefab] ApplyRuntimeModifications called for '{gameObject.name}'", LogCategory.SaveablePrefab, LogLevel.Info);

	    var mods = SaveDataSerializer.Instance.Deserialize<RuntimeModificationData>(bytes);
            if (mods == null) return;

	    mods.AddedComponents   ??= new();
            mods.MeshOverrides     ??= new();
            mods.MaterialOverrides ??= new();
            mods.BlendshapeOverrides ??= new();
            mods.TextureOverrides  ??= new();
	    mods.ParticleSnapshots ??= new();
	    mods.ChildStates       ??= new();
            if (trackChildStateOverrides || trackChildTransformOverrides)
            {
                _removedPaths.Clear();
                removedPathSources.Clear();
                foreach (var child in mods.ChildStates)
                {
                    var live = FindInSelf<Transform>(child.Path);
                    if (!child.Exists && trackChildStateOverrides) NotifyChildDestroyed(child.Path, live);
                    // Create authored children before building the component-blob lookup.
                    if (!live && child.Exists && trackChildStateOverrides) RestoreAuthoredChild(child.Path);
                }
            }
	    mods.ComponentBlobs    ??= new();

            /* 0️⃣ root restore -------------------------------------------- */
            bool? targetActiveState = null;
            if (mods.RootState != null)
            {
                if (!string.IsNullOrEmpty(mods.RootState.Name))
                    gameObject.name = mods.RootState.Name;

                if (mods.RootState.Layer is >= 0 and < 32)
                    gameObject.layer = mods.RootState.Layer.Value;

#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(mods.RootState.Tag) && InternalEditorUtility.tags.Contains(mods.RootState.Tag))
                    gameObject.tag = mods.RootState.Tag;
#else
                if (!string.IsNullOrEmpty(mods.RootState.Tag))
                    gameObject.tag = mods.RootState.Tag;
#endif

                if (mods.RootState.ActiveSelf.HasValue)
                    targetActiveState = mods.RootState.ActiveSelf.Value;
            }

		/* 1️⃣ added components (guarded) */
		if (Logger.ShouldLog(LogCategory.SaveablePrefab, LogLevel.Info)) Logger.Log($"[ApplyRuntimeMods] trackAddedComponents={trackAddedComponents}, " +
		           $"AddedComponents.Count={mods.AddedComponents?.Count ?? 0}",
		           LogCategory.SaveablePrefab, LogLevel.Info);
		if (trackAddedComponents)
		{
			// Sort components to handle Unity's dependency requirements:
			// - MeshFilter must be added before MeshRenderer
			// - Rigidbody should be added before joints, etc.
			var sortedComponents = mods.AddedComponents
				.OrderBy(c => GetComponentPriority(c.ComponentTypeName))
				.ToList();

			foreach (var cMod in sortedComponents)
			{
				var t = SaveablePrefabUtil.FindByName(cMod.ComponentTypeName);
				if (t == null)
				{
					Logger.Log($"[SaveablePrefab] Could not find type '{cMod.ComponentTypeName}' when restoring added components.",
					           LogCategory.SaveablePrefab, LogLevel.Warning);
					continue;
				}

				var existing = gameObject.GetComponent(t);
				Component c = existing;
				if (c == null)
				{
					try
					{
						c = gameObject.AddComponent(t);
					}
					catch (System.Exception ex)
					{
						Logger.Log($"[SaveablePrefab] AddComponent exception for '{cMod.ComponentTypeName}': {ex.Message}\nStack Trace: {ex.StackTrace}",
						           LogCategory.SaveablePrefab, LogLevel.Error);
					}
				}
				
				if (c == null)
				{
					Logger.Log($"[SaveablePrefab] Failed to add component '{cMod.ComponentTypeName}' to '{gameObject.name}'. " +
					           $"The component may require dependencies or cannot be added at runtime.",
					           LogCategory.SaveablePrefab, LogLevel.Warning);
					continue;
				}

				string previousIdentifier = null;
				if (c is ISaveable saveableBefore)
				{
					previousIdentifier = saveableBefore.UniqueIdentifier;
				}

				DeserializeComponent(c, cMod.SerializedData);

				if (c is ISaveable saveableAfter)
				{
					string currentIdentifier = saveableAfter.UniqueIdentifier;
					if (!string.Equals(previousIdentifier, currentIdentifier, StringComparison.Ordinal))
					{
						SaveManager.Instance?.ComponentManager?.ReindexComponentIdentifier(saveableAfter, previousIdentifier);
					}
				}
			}
		}

		/* 2️⃣ mesh overrides (guarded) */
		if (trackSkinnedMeshOverrides)
		{
			foreach (var mo in mods.MeshOverrides)
			{
				// ← swapped lookup order here too
				SkinnedMeshRenderer smr = FindInSelf<SkinnedMeshRenderer>(mo.Path);
				MeshFilter mf           = (smr != null)
										  ? null
										  : FindInSelf<MeshFilter>(mo.Path);
				if (smr == null && mf == null) continue;

				Mesh mesh = null;
#if UNITY_EDITOR
				if (!string.IsNullOrEmpty(mo.MeshGUID))
				{
					string p = UnityEditor.AssetDatabase.GUIDToAssetPath(mo.MeshGUID);
					mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(p);
				}
#endif
				if (mesh == null && !string.IsNullOrEmpty(mo.MeshName))
					mesh = SavedAssetReferenceResolver.Load<Mesh>(mo.MeshName, "SaveablePrefab mesh override");
				if (!mesh) continue;

				if (smr != null) smr.sharedMesh = mesh;
				else           mf.sharedMesh  = mesh;
			}
		}

                /* 3️⃣ blendshape overrides (guarded) */
                if (trackBlendshapeOverrides)
                {
                        foreach (var bo in mods.BlendshapeOverrides)
                        {
                                if (bo?.Weights == null || bo.Weights.Count == 0) continue;

                                SkinnedMeshRenderer smr = FindInSelf<SkinnedMeshRenderer>(bo.Path);
                                if (!smr || !smr.sharedMesh) continue;

                                int blendCount = smr.sharedMesh.blendShapeCount;
                                foreach (var weight in bo.Weights)
                                {
                                        if (weight == null) continue;
                                        if (weight.Index < 0 || weight.Index >= blendCount) continue;
                                        smr.SetBlendShapeWeight(weight.Index, weight.Weight);
                                }
                        }
                }

                /* 3️⃣.5️⃣ texture overrides (guarded) */
                if (trackTextureOverrides)
                {
                        foreach (var to in mods.TextureOverrides)
                        {
                                if (to?.Textures == null || to.Textures.Count == 0) continue;

                                Renderer rend = FindInSelf<Renderer>(to.Path);
                                if (!rend)
                                {
                                        Logger.Log($"[SaveablePrefab] Texture restore: Renderer not found at path '{to.Path}'", LogCategory.SaveablePrefab, LogLevel.Warning);
                                        continue;
                                }

                                // Check if RememberMaterial exists on this renderer - if so, defer to it
                                var rememberMaterial = rend.GetComponent<RememberMaterial>();
                                if (rememberMaterial != null && 
                                    (rememberMaterial.RememberMainTexture || rememberMaterial.RememberAdditionalTextures))
                                {
                                        Logger.Log($"[SaveablePrefab] Texture restore: Skipping '{rend.name}' - RememberMaterial has authority", LogCategory.SaveablePrefab, LogLevel.Info);
                                        Logger.Log($"SaveablePrefab: Skipping texture restoration for '{rend.name}' - RememberMaterial has authority.", LogCategory.SaveablePrefab, LogLevel.Info);
                                        continue;  // Skip this renderer, RememberMaterial will handle it
                                }

                                Logger.Log($"[SaveablePrefab] Texture restore: Processing renderer '{rend.name}' at path '{to.Path}'", LogCategory.SaveablePrefab, LogLevel.Info);

                                // CRITICAL FIX: Use materials (instances) to match capture behavior
                                // This ensures we restore to the same material instances we captured from
                                var mats = rend.materials;  // ✅ Gets/creates instances
                                if (to.MaterialSlot >= mats.Length)
                                {
                                        Logger.Log($"[SaveablePrefab] Texture restore: Material slot {to.MaterialSlot} out of range (count: {mats.Length})", LogCategory.SaveablePrefab, LogLevel.Warning);
                                        continue;
                                }

                                Material mat = mats[to.MaterialSlot];
                                if (!mat)
                                {
                                        Logger.Log($"[SaveablePrefab] Texture restore: Material at slot {to.MaterialSlot} is null", LogCategory.SaveablePrefab, LogLevel.Warning);
                                        continue;
                                }

                            Logger.Log($"[SaveablePrefab] Texture restore: Material '{mat.name}' (InstanceID: {UnityObjectHelper.GetUniqueId(mat)}) at slot {to.MaterialSlot}", LogCategory.SaveablePrefab, LogLevel.Info);

                            Logger.Log($"[SaveablePrefab] Texture restore: Material '{mat.name}' (InstanceID: {UnityObjectHelper.GetUniqueId(mat)}) at slot {to.MaterialSlot}", LogCategory.SaveablePrefab, LogLevel.Info);                                foreach (var texProp in to.Textures)
                                {
                                        if (texProp == null) continue;

                                        Logger.Log($"[SaveablePrefab] Texture restore: Property '{texProp.PropertyName}' - InstanceID: {texProp.TextureInstanceID}, Name: '{texProp.TextureName}'", LogCategory.SaveablePrefab, LogLevel.Info);

                                        Texture tex = null;
                                        
                                        // Try loading by instance ID first (for material instances with non-Resources textures)
                                        if (texProp.TextureInstanceID != 0)
                                        {
                                                Logger.Log($"[SaveablePrefab] Texture restore: Attempting Instance ID lookup for ID {texProp.TextureInstanceID}...", LogCategory.SaveablePrefab, LogLevel.Info);
                                                UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(Texture));
                                                Logger.Log($"[SaveablePrefab] Texture restore: Found {objects.Length} total Texture objects in memory", LogCategory.SaveablePrefab, LogLevel.Info);
                                                
                                                foreach (var obj in objects)
                                                {
                                                        								if (obj != null && UnityObjectHelper.GetUniqueId(obj) == texProp.TextureInstanceID)
                                                        {
                                                                tex = obj as Texture;
                                                                if (tex != null)
                                                                {
                                                                        Logger.Log($"[SaveablePrefab] Texture restore: Found texture by Instance ID: '{tex.name}' (ID: {UnityObjectHelper.GetUniqueId(tex)})", LogCategory.SaveablePrefab, LogLevel.Info);
                                                                        break;
                                                                }
                                                        }
                                                }
                                                
                                                if (tex == null)
                                                {
                                                        Logger.Log($"[SaveablePrefab] Texture restore: ✗ Instance ID {texProp.TextureInstanceID} not found in memory", LogCategory.SaveablePrefab, LogLevel.Warning);
                                                }
                                        }
                                        
                                        // Fall back to GUID-based loading (Editor)
#if UNITY_EDITOR
                                        if (tex == null && !string.IsNullOrEmpty(texProp.TextureGUID))
                                        {
                                                Logger.Log($"[SaveablePrefab] Texture restore: Attempting GUID lookup for '{texProp.TextureGUID}'...", LogCategory.SaveablePrefab, LogLevel.Info);
                                                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(texProp.TextureGUID);
                                                if (!string.IsNullOrEmpty(path))
                                                {
                                                        tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(path);
                                                        if (tex != null)
                                                        {
                                                                Logger.Log($"[SaveablePrefab] Texture restore: ✓ Found texture by GUID: '{tex.name}' at path '{path}'", LogCategory.SaveablePrefab, LogLevel.Info);
                                                        }
                                                }
                                        }
#endif
                                        // Fall back to name-based loading
                                        if (tex == null && !string.IsNullOrEmpty(texProp.TextureName))
                                        {
                                                Logger.Log($"[SaveablePrefab] Texture restore: Attempting name-based lookup for '{texProp.TextureName}'...", LogCategory.SaveablePrefab, LogLevel.Info);
                                                tex = SavedAssetReferenceResolver.Load<Texture>(texProp.TextureName, "SaveablePrefab texture override");
                                                if (tex != null)
                                                {
                                                        Logger.Log($"[SaveablePrefab] Texture restore: ✓ Found texture by name: '{tex.name}'", LogCategory.SaveablePrefab, LogLevel.Info);
                                                }
                                        }

                                        if (tex && mat.HasProperty(texProp.PropertyName))
                                        {
                                                Logger.Log($"[SaveablePrefab] Texture restore: ✓ Setting texture '{tex.name}' to property '{texProp.PropertyName}' on material '{mat.name}'", LogCategory.SaveablePrefab, LogLevel.Info);
                                                mat.SetTexture(texProp.PropertyName, tex);
                                        }
                                        else if (tex == null)
                                        {
                                                Logger.Log($"[SaveablePrefab] Texture restore: ✗ Could not load texture for property '{texProp.PropertyName}' (InstanceID: {texProp.TextureInstanceID}, Name: '{texProp.TextureName}')", LogCategory.SaveablePrefab, LogLevel.Warning);
                                        }
                                        else if (!mat.HasProperty(texProp.PropertyName))
                                        {
                                                Logger.Log($"[SaveablePrefab] Texture restore: ✗ Material '{mat.name}' does not have property '{texProp.PropertyName}'", LogCategory.SaveablePrefab, LogLevel.Warning);
                                        }
                                }

                                // Re-apply the material array to ensure changes persist
                                Logger.Log($"[SaveablePrefab] Texture restore: Re-applying materials array (instances) to '{rend.name}'", LogCategory.SaveablePrefab, LogLevel.Info);
                                rend.materials = mats;  // ✅ Assign instances back
                        }
                }

                /* 4️⃣ material overrides (guarded) */
                if (trackMaterialOverrides)
                {
			foreach (var mm in mods.MaterialOverrides)
			{
				Renderer rend = FindInSelf<Renderer>(mm.Path);
				if (!rend) continue;

				Material mat = null;
#if UNITY_EDITOR
				if (!string.IsNullOrEmpty(mm.MaterialGUID))
				{
					string p = UnityEditor.AssetDatabase.GUIDToAssetPath(mm.MaterialGUID);
					mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p);
				}
#endif
				if (mat == null && !string.IsNullOrEmpty(mm.MaterialName))
					mat = SavedAssetReferenceResolver.Load<Material>(mm.MaterialName, "SaveablePrefab material override");
				if (!mat) continue;

				var arr = rend.sharedMaterials;
				if (mm.SlotIndex < arr.Length)
				{
					arr[mm.SlotIndex] = mat;
					rend.sharedMaterials = arr;
				}
			}
		}

                /* 5️⃣ particle rewind (guarded) */
		if (trackParticleSnapshots)
		{
			foreach (var snap in mods.ParticleSnapshots)
			{
				var ps = FindInSelf<ParticleSystem>(snap.Path);
				if (!ps) continue;

				ps.Simulate(snap.Time, true, true, true);
				if (snap.WasPlaying) ps.Play();
			}
		}

                /* 6️⃣ helper blobs (guarded) */
			if (EffectiveTrackComponentBlobs && mods.ComponentBlobs.Count > 0)
		{
			var helperLookup = new PrefabComponentBlobLookup(this,
                    h => CanParticipateInComponentBlob(h) && OwnsComponentBlob(h));
                bool logInfo = Logger.ShouldLog(LogCategory.SaveablePrefab, LogLevel.Info);
                foreach (var blob in mods.ComponentBlobs)
			{
				var helper = helperLookup.Find(blob.GuidSuffix);
				if (helper == null)
				{
					Logger.Log($"SaveablePrefab: ISaveable with GUID suffix '{blob.GuidSuffix}' not found under '{name}'.", LogCategory.SaveablePrefab, LogLevel.Warning);
					continue;
				}

				try
				{
					if (logInfo) Logger.Log($"SaveablePrefab: Calling LoadData for helper '{helper.UniqueIdentifier}' (suffix: '{blob.GuidSuffix}') via prefab blob restoration on prefab '{name}'.", LogCategory.SaveablePrefab, LogLevel.Info);
                    var manager = SaveManager.Instance?.ComponentManager;
                    if (manager != null) manager.RestoreComponent(helper, blob.Payload);
                    else ComponentOperationScope.Restore(helper, blob.Payload, null, null, null);
				}
				catch (Exception ex)
				{
					Logger.Log($"SaveablePrefab: error applying blob to '{blob.GuidSuffix}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveablePrefab, LogLevel.Error);
				}
			}
		}


                /* 7️⃣ child restore (guarded per feature) */
                if (trackChildStateOverrides || trackChildTransformOverrides)
                {
                        foreach (var cs in mods.ChildStates)
                        {
                                Transform t = FindInSelf<Transform>(cs.Path);

                                        if (!t && !string.IsNullOrEmpty(cs.Guid))
                                        {
                                        var uid = GetComponentsInChildren<UniqueID>(true)
                                                          .FirstOrDefault(u => u.ID == cs.Guid);
                                        t = uid ? uid.transform : null;
                                }

                                if (!t) continue;

                                if (!cs.Exists)
                                {
                                        if (trackChildStateOverrides)
                                        {
                                                if (SaveManager.Instance != null)
                                                        SaveManager.Instance.DestroyWithSnapshot(t.gameObject);
                                                else
                                                        Destroy(t.gameObject);
                                        }
                                        continue;
                                }

				if (trackChildStateOverrides)
				{
					t.gameObject.SetActive(cs.ActiveWhenSaved);

					if (cs.Layer is >= 0 and < 32) t.gameObject.layer = cs.Layer.Value;

#if UNITY_EDITOR
					if (!string.IsNullOrEmpty(cs.Tag) && InternalEditorUtility.tags.Contains(cs.Tag))
						t.gameObject.tag = cs.Tag;
#else
					if (!string.IsNullOrEmpty(cs.Tag))
						t.gameObject.tag = cs.Tag;
#endif
				}

				if (trackChildTransformOverrides)
				{
					if (cs.Position.HasValue)
						t.localPosition = cs.Position.Value;
					if (cs.Rotation.HasValue)
						t.localRotation = cs.Rotation.Value;
					if (cs.Scale.HasValue)
						t.localScale = cs.Scale.Value;
                                }
                        }
                }

                if (targetActiveState.HasValue && gameObject.activeSelf != targetActiveState.Value)
                        gameObject.SetActive(targetActiveState.Value);
        }

        private T FindInOriginal<T>(string path) where T : Component
        {
	    if (!originalPrefabAsset) return null;
	    
	    // Handle root path (empty string) - look on the root of the original prefab
	    if (string.IsNullOrEmpty(path))
	    {
	        return originalPrefabAsset.GetComponent<T>();
	    }
	    
	    Transform tr = originalPrefabAsset.transform.Find(path);
	    return tr ? tr.GetComponent<T>() : null;
	}

        private Transform RestoreAuthoredChild(string path)
        {
            if (!originalPrefabAsset || string.IsNullOrEmpty(path)) return null;
            var source = originalPrefabAsset.transform.Find(path);
            if (!source || source.GetComponent<SaveablePrefab>()) return null;
            int separator = path.LastIndexOf('/');
            string parentPath = separator < 0 ? null : path.Substring(0, separator);
            var parent = parentPath == null ? transform : transform.Find(parentPath) ?? RestoreAuthoredChild(parentPath);
            if (!parent) return null;
            var child = Instantiate(source.gameObject, parent, false).transform;
            child.name = source.name;
            AttachDestroyObserversRecursively(parent);
            return child;
        }

	private T FindInSelf<T>(string path) where T : Component
	{
	    Transform tr = string.IsNullOrEmpty(path) ? transform : transform.Find(path);
	    return tr ? tr.GetComponent<T>() : null;
	}

	private bool IsRuntimeAdded(Component c) =>
	    !(c is Transform) && !(c is SaveablePrefab) && !(c is UniqueID);

	/// <summary>
	/// Returns a priority value for component restoration order.
	/// Lower values are restored first. This handles Unity's component dependencies:
	/// - MeshFilter must exist before MeshRenderer
	/// - Rigidbody must exist before joints
	/// - Colliders should exist before Rigidbody for proper setup
	/// </summary>
	private static int GetComponentPriority(string componentTypeName)
	{
	    return componentTypeName switch
	    {
	        // Tier 0: Core mesh components (MeshFilter required by MeshRenderer)
	        "MeshFilter" => 0,
	        
	        // Tier 1: Renderers (depend on MeshFilter)
	        "MeshRenderer" => 10,
	        "SkinnedMeshRenderer" => 10,
	        "LineRenderer" => 10,
	        "TrailRenderer" => 10,
	        "SpriteRenderer" => 10,
	        
	        // Tier 2: Colliders (should exist before Rigidbody for proper setup)
	        "BoxCollider" => 20,
	        "SphereCollider" => 20,
	        "CapsuleCollider" => 20,
	        "MeshCollider" => 20,
	        "BoxCollider2D" => 20,
	        "CircleCollider2D" => 20,
	        "PolygonCollider2D" => 20,
	        "EdgeCollider2D" => 20,
	        "CapsuleCollider2D" => 20,
	        
	        // Tier 3: Physics bodies
	        "Rigidbody" => 30,
	        "Rigidbody2D" => 30,
	        "ArticulationBody" => 30,
	        
	        // Tier 4: Joints (depend on Rigidbody)
	        "FixedJoint" => 40,
	        "HingeJoint" => 40,
	        "SpringJoint" => 40,
	        "CharacterJoint" => 40,
	        "ConfigurableJoint" => 40,
	        "FixedJoint2D" => 40,
	        "HingeJoint2D" => 40,
	        "SpringJoint2D" => 40,
	        "DistanceJoint2D" => 40,
	        "SliderJoint2D" => 40,
	        "WheelJoint2D" => 40,
	        
	        // Tier 5: Audio (AudioSource can depend on AudioListener setup)
	        "AudioListener" => 50,
	        "AudioSource" => 51,
	        
	        // Tier 6: Animation
	        "Animator" => 60,
	        "Animation" => 60,
	        
	        // Tier 7: Navigation
	        "NavMeshAgent" => 70,
	        "NavMeshObstacle" => 70,
	        
	        // Default: Everything else gets standard priority
	        _ => 100
	    };
	}

	private byte[] SerializeComponent(Component comp)
	{
	    var dict = new Dictionary<string, string>();
	    Type type = comp.GetType();
	    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	    foreach (var field in fields)
	    {
	        if (IsSupportedType(field.FieldType))
	        {
	            object value = field.GetValue(comp);
	            if (value != null)
	                dict[field.Name] = SerializeSupportedField(value);
	        }
	    }
	    return SaveDataSerializer.Instance.Serialize(dict);
	}

	private void DeserializeComponent(Component comp, byte[] data)
	{
	    var dict = SaveDataSerializer.Instance.Deserialize<Dictionary<string, string>>(data);
	    if (dict == null)
	        return;
	    Type type = comp.GetType();
	    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	    foreach (var field in fields)
	    {
	        if (IsSupportedType(field.FieldType) && dict.TryGetValue(field.Name, out string strValue))
	        {
	            try
	            {
	                object value = DeserializeSupportedField(strValue, field.FieldType);
	                field.SetValue(comp, value);
	            }
	            catch (Exception ex)
	            {
	                ComponentOperationScope.Record(new InvalidOperationException(
	                    $"Added component field '{type.Name}.{field.Name}' could not be restored.", ex));
	                Logger.Log($"Failed to convert and set field '{field.Name}' on '{type.Name}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveablePrefab, LogLevel.Warning);
	            }
	        }
	    }
	}

        // Keep the existing Dictionary<string, string> payload layout. New vector
        // values use explicit, invariant round-trip floats instead of display text.
        private static string SerializeSupportedField(object value)
        {
            string Scalar(float number) => number.ToString("R", CultureInfo.InvariantCulture);
            if (value is Vector3 vector)
                return "csav:v3:" + Scalar(vector.x) + "," + Scalar(vector.y) + "," + Scalar(vector.z);
            if (value is Quaternion rotation)
                return "csav:q4:" + Scalar(rotation.x) + "," + Scalar(rotation.y) + "," + Scalar(rotation.z) + "," + Scalar(rotation.w);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static object DeserializeSupportedField(string value, Type type)
        {
            if (type != typeof(Vector3) && type != typeof(Quaternion))
                return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);

            int count = type == typeof(Vector3) ? 3 : 4;
            string prefix = count == 3 ? "csav:v3:" : "csav:q4:";
            string coordinates = value?.Trim();
            if (coordinates != null && coordinates.StartsWith(prefix, StringComparison.Ordinal))
                coordinates = coordinates.Substring(prefix.Length);
            else if (coordinates != null && coordinates.StartsWith("(", StringComparison.Ordinal) &&
                coordinates.EndsWith(")", StringComparison.Ordinal))
                coordinates = coordinates.Substring(1, coordinates.Length - 2); // Legacy Unity display format.
            else
                throw new FormatException("Invalid saved " + type.Name + " field.");

            string[] parts = coordinates.Split(',');
            if (parts.Length != count) throw new FormatException("Invalid coordinate count for " + type.Name + ".");
            float Parse(int index) => float.Parse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture);
            if (count == 3) return new Vector3(Parse(0), Parse(1), Parse(2));
            return new Quaternion(Parse(0), Parse(1), Parse(2), Parse(3));
        }

	private bool IsSupportedType(Type type)
	{
	    return type.IsPrimitive
	           || type == typeof(string)
	           || type == typeof(Vector3)
	           || type == typeof(Quaternion)
	           || type == typeof(float)
	           || type == typeof(double)
	           || type == typeof(bool)
	           || type == typeof(int);
	}

        /// <summary>
        /// Marks this prefab as loading saved data. During restoration, call
        /// <see cref="SetLoading"/> on the original prefab asset before
        /// instantiating so spawned instances inherit the loading state and can
        /// bypass runtime initialization logic.
        /// </summary>
        /// <param name="loading">True while serialized state is being applied.</param>
        public void SetLoading(bool loading)
        {
            bool wasLoading = isLoading;
            isLoading = loading;

            if (wasLoading && !loading && componentBlobOwnershipRefreshPending)
                RequestComponentBlobOwnershipRefresh();
        }

	public void GenerateAndRegisterPrefabAssetIDAtRuntime()
	{
	    if (string.IsNullOrEmpty(prefabAssetID))
	    {
	        prefabAssetID = Guid.NewGuid().ToString();
	        if (Logger.ShouldLog(LogCategory.SaveablePrefab))
	        {
	            Logger.Log($"SaveablePrefab: Generated new prefabAssetID '{prefabAssetID}' for '{Logger.GetUnityObjectName(gameObject)}' at runtime.", LogCategory.SaveablePrefab, LogLevel.Info);
	        }

	        PrefabRegistry registry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
	        if (registry != null)
	        {
	            GameObject assetToRegister = originalPrefabAsset != null ? originalPrefabAsset : gameObject;
	            registry.AddPrefab(prefabAssetID, assetToRegister);
	        }
	        else
	        {
	            Logger.Log("SaveablePrefab: PrefabRegistry not found in Resources.", LogCategory.SaveablePrefab, LogLevel.Error);
	        }
	    }
	    else
	    {
	        if (Logger.ShouldLog(LogCategory.SaveablePrefab))
	        {
	            Logger.Log($"SaveablePrefab: PrefabAssetID already exists for '{Logger.GetUnityObjectName(gameObject)}' and is '{prefabAssetID}'.", LogCategory.SaveablePrefab, LogLevel.Info);
	        }
	    }
	    RegisterForSaving();
	}

	internal void ResetForPooling()
	{
	    // Removed children are physical state of the pooled instance. Keep their tombstones
	    // until a snapshot restores them or gameplay recreates them; a new ID alone does not.
	    // A pooled component can represent a different logical instance after its next spawn.
	    // Never let a delayed factory destroy from the previous lifecycle affect that reuse.
	    SaveablePrefabFactory.CancelPendingDestroy(this);

	    isRegisteredWithSaveManager = false;
	    RegisterWithSaveSystem       = false;
		uniqueID                     = string.Empty;
		homeScene                    = string.Empty;
		_instantiatedRaised          = false;

	    // reset baseline for property tracking
            initialName        = gameObject.name;
            initialTag         = gameObject.tag;
            initialLayer       = gameObject.layer;
            initialActiveState = gameObject.activeSelf;

		var uid = GetComponent<UniqueID>();
		if (uid != null) uid.ID = string.Empty;

		var saveableComponents = new List<SaveableComponent>();
		GetComponentsInChildren(true, saveableComponents);
		foreach (var component in saveableComponents)
		{
			component.SuppressRegistrationUntilPrefabIdentity();
		}
	}
	}
}
#endif
