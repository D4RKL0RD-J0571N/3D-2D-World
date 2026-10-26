using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class UINavigationManager : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public int playerId;

    public bool useBothJoysticks;

    public bool useRightJoystickAxis;

    public int joystickNumber = 1;

    [Space]
    [Header ("Navigation Settings")]
    [Space]

    [SerializeField] private float deadZone = 0.5f;
    [SerializeField] private float repeatDelay = 0.25f;
    [SerializeField] private float repeatRate = 0.08f;
    [SerializeField] private float repeatRateOnEditingItem = 0.08f;

    [Space]
    [Header ("Scoring UI Settings")]
    [Space]

    [SerializeField] private float directionWeight = 1000f;
    [SerializeField] private float minDot = 0.5f;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool useVibrationOnUINavigation;
    public int vibrationIDOnUINavigation;

    [Space]

    public bool useVibrationOnPressUIInteraction;
    public int vibrationIDOnPressUIInteraction;

    [Space]

    public bool useVibrationOnUseUIInteraction;
    public int vibrationIDOnUseUIInteraction;

    [Space]

    [Range (0f, 1f)] public float visibleThreshold = 0.15f;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    [SerializeField] private bool inputEnabled = false;

    public Vector2 axisInput;

    public UINavigationItem currentSelection;

    public bool moveInputPaused;

    public bool submitInputPaused;

    public bool blockKeyboardActive;

    public bool editingItemActive;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsOnUINavigationActiveStateChange;
    public UnityEvent eventOnEnableUINavigation;
    public UnityEvent eventOnDisableUINavigation;

    [Space]
    [Header ("Components")]
    [Space]

    public playerInputManager playerInput;

    public simpleGamepadVibrationHelper mainSimpleGamepadVibrationHelper;




    public UINavigationItem CurrentSelection => currentSelection;

    public event Action<bool> OnKeyboardInputBlockStateChanged;


    private readonly Stack<UINavigationContext> contextStack = new Stack<UINavigationContext> ();

    private float nextMoveTime;
    private bool firstRepeat = true;


    public int PlayerId => playerId;

    bool playerInputLocated;

    UIStateSystem mainUIStateSystem;

    bool mainUIStateSystemLocated;

    public void setUINavigationMoveInputPausedState (bool state)
    {
        moveInputPaused = state;
    }

    public void setSubmitInputPausedState (bool state)
    {
        submitInputPaused = state;

        if (!state) {
            if (CurrentSelection != null) {
                if (CurrentSelection.IsPressed ()) {
                    CurrentSelection.SubmitUp ();
                }
            }
        }
    }

    public void SetInputEnabled (bool enabled)
    {
        inputEnabled = enabled;

        playerInputLocated = playerInput != null;

        if (inputEnabled) {
            findUIStateSystem ();
        }

        if (playerInputLocated) {
            playerId = playerInput.getPlayerID ();

            if (useVibrationOnUINavigation) {
                playerInput.setGamePausedStateOnGamepadByID (false);

                playerInput.stopGamepadVibrationByPlayerID ();
            }

            if (!inputEnabled) {
                if (blockKeyboardActive) {
                    playerInput.setIgnoreKeyboardInputActiveState (false);

                    blockKeyboardActive = false;

                    editingItemActive = false;
                }
            }
        }

        checkEventsOnUINavigationActiveStateChange (inputEnabled);
    }

    void checkEventsOnUINavigationActiveStateChange (bool state)
    {
        if (useEventsOnUINavigationActiveStateChange) {
            if (state) {
                eventOnEnableUINavigation.Invoke ();
            } else {
                eventOnDisableUINavigation.Invoke ();
            }
        }
    }

    private void Update ()
    {
        if (!CanProcessInput ())
            return;

        HandleInput ();
    }

    private void HandleInput ()
    {
        axisInput = Vector2.zero;

        if (playerInputLocated) {
            if (playerInput.isInputPaused () || playerInput.isPlayerDead ()) {
                if (useBothJoysticks) {
                    axisInput = playerInput.getRealMouseGamepadAxisInput ()
                        + playerInput.getRealMovementGamepadAxisInput ();
                } else {
                    if (useRightJoystickAxis) {
                        axisInput = playerInput.getRealMouseGamepadAxisInput ();
                    } else {
                        axisInput = playerInput.getRealMovementGamepadAxisInput ();
                    }
                }
            } else {
                if (useBothJoysticks) {
                    axisInput = playerInput.getPlayerMouseAxis () +
                        playerInput.getPlayerMovementAxis ();
                } else {
                    if (useRightJoystickAxis) {
                        axisInput = playerInput.getPlayerMouseAxis ();
                    } else {
                        axisInput = playerInput.getPlayerMovementAxis ();
                    }
                }
            }
        } else {
            axisInput = GamepadRegistry.getNewInputLeftJoysticksAxisInputValue (joystickNumber);
        }

        if (moveInputPaused) {
            axisInput = Vector2.zero;
        }

        HandleNavigation (axisInput);

        HandleSubmit ();

        HandleCancel ();
    }

    private void HandleNavigation (Vector2 input)
    {
        input = GetCardinal (input);

        if (input == Vector2.zero) {
            firstRepeat = true;

            return;
        }

        if (Time.unscaledTime < nextMoveTime) {
            return;
        }

        Move (input);

        if (firstRepeat) {
            nextMoveTime = Time.unscaledTime + repeatDelay;

            firstRepeat = false;
        } else {
            nextMoveTime = Time.unscaledTime + repeatRate;
        }

        if (editingItemActive) {
            nextMoveTime = Time.unscaledTime + repeatRateOnEditingItem;
        }
    }

    // ======================================================
    // SUBMIT
    // ======================================================

    private UINavigationItem pressedItem;

    private void HandleSubmit ()
    {
        if (submitInputPaused) {
            return;
        }

        var context = GetCurrentContext ();

        //==================================================
        // BUTTON DOWN
        //==================================================

        if (GamepadRegistry.GetClickDown (playerId)) {
            if (currentSelection == null)
                return;

            pressedItem = currentSelection;

            if (showDebugPrint) {
                print ("pressing click down");
            }

            currentSelection.SubmitDown ();

            checkVibrationOnPressUIInteraction ();
        }

        //==================================================
        // BUTTON UP
        //==================================================

        if (GamepadRegistry.GetClickUp (playerId)) {

            if (pressedItem == null)
                return;

            if (showDebugPrint) {
                print ("pressing click up");
            }

            //store the pressed item, just in case the submit up changes the selection or page
            var item = pressedItem;

            pressedItem = null;

            item.SubmitUp ();

            //if item is different from current selection, avoid any action
            if (item != currentSelection)
                return;

            //==================================================
            // EDITING
            //==================================================

            if (item.SupportsEditing ()) {

                if (context != null) {

                    if (context.CurrentEditingItem == item) {
                        context.ClearEditing ();

                        NotifyEditingState (false);
                    } else {
                        context.SetEditingItem (item);

                        NotifyEditingState (true);
                    }
                }

                return;
            }

            //==================================================
            // NORMAL SUBMIT
            //==================================================

            checkVibrationOnUseUIInteraction ();

            item.Submit ();
        }
    }

    // ======================================================
    // CANCEL
    // ======================================================

    private void HandleCancel ()
    {
        if (currentSelection == null)
            return;

        var context = GetCurrentContext ();

        if (GamepadRegistry.GetReturnClickDown (playerId)) {

            // ==============================================
            // IF EDITING -> EXIT EDIT MODE ONLY
            // ==============================================

            // exit editing first
            if (context != null && context.HasEditingItem ()) {
                context.ClearEditing ();

                NotifyEditingState (false);

                return;
            }

            currentSelection.Cancel ();

            if (showDebugPrint) {
                print ("pressing cancel");
            }
        }
    }


    private Vector2 GetCardinal (Vector2 input)
    {
        if (input.magnitude < deadZone)
            return Vector2.zero;

        input.Normalize ();

        if (Mathf.Abs (input.x) > Mathf.Abs (input.y))
            return new Vector2 (Mathf.Sign (input.x), 0);

        return new Vector2 (0, Mathf.Sign (input.y));
    }


    public void SetSelection (UINavigationItem item)
    {
        if (item == null)
            return;

        if (!item.IsInteractable ())
            return;

        if (currentSelection == item)
            return;

        var context = GetCurrentContext ();

        if (context != null && context.HasEditingItem ()) {

            context.ClearEditing ();

            NotifyEditingState (false);
        }

        if (currentSelection != null) {
            currentSelection.Deselect ();
        }

        currentSelection = item;

        currentSelection.Select ();

        if (context != null) {
            context.updateNavigationZoneState (currentSelection);

            context.HandleFocus (item);

            context.setLastSelectedItem (currentSelection);
        }
    }

    private void Move (Vector2 dir)
    {
        var context = GetCurrentContext ();

        if (context == null)
            return;

        // ==================================================
        // NEW : AUTO RECOVERY IF SELECTION LOST
        // ==================================================

        if (currentSelection == null || !currentSelection.IsInteractable ()) {
            TrySetInitialSelection (context);

            return;
        }

        // ==================================================
        // IF CURRENT ITEM IS IN EDIT MODE
        // ELEMENT CONSUMES NAVIGATION
        // ==================================================

        if (context.HasEditingItem ()) {

            var editingItem = context.CurrentEditingItem;

            if (editingItem != null) {
                bool consumed = editingItem.ConsumeNavigation (dir);

                if (consumed) {
                    checkVibrationOnUseUIInteraction ();

                    return;
                }
            }

            return;
        }


        // ==================================================
        // EXPLICIT OVERRIDES
        // ==================================================

        var overrideTarget = currentSelection.GetOverride (dir);

        if (overrideTarget != null &&
            overrideTarget.IsInteractable ()) {

            SetSelection (overrideTarget);

            checkVibrationOnUINavigation ();

            return;
        }

        // ==================================================
        // AUTO FIND
        // ==================================================

        var best = FindBest (context.Items, dir);

        if (best != null) {
            if (showDebugPrint) {
                print ("best elemenet found " + best.name);
            }

            SetSelection (best);

            checkVibrationOnUINavigation ();
        } else {
            if (showDebugPrint) {
                print ("best element not found");
            }
        }
    }

    void checkVibrationOnUINavigation ()
    {
        if (useVibrationOnUINavigation) {
            if (playerInputLocated) {
                playerInput.sendSignalToGamepadVibrationHelper (vibrationIDOnUINavigation);
            } else {
                mainSimpleGamepadVibrationHelper.setGamepadVibrationByID (vibrationIDOnUINavigation);
            }
        }
    }

    void checkVibrationOnPressUIInteraction ()
    {
        if (useVibrationOnPressUIInteraction) {
            if (playerInputLocated) {
                playerInput.sendSignalToGamepadVibrationHelper (vibrationIDOnPressUIInteraction);
            } else {
                mainSimpleGamepadVibrationHelper.setGamepadVibrationByID (vibrationIDOnPressUIInteraction);
            }
        }
    }

    void checkVibrationOnUseUIInteraction ()
    {
        if (useVibrationOnUseUIInteraction) {
            if (playerInputLocated) {
                playerInput.sendSignalToGamepadVibrationHelper (vibrationIDOnUseUIInteraction);
            } else {
                mainSimpleGamepadVibrationHelper.setGamepadVibrationByID (vibrationIDOnUseUIInteraction);
            }
        }
    }

    private UINavigationItem FindBest (IReadOnlyList<UINavigationItem> items, Vector2 dir)
    {
        if (currentSelection == null)
            return null;

        var context = GetCurrentContext ();

        if (context == null)
            return null;

        RectTransform navigationSpace = context.getNavigationSpace ();

        if (navigationSpace == null)
            return null;

        RectTransform currentRect =
            currentSelection.GetNavigationRectTransform ();

        if (currentRect == null)
            return null;

        Vector3 currentWorldCenter =
            currentRect.TransformPoint (
                currentRect.rect.center);

        Vector2 pos =
            navigationSpace.InverseTransformPoint (
                currentWorldCenter);

        UINavigationItem best = null;

        float bestScore = float.MinValue;

        Vector2 normalizedDir =
            dir.normalized;

        foreach (var item in items) {

            if (item == null)
                continue;

            if (item == currentSelection)
                continue;

            if (!CanNavigateTo (currentSelection, item))
                continue;

            RectTransform itemRect =
                item.GetNavigationRectTransform ();

            if (itemRect == null)
                continue;

            Vector3 itemWorldCenter =
                itemRect.TransformPoint (
                    itemRect.rect.center);

            Vector2 itemPos =
                navigationSpace.InverseTransformPoint (
                    itemWorldCenter);

            Vector2 to =
                itemPos - pos;

            if (to.sqrMagnitude <= 0.001f)
                continue;

            Vector2 normalizedTo =
                to.normalized;

            float dot =
                Vector2.Dot (
                    normalizedDir,
                    normalizedTo);

            if (dot < minDot)
                continue;

            float dist =
                to.magnitude;

            float score =
                (dot * directionWeight) /
                (1f + dist);

            if (score > bestScore) {
                bestScore = score;
                best = item;
            }
        }

        return best;
    }

    private bool CanNavigateTo (UINavigationItem from, UINavigationItem to)
    {
        //check if both items are on the same scroll rect, to ignore the visibility part
        if (from.HasScrollRect &&
            to.HasScrollRect &&
            from.ScrollRect == to.ScrollRect) {
            return to.IsInteractable ();
        }

        //if not, check for visiblity of the object to move
        return to.IsSelectableForNavigation (visibleThreshold);
    }

    private void TrySetInitialSelection (UINavigationContext context)
    {
        if (context == null)
            return;

        if (context.Items == null)
            return;

        foreach (var item in context.Items) {
            if (item == null)
                continue;

            if (!item.IsInteractable ())
                continue;

            SetSelection (item);

            return;
        }
    }

    public void PushContext (UINavigationContext context)
    {
        if (context == null)
            return;

        context.EnsureItems ();

        if (!contextStack.Contains (context))
            contextStack.Push (context);

        SetInputEnabled (true);
    }

    public void PopContext (UINavigationContext context)
    {
        if (context == null)
            return;

        if (contextStack.Count == 0)
            return;

        Stack<UINavigationContext> temp = new Stack<UINavigationContext> ();

        while (contextStack.Count > 0) {
            var c = contextStack.Pop ();

            if (c != context)
                temp.Push (c);
        }

        while (temp.Count > 0)
            contextStack.Push (temp.Pop ());

        // ==================================================
        // VALIDATE CURRENT SELECTION
        // ==================================================

        validateCurrentSelectionOnCurrentContext ();

        if (GetCurrentContext () == null) {
            ClearAllEditing ();
        } else {
            RestoreCurrentContextSelection ();
        }

        // ==================================================
        // DISABLE INPUT IF NO CONTEXT
        // ==================================================

        if (!HasActiveContext ()) {
            SetInputEnabled (false);
        }
    }

    public void RestoreCurrentContextSelection ()
    {
        var context = GetCurrentContext ();

        if (context == null)
            return;

        if (currentSelection != null)
            return;

        UINavigationItem lastSelectedItem = context.getLastSelectedItem ();

        if (lastSelectedItem != null && lastSelectedItem.IsInteractable ()) {

            SetSelection (lastSelectedItem);
            return;
        }

        //TrySetInitialSelection (context);
    }

    public void validateCurrentSelectionOnCurrentContext ()
    {
        if (currentSelection == null)
            return;

        var currentContext = GetCurrentContext ();

        bool invalid =
            currentContext == null ||
            !currentContext.Items.Contains (currentSelection) ||
            !currentSelection.IsInteractable ();

        if (invalid) {
            currentSelection.Deselect ();
            currentSelection = null;
        }
    }

    public bool HasActiveContext ()
    {
        while (contextStack.Count > 0 && contextStack.Peek () == null)
            contextStack.Pop ();

        return contextStack.Count > 0;
    }

    private bool CanProcessInput ()
    {
        if (!inputEnabled)
            return false;

        if (!HasActiveContext ())
            return false;

        if (mainUIStateSystemLocated && !mainUIStateSystem.IsInputAllowed ()) {
            return false;
        }

        return true;
    }

    void findUIStateSystem ()
    {
        mainUIStateSystemLocated = mainUIStateSystem != null;

        if (!mainUIStateSystemLocated) {
            mainUIStateSystem = UIStateSystem.Instance;

            mainUIStateSystemLocated = mainUIStateSystem != null;
        }
    }

    private UINavigationContext GetCurrentContext ()
    {
        if (contextStack.Count == 0)
            return null;

        return contextStack.Peek ();
    }


    public void selectFirstInteractableItemOnCurrentContext ()
    {
        var context = GetCurrentContext ();

        if (context == null)
            return;

        foreach (var item in context.Items) {
            if (item != null && item.IsInteractable ()) {
                SetSelection (item);

                return;
            }
        }
    }

    public void selectFirstInteractableItemIfContextIsCurrent (UINavigationContext newContext)
    {
        var context = GetCurrentContext ();

        if (context == null)
            return;

        if (newContext != context) {
            return;
        }

        selectFirstInteractableItemOnCurrentContext ();
    }

    private void ClearAllEditing ()
    {
        foreach (var ctx in contextStack) {
            if (ctx != null) {
                ctx.ClearEditing ();

                NotifyEditingState (false);
            }
        }
    }


    public void checkRefreshItemListOnCurrentContext ()
    {
        var context = GetCurrentContext ();

        if (context == null)
            return;

        UIPage currentUIPage = context.gameObject.GetComponent<UIPage> ();

        if (currentUIPage != null) {
            currentUIPage.checkRefreshItemListOnCurrentContext ();
        }
    }

    void selectNextOrPreviousNavigationZonePanel (bool state)
    {
        var context = GetCurrentContext ();

        if (context == null)
            return;

        if (!context.isAllowQuickSelectionBetweenNavigationZonesEnabled ()) {
            return;
        }

        if (context.HasEditingItem ()) {
            return;
        }

        UINavigationItem currentItemToSelect = context.getNextNavigationZoneItem (state);

        if (showDebugPrint) {
            print ("selectNextOrPreviousNavigationZonePanel " + state);

            if (currentItemToSelect != null) {
                print ("found " + currentItemToSelect.name);
            } else {
                print ("not found");
            }

            if (currentItemToSelect != currentSelection) {
                print ("items different");
            } else {
                print ("items equal");
            }
        }

        if (currentItemToSelect != null && currentItemToSelect != currentSelection) {
            checkVibrationOnUINavigation ();

            SetSelection (currentItemToSelect);
        }
    }

    public void selectNavigationZonePanelByName (string zoneName)
    {
        if (!inputEnabled) {
            return;
        }

        var context = GetCurrentContext ();

        if (context == null)
            return;

        if (!context.isAllowQuickSelectionBetweenNavigationZonesEnabled ()) {
            return;
        }

        if (context.HasEditingItem ()) {
            return;
        }

        UINavigationItem currentItemToSelect = context.getNavigationZoneItemByName (zoneName);

        if (showDebugPrint) {
            print ("selectNavigationZonePanelByName " + zoneName);

            if (currentItemToSelect != null) {
                print ("found " + currentItemToSelect.name);
            } else {
                print ("not found");
            }

            if (currentItemToSelect != currentSelection) {
                print ("items different");
            } else {
                print ("items equal");
            }
        }

        if (currentItemToSelect != null && currentItemToSelect != currentSelection) {
            SetSelection (currentItemToSelect);
        }
    }

    void NotifyEditingState (bool editing)
    {
        blockKeyboardActive = false;

        var context = GetCurrentContext ();

        if (context != null) {
            blockKeyboardActive =
                editing &&

                context.HasEditingItem () &&

                context.CurrentEditingItem.BlocksKeyboardInput;
        }

        OnKeyboardInputBlockStateChanged?.Invoke (blockKeyboardActive);

        //calls to player input to pause/resume the input from keybarod 

        if (playerInputLocated) {
            playerInput.setIgnoreKeyboardInputActiveState (blockKeyboardActive);
        }

        editingItemActive = false;

        if (context != null) {
            editingItemActive =
                editing &&

                context.HasEditingItem ();
        }
    }

    public GameObject getCurrentSelection ()
    {
        if (inputEnabled) {
            if (currentSelection != null) {
                return currentSelection.gameObject;
            }
        }

        return null;
    }

    //it can be called when an UI item activates or deactivates its editing state
    //OnKeyboardInputBlockStateChanged += FUNCIONNAMETOCALLONSCRIPT;

    //INPUT FUNCTIONS

    public void inputSelectNextNavigationZonePanel ()
    {
        if (!inputEnabled) {
            return;
        }

        selectNextOrPreviousNavigationZonePanel (true);
    }

    public void inputSelectPreviousNavigationZonePanel ()
    {
        if (!inputEnabled) {
            return;
        }

        selectNextOrPreviousNavigationZonePanel (false);
    }

    public void inputSetMoveInputPausedState (bool state)
    {
        setUINavigationMoveInputPausedState (state);
    }

    public void inputSetSubmitInputPausedState (bool state)
    {
        setSubmitInputPausedState (state);
    }
}