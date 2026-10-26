using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.TMonoBehaviourHooks.Demo {

    public class Demo : MonoBehaviour {


        [SerializeField] private TextMeshProUGUI logTextMesh;


        private GameObject spawnedGameObject;


        private void Update() {
#if ENABLE_INPUT_SYSTEM
            bool isTKeyDown = Keyboard.current.tKey.wasPressedThisFrame;
#else
            bool isTKeyDown = Input.GetKeyDown(KeyCode.T);
#endif
            if (isTKeyDown) {
                if (spawnedGameObject != null) {
                    // Object already spawned
                    return;
                }
                spawnedGameObject = new GameObject();
                MonoBehaviourHooks monoBehaviourHooks = spawnedGameObject.AddComponent<MonoBehaviourHooks>();
                monoBehaviourHooks.onStartAction = () => {
                    logTextMesh.text = "Attached message on Start...\n" + logTextMesh.text;
                };
                monoBehaviourHooks.onEnableAction = () => {
                    logTextMesh.text = "Attached message on Enable...\n" + logTextMesh.text;
                };
                monoBehaviourHooks.onDisableAction = () => {
                    logTextMesh.text = "Attached message on Disable...\n" + logTextMesh.text;
                };
            }

#if ENABLE_INPUT_SYSTEM
            bool isYKeyDown = Keyboard.current.yKey.wasPressedThisFrame;
#else
            bool isYKeyDown = Input.GetKeyDown(KeyCode.Y);
#endif
            if (isYKeyDown) {
                spawnedGameObject?.SetActive(false);
            }
#if ENABLE_INPUT_SYSTEM
            bool isUKeyDown = Keyboard.current.uKey.wasPressedThisFrame;
#else
            bool isUKeyDown = Input.GetKeyDown(KeyCode.U);
#endif
            if (isUKeyDown) {
                spawnedGameObject?.SetActive(true);
            }
        }

    }

}