using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class switchCompanionSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool switchCompanionEnabled = true;

    public bool changeControlToNextPlayerOnDeath;

    public bool resurrectAllCharactersEnabled;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public UnityEvent eventToSetCharacterAsAI;

    [Space]

    public UnityEvent eventToSetCharacterAsPlayer;

    [Space]

    public UnityEvent eventToSetCharacterAsOnlyPlayerActive;

    [Space]

    public UnityEvent eventToSetCharacterAsPlayerNotActive;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController playerControllerManager;
    public playerCharactersManager mainPlayerCharactersManager;

    public friendListManager mainfriendListManager;


    bool mainPlayerCharactersManagerLocated;


    public void activateEventToSetCharacterAsAI ()
    {
        eventToSetCharacterAsAI.Invoke ();
    }

    public void activateEventToSetCharacterAsPlayer ()
    {
        eventToSetCharacterAsPlayer.Invoke ();
    }

    public void activateEventToSetCharacterAsOnlyPlayerActive ()
    {
        eventToSetCharacterAsOnlyPlayerActive.Invoke ();
    }

    public void activateEventToSetCharacterAsPlayerNotActive ()
    {
        eventToSetCharacterAsPlayerNotActive.Invoke ();
    }

    public void inputSwitchToNextCharacter ()
    {
        if (!switchCompanionEnabled) {
            return;
        }

        if (!playerControllerManager.isPlayerMenuActive () &&
            (!playerControllerManager.isUsingDevice () || playerControllerManager.isPlayerDriving ()) &&
            !playerControllerManager.isGamePaused () &&
            playerControllerManager.canPlayerMove ()) {

            checkMainPlayerCharactersManagerLocted ();

            if (mainPlayerCharactersManagerLocated) {
                mainPlayerCharactersManager.inputSetNextCharacterToControl ();
            }
        }
    }

    public void switchToNextCharacterOnDeath ()
    {
        if (!switchCompanionEnabled) {
            return;
        }

        if (!changeControlToNextPlayerOnDeath) {
            return;
        }

        if (showDebugPrint) {
            print ("checking switchToNextCharacterOnDeath");
        }

        if (playerControllerManager.checkIfPlayerDeadFromHealthComponent ()) {

            checkMainPlayerCharactersManagerLocted ();

            if (mainPlayerCharactersManagerLocated) {
                if (mainPlayerCharactersManager.isSwitchCompanionActive ()) {
                    if (mainPlayerCharactersManager.anyCharacterAlive ()) {
                        if (showDebugPrint) {
                            print ("more characters alive, changing to next");
                        }

                        mainPlayerCharactersManager.inputSetNextCharacterToControl ();
                    } else {
                        mainPlayerCharactersManager.setShowDeathMenuPausedStateOnPlayer (false, playerControllerManager.gameObject);

                        if (showDebugPrint) {
                            print ("all characters dead, allowing to show death menu");
                        }
                    }
                } else {
                    if (showDebugPrint) {
                        print ("switch companion not active");
                    }
                }
            }
        } else {
            if (showDebugPrint) {
                print ("current player is not dead");
            }
        }
    }

    public void checkResurrectCharacters ()
    {
        if (!switchCompanionEnabled) {
            return;
        }

        if (!resurrectAllCharactersEnabled) {
            return;
        }

        if (showDebugPrint) {
            print ("checking checkResurrectCharacters");
        }

        checkMainPlayerCharactersManagerLocted ();

        if (mainPlayerCharactersManagerLocated) {
            mainPlayerCharactersManager.resurrectRestOfCharacters (playerControllerManager.gameObject);
        }
    }

    void checkMainPlayerCharactersManagerLocted ()
    {
        if (mainPlayerCharactersManagerLocated) {
            return;
        }

        mainPlayerCharactersManagerLocated = mainPlayerCharactersManager != null;

        if (!mainPlayerCharactersManagerLocated) {
            mainPlayerCharactersManager = playerCharactersManager.Instance;
        }

        if (!mainPlayerCharactersManagerLocated) {
            mainPlayerCharactersManager = GKC_Utils.findObjectByComponent<playerCharactersManager> ();

            mainPlayerCharactersManager.getComponentInstanceOnApplicationPlaying ();

            mainPlayerCharactersManagerLocated = mainPlayerCharactersManager != null;
        }
    }
}
