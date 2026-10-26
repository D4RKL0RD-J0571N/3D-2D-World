using System.ComponentModel;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CodeMonkey.Toolkit.TDragWindow {

    /// <summary>
    /// ** Drag Window **
    /// 
    /// Just add this component to drag a RectTransform, usually a UI window.
    /// 
    /// Also usually you place the DragWindow on the Title Bar Background image.
    /// 
    /// Check how the WindowUI is set up in the Demo scene.
    /// </summary>
    public class DragWindow : MonoBehaviour, IDragHandler, IPointerDownHandler {


        [SerializeField] private RectTransform dragRectTransform;
        [SerializeField] private Canvas canvas;
        [SerializeField] private bool setAsLastSiblingOnPress;


        private void Awake() {
            if (dragRectTransform == null) {
                // This assumes it is set up just like in the demo, as a child of the main parent
                dragRectTransform = transform.parent.GetComponent<RectTransform>();
            }

            if (canvas == null) {
                canvas = transform.GetComponentInParent<Canvas>();
            }
        }

        public void OnDrag(PointerEventData eventData) {
            dragRectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }

        public void OnPointerDown(PointerEventData eventData) {
            if (setAsLastSiblingOnPress) {
                dragRectTransform.SetAsLastSibling();
            }
        }

    }

}