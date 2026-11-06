using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.TGameAssets.Demo {

    public class Demo : MonoBehaviour {


        private void Update() {
#if ENABLE_INPUT_SYSTEM
            bool isLeftMouseDown = Mouse.current.leftButton.wasPressedThisFrame;
#else
            bool isLeftMouseDown = Input.GetMouseButtonDown(0);
#endif
            if (isLeftMouseDown) {
                Debug.Log("Click");
#if ENABLE_INPUT_SYSTEM
                Vector2 mousePosition = Mouse.current.position.value;
#else
                Vector2 mousePosition = Mouse.current.position.value;
#endif
                Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
                mouseWorldPosition.z = 0f;
                Instantiate(GameAssets.Instance.codeMonkeySpritePrefab, mouseWorldPosition, Quaternion.identity);
            }
        }

    }

}