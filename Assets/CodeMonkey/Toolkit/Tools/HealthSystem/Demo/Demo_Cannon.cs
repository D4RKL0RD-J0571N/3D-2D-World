using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.THealthSystem.Demo {

    /// <summary>
    /// 
    /// </summary>
    public class Demo_Cannon : MonoBehaviour {


        [SerializeField] private Transform pfBullet;


        private Transform aimTransform;


        private void Awake() {
            aimTransform = transform.Find("Aim");
        }

        private void Update() {
            Vector3 aimDir = (GetMouseWorldPosition() - transform.position).normalized;
            float angle = GetAngleFromVector(aimDir);
            aimTransform.eulerAngles = new Vector3(0, 0, angle);

#if ENABLE_INPUT_SYSTEM
            bool isLeftMouseDown = Mouse.current.leftButton.wasPressedThisFrame;
#else
            bool isLeftMouseDown = Input.GetMouseButtonDown(0);
#endif
            if (isLeftMouseDown) {
                Instantiate(pfBullet, transform.position, Quaternion.Euler(0, 0, angle));
            }
        }


        /// <summary>Get Mouse Position in World with Z = 0f</summary>
        public Vector3 GetMouseWorldPosition() {
#if ENABLE_INPUT_SYSTEM
            Vector2 mousePosition = Mouse.current.position.value;
#else
            Vector2 mousePosition = Mouse.current.position.value;
#endif
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
            worldPosition.z = 0f;
            return worldPosition;
        }

        /// <summary>Convert Vector direction into a Euler angle</summary>
        public float GetAngleFromVector(Vector3 dir) {
            dir = dir.normalized;
            float n = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (n < 0) n += 360;
            return n;
        }

    }

}