using GKC.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static InputAxisWrapper;

public class inventoryBankUISystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool useNumberOfElementsOnPressUp;
    public bool useNumberOfElementsWhenIconNotMoved;
    public bool dropFullObjectsAmount = true;
    public bool dragAndDropIcons = true;

    public float timeToDrag = 0.5f;

    public float configureNumberObjectsToUseRate = 0.4f;
    public float fasterNumberObjectsToUseRate = 0.1f;
    public float waitTimeToUseFasterNumberObjectsToUseRate = 1;

    public string mainInventoryManagerName = "Main Inventory Manager";

    public bool allowToDropPlayerInventoryIntoCustomInventoryBanks;

    public bool closeMenuIfInventoryBankObjectDestroyed;

    public bool allowToSelectNewSlotObjectIfSettingAmountOnPreviousEnabled = true;

    [Space]

    public string inventoryBankFullMessage = "Inventory Bank is full";

    [Space]

    public bool useCustomMaxInventoryBankSlotsAmount;

    public bool showEmptySlotsOnUseMaxInventoryBankSlotsAmount;

    public bool useMaxInventoryBankSlotsAmount;
    public int maxInventoryBankSlotsAmount;

    [Space]
    [Header ("Zoom/Rotation Settings")]
    [Space]

    public float zoomSpeed;
    public float maxZoomValue;
    public float minZoomValue;
    public float rotationSpeed;

    public bool useBlurUIPanel = true;

    [Space]
    [Header ("Debug State")]
    [Space]

    public bool menuOpened;

    public bool slotToMoveFound;
    public GameObject slotFound;
    public bool draggedFromPlayerInventoryList;
    public bool draggedFromBankInventoryList;
    public bool droppedInPlayerInventoryList;
    public bool droppedInBankInventoryList;

    public bool canDropObjectsToInventoryBank = true;
    public bool touching;

    public bool draggingSlotActive;

    public bool resetDragDropElementsEnabled = true;

    public inventoryInfo currentSlotToMoveInventoryObject;
    public inventoryInfo currentSlotFoundInventoryObject;

    [Space]
    [Header ("Debug List")]
    [Space]

    public List<inventoryInfo> playerInventoryList = new List<inventoryInfo> ();

    [Space]

    public List<inventoryInfo> currentBankInventoryList = new List<inventoryInfo> ();

    [Space]

    public List<inventoryMenuIconElement> playerIconElementList = new List<inventoryMenuIconElement> ();
    public List<inventoryMenuIconElement> bankIconElementList = new List<inventoryMenuIconElement> ();

    [Space]

    public bool showDebugPrint;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsOnOpenCloseInventoryBank;
    public UnityEvent eventOnOpenInventoryBank;
    public UnityEvent eventOnCloseInventoryBank;

    public bool useEventOnUpdateInventoryGrids;
    public UnityEvent eventOnUpdateInventoryGrids;

    [Space]
    [Header ("UI Elements")]
    [Space]

    public RawImage inventoryMenuRenderTexture;

    public Transform lookObjectsPosition;

    public GameObject slotToMove;
    public GameObject menuBackground;
    public GameObject inventoryObjectRenderPanel;

    public Camera inventoryCamera;

    public GameObject inventoryMenu;

    public GameObject playerInventorySlots;
    public GameObject playerInventorySlotsContent;
    public GameObject playerInventorySlot;
    public Scrollbar playerInventoryScrollbar;

    public GameObject bankInventorySlots;
    public GameObject bankInventorySlotsContent;
    public GameObject bankInventorySlot;
    public Scrollbar bankInventoryScrollbar;

    public GameObject numberOfElementsWindow;
    public Text numberOfObjectsToUseText;

    [Space]
    [Header ("Components")]
    [Space]

    public inventoryManager playerInventoryManager;

    public usingDevicesSystem usingDevicesManager;

    public inventoryListManager mainInventoryListManager;

    public inventoryBankManager mainInventoryBankManager;

    public playerInputManager playerInput;

    public menuPause pauseManager;

    inventoryInfo currentInventoryObject;
    Coroutine resetCameraFov;

    float originalFov;
    GameObject objectInCamera;
    int numberOfObjectsToUse;

    bool touchPlatform;

    Touch currentTouch;

    InputAxisWrapper.TouchPressType touchPressType;

    bool useNewInputSystem;

    Vector2 currentTouchPosition;


    readonly List<RaycastResult> captureRaycastResults = new List<RaycastResult> ();

    bool zoomingIn;
    bool zoomingOut;

    bool enableRotation;

    inventoryBankSystem currentInventoryBankSystem;

    GameObject currentInventoryBankObject;

    bool useInventoryFromThisBank;

    int objectToMoveIndex;
    int objectFoundIndex;

    int currentAmountToMove;
    int currentBankInventoryListIndex;
    bool currentObjectAlreadyInBankList;
    bool settingAmountObjectsToMove;

    float lastTimeTouched;

    bool addingObjectToUse;
    bool removinvObjectToUse;
    float lastTimeAddObjectToUse;
    float lastTimeRemoveObjectToUse;
    bool useFasterNumberObjectsToUseRateActive;
    float lastTimeConfigureNumberOfObjects;

    inventoryObject currentInventoryObjectManager;
    float currentMaxZoomValue;
    float currentMinZoomValue;

    Vector2 axisValues;

    bool mainInventoryManagerLocated;

    Coroutine updateCoroutine;

    float lastTimeSlotToMoveFound = 0;

    bool replacingObjectsActive;


    void Start ()
    {
        originalFov = inventoryCamera.fieldOfView;

        inventoryMenu.SetActive (false);

        playerInventorySlot.SetActive (false);

        bankInventorySlot.SetActive (false);

        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();
    }

    void checkGetMainInventoryManager ()
    {
        if (!mainInventoryManagerLocated) {
            mainInventoryManagerLocated = mainInventoryListManager != null;

            if (!mainInventoryManagerLocated) {
                mainInventoryListManager = inventoryListManager.Instance;

                mainInventoryManagerLocated = mainInventoryListManager != null;
            }

            if (!mainInventoryManagerLocated) {
                GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (inventoryListManager.getMainManagerName (), typeof (inventoryListManager), true);

                mainInventoryListManager = inventoryListManager.Instance;

                mainInventoryManagerLocated = (mainInventoryListManager != null);
            }

            if (!mainInventoryManagerLocated) {
                mainInventoryListManager = GKC_Utils.findObjectByComponent<inventoryListManager> ();

                mainInventoryManagerLocated = mainInventoryListManager != null;
            }
        }
    }

    public void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }
    }

    IEnumerator updateSystemCoroutine ()
    {
        var waitTime = new WaitForSecondsRealtime (0.00001f);

        while (true) {
            updateSystem ();

            yield return waitTime;
        }
    }

    void updateSystem ()
    {
        if (menuOpened) {
            if (enableRotation) {
                axisValues = playerInput.getPlayerMouseAxis ();
                objectInCamera.transform.Rotate (inventoryCamera.transform.up, -Mathf.Deg2Rad * rotationSpeed * axisValues.x, Space.World);
                objectInCamera.transform.Rotate (inventoryCamera.transform.right, Mathf.Deg2Rad * rotationSpeed * axisValues.y, Space.World);
            }

            if (currentInventoryObjectManager != null && currentInventoryObjectManager.useZoomRange) {
                currentMaxZoomValue = currentInventoryObjectManager.maxZoom;
                currentMinZoomValue = currentInventoryObjectManager.minZoom;
            } else {
                currentMaxZoomValue = maxZoomValue;
                currentMinZoomValue = minZoomValue;
            }

            if (zoomingIn) {
                if (inventoryCamera.fieldOfView > currentMaxZoomValue) {
                    inventoryCamera.fieldOfView -= Time.deltaTime * zoomSpeed;
                } else {
                    inventoryCamera.fieldOfView = currentMaxZoomValue;
                }
            }

            if (zoomingOut) {
                if (inventoryCamera.fieldOfView < currentMinZoomValue) {
                    inventoryCamera.fieldOfView += Time.deltaTime * zoomSpeed;
                } else {
                    inventoryCamera.fieldOfView = currentMinZoomValue;
                }
            }

            if (addingObjectToUse) {
                if (!useFasterNumberObjectsToUseRateActive) {
                    if (Time.time > configureNumberObjectsToUseRate + lastTimeAddObjectToUse) {

                        lastTimeAddObjectToUse = Time.time;

                        addObjectToUse ();
                    }

                    if (Time.time > lastTimeConfigureNumberOfObjects + waitTimeToUseFasterNumberObjectsToUseRate) {
                        useFasterNumberObjectsToUseRateActive = true;
                    }
                } else {
                    if (Time.time > fasterNumberObjectsToUseRate + lastTimeAddObjectToUse) {
                        lastTimeAddObjectToUse = Time.time;

                        addObjectToUse ();
                    }
                }
            }

            if (removinvObjectToUse) {
                if (!useFasterNumberObjectsToUseRateActive) {
                    if (Time.time > configureNumberObjectsToUseRate + lastTimeRemoveObjectToUse) {

                        lastTimeRemoveObjectToUse = Time.time;

                        removeObjectToUse ();
                    }

                    if (Time.time > lastTimeConfigureNumberOfObjects + waitTimeToUseFasterNumberObjectsToUseRate) {
                        useFasterNumberObjectsToUseRateActive = true;
                    }
                } else {
                    if (Time.time > fasterNumberObjectsToUseRate + lastTimeRemoveObjectToUse) {
                        lastTimeRemoveObjectToUse = Time.time;

                        removeObjectToUse ();
                    }
                }
            }

            if (settingAmountObjectsToMove && !allowToSelectNewSlotObjectIfSettingAmountOnPreviousEnabled) {
                return;
            }

            checkInventoryObjectSlotsInput ();

            if (closeMenuIfInventoryBankObjectDestroyed) {
                if (currentInventoryBankObject == null && currentInventoryBankSystem == null) {
                    openOrCloseInventoryBankMenuByButton ();

                    return;
                }
            }
        }
    }

    public void setCurrentInventoryBankObject (GameObject inventoryBankGameObject)
    {
        currentInventoryBankObject = inventoryBankGameObject;
    }

    public void setCurrentInventoryBankSystem (GameObject inventoryBankGameObject)
    {
        currentInventoryBankSystem = inventoryBankGameObject.GetComponent<inventoryBankSystem> ();

        useInventoryFromThisBank = currentInventoryBankSystem.useInventoryFromThisBank;

        if (useInventoryFromThisBank) {
            currentBankInventoryList = currentInventoryBankSystem.getBankInventoryList ();

            if (!allowToDropPlayerInventoryIntoCustomInventoryBanks) {
                canDropObjectsToInventoryBank = false;
            }

            if (!useCustomMaxInventoryBankSlotsAmount) {
                useMaxInventoryBankSlotsAmount = currentInventoryBankSystem.useMaxInventoryBankSlotsAmount;
                maxInventoryBankSlotsAmount = currentInventoryBankSystem.maxInventoryBankSlotsAmount;
            }
        } else {
            mainInventoryListManager = inventoryListManager.Instance;

            bool mainInventoryManagerLocated = mainInventoryListManager != null;

            if (!mainInventoryManagerLocated) {
                GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (inventoryListManager.getMainManagerName (), typeof (inventoryListManager), true);

                mainInventoryListManager = inventoryListManager.Instance;

                mainInventoryManagerLocated = (mainInventoryListManager != null);
            }

            if (!mainInventoryManagerLocated) {
                mainInventoryListManager = GKC_Utils.findObjectByComponent<inventoryListManager> ();

                mainInventoryManagerLocated = mainInventoryListManager != null;
            }

            if (mainInventoryManagerLocated) {
                mainInventoryBankManager = mainInventoryListManager.getMainInventoryBankManager ();
            }

            if (mainInventoryBankManager != null) {
                currentBankInventoryList = mainInventoryBankManager.getBankInventoryList ();

                if (!useCustomMaxInventoryBankSlotsAmount) {
                    useMaxInventoryBankSlotsAmount = mainInventoryBankManager.useMaxInventoryBankSlotsAmount;
                    maxInventoryBankSlotsAmount = mainInventoryBankManager.maxInventoryBankSlotsAmount;
                }
            } else {
                print ("WARNING: No Inventory Bank Manager found in the scene, make sure to add the Main Inventory Manager prefab to the scene");
            }

            canDropObjectsToInventoryBank = true;
        }

        menuBackground.SetActive (!useInventoryFromThisBank);

        inventoryObjectRenderPanel.SetActive (!useInventoryFromThisBank);
    }

    public void openOrCloseInventoryBankMenuByButton ()
    {
        bool vendorObjectNotFound = false;

        bool currentInventoryBankObjectNotFound = false;

        if (usingDevicesManager != null) {
            if (currentInventoryBankObject != null) {
                if (usingDevicesManager.existInDeviceList (currentInventoryBankObject)) {
                    usingDevicesManager.useDevice ();
                } else {
                    usingDevicesManager.useCurrentDevice (currentInventoryBankObject);

                    vendorObjectNotFound = true;
                }
            } else {
                usingDevicesManager.useDevice ();

                currentInventoryBankObjectNotFound = true;
            }
        }

        if (currentInventoryBankSystem != null) {
            currentInventoryBankSystem.setUsingInventoryBankState (menuOpened);
        }

        if (vendorObjectNotFound) {
            if (usingDevicesManager != null) {
                usingDevicesManager.removeDeviceFromList (currentInventoryBankObject);

                usingDevicesManager.updateClosestDeviceList ();
            }
        }

        if (currentInventoryBankObjectNotFound) {
            closeInventoryBankIfDestroyed ();
        }
    }

    public void closeInventoryBankIfDestroyed ()
    {
        if (menuOpened) {
            if (currentInventoryBankSystem == null) {
                pauseManager.setUsingDeviceState (false);

                openOrCloseInventoryBankMenu (false);
            }
        }
    }

    public void openOrCloseInventoryBankMenu (bool state)
    {
        menuOpened = state;

        stopUpdateCoroutine ();

        setMenuIconElementPressedState (false);

        pauseManager.openOrClosePlayerMenu (menuOpened, inventoryMenu.transform, useBlurUIPanel);

        inventoryMenu.SetActive (menuOpened);

        pauseManager.setIngameMenuOpenedState ("Inventory Bank Manager", menuOpened, true);

        if (menuOpened) {
            int bankIconElementListCount = bankIconElementList.Count;

            for (int i = 0; i < bankIconElementListCount; i++) {
                if (bankIconElementList [i].button != null && bankIconElementList [i].button.gameObject.activeSelf) {
                    bankIconElementList [i].button.gameObject.SetActive (false);
                }
            }

            int playerInventoryListCount = playerInventoryList.Count;

            for (int i = 0; i < playerInventoryListCount; i++) {
                if (playerInventoryList [i].button != null && playerInventoryList [i].button.gameObject.activeSelf) {
                    playerInventoryList [i].button.gameObject.SetActive (false);
                }
            }

            getCurrentPlayerInventoryList ();

            checkMaximumAmountPerSlotOnInventoryList (currentBankInventoryList);

            createInventoryIcons (playerInventoryList, playerInventorySlot, playerInventorySlotsContent, playerIconElementList);

            if (showEmptySlotsOnUseMaxInventoryBankSlotsAmount) {
                int extraEmptyInventorySlots = Math.Abs (currentBankInventoryList.Count - maxInventoryBankSlotsAmount);

                for (int i = 0; i < extraEmptyInventorySlots; i++) {
                    currentBankInventoryList.Add (new inventoryInfo ());
                }
            }

            createInventoryIcons (currentBankInventoryList, bankInventorySlot, bankInventorySlotsContent, bankIconElementList);

            playerInventorySlotsContent.GetComponentInParent<ScrollRect> ().verticalNormalizedPosition = 0.5f;
            bankInventorySlotsContent.GetComponentInParent<ScrollRect> ().verticalNormalizedPosition = 0.5f;

            resetScroll (bankInventoryScrollbar);
            resetScroll (playerInventoryScrollbar);
        }

        pauseManager.enableOrDisablePlayerMenu (menuOpened, true, false);

        destroyObjectInCamera ();

        resetAndDisableNumberOfObjectsToUseMenu ();

        inventoryCamera.fieldOfView = originalFov;

        currentInventoryObject = null;

        inventoryCamera.enabled = menuOpened;

        currentInventoryObjectManager = null;

        if (!menuOpened) {
            resetDragDropElements ();
        }

        if (menuOpened) {
            updateCoroutine = StartCoroutine (updateSystemCoroutine ());
        }

        checkEventsOnOpenCloseInventoryBank (menuOpened);

        lastPressedButtonByGamepad = null;

        if (state) {
            checkEventOnUpdateInventoryGrids ();

            checkRefreshItemListOnCurrentContextOnUINavigation ();
        }
    }

    void checkEventsOnOpenCloseInventoryBank (bool state)
    {
        if (useEventsOnOpenCloseInventoryBank) {
            if (state) {
                eventOnOpenInventoryBank.Invoke ();
            } else {
                eventOnCloseInventoryBank.Invoke ();
            }
        }
    }

    void checkEventOnUpdateInventoryGrids ()
    {
        if (useEventOnUpdateInventoryGrids) {
            eventOnUpdateInventoryGrids.Invoke ();
        }
    }

    void checkRefreshItemListOnCurrentContextOnUINavigation ()
    {
        if (!menuOpened) {
            return;
        }

        pauseManager.checkRefreshItemListOnCurrentContextOnUINavigation ();
    }

    public void createInventoryIcons (List<inventoryInfo> currentInventoryListToCreate, GameObject currentInventorySlot, GameObject currentInventorySlotsContent,
                                      List<inventoryMenuIconElement> currentIconElementList)
    {
        for (int i = 0; i < currentInventoryListToCreate.Count; i++) {
            if (currentInventoryListToCreate [i].button != null && currentInventoryListToCreate [i].button.gameObject.activeSelf) {
                currentInventoryListToCreate [i].button.gameObject.SetActive (false);
            }
        }

        for (int i = 0; i < currentInventoryListToCreate.Count; i++) {
            createInventoryIcon (currentInventoryListToCreate [i], i, currentInventorySlot, currentInventorySlotsContent, currentIconElementList);
        }
    }

    public void createInventoryIcon (inventoryInfo currentInventoryInfo, int index, GameObject currentInventorySlot, GameObject currentInventorySlotsContent,
                                     List<inventoryMenuIconElement> currentIconElementList)
    {
        GameObject newIconButton = null;

        bool createNewIconResult = true;

        if (currentIconElementList.Count > 0) {
            for (int i = 0; i < currentIconElementList.Count; i++) {
                if (createNewIconResult) {
                    if (!currentIconElementList [i].button.gameObject.activeSelf) {
                        createNewIconResult = false;

                        newIconButton = currentIconElementList [i].button.gameObject;
                    }
                }
            }
        }

        if (createNewIconResult) {
            newIconButton = (GameObject)Instantiate (currentInventorySlot, Vector3.zero, Quaternion.identity, currentInventorySlotsContent.transform);
        }

        if (!newIconButton.activeSelf) {
            newIconButton.SetActive (true);
        }

        newIconButton.transform.localScale = Vector3.one;
        newIconButton.transform.localPosition = Vector3.zero;

        inventoryMenuIconElement menuIconElement = newIconButton.GetComponent<inventoryMenuIconElement> ();

        string currentInventoryInfoName = currentInventoryInfo.Name;

        if (gameLanguageSelector.isCheckLanguageActive ()) {
            currentInventoryInfoName = inventoryLocalizationManager.GetLocalizedValue (currentInventoryInfoName);
        }

        menuIconElement.iconName.text = currentInventoryInfoName;

        if (currentInventoryInfo.inventoryGameObject != null) {
            menuIconElement.icon.texture = currentInventoryInfo.icon;
        } else {
            menuIconElement.icon.texture = null;
        }

        menuIconElement.amount.text = currentInventoryInfo.amount.ToString ();

        menuIconElement.pressedIcon.SetActive (false);

        newIconButton.name = "Inventory Object-" + (index + 1);

        Button button = menuIconElement.button;
        currentInventoryInfo.button = button;
        currentInventoryInfo.menuIconElement = menuIconElement;

        bool slotIsActive = currentInventoryInfo.amount > 0;

        if (currentInventoryInfo.menuIconElement.activeSlotContent != null) {
            currentInventoryInfo.menuIconElement.activeSlotContent.SetActive (slotIsActive);
        }

        if (currentInventoryInfo.menuIconElement.emptySlotContent != null) {
            currentInventoryInfo.menuIconElement.emptySlotContent.SetActive (!slotIsActive);
        }

        if (currentInventoryInfo.hideSlotOnMenu) {
            if (currentInventoryInfo.button.gameObject.activeSelf) {
                currentInventoryInfo.button.gameObject.SetActive (false);
            }
        }

        if (createNewIconResult) {
            currentIconElementList.Add (menuIconElement);
        }
    }

    public void enableNumberOfElementsOnPressUp ()
    {
        if (dragAndDropIcons && (!dropFullObjectsAmount || useNumberOfElementsWhenIconNotMoved)) {
            enableNumberOfObjectsToUseMenu (currentSlotToMoveInventoryObject.button.GetComponent<RectTransform> ());

            if (draggedFromPlayerInventoryList) {
                droppedInBankInventoryList = true;
                droppedInPlayerInventoryList = false;
            }

            if (draggedFromBankInventoryList) {
                droppedInPlayerInventoryList = true;
                droppedInBankInventoryList = false;
            }

            draggedFromPlayerInventoryList = true;
            draggedFromBankInventoryList = true;
        }
    }

    public void enableObjectRotation ()
    {
        if (objectInCamera) {
            enableRotation = true;
        }
    }

    public void disableObjectRotation ()
    {
        enableRotation = false;
    }

    public void destroyObjectInCamera ()
    {
        if (objectInCamera) {
            Destroy (objectInCamera);
        }
    }

    public void zoomInEnabled ()
    {
        zoomingIn = true;
    }

    public void zoomInDisabled ()
    {
        zoomingIn = false;
    }

    public void zoomOutEnabled ()
    {
        zoomingOut = true;
    }

    public void zoomOutDisabled ()
    {
        zoomingOut = false;
    }

    public void checkResetCameraFov (float targetValue)
    {
        if (resetCameraFov != null) {
            StopCoroutine (resetCameraFov);
        }

        resetCameraFov = StartCoroutine (resetCameraFovCorutine (targetValue));
    }

    IEnumerator resetCameraFovCorutine (float targetValue)
    {
        while (inventoryCamera.fieldOfView != targetValue) {
            inventoryCamera.fieldOfView = Mathf.MoveTowards (inventoryCamera.fieldOfView, targetValue, Time.deltaTime * zoomSpeed);

            yield return null;
        }
    }

    public void getCurrentPlayerInventoryList ()
    {
        playerInventoryList.Clear ();

        for (int i = 0; i < playerInventoryManager.inventoryList.Count; i++) {

            inventoryInfo newPlayerInventoryObjectInfo = new inventoryInfo (playerInventoryManager.inventoryList [i]);

            newPlayerInventoryObjectInfo.button = null;

            playerInventoryList.Add (newPlayerInventoryObjectInfo);
        }

        checkMaximumAmountPerSlotOnInventoryList (playerInventoryList);
    }

    void checkMaximumAmountPerSlotOnInventoryList (List<inventoryInfo> currentInventoryList)
    {
        int currentInventoryListCount = currentInventoryList.Count;

        for (int i = 0; i < currentInventoryListCount; i++) {
            inventoryInfo currentInventoryInfo = currentInventoryList [i];

            if (currentInventoryInfo.setMaximumAmountPerSlot) {
                bool allAmountTaken = false;

                int currentAmountPerSlot = currentInventoryInfo.maximumAmountPerSlot;

                int totalAmountToTake = 0;

                bool checkRestAmount = true;

                if (currentInventoryInfo.amount > currentAmountPerSlot) {
                    totalAmountToTake = currentInventoryInfo.amount - currentAmountPerSlot;

                    currentInventoryInfo.amount = currentAmountPerSlot;
                } else {
                    checkRestAmount = false;
                }

                if (checkRestAmount) {
                    int loopCount = 0;

                    while (!allAmountTaken) {
                        inventoryInfo duplicatedInventoryInfo = new inventoryInfo (currentInventoryInfo);

                        if (totalAmountToTake - currentAmountPerSlot >= 0) {
                            duplicatedInventoryInfo.amount = currentAmountPerSlot;

                            totalAmountToTake -= currentAmountPerSlot;
                        } else {
                            duplicatedInventoryInfo.amount = totalAmountToTake;

                            totalAmountToTake = 0;
                        }

                        duplicatedInventoryInfo.isEquipped = false;

                        duplicatedInventoryInfo.quickAccessSlotIndex = -1;

                        duplicatedInventoryInfo.projectilesInMagazine = -1;

                        duplicatedInventoryInfo.button = null;

                        //if (i == 0) {
                        //    currentInventoryList.Insert (i, duplicatedInventoryInfo);
                        //} else {
                        //    currentInventoryList.Insert (i - 1, duplicatedInventoryInfo);
                        //}

                        currentInventoryList.Add (duplicatedInventoryInfo);

                        loopCount++;

                        if (loopCount > 100) {
                            allAmountTaken = true;
                        }

                        if (totalAmountToTake <= 0) {
                            allAmountTaken = true;
                        }
                    }
                }
            }
        }
    }

    public void addObjectToUse ()
    {
        numberOfObjectsToUse++;

        if (numberOfObjectsToUse > currentInventoryObject.amount) {
            numberOfObjectsToUse = currentInventoryObject.amount;
        }

        currentAmountToMove = numberOfObjectsToUse;

        setNumberOfObjectsToUseText (numberOfObjectsToUse);
    }

    public void removeObjectToUse ()
    {
        numberOfObjectsToUse--;

        if (numberOfObjectsToUse < 1) {
            numberOfObjectsToUse = 1;
        }

        currentAmountToMove = numberOfObjectsToUse;

        setNumberOfObjectsToUseText (numberOfObjectsToUse);
    }

    void setMaxAmountToMoveAndUpdateUI ()
    {
        numberOfObjectsToUse = currentInventoryObject.amount;

        currentAmountToMove = numberOfObjectsToUse;

        setNumberOfObjectsToUseText (numberOfObjectsToUse);
    }

    public void startToAddObjectToUse ()
    {
        addingObjectToUse = true;

        lastTimeConfigureNumberOfObjects = Time.time;
    }

    public void stopToAddObjectToUse ()
    {
        addingObjectToUse = false;

        lastTimeAddObjectToUse = 0;

        useFasterNumberObjectsToUseRateActive = false;
    }

    public void startToRemoveObjectToUse ()
    {
        removinvObjectToUse = true;

        lastTimeConfigureNumberOfObjects = Time.time;
    }

    public void stopToRemoveObjectToUse ()
    {
        removinvObjectToUse = false;

        lastTimeRemoveObjectToUse = 0;

        useFasterNumberObjectsToUseRateActive = false;
    }

    public void enableNumberOfObjectsToUseMenu (RectTransform menuPosition)
    {
        numberOfElementsWindow.SetActive (true);

        numberOfObjectsToUse = 1;

        currentAmountToMove = numberOfObjectsToUse;

        setNumberOfObjectsToUseText (numberOfObjectsToUse);

        settingAmountObjectsToMove = true;
    }

    public void resetAndDisableNumberOfObjectsToUseMenu ()
    {
        disableNumberOfObjectsToUseMenu ();

        resetNumberOfObjectsToUse ();
    }

    public void disableNumberOfObjectsToUseMenu ()
    {
        numberOfObjectsToUse = 1;

        numberOfElementsWindow.SetActive (false);
    }

    public void resetNumberOfObjectsToUse ()
    {
        numberOfObjectsToUse = 1;

        setNumberOfObjectsToUseText (numberOfObjectsToUse);
    }

    public void setNumberOfObjectsToUseText (int amount)
    {
        numberOfObjectsToUseText.text = amount.ToString ();
    }

    public void resetScroll (Scrollbar scrollBarToReset)
    {
        StartCoroutine (resetScrollCoroutine (scrollBarToReset));
    }

    IEnumerator resetScrollCoroutine (Scrollbar scrollBarToReset)
    {
        yield return new WaitForEndOfFrame ();
        yield return new WaitForEndOfFrame ();

        scrollBarToReset.value = 1;
    }

    public void resetBankInventoryRectTransform ()
    {
        StartCoroutine (resetBankInventoryRectTransformCoroutine ());
    }

    IEnumerator resetBankInventoryRectTransformCoroutine ()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate (bankInventorySlotsContent.GetComponent<RectTransform> ());

        yield return new WaitForEndOfFrame ();
        yield return new WaitForEndOfFrame ();

        yield return new WaitForEndOfFrame ();
        yield return new WaitForEndOfFrame ();

        LayoutRebuilder.ForceRebuildLayoutImmediate (bankInventoryScrollbar.GetComponent<RectTransform> ());
    }

    public void updateFullInventorySlots (List<inventoryInfo> currentInventoryList, List<inventoryMenuIconElement> currentIconElementList,
                                          bool checkInventorySlotAmount)
    {
        int currentIconElementListCount = currentIconElementList.Count;

        for (int i = 0; i < currentIconElementListCount; i++) {
            if (currentIconElementList [i].button != null && currentIconElementList [i].button.gameObject.activeSelf) {
                currentIconElementList [i].button.gameObject.SetActive (false);
            }
        }

        int currentInventoryListCount = currentInventoryList.Count;

        for (int i = 0; i < currentInventoryListCount; i++) {
            if (i < currentIconElementListCount) {
                inventoryInfo currentInventoryInfo = currentInventoryList [i];
                currentInventoryInfo.menuIconElement = currentIconElementList [i];
                currentInventoryInfo.button = currentIconElementList [i].button;

                bool showIconButtonResult = true;

                if (currentInventoryInfo.hideSlotOnMenu) {
                    showIconButtonResult = false;
                }

                if (currentInventoryInfo.button.gameObject.activeSelf != showIconButtonResult) {
                    currentInventoryInfo.button.gameObject.SetActive (showIconButtonResult);
                }

                string currentInventoryInfoName = currentInventoryInfo.Name;

                if (gameLanguageSelector.isCheckLanguageActive ()) {
                    currentInventoryInfoName = inventoryLocalizationManager.GetLocalizedValue (currentInventoryInfoName);
                }

                currentInventoryInfo.menuIconElement.iconName.text = currentInventoryInfoName;

                currentInventoryInfo.menuIconElement.icon.texture = currentInventoryInfo.icon;

                currentInventoryInfo.menuIconElement.amount.text = currentInventoryInfo.amount.ToString ();

                currentInventoryInfo.button.name = "Inventory Object-" + (i + 1);

                if (checkInventorySlotAmount) {
                    bool slotIsActive = currentInventoryInfo.amount > 0;

                    if (currentInventoryInfo.menuIconElement.activeSlotContent != null) {
                        currentInventoryInfo.menuIconElement.activeSlotContent.SetActive (slotIsActive);
                    }

                    if (currentInventoryInfo.menuIconElement.emptySlotContent != null) {
                        currentInventoryInfo.menuIconElement.emptySlotContent.SetActive (!slotIsActive);
                    }
                }
            }
        }
    }

    public void setMenuIconElementPressedState (bool state)
    {
        if (currentInventoryObject != null) {
            if (currentInventoryObject.menuIconElement != null) {
                currentInventoryObject.menuIconElement.pressedIcon.SetActive (state);
            }
        }
    }

    public void removeButton (List<inventoryInfo> currentInventoryList, List<inventoryMenuIconElement> currentIconElementList,
        bool removeElementFromInventoryList)
    {

        for (int i = currentInventoryList.Count - 1; i >= 0; i--) {
            if (currentInventoryList [i].amount == 0) {
                currentInventoryList.RemoveAt (i);
            }
        }

        if (!removeElementFromInventoryList) {
            if (showEmptySlotsOnUseMaxInventoryBankSlotsAmount) {
                int extraEmptyInventorySlots = Math.Abs (currentInventoryList.Count - maxInventoryBankSlotsAmount);

                for (int i = 0; i < extraEmptyInventorySlots; i++) {
                    currentInventoryList.Add (new inventoryInfo ());
                }
            }
        }

        enableRotation = false;

        destroyObjectInCamera ();

        setMenuIconElementPressedState (false);

        currentInventoryObject = null;

        currentInventoryObjectManager = null;

        lastPressedButtonByGamepad = null;
    }

    public void resetDragDropElements ()
    {
        if (!resetDragDropElementsEnabled) {
            return;
        }

        slotFound = null;

        draggedFromPlayerInventoryList = false;

        draggedFromBankInventoryList = false;

        droppedInBankInventoryList = false;

        droppedInPlayerInventoryList = false;

        slotToMoveFound = false;

        currentSlotFoundInventoryObject = null;

        currentSlotToMoveInventoryObject = null;

        settingAmountObjectsToMove = false;

        lastTimeSlotToMoveFound = 0;

        resetAndDisableNumberOfObjectsToUseMenu ();
    }

    public void checkIfObjecToMoveAlreadyInBankList ()
    {
        currentObjectAlreadyInBankList = false;

        if (currentSlotToMoveInventoryObject != null) {
            for (int j = 0; j < currentBankInventoryList.Count; j++) {
                if (!currentObjectAlreadyInBankList && currentBankInventoryList [j].Name.Equals (currentSlotToMoveInventoryObject.Name)) {
                    currentObjectAlreadyInBankList = true;
                    currentBankInventoryListIndex = j;

                    if (showDebugPrint) {
                        print ("Current Object Already In Bank List, adding amount to that object");
                    }
                }
            }
        }
    }

    public void confirmMoveObjects ()
    {
        if (draggedFromPlayerInventoryList) {

            if ((dragAndDropIcons && droppedInBankInventoryList) || !dragAndDropIcons) {

                checkIfObjecToMoveAlreadyInBankList ();

                moveObjectsToBankInventory ();

                return;
            }
        }

        if (draggedFromBankInventoryList) {

            if ((dragAndDropIcons && droppedInPlayerInventoryList) || !dragAndDropIcons) {
                moveObjectsToPlayerInventory ();

                return;
            }
        }
    }

    public void confirmMoveAllObjects ()
    {
        currentAmountToMove = currentInventoryObject.amount;

        confirmMoveObjects ();
    }

    public void confirmMoveFullInventoryBankToPlayerInventory ()
    {
        pauseUpdateUIOnMoveFullInventoryBankToPlayerInventoryActive = true;

        for (int i = currentBankInventoryList.Count - 1; i >= 0; i--) {
            currentSlotToMoveInventoryObject = currentBankInventoryList [i];

            if (currentSlotToMoveInventoryObject.amount > 0) {
                bool canMoveObjectResult = true;

                if (playerInventoryManager.isInventoryFull ()) {
                    canMoveObjectResult = false;
                }

                if (playerInventoryManager.getNumberOfFreeSlots () <= 0) {
                    canMoveObjectResult = false;
                }

                if (!canMoveObjectResult) {
                    if (playerInventoryManager.checkIfObjectCanBeStored (currentSlotToMoveInventoryObject.inventoryGameObject,
                            currentSlotToMoveInventoryObject.amount)) {
                        canMoveObjectResult = true;
                    }
                }

                if (canMoveObjectResult) {
                    currentAmountToMove = currentSlotToMoveInventoryObject.amount;

                    moveObjectsToPlayerInventory ();
                }
            }
        }

        pauseUpdateUIOnMoveFullInventoryBankToPlayerInventoryActive = false;

        updateInventoryInfoOnMoveObjectsToPlayerInventory ();

        cancelMoveObjects ();
    }

    bool pauseUpdateUIOnMoveFullInventoryBankToPlayerInventoryActive;

    bool pauseUpdateUIOnMoveFullPlayerInventoryToBankInventoryActive;

    public void confirmMoveFullPlayerInventoryToBankInventory ()
    {
        if (!canDropObjectsToInventoryBank) {
            return;
        }

        pauseUpdateUIOnMoveFullPlayerInventoryToBankInventoryActive = true;

        for (int i = playerInventoryList.Count - 1; i >= 0; i--) {
            currentSlotToMoveInventoryObject = playerInventoryList [i];

            if (currentSlotToMoveInventoryObject.amount > 0) {
                bool canMoveObjectResult = true;

                if (useMaxInventoryBankSlotsAmount) {
                    if (currentBankInventoryList.Count >= maxInventoryBankSlotsAmount) {
                        canMoveObjectResult = false;
                    }

                    if (checkIfEmptySlotsOnInventoryList (currentBankInventoryList)) {
                        canMoveObjectResult = true;
                    } else {
                        canMoveObjectResult = false;
                    }
                }

                if (canMoveObjectResult) {
                    currentAmountToMove = currentSlotToMoveInventoryObject.amount;

                    objectToMoveIndex = i;

                    checkIfObjecToMoveAlreadyInBankList ();

                    moveObjectsToBankInventory ();
                }
            }
        }

        pauseUpdateUIOnMoveFullPlayerInventoryToBankInventoryActive = false;

        updateInventoryInfoOnMoveObjectsToBankInventory ();

        cancelMoveObjects ();
    }

    public void cancelMoveObjects ()
    {
        resetDragDropElements ();

        resetAndDisableNumberOfObjectsToUseMenu ();
    }

    //Inventory bank UI management
    public void getPressedButton (Button buttonObj)
    {
        int inventoryObjectIndex = -1;

        if (draggedFromPlayerInventoryList) {
            for (int i = 0; i < playerInventoryList.Count; i++) {
                if (playerInventoryList [i].button == buttonObj) {
                    inventoryObjectIndex = i;
                }
            }
        } else {
            for (int i = 0; i < currentBankInventoryList.Count; i++) {
                if (currentBankInventoryList [i].button == buttonObj) {
                    inventoryObjectIndex = i;
                }
            }
        }

        if (currentInventoryObject != null) {
            if (((draggedFromPlayerInventoryList && currentInventoryObject == playerInventoryList [inventoryObjectIndex]) ||
                (!draggedFromPlayerInventoryList && currentInventoryObject == currentBankInventoryList [inventoryObjectIndex])) &&
                currentInventoryObject.menuIconElement.pressedIcon.activeSelf) {
                return;
            }
        }

        setObjectInfo (inventoryObjectIndex, draggedFromPlayerInventoryList);

        float currentCameraFov = originalFov;

        if (currentInventoryObjectManager) {
            if (currentInventoryObjectManager.useZoomRange) {
                currentCameraFov = currentInventoryObjectManager.initialZoom;
            }
        }

        if (inventoryCamera.fieldOfView != currentCameraFov) {
            checkResetCameraFov (currentCameraFov);
        }
    }

    public void setObjectInfo (int index, bool isPlayerInventoryList)
    {
        resetAndDisableNumberOfObjectsToUseMenu ();

        setMenuIconElementPressedState (false);

        if (isPlayerInventoryList) {
            currentInventoryObject = playerInventoryList [index];
        } else {
            currentInventoryObject = currentBankInventoryList [index];
        }
        setMenuIconElementPressedState (true);

        GameObject currentInventoryObjectPrefab = currentInventoryObject.inventoryGameObject;

        checkGetMainInventoryManager ();

        for (int i = 0; i < mainInventoryListManager.inventoryList.Count; i++) {
            if (mainInventoryListManager.inventoryList [i].inventoryGameObject == currentInventoryObject.inventoryGameObject) {
                currentInventoryObjectPrefab = mainInventoryListManager.inventoryList [i].inventoryObjectPrefab;
            }
        }

        currentInventoryObjectManager = currentInventoryObjectPrefab.GetComponentInChildren<inventoryObject> ();

        inventoryMenuRenderTexture.enabled = true;

        destroyObjectInCamera ();

        if (currentInventoryObject.inventoryGameObject != null) {

            objectInCamera = (GameObject)Instantiate (currentInventoryObject.inventoryGameObject, lookObjectsPosition.position, Quaternion.identity, lookObjectsPosition);
        }
    }

    Button lastPressedButtonByGamepad;

    public void checkPressedInventorySlotByGamepad (Button pressedButton)
    {
        if (!menuOpened) {
            return;
        }

        if (!playerInput.isUsingGamepad () || !playerInput.isUINavigationCurrentlyActive ()) {
            return;
        }

        if (lastPressedButtonByGamepad != pressedButton) {
            lastPressedButtonByGamepad = pressedButton;

            //getPressedButton (pressedButton);

            RectTransform rect = pressedButton.GetComponent<RectTransform> ();

            Canvas mainCanvas = pressedButton.GetComponentInParent<Canvas> ().rootCanvas;

            currentTouchPosition = RectTransformUtility.WorldToScreenPoint (
                mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera,
                rect.TransformPoint (rect.rect.center)
            );

            touchPressType = TouchPressType.Began;

            checkTouchPress (0);

            touchPressType = TouchPressType.Moved;

            checkTouchPress (1);

            touchPressType = TouchPressType.Ended;

            checkTouchPress (2);

            setMaxAmountToMoveAndUpdateUI ();

            return;
        }

        if (currentInventoryObject != null) {

            confirmMoveAllObjects ();

            lastPressedButtonByGamepad = null;
        } else {
            print ("trying to move a null inventory object");
        }
    }

    public void checkInventoryObjectSlotsInput ()
    {
        //check the mouse position in the screen if we are in the editor, or the finger position in a touch device
        int touchCount = 0;

        if (touchPlatform && useNewInputSystem) {
            touchCount = InputAxisWrapper.getNewTouchCount ();
        } else {
            touchCount = InputAxisWrapper.getTouchCount ();
        }

        bool usingGamepadOnTouchPlatform = false;

        if (touchPlatform) {
            if (touchCount == 0 && playerInput.isUsingGamepad ()) {
                touchCount++;

                usingGamepadOnTouchPlatform = true;
            }
        } else {
            touchCount++;
        }

        for (int i = 0; i < touchCount; i++) {
            if (!touchPlatform) {
                if (!useNewInputSystem) {
                    currentTouch = InputAxisWrapper.convertMouseIntoFinger ();
                }
            } else {
                if (!useNewInputSystem) {
                    currentTouch = InputAxisWrapper.getTouch (i);
                }
            }

            if (touchPlatform && useNewInputSystem) {
                touchPressType = InputAxisWrapper.GetTouchPressType (i);

                currentTouchPosition = InputAxisWrapper.GetTouchPosition (i);

                if (usingGamepadOnTouchPlatform) {
                    touchPressType = playerInput.GetTouchPressType ();

                    currentTouchPosition = playerInput.getCurrentTouchPosition ();
                }
            } else {
                if (useNewInputSystem) {
                    touchPressType = playerInput.GetTouchPressType ();

                    currentTouchPosition = playerInput.getCurrentTouchPosition ();
                } else {
                    touchPressType = InputAxisWrapper.GetTouchPressType (currentTouch);

                    currentTouchPosition = currentTouch.position;
                }
            }

            checkTouchPress (i);
        }
    }


    void checkTouchPress (int i)
    {
        if (touchPressType == InputAxisWrapper.TouchPressType.Began) {
            touching = true;

            draggingSlotActive = false;

            lastTimeTouched = Time.time;

            captureRaycastResults.Clear ();

            PointerEventData p = new PointerEventData (EventSystem.current);
            p.position = currentTouchPosition;
            p.clickCount = i;
            p.dragging = false;

            EventSystem.current.RaycastAll (p, captureRaycastResults);

            if (slotToMoveFound) {
                bool pressingOnAnyInventoryGridResult = false;

                foreach (RaycastResult r in captureRaycastResults) {
                    if (r.gameObject == playerInventorySlots || r.gameObject == bankInventorySlots) {
                        pressingOnAnyInventoryGridResult = true;
                    }
                }

                if (!pressingOnAnyInventoryGridResult) {
                    //print ("ignoring press");

                    touching = false;

                    return;
                }
            }

            foreach (RaycastResult r in captureRaycastResults) {

                bool checkSlotsResult = true;

                if (slotToMoveFound) {
                    if (lastTimeSlotToMoveFound == 0 || Time.time < lastTimeSlotToMoveFound + 0.5f) {
                        checkSlotsResult = false;
                    } else {
                        if (draggingSlotActive) {
                            checkSlotsResult = false;
                        }
                    }

                    if (checkSlotsResult) {
                        resetDragDropElementsEnabled = true;

                        cancelMoveObjects ();

                        //print ("reset");
                    }
                }

                if (checkSlotsResult) {
                    inventoryMenuIconElement currentInventoryMenuIconElement = r.gameObject.GetComponent<inventoryMenuIconElement> ();

                    if (currentInventoryMenuIconElement) {

                        bool objectFound = false;

                        for (int j = 0; j < playerInventoryList.Count; j++) {
                            if (!objectFound && playerInventoryList [j].button == currentInventoryMenuIconElement.button) {

                                if (!canDropObjectsToInventoryBank) {
                                    return;
                                }

                                if (playerInventoryList [j].amount > 0) {
                                    currentSlotToMoveInventoryObject = playerInventoryList [j];

                                    draggedFromPlayerInventoryList = true;

                                    slotToMoveFound = true;

                                    lastTimeSlotToMoveFound = Time.time;

                                    objectToMoveIndex = j;

                                    if (showDebugPrint) {
                                        print ("dragged from player inventory list " + j);
                                    }

                                    objectFound = true;

                                    getPressedButton (currentInventoryMenuIconElement.button);
                                }
                            }
                        }

                        if (!draggedFromPlayerInventoryList) {

                            objectFound = false;

                            for (int j = 0; j < currentBankInventoryList.Count; j++) {
                                if (!objectFound && currentBankInventoryList [j].button == currentInventoryMenuIconElement.button) {
                                    if (currentBankInventoryList [j].amount > 0) {
                                        currentSlotToMoveInventoryObject = currentBankInventoryList [j];

                                        draggedFromBankInventoryList = true;

                                        slotToMoveFound = true;

                                        lastTimeSlotToMoveFound = Time.time;

                                        objectToMoveIndex = j;

                                        if (showDebugPrint) {
                                            print ("dragged from bank inventory list " + j);
                                        }

                                        objectFound = true;

                                        getPressedButton (currentInventoryMenuIconElement.button);
                                    }
                                }
                            }
                        }

                        if (slotToMoveFound && dragAndDropIcons) {
                            slotToMove.GetComponentInChildren<RawImage> ().texture = currentInventoryMenuIconElement.icon.texture;
                        }
                    }
                }
            }
        }

        if (touching && !dragAndDropIcons && slotToMoveFound) {
            touching = false;

            draggingSlotActive = false;

            checkDroppedObject ();

            return;
        }

        if (touchPressType == InputAxisWrapper.TouchPressType.Stationary || touchPressType == InputAxisWrapper.TouchPressType.Moved) {
            if (slotToMoveFound && dragAndDropIcons) {
                if (touching && Time.time > lastTimeTouched + timeToDrag) {
                    if (!slotToMove.activeSelf) {
                        slotToMove.SetActive (true);
                    }

                    slotToMove.GetComponent<RectTransform> ().position = currentTouchPosition;

                    draggingSlotActive = true;
                }
            }
        }

        //if the mouse/finger press is released, then
        if (touchPressType == InputAxisWrapper.TouchPressType.Ended && touching) {

            touching = false;

            if (draggingSlotActive) {
                draggingSlotActive = false;

                if (slotToMoveFound) {
                    //get the elements in the position where the player released the slot element
                    captureRaycastResults.Clear ();

                    PointerEventData p = new PointerEventData (EventSystem.current);
                    p.position = currentTouchPosition;
                    p.clickCount = i;
                    p.dragging = false;

                    EventSystem.current.RaycastAll (p, captureRaycastResults);

                    foreach (RaycastResult r in captureRaycastResults) {
                        if (r.gameObject != slotToMove) {
                            if (r.gameObject.GetComponent<inventoryMenuIconElement> ()) {
                                slotFound = r.gameObject;
                            } else if (r.gameObject == playerInventorySlots) {
                                droppedInPlayerInventoryList = true;
                            } else if (r.gameObject == bankInventorySlots) {
                                droppedInBankInventoryList = true;
                            }
                        }
                    }

                    checkDroppedObject ();
                }
            } else {
                if (useNumberOfElementsWhenIconNotMoved) {
                    if (currentSlotToMoveInventoryObject != null && currentSlotToMoveInventoryObject.button != null) {
                        enableNumberOfElementsOnPressUp ();
                    }
                }
            }
        }
    }

    public void checkDroppedObject ()
    {
        replacingObjectsActive = false;

        //an icon has been dropped on top of another icon from both inventory list
        if (slotFound != null) {
            bool droppedCorreclty = true;
            bool currentSlotDroppedFoundOnList = false;

            inventoryMenuIconElement slotFoundInventoryMenuIconElement = slotFound.GetComponent<inventoryMenuIconElement> ();

            for (int j = 0; j < playerInventoryList.Count; j++) {
                if (playerInventoryList [j].button == slotFoundInventoryMenuIconElement.button) {
                    currentSlotFoundInventoryObject = playerInventoryList [j];
                    currentSlotDroppedFoundOnList = true;
                    objectFoundIndex = j;
                }
            }

            if (!currentSlotDroppedFoundOnList) {
                for (int j = 0; j < currentBankInventoryList.Count; j++) {
                    if (currentBankInventoryList [j].button == slotFoundInventoryMenuIconElement.button) {
                        currentSlotFoundInventoryObject = currentBankInventoryList [j];
                        currentSlotDroppedFoundOnList = true;
                        objectFoundIndex = j;
                    }
                }
            }

            if (currentSlotDroppedFoundOnList) {
                if (((draggedFromBankInventoryList && droppedInPlayerInventoryList) || (draggedFromPlayerInventoryList && droppedInBankInventoryList))) {
                    if (showDebugPrint) {
                        print (currentSlotToMoveInventoryObject.Name + " dropped on top of " + currentSlotFoundInventoryObject.Name);
                    }

                    if (droppedInPlayerInventoryList) {
                        if (showDebugPrint) {
                            print ("dropped correctly to player inventory");
                        }

                        resetDragDropElementsEnabled = false;

                        inventoryInfo auxInventoryInfo = currentSlotToMoveInventoryObject;
                        int auxObjectToMoveIndex = objectToMoveIndex;

                        //move the object dropped to players's inventory
                        currentSlotToMoveInventoryObject = currentSlotFoundInventoryObject;
                        objectToMoveIndex = objectFoundIndex;

                        if (canDropObjectsToInventoryBank) {
                            replacingObjectsActive = true;

                            dragToBankInventory ();

                            replacingObjectsActive = false;
                        }

                        objectToMoveIndex = auxObjectToMoveIndex;

                        currentSlotToMoveInventoryObject = auxInventoryInfo;

                        if (showDebugPrint) {
                            print (currentSlotToMoveInventoryObject.Name + " found on player's inventory");
                        }

                        resetDragDropElementsEnabled = true;

                        dragToPlayerInventory ();

                    } else if (droppedInBankInventoryList) {
                        if (showDebugPrint) {
                            print ("dropped correctly to bank inventory");
                        }

                        resetDragDropElementsEnabled = false;
                        //move the object dropped to bank inventory

                        replacingObjectsActive = true;

                        dragToBankInventory ();

                        replacingObjectsActive = false;

                        objectToMoveIndex = objectFoundIndex;

                        currentSlotToMoveInventoryObject = currentBankInventoryList [objectToMoveIndex];

                        if (showDebugPrint) {
                            print (currentSlotToMoveInventoryObject.Name + " found on bank");
                        }

                        resetDragDropElementsEnabled = true;

                        dragToPlayerInventory ();
                    }
                } else {
                    if (showDebugPrint) {
                        print ("dropped into the same dragged list");
                    }

                    droppedCorreclty = false;

                    if (useNumberOfElementsWhenIconNotMoved) {
                        enableNumberOfElementsOnPressUp ();
                        droppedCorreclty = true;
                    }
                }
            } else {
                if (showDebugPrint) {
                    print ("dropped outside the list");
                }

                droppedCorreclty = false;
            }

            slotToMove.SetActive (false);

            if (!droppedCorreclty) {
                resetDragDropElements ();
            }
        }

        //the icon is dropped on top or bottom of any of the inventory list
        else {
            bool droppedCorreclty = true;

            //if the object to move has been dragged and dropped from one list to another correctly, then
            if (((draggedFromBankInventoryList && droppedInPlayerInventoryList) || (draggedFromPlayerInventoryList && droppedInBankInventoryList))) {
                //dropping object from bank to player's inventory
                if (droppedInPlayerInventoryList) {
                    dragToPlayerInventory ();
                } else if (droppedInBankInventoryList) {
                    //dropping object from player's inventory to bank
                    bool canDropToInventoryResult = true;

                    if (useMaxInventoryBankSlotsAmount) {
                        if (showEmptySlotsOnUseMaxInventoryBankSlotsAmount) {
                            if (!checkIfEmptySlotsOnInventoryList (currentBankInventoryList)) {
                                canDropToInventoryResult = false;
                            }
                        } else {
                            if (bankIconElementList.Count >= maxInventoryBankSlotsAmount) {
                                canDropToInventoryResult = false;
                            }
                        }
                    }

                    if (canDropToInventoryResult) {
                        dragToBankInventory ();
                    } else {
                        playerInventoryManager.showObjectMessage (inventoryBankFullMessage, 5);

                        resetDragDropElements ();
                    }
                } else {
                    if (showDebugPrint) {
                        print ("not dropped correctly");
                    }

                    droppedCorreclty = false;
                }
            } else {
                if (dragAndDropIcons) {
                    if (showDebugPrint) {
                        print ("dropped into the same dragged list or outside of the list");
                    }

                    droppedCorreclty = false;

                    if (useNumberOfElementsOnPressUp) {
                        enableNumberOfElementsOnPressUp ();

                        droppedCorreclty = true;
                    }

                } else {
                    //if the drag and drop function is not being used, check if the pressed icon is in player or bank inventory to move the object
                    if (draggedFromBankInventoryList) {
                        dragToPlayerInventory ();
                    } else if (draggedFromPlayerInventoryList) {
                        bool canDropToInventoryResult = true;

                        if (useMaxInventoryBankSlotsAmount) {
                            if (showEmptySlotsOnUseMaxInventoryBankSlotsAmount) {
                                if (!checkIfEmptySlotsOnInventoryList (currentBankInventoryList)) {
                                    canDropToInventoryResult = false;
                                }
                            } else {
                                if (bankIconElementList.Count >= maxInventoryBankSlotsAmount) {
                                    canDropToInventoryResult = false;
                                }
                            }
                        }

                        if (canDropToInventoryResult) {
                            dragToBankInventory ();
                        } else {
                            playerInventoryManager.showObjectMessage (inventoryBankFullMessage, 5);

                            resetDragDropElements ();
                        }
                    }
                }
            }

            slotToMove.SetActive (false);

            if (!droppedCorreclty) {
                resetDragDropElements ();
            }
        }
    }

    bool checkIfEmptySlotsOnInventoryList (List<inventoryInfo> currentInventoryList)
    {
        for (int j = 0; j < currentInventoryList.Count; j++) {
            if (currentInventoryList [j].amount <= 0) {
                return true;
            }
        }

        return false;
    }

    public void dragToPlayerInventory ()
    {
        if (showDebugPrint) {
            print ("dropped correctly to player inventory");

            print (currentSlotToMoveInventoryObject.Name + " dropped correctly to player inventory");
        }

        currentAmountToMove = currentSlotToMoveInventoryObject.amount;

        if (dropFullObjectsAmount) {
            moveObjectsToPlayerInventory ();
        } else {
            enableNumberOfObjectsToUseMenu (currentSlotToMoveInventoryObject.button.GetComponent<RectTransform> ());
        }
    }

    public void dragToBankInventory ()
    {
        if (showDebugPrint) {
            print ("dropped correctly to bank inventory");
        }

        currentObjectAlreadyInBankList = false;
        currentBankInventoryListIndex = -1;

        for (int j = 0; j < currentBankInventoryList.Count; j++) {
            if (!currentObjectAlreadyInBankList && currentBankInventoryList [j].Name.Equals (currentSlotToMoveInventoryObject.Name)) {
                currentObjectAlreadyInBankList = true;
                currentBankInventoryListIndex = j;
            }
        }

        currentAmountToMove = currentSlotToMoveInventoryObject.amount;

        if (dropFullObjectsAmount) {
            moveObjectsToBankInventory ();
        } else {
            enableNumberOfObjectsToUseMenu (currentSlotToMoveInventoryObject.button.GetComponent<RectTransform> ());
        }
    }

    public void moveObjectsToBankInventory ()
    {
        if (currentObjectAlreadyInBankList) {
            currentBankInventoryList [currentBankInventoryListIndex].amount += currentAmountToMove;

            if (showDebugPrint) {
                print ("combine objects amount + " + currentAmountToMove);
            }
        } else {
            currentSlotToMoveInventoryObject.amount = currentAmountToMove;

            if (showEmptySlotsOnUseMaxInventoryBankSlotsAmount) {
                if (replacingObjectsActive) {
                    currentBankInventoryList.Add (new inventoryInfo (currentSlotToMoveInventoryObject));
                } else {
                    int indexToInsert = -1;

                    for (int i = 0; i < currentBankInventoryList.Count; i++) {
                        if (indexToInsert == -1) {
                            if (currentBankInventoryList [i].amount <= 0) {
                                indexToInsert = i;
                            }
                        }
                    }

                    if (indexToInsert > -1) {
                        currentBankInventoryList.Insert (indexToInsert, new inventoryInfo (currentSlotToMoveInventoryObject));

                        currentBankInventoryList.RemoveAt (indexToInsert + 1);
                    }
                }
            } else {
                currentBankInventoryList.Add (new inventoryInfo (currentSlotToMoveInventoryObject));

                int lastAddedObjectIndex = currentBankInventoryList.Count;

                createInventoryIcon (currentBankInventoryList [lastAddedObjectIndex - 1], lastAddedObjectIndex - 1, bankInventorySlot,
                    bankInventorySlotsContent, bankIconElementList);
            }

            if (showDebugPrint) {
                print ("new object added +" + currentAmountToMove);
            }
        }

        playerInventoryManager.moveObjectToBank (objectToMoveIndex, currentAmountToMove);

        if (pauseUpdateUIOnMoveFullPlayerInventoryToBankInventoryActive) {
            return;
        }

        updateInventoryInfoOnMoveObjectsToBankInventory ();
    }

    void updateInventoryInfoOnMoveObjectsToBankInventory ()
    {
        getCurrentPlayerInventoryList ();

        checkMaximumAmountPerSlotOnInventoryList (currentBankInventoryList);

        checkIfAddInventoryIconOnBankInventoryList ();

        updateFullInventorySlots (playerInventoryList, playerIconElementList, true);

        updateFullInventorySlots (currentBankInventoryList, bankIconElementList, false || showEmptySlotsOnUseMaxInventoryBankSlotsAmount);

        resetDragDropElements ();

        resetBankInventoryRectTransform ();

        checkEventOnUpdateInventoryGrids ();

        checkRefreshItemListOnCurrentContextOnUINavigation ();
    }

    void checkIfAddInventoryIconOnBankInventoryList ()
    {
        int currentInventoryListCount = currentBankInventoryList.Count;

        for (int i = 0; i < currentInventoryListCount; i++) {
            inventoryInfo currentInventoryInfo = currentBankInventoryList [i];

            if (currentInventoryInfo.button == null) {
                createInventoryIcon (currentInventoryInfo, i, bankInventorySlot,
                        bankInventorySlotsContent, bankIconElementList);
            }
        }
    }

    public void moveObjectsToPlayerInventory ()
    {
        int currentSlotToMoveAmount = currentSlotToMoveInventoryObject.amount;
        currentSlotToMoveInventoryObject.amount = currentAmountToMove;

        if (currentSlotToMoveAmount > 0) {
            if (!playerInventoryManager.isInventoryFull ()) {
                int inventoryAmountPicked = playerInventoryManager.tryToPickUpObject (currentSlotToMoveInventoryObject);

                if (showDebugPrint) {
                    print ("amount taken " + inventoryAmountPicked);
                }

                if (inventoryAmountPicked > 0) {
                    currentSlotToMoveInventoryObject.amount = currentSlotToMoveAmount - inventoryAmountPicked;
                } else {
                    if (!playerInventoryManager.isIgnoreMaximumObjectAmountActive () && currentSlotToMoveInventoryObject.setMaximumObjectAmount) {
                        playerInventoryManager.showMaxObjectAmountReachedMessage (currentSlotToMoveInventoryObject.Name);
                    } else {
                        playerInventoryManager.showInventoryFullMessage ();
                    }
                }
            } else {
                playerInventoryManager.showInventoryFullMessage ();
            }
        }

        if (pauseUpdateUIOnMoveFullInventoryBankToPlayerInventoryActive) {
            return;
        }

        updateInventoryInfoOnMoveObjectsToPlayerInventory ();
    }

    void updateInventoryInfoOnMoveObjectsToPlayerInventory ()
    {
        getCurrentPlayerInventoryList ();

        checkMaximumAmountPerSlotOnInventoryList (currentBankInventoryList);

        removeButton (currentBankInventoryList, bankIconElementList, !showEmptySlotsOnUseMaxInventoryBankSlotsAmount);

        updateFullInventorySlots (playerInventoryList, playerIconElementList, true);

        updateFullInventorySlots (currentBankInventoryList, bankIconElementList, false || showEmptySlotsOnUseMaxInventoryBankSlotsAmount);

        LayoutRebuilder.ForceRebuildLayoutImmediate (bankInventorySlotsContent.GetComponent<RectTransform> ());

        resetDragDropElements ();

        resetBankInventoryRectTransform ();

        checkEventOnUpdateInventoryGrids ();

        checkRefreshItemListOnCurrentContextOnUINavigation ();
    }
}