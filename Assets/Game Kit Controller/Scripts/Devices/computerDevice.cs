using GameKitController.Audio;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static InputAxisWrapper;

public class computerDevice : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool locked;

    public string code;

    public Color unlockedColor;

    public int numberLetterToStartToMovePasswordText;
    public float passwordTextMoveDistacePerLetter;
    public bool allowToUseKeyboard;

    public bool disableUseDeviceActionButton;

    [Space]
    [Header ("Sound Settings")]
    [Space]

    public AudioClip wrongPassSound;
    public AudioElement wrongPassAudioElement;
    public AudioClip corretPassSound;
    public AudioElement corretPassAudioElement;
    public AudioClip keyPressSound;
    public AudioElement keyPressAudioElement;

    [Space]
    [Header ("Allowed Key List Settings")]
    [Space]

    public List<string> allowedKeysList = new List<string> ();

    [Space]
    [Header ("Text Settings")]
    [Space]

    public string passwordText = "Password";
    public string unlockedText = "Unlocked";

    public string leftShiftKey = "leftshift";

    public string numberKey = "alpha";
    public string spaceKey = "space";
    public string deleteKey = "delete";
    public string backSpaceKey = "backspace";
    public string enterKey = "enter";
    public string returnKey = "return";
    public string capsLockKey = "capslock";


    [Space]
    [Header ("Debug Settings")]
    [Space]

    public bool showDebugPrint;
    public bool usingDevice;
    public bool printKeysPressed;

    public List<Image> keysList = new List<Image> ();

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventOnComputerUnlocked;
    public UnityEvent eventOnComputerUnlocked;

    [Space]

    public bool useEventOnUsingDevice;
    public UnityEvent eventOnStartUsingDevice;
    public UnityEvent eventOnStopUsingDevice;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject keyboard;
    public Text currentCode;
    public Text stateText;
    public GameObject computerLockedContent;
    public GameObject computerUnlockedContent;
    public Image wallPaper;

    public AudioSource audioSource;
    public electronicDevice deviceManager;

    public inputManager input;


    readonly List<RaycastResult> captureRaycastResults = new List<RaycastResult> ();
    int totalKeysPressed = 0;
    bool changeScreen;
    bool unlocked;
    bool changedColor;

    bool touchPlatform;

    bool useNewInputSystem;

    InputAxisWrapper.TouchPressType touchPressType;

    Vector2 currentTouchPosition;


    GameObject currentCaptured;
    Touch currentTouch;

    string currentKeyPressed;
    bool letterAdded;
    Vector3 originalCurrentCodePosition;
    string originalKeyPressed;
    bool capsLockActivated;

    GameObject currentPlayer;
    usingDevicesSystem usingDevicesManager;
    playerInputManager playerInput;

    float lastTimeComputerActive;

    RectTransform currentCodeRectTransform;

    Coroutine mainCoroutine;



    private void InitializeAudioElements ()
    {
        if (audioSource == null) {
            audioSource = GetComponent<AudioSource> ();
        }

        if (audioSource != null) {
            wrongPassAudioElement.audioSource = audioSource;
            corretPassAudioElement.audioSource = audioSource;
            keyPressAudioElement.audioSource = audioSource;
        }

        if (wrongPassSound != null) {
            wrongPassAudioElement.clip = wrongPassSound;
        }

        if (corretPassSound != null) {
            corretPassAudioElement.clip = corretPassSound;
        }

        if (keyPressSound != null) {
            keyPressAudioElement.clip = keyPressSound;
        }
    }

    void Start ()
    {
        //get all the keys button in the keyboard and store them in a list
        if (keyboard != null) {
            Component [] components = keyboard.GetComponentsInChildren (typeof (Image));
            foreach (Image child in components) {
                keysList.Add (child);
            }
        }

        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();


        if (!locked) {
            if (computerLockedContent != null) {
                computerLockedContent.SetActive (false);
            }

            if (computerUnlockedContent != null) {
                computerUnlockedContent.SetActive (true);
            }
        }

        InitializeAudioElements ();

        if (deviceManager == null) {
            deviceManager = GetComponent<electronicDevice> ();
        }

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

        if (currentCode != null) {
            currentCodeRectTransform = currentCode.GetComponent<RectTransform> ();

            originalCurrentCodePosition = currentCodeRectTransform.localPosition;
        }
    }

    public void stopUpdateCoroutine ()
    {
        if (mainCoroutine != null) {
            StopCoroutine (mainCoroutine);
        }
    }

    IEnumerator updateCoroutine ()
    {
        //		var waitTime = new WaitForFixedUpdate ();

        var waitTime = new WaitForSecondsRealtime (0.00001f);

        while (true) {
            updateState ();

            yield return waitTime;
        }
    }

    void updateState ()
    {
        //if the computer is locked and the player is inside its trigger 
        if (!unlocked && usingDevice && locked) {
            //get all the input touchs, including the mouse
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

                //get a list with all the objects under mouse or the finger tap
                captureRaycastResults.Clear ();

                PointerEventData p = new PointerEventData (EventSystem.current);
                p.position = currentTouchPosition;
                p.clickCount = i;
                p.dragging = false;
                EventSystem.current.RaycastAll (p, captureRaycastResults);

                foreach (RaycastResult r in captureRaycastResults) {
                    currentCaptured = r.gameObject;

                    //check the current key pressed with the finger
                    if (touchPressType == InputAxisWrapper.TouchPressType.Began) {
                        checkButton (currentCaptured);
                    }
                }
            }

            if (Time.time > lastTimeComputerActive + 0.3f) {
                if (allowToUseKeyboard) {
                    currentKeyPressed = input.getKeyPressed (inputManager.buttonType.getKeyDown, false);

                    if (currentKeyPressed != "") {
                        checkKeyPressed (currentKeyPressed);
                    }

                    currentKeyPressed = input.getKeyPressed (inputManager.buttonType.getKeyUp, false);

                    if (currentKeyPressed.ToLower () == leftShiftKey) {
                        capsLockActivated = !capsLockActivated;
                    }
                }
            }
        }

        //if the device is unlocked, change the color of the interface for the unlocked color
        if (unlocked) {
            if (!changedColor) {
                wallPaper.color = Vector4.MoveTowards (wallPaper.color, unlockedColor, Time.deltaTime * 3);
                stateText.color = Vector4.MoveTowards (stateText.color, unlockedColor, Time.deltaTime * 3);

                if (wallPaper.color == unlockedColor && stateText.color == unlockedColor) {
                    changedColor = true;
                }
            } else {
                //change the password screen for the unlocked screen
                if (changeScreen) {
                    computerLockedContent.SetActive (false);
                    computerUnlockedContent.SetActive (true);
                    changeScreen = false;
                }
            }
        }
    }

    void checkEventsOnUsingDevice (bool state)
    {
        if (useEventOnUsingDevice) {
            if (state) {
                eventOnStartUsingDevice.Invoke ();
            } else {
                eventOnStopUsingDevice.Invoke ();
            }
        }
    }

    //activate the device
    public void activateComputer ()
    {
        usingDevice = !usingDevice;

        stopUpdateCoroutine ();

        if (showDebugPrint) {
            print ("Setting using computer state " + usingDevice);
        }

        if (usingDevice) {
            getCurrentUser ();
        }

        if (disableUseDeviceActionButton) {
            if (currentPlayer != null) {
                usingDevicesManager.setUseDeviceButtonEnabledState (!usingDevice);
            }
        }

        if (usingDevice) {
            lastTimeComputerActive = Time.time;

            mainCoroutine = StartCoroutine (updateCoroutine ());
        }

        checkEventsOnUsingDevice (usingDevice);
    }

    void getCurrentUser ()
    {
        currentPlayer = deviceManager.getCurrentPlayer ();

        if (currentPlayer != null) {
            playerComponentsManager currentPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

            if (currentPlayerComponentsManager != null) {

                usingDevicesManager = currentPlayer.GetComponent<usingDevicesSystem> ();

                playerInput = currentPlayerComponentsManager.getPlayerInputManager ();
            }
        }
    }

    //the currentCaptured is checked, to write the value of the key in the screen device
    void checkButton (GameObject button)
    {
        Image currentImage = button.GetComponent<Image> ();

        if (currentImage != null) {
            //check if the currentCaptured is a key number
            if (keysList.Contains (currentImage)) {
                checkKeyPressed (button.name);
            }
        }
    }

    public void checkKeyPressed (string keyPressed)
    {
        originalKeyPressed = keyPressed;

        keyPressed = keyPressed.ToLower ();

        if (!allowedKeysList.Contains (keyPressed)) {
            return;
        }

        bool checkPass = false;

        letterAdded = true;

        if (printKeysPressed) {
            if (showDebugPrint) {
                print (keyPressed);
            }
        }

        //reset the password in the screen
        if (totalKeysPressed == 0) {
            currentCode.text = "";
        }

        if (keyPressed.Contains (numberKey)) {
            if (showDebugPrint) {
                print (keyPressed);
            }

            keyPressed = keyPressed.Substring (keyPressed.Length - 1);
            originalKeyPressed = keyPressed;

            if (showDebugPrint) {
                print (keyPressed);
            }
        }

        //add the an space
        if (keyPressed == spaceKey) {
            currentCode.text += " ";
        }

        //delete the last character
        else if (keyPressed == deleteKey || keyPressed == backSpaceKey) {
            if (currentCode.text.Length > 0) {
                currentCode.text = currentCode.text.Remove (currentCode.text.Length - 1);

                letterAdded = false;
            }
        }

        //check the current word added
        else if (keyPressed == enterKey || keyPressed == returnKey) {
            checkPass = true;
        }

        //check if the caps are being using
        else if (keyPressed == capsLockKey || keyPressed == leftShiftKey) {
            capsLockActivated = !capsLockActivated;

            return;
        }

        //add the current key pressed to the password
        else {
            if (capsLockActivated) {
                originalKeyPressed = originalKeyPressed.ToUpper ();
            } else {
                originalKeyPressed = originalKeyPressed.ToLower ();
            }

            currentCode.text += originalKeyPressed;
        }

        totalKeysPressed = currentCode.text.Length;

        //play the key press sound
        playSound (keyPressAudioElement);

        //the enter key has been pressed, so check if the current text written is the correct password
        if (checkPass) {
            if (currentCode.text == code) {
                enableAccessToCompturer ();
            }
            //else, reset the terminal, and try again
            else {
                playSound (wrongPassAudioElement);

                currentCode.text = passwordText;

                totalKeysPressed = 0;

                currentCodeRectTransform.localPosition = originalCurrentCodePosition;
            }
        } else if (totalKeysPressed > numberLetterToStartToMovePasswordText) {
            if (letterAdded) {
                currentCodeRectTransform.localPosition -= new Vector3 (passwordTextMoveDistacePerLetter, 0, 0);
            } else {
                currentCodeRectTransform.localPosition += new Vector3 (passwordTextMoveDistacePerLetter, 0, 0);
            }
        }
    }

    //if the object to unlock is this device, change the screen
    public void unlockComputer ()
    {
        changeScreen = true;
    }

    public void enableAccessToCompturer ()
    {
        //if it is equal, then call the object to unlock and play the corret pass sound
        playSound (corretPassAudioElement);

        stateText.text = unlockedText;

        unlocked = true;

        //the object to unlock can be also this terminal, to see more content inside it
        deviceManager.unlockObject ();

        if (useEventOnComputerUnlocked) {
            eventOnComputerUnlocked.Invoke ();
        }
    }

    public void unlockComputerWithUsb ()
    {
        unlockComputer ();

        enableAccessToCompturer ();
    }

    public void playSound (AudioElement clipToPlay)
    {
        if (clipToPlay != null) {
            AudioPlayer.PlayOneShot (clipToPlay, gameObject);
        }
    }
}