using CodeMonkey.Toolkit.TMousePosition;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodeMonkey.Toolkit.TGridSystem.Demo {

    public class Demo : MonoBehaviour {


        [SerializeField] private Button blueButton;
        [SerializeField] private Button redButton;
        [SerializeField] private Button whiteButton;


        private GridSystemVisual.GridVisualType selectedGridVisualType;
        private int selectedValue;


        private void Awake() {
            blueButton.onClick.AddListener(() => {
                selectedValue = 56;
                selectedGridVisualType = GridSystemVisual.GridVisualType.Blue;
            });
            redButton.onClick.AddListener(() => {
                selectedValue = 12;
                selectedGridVisualType = GridSystemVisual.GridVisualType.Red;
            });
            whiteButton.onClick.AddListener(() => {
                selectedValue = 0;
                selectedGridVisualType = GridSystemVisual.GridVisualType.White;
            });
        }

        private void Update() {
#if ENABLE_INPUT_SYSTEM
            bool isLeftMouseDown = Mouse.current.leftButton.wasPressedThisFrame;
#else
            bool isLeftMouseDown = Input.GetMouseButtonDown(0);
#endif
            if (isLeftMouseDown && !IsPointerOverUI()) {
                Vector3 mouseWorldPosition = MousePositionPlane.GetPosition();

                GridPosition gridPosition = LevelGrid.Instance.GetGridPosition(mouseWorldPosition);

                if (!LevelGrid.Instance.IsValidGridPosition(gridPosition)) {
                    return;
                }

                LevelGrid.Instance.GetGridObject(gridPosition).SetValue(selectedValue);
                GridSystemVisual.Instance.ShowGridPosition(gridPosition, selectedGridVisualType);
            }
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

    }

}