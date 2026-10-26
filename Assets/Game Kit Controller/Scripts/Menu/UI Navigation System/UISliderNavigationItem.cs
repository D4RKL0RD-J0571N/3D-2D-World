using UnityEngine;
using UnityEngine.UI;

public class UISliderNavigationItem : UINavigationItem
{
    [Header ("Slider Item Elements")]
    [Space]

    [SerializeField] private Slider slider;

    [Space]
    [Header ("Slider Settings")]
    [Space]

    public bool itemOnlyEditedIfSelected = true;

    [SerializeField] private float step = 0.1f;

    public override bool SupportsEditing () => itemOnlyEditedIfSelected;


    protected override void AutoAssignComponents ()
    {
        if (slider == null) {
            slider = GetComponent<Slider> ();
        }

        if (visualTarget == null && slider != null) {
            visualTarget = slider.targetGraphic as Image;
        }

        if (eventTarget == null && slider != null) {
            eventTarget = slider.gameObject;
        }
    }

    protected override void CacheVisualState ()
    {
        if (!autoGetColorsFromUIElement) {
            return;
        }

        if (slider == null) {
            return;
        }

        var colors = slider.colors;

        normalColor = colors.normalColor;
        focusedColor = colors.highlightedColor;
        editingColor = colors.pressedColor;
    }

    public override bool IsInteractable ()
    {
        return base.IsInteractable () &&
               slider != null &&
               slider.interactable;
    }

    public override bool ConsumeNavigation (Vector2 dir)
    {
        if (!isEditing)
            return false;

        switch (slider.direction) {

            case Slider.Direction.LeftToRight:

                if (dir == Vector2.left)
                    slider.value -= step;
                else if (dir == Vector2.right)
                    slider.value += step;
                else
                    return true;

                break;

            case Slider.Direction.RightToLeft:

                if (dir == Vector2.left)
                    slider.value += step;
                else if (dir == Vector2.right)
                    slider.value -= step;
                else
                    return true;

                break;

            case Slider.Direction.BottomToTop:

                if (dir == Vector2.down)
                    slider.value -= step;
                else if (dir == Vector2.up)
                    slider.value += step;
                else
                    return true;

                break;

            case Slider.Direction.TopToBottom:

                if (dir == Vector2.down)
                    slider.value += step;
                else if (dir == Vector2.up)
                    slider.value -= step;
                else
                    return true;

                break;
        }

        return true;
    }

    protected override bool ShouldSendPointerDownEvent ()
    {
        return false;
    }

    protected override bool ShouldSendSubmitEvent ()
    {
        return false;
    }

    protected override bool ShouldSendPointerUpEvent ()
    {
        return false;
    }
}