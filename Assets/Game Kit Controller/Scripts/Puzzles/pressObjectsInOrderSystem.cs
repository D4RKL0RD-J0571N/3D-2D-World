using GameKitController.Audio;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using static InputAxisWrapper;

public class pressObjectsInOrderSystem : MonoBehaviour
{
    public List<positionInfo> positionInfoList = new List<positionInfo> ();

    public bool allPositionsPressed;
    public int correctPressedIndex;
    public bool useIncorrectOrderSound;
    public AudioClip incorrectOrderSound;
    public AudioElement incorrectOrderAudioElement;

    public bool usingPressedObjectSystem;
    public LayerMask pressObjectsLayer;
    public bool pressObjectsWhileMousePressed;

    public UnityEvent allPositionsPressedEvent;

    public Camera mainCamera;
    public AudioSource mainAudioSource;

    public GameObject currentPlayer;

    playerInputManager playerInput;

    playerComponentsManager mainPlayerComponentsManager;


    InputAxisWrapper.TouchPressType touchPressType;

    bool useNewInputSystem;

    Vector2 currentTouchPosition;


    bool touchPlatform;
    Touch currentTouch;
    bool touching;
    GameObject currentObjectPressed;
    GameObject previousObjectPressed;
    RaycastHit hit;

    bool mainCameraLocated;

    Ray ray;

    Coroutine updateCoroutine;


    private void Start ()
    {
        if (mainAudioSource == null) {
            mainAudioSource = GetComponent<AudioSource> ();
        }

        if (mainAudioSource != null) {
            incorrectOrderAudioElement.audioSource = mainAudioSource;
        }

        if (incorrectOrderSound != null) {
            incorrectOrderAudioElement.clip = incorrectOrderSound;
        }
    }

    IEnumerator mainUpdateCoroutine ()
    {
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            updateState ();

            yield return waitTime;
        }
    }

    void updateState ()
    {
        if (usingPressedObjectSystem && pressObjectsWhileMousePressed && mainCameraLocated) {
            //meter lo de touch aqui para llamar mientras se tenga pulsado el raton, distinguiendo que sea un nuevo objeto

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

                if (touchPressType == InputAxisWrapper.TouchPressType.Began) {
                    touching = true;

                    ray = mainCamera.ScreenPointToRay (currentTouchPosition);

                    if (Physics.Raycast (ray, out hit, Mathf.Infinity, pressObjectsLayer)) {
                        currentObjectPressed = hit.collider.gameObject;

                        if (currentObjectPressed != previousObjectPressed) {
                            previousObjectPressed = currentObjectPressed;
                        }
                    }
                }

                if ((touchPressType == InputAxisWrapper.TouchPressType.Stationary || touchPressType == InputAxisWrapper.TouchPressType.Moved) && touching) {
                    ray = mainCamera.ScreenPointToRay (currentTouchPosition);

                    if (Physics.Raycast (ray, out hit, Mathf.Infinity, pressObjectsLayer)) {
                        currentObjectPressed = hit.collider.gameObject;

                        if (currentObjectPressed != previousObjectPressed) {
                            previousObjectPressed = currentObjectPressed;
                            EventTrigger currentEventTrigger = currentObjectPressed.GetComponent<EventTrigger> ();

                            if (currentEventTrigger != null) {
                                var pointer = new PointerEventData (EventSystem.current);

                                ExecuteEvents.Execute (currentObjectPressed, pointer, ExecuteEvents.pointerEnterHandler);
                                ExecuteEvents.Execute (currentObjectPressed, pointer, ExecuteEvents.pointerDownHandler);
                            }
                        }
                    }
                }

                if (touchPressType == InputAxisWrapper.TouchPressType.Ended) {
                    touching = false;
                }
            }
        }
    }

    public void checkPressedPosition (string positionName)
    {
        if (!usingPressedObjectSystem) {
            return;
        }

        if (allPositionsPressed) {
            return;
        }

        if (correctPressedIndex < positionInfoList.Count) {
            positionInfo currentPosition = positionInfoList [correctPressedIndex];

            if (currentPosition.positionName.Equals (positionName)) {
                currentPosition.positionActive = true;

                if (currentPosition.usePositionEvent) {
                    if (currentPosition.positionEvent.GetPersistentEventCount () > 0) {
                        currentPosition.positionEvent.Invoke ();
                    }
                }

                correctPressedIndex++;

                if (correctPressedIndex == positionInfoList.Count) {
                    if (allPositionsPressedEvent.GetPersistentEventCount () > 0) {
                        allPositionsPressedEvent.Invoke ();
                    }

                    allPositionsPressed = true;
                }
            } else {

                resetPressedObjects ();

                if (useIncorrectOrderSound) {
                    if (incorrectOrderAudioElement != null) {
                        AudioPlayer.PlayOneShot (incorrectOrderAudioElement, gameObject);
                    }
                }
            }
        }
    }

    public void resetPressedObjects ()
    {
        correctPressedIndex = 0;

        for (int i = 0; i < positionInfoList.Count; i++) {
            positionInfoList [i].positionActive = false;
        }
    }

    public void startOrStopPressedObjectSystem ()
    {
        setUsingPressedObjectSystemState (!usingPressedObjectSystem);
    }

    public void setUsingPressedObjectSystemState (bool state)
    {
        usingPressedObjectSystem = state;

        stopUpdateCoroutine ();

        if (usingPressedObjectSystem) {
            updateCoroutine = StartCoroutine (mainUpdateCoroutine ());

            touchPlatform = InputAxisWrapper.checkTouchPlatform ();

            useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();

            mainCameraLocated = mainCamera != null;

            if (!mainCameraLocated) {
                mainCamera = GKC_Utils.findMainPlayerCameraComponentOnScene ();

                mainCameraLocated = mainCamera != null;
            }
        }
    }

    public void setCurrentPlayer (GameObject newPlayer)
    {
        if (newPlayer == null) {
            return;
        }

        currentPlayer = newPlayer;

        mainPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

        if (mainPlayerComponentsManager != null) {

            playerInput = mainPlayerComponentsManager.getPlayerInputManager ();

            mainCamera = mainPlayerComponentsManager.getPlayerCamera ().getMainCamera ();

            mainCameraLocated = true;
        }
    }

    public void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }
    }

    [System.Serializable]
    public class positionInfo
    {
        public string positionName;
        public bool usePositionEvent;
        public UnityEvent positionEvent;
        public bool positionActive;
    }
}
