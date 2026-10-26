using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class hideCharacterFixedPlaceSystemPlayerManagement : MonoBehaviour
{
    [Header ("Debug")]
    [Space]

    public bool playerHiding;

    public bool playerHidingOnFreeZone;

    [Space]

    public hideCharacterFixedPlaceSystem currentFixedHideSystem;

    public hideFromEnemiesSystem currentHideFromEnemiesSystem;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventsOnStateChange;
    public UnityEvent evenOnStateEnabled;
    public UnityEvent eventOnStateDisabled;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject characterGameObject;


    public void setPlayerHidingState (bool state)
    {
        playerHiding = state;

        checkEventsOnStateChange (playerHiding);
    }

    public void setCurrentFixedHideSystem (hideCharacterFixedPlaceSystem newFixedHideSystem)
    {
        currentFixedHideSystem = newFixedHideSystem;
    }


    //FREE HIDE ZONE FUNCTIONS
    public void setPlayerHidingOnFreeZoneState (bool state)
    {
        playerHidingOnFreeZone = state;
    }

    public void setCurrentHideFromEnemiesSystem (hideFromEnemiesSystem newHideFromEnemiesSystem)
    {
        currentHideFromEnemiesSystem = newHideFromEnemiesSystem;
    }

    public void removeCurrentHideFromEnemiesSystem ()
    {
        if (playerHidingOnFreeZone) {
            currentHideFromEnemiesSystem.removeCharacterFromHiddenZone (characterGameObject);
        }
    }

    //CALL INPUT FUNCTIONS TO CURRENT HIDE SYSTEM
    public void hideInputResetCameraTransform ()
    {
        if (!playerHiding) {
            return;
        }

        if (currentFixedHideSystem != null) {
            currentFixedHideSystem.inputResetCameraTransform ();
        }
    }

    public void hideInputSetIncreaseZoomStateByButton (bool state)
    {
        if (!playerHiding) {
            return;
        }

        if (currentFixedHideSystem != null) {
            currentFixedHideSystem.inputSetIncreaseZoomStateByButton (state);
        }
    }

    public void hideInputSetDecreaseZoomStateByButton (bool state)
    {
        if (!playerHiding) {
            return;
        }

        if (currentFixedHideSystem != null) {
            currentFixedHideSystem.inputSetDecreaseZoomStateByButton (state);
        }
    }

    public void hideInputSetZoomValueByMouseWheel (bool state)
    {
        if (!playerHiding) {
            return;
        }

        if (currentFixedHideSystem != null) {
            currentFixedHideSystem.inputSetZoomValueByMouseWheel (state);
        }
    }

    public void checkEventsOnStateChange (bool state)
    {
        if (useEventsOnStateChange) {
            if (state) {
                evenOnStateEnabled.Invoke ();
            } else {
                eventOnStateDisabled.Invoke ();
            }
        }
    }
}
