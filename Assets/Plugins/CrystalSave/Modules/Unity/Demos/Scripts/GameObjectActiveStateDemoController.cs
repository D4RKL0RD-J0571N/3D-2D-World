#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using UnityEngine;
using UnityEngine.UI;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Demo
{
    /// <summary>
    /// Demonstrates that a GameObject's active state is reliably persisted across
    /// scene unloads/reloads when it carries a <see cref="RememberGameObject"/>
    /// component (with <c>RememberActive</c> enabled).
    ///
    /// The user can:
    ///   1. Toggle the target GameObject (e.g. a "pickup") active/inactive.
    ///   2. Snapshot the current scene state and switch to another scene.
    ///   3. Switch back to the original scene and observe that the GameObject
    ///      remains in the state it was left in (e.g. inactive after pickup).
    /// </summary>
    [AddComponentMenu("Crystal Save/Demos/GameObject Active State Demo Controller")]
    public class GameObjectActiveStateDemoController : MonoBehaviour
    {
        [Header("Target (optional)")]
        [Tooltip("The GameObject whose active state will be toggled by the toggle button. " +
                 "Leave null in scenes that only need a 'switch back' button.")]
        public GameObject targetObject;

        [Header("UI")]
        public Button toggleButton;
        public Text toggleButtonLabel;
        public Button switchSceneButton;
        public Text switchSceneButtonLabel;
        public Text statusText;

        [Header("Scene Switch")]
        [Tooltip("Scene to load when the 'Switch Scene' button is pressed.")]
        public string targetSceneName;

        [Tooltip("Optional label shown on the switch-scene button.")]
        public string switchSceneButtonText = "Snapshot && Switch Scene";

        private bool _busy;
        private SaveManager _subscribedManager;

        private void Start()
        {
            if (toggleButton != null)
                toggleButton.onClick.AddListener(OnToggleClicked);

            if (switchSceneButton != null)
                switchSceneButton.onClick.AddListener(OnSwitchClicked);

            if (switchSceneButtonLabel != null && !string.IsNullOrEmpty(switchSceneButtonText))
                switchSceneButtonLabel.text = switchSceneButtonText;

            // If there's no target to toggle, hide the toggle button entirely.
            if (targetObject == null && toggleButton != null)
                toggleButton.gameObject.SetActive(false);

            TrySubscribeToSaveManager();

            // SaveManager may not exist yet at Start(); also wait for its
            // initialization callback so subscriptions happen exactly once.
            SaveManager.Initialized -= OnSaveManagerInitialized;
            SaveManager.Initialized += OnSaveManagerInitialized;

            UpdateUI();
        }

        private void OnDisable()
        {
            SaveManager.Initialized -= OnSaveManagerInitialized;
            if (_subscribedManager != null)
            {
                _subscribedManager.OnLoadCompleted -= HandleLoadCompleted;
                _subscribedManager.OnSceneLoadCompleted -= HandleSceneLoadCompleted;
                _subscribedManager = null;
            }
        }

        private void OnDestroy()
        {
            OnDisable();
        }

        private void OnSaveManagerInitialized(SaveManager mgr) => TrySubscribeToSaveManager();

        private void TrySubscribeToSaveManager()
        {
            var mgr = SaveManager.Instance;
            if (mgr == null || _subscribedManager == mgr) return;
            _subscribedManager = mgr;
            // OnLoadCompleted: fired on the main thread AFTER component LoadData
            //   has already applied the saved active state.
            // OnSceneLoadCompleted: fired after a scene-switch (covers the
            //   snapshot-and-switch path used by this demo).
            mgr.OnLoadCompleted += HandleLoadCompleted;
            mgr.OnSceneLoadCompleted += HandleSceneLoadCompleted;
        }

        private void HandleLoadCompleted(object sender, SaveLoadEventArgs args) => UpdateUI();
        private void HandleSceneLoadCompleted(string sceneName) => UpdateUI();

        private void OnToggleClicked()
        {
            if (targetObject == null) return;
            targetObject.SetActive(!targetObject.activeSelf);
            UpdateUI();
        }

        private async void OnSwitchClicked()
        {
            if (_busy) return;
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogError("GameObjectActiveStateDemoController: targetSceneName is empty.");
                return;
            }

            _busy = true;
            if (switchSceneButton != null) switchSceneButton.interactable = false;

            try
            {
                var mgr = SaveManager.Instance;
                if (mgr == null)
                {
                    Debug.LogWarning("GameObjectActiveStateDemoController: SaveManager not found, falling back to plain LoadScene.");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
                    return;
                }

                if (!SaveManager.AreSaveSlotsReady)
                    await mgr.WaitForSaveSlotsAsync();

                // In-memory snapshot + scene load: no save-slot/disk I/O required.
                await SaveManagerExtensions.LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync(
                    mgr, targetSceneName, false, false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"GameObjectActiveStateDemoController: failed to switch scene – {ex.Message}\n{ex.StackTrace}");
                _busy = false;
                if (switchSceneButton != null) switchSceneButton.interactable = true;
            }
        }

        private void UpdateUI()
        {
            if (targetObject != null)
            {
                bool active = targetObject.activeSelf;
                if (toggleButtonLabel != null)
                    toggleButtonLabel.text = active ? "Disable Cube" : "Enable Cube";
                if (statusText != null)
                    statusText.text = $"Cube state: <b>{(active ? "ACTIVE" : "INACTIVE")}</b>\nSwitch scenes and come back – the state will be restored.";
            }
            else if (statusText != null && string.IsNullOrEmpty(statusText.text))
            {
                statusText.text = "You are now in the second scene.\nReturn to Scene A and verify the cube's state was preserved.";
            }
        }
    }
}
#endif
