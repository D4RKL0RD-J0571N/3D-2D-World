using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class VirtualPointerManager : MonoBehaviour
{
    public class Pointer
    {
        public int id;

        public Vector2 position;

        public bool down;
        public bool held;
        public bool up;

        public bool dragging;

        public GameObject hover;
        public GameObject pressed;
        public GameObject drag;

        public PointerEventData data;
    }

    public EventSystem eventSystem;

    public Dictionary<int, Pointer> pointers = new Dictionary<int, Pointer> ();

    void Awake ()
    {
        eventSystem = EventSystem.current;
    }

    public void Register (int id)
    {
        pointers [id] = new Pointer
        {
            id = id,
            data = new PointerEventData (eventSystem)
        };
    }

    public void ProcessAll ()
    {
        foreach (var kv in pointers) {
            ProcessPointer (kv.Value);
        }
    }


    void ProcessPointer (Pointer p)
    {
        // =========================
        // POINTER SETUP
        // =========================
        p.data.Reset ();
        p.data.position = p.position;
        p.data.pointerId = p.id;
        p.data.button = PointerEventData.InputButton.Left;

        List<RaycastResult> results = new List<RaycastResult> ();
        eventSystem.RaycastAll (p.data, results);

        GameObject rawHit =
            results.Count > 0 ? results [0].gameObject : null;

        // =========================
        // GET ROOT INTERACTABLE
        // =========================
        GameObject currentHover =
            ExecuteEvents.GetEventHandler<IPointerEnterHandler> (rawHit);

        p.data.pointerCurrentRaycast = new RaycastResult
        {
            gameObject = currentHover
        };

        HandleHover (p, currentHover);
        HandleInput (p, currentHover);

        results.Clear ();
    }

    void HandleHover (Pointer p, GameObject currentHover)
    {
        if (p.hover == currentHover) {
            return;
        }

        if (p.hover != null) {
            ExecuteEvents.Execute (
                p.hover,
                p.data,
                ExecuteEvents.pointerExitHandler
            );
        }

        if (currentHover != null) {
            ExecuteEvents.Execute (
                currentHover,
                p.data,
                ExecuteEvents.pointerEnterHandler
            );
        }

        p.hover = currentHover;
    }

    void HandleInput (Pointer p, GameObject currentHover)
    {
        // =========================
        // DOWN
        // =========================
        if (p.down && currentHover != null) {
            p.pressed =
                ExecuteEvents.GetEventHandler<IPointerDownHandler> (currentHover);

            p.drag =
                ExecuteEvents.GetEventHandler<IDragHandler> (currentHover);

            if (p.pressed != null) {
                ExecuteEvents.Execute (
                    p.pressed,
                    p.data,
                    ExecuteEvents.pointerDownHandler
                );
            }

            if (p.drag != null) {
                ExecuteEvents.Execute (
                    p.drag,
                    p.data,
                    ExecuteEvents.beginDragHandler
                );

                p.dragging = true;
            }
        }

        // =========================
        // DRAG
        // =========================
        if (p.held && p.dragging && p.drag != null) {
            ExecuteEvents.Execute (
                p.drag,
                p.data,
                ExecuteEvents.dragHandler
            );
        }

        // =========================
        // UP
        // =========================
        if (p.up) {
            if (p.pressed != null) {
                ExecuteEvents.Execute (
                    p.pressed,
                    p.data,
                    ExecuteEvents.pointerUpHandler
                );
            }

            if (p.dragging && p.drag != null) {
                ExecuteEvents.Execute (
                    p.drag,
                    p.data,
                    ExecuteEvents.endDragHandler
                );
            }

            if (currentHover != null) {
                var click =
                    ExecuteEvents.GetEventHandler<IPointerClickHandler> (currentHover);

                if (p.pressed == click) {
                    ExecuteEvents.Execute (
                        click,
                        p.data,
                        ExecuteEvents.pointerClickHandler
                    );
                }
            }

            p.pressed = null;
            p.drag = null;
            p.dragging = false;
        }
    }

    public void ResetPointer (int id)
    {
        if (!pointers.TryGetValue (id, out var p))
            return;

        // EXIT current hover
        if (p.hover != null) {
            ExecuteEvents.Execute (
                p.hover,
                p.data,
                ExecuteEvents.pointerExitHandler
            );
        }

        // END DRAG if exists
        if (p.dragging && p.drag != null) {
            ExecuteEvents.Execute (
                p.drag,
                p.data,
                ExecuteEvents.endDragHandler
            );
        }

        p.hover = null;
        p.pressed = null;
        p.drag = null;

        p.dragging = false;

        p.down = false;
        p.held = false;
        p.up = false;

        p.data.Reset ();
    }

    public void ResetAllPointers ()
    {
        foreach (var kv in pointers) {

            ResetPointer (kv.Key);
        }

        GKC_Utils.simpleSetSelectedGameObject (null);
    }
}