using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class adjustCharacterHeightSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool adjustCharacterHeight;
    public float characterHeight = 2;

    public bool setCharacterModelScale;
    public float characterModelScale = 1;

    public bool resetMountPointLocalPositions;

    [Space]

    public float capsuleHeight = 2;
    public float capsuleRadius = 0.3f;
    public float capsuleHeightOnCrouch = 1;

    [Space]

    public float characterRadius;

    [Space]

    public bool enableHeadTrack;

    [Space]
    [Header ("Camera Settings")]
    [Space]

    public List<cameraStateHeightInfo> cameraStateHeightInfoList = new List<cameraStateHeightInfo> ();

    [Space]
    [Header ("Character Transform Parts Settings")]
    [Space]

    public List<transformPartInfo> transformPartInfoList = new List<transformPartInfo> ();

    [Space]
    [Header ("Fire Weapons Settings")]
    [Space]

    public bool applyScaleToFireWeapons;
    public float scaleForFireWeapons;

    public Vector3 offsetParentThirdPerson;

    [Space]

    public bool setFixedOffsetParentThirdPerson;
    public Vector3 fixedOffsetParentThirdPerson;

    [Space]

    public float fireWeaponTransformReferenceScale = 1;

    [Space]
    [Header ("Melee Weapons Settings")]
    [Space]

    public bool applyScaleToMeleeWapons;
    public float scaleForMeleeWeapons;

    public bool placeTriggerInFrontOfCharacterOnAllMeleeAttacks;

    [Space]

    public bool useExtraTriggerMultiplierScaleOnAllMeleeAttacksActive;
    public float extraTriggerMultiplierScaleOnAllMeleeAttacksAmount;

    [Space]
    [Header ("Close Combat Settings")]
    [Space]

    public bool setNewCloseCombatTriggerRadius;
    public float newCloseCombatTriggerRadius;

    public bool placeTriggerInFrontOfCharacterOnAllCloseCombatAttacks;

    [Space]
    [Header ("AI Settings")]
    [Space]

    public bool characterIsAI;
    public bool characterUsesExtraColliders;
    public List<Collider> extraColliderList = new List<Collider> ();

    [Space]

    public bool useNewMinDistanceToTarget;
    public float newMinDistanceToEnemyUsingCloseCombat;
    public float newMinDistanceToCloseCombat;

    [Space]

    public float newMinDistanceToMelee;

    public float newMinDistanceToEnemyUsingMelee;

    [Space]

    public bool setNewAgentRadius;
    public float newAgentRadius;

    [Space]
    [Header ("Components")]
    [Space]

    public playerComponentsManager mainPlayerComponentsManager;
    public playerController mainPlayerController;
    public playerCamera mainPlayerCamera;
    public playerWeaponsManager mainPlayerWeaponsManager;
    public meleeWeaponsGrabbedManager mainMeleeWeaponsGrabbedManager;
    public bodyMountPointsSystem mainBodyMountPointSystem;
    public buildPlayer mainBuildPlayer;
    public headTrack mainHeadTrack;
    public IKSystem mainIKSystem;
    public health mainHealth;
    public closeCombatSystem mainCloseCombatSystem;

    public findObjectivesSystem mainFindObjectivesSystem;
    public AINavMesh mainAINavmesh;

    bool adjustingHeighValueOnRuntimeActive;

    bool applicationisPlaying;


    void Start ()
    {
        if (characterUsesExtraColliders) {
            int extraColliderListCount = extraColliderList.Count;

            for (int i = 0; i < extraColliderListCount; i++) {
                mainPlayerController.setIgnoreCollisionOnExternalCollider (extraColliderList [i], true);
            }
        }
    }

    public void adjustHeightValueInRuntime ()
    {
        updateApplicationisPlayingValue ();

        adjustingHeighValueOnRuntimeActive = true;

        adjustHeightValue ();

        adjustCameraStates ();

        adjustFireWeaponsScale ();

        if (setFixedOffsetParentThirdPerson) {
            Vector3 previousOffsetParentThirdPerson = offsetParentThirdPerson;

            offsetParentThirdPerson = fixedOffsetParentThirdPerson;

            adjustFireWeaponsOffset ();

            adjustFireWeaponsTransformReferences ();

            offsetParentThirdPerson = previousOffsetParentThirdPerson;
        }

        adjustingHeighValueOnRuntimeActive = false;
    }


    public void adjustHeightValue ()
    {
        getComponents ();

        bool resetWeaponsTransformResult = false;

        if (setCharacterModelScale) {
            //unpack the character to avoid issues with the prefab
            if (!adjustingHeighValueOnRuntimeActive) {
                GKC_Utils.unpackPrefabObjectByCheckingTransformRoot (mainPlayerController.gameObject);
            }

            //put all the internal objects that are inside the body into a temporal transform
            mainBodyMountPointSystem.removeCharacterBodyMountPointsToTemporalParent (transform);

            Transform mainCOMTransform = mainIKSystem.getIKBodyCOM ();

            Transform characterCOMChild = null;

            if (mainCOMTransform != null && mainCOMTransform.childCount > 0) {
                characterCOMChild = mainCOMTransform.GetChild (0);
            }

            if (characterCOMChild != null) {
                characterCOMChild.localScale = Vector3.one * characterModelScale;
            }

            //put back all the mount points back to the character's body 
            mainBodyMountPointSystem.setCharacterBodyMountPointsInfoList ();

            mainPlayerController.setCharacterCustomScale (characterModelScale);

            resetWeaponsTransformResult = true;
        }

        if (adjustCharacterHeight) {
            resetWeaponsTransformResult = true;
        }

        if (resetMountPointLocalPositions) {
            mainBodyMountPointSystem.resetCharacterBodyMountPointsLocalPositions ();
        }

        adjustTransformParts ();

        if (resetWeaponsTransformResult) {
            mainPlayerWeaponsManager.resetWeaponsParentToPlayerPosition ();
            mainPlayerWeaponsManager.resetWeaponsTransformInThirdPersonToPlayerPosition ();
        }

        mainPlayerController.setPlayerCapsuleColliderRadius (capsuleRadius);

        mainPlayerController.setPlayerColliderCapsuleScaleWithCenterAsRadius (capsuleHeight);

        mainPlayerController.setCapsuleHeightOnCrouch (capsuleHeightOnCrouch);

        mainPlayerController.setCharacterRadius (characterRadius);

        if (enableHeadTrack) {
            mainHeadTrack.setHeadTrackEnabledState (true);
        }

        if (characterIsAI) {
            if (setNewAgentRadius) {
                NavMeshAgent currenAgent = mainAINavmesh.getAgent ();

                currenAgent.radius = newAgentRadius;

                currenAgent.height = characterHeight;
            }
        }

        if (characterUsesExtraColliders) {
            int extraColliderListCount = extraColliderList.Count;

            int characterLayer = mainPlayerController.getCharacterLayer ();

            for (int i = 0; i < extraColliderListCount; i++) {
                mainPlayerController.addOrRemoveExtraColliders (extraColliderList [i], true);

                extraColliderList [i].gameObject.layer = characterLayer;

                mainCloseCombatSystem.addOrRemoveObjectToIgnoreByHitTriggers (extraColliderList [i].gameObject, true);

                characterDamageReceiver currentDamageReceiverToCheck = extraColliderList [i].GetComponent<characterDamageReceiver> ();

                if (currentDamageReceiverToCheck != null) {
                    currentDamageReceiverToCheck.setCharacter (mainPlayerController.gameObject, mainHealth);

                    mainHealth.addDamageReceiverGameObjectList (currentDamageReceiverToCheck.gameObject);
                }
            }

            if (adjustingHeighValueOnRuntimeActive) {
                for (int i = 0; i < extraColliderListCount; i++) {
                    mainPlayerController.setIgnoreCollisionOnExternalCollider (extraColliderList [i], true);
                }
            }

            mainAINavmesh.setCheckForExtraCollidersOnOwnerIfDetectedStateFromEditor (true);

            mainFindObjectivesSystem.setCheckForExtraCollidersOnOwnerIfDetectedStateFromEditor (true);
        }

        if (setNewCloseCombatTriggerRadius) {
            mainCloseCombatSystem.updateHitCombatTriggerRadius (newCloseCombatTriggerRadius);
        }

        if (useNewMinDistanceToTarget) {
            if (newMinDistanceToEnemyUsingCloseCombat > 0) {
                mainFindObjectivesSystem.setNewMinDistanceToEnemyUsingCloseCombat (newMinDistanceToEnemyUsingCloseCombat);
            }

            if (newMinDistanceToCloseCombat > 0) {
                mainFindObjectivesSystem.setNewMinDistanceToCloseCombat (newMinDistanceToCloseCombat);
            }

            if (newMinDistanceToMelee > 0) {
                mainFindObjectivesSystem.setNewMinDistanceToMelee (newMinDistanceToMelee);
            }

            if (newMinDistanceToEnemyUsingMelee > 0) {
                mainFindObjectivesSystem.setMinDistanceToEnemyUsingMelee (newMinDistanceToEnemyUsingMelee);
            }
        }

        mainHealth.setSliderOffset (characterHeight + 0.5f);

        if (applyScaleToMeleeWapons) {
            mainMeleeWeaponsGrabbedManager.setWeaponMeshScaleOnAllWeapons (scaleForMeleeWeapons);
        }

        if (placeTriggerInFrontOfCharacterOnAllMeleeAttacks) {
            mainMeleeWeaponsGrabbedManager.setPlaceTriggerInFrontOfCharacterOnAllAttacksState (true);
        }

        if (placeTriggerInFrontOfCharacterOnAllCloseCombatAttacks) {
            mainCloseCombatSystem.setPlaceTriggerInFrontOfCharacterOnAllAttacksState (true);
        }

        if (useExtraTriggerMultiplierScaleOnAllMeleeAttacksActive) {
            mainMeleeWeaponsGrabbedManager.setExtraTriggerMultiplierScaleOnAllMeleeAttacksActiveState (true, extraTriggerMultiplierScaleOnAllMeleeAttacksAmount);
        }

        if (!adjustingHeighValueOnRuntimeActive) {
            updateComponent ();

            print ("Height values adjusted on Character " + gameObject.name);
        }
    }

    void adjustTransformParts ()
    {
        for (int i = 0; i < transformPartInfoList.Count; i++) {

            transformPartInfo currentPart = transformPartInfoList [i];

            Vector3 newPosition = currentPart.transformPart.position;

            float newYValue = (characterHeight * currentPart.defaultHeightPercentage) / 100;

            newPosition = new Vector3 (newPosition.x, newYValue, newPosition.z);

            currentPart.transformPart.position = newPosition;
        }
    }

    public void adjustFireWeaponsScale ()
    {
        if (applyScaleToFireWeapons) {
            mainPlayerWeaponsManager.setWeaponMeshScaleOnAllWeapons (scaleForFireWeapons);

            if (!adjustingHeighValueOnRuntimeActive) {
                updateComponent ();
            }
        }
    }

    public void adjustFireWeaponsOffset ()
    {
        mainPlayerWeaponsManager.setCurrentOffsetParentThirdPersonValues (offsetParentThirdPerson);

        mainPlayerWeaponsManager.setCurrentOffsetParentOnThirdPersonOnAllWeapons ();

        mainPlayerWeaponsManager.adjustDrawKeepPositionToWeaponPositionOnAllWeapons ();

        if (!adjustingHeighValueOnRuntimeActive) {
            updateComponent ();
        }
    }

    public void adjustFireWeaponsTransformReferences ()
    {
        mainPlayerWeaponsManager.adjustAllFireWeaponTransformReferencePositionOnAllWeapons (fireWeaponTransformReferenceScale);

        if (!adjustingHeighValueOnRuntimeActive) {
            updateComponent ();
        }
    }

    public void adjustCameraStates ()
    {
        int cameraIndex = 0;

        for (int i = 0; i < cameraStateHeightInfoList.Count; i++) {

            cameraStateHeightInfo currentState = cameraStateHeightInfoList [i];

            cameraIndex = mainPlayerCamera.playerCameraStates.FindIndex (s => s.Name.Equals (currentState.cameraStateName));

            if (cameraIndex > -1) {
                float newYValue = (characterHeight * currentState.defaultPivotHeightPercentage) / 100;

                mainPlayerCamera.playerCameraStates [cameraIndex].pivotPositionOffset.y = newYValue;

                if (adjustingHeighValueOnRuntimeActive) {
                    mainPlayerCamera.updateCameraStateValuesOnEditor (cameraIndex);
                }

                if (!adjustingHeighValueOnRuntimeActive) {
                    print ("Camera State " + currentState.cameraStateName + " update with " + newYValue);
                }
            }
        }

        cameraIndex = mainPlayerCamera.playerCameraStates.FindIndex (s => s.Name.Equals (mainPlayerCamera.getCurrentStateName ()));

        if (cameraIndex > -1) {
            mainPlayerCamera.getPivotCameraTransform ().localPosition = mainPlayerCamera.playerCameraStates [cameraIndex].pivotPositionOffset;

            if (adjustingHeighValueOnRuntimeActive) {
                mainPlayerCamera.updateCameraStateValuesOnEditor (cameraIndex);
            }
        }

        if (!adjustingHeighValueOnRuntimeActive) {
            updateComponent ();
        }
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Character Height/Scale", gameObject);
    }

    void getComponents ()
    {
        if (mainPlayerComponentsManager != null) {
            if (mainPlayerController == null) {
                mainPlayerController = mainPlayerComponentsManager.getPlayerController ();
            }

            if (mainPlayerCamera == null) {
                mainPlayerCamera = mainPlayerComponentsManager.getPlayerCamera ();
            }

            if (mainPlayerWeaponsManager == null) {
                mainPlayerWeaponsManager = mainPlayerComponentsManager.getPlayerWeaponsManager ();
            }

            if (mainBodyMountPointSystem == null) {
                mainBodyMountPointSystem = mainPlayerComponentsManager.getBodyMountPointsSystem ();
            }

            if (mainBuildPlayer == null) {
                mainBuildPlayer = mainPlayerComponentsManager.getBuildPlayer ();
            }

            if (mainHeadTrack == null) {
                mainHeadTrack = mainPlayerComponentsManager.getHeadTrack ();
            }

            if (mainIKSystem == null) {
                mainIKSystem = mainPlayerComponentsManager.getIKSystem ();
            }

            if (mainHealth == null) {
                mainHealth = mainPlayerComponentsManager.getHealth ();
            }

            if (mainCloseCombatSystem == null) {
                mainCloseCombatSystem = mainPlayerComponentsManager.getCloseCombatSystem ();
            }

            if (mainFindObjectivesSystem == null) {
                mainFindObjectivesSystem = mainPlayerComponentsManager.getFindObjectivesSystem ();
            }

            if (mainAINavmesh == null) {
                mainAINavmesh = mainFindObjectivesSystem.getAINavMesh ();
            }

            if (mainMeleeWeaponsGrabbedManager == null) {
                mainMeleeWeaponsGrabbedManager = mainPlayerComponentsManager.getMeleeWeaponsGrabbedManager ();
            }
        }
    }

    void updateApplicationisPlayingValue ()
    {
        applicationisPlaying = Application.isPlaying;
    }

    [System.Serializable]
    public class transformPartInfo
    {
        public Transform transformPart;

        public float defaultHeightPercentage;

    }

    [System.Serializable]
    public class cameraStateHeightInfo
    {
        public string cameraStateName;

        public float defaultPivotHeightPercentage;
    }
}
