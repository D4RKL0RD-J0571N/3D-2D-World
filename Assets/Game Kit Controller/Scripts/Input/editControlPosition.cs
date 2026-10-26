using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;


[System.Serializable]
//this script allows to edit the touch buttons to set their positions in run time with the mouse or finger in a touch screen, save those positions in a file, 
//and reload in the next execution, in case you want to load it
public class editControlPosition : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool loadButtonsPositionEnabled = false;

    public bool initializeTouchButtonsPositionEnabled = false;

    [Space]
    [Header ("Debug")]
    [Space]

    public List<RectTransform> buttons = new List<RectTransform> ();

    public bool mainGameManagerLocated;

    public bool showDebugInfo;

    [Space]
    [Header ("Button Info Debug")]
    [Space]

    public List<Vector2> buttonsPos = new List<Vector2> ();
    public List<Vector2> buttonPosDefault = new List<Vector2> ();
    public List<touchButtonListener> touchButtonList = new List<touchButtonListener> ();

    [Space]
    [Header ("Components")]
    [Space]

    public inputManager input;
    public gameManager mainGameManager;
    public playerInputManager playerInput;


    RectTransform buttonToMove;

    bool enableButtonEdit = false;
    bool touching;
    bool grab;

    int i = 0;
    readonly List<RaycastResult> captureRaycastResults = new List<RaycastResult> ();

    InputAxisWrapper.TouchPressType touchPressType;

    bool useNewInputSystem;

    Vector2 currentTouchPosition;

    Touch currentTouch;

    bool touchPlatform;

    string currentSaveDataPath;
    bool checkSettingsInitialized;

    bool useRelativePath;

    string touchControlsPositionFolderName;
    string touchControlsPositionFileName;

    Coroutine mainUpdateCoroutine;


    void Start ()
    {
        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();

        checkMainManager ();

        if (mainGameManagerLocated) {
            touchControlsPositionFolderName = mainGameManager.getTouchControlsPositionFolderName ();

            touchControlsPositionFileName = mainGameManager.getTouchControlsPositionFileName ();
        }
    }

    void checkMainManager ()
    {
        mainGameManagerLocated = mainGameManager != null;

        if (!mainGameManagerLocated) {
            mainGameManager = gameManager.Instance;

            mainGameManagerLocated = mainGameManager != null;
        }

        if (!mainGameManagerLocated) {
            mainGameManager = GKC_Utils.findObjectByComponent<gameManager> ();

            mainGameManager.getComponentInstanceOnApplicationPlaying ();

            mainGameManagerLocated = mainGameManager != null;
        }
    }

    public void stopUpdateCoroutine ()
    {
        if (mainUpdateCoroutine != null) {
            StopCoroutine (mainUpdateCoroutine);
        }
    }

    IEnumerator updateCoroutine ()
    {
        var waitTime = new WaitForSecondsRealtime (0.00001f);

        while (true) {
            if (enableButtonEdit) {
                //check the mouse position in the screen if we are in the editor, or the finger position in a touch device
                int touchCount = 0;

                if (touchPlatform && useNewInputSystem) {
                    touchCount = InputAxisWrapper.getNewTouchCount ();
                } else {
                    touchCount = InputAxisWrapper.getTouchCount ();
                }

                if (!touchPlatform) {
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
                    } else {
                        if (useNewInputSystem) {
                            touchPressType = playerInput.GetTouchPressType ();

                            currentTouchPosition = playerInput.getCurrentTouchPosition ();
                        } else {
                            touchPressType = InputAxisWrapper.GetTouchPressType (currentTouch);

                            currentTouchPosition = currentTouch.position;
                        }
                    }

                    if (touchPressType == InputAxisWrapper.TouchPressType.Began) {
                        //if the finger/mouse position is above a button, it is selected to translate
                        captureRaycastResults.Clear ();

                        PointerEventData p = new PointerEventData (EventSystem.current);

                        p.position = currentTouchPosition;
                        p.clickCount = i;
                        p.dragging = false;
                        EventSystem.current.RaycastAll (p, captureRaycastResults);

                        foreach (RaycastResult r in captureRaycastResults) {
                            if (buttons.Contains (r.gameObject.GetComponent<RectTransform> ())) {
                                grab = true;
                                buttonToMove = r.gameObject.GetComponent<RectTransform> ();
                                touching = true;
                            }
                        }
                    }

                    //the button follows the mouse/finger position
                    if ((touchPressType == InputAxisWrapper.TouchPressType.Stationary || touchPressType == InputAxisWrapper.TouchPressType.Moved) && grab) {
                        if (grab && buttonToMove != null) {
                            buttonToMove.position = currentTouchPosition;
                        }
                    }

                    //release the button 
                    if (touchPressType == InputAxisWrapper.TouchPressType.Ended && touching) {
                        touching = false;

                        if (grab) {
                            grab = false;

                            buttonToMove = null;
                        }
                    }
                }
            }

            yield return waitTime;
        }
    }

    public void checkSettings ()
    {
        checkMainManager ();

        if (!checkSettingsInitialized && mainGameManagerLocated) {

            useRelativePath = mainGameManager.useRelativePath;

            currentSaveDataPath = getDataPath ();

            checkSettingsInitialized = true;
        }
    }

    public string getDataPath ()
    {
        string dataPath = "";

        if (useRelativePath) {
            dataPath = touchControlsPositionFolderName;
        } else {
            dataPath = Application.persistentDataPath + "/" + touchControlsPositionFolderName;
        }

        if (!Directory.Exists (dataPath)) {
            Directory.CreateDirectory (dataPath);
        }

        dataPath += "/";

        return dataPath;
    }

    public void getTouchButtons ()
    {
        checkMainManager ();

        if (mainGameManagerLocated) {
            //load or not the previous positions of the buttons, saved in another execution
            if (mainGameManager.isLoadGameEnabled ()) {
                if (loadButtonsPositionEnabled) {
                    Load ();

                    loadButtonsPositionEnabled = false;
                }

                if (initializeTouchButtonsPositionEnabled) {
                    for (i = 0; i < buttons.Count; i++) {
                        if (buttons [i] != null) {
                            buttonsPos.Add (buttons [i].anchoredPosition);
                        }
                    }
                }
            }
        }
    }

    public void getTouchButtonsComponents ()
    {
        buttons.Clear ();

        buttonPosDefault.Clear ();

        touchButtonList.Clear ();

        bool inputManagerLocated = input != null;

        if (!inputManagerLocated) {
            input = inputManager.Instance;

            inputManagerLocated = input != null;
        }

        if (!inputManagerLocated) {
            input = GKC_Utils.findObjectByComponent<inputManager> ();

            input.getComponentInstanceOnApplicationPlaying ();

            inputManagerLocated = input != null;
        }

        if (!inputManagerLocated) {
            return;
        }

        //save all touch buttons in a list and their positions
        for (i = 0; i < input.touchButtonList.Count; i++) {
            if (input.touchButtonList [i] != null) {
                RectTransform currentRectTransform = input.touchButtonList [i].GetComponent<RectTransform> ();

                if (currentRectTransform != null) {
                    buttons.Add (currentRectTransform);

                    buttonPosDefault.Add (currentRectTransform.anchoredPosition);

                    touchButtonList.Add (input.touchButtonList [i]);
                }
            }
        }

        print ("Touch Buttons Components stored");

        updateComponent ();
    }

    //after edit the buttons, their positions are saved
    public void activateEdit ()
    {
        enableButtonEdit = !enableButtonEdit;

        stopUpdateCoroutine ();

        if (enableButtonEdit) {
            changeButtonsState (false);

            mainUpdateCoroutine = StartCoroutine (updateCoroutine ());

        } else {
            buttonsPos.Clear ();

            for (i = 0; i < buttons.Count; i++) {
                if (buttons [i] != null) {
                    buttonsPos.Add (buttons [i].anchoredPosition);
                }
            }

            Save ();
        }
    }

    //if we cancel the edition, the buttons back to their previous position, if any of them has been moved
    public void disableEdit ()
    {
        if (enableButtonEdit) {
            enableButtonEdit = false;

            for (i = 0; i < buttons.Count; i++) {
                if (buttons [i] != null) {
                    buttons [i].anchoredPosition = buttonsPos [i];
                }
            }
        }
    }

    //set the buttons with their default position
    public void changeToDefault ()
    {
        for (i = 0; i < buttons.Count; i++) {
            if (buttons [i] != null) {
                buttons [i].anchoredPosition = buttonPosDefault [i];
            }
        }

        Save ();
    }

    //disable and enable the pointer click components in the buttons, to avoid activate them while the game is paused
    public void changeButtonsState (bool state)
    {
        for (i = 0; i < touchButtonList.Count; i++) {
            if (touchButtonList [i] != null && touchButtonList [i].enabled != state) {
                touchButtonList [i].enabled = state;
            }
        }
    }

    //save in a file the positions of the button, vector2 are not serializable if I am not wrong, so every axis is save in a list of float
    public void Save ()
    {
        checkSettings ();

        List<saveButtonPositionInfo> saveButtonPositionInfoList = new List<saveButtonPositionInfo> ();

        for (i = 0; i < buttons.Count; i++) {
            if (buttons [i] != null) {
                saveButtonPositionInfo newSaveButtonPositionInfo = new saveButtonPositionInfo ();

                newSaveButtonPositionInfo.positionX = buttons [i].anchoredPosition.x;
                newSaveButtonPositionInfo.positionY = buttons [i].anchoredPosition.y;

                saveButtonPositionInfoList.Add (newSaveButtonPositionInfo);
            }
        }

        string path = currentSaveDataPath + touchControlsPositionFileName + ".txt";

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        saveData.infoList = saveButtonPositionInfoList;

        string jsonToSave = JsonUtility.ToJson (saveData, true);

        saveFileContent (path, jsonToSave);

        buttonsPos.Clear ();

        for (i = 0; i < buttons.Count; i++) {
            buttonsPos.Add (new Vector2 (saveButtonPositionInfoList [i].positionX,
                saveButtonPositionInfoList [i].positionY));
        }
    }

    //load the positions of the buttons reading the file if it exists
    public void Load ()
    {
        checkSettings ();

        List<saveButtonPositionInfo> saveButtonPositionInfoList = new List<saveButtonPositionInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        string path = currentSaveDataPath + touchControlsPositionFileName + ".txt";

        if (getFileContent (path, out saveData)) {
            saveButtonPositionInfoList = saveData.infoList;
        }

        int saveButtonPositionInfoListCount = saveButtonPositionInfoList.Count;

        //also check the lenght are equal in both list, to avoid exceptions
        for (i = 0; i < buttons.Count; i++) {
            if (buttons [i] != null) {
                if (i < saveButtonPositionInfoListCount) {
                    buttons [i].anchoredPosition =
                            new Vector2 (saveButtonPositionInfoList [i].positionX, saveButtonPositionInfoList [i].positionY);
                }
            }
        }
    }

    bool getFileContent (string path, out SaveSlotListWrapper saveData)
    {
        bool fileResult = false;

        saveData = new SaveSlotListWrapper ();

        if (File.Exists (path)) {
            try {
                string json = File.ReadAllText (path);

                if (string.IsNullOrWhiteSpace (json)) {
                    if (showDebugInfo) {
                        Debug.Log ("Save File located but empty" + path);
                        Debug.Log ("\n\n");
                    }
                } else {
                    saveData = JsonUtility.FromJson<SaveSlotListWrapper> (json);

                    if (saveData == null) {
                        saveData = new SaveSlotListWrapper ();
                    }

                    if (saveData.infoList == null) {
                        saveData.infoList = new List<saveButtonPositionInfo> ();
                    }

                    fileResult = true;

                    if (showDebugInfo) {
                        Debug.Log ("Save File located " + path);
                        Debug.Log ("\n\n");
                    }
                }
            }

            catch (Exception ex) {
                Debug.LogError (
                    $"Error loading save file {path}\n{ex}"
                );

                saveData = new SaveSlotListWrapper ();

                if (saveData.infoList == null) {
                    saveData.infoList = new List<saveButtonPositionInfo> ();
                }
            }
        } else {
            if (showDebugInfo) {
                Debug.Log ("Save File not located " + path);
                Debug.Log ("\n\n");
            }
        }

        return fileResult;
    }

    void saveFileContent (string path, string jsonToSave)
    {
        try {
            string dir = Path.GetDirectoryName (path);

            if (!string.IsNullOrEmpty (dir)) {
                Directory.CreateDirectory (dir);
            }

            File.WriteAllText (path, jsonToSave, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) {
            Debug.LogError (
                $"Error saving file {path}\n{ex}"
            );
        }
    }

    [System.Serializable]
    public class SaveSlotListWrapper
    {
        public List<saveButtonPositionInfo> infoList = new List<saveButtonPositionInfo> ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Touch Control Positions", gameObject);
    }
}

//the positions of the buttons need to be serializable to save them in a file
[Serializable]
public class saveButtonPositionInfo
{
    public float positionX;
    public float positionY;
}