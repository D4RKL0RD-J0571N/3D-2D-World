using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodeMonkey.Toolkit.TBlockerUI.Demo {

    public class Demo : MonoBehaviour {


        [SerializeField] private Button testButton;
        [SerializeField] private TextMeshProUGUI textMesh;


        private int clickCounter = 0;


        private void Awake() {
            testButton.onClick.AddListener(() => {
                clickCounter++;
                textMesh.text = "Clicked! " + clickCounter + "\n" + textMesh.text;
            });
        }

        private void Update() {
#if ENABLE_INPUT_SYSTEM
            bool isTKeyDown = Keyboard.current.tKey.wasPressedThisFrame;
#else
            bool isTKeyDown = Input.GetKeyDown(KeyCode.T);
#endif
            if (isTKeyDown) {
                BlockerUI.Show();
            }
#if ENABLE_INPUT_SYSTEM
            bool isYKeyDown = Keyboard.current.yKey.wasPressedThisFrame;
#else
            bool isYKeyDown = Input.GetKeyDown(KeyCode.Y);
#endif
            if (isYKeyDown) {
                BlockerUI.Hide();
            }
        }

    }

}
