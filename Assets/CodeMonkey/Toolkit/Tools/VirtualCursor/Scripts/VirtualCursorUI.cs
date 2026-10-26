using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace CodeMonkey.Toolkit.TVirtualCursor {

    /// <summary>
    /// ** Virtual Cursor **
    /// 
    /// </summary>
    public class VirtualCursorUI : MonoBehaviour {


        [SerializeField] private RectTransform canvasRectTransform;


        private VirtualMouseInput virtualMouseInput;


        private void Awake() {
            if (canvasRectTransform == null) {
                Canvas canvas = transform.GetComponentInParent<Canvas>();
                if (canvas != null) {
                    canvasRectTransform = canvas.GetComponent<RectTransform>();
                }
            }

            if (canvasRectTransform == null) {
                Debug.LogError("VirtualCursor could not locate Canvas Rect Transform!");
            }

            virtualMouseInput = GetComponent<VirtualMouseInput>();
        }

        private void Update() {
            transform.localScale = Vector3.one * (1f / canvasRectTransform.localScale.x);
            transform.SetAsLastSibling();
        }

        private void LateUpdate() {
            Vector2 virtualMousePosition = virtualMouseInput.virtualMouse.position.value;
            virtualMousePosition.x = Mathf.Clamp(virtualMousePosition.x, 0f, Screen.width);
            virtualMousePosition.y = Mathf.Clamp(virtualMousePosition.y, 0f, Screen.height);
            InputState.Change(virtualMouseInput.virtualMouse.position, virtualMousePosition);
        }

    }

}