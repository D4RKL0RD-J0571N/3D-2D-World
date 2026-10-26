using UnityEngine;
using UnityEngine.UI;

public class UIToggleNavigationItem : UINavigationItem
{
    [Header ("Toggle Item Elements")]
    [Space]

    [SerializeField] private Toggle toggle;

    protected override void AutoAssignComponents ()
    {
        if (toggle == null) {
            toggle = GetComponent<Toggle> ();
        }

        if (visualTarget == null && toggle != null) {
            visualTarget = toggle.targetGraphic as Image;
        }

        if (eventTarget == null && toggle != null) {
            eventTarget = toggle.gameObject;
        }
    }

    protected override void CacheVisualState ()
    {
        if (!autoGetColorsFromUIElement) {
            return;
        }

        if (toggle == null) {
            return;
        }

        var colors = toggle.colors;

        normalColor = colors.normalColor;
        focusedColor = colors.highlightedColor;
        editingColor = colors.pressedColor;
    }

    public override bool IsInteractable ()
    {
        return base.IsInteractable () &&
               toggle != null &&
               toggle.interactable;
    }

    protected override bool ShouldSendPointerDownEvent ()
    {
        return false;
    }

    protected override bool ShouldSendPointerUpEvent ()
    {
        return false;
    }
}