using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UIPointerDispatcher
{
    private PointerEventData pointerData;
    private EventSystem eventSystem;

    private GameObject currentHover;
    private GameObject lastHover;

    private GameObject pressedObject;
    private GameObject dragObject;

    private bool isDragging;

    public UIPointerDispatcher ()
    {
        eventSystem = EventSystem.current;
        pointerData = new PointerEventData (eventSystem);
    }

    public void Process (VirtualTouch touch)
    {
        // =========================
        // POINTER POSITION
        // =========================
        pointerData.position = touch.position;

        // =========================
        // UI RAYCAST
        // =========================
        List<RaycastResult> results = new List<RaycastResult> ();

        eventSystem.RaycastAll (pointerData, results);

        currentHover = results.Count > 0
            ? results [0].gameObject
            : null;

        // =========================
        // HOVER ENTER / EXIT
        // =========================
        HandleHoverChange ();

        // =========================
        // POINTER DOWN
        // =========================
        if (touch.down && currentHover != null) {
            pressedObject = ExecuteEvents.GetEventHandler<IPointerDownHandler> (currentHover);

            dragObject = ExecuteEvents.GetEventHandler<IDragHandler> (currentHover);

            if (pressedObject != null) {
                ExecuteEvents.Execute (
                    pressedObject,
                    pointerData,
                    ExecuteEvents.pointerDownHandler
                    );

                Debug.Log ("pointer down " + pressedObject.name);
            }

            // BEGIN DRAG
            if (dragObject != null) {
                ExecuteEvents.Execute (
                    dragObject,
                    pointerData,
                    ExecuteEvents.beginDragHandler
                );


                Debug.Log ("drag down " + dragObject.name);

                isDragging = true;
            }
        }

        // =========================
        // POINTER HOLD
        // =========================
        if (touch.held && dragObject != null && isDragging) {
            ExecuteEvents.Execute (
                dragObject,
                pointerData,
                ExecuteEvents.dragHandler
            );
        }

        // =========================
        // POINTER UP
        // =========================
        if (touch.up) {
            if (pressedObject != null) {
                ExecuteEvents.Execute (
                    pressedObject,
                    pointerData,
                    ExecuteEvents.pointerUpHandler
                );

                Debug.Log ("pointer up " + pressedObject.name);
            }

            if (isDragging && dragObject != null) {
                ExecuteEvents.Execute (
                    dragObject,
                    pointerData,
                    ExecuteEvents.endDragHandler
                );
            }


            // CLICK
            GameObject clickHandler =
                 ExecuteEvents.GetEventHandler<IPointerClickHandler> (currentHover);

            if (pressedObject != null && pressedObject == clickHandler) {
                ExecuteEvents.Execute (
                    clickHandler,
                    pointerData,
                    ExecuteEvents.pointerClickHandler
                );
            }

            pressedObject = null;
            dragObject = null;
            isDragging = false;
        }
    }


    // =========================
    // HOVER SYSTEM
    // =========================
    private void HandleHoverChange ()
    {
        if (currentHover == lastHover) {
            return;
        }

        // EXIT previous
        if (lastHover != null) {
            ExecuteEvents.Execute (
                lastHover,
                pointerData,
                ExecuteEvents.pointerExitHandler
            );
        }

        // ENTER new
        if (currentHover != null) {
            ExecuteEvents.Execute (
                currentHover,
                pointerData,
                ExecuteEvents.pointerEnterHandler
            );

            Debug.Log ("enter hover " + currentHover.name);
        }

        lastHover = currentHover;
    }
}