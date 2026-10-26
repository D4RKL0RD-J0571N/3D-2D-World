using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.TMousePosition {

    public class MousePositionRaycast : MonoBehaviour {


        public static MousePositionRaycast Instance { get; private set; }


        [SerializeField] private LayerMask mouseColliderLayerMask = new LayerMask();
        [SerializeField] private bool moveThisTransformOnUpdate;


        private void Awake() {
            Instance = this;
        }

        private void Update() {
            if (moveThisTransformOnUpdate) {
#if ENABLE_INPUT_SYSTEM
                Vector2 mousePosition = Mouse.current.position.value;
#else
                Vector2 mousePosition = Mouse.current.position.value;
#endif
                Ray ray = Camera.main.ScreenPointToRay(mousePosition);
                if (Physics.Raycast(ray, out RaycastHit raycastHit, float.MaxValue, mouseColliderLayerMask)) {
                    transform.position = raycastHit.point;
                }
            }
        }

        private Vector3 GetPosition_Instance() {
#if ENABLE_INPUT_SYSTEM
            Vector2 mousePosition = Mouse.current.position.value;
#else
            Vector2 mousePosition = Mouse.current.position.value;
#endif
            Ray ray = Camera.main.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit raycastHit, float.MaxValue, mouseColliderLayerMask)) {
                return raycastHit.point;
            } else {
                return Vector3.zero;
            }
        }

        public static Vector3 GetPosition() {
            if (Instance == null) {
                Debug.LogError("There is no MousePositionRaycast in the Scene!");
            }
            return Instance.GetPosition_Instance();
        }

    }

}