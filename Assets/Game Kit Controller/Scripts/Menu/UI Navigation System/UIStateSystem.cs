using UnityEngine;

public enum UIState
{
    Navigation,
    Editing,
    Modal,
    Blocked
}

public class UIStateSystem : MonoBehaviour
{
    public static UIStateSystem _UIStateSystemInstance;

    public static UIStateSystem Instance { get { return _UIStateSystemInstance; } }

    [SerializeField] private UIState currentState = UIState.Navigation;

    bool instanceInitialized;

    private void Awake ()
    {
        getComponentInstance ();
    }

    public void getComponentInstance ()
    {
        if (instanceInitialized) {
            return;
        }

        if (_UIStateSystemInstance != null && _UIStateSystemInstance != this) {
            Destroy (this.gameObject);

            return;
        }

        _UIStateSystemInstance = this;

        instanceInitialized = true;
    }

    public UIState CurrentState => currentState;

    public bool IsInputAllowed () => currentState != UIState.Blocked;
    public bool IsNavigationAllowed () => currentState == UIState.Navigation;

    public void SetState (UIState state)
    {
        currentState = state;
    }
}