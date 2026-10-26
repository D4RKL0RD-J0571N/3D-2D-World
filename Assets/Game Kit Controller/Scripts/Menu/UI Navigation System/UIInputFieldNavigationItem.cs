using System;
using UnityEngine;
using UnityEngine.UI;

public class UIInputFieldNavigationItem : UINavigationItem
{
    [Header ("Input Field Elements")]
    [Space]

    public InputField inputField;

    protected override void AutoAssignComponents ()
    {
        base.AutoAssignComponents ();

        if (inputField == null)
            inputField = GetComponent<InputField> ();

        if (eventTarget == null)
            eventTarget = inputField != null ? inputField.gameObject : gameObject;

        if (visualTarget == null)
            visualTarget = inputField != null ? inputField.targetGraphic : GetComponent<Graphic> ();
    }

    public override bool SupportsEditing ()
    {
        return true;
    }

    public override bool BlocksKeyboardInput => true;

    protected override void OnBeginEdit ()
    {
        base.OnBeginEdit ();

        if (inputField == null) {
            FinishEditing ();

            return;
        }

        inputField.Select ();

        UIKeyboardManager.OpenKeyboard (
            inputField,
            accepted =>
            {
                FinishEditing ();
            });
    }

    protected override void OnEndEdit ()
    {
        base.OnEndEdit ();

        if (inputField != null) {
            inputField.DeactivateInputField ();
        }
    }

    protected override bool ShouldSendSubmitEvent ()
    {
        //the keyboard is opened manually
        return false;
    }

    protected override void OnSubmit ()
    {
        if (!IsEditing ())
            BeginEditInternal ();
    }

    protected override void OnCancel ()
    {
        if (IsEditing ())
            EndEditInternal ();
    }

    public void FinishEditing ()
    {
        EndEditInternal ();
    }
}