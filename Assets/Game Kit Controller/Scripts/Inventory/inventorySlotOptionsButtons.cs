using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class inventorySlotOptionsButtons : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public int buttonHeight;

    public int extraHeight;

    public int panelBackgroundOffset;

    [Space]
    [Header ("Show Buttons Settings")]
    [Space]

    public bool useButtonEnabled = true;
    public bool equipButtonEnabled = true;
    public bool unEquipButtonEnabled = true;
    public bool dropButtonEnabled = true;
    public bool combineButtonEnabled = true;
    public bool examineButtonEnabled = true;
    public bool holdButtonEnabled = true;
    public bool discardButtonEnabled = true;
    public bool setOnQuickSlotsButtonEnabled = true;
    public bool repairButtonEnabled = true;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool panelActive;

    public int numberOfOptionsEnabled;

    [Space]
    [Header ("UI Navigation Settings")]
    [Space]

    public UIPage mainUIPage;

    [Space]
    [Header ("UI Components Settings")]
    [Space]

    public GameObject useButton;
    public GameObject equipButton;
    public GameObject unEquipButton;
    public GameObject dropButton;
    public GameObject combineButton;
    public GameObject examineButton;
    public GameObject holdButton;
    public GameObject discardButton;
    public GameObject setOnQuickSlotsButton;
    public GameObject repairButton;

    [Space]

    public RectTransform optionsPanel;

    public RectTransform panelBackground;


    public void setButtonsState (bool useState, bool equipState, bool unEquipState, bool dropState,
        bool combineState, bool examineState, bool holdState, bool discardState,
        bool setOnQuickSlotsState, bool repairState)
    {
        if (!useButtonEnabled) {
            useState = false;
        }

        if (!equipButtonEnabled) {
            equipState = false;
        }

        if (!unEquipButtonEnabled) {
            unEquipState = false;
        }

        if (!dropButtonEnabled) {
            dropState = false;
        }

        if (!combineButtonEnabled) {
            combineState = false;
        }

        if (!examineButtonEnabled) {
            examineState = false;
        }

        if (!holdButtonEnabled) {
            holdState = false;
        }

        if (!discardButtonEnabled) {
            discardState = false;
        }

        if (!setOnQuickSlotsButtonEnabled) {
            setOnQuickSlotsState = false;
        }

        if (!repairButtonEnabled) {
            repairState = false;
        }

        if (useButton != null && useButton.activeSelf != useState) {
            useButton.SetActive (useState);
        }

        if (equipButton != null && equipButton.activeSelf != equipState) {
            equipButton.SetActive (equipState);
        }

        if (unEquipButton != null && unEquipButton.activeSelf != unEquipState) {
            unEquipButton.SetActive (unEquipState);
        }

        if (dropButton != null && dropButton.activeSelf != dropState) {
            dropButton.SetActive (dropState);
        }

        if (combineButton != null && combineButton.activeSelf != combineState) {
            combineButton.SetActive (combineState);
        }

        if (examineButton != null && examineButton.activeSelf != examineState) {
            examineButton.SetActive (examineState);
        }

        if (holdButton != null && holdButton.activeSelf != holdState) {
            holdButton.SetActive (holdState);
        }

        if (discardButton != null && discardButton.activeSelf != discardState) {
            discardButton.SetActive (discardState);
        }

        if (setOnQuickSlotsButton != null && setOnQuickSlotsButton.activeSelf != setOnQuickSlotsState) {
            setOnQuickSlotsButton.SetActive (setOnQuickSlotsState);
        }

        if (repairButton != null && repairButton.activeSelf != repairState) {
            repairButton.SetActive (repairState);
        }

        numberOfOptionsEnabled = 0;

        if (useState) {
            numberOfOptionsEnabled++;
        }

        if (equipState) {
            numberOfOptionsEnabled++;
        }

        if (unEquipState) {
            numberOfOptionsEnabled++;
        }

        if (dropState) {
            numberOfOptionsEnabled++;
        }

        if (combineState) {
            numberOfOptionsEnabled++;
        }

        if (examineState) {
            numberOfOptionsEnabled++;
        }

        if (holdState) {
            numberOfOptionsEnabled++;
        }

        if (discardState) {
            numberOfOptionsEnabled++;
        }

        if (setOnQuickSlotsState) {
            numberOfOptionsEnabled++;
        }

        if (repairState) {
            numberOfOptionsEnabled++;
        }

        optionsPanel.sizeDelta = new Vector2 (optionsPanel.sizeDelta.x, (buttonHeight * numberOfOptionsEnabled) + extraHeight);

        panelBackground.sizeDelta = new Vector2 (panelBackground.sizeDelta.x, (buttonHeight * numberOfOptionsEnabled) + panelBackgroundOffset);
    }

    public void setPanelActiveState (bool state)
    {
        if (numberOfOptionsEnabled == 0) {
            panelActive = false;
        }

        panelActive = state;

        if (optionsPanel.gameObject.activeSelf != state) {
            optionsPanel.gameObject.SetActive (state);
        }
    }

    public bool isPanelActive ()
    {
        return panelActive;
    }

    public UIPage getMainUIPage ()
    {
        return mainUIPage;
    }
}
