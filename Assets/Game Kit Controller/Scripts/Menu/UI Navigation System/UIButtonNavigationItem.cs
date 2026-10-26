using UnityEngine;
using UnityEngine.UI;

public class UIButtonNavigationItem : UINavigationItem
{
    [Header ("Button Item Elements")]
    [Space]

    [SerializeField] private Button button;

    [Space]
    [Header ("Button Settings")]
    [Space]

    public bool checkIfButtonInteractableEnabled = true;

    protected override void AutoAssignComponents ()
    {
        if (button == null) {
            button = GetComponent<Button> ();
        }

        if (visualTarget == null && button != null) {
            visualTarget = button.targetGraphic as Image;
        }

        if (eventTarget == null && button != null) {
            eventTarget = button.gameObject;
        }
    }

    protected override void CacheVisualState ()
    {
        if (!autoGetColorsFromUIElement) {
            return;
        }

        if (button == null) {
            return;
        }

        if (visualTarget != null) {
            normalColor = visualTarget.color;
        } else {
            print (gameObject.name);
        }

        var colors = button.colors;

        focusedColor = colors.highlightedColor;
        editingColor = colors.pressedColor;
    }

    protected override Graphic GetTargetGraphic ()
    {
        if (visualTarget != null) {
            return visualTarget;
        }

        if (button != null) {
            return button.targetGraphic;
        }

        return null;
    }

    public override bool IsInteractable ()
    {
        return base.IsInteractable () &&
               button != null &&
               (button.interactable || !checkIfButtonInteractableEnabled);
    }
}