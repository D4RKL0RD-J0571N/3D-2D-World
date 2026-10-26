using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.TDrawMesh {

    /// <summary>
    /// ** Draw Mesh **
    /// Draw a mesh with the mouse
    /// 
    /// You can use this to let the player draw anything
    /// It could be a logo, their player icon
    /// Or since it's a mesh you can also add a MeshCollider
    /// and do something like a Level Builder
    /// </summary>
    public class DrawMesh : MonoBehaviour {


        public static DrawMesh Instance { get; private set; }


        [SerializeField] private Material drawMeshMaterial;


        private GameObject lastGameObject;
        private int lastSortingOrder;
        private Mesh mesh;
        private Vector3 lastMouseWorldPosition;
        private float lineThickness = 0.6f;
        private Color lineColor = Color.green;


        private void Awake() {
            Instance = this;
        }

        private void Update() {
            if (!IsPointerOverUI()) {
                // Only run logic if not over UI
                Vector3 mouseWorldPosition = GetMouseWorldPosition();
#if ENABLE_INPUT_SYSTEM
                bool isLeftMouseDown = Mouse.current.leftButton.wasPressedThisFrame;
#else
                bool isLeftMouseDown = Input.GetMouseButtonDown(0);
#endif
                if (isLeftMouseDown) {
                    // Mouse Down
                    CreateMeshObject();
                    mesh = MeshUtils.CreateMesh(mouseWorldPosition, mouseWorldPosition, mouseWorldPosition, mouseWorldPosition);
                    mesh.MarkDynamic();
                    lastGameObject.GetComponent<MeshFilter>().mesh = mesh;

                    Material material = new Material(drawMeshMaterial);
                    material.color = lineColor;
                    lastGameObject.GetComponent<MeshRenderer>().material = material;
                }

#if ENABLE_INPUT_SYSTEM
                bool isLeftMouseButtonPressed = Mouse.current.leftButton.isPressed;
#else
                bool isLeftMouseButtonPressed = Input.GetMouseButton(0);
#endif
                if (isLeftMouseButtonPressed) {
                    // Mouse Held Down
                    float minDistance = .1f;
                    if (Vector2.Distance(lastMouseWorldPosition, mouseWorldPosition) > minDistance) {
                        // Far enough from last point
                        Vector2 forwardVector = (mouseWorldPosition - lastMouseWorldPosition).normalized;

                        lastMouseWorldPosition = mouseWorldPosition;

                        MeshUtils.AddLinePoint(mesh, mouseWorldPosition, lineThickness);
                    }
                }

#if ENABLE_INPUT_SYSTEM
                bool isLeftMouseButtonUp = Mouse.current.leftButton.wasReleasedThisFrame;
#else
                bool isLeftMouseButtonUp = Input.GetMouseButtonUp(0);
#endif
                if (isLeftMouseButtonUp) {
                    // Mouse Up
                    MeshUtils.AddLinePoint(mesh, mouseWorldPosition, 0f);
                }
            }
        }

        private void CreateMeshObject() {
            lastGameObject = new GameObject("DrawMeshSingle", typeof(MeshFilter), typeof(MeshRenderer));
            lastSortingOrder++;
            lastGameObject.GetComponent<MeshRenderer>().sortingOrder = lastSortingOrder;
        }

        private Vector3 GetMouseWorldPosition() {
#if ENABLE_INPUT_SYSTEM
            Vector2 mousePosition = Mouse.current.position.value;
#else
            Vector2 mousePosition = Mouse.current.position.value;
#endif
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
            worldPosition.z = 0f;
            return worldPosition;
        }

        private bool IsPointerOverUI() {
            if (EventSystem.current.IsPointerOverGameObject()) {
                return true;
            } else {
                PointerEventData pe = new PointerEventData(EventSystem.current);
#if ENABLE_INPUT_SYSTEM
                Vector2 mousePosition = Mouse.current.position.value;
#else
                Vector2 mousePosition = Mouse.current.position.value;
#endif
                pe.position = mousePosition;
                List<RaycastResult> hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pe, hits);
                return hits.Count > 0;
            }
        }

        public void SetThickness(float lineThickness) {
            this.lineThickness = lineThickness;
        }

        public void SetColor(Color lineColor) {
            this.lineColor = lineColor;
        }

    }

}