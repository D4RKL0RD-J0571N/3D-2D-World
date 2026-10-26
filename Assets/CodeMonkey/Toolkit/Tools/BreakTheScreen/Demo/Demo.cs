using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.TBreakTheScreen {

    public class Demo : MonoBehaviour {


        private void Update() {
#if ENABLE_INPUT_SYSTEM
            bool isTKeyDown = Keyboard.current.tKey.wasPressedThisFrame;
#else
            bool isTKeyDown = Input.GetKeyDown(KeyCode.T);
#endif
            if (isTKeyDown) {
                BreakTheScreen.Spawn();
            }
        }

    }

}