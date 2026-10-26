using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class UINavigationItem : MonoBehaviour
{
    [Header ("Navigation")]
    [Space]

    [SerializeField] protected bool interactable = true;

    public bool setScaleOnSelectableEnabled = true;
    public float extraScaleOnSelectable = 1.1f;

    public bool useDefaultCustomScaleMultiplier;
    public float defaultCustomScaleMultiplier;

    public bool useOnlyVerticalScale;
    public bool useOnlyHorizontalScale;

    [Space]

    public RectTransform FocusTarget;

    [Space]

    [SerializeField] protected GameObject eventTarget;

    [Space]
    [Header ("Optional Explicit Navigation")]
    [Space]

    [SerializeField] protected UINavigationItem upOverride;
    [SerializeField] protected UINavigationItem downOverride;
    [SerializeField] protected UINavigationItem leftOverride;
    [SerializeField] protected UINavigationItem rightOverride;

    [Space]
    [Header ("Visual Colors")]
    [Space]

    [SerializeField] protected bool autoGetColorsFromUIElement = true;

    [SerializeField] protected Color normalColor = Color.white;
    [SerializeField] protected Color focusedColor = Color.cyan;
    [SerializeField] protected Color editingColor = Color.yellow;
    [SerializeField] protected Graphic visualTarget;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool isSelected;

    public bool isEditing;

    public bool isPressed;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventOnUIItemSelected;
    public UnityEvent eventOnUIItemSelected;

    [Space]

    public bool useEventOnUIItemDeselected;
    public UnityEvent eventOnUIItemDeselected;

    [Space]

    public bool useEventOnUIItemPressed;
    public UnityEvent eventOnUIItemPressed;


    [Space]
    [Header ("Components")]
    [Space]

    [SerializeField] protected RectTransform rectTransform;

    [SerializeField] protected ScrollRect cachedScrollRect;
    [SerializeField] protected RectTransform cachedViewport;
    [SerializeField] protected RectTransform cachedContent;

    protected PointerEventData pointerEventData;

    public ScrollRect ScrollRect => cachedScrollRect;

    public RectTransform Viewport => cachedViewport;

    public RectTransform Content => cachedContent;

    public bool HasViewport => cachedViewport != null;

    public bool HasScrollRect => cachedScrollRect != null;

    public bool HasContent => cachedContent != null;

    bool FocusTargetLocated;

    public RectTransform getFocusTarget ()
    {
        if (!FocusTargetLocated) {
            FocusTargetLocated = FocusTarget != null;

            if (!FocusTargetLocated) {
                FocusTarget = (RectTransform)transform;

                FocusTargetLocated = FocusTarget != null;
            }
        }

        return FocusTarget;
    }

    private readonly Vector3 [] viewportCorners = new Vector3 [4];
    private readonly Vector3 [] itemCorners = new Vector3 [4];


    public virtual void Bake ()
    {
        rectTransform = transform as RectTransform;

        bool ApplicationisPlaying = Application.isPlaying;

        AutoAssignComponents ();

        CacheScrollData (ApplicationisPlaying);

        CacheVisualState ();

        ApplyVisual ();

        if (!ApplicationisPlaying) {
            updateComponent ();

            print ("baking UI navigation item on editor" + gameObject.name);
        }
    }

    //public bool showDebugPrint;

    protected virtual void CacheScrollData (bool ApplicationisPlaying)
    {
        if (ApplicationisPlaying) {
            if (cachedScrollRect == null) {
                cachedScrollRect = transform.GetComponentInParent<ScrollRect> ();
            }
        } else {
            cachedScrollRect = transform.GetComponentInParent<ScrollRect> ();
        }

        if (cachedScrollRect == null) {
            cachedViewport = null;
            cachedContent = null;

            return;
        }

        cachedViewport = cachedScrollRect.viewport;

        if (cachedViewport == null)
            cachedViewport = cachedScrollRect.transform as RectTransform;

        cachedContent = cachedScrollRect.content;
    }

    protected virtual void AutoAssignComponents ()
    {

    }

    protected virtual void CacheVisualState ()
    {

    }

    public virtual bool IsVisible (float minimumVisiblePercentage)
    {
        if (!gameObject.activeInHierarchy)
            return false;

        if (!HasScrollRect)
            return true;

        if (!HasViewport)
            return true;

        return GetVisibleAreaPercentage () >= minimumVisiblePercentage;
    }

    public float GetVisibleAreaPercentage ()
    {
        if (!HasScrollRect)
            return 1f;

        if (!HasViewport)
            return 1f;

        return GetVisibleAreaPercentageInternal ();
    }

    protected virtual float GetVisibleAreaPercentageInternal ()
    {
        cachedViewport.GetWorldCorners (viewportCorners);
        rectTransform.GetWorldCorners (itemCorners);

        return CalculateVisibleArea (viewportCorners, itemCorners);
    }

    protected static float CalculateVisibleArea (Vector3 [] viewport, Vector3 [] item)
    {
        float viewportMinX = viewport [0].x;
        float viewportMaxX = viewport [2].x;
        float viewportMinY = viewport [0].y;
        float viewportMaxY = viewport [2].y;

        float itemMinX = item [0].x;
        float itemMaxX = item [2].x;
        float itemMinY = item [0].y;
        float itemMaxY = item [2].y;

        float overlapMinX = Mathf.Max (viewportMinX, itemMinX);
        float overlapMaxX = Mathf.Min (viewportMaxX, itemMaxX);

        float overlapMinY = Mathf.Max (viewportMinY, itemMinY);
        float overlapMaxY = Mathf.Min (viewportMaxY, itemMaxY);

        if (overlapMaxX <= overlapMinX ||
            overlapMaxY <= overlapMinY) {
            return 0f;
        }

        float visibleArea =
            (overlapMaxX - overlapMinX) *
            (overlapMaxY - overlapMinY);

        float totalArea =
            (itemMaxX - itemMinX) *
            (itemMaxY - itemMinY);

        if (totalArea <= Mathf.Epsilon)
            return 0f;

        return visibleArea / totalArea;
    }

    // ======================================================
    // SELECTION
    // ======================================================

    public virtual void Select ()
    {
        if (isSelected)
            return;

        if (!isSelected) {
            checkEventOnUIItemSelected ();
        }

        isSelected = true;

        ApplyVisual ();

        OnSelected ();
    }

    public virtual void Deselect ()
    {
        if (!isSelected)
            return;

        if (isSelected) {
            checkEventOnUIItemDeselected ();
        }

        isSelected = false;

        ApplyVisual ();

        OnDeselected ();
    }

    protected virtual void OnSelected ()
    {

    }

    protected virtual void OnDeselected ()
    {

    }

    public void checkEventOnUIItemSelected ()
    {
        if (useEventOnUIItemSelected) {
            eventOnUIItemSelected.Invoke ();
        }
    }

    public void checkEventOnUIItemDeselected ()
    {
        if (useEventOnUIItemDeselected) {
            eventOnUIItemDeselected.Invoke ();
        }
    }

    public void checkEventOnUIItemPressed ()
    {
        if (useEventOnUIItemPressed) {
            eventOnUIItemPressed.Invoke ();
        }
    }

    // ======================================================
    // EDITING
    // ======================================================

    public virtual bool BlocksKeyboardInput => false;

    public void BeginEditInternal ()
    {
        if (!SupportsEditing ())
            return;

        if (!IsInteractable ())
            return;

        isEditing = true;

        ApplyVisual ();

        OnBeginEdit ();
    }

    public void EndEditInternal ()
    {
        isEditing = false;

        ApplyVisual ();

        OnEndEdit ();
    }

    public bool IsEditing ()
    {
        return isEditing;
    }

    public bool IsPressed ()
    {
        return isPressed;
    }

    protected virtual void OnBeginEdit ()
    {

    }
    protected virtual void OnEndEdit ()
    {

    }

    public virtual bool SupportsEditing () => false;


    // ======================================================
    // VISUALS
    // ======================================================


    protected virtual void ApplyVisual ()
    {
        var graphic = GetTargetGraphic ();

        if (graphic != null) {
            if (isEditing) {
                graphic.color = editingColor;
            } else if (isSelected) {
                graphic.color = focusedColor;
            } else {
                graphic.color = normalColor;
            }
        }

        if (setScaleOnSelectableEnabled) {
            if (isSelected || isEditing) {
                if (useOnlyVerticalScale || useOnlyHorizontalScale) {
                    if (useOnlyVerticalScale) {
                        transform.localScale = new Vector3 (1, extraScaleOnSelectable, 1);
                    }

                    if (useOnlyHorizontalScale) {
                        transform.localScale = new Vector3 (extraScaleOnSelectable, 1, 1);
                    }
                } else {
                    transform.localScale = Vector3.one * extraScaleOnSelectable;
                }
            } else {
                if (useDefaultCustomScaleMultiplier) {
                    transform.localScale = Vector3.one * defaultCustomScaleMultiplier;
                } else {
                    transform.localScale = Vector3.one;
                }
            }
        }
    }

    protected virtual Graphic GetTargetGraphic ()
    {
        return visualTarget;
    }

    // ======================================================
    // EVENT SYSTEM
    // ======================================================

    protected virtual void EnsurePointerEventData ()
    {
        if (EventSystem.current == null)
            return;

        if (pointerEventData == null) {
            pointerEventData = new PointerEventData (EventSystem.current);
        }

        pointerEventData.Reset ();
    }

    protected virtual void SendPointerDownEvent ()
    {
        EnsurePointerEventData ();

        if (pointerEventData == null)
            return;

        ExecuteEvents.Execute (
            GetEventTarget (),
            pointerEventData,
            ExecuteEvents.pointerDownHandler);
    }

    protected virtual void SendPointerUpEvent ()
    {
        EnsurePointerEventData ();

        if (pointerEventData == null)
            return;

        ExecuteEvents.Execute (
            GetEventTarget (),
            pointerEventData,
            ExecuteEvents.pointerUpHandler);
    }

    protected virtual void SendSubmitEvent ()
    {
        if (EventSystem.current == null)
            return;

        ExecuteEvents.Execute (
            GetEventTarget (),
            new BaseEventData (EventSystem.current),
            ExecuteEvents.submitHandler);
    }

    protected virtual GameObject GetEventTarget ()
    {
        if (eventTarget != null)
            return eventTarget;

        return gameObject;
    }

    // ======================================================
    // INPUT
    // ======================================================

    public virtual void SubmitDown ()
    {
        if (ShouldSendPointerDownEvent ()) {
            SendPointerDownEvent ();
        }

        checkEventOnUIItemPressed ();

        isPressed = true;
    }

    public virtual void SubmitUp ()
    {
        if (ShouldSendPointerUpEvent ()) {
            SendPointerUpEvent ();
        }

        isPressed = false;
    }

    public virtual void Submit ()
    {
        OnSubmit ();

        if (ShouldSendSubmitEvent ()) {
            SendSubmitEvent ();
        }

    }

    public virtual void Cancel ()
    {
        OnCancel ();
    }

    protected virtual void OnSubmit ()
    {

    }

    protected virtual void OnCancel ()
    {

    }

    // ======================================================
    // NAVIGATION
    // ======================================================

    public virtual bool ConsumeNavigation (Vector2 dir)
    {
        return false;
    }

    public UINavigationItem GetOverride (Vector2 dir)
    {
        if (dir == Vector2.up)
            return upOverride;

        if (dir == Vector2.down)
            return downOverride;

        if (dir == Vector2.left)
            return leftOverride;

        if (dir == Vector2.right)
            return rightOverride;

        return null;
    }

    // ======================================================
    // STATE
    // ======================================================

    public virtual bool IsInteractable ()
    {
        return interactable && gameObject.activeInHierarchy;
    }

    public virtual bool IsSelectableForNavigation (float visibleThreshold)
    {
        if (!IsInteractable ())
            return false;

        if (!HasScrollRect)
            return true;

        return IsVisible (visibleThreshold);
    }

    public virtual Vector2 GetNavigationPosition ()
    {
        return rectTransform.position;
    }

    public RectTransform GetNavigationRectTransform ()
    {
        return transform as RectTransform;
    }

    protected virtual bool ShouldSendPointerDownEvent ()
    {
        return true;
    }

    protected virtual bool ShouldSendSubmitEvent ()
    {
        return true;
    }

    protected virtual bool ShouldSendPointerUpEvent ()
    {
        return true;
    }

    public void checkItemObjectsOnBake ()
    {
        Bake ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update UI Navigation Item " + gameObject.name + " " + GKC_Utils.getRandomString (6), gameObject);
    }
}