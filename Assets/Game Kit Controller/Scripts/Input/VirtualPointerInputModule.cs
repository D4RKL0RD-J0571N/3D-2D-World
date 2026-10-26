using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class VirtualPointerInputModule : BaseInputModule
{
    public Vector2 virtualPosition;
    public bool down;
    public bool held;
    public bool up;

    private PointerEventData pointerEvent;

    private GameObject currentHover;
    private GameObject pressedObject;
    private GameObject dragObject;

    private bool isDragging;

    protected override void Awake ()
    {
        base.Awake ();
        pointerEvent = new PointerEventData (eventSystem);
    }

    public override void Process ()
    {
        // =========================
        // 1. SET POINTER DATA
        // =========================
        pointerEvent.Reset ();
        pointerEvent.position = virtualPosition;
        pointerEvent.pointerId = -1;

        // =========================
        // 2. RAYCAST
        // =========================
        eventSystem.RaycastAll (pointerEvent, m_RaycastResultCache);

        var raycast = FindFirstRaycast (m_RaycastResultCache);
        currentHover = raycast.gameObject;

        pointerEvent.pointerCurrentRaycast = raycast;

        // =========================
        // 3. HOVER (UNITY INTERNAL WAY)
        // =========================
        HandlePointerExitAndEnter (pointerEvent, currentHover);

        // =========================
        // 4. DOWN
        // =========================
        if (down) {
            pressedObject =
                ExecuteEvents.GetEventHandler<IPointerDownHandler> (currentHover);

            dragObject =
                ExecuteEvents.GetEventHandler<IDragHandler> (currentHover);

            if (pressedObject != null) {
                ExecuteEvents.Execute (
                    pressedObject,
                    pointerEvent,
                    ExecuteEvents.pointerDownHandler
                );
            }

            if (dragObject != null) {
                ExecuteEvents.Execute (
                    dragObject,
                    pointerEvent,
                    ExecuteEvents.beginDragHandler
                );

                isDragging = true;
            }
        }

        // =========================
        // DRAG
        // =========================
        if (held && isDragging && dragObject != null) {
            ExecuteEvents.Execute (
                dragObject,
                pointerEvent,
                ExecuteEvents.dragHandler
            );
        }

        // =========================
        // UP + CLICK
        // =========================
        if (up) {
            if (pressedObject != null) {
                ExecuteEvents.Execute (
                    pressedObject,
                    pointerEvent,
                    ExecuteEvents.pointerUpHandler
                );
            }

            if (isDragging && dragObject != null) {
                ExecuteEvents.Execute (
                    dragObject,
                    pointerEvent,
                    ExecuteEvents.endDragHandler
                );
            }

            if (currentHover != null) {
                var clickHandler =
                    ExecuteEvents.GetEventHandler<IPointerClickHandler> (currentHover);

                if (pressedObject != null && pressedObject == clickHandler) {
                    ExecuteEvents.Execute (
                        clickHandler,
                        pointerEvent,
                        ExecuteEvents.pointerClickHandler
                    );
                }
            }

            pressedObject = null;
            dragObject = null;
            isDragging = false;
        }

        m_RaycastResultCache.Clear ();
    }
}