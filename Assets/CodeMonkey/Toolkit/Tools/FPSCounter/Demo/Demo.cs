using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.TFPSCounter.Demo {

    public class Demo : MonoBehaviour {


        [SerializeField] private Transform prefab;


        private float timer;


        private void Update() {
#if ENABLE_INPUT_SYSTEM
            bool isLeftMouseButtonPressed = Mouse.current.leftButton.isPressed;
#else
            bool isLeftMouseButtonPressed = Input.GetMouseButton(0);
#endif
            if (isLeftMouseButtonPressed) {
                timer -= Time.deltaTime;
                if (timer <= 0f) {
                    timer += .025f;
                    Vector3 spawnPosition =
                        new Vector3(0, 3, 0) +
                        new Vector3(Random.Range(-1f, +1f), 0, Random.Range(-1f, +1f));
                    Instantiate(prefab, spawnPosition, Quaternion.identity);
                }
            }
        }

    }

}