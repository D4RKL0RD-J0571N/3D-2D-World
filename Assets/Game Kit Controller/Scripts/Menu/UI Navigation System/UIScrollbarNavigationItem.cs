using UnityEngine;
using UnityEngine.UI;

public class UIScrollbarNavigationItem : UINavigationItem
{
    [Header ("Scrollbar Item Elements")]
    [Space]

    [SerializeField] private Scrollbar scrollbar;

    [Space]
    [Header ("Scrollbar Settings")]
    [Space]

    public bool itemOnlyEditedIfSelected = true;

    [SerializeField] private float step = 0.1f;

    [Space]

    public bool sendPointerDownEvent;
    public bool sendSubmitEvent;
    public bool sendPointerUpEvent;

    public override bool SupportsEditing () => itemOnlyEditedIfSelected;


    protected override void AutoAssignComponents ()
    {
        if (scrollbar == null) {
            scrollbar = GetComponent<Scrollbar> ();
        }

        if (visualTarget == null && scrollbar != null) {
            visualTarget = scrollbar.targetGraphic as Image;
        }

        if (eventTarget == null && scrollbar != null) {
            eventTarget = scrollbar.gameObject;
        }
    }

    protected override void CacheVisualState ()
    {
        if (!autoGetColorsFromUIElement) {
            return;
        }

        if (scrollbar == null) {
            return;
        }

        var colors = scrollbar.colors;

        normalColor = colors.normalColor;
        focusedColor = colors.highlightedColor;
        editingColor = colors.pressedColor;
    }

    public override bool IsInteractable ()
    {
        return base.IsInteractable () &&
               scrollbar != null &&
               scrollbar.interactable;
    }

    public override bool ConsumeNavigation (Vector2 dir)
    {
        if (!isEditing)
            return false;

        switch (scrollbar.direction) {
            case Scrollbar.Direction.LeftToRight:

                if (dir == Vector2.left) {
                    scrollbar.value -= step;
                    return true;
                }

                if (dir == Vector2.right) {
                    scrollbar.value += step;
                    return true;
                }

                break;

            case Scrollbar.Direction.RightToLeft:

                if (dir == Vector2.left) {
                    scrollbar.value += step;
                    return true;
                }

                if (dir == Vector2.right) {
                    scrollbar.value -= step;
                    return true;
                }

                break;

            case Scrollbar.Direction.BottomToTop:

                if (dir == Vector2.down) {
                    scrollbar.value -= step;
                    return true;
                }

                if (dir == Vector2.up) {
                    scrollbar.value += step;
                    return true;
                }

                break;

            case Scrollbar.Direction.TopToBottom:

                if (dir == Vector2.down) {
                    scrollbar.value += step;
                    return true;
                }

                if (dir == Vector2.up) {
                    scrollbar.value -= step;
                    return true;
                }

                break;
        }

        return true;
    }

    protected override bool ShouldSendPointerDownEvent ()
    {
        return sendPointerDownEvent;
    }

    protected override bool ShouldSendSubmitEvent ()
    {
        return sendSubmitEvent;
    }

    protected override bool ShouldSendPointerUpEvent ()
    {
        return sendPointerUpEvent;
    }
}