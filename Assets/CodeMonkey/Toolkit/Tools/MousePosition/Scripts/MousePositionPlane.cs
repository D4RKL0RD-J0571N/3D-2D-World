using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.TMousePosition {

    public class MousePositionPlane {


        public static Vector3 GetPosition() {
#if ENABLE_INPUT_SYSTEM
            Vector2 mousePosition = Mouse.current.position.value;
#else
            Vector2 mousePosition = Mouse.current.position.value;
#endif
            Ray mouseCameraRay = Camera.main.ScreenPointToRay(mousePosition);

            Plane plane = new Plane(Vector3.up, Vector3.zero);

            if (plane.Raycast(mouseCameraRay, out float distance)) {
                return mouseCameraRay.GetPoint(distance);
            } else {
                return Vector3.zero;
            }
        }

    }

}