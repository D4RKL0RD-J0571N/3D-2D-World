using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodeMonkey.Toolkit.TTakeScreenshot {

    public class Demo : MonoBehaviour {


        [SerializeField] private RawImage rawImage;


        private void Awake() {
            rawImage.gameObject.SetActive(false);
        }

        private void Update() {
#if ENABLE_INPUT_SYSTEM
            bool isTKeyDown = Keyboard.current.tKey.wasPressedThisFrame;
#else
            bool isTKeyDown = Input.GetKeyDown(KeyCode.T);
#endif
            if (isTKeyDown) {
                TakeScreenshot.TakeScreenshotTexture((Texture2D texture2D) => {
                    rawImage.texture = texture2D;
                },
                Application.dataPath + "/CodeMonkey/Toolkit/Tools/TakeScreenshot/Screenshot.png");
                rawImage.gameObject.SetActive(true);
            }
#if ENABLE_INPUT_SYSTEM
            bool isYKeyDown = Keyboard.current.yKey.wasPressedThisFrame;
#else
            bool isYKeyDown = Input.GetKeyDown(KeyCode.Y);
#endif
            if (isYKeyDown) {
                TakeScreenshot.TakeScreenshotTexture((Texture2D texture2D) => {
                    rawImage.texture = texture2D;
                },
                Application.dataPath + "/CodeMonkey/Toolkit/Tools/TakeScreenshot/ScreenshotNoUI.png", false);
                rawImage.gameObject.SetActive(true);
            }
        }

    }

}