using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class characterModelListManager : MonoBehaviour
{
    [Header ("Main Setting")]
    [Space]

    public string currentCharacterModelName;

    [Space]

    public List<characterModelInfo> characterModelInfoList = new List<characterModelInfo> ();

    [Space]

    public bool setCharacterModelOnStart;

    public string characterModelOnStartName;

    public bool useRandomCharacterModelOnStart;

    [Space]

    public bool ignoreSetInitialArmorClothSetOnChangeModel;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public int currentCharacterModelIndex;

    public int currentCharacterModelID;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventOnReplaceCharacterModel;
    public UnityEvent eventOnReplaceCharacterModel;

    [Space]

    public bool useEventToSendCurrentCharacterModelSelected;
    public eventParameters.eventToCallWithGameObject eventToSendCurrentCharacterModelSelected;

    [Space]
    [Header ("Components")]
    [Space]

    public buildPlayer mainBuildPlayer;
    public bodyMountPointsSystem mainBodyMountPointsSystem;
    public RuntimeAnimatorController originalRuntimeAnimatorController;
    public playerController mainPlayerController;
    public playerComponentsManager mainPlayerComponentsManager;

    public ragdollBuilder mainRagdollBuilder;


    bool mainBuildPlayerLocated;

    int previousCharacterModelIndex = -1;

    void Start ()
    {
        if (setCharacterModelOnStart) {
            StartCoroutine (setCharacterModelOnStartCoroutine ());
        }
    }

    IEnumerator setCharacterModelOnStartCoroutine ()
    {
        yield return new WaitForSeconds (0.01f);

        string modelNameToUse = characterModelOnStartName;

        if (useRandomCharacterModelOnStart) {
            int randomIndex = Random.Range (0, characterModelInfoList.Count);

            modelNameToUse = characterModelInfoList [randomIndex].Name;
        }

        replaceCharacterModel (modelNameToUse);
    }

    public int getCurrentCharacterModelID ()
    {
        if (mainPlayerController != null) {
            return mainPlayerController.getCharacterModelID ();
        }

        return currentCharacterModelID;
    }

    public void replaceCharacterModelByID (int id)
    {
        int listIndex = characterModelInfoList.FindIndex (s => s.characterModelID.Equals (id));

        if (showDebugPrint) {
            print ("replaceCharacterModelByID " + id + " " + listIndex);
        }

        if (listIndex > -1) {
            replaceCharacterModel (characterModelInfoList [listIndex].Name);
        }
    }

    public void replaceCharacterModel ()
    {
        //add a check to see if the play is active or not, to allow the change also on editor time, out of play mode

        replaceCharacterModel (currentCharacterModelName);
    }

    public void replaceCharacterModel (string name)
    {
        if (name == null || name == "") {
            return;
        }

        int listIndex = characterModelInfoList.FindIndex (s => s.Name.Equals (name));

        if (showDebugPrint) {
            print ("checking character name " + name + " " + listIndex);
        }

        if (listIndex > -1) {
            if (currentCharacterModelIndex == listIndex) {
                print ("character model selected is already the current one, cancelling");

                return;
            }

            if (previousCharacterModelIndex > -1) {
                characterModelInfo previousCharacterModelInfo = characterModelInfoList [previousCharacterModelIndex];

                if (previousCharacterModelInfo.characterGameObject != null) {
                    if (previousCharacterModelInfo.useEventOnSelectCharacterModel) {
                        previousCharacterModelInfo.eventOnChangeToAnotherCharacterModel.Invoke ();
                    }
                }
            }

            previousCharacterModelIndex = currentCharacterModelIndex;

            currentCharacterModelIndex = listIndex;

            characterModelInfo currentCharacterModelInfo = characterModelInfoList [currentCharacterModelIndex];

            if (currentCharacterModelInfo.characterGameObject == null) {
                currentCharacterModelInfo.characterGameObject = (GameObject)Instantiate (currentCharacterModelInfo.characterPrefab);
            }

            characterModelElementInfo currentCharacterModelElementInfo = currentCharacterModelInfo.characterGameObject.GetComponent<characterModelElementInfo> ();

            if (currentCharacterModelElementInfo == null) {
                return;
            }

            if (!currentCharacterModelInfo.characterGameObject.activeSelf) {
                currentCharacterModelInfo.characterGameObject.SetActive (true);
            }

            currentCharacterModelID = currentCharacterModelInfo.characterModelID;

            currentCharacterModelElementInfo.mainAnimator.enabled = true;


            mainBuildPlayerLocated = mainBuildPlayer != null;

            bool applicationisPlaying = Application.isPlaying;

            if (mainBuildPlayerLocated) {
                inventoryCharacterCustomizationSystem currentInventoryCharacterCustomizationSystem = mainPlayerComponentsManager.getInventoryCharacterCustomizationSystem ();

                if (currentInventoryCharacterCustomizationSystem != null) {
                    if (applicationisPlaying) {
                        currentInventoryCharacterCustomizationSystem.unequipAllCurrentArmorCloth ();
                    }
                }

                mainPlayerController.setCharacterModelID (currentCharacterModelInfo.characterModelID);

                if (currentCharacterModelElementInfo.setNewAnimator) {
                    mainPlayerController.setNewRuntimeAnimatorController (currentCharacterModelElementInfo.newRuntimeAnimatorController);
                } else {
                    if (originalRuntimeAnimatorController != null) {
                        mainPlayerController.setNewRuntimeAnimatorController (originalRuntimeAnimatorController);
                    }
                }

                mainRagdollBuilder.setCheckIfRagdollComponentsAlreadyAddedOnModelActiveState (true);

                if (currentCharacterModelElementInfo.setRagdollBonesManually) {
                    mainRagdollBuilder.setIgnoreStoreBonesByGetBoneTransformActiveState (true);

                    mainRagdollBuilder.setCharacterBones (currentCharacterModelElementInfo.head,
                        currentCharacterModelElementInfo.rightLowerArm,
                        currentCharacterModelElementInfo.leftLowerArm,
                        currentCharacterModelElementInfo.rightUpperArm,
                        currentCharacterModelElementInfo.leftUpperArm,
                        currentCharacterModelElementInfo.middleSpine,
                        currentCharacterModelElementInfo.pelvis,
                        currentCharacterModelElementInfo.rightUpperLeg,
                        currentCharacterModelElementInfo.leftUpperLeg,
                        currentCharacterModelElementInfo.rightLowerLeg,
                        currentCharacterModelElementInfo.leftLowerLeg);
                }

                mainBuildPlayer.checkElementsBeforeReplacingCharacterModelInRuntime ();

                mainBuildPlayer.setNewCharacterModelToReplaceInRuntime (currentCharacterModelInfo.characterGameObject);

                mainBuildPlayer.setCharacterBones (
                                    currentCharacterModelElementInfo.head,
                                    currentCharacterModelElementInfo.neck,
                                    currentCharacterModelElementInfo.chest,
                                    currentCharacterModelElementInfo.spine,

                                    currentCharacterModelElementInfo.hips,

                                    currentCharacterModelElementInfo.rightLowerArm,
                                    currentCharacterModelElementInfo.leftLowerArm,
                                    currentCharacterModelElementInfo.rightHand,
                                    currentCharacterModelElementInfo.leftHand,

                                    currentCharacterModelElementInfo.rightLowerLeg,
                                    currentCharacterModelElementInfo.leftLowerLeg,
                                    currentCharacterModelElementInfo.rightFoot,
                                    currentCharacterModelElementInfo.leftFoot,
                                    currentCharacterModelElementInfo.rightToes,
                                    currentCharacterModelElementInfo.leftToes);

                if (currentCharacterModelElementInfo.setNewChestUpVectorValueOnRuntime) {
                    mainBuildPlayer.setCustomChestUpVectorValue (currentCharacterModelElementInfo.chestUpVector);
                }

                mainBuildPlayer.buildCharacterInRuntime ();

                mainBuildPlayer.checkElementsAfterReplacingCharacterModelInRuntime ();


                if (currentCharacterModelElementInfo.setNewHeightOnCharacter) {
                    if (currentCharacterModelInfo.mainAdjustCharacterHeightSystem != null) {
                        currentCharacterModelInfo.mainAdjustCharacterHeightSystem.adjustHeightValueInRuntime ();
                    }
                }


                if (currentInventoryCharacterCustomizationSystem != null) {
                    currentInventoryCharacterCustomizationSystem.updateCharacterTransform ();

                    currentInventoryCharacterCustomizationSystem.updateCheckAssignCharacterAspectCustomizationUISystem ();

                    if (currentCharacterModelElementInfo.setNewArmorClothData) {
                        currentInventoryCharacterCustomizationSystem.setNewArmorClothPieceTemplateData (currentCharacterModelElementInfo.mainArmorClothPieceTemplateData);
                        currentInventoryCharacterCustomizationSystem.setNewFullArmorClothTemplateData (currentCharacterModelElementInfo.mainFullArmorClothTemplateData);
                    }

                    bool setNewArmorClothSet = currentCharacterModelElementInfo.setNewArmorClothSet;

                    if (ignoreSetInitialArmorClothSetOnChangeModel) {
                        currentInventoryCharacterCustomizationSystem.setInitialArmorClothPieceListState (false);
                    } else {
                        currentInventoryCharacterCustomizationSystem.setInitialArmorClothPieceListState (setNewArmorClothSet);
                    }

                    currentInventoryCharacterCustomizationSystem.setUseCharacterAspectCustomizationTemplateState (setNewArmorClothSet);

                    if (setNewArmorClothSet) {
                        currentInventoryCharacterCustomizationSystem.setInitialCharacterAspectCustomizationTemplate (
                            currentCharacterModelElementInfo.initialCharacterAspectCustomizationTemplate);

                        bool setRandomArmorClothSet = currentCharacterModelElementInfo.setRandomArmorClothSet;

                        currentInventoryCharacterCustomizationSystem.setUseRandomCharacterAspectCustomizationTemplateState (setRandomArmorClothSet);

                        if (setRandomArmorClothSet) {
                            currentInventoryCharacterCustomizationSystem.setCharacterAspectCustomizationTemplateList (
                                currentCharacterModelElementInfo.characterAspectCustomizationTemplateList);
                        }

                        if (applicationisPlaying) {
                            currentInventoryCharacterCustomizationSystem.checkSetInitialArmorClothList ();
                        }
                    }
                }

                mainRagdollBuilder.setCheckIfRagdollComponentsAlreadyAddedOnModelActiveState (false);

                mainRagdollBuilder.setIgnoreStoreBonesByGetBoneTransformActiveState (false);
            } else {
                for (int i = 0; i < characterModelInfoList.Count; i++) {
                    if (i != currentCharacterModelIndex) {
                        characterModelInfo characterInfo = characterModelInfoList [i];

                        if (characterInfo.characterGameObject != null) {
                            if (characterInfo.characterGameObject.activeSelf) {
                                characterInfo.characterGameObject.SetActive (false);
                            }
                        }
                    }
                }
            }

            if (useEventOnReplaceCharacterModel) {
                eventOnReplaceCharacterModel.Invoke ();
            }

            if (useEventToSendCurrentCharacterModelSelected) {
                eventToSendCurrentCharacterModelSelected.Invoke (currentCharacterModelInfo.characterGameObject);
            }

            if (showDebugPrint) {
                print ("selecting " + currentCharacterModelInfo.Name);
            }

            if (currentCharacterModelInfo.useEventOnSelectCharacterModel) {
                currentCharacterModelInfo.eventOnSelectCharacterModel.Invoke ();
            }
        }
    }

    public void setNextCharacterModel ()
    {
        setNextOrPreviousCharacterModel (true);
    }

    public void setPreviousCharacterModel ()
    {
        setNextOrPreviousCharacterModel (false);
    }

    public void setNextOrPreviousCharacterModel (bool state)
    {
        if (state) {
            currentCharacterModelIndex++;

            if (currentCharacterModelIndex >= characterModelInfoList.Count) {
                currentCharacterModelIndex = 0;
            }
        } else {
            currentCharacterModelIndex--;

            if (currentCharacterModelIndex < 0) {
                currentCharacterModelIndex = characterModelInfoList.Count - 1;
            }
        }

        string characterModelNameToUse = characterModelInfoList [currentCharacterModelIndex].Name;

        currentCharacterModelIndex = -1;

        replaceCharacterModel (characterModelNameToUse);
    }


    [System.Serializable]
    public class characterModelInfo
    {
        [Header ("Main Setting")]
        [Space]

        public string Name;

        public int characterModelID;

        [Space]
        [Header ("Components")]
        [Space]

        public GameObject characterPrefab;
        public GameObject characterGameObject;

        public adjustCharacterHeightSystem mainAdjustCharacterHeightSystem;

        [Space]
        [Header ("Event Settings")]
        [Space]

        public bool useEventOnSelectCharacterModel;
        public UnityEvent eventOnSelectCharacterModel;
        public UnityEvent eventOnChangeToAnotherCharacterModel;
    }
}
