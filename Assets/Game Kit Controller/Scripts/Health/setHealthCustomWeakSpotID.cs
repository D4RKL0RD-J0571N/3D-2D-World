using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class setHealthCustomWeakSpotID : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool healthCustomWeakSpotEnabled = true;

    [Space]

    public List<customStateInfo> customStateInfoList = new List<customStateInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool healthCustomWeakSpotIgnoreActive;

    [Space]
    [Header ("Components")]
    [Space]

    public health mainHealth;

    public void setHealthCustomWeakSpotIDByName (string newName)
    {
        if (!healthCustomWeakSpotEnabled) {
            return;
        }

        if (healthCustomWeakSpotIgnoreActive) {
            return;
        }

        int currentIndex = customStateInfoList.FindIndex (s => s.Name.Equals (newName));

        if (currentIndex > -1) {
            customStateInfo currentCustomStateInfo = customStateInfoList [currentIndex];

            if (currentCustomStateInfo.customStateEnabled) {
                for (int i = 0; i < currentCustomStateInfo.healthCustomWeakSpotInfoList.Count; i++) {

                    healthCustomWeakSpotInfo currentHealthCustomWeakSpotInfo =
                        currentCustomStateInfo.healthCustomWeakSpotInfoList [i];

                    mainHealth.setUseCustomStateHealthAmountOnSpotEnabledState (currentHealthCustomWeakSpotInfo.useCustomStateHealthAmountOnSpotEnabled,
                                               currentHealthCustomWeakSpotInfo.Name);

                    if (currentHealthCustomWeakSpotInfo.useCustomStateHealthAmountOnSpotEnabled) {
                        mainHealth.setCurrentCustomStateHealthAmountOnSpotID (currentHealthCustomWeakSpotInfo.ID,
                            currentHealthCustomWeakSpotInfo.Name);
                    }

                    if (currentHealthCustomWeakSpotInfo.setSendFunctionWhenDamageState) {
                        mainHealth.setSendFunctionWhenDamageStateOnWeakSpot (currentHealthCustomWeakSpotInfo.Name,
                            currentHealthCustomWeakSpotInfo.sendFunctionWhenDamageState);
                    }

                    if (currentHealthCustomWeakSpotInfo.setUseHealthAmountOnSpot) {
                        mainHealth.setUseHealthAmountOnSpotState (currentHealthCustomWeakSpotInfo.useHealthAmountOnSpotState,
                            currentHealthCustomWeakSpotInfo.Name);
                    }
                }
            }
        }
    }

    public void enableOrDisableCustomStateInfo (bool state, string newName)
    {
        int currentIndex = customStateInfoList.FindIndex (s => s.Name.Equals (newName));

        if (currentIndex > -1) {
            customStateInfo currentCustomStateInfo = customStateInfoList [currentIndex];

            currentCustomStateInfo.customStateEnabled = state;
        }
    }

    public void enableCustomStateInfo (string newName)
    {
        enableOrDisableCustomStateInfo (true, newName);
    }

    public void disableCustomStateInfo (string newName)
    {
        enableOrDisableCustomStateInfo (false, newName);
    }

    public void setHealthCustomWeakSpotEnabledState (bool state)
    {
        healthCustomWeakSpotEnabled = state;
    }

    public void setHealthCustomWeakSpotIgnoreActiveState (bool state)
    {
        healthCustomWeakSpotIgnoreActive = state;
    }

    //EDITOR FUNCTIONS
    public void setHealthCustomWeakSpotEnabledStateFromEditor (bool state)
    {
        setHealthCustomWeakSpotEnabledState (state);

        updateComponent ();
    }

    public void enableCustomStateInfoFromEditor (string newName)
    {
        enableOrDisableCustomStateInfo (true, newName);

        updateComponent ();
    }

    public void disableCustomStateInfoFromEditor (string newName)
    {
        enableOrDisableCustomStateInfo (false, newName);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Health Custom Info " + gameObject.name, gameObject);
    }

    [System.Serializable]
    public class customStateInfo
    {
        public string Name;

        public bool customStateEnabled = true;

        [Space]

        public List<healthCustomWeakSpotInfo> healthCustomWeakSpotInfoList = new List<healthCustomWeakSpotInfo> ();
    }

    [System.Serializable]
    public class healthCustomWeakSpotInfo
    {
        public string Name;

        public int ID;

        public bool useCustomStateHealthAmountOnSpotEnabled;

        [Space]

        public bool setSendFunctionWhenDamageState;

        public bool sendFunctionWhenDamageState;

        [Space]

        public bool setUseHealthAmountOnSpot;

        public bool useHealthAmountOnSpotState;

    }
}
