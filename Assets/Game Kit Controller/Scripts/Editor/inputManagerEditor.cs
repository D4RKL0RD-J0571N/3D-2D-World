using UnityEngine;
using System.Collections;
using System;

using System.Linq;
using HPhysic;


#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof (inputManager))]
public class inputManagerEditor : Editor
{
    SerializedProperty checkInputManagerElementsOnStartEnabled;

    SerializedProperty showActionKeysOnMenu;

#if REWIRED
	SerializedProperty useRewired;
#endif

    SerializedProperty editInputPanelPrefab;
    SerializedProperty editInputMenu;
    SerializedProperty currentInputPanelListText;
    SerializedProperty charactersManager;
    SerializedProperty pauseManager;
    SerializedProperty mainGameManager;
    SerializedProperty checkInputKeyDuplicatedOnRebindAction;
    SerializedProperty checkInputKeyDuplicatedOnlyOnSameInputCategory;

    SerializedProperty saveInputValuesOnLoadDefaultEnabled;

    SerializedProperty buttonsDisabledAtStart;
    SerializedProperty touchButtonList;
    SerializedProperty touchButtonsInfoList;

    SerializedProperty buttonsToIgnoreSetVisibleStateList;

    SerializedProperty playerInputManagerList;
    SerializedProperty ignoreGamepad;
    SerializedProperty allowKeyboardAndGamepad;
    SerializedProperty allowGamepadInTouchDevice;
    SerializedProperty checkConnectedGamepadRate;
    SerializedProperty showKeyboardPressed;
    SerializedProperty showKeyboardPressedAction;
    SerializedProperty showGamepadPressed;
    SerializedProperty showGamepadPressedAction;

    SerializedProperty showGamepadDebug;

    SerializedProperty showPressedKeyWhenEditingInput;
    SerializedProperty showDebugKeyPressed;
    SerializedProperty usingGamepad;
    SerializedProperty onlyOnePlayer;
    SerializedProperty usingKeyBoard;
    SerializedProperty numberOfGamepads;
    SerializedProperty lastButtonPressedOnGamepad;
    SerializedProperty lastKeyPressedOnKeyboard;
    SerializedProperty useTouchControls;
    SerializedProperty editingInput;
    SerializedProperty loadOption;
    SerializedProperty createRebindInputMenuWithCurrentAxes;
    SerializedProperty rebindInputMenuPanelsAmount;
    SerializedProperty rebindInputMenuActionsPerPanelAmount;
    SerializedProperty multiAxesList;

    SerializedProperty gamepadList;

    SerializedProperty newInputGamepadInfoListDebug;

    SerializedProperty currentInputActionList;

    SerializedProperty actionKeyToFilter;
    SerializedProperty joystickButtonToFilter;

    SerializedProperty newInputActionKeyToFilter;

    SerializedProperty newInputMouseButtonToFilter;

    SerializedProperty newInputGamepadButtonToFilter;

    SerializedProperty filterForMouseButton;

    SerializedProperty showMissingInputWarningMessagesEnabled;

    SerializedProperty useCustomInputFileName;
    SerializedProperty customInputFileName;

    SerializedProperty useCustomInputFilePathName;
    SerializedProperty customInputFilePathName;

    SerializedProperty customInputFileListAssigned;

    SerializedProperty useNewInputSystem;

    SerializedProperty useNewInputKeys;

    SerializedProperty moveActionReference;

    SerializedProperty lookActionReference;

    SerializedProperty newInputSystemMoveAxisSmoothSpeed;
    SerializedProperty newInputSystemLookAxisSmoothSpeed;

    SerializedProperty inputActionCategoryNameToSearch;
    SerializedProperty inputActionNameToRename;
    SerializedProperty newInputActionNameToUse;

    SerializedProperty inputActionCategoryNameToRename;
    SerializedProperty newInputActionCategoryName;

    SerializedProperty mainGamepadIconsData;

    public int customInputFileListIndex;

    inputManager manager;
    string controlScheme;
    bool checkState;

    string gamepadActive;
    bool actionEnabled;
    string isEnabled;

    string buttonMessage;

    Color defBackgroundColor;

    bool showCurentInputActionList;

    int touchButtonPanelIndex;
    int touchButtonIndex;

    string [] currentStringList;

    inputManager.Axes currentAxes;


    bool showHideSaveLoadOptions;

    bool showRenameInputActive;


    GUIStyle buttonStyle = new GUIStyle ();

    GUIStyle windowTextStyle = new GUIStyle ();

    GUIStyle mainInputListButtonStyle = new GUIStyle ();

#if ENABLE_INPUT_SYSTEM
    public const GamepadButton None = (GamepadButton)(-1);

    private static readonly GamepadButton [] Values =
        new [] { None }
        .Concat (Enum.GetValues (typeof (GamepadButton))
        .Cast<GamepadButton> ()
        .Distinct ())
        .ToArray ();

    private static readonly string [] Names =
        Values.Select (v =>
            v == None ? "None" : v.ToString ())
        .ToArray ();
#endif

    void OnEnable ()
    {
        checkInputManagerElementsOnStartEnabled = serializedObject.FindProperty ("checkInputManagerElementsOnStartEnabled");

        showActionKeysOnMenu = serializedObject.FindProperty ("showActionKeysOnMenu");

#if REWIRED
		useRewired = serializedObject.FindProperty ("useRewired");
#endif

        editInputPanelPrefab = serializedObject.FindProperty ("editInputPanelPrefab");
        editInputMenu = serializedObject.FindProperty ("editInputMenu");
        currentInputPanelListText = serializedObject.FindProperty ("currentInputPanelListText");
        charactersManager = serializedObject.FindProperty ("charactersManager");
        pauseManager = serializedObject.FindProperty ("pauseManager");
        mainGameManager = serializedObject.FindProperty ("mainGameManager");
        checkInputKeyDuplicatedOnRebindAction = serializedObject.FindProperty ("checkInputKeyDuplicatedOnRebindAction");
        checkInputKeyDuplicatedOnlyOnSameInputCategory = serializedObject.FindProperty ("checkInputKeyDuplicatedOnlyOnSameInputCategory");

        saveInputValuesOnLoadDefaultEnabled = serializedObject.FindProperty ("saveInputValuesOnLoadDefaultEnabled");

        buttonsDisabledAtStart = serializedObject.FindProperty ("buttonsDisabledAtStart");
        touchButtonList = serializedObject.FindProperty ("touchButtonList");
        touchButtonsInfoList = serializedObject.FindProperty ("touchButtonsInfoList");

        buttonsToIgnoreSetVisibleStateList = serializedObject.FindProperty ("buttonsToIgnoreSetVisibleStateList");

        playerInputManagerList = serializedObject.FindProperty ("playerInputManagerList");
        ignoreGamepad = serializedObject.FindProperty ("ignoreGamepad");
        allowKeyboardAndGamepad = serializedObject.FindProperty ("allowKeyboardAndGamepad");
        allowGamepadInTouchDevice = serializedObject.FindProperty ("allowGamepadInTouchDevice");
        checkConnectedGamepadRate = serializedObject.FindProperty ("checkConnectedGamepadRate");
        showKeyboardPressed = serializedObject.FindProperty ("showKeyboardPressed");
        showKeyboardPressedAction = serializedObject.FindProperty ("showKeyboardPressedAction");
        showGamepadPressed = serializedObject.FindProperty ("showGamepadPressed");
        showGamepadPressedAction = serializedObject.FindProperty ("showGamepadPressedAction");

        showGamepadDebug = serializedObject.FindProperty ("showGamepadDebug");

        showPressedKeyWhenEditingInput = serializedObject.FindProperty ("showPressedKeyWhenEditingInput");
        showDebugKeyPressed = serializedObject.FindProperty ("showDebugKeyPressed");
        usingGamepad = serializedObject.FindProperty ("usingGamepad");
        onlyOnePlayer = serializedObject.FindProperty ("onlyOnePlayer");
        usingKeyBoard = serializedObject.FindProperty ("usingKeyBoard");
        numberOfGamepads = serializedObject.FindProperty ("numberOfGamepads");
        lastButtonPressedOnGamepad = serializedObject.FindProperty ("lastButtonPressedOnGamepad");
        lastKeyPressedOnKeyboard = serializedObject.FindProperty ("lastKeyPressedOnKeyboard");
        useTouchControls = serializedObject.FindProperty ("useTouchControls");
        editingInput = serializedObject.FindProperty ("editingInput");
        loadOption = serializedObject.FindProperty ("loadOption");
        createRebindInputMenuWithCurrentAxes = serializedObject.FindProperty ("createRebindInputMenuWithCurrentAxes");
        rebindInputMenuPanelsAmount = serializedObject.FindProperty ("rebindInputMenuPanelsAmount");
        rebindInputMenuActionsPerPanelAmount = serializedObject.FindProperty ("rebindInputMenuActionsPerPanelAmount");
        multiAxesList = serializedObject.FindProperty ("multiAxesList");

        gamepadList = serializedObject.FindProperty ("gamepadList");

        newInputGamepadInfoListDebug = serializedObject.FindProperty ("newInputGamepadInfoListDebug");

        currentInputActionList = serializedObject.FindProperty ("currentInputActionList");

        actionKeyToFilter = serializedObject.FindProperty ("actionKeyToFilter");
        joystickButtonToFilter = serializedObject.FindProperty ("joystickButtonToFilter");

        newInputActionKeyToFilter = serializedObject.FindProperty ("newInputActionKeyToFilter");

        newInputMouseButtonToFilter = serializedObject.FindProperty ("newInputMouseButtonToFilter");

        newInputGamepadButtonToFilter = serializedObject.FindProperty ("newInputGamepadButtonToFilter");

        filterForMouseButton = serializedObject.FindProperty ("filterForMouseButton");

        showMissingInputWarningMessagesEnabled = serializedObject.FindProperty ("showMissingInputWarningMessagesEnabled");

        useCustomInputFileName = serializedObject.FindProperty ("useCustomInputFileName");
        customInputFileName = serializedObject.FindProperty ("customInputFileName");

        useCustomInputFilePathName = serializedObject.FindProperty ("useCustomInputFilePathName");
        customInputFilePathName = serializedObject.FindProperty ("customInputFilePathName");

        customInputFileListAssigned = serializedObject.FindProperty ("customInputFileListAssigned");

        useNewInputSystem = serializedObject.FindProperty ("useNewInputSystem");

        useNewInputKeys = serializedObject.FindProperty ("useNewInputKeys");

        moveActionReference = serializedObject.FindProperty ("moveActionReference");

        lookActionReference = serializedObject.FindProperty ("lookActionReference");

        newInputSystemMoveAxisSmoothSpeed = serializedObject.FindProperty ("newInputSystemMoveAxisSmoothSpeed");
        newInputSystemLookAxisSmoothSpeed = serializedObject.FindProperty ("newInputSystemLookAxisSmoothSpeed");

        inputActionCategoryNameToSearch = serializedObject.FindProperty ("inputActionCategoryNameToSearch");
        inputActionNameToRename = serializedObject.FindProperty ("inputActionNameToRename");
        newInputActionNameToUse = serializedObject.FindProperty ("newInputActionNameToUse");

        inputActionCategoryNameToRename = serializedObject.FindProperty ("inputActionCategoryNameToRename");
        newInputActionCategoryName = serializedObject.FindProperty ("newInputActionCategoryName");

        mainGamepadIconsData = serializedObject.FindProperty ("mainGamepadIconsData");

        manager = (inputManager)target;
    }

    public override void OnInspectorGUI ()
    {
        EditorGUI.BeginChangeCheck ();

        EditorGUILayout.Space ();

        buttonStyle = new GUIStyle (GUI.skin.button);

        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.fontSize = 16;

        GUILayout.BeginVertical ("Main Settings", "window");
        EditorGUILayout.PropertyField (showActionKeysOnMenu);
        EditorGUILayout.PropertyField (checkInputManagerElementsOnStartEnabled);


        EditorGUILayout.Space ();

        EditorGUILayout.PropertyField (useNewInputSystem);

        if (useNewInputSystem.boolValue) {
            EditorGUILayout.PropertyField (useNewInputKeys);

#if ENABLE_INPUT_SYSTEM

            EditorGUILayout.Space ();

            EditorGUILayout.PropertyField (moveActionReference);

            EditorGUILayout.PropertyField (lookActionReference);

            EditorGUILayout.Space ();

            EditorGUILayout.PropertyField (newInputSystemMoveAxisSmoothSpeed);
            EditorGUILayout.PropertyField (newInputSystemLookAxisSmoothSpeed);

            EditorGUILayout.Space ();

            if (GUILayout.Button ("Convert Key Code To Keys", buttonStyle)) {
                if (!Application.isPlaying) {
                    manager.convertKeyCodeToKeys ();
                }
            }

#endif
        }

#if REWIRED
		EditorGUILayout.PropertyField (useRewired);
#endif
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Gamepad Settings", "window");
        EditorGUILayout.PropertyField (ignoreGamepad);
        EditorGUILayout.PropertyField (allowKeyboardAndGamepad);
        EditorGUILayout.PropertyField (allowGamepadInTouchDevice);
        EditorGUILayout.PropertyField (checkConnectedGamepadRate);
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Debug Settings", "window");
        EditorGUILayout.PropertyField (showMissingInputWarningMessagesEnabled);
        EditorGUILayout.PropertyField (showKeyboardPressed);
        EditorGUILayout.PropertyField (showKeyboardPressedAction);
        EditorGUILayout.PropertyField (showGamepadPressed);
        EditorGUILayout.PropertyField (showGamepadPressedAction);
        EditorGUILayout.PropertyField (showGamepadDebug);
        EditorGUILayout.PropertyField (showPressedKeyWhenEditingInput);

        EditorGUILayout.PropertyField (showDebugKeyPressed);
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Input State (DEBUG)", "window");
        EditorGUILayout.PropertyField (usingGamepad);
        EditorGUILayout.PropertyField (onlyOnePlayer);
        EditorGUILayout.PropertyField (usingKeyBoard);
        EditorGUILayout.PropertyField (numberOfGamepads);
        EditorGUILayout.PropertyField (lastButtonPressedOnGamepad);
        EditorGUILayout.PropertyField (lastKeyPressedOnKeyboard);
        EditorGUILayout.PropertyField (useTouchControls);
        EditorGUILayout.PropertyField (editingInput);
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Rebind Settings", "window");
        EditorGUILayout.PropertyField (checkInputKeyDuplicatedOnRebindAction);
        EditorGUILayout.PropertyField (checkInputKeyDuplicatedOnlyOnSameInputCategory);

        EditorGUILayout.Space ();

        EditorGUILayout.PropertyField (saveInputValuesOnLoadDefaultEnabled);
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Save/Load Settings", "window");
        EditorGUILayout.PropertyField (loadOption);
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();
        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        windowTextStyle.fontStyle = FontStyle.Bold;
        windowTextStyle.fontSize = 25;
        windowTextStyle.alignment = TextAnchor.MiddleCenter;

        GUILayout.Label ("Multi Input List", windowTextStyle);

        //		GUILayout.BeginVertical (windowTextStyle);
        EditorGUILayout.Space ();

        showMultiAxesList (multiAxesList);
        //		EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        //check the current controls enabled
        if (!checkState) {
            if (useTouchControls.boolValue) {
                controlScheme = "Mobile";
            } else {
                controlScheme = "Keyboard";
            }
            checkState = true;
        }

        GUILayout.Label ("Input Save/Load Options", windowTextStyle);

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide Save/Load Options", buttonStyle)) {
            showHideSaveLoadOptions = !showHideSaveLoadOptions;
        }

        if (showHideSaveLoadOptions) {
            EditorGUILayout.Space ();
            EditorGUILayout.Space ();

            //set the axes list in the inspector to the default value
            if (GUILayout.Button ("Save Current Input As Default")) {
                manager.saveCurrentInputAsDefault ();
            }

            if (GUILayout.Button ("Load Default Input")) {
                manager.setCurrentInputToDefault ();
            }

            //save the axes list in the inspector in a file
            if (GUILayout.Button ("Save Input To File")) {
                manager.saveButtonsInputToSaveFile ();
            }

            //set the axes list in the inspector to the values stored in a file
            if (GUILayout.Button ("Load Input From File")) {
                manager.loadButtonsInspectorFromSaveFile ();
            }

            EditorGUILayout.Space ();
            EditorGUILayout.Space ();

            EditorGUILayout.PropertyField (useCustomInputFileName);
            if (useCustomInputFileName.boolValue) {
                GUILayout.Label ("Only used for save and load custom input file");

                EditorGUILayout.Space ();

                if (GUILayout.Button ("Show Custom Input File List")) {
                    manager.showCustomInputFileList ();
                }

                if (customInputFileListAssigned.boolValue) {
                    if (manager.customInputFileList.Length > 0) {
                        if (customInputFileListIndex < manager.customInputFileList.Length) {
                            EditorGUILayout.Space ();

                            GUILayout.BeginHorizontal ();
                            GUILayout.Label ("Current Input File Name", EditorStyles.boldLabel, GUILayout.MaxWidth (150));
                            customInputFileListIndex = EditorGUILayout.Popup (customInputFileListIndex, manager.customInputFileList, GUILayout.ExpandWidth (true));
                            GUILayout.EndHorizontal ();

                            EditorGUILayout.Space ();

                            manager.customInputFileName = manager.customInputFileList [customInputFileListIndex];
                        }
                    }
                }

                EditorGUILayout.Space ();

                EditorGUILayout.PropertyField (customInputFileName);

                EditorGUILayout.Space ();

                EditorGUILayout.PropertyField (useCustomInputFilePathName);
                if (useCustomInputFilePathName.boolValue) {
                    EditorGUILayout.PropertyField (customInputFilePathName);
                }
            }

            EditorGUILayout.Space ();
            EditorGUILayout.Space ();
        }

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();
        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        //show the controls scheme
        GUILayout.Label ("CURRENT CONTROLS: " + controlScheme, windowTextStyle);
        //set the keyboard controls
        if (GUILayout.Button ("Set Keyboard Controls")) {
            manager.setKeyboardControlsFromEditor (true);
            controlScheme = "Keyboard";
        }

        //set the touch controls
        if (GUILayout.Button ("Set Touch Controls")) {
            manager.setKeyboardControlsFromEditor (false);
            controlScheme = "Mobile";
        }

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();
        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Player Input Manager List", "window");
        showSimpleList (playerInputManagerList, "Player");

        if (playerInputManagerList.isExpanded) {

            EditorGUILayout.Space ();

            if (GUILayout.Button ("Update Players Input Manager Info")) {
                manager.updatePlayerInputManagerList ();

            }
        }
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Input Elements", "window");
        EditorGUILayout.PropertyField (editInputPanelPrefab);
        EditorGUILayout.PropertyField (editInputMenu);
        EditorGUILayout.PropertyField (currentInputPanelListText);
        EditorGUILayout.PropertyField (charactersManager);
        EditorGUILayout.PropertyField (pauseManager);
        EditorGUILayout.PropertyField (mainGameManager);
        EditorGUILayout.PropertyField (mainGamepadIconsData);
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Touch Controls Settings", "window");

        GUILayout.BeginVertical ("Touch Buttons Disabled At Start", "window");
        showButtonsToDisableAtStartList (buttonsDisabledAtStart);
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Touch Buttons Ignores To Set Visible State", "window");
        showButtonsToIgnoreSetVisibleStateList (buttonsToIgnoreSetVisibleStateList);
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Get Touch Button List")) {
            manager.getTouchButtonListString ();
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Select Main Touch Button Panel On Editor")) {
            manager.selectMainTouchButtonPanelOnEditor ();
        }

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Touch Button List", "window");

        EditorGUILayout.Space ();

        showTouchButtonList (touchButtonList);

        EditorGUILayout.Space ();

        showTouchButtonsInfoList (touchButtonsInfoList);

        EditorGUILayout.Space ();

        EditorGUILayout.EndVertical ();

        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Rebind Input Menu Options", "window");
        EditorGUILayout.PropertyField (createRebindInputMenuWithCurrentAxes);
        EditorGUILayout.PropertyField (rebindInputMenuPanelsAmount);
        EditorGUILayout.PropertyField (rebindInputMenuActionsPerPanelAmount);
        EditorGUILayout.Space ();

        if (GUILayout.Button ("Instantiate Rebind Menus And Actions Panels")) {
            manager.instantiateRebindMenusAndActionsPanels (true);
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Destroy Rebind Menus And Actions Panels")) {
            manager.destroyRebindMenusAndActionsPanels ();
        }
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        gamepadActive = "NO";
        if (usingGamepad.boolValue) {
            gamepadActive = "YES";
        }

        GUILayout.BeginVertical ("Gamepad State (Debug)", "window");
        GUILayout.Label ("Using Gamepad: " + gamepadActive);

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Gamepad List", "window");
        if (useNewInputSystem.boolValue) {
            showNewGamepadList (newInputGamepadInfoListDebug);
        } else {
            showGamepadList (gamepadList);
        }
        EditorGUILayout.EndVertical ();
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Show Current Input Action List Settings Filter", "window");

        EditorGUILayout.Space ();

        defBackgroundColor = GUI.backgroundColor;
        EditorGUILayout.BeginHorizontal ();
        if (showCurentInputActionList) {
            GUI.backgroundColor = Color.gray;
            buttonMessage = "Hide Current Input Action List Settings";
        } else {
            GUI.backgroundColor = defBackgroundColor;
            buttonMessage = "Show Current Input Action List Settings";
        }
        if (GUILayout.Button (buttonMessage, buttonStyle)) {
            showCurentInputActionList = !showCurentInputActionList;
        }
        GUI.backgroundColor = defBackgroundColor;
        EditorGUILayout.EndHorizontal ();

        if (showCurentInputActionList) {

            showCurrentInputActionListButtons ();

            EditorGUILayout.Space ();

            if (currentInputActionList.arraySize > 0) {
                for (int i = 0; i < currentInputActionList.arraySize; i++) {


                    GUILayout.Label (currentInputActionList.GetArrayElementAtIndex (i).stringValue);
                }


                showCurrentInputActionListButtons ();
            }
        }
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();




        GUILayout.BeginVertical ("Rename Input Settings", "window");

        EditorGUILayout.Space ();

        defBackgroundColor = GUI.backgroundColor;
        EditorGUILayout.BeginHorizontal ();
        if (showRenameInputActive) {
            GUI.backgroundColor = Color.gray;
            buttonMessage = "Hide Rename Input Settings";
        } else {
            GUI.backgroundColor = defBackgroundColor;
            buttonMessage = "Show Rename Input Settings";
        }
        if (GUILayout.Button (buttonMessage, buttonStyle)) {
            showRenameInputActive = !showRenameInputActive;
        }
        GUI.backgroundColor = defBackgroundColor;
        EditorGUILayout.EndHorizontal ();

        if (showRenameInputActive) {
            EditorGUILayout.PropertyField (inputActionCategoryNameToSearch);
            EditorGUILayout.PropertyField (inputActionNameToRename);
            EditorGUILayout.PropertyField (newInputActionNameToUse);

            EditorGUILayout.Space ();

            if (GUILayout.Button ("Confirm Rename Action", buttonStyle)) {
                if (!GKC_Utils.isApplicationPlaying ()) {
                    manager.confirmInputActionRename ();
                }
            }

            EditorGUILayout.Space ();
            EditorGUILayout.Space ();

            EditorGUILayout.PropertyField (inputActionCategoryNameToRename);
            EditorGUILayout.PropertyField (newInputActionCategoryName);

            EditorGUILayout.Space ();

            if (GUILayout.Button ("Confirm Rename Category", buttonStyle)) {
                if (!GKC_Utils.isApplicationPlaying ()) {
                    manager.confirmInputActionCategoryRename ();
                }
            }
        }
        EditorGUILayout.EndVertical ();



        if (EditorGUI.EndChangeCheck ()) {
            serializedObject.ApplyModifiedProperties ();

            Repaint ();
        }
    }

    void showCurrentInputActionListButtons ()
    {
        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show Full Current Input Action List")) {
            manager.showCurrentInputActionList (false, false);
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show Only Keys Input Action List")) {
            manager.showCurrentInputActionList (true, false);
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show Only Joysticks Input Action List")) {
            manager.showCurrentInputActionList (false, true);
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Clear Keys Input Action List Text")) {
            manager.clearInputActionListText ();
        }

        EditorGUILayout.Space ();

        if (useNewInputSystem.boolValue) {
            EditorGUILayout.PropertyField (filterForMouseButton);

            EditorGUILayout.Space ();

            if (filterForMouseButton.boolValue) {
                EditorGUILayout.PropertyField (newInputMouseButtonToFilter);
            } else {
                EditorGUILayout.PropertyField (newInputActionKeyToFilter);
            }
        } else {
            EditorGUILayout.PropertyField (actionKeyToFilter);
        }

        if (GUILayout.Button ("Show Input Actions Keys By Filter")) {
            manager.showCurrentInputActionListByKeyFilter ();
        }

        EditorGUILayout.Space ();

        if (useNewInputSystem.boolValue) {
            EditorGUILayout.PropertyField (newInputGamepadButtonToFilter);
        } else {
            EditorGUILayout.PropertyField (joystickButtonToFilter);
        }

        if (GUILayout.Button ("Show Input Actions Joystick By Filter")) {
            manager.showCurrentInputActionListByJoystickFilter ();
        }
    }

    void showListElementInfo (SerializedProperty list, int multiAxesIndex, int axesIndex)
    {
        EditorGUILayout.BeginVertical ("box");
        EditorGUILayout.PropertyField (list.FindPropertyRelative ("Name"));

        EditorGUILayout.PropertyField (list.FindPropertyRelative ("actionEnabled"));

        EditorGUILayout.PropertyField (list.FindPropertyRelative ("showActionInRebindPanel"));

        EditorGUILayout.PropertyField (list.FindPropertyRelative ("keyButton"));

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Keyboard Input", "window");

        if (!useNewInputKeys.boolValue) {
            EditorGUILayout.PropertyField (list.FindPropertyRelative ("key"));
        }

#if ENABLE_INPUT_SYSTEM
        EditorGUILayout.Space ();

        if (useNewInputKeys.boolValue && !list.FindPropertyRelative ("useMouseButtonType").boolValue) {

            EditorGUILayout.PropertyField (list.FindPropertyRelative ("keyInput"));

            EditorGUILayout.Space ();
        }

        EditorGUILayout.PropertyField (list.FindPropertyRelative ("useMouseButtonType"));

        if (list.FindPropertyRelative ("useMouseButtonType").boolValue) {
            EditorGUILayout.Space ();

            EditorGUILayout.PropertyField (list.FindPropertyRelative ("mouseButtonInput"));
        }

        EditorGUILayout.Space ();
#endif

        EditorGUILayout.Space ();

        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Joystick Input", "window");

        if (useNewInputSystem.boolValue) {
#if ENABLE_INPUT_SYSTEM

            EditorGUILayout.Space ();

            //EditorGUILayout.PropertyField (list.FindPropertyRelative ("gamepadButton"));

            list.FindPropertyRelative ("gamepadButton").intValue =
                (int)drawGamepadButtonField (list.FindPropertyRelative ("gamepadButton").displayName,
                (GamepadButton)list.FindPropertyRelative ("gamepadButton").intValue);

#endif
        } else {
            EditorGUILayout.PropertyField (list.FindPropertyRelative ("joystickButton"));
        }
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Touch Button Input", "window");

        if (manager.touchButtonsStringInfoList.Count > 0) {

            currentStringList = manager.touchButtonListString;

            if (currentStringList.Length > 0) {

                currentAxes = manager.multiAxesList [multiAxesIndex].axes [axesIndex];

                touchButtonPanelIndex = currentAxes.touchButtonPanelIndex;

                touchButtonPanelIndex = EditorGUILayout.Popup ("Touch Panel", touchButtonPanelIndex, currentStringList);

                currentAxes.touchButtonPanelIndex = touchButtonPanelIndex;

                if (touchButtonPanelIndex >= 0) {
                    if (touchButtonPanelIndex > 0) {
                        if (touchButtonPanelIndex < currentStringList.Length) {
                            currentAxes.touchButtonPanel = currentStringList [touchButtonPanelIndex];

                            touchButtonIndex = currentAxes.touchButtonIndex;

                            if (manager.touchButtonsStringInfoList.Count > touchButtonPanelIndex) {
                                currentStringList = manager.touchButtonsStringInfoList [touchButtonPanelIndex].touchButtonListString;

                                touchButtonIndex = EditorGUILayout.Popup ("Touch Button", touchButtonIndex, currentStringList);

                                currentAxes.touchButtonIndex = touchButtonIndex;

                                if (touchButtonIndex >= 0) {
                                    if (touchButtonIndex < currentStringList.Length) {
                                        currentAxes.touchButtonName = currentStringList [touchButtonIndex];
                                    }
                                } else {
                                    currentAxes.touchButtonName = "TOUCH BUTTON NAME NO FOUND";
                                }
                            }
                        }
                    }
                } else {
                    currentAxes.touchButtonPanel = "TOUCH PANEL NAME NO FOUND";
                }
            }
        }

        EditorGUILayout.EndVertical ();

#if REWIRED
		EditorGUILayout.Space ();

		GUILayout.BeginVertical ("Rewired Input", "window");
		EditorGUILayout.PropertyField (list.FindPropertyRelative ("rewiredAction"));
		EditorGUILayout.PropertyField (list.FindPropertyRelative ("axisContribution"));
		EditorGUILayout.EndVertical ();
#endif

        EditorGUILayout.EndVertical ();
    }

#if ENABLE_INPUT_SYSTEM
    public static GamepadButton drawGamepadButtonField (string label, GamepadButton value)
    {
        int currentIndex =
            Array.IndexOf (Values, value);

        if (currentIndex < 0)
            currentIndex = 0;

        int selected =
            EditorGUILayout.Popup (
                label,
                currentIndex,
                Names);

        return Values [selected];
    }
#endif

    void showMultiListElementInfo (SerializedProperty list, int axeListIndex)
    {
        GUILayout.BeginVertical ("Axes", "window");

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Axes List Settings", "window");
        EditorGUILayout.PropertyField (list.FindPropertyRelative ("axesName"));
        EditorGUILayout.PropertyField (list.FindPropertyRelative ("currentlyActive"));
        EditorGUILayout.PropertyField (list.FindPropertyRelative ("multiAxesEditPanelActive"));
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Input List", "window");
        showAxesList (list.FindPropertyRelative ("axes"), axeListIndex);
        EditorGUILayout.EndVertical ();

        EditorGUILayout.Space ();

        EditorGUILayout.EndVertical ();
    }

    void showAxesList (SerializedProperty list, int axeListIndex)
    {
        EditorGUILayout.BeginVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide Axes List", buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {

            int currentArraySize = list.arraySize;

            EditorGUILayout.Space ();

            GUILayout.Label ("Number Of Actions: \t" + currentArraySize);

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();

            if (GUILayout.Button ("Add Action")) {
                manager.addNewAxe (axeListIndex);

                currentArraySize = list.arraySize;
            }

            if (GUILayout.Button ("Clear")) {
                list.arraySize = 0;

                currentArraySize = list.arraySize;
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Expand All")) {
                for (int i = 0; i < currentArraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = true;
                }
            }
            if (GUILayout.Button ("Collapse All")) {
                for (int i = 0; i < currentArraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = false;
                }
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            for (int i = 0; i < currentArraySize; i++) {
                bool expanded = false;
                EditorGUILayout.BeginHorizontal ();
                EditorGUILayout.BeginHorizontal ("box");

                EditorGUILayout.Space ();

                if (i < currentArraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();

                    SerializedProperty currentArrayElement = list.GetArrayElementAtIndex (i);

                    actionEnabled = currentArrayElement.FindPropertyRelative ("actionEnabled").boolValue;
                    isEnabled = " +";
                    if (!actionEnabled) {
                        isEnabled = " -";
                    }
                    EditorGUILayout.PropertyField (currentArrayElement, new GUIContent (currentArrayElement.displayName + isEnabled), false);

                    if (currentArrayElement.isExpanded) {
                        expanded = true;
                        showListElementInfo (currentArrayElement, axeListIndex, i);
                    }

                    EditorGUILayout.Space ();

                    EditorGUILayout.EndVertical ();
                }
                EditorGUILayout.EndHorizontal ();
                if (expanded) {
                    EditorGUILayout.BeginVertical ();
                } else {
                    EditorGUILayout.BeginHorizontal ();
                }
                if (GUILayout.Button ("x")) {
                    manager.removeAxesElement (axeListIndex, i);

                    currentArraySize = list.arraySize;
                }
                if (GUILayout.Button ("v")) {
                    if (i >= 0) {
                        list.MoveArrayElement (i, i + 1);
                    }
                }
                if (GUILayout.Button ("^")) {
                    if (i < currentArraySize) {
                        list.MoveArrayElement (i, i - 1);
                    }
                }
                if (expanded) {
                    EditorGUILayout.EndVertical ();
                } else {
                    EditorGUILayout.EndHorizontal ();
                }
                EditorGUILayout.EndHorizontal ();
            }
        }
        EditorGUILayout.EndVertical ();
    }

    void showMultiAxesList (SerializedProperty list)
    {
        EditorGUILayout.BeginVertical ();

        EditorGUILayout.Space ();

        mainInputListButtonStyle = new GUIStyle (GUI.skin.button);

        mainInputListButtonStyle.fontStyle = FontStyle.Bold;
        mainInputListButtonStyle.fontSize = 20;

        if (GUILayout.Button ("Show/Hide Multi Axes List", mainInputListButtonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {

            int currentArraySize = list.arraySize;

            EditorGUILayout.Space ();

            GUILayout.Label ("Number Of Axes: \t" + currentArraySize);

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();

            if (GUILayout.Button ("Add Axes List")) {
                manager.addNewAxesList ();

                currentArraySize = list.arraySize;
            }

            if (GUILayout.Button ("Clear")) {
                list.arraySize = 0;

                currentArraySize = list.arraySize;
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Expand All")) {
                for (int i = 0; i < currentArraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = true;
                }
            }
            if (GUILayout.Button ("Collapse All")) {
                for (int i = 0; i < currentArraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = false;
                }
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            if (GUILayout.Button ("Update Input Inspector")) {
                manager.updateInputInspector ();
            }

            EditorGUILayout.Space ();

            for (int i = 0; i < currentArraySize; i++) {
                bool expanded = false;
                EditorGUILayout.BeginHorizontal ();
                EditorGUILayout.BeginHorizontal ("box");

                EditorGUILayout.Space ();

                if (i < currentArraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();

                    SerializedProperty currentArrayElement = list.GetArrayElementAtIndex (i);

                    EditorGUILayout.PropertyField (currentArrayElement, new GUIContent (currentArrayElement.displayName), false);

                    if (currentArrayElement.isExpanded) {
                        expanded = true;
                        showMultiListElementInfo (currentArrayElement, i);
                    }

                    EditorGUILayout.Space ();

                    EditorGUILayout.EndVertical ();
                }
                EditorGUILayout.EndHorizontal ();
                if (expanded) {
                    EditorGUILayout.BeginVertical ();
                } else {
                    EditorGUILayout.BeginHorizontal ();
                }
                if (GUILayout.Button ("x")) {
                    manager.removeAxesList (i);

                    currentArraySize = list.arraySize;
                }
                if (GUILayout.Button ("v")) {
                    if (i >= 0) {
                        list.MoveArrayElement (i, i + 1);
                    }
                }
                if (GUILayout.Button ("^")) {
                    if (i < currentArraySize) {
                        list.MoveArrayElement (i, i - 1);
                    }
                }
                if (expanded) {
                    EditorGUILayout.EndVertical ();
                } else {
                    EditorGUILayout.EndHorizontal ();
                }
                EditorGUILayout.EndHorizontal ();
            }
        }
        EditorGUILayout.EndVertical ();
    }

    void showButtonsToDisableAtStartList (SerializedProperty list)
    {
        EditorGUILayout.BeginVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide Buttons To Disable At Start List", buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {

            int currentArraySize = list.arraySize;

            EditorGUILayout.Space ();

            GUILayout.Label ("Number Of Actions: \t" + currentArraySize);

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();

            if (GUILayout.Button ("Add Button")) {
                list.arraySize++;

                currentArraySize = list.arraySize;
            }

            if (GUILayout.Button ("Clear")) {
                list.arraySize = 0;

                currentArraySize = list.arraySize;
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            for (int i = 0; i < currentArraySize; i++) {
                EditorGUILayout.BeginHorizontal ();
                EditorGUILayout.BeginHorizontal ("box");

                EditorGUILayout.Space ();

                if (i < currentArraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();

                    SerializedProperty currentArrayElement = list.GetArrayElementAtIndex (i);

                    EditorGUILayout.PropertyField (currentArrayElement, new GUIContent (currentArrayElement.displayName), false);

                    EditorGUILayout.Space ();

                    EditorGUILayout.EndVertical ();
                }
                EditorGUILayout.EndHorizontal ();
                EditorGUILayout.BeginHorizontal ();
                if (GUILayout.Button ("x")) {
                    list.DeleteArrayElementAtIndex (i);

                    currentArraySize = list.arraySize;
                }
                EditorGUILayout.EndHorizontal ();
                EditorGUILayout.EndHorizontal ();
            }
        }
        EditorGUILayout.EndVertical ();
    }

    void showSimpleList (SerializedProperty list, string listName)
    {
        EditorGUILayout.BeginVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide Player Input Manager List", buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {

            int currentArraySize = list.arraySize;

            EditorGUILayout.Space ();

            GUILayout.Label (listName + "s: \t" + currentArraySize);

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();

            if (GUILayout.Button ("Add " + listName)) {
                list.arraySize++;

                currentArraySize = list.arraySize;
            }

            if (GUILayout.Button ("Clear")) {
                list.arraySize = 0;

                currentArraySize = list.arraySize;
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            for (int i = 0; i < currentArraySize; i++) {
                EditorGUILayout.BeginHorizontal ();
                EditorGUILayout.BeginHorizontal ("box");

                EditorGUILayout.Space ();

                if (i < currentArraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();

                    SerializedProperty currentArrayElement = list.GetArrayElementAtIndex (i);

                    EditorGUILayout.PropertyField (currentArrayElement, new GUIContent (currentArrayElement.displayName), false);

                    EditorGUILayout.Space ();

                    EditorGUILayout.EndVertical ();
                }
                EditorGUILayout.EndHorizontal ();
                EditorGUILayout.BeginHorizontal ();
                if (GUILayout.Button ("x")) {
                    list.DeleteArrayElementAtIndex (i);

                    currentArraySize = list.arraySize;
                }
                EditorGUILayout.EndHorizontal ();
                EditorGUILayout.EndHorizontal ();
            }
        }
        EditorGUILayout.EndVertical ();
    }

    void showTouchButtonsInfoListElementButton (SerializedProperty list, int touchPanelIndex)
    {
        EditorGUILayout.BeginVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide Touch Buttons Info List", buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {

            int currentArraySize = list.arraySize;

            EditorGUILayout.Space ();

            GUILayout.Label ("Touch Button List \t" + currentArraySize);

            EditorGUILayout.Space ();

            for (int i = 0; i < currentArraySize; i++) {
                EditorGUILayout.BeginHorizontal ();
                EditorGUILayout.BeginHorizontal ("box");

                EditorGUILayout.Space ();

                if (i < currentArraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();

                    SerializedProperty currentArrayElement = list.GetArrayElementAtIndex (i);

                    EditorGUILayout.PropertyField (currentArrayElement, new GUIContent (currentArrayElement.displayName), false);

                    EditorGUILayout.Space ();

                    EditorGUILayout.EndVertical ();
                }
                EditorGUILayout.EndHorizontal ();
                EditorGUILayout.BeginHorizontal ();
                if (GUILayout.Button ("x")) {
                    manager.removeTouchButton (touchPanelIndex, i);

                    currentArraySize = list.arraySize;

                }
                EditorGUILayout.EndHorizontal ();
                EditorGUILayout.EndHorizontal ();
            }
        }
        EditorGUILayout.EndVertical ();
    }

    void showTouchButtonList (SerializedProperty list)
    {
        EditorGUILayout.BeginVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide Touch Button List", buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {

            int currentArraySize = list.arraySize;

            EditorGUILayout.Space ();

            GUILayout.Label ("Touch Button List: \t" + currentArraySize);

            EditorGUILayout.Space ();

            for (int i = 0; i < currentArraySize; i++) {
                EditorGUILayout.BeginHorizontal ();
                EditorGUILayout.BeginHorizontal ("box");

                EditorGUILayout.Space ();

                if (i < currentArraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();

                    SerializedProperty currentArrayElement = list.GetArrayElementAtIndex (i);

                    EditorGUILayout.PropertyField (currentArrayElement, new GUIContent (currentArrayElement.displayName), false);

                    EditorGUILayout.Space ();

                    EditorGUILayout.EndVertical ();
                }
                EditorGUILayout.EndHorizontal ();
                EditorGUILayout.EndHorizontal ();
            }
        }
        EditorGUILayout.EndVertical ();
    }


    void showTouchButtonsInfoListElement (SerializedProperty list, int touchPanelIndex)
    {
        EditorGUILayout.BeginVertical ("box");
        EditorGUILayout.PropertyField (list.FindPropertyRelative ("Name"));

        EditorGUILayout.Space ();

        showTouchButtonsInfoListElementButton (list.FindPropertyRelative ("touchButtonList"), touchPanelIndex);
        EditorGUILayout.EndVertical ();
    }

    void showTouchButtonsInfoList (SerializedProperty list)
    {
        EditorGUILayout.BeginVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide Touch Buttons Info List", buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {
            int currentArraySize = list.arraySize;

            GUILayout.Label ("Number Of Panels: \t" + currentArraySize);

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Add Panel")) {
                list.arraySize++;

                currentArraySize = list.arraySize;
            }
            if (GUILayout.Button ("Clear List")) {
                list.arraySize = 0;

                currentArraySize = list.arraySize;
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Expand All")) {
                for (int i = 0; i < currentArraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = true;
                }
            }
            if (GUILayout.Button ("Collapse All")) {
                for (int i = 0; i < currentArraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = false;
                }
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            for (int i = 0; i < currentArraySize; i++) {
                bool expanded = false;
                EditorGUILayout.BeginHorizontal ();
                EditorGUILayout.BeginHorizontal ("box");

                EditorGUILayout.Space ();

                if (i < currentArraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();

                    SerializedProperty currentArrayElement = list.GetArrayElementAtIndex (i);

                    EditorGUILayout.PropertyField (currentArrayElement, false);

                    if (currentArrayElement.isExpanded) {
                        showTouchButtonsInfoListElement (currentArrayElement, i);
                        expanded = true;
                    }

                    EditorGUILayout.Space ();

                    EditorGUILayout.EndVertical ();
                }
                EditorGUILayout.EndHorizontal ();
                if (expanded) {
                    EditorGUILayout.BeginVertical ();
                    if (GUILayout.Button ("x")) {
                        manager.removeTouchPanel (i);

                        currentArraySize = list.arraySize;
                    }
                    if (GUILayout.Button ("v")) {
                        if (i >= 0) {
                            list.MoveArrayElement (i, i + 1);
                        }
                    }
                    if (GUILayout.Button ("^")) {
                        if (i < currentArraySize) {
                            list.MoveArrayElement (i, i - 1);
                        }
                    }
                    EditorGUILayout.EndVertical ();
                } else {
                    EditorGUILayout.BeginHorizontal ();
                    if (GUILayout.Button ("x")) {
                        manager.removeTouchPanel (i);

                        currentArraySize = list.arraySize;
                    }
                    if (GUILayout.Button ("v")) {
                        if (i >= 0) {
                            list.MoveArrayElement (i, i + 1);
                        }
                    }
                    if (GUILayout.Button ("^")) {
                        if (i < currentArraySize) {
                            list.MoveArrayElement (i, i - 1);
                        }
                    }
                    EditorGUILayout.EndHorizontal ();
                }
                EditorGUILayout.EndHorizontal ();
            }
        }
        EditorGUILayout.EndVertical ();
    }

    void showButtonsToIgnoreSetVisibleStateList (SerializedProperty list)
    {
        EditorGUILayout.BeginVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide Buttons To Ignore List", buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {

            int currentArraySize = list.arraySize;

            GUILayout.Label ("Number Of Buttons: \t" + currentArraySize);

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Add Button")) {
                list.arraySize++;

                currentArraySize = list.arraySize;
            }
            if (GUILayout.Button ("Clear List")) {
                list.arraySize = 0;

                currentArraySize = list.arraySize;
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            for (int i = 0; i < currentArraySize; i++) {
                EditorGUILayout.BeginHorizontal ("box");

                if (i < currentArraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();
                    EditorGUILayout.PropertyField (list.GetArrayElementAtIndex (i), false);

                    EditorGUILayout.EndVertical ();
                }

                if (GUILayout.Button ("x")) {
                    list.DeleteArrayElementAtIndex (i);

                    currentArraySize = list.arraySize;
                }

                EditorGUILayout.EndHorizontal ();
            }
        }
        EditorGUILayout.EndVertical ();
    }

    void showGamepadList (SerializedProperty list)
    {
        EditorGUILayout.BeginVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide Gamepad List", buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {

            int currentArraySize = list.arraySize;

            EditorGUILayout.Space ();

            GUILayout.Label ("Number Of Gamepads: \t" + currentArraySize);

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Expand All")) {
                for (int i = 0; i < currentArraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = true;
                }
            }
            if (GUILayout.Button ("Collapse All")) {
                for (int i = 0; i < currentArraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = false;
                }
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            for (int i = 0; i < currentArraySize; i++) {
                EditorGUILayout.BeginHorizontal ();
                EditorGUILayout.BeginHorizontal ("box");

                EditorGUILayout.Space ();

                if (i < currentArraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();

                    SerializedProperty currentArrayElement = list.GetArrayElementAtIndex (i);

                    EditorGUILayout.PropertyField (currentArrayElement, false);

                    if (currentArrayElement.isExpanded) {
                        showGamepadListElement (currentArrayElement);
                    }

                    EditorGUILayout.Space ();

                    EditorGUILayout.EndVertical ();
                }
                EditorGUILayout.EndHorizontal ();

                EditorGUILayout.EndHorizontal ();
            }
        }
        EditorGUILayout.EndVertical ();
    }

    void showGamepadListElement (SerializedProperty list)
    {
        EditorGUILayout.PropertyField (list.FindPropertyRelative ("gamepadNumber"));
        EditorGUILayout.PropertyField (list.FindPropertyRelative ("gamepadName"));
    }

    void showNewGamepadList (SerializedProperty list)
    {
        EditorGUILayout.BeginVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide Gamepad List", buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {

            int currentArraySize = list.arraySize;

            EditorGUILayout.Space ();

            GUILayout.Label ("Number Of Gamepads: \t" + currentArraySize);

            EditorGUILayout.Space ();

            EditorGUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Expand All")) {
                for (int i = 0; i < currentArraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = true;
                }
            }
            if (GUILayout.Button ("Collapse All")) {
                for (int i = 0; i < currentArraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = false;
                }
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            if (GUILayout.Button ("Clear & Obtain Gamepads connected")) {
                if (Application.isPlaying) {
                    manager.clearAndObtainGamepadsConnected ();
                }
            }

            EditorGUILayout.Space ();

            for (int i = 0; i < currentArraySize; i++) {
                EditorGUILayout.BeginHorizontal ();
                EditorGUILayout.BeginHorizontal ("box");

                EditorGUILayout.Space ();

                if (i < currentArraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();

                    SerializedProperty currentArrayElement = list.GetArrayElementAtIndex (i);

                    EditorGUILayout.PropertyField (currentArrayElement, false);

                    if (currentArrayElement.isExpanded) {
                        showNewGamepadListElement (currentArrayElement);
                    }

                    EditorGUILayout.Space ();

                    EditorGUILayout.EndVertical ();
                }
                EditorGUILayout.EndHorizontal ();

                EditorGUILayout.EndHorizontal ();
            }
        }
        EditorGUILayout.EndVertical ();
    }

    void showNewGamepadListElement (SerializedProperty list)
    {
        EditorGUILayout.PropertyField (list.FindPropertyRelative ("deviceName"));
        EditorGUILayout.PropertyField (list.FindPropertyRelative ("playerIndex"));

        EditorGUILayout.PropertyField (list.FindPropertyRelative ("connected"));

        EditorGUILayout.Space ();

        EditorGUILayout.PropertyField (list.FindPropertyRelative ("deviceId"));

        EditorGUILayout.PropertyField (list.FindPropertyRelative ("deviceKey"));

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Remove Gamepad")) {
            if (Application.isPlaying) {
                manager.removeGamepadByID (list.FindPropertyRelative ("playerIndex").intValue);
            }
        }
    }
}
#endif