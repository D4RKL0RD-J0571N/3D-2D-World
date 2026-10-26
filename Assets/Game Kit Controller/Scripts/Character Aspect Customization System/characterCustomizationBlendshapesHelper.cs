using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class characterCustomizationBlendshapesHelper : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public GameObject playerGameObject;

    public characterCustomizationManager mainCharacterCustomizationManager;

    public characterModelListManager mainCharacterModelListManager;

    [Space]
    [Header ("Debug Temporal BlendShapes")]
    [Space]

    public List<characterCustomizationManager.temporalBlendshapeInfo> temporalBlendshapeInfoList = new List<characterCustomizationManager.temporalBlendshapeInfo> ();

    [Space]
    [Header ("Debug Temporal Accessories")]
    [Space]

    public List<string> temporalCurrentAccessoriesList = new List<string> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool componentLocated;


    public void storeBlendshapesCustomization ()
    {
        if (!componentLocated) {
            checkMainCHaracterCustomizatationManager ();
        }

        if (componentLocated) {
            temporalBlendshapeInfoList = mainCharacterCustomizationManager.getTemporalBlendshapeInfoList ();

            temporalCurrentAccessoriesList = mainCharacterCustomizationManager.getCurrentAccessoriesList ();
        }
    }

    public void setBlendshapeList ()
    {
        if (!componentLocated) {
            checkMainCHaracterCustomizatationManager ();
        }

        if (componentLocated) {
            mainCharacterCustomizationManager.setBlendshapeList (temporalBlendshapeInfoList);

            mainCharacterCustomizationManager.setCurrentAccessoriesList (temporalCurrentAccessoriesList);
        }
    }

    public void checkMainCHaracterCustomizatationManager ()
    {
        if (!componentLocated) {
            if (mainCharacterCustomizationManager == null && playerGameObject != null) {
                getMainCharacterCustomizationManager ();
            }

            componentLocated = mainCharacterCustomizationManager != null;
        }
    }

    public void setPlayerGameObject (GameObject newObject)
    {
        if (playerGameObject != newObject) {
            playerGameObject = newObject;

            getMainCharacterCustomizationManager ();
        }
    }

    void getMainCharacterCustomizationManager ()
    {
        mainCharacterCustomizationManager = playerGameObject.GetComponentInChildren<characterCustomizationManager> ();

        componentLocated = mainCharacterCustomizationManager != null;
    }

    public int getCurrentCharacterModelID ()
    {
        if (mainCharacterModelListManager != null) {
            return mainCharacterModelListManager.getCurrentCharacterModelID ();
        }

        return -1;
    }

    public void setCurrentCharacterModelID (int newID)
    {
        if (mainCharacterModelListManager != null) {
            mainCharacterModelListManager.replaceCharacterModelByID (newID);
        }
    }
}
