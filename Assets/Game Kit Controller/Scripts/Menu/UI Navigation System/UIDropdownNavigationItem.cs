using UnityEngine;
using UnityEngine.UI;

public class UIDropdownNavigationItem : UINavigationItem
{
    [Header ("Dropdown Item Elements")]
    [Space]

    [SerializeField] private Dropdown dropdown;

    public override bool SupportsEditing () => true;

    protected override void AutoAssignComponents ()
    {
        if (dropdown == null) {
            dropdown = GetComponent<Dropdown> ();
        }

        if (visualTarget == null && dropdown != null) {
            visualTarget = dropdown.targetGraphic as Image;
        }

        if (eventTarget == null && dropdown != null) {
            eventTarget = dropdown.gameObject;
        }
    }

    protected override void CacheVisualState ()
    {
        if (!autoGetColorsFromUIElement) {
            return;
        }

        if (dropdown == null) {
            return;
        }

        var colors = dropdown.colors;

        normalColor = colors.normalColor;
        focusedColor = colors.highlightedColor;
        editingColor = colors.pressedColor;
    }

    public override bool IsInteractable ()
    {
        return base.IsInteractable () &&
               dropdown != null &&
               dropdown.interactable;
    }

    protected override void OnBeginEdit ()
    {
        dropdown.Show ();
    }

    protected override void OnEndEdit ()
    {
        dropdown.Hide ();
    }

    public override bool ConsumeNavigation (Vector2 dir)
    {
        if (!isEditing)
            return false;

        if (dir != Vector2.up &&
            dir != Vector2.down) {
            return true;
        }

        int newValue = dropdown.value;

        if (dir == Vector2.up) {
            newValue--;
        } else if (dir == Vector2.down) {
            newValue++;
        }

        newValue = Mathf.Clamp (
            newValue,
            0,
            dropdown.options.Count - 1);

        if (newValue != dropdown.value) {
            dropdown.value = newValue;
            dropdown.RefreshShownValue ();
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