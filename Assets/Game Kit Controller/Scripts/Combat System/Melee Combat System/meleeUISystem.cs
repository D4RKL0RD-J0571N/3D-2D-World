using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class meleeUISystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool meleeUIEnabled = true;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool meleeUIActive;
    public string currentMeleeWeaponName;

    [Space]
    [Header ("UI Components")]
    [Space]

    public Text meleeWeaponText;
    public RawImage meleeWeaponImage;
    public GameObject meleePanelGameObject;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventsOnMeleePanelStateChange;
    public UnityEvent eventOnEnableMeleePanel;
    public UnityEvent eventOnDisableMeleePanel;


    public void setCurrentMeleeWeaponName (string newName)
    {
        currentMeleeWeaponName = newName;

        meleeWeaponText.text = GKC_Utils.getInteractionObjectsLocalizationManagerLocalizedText (currentMeleeWeaponName);
    }

    public void setCurrrentMeleeWeaponIcon (Texture newIcon)
    {
        meleeWeaponImage.texture = newIcon;
    }

    public void enableOrDisableMeleeUI (bool state)
    {
        if (!meleeUIEnabled) {
            return;
        }

        if (meleeUIActive == state) {
            return;
        }

        meleeUIActive = state;

        if (meleeUIActive) {

        } else {

        }

        if (meleePanelGameObject.activeSelf != meleeUIActive) {
            meleePanelGameObject.SetActive (meleeUIActive);
        }

        checkEventsOnMeleePanelStateChange (meleeUIActive);
    }

    void checkEventsOnMeleePanelStateChange (bool state)
    {
        if (useEventsOnMeleePanelStateChange) {
            if (state) {
                eventOnEnableMeleePanel.Invoke ();
            } else {
                eventOnDisableMeleePanel.Invoke ();
            }
        }
    }
}
