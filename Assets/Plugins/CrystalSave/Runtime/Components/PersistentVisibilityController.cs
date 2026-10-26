#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
	[DefaultExecutionOrder(-10)]
	[DisallowMultipleComponent]
	public class PersistentVisibilityController : MonoBehaviour 
	{
		private SaveableComponent saveableComponent;
		private SaveablePrefab saveablePrefab;

		private VisibilitySettingsData visibilitySettings;

		private readonly Dictionary<Collider, bool> originalColliderStates = new Dictionary<Collider, bool>();
		private readonly Dictionary<Renderer, bool> originalRendererStates = new Dictionary<Renderer, bool>();
		private readonly Dictionary<Canvas, bool> originalCanvasStates = new Dictionary<Canvas, bool>();
		private readonly Dictionary<Rigidbody, bool> originalRigidbodyStates = new Dictionary<Rigidbody, bool>();
		private readonly Dictionary<CharacterController, bool> originalCharacterControllerStates = new Dictionary<CharacterController, bool>();

		private Collider[] colliders;
		private Renderer[] renderers;
		private Canvas[] canvases;
		private Rigidbody[] rigidbodies;
		private CharacterController[] characterControllers;
        private int enableGeneration;


		private void Awake()
		{
			InitializeReferences();
			InitializeComponentStates();


			string currentScene = SceneManager.GetActiveScene().name;
			ApplyVisibilityBasedOnScene(currentScene);
		}

        private void OnDisable()
        {
            enableGeneration++;
            StopAllCoroutines();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ReleaseOverrides();
        }

        private void ReleaseOverrides()
        {
            ApplyOverrides(colliders, originalColliderStates, false, false, c => c.enabled, (c, value) => c.enabled = value);
            ApplyOverrides(renderers, originalRendererStates, false, false, c => c.enabled, (c, value) => c.enabled = value);
            ApplyOverrides(canvases, originalCanvasStates, false, false, c => c.enabled, (c, value) => c.enabled = value);
            ApplyOverrides(characterControllers, originalCharacterControllerStates, false, false, c => c.enabled, (c, value) => c.enabled = value);
            ApplyOverrides(rigidbodies, originalRigidbodyStates, false, true, c => c.isKinematic, (c, value) => c.isKinematic = value);
        }

		private void OnDestroy()
		{
            ReleaseOverrides();
			SceneManager.sceneLoaded -= OnSceneLoaded;
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
			SceneManager.activeSceneChanged -= OnActiveSceneChanged;
		}

		private void OnEnable()
		{
			if (!Application.isPlaying) return;
            enableGeneration++;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
			// Re-evaluate visibility whenever this object is re-enabled (e.g., after pool revive or after a load)
			var sceneName = SceneManager.GetActiveScene().name;
			ApplyVisibilityBasedOnScene(sceneName);
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (!Application.isPlaying)
				hideFlags |= HideFlags.HideInInspector | HideFlags.NotEditable;
		}
#endif

		private void InitializeReferences()
		{
			saveableComponent = GetComponent<SaveableComponent>();
			saveablePrefab = GetComponent<SaveablePrefab>();

			if (saveableComponent == null && saveablePrefab == null)
			{
				Logger.Log($"PersistentVisibilityController: Neither SaveableComponent nor SaveablePrefab is attached to '{gameObject.name}'. Disabling component.", LogCategory.SaveableComponent, LogLevel.Error);
				enabled = false;
				return;
			}

			Collider[] collidersOnSelf = GetComponents<Collider>();
			colliders = (collidersOnSelf.Length > 0 ? collidersOnSelf : GetComponentsInChildren<Collider>())
                .Where(c => !(c is CharacterController)).ToArray();

			MeshRenderer[] meshRenderers = GetComponents<MeshRenderer>();
			SkinnedMeshRenderer[] skinnedMeshRenderers = GetComponents<SkinnedMeshRenderer>();
			renderers = (meshRenderers.Length > 0 || skinnedMeshRenderers.Length > 0)
				? meshRenderers.Cast<Renderer>().Concat(skinnedMeshRenderers).ToArray()
				: GetComponentsInChildren<MeshRenderer>().Cast<Renderer>().Concat(GetComponentsInChildren<SkinnedMeshRenderer>()).ToArray();

			Rigidbody[] rigidbodiesOnSelf = GetComponents<Rigidbody>();
			rigidbodies = rigidbodiesOnSelf.Length > 0 ? rigidbodiesOnSelf : GetComponentsInChildren<Rigidbody>();

			CharacterController[] characterControllersOnSelf = GetComponents<CharacterController>();
			characterControllers = characterControllersOnSelf.Length > 0 ? characterControllersOnSelf : GetComponentsInChildren<CharacterController>();

			canvases = GetComponents<Canvas>();
		}

		private void InitializeComponentStates()
		{
			visibilitySettings = new VisibilitySettingsData
			{
				DisableAllColliders = saveablePrefab?.DisableAllColliders ?? saveableComponent.DisableAllColliders,
				DisableRenderers = saveablePrefab?.DisableRenderers ?? saveableComponent.DisableRenderers,
				DisableCharacterController = saveablePrefab?.DisableCharacterController ?? saveableComponent.DisableCharacterController,
				SetRigidbodyKinematic = saveablePrefab?.SetRigidbodyKinematic ?? saveableComponent.SetRigidbodyKinematic,
				VisibleInScenes = saveablePrefab?.VisibleInScenes ?? saveableComponent.VisibleInScenes
			};
		}

		public byte[] CaptureAndSerializeSettings()
		{
			if (saveablePrefab != null)
			{
				UpdateVisibilitySettingsFromPrefab();
			}
			else if (saveableComponent != null)
			{
				UpdateVisibilitySettingsFromComponent();
			}

			if (saveablePrefab != null && saveablePrefab.VisibleInScenes != null)
			{
				visibilitySettings.VisibleInScenes = new List<string>(saveablePrefab.VisibleInScenes);
			}
			else if (saveableComponent != null && saveableComponent.VisibleInScenes != null)
			{
				visibilitySettings.VisibleInScenes = new List<string>(saveableComponent.VisibleInScenes);
			}

			return SaveDataSerializer.Instance.Serialize(visibilitySettings);
		}

		public void DeserializeAndStoreSettings(byte[] data)
		{
			if (data == null || data.Length == 0)
			{
				Logger.Log($"PersistentVisibilityController: No visibility settings data to deserialize for '{gameObject.name}'.", LogCategory.SaveableComponent, LogLevel.Warning);
				return;
			}

			try
			{
				visibilitySettings = SaveDataSerializer.Instance.Deserialize<VisibilitySettingsData>(data);
				ApplyVisibilitySettingsToSource();

				if (visibilitySettings.VisibleInScenes != null && visibilitySettings.VisibleInScenes.Count > 0)
				{
					Logger.Log($"PersistentVisibilityController: '{gameObject.name}' is set to be visible in the following scenes: {string.Join(", ", visibilitySettings.VisibleInScenes)}.");
				}

				string currentScene = SceneManager.GetActiveScene().name;
				ApplyVisibilityBasedOnScene(currentScene);
				// Also re-apply next frame to win against late systems (pool toggles, etc.).
                if (isActiveAndEnabled)
                    StartCoroutine(ReapplyNextFrame(currentScene));

				Logger.Log($"PersistentVisibilityController: Visibility settings applied to '{gameObject.name}'.");
			}
			catch (Exception ex)
			{
				Logger.Log($"PersistentVisibilityController: Failed to deserialize visibility settings for '{gameObject.name}'. Error: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveableComponent, LogLevel.Warning);
			}
		}

		public void ApplyVisibilityBasedOnScene(string currentSceneName)
		{
            if (visibilitySettings == null || (Application.isPlaying && !isActiveAndEnabled)) return;
            if (saveablePrefab)
            {
                UpdateVisibilitySettingsFromPrefab();
                visibilitySettings.VisibleInScenes = saveablePrefab.VisibleInScenes;
            }
            else if (saveableComponent)
            {
                UpdateVisibilitySettingsFromComponent();
                visibilitySettings.VisibleInScenes = saveableComponent.VisibleInScenes;
            }
			bool hasSceneFilter = visibilitySettings.VisibleInScenes != null &&
								  visibilitySettings.VisibleInScenes.Count > 0;
			bool shouldBeVisible = !hasSceneFilter ||
								   visibilitySettings.VisibleInScenes
									   .Any(scene => string.Equals(scene, currentSceneName, StringComparison.OrdinalIgnoreCase));

            ApplyOverrides(colliders, originalColliderStates,
                !shouldBeVisible && visibilitySettings.DisableAllColliders, false, c => c.enabled, (c, value) => c.enabled = value);
            ApplyOverrides(renderers, originalRendererStates,
                !shouldBeVisible && visibilitySettings.DisableRenderers, false, c => c.enabled, (c, value) => c.enabled = value);
            ApplyOverrides(canvases, originalCanvasStates,
                !shouldBeVisible && visibilitySettings.DisableRenderers, false, c => c.enabled, (c, value) => c.enabled = value);
            ApplyOverrides(characterControllers, originalCharacterControllerStates,
                !shouldBeVisible && (visibilitySettings.DisableAllColliders || visibilitySettings.DisableCharacterController), false, c => c.enabled, (c, value) => c.enabled = value);
            ApplyOverrides(rigidbodies, originalRigidbodyStates,
                !shouldBeVisible && visibilitySettings.SetRigidbodyKinematic, true, c => c.isKinematic, (c, value) => c.isKinematic = value);
        }

        // Capture only states this controller actually overrides, immediately before hiding.
        // Repeated scene callbacks must neither recapture forced states nor overwrite gameplay
        // changes on visible objects. Releasing an override restores its exact previous value.
        private static void ApplyOverrides<T>(T[] components, Dictionary<T, bool> originals,
            bool apply, bool forcedValue, Func<T, bool> read, Action<T, bool> write) where T : UnityEngine.Object
        {
            if (apply)
            {
                foreach (T component in components)
                {
                    if (!component) continue;
                    if (!originals.ContainsKey(component)) originals.Add(component, read(component));
                    write(component, forcedValue);
                }
            }
            else
            {
                foreach (var entry in originals)
                    if (entry.Key) write(entry.Key, entry.Value);
                originals.Clear();
            }
        }

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			ApplyVisibilityBasedOnScene(SceneManager.GetActiveScene().name);
			
			// Only start coroutine if GameObject is active (avoid issues with pooled prefabs)
			if (isActiveAndEnabled)
			{
				StartCoroutine(ReapplyNextFrame(scene.name));
			}
		}

		private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
		{
			ApplyVisibilityBasedOnScene(newScene.name);
			
			// Only start coroutine if GameObject is active (avoid issues with pooled prefabs)
			if (isActiveAndEnabled)
			{
				StartCoroutine(ReapplyNextFrame(newScene.name));
			}
		}

		private System.Collections.IEnumerator ReapplyNextFrame(string sceneName)
		{
            int generation = enableGeneration;
            yield return null;
            if (generation == enableGeneration && isActiveAndEnabled)
                ApplyVisibilityBasedOnScene(SceneManager.GetActiveScene().name);
		}

		private void OnSceneUnloaded(Scene scene) { }

		private void UpdateVisibilitySettingsFromPrefab()
		{
			visibilitySettings.DisableAllColliders = saveablePrefab.DisableAllColliders;
			visibilitySettings.DisableRenderers = saveablePrefab.DisableRenderers;
			visibilitySettings.DisableCharacterController = saveablePrefab.DisableCharacterController;
			visibilitySettings.SetRigidbodyKinematic = saveablePrefab.SetRigidbodyKinematic;
		}

		private void UpdateVisibilitySettingsFromComponent()
		{
			visibilitySettings.DisableAllColliders = saveableComponent.DisableAllColliders;
			visibilitySettings.DisableRenderers = saveableComponent.DisableRenderers;
			visibilitySettings.DisableCharacterController = saveableComponent.DisableCharacterController;
			visibilitySettings.SetRigidbodyKinematic = saveableComponent.SetRigidbodyKinematic;
		}

		private void ApplyVisibilitySettingsToSource()
		{
			if (saveablePrefab != null)
			{
				saveablePrefab.DisableAllColliders = visibilitySettings.DisableAllColliders;
				saveablePrefab.DisableRenderers = visibilitySettings.DisableRenderers;
				saveablePrefab.DisableCharacterController = visibilitySettings.DisableCharacterController;
				saveablePrefab.SetRigidbodyKinematic = visibilitySettings.SetRigidbodyKinematic;
			}
			else if (saveableComponent != null)
			{
				saveableComponent.DisableAllColliders = visibilitySettings.DisableAllColliders;
				saveableComponent.DisableRenderers = visibilitySettings.DisableRenderers;
				saveableComponent.DisableCharacterController = visibilitySettings.DisableCharacterController;
				saveableComponent.SetRigidbodyKinematic = visibilitySettings.SetRigidbodyKinematic;
			}
		}
	}

	[MemoryPackable]
	public partial class VisibilitySettingsData
	{
		public bool DisableAllColliders { get; set; }
		public bool DisableRenderers { get; set; }
		public bool DisableCharacterController { get; set; }
		public bool SetRigidbodyKinematic { get; set; }
		public List<string> VisibleInScenes { get; set; } = new List<string>();

		[MemoryPackConstructor]
		public VisibilitySettingsData() { }
	}
}
#endif
