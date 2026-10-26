using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

public class playerController : MonoBehaviour
{
    [Header ("Movement Settings")]
    [Space]

    [Tooltip ("Sets how high/far the player will jump under normal circumstances.")]
    public float jumpPower = 12;

    public float airSpeed = 6;
    public float airControl = 2;

    public bool ignorePlayerExtraRotationOnAirEnabled;

    public float stationaryTurnSpeed = 180;
    public float movingTurnSpeed = 200;

    public bool useTurnSpeedOnAim = true;

    public float autoTurnSpeed = 2;
    public float aimTurnSpeed = 10;

    public float thresholdAngleDifference = 5;

    public bool regularMovementOnBulletTime;

    public bool useMoveInpuptMagnitedeToForwardAmount;

    [Space]
    [Header ("Main Capsule Settings")]
    [Space]

    public bool setCustomCapsuleCenter;
    public Vector3 customCapsuleCenter;
    public Vector3 customCapsuleCenterOnCrouch;

    [Space]
    [Header ("Animator Settings")]
    [Space]

    public int baseLayerIndex = 0;

    public float inputHorizontalLerpSpeed = 0.03f;
    public float inputVerticalLerpSpeed = 0.03f;
    public float inputStrafeHorizontalLerpSpeed = 0.3f;
    public float inputStrafeVerticalLerpSpeed = 0.3f;

    public float animatorForwardInputLerpSpeed = 0.1f;
    public float animatorTurnInputLerpSpeed = 0.1f;

    public float moveSpeedMultiplier = 1;
    public float animSpeedMultiplier = 1;

    public float minTimeToSetIdleOnStrafe = 0.5f;

    public bool overrideAnimationSpeedActive;

    public float characterRadius;

    public bool usingAnimator = true;

    [Space]
    [Header ("Animator ID State")]
    [Space]

    public float currentIdleID;

    public int currentStrafeID;

    public int currentAirID;

    public int currentCrouchID;

    public int currentAirSpeed = 1;

    public int currentShieldActive;

    public int currentUseStrafeLanding = 0;

    public int currentWeaponID = 0;

    bool usingNewWeaponID;

    [Space]
    [Header ("Tank Controls Animator Settings")]
    [Space]

    public float inputTankControlsHorizontalLerpSpeed = 0.3f;
    public float inputTankControlsVerticalLerpSpeed = 0.3f;
    public float inputTankControlsHorizontalStrafeLerpSpeed = 0.06f;
    public float inputTankControlsVerticalStrafeLerpSpeed = 0.06f;


    [Space]
    [Header ("Player Status ID Settings")]
    [Space]

    public int playerStatusID;
    public float playerStatusIDLerpSpeed = 0.1f;

    [Space]
    [Header ("Ground Detection Settings")]
    [Space]

    public LayerMask layer;
    public float rayDistance;
    public bool updateFootStepStateActive = true;

    public bool useSphereRaycastForGroundDetection;

    public float sphereCastRadius = 0.4f;
    public float maxDistanceSphereCast = 0.5f;

    public float sphereCastOffset;

    public float maxExtraRaycastDistanceToGroundDetection = 0.4f;

    public bool checkIfSurfaceBelowIsTerrainEnabled = true;

    [Space]
    [Header ("Walk/Run Sprint Settings")]
    [Space]

    [Range (0, 1)] public float walkSpeed = 1;

    public bool increaseWalkSpeedEnabled;
    public float increaseWalkSpeedValue = 1;
    public bool holdButtonToKeepIncreasedWalkSpeed;

    [TextArea (3, 8)]
    public string walkRunTip = "Set 'Sprint Enabled' field to false and change 'Walk Speed' to 0.5 to start the " +
                                                 "game walking and toggle to run. Or set 'Increase Walk Speed Value' to 0.5 to vice versa.";

    [Space]
    [Header ("Sprint Settings")]
    [Space]

    public bool sprintEnabled = true;
    public bool changeCameraFovOnSprint = true;
    public bool shakeCameraOnSprintThirdPerson = true;
    public string sprintThirdPersonCameraShakeName = "Sprint Shake";

    public bool canRunThirdPersonActive = true;

    public bool useSecondarySprintValues = true;
    public float sprintVelocity = 1;
    public float sprintJumpPower = 15;
    public float sprintAirSpeed = 20;
    public float sprintAirControl = 4;

    public bool runOnCrouchEnabled = true;

    [Space]
    [Header ("Ground Adherence Settings")]
    [Space]

    public float regularGroundAdherence = 5;
    public float slopesGroundAdherenceUp = 2;
    public float slopesGroundAdherenceDown = 4;

    [Range (0, 1)] public float maxRayDistanceRange = 0.5f;
    [Range (0, 1)] public float maxSlopeRayDistance = 0.5f;
    [Range (0, 1)] public float maxStairsRayDistance = 0.5f;

    [Tooltip ("Max surface to walk, higher slopes will make the player to enter on air state")]
    public bool useMaxWalkSurfaceAngle;
    public float maxWalkSurfaceAngle;

    public bool useMaxDistanceToCheckSurfaceAngle;
    public float maxDistanceToCheckSurfaceAngle;

    public bool useMaxSlopeInclination = true;
    public float maxSlopeInclination = 80;
    public float minSlopeInclination = -80;


    [Space]
    [Header ("Root Motion Settings")]
    [Space]

    public bool useRootMotionActive = true;
    public float noRootLerpSpeed;
    public float noRootWalkMovementSpeed;
    public float noRootRunMovementSpeed;
    public float noRootSprintMovementSpeed;
    public float noRootCrouchMovementSpeed;
    public float noRootCrouchRunMovementSpeed = 4;
    public float noRootWalkStrafeMovementSpeed;
    public float noRootRunStrafeMovementSpeed;

    public float noRootDelayToMoveOnLand = 0.2f;

    public bool disableRootMotionTemporalyOnLandThirdPerson;
    public float durationDisableRootMotionTemporalyOnLandThirdPerson;
    public float waitToDisableRootMotionTemporalyOnLandThirdPerson;

    bool forceUseRootMotionActive;

    [Space]
    [Header ("Player ID Settings")]
    [Space]


    public int playerID;

    public bool playerIsNonLocalAvatar;

    public int playerNonLocalAvatarID;

    public int characterModelID = 0;

    [Space]
    [Header ("AI Settings")]
    [Space]

    public bool usedByAI;

    [Space]
    [Header ("First Person Settings")]
    [Space]

    public float noAnimatorSpeed;
    public float noAnimatorWalkMovementSpeed;
    public float noAnimatorRunMovementSpeed;
    public float noAnimatorCrouchMovementSpeed;
    public float noAnimatorRunCrouchMovementSpeed;
    public float noAnimatorStrafeMovementSpeed;
    public bool noAnimatorCanRun;
    public float noAnimatorWalkBackwardMovementSpeed;
    public float noAnimatorRunBackwardMovementSpeed;
    public float noAnimatorCrouchBackwardMovementSpeed;
    public float noAnimatorRunCrouchBackwardMovementSpeed;
    public float noAnimatorStrafeBackwardMovementSpeed;

    public bool noAnimatorCanRunBackwards;
    public float noAnimatorAirSpeed = 11;

    public float maxVelocityChange;

    public float noAnimatorMovementSpeedMultiplier = 1;

    public float noAnimatorSlopesGroundAdherenceUp = 1;
    public float noAnimatorSlopesGroundAdherenceDown = 8;

    public float noAnimatorStairsGroundAdherence = 2;

    [Space]
    [Header ("Full Body Awareness Settings")]
    [Space]

    public float FBAWalkMovementSpeed = 4;
    public float FBARunMovementSpeed = 8;
    public float FBASprintMovementSpeed = 12;
    public float FBACrouchMovementSpeed = 3;
    public float FBACrouchRunMovementSpeed = 6;
    public float FBAWalkStrafeMovementSpeed = 5;
    public float FBARunStrafeMovementSpeed = 9;

    public bool useFBACustomMoveSpeedMultiplier;
    public float FBACustomMoveSpeedMultiplier;
    public bool ignoreFBACustomMoveSpeedMultiplierOnActionActive = true;

    [Space]
    [Header ("Advanced Settings")]
    [Space]

    [Space]
    [Header ("Sprint Settings")]
    [Space]

    public bool useEventOnSprint;
    public UnityEvent eventOnStartSprint;
    public UnityEvent eventOnStopSprint;

    [Space]
    [Header ("Look In Camera Direction Settings")]
    [Space]

    public bool lookAlwaysInCameraDirection;
    public bool lookInCameraDirectionIfLookingAtTarget;
    public bool lookOnlyIfMoving;

    public bool lookInCameraDirectionOnCrouchState;

    public bool ignoreStrafeStateOnAirEnabled = true;

    public bool updateUseStrafeLandingEnabled;

    public bool disableLookAlwaysInCameraDirectionIfIncreaseWalkSpeedActive;

    public float defaultStrafeWalkSpeed = 1;
    public float defaultStrafeRunSpeed = 2;
    public bool rotateDirectlyTowardCameraOnStrafe;
    public float strafeLerpSpeed = 0.1f;

    public bool updateHeadbobState;

    [Space]
    [Header ("Quick Movement Turn Settings")]
    [Space]

    public bool checkQuickMovementTurn180DegreesOnRun;
    public float minDelayToActivateQuickMovementTurn180OnRun = 2;
    public float quickTurnMovementDurationWalking = 1;
    public float quickTurnMovementDurationRunning = 0.8f;
    public float quickTurnMovementDurationSprinting = 2.1f;

    public float quickTurnMovementRotationSpeed = 2;

    [Space]
    [Header ("Character Mesh Settings")]
    [Space]

    public GameObject characterMeshGameObject;

    public List<GameObject> extraCharacterMeshGameObject = new List<GameObject> ();

    public eventParameters.eventToCallWithBool characterMeshesListToDisableOnEvent;

    public bool checkCharacterMeshIfGeneratedOnStart;

    public GameObject objectToCheckCharacterIfGeneratedOnStart;

    [Space]
    [Header ("Move While Aim Settings")]
    [Space]

    public bool canMoveWhileAimFirstPerson = true;
    public bool canMoveWhileAimThirdPerson = true;

    public bool canMoveWhileFreeFireOnThirdPerson = true;

    [Space]
    [Header ("New Ground Adherence Settings")]
    [Space]

    [Tooltip ("Improved ground adherence with better settings and movement")]
    public bool useNewGroundAdherenceEnabled;

    [Tooltip ("Distance to check the ground below the player with raycast")]
    public float groundRaycastDistance = 2f;
    [Tooltip ("Speed to adjust the player to the surface detected on the movement or idle")]
    public float groundLerpSpeed = 15f;
    [Tooltip ("Max height for steps or stairs on the surface")]
    public float maxStepHeight = 0.5f;
    [Tooltip ("Max surface slope that the player can walk")]
    public float maxSlopeAngle = 45f;

    [Tooltip ("Min angle to use normalized input value, to allow better movement on slopes")]
    public float minAngleToUsedInputNormalized = 25;

    [Tooltip ("Raycast range around the player position used for the movement input")]
    [Range (0, 1)] public float raycastPositionRange = 1;

    [Tooltip ("Raycast distance to check surface below the player on idle")]
    public float raycastDistanceOnSphereCastOnIdle = 0.75f;

    [Tooltip ("Radius to check for height difference around the player center position, to check for steps")]
    public float heightDiffRaycastRadius = 1.5f;

    [Tooltip ("Duration that the player takes to be moved to the step detected")]
    public float durationToWalkToStep = 0.5f;

    [Tooltip ("Min height to check for steps in front of the player, using the feet as center position")]
    public float checkStepRaycastHeight = 0.2f;

    [Tooltip ("Extra distance to check for steps around player's center")]
    public float checkStepRaycastDistance = 0.2f;

    [Space]
    [Header ("Stairs Settings")]
    [Space]

    public float stairsMinValue = 0.2f;
    public float stairsMaxValue = 0.25f;

    public float stairsGroundAdherence = 8;

    public bool checkStairsWithInclination;
    public float minStairInclination = -10;
    public float maxStairInclination = 10;

    public bool checkForStairAdherenceSystem;

    public float currentStairAdherenceSystemMaxValue;
    public float currentStairAdherenceSystemMinValue;
    public float currentStairAdherenceSystemAdherenceValue;

    [Space]
    [Header ("Locked Camera Settings")]
    [Space]

    public lockedPlayerMovementMode lockedPlayerMovement;

    [Range (0, 1)] public float tankModeRotationSpeed;
    public bool canMoveWhileAimLockedCamera = true;

    public bool crouchVerticalInput2_5dEnabled;

    bool moveInXAxisOn2_5d;

    [Space]
    [Header ("Gravity Settings")]
    [Space]

    public float gravityMultiplier = 2;
    public float gravityForce = -9.8f;

    public bool useMaxFallSpeed;
    public float maxFallSpeed;

    [Space]
    [Header ("Physics Settings")]
    [Space]

    public PhysicsMaterial zeroFrictionMaterial;
    public PhysicsMaterial highFrictionMaterial;

    [Space]
    [Header ("Jump Settings")]
    [Space]

    public bool enabledRegularJump;
    public bool enabledDoubleJump;
    public int maxNumberJumpsInAir;

    [Range (0, 1)] public float jumpMassMultiplier = 1;

    public bool useNewDoubleJumpPower;
    public float doubleJumpPower = 10;

    public float readyToDoubleJumpTime = 0.2f;

    public bool removeVerticalSpeedOnDoubleJump;

    public bool holdJumpSlowDownFallEnabled = true;

    public float slowDownGravityMultiplier;

    public float readyToJumpTime = 0.25f;
    public bool ignoreAnimatorGroundStateOnJumpCheck;

    public bool addJumpForceWhileButtonPressed;
    public float addJumpForceDuration;
    public float jumpForceAmontWhileButtonPressed;

    public float minWaitToAddJumpForceAfterButtonPressed;
    public bool useJumpForceOnAirJumpEnabled;

    public bool keepLastMovementInputOnJump;
    public float keepLastMovementInputOnJumpDuration;
    public bool allowRegularAirInputControlOnAirAfterJumpDuration;

    public bool allowAirControlOnKeepLastMovementInputOnJump;

    public bool useDelayOnGroundJump;
    public float delayOnGroundJump;
    public bool useDelayOnGroundJumpOnlyIfNotMoving;

    public bool ignoreDelayOnGroundJumpOnFBA = true;

    public bool useEventOnDelayOnGroundJump;
    public UnityEvent eventOnDelayOnGroundJump;

    public bool useEventOnJump;
    public UnityEvent eventOnJump;

    public bool useEventOnGroundJump;
    public UnityEvent eventOnGroundJump;

    public bool useEventOnLandFromJumpInput;
    public UnityEvent eventOnLandFromJumpInput;

    public bool useEventOnLand;
    public UnityEvent eventOnLand;

    public bool stopCrouchOnJumpEnabled;

    public bool useEventOnDoubleJump;
    public UnityEvent eventOnDoubleJump;

    public bool useEventOnAirborne;
    public UnityEvent eventOnAirborne;

    [Space]

    public bool checkObstacleAbovePlayerOnJumpEnabled;
    public float checkObstacleAbovePlayerOnJumpRaycastDistance;
    public LayerMask checkObstacleAbovePlayerOnJumpLayermask;

    [Space]
    [Header ("Fall Damage Settings")]
    [Space]

    public bool fallDamageEnabled;
    [Tooltip ("The time a player can be in the air and falling before they may receive damage.")]
    public float maxTimeInAirBeforeGettingDamage = 4;

    public float fallingDamageMultiplier = 0.2f;

    public float minPlayerVelocityToApplyDamageThirdPerson = 15;
    public float minPlayerVelocityToApplyDamageFirstPerson = 10;

    public bool ignoreShieldOnFallDamage;

    public int damageTypeIDOnFallDamage = -1;

    public bool applyFallDamageEnabled = true;

    bool fallDamageCheckOnHealthPaused;

    public bool callEventOnFallDamage;

    public float minTimeOnAirToUseEvent;

    public bool callEventOnlyIfPlayerAlive;
    public UnityEvent eventOnFallDamage;

    [Space]
    [Header ("Land Mark Settings")]
    [Space]

    public bool useLandMark;
    public float maxLandDistance;
    public float minDistanceShowLandMark;
    public GameObject landMark;
    public Transform landMark1;
    public Transform landMark2;

    [Space]
    [Header ("Player Modes Settings")]
    [Space]

    public bool canUseSphereMode;

    [Space]
    [Header ("Vehicle Settings")]
    [Space]

    public bool canGetOnVehicles;
    public bool canDrive;

    [Space]
    [Header ("Crouch Settings")]
    [Space]

    public float capsuleHeightOnCrouch = 1;

    public bool getUpIfJumpOnCrouchInThirdPerson;
    public bool getUpIfJumpOnCrouchInFirstPerson;

    public bool useAutoCrouch;

    public LayerMask layerToCrouch;

    public bool useCustomLayerToCheckIfPlayerCanGetUpFromCrouch;
    public LayerMask customLayerToCheckIfPlayerCanGetUpFromCrouch;

    public float raycastDistanceToAutoCrouch;
    public Transform autoCrouchRayPosition;
    public Vector3 secondRaycastOffset;

    public bool canCrouchWhenUsingWeaponsOnThirdPerson = true;

    [Space]
    [Header ("Obstacle Detection To Avoid Movement Settings")]
    [Space]

    public bool useObstacleDetectionToAvoidMovement;
    public float obstacleDetectionToAvoidMovementRaycastDistance = 0.5f;
    public float obstacleDetectionRaycastDistanceRightAndLeft = 0.1f;
    public float obstacleDetectionRaycastHeightOffset;

    public LayerMask obstacleDetectionToAvoidMovementLayermask;

    [Space]
    [Header ("Abilities Settings")]
    [Space]

    [Space]
    [Header ("Crouch Sliding Settings")]
    [Space]

    public bool crouchSlidingEnabled;
    public float noAnimatorCrouchSlidingSpeed;

    public float noAnimatorCrouchSlidingDuration;

    public float noAnimatorCrouchSlidingLerpSpeed;
    public float noAnimatorCrouchSlidingLerpDelay;

    public bool getUpAfterCrouchSlidingEnd;

    public bool keepCrouchSlidingOnInclinatedSurface;
    public float minInclinitaonSurfaceAngleToCrouchcSliding;

    public bool crouchSlidingOnAirEnabled;

    public float noAnimatorCrouchSlidingOnAirSpeed;

    public bool useNoAnimatorCrouchSlidingDurationOnAir;
    public float noAnimatorCrouchSlidingDurationOnAir;

    public UnityEvent eventOnCrouchSlidingStart;
    public UnityEvent eventOnCrouchSlidingEnd;

    public bool useCrouchSlidingOnThirdPersonEnabled;

    public UnityEvent eventOnCrouchSlidingThirdPersonStart;
    public UnityEvent eventOnCrouchSlidingThirdPersonEnd;

    bool slidindOnThirdPersonActive;

    [Space]
    [Header ("Wall Running Settings")]
    [Space]

    public bool wallRunningEnabled;

    public externalControllerBehavior wallRunningExternalControllerBehavior;

    [Space]
    [Header ("Air Dash Settings")]
    [Space]

    public bool airDashEnabled;
    public float airDashForce = 20;
    public float airDashColdDown = 0.5f;
    public bool pauseGravityForce;
    public float gravityForcePausedTime = 1;
    public bool resetGravityForceOnDash;
    public bool useDashLimit;
    public int dashLimit;
    public bool changeCameraFovOnDash;
    public float cameraFovOnDash;
    public float cameraFovOnDashSpeed;

    public float minTimeOnAirToUseAirDash = 0.5f;

    [Space]

    public bool useEventsOnAirDash;
    public UnityEvent eventOnAirDashThirdPerson;
    public UnityEvent eventOnAirDashFirstPerson;

    [Space]
    [Header ("Zero Gravity Settings")]
    [Space]

    public float zeroGravityMovementSpeed;
    public float zeroGravityControlSpeed;
    public float zeroGravityLookCameraSpeed;
    public bool useGravityDirectionLandMark;
    public Transform forwardSurfaceRayPosition;
    public float maxDistanceToAdjust;
    public bool pauseCheckOnGroundStateZG;
    public bool pushPlayerWhenZeroGravityModeIsEnabled;
    public float pushZeroGravityEnabledAmount;
    public bool canMoveVerticallyOnZeroGravity;
    public bool canMoveVerticallyAndHorizontalZG;
    public float zeroGravitySpeedMultiplier = 1.4f;
    float currentZeroGravitySpeedMultiplier = 1;
    public float zeroGravityModeVerticalSpeed = 2;

    public bool useMaxAngleToCheckOnGroundStateZGFF;
    public float maxAngleToChekOnGroundStateZGFF;

    public bool ignoreSetCheckOnGrundStatePausedFFOrZGStateActive;

    [Space]
    [Header ("Free Floating Mode Settings")]
    [Space]

    public float freeFloatingMovementSpeed;
    public float freeFloatingControlSpeed;
    public bool pauseCheckOnGroundStateFF;
    public bool canMoveVerticallyOnFreeFloating;
    public bool canMoveVerticallyAndHorizontalFF;
    public float freeFloatingSpeedMultiplier = 1.4f;
    public float pushFreeFloatingModeEnabledAmount;
    public float freeFloatingModeVerticalSpeed = 2;

    [Space]
    [Header ("Ragoll Settings")]
    [Space]

    public bool activateRagdollOnFallState;
    public float minWaitTimeToActivateRagdollOnFall;
    public float minSpeedToActivateRagdollOnFall;
    public float pushRagdollMultiplierOnFall;
    public eventParameters.eventToCallWithVector3 eventToActivateRagdollOnFall;

    [Space]
    [Header ("Debug")]
    [Space]

    [Space]
    [Header ("Controller State")]
    [Space]

    public bool playerOnGround;
    public bool isMoving;

    public Vector3 moveInput;

    public bool jump;
    public bool running;
    public bool crouching = false;
    public bool canMove = true;
    public bool slowingFall;

    public bool isDead;

    float lastTimeResurrect;

    public bool playerSetAsChildOfParent;

    Transform currentTemporalPlayerParent;

    bool keepCameraRelativeMovementWhenNotAiming;

    public bool useRelativeMovementToLockedCamera;
    public bool ladderFound;

    public bool ignoreCameraDirectionOnMovement;
    public bool strafeModeActive;

    public footStepState currentFootStepState;

    public headbobState currentHeadbobState;
    public bool actionActive;
    public bool rootMotionCurrentlyActive;

    public bool usingGenericModelActive;

    public bool canMoveAI = true;

    [Space]
    [Header ("Ground Adherence State")]
    [Space]

    public bool adhereToGround;

    public float distanceToGround;
    public float hitAngle;

    public bool slopeFound;
    public bool movingOnSlopeUp;
    public bool movingOnSlopeDown;

    public bool stairsFound;
    public bool stairAdherenceSystemDetected;
    public bool wallRunningActive;
    public bool crouchSlidingActive;

    public bool terrainDetected;

    [Space]
    [Header ("New Ground Adherence State")]
    [Space]

    public bool isGrounded;

    public bool stickToGroundOnIdleResult;

    public bool adjustPositionResult;

    public float groundHeightDifference;

    public bool futureHasGround;

    public bool stepsDetected;
    public float futureSurfaceAngle;

    public bool movingTowardWall;

    public float extraHeightDiffRaycastRadius = 0;

    public float customMaxStepHeight = 0;

    public bool ignoreNewGroundAdherenceActive;

    bool ignoreInputOnNewGroundAdherenceActive;

    [Space]
    [Header ("Weapons And Powers State")]
    [Space]

    public bool playerIsAiming;
    public bool aimingInThirdPerson;
    public bool aimingInFirstPerson;
    public bool usingFreeFireMode;
    public bool lookInCameraDirectionOnFreeFireActive;

    public bool playerUsingMeleeWeapons;

    bool playerMeleeWeaponThrown;

    [Space]
    [Header ("Mode State")]
    [Space]

    public bool jetPackEquiped;
    public bool usingJetpack;

    public bool flyModeActive;

    public bool swimModeActive;
    public bool sphereModeActive;

    public bool freeClimbActive;

    public bool grabbingToSurfaceActive;

    public bool adjustingToSurfaceToGrabInProcess;

    public bool enableExternalForceOnSwimMode;

    public bool enableExternalForceOnFlyMode;

    public bool carryingPhysicalObject;

    [Space]
    [Header ("Camera State")]
    [Space]

    public bool lockedCameraActive;
    public bool lookInCameraDirectionActive;
    public bool firstPersonActive;

    public bool update2_5dClampedPositionPaused;

    [Space]
    [Header ("Other Elements State")]
    [Space]

    public bool usingDevice;

    public bool visibleToAI = true;
    public bool stealthModeActive;
    public bool playerNavMeshEnabled;
    public bool ignoreExternalActionsActiveState;

    public bool movingOnPlatformActive;

    bool movingInsideVehicle;

    [Space]
    [Header ("Menu Elements State")]
    [Space]

    public bool usingSubMenu;
    public bool playerMenuActive;
    public bool gamePaused;

    [Space]
    [Header ("Vehicle State")]
    [Space]

    public bool driving;
    public bool drivingRemotely;
    public bool overridingElement;
    public string currentVehicleName;

    float lastTimeDriving = -1;

    [Space]
    [Header ("Gravity State")]
    [Space]

    public bool gravityForcePaused;
    public bool zeroGravityModeOn;
    public bool freeFloatingModeOn;
    public Vector3 currentNormal = new Vector3 (0, 1, 0);
    public bool checkOnGroundStatePausedFFOrZG;
    public bool checkOnGroundStatePaused;

    [Space]
    [Header ("Gizmo State")]
    [Space]

    public bool showDebugPrint;
    public bool showGizmo;
    public Color gizmoColor;
    public Color gizmoLabelColor;
    public float gizmoRadius;

    //ANIMATOR VALUES
    [Space]
    [Header ("ANIMATOR ID VALUES")]
    [Space]

    public string forwardAnimatorName = "Forward";
    public string turnAnimatorName = "Turn";

    public string horizontalAnimatorName = "Horizontal";
    public string verticalAnimatorName = "Vertical";

    public string horizontalStrafeAnimatorName = "Horizontal Strafe";
    public string verticalStrafeAnimatorName = "Vertical Strafe";

    public string onGroundAnimatorName = "OnGround";
    public string crouchAnimatorName = "Crouch";
    public string movingAnimatorName = "Moving";
    public string jumpAnimatorName = "Jump";
    public string jumpLegAnimatorName = "JumpLeg";

    public string strafeModeActiveAnimatorName = "Strafe Mode Active";
    public string movementInputActiveAnimatorName = "Movement Input Active";
    public string movementRelativeToCameraAnimatorName = "Movement Relative To Camera";
    public string movementIDAnimatorName = "Movement ID";
    public string playerModeIDAnimatorName = "Player Mode ID";
    public string movementSpeedAnimatorName = "Movement Speed";
    public string lastTimeInputPressedAnimatorName = "Last Time Input Pressed";

    public string carryingWeaponAnimatorName = "Carrying Weapon";
    public string aimingModeActiveAnimatorName = "Aiming Mode Active";

    public string playerStatusIDAnimatorName = "Player Status ID";

    public string idleIDAnimatorName = "Idle ID";

    public string strafeIDAnimatorName = "Strafe ID";

    public string crouchIDAnimatorName = "Crouch ID";

    public string airIDAnimatorName = "Air ID";

    public string airSpeedAnimatorName = "Air Speed";

    public string quickTurnRightDirectionName = "Quick Turn Right Direction";
    public string quickTurnLeftDirectionName = "Quick Turn Left Direction";
    public string quickTurnDirectionIDSpeedName = "Quick Turn Direction ID";

    public string shieldActiveAnimatorName = "Shield Active";

    public string inputAmountName = "Input Amount";

    public string useStrafeLandingName = "Use Strafe Landing";

    public string weaponIDAnimatorName = "Weapon ID";

    public string rightArmdIDAnimatorName = "Right Arm ID";
    public string leftArmIDAnimatorName = "Left Arm ID";

    public string timeOnAirAnimatorName = "Time On Air";


    public string animatorDeathLayerName = "Death";
    public string animatorDialogueLayerName = "Dialogue";
    public string animatorArmsLayerName = "Arms";
    public string animatorRightHandLayerName = "Right Hand";
    public string animatorLeftHandLayerName = "Left Hand";
    public string animatorRightArmLayerName = "Right Arm";
    public string animatorLeftArmLayerName = "Left Arm";

    public string animatorFBAActiveName = "FBA Active";

    [Space]
    [Header ("Character Components")]
    [Space]

    //Player components
    public headBob headBobManager;
    public playerInputManager playerInput;
    public playerWeaponsManager weaponsManager;
    public healthManagement healthManager;
    public IKSystem IKSystemManager;

    public Transform playerTransform;

    //Camera variables
    public GameObject playerCameraGameObject;
    public Transform playerCameraTransform;
    public Transform mainCameraTransform;
    public cameraControllerManager playerCameraManager;
    public Animator animator;
    public footStepManager stepManager;
    public Rigidbody mainRigidbody;
    public gravityObjectManager gravityManager;
    public characterStateIconSystem characterStateIconManager;
    public CapsuleCollider capsule;
    public Collider mainCollider;

    public bool useExtraColliderList;
    public List<Collider> extraColliderList = new List<Collider> ();

    public GameObject AIElements;

    public playerActionSystem mainPlayerActionSystem;

    public bool useEventsOnWalk;
    public UnityEvent eventOnWalkStart;
    public UnityEvent eventOnWalkEnd;

    public bool useEventsOnRun;
    public UnityEvent eventOnRunStart;
    public UnityEvent eventOnRunEnd;


    public bool useEventsOnEnableDisableCharacterMeshes;
    public UnityEvent eventOnEnableCharacterMeshes;
    public UnityEvent eventOnDisableCharacterMeshes;
    public UnityEvent eventOnEnableCharacterMeshesOnEditor;
    public UnityEvent eventOnDisableCharacterMeshesOnEditor;

    bool playeCurrentlyUsingInput;

    int forwardAnimatorID;
    int turnAnimatorID;
    int horizontalAnimatorID;
    int verticalAnimatorID;

    int horizontalStrafeAnimatorID;
    int verticalStrafeAnimatorID;

    int onGroundAnimatorID;
    int crouchAnimatorID;
    int movingAnimatorID;
    int jumpAnimatorID;
    int jumpLegAnimatorID;

    int strafeModeActiveAnimatorID;
    int movementInputActiveAnimatorID;
    int movementRelativeToCameraAnimatorID;
    int movementIDAnimatorID;
    int playerModeIDAnimatorID;
    int movementSpeedAnimatorID;
    int lastTimeInputPressedAnimatorID;

    int carryingWeaponAnimatorID;
    int aimingModeActiveAnimatorID;

    int playerStatusIDAnimatorID;

    int idleIDAnimatorID;

    int strafeIDAnimatorID;

    int crouchIDAnimatorID;

    int airIDAnimatorID;

    int airSpeedAnimatorID;

    int quickTurnRightDirectionID;
    int quickTurnLeftDirectionID;
    int quickTurnDirectionIDSpeedID;

    int shieldActiveAnimatorID;

    int inputAmountID;

    int useStrafeLandingID;

    int weaponIDAnimatorID;

    int rightArmdIDAnimatorID;
    int leftArmIDAnimatorID;

    int timeOnAirAnimatorID;

    int animatorFBAActiveID;


    int lastCurrentUseStrafeLandingValue = -1;

    float lastForwardAnimatorIDValue = -1;
    float lastTurnAnimatorIDValue = -1;

    float lastHorizontalAnimatorIDValue = -1;
    float lastVerticalAnimatorIDValue = -1;

    float lastJumpAnimatorIDValue = -1;

    float lastJumpLegAnimatorIDValue = -1;


    Vector3 vector3Zero = Vector3.zero;
    Quaternion quaternionIdentity = Quaternion.identity;

    Vector3 Vector3Up = Vector3.up;

    Vector3 playerTransformUp;
    Vector3 playerTransformForward;
    Vector3 playerTransformRight;

    float half = 0.5f;

    float halfJumpPower = 0;


    //OTHER FIELDS
    [HideInInspector] public bool canMoveVertically;
    [HideInInspector] public bool canMoveVerticallyAndHorizontal;
    [HideInInspector] public bool movingVertically;

    float currentFreeFloatingSpeedMultiplier = 1;

    float originalStationaryTurnSpeed;

    float originalWalkSpeedValue;

    bool increaseWalkSpeedActive;

    float currentHorizontalInputLerpSpeed;
    float currentVerticalInputLerpSpeed;
    float currentInputStrafeHorizontalLerpSpeed;
    float currentInputStrafeVerticalLerpSpeed;

    //	public bool useWalkToRunAccelerationChange;
    //	public float walkToRunAccelerationSpeed = 0.6f;
    //	float currentWalkSpeedTarget;

    bool sprintCanBeUsed = true;

    //Input variables
    [HideInInspector] public float horizontalInput;
    [HideInInspector] public float verticalInput;

    [HideInInspector] public Vector2 axisValues;
    [HideInInspector] public Vector2 rawAxisValues;

    bool movementInputIgnoredOnAimActive;

    Vector3 secondaryMoveInput;
    Vector3 secondaryMove;
    Vector3 stairsMoveInput;
    Vector3 airMove;

    [HideInInspector] public Vector3 currentMoveInput;

    Vector3 lastMoveInputOnJump = Vector3.zero;

    bool ignoreLastMoveInputOnJump;

    Vector3 lockedCameraMoveInput;
    Vector3 currentLockedCameraInput;
    Vector3 lockedMove;

    Vector3 currentAdherenceInput;

    bool overrideOnGroundAnimatorValueActive;
    float overrideOnGroundDuration;
    float lastTimeOverrideOnGroundAnimator;

    bool playerOnGroundAnimatorStateOnOverrideOnGround = true;

    bool playerOnGroundAnimatorStateOnOverrideOnGroundWithTime = true;

    [HideInInspector] public bool playerActionsInputEnabled = true;

    //Fall damage variables

    bool fallDamageCheckPaused;

    [HideInInspector] public float lastTimeFalling;

    float currentLastTimeFalling;
    float currentFallSpeed;

    bool checkFallState;

    bool checkFallStatePaused;

    //Stairs variables

    float currentStairMaxValue;
    float currentStairMinValue;
    float currentStairAdherenceValue;

    GameObject currentDetectedSurface;
    GameObject currentTargetDetected;

    bool stairInclinationDetected;

    //Animator variables

    public bool useRootMotionOnFullBodyAwareness;

    public bool fullBodyAwarenessActive;

    float originalAnimationSpeed;

    [HideInInspector] public bool animatorSpeedUpdatePaused;

    [HideInInspector] public bool useFirstPersonPhysicsInThirdPersonActive;

    [HideInInspector] public bool lookInCameraDirectionOnFreeFire = true;

    [HideInInspector] public float currentNoRootMovementSpeed;
    [HideInInspector] public float lerpNoRootMovementSpeed;

    [HideInInspector] public bool originalSetUseRootMotionActiveState;

    [HideInInspector] public bool fullBodyAwarenessActiveAtStart;

    float originalNoRootWalkMovementSpeed;
    float originalNoRootRunMovementSpeed;
    float originalNoRootSprintMovementSpeed;
    float originalNoRootCrouchMovementSpeed;
    float originalNoRootCrouchRunMovementSpeed;
    float originalNoRootWalkStrafeMovementSpeed;
    float originalNoRootRunStrafeMovementSpeed;

    bool rootMotionDisabledTemporaly;

    bool checkingDisableRootMotionTemporaly;

    float lastTimeRootMotionDisabledTemporaly;

    [HideInInspector] public Vector3 characterVelocity;

    float lastTimeMoveInputPressed;
    float lastTimePlayerUserInput;

    bool lastTimeInputPressed;
    bool previousValueLastTimeInputPressed;

    bool previousValueOnGroundAnimatorState;
    bool onGroundAnimatorState;

    bool movementRelativeToCameraAnimatorState;
    bool previousValueMovementRelativeToCameraState;

    bool carryingWeaponAnimatorState;
    bool previousValueCarryingWeaponAnimatorState;

    bool aimingModeActiveAnimatorState;
    bool previousValueAimingModeActiveAnimatorState;

    bool tankModeActive;
    bool scrollModeActive;
    float currentMovementID;
    float currentPlayerModeID;
    float currentMovementSpeed;
    float currentHorizontalValue;
    float currentVerticalValue;

    [HideInInspector] public float noAnimatorCurrentMovementSpeed;

    [HideInInspector] public bool crouchSlidingOnAirActive;

    //States variables
    public bool gravityPowerActive;
    [HideInInspector] public bool choosingGravityDirection;

    [HideInInspector] public bool headBobPaused;

    [HideInInspector] public bool canRagdollMove = true;

    public bool usingCloseCombatActive;
    public bool closeCombatAttackInProcess;

    public bool meleeAttackInProcess;

    bool jumpLegExternallyActiveState;

    bool movingAnimator;
    bool previousValueMovingAnimator;

    public bool moveIputPaused;

    [HideInInspector] public float characterScale = 1;
    [HideInInspector] public bool customCharacterScaleActive;

    //Physics variables
    public Vector3 currentVelocity;
    RaycastHit hit;
    Ray crouchRay;

    RaycastHit mainGroundHit;

    RaycastHit temporalHit;

    RaycastHit sphereRaycastHit = new RaycastHit ();

    bool highFrictionMaterialActive;

    RaycastHit adherenceHit;

    bool physicMaterialAssigmentPaused;

    float currentNoAnimatorSlidingCrouchSpeed;

    float originalNoAnimatorMovementSpeedMultiplier;

    Vector3 noAnimatorCurrentForce;
    Vector3 velocityChange;
    [HideInInspector] public float currentVelocityChangeMagnitude;

    float lastFrameAirVelocity;

    float originalNoAnimWalkMovementSpeed;
    float originalNoAnimRunMovementSpeed;
    float originalNoAnimCrouchMovementSpeed;
    float originalNoAnimStrafeMovementSpeed;
    float originalNoAnimWalkBackwardMovementSpeed;
    float originalNoAnimRunBackwardMovementSpeed;
    float originalNoAnimCrouchBackwardMovementSpeed;
    float originalNoAnimStrafeBackwardMovementSpeed;
    bool originalNoAnimCanRun;

    //Ground variables
    bool onGroundChecked;
    float currentGroundAdherence;
    Transform currentSurfaceBelowPlayer;
    Transform previousSurfaceBelowPlayer;


    Vector3 groundPoint;
    Vector3 mainHitPoint;
    float mainHitAngle;

    Vector3 mainRaycastPosition;
    bool canSetGroundState;
    float lastTimeGround;

    //Collider variables
    [HideInInspector] public float originalHeight;
    [HideInInspector] public float originalRadius;

    float currentHeight;

    //Air variables
    float lastTimeAir;

    //Gravity variables
    float originalGravityMultiplier;

    Vector3 extraGravityForce;
    float currentGravityMultiplier;
    float originalGravityForce;

    float lastTimeGravityForcePaused;
    bool unPauseGravityForceActive;

    bool movementSpeedIncreased;

    //Jump variables
    [HideInInspector] public bool jumpInput;
    [HideInInspector] public bool doubleJump;

    bool lastJumpActivatedExternally;

    float lastJumpTime;
    float lastDoubleJumpTime;
    int jumpsAmount;
    float currentJumpLeg;
    bool canJumpActive = true;
    float currentJumpAnimatorValue;
    bool jumpInputPaused;

    bool jumpButtonHoldActive;
    float lastTimeJumpButtonHoldActive;

    float lastTimeJumpActivated;

    bool runInputPaused;

    bool crouchInputPaused;

    public Transform head;

    Vector3 landMarkRayPosition;
    Vector3 landMarkRayDirection;
    float currentMaxLandDistance;
    Vector3 landMarkForwardDirection;
    Quaternion landMarkTargetRotation;

    //Locked camera variables

    [HideInInspector] public bool useTankControls;
    bool playerCanMoveOnAimInTankMode;

    public enum lockedPlayerMovementMode
    {
        world3d,
        world2_5d
    }

    [HideInInspector] public bool checkCameraDirectionFromLockedToFree;
    bool tankModeCurrentlyEnabled;

    bool inputNotPressed;

    bool followSplinePathActive;

    //Air dash ability variables

    int currentNumberOfDash;
    float lastTimeDash;

    [HideInInspector] public bool usingAbilityActive;

    float currentDeltaTime;

    float currentUpdateDeltaTime;
    float currentFixedUpdateDeltaTime;

    //Crouch variables

    float lastTimeCrouchSlidingActive;

    bool crouchingOnAir;

    bool previousValueCrouching;

    //Run variables

    float lastTimeMovingOnRun;

    float runButtonPressedTimer;
    float lastTimeRun;

    //AI variables

    Vector3 navMeshCurrentLookPos;
    Vector3 navMeshMoveInput;
    bool lookInCameraDirection;

    bool characterControlOverrideActive;

    bool AIStrafeModeActive;
    bool AIEnableInputMovementOnStrafe;

    //Fly mode variables

    bool turnAndForwardAnimatorValuesPaused;

    //Vehicle variables
    GameObject currentVehicle;
    Transform currentVehicleCameraControllerTransform;

    //Controller variables
    float turnAmount;
    float forwardAmount;
    float runCycleLegOffset = 0.2f;
    float lastTimeMoved;

    bool slowFallExternallyActive;

    bool slowingFallInput;

    //External force variables
    bool externalForceActive;
    Vector3 externalForceValue;

    bool externalForceOnAirActive;
    Vector3 externalForceOnAirValue;
    float externalForceOnAirControlValue;

    float originalMoveSpeedMultiplier;
    float originalJumpPower;
    float originalAirSpeed;
    float originalAirControl;

    float originalNoAnimatorAirSpeed;

    GameObject playerManagersParentGameObject;

    bool checkCharacterMeshIfGeneratedOnStartInitialized;

    Transform currentLockedCameraTransform;

    bool inputCanBeUsed;
    bool characterRotatingToSurface;

    float raycastDistance;
    Vector3 rayPosition;
    //	Vector3 rayDirection;

    float hitPointVerticalPosition;

    float mainHitPointVerticalPosition;

    float upDistance;

    bool checkForSlopes;

    float waitTimeToSlowDown;

    bool previouslyOnGround;

    [HideInInspector] public bool checkOnGroundPaused;

    bool rotatingPlayerIn2_5dToRight;
    bool rotatingPlayerIn2_5dToRLeft;

    bool canRotateOn3dWorld;
    bool canRotateOn2_5dWorld;

    bool applyRootMotionAlwaysActive;

    [HideInInspector] public bool actionActiveWithMovement;

    [HideInInspector] public float lastTimeActionActive;

    [HideInInspector] public bool ignoreCameraDirectionOnStrafeMovement;

    [HideInInspector] public bool addExtraRotationPaused;

    [HideInInspector] public bool deactivateRootMotionOnStrafeActiveOnLockedView;

    bool previousStrafeModeActive;

    Vector3 lastForwardDirection;
    Vector3 lastRigthDirection;

    Coroutine resetInputCoroutine;

    bool playerUsingInput;

    bool movementInputActive;
    bool previousValueMovementInputActive;

    [HideInInspector] public Vector3 currentForwardDirection;
    [HideInInspector] public Vector3 currentRightDirection;

    bool originalLookAlwaysInCameraDirection;
    bool originalLookOnlyIfMoving;

    Vector3 currentPlayerPosition;
    Vector3 currentPlayerUp;

    bool stairStepDetected;

    bool setHeadbodStatesPaused;

    [HideInInspector] public bool pauseAllPlayerDownForces;

    bool toggleWalkRunState;
    float previousWalkSpeedValue = 1;

    bool originalSprintEnabledValue;
    bool originalIncreaseWalkSpeedEnabledValue;

    bool obstacleToAvoidMovementFound;

    Vector3 checkObstacleRaycastPosition;
    Vector3 checkObstacleRaycastDirection;

    bool obstacleDetectedAtRight;
    bool obstacleDetectedAtLeft;

    [HideInInspector] public bool headTrackCanBeUsed = true;

    [HideInInspector] public bool disableStrafeModeExternallyIfIncreaseWalkSpeedActive;

    bool currentStrafeModeExternallyActive;

    public externalControllerBehavior currentExternalControllerBehavior;

    public bool useExternalControllerBehaviorPaused;

    public bool externalControllBehaviorActive;

    bool externalControlBehaviorForAirTypeActive;

    bool playerActionSystemLocated;

    bool weaponsManagerLocated;

    float lastTimeFiring;

    bool aimingPowers;

    bool usingPowers;

    float quickTurnMovementDuration;

    float lastTimeQuickTurnActive;

    bool pausePlayerTransformExtraRotationActive;
    float lastTimeMovementInputPressed;
    bool checkingQuickMovementDirectionActive;
    bool activateExtraRotationInQuickTurnMovement;

    public enum footStepState
    {
        None,
        Air_Landing,
        Running,
        Run_Crouching,
        Crouching,
        Walking,
        Jumping
    }

    public enum headbobState
    {
        None,
        Jump_Start,
        Jump_End,
        Air,
        Run_Crouching,
        Running,
        Crouching,
        Walking,
        Dynamic_Idle,
        Static_Idle
    }

    bool addExtraCharacterVelocityActive;

    Vector3 currentExtraCharacterVelocity;

    bool actionCanHappenOnAir;

    bool enableChangeScriptStateAfterFinishActionState;

    bool disableChangeScriptStateAfterFinishActionState;

    bool allowDownVelocityDuringAction;

    bool groundDetected;

    Ray sphereRaycastRay;


    bool headScaleChanged;

    [HideInInspector] public bool customCharacterControllerActive;

    customCharacterControllerBase currentCustomCharacterControllerBase;

    [HideInInspector] public int regularAirboneID = -1;
    [HideInInspector] public int regularAirID = 0;

    bool regularAirboneIDActivePreviously;

    bool overrideTurnAmountActive;
    float overrideTurnAmount;

    bool overrideMainCameraTransformActive;
    Transform overrideMainCameraTransform;

    bool useForwardDirectionForCameraDirection;

    bool useRightDirectionForCameraDirection;

    bool actionToGetOnVehicleActive;
    bool actionToGetOffFromVehicleActive;

    Coroutine rotateCharacterCoroutine;

    [HideInInspector] public bool checkToKeepWeaponAfterAimingWeaponFromShooting;

    float lastTimeCheckToKeepWeapon;

    bool animSpeedMultiplierChangedDuringXTimeActive;

    float changeAnimSpeedMultiplierDuration;

    Coroutine changeAnimSpeedMultiplierCoroutine;

    bool pauseResetAnimatorStateFOrGroundAnimator;

    GameObject currentTemporaVehicle;

    bool applyAnimatorVelocityWithoutMoving;

    bool disableCrouchState;

    float currentVerticalSpeed;

    bool currentApplyRootMotionValue;

    float currentAnimatorSpeed = -2;
    float previousAnimatorSpeed = -1;

    float updateSetFloatAnimatorSpeed = 0.1f;

    float currentWalkSpeedOnLockedCamera;

    bool headBobManagerLocated;

    bool originalCanDrive;
    bool originalCanCharacterGetOnVehiclesState;

    bool originalCanMoveWhileAimLockedCamera;

    bool pauseCameraShakeFromGravityActive;

    bool ignoreInputOnAirControlActive;

    bool useMaxFallSpeedExternallyActive;

    float customMaxFallSpeedExternally;

    bool jumpActivatedByInput;

    bool usingGeneralDriveRiderSystem;

    bool currentSurfaceBelowPlayerLocated;

    bool ignoreUseDelayOnGroundJumpActive;

    float originalRayDistance;

    Vector3 inputDir;

    float lastTimeStepDetected = 0;

    Vector3 dbgFuturePos;
    RaycastHit dbgGroundHit;
    bool dbgHasGroundHit;
    float dbgSlopeAngle;

    Vector3 movementDirection;

    bool checkingLastTimeActionActiveAndCantMove;

    float lastTimeOnAirAnimator;

    float lastTimeMaxWalkSurfaceAngleLocated = 0;

    //int stickToGroundValue;
    //int tryMoveValue;


    void initilizeAnimatorIDValues ()
    {
        forwardAnimatorID = Animator.StringToHash (forwardAnimatorName);
        turnAnimatorID = Animator.StringToHash (turnAnimatorName);

        horizontalAnimatorID = Animator.StringToHash (horizontalAnimatorName);
        verticalAnimatorID = Animator.StringToHash (verticalAnimatorName);

        horizontalStrafeAnimatorID = Animator.StringToHash (horizontalStrafeAnimatorName);
        verticalStrafeAnimatorID = Animator.StringToHash (verticalStrafeAnimatorName);

        onGroundAnimatorID = Animator.StringToHash (onGroundAnimatorName);
        crouchAnimatorID = Animator.StringToHash (crouchAnimatorName);
        movingAnimatorID = Animator.StringToHash (movingAnimatorName);
        jumpAnimatorID = Animator.StringToHash (jumpAnimatorName);
        jumpLegAnimatorID = Animator.StringToHash (jumpLegAnimatorName);

        strafeModeActiveAnimatorID = Animator.StringToHash (strafeModeActiveAnimatorName);
        movementInputActiveAnimatorID = Animator.StringToHash (movementInputActiveAnimatorName);
        movementRelativeToCameraAnimatorID = Animator.StringToHash (movementRelativeToCameraAnimatorName);
        movementIDAnimatorID = Animator.StringToHash (movementIDAnimatorName);
        playerModeIDAnimatorID = Animator.StringToHash (playerModeIDAnimatorName);
        movementSpeedAnimatorID = Animator.StringToHash (movementSpeedAnimatorName);
        lastTimeInputPressedAnimatorID = Animator.StringToHash (lastTimeInputPressedAnimatorName);

        carryingWeaponAnimatorID = Animator.StringToHash (carryingWeaponAnimatorName);
        aimingModeActiveAnimatorID = Animator.StringToHash (aimingModeActiveAnimatorName);

        playerStatusIDAnimatorID = Animator.StringToHash (playerStatusIDAnimatorName);

        idleIDAnimatorID = Animator.StringToHash (idleIDAnimatorName);

        strafeIDAnimatorID = Animator.StringToHash (strafeIDAnimatorName);

        crouchIDAnimatorID = Animator.StringToHash (crouchIDAnimatorName);

        airIDAnimatorID = Animator.StringToHash (airIDAnimatorName);

        airSpeedAnimatorID = Animator.StringToHash (airSpeedAnimatorName);

        quickTurnRightDirectionID = Animator.StringToHash (quickTurnRightDirectionName);
        quickTurnLeftDirectionID = Animator.StringToHash (quickTurnLeftDirectionName);
        quickTurnDirectionIDSpeedID = Animator.StringToHash (quickTurnDirectionIDSpeedName);

        shieldActiveAnimatorID = Animator.StringToHash (shieldActiveAnimatorName);

        inputAmountID = Animator.StringToHash (inputAmountName);

        useStrafeLandingID = Animator.StringToHash (useStrafeLandingName);

        weaponIDAnimatorID = Animator.StringToHash (weaponIDAnimatorName);

        rightArmdIDAnimatorID = Animator.StringToHash (rightArmdIDAnimatorName);
        leftArmIDAnimatorID = Animator.StringToHash (leftArmIDAnimatorName);

        timeOnAirAnimatorID = Animator.StringToHash (timeOnAirAnimatorName);

        animatorFBAActiveID = Animator.StringToHash (animatorFBAActiveName);
    }

    void Awake ()
    {
        initilizeAnimatorIDValues ();

        originalWalkSpeedValue = walkSpeed;
        originalGravityMultiplier = gravityMultiplier;
        currentGravityMultiplier = gravityMultiplier;
        originalGravityForce = gravityForce;

        originalStationaryTurnSpeed = stationaryTurnSpeed;

        if (playerTransform == null) {
            playerTransform = transform;
        }

        if (playerTransform.parent != null) {
            playerManagersParentGameObject = playerTransform.parent.gameObject;
        }

        playerActionSystemLocated = mainPlayerActionSystem != null;

        setApplyRootMotionState (true);

        weaponsManagerLocated = weaponsManager != null;

        headBobManagerLocated = headBobManager != null;

        originalCanDrive = canDrive;

        originalCanCharacterGetOnVehiclesState = canGetOnVehicles;

        originalCanMoveWhileAimLockedCamera = canMoveWhileAimLockedCamera;

        originalRayDistance = rayDistance;
    }

    void Start ()
    {
        //set the collider center in the correct place
        originalHeight = capsule.height;
        originalRadius = capsule.radius;

        if (setCustomCapsuleCenter) {
            capsule.center = customCapsuleCenter;
        } else {
            capsule.center = capsule.height * half * Vector3Up;
        }

        currentHeight = originalHeight;

        halfJumpPower = jumpPower * half;

        //get the player camera
        if (playerCameraTransform == null) {
            playerCameraTransform = playerCameraGameObject.transform;
        }

        if (head == null && animator.avatar != null && animator.avatar.isHuman) {
            head = animator.GetBoneTransform (HumanBodyBones.Head);
        }

        if (useLandMark) {
            if (landMark.activeSelf) {
                landMark.SetActive (false);
            }

            landMark.transform.SetParent (null);
        }

        setHighFrictionMaterial ();

        originalAnimationSpeed = animSpeedMultiplier;

        //get all the important parameters of player controller
        originalMoveSpeedMultiplier = moveSpeedMultiplier;
        originalJumpPower = jumpPower;
        originalAirSpeed = airSpeed;
        originalAirControl = airControl;

        originalNoAnimatorAirSpeed = noAnimatorAirSpeed;

        //no animator original movement values assignment
        originalNoAnimWalkMovementSpeed = noAnimatorWalkMovementSpeed;
        originalNoAnimRunMovementSpeed = noAnimatorRunMovementSpeed;
        originalNoAnimCrouchMovementSpeed = noAnimatorCrouchMovementSpeed;
        originalNoAnimStrafeMovementSpeed = noAnimatorStrafeMovementSpeed;
        originalNoAnimWalkBackwardMovementSpeed = noAnimatorWalkBackwardMovementSpeed;
        originalNoAnimRunBackwardMovementSpeed = noAnimatorRunBackwardMovementSpeed;
        originalNoAnimCrouchBackwardMovementSpeed = noAnimatorCrouchBackwardMovementSpeed;
        originalNoAnimStrafeBackwardMovementSpeed = noAnimatorStrafeBackwardMovementSpeed;
        originalNoAnimCanRun = noAnimatorCanRun;

        originalSetUseRootMotionActiveState = useRootMotionActive;

        originalNoRootWalkMovementSpeed = noRootWalkMovementSpeed;
        originalNoRootRunMovementSpeed = noRootRunMovementSpeed;
        originalNoRootSprintMovementSpeed = noRootSprintMovementSpeed;
        originalNoRootCrouchMovementSpeed = noRootCrouchMovementSpeed;
        originalNoRootCrouchRunMovementSpeed = noRootCrouchRunMovementSpeed;
        originalNoRootWalkStrafeMovementSpeed = noRootWalkStrafeMovementSpeed;
        originalNoRootRunStrafeMovementSpeed = noRootRunStrafeMovementSpeed;


        originalLookAlwaysInCameraDirection = lookAlwaysInCameraDirection;

        originalLookOnlyIfMoving = lookOnlyIfMoving;

        originalSprintEnabledValue = sprintEnabled;

        originalIncreaseWalkSpeedEnabledValue = increaseWalkSpeedEnabled;

        originalNoAnimatorMovementSpeedMultiplier = noAnimatorMovementSpeedMultiplier;

        previousValueOnGroundAnimatorState = !checkIfPlayerOnGroundWithRaycast ();

        if (lastCurrentUseStrafeLandingValue != currentUseStrafeLanding) {
            lastCurrentUseStrafeLandingValue = currentUseStrafeLanding;

            animator.SetFloat (useStrafeLandingID, currentUseStrafeLanding);
        }

        if (!usedByAI) {
            if (fullBodyAwarenessActiveAtStart) {
                setFullBodyAwarenessActiveState (true);
            } else {
                playerCameraManager.checkActivateOrDeactivateHeadColliderOnFBA (true);
            }
        }
    }

    void Update ()
    {
        currentUpdateDeltaTime = getCurrentDeltaTime ();

        inputCanBeUsed = canUseInput ();

        playerIsAiming = isPlayerAiming ();

        lookInCameraDirectionOnFreeFireActive = hasToLookInCameraDirectionOnFreeFire ();

        playerUsingInput = isPlayerUsingInput ();

        if (useEventsOnWalk || useEventsOnRun) {
            if (playeCurrentlyUsingInput != playerUsingInput) {
                playeCurrentlyUsingInput = playerUsingInput;

                if (playeCurrentlyUsingInput) {
                    if (walkSpeed > half) {
                        if (useEventsOnWalk) {
                            eventOnWalkEnd.Invoke ();
                        }

                        if (useEventsOnRun) {
                            eventOnRunStart.Invoke ();
                        }
                    } else {
                        if (useEventsOnRun) {
                            eventOnRunEnd.Invoke ();
                        }

                        if (useEventsOnWalk) {
                            eventOnWalkStart.Invoke ();
                        }
                    }
                } else {
                    if (useEventsOnWalk) {
                        eventOnWalkEnd.Invoke ();
                    }

                    if (useEventsOnRun) {
                        eventOnRunEnd.Invoke ();
                    }
                }
            }
        }

        if (inputCanBeUsed && !playerOnGround && !gravityPowerActive && holdJumpSlowDownFallEnabled) {

            waitTimeToSlowDown = lastJumpTime + 0.2f;

            if (enabledDoubleJump && doubleJump) {
                waitTimeToSlowDown += lastDoubleJumpTime + 1;
            }
        }

        if (lockedCameraActive) {
            if (!update2_5dClampedPositionPaused) {
                if (isPlayerMovingOn2_5dWorld () && !turnBasedCombatActionActive) {
                    if (followSplinePathActive) {
                        //maybe use this on 2.5d mode

                        Vector3 newPosition = playerCameraManager.getSplineForPlayerPosition (playerTransform.position);

                        newPosition = new Vector3 (newPosition.x, playerTransform.position.y, newPosition.z);

                        playerTransform.position = newPosition;

                    } else {
                        if (moveInXAxisOn2_5d) {
                            playerTransform.position =
                                new Vector3 (playerTransform.position.x,
                                playerTransform.position.y,
                                playerCameraManager.getOriginalLockedCameraPivotPosition ().z);
                        } else {
                            playerTransform.position =
                                new Vector3 (playerCameraManager.getOriginalLockedCameraPivotPosition ().x,
                                playerTransform.position.y,
                                playerTransform.position.z);
                        }
                    }
                }
            }
        }

        if (gravityForcePaused) {
            if (unPauseGravityForceActive && Time.time > lastTimeGravityForcePaused + gravityForcePausedTime) {
                setGravityForcePuase (false);

                unPauseGravityForceActive = false;
            }
        }

        bool checkLookCameraDirectionResult =
            !crouching ||
            lookInCameraDirectionOnCrouchState ||
            fullBodyAwarenessActive ||
            !playerUsingWeapons;

        lookInCameraDirectionActive = hasToLookInCameraDirection () && checkLookCameraDirectionResult;

        strafeModeActive = ((playerIsAiming && lookInCameraDirectionOnFreeFireActive) || lookInCameraDirectionActive) &&
        (playerOnGround || !ignoreStrafeStateOnAirEnabled || fullBodyAwarenessActive);


        if (previousStrafeModeActive != strafeModeActive) {
            previousStrafeModeActive = strafeModeActive;

            if (deactivateRootMotionOnStrafeActiveOnLockedView) {
                setUseRootMotionActiveState (!strafeModeActive);
            }
        }

        if (!fullBodyAwarenessActive) {
            if (originalLookAlwaysInCameraDirection && disableLookAlwaysInCameraDirectionIfIncreaseWalkSpeedActive) {
                if (sprintEnabled) {
                    if (running) {
                        if (originalLookAlwaysInCameraDirection == lookAlwaysInCameraDirection) {
                            setLookAlwaysInCameraDirectionState (false);
                        }
                    } else {
                        if (originalLookAlwaysInCameraDirection != lookAlwaysInCameraDirection) {
                            setLookAlwaysInCameraDirectionState (true);
                        }
                    }
                } else {
                    if (walkSpeed == 1) {
                        if (originalLookAlwaysInCameraDirection == lookAlwaysInCameraDirection) {
                            setLookAlwaysInCameraDirectionState (false);
                        }
                    } else {
                        if (originalLookAlwaysInCameraDirection != lookAlwaysInCameraDirection) {
                            setLookAlwaysInCameraDirectionState (true);
                        }
                    }
                }
            }

            if (disableStrafeModeExternallyIfIncreaseWalkSpeedActive) {

                bool disableStrafeModeResult = true;

                if (lockedCameraActive && isPlayerMovingOn3dWorld ()) {
                    if (playerCameraManager.isPlayerLookingAtTarget () && playerCameraManager.isLookintAtTargetByInput ()) {
                        disableStrafeModeResult = false;
                    }
                }

                if (disableStrafeModeResult) {
                    if (sprintEnabled) {
                        if (running) {
                            if (!currentStrafeModeExternallyActive) {
                                setLookAlwaysInCameraDirectionState (false);

                                currentStrafeModeExternallyActive = true;
                            }
                        } else {
                            if (currentStrafeModeExternallyActive) {
                                setLookAlwaysInCameraDirectionState (true);

                                currentStrafeModeExternallyActive = false;
                            }
                        }
                    } else {
                        if (walkSpeed == 1) {
                            if (!currentStrafeModeExternallyActive) {
                                setLookAlwaysInCameraDirectionState (false);

                                currentStrafeModeExternallyActive = true;
                            }
                        } else {
                            if (currentStrafeModeExternallyActive) {
                                setLookAlwaysInCameraDirectionState (true);

                                currentStrafeModeExternallyActive = false;
                            }
                        }
                    }
                }
            }
        }

        if (!usedByAI) {
            if (running) {
                //stop the running action if the player is not moving
                if (getMoveInputDirection ().magnitude == 0) {

                    if (Time.time > lastTimeMovingOnRun + 0.26f) {
                        stopRun ();
                    }
                }

                if (isPlayerMoving (0.05f) || isPlayerUsingInput ()) {
                    lastTimeMovingOnRun = Time.time;
                } else {
                    lastTimeMovingOnRun = 0;
                }
            }
        }
    }

    void FixedUpdate ()
    {
        currentFixedUpdateDeltaTime = getCurrentDeltaTime ();

        playerTransformUp = playerTransform.up;

        playerTransformForward = playerTransform.forward;

        playerTransformRight = playerTransform.right;

        if (jumpButtonHoldActive) {
            if (Time.time > lastTimeJumpButtonHoldActive + addJumpForceDuration) {
                jumpButtonHoldActive = false;
            } else {
                if (Time.time > lastTimeJumpButtonHoldActive + minWaitToAddJumpForceAfterButtonPressed) {
                    Vector3 totalExtraJump = jumpForceAmontWhileButtonPressed * (mainRigidbody.mass * jumpMassMultiplier) * currentNormal;

                    mainRigidbody.AddForce (totalExtraJump);
                }
            }
        }

        //convert the input from keyboard or a touch screen into values to move the player, given the camera direction
        if (canMove && !usedByAI && !playerNavMeshEnabled && !moveIputPaused) {
            setMainAxisValues ();

            horizontalInput = axisValues.x;
            verticalInput = axisValues.y;
        } else {
            axisValues = Vector2.zero;
        }

        if (canMove && !moveIputPaused) {
            setMainRawAxisValues ();
        } else {
            rawAxisValues = Vector2.zero;
        }

        if (moveInputPausedWithDurationActive) {
            axisValues = Vector2.zero;

            rawAxisValues = Vector2.zero;

            horizontalInput = axisValues.x;
            verticalInput = axisValues.y;

            if (Time.time > moveInputPausedWithDurationAmount + lastTimeMoveInputPausedWithDurationActive) {
                moveInputPausedWithDurationActive = false;
            }
        }

        if (fullBodyAwarenessActive && !useRootMotionOnFullBodyAwareness) {
            if (canMove) {
                if (lastTimeActionActive != 0) {
                    if (Time.time < lastTimeActionActive + 0.25f) {
                        verticalInput = 0;
                        horizontalInput = 0;

                        axisValues = Vector2.zero;

                        rawAxisValues = Vector2.zero;
                    }
                }
            }
        }

        if (!usedByAI && !playerNavMeshEnabled) {
            //get the axis of the player camera, to move him properly
            if (lockedCameraActive) {

                currentLockedCameraTransform = playerCameraManager.getLockedCameraTransform ();

                if (isPlayerMovingOn3dWorld ()) {

                    //player can move while aiming
                    if (!canMoveWhileAimLockedCamera && playerIsAiming) {
                        verticalInput = 0;
                        horizontalInput = 0;
                    }

                    //if the player is on tank mode, use his forward and right direction as the input
                    if (useTankControls) {
                        currentForwardDirection = playerTransformForward;
                        currentRightDirection = playerTransformRight;
                    } else {
                        //else, the player will follow the direction of the current locked camera 

                        //if the player is looking at a target, the input direction used will be the player camera
                        if (playerCameraManager.isPlayerLookingAtTarget ()) {
                            if (!isPlayerOnGround ()) {
                                currentForwardDirection = currentLockedCameraTransform.forward;
                                currentRightDirection = currentLockedCameraTransform.right;
                            } else {
                                currentForwardDirection = playerCameraTransform.forward;
                                currentRightDirection = mainCameraTransform.right;
                            }
                        } else {
                            //else, he will use the locked camera direction
                            currentForwardDirection = currentLockedCameraTransform.forward;
                            currentRightDirection = currentLockedCameraTransform.right;
                        }
                    }
                } else {
                    //else, the player is moving in 2.5d camera, so use only the horizontal input

                    currentForwardDirection = vector3Zero;
                    currentRightDirection = currentLockedCameraTransform.right;

                    if (followSplinePathActive) {
                        currentRightDirection = current2_5dRightDirection;
                    }

                    if (playerOnGround) {
                        verticalInput = 0;

                        rawAxisValues.y = 0;
                    }
                }
            } else {
                //the player is on free camera mode

                movementInputIgnoredOnAimActive = false;

                //check if the player can move while aiming on first or third person according to settings
                if (playerIsAiming) {
                    if (usingFreeFireMode) {
                        if (!canMoveWhileFreeFireOnThirdPerson) {
                            if (!firstPersonActive) {
                                verticalInput = 0;
                                horizontalInput = 0;

                                movementInputIgnoredOnAimActive = true;
                            }
                        }
                    } else {
                        if ((firstPersonActive && !canMoveWhileAimFirstPerson) ||
                            (!firstPersonActive && !canMoveWhileAimThirdPerson)) {

                            verticalInput = 0;
                            horizontalInput = 0;

                            movementInputIgnoredOnAimActive = true;
                        }
                    }
                }

                //in other case, the player uses the player camera direction as input
                if (checkCameraDirectionFromLockedToFree) {
                    currentLockedCameraTransform = playerCameraManager.getLockedCameraTransform ();
                    //keep the latest locked camera direction until the player stops to move

                    currentForwardDirection = currentLockedCameraTransform.forward;
                    currentRightDirection = currentLockedCameraTransform.right;

                    if (!isPlayerMoving (0.6f) && !playerUsingInput) {
                        inputNotPressed = true;
                    }

                    if (inputNotPressed && (playerUsingInput || !isPlayerMoving (0))) {
                        checkCameraDirectionFromLockedToFree = false;

                        inputNotPressed = false;
                    }
                } else {
                    currentForwardDirection = playerCameraTransform.forward;
                    currentRightDirection = mainCameraTransform.right;

                    if (useForwardDirectionForCameraDirection) {
                        currentForwardDirection = playerTransform.forward;
                    }

                    if (useRightDirectionForCameraDirection) {
                        currentRightDirection = playerTransform.right;
                    }

                    if (ignoreCameraDirectionOnMovement) {
                        currentForwardDirection = lastForwardDirection;
                        currentRightDirection = lastRigthDirection;
                    }
                }
            }

            //the camera direccion and input is override by some external function, to make the playe to move toward some direction or position
            if (overrideMainCameraTransformActive) {
                currentForwardDirection = overrideMainCameraTransform.forward;
                currentRightDirection = overrideMainCameraTransform.right;
            }

            moveInput = (verticalInput * currentForwardDirection + horizontalInput * currentRightDirection) * walkSpeed;

        } else {
            if (playerNavMeshEnabled) {
                currentLockedCameraTransform = playerCameraManager.getLockedCameraTransform ();
            }

            if (moveIputPaused ||
                (actionActive && !useRootMotionActive)) {
                navMeshMoveInput = vector3Zero;
            }

            updateOverrideInputValues (new Vector2 (navMeshMoveInput.x, navMeshMoveInput.z), true);

            setAIMainAxisValues ();

            setMainAIRawAxisValues ();

            if (playerIsAiming || AIStrafeModeActive) {
                if (AIEnableInputMovementOnStrafe) {
                    moveInput = playerTransform.InverseTransformDirection (navMeshMoveInput);

                    horizontalInput = moveInput.x;
                    verticalInput = moveInput.z;
                } else {
                    moveInput = navMeshMoveInput;

                    horizontalInput = 0;
                    verticalInput = moveInput.magnitude;
                }
            } else {
                moveInput = navMeshMoveInput;

                horizontalInput = moveInput.x;
                verticalInput = moveInput.z;
            }

            if (!canMoveAI) {
                moveInput = vector3Zero;

                horizontalInput = 0;
                verticalInput = 0;
            }

            if (useNewGroundAdherenceEnabled) {
                if (actionActive && !canMove) {
                    moveInput = vector3Zero;

                    horizontalInput = 0;
                    verticalInput = 0;
                }
            }
        }

        currentMoveInput = moveInput;

        if (useObstacleDetectionToAvoidMovement && playerOnGround) {
            checkObstacleToAvoidMovement ();
        }

        //		if (useWalkToRunAccelerationChange) {
        //			currentWalkSpeedTarget = 1;
        //
        //			if (obstacleToAvoidMovementFound || !isPlayerUsingInput ()) {
        //				currentWalkSpeedTarget = originalWalkSpeedValue;
        //			} 
        //
        //			walkSpeed = Mathf.Lerp (walkSpeed, currentWalkSpeedTarget, Time.deltaTime * walkToRunAccelerationSpeed);
        //		}

        //isMoving is true if the player is moving, else is false
        isMoving = Mathf.Abs (horizontalInput) > 0.1f || Mathf.Abs (verticalInput) > 0.1f;

        if (currentMoveInput.magnitude > 1) {
            currentMoveInput.Normalize ();
        }

        //get the velocity of the rigidbody
        if (!gravityPowerActive) {
            if ((usingAnimator && !useFirstPersonPhysicsInThirdPersonActive) ||
                (!usingAnimator && !playerOnGround) ||
                (usingAnimator && useFirstPersonPhysicsInThirdPersonActive && !playerOnGround)) {

                currentVelocity = mainRigidbody.linearVelocity;
            }
        }

        //convert the global movement in local
        getMoveInput ();

        if (fullBodyAwarenessActive) {
            if (wallRunningActive) {
                updateWallRunningState ();
            }
        } else {
            //look in camera direction when the player is aiming
            lookCameraDirection ();
        }

        //add an extra rotation to the player to get a better control of him
        addExtraRotation ();

        bool updateThirdPersonState = usingAnimator && !useFirstPersonPhysicsInThirdPersonActive;

        //if the animator is used, then
        if (updateThirdPersonState) {

            //check when the player is on ground
            checkOnGround ();

            //update mecanim
            updateAnimator ();
        } else {
            //else, apply force to the player's rigidbody
            if (playerOnGround) {
                if (!isMoving) {
                    currentMoveInput = vector3Zero;
                }

                if (running) {
                    if (noAnimatorCanRun) {
                        if (verticalInput < 0) {
                            if (noAnimatorCanRunBackwards) {
                                if (crouching) {
                                    noAnimatorCurrentMovementSpeed = noAnimatorRunCrouchBackwardMovementSpeed;
                                } else {
                                    noAnimatorCurrentMovementSpeed = noAnimatorRunBackwardMovementSpeed;
                                }
                            } else {
                                stopRun ();
                            }
                        } else {
                            if (crouching) {
                                if (crouchSlidingActive) {
                                    noAnimatorCurrentMovementSpeed = noAnimatorCrouchSlidingSpeed;

                                    if (keepCrouchSlidingOnInclinatedSurface) {
                                        if (hitAngle > minInclinitaonSurfaceAngleToCrouchcSliding) {
                                            if (Physics.Raycast (playerTransform.position + (playerTransformForward / 2), -playerTransformUp, out hit, 1, layer)) {
                                                if (Mathf.Abs (playerTransform.InverseTransformPoint (hit.point).y) > 0.1f) {

                                                    lastTimeCrouchSlidingActive = Time.time;
                                                }
                                            }
                                        }
                                    }

                                    if (Time.time > lastTimeCrouchSlidingActive + noAnimatorCrouchSlidingLerpDelay) {
                                        currentNoAnimatorSlidingCrouchSpeed =
                                            Mathf.Lerp (currentNoAnimatorSlidingCrouchSpeed, noAnimatorRunCrouchMovementSpeed, Time.fixedDeltaTime * noAnimatorCrouchSlidingLerpSpeed);

                                        noAnimatorCurrentMovementSpeed = currentNoAnimatorSlidingCrouchSpeed;
                                    } else {
                                        currentNoAnimatorSlidingCrouchSpeed = noAnimatorCrouchSlidingSpeed;
                                    }

                                    if (Time.time > lastTimeCrouchSlidingActive + noAnimatorCrouchSlidingDuration) {
                                        stopCrouchSliding ();

                                        if (getUpAfterCrouchSlidingEnd && crouching) {
                                            crouch ();
                                        }
                                    }
                                } else {
                                    noAnimatorCurrentMovementSpeed = noAnimatorRunCrouchMovementSpeed;
                                }
                            } else {
                                noAnimatorCurrentMovementSpeed = noAnimatorRunMovementSpeed;
                            }
                        }
                    } else {
                        stopRun ();
                    }
                } else if (crouching) {
                    if (verticalInput >= 0) {
                        noAnimatorCurrentMovementSpeed = noAnimatorCrouchMovementSpeed;
                    } else {
                        noAnimatorCurrentMovementSpeed = noAnimatorCrouchBackwardMovementSpeed;
                    }
                } else if (Mathf.Abs (verticalInput) > half && Mathf.Abs (horizontalInput) > half) {
                    if (verticalInput >= 0) {
                        noAnimatorCurrentMovementSpeed = noAnimatorStrafeMovementSpeed;
                    } else {
                        noAnimatorCurrentMovementSpeed = noAnimatorStrafeBackwardMovementSpeed;
                    }
                } else {
                    if (verticalInput >= 0) {
                        noAnimatorCurrentMovementSpeed = noAnimatorWalkMovementSpeed;
                    } else {
                        noAnimatorCurrentMovementSpeed = noAnimatorWalkBackwardMovementSpeed;
                    }
                }

                noAnimatorCurrentForce = (noAnimatorSpeed * noAnimatorCurrentMovementSpeed) * currentMoveInput;

                // Subtract the local Y axis velocity of the rigidbody
                noAnimatorCurrentForce = noAnimatorCurrentForce - playerTransform.InverseTransformDirection (noAnimatorCurrentForce).y * playerTransformUp;

                noAnimatorCurrentForce *= noAnimatorMovementSpeedMultiplier;

                velocityChange = noAnimatorCurrentForce - mainRigidbody.linearVelocity;

                velocityChange = Vector3.ClampMagnitude (velocityChange, maxVelocityChange);

                mainRigidbody.AddForce (velocityChange, ForceMode.VelocityChange);
            } else {
                if (!wallRunningActive &&
                    !gravityPowerActive &&
                    !pauseAllPlayerDownForces &&
                    !externalForceOnAirActive &&
                    !externalControlBehaviorForAirTypeActive &&
                    !swimModeActive) {

                    if (crouchSlidingOnAirActive) {
                        noAnimatorCurrentMovementSpeed = noAnimatorCrouchSlidingOnAirSpeed;

                        if (useNoAnimatorCrouchSlidingDurationOnAir && Time.time > lastTimeCrouchSlidingActive + noAnimatorCrouchSlidingDurationOnAir) {
                            stopCrouchSliding ();
                        }
                    } else {
                        noAnimatorCurrentMovementSpeed = noAnimatorAirSpeed;
                    }

                    noAnimatorCurrentForce = noAnimatorCurrentMovementSpeed * currentMoveInput + playerTransform.InverseTransformDirection (currentVelocity).y * playerTransformUp;

                    noAnimatorCurrentForce *= noAnimatorMovementSpeedMultiplier;

                    velocityChange = noAnimatorCurrentForce - mainRigidbody.linearVelocity;

                    velocityChange = Vector3.ClampMagnitude (velocityChange, maxVelocityChange);

                    mainRigidbody.AddForce (velocityChange, ForceMode.VelocityChange);
                }

                if (externalForceOnAirActive) {
                    velocityChange = externalForceOnAirValue - mainRigidbody.linearVelocity;

                    velocityChange = Vector3.ClampMagnitude (velocityChange, maxVelocityChange);

                    mainRigidbody.AddForce (velocityChange, ForceMode.VelocityChange);

                    externalForceOnAirActive = false;
                }
            }

            if (!gravityPowerActive) {
                currentVelocity = mainRigidbody.linearVelocity;
            }

            currentVelocityChangeMagnitude = mainRigidbody.linearVelocity.magnitude;

            checkOnGround ();
        }

        //check if the player is on ground or in air
        //also set the friction of the character if he is on the ground or in the air
        if (playerOnGround) {

            onGroundVelocity ();

            if (!onGroundChecked) {
                gravityManager.onGroundOrOnAir (true);

                onGroundChecked = true;

                if (updateHeadbobState && headBobManagerLocated) {
                    //send a message to the headbob in the camera, when the player lands from a jump
                    if (currentHeadbobState != headbobState.Jump_End) {
                        headBobManager.setState ("Jump End");

                        currentHeadbobState = headbobState.Jump_End;
                    }
                }

                if (updateFootStepStateActive) {
                    if (currentFootStepState != footStepState.Air_Landing) {
                        stepManager.setFootStepState ("Air Landing");

                        currentFootStepState = footStepState.Air_Landing;
                    }
                }

                //set the number of jumps made by the player since this moment
                jumpsAmount = 0;

                if (weaponsManagerLocated) {
                    weaponsManager.setWeaponsJumpEndPositionState (true);
                }

                if (!isPlayerDead () && canRagdollMove) {
                    //check the last time since the player is in the air, falling in its gravity direction
                    //if the player has been in the air more time than maxTimeInAirBeforeGetDamage and his velocity is higher than 15, then apply damage
                    if (fallDamageEnabled && !gravityPowerActive && !fallDamageCheckPaused) {
                        currentFallSpeed = Mathf.Abs (mainRigidbody.linearVelocity.magnitude);

                        float lastFrameAirVelocityABS = Mathf.Abs (lastFrameAirVelocity);

                        checkFallState = false;

                        if (firstPersonActive) {
                            if (currentFallSpeed > minPlayerVelocityToApplyDamageFirstPerson ||
                                lastFrameAirVelocityABS > minPlayerVelocityToApplyDamageFirstPerson) {
                                checkFallState = true;
                            }
                        } else {
                            if (currentFallSpeed > minPlayerVelocityToApplyDamageThirdPerson ||
                                lastFrameAirVelocityABS > minPlayerVelocityToApplyDamageThirdPerson) {
                                checkFallState = true;
                            }
                        }

                        if (checkFallStatePaused) {
                            checkFallState = false;
                        }

                        if (checkFallState) {
                            currentLastTimeFalling = lastTimeFalling;

                            if (applyFallDamageEnabled && !fallDamageCheckOnHealthPaused) {
                                if (Time.time > currentLastTimeFalling + maxTimeInAirBeforeGettingDamage) {
                                    //get the last time since the player is in the air and his velocity, and call the health damage function
                                    float totalTimeOnAir = Mathf.Abs (Time.time - currentLastTimeFalling);

                                    float damageValue = totalTimeOnAir * currentFallSpeed;

                                    if (fallingDamageMultiplier != 1) {
                                        damageValue *= fallingDamageMultiplier;
                                    }

                                    float remainingHealthAmount = healthManager.getCurrentHealthAmount ();

                                    if (ignoreShieldOnFallDamage) {
                                        damageValue = Mathf.Clamp (damageValue, 0, remainingHealthAmount);
                                    } else {
                                        if (healthManager.isUseShieldActive ()) {
                                            float remainShield = healthManager.getCurrentShieldAmount ();

                                            damageValue = Mathf.Clamp (damageValue, 0, remainingHealthAmount + remainShield);
                                        } else {
                                            damageValue = Mathf.Clamp (damageValue, 0, remainingHealthAmount);
                                        }
                                    }

                                    if (damageValue > 0) {
                                        healthManager.setDamageWithHealthManagement (damageValue, -mainRigidbody.linearVelocity.normalized, playerTransform.position + playerTransformUp,
                                            gameObject, gameObject, false, false, ignoreShieldOnFallDamage, false, false, false, -2, damageTypeIDOnFallDamage);
                                    }
                                }
                            }

                            //call another function when the player receives damage from a long fall
                            if (callEventOnFallDamage && (!isPlayerDead () || !callEventOnlyIfPlayerAlive)) {
                                if (Time.time > currentLastTimeFalling + minTimeOnAirToUseEvent) {
                                    eventOnFallDamage.Invoke ();
                                }
                            }
                        }
                    }
                }

                if (freeFloatingModeOn || zeroGravityModeOn) {
                    setFootStepManagerState (false);
                }

                lastTimeGround = Time.time;

                if (wallRunningActive) {
                    setWallRunningActiveState (false);
                }

                if (crouchingOnAir && !crouching) {
                    crouching = true;

                    crouchingOnAir = false;

                    crouchSlidingOnAirActive = false;
                } else {
                    if (disableCrouchState && !firstPersonActive) {
                        crouching = false;

                        disableCrouchState = false;
                    }
                }

                if (disableRootMotionTemporalyOnLandThirdPerson) {
                    if (!firstPersonActive) {
                        if (!checkingDisableRootMotionTemporaly) {
                            checkingDisableRootMotionTemporaly = true;
                        }
                    }
                }

                jumpButtonHoldActive = false;

                if (customCharacterControllerActive) {
                    currentCustomCharacterControllerBase.updateOnGroundValue (true);
                }

                if (stopCrouchFromAutoJumpActive) {
                    crouching = false;

                    stopCrouchFromAutoJumpActive = false;
                }

                if (jumpActivatedByInput) {
                    if (useEventOnLandFromJumpInput) {
                        eventOnLandFromJumpInput.Invoke ();
                    }

                    jumpActivatedByInput = false;
                }

                if (useEventOnLand) {
                    eventOnLand.Invoke ();
                }

                if (keepLastMovementInputOnJump) {
                    resetLastMoveInputOnJumpValue ();
                }
            }

            if (checkingDisableRootMotionTemporaly) {
                if (firstPersonActive) {
                    if (rootMotionDisabledTemporaly) {
                        checkingDisableRootMotionTemporaly = false;
                        rootMotionDisabledTemporaly = false;

                        setOriginalUseRootMotionActiveState ();
                    }
                } else {
                    if (!rootMotionDisabledTemporaly) {
                        if (Time.time > lastTimeGround + waitToDisableRootMotionTemporalyOnLandThirdPerson) {
                            setUseRootMotionActiveState (false);

                            lastTimeRootMotionDisabledTemporaly = Time.time;

                            rootMotionDisabledTemporaly = true;
                        }
                    } else {
                        if (Time.time > lastTimeRootMotionDisabledTemporaly + durationDisableRootMotionTemporalyOnLandThirdPerson) {
                            checkingDisableRootMotionTemporaly = false;
                            rootMotionDisabledTemporaly = false;

                            setOriginalUseRootMotionActiveState ();
                        }
                    }
                }
            }

            if (updateFootStepStateActive) {
                if (playerOnGround) {
                    setCurrentFootStepsState ();
                }
            }

            //change the collider material when the player moves and when the player is not moving
            if (!physicMaterialAssigmentPaused) {
                if (currentMoveInput.magnitude == 0) {
                    if (!highFrictionMaterialActive) {
                        setHighFrictionMaterial ();
                    }
                } else {
                    if (highFrictionMaterialActive) {
                        setZeroFrictionMaterial ();
                    }
                }
            }

            //check the headbob state
            if (updateHeadbobState && headBobManagerLocated) {
                if (headBobManager.headBobEnabled && !setHeadbodStatesPaused) {
                    setCurrentHeadBobState ();
                }
            }
        }

        //the player is in the air, so
        else {
            //call the air velocity function
            onAirVelocity ();

            if (onGroundChecked) {
                //set in other script this state
                gravityManager.onGroundOrOnAir (false);

                onGroundChecked = false;

                setLastTimeFalling ();

                if (weaponsManagerLocated) {
                    weaponsManager.setWeaponsJumpStartPositionState (true);
                }

                currentJumpLeg = 0;

                if (zeroGravityModeOn || freeFloatingModeOn) {
                    if (!ignoreSetCheckOnGrundStatePausedFFOrZGStateActive) {
                        setCheckOnGrundStatePausedFFOrZGState (true);

                        setFootStepManagerState (true);
                    }
                }

                if (customCharacterControllerActive) {
                    currentCustomCharacterControllerBase.updateOnGroundValue (false);
                }

                ignoreSetCheckOnGrundStatePausedFFOrZGStateActive = false;

                lastTimeOnAirAnimator = Time.time;

                if (lastTimeJumpActivated == 0 || Time.time > lastTimeJumpActivated + 0.7f) {
                    if (useEventOnAirborne) {
                        eventOnAirborne.Invoke ();
                    }
                }
            }

            if (highFrictionMaterialActive) {
                setZeroFrictionMaterial ();
            }

            if (updateHeadbobState && headBobManagerLocated) {
                if (!wallRunningActive && !externalControlBehaviorForAirTypeActive) {
                    if (currentHeadbobState != headbobState.Air) {

                        headBobManager.setState ("Air");


                        currentHeadbobState = headbobState.Air;
                    }

                    if (headBobManager.useDynamicIdle) {
                        setLastTimeMoved ();
                    }
                }
            }

            if (!isPlayerDead () && canRagdollMove) {
                if (activateRagdollOnFallState) {
                    currentFallSpeed = playerTransform.InverseTransformDirection (mainRigidbody.linearVelocity).y;

                    checkFallState = false;

                    if (currentFallSpeed < 0 && Mathf.Abs (currentFallSpeed) > minSpeedToActivateRagdollOnFall) {
                        checkFallState = true;
                    }

                    if (checkFallStatePaused) {
                        checkFallState = false;
                    }

                    if (checkFallState) {
                        currentLastTimeFalling = lastTimeFalling;

                        if (Time.time > currentLastTimeFalling + minWaitTimeToActivateRagdollOnFall) {
                            //get the last time since the player is in the air and his velocity, and call the health damage function
                            //call another function when the player receives damage from a long fall
                            eventToActivateRagdollOnFall.Invoke (pushRagdollMultiplierOnFall * mainRigidbody.linearVelocity);
                        }
                    }
                }
            }
        }

        if (externalControllBehaviorActive) {
            currentExternalControllerBehavior.updateControllerBehavior ();
        }

        if (customCharacterControllerActive) {
            currentCustomCharacterControllerBase.updateCharacterControllerState ();
        }

        //in case the player is using the gravity power, the update of the rigidbody velocity stops
        if (!gravityPowerActive) {
            if ((usingAnimator && !useFirstPersonPhysicsInThirdPersonActive) ||
                (!usingAnimator && !playerOnGround) ||
                (usingAnimator && useFirstPersonPhysicsInThirdPersonActive && !playerOnGround)) {

                if ((useMaxFallSpeed || useMaxFallSpeedExternallyActive) && !playerOnGround) {

                    float verticalSpeed = playerTransform.InverseTransformDirection (currentVelocity).y;

                    if (verticalSpeed < 0) {

                        Vector3 horizontalSpeed = currentVelocity - verticalSpeed * playerTransformUp;

                        float currentMaxFallSpeed = -maxFallSpeed;

                        if (customMaxFallSpeedExternally != 0) {
                            currentMaxFallSpeed = -customMaxFallSpeedExternally;
                        }

                        float verticalSpeedClamped = Mathf.Clamp (verticalSpeed, currentMaxFallSpeed, 100);

                        horizontalSpeed += verticalSpeedClamped * playerTransformUp;

                        currentVelocity = horizontalSpeed;
                    }
                }

                if (!pauseMainRigidbodyVelocityUpdate) {
                    mainRigidbody.linearVelocity = currentVelocity;
                }
            }

            if (externalForceActive) {
                mainRigidbody.linearVelocity += externalForceValue;

                externalForceActive = false;
            }
        }

        if (useLandMark && !externalControllBehaviorActive) {
            updateLandMark ();
        }
    }

    bool pauseMainRigidbodyVelocityUpdate;

    public void setPauseMainRigidbodyVelocityUpdateState (bool state)
    {
        pauseMainRigidbodyVelocityUpdate = state;
    }

    public void setUseMaxFallSpeedExternallyActiveState (bool state)
    {
        useMaxFallSpeedExternallyActive = state;
    }

    public void setCustomMaxFallSpeedExternally (float newValue)
    {
        customMaxFallSpeedExternally = newValue;
    }

    public virtual void setMainAxisValues ()
    {
        axisValues = playerInput.getPlayerMovementAxis ();
    }

    public virtual void setMainRawAxisValues ()
    {
        rawAxisValues = playerInput.getPlayerRawMovementAxis ();
    }

    public virtual void setCustomAxisValues (Vector2 newValue)
    {

    }

    public virtual void setAIMainAxisValues ()
    {
        axisValues = playerInput.getPlayerMovementAxisWithoutCheckingEnabled ();
    }

    public virtual void setMainAIRawAxisValues ()
    {
        rawAxisValues = playerInput.getPlayerRawMovementAxisWithoutCheckingEnabled ();
    }

    public virtual void updateOverrideInputValues (Vector2 inputValues, bool state)
    {
        playerInput.overrideInputValues (inputValues, state);
    }

    public virtual void setNewMainCameraTransform (Transform newTransform)
    {
        mainCameraTransform = newTransform;
    }

    public virtual void setNewPlayerCameraTransform (Transform newTransform)
    {
        playerCameraTransform = newTransform;
    }

    public void setDisableStrafeModeExternallyIfIncreaseWalkSpeedActiveState (bool state)
    {
        disableStrafeModeExternallyIfIncreaseWalkSpeedActive = state;

        currentStrafeModeExternallyActive = false;
    }

    public void checkObstacleToAvoidMovement ()
    {
        checkObstacleRaycastPosition = currentPlayerPosition + (((currentHeight / 2) + obstacleDetectionRaycastHeightOffset) * playerTransformUp);

        if (lockedCameraActive && useRelativeMovementToLockedCamera) {
            checkObstacleRaycastDirection = lockedCameraMoveInput;
        } else {
            checkObstacleRaycastDirection = moveInput;
        }

        checkObstacleRaycastDirection.Normalize ();

        obstacleDetectedAtRight = false;
        obstacleDetectedAtLeft = false;

        checkObstacleRaycastPosition += obstacleDetectionRaycastDistanceRightAndLeft * playerTransformRight;

        if (Physics.Raycast (checkObstacleRaycastPosition, checkObstacleRaycastDirection, out hit, obstacleDetectionToAvoidMovementRaycastDistance, obstacleDetectionToAvoidMovementLayermask)) {
            obstacleDetectedAtRight = true;

            if (showGizmo) {
                Debug.DrawRay (checkObstacleRaycastPosition, obstacleDetectionToAvoidMovementRaycastDistance * checkObstacleRaycastDirection, Color.red);
            }
        } else {
            if (showGizmo) {
                Debug.DrawRay (checkObstacleRaycastPosition, obstacleDetectionToAvoidMovementRaycastDistance * checkObstacleRaycastDirection, Color.green);
            }
        }

        checkObstacleRaycastPosition -= (obstacleDetectionRaycastDistanceRightAndLeft * 2) * playerTransformRight;

        if (Physics.Raycast (checkObstacleRaycastPosition, checkObstacleRaycastDirection, out hit, obstacleDetectionToAvoidMovementRaycastDistance, obstacleDetectionToAvoidMovementLayermask)) {
            obstacleDetectedAtLeft = true;

            if (showGizmo) {
                Debug.DrawRay (checkObstacleRaycastPosition, obstacleDetectionToAvoidMovementRaycastDistance * checkObstacleRaycastDirection, Color.red);
            }
        } else {
            if (showGizmo) {
                Debug.DrawRay (checkObstacleRaycastPosition, obstacleDetectionToAvoidMovementRaycastDistance * checkObstacleRaycastDirection, Color.green);
            }
        }

        if (obstacleDetectedAtRight || obstacleDetectedAtLeft) {

            obstacleToAvoidMovementFound = true;
        } else {

            obstacleToAvoidMovementFound = false;
        }

        if (obstacleToAvoidMovementFound) {
            currentMoveInput = vector3Zero;
        }
    }

    public bool isObstacleToAvoidMovementFound ()
    {
        return obstacleToAvoidMovementFound;
    }

    public Vector3 getCurrentForwardDirection ()
    {
        return currentForwardDirection;
    }

    public Vector3 getCurrentRightDirection ()
    {
        return currentRightDirection;
    }

    public bool useSmoothPlayerRotationAxisOn2_5dEnabled = true;

    public bool isUseSmoothPlayerRotationAxisOn2_5dEnabled ()
    {
        return useSmoothPlayerRotationAxisOn2_5dEnabled;
    }

    Vector3 current2_5dRightDirection;
    Vector3 current2_5dLeftDirection;

    public Vector3 getCurrent2_5dRightDirection ()
    {
        return current2_5dRightDirection;
    }

    //convert the global movement into local movement
    void getMoveInput ()
    {
        Vector3 localMove = playerTransform.InverseTransformDirection (currentMoveInput);

        //get the amount of rotation added to the character mecanim
        if (currentMoveInput.magnitude > 0) {
            turnAmount = Mathf.Atan2 (localMove.x, localMove.z);
        } else {
            turnAmount = Mathf.Atan2 (0, 0);
        }

        //adjust player orientation to lef or right according to the input direction pressed just once
        if (!usedByAI && isPlayerMovingOn2_5dWorld ()) {
            if (horizontalInput > 0) {
                rotatingPlayerIn2_5dToRight = true;
                rotatingPlayerIn2_5dToRLeft = false;
            }

            if (horizontalInput < 0) {
                rotatingPlayerIn2_5dToRLeft = true;
                rotatingPlayerIn2_5dToRight = false;
            }

            if (Mathf.Abs (horizontalInput) == 1) {
                rotatingPlayerIn2_5dToRLeft = false;
                rotatingPlayerIn2_5dToRight = false;
            }

            current2_5dRightDirection = currentLockedCameraTransform.right;
            current2_5dLeftDirection = -currentLockedCameraTransform.right;

            if (followSplinePathActive) {
                Vector3 lookDirection = playerCameraManager.getSplineForwardDirection (playerTransform.position);

                current2_5dRightDirection = lookDirection;
                current2_5dLeftDirection = -lookDirection;
            }

            if (rotatingPlayerIn2_5dToRight) {
                float angle = Vector3.SignedAngle (playerTransformForward, current2_5dRightDirection, playerTransformUp);

                if (Mathf.Abs (angle) > 5) {
                    turnAmount = angle * Mathf.Deg2Rad;
                } else {
                    turnAmount = 0;

                    rotatingPlayerIn2_5dToRight = false;
                }
            }

            if (rotatingPlayerIn2_5dToRLeft) {
                float angle = Vector3.SignedAngle (playerTransformForward, current2_5dLeftDirection, playerTransformUp);

                if (Mathf.Abs (angle) > 5) {
                    turnAmount = angle * Mathf.Deg2Rad;
                } else {
                    turnAmount = 0;

                    rotatingPlayerIn2_5dToRLeft = false;
                }
            }

            if (playerIsAiming) {
                if (horizontalInput != 0 || !useSmoothPlayerRotationAxisOn2_5dEnabled) {
                    turnAmount = 0;

                    rotatingPlayerIn2_5dToRLeft = false;
                    rotatingPlayerIn2_5dToRight = false;
                }

            } else {
                if (!useSmoothPlayerRotationAxisOn2_5dEnabled) {
                    if (rotatingPlayerIn2_5dToRLeft) {
                        playerTransform.rotation = Quaternion.LookRotation (current2_5dLeftDirection);
                        playerCameraTransform.rotation = playerTransform.rotation;

                        turnAmount = 0;

                        rotatingPlayerIn2_5dToRLeft = false;
                        rotatingPlayerIn2_5dToRight = false;
                    } else if (rotatingPlayerIn2_5dToRight) {
                        playerTransform.rotation = Quaternion.LookRotation (current2_5dRightDirection);
                        playerCameraTransform.rotation = playerTransform.rotation;

                        turnAmount = 0;

                        rotatingPlayerIn2_5dToRLeft = false;
                        rotatingPlayerIn2_5dToRight = false;
                    }
                }
            }

            //crouch using vertical input
            if (crouchVerticalInput2_5dEnabled) {
                if ((!crouching && verticalInput < 0) || (crouching && verticalInput > 0)) {
                    crouch ();
                }
            }

            //move vertically on free floating mode or zero gravity mode
            if (freeFloatingModeOn || zeroGravityModeOn) {
                if (rawAxisValues.y != 0) {
                    movingVertically = true;
                } else {
                    movingVertically = false;
                }
            }

            if (!canMove || moveIputPaused) {
                turnAmount = 0;

                rotatingPlayerIn2_5dToRLeft = false;
                rotatingPlayerIn2_5dToRight = false;
            }
        }

        //get the amount of movement in forward direction

        if (useMoveInpuptMagnitedeToForwardAmount) {
            forwardAmount = currentMoveInput.magnitude;
        } else {
            forwardAmount = localMove.z;
        }

        forwardAmount = Mathf.Clamp (forwardAmount, -walkSpeed, walkSpeed);

        if (checkQuickMovementTurn180DegreesOnRun) {
            updatecheckQuickMovementTurn180DegreesOnRun (localMove);
        }
    }

    void updatecheckQuickMovementTurn180DegreesOnRun (Vector3 localMove)
    {
        if (playerIsAiming ||
            firstPersonActive ||
            fullBodyAwarenessActive ||
            !playerOnGround ||
            actionActive ||
            Time.time < lastTimeActionActive + 1) {

            if (checkingQuickMovementDirectionActive || pausePlayerTransformExtraRotationActive) {
                lastTimeMovementInputPressed = 0;

                checkingQuickMovementDirectionActive = false;

                pausePlayerTransformExtraRotationActive = false;

                activateExtraRotationInQuickTurnMovement = false;
            }
        } else {
            if (rawAxisValues != Vector2.zero) {
                if (lastTimeMovementInputPressed == 0 && isPlayerMoving (0.95f)) {
                    lastTimeMovementInputPressed = Time.time;

                    if (showDebugPrint) {
                        print ("Starting to check");
                    }

                    checkingQuickMovementDirectionActive = true;
                }
            } else {
                if (lastTimeMovementInputPressed != 0 && !isPlayerMoving (0.1f)) {
                    lastTimeMovementInputPressed = 0;

                    if (showDebugPrint) {
                        print ("Resetting check");
                    }

                    checkingQuickMovementDirectionActive = false;
                }
            }

            if (checkingQuickMovementDirectionActive && Time.time > lastTimeMovementInputPressed + minDelayToActivateQuickMovementTurn180OnRun) {
                float movementDirectionTurnAmount = Mathf.Atan2 (localMove.x, localMove.z);

                if (Mathf.Abs (movementDirectionTurnAmount) > 0.95f) {

                    if (walkSpeed < 1) {
                        animator.SetInteger (quickTurnDirectionIDSpeedID, 0);

                        quickTurnMovementDuration = quickTurnMovementDurationWalking;
                    } else {
                        if (running) {
                            animator.SetInteger (quickTurnDirectionIDSpeedID, 2);

                            quickTurnMovementDuration = quickTurnMovementDurationRunning;
                        } else {
                            animator.SetInteger (quickTurnDirectionIDSpeedID, 1);

                            quickTurnMovementDuration = quickTurnMovementDurationSprinting;

                            activateExtraRotationInQuickTurnMovement = true;
                        }
                    }

                    if (movementDirectionTurnAmount > 0) {
                        animator.SetTrigger (quickTurnRightDirectionID);
                    } else {
                        animator.SetTrigger (quickTurnLeftDirectionID);
                    }

                    pausePlayerTransformExtraRotationActive = true;

                    checkingQuickMovementDirectionActive = false;

                    lastTimeQuickTurnActive = Time.time;

                    if (showDebugPrint) {
                        print ("quick change of direction");
                    }
                }
            }

            if (pausePlayerTransformExtraRotationActive) {
                if ((Time.time > lastTimeQuickTurnActive + quickTurnMovementDuration) ||
                    (playerInput.getPlayerRawMovementAxisWithoutCheckingEnabled () == Vector2.zero &&
                    Time.time > lastTimeQuickTurnActive + quickTurnMovementDuration / 2)) {

                    lastTimeMovementInputPressed = 0;

                    checkingQuickMovementDirectionActive = false;

                    pausePlayerTransformExtraRotationActive = false;

                    activateExtraRotationInQuickTurnMovement = false;

                    if (showDebugPrint) {
                        print ("resume player");
                    }
                } else {
                    if (activateExtraRotationInQuickTurnMovement) {
                        Quaternion quickTurnRotatation = Quaternion.LookRotation (currentMoveInput, playerTransformUp);

                        mainRigidbody.rotation =
                            Quaternion.Lerp (mainRigidbody.rotation, quickTurnRotatation, quickTurnMovementRotationSpeed * currentFixedUpdateDeltaTime);
                    }
                }
            }
        }
    }

    bool ignoreLookInCameraDirection;
    bool ignoreStrafeModeInputCheckActive;

    public void setIgnoreLookInCameraDirectionValue (bool state)
    {
        ignoreLookInCameraDirection = state;
    }

    public void setIgnoreStrafeModeInputCheckActiveState (bool state)
    {
        ignoreStrafeModeInputCheckActive = state;
    }

    float turnAmountClampValue = 1;

    public void setTurnAmountClampValue (float newValue)
    {
        turnAmountClampValue = newValue;
    }

    bool rotateInCameraDirectionOnAirOnExternalActionActive;

    public void setRotateInCameraDirectionOnAirOnExternalActionActiveState (bool state)
    {
        rotateInCameraDirectionOnAirOnExternalActionActive = state;
    }

    //function used when the player is aim mode, so the character will rotate in the camera direction
    void lookCameraDirection ()
    {
        if (usingAnimator) {
            if (wallRunningActive) {
                updateWallRunningState ();
            } else {
                bool rotateInLookDirectionResult = false;

                if ((((playerIsAiming && lookInCameraDirectionOnFreeFireActive) || lookInCameraDirection || lookInCameraDirectionActive) && playerOnGround) ||

                    (playerIsAiming && !usingFreeFireMode && !playerOnGround)) {

                    rotateInLookDirectionResult = true;
                }

                if (fullBodyAwarenessActive) {
                    rotateInLookDirectionResult = true;
                }

                if (ignoreLookInCameraDirection) {
                    rotateInLookDirectionResult = false;
                }

                if (rotateInCameraDirectionOnAirOnExternalActionActive) {
                    if (!ignoreLookInCameraDirection && !rotateInLookDirectionResult && !playerOnGround) {
                        if (playerIsAiming || usingFreeFireMode) {

                            rotateInLookDirectionResult = true;
                        }
                    }
                }

                if (rotateInLookDirectionResult) {
                    bool ZGFFModeOnAir = ((zeroGravityModeOn || freeFloatingModeOn) && !playerOnGround);

                    if (!ZGFFModeOnAir) {
                        //get the camera direction, getting the local direction in any surface
                        Vector3 forward = playerCameraTransform.TransformDirection (Vector3.forward);
                        float newAngle = Vector3.Angle (forward, playerTransformForward);

                        Vector3 targetDirection = vector3Zero;

                        Quaternion targetRotation = quaternionIdentity;

                        forward = forward - playerTransform.InverseTransformDirection (forward).y * playerTransformUp;

                        forward = forward.normalized;

                        targetDirection = forward;

                        Quaternion currentRotation = Quaternion.LookRotation (targetDirection, playerTransformUp);

                        float currentFixedDeltaTime = getCurrentFixedDeltaTime ();

                        targetRotation = Quaternion.Lerp (mainRigidbody.rotation, currentRotation, aimTurnSpeed * currentFixedDeltaTime);

                        bool canRotatePlayer = (!lookAlwaysInCameraDirection ||
                                               (lookAlwaysInCameraDirection && !lookOnlyIfMoving) ||
                                               (lookAlwaysInCameraDirection && lookOnlyIfMoving && playerUsingInput) ||
                                               (lookAlwaysInCameraDirection && lookOnlyIfMoving && !playerUsingInput && playerIsAiming));

                        if (ignoreCameraDirectionOnStrafeMovement) {
                            canRotatePlayer = false;
                        }

                        if (fullBodyAwarenessActive) {
                            canRotatePlayer = true;
                        }

                        if (newAngle >= Mathf.Abs (thresholdAngleDifference)) {
                            //if the player is not moving, set the turnamount to rotate him around, setting its turn animation properly
                            if (canRotatePlayer) {
                                targetRotation = Quaternion.Lerp (mainRigidbody.rotation, currentRotation, autoTurnSpeed * currentFixedDeltaTime);

                                Vector3 lookDelta = playerTransform.InverseTransformDirection (targetDirection * 100);

                                float lookAngle = Mathf.Atan2 (lookDelta.x, lookDelta.z) * Mathf.Rad2Deg;

                                turnAmount += lookAngle * .01f * 6;
                            }
                        } else {
                            turnAmount = Mathf.MoveTowards (turnAmount, 0, currentFixedDeltaTime * 2);
                        }

                        if (rotateDirectlyTowardCameraOnStrafe || fullBodyAwarenessActive) {
                            turnAmount = 0;
                        }

                        if (canRotatePlayer) {
                            if (fullBodyAwarenessActive) {
                                lookCameraDirectionInFBAIsCurrentlyActive = true;
                            } else {
                                mainRigidbody.MoveRotation (targetRotation);
                            }
                        } else {
                            if (fullBodyAwarenessActive) {
                                lookCameraDirectionInFBAIsCurrentlyActive = false;
                            }
                        }
                    }
                }

                if ((zeroGravityModeOn || freeFloatingModeOn) && !firstPersonActive && !playerOnGround && !characterRotatingToSurface) {
                    if (isPlayerMovingOn3dWorld () || playerIsAiming) {
                        if (playerIsAiming) {
                            if (fullBodyAwarenessActive) {
                                lookCameraDirectionInFBAIsCurrentlyActive = true;
                            } else {
                                playerTransform.rotation = playerCameraTransform.rotation;
                            }
                        } else {
                            if (fullBodyAwarenessActive) {
                                lookCameraDirectionInFBAIsCurrentlyActive = true;
                            } else {
                                if ((zeroGravityModeOn || freeFloatingModeOn) && isPlayerMovingOn3dWorld ()) {
                                    Quaternion targetRotation = Quaternion.Lerp (playerTransform.rotation,
                                        playerCameraTransform.rotation, zeroGravityLookCameraSpeed * getCurrentFixedDeltaTime ());

                                    playerTransform.rotation = targetRotation;
                                }
                            }
                        }
                    }
                }
            }
        } else {

            //Check if the wall running function is active
            if (wallRunningActive) {
                updateWallRunningState ();
            } else {
                if (!characterRotatingToSurface && !pausePlayerRotateToCameraDirectionOnFirstPersonActive) {
                    playerTransform.rotation = playerCameraTransform.rotation;
                }
            }
        }
    }

    public bool isLookCameraDirectionInFBAIsCurrentlyActive ()
    {
        return lookCameraDirectionInFBAIsCurrentlyActive;
    }

    bool lookCameraDirectionInFBAIsCurrentlyActive;

    bool pausePlayerRotateToCameraDirectionOnFirstPersonActive;

    public void setPausePlayerRotateToCameraDirectionOnFirstPersonActiveState (bool state)
    {
        pausePlayerRotateToCameraDirectionOnFirstPersonActive = state;
    }

    void addExtraRotation ()
    {
        if (addExtraRotationPaused) {
            return;
        }

        if (fullBodyAwarenessActive) {
            return;
        }

        if (usingAnimator && !wallRunningActive) {
            if ((!playerIsAiming || !lookInCameraDirectionOnFreeFireActive) && !lookInCameraDirectionActive) {

                canRotateOn3dWorld = isPlayerMovingOn3dWorld () && ((!freeFloatingModeOn && !zeroGravityModeOn) || playerOnGround);
                //&& !playerCameraManager.isCurrentCameraLookInPlayerDirection ();

                canRotateOn2_5dWorld = isPlayerMovingOn2_5dWorld ();

                if (flyModeActive) {
                    canRotateOn3dWorld = false;
                }

                if (canRotateOn3dWorld || canRotateOn2_5dWorld) {

                    if (pausePlayerTransformExtraRotationActive) {
                        return;
                    }

                    if (!freeFloatingModeOn && !zeroGravityModeOn) {
                        if (keepLastMovementInputOnJump) {
                            if (!playerOnGround && lastMoveInputOnJump != vector3Zero && !ignoreLastMoveInputOnJump) {
                                return;
                            }
                        }
                    }

                    //add an extra rotation to the player to get a smooth movement
                    if (lockedCameraActive && !usedByAI) {
                        float turnSpeed = Mathf.Lerp (stationaryTurnSpeed, movingTurnSpeed, forwardAmount);

                        if (useTankControls) {
                            turnSpeed *= tankModeRotationSpeed;

                            float newRotationValue = horizontalInput * turnSpeed * currentFixedUpdateDeltaTime * 1.5f;

                            if (ignorePlayerExtraRotationOnAirEnabled && !playerOnGround) {
                                newRotationValue = 0;
                            }

                            if (Mathf.Abs (newRotationValue) > 0.001f) {
                                playerTransform.Rotate (0, newRotationValue, 0);
                            }
                        } else {
                            float newRotationValue = turnAmount * turnSpeed * currentFixedUpdateDeltaTime * 1.5f;

                            if (ignorePlayerExtraRotationOnAirEnabled && !playerOnGround) {
                                newRotationValue = 0;
                            }

                            if (Mathf.Abs (newRotationValue) > 0.001f) {
                                playerTransform.Rotate (0, newRotationValue, 0);
                            }
                        }
                    } else {
                        if (!playerIsAiming || useTurnSpeedOnAim) {
                            float turnSpeed = Mathf.Lerp (stationaryTurnSpeed, movingTurnSpeed, forwardAmount);

                            float newRotationValue = turnAmount * turnSpeed * currentFixedUpdateDeltaTime * 1.5f;

                            if (ignorePlayerExtraRotationOnAirEnabled && !playerOnGround) {
                                newRotationValue = 0;
                            }

                            if (Mathf.Abs (newRotationValue) > 0.001f) {
                                playerTransform.Rotate (0, newRotationValue, 0);
                            }
                        }
                    }
                }
            }
        }
    }

    //set the normal of the player every time it is changed in the other script
    public void setCurrentNormalCharacter (Vector3 newNormal)
    {
        currentNormal = newNormal;
    }

    public void setApplyAnimatorVelocityWithoutMovingState (bool state)
    {
        applyAnimatorVelocityWithoutMoving = state;
    }

    //check if the player jumps
    void onGroundVelocity ()
    {
        if (currentMoveInput.magnitude == 0 && !actionActiveWithMovement && (!applyAnimatorVelocityWithoutMoving || actionActive)) {
            if (!movingInsideVehicle) {
                currentVelocity = vector3Zero;
            }
        }

        //check when the player is able to jump, according to the timer and the animator state
        bool animationGrounded = false;

        if (usingAnimator && !useFirstPersonPhysicsInThirdPersonActive && !customCharacterControllerActive && !fullBodyAwarenessActive) {
            if (ignoreAnimatorGroundStateOnJumpCheck) {
                if (playerOnGround) {
                    animationGrounded = true;
                }
            } else {
                animationGrounded = animator.GetCurrentAnimatorStateInfo (baseLayerIndex).IsName ("Grounded");

                if (!animationGrounded) {
                    if (playerOnGround && Time.time > lastTimeGround + half) {
                        animationGrounded = true;
                    }
                }
            }
        } else {
            animationGrounded = playerOnGround;
        }

        //if the player jumps, apply velocity to its rigidbody
        if (jumpInput && Time.time > lastTimeAir + readyToJumpTime && animationGrounded) {
            bool activateJumpInputResult = true;

            if (!ignoreUseDelayOnGroundJumpActive) {
                if (useDelayOnGroundJump) {
                    activateJumpInputInProcess = false;

                    if (useDelayOnGroundJumpOnlyIfNotMoving) {
                        if (!isMoving) {
                            activateJumpInputResult = false;
                        }
                    } else {
                        activateJumpInputResult = false;
                    }
                }
            }

            if (fullBodyAwarenessActive) {
                if (ignoreDelayOnGroundJumpOnFBA) {
                    activateJumpInputResult = true;
                }
            }

            if (lastJumpActivatedExternally) {
                activateJumpInputResult = true;
            }

            waitingToActivateJumpInputWithDelay = false;

            if (activateJumpInputResult) {
                activateJumpInput ();
            } else {
                StartCoroutine (activateJumpInputCoroutine ());
            }
        }

        if (activateJumpInputInProcess) {
            if (Time.time > lastTimeAir + readyToJumpTime && animationGrounded) {
                activateJumpInput ();
            }

            activateJumpInputInProcess = false;
        }

        jumpInput = false;

        lastJumpActivatedExternally = false;
    }

    bool activateJumpInputInProcess;

    bool waitingToActivateJumpInputWithDelay;

    IEnumerator activateJumpInputCoroutine ()
    {
        waitingToActivateJumpInputWithDelay = true;

        if (useEventOnDelayOnGroundJump) {
            eventOnDelayOnGroundJump.Invoke ();
        }

        WaitForSeconds delay = new WaitForSeconds (delayOnGroundJump);

        yield return delay;

        activateJumpInputInProcess = true;

        waitingToActivateJumpInputWithDelay = false;
    }

    void activateJumpInput ()
    {
        if (activateJumpInputInProcess) {
            if (!playerOnGround) {
                return;
            }
        }

        if (updateFootStepStateActive) {
            if (playerOnGround) {
                if (currentFootStepState != footStepState.Jumping) {
                    stepManager.setFootStepState ("Jumping");

                    currentFootStepState = footStepState.Jumping;
                }
            }
        }

        if (customCharacterControllerActive) {
            currentCustomCharacterControllerBase.activateJumpAnimatorState ();
        } else {
            if (lastJumpAnimatorIDValue != 0) {
                lastJumpAnimatorIDValue = 0;

                animator.SetFloat (jumpAnimatorID, 0);
            }
        }

        setOnGroundState (false);

        if (keepLastMovementInputOnJump) {
            lastMoveInputOnJump = currentMoveInput;
        }

        if (usingAnimator && !useFirstPersonPhysicsInThirdPersonActive) {
            currentVelocity = airSpeed * currentMoveInput;
        } else {
            currentVelocity = noAnimatorAirSpeed * currentMoveInput;
        }

        currentVelocity = currentVelocity + jumpPower * playerTransformUp;

        if (extraForceOnJumpActive) {
            currentVelocity += extraForceOnJumpAmount * playerTransformUp;

            extraForceOnJumpActive = false;
        }

        jump = true;

        lastJumpTime = Time.time;

        //this is used for the headbod, to shake the camera when the player jumps
        if (updateHeadbobState && headBobManagerLocated) {
            if (currentHeadbobState != headbobState.Jump_Start) {
                headBobManager.setState ("Jump Start");

                currentHeadbobState = headbobState.Jump_Start;
            }
        }

        if (weaponsManagerLocated) {
            weaponsManager.setWeaponsJumpStartPositionState (true);
        }

        disableCrouchState = false;

        if (firstPersonActive) {
            if (getUpIfJumpOnCrouchInFirstPerson) {
                disableCrouchState = true;
            }
        } else {
            if (getUpIfJumpOnCrouchInThirdPerson) {
                disableCrouchState = true;
            }
        }

        if (disableCrouchState) {
            if (crouching) {
                setCrouchComponentsState (false);

                if (firstPersonActive) {
                    crouching = false;
                }
            }
        }

        if (useEventOnJump) {
            eventOnJump.Invoke ();
        }

        if (useEventOnGroundJump) {
            eventOnGroundJump.Invoke ();
        }

        if (stopCrouchOnJumpEnabled) {
            if (crouching) {
                if (playerCanGetUpFromCrouch ()) {
                    stopCrouchFromAutoJumpActive = true;

                    if (!firstPersonActive) {
                        if (weaponsManagerLocated) {
                            if (weaponsManager.isIgnoreCrouchWhileWeaponActive () || !weaponsManager.isAimingWeapons ()) {
                                if (!fullBodyAwarenessActive) {
                                    weaponsManager.enableOrDisableIKOnHands (true);
                                }
                            }
                        }
                    }

                    setCrouchComponentsState (false);

                    if (crouchSlidingEnabled && running) {
                        if (firstPersonActive) {
                            stopCrouchSliding ();
                        }
                    }
                }
            }
        }
    }

    public void setIgnoreUseDelayOnGroundJumpActiveState (bool state)
    {
        ignoreUseDelayOnGroundJumpActive = state;
    }

    public void enableOrDisableUseDelayOnGroundJumpState (bool state, float delayAmount)
    {
        useDelayOnGroundJump = state;
        delayOnGroundJump = delayAmount;
    }

    bool stopCrouchFromAutoJumpActive;

    //check if the player is in the air falling, applying the gravity force in his local up negative
    void onAirVelocity ()
    {
        if (!gravityPowerActive &&

            !usingJetpack &&

            (!flyModeActive || enableExternalForceOnFlyMode) &&

            !characterRotatingToSurface &&

            (!swimModeActive || enableExternalForceOnSwimMode) &&

            !externalControlBehaviorForAirTypeActive) {

            //when the player falls, allow him to move to his right, left, forward and backward with WASD

            if (zeroGravityModeOn || freeFloatingModeOn) {

                secondaryMoveInput = currentMoveInput;

                canMoveVertically = (zeroGravityModeOn && canMoveVerticallyOnZeroGravity) || (freeFloatingModeOn && canMoveVerticallyOnFreeFloating);

                canMoveVerticallyAndHorizontal = (zeroGravityModeOn && canMoveVerticallyAndHorizontalZG) || (freeFloatingModeOn && canMoveVerticallyAndHorizontalFF);

                if (movingVertically) {
                    if (zeroGravityModeOn) {
                        currentVerticalSpeed = zeroGravityModeVerticalSpeed;
                    } else {
                        currentVerticalSpeed = freeFloatingModeVerticalSpeed;
                    }

                    if (canMoveVerticallyAndHorizontal) {
                        secondaryMoveInput += (verticalInput * currentVerticalSpeed) * playerCameraTransform.up;
                    } else {
                        secondaryMoveInput = currentVerticalSpeed * (verticalInput * playerCameraTransform.up + horizontalInput * mainCameraTransform.right);
                    }
                }

                if (externalForceOnAirActive) {
                    airMove = externalForceOnAirValue;

                    currentVelocity = Vector3.Lerp (currentVelocity, airMove, currentFixedUpdateDeltaTime * externalForceOnAirControlValue);

                    externalForceOnAirActive = false;
                } else {
                    if (zeroGravityModeOn) {
                        secondaryMove = (zeroGravityMovementSpeed * currentZeroGravitySpeedMultiplier) * secondaryMoveInput;

                        currentVelocity = Vector3.Lerp (currentVelocity, secondaryMove, currentFixedUpdateDeltaTime * zeroGravityControlSpeed);
                    }

                    if (freeFloatingModeOn) {
                        secondaryMove = (freeFloatingMovementSpeed * currentFreeFloatingSpeedMultiplier) * secondaryMoveInput;

                        currentVelocity = Vector3.Lerp (currentVelocity, secondaryMove, currentFixedUpdateDeltaTime * freeFloatingControlSpeed);
                    }
                }
            } else {
                if (usingAnimator && !useFirstPersonPhysicsInThirdPersonActive && !wallRunningActive) {
                    if (externalForceOnAirActive) {
                        airMove = externalForceOnAirValue;

                        currentVelocity = Vector3.Lerp (currentVelocity, airMove, currentFixedUpdateDeltaTime * externalForceOnAirControlValue);

                        externalForceOnAirActive = false;
                    } else {
                        if (!ignoreInputOnAirControlActive) {
                            airMove = playerTransform.InverseTransformDirection (currentVelocity).y * playerTransformUp;

                            bool surfaceDetectedCloseToPlayerResult = false;

                            if (Time.time > lastTimeFalling + half) {
                                if (Physics.Raycast (playerTransform.position, -currentPlayerUp, 0.6f, layer)) {
                                    surfaceDetectedCloseToPlayerResult = true;
                                }
                            }

                            if (!surfaceDetectedCloseToPlayerResult) {
                                if (keepLastMovementInputOnJump) {
                                    Vector3 newAirMove = vector3Zero;

                                    if (Time.time < lastJumpTime + keepLastMovementInputOnJumpDuration) {
                                        if (lastMoveInputOnJump != vector3Zero) {
                                            newAirMove = airSpeed * lastMoveInputOnJump;
                                        }

                                        if (allowRegularAirInputControlOnAirAfterJumpDuration) {
                                            if (isPlayerUsingInput ()) {
                                                // && isPlayerMoving (0.6f)
                                                lastMoveInputOnJump = currentMoveInput;

                                                newAirMove = airSpeed * currentMoveInput;

                                                ignoreLastMoveInputOnJump = true;
                                            }
                                        }
                                    } else {
                                        resetLastMoveInputOnJumpValue ();

                                        if (allowRegularAirInputControlOnAirAfterJumpDuration) {
                                            newAirMove = airSpeed * currentMoveInput;
                                        }
                                    }

                                    airMove += newAirMove;
                                } else {
                                    airMove += airSpeed * currentMoveInput;
                                }
                            }

                            currentVelocity = Vector3.Lerp (currentVelocity, airMove, currentFixedUpdateDeltaTime * airControl);
                        }
                    }
                }

                checkIfActivateWallRunningState ();
            }

            //also, apply force in his local negative Y Axis
            if (!playerOnGround) {
                if (slowingFallInput) {
                    slowingFall = true;
                    slowingFallInput = false;
                }

                if (!gravityForcePaused && !zeroGravityModeOn && !freeFloatingModeOn && !wallRunningActive && !pauseAllPlayerDownForces && !externalControlBehaviorForAirTypeActive) {
                    mainRigidbody.AddForce (mainRigidbody.mass * gravityForce * playerTransformUp);

                    if (gravityMultiplier != 0) {
                        extraGravityForce = (gravityForce * gravityMultiplier) * playerTransformUp
                        + (-gravityForce) * playerTransformUp;

                        mainRigidbody.AddForce (extraGravityForce);
                    }
                }
            }

            //also apply force if the player jumps again
            if (doubleJump) {
                if (usingAnimator && !useFirstPersonPhysicsInThirdPersonActive) {
                    currentVelocity += airSpeed * currentMoveInput;
                } else {
                    currentVelocity += noAnimatorAirSpeed * currentMoveInput;
                }

                float currentDoubleJumpPower = jumpPower;

                if (useNewDoubleJumpPower) {
                    currentDoubleJumpPower = doubleJumpPower;
                }

                if (removeVerticalSpeedOnDoubleJump) {
                    float currentVerticalVelocity = playerTransform.InverseTransformDirection (currentVelocity).y;

                    currentVelocity -= currentVerticalVelocity * playerTransform.up;
                }

                currentVelocity = currentVelocity + currentDoubleJumpPower * playerTransformUp;

                if (!ladderFound) {
                    jumpsAmount++;
                }

                lastDoubleJumpTime = Time.time;

                if (useEventOnJump) {
                    eventOnJump.Invoke ();
                }

                if (useEventOnDoubleJump) {
                    eventOnDoubleJump.Invoke ();
                }
            }

            doubleJump = false;

        } else {
            if (characterRotatingToSurface) {
                mainRigidbody.linearVelocity = vector3Zero;

                currentVelocity = vector3Zero;
            }
        }

        if (fallDamageEnabled) {
            lastFrameAirVelocity = mainRigidbody.linearVelocity.magnitude;
        }
    }

    public void setApplyRootMotionAlwaysActiveState (bool state)
    {
        applyRootMotionAlwaysActive = state;
    }

    public void updateAnimatorExternally ()
    {
        if (animator.enabled) {
            Update ();

            updateAnimator ();
        }
    }

    void setApplyRootMotionState (bool state)
    {
        if (currentApplyRootMotionValue != state) {
            animator.applyRootMotion = state;
        }

        currentApplyRootMotionValue = state;

        rootMotionCurrentlyActive = state;
    }

    //update the animator values
    void updateAnimator ()
    {
        //set the rootMotion according to the player state
        if (fullBodyAwarenessActive && !useRootMotionOnFullBodyAwareness) {
            if (currentApplyRootMotionValue) {
                setApplyRootMotionState (false);
            }
        } else {
            if (!applyRootMotionAlwaysActive) {
                if (useRootMotionActive) {
                    if (currentApplyRootMotionValue != playerOnGround) {
                        setApplyRootMotionState (playerOnGround);
                    }
                } else {
                    if (currentApplyRootMotionValue) {
                        setApplyRootMotionState (false);
                    }
                }
            } else {
                if (!currentApplyRootMotionValue) {
                    setApplyRootMotionState (true);
                }
            }
        }

        if (running && sprintEnabled && sprintCanBeUsed) {
            forwardAmount *= 2;
        }

        if (customCharacterControllerActive) {
            if (currentCustomCharacterControllerBase.updateForwardAmountInputValueFromPlayerController) {
                currentCustomCharacterControllerBase.updateForwardAmountInputValue (forwardAmount);
            }

            if (currentCustomCharacterControllerBase.updateTurnAmountInputValueFromPlayerController) {
                currentCustomCharacterControllerBase.updateTurnAmountInputValue (turnAmount);
            }

            if (currentCustomCharacterControllerBase.updateUsingInputValueFromPlayerController) {
                currentCustomCharacterControllerBase.updatePlayerUsingInputValue (playerUsingInput);
            }

            updateAnimatorSpeed ();
        } else {
            if (!lockedCameraActive || !isPlayerMovingOn2_5dWorld ()) {
                //if the player is not aiming, set the forward direction
                if (!playerIsAiming || (!lookInCameraDirectionActive && (usingFreeFireMode || (checkToKeepWeaponAfterAimingWeaponFromShooting || Time.time < lastTimeCheckToKeepWeapon + 0.2f)))) {
                    lastForwardAnimatorIDValue = forwardAmount;

                    animator.SetFloat (forwardAnimatorID, forwardAmount, animatorForwardInputLerpSpeed, currentFixedUpdateDeltaTime);
                } else {
                    if (lastForwardAnimatorIDValue != 0) {
                        lastForwardAnimatorIDValue = 0;

                        //else, set its forward to 0, to prevent any issue
                        //this value is set to 0, because the player uses another layer of the mecanim to move while he is aiming
                        animator.SetFloat (forwardAnimatorID, 0);
                    }
                }
            } else {
                lastForwardAnimatorIDValue = forwardAmount;

                animator.SetFloat (forwardAnimatorID, forwardAmount, animatorForwardInputLerpSpeed, currentFixedUpdateDeltaTime);
            }

            if (isTankModeActive ()) {
                if (!playerIsAiming) {
                    if (lastTurnAnimatorIDValue != 0) {
                        lastTurnAnimatorIDValue = 0;

                        animator.SetFloat (turnAnimatorID, 0);
                    }

                    if (tankModeCurrentlyEnabled) {
                        tankModeActive = true;
                    } else {
                        tankModeActive = false;
                    }
                } else {
                    if (isPlayerMoving (0)) {
                        turnAmount = 0;
                    }

                    if (overrideTurnAmountActive) {
                        turnAmount = overrideTurnAmount;
                    }

                    turnAmount = Mathf.Clamp (turnAmount, -1, 1);

                    lastTurnAnimatorIDValue = turnAmount;

                    animator.SetFloat (turnAnimatorID, turnAmount, animatorTurnInputLerpSpeed, currentFixedUpdateDeltaTime);

                    if (playerCanMoveOnAimInTankMode) {
                        tankModeActive = false;
                    }
                }
            } else {
                if (ignoreStrafeModeInputCheckActive) {
                    lastTimePlayerUserInput = 0;

                    ignoreStrafeModeInputCheckActive = false;

                    strafeModeActive = false;
                }

                if (strafeModeActive) {
                    if (lookAlwaysInCameraDirection && lookOnlyIfMoving) {
                        if (!playerIsAiming) {
                            turnAmount = 0;
                            lastTimePlayerUserInput = Time.time;
                        }
                    }

                    if (playerUsingInput) {
                        turnAmount = 0;
                        lastTimePlayerUserInput = Time.time;
                    }
                }

                if (lastTimePlayerUserInput > 0) {
                    if (isPlayerMoving (0)) {
                        turnAmount = 0;
                    } else {
                        lastTimePlayerUserInput = 0;
                    }
                }

                if (overrideTurnAmountActive) {
                    turnAmount = overrideTurnAmount;
                }

                turnAmount = Mathf.Clamp (turnAmount, -turnAmountClampValue, turnAmountClampValue);

                lastTurnAnimatorIDValue = turnAmount;

                animator.SetFloat (turnAnimatorID, turnAmount, animatorTurnInputLerpSpeed, currentFixedUpdateDeltaTime);

                tankModeActive = false;
            }

            if (Mathf.Abs (horizontalInput) > 0 || Mathf.Abs (verticalInput) > 0) {
                lastTimeMoveInputPressed = Time.time;
            }

            if (useTankControls) {
                lastTimeInputPressed = true;
            } else {
                lastTimeInputPressed = Time.time > lastTimeMoveInputPressed + minTimeToSetIdleOnStrafe;
            }

            if (obstacleToAvoidMovementFound) {
                lastTimeInputPressed = true;
            }

            if (previousValueLastTimeInputPressed != lastTimeInputPressed) {
                previousValueLastTimeInputPressed = lastTimeInputPressed;

                animator.SetBool (lastTimeInputPressedAnimatorID, lastTimeInputPressed);
            }

            if (overrideOnGroundAnimatorValueActive) {
                onGroundAnimatorState = playerOnGroundAnimatorStateOnOverrideOnGround;

                if (playerOnGroundAnimatorStateOnOverrideOnGroundWithTime) {
                    if (Time.time > lastTimeOverrideOnGroundAnimator + overrideOnGroundDuration) {
                        overrideOnGroundAnimatorValueActive = false;
                    }
                }
            } else {
                onGroundAnimatorState = playerOnGround;
            }

            if (previousValueOnGroundAnimatorState != onGroundAnimatorState) {
                previousValueOnGroundAnimatorState = onGroundAnimatorState;

                animator.SetBool (onGroundAnimatorID, onGroundAnimatorState);

                setTimeOnAirAnimatorIDValue ();
            }

            if (previousValueCrouching != crouching) {
                previousValueCrouching = crouching;

                animator.SetBool (crouchAnimatorID, crouching);
            }

            animator.SetBool (strafeModeActiveAnimatorID, strafeModeActive);

            if (lockedCameraActive) {
                if (isPlayerMovingOn2_5dWorld ()) {

                    bool avoidStrafeModeActiveOnAnimatorResult = true;

                    if (playerUsingMeleeWeapons && isPlayerAiming ()) {
                        avoidStrafeModeActiveOnAnimatorResult = false;
                    }

                    if (avoidStrafeModeActiveOnAnimatorResult) {
                        scrollModeActive = true;

                        animator.SetBool (strafeModeActiveAnimatorID, false);
                    } else {
                        scrollModeActive = false;
                    }
                } else {
                    scrollModeActive = false;
                }
            } else {
                scrollModeActive = false;
            }

            movingAnimator = isMoving;

            if (obstacleToAvoidMovementFound) {
                movingAnimator = false;
            }

            if (previousValueMovingAnimator != movingAnimator) {
                previousValueMovingAnimator = movingAnimator;

                animator.SetBool (movingAnimatorID, movingAnimator);
            }

            movementInputActive = playerUsingInput;

            if (obstacleToAvoidMovementFound) {
                movementInputActive = false;
            }

            if (tankModeActive && !playerCanMoveOnAimInTankMode) {
                if (playerIsAiming) {
                    movementInputActive = false;
                }
            }

            if (movementInputIgnoredOnAimActive) {
                movementInputActive = false;
            }

            if (previousValueMovementInputActive != movementInputActive) {
                previousValueMovementInputActive = movementInputActive;

                animator.SetBool (movementInputActiveAnimatorID, movementInputActive);
            }

            movementRelativeToCameraAnimatorState = lockedCameraActive && useRelativeMovementToLockedCamera && !usedByAI;

            if (previousValueMovementRelativeToCameraState != movementRelativeToCameraAnimatorState) {
                previousValueMovementRelativeToCameraState = movementRelativeToCameraAnimatorState;

                animator.SetBool (movementRelativeToCameraAnimatorID, movementRelativeToCameraAnimatorState);
            }

            if (playerIsAiming) {
                currentMovementID = 1;
            } else {
                if (fullBodyAwarenessActive) {
                    if (playerUsingWeapons) {
                        currentMovementID = 1;
                    } else {
                        currentMovementID = 0;
                    }
                } else {
                    currentMovementID = 0;
                }
            }

            //CHECK FOR FBA HOLDING POSTURE WEAPON CHECK
            animator.SetFloat (movementIDAnimatorID, currentMovementID, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime);

            if (!tankModeActive && !scrollModeActive) {
                currentPlayerModeID = 0;
            } else {
                if (tankModeActive) {
                    currentPlayerModeID = 1;
                }

                if (scrollModeActive) {
                    currentPlayerModeID = 2;
                }
            }

            animator.SetFloat (playerModeIDAnimatorID, currentPlayerModeID, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime);

            if (weaponsManagerLocated) {
                carryingWeaponAnimatorState = playerUsingWeapons;
            } else {
                carryingWeaponAnimatorState = false;
            }

            if (previousValueCarryingWeaponAnimatorState != carryingWeaponAnimatorState) {
                previousValueCarryingWeaponAnimatorState = carryingWeaponAnimatorState;

                animator.SetBool (carryingWeaponAnimatorID, carryingWeaponAnimatorState);
            }

            if (lockedCameraActive && useRelativeMovementToLockedCamera && !usedByAI) {
                currentWalkSpeedOnLockedCamera = walkSpeed;

                if (isPlayerAiming ()) {
                    currentWalkSpeedOnLockedCamera = 1;
                }

                bool useMainCameraRelativeDirectionResult = false;

                if (!playerIsAiming) {
                    if (playerCameraManager.isShowCameraCursorWhenNotAimingActive ()) {
                        useMainCameraRelativeDirectionResult = true;

                        if (keepCameraRelativeMovementWhenNotAiming) {
                            useMainCameraRelativeDirectionResult = false;
                        }
                    }
                }

                if (useMainCameraRelativeDirectionResult) {
                    lockedCameraMoveInput = currentWalkSpeedOnLockedCamera *
                        (verticalInput * playerCameraTransform.forward + horizontalInput * mainCameraTransform.right);
                } else {
                    lockedCameraMoveInput = currentWalkSpeedOnLockedCamera *
                        (verticalInput * currentLockedCameraTransform.forward + horizontalInput * currentLockedCameraTransform.right);
                }

                lockedMove = playerTransform.InverseTransformDirection (lockedCameraMoveInput);

                currentHorizontalValue = lockedMove.x;
                currentVerticalValue = lockedMove.z;
            } else {

                if (tankModeActive) {
                    if (running && sprintEnabled && sprintCanBeUsed) {
                        verticalInput *= 2;
                    } else {
                        if (verticalInput > 0) {
                            currentWalkSpeedOnLockedCamera = walkSpeed;

                            if (isPlayerAiming ()) {
                                currentWalkSpeedOnLockedCamera = 1;
                            }

                            verticalInput = Mathf.Clamp (verticalInput, 0, currentWalkSpeedOnLockedCamera);
                        }
                    }
                }

                currentHorizontalValue = horizontalInput;
                currentVerticalValue = verticalInput;
            }

            if (useTankControls) {
                currentHorizontalInputLerpSpeed = inputTankControlsHorizontalLerpSpeed;
                currentVerticalInputLerpSpeed = inputTankControlsVerticalLerpSpeed;
            } else {
                currentHorizontalInputLerpSpeed = inputHorizontalLerpSpeed;
                currentVerticalInputLerpSpeed = inputVerticalLerpSpeed;
            }

            if (flyModeActive || swimModeActive || externalControlBehaviorForAirTypeActive) {
                if (!firstPersonActive && !characterRotatingToSurface) {
                    if (isMoving && isPlayerAiming ()) {
                        currentHorizontalValue = 0;
                        currentVerticalValue = 0;
                    }

                    if (flyModeActive) {
                        if (isPlayerMovingOn2_5dWorld ()) {
                            currentHorizontalValue = 0;
                        }
                    }
                }
            }

            lastHorizontalAnimatorIDValue = currentHorizontalValue;

            animator.SetFloat (horizontalAnimatorID, currentHorizontalValue, currentHorizontalInputLerpSpeed, currentFixedUpdateDeltaTime);

            lastVerticalAnimatorIDValue = currentVerticalValue;

            animator.SetFloat (verticalAnimatorID, currentVerticalValue, currentVerticalInputLerpSpeed, currentFixedUpdateDeltaTime);


            if (obstacleToAvoidMovementFound) {
                currentHorizontalValue = 0;
                currentVerticalValue = 0;
            }

            if (useTankControls) {
                currentInputStrafeHorizontalLerpSpeed = inputTankControlsHorizontalStrafeLerpSpeed;
                currentInputStrafeVerticalLerpSpeed = inputTankControlsVerticalStrafeLerpSpeed;
            } else {
                currentInputStrafeHorizontalLerpSpeed = inputStrafeHorizontalLerpSpeed;
                currentInputStrafeVerticalLerpSpeed = inputStrafeVerticalLerpSpeed;
            }

            if (usedByAI) {
                if (!playerIsAiming && !AIStrafeModeActive) {
                    currentHorizontalValue = 0;
                    currentVerticalValue = 0;
                }
            }

            animator.SetFloat (horizontalStrafeAnimatorID, currentHorizontalValue, currentInputStrafeHorizontalLerpSpeed, currentFixedUpdateDeltaTime);

            animator.SetFloat (verticalStrafeAnimatorID, currentVerticalValue, currentInputStrafeVerticalLerpSpeed, currentFixedUpdateDeltaTime);

            if (strafeModeActive) {
                if (running) {
                    currentMovementSpeed = defaultStrafeRunSpeed;
                } else {
                    currentMovementSpeed = defaultStrafeWalkSpeed;
                }
            } else {
                currentMovementSpeed = defaultStrafeWalkSpeed;
            }

            animator.SetFloat (movementSpeedAnimatorID, currentMovementSpeed, strafeLerpSpeed, currentFixedUpdateDeltaTime);

            if (!playerOnGround) {
                //when the player enables the power gravity and he is floating in the air, set this value to 0 to set 
                //the look like floating animation
                if (gravityPowerActive) {
                    if (lastJumpAnimatorIDValue != 0) {
                        lastJumpAnimatorIDValue = 0;

                        animator.SetFloat (jumpAnimatorID, 0);
                    }
                }
                //else set his jump value as his current rigidbody velocity
                else {
                    if (zeroGravityModeOn || freeFloatingModeOn) {
                        if (lastJumpAnimatorIDValue != 0) {
                            lastJumpAnimatorIDValue = 0;

                            animator.SetFloat (jumpAnimatorID, 0);
                        }
                    } else {
                        currentJumpAnimatorValue = playerTransform.InverseTransformDirection (mainRigidbody.linearVelocity).y;

                        if (jumpLegExternallyActiveState) {
                            currentJumpAnimatorValue = -9;
                        }

                        lastJumpAnimatorIDValue = currentJumpAnimatorValue;

                        animator.SetFloat (jumpAnimatorID, currentJumpAnimatorValue, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime / 2);
                    }
                }
            }

            if (usingJetpack || flyModeActive || turnAndForwardAnimatorValuesPaused) {
                if (lastForwardAnimatorIDValue != 0) {
                    lastForwardAnimatorIDValue = 0;

                    animator.SetFloat (forwardAnimatorID, 0);
                }

                if (lastTurnAnimatorIDValue != 0) {
                    lastTurnAnimatorIDValue = 0;

                    animator.SetFloat (turnAnimatorID, 0);
                }
            }

            //this value is used to know in which leg the player has to jump, left of right
            float runCycle = Mathf.Repeat (animator.GetCurrentAnimatorStateInfo (baseLayerIndex).normalizedTime + runCycleLegOffset, 1);

            float jumpLeg = (runCycle < half ? 1 : -1) * forwardAmount;

            if (playerOnGround) {
                if (lastJumpLegAnimatorIDValue != jumpLeg) {
                    lastJumpLegAnimatorIDValue = jumpLeg;

                    animator.SetFloat (jumpLegAnimatorID, jumpLeg);
                }
            } else {
                if (zeroGravityModeOn || freeFloatingModeOn) {
                    if (currentJumpLeg == 0) {
                        currentJumpLeg = jumpLeg;
                    }

                    if (zeroGravityModeOn) {
                        currentJumpLeg = 1;
                    }

                    currentJumpLeg = Mathf.Lerp (currentJumpLeg, 1, currentFixedUpdateDeltaTime);

                    if (lastJumpLegAnimatorIDValue != currentJumpLeg) {
                        lastJumpLegAnimatorIDValue = currentJumpLeg;

                        animator.SetFloat (jumpLegAnimatorID, currentJumpLeg);
                    }
                }

                if (gravityPowerActive || jumpLegExternallyActiveState) {
                    if (playerUsingWeapons || aimingPowers) {
                        currentJumpLeg = Mathf.Lerp (currentJumpLeg, 1, currentFixedUpdateDeltaTime / 2);
                    } else {
                        currentJumpLeg = Mathf.Lerp (currentJumpLeg, 0, currentFixedUpdateDeltaTime / 2);
                    }

                    lastJumpLegAnimatorIDValue = currentJumpLeg;

                    animator.SetFloat (jumpLegAnimatorID, currentJumpLeg, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime / 2);
                }
            }


            //CHECK FOR FBA HOLDING POSTURE WEAPON CHECK
            if (playerIsAiming && !usingFreeFireMode && !checkToKeepWeaponAfterAimingWeaponFromShooting) {
                aimingModeActiveAnimatorState = true;
            } else {
                if (fullBodyAwarenessActive) {
                    if ((playerIsAiming && weaponsManager.isAimModeInputPressed ()) || playerUsingWeapons) {
                        aimingModeActiveAnimatorState = true;
                    } else {
                        if (weaponsManager.isHolsteringWeaponInProcess () && weaponsManager.isdrawKeepWeaponAnimationInProcess ()) {
                            aimingModeActiveAnimatorState = true;
                        } else {
                            aimingModeActiveAnimatorState = false;
                        }
                    }
                } else {
                    if (playerIsAiming && usingNewWeaponID) {
                        aimingModeActiveAnimatorState = true;
                    } else {
                        aimingModeActiveAnimatorState = false;
                    }
                }
            }

            if (previousValueAimingModeActiveAnimatorState != aimingModeActiveAnimatorState) {
                previousValueAimingModeActiveAnimatorState = aimingModeActiveAnimatorState;

                animator.SetBool (aimingModeActiveAnimatorID, aimingModeActiveAnimatorState);
            }

            updateAnimatorSpeed ();

            animator.SetFloat (playerStatusIDAnimatorID, playerStatusID, playerStatusIDLerpSpeed, currentFixedUpdateDeltaTime);

            animator.SetFloat (idleIDAnimatorID, currentIdleID, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime);

            animator.SetFloat (strafeIDAnimatorID, currentStrafeID, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime);

            animator.SetFloat (crouchIDAnimatorID, currentCrouchID, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime);

            animator.SetFloat (airIDAnimatorID, currentAirID, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime);

            animator.SetFloat (airSpeedAnimatorID, currentAirSpeed, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime);

            animator.SetFloat (shieldActiveAnimatorID, currentShieldActive, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime);

            if (playerUsingInput) {
                animator.SetFloat (inputAmountID, 1, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime);
            } else {
                animator.SetFloat (inputAmountID, 0, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime);
            }

            if (updateUseStrafeLandingEnabled || fullBodyAwarenessActive) {
                if (strafeModeActive) {
                    if (playerOnGround || !ignoreStrafeStateOnAirEnabled || fullBodyAwarenessActive) {
                        currentUseStrafeLanding = 1;
                    } else {
                        currentUseStrafeLanding = 0;
                    }
                } else {
                    currentUseStrafeLanding = 0;
                }

                if (lastCurrentUseStrafeLandingValue != currentUseStrafeLanding) {
                    lastCurrentUseStrafeLandingValue = currentUseStrafeLanding;

                    animator.SetFloat (useStrafeLandingID, currentUseStrafeLanding);
                }
            }
        }

        if (customCharacterControllerActive) {
            currentCustomCharacterControllerBase.updateCharacterControllerAnimator ();
        }
    }

    void updateAnimatorSpeed ()
    {
        if (!animatorSpeedUpdatePaused) {
            //if the player is on ground and moving set the speed of his animator to the properly value
            if (playerOnGround && currentMoveInput.magnitude > 0) {
                currentAnimatorSpeed = animSpeedMultiplier;
            } else {
                currentAnimatorSpeed = 1;
            }
        }

        if (overrideAnimationSpeedActive) {
            currentAnimatorSpeed = animSpeedMultiplier;
        }

        if (regularMovementOnBulletTime) {
            currentAnimatorSpeed *= GKC_Utils.getCurrentDeltaTime ();
        }

        if (currentAnimatorSpeed != previousAnimatorSpeed) {
            previousAnimatorSpeed = currentAnimatorSpeed;

            animator.speed = currentAnimatorSpeed;
        }
    }

    public void setPlayerStatusIDValue (int newValue)
    {
        playerStatusID = newValue;
    }

    public void resetPlayerStatusID ()
    {
        setPlayerStatusIDValue (0);
    }

    public int getPlayerStatusID ()
    {
        return playerStatusID;
    }

    public void setCurrentIdleIDValue (int newValue)
    {
        currentIdleID = newValue;
    }

    public void setCurrentIdleIDValueFloat (float newValue)
    {
        currentIdleID = newValue;
    }

    public float getCurrentIdleID ()
    {
        return currentIdleID;
    }

    public void setCurrentStrafeIDValue (int newValue)
    {
        currentStrafeID = newValue;
    }

    public int getCurrentStrafeIDValue ()
    {
        return currentStrafeID;
    }

    public void setCurrentCrouchIDValue (int newValue)
    {
        currentCrouchID = newValue;
    }

    public int getCurrentCrouchIDValue ()
    {
        return currentCrouchID;
    }

    public void setCurrentAirIDValue (int newValue)
    {
        currentAirID = newValue;
    }

    public void setOriginalAirIDValue ()
    {
        setCurrentAirIDValue (regularAirboneID);
    }

    public void setCurrentAirSpeedValue (int newValue)
    {
        currentAirSpeed = newValue;
    }

    public void setCurrentShieldActiveValue (int newValue)
    {
        currentShieldActive = newValue;
    }

    public int getCurrentAirID ()
    {
        return currentAirID;
    }

    public void updatePlayerStatusIDOnAnimator ()
    {
        if (!customCharacterControllerActive) {
            animator.SetFloat (playerStatusIDAnimatorID, playerStatusID);
        }
    }

    public void updateIdleIDOnAnimator ()
    {
        if (!customCharacterControllerActive) {
            animator.SetFloat (idleIDAnimatorID, currentIdleID);
        }
    }

    public void updateStrafeIDOnAnimator ()
    {
        if (!customCharacterControllerActive) {
            animator.SetFloat (strafeIDAnimatorID, currentStrafeID);
        }
    }

    public void updateAirIDOnAnimator ()
    {
        if (!customCharacterControllerActive) {
            animator.SetFloat (airIDAnimatorID, currentAirID);
        }
    }

    public void updateAirIDAnimatorID ()
    {
        if (!customCharacterControllerActive) {
            animator.SetFloat (airIDAnimatorID, currentAirID);
        }
    }

    public void setCurrentWeaponID (int newValue)
    {
        currentWeaponID = newValue;
    }

    public void updateWeaponID ()
    {
        stopUpdateWeaponIDSmoothlyCoroutine ();

        if (!customCharacterControllerActive) {
            animator.SetFloat (weaponIDAnimatorID, currentWeaponID);
        }
    }

    Coroutine weaponIDSmoothlyCoroutine;

    public void updateWeaponIDSmoothly ()
    {
        stopUpdateWeaponIDSmoothlyCoroutine ();

        weaponIDSmoothlyCoroutine = StartCoroutine (updateWeaponIDSmoothlyCoroutine ());
    }

    void stopUpdateWeaponIDSmoothlyCoroutine ()
    {
        if (weaponIDSmoothlyCoroutine != null) {
            StopCoroutine (weaponIDSmoothlyCoroutine);
        }
    }

    IEnumerator updateWeaponIDSmoothlyCoroutine ()
    {
        bool targetReached = false;

        float timer = 0;

        while (!targetReached) {

            animator.SetFloat (weaponIDAnimatorID, currentWeaponID, updateSetFloatAnimatorSpeed, currentFixedUpdateDeltaTime * 2);

            float temporalweaponIDAnimatorID = animator.GetFloat (weaponIDAnimatorID);

            if (Mathf.Abs (temporalweaponIDAnimatorID - currentWeaponID) < 0.001f) {
                targetReached = true;
            }

            timer += Time.deltaTime;

            if (timer >= 10) {
                targetReached = true;
            }

            yield return null;
        }

        updateWeaponID ();
    }

    public void setUsingNewWeaponID (bool state)
    {
        usingNewWeaponID = state;
    }

    public void setAnimatorTrigger (int triggerID)
    {
        animator.SetTrigger (triggerID);
    }

    public void setAnimatorTrigger (string triggerName)
    {
        int triggerID = Animator.StringToHash (triggerName);

        setAnimatorTrigger (triggerID);
    }

    public void resetAnimatorTrigger (int triggerID)
    {
        animator.ResetTrigger (triggerID);
    }

    public void resetAnimatorTrigger (string triggerName)
    {
        int triggerID = Animator.StringToHash (triggerName);

        resetAnimatorTrigger (triggerID);
    }

    public void setPlayerOnGroundAnimatorStateOnOverrideOnGround (bool state)
    {
        playerOnGroundAnimatorStateOnOverrideOnGround = state;
    }

    public void setPlayerOnGroundAnimatorStateOnOverrideOnGroundWithTime (bool state)
    {
        playerOnGroundAnimatorStateOnOverrideOnGroundWithTime = state;
    }

    public void setPreviousValueOnGroundAnimatorStateValue (bool state)
    {
        previousValueOnGroundAnimatorState = state;
    }

    public void overrideOnGroundAnimatorValue (float newOverrideOnGroundDuration)
    {
        overrideOnGroundDuration = newOverrideOnGroundDuration;

        overrideOnGroundAnimatorValueActive = true;

        lastTimeOverrideOnGroundAnimator = Time.time;
    }

    public void disableOverrideOnGroundAnimatorValue ()
    {
        overrideOnGroundAnimatorValueActive = false;
    }

    bool useCustomMoveSpeedMultiplier;
    float customMoveSpeedMultiplier;

    bool ignoreRootMotionDuringAction;
    public void setIgnoreRootMotionDuringActionState (bool state)
    {
        ignoreRootMotionDuringAction = state;
    }

    //update the velocity of the player rigidbody
    public void OnAnimatorMove ()
    {
        if (!gravityPowerActive && usingAnimator && !useFirstPersonPhysicsInThirdPersonActive) {

            if (!fullBodyAwarenessActive || useRootMotionOnFullBodyAwareness) {
                mainRigidbody.rotation = animator.rootRotation;
            }

            if ((playerOnGround || (actionActive && !actionCanHappenOnAir)) && Time.deltaTime > 0) {
                characterVelocity = vector3Zero;

                bool checkRootMotionDeltaPositionResult = false;

                if (useRootMotionActive || actionActive || forceUseRootMotionActive) {
                    checkRootMotionDeltaPositionResult = true;
                }

                if (actionActive && ignoreRootMotionDuringAction) {
                    checkRootMotionDeltaPositionResult = false;
                }

                if (checkRootMotionDeltaPositionResult) {
                    float currentMoveSpeedMultiplier = 0;

                    if (useCustomMoveSpeedMultiplier && (!actionActive || ignoreFBACustomMoveSpeedMultiplierOnActionActive)) {
                        currentMoveSpeedMultiplier = customMoveSpeedMultiplier;
                    } else {
                        currentMoveSpeedMultiplier = moveSpeedMultiplier;
                    }

                    if (customCharacterScaleActive) {
                        currentMoveSpeedMultiplier *= characterScale;
                    }

                    characterVelocity = (currentMoveSpeedMultiplier * animator.deltaPosition) / Time.deltaTime;
                } else {
                    currentNoRootMovementSpeed = 1;

                    if (running && !crouching) {
                        if (strafeModeActive) {
                            currentNoRootMovementSpeed = noRootRunStrafeMovementSpeed;
                        } else {
                            currentNoRootMovementSpeed = noRootSprintMovementSpeed;
                        }
                    } else {
                        if (crouching) {
                            if (running) {
                                currentNoRootMovementSpeed = noRootCrouchRunMovementSpeed;
                            } else {
                                currentNoRootMovementSpeed = noRootCrouchMovementSpeed;
                            }
                        } else {
                            if (strafeModeActive) {
                                currentNoRootMovementSpeed = noRootWalkStrafeMovementSpeed;
                            } else {
                                if (walkSpeed >= half) {
                                    if (forwardAmount < half) {
                                        currentNoRootMovementSpeed = noRootWalkMovementSpeed / walkSpeed;
                                    } else {
                                        currentNoRootMovementSpeed = noRootRunMovementSpeed * walkSpeed;
                                    }
                                } else {
                                    currentNoRootMovementSpeed = noRootWalkMovementSpeed;
                                }
                            }
                        }
                    }

                    if (!strafeModeActive) {
                        currentNoRootMovementSpeed -= currentMovementSpeed * (Mathf.Abs (turnAmount));
                    }

                    if (Time.time < lastTimeGround + noRootDelayToMoveOnLand) {
                        currentNoRootMovementSpeed = 0;
                    }

                    lerpNoRootMovementSpeed = Mathf.Lerp (lerpNoRootMovementSpeed, currentNoRootMovementSpeed, Time.deltaTime * noRootRunMovementSpeed);

                    if (lockedCameraActive && useRelativeMovementToLockedCamera && !usedByAI) {

                        characterVelocity = lerpNoRootMovementSpeed * lockedCameraMoveInput;

                        characterVelocity = Vector3.ClampMagnitude (characterVelocity, currentNoRootMovementSpeed);
                    } else {
                        characterVelocity = lerpNoRootMovementSpeed * currentMoveInput;
                    }
                }

                mainRigidbody.linearVelocity = characterVelocity;

                if (addExtraCharacterVelocityActive) {
                    mainRigidbody.linearVelocity += currentExtraCharacterVelocity;
                }

                if (actionActive) {

                    if (!allowDownVelocityDuringAction) {
                        if (mainRigidbody.linearVelocity.y < 0) {
                            mainRigidbody.linearVelocity = new Vector3 (mainRigidbody.linearVelocity.x, 0, mainRigidbody.linearVelocity.z);
                        }
                    }
                }

                currentVelocityChangeMagnitude = mainRigidbody.linearVelocity.magnitude;
            }
        }

        if (playerActionSystemLocated && mainPlayerActionSystem.actionActive) {
            mainPlayerActionSystem.OnAnimatorMove ();
        }
    }

    void LateUpdate ()
    {
        if (movingInsideVehicle) {
            //			mainRigidbody.velocity += externalForceInsideVehicle;

            mainRigidbody.transform.Translate (externalForceInsideVehicle, Space.World);
        }

        if (fullBodyAwarenessActive) {
            //look in camera direction when the player is aiming
            lookCameraDirection ();
        }
    }

    public void activateCustomAction (string actionName)
    {
        if (playerActionSystemLocated) {
            mainPlayerActionSystem.activateCustomAction (actionName);
        }
    }

    public void stopCustomAction (string actionName)
    {
        if (playerActionSystemLocated) {
            mainPlayerActionSystem.stopCustomAction (actionName);
        }
    }

    public void stopAllActionsOnActionSystem ()
    {
        if (playerActionSystemLocated) {
            mainPlayerActionSystem.stopAllActions ();
        }
    }

    public string getCurrentActionName ()
    {
        if (playerActionSystemLocated) {
            return mainPlayerActionSystem.getCurrentActionName ();
        }

        return "";
    }

    public void playCurrentAnimationOnPlayerActionSystem ()
    {
        if (playerActionSystemLocated) {
            mainPlayerActionSystem.inputPlayCurrentAnimation ();
        }
    }

    public void setExtraCharacterVelocity (Vector3 extraValue)
    {
        if (!canRagdollMove) {
            extraValue = vector3Zero;
        }

        currentExtraCharacterVelocity = extraValue;

        addExtraCharacterVelocityActive = (extraValue != vector3Zero);
    }

    public void setActionCanHappenOnAirState (bool state)
    {
        actionCanHappenOnAir = state;
    }

    public void setActionActiveState (bool state)
    {
        actionActive = state;

        //If an action is over but some menu is opened when an action is over, make sure to disable the 
        //player movement once the action is over in case the menu is still opened
        if (!actionActive) {
            if (enableChangeScriptStateAfterFinishActionState) {
                if (!playerIsBusy ()) {
                    changeScriptState (true);
                }

                enableChangeScriptStateAfterFinishActionState = false;
            }

            if (disableChangeScriptStateAfterFinishActionState) {
                changeScriptState (false);

                disableChangeScriptStateAfterFinishActionState = false;
            }

            lastTimeActionActive = Time.time;
        }
    }

    public float getLastTimeActionActive ()
    {
        return lastTimeActionActive;
    }

    public bool isActionActive ()
    {
        return actionActive;
    }

    public void setNewActionIDExternally (int newID)
    {
        if (playerActionSystemLocated) {
            mainPlayerActionSystem.setNewActionIDExternally (newID);
        }
    }

    public void setActionActiveExternally (bool state)
    {
        if (playerActionSystemLocated) {
            mainPlayerActionSystem.setActionActiveExternally (state);
        }
    }

    public string getCurrentActionCategoryActive ()
    {
        if (playerActionSystemLocated) {
            return mainPlayerActionSystem.getCurrentActionCategoryActive ();
        }

        return "";
    }

    public void setCurrentCustomActionCategoryID (int newID)
    {
        if (playerActionSystemLocated) {
            mainPlayerActionSystem.setCurrentCustomActionCategoryID (newID);
        }
    }

    public void setEnableChangeScriptStateAfterFinishActionState (bool state)
    {
        enableChangeScriptStateAfterFinishActionState = state;
    }

    public void setDisableChangeScriptStateAfterFinishActionState (bool state)
    {
        disableChangeScriptStateAfterFinishActionState = state;
    }

    public void setActionActiveWithMovementState (bool state)
    {
        actionActiveWithMovement = state;
    }

    public void setIgnoreCameraDirectionOnMovementState (bool state)
    {
        if (state) {
            lastForwardDirection = playerCameraTransform.forward;
            lastRigthDirection = mainCameraTransform.right;
        }

        ignoreCameraDirectionOnMovement = state;
    }


    public void setIgnoreCameraDirectionOnStrafeMovementState (bool state)
    {
        ignoreCameraDirectionOnStrafeMovement = state;
    }

    public void setUseRootMotionActiveState (bool state)
    {
        useRootMotionActive = state;
    }

    public void setForceUseRootMotionActiveState (bool state)
    {
        forceUseRootMotionActive = state;
    }

    public void setOriginalUseRootMotionActiveState ()
    {
        setUseRootMotionActiveState (originalSetUseRootMotionActiveState);
    }

    public void setNoRootMotionValues (float noRootWalkMovementSpeedValue, float noRootRunMovementSpeedValue,
                                       float noRootSprintMovementSpeedValue, float noRootCrouchMovementSpeedValue,
                                       float noRootCrouchRunMovementSpeedValue,
                                       float noRootWalkStrafeMovementSpeedValue, float noRootRunStrafeMovementSpeedValue)
    {
        noRootWalkMovementSpeed = noRootWalkMovementSpeedValue;
        noRootRunMovementSpeed = noRootRunMovementSpeedValue;
        noRootSprintMovementSpeed = noRootSprintMovementSpeedValue;
        noRootCrouchMovementSpeed = noRootCrouchMovementSpeedValue;
        noRootCrouchRunMovementSpeed = noRootCrouchRunMovementSpeedValue;
        noRootWalkStrafeMovementSpeed = noRootWalkStrafeMovementSpeedValue;
        noRootRunStrafeMovementSpeed = noRootRunStrafeMovementSpeedValue;
    }

    public void setOriginalNoRootMotionValues ()
    {
        setNoRootMotionValues (originalNoRootWalkMovementSpeed, originalNoRootRunMovementSpeed,
            originalNoRootSprintMovementSpeed, originalNoRootCrouchMovementSpeed, originalNoRootCrouchRunMovementSpeed,
            originalNoRootWalkStrafeMovementSpeed, originalNoRootRunStrafeMovementSpeed);
    }

    public void setDeactivateRootMotionOnStrafeActiveOnLockedViewState (bool state)
    {
        deactivateRootMotionOnStrafeActiveOnLockedView = state;

        previousStrafeModeActive = false;

        if (deactivateRootMotionOnStrafeActiveOnLockedView) {
            if (strafeModeActive) {
                setUseRootMotionActiveState (false);
            }
        } else {
            setOriginalUseRootMotionActiveState ();
        }
    }

    public void setRotateDirectlyTowardCameraOnStrafeState (bool state)
    {
        rotateDirectlyTowardCameraOnStrafe = state;
    }

    public void setAllowDownVelocityDuringActionState (bool state)
    {
        allowDownVelocityDuringAction = state;
    }

    public void setPlayerControllerMovementValues (float moveSpeedMultiplierValue, float animSpeedMultiplierValue, float jumpPowerValue, float airSpeedValue, float airControlValue)
    {
        setMoveSpeedMultiplierValue (moveSpeedMultiplierValue);

        setAnimSpeedMultiplierValue (animSpeedMultiplierValue);

        setJumpPower (jumpPowerValue);

        airSpeed = airSpeedValue;
        airControl = airControlValue;

        noAnimatorAirSpeed = airSpeedValue;
    }

    public void resetPlayerControllerMovementValues ()
    {
        setMoveSpeedMultiplierValue (originalMoveSpeedMultiplier);

        setAnimSpeedMultiplierValue (originalAnimationSpeed);

        setOriginalJumpPower ();

        airSpeed = originalAirSpeed;
        airControl = originalAirControl;

        noAnimatorAirSpeed = originalNoAnimatorAirSpeed;
    }

    public void setAirSpeedValue (float newAirSpeedValue)
    {
        airSpeed = newAirSpeedValue;
    }

    public void setAirControlValue (float newAirControlValue)
    {
        airControl = newAirControlValue;
    }

    public void setOriginalAirSpeed ()
    {
        setAirSpeedValue (originalAirSpeed);
    }

    public void setOriginalAirControl ()
    {
        setAirControlValue (originalAirControl);
    }

    public void setIgnoreInputOnAirControlActiveState (bool state)
    {
        ignoreInputOnAirControlActive = state;
    }

    public void setAnimatorSpeedUpdatePausedState (bool state)
    {
        animatorSpeedUpdatePaused = state;
    }

    public void setOverrideAnimationSpeedActiveState (bool state)
    {
        overrideAnimationSpeedActive = state;
    }

    public void updateCurrentAnimatorSpeed (float newSpeed)
    {
        currentAnimatorSpeed = newSpeed;
    }

    public void setNoAnimatorMovementSpeedMultiplierValue (float newValue)
    {
        noAnimatorMovementSpeedMultiplier = newValue;
    }

    public void setOriginalNoAnimatorMovementSpeedMultiplierValue ()
    {
        setNoAnimatorMovementSpeedMultiplierValue (originalNoAnimatorMovementSpeedMultiplier);
    }

    public Transform getCurrentSurfaceBelowPlayer ()
    {
        return currentSurfaceBelowPlayer;
    }

    public bool checkIfPlayerOnGroundWithRaycast ()
    {
        if (playerTransform == null) {
            return false;
        }

        if (Physics.Raycast (playerTransform.position + playerTransform.up, -playerTransform.up, 1.3f, layer)) {
            return true;
        }

        return false;
    }

    public void setCheckOnGroungPausedState (bool state)
    {
        checkOnGroundPaused = state;
    }

    float downSpeedValue = 0;

    public bool isPlayerOnGround ()
    {
        return playerOnGround;
    }

    public void setPlayerOnGroundState (bool state)
    {
        playerOnGround = state;
    }

    public void setOnGroundState (bool state)
    {
        playerOnGround = state;

        if (checkOnGroundStatePaused || checkOnGroundStatePausedFFOrZG) {
            playerOnGround = false;
        }
    }

    public bool isStrafeModeActive ()
    {
        return strafeModeActive;
    }

    public void updateStrafeModeActiveState ()
    {
        strafeModeActive = ((playerIsAiming && lookInCameraDirectionOnFreeFireActive) || lookInCameraDirectionActive) &&
        (playerOnGround || !ignoreStrafeStateOnAirEnabled || fullBodyAwarenessActive);
    }

    public void updateWallRunningState ()
    {
        if (wallRunningEnabled) {
            wallRunningExternalControllerBehavior.updateControllerBehavior ();
        }
    }

    public void checkIfActivateWallRunningState ()
    {
        if (wallRunningEnabled) {
            wallRunningExternalControllerBehavior.checkIfActivateExternalForce ();
        }
    }

    public bool isWallRunningActive ()
    {
        return wallRunningActive;
    }

    public void setWallRunningActiveValue (bool state)
    {
        wallRunningActive = state;
    }

    public bool isExternalControlBehaviorForAirTypeActive ()
    {
        return externalControlBehaviorForAirTypeActive;
    }

    public void setWallRunningActiveState (bool state)
    {
        if (wallRunningEnabled) {
            wallRunningExternalControllerBehavior.setExternalForceActiveState (state);
        }
    }

    public void setWallRunningImpulseForce (Vector3 forceAmount, bool useCameraDirection)
    {
        if (wallRunningEnabled) {
            wallRunningExternalControllerBehavior.setExtraImpulseForce (forceAmount, useCameraDirection);
        }
    }

    public void setWallRunningEnabledState (bool state)
    {
        if (wallRunningEnabled) {
            wallRunningExternalControllerBehavior.setExternalForceEnabledState (state);
        }
    }

    public void setPauseAllPlayerDownForces (bool state)
    {
        pauseAllPlayerDownForces = state;
    }

    public void setLadderFoundState (bool state)
    {
        ladderFound = state;

        if (ladderFound) {
            if (wallRunningActive) {
                setWallRunningActiveState (false);
            }
        } else {
            jumpInput = false;

            lastJumpActivatedExternally = false;
        }
    }

    public bool isLadderFound ()
    {
        return ladderFound;
    }

    public bool isPauseAllPlayerDownForcesActive ()
    {
        return pauseAllPlayerDownForces;
    }

    public void setUseObstacleDetectionToAvoidMovementState (bool state)
    {
        useObstacleDetectionToAvoidMovement = state;
    }

    public void setCheckQuickMovementTurn180DegreesOnRunState (bool state)
    {
        checkQuickMovementTurn180DegreesOnRun = state;
    }

    public void setAirDashEnabledState (bool state)
    {
        airDashEnabled = state;
    }

    public void doAirDash ()
    {
        if (characterRotatingToSurface) {
            return;
        }

        if (ignoreExternalActionsActiveState) {
            return;
        }

        if (useDashLimit) {
            if (currentNumberOfDash >= dashLimit) {
                return;
            }
        }

        if (airDashEnabled) {
            bool canActivateAirDashResult = false;

            if (Time.time > lastTimeDash + airDashColdDown && Time.time > lastTimeFalling + minTimeOnAirToUseAirDash) {
                canActivateAirDashResult = true;
            }

            if (!canActivateAirDashResult) {
                return;
            }

            Vector3 dashDirection = currentMoveInput;

            if (dashDirection == vector3Zero || dashDirection.magnitude < 0.1f) {
                if (firstPersonActive) {
                    dashDirection = playerCameraTransform.forward;
                } else {
                    dashDirection = playerTransformForward;
                }
            }

            if (resetGravityForceOnDash) {
                mainRigidbody.linearVelocity = vector3Zero;
            }

            mainRigidbody.AddForce (mainRigidbody.mass * airDashForce * dashDirection, ForceMode.Impulse);

            lastTimeDash = Time.time;

            if (pauseGravityForce) {
                setGravityForcePuase (true);

                lastTimeGravityForcePaused = Time.time;

                unPauseGravityForceActive = true;
            }

            if (useDashLimit) {
                currentNumberOfDash++;
            }

            if (changeCameraFovOnDash) {
                playerCameraManager.setMainCameraFovStartAndEnd (cameraFovOnDash, playerCameraManager.getOriginalCameraFov (), cameraFovOnDashSpeed);
            }

            if (useEventsOnAirDash) {
                if (firstPersonActive) {
                    eventOnAirDashFirstPerson.Invoke ();
                } else {
                    eventOnAirDashThirdPerson.Invoke ();
                }
            }
        }
    }

    public void addExternalForce (Vector3 forceDireciton)
    {
        mainRigidbody.AddForce (mainRigidbody.mass * forceDireciton, ForceMode.Impulse);
    }

    public void setGravityForcePuase (bool state)
    {
        gravityForcePaused = state;

        unPauseGravityForceActive = false;
    }

    public bool isGravityForcePaused ()
    {
        return gravityForcePaused;
    }

    public void setRigidbodyVelocityToZero ()
    {
        mainRigidbody.linearVelocity = vector3Zero;
    }

    public float getCurrentDeltaTime ()
    {
        currentDeltaTime = Time.deltaTime;

        if (regularMovementOnBulletTime) {
            currentDeltaTime *= GKC_Utils.getCurrentDeltaTime ();
        }

        return currentDeltaTime;
    }

    float getCurrentFixedDeltaTime ()
    {
        float currentFixedDeltaTime = Time.fixedDeltaTime;

        if (regularMovementOnBulletTime) {
            currentFixedDeltaTime *= GKC_Utils.getCurrentDeltaTime ();
        }

        return currentFixedDeltaTime;
    }

    public float getCurrentScaleTime ()
    {
        return GKC_Utils.getCurrentDeltaTime ();
    }

    //set the scale of the capsule if the player is crouched
    void scaleCapsule (bool state)
    {
        if (state) {
            capsule.height = capsuleHeightOnCrouch;

            if (setCustomCapsuleCenter) {
                capsule.center = customCapsuleCenterOnCrouch;
            } else {
                capsule.center = (capsuleHeightOnCrouch * half) * Vector3Up;
            }
        } else {
            capsule.height = originalHeight;

            if (setCustomCapsuleCenter) {
                capsule.center = customCapsuleCenter;
            } else {
                capsule.center = (originalHeight * half) * Vector3Up;
            }
        }

        currentHeight = capsule.height;
    }

    public void setPlayerColliderCapsuleScale (float capsuleHeight)
    {
        capsule.height = capsuleHeight;

        capsule.center = (capsuleHeight * half) * Vector3Up;

        currentHeight = capsule.height;
    }

    public void setPlayerColliderCapsuleScaleWithCenterAsRadius (float capsuleHeight)
    {
        setPlayerColliderCapsuleScale (capsuleHeight);

        Vector3 capsuleCenter = capsule.center;

        if (capsuleCenter.y < capsule.radius) {
            capsuleCenter = new Vector3 (capsuleCenter.x, capsule.radius, capsuleCenter.z);

            capsule.center = capsuleCenter;
        }
    }

    public void setCapsuleHeightOnCrouch (float newValue)
    {
        capsuleHeightOnCrouch = newValue;
    }

    public void setPlayerColliderCapsuleCenter (Vector3 newValue)
    {
        capsule.center = newValue;
    }

    public void setPlayerCapsuleColliderDirection (int newDirection)
    {
        capsule.direction = newDirection;
    }

    public void setPlayerCapsuleColliderRadius (float newValue)
    {
        capsule.radius = newValue;
    }

    public void setOriginalPlayerCapsuleColliderRadius ()
    {
        setPlayerCapsuleColliderRadius (originalRadius);
    }

    public void setOriginalPlayerColliderCapsuleScale ()
    {
        scaleCapsule (false);
    }

    public void setOriginalPlayerColliderCapsuleScaleIfCrouchNotActive ()
    {
        if (!crouching) {
            scaleCapsule (false);
        } else {
            healthManager.changePlaceToShootPosition (true);
        }
    }

    public CapsuleCollider getMainCapsuleCollider ()
    {
        return capsule;
    }

    public Bounds getMainCapsuleColliderBounds ()
    {
        return capsule.bounds;
    }

    public void setCharacterRadius (float newValue)
    {
        characterRadius = newValue;
    }

    public float getCharacterRadius ()
    {
        return characterRadius;
    }

    public void setCharacterCustomScale (float newValue)
    {
        characterScale = newValue;

        customCharacterScaleActive = characterScale != 1;
    }

    public void setPlaceToShootPositionOffset (float newValue)
    {
        if (newValue > 0) {
            healthManager.setPlaceToShootPositionOffset (newValue);
        } else {
            healthManager.changePlaceToShootPosition (false);
        }
    }

    public void setPlaceToShootLocalPosition (Vector3 newValue)
    {
        healthManager.setPlaceToShootLocalPosition (newValue);
    }

    public void setCheckFallStatePausedState (bool state)
    {
        checkFallStatePaused = state;
    }

    public Collider getMainCollider ()
    {
        return mainCollider;
    }

    public void setMainColliderState (bool state)
    {
        if (!canRagdollMove && state) {
            return;
        }

        mainCollider.enabled = state;
    }

    public void setMainColliderTriggerState (bool state)
    {
        mainCollider.isTrigger = state;
    }

    public bool isMainColliderEnabled ()
    {
        return mainCollider.enabled;
    }

    public void reactivateColliderIfPossible ()
    {
        if (mainCollider.enabled) {
            setMainColliderState (false);

            setMainColliderState (true);
        }
    }

    public void addOrRemoveExtraColliders (Collider newCollider, bool state)
    {
        if (newCollider == null) {
            return;
        }

        if (state) {
            if (!extraColliderList.Contains (newCollider)) {
                extraColliderList.Add (newCollider);

                useExtraColliderList = true;
            }
        } else {
            if (extraColliderList.Contains (newCollider)) {
                extraColliderList.Remove (newCollider);

                if (extraColliderList.Count == 0) {
                    useExtraColliderList = false;
                }
            }
        }
    }

    public void setIgnoreCollisionOnExternalCollider (Collider newCollider, bool state)
    {
        if (newCollider == null) {
            return;
        }

        Physics.IgnoreCollision (newCollider, mainCollider, state);

        if (useExtraColliderList) {
            int extraColliderListCount = extraColliderList.Count;

            for (int i = 0; i < extraColliderListCount; i++) {
                if (extraColliderList [i] != null) {
                    Physics.IgnoreCollision (newCollider, extraColliderList [i], state);
                }
            }
        }
    }

    public void setIgnoreCollisionOnExternalColliderOnlyWithExtraColliderList (Collider newCollider, bool state)
    {
        if (useExtraColliderList) {
            if (newCollider == null) {
                return;
            }

            int extraColliderListCount = extraColliderList.Count;

            for (int i = 0; i < extraColliderListCount; i++) {
                if (extraColliderList [i] != null) {
                    Physics.IgnoreCollision (newCollider, extraColliderList [i], state);
                }
            }
        }
    }

    public bool isUseExtraColliderListActive ()
    {
        return useExtraColliderList;
    }

    public List<Collider> getExtraColliderList ()
    {
        if (useExtraColliderList) {
            return extraColliderList;
        }

        return null;
    }

    public int getCharacterLayer ()
    {
        return gameObject.layer;
    }

    public void setGravityMultiplierValue (bool setOriginal, float newValue)
    {
        if (setOriginal) {
            currentGravityMultiplier = originalGravityMultiplier;

            gravityMultiplier = originalGravityMultiplier;
        } else {
            currentGravityMultiplier = newValue;

            gravityMultiplier = newValue;
        }
    }

    public void setGravityMultiplierValueFromExternalFunction (float newValue)
    {
        gravityMultiplier = newValue;
    }

    public float getGravityMultiplier ()
    {
        return gravityMultiplier;
    }

    public void setGravityForceValue (bool setOriginal, float newValue)
    {
        if (setOriginal) {
            gravityForce = originalGravityForce;
        } else {
            gravityForce = newValue;
        }
    }

    public void setOriginalGravityForceValue ()
    {
        setGravityForceValue (true, 0);
    }

    public float getGravityForce ()
    {
        return gravityForce;
    }

    public void setSlowFallExternallyActiveState (bool state)
    {
        slowFallExternallyActive = state;
    }

    public bool isSlowFallExternallyActive ()
    {
        return slowFallExternallyActive;
    }

    //CALL INPUT FUNCTIONS
    public bool canUseInput ()
    {
        if (jetPackEquiped) {
            return false;
        }

        if (flyModeActive) {
            return false;
        }

        if (!canMove) {
            return false;
        }

        if (usedByAI) {
            return false;
        }

        if (playerNavMeshEnabled) {
            return false;
        }

        if (driving) {
            return false;
        }

        if (swimModeActive) {
            return false;
        }

        if (closeCombatAttackInProcess) {
            return false;
        }

        return true;
    }

    public void inputCrouch ()
    {
        if (!playerActionsInputEnabled) {
            return;
        }

        if (crouchInputPaused) {
            return;
        }

        if (!inputCanBeUsed || !enabled) {
            return;
        }

        if (isUsingSubMenu () || isPlayerMenuActive ()) {
            return;
        }

        if (usingGenericModelActive) {
            return;
        }

        if (!playerCameraManager.isFirstPersonActive ()) {
            if (playerUsingWeapons) {
                if (weaponsManager.checkIfWeaponStateIsBusy () && !crouching) {
                    return;
                }
            }
        }

        //check if the crouch button has been pressed
        crouch ();
    }

    public void inputJump ()
    {
        if (!playerActionsInputEnabled) {
            return;
        }

        if (!inputCanBeUsed || !enabled) {
            return;
        }

        if (!canJumpActive) {
            return;
        }

        if (jumpInputPaused) {
            return;
        }

        if (checkObstacleAbovePlayerOnJumpEnabled) {
            RaycastHit newRaycastHit = new RaycastHit ();

            bool canJumpResult = true;

            if (Physics.Raycast (playerTransform.position + playerTransform.up, playerTransform.up, out newRaycastHit,
                checkObstacleAbovePlayerOnJumpRaycastDistance, checkObstacleAbovePlayerOnJumpLayermask)) {
                if (!isColliderPartOfCharacter (newRaycastHit.collider)) {
                    canJumpResult = true;
                } else {
                    if (newRaycastHit.rigidbody == null) {
                        canJumpResult = true;
                    }
                }
            }

            if (!canJumpResult) {
                if (showDebugPrint) {
                    print ("obstacle detected above the player, cancelling jump");
                }

                return;
            }
        }

        activateJump ();
    }

    public bool isColliderPartOfCharacter (Collider newCollider)
    {
        if (newCollider == null) {
            return false;
        }

        if (newCollider == mainCollider) {
            return true;
        }

        if (useExtraColliderList) {
            if (extraColliderList.Contains (newCollider)) {
                return true;
            }
        }

        return false;
    }

    public void inputReleaseJumpButton ()
    {
        if (!playerActionsInputEnabled) {
            return;
        }

        if (!inputCanBeUsed || !enabled) {
            return;
        }

        if (!canJumpActive) {
            return;
        }

        releaseJumpButton ();
    }

    public void setJumpsAmountValue (int newValue)
    {
        jumpsAmount = newValue;
    }

    public void activateJump ()
    {
        setJumpActive (false);
    }

    public void setJumpActive (bool activateJumpOnAir)
    {
        if (wallRunningActive) {
            if (wallRunningEnabled) {
                wallRunningExternalControllerBehavior.setJumpActiveForExternalForce ();
            }

            return;
        }

        if (externalControllBehaviorActive) {
            if (currentExternalControllerBehavior.externalControllerJumpEnabled &&
                currentExternalControllerBehavior.isBehaviorCurrentlyActive ()) {
                currentExternalControllerBehavior.setJumpActiveForExternalForce ();

                return;
            }
        }

        if (enabledRegularJump) {
            jumpInput = true;

            jumpActivatedByInput = true;

            if (ladderFound || activateJumpOnAir) {
                jumpInput = false;
            }

            if (!playerOnGround && !gravityPowerActive) {
                if ((ladderFound || enabledDoubleJump) && !zeroGravityModeOn && !freeFloatingModeOn) {
                    //then check the last time the jump button has been pressed, so the player can jump again  
                    //jump again
                    if (Time.time > lastJumpTime + readyToDoubleJumpTime && (jumpsAmount < maxNumberJumpsInAir || ladderFound)) {
                        doubleJump = true;
                    }
                }
            }

            if (activateJumpOnAir) {
                doubleJump = true;
            }

            if (waitingToActivateJumpInputWithDelay) {
                doubleJump = false;

                if (showDebugPrint) {
                    print ("cancel jump on air");
                }
            }

            if (addJumpForceWhileButtonPressed) {
                bool canActivateJumpForceOnButtonPressed = false;

                if (playerOnGround) {
                    canActivateJumpForceOnButtonPressed = true;
                } else {
                    if (useJumpForceOnAirJumpEnabled) {
                        canActivateJumpForceOnButtonPressed = true;
                    }
                }

                if (canActivateJumpForceOnButtonPressed) {
                    lastTimeJumpButtonHoldActive = Time.time;

                    jumpButtonHoldActive = true;
                }
            }

            if (jumpInput || doubleJump) {
                lastTimeJumpActivated = Time.time;
            }
        }
    }

    public void releaseJumpButton ()
    {
        if (addJumpForceWhileButtonPressed) {
            jumpButtonHoldActive = false;
        }
    }

    public void relaseJumpButtonIfAddForceWhileButtonPressedActive ()
    {
        if (addJumpForceWhileButtonPressed && !playerOnGround && jumpButtonHoldActive) {
            releaseJumpButton ();
        }
    }

    public void inputStartSlowDownFall ()
    {
        if (!playerActionsInputEnabled) {
            return;
        }

        if (!inputCanBeUsed || !enabled) {
            return;
        }

        if (holdJumpSlowDownFallEnabled) {
            if (Time.time > waitTimeToSlowDown) {

                gravityMultiplier = slowDownGravityMultiplier;

                if (!slowingFall) {
                    slowingFallInput = true;
                }
            }
        }
    }

    public void inputStopSlowDownFall ()
    {
        if (!playerActionsInputEnabled) {
            return;
        }

        if (!inputCanBeUsed || !enabled) {
            return;
        }

        if (holdJumpSlowDownFallEnabled) {
            gravityMultiplier = currentGravityMultiplier;
        }
    }

    public void inputAirDash ()
    {
        if (!playerActionsInputEnabled) {
            return;
        }

        if (!inputCanBeUsed || !enabled) {
            return;
        }

        if (airDashEnabled && !gravityPowerActive && !playerOnGround && !gravityManager.isSearchingSurface () && !usingAbilityActive && canMove) {
            doAirDash ();
        }
    }

    public void inputSetMoveVerticallyState (bool state)
    {
        if (!playerActionsInputEnabled) {
            return;
        }

        if (!inputCanBeUsed || !enabled) {
            return;
        }

        if (zeroGravityModeOn || freeFloatingModeOn) {
            if (canMoveVertically && isPlayerMovingOn3dWorld ()) {
                movingVertically = state;
            }
        }
    }

    public void inputSetChangeZeroGravityMovementSpeeState (bool state)
    {
        if (!playerActionsInputEnabled) {
            return;
        }

        if (!inputCanBeUsed || !enabled) {
            return;
        }

        if (zeroGravityModeOn) {
            changeZeroGravityMovementVelocity (state);
        }
    }

    public void inputSetChangeFreeFloatingMovementSpeeState (bool state)
    {
        if (!playerActionsInputEnabled) {
            return;
        }

        if (!inputCanBeUsed || !enabled) {
            return;
        }

        if (freeFloatingModeOn) {
            changeFreeFloatingMovementVelocity (state);
        }
    }

    public void inputSetIncreaseMovementSpeedState (bool state)
    {
        if (!playerActionsInputEnabled) {
            return;
        }

        if (jetPackEquiped || flyModeActive || swimModeActive || !canMove || usedByAI || playerNavMeshEnabled || driving) {
            return;
        }

        if (!enabled) {
            return;
        }

        if (runInputPaused) {
            return;
        }

        if (increaseWalkSpeedEnabled) {
            if (holdButtonToKeepIncreasedWalkSpeed) {
                setIncreaseMovementSpeedState (state);
            } else {
                if (state) {
                    increaseWalkSpeedActive = !increaseWalkSpeedActive;

                    setIncreaseMovementSpeedState (increaseWalkSpeedActive);
                }
            }
        }
    }

    public void inputStartToRun ()
    {
        if (runInputPaused) {
            return;
        }

        //check if the player is moving and he is not using the gravity power
        //in that case according to the duration of the press key, the player will only run or run and change his gravity
        //also in the new version of the asset also check if the touch control is being used
        if (!playerIsBusy () && canMove && isSprintEnabled ()) {
            checkIfCanRun ();
        }
    }

    public void inputStopToRun ()
    {
        //if the run button is released, stop the run power, if the power was holding the run button to adhere to other surfaces, else the 
        //run button has to be pressed again to stop the run power
        if (!playerIsBusy () && canMove) {
            checkIfStopRun ();
        }
    }

    public void inputToggleWalkAndRun ()
    {
        if (runInputPaused) {
            return;
        }

        if (!playerIsBusy () && canMove) {
            toggleWalkRunState = !toggleWalkRunState;

            if (toggleWalkRunState) {
                previousWalkSpeedValue = originalWalkSpeedValue;

                increaseWalkSpeedValue = 1;

                originalWalkSpeedValue = half;

                setWalkBydefaultFromEditor ();
            } else {
                originalWalkSpeedValue = previousWalkSpeedValue;

                setWalkSpeedValue (originalWalkSpeedValue);

                setSprintEnabledState (originalSprintEnabledValue);

                increaseWalkSpeedValue = 1;
            }
        }
    }

    //END OF INPUT CALL FUNCTIONS

    public void checkIfCanRun ()
    {
        bool canActivateRunResult = false;

        if (getMoveInputDirection ().magnitude > 0 &&
            !isGravityPowerActive () &&
            (isPlayerOnGround () || wallRunningActive || externalControlBehaviorForAirTypeActive) &&
            !gravityManager.isSearchingSurface ()) {

            canActivateRunResult = true;
        }

        if (running) {
            canActivateRunResult = true;
        }

        if (canActivateRunResult) {
            //avoid to run backwards
            if (playerCanRunNow ()) {
                //check if he can run while crouching
                if (!isCrouching () || (runOnCrouchEnabled && isCrouching ())) {
                    //check the amount of time that the button is being pressed
                    runButtonPressedTimer += Time.deltaTime;

                    if (!running) {
                        activateRun ();

                        lastTimeRun = runButtonPressedTimer;
                    }
                }
            }
        }
    }

    public void activateRun ()
    {
        lastTimeMovingOnRun = Time.time;

        setRunnningState (true);

        if (weaponsManagerLocated) {
            weaponsManager.setRunningState (true);
        }
    }

    public void checkIfStopRun ()
    {
        if (running && (runButtonPressedTimer > half || runButtonPressedTimer - lastTimeRun > 0.12f)) {
            forceStopRun ();
        }
    }

    public void forceStopRun ()
    {
        stopRun ();

        runButtonPressedTimer = 0;

        lastTimeRun = 0;
    }

    public void stopRun ()
    {
        setRunnningState (false);

        if (weaponsManagerLocated) {
            weaponsManager.setRunningState (false);
        }
    }

    public float getRunButtonPressedTimer ()
    {
        return runButtonPressedTimer;
    }

    public bool playerIsBusy ()
    {
        if (isUsingDevice ()) {
            return true;
        }

        if (isTurnBasedCombatActionActive ()) {
            return false;
        }

        if (isUsingSubMenu ()) {
            return true;
        }

        if (isPlayerMenuActive ()) {
            return true;
        }

        return false;
    }

    public void setSlidindOnThirdPersonActiveState (bool state)
    {
        slidindOnThirdPersonActive = state;
    }

    //check with a sphere cast if the there are any surface too close
    public void crouch ()
    {
        if (slidindOnThirdPersonActive) {
            if (useCrouchSlidingOnThirdPersonEnabled) {

                slidindOnThirdPersonActive = false;

                eventOnCrouchSlidingThirdPersonEnd.Invoke ();

                return;
            }
        }

        if (!gravityPowerActive && checkWeaponsStateToCrouch () && playerOnGround) {
            if (crouchSlidingEnabled) {
                if (!firstPersonActive) {
                    if (!crouching) {

                        if (running) {
                            if (useCrouchSlidingOnThirdPersonEnabled && !slidindOnThirdPersonActive) {

                                slidindOnThirdPersonActive = true;

                                eventOnCrouchSlidingThirdPersonStart.Invoke ();

                                return;
                            }
                        }
                    }
                }
            }

            crouching = !crouching;

            if (!crouching) {
                //check if there is anything above the player when he is crouched, to prevent he stand up
                //stop the player to get up
                if (!playerCanGetUpFromCrouch ()) {
                    crouching = true;
                }
            }

            if (!firstPersonActive) {
                if (weaponsManagerLocated) {
                    if (weaponsManager.isIgnoreCrouchWhileWeaponActive () || !weaponsManager.isAimingWeapons ()) {
                        if (!fullBodyAwarenessActive) {
                            weaponsManager.enableOrDisableIKOnHands (!crouching);
                        }
                    }
                }
            }

            //set the pivot position
            setCrouchComponentsState (crouching);

            if (crouchSlidingEnabled && running) {
                if (firstPersonActive) {
                    if (crouching) {
                        crouchSlidingActive = true;

                        lastTimeCrouchSlidingActive = Time.time;

                        eventOnCrouchSlidingStart.Invoke ();
                    } else {
                        stopCrouchSliding ();
                    }
                }
            }
        } else if (crouchSlidingOnAirEnabled) {
            if (!gravityPowerActive && checkWeaponsStateToCrouch () && !playerOnGround) {
                if (crouchSlidingEnabled && running) {

                    if (firstPersonActive) {

                        crouchingOnAir = !crouchingOnAir;

                        if (crouchingOnAir) {
                            setCrouchComponentsState (true);

                            crouchSlidingOnAirActive = true;

                            crouchSlidingActive = true;

                            lastTimeCrouchSlidingActive = Time.time;

                            eventOnCrouchSlidingStart.Invoke ();
                        } else {
                            stopCrouchSliding ();
                        }
                    }
                }
            }
        }
    }

    bool checkWeaponsStateToCrouch ()
    {
        if (playerCameraManager.isFirstPersonActive ()) {
            return true;
        }

        //		if (aimingPowers) { 
        //			return false;
        //		}

        if (weaponsManagerLocated) {
            if (weaponsManager.isAimingWeapons () && weaponsManager.isIgnoreCrouchWhileWeaponActive ()) {
                return false;
            }

            if (!canCrouchWhenUsingWeaponsOnThirdPerson && playerUsingWeapons) {
                return false;
            }
        }

        if (crouching) {
            return true;
        }

        return true;
    }

    public void checkIfActivateCrouchState ()
    {
        if (!playerCanGetUpFromCrouch () && !crouching) {
            crouch ();
        }
    }

    public void setCrouchState (bool state)
    {
        if (crouching) {
            if (!state) {
                crouch ();
            }
        } else {
            if (state) {
                crouch ();
            }
        }
    }

    public void setCrouchComponentsState (bool state)
    {
        playerCameraManager.crouch (state);

        healthManager.changePlaceToShootPosition (state);

        scaleCapsule (state);

        setLastTimeMoved ();
    }

    public void stopCrouchSliding ()
    {
        if (crouchSlidingActive || crouchingOnAir) {
            eventOnCrouchSlidingEnd.Invoke ();

            crouchSlidingActive = false;

            if (crouchingOnAir && playerCanGetUpFromCrouch () && !crouching) {
                setCrouchComponentsState (false);
            }

            crouchingOnAir = false;

            crouchSlidingOnAirActive = false;
        }
    }

    public void setCrouchSlidingEnabledState (bool state)
    {
        crouchSlidingEnabled = state;
    }

    public bool playerCanGetUpFromCrouch ()
    {
        crouchRay = new Ray (mainRigidbody.position + (originalRadius * half) * playerTransformUp, playerTransformUp);

        float crouchRayLength = originalHeight - originalRadius * half;

        if (useCustomLayerToCheckIfPlayerCanGetUpFromCrouch) {
            if (Physics.SphereCast (crouchRay, originalRadius * half, crouchRayLength, customLayerToCheckIfPlayerCanGetUpFromCrouch)) {
                return false;
            }
        } else {
            if (Physics.SphereCast (crouchRay, originalRadius * half, crouchRayLength, layer)) {
                return false;
            }
        }

        return true;
    }

    public bool isCrouching ()
    {
        return crouching;
    }

    public bool isCanCrouchWhenUsingWeaponsOnThirdPerson ()
    {
        return canCrouchWhenUsingWeaponsOnThirdPerson;
    }

    public void setChoosingGravityDirectionState (bool state)
    {
        choosingGravityDirection = state;
    }

    public void setGravityPowerActiveState (bool state)
    {
        gravityPowerActive = state;
    }

    public bool isGravityPowerActive ()
    {
        return gravityPowerActive;
    }

    public bool isChoosingGravityDirection ()
    {
        return choosingGravityDirection;
    }

    public void setUsingCloseCombatActiveState (bool state)
    {
        usingCloseCombatActive = state;
    }

    public bool isUsingCloseCombatActive ()
    {
        return usingCloseCombatActive;
    }

    public bool iscloseCombatAttackInProcess ()
    {
        return closeCombatAttackInProcess;
    }

    public void setCloseCombatAttackInProcessState (bool state)
    {
        closeCombatAttackInProcess = state;
    }

    public bool isMeleeAttackInProcess ()
    {
        return meleeAttackInProcess;
    }

    public void setMeleeAttackInProcessState (bool state)
    {
        meleeAttackInProcess = state;
    }

    public void setJumpLegExternallyActiveState (bool state)
    {
        jumpLegExternallyActiveState = state;
    }

    public void setIgnoreExternalActionsActiveState (bool state)
    {
        ignoreExternalActionsActiveState = state;
    }

    public bool isIgnoreExternalActionsActiveState ()
    {
        return ignoreExternalActionsActiveState;
    }

    public void setRunnningState (bool state)
    {
        running = state;

        if (useEventOnSprint) {
            if (running) {
                eventOnStartSprint.Invoke ();
            } else {
                eventOnStopSprint.Invoke ();
            }
        }

        if (useSecondarySprintValues) {
            if (running) {
                setPlayerControllerMovementValues (sprintVelocity, sprintVelocity, sprintJumpPower, sprintAirSpeed, sprintAirControl);
            } else {
                resetPlayerControllerMovementValues ();
            }
        }

        if (sprintEnabled && sprintCanBeUsed) {

            if (lockedCameraActive) {
                setHeadbodStatesPaused = false;
            } else {
                if (firstPersonActive) {

                    if (updateHeadbobState) {
                        if (setHeadbodStatesPaused) {
                            setHeadbodStatesPaused = false;

                            resetHeadBodState ();

                            playerCameraManager.setShakeCameraState (false, "");
                        }
                    }
                } else {
                    if (changeCameraFovOnSprint) {
                        changeSprintFovThirdPerson (running);
                    }

                    if (updateHeadbobState) {
                        if (shakeCameraOnSprintThirdPerson) {
                            setHeadbodStatesPaused = running;

                            playerCameraManager.setShakeCameraState (running, sprintThirdPersonCameraShakeName);

                            resetHeadBodState ();
                        }
                    }
                }
            }
        }

        stopCrouchSliding ();

        if (updateFootStepStateActive) {
            setCurrentFootStepsState ();
        }
    }

    public bool isPlayerRunning ()
    {
        return running;
    }

    public void setSprintEnabledState (bool state)
    {
        sprintEnabled = state;
    }

    public void setSprintCanBeUsedState (bool state)
    {
        sprintCanBeUsed = state;
    }

    public void stopSprint ()
    {
        stopRun ();
    }

    public bool isSprintEnabled ()
    {
        return sprintEnabled && sprintCanBeUsed;
    }

    public void setOriginalSprintEnabledValue ()
    {
        if (!originalSprintEnabledValue) {
            stopSprint ();
        }

        setSprintEnabledState (originalSprintEnabledValue);
    }

    public void setWalkBydefaultFromEditor ()
    {
        setWalkSpeedValue (half);

        setSprintEnabledState (false);
    }

    public void setRunAndWalkToggleBydefaultFromEditor ()
    {
        increaseWalkSpeedValue = half;

        setSprintEnabledState (false);
    }




    public void enableOrDisableSphereMode (bool state)
    {
        if (canUseSphereMode) {
            sphereModeActive = state;
        }
    }

    public bool isSphereModeActive ()
    {
        return sphereModeActive;
    }

    public void setLastTimeMoved ()
    {
        lastTimeMoved = Time.time;
    }

    public void setLastTimeFalling ()
    {
        lastTimeFalling = Time.time;
    }

    public float getLastTimeGround ()
    {
        return lastTimeGround;
    }

    public float getLastTimeMoveInputPressed ()
    {
        return lastTimeMoveInputPressed;
    }

    public float getLastTimeFalling ()
    {
        return lastTimeFalling;
    }

    public float getLastJumpTime ()
    {
        return lastJumpTime;
    }

    public float getLastDoubleJumpTime ()
    {
        return lastDoubleJumpTime;
    }

    public void setLastTimeFiring ()
    {
        lastTimeFiring = Time.time;
    }

    public float getLastTimeFiring ()
    {
        return lastTimeFiring;
    }

    public void setAimingPowersState (bool state)
    {
        aimingPowers = state;
    }

    public void setUsingPowersState (bool state)
    {
        usingPowers = state;
    }

    public bool checkWeaponsState ()
    {
        if (playerCameraManager.isFirstPersonActive ()) {
            return true;
        }

        if (aimingPowers) {
            return false;
        }

        if (weaponsManagerLocated) {
            if (weaponsManager.isAimingWeapons ()) {
                return false;
            }

            if (!canCrouchWhenUsingWeaponsOnThirdPerson && playerUsingWeapons) {
                return false;
            }
        }

        if (crouching) {
            return true;
        }

        return true;
    }

    public void setUsingFreeFireModeState (bool state)
    {
        usingFreeFireMode = state;
    }

    public bool isUsingFreeFireMode ()
    {
        return usingFreeFireMode;
    }

    //set if the animator is enabled or not according to if the usingAnimatorInFirstMode is true or false
    public void checkAnimatorIsEnabled (bool state)
    {
        //if the animator in first person is disabled, then

        //the first person mode is enabled, so disable the animator
        if (state) {
            setAnimatorState (false);
        } else {
            //the third person mode is enabled, so enable the animator

            setAnimatorState (true);
        }

        //if the animator is enabled, 
        if (animator.enabled) {
            //check the state of the animator to set the values in the mecanim
            usingAnimator = true;
        } else {
            //disable the functions that set the values in the mecanim, and apply force to the player rigidbody directly, instead of using the animation motion
            usingAnimator = false;
        }

        if (updateFootStepStateActive) {
            //change the type of footsteps, with the triggers in the feet of the player or the raycast checking the surface under the player
            stepManager.changeFootStespType (usingAnimator);
        }
    }

    public void setUseFirstPersonPhysicsInThirdPersonActiveState (bool state)
    {
        useFirstPersonPhysicsInThirdPersonActive = state;
    }

    public void setFootStepManagerState (bool state)
    {
        if (updateFootStepStateActive) {
            if (state) {
                stepManager.enableOrDisableFootStepsComponents (false);
            } else {
                stepManager.enableOrDisableFootStepsWithDelay (true, 0);
            }
        }
    }

    public void setCurrentFootStepsState ()
    {
        if (updateFootStepStateActive) {
            if (!stepManager.isDelayToNewStateActive ()) {
                if (running) {
                    if (crouching) {
                        if (currentFootStepState != footStepState.Run_Crouching) {
                            stepManager.setFootStepState ("Run Crouching");

                            currentFootStepState = footStepState.Run_Crouching;
                        }
                    } else {
                        if (currentFootStepState != footStepState.Running) {
                            stepManager.setFootStepState ("Running");

                            currentFootStepState = footStepState.Running;
                        }
                    }
                } else {
                    if (crouching) {
                        if (currentFootStepState != footStepState.Crouching) {
                            stepManager.setFootStepState ("Crouching");

                            currentFootStepState = footStepState.Crouching;
                        }
                    } else {
                        if (currentFootStepState != footStepState.Walking) {
                            stepManager.setFootStepState ("Walking");

                            currentFootStepState = footStepState.Walking;
                        }
                    }
                }
            }
        }
    }

    public void setCurrentHeadBobState ()
    {
        if (isMoving && !obstacleToAvoidMovementFound) {
            if (running) {
                if (crouching) {
                    if (currentHeadbobState != headbobState.Run_Crouching) {
                        headBobManager.setState ("Run Crouching");

                        currentHeadbobState = headbobState.Run_Crouching;
                    }
                } else {
                    if (currentHeadbobState != headbobState.Running) {
                        headBobManager.setState ("Running");

                        currentHeadbobState = headbobState.Running;
                    }
                }
            } else {
                if (crouching) {
                    if (currentHeadbobState != headbobState.Crouching) {
                        headBobManager.setState ("Crouching");

                        currentHeadbobState = headbobState.Crouching;
                    }
                } else {
                    if (currentHeadbobState != headbobState.Walking) {
                        headBobManager.setState ("Walking");

                        currentHeadbobState = headbobState.Walking;
                    }
                }
            }

            if (headBobManager.useDynamicIdle) {
                setLastTimeMoved ();
            }
        } else {
            if (headBobManager.useDynamicIdle && canMove && !usingDevice && firstPersonActive && !headBobManager.isExternalShakingActive ()) {
                float timeToActiveDynamicIdle = headBobManager.timeToActiveDynamicIdle;

                if (Time.time > lastTimeMoved + timeToActiveDynamicIdle &&
                    Time.time > playerCameraManager.getLastTimeMoved () + timeToActiveDynamicIdle &&
                    Time.time > lastTimeFiring + timeToActiveDynamicIdle &&
                    !headBobPaused) {

                    if (currentHeadbobState != headbobState.Dynamic_Idle) {
                        headBobManager.setState ("Dynamic Idle");

                        currentHeadbobState = headbobState.Dynamic_Idle;
                    }
                } else {
                    if (currentHeadbobState != headbobState.Static_Idle) {
                        headBobManager.setState ("Static Idle");

                        currentHeadbobState = headbobState.Static_Idle;
                    }
                }
            } else {
                if (currentHeadbobState != headbobState.Static_Idle) {
                    headBobManager.setState ("Static Idle");

                    currentHeadbobState = headbobState.Static_Idle;
                }
            }
        }
    }

    public void resetHeadBodState ()
    {
        currentHeadbobState = headbobState.None;
    }

    public bool isLastJumpActivatedExternally ()
    {
        return lastJumpActivatedExternally;
    }

    //if the vehicle driven by the player ejects him, add force to his rigidbody
    public void ejectPlayerFromVehicle (float force)
    {
        extraForceOnJumpActive = true;

        extraForceOnJumpAmount = force;

        jumpInput = true;

        lastJumpActivatedExternally = true;

        //mainRigidbody.AddForce (force * currentNormal, ForceMode.Impulse);
    }

    bool extraForceOnJumpActive;
    float extraForceOnJumpAmount;

    //use a jump platform
    public void useJumpPlatform (Vector3 direction, ForceMode forceMode)
    {
        jumpInput = true;

        lastJumpActivatedExternally = true;

        if (forceMode == ForceMode.VelocityChange) {
            mainRigidbody.linearVelocity = vector3Zero;

            currentVelocity = vector3Zero;
        }

        mainRigidbody.AddForce (direction, forceMode);
    }

    public void useJumpPlatformWithKeyButton (bool state, float newJumpPower)
    {
        if (state) {
            setJumpPower (newJumpPower);
        } else {
            setOriginalJumpPower ();
        }
    }

    public void setJumpPower (float newValue)
    {
        jumpPower = newValue;

        halfJumpPower = jumpPower * half;
    }

    public void setOriginalJumpPower ()
    {
        setJumpPower (originalJumpPower);
    }

    public Vector3 getCurrentNormal ()
    {
        return gravityManager.getCurrentNormal ();
    }

    public void externalForce (Vector3 direction)
    {
        externalForceActive = true;
        externalForceValue = direction;
    }

    public void setExternalForceOnAir (Vector3 direction, float externalForceOnAirControl)
    {
        externalForceOnAirActive = true;
        externalForceOnAirValue = direction;
        externalForceOnAirControlValue = externalForceOnAirControl;
    }

    public void disableExternalForceOnAirActive ()
    {
        externalForceOnAirActive = false;
    }

    public void changeHeadScale (bool state)
    {
        if (drivingRemotely) {
            return;
        }

        if (fullBodyAwarenessActive) {
            return;
        }

        if (playerCameraManager.isFullBodyAwarenessEnabled ()) {
            return;
        }

        headScaleChanged = state;

        if (head != null) {
            if (state) {
                head.localScale = vector3Zero;
            } else {
                head.localScale = Vector3.one;
            }
        }
    }

    public bool isHeadScaleChanged ()
    {
        return headScaleChanged;
    }

    //if the player is driving, set to 0 his movement values and disable the player controller component
    public void setDrivingState (bool state, GameObject vehicle, string vehicleName, Transform vehicleCameraControllerTransform)
    {
        driving = state;

        lastTimeDriving = Time.time;

        if (driving) {
            currentVehicle = vehicle;
            currentVehicleName = vehicleName;

            currentVehicleCameraControllerTransform = vehicleCameraControllerTransform;
        } else {
            currentVehicle = null;
            currentVehicleName = "";

            currentVehicleCameraControllerTransform = vehicleCameraControllerTransform;
        }

        if (usingAnimator) {
            if (customCharacterControllerActive) {

            } else {
                if (lastForwardAnimatorIDValue != 0) {
                    lastForwardAnimatorIDValue = 0;

                    animator.SetFloat (forwardAnimatorID, 0);
                }

                if (lastTurnAnimatorIDValue != 0) {
                    lastTurnAnimatorIDValue = 0;

                    animator.SetFloat (turnAnimatorID, 0);
                }

                currentHorizontalValue = 0;
                currentVerticalValue = 0;
            }
        }

        playerCameraManager.setDrivingState (driving);
    }

    public float getLastTimeDriving ()
    {
        return lastTimeDriving;
    }

    public void setTemporalVehicle (GameObject newVehicle)
    {
        currentTemporaVehicle = newVehicle;
    }

    public GameObject getTemporalVehicle ()
    {
        return currentTemporaVehicle;
    }

    public bool isPlayerRotatingToSurface ()
    {
        return characterRotatingToSurface;
    }

    public void setCharacterRotatingToSurfaceState (bool state)
    {
        characterRotatingToSurface = state;
    }

    public bool isPlayerDriving ()
    {
        return driving;
    }

    public bool isDrivingRemotely ()
    {
        return drivingRemotely;
    }

    public void setDrivingRemotelyState (bool state)
    {
        drivingRemotely = state;
    }

    public bool isOverridingElement ()
    {
        return overridingElement;
    }

    public void setOverridingElementState (bool state)
    {
        overridingElement = state;

        playerCameraManager.setHeadColliderStateOnFBA (!overridingElement);
    }

    public void setUsingDeviceState (bool state)
    {
        usingDevice = state;

        playerCameraManager.setHeadColliderStateOnFBA (!usingDevice);

        playerCameraManager.checkResetLeanState (false);

        setLastTimeMoved ();
    }

    public void setUsingGeneralDriveRiderSystem (bool state)
    {
        usingGeneralDriveRiderSystem = state;
    }

    public bool isUsingGeneralDriveRiderSystem ()
    {
        return usingGeneralDriveRiderSystem;
    }

    public bool isUsingDevice ()
    {
        return usingDevice;
    }

    //Menu States
    public void setUsingSubMenuState (bool state)
    {
        usingSubMenu = state;
    }

    public bool isUsingSubMenu ()
    {
        return usingSubMenu;
    }

    public void setPlayerMenuActiveState (bool state)
    {
        playerMenuActive = state;

        healthManager.setPlayerMenuActiveState (playerMenuActive);
    }

    public bool isPlayerMenuActive ()
    {
        return playerMenuActive;
    }

    public void setGamePausedState (bool state)
    {
        gamePaused = state;

        setLastTimeMoved ();
    }

    public bool isGamePaused ()
    {
        return gamePaused;
    }

    public void setHeadBobPausedState (bool state)
    {
        headBobPaused = state;
    }

    public GameObject getCurrentVehicle ()
    {
        return currentVehicle;
    }

    public Transform getCurrentVehicleCameraControllerTransform ()
    {
        return currentVehicleCameraControllerTransform;
    }

    public string getCurrentVehicleName ()
    {
        return currentVehicleName;
    }

    //if it is neccessary, stop any movement from the keyboard or the touch controls in the player controller
    public void changeScriptState (bool state)
    {
        if (usingAnimator) {
            if (customCharacterControllerActive) {

            } else {
                if (lastForwardAnimatorIDValue != 0) {
                    lastForwardAnimatorIDValue = 0;

                    animator.SetFloat (forwardAnimatorID, 0);
                }

                if (lastTurnAnimatorIDValue != 0) {
                    lastTurnAnimatorIDValue = 0;

                    animator.SetFloat (turnAnimatorID, 0);
                }
            }
        }

        isMoving = false;

        setCanMoveState (state);

        resetOtherInputFields ();

        resetPlayerControllerInput ();
    }

    public void removeJumpInputState ()
    {
        jumpInput = false;

        lastJumpActivatedExternally = false;
    }

    public void resetOtherInputFields ()
    {
        jump = false;

        jumpInput = false;

        doubleJump = false;

        slowingFall = false;

        slowingFallInput = false;

        setHeadbodStatesPaused = false;

        gravityMultiplier = currentGravityMultiplier;

        slowFallExternallyActive = false;
    }

    public void setCanMoveState (bool state)
    {
        canMove = state;

        if (showDebugPrint) {
            print ("setting can move state to " + state);
        }

        if (customCharacterControllerActive) {
            currentCustomCharacterControllerBase.updateCanMoveState (canMove);
        }
    }

    public void setCanMoveAIState (bool state)
    {
        canMoveAI = state;

        if (showDebugPrint) {
            print ("setting can move AI state to " + state);
        }
    }

    public void setPlayerVelocityToZero ()
    {
        mainRigidbody.linearVelocity = vector3Zero;

        currentVelocity = vector3Zero;

        velocityChange = vector3Zero;
    }

    public void setCurrentVelocityValue (Vector3 newValue)
    {
        currentVelocity = newValue;
    }

    public void setVelocityChangeValue (Vector3 newValue)
    {
        velocityChange = newValue;
    }

    public void smoothChangeScriptState (bool state)
    {
        setCanMoveState (state);

        if (!state) {
            smoothResetPlayerControllerInput ();
        } else {
            if (resetInputCoroutine != null) {
                StopCoroutine (resetInputCoroutine);
            }
        }
    }

    public bool canPlayerMove ()
    {
        return canMove;
    }

    public void setMoveInputPausedState (bool state)
    {
        moveIputPaused = state;
    }

    bool moveInputPausedWithDurationActive;
    float moveInputPausedWithDurationAmount;
    float lastTimeMoveInputPausedWithDurationActive;

    public void setMoveInputPausedWithDuration (bool state, float newDuration)
    {
        moveInputPausedWithDurationActive = state;

        moveInputPausedWithDurationAmount = newDuration;

        lastTimeMoveInputPausedWithDurationActive = Time.time;
    }

    public bool canPlayerRagdollMove ()
    {
        return canRagdollMove;
    }

    public void setCanRagdollMoveState (bool state)
    {
        canRagdollMove = state;
    }

    bool ragdollCurrentlyActive;

    public void setRagdollCurrentlyActiveState (bool state)
    {
        ragdollCurrentlyActive = state;

        if (customCharacterControllerActive) {
            currentCustomCharacterControllerBase.setRagdollCurrentlyActiveState (ragdollCurrentlyActive);
        }

        playerCameraManager.setHeadColliderStateOnFBA (!state);
    }

    public bool isRagdollCurrentlyActive ()
    {
        return ragdollCurrentlyActive;
    }

    public void setPlayerActionsInputEnabledState (bool state)
    {
        playerActionsInputEnabled = state;
    }

    public void enablePlayerActionsInput ()
    {
        setPlayerActionsInputEnabledState (true);
    }

    public void disablePlayerActionsInput ()
    {
        setPlayerActionsInputEnabledState (false);
    }

    public void smoothResetPlayerControllerInput ()
    {
        if (resetInputCoroutine != null) {
            StopCoroutine (resetInputCoroutine);
        }

        resetInputCoroutine = StartCoroutine (smoothResetPlayerControllerInputCoroutine ());
    }

    IEnumerator smoothResetPlayerControllerInputCoroutine ()
    {
        float currentTime = 0;

        while (verticalInput != 0 || horizontalInput != 0) {
            currentTime = getCurrentDeltaTime ();

            verticalInput = Mathf.MoveTowards (verticalInput, 0, currentTime * 2);

            horizontalInput = Mathf.MoveTowards (horizontalInput, 0, currentTime * 2);

            yield return null;
        }
    }

    public void enableOrDisablePlayerControllerScript (bool state)
    {
        enabled = state;

        if (!enabled) {
            if (customCharacterControllerActive) {

            } else {
                if (lastForwardAnimatorIDValue != 0) {
                    lastForwardAnimatorIDValue = 0;

                    animator.SetFloat (forwardAnimatorID, 0);
                }

                if (lastTurnAnimatorIDValue != 0) {
                    lastTurnAnimatorIDValue = 0;

                    animator.SetFloat (turnAnimatorID, 0);
                }

                if (lastJumpAnimatorIDValue != 0) {
                    lastJumpAnimatorIDValue = 0;

                    animator.SetFloat (jumpAnimatorID, 0);
                }

                if (lastJumpLegAnimatorIDValue != 0) {
                    lastJumpLegAnimatorIDValue = 0;

                    animator.SetFloat (jumpLegAnimatorID, 0);
                }

                if (lastHorizontalAnimatorIDValue != 0) {
                    lastHorizontalAnimatorIDValue = 0;

                    animator.SetFloat (horizontalAnimatorID, 0);
                }

                if (lastVerticalAnimatorIDValue != 0) {
                    lastVerticalAnimatorIDValue = 0;

                    animator.SetFloat (verticalAnimatorID, 0);
                }

                animator.SetBool (movingAnimatorID, false);
            }

            previousValueAimingModeActiveAnimatorState = false;
        }
    }

    public void resetAnimatorState ()
    {
        if (isCrouching ()) {
            crouch ();

            if (customCharacterControllerActive) {

            } else {
                animator.SetBool (crouchAnimatorID, false);
            }
        }

        if (customCharacterControllerActive) {

        } else {
            if (lastJumpAnimatorIDValue != 0) {
                lastJumpAnimatorIDValue = 0;

                animator.SetFloat (jumpAnimatorID, 0);
            }

            if (lastJumpLegAnimatorIDValue != 0) {
                lastJumpLegAnimatorIDValue = 0;

                animator.SetFloat (jumpLegAnimatorID, 0);
            }

            if (lastTurnAnimatorIDValue != 0) {
                lastTurnAnimatorIDValue = 0;

                animator.SetFloat (turnAnimatorID, 0);
            }

            if (lastForwardAnimatorIDValue != 0) {
                lastForwardAnimatorIDValue = 0;

                animator.SetFloat (forwardAnimatorID, 0);
            }

            if (!pauseResetAnimatorStateFOrGroundAnimator) {
                animator.SetBool (onGroundAnimatorID, true);

                setTimeOnAirAnimatorIDValue ();
            }
        }

        pauseResetAnimatorStateFOrGroundAnimator = false;
    }

    public void setPauseResetAnimatorStateFOrGroundAnimatorState (bool state)
    {
        pauseResetAnimatorStateFOrGroundAnimator = state;
    }

    public void setOnGroundAnimatorIDValue (bool state)
    {
        if (!customCharacterControllerActive) {
            setOnGroundAnimatorIDValueWithoutCheck (state);
        }
    }

    public void setOnGroundAnimatorIDValueWithoutCheck (bool state)
    {
        animator.SetBool (onGroundAnimatorID, state);

        setTimeOnAirAnimatorIDValue ();
    }

    public void setJumpAnimatorIDValue (float newValue)
    {
        if (lastJumpAnimatorIDValue != newValue) {
            lastJumpAnimatorIDValue = newValue;

            animator.SetFloat (jumpAnimatorID, newValue);
        }
    }

    public void setRightArmdIDAnimatorIDValue (int newValue)
    {
        animator.SetInteger (rightArmdIDAnimatorID, newValue);
    }

    public void setLeftArmIDAnimatorIDValue (int newValue)
    {
        animator.SetInteger (leftArmIDAnimatorID, newValue);
    }

    public void setTimeOnAirAnimatorIDValue ()
    {
        float newValue = Time.time - lastTimeOnAirAnimator;

        if (newValue < 0) {
            newValue = 0;
        }

        animator.SetFloat (timeOnAirAnimatorID, newValue);
    }

    public void equipJetpack (bool state)
    {
        jetPackEquiped = state;
    }

    public void setUsingJetpackState (bool state)
    {
        usingJetpack = state;
    }

    public bool isJetpackEquiped ()
    {
        return jetPackEquiped;
    }

    public bool isUsingJetpack ()
    {
        return usingJetpack;
    }

    public void enableOrDisableFlyingMode (bool state)
    {
        flyModeActive = state;

        if (!swimModeActive) {
            setEnableExternalForceOnFlyModeState (false);
        }
    }

    public bool isFlyingActive ()
    {
        return flyModeActive;
    }

    public void enableOrDisableFlyModeTurbo (bool state)
    {

    }

    public void setFreeClimbActiveState (bool state)
    {
        freeClimbActive = state;

        playerCameraManager.setHeadColliderStateOnFBA (!freeClimbActive);
    }

    public bool isFreeClimbActive ()
    {
        return freeClimbActive;
    }

    public void setGrabbingToSurfaceActiveState (bool state)
    {
        grabbingToSurfaceActive = state;
    }

    public bool isGrabbingToSurfaceActive ()
    {
        return grabbingToSurfaceActive;
    }

    public void setAdjustingToSurfaceToGrabInProcessState (bool state)
    {
        adjustingToSurfaceToGrabInProcess = state;
    }

    public bool isAjustingToSurfaceToGrabInProcess ()
    {
        return adjustingToSurfaceToGrabInProcess;
    }

    public void setCarryingPhysicalObjectState (bool state)
    {
        carryingPhysicalObject = state;
    }

    public bool isCarryingPhysicalObject ()
    {
        return carryingPhysicalObject;
    }

    public void enableOrDisableSwimMode (bool state)
    {
        swimModeActive = state;

        if (!swimModeActive) {
            setEnableExternalForceOnSwimModeState (false);
        }
    }

    public bool isSwimModeActive ()
    {
        return swimModeActive;
    }

    public void setEnableExternalForceOnSwimModeState (bool state)
    {
        enableExternalForceOnSwimMode = state;
    }

    public void setEnableExternalForceOnFlyModeState (bool state)
    {
        enableExternalForceOnFlyMode = state;
    }

    public void setTurnAndForwardAnimatorValuesPausedState (bool state)
    {
        turnAndForwardAnimatorValuesPaused = state;
    }

    public void setPauseCameraShakeFromGravityActiveState (bool state)
    {
        pauseCameraShakeFromGravityActive = state;
    }

    public bool isPauseCameraShakeFromGravityActive ()
    {
        return pauseCameraShakeFromGravityActive;
    }

    public externalControllerBehaviorManager mainExternalControllerBehaviorManager;

    string currentExternalControllerBehaviorName = "";

    public void setExternalControllerBehavior (externalControllerBehavior newExternalControllerBehavior)
    {
        currentExternalControllerBehavior = newExternalControllerBehavior;

        externalControllBehaviorActive = currentExternalControllerBehavior != null;

        if (newExternalControllerBehavior != null) {
            currentExternalControllerBehaviorName = newExternalControllerBehavior.behaviorName;
        }

        if (externalControllBehaviorActive) {
            if (freeFloatingModeOn) {
                GKC_Utils.enableOrDisableFreeFloatingModeOnState (gameObject, false);
            }
        }

        if (mainExternalControllerBehaviorManager != null) {
            if (externalControllBehaviorActive) {
                mainExternalControllerBehaviorManager.setExternalControllerActive (currentExternalControllerBehaviorName);
            } else {
                mainExternalControllerBehaviorManager.setExternalControllerDeactive (currentExternalControllerBehaviorName);
            }
        }

        if (newExternalControllerBehavior == null) {
            currentExternalControllerBehaviorName = "";
        }

        resetLastMoveInputOnJumpValue ();

        if (externalControllBehaviorActive) {
            releaseJumpButton ();
        }
    }

    public externalControllerBehavior getCurrentExternalControllerBehavior ()
    {
        return currentExternalControllerBehavior;
    }

    public bool isExternalControllBehaviorActive ()
    {
        return externalControllBehaviorActive;
    }

    public string getCurrentExternalControllerBehaviorName ()
    {
        return currentExternalControllerBehaviorName;
    }

    public void setUseExternalControllerBehaviorPausedState (bool state)
    {
        useExternalControllerBehaviorPaused = state;
    }

    public bool isUseExternalControllerBehaviorPaused ()
    {
        return useExternalControllerBehaviorPaused;
    }

    public externalControllerBehavior getExternalControllerBehaviorWallRunning ()
    {
        return wallRunningExternalControllerBehavior;
    }

    public void checkChangeCameraViewStateOnExternalControllerBehavior ()
    {
        if (externalControllBehaviorActive) {
            currentExternalControllerBehavior.checkChangeCameraViewStateOnExternalControllerBehavior ();
        }
    }

    public void setAvoidListOfExternalControllerStatesActiveState (bool state, List<string> newList)
    {
        avoidListOfExternalControllerStatesActive = state;

        externalControllerToAvoidList = newList;
    }

    public bool isAvoidListOfExternalControllerStatesActive ()
    {
        return avoidListOfExternalControllerStatesActive;
    }

    public List<string> getExternalControllerToAvoidList ()
    {
        return externalControllerToAvoidList;
    }

    public bool checkIfInsideExternalControllerToAvoidList (string nameToCheck)
    {
        return externalControllerToAvoidList.Contains (nameToCheck);
    }

    bool avoidListOfExternalControllerStatesActive;

    List<string> externalControllerToAvoidList = new List<string> ();


    public void setCustomCharacterControllerActiveState (bool state, customCharacterControllerBase customCharacterControllerBaseInfo)
    {
        customCharacterControllerActive = state;

        currentCustomCharacterControllerBase = customCharacterControllerBaseInfo;

        if (currentCustomCharacterControllerBase != null) {
            currentCustomCharacterControllerBase.updateCanMoveState (canMove);

            currentCustomCharacterControllerBase.setRagdollCurrentlyActiveState (ragdollCurrentlyActive);
        }
    }

    public bool isCustomCharacterControllerActive ()
    {
        return customCharacterControllerActive;
    }

    public void setExternalControlBehaviorForAirTypeActiveState (bool state)
    {
        externalControlBehaviorForAirTypeActive = state;
    }

    public void enableOrDisableAiminig (bool state)
    {
        if (playerCameraManager.isFirstPersonActive ()) {
            enableOrDisableAimingInFirstPerson (state);

            enableOrDisableAimingInThirdPerson (false);
        } else {
            enableOrDisableAimingInThirdPerson (state);

            enableOrDisableAimingInFirstPerson (false);
        }

        if (!state) {
            lastTimePlayerUserInput = 0;
        }
    }

    public void enableOrDisableAimingInFirstPerson (bool state)
    {
        aimingInFirstPerson = state;

        playerIsAiming = isPlayerAiming ();
    }

    public void enableOrDisableAimingInThirdPerson (bool state)
    {
        aimingInThirdPerson = state;

        playerIsAiming = isPlayerAiming ();

        checkOtherStatesOnAirID (state);
    }

    public bool isPlayerAiming ()
    {
        return aimingInFirstPerson || aimingInThirdPerson;
    }

    public bool isPlayerAimingInThirdPerson ()
    {
        return aimingInThirdPerson;
    }

    public bool isPlayerAimingInFirstPerson ()
    {
        return aimingInFirstPerson;
    }

    public void checkOtherStatesOnAirID (bool state)
    {
        if (state) {
            if (!regularAirboneIDActivePreviously) {
                if (currentAirID == regularAirboneID) {
                    setCurrentAirIDValue (regularAirID);

                    regularAirboneIDActivePreviously = true;
                }
            }
        } else {
            if (regularAirboneIDActivePreviously) {
                if (!isPlayerOnFFOrZeroGravityModeOn ()) {
                    setCurrentAirIDValue (regularAirboneID);
                }

                regularAirboneIDActivePreviously = false;
            }
        }
    }

    public bool isLookingInCameraDirection ()
    {
        return ((isPlayerAiming () && hasToLookInCameraDirectionOnFreeFire ()) || lookInCameraDirectionActive || firstPersonActive);
    }

    public playerCamera getPlayerCameraManager ()
    {
        return playerCameraManager as playerCamera;
    }

    public GameObject getPlayerCameraGameObject ()
    {
        return playerCameraGameObject;
    }

    public bool isPlayerOnFirstPerson ()
    {
        return firstPersonActive;
    }

    public void setFirstPersonViewActiveState (bool state)
    {
        firstPersonActive = state;

        resetHeadBodState ();

        if (firstPersonActive) {
            if (setHeadbodStatesPaused) {
                setHeadbodStatesPaused = false;

                playerCameraManager.setShakeCameraState (false, "");
            }
        } else {
            if (currentSurfaceBelowPlayer != null) {
                previousValueOnGroundAnimatorState = false;
            }

            previousValueCrouching = !crouching;
        }

        IKSystemManager.setIsThirdPersonViewState (!firstPersonActive);
    }

    bool strafeModeActivatedExternally;

    public void setFullBodyAwarenessActiveStateAtStart (bool state)
    {
        fullBodyAwarenessActiveAtStart = state;
    }

    public void setFullBodyAwarenessActiveStateExternally (bool state)
    {
        fullBodyAwarenessActive = state;
    }

    public void setFullBodyAwarenessActiveState (bool state)
    {
        fullBodyAwarenessActive = state;

        if (fullBodyAwarenessActive) {
            activateStrafeMode ();

            if (!useRootMotionOnFullBodyAwareness) {
                setApplyRootMotionState (false);

                setUseRootMotionActiveState (false);

                setNoRootMotionValues (FBAWalkMovementSpeed, FBARunMovementSpeed, FBASprintMovementSpeed,
                    FBACrouchMovementSpeed, FBACrouchRunMovementSpeed, FBAWalkStrafeMovementSpeed, FBARunStrafeMovementSpeed);
            }

            if (useFBACustomMoveSpeedMultiplier) {
                useCustomMoveSpeedMultiplier = true;

                customMoveSpeedMultiplier = FBACustomMoveSpeedMultiplier;
            }
        } else {
            if (!strafeModeActivatedExternally) {
                deactivateStrafeMode ();
            }

            if (!useRootMotionOnFullBodyAwareness) {
                setApplyRootMotionState (originalSetUseRootMotionActiveState);

                setUseRootMotionActiveState (originalSetUseRootMotionActiveState);

                setOriginalNoRootMotionValues ();
            }

            if (useFBACustomMoveSpeedMultiplier) {
                useCustomMoveSpeedMultiplier = false;
            }
        }

        playerCameraManager.setFBAPivotCameraTransformActiveState (state);

        playerCameraManager.startOrStopUpdatePivotPositionOnFBA (state);

        playerCameraManager.checkActivateOrDeactivateHeadColliderOnFBA (state);

        IKSystemManager.setFullBodyAwarenessActiveState (state);

        animator.SetBool (animatorFBAActiveID, state);
    }

    public bool isFullBodyAwarenessActive ()
    {
        return fullBodyAwarenessActive;
    }

    public void setPivotCameraTransformParentCurrentTransformToFollow ()
    {
        playerCameraManager.setPivotCameraTransformParentCurrentTransformToFollow ();
    }

    public void setPivotCameraTransformOriginalParent ()
    {
        playerCameraManager.setPivotCameraTransformOriginalParent ();
    }

    public void resetPivotCameraTransformLocalRotation ()
    {
        playerCameraManager.resetPivotCameraTransformLocalRotation ();
    }

    public void setNewPlayerCharacterPositionAndRotation (Vector3 targetPosition, Vector3 targetRotation)
    {
        playerTransform.position = targetPosition;

        playerTransform.eulerAngles = targetRotation;

        playerCameraTransform.position = targetPosition;

        playerCameraTransform.eulerAngles = targetRotation;
    }

    public void stopShakeCamera ()
    {
        playerCameraManager.stopShakeCamera ();
    }

    public float getForwardAmount ()
    {
        return forwardAmount;
    }

    public void setForwardAmountExternally (float newValue)
    {
        animator.SetFloat (forwardAnimatorID, newValue);
    }

    public bool isPlayerMoving (float movingTolerance)
    {
        if (Mathf.Abs (horizontalInput) > movingTolerance || Mathf.Abs (verticalInput) > movingTolerance) {
            return true;
        }

        return false;
    }

    public bool isPlayerMovingHorizontal (float movingTolerance)
    {
        if (Mathf.Abs (horizontalInput) > movingTolerance) {
            return true;
        }

        return false;
    }

    public bool isPlayerMovingVertical (float movingTolerance)
    {
        if (Mathf.Abs (verticalInput) > movingTolerance) {
            return true;
        }

        return false;
    }

    public bool isPlayerUsingInput ()
    {
        if (rawAxisValues.x != 0 || rawAxisValues.y != 0) {
            return true;
        }

        return false;
    }

    public bool isPlayerUsingVerticalInput ()
    {
        if (rawAxisValues.y != 0) {
            return true;
        }

        return false;
    }

    public bool isPlayerUsingHorizontalInput ()
    {
        if (rawAxisValues.x != 0) {
            return true;
        }

        return false;
    }

    public Vector2 getAxisValues ()
    {
        return axisValues;
    }

    public Vector2 getRawAxisValues ()
    {
        return rawAxisValues;
    }

    public void resetPlayerControllerInput ()
    {
        horizontalInput = 0;
        verticalInput = 0;
    }

    public void resetLastMoveInputOnJumpValue ()
    {
        lastMoveInputOnJump = vector3Zero;

        ignoreLastMoveInputOnJump = false;
    }

    public void overrideMainCameraTransformDirection (Transform newCameraDirection, bool overrideState)
    {
        overrideMainCameraTransform = newCameraDirection;
        overrideMainCameraTransformActive = overrideState;
    }

    public void setUseForwardDirectionForCameraDirectionState (bool state)
    {
        useForwardDirectionForCameraDirection = state;
    }

    public void setUseRightDirectionForCameraDirectionState (bool state)
    {
        useRightDirectionForCameraDirection = state;
    }

    public void setOverrideTurnAmount (float newTurnAmount, bool newOverrideTurnAmountActiveState)
    {
        overrideTurnAmount = newTurnAmount;
        overrideTurnAmountActive = newOverrideTurnAmountActiveState;
    }

    public void setLockedCameraState (bool state, bool useTankControlsValue, bool useRelativeMovementToLockedCameraValue,
                                      bool playerCanMoveOnAimInTankModeValue, bool keepCameraRelativeMovementWhenNotAimingValue)
    {
        lockedCameraActive = state;

        useTankControls = useTankControlsValue;

        playerCanMoveOnAimInTankMode = playerCanMoveOnAimInTankModeValue;

        tankModeCurrentlyEnabled = lockedCameraActive && useTankControls;

        useRelativeMovementToLockedCamera = useRelativeMovementToLockedCameraValue;

        keepCameraRelativeMovementWhenNotAiming = keepCameraRelativeMovementWhenNotAimingValue;

        if (!lockedCameraActive) {
            checkCameraDirectionFromLockedToFree = true;
        }
    }

    public bool isLockedCameraStateActive ()
    {
        return lockedCameraActive;
    }

    public void setUpdate2_5dClampedPositionPausedState (bool state)
    {
        update2_5dClampedPositionPaused = state;
    }

    public bool isTankModeActive ()
    {
        return useTankControls && lockedCameraActive;
    }

    public bool canCharacterGetOnVehicles ()
    {
        return canGetOnVehicles;
    }

    public void setCanCharacterGetOnVehiclesState (bool state)
    {
        canGetOnVehicles = state;
    }

    public void setOriginalCanCharacterGetOnVehiclesState ()
    {
        setCanCharacterGetOnVehiclesState (originalCanCharacterGetOnVehiclesState);
    }

    public bool isActionToGetOnVehicleActive ()
    {
        return actionToGetOnVehicleActive;
    }

    public void setActionToGetOnVehicleActiveState (bool state)
    {
        actionToGetOnVehicleActive = state;
    }

    public bool isActionToGetOffFromVehicleActive ()
    {
        return actionToGetOffFromVehicleActive;
    }

    public void setActionToGetOffFromVehicleActiveState (bool state)
    {
        actionToGetOffFromVehicleActive = state;
    }

    public bool canCharacterDrive ()
    {
        return canDrive;
    }

    public void setCanDriveState (bool state)
    {
        canDrive = state;
    }

    public void setOriginalCanDriveState ()
    {
        setCanDriveState (originalCanDrive);
    }

    public void setCanMoveWhileAimLockedCameraValue (bool state)
    {
        canMoveWhileAimLockedCamera = state;
    }

    public void setOriginalCanMoveWhileAimLockedCameraValue ()
    {
        setCanMoveWhileAimLockedCameraValue (originalCanMoveWhileAimLockedCamera);
    }

    public GameObject getPlayerManagersParentGameObject ()
    {
        if (playerManagersParentGameObject != null) {
            return playerManagersParentGameObject;
        } else {
            return playerTransform.parent.gameObject;
        }
    }

    public void setPlayerAndCameraParent (Transform newParent)
    {
        playerCameraManager.setPlayerAndCameraParent (newParent);

        if (newParent != null) {
            setPlayerSetAsChildOfParentState (true);
        } else {
            setPlayerSetAsChildOfParentState (false);
        }

        currentTemporalPlayerParent = newParent;
    }

    public void setPlayerAndCameraAndFBAPivotTransformParent (Transform newParent)
    {
        if (newParent == null) {
            playerCameraManager.setFBAPivotTransformParentFromPlayer (null);
        }

        setPlayerAndCameraParent (newParent);

        if (newParent != null) {
            playerCameraManager.setFBAPivotTransformParentFromPlayer (newParent);
        }
    }

    public Transform getCurrentTemporalPlayerParent ()
    {
        return currentTemporalPlayerParent;
    }

    public void setMovingOnPlatformActiveState (bool state)
    {
        movingOnPlatformActive = state;
    }

    public void setUpdatePlayerCameraPositionOnLateUpdateActiveState (bool state)
    {
        playerCameraManager.setUpdatePlayerCameraPositionOnLateUpdateActiveState (state);
    }

    public void setUpdatePlayerCameraPositionOnFixedUpdateActiveState (bool state)
    {
        playerCameraManager.setUpdatePlayerCameraPositionOnFixedUpdateActiveState (state);
    }

    public void setMovingInsideVehicleState (bool state)
    {
        movingInsideVehicle = state;
    }

    public bool isMovingInsideVehicle ()
    {
        return movingInsideVehicle;
    }

    Vector3 externalForceInsideVehicle;

    public void setMovingInsideVehicleState (Vector3 externalForceInsideVehicleValue)
    {
        externalForceInsideVehicle = externalForceInsideVehicleValue;
    }

    public bool isMovingOnPlatformActive ()
    {
        return movingOnPlatformActive;
    }

    //AI input and functions to use this controller with an AI system
    public void setReducedVelocity (float newValue)
    {
        setAnimSpeedMultiplierValue (newValue);
    }

    public void setNormalVelocity ()
    {
        setAnimSpeedMultiplierValue (originalAnimationSpeed);
    }

    void setAnimSpeedMultiplierValue (float newValue)
    {
        animSpeedMultiplier = newValue;
    }

    public float getAnimSpeedMultiplier ()
    {
        return animSpeedMultiplier;
    }

    public void setMoveSpeedMultiplierValue (float newValue)
    {
        moveSpeedMultiplier = newValue;
    }

    public void setOriginalMoveSpeedMultiplierValue ()
    {
        setMoveSpeedMultiplierValue (originalMoveSpeedMultiplier);
    }

    public void setNewAnimSpeedMultiplierDuringXTime (float newValue)
    {
        changeAnimSpeedMultiplierDuration = newValue;

        stopSetNewAnimSpeedMultiplierDuringXTimeCoroutine ();

        changeAnimSpeedMultiplierCoroutine = StartCoroutine (setNewAnimSpeedMultiplierDuringXTimeCoroutine ());
    }

    public void setRegularAnimSpeedMultiplier ()
    {
        if (overrideAnimationSpeedActive) {
            stopSetNewAnimSpeedMultiplierDuringXTimeCoroutine ();

            setNormalVelocity ();

            if (usedByAI) {
                setCanMoveAIState (true);
            }
        }
    }

    IEnumerator setNewAnimSpeedMultiplierDuringXTimeCoroutine ()
    {
        animSpeedMultiplierChangedDuringXTimeActive = true;

        overrideAnimationSpeedActive = true;

        WaitForSeconds delay = new WaitForSeconds (changeAnimSpeedMultiplierDuration);

        yield return delay;

        setNormalVelocity ();

        overrideAnimationSpeedActive = false;

        animSpeedMultiplierChangedDuringXTimeActive = false;

        if (usedByAI) {
            setCanMoveAIState (true);
        }
    }

    void stopSetNewAnimSpeedMultiplierDuringXTimeCoroutine ()
    {
        if (changeAnimSpeedMultiplierCoroutine != null) {
            StopCoroutine (changeAnimSpeedMultiplierCoroutine);
        }

        overrideAnimationSpeedActive = false;

        animSpeedMultiplierChangedDuringXTimeActive = false;
    }

    public bool isAnimSpeedMultiplierChangedDuringXTimeActive ()
    {
        return animSpeedMultiplierChangedDuringXTimeActive;
    }

    int pauseCharacterPriority = 0;

    public void setPauseCharacterPriorityValue (int newValue)
    {
        pauseCharacterPriority = newValue;
    }

    public int getPauseCharacterPriorityValue ()
    {
        return pauseCharacterPriority;
    }

    public void changeControlInputType (bool value)
    {
        usedByAI = value;

        if (usedByAI) {
            resetPlayerControllerInput ();
        } else {
            playerInput.overrideInputValues (Vector2.zero, false);
        }
    }

    public void setAIElementsEnabledState (bool state)
    {
        if (AIElements.activeSelf != state) {
            AIElements.SetActive (state);
        }
    }

    public void setUsedByAIState (bool state)
    {
        usedByAI = state;

        if (!usedByAI) {
            updateOverrideInputValues (Vector2.zero, false);
        }
    }

    public void startOverride ()
    {
        overrideCharacterControlState (true);
    }

    public void stopOverride ()
    {
        overrideCharacterControlState (false);
    }

    public void overrideCharacterControlState (bool state)
    {
        usedByAI = !state;

        characterControlOverrideActive = state;

        if (!usedByAI) {
            playerInput.overrideInputValues (Vector2.zero, false);
        }
    }

    public bool isCharacterUsedByAI ()
    {
        return usedByAI;
    }

    public bool isCharacterControlOverrideActive ()
    {
        return characterControlOverrideActive;
    }

    public float getHorizontalInput ()
    {
        return horizontalInput;
    }

    public float getVerticalInput ()
    {
        return verticalInput;
    }

    public Vector3 getMoveInputDirection ()
    {
        return currentMoveInput;
    }

    public void setLookInCameraDirectionState (bool state)
    {
        lookInCameraDirection = state;
    }

    // The Move function is designed to be called from a separate component
    // based on User input, or an AI control script
    public void Move (AINavMeshMoveInfo inputInfo)
    {
        if (inputInfo.moveInput.magnitude > 1) {
            inputInfo.moveInput.Normalize ();
        }

        navMeshMoveInput = inputInfo.moveInput;

        //		crouchInput = inputInfo.crouchInput;

        if (inputInfo.jumpInput) {
            activateJump ();
        }

        lookInCameraDirection = inputInfo.lookAtTarget;

        AIStrafeModeActive = inputInfo.strafeModeActive;

        AIEnableInputMovementOnStrafe = inputInfo.AIEnableInputMovementOnStrafe;
    }

    public void setVisibleToAIState (bool state)
    {
        visibleToAI = state;
    }

    public bool isCharacterInVisibleState ()
    {
        return visibleToAI;
    }

    public bool isCharacterVisibleToAI ()
    {
        return visibleToAI && !stealthModeActive;
    }

    public void setStealthModeActiveState (bool state)
    {
        stealthModeActive = state;
    }

    public bool isCharacterInStealthMode ()
    {
        return stealthModeActive;
    }

    public void setCharacterStateIcon (string stateName)
    {
        if (characterStateIconManager != null) {
            characterStateIconManager.setCharacterStateIcon (stateName);
        }
    }

    public void disableCharacterStateIcon ()
    {
        if (characterStateIconManager != null) {
            characterStateIconManager.disableCharacterStateIcon ();
        }
    }

    public int getPlayerID ()
    {
        return playerID;
    }

    public void setPlayerID (int newID)
    {
        playerID = newID;
    }

    public void setCharacterModelID (int newID)
    {
        characterModelID = newID;
    }

    public int getCharacterModelID ()
    {
        return characterModelID;
    }

    public bool isPlayerNonLocalAvatar ()
    {
        return playerIsNonLocalAvatar;
    }

    public void setPlayerIsNonLocalAvatarState (bool state)
    {
        playerIsNonLocalAvatar = state;
    }

    public int getPlayerNonLocalAvatarID ()
    {
        return playerNonLocalAvatarID;
    }

    public void setPlayerNonLocalAvatarID (int newID)
    {
        playerNonLocalAvatarID = newID;
    }


    public void setPlayerDeadState (bool state)
    {
        isDead = state;

        if (isDead) {
            if (isPlayerRunning ()) {
                stopRun ();
            }

            if (isCrouching ()) {
                crouch ();
            }

            lastTimeResurrect = 0;
        } else {
            lastTimeResurrect = Time.time;
        }
    }

    public bool isPlayerDead ()
    {
        return isDead;
    }

    public float getLastTimeResurrect ()
    {
        return lastTimeResurrect;
    }

    public void setFallDamageCheckPausedState (bool state)
    {
        fallDamageCheckPaused = state;
    }

    public void setFallDamageCheckOnHealthPausedState (bool state)
    {
        fallDamageCheckOnHealthPaused = state;
    }

    public bool checkIfPlayerDeadFromHealthComponent ()
    {
        return healthManager.isDead ();
    }

    public void setDamageReactionPausedState (bool state)
    {
        healthManager.setDamageReactionPausedState (state);
    }

    bool playerUsingWeapons;

    public void setPlayerUsingWeaponsState (bool state)
    {
        playerUsingWeapons = state;
    }

    public bool isPlayerUsingWeapons ()
    {
        if (weaponsManagerLocated) {
            return playerUsingWeapons;
        }

        return false;
    }

    public bool isPlayerUsingPowers ()
    {
        return usingPowers;
    }

    public bool isPlayerUsingMeleeWeapons ()
    {
        return playerUsingMeleeWeapons;
    }

    public bool isPlayerMeleeWeaponThrown ()
    {
        return playerMeleeWeaponThrown;
    }

    public void setPlayerMeleeWeaponThrownState (bool state)
    {
        playerMeleeWeaponThrown = state;
    }

    public void setPlayerUsingMeleeWeaponsState (bool state)
    {
        playerUsingMeleeWeapons = state;
    }

    public void setPlayerNavMeshEnabledState (bool state)
    {
        playerNavMeshEnabled = state;

        resetPlayerControllerInput ();

        if (!playerNavMeshEnabled) {
            playerInput.overrideInputValues (Vector2.zero, false);
        }
    }

    public bool isPlayerNavMeshEnabled ()
    {
        return playerNavMeshEnabled;
    }

    public void enableOrDisableDoubleJump (bool state)
    {
        enabledDoubleJump = state;
    }

    public void setMaxNumberJumpsInAir (float amount)
    {
        maxNumberJumpsInAir = (int)amount;
    }

    public void enableOrDisableJump (bool state)
    {
        enabledRegularJump = state;
    }

    public void setcanJumpActiveState (bool state)
    {
        canJumpActive = state;
    }

    public void setJumpInputPausedState (bool state)
    {
        jumpInputPaused = state;
    }

    public void setHoldJumpSlowDownFallEnabledState (bool state)
    {
        holdJumpSlowDownFallEnabled = state;
    }

    public void setRunInputPausedState (bool state)
    {
        runInputPaused = state;
    }

    public void setPhysicMaterialAssigmentPausedState (bool state)
    {
        physicMaterialAssigmentPaused = state;
    }

    public void setUsingAbilityActiveState (bool state)
    {
        usingAbilityActive = state;
    }

    public bool isUsingAbilityActive ()
    {
        return usingAbilityActive;
    }

    public void setCrouchInputPausedState (bool state)
    {
        crouchInputPaused = state;
    }

    public void setHighFrictionMaterial ()
    {
        if (!highFrictionMaterialActive) {
            capsule.material = highFrictionMaterial;
            highFrictionMaterialActive = true;
        }
    }

    public void setZeroFrictionMaterial ()
    {
        if (highFrictionMaterialActive) {
            capsule.material = zeroFrictionMaterial;
            highFrictionMaterialActive = false;
        }
    }

    public bool playerCanRunNow ()
    {
        return (!playerCameraManager.isFirstPersonActive () && canRunThirdPersonActive) ||
        (playerCameraManager.isFirstPersonActive () && (noAnimatorCanRun && (getVerticalInput () > 0 || noAnimatorCanRunBackwards)));
    }

    public void setNoAnimatorWalkMovementSpeed (float newValue, bool setOriginalValue)
    {
        if (setOriginalValue) {
            noAnimatorWalkMovementSpeed = originalNoAnimWalkMovementSpeed;
        } else {
            noAnimatorWalkMovementSpeed = originalNoAnimWalkMovementSpeed * newValue;
        }
    }

    public void setNoAnimatorRunMovementSpeed (float newValue, bool setOriginalValue)
    {
        if (setOriginalValue) {
            noAnimatorRunMovementSpeed = originalNoAnimRunMovementSpeed;
        } else {
            noAnimatorRunMovementSpeed = originalNoAnimRunMovementSpeed * newValue;
        }
    }

    public void setNoAnimatorCrouchMovementSpeed (float newValue, bool setOriginalValue)
    {
        if (setOriginalValue) {
            noAnimatorCrouchMovementSpeed = originalNoAnimCrouchMovementSpeed;
        } else {
            noAnimatorCrouchMovementSpeed = originalNoAnimCrouchMovementSpeed * newValue;
        }
    }

    public void setNoAnimatorStrafeMovementSpeed (float newValue, bool setOriginalValue)
    {
        if (setOriginalValue) {
            noAnimatorStrafeMovementSpeed = originalNoAnimStrafeMovementSpeed;
        } else {
            noAnimatorStrafeMovementSpeed = originalNoAnimStrafeMovementSpeed * newValue;
        }
    }

    public void setNoAnimatorWalkBackwardMovementSpeed (float newValue, bool setOriginalValue)
    {
        if (setOriginalValue) {
            noAnimatorWalkBackwardMovementSpeed = originalNoAnimWalkBackwardMovementSpeed;
        } else {
            noAnimatorWalkBackwardMovementSpeed = originalNoAnimWalkBackwardMovementSpeed * newValue;
        }
    }

    public void setNoAnimatorRunBackwardMovementSpeed (float newValue, bool setOriginalValue)
    {
        if (setOriginalValue) {
            noAnimatorRunBackwardMovementSpeed = originalNoAnimRunBackwardMovementSpeed;
        } else {
            noAnimatorRunBackwardMovementSpeed = originalNoAnimRunBackwardMovementSpeed * newValue;
        }
    }

    public void setNoAnimatorCrouchBackwardMovementSpeed (float newValue, bool setOriginalValue)
    {
        if (setOriginalValue) {
            noAnimatorCrouchBackwardMovementSpeed = originalNoAnimCrouchBackwardMovementSpeed;
        } else {
            noAnimatorCrouchBackwardMovementSpeed = originalNoAnimCrouchBackwardMovementSpeed * newValue;
        }
    }

    public void setNoAnimatorStrafeBackwardMovementSpeed (float newValue, bool setOriginalValue)
    {
        if (setOriginalValue) {
            noAnimatorStrafeBackwardMovementSpeed = originalNoAnimStrafeBackwardMovementSpeed;
        } else {
            noAnimatorStrafeBackwardMovementSpeed = originalNoAnimStrafeBackwardMovementSpeed * newValue;
        }
    }

    public void setNoAnimatorCanRunState (bool newState, bool setOriginalValue)
    {
        if (setOriginalValue) {
            noAnimatorCanRun = originalNoAnimCanRun;
        } else {
            noAnimatorCanRun = newState;
        }
    }

    public void setNoAnimatorGeneralMovementSpeed (float newValue, bool setOriginalValue)
    {
        setNoAnimatorWalkMovementSpeed (newValue, setOriginalValue);
        setNoAnimatorRunMovementSpeed (newValue, setOriginalValue);
        setNoAnimatorCrouchMovementSpeed (newValue, setOriginalValue);
        setNoAnimatorStrafeMovementSpeed (newValue, setOriginalValue);
        setNoAnimatorWalkBackwardMovementSpeed (newValue, setOriginalValue);
        setNoAnimatorRunBackwardMovementSpeed (newValue, setOriginalValue);
        setNoAnimatorCrouchBackwardMovementSpeed (newValue, setOriginalValue);
        setNoAnimatorStrafeBackwardMovementSpeed (newValue, setOriginalValue);
    }

    public void setNoAnimatorSpeedValue (float newValue)
    {
        noAnimatorSpeed = newValue;
    }

    public void setAnimatorCanRunState (bool newState, bool setOriginalValue)
    {
        if (setOriginalValue) {
            canRunThirdPersonActive = true;
        } else {
            canRunThirdPersonActive = newState;
        }
    }

    public void setAnimatorGeneralMovementSpeed (float newValue, bool setOriginalValue)
    {
        if (setOriginalValue) {
            setOriginalWalkSpeed ();
        } else {
            setWalkSpeedValue (newValue);
        }
    }

    public bool hasToLookInCameraDirection ()
    {
        //if the player has active the option to look in the camera direction, or to look on that direction when the camera is looking to a target, return true
        //also, check if the player only follows the camera direction while moving or not, and also, check if he is currently on ground state
        return (lookAlwaysInCameraDirection || (lookInCameraDirectionIfLookingAtTarget && playerCameraManager.isPlayerLookingAtTarget ()))
        && lookInCameraDirectionOnFreeFireActive
        && (playerOnGround || !ignoreStrafeStateOnAirEnabled || fullBodyAwarenessActive);
    }

    bool ignoreLookInCameraDirectionOnFreeFireActive;

    public void setIgnoreLookInCameraDirectionOnFreeFireActiveState (bool state)
    {
        ignoreLookInCameraDirectionOnFreeFireActive = state;
    }

    public bool hasToLookInCameraDirectionOnFreeFire ()
    {
        if (ignoreLookInCameraDirectionOnFreeFireActive) {
            return false;
        }

        return (!usingFreeFireMode && !checkToKeepWeaponAfterAimingWeaponFromShooting) || lookInCameraDirectionOnFreeFire || useRelativeMovementToLockedCamera;
    }

    public bool isPlayerLookingAtTarget ()
    {
        return playerCameraManager.isPlayerLookingAtTarget ();
    }

    public bool istargetToLookLocated ()
    {
        return playerCameraManager.istargetToLookLocated ();
    }

    public Transform getCurrentTargetToLook ()
    {
        return playerCameraManager.getCurrentTargetToLook ();
    }

    public Vector3 getOriginalLockedCameraPivotPosition ()
    {
        return playerCameraManager.getOriginalLockedCameraPivotPosition ();
    }

    public void setCheckToKeepWeaponAfterAimingWeaponFromShootingState (bool state)
    {
        checkToKeepWeaponAfterAimingWeaponFromShooting = state;

        if (!checkToKeepWeaponAfterAimingWeaponFromShooting) {
            lastTimeCheckToKeepWeapon = Time.time;
        }
    }

    public bool isCheckToKeepWeaponAfterAimingWeaponFromShooting ()
    {
        return checkToKeepWeaponAfterAimingWeaponFromShooting;
    }

    public void setLookAlwaysInCameraDirectionState (bool state)
    {
        lookAlwaysInCameraDirection = state;
    }

    public bool isLookAlwaysInCameraDirectionActive ()
    {
        return lookAlwaysInCameraDirection;
    }

    public void setOriginalLookAlwaysInCameraDirectionState ()
    {
        if (fullBodyAwarenessActive) {
            setLookAlwaysInCameraDirectionState (true);

            return;
        }

        setLookAlwaysInCameraDirectionState (originalLookAlwaysInCameraDirection);
    }

    public void setLookInCameraDirectionIfLookingAtTargetState (bool state)
    {
        lookInCameraDirectionIfLookingAtTarget = state;
    }

    public void setLookOnlyIfMovingState (bool state)
    {
        lookOnlyIfMoving = state;
    }

    public void setOriginalLookOnlyIfMovingState ()
    {
        if (fullBodyAwarenessActive) {
            setLookOnlyIfMovingState (false);

            return;
        }

        setLookOnlyIfMovingState (originalLookOnlyIfMoving);
    }

    public void activateOrDeactivateStrafeModeDuringActionSystem (bool state)
    {
        if (state) {
            activateStrafeMode ();
        } else {
            deactivateStrafeMode ();
        }
    }

    public void activateOrDeactivateStrafeMode (bool state)
    {
        if (!state) {
            if (fullBodyAwarenessActive) {
                strafeModeActivatedExternally = false;

                return;
            }
        }

        if (state) {
            activateStrafeMode ();
        } else {
            deactivateStrafeMode ();
        }

        strafeModeActivatedExternally = state;
    }

    public void activateStrafeMode ()
    {
        setLookAlwaysInCameraDirectionState (true);

        setLookOnlyIfMovingState (false);
    }

    public void deactivateStrafeMode ()
    {
        setOriginalLookAlwaysInCameraDirectionState ();

        setOriginalLookOnlyIfMovingState ();
    }

    public void setZeroGravityModeOnState (bool state)
    {
        zeroGravityModeOn = state;

        if (zeroGravityModeOn) {
            if (pushPlayerWhenZeroGravityModeIsEnabled) {
                useJumpPlatform (pushZeroGravityEnabledAmount * playerTransformUp, ForceMode.Impulse);
            }

            if (!playerOnGround) {
                setCheckOnGrundStatePausedFFOrZGState (true);
            }
        } else {
            setCheckOnGrundStatePausedFFOrZGState (false);
        }

        movingVertically = false;

        setLastTimeFalling ();

        IKSystemManager.setIKBodyState (zeroGravityModeOn, "Zero Gravity Mode");

        setFootStepManagerState (zeroGravityModeOn);

        checkOtherStatesOnAirID (state);

        ignoreSetCheckOnGrundStatePausedFFOrZGStateActive = false;
    }

    public void setPushPlayerWhenZeroGravityModeIsEnabledState (bool state)
    {
        pushPlayerWhenZeroGravityModeIsEnabled = state;
    }

    public bool isPlayerOnZeroGravityMode ()
    {
        return zeroGravityModeOn;
    }

    public void changeZeroGravityMovementVelocity (bool value)
    {
        if (value) {
            currentZeroGravitySpeedMultiplier = zeroGravitySpeedMultiplier;

            playerCameraManager.changeCameraFov (true);
        } else {
            currentZeroGravitySpeedMultiplier = 1;

            playerCameraManager.changeCameraFov (false);
        }

        movementSpeedIncreased = value;
    }

    public void setCheckOnGrundStatePausedFFOrZGState (bool state)
    {
        if (state) {
            if ((zeroGravityModeOn && pauseCheckOnGroundStateZG) || (freeFloatingModeOn && pauseCheckOnGroundStateFF)) {
                checkOnGroundStatePausedFFOrZG = state;
            }
        } else {
            checkOnGroundStatePausedFFOrZG = state;
        }
    }

    public void setIgnoreSetCheckOnGrundStatePausedFFOrZGStateActive (bool state)
    {
        ignoreSetCheckOnGrundStatePausedFFOrZGStateActive = state;
    }

    public void setcheckOnGroundStatePausedState (bool state)
    {
        checkOnGroundStatePaused = state;
    }

    public bool isPauseCheckOnGroundStateZGActive ()
    {
        return pauseCheckOnGroundStateZG;
    }

    public void setfreeFloatingModeOnState (bool state)
    {
        freeFloatingModeOn = state;

        if (freeFloatingModeOn) {
            useJumpPlatform (playerTransformUp * pushFreeFloatingModeEnabledAmount, ForceMode.Force);

            if (!playerOnGround) {
                setCheckOnGrundStatePausedFFOrZGState (true);
            }
        } else {
            setCheckOnGrundStatePausedFFOrZGState (false);
        }

        setLastTimeFalling ();

        movingVertically = false;

        playerCameraManager.changeCameraFov (false);

        IKSystemManager.setIKBodyState (freeFloatingModeOn, "Free Floating Mode");

        setFootStepManagerState (freeFloatingModeOn);

        checkOtherStatesOnAirID (state);
    }

    public void enableOrDisableIKSystemManagerState (bool state)
    {
        IKSystemManager.enableOrDisableIKSystemManagerState (state);
    }

    public void setIKBodyPausedState (bool state)
    {
        IKSystemManager.setIKBodyPausedState (state);
    }

    public bool isPlayerOnFreeFloatingMode ()
    {
        return freeFloatingModeOn;
    }

    public void changeFreeFloatingMovementVelocity (bool value)
    {
        if (value) {
            currentFreeFloatingSpeedMultiplier = freeFloatingSpeedMultiplier;

            playerCameraManager.changeCameraFov (true);
        } else {
            currentFreeFloatingSpeedMultiplier = 1;

            playerCameraManager.changeCameraFov (false);
        }

        movementSpeedIncreased = value;
    }

    public void changeSprintFovThirdPerson (bool value)
    {
        if (value) {
            playerCameraManager.changeCameraFov (true);
        } else {
            playerCameraManager.changeCameraFov (false);
        }
    }

    public bool isPlayerOnFFOrZeroGravityModeOn ()
    {
        return zeroGravityModeOn || freeFloatingModeOn;
    }

    public bool isMovementSpeedIncreased ()
    {
        return movementSpeedIncreased;
    }

    public float getCharacterHeight ()
    {
        return originalHeight;
    }

    public float getVerticalSpeed ()
    {
        return playerTransform.InverseTransformDirection (mainRigidbody.linearVelocity).y;
    }

    public float getCurrentSurfaceHitAngle ()
    {
        return hitAngle;
    }

    public void setWalkSpeedValue (float newValue)
    {
        walkSpeed = Mathf.Clamp (newValue, 0, 1);
    }

    public void setOriginalWalkSpeed ()
    {
        setWalkSpeedValue (originalWalkSpeedValue);
    }

    public void setWalkByDefaultState (bool state)
    {
        if (state) {
            setWalkSpeedValue (half);
        } else {
            setWalkSpeedValue (1);
        }
    }

    public void setIncreaseMovementSpeedState (bool state)
    {
        if (state) {
            setWalkSpeedValue (increaseWalkSpeedValue);
        } else {
            setWalkSpeedValue (originalWalkSpeedValue);
        }
    }

    public void setIncreaseWalkSpeedValue (float newValue)
    {
        increaseWalkSpeedValue = newValue;

        if (increaseWalkSpeedValue < originalWalkSpeedValue) {
            increaseWalkSpeedActive = true;
        } else {
            increaseWalkSpeedActive = false;
        }
    }

    public void setIncreaseWalkSpeedEnabled (bool state)
    {
        increaseWalkSpeedEnabled = state;

        increaseWalkSpeedActive = false;
    }

    public void setOriginalIncreaseWalkSpeedEnabledValue ()
    {
        setIncreaseWalkSpeedEnabled (originalIncreaseWalkSpeedEnabledValue);
    }

    public void setNewStationaryTurnSpeedValue (float newValue)
    {
        stationaryTurnSpeed = newValue;
    }

    public void setOriginalStationaryTurnSpeed ()
    {
        setNewStationaryTurnSpeedValue (originalStationaryTurnSpeed);
    }

    public void setStairsValues (float stairsMin, float stairsMax, float stairsAdherence)
    {
        currentStairAdherenceSystemMinValue = stairsMin;
        currentStairAdherenceSystemMaxValue = stairsMax;
        currentStairAdherenceSystemAdherenceValue = stairsAdherence;
    }

    bool usingExternalCharacterMeshList;

    public List<GameObject> externalCharacterMeshList = new List<GameObject> ();

    public void setUsingExternalCharacterMeshListState (bool state)
    {
        usingExternalCharacterMeshList = state;
    }

    public void setExternalCharacterMeshList (List<GameObject> newList)
    {
        externalCharacterMeshList = newList;
    }

    public void setCharacterMeshGameObjectStateOnlyIfThirdPersonActive (bool state)
    {
        if (firstPersonActive) {
            return;
        }

        setCharacterMeshGameObjectState (state);
    }

    public void setExternalCharacterMeshListGameObjectState (bool state)
    {
        if (usingExternalCharacterMeshList) {
            int externalCharacterMeshListCount = externalCharacterMeshList.Count;

            if (externalCharacterMeshListCount > 0) {
                for (int i = 0; i < externalCharacterMeshListCount; i++) {
                    if (externalCharacterMeshList [i] != null && externalCharacterMeshList [i].activeSelf != state) {
                        externalCharacterMeshList [i].SetActive (state);
                    }
                }
            }
        }
    }

    public void setCharacterMeshGameObjectState (bool state)
    {
        if (!checkCharacterMeshIfGeneratedOnStartInitialized) {
            if (checkCharacterMeshIfGeneratedOnStart) {

                Transform objectToCheckCharacterIfGeneratedOnStartTransform = objectToCheckCharacterIfGeneratedOnStart.transform;

                int count = objectToCheckCharacterIfGeneratedOnStartTransform.childCount;

                for (int i = 0; i < count; i++) {
                    Transform child = objectToCheckCharacterIfGeneratedOnStartTransform.GetChild (i);

                    if (child != null) {
                        if (child.GetComponent<SkinnedMeshRenderer> () != null) {
                            extraCharacterMeshGameObject.Add (child.gameObject);
                        }
                    }
                }
            }

            checkCharacterMeshIfGeneratedOnStartInitialized = true;
        }

        if (usingExternalCharacterMeshList) {
            setExternalCharacterMeshListGameObjectState (state);
        } else {
            if (characterMeshGameObject != null) {
                if (characterMeshGameObject.activeSelf != state) {
                    characterMeshGameObject.SetActive (state);
                }
            }

            int extraCharacterMeshGameObjectCount = extraCharacterMeshGameObject.Count;

            if (extraCharacterMeshGameObjectCount > 0) {
                for (int i = 0; i < extraCharacterMeshGameObjectCount; i++) {
                    if (extraCharacterMeshGameObject [i] != null && extraCharacterMeshGameObject [i].activeSelf != state) {
                        extraCharacterMeshGameObject [i].SetActive (state);
                    }
                }
            }
        }

        if (useEventsOnEnableDisableCharacterMeshes) {
            if (Application.isPlaying) {
                if (state) {
                    eventOnEnableCharacterMeshes.Invoke ();
                } else {
                    eventOnDisableCharacterMeshes.Invoke ();
                }
            } else {
                if (state) {
                    eventOnEnableCharacterMeshesOnEditor.Invoke ();
                } else {
                    eventOnDisableCharacterMeshesOnEditor.Invoke ();
                }
            }
        }
    }

    public void setCharacterMeshesListToDisableOnEventState (bool state)
    {
        characterMeshesListToDisableOnEvent.Invoke (state);
    }

    public void setUsingGenericModelActiveState (bool state)
    {
        usingGenericModelActive = state;
    }

    public bool isUsingGenericModelActive ()
    {
        return usingGenericModelActive;
    }

    public void setExtraHeightDiffRaycastRadius (float newValue)
    {
        extraHeightDiffRaycastRadius = newValue;
    }

    public void setCustomMaxStepHeight (float newValue)
    {
        customMaxStepHeight = newValue;
    }

    public void setNewRayDistance (float newValue)
    {
        rayDistance = newValue;
    }

    public void setOriginalRayDistance ()
    {
        setNewRayDistance (originalRayDistance);
    }

    public Transform getGravityCenter ()
    {
        return gravityManager.getGravityCenter ();
    }

    public bool isUsingRegularGravity ()
    {
        return gravityManager.isUsingRegularGravity ();
    }

    public GameObject getCharacterMeshGameObject ()
    {
        return characterMeshGameObject;
    }

    public Transform getCharacterHumanBone (HumanBodyBones boneToFind)
    {
        return animator.GetBoneTransform (boneToFind);
    }

    public Transform getRightHandTransform ()
    {
        Transform rightHandTransform = getCharacterHumanBone (HumanBodyBones.RightHand);

        if (rightHandTransform != null) {
            return rightHandTransform;
        } else {
            return weaponsManager.getRightHandTransform ();
        }
    }

    public Transform getLeftHandTransform ()
    {
        Transform leftHandTransform = getCharacterHumanBone (HumanBodyBones.LeftHand);

        if (leftHandTransform != null) {
            return leftHandTransform;
        } else {
            return weaponsManager.getLeftHandTransform ();
        }
    }

    public bool checkPlayerIsNotCarringWeapons ()
    {
        if (weaponsManagerLocated) {
            return weaponsManager.checkPlayerIsNotCarringWeapons ();
        }

        return false;
    }

    public Animator getCharacterAnimator ()
    {
        return animator;
    }

    public void setNewRuntimeAnimatorController (RuntimeAnimatorController newRuntimeAnimatorController)
    {
        if (animator.runtimeAnimatorController != newRuntimeAnimatorController) {
            animator.runtimeAnimatorController = newRuntimeAnimatorController;
        }
    }

    public void setAnimatorState (bool state)
    {
        animator.enabled = state;
    }

    public void resetAnimator ()
    {
        if (playerCameraManager.isFullBodyAwarenessEnabled ()) {
            return;
        }

        resetAndRebindAnimator ();
    }

    public void resetAnimatorEvenIfFullBodyAwarenessActive ()
    {
        resetAndRebindAnimator ();
    }

    void resetAndRebindAnimator ()
    {
        animator.Rebind ();
        animator.Update (0f);
    }

    public void setAnimatorUnscaledTimeState (bool state)
    {
        if (state) {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        } else {
            animator.updateMode = AnimatorUpdateMode.Normal;
        }
    }

    public void playerCrossFadeInFixedTime (string actionName)
    {
        animator.CrossFadeInFixedTime (actionName, updateSetFloatAnimatorSpeed);
    }

    public void setAnimatorLayerWeight (string animatorLayerName, float animatorLayerWeight)
    {
        animator.SetLayerWeight (animator.GetLayerIndex (animatorLayerName), animatorLayerWeight);
    }

    public void enableAnimatorLayerWeight (string animatorLayerName)
    {
        setAnimatorLayerWeight (animatorLayerName, 1);
    }

    public void disableAnimatorLayerWeight (string animatorLayerName)
    {
        setAnimatorLayerWeight (animatorLayerName, 0);
    }

    public playerInputManager getPlayerInput ()
    {
        return playerInput;
    }

    public void set3dOr2_5dWorldType (bool movingOn3dWorld)
    {
        if (movingOn3dWorld) {
            lockedPlayerMovement = lockedPlayerMovementMode.world3d;
        } else {
            lockedPlayerMovement = lockedPlayerMovementMode.world2_5d;
        }
    }

    public void setMoveInXAxisOn2_5dState (bool state)
    {
        moveInXAxisOn2_5d = state;
    }

    public bool isMoveInXAxisOn2_5d ()
    {
        return moveInXAxisOn2_5d;
    }

    public bool isPlayerMovingOn3dWorld ()
    {
        return lockedPlayerMovement == lockedPlayerMovementMode.world3d;
    }

    public bool isPlayerMovingOn2_5dWorld ()
    {
        return lockedPlayerMovement == lockedPlayerMovementMode.world2_5d;
    }

    public bool isPlayerLookingAtRightOn2_5d ()
    {
        if (isPlayerMovingOn2_5dWorld ()) {
            float angle = Vector3.SignedAngle (playerTransform.forward, currentLockedCameraTransform.right, playerTransform.up);

            if (Mathf.Abs (angle) < 10) {
                return true;
            }
        }

        return false;
    }

    public void setFollowSplinePathActiveState (bool state)
    {
        followSplinePathActive = state;
    }

    public bool isPlayerSetAsChildOfParent ()
    {
        return playerSetAsChildOfParent;
    }

    public Rigidbody getRigidbody ()
    {
        return mainRigidbody;
    }

    public void setPlayerSetAsChildOfParentState (bool state)
    {
        playerSetAsChildOfParent = state;
    }

    public void setHeadTrackCanBeUsedState (bool state)
    {
        headTrackCanBeUsed = state;
    }

    public bool canHeadTrackBeUsed ()
    {
        return headTrackCanBeUsed;
    }

    public void setAddExtraRotationPausedState (bool state)
    {
        addExtraRotationPaused = state;
    }

    public void setAutoTurnSpeed (float newValue)
    {
        autoTurnSpeed = newValue;
    }

    public void setAimTurnSpeed (float newValue)
    {
        aimTurnSpeed = newValue;
    }

    public void updateLandMark ()
    {
        landMarkRayPosition = playerTransform.position;
        landMarkRayDirection = -playerTransformUp;

        if (gravityManager.isPlayerSearchingGravitySurface ()) {
            landMarkRayPosition = mainCameraTransform.position;
            landMarkRayDirection = mainCameraTransform.forward;
        }

        if (zeroGravityModeOn && useGravityDirectionLandMark) {
            if (firstPersonActive) {
                landMarkRayPosition = mainCameraTransform.position;
                landMarkRayDirection = mainCameraTransform.forward;
            } else {
                landMarkRayPosition = forwardSurfaceRayPosition.position;
                landMarkRayDirection = forwardSurfaceRayPosition.forward;
            }
            currentMaxLandDistance = maxDistanceToAdjust;
        } else {
            currentMaxLandDistance = maxLandDistance;
        }

        if (!playerOnGround && canMove) {
            if (flyModeActive || swimModeActive) {
                if (landMark.activeSelf) {
                    landMark.SetActive (false);
                }
            } else {
                if (Physics.Raycast (landMarkRayPosition, landMarkRayDirection, out hit, currentMaxLandDistance, layer)) {

                    if (hit.distance >= minDistanceShowLandMark) {
                        if (!landMark.activeSelf) {
                            landMark.SetActive (true);
                        }

                        landMark.transform.position = hit.point + 0.08f * hit.normal;

                        landMarkForwardDirection = Vector3.Cross (landMark.transform.right, hit.normal);
                        landMarkTargetRotation = Quaternion.LookRotation (landMarkForwardDirection, hit.normal);

                        landMark.transform.rotation = landMarkTargetRotation;

                        landMark1.transform.Rotate (0, 100 * currentUpdateDeltaTime, 0);
                        landMark2.transform.Rotate (0, -100 * currentUpdateDeltaTime, 0);
                    } else {
                        if (landMark.activeSelf) {
                            landMark.SetActive (false);
                        }
                    }
                } else {
                    if (zeroGravityModeOn && useGravityDirectionLandMark) {
                        if (landMark.activeSelf) {
                            landMark.SetActive (false);
                        }
                    } else {
                        if (landMark.activeSelf) {
                            landMark.SetActive (false);
                        }
                    }
                }
            }
        } else {
            if (landMark.activeSelf) {
                landMark.SetActive (false);
            }
        }
    }

    public void setUseLandMarkState (bool state)
    {
        useLandMark = state;
    }

    public void rotateCharacterTowardDirection (Transform directionTransform, float minRotationAngle, float minRotationAmount, float maxRotationAmount)
    {
        stopRotateCharacterTowardDirectionCoroutine ();

        rotateCharacterCoroutine = StartCoroutine (rotateCharacterTowardDirectionCoroutine (directionTransform, minRotationAngle, minRotationAmount, maxRotationAmount));
    }

    public void stopRotateCharacterTowardDirectionCoroutine ()
    {
        if (rotateCharacterCoroutine != null) {
            StopCoroutine (rotateCharacterCoroutine);
        }

        actionActiveWithMovement = false;

        applyRootMotionAlwaysActive = false;

        actionActive = false;

        lastTimeActionActive = Time.time;

        setCanMoveState (true);
    }

    IEnumerator rotateCharacterTowardDirectionCoroutine (Transform directionTransform, float minRotationAngle, float minRotationAmount, float maxRotationAmount)
    {
        float timer = 0;

        bool targetReached = false;

        actionActiveWithMovement = true;

        applyRootMotionAlwaysActive = true;

        actionActive = true;

        setCanMoveState (false);

        resetPlayerControllerInput ();

        resetOtherInputFields ();

        while (!targetReached) {

            float newTurnAmount = 0;

            float angle = Vector3.SignedAngle (playerTransformForward, directionTransform.forward, playerTransformUp);

            if (Mathf.Abs (angle) > minRotationAngle) {
                newTurnAmount = angle * Mathf.Deg2Rad;

                newTurnAmount = Mathf.Clamp (newTurnAmount, -maxRotationAmount, maxRotationAmount);
            } else {
                newTurnAmount = 0;
            }

            float turnAmountToApply = newTurnAmount;
            if (turnAmountToApply < 0) {
                if (turnAmountToApply > -minRotationAmount) {
                    turnAmountToApply = -minRotationAmount;
                }
            } else {
                if (turnAmountToApply < minRotationAmount) {
                    turnAmountToApply = minRotationAmount;
                }
            }

            setOverrideTurnAmount (turnAmountToApply, true);

            timer += Time.deltaTime;

            if (newTurnAmount == 0) {
                targetReached = true;

                setOverrideTurnAmount (0, false);
            }

            yield return null;
        }

        stopRotateCharacterTowardDirectionCoroutine ();
    }

    bool turnBasedCombatActionActive;

    public bool isTurnBasedCombatActionActive ()
    {
        return turnBasedCombatActionActive;
    }

    public void setTurnBasedCombatActionActiveState (bool state)
    {
        turnBasedCombatActionActive = state;
    }

    public bool isTerrainDetected ()
    {
        return terrainDetected;
    }

    public void setTerrainDetectedState (bool state)
    {
        terrainDetected = state;
    }

    public void destroyCharacterAtOnce ()
    {
        Destroy (playerManagersParentGameObject);
        Destroy (playerCameraGameObject);
        Destroy (gameObject);
    }

    //EDITOR FUNCTIONS
    public void setCharacterMeshGameObject (GameObject characterMesh)
    {
        characterMeshGameObject = characterMesh;

        updateComponent ();
    }

    public void setStrafeModeFromEditor (bool state)
    {
        setLookAlwaysInCameraDirectionState (state);

        setLookOnlyIfMovingState (false);

        updateComponent ();
    }

    public void setCrouchSlidingEnabledStateFromEditor (bool state)
    {
        setCrouchSlidingEnabledState (state);

        updateComponent ();
    }

    public void setAirDashEnabledStateFromEditor (bool state)
    {
        setAirDashEnabledState (state);

        updateComponent ();
    }

    public void setWallRunningEnabledStateFromEditor (bool state)
    {
        wallRunningEnabled = state;

        updateComponent ();
    }

    public void setUseObstacleDetectionToAvoidMovementStateFromEditor (bool state)
    {
        setUseObstacleDetectionToAvoidMovementState (state);

        updateComponent ();
    }

    public void setCheckQuickMovementTurn180DegreesOnRunStateFromEditor (bool state)
    {
        setCheckQuickMovementTurn180DegreesOnRunState (state);

        updateComponent ();
    }

    public void enableOrDisableDoubleJumpFromEditor (bool state)
    {
        enableOrDisableDoubleJump (state);

        updateComponent ();
    }

    public void setUseLandMarkStateFromEditor (bool state)
    {
        setUseLandMarkState (state);

        updateComponent ();
    }

    public void setSprintEnabledStateFromEditor (bool state)
    {
        setSprintEnabledState (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Player Controller", gameObject);
    }

#if UNITY_EDITOR
    //Draw gizmos
    void OnDrawGizmos ()
    {
        if (!showGizmo) {
            return;
        }

        if (GKC_Utils.isCurrentSelectionActiveGameObject (gameObject)) {
            DrawGizmos ();
        }
    }

    void OnDrawGizmosSelected ()
    {
        DrawGizmos ();
    }

    void DrawGizmos ()
    {
        if (showGizmo) {
            if (playerTransform == null) {
                playerTransform = transform;
            }

            if (useAutoCrouch) {
                Gizmos.color = gizmoColor;

                Vector3 position = autoCrouchRayPosition.position;
                Gizmos.DrawSphere (position, gizmoRadius);
                Gizmos.color = Color.white;
                Gizmos.DrawLine (position, position + raycastDistanceToAutoCrouch * playerTransform.forward);

                Gizmos.DrawSphere (position + secondRaycastOffset, gizmoRadius);

                Gizmos.DrawLine (position + secondRaycastOffset, position + secondRaycastOffset + raycastDistanceToAutoCrouch * playerTransform.forward);
            }

            Debug.DrawRay (playerTransform.position + playerTransform.up, rayDistance * (-playerTransform.up), Color.white);

            if (useSphereRaycastForGroundDetection) {
                Gizmos.DrawSphere (currentPlayerPosition - sphereCastOffset * currentPlayerUp, sphereCastRadius);
            }

            showNewGroundAdherenceGizmo ();
        }
    }

    void showNewGroundAdherenceGizmo ()
    {
        Vector3 localUp = playerTransform.up;

        Gizmos.color = Color.green;
        Vector3 bottom = transform.position + localUp * capsule.radius;
        Vector3 top = transform.position + localUp * (capsule.height - capsule.radius);
        Gizmos.DrawLine (bottom, top);
        Gizmos.DrawWireSphere (bottom, capsule.radius);
        Gizmos.DrawWireSphere (top, capsule.radius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere (dbgFuturePos, 0.05f);

        if (dbgHasGroundHit) {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere (dbgGroundHit.point, 0.06f);
            Gizmos.DrawLine (playerTransform.position, dbgGroundHit.point);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine (dbgGroundHit.point, dbgGroundHit.point + dbgGroundHit.normal);

            UnityEditor.Handles.Label (dbgGroundHit.point + localUp * 0.2f,
                $"Slope: {dbgSlopeAngle:F1}°");
        }

        //Gizmos.color = Color.cyan;
        //Gizmos.DrawLine (playerTransform.position, playerTransform.position - localUp * stickToGroundDistance);
    }
#endif


    void updateAutoCrouchCheck ()
    {
        if (!crouching) {
            if (!Physics.Raycast (autoCrouchRayPosition.position, playerTransformForward, raycastDistanceToAutoCrouch, layerToCrouch)) {

                if (showGizmo) {
                    Debug.DrawRay (autoCrouchRayPosition.position, raycastDistanceToAutoCrouch * playerTransformForward, Color.white);
                }

                if (Physics.Raycast (autoCrouchRayPosition.position + secondRaycastOffset, playerTransformForward, raycastDistanceToAutoCrouch, layerToCrouch)) {

                    if (showGizmo) {
                        Debug.DrawRay (autoCrouchRayPosition.position + secondRaycastOffset, raycastDistanceToAutoCrouch * playerTransformForward, Color.red);
                    }

                    crouch ();
                }
            }
        }
    }

    //check if the player is in the ground with a raycast
    void checkOnGround ()
    {
        if (useNewGroundAdherenceEnabled) {
            newCheckOnGroundAdherence ();
        } else {
            oldCheckOnGroundAdherence ();
        }
    }

    void newCheckOnGroundAdherence ()
    {
        if (checkOnGroundPaused) {
            if (currentSurfaceBelowPlayerLocated) {
                currentSurfaceBelowPlayer = null;

                currentSurfaceBelowPlayerLocated = false;

                previousSurfaceBelowPlayer = null;

                terrainDetected = false;
            }

            return;
        }

        if (pauseAllPlayerDownForces) {
            return;
        }

        //START OF CHECK ON GROUND STATE
        if (!gravityPowerActive && !usingJetpack && !flyModeActive && !swimModeActive && !externalControlBehaviorForAirTypeActive) {

            currentPlayerPosition = playerTransform.position;
            currentPlayerUp = playerTransform.up;

            downSpeedValue = halfJumpPower;

            if (downSpeedValue == 0) {
                downSpeedValue = Mathf.Abs (gravityForce) * half;
            }

            if (playerTransform.InverseTransformDirection (mainRigidbody.linearVelocity).y < downSpeedValue) {
                previouslyOnGround = playerOnGround;

                setOnGroundState (false);

                if (jump || slowingFall) {
                    if (!gravityForcePaused &&
                        !zeroGravityModeOn &&
                        !freeFloatingModeOn &&
                        !wallRunningActive &&
                        !pauseAllPlayerDownForces &&
                        !externalControlBehaviorForAirTypeActive) {

                        Vector3 totalAirForce = gravityForce * (mainRigidbody.mass * jumpMassMultiplier) * currentNormal;

                        mainRigidbody.AddForce (totalAirForce);
                    }
                }

                //check what it is under the player
                mainRaycastPosition = currentPlayerPosition + currentPlayerUp;

                hitAngle = 0;
                groundPoint = vector3Zero;

                mainHitPoint = currentPlayerPosition;

                groundDetected = false;

                if (Physics.Raycast (mainRaycastPosition, -currentPlayerUp, out mainGroundHit, rayDistance, layer)) {
                    groundDetected = true;
                }

                if (useSphereRaycastForGroundDetection) {
                    if (!groundDetected) {
                        if (Physics.Raycast (mainRaycastPosition, -currentPlayerUp, out temporalHit, rayDistance + 1.5f, layer)) {
                            float temporalHitDistance = temporalHit.distance;

                            if (temporalHitDistance < rayDistance + 0.2f) {
                                mainGroundHit = temporalHit;

                                groundDetected = true;
                            }
                        }
                    }

                    if (!groundDetected) {
                        sphereRaycastRay = new Ray (currentPlayerPosition - sphereCastOffset * currentPlayerUp, -currentPlayerUp);

                        if (Physics.SphereCast (sphereRaycastRay, sphereCastRadius, out sphereRaycastHit, maxDistanceSphereCast, layer)) {
                            groundDetected = true;

                            mainGroundHit = sphereRaycastHit;

                            if (showGizmo) {
                                Debug.DrawRay (mainGroundHit.point, 100 * Vector3.up, Color.black, 10);
                            }

                            bool nextSurfaceLocated = false;

                            Vector3 playerMovementDirection = 5 * mainRigidbody.linearVelocity;

                            playerMovementDirection.Normalize ();

                            Vector3 temporalRaycastPosition = currentPlayerPosition + 0.05f * playerMovementDirection + 0.1f * currentPlayerUp;

                            int temporalRaycastCheckAmount = 0;

                            while (!nextSurfaceLocated) {

                                if (Physics.Raycast (temporalRaycastPosition, -currentPlayerUp, out sphereRaycastHit, 0.17f, layer)) {
                                    mainGroundHit = sphereRaycastHit;

                                    nextSurfaceLocated = true;
                                } else {
                                    temporalRaycastPosition += 0.03f * playerMovementDirection;

                                    temporalRaycastCheckAmount++;

                                    if (showGizmo) {
                                        Debug.DrawRay (temporalRaycastPosition, 100 * Vector3.up, Color.yellow, 10);
                                    }
                                }

                                if (temporalRaycastCheckAmount >= 5) {
                                    nextSurfaceLocated = true;

                                    groundDetected = false;
                                }
                            }
                        }
                    }

                    if (!groundDetected) {
                        if (Physics.Raycast (mainRaycastPosition, -currentPlayerUp, out temporalHit, rayDistance + 1.5f, layer)) {
                            float temporalHitDistance = temporalHit.distance;

                            if (previouslyOnGround) {
                                if (temporalHitDistance < rayDistance + maxExtraRaycastDistanceToGroundDetection) {
                                    mainGroundHit = temporalHit;

                                    groundDetected = true;
                                }
                            }
                        }
                    }
                }

                if (groundDetected) {
                    //get the angle of the current surface

                    hitAngle = Vector3.Angle (currentNormal, mainGroundHit.normal);

                    canSetGroundState = false;

                    //check max angle between the ground and the player if he is on zero gravity or free floating mode
                    if ((freeFloatingModeOn || zeroGravityModeOn) && !checkOnGroundStatePausedFFOrZG && !previouslyOnGround) {
                        if (useMaxAngleToCheckOnGroundStateZGFF) {
                            if (hitAngle < maxAngleToChekOnGroundStateZGFF) {
                                canSetGroundState = true;
                            }
                        } else {
                            canSetGroundState = true;
                        }
                    } else {
                        canSetGroundState = true;
                    }

                    if (zeroGravityModeOn && ignoreSetCheckOnGrundStatePausedFFOrZGStateActive) {
                        canSetGroundState = true;
                    }

                    if (canSetGroundState) {
                        setOnGroundState (true);
                    }

                    jump = false;

                    slowingFall = false;

                    slowFallExternallyActive = false;

                    groundPoint = mainGroundHit.point;

                    currentSurfaceBelowPlayer = mainGroundHit.collider.transform;

                    currentSurfaceBelowPlayerLocated = true;

                    currentNumberOfDash = 0;

                    distanceToGround = mainGroundHit.distance;

                    if (!useSphereRaycastForGroundDetection) {
                        distanceToGround -= 1;
                    }

                    mainHitPoint = groundPoint;
                    mainHitAngle = hitAngle;

                    if (checkIfSurfaceBelowIsTerrainEnabled) {
                        if (previousSurfaceBelowPlayer != currentSurfaceBelowPlayer) {
                            previousSurfaceBelowPlayer = currentSurfaceBelowPlayer;

                            terrainDetected = previousSurfaceBelowPlayer.GetComponent<Terrain> ();
                        }
                    }
                } else {
                    if (currentSurfaceBelowPlayerLocated) {
                        currentSurfaceBelowPlayer = null;

                        currentSurfaceBelowPlayerLocated = false;

                        previousSurfaceBelowPlayer = null;

                        terrainDetected = false;
                    }
                }

                if (useMaxWalkSurfaceAngle && !stairStepDetected) {
                    if (playerOnGround && hitAngle > maxWalkSurfaceAngle && (!useMaxDistanceToCheckSurfaceAngle || distanceToGround > maxDistanceToCheckSurfaceAngle)) {
                        if (lastTimeMaxWalkSurfaceAngleLocated == 0) {
                            lastTimeMaxWalkSurfaceAngleLocated = Time.time;
                        } else if (Time.time > lastTimeMaxWalkSurfaceAngleLocated + 0.3f) {
                            setOnGroundState (false);
                        }
                    } else {
                        lastTimeMaxWalkSurfaceAngleLocated = 0;
                    }
                }

                //new checks for the new ground adherence

                //check if the player has to adhere to the surface or not
                adhereToGround = false;

                if (playerOnGround) {
                    adhereToGround = true;
                }


                //ignore to adhere to the ground below if the player is rotating to a different gravity direction
                if (lockedCameraActive) {
                    if (isPlayerMovingOn2_5dWorld ()) {
                        if (characterRotatingToSurface) {
                            adhereToGround = false;
                        }
                    }
                }

                //the player has to adhere to the current surface, so
                if (adhereToGround) {
                    //check what states should allow the adhere to ground
                    inputDir = GetInputDirection ();

                    stickToGroundOnIdleResult = true;

                    bool pressingInput = isPlayerUsingInput ();

                    //if the player is not moving and the angle of the surface is 0, adhere to it, so if the player jumps for example, the player is correctly
                    //placed in the ground, with out a gap between the player's collider and the ground

                    if (useTankControls) {
                        pressingInput = isPlayerUsingVerticalInput ();
                    }

                    if ((pressingInput && isPlayerMoving (0.1f)) || (!pressingInput && isPlayerMoving (0.4f))) {
                        stickToGroundOnIdleResult = false;
                    }

                    //add a check to avoid movement from input axis at the end of an action system
                    //during the transition to the movement state machine
                    if (actionActive) {
                        if (!canMove) {
                            checkingLastTimeActionActiveAndCantMove = true;
                        } else {
                            checkingLastTimeActionActiveAndCantMove = false;
                        }
                    } else {
                        if (checkingLastTimeActionActiveAndCantMove) {
                            if (Time.time > lastTimeActionActive + 0.3f) {
                                checkingLastTimeActionActiveAndCantMove = false;
                            }
                        }
                    }

                    //Maybe check if an action is active and using movement or forces, to avoid to stick to the ground

                    if (stickToGroundOnIdleResult) {
                        StickToGroundIdle (true, false, false);
                    } else {
                        TryMove (inputDir);
                    }
                }

                //END OF CHECK ON GROUND STATE

                if (showGizmo) {
                    Debug.DrawLine (currentPlayerPosition + (4 * currentPlayerUp), adherenceHit.point, Color.black);
                }

                if (playerOnGround) {
                    if (useAutoCrouch) {
                        updateAutoCrouchCheck ();
                    }
                }
            }

            if (!playerOnGround) {
                lastTimeAir = Time.time;
            }
        } else {
            setOnGroundState (false);
        }
    }

    public void setIgnoreNewGroundAdherenceActiveState (bool state)
    {
        ignoreNewGroundAdherenceActive = state;
    }


    public void setInoreInputOnNewGroundAdherenceActive (bool state)
    {
        ignoreInputOnNewGroundAdherenceActive = state;
    }

    Vector3 GetInputDirection ()
    {
        if (lockedCameraActive && useRelativeMovementToLockedCamera && !usedByAI) {
            currentAdherenceInput = lockedCameraMoveInput;
        } else {
            currentAdherenceInput = currentMoveInput;
        }

        return currentAdherenceInput * raycastPositionRange;
    }

    void TryMove (Vector3 moveDirectionInput)
    {
        if (ignoreNewGroundAdherenceActive) {
            return;
        }

        //add also a check for the input values

        float dt = Time.fixedDeltaTime;
        Vector3 localUp = playerTransform.up;

        //add a check for the different view types, first or third person and the locked cameras

        if (!playerCameraManager.is2_5ViewActive ()) {
            if (useTankControls) {
                if (!playerIsAiming && !isPlayerUsingVerticalInput ()) {

                    StickToGroundIdle (false, true, false);

                    return;
                }
            }
        }

        if (ignoreInputOnNewGroundAdherenceActive) {
            moveDirectionInput = Vector3.zero;
        }

        Vector3 mainMovementDirection = moveDirectionInput.normalized;

        //if (usedByAI || (hitAngle > minAngleToUsedInputNormalized) || (walkSpeed <= 0.5f)) {
        //    moveDirectionInput = moveDirectionInput.normalized;
        //}

        if (hitAngle > minAngleToUsedInputNormalized) {
            moveDirectionInput = moveDirectionInput.normalized;
        }

        if (!actionActive) {
            if (checkingLastTimeActionActiveAndCantMove) {
                moveDirectionInput /= 20;
            }
        }

        Vector3 desiredMove = moveDirectionInput * dt;
        Vector3 futurePos = playerTransform.position + desiredMove;
        dbgFuturePos = futurePos;

        futureHasGround = TryGetGroundAtPosition (futurePos, localUp, out RaycastHit groundHit);
        dbgHasGroundHit = futureHasGround;
        dbgGroundHit = groundHit;
        dbgSlopeAngle = 0f;

        groundHeightDifference = 0f;

        float slopeAngle = 0;

        //tryMoveValue = 0;

        float capsuleRadius = capsule.radius;

        if (usingGenericModelActive) {
            if (characterRadius > 0) {
                capsuleRadius = characterRadius;
            }
        }

        float currentHeightDiffRaycastRadius = heightDiffRaycastRadius;

        if (extraHeightDiffRaycastRadius > 0) {
            currentHeightDiffRaycastRadius += extraHeightDiffRaycastRadius;
        }

        float currentMaxStepHeight = maxStepHeight;

        if (customMaxStepHeight > 0) {
            currentMaxStepHeight = customMaxStepHeight;
        }

        //add a check for max slope to prevent movement into that direction, using stick to ground instead

        if (futureHasGround) {
            Vector3 surfaceInFront = playerTransform.position + mainMovementDirection * (currentHeightDiffRaycastRadius + 0.3f);

            if (TryGetGroundAtPosition (surfaceInFront, localUp, out RaycastHit futureGroundHit)) {
                futureSurfaceAngle = Vector3.Angle (futureGroundHit.normal, localUp);

                if (futureSurfaceAngle > maxSlopeAngle) {
                    movingTowardWall = false;

                    //add a check to see if the surface is an actual wall that should
                    //stop movement by just collision and not stick to ground
                    groundHeightDifference = Vector3.Dot (futureGroundHit.point - playerTransform.position, localUp);

                    float heightDiffABS = Mathf.Abs (groundHeightDifference);

                    if (heightDiffABS > currentMaxStepHeight) {
                        movingTowardWall = true;
                    } else {
                        if (Physics.SphereCast (playerTransform.position + capsule.center, originalRadius,
                            mainMovementDirection, out RaycastHit hitInFront, capsuleRadius + 0.5f, layer)) {

                            movingTowardWall = true;
                        }
                    }

                    StickToGroundIdle (false, true, movingTowardWall);

                    return;
                }
            }
        }

        if (futureHasGround) {
            //calculate height from the ground to the player pivot position
            groundHeightDifference = Vector3.Dot (groundHit.point - playerTransform.position, localUp);

            float heightDiffABS = Mathf.Abs (groundHeightDifference);

            slopeAngle = Vector3.Angle (groundHit.normal, localUp);

            float slopeAngleABS = Mathf.Abs (slopeAngle);

            //check to avoid this if the character is moving on a slope

            stepsDetected = false;

            //maybe launch a raycast in the movement slope direction to check for possible steps

            Vector3 raycastPosition = playerTransform.position + localUp * checkStepRaycastHeight;

            Vector3 raycastDirection = mainMovementDirection;

            //tryMoveValue = 1;

            if (slopeAngleABS > 2) {
                if (lastTimeStepDetected == 0) {
                    raycastDirection = Vector3.ProjectOnPlane (mainMovementDirection, groundHit.normal).normalized;

                    if (showGizmo) {
                        Debug.DrawRay (raycastPosition, raycastDirection * 3, Color.black);
                    }

                    if (Physics.Raycast (raycastPosition, raycastDirection, out RaycastHit hitInFront, capsuleRadius + checkStepRaycastDistance, layer)) {
                        float futureStepAngle = Vector3.Angle (hitInFront.normal, localUp);

                        float futureStepAngleABS = Mathf.Abs (futureStepAngle);

                        if (futureStepAngleABS > maxSlopeAngle || futureStepAngleABS > 60) {
                            stepsDetected = true;

                            lastTimeStepDetected = Time.time;
                        }
                    }
                } else {
                    if (Time.time > lastTimeStepDetected + durationToWalkToStep) {
                        lastTimeStepDetected = 0;
                    } else {
                        stepsDetected = true;
                    }
                }
            } else {
                //maybe check a delay to use this check as true for x amount of time to climb into the step if it goes up 
                if (lastTimeStepDetected == 0) {
                    if (showGizmo) {
                        Debug.DrawRay (raycastPosition, raycastDirection * (capsuleRadius + checkStepRaycastDistance), Color.black);
                    }

                    if (Physics.Raycast (raycastPosition, raycastDirection, out RaycastHit hitInFront, capsuleRadius + checkStepRaycastDistance, layer)) {
                        if (groundHeightDifference < 0.01f) {
                            stepsDetected = true;

                            lastTimeStepDetected = Time.time;
                        }
                    }
                } else {
                    if (Time.time > lastTimeStepDetected + durationToWalkToStep) {
                        lastTimeStepDetected = 0;
                    } else {
                        stepsDetected = true;
                    }
                }
            }

            if (stepsDetected) {
                Vector3 surfaceInFront = playerTransform.position + mainMovementDirection * currentHeightDiffRaycastRadius;

                futureHasGround = TryGetGroundAtPosition (surfaceInFront, localUp, out groundHit);
                dbgHasGroundHit = futureHasGround;
                dbgGroundHit = groundHit;

                groundHeightDifference = Vector3.Dot (groundHit.point - playerTransform.position, localUp);

                if (showGizmo) {
                    Debug.DrawLine (groundHit.point, groundHit.point + localUp * 5, Color.red);
                }

                slopeAngle = Vector3.Angle (groundHit.normal, localUp);

                //tryMoveValue = 2;
            }

            movementDirection = raycastDirection;
        }

        //check if the slope is high enough to avoid movement on that direction
        if (futureHasGround) {

            dbgSlopeAngle = slopeAngle;

            if (slopeAngle > maxSlopeAngle) {
                //tryMoveValue = 3;

                return;
            }
        }

        //allow movement
        Vector3 newPos = playerTransform.position + desiredMove;

        if (!actionActive) {
            if (checkingLastTimeActionActiveAndCantMove) {
                newPos = playerTransform.position;
            }
        }

        //a surface in the future movement direction was found, so adjust player position to that position
        if (futureHasGround) {
            //tryMoveValue = 4;

            movingOnSlopeUp = false;
            movingOnSlopeDown = false;

            if (slopeAngle > 1) {
                float movementAngle = Vector3.Angle (movementDirection, localUp);

                if (movementAngle > 90) {
                    movingOnSlopeDown = true;
                } else {
                    movingOnSlopeUp = true;
                }
            }

            if (groundHeightDifference > 0f && groundHeightDifference <= currentMaxStepHeight) {
                //go up

                newPos = Vector3.Lerp (newPos, playerTransform.position + localUp * groundHeightDifference, groundLerpSpeed * dt);

                isGrounded = true;

                //tryMoveValue = 5;
            } else if (groundHeightDifference < 0f && Mathf.Abs (groundHeightDifference) <= currentMaxStepHeight) {
                //go down

                newPos = Vector3.Lerp (newPos, playerTransform.position + localUp * groundHeightDifference, groundLerpSpeed * dt);

                isGrounded = true;

                //tryMoveValue = 6;
            } else if (Mathf.Abs (groundHeightDifference) > currentMaxStepHeight) {
                isGrounded = false;

                //tryMoveValue = 7;
            }
        } else {
            isGrounded = false;

            //tryMoveValue = 8;
        }


        //check when the input axis is not pressed after release or is not fully pressed, to keep the position to adhere better
        bool pressingInput = isPlayerUsingInput ();

        if (!pressingInput && hitAngle == 0) {
            //if no steps or slope is found, use the position below the player
            if (!stepsDetected && !movingOnSlopeDown && !movingOnSlopeUp) {
                newPos = mainHitPoint;
            }
        } else if (!pressingInput || (movingOnSlopeUp && hitAngle != 0 && !isPlayerMoving (0.99f)) || (movingOnSlopeDown && hitAngle != 0 && !isPlayerMoving (0.99f))) {

            //if on a slope, check if moving up or down on it and the distance to the ground
            bool sphereCastFound = false;

            if (Physics.SphereCast (currentPlayerPosition + currentPlayerUp, originalRadius, -currentPlayerUp, out adherenceHit, 0.75f, layer)) {

                if (showGizmo) {
                    Debug.DrawLine (currentPlayerPosition + currentPlayerUp, adherenceHit.point, Color.black);
                }

                sphereCastFound = true;
            }

            float heightDiffABS = Mathf.Abs (Vector3.Dot (mainHitPoint - playerTransform.position, localUp));

            if (movingOnSlopeUp) {
                if (heightDiffABS < 0.05f || sphereCastFound) {
                    newPos = currentPlayerPosition;
                } else {
                    newPos = mainHitPoint;
                }
            } else if (movingOnSlopeDown) {
                if (pressingInput) {
                    if (!isPlayerMoving (0.25f) && (sphereCastFound || heightDiffABS < 0.05f)) {
                        newPos = currentPlayerPosition;
                    }
                } else {
                    if (sphereCastFound) {
                        if (heightDiffABS > 0.1f) {
                            if (Mathf.Abs (adherenceHit.distance) > heightDiffABS) {
                                newPos = currentPlayerPosition;
                            }
                        } else {
                            newPos = currentPlayerPosition;
                        }
                    } else {
                        if (heightDiffABS > 0.1f) {
                            if (Mathf.Abs (adherenceHit.distance) > heightDiffABS) {
                                newPos = currentPlayerPosition;
                            }
                        } else {
                            newPos = currentPlayerPosition;
                        }
                    }
                }
            }
        }

        //apply the position to the rigidbody
        mainRigidbody.MovePosition (newPos);
    }

    bool TryGetGroundAtPosition (Vector3 samplePos, Vector3 localUp, out RaycastHit hitInfo)
    {
        float originHeight = (currentHeight / 4) + 1f;

        Vector3 origin = samplePos + localUp * originHeight;

        float maxDist = originHeight + groundRaycastDistance;

        if (Physics.Raycast (origin, -localUp, out RaycastHit hit, maxDist, layer)) {
            hitInfo = hit;

            return true;
        }

        hitInfo = new RaycastHit ();

        return false;
    }

    void StickToGroundIdle (bool useMovePositionLerp, bool useFixedPositionBelowPlayer, bool movingTowardWall)
    {
        if (ignoreNewGroundAdherenceActive) {
            return;
        }

        lastTimeStepDetected = 0;

        Vector3 localUp = playerTransform.up;

        //stickToGroundValue = 0;

        if (TryGetGroundAtPosition (playerTransform.position, localUp, out RaycastHit groundHit)) {
            //stickToGroundValue = 1;

            groundHeightDifference = Vector3.Dot (groundHit.point - playerTransform.position, localUp);

            float heightDiffABS = Mathf.Abs (groundHeightDifference);

            //add checks to stick to the surfce below according to if the player is on a certain slope angle

            currentPlayerPosition = playerTransform.position;

            adjustPositionResult = false;

            Vector3 targetPosition = groundHit.point;


            //add a check for the surface below, to check its angle

            if (hitAngle < 1) {
                adjustPositionResult = true;
            } else {
                float currentMaxStepHeight = maxStepHeight;

                if (customMaxStepHeight > 0) {
                    currentMaxStepHeight = customMaxStepHeight;
                }

                if (heightDiffABS <= currentMaxStepHeight) {
                    //on terrain, maybe add a check for a higher distance or depending of the surface angle
                    bool surfaceFoundBySphereCastResult = false;

                    if (Physics.SphereCast (currentPlayerPosition + currentPlayerUp, originalRadius, -currentPlayerUp, out adherenceHit, raycastDistanceOnSphereCastOnIdle, layer)) {

                        if (showGizmo) {
                            Debug.DrawLine (currentPlayerPosition + currentPlayerUp, adherenceHit.point, Color.black);
                        }

                        surfaceFoundBySphereCastResult = true;
                    }

                    if (useFixedPositionBelowPlayer) {
                        if (heightDiffABS > 0.1f) {
                            adjustPositionResult = true;
                        } else {
                            if (surfaceFoundBySphereCastResult) {
                                adjustPositionResult = true;

                                targetPosition = currentPlayerPosition;
                            }
                        }
                    } else {
                        if (heightDiffABS > 0.1f) {
                            adjustPositionResult = true;

                            if (surfaceFoundBySphereCastResult) {
                                if (Mathf.Abs (adherenceHit.distance) > heightDiffABS) {
                                    targetPosition = currentPlayerPosition;
                                }
                            }
                        } else {
                            if (surfaceFoundBySphereCastResult) {
                                adjustPositionResult = true;

                                targetPosition = currentPlayerPosition;
                            }
                        }
                    }
                }
            }

            if (movingTowardWall) {
                adjustPositionResult = true;

                targetPosition = groundHit.point;

                useMovePositionLerp = false;
            }

            if (adjustPositionResult) {
                if (useMovePositionLerp) {
                    Vector3 pos = Vector3.Lerp (playerTransform.position, targetPosition, groundLerpSpeed * Time.fixedDeltaTime);

                    mainRigidbody.MovePosition (pos);
                } else {
                    if (showGizmo) {
                        Debug.DrawRay (targetPosition, playerTransform.up * 100, Color.white);
                    }

                    mainRigidbody.MovePosition (targetPosition);
                }

                isGrounded = true;

                //stickToGroundValue = 2;
            } else {
                isGrounded = false;

                //stickToGroundValue = 3;
            }
        } else {
            isGrounded = false;

            //stickToGroundValue = 4;
        }
    }

    //###############################################################################################################
    //old ground adherence code, it will be revamped to make some improvements to the new method in another update

    void oldCheckOnGroundAdherence ()
    {
        if (checkOnGroundPaused) {
            if (currentSurfaceBelowPlayerLocated) {
                currentSurfaceBelowPlayer = null;

                currentSurfaceBelowPlayerLocated = false;

                previousSurfaceBelowPlayer = null;

                terrainDetected = false;
            }

            return;
        }

        if (pauseAllPlayerDownForces) {
            return;
        }

        //START OF CHECK ON GROUND STATE
        if (!gravityPowerActive && !usingJetpack && !flyModeActive && !swimModeActive && !externalControlBehaviorForAirTypeActive) {

            currentPlayerPosition = playerTransform.position;
            currentPlayerUp = playerTransform.up;

            downSpeedValue = halfJumpPower;

            if (downSpeedValue == 0) {
                downSpeedValue = Mathf.Abs (gravityForce) * half;
            }

            if (playerTransform.InverseTransformDirection (mainRigidbody.linearVelocity).y < downSpeedValue) {
                previouslyOnGround = playerOnGround;

                setOnGroundState (false);

                if (jump || slowingFall) {
                    if (!gravityForcePaused &&
                        !zeroGravityModeOn &&
                        !freeFloatingModeOn &&
                        !wallRunningActive &&
                        !pauseAllPlayerDownForces &&
                        !externalControlBehaviorForAirTypeActive) {

                        Vector3 totalAirForce = gravityForce * (mainRigidbody.mass * jumpMassMultiplier) * currentNormal;

                        mainRigidbody.AddForce (totalAirForce);
                    }
                }

                //check what it is under the player
                mainRaycastPosition = currentPlayerPosition + currentPlayerUp;

                hitAngle = 0;
                groundPoint = vector3Zero;

                mainHitPoint = currentPlayerPosition;

                groundDetected = false;


                if (Physics.Raycast (mainRaycastPosition, -currentPlayerUp, out mainGroundHit, rayDistance, layer)) {
                    groundDetected = true;
                }

                if (useSphereRaycastForGroundDetection) {
                    if (!groundDetected) {
                        if (Physics.Raycast (mainRaycastPosition, -currentPlayerUp, out temporalHit, rayDistance + 1.5f, layer)) {
                            float temporalHitDistance = temporalHit.distance;

                            if (temporalHitDistance < rayDistance + 0.2f) {
                                mainGroundHit = temporalHit;

                                groundDetected = true;
                            }
                        }
                    }

                    if (!groundDetected) {
                        sphereRaycastRay = new Ray (currentPlayerPosition - sphereCastOffset * currentPlayerUp, -currentPlayerUp);

                        if (Physics.SphereCast (sphereRaycastRay, sphereCastRadius, out sphereRaycastHit, maxDistanceSphereCast, layer)) {
                            groundDetected = true;

                            mainGroundHit = sphereRaycastHit;

                            if (showGizmo) {
                                Debug.DrawRay (mainGroundHit.point, 100 * Vector3.up, Color.black, 10);
                            }

                            bool nextSurfaceLocated = false;

                            Vector3 playerMovementDirection = 5 * mainRigidbody.linearVelocity;

                            playerMovementDirection.Normalize ();

                            Vector3 temporalRaycastPosition = currentPlayerPosition + 0.05f * playerMovementDirection + 0.1f * currentPlayerUp;

                            int temporalRaycastCheckAmount = 0;

                            while (!nextSurfaceLocated) {

                                if (Physics.Raycast (temporalRaycastPosition, -currentPlayerUp, out sphereRaycastHit, 0.17f, layer)) {
                                    mainGroundHit = sphereRaycastHit;

                                    nextSurfaceLocated = true;
                                } else {
                                    temporalRaycastPosition += 0.03f * playerMovementDirection;

                                    temporalRaycastCheckAmount++;

                                    if (showGizmo) {
                                        Debug.DrawRay (temporalRaycastPosition, 100 * Vector3.up, Color.yellow, 10);
                                    }
                                }

                                if (temporalRaycastCheckAmount >= 5) {
                                    nextSurfaceLocated = true;

                                    groundDetected = false;
                                }
                            }
                        }
                    }

                    if (!groundDetected) {
                        if (Physics.Raycast (mainRaycastPosition, -currentPlayerUp, out temporalHit, rayDistance + 1.5f, layer)) {
                            float temporalHitDistance = temporalHit.distance;

                            if (previouslyOnGround) {
                                if (temporalHitDistance < rayDistance + maxExtraRaycastDistanceToGroundDetection) {
                                    mainGroundHit = temporalHit;

                                    groundDetected = true;
                                }
                            }
                        }
                    }
                }

                if (groundDetected) {
                    //get the angle of the current surface
                    hitAngle = Vector3.Angle (currentNormal, mainGroundHit.normal);

                    canSetGroundState = false;

                    //check max angle between the ground and the player if he is on zero gravity or free floating mode
                    if ((freeFloatingModeOn || zeroGravityModeOn) && !checkOnGroundStatePausedFFOrZG && !previouslyOnGround) {
                        if (useMaxAngleToCheckOnGroundStateZGFF) {
                            if (hitAngle < maxAngleToChekOnGroundStateZGFF) {
                                canSetGroundState = true;
                            }
                        } else {
                            canSetGroundState = true;
                        }
                    } else {
                        canSetGroundState = true;
                    }

                    if (zeroGravityModeOn && ignoreSetCheckOnGrundStatePausedFFOrZGStateActive) {
                        canSetGroundState = true;
                    }

                    if (canSetGroundState) {
                        setOnGroundState (true);
                    }

                    jump = false;

                    slowingFall = false;

                    slowFallExternallyActive = false;

                    groundPoint = mainGroundHit.point;

                    currentSurfaceBelowPlayer = mainGroundHit.collider.transform;

                    currentSurfaceBelowPlayerLocated = true;

                    currentNumberOfDash = 0;

                    distanceToGround = mainGroundHit.distance;

                    if (!useSphereRaycastForGroundDetection) {
                        distanceToGround -= 1;
                    }

                    mainHitPoint = groundPoint;
                    mainHitAngle = hitAngle;

                    if (checkIfSurfaceBelowIsTerrainEnabled) {
                        if (previousSurfaceBelowPlayer != currentSurfaceBelowPlayer) {
                            previousSurfaceBelowPlayer = currentSurfaceBelowPlayer;

                            terrainDetected = previousSurfaceBelowPlayer.GetComponent<Terrain> ();
                        }
                    }
                } else {
                    if (currentSurfaceBelowPlayerLocated) {
                        currentSurfaceBelowPlayer = null;

                        currentSurfaceBelowPlayerLocated = false;

                        previousSurfaceBelowPlayer = null;

                        terrainDetected = false;
                    }
                }

                if (useMaxWalkSurfaceAngle && !stairStepDetected) {
                    if (playerOnGround && hitAngle > maxWalkSurfaceAngle && (!useMaxDistanceToCheckSurfaceAngle || distanceToGround > maxDistanceToCheckSurfaceAngle)) {
                        setOnGroundState (false);
                    }
                }

                //check if the player has to adhere to the surface or not
                adhereToGround = false;

                if (playerOnGround) {

                    movingOnSlopeUp = false;
                    movingOnSlopeDown = false;

                    //use a raycast to check the distance to the ground in front of the player, 
                    //if the ray doesn't find a collider, it means that the player is going down in an inclinated surface, so adhere to that surface
                    bool hitInFront = false;

                    slopeFound = false;
                    stairsFound = false;

                    //assign the current input direction, taking into account if the player is moving in free mode or in locked camera with movement relative to the camera direction
                    if (lockedCameraActive && useRelativeMovementToLockedCamera && !usedByAI) {
                        currentAdherenceInput = lockedCameraMoveInput;
                    } else {
                        currentAdherenceInput = currentMoveInput;
                    }

                    rayPosition = mainRaycastPosition + (maxRayDistanceRange * currentAdherenceInput);

                    //Debug.DrawRay (rayPosition - currentPlayerUp, currentPlayerUp * 10, Color.yellow);

                    raycastDistance = rayDistance - 0.2f;

                    if (walkSpeed <= half) {
                        raycastDistance = rayDistance;
                    }

                    float hitAngleInFront = 0;

                    if (Physics.Raycast (rayPosition, -currentPlayerUp, out mainGroundHit, raycastDistance, layer)) {
                        //Debug.DrawRay (rayPosition, -currentPlayerUp * raycastDistance, Color.red);
                        hitInFront = true;

                        hitAngleInFront = Vector3.Angle (currentNormal, mainGroundHit.normal);
                    }

                    bool ignoringSlopesFromInclination = false;

                    //check for slopes while aiming in third person or moving in first person
                    if (hitAngle != 0 || hitAngleInFront != 0) {

                        checkForSlopes = false;

                        if (firstPersonActive) {
                            rayPosition = mainRaycastPosition + (maxSlopeRayDistance * currentAdherenceInput);

                            checkForSlopes = true;
                        } else {
                            if (!playerCameraManager.is2_5ViewActive ()) {
                                if ((playerIsAiming && lookInCameraDirectionOnFreeFireActive) || lookInCameraDirectionActive) {

                                    rayPosition = mainRaycastPosition + (maxSlopeRayDistance * currentAdherenceInput);

                                    checkForSlopes = true;
                                } else {
                                    if (useTankControls) {
                                        if (!isPlayerUsingVerticalInput ()) {
                                            currentAdherenceInput = vector3Zero;
                                        }

                                        rayPosition = mainRaycastPosition + (maxSlopeRayDistance * currentAdherenceInput);
                                    } else {
                                        rayPosition = mainRaycastPosition + (maxSlopeRayDistance * playerTransformForward);
                                    }

                                    checkForSlopes = true;
                                }
                            } else {
                                if (playerIsAiming) {
                                    rayPosition = mainRaycastPosition + (maxSlopeRayDistance * currentAdherenceInput);
                                } else {
                                    rayPosition = mainRaycastPosition + (maxSlopeRayDistance * playerTransformForward);
                                }

                                checkForSlopes = true;
                            }
                        }

                        if (useMaxSlopeInclination) {
                            if (hitAngle < minSlopeInclination || hitAngle > maxSlopeInclination ||
                                hitAngleInFront < minSlopeInclination || hitAngleInFront > maxSlopeInclination) {
                                checkForSlopes = false;

                                ignoringSlopesFromInclination = true;
                            }
                        }

                        if (checkForSlopes) {
                            if (showGizmo) {
                                Debug.DrawRay (rayPosition + 4 * Vector3Up, (rayDistance + half) * (-currentPlayerUp) - 4 * Vector3Up, Color.green, 3);
                            }

                            if (Physics.Raycast (rayPosition, -currentPlayerUp, out adherenceHit, (rayDistance + half), layer)) {

                                hitPointVerticalPosition = playerTransform.InverseTransformPoint (adherenceHit.point).y;

                                mainHitPointVerticalPosition = playerTransform.InverseTransformPoint (mainHitPoint).y;

                                adhereToGround = true;

                                groundPoint = adherenceHit.point;

                                if (usingAnimator && !useFirstPersonPhysicsInThirdPersonActive) {
                                    if (hitPointVerticalPosition >= mainHitPointVerticalPosition) {
                                        currentGroundAdherence = slopesGroundAdherenceUp;

                                        movingOnSlopeUp = true;
                                    } else {
                                        currentGroundAdherence = slopesGroundAdherenceDown;

                                        movingOnSlopeDown = true;
                                    }
                                } else {
                                    if (hitPointVerticalPosition >= mainHitPointVerticalPosition) {
                                        currentGroundAdherence = noAnimatorSlopesGroundAdherenceUp;

                                        movingOnSlopeUp = true;
                                    } else {
                                        currentGroundAdherence = noAnimatorSlopesGroundAdherenceDown;

                                        movingOnSlopeDown = true;
                                    }
                                }

                                slopeFound = true;

                                //Debug.DrawRay (rayPosition + Vector3Up * 4, -currentPlayerUp * (rayDistance + half) - Vector3Up * 4, Color.green, 3);
                            } else {
                                //Debug.DrawRay (rayPosition + Vector3Up * 4, -currentPlayerUp * (rayDistance + half) - Vector3Up * 4, Color.yellow, 3);
                            }
                        }
                    }

                    //check for stairs below and in front of the player
                    stairsMoveInput = (1 / walkSpeed) * currentAdherenceInput;

                    rayPosition = mainRaycastPosition + stairsMoveInput;

                    if (Physics.Raycast (rayPosition, -currentPlayerUp, out adherenceHit, raycastDistance, layer)) {

                        hitAngle = Vector3.Angle (currentNormal, adherenceHit.normal);

                        if (checkStairsWithInclination) {
                            if (!stairStepDetected) {
                                stairInclinationDetected = false;
                            }

                            if (hitAngle != 0 && hitAngle >= minStairInclination && hitAngle <= maxStairInclination) {
                                stairInclinationDetected = true;
                            }
                        }
                    }

                    bool canCheckStairsState = true;

                    if (terrainDetected) {
                        canCheckStairsState = false;
                    }

                    //walk on stairs
                    if (hitInFront && (hitAngle == 0 || stairInclinationDetected) && !ignoringSlopesFromInclination && canCheckStairsState) {

                        if (!slopeFound || mainHitAngle == 0 || (mainHitAngle >= minStairInclination && mainHitAngle <= maxStairInclination)) {
                            if (checkForStairAdherenceSystem) {
                                rayPosition = mainRaycastPosition + stairsMoveInput;

                                //Debug.DrawRay (rayPosition, -currentPlayerUp * raycastDistance, Color.black);

                                if (Physics.Raycast (rayPosition, -currentPlayerUp, out adherenceHit, raycastDistance, layer)) {
                                    currentDetectedSurface = adherenceHit.collider.gameObject;
                                } else {
                                    currentDetectedSurface = null;
                                }

                                if (currentTargetDetected != currentDetectedSurface) {

                                    stairAdherenceSystemDetected = false;

                                    currentTargetDetected = currentDetectedSurface;

                                    if (currentTargetDetected != null) {
                                        stairAdherenceSystem currentStairAdherenceSystem = currentTargetDetected.GetComponent<stairAdherenceSystem> ();

                                        if (currentStairAdherenceSystem != null) {
                                            if (currentStairAdherenceSystem.modifyStairsValuesOnPlayer) {
                                                currentStairAdherenceSystem.setStairsValuesOnPlayer (this);
                                            }

                                            stairAdherenceSystemDetected = true;
                                        } else {
                                            currentStairAdherenceSystem = currentSurfaceBelowPlayer.GetComponent<stairAdherenceSystem> ();

                                            if (currentStairAdherenceSystem != null) {
                                                if (currentStairAdherenceSystem.modifyStairsValuesOnPlayer) {
                                                    currentStairAdherenceSystem.setStairsValuesOnPlayer (this);
                                                }

                                                stairAdherenceSystemDetected = true;
                                            }
                                        }
                                    }
                                }
                            }

                            if (stairAdherenceSystemDetected) {
                                currentStairMaxValue = currentStairAdherenceSystemMaxValue;
                                currentStairMinValue = currentStairAdherenceSystemMinValue;
                                currentStairAdherenceValue = currentStairAdherenceSystemAdherenceValue;
                            } else {
                                currentStairMaxValue = stairsMaxValue;
                                currentStairMinValue = stairsMinValue;
                                currentStairAdherenceValue = stairsGroundAdherence;
                            }

                            rayPosition = mainRaycastPosition + (maxStairsRayDistance * stairsMoveInput);

                            Vector3 clampRayPosition = rayPosition - currentPlayerPosition;

                            clampRayPosition = Vector3.ClampMagnitude (clampRayPosition, 1.1f);

                            clampRayPosition = currentPlayerPosition + clampRayPosition;


                            //Debug.DrawRay (clampRayPosition, -currentPlayerUp, Color.grey, 20);

                            //MAYBE HERE GET THE LAST Y POSITION TO REACH AND USE THAT TO CHECK TEH DIFFERENCE WITH CURRENT PLAYER Y POSITION

                            //maybe add a vertical movement speed on slope up or down instead of forcing vertical position

                            if (Physics.Raycast (clampRayPosition, -currentPlayerUp, out adherenceHit, raycastDistance, layer)) {
                                if (!stairStepDetected) {

                                    float stepPosition = playerTransform.InverseTransformPoint (adherenceHit.point).y;

                                    if (stepPosition > 0) {
                                        upDistance = Mathf.Abs (Mathf.Abs (playerTransform.InverseTransformPoint (currentPlayerPosition).y) - Mathf.Abs (stepPosition));

                                        if (upDistance >= currentStairMinValue && upDistance <= currentStairMaxValue) {
                                            stairStepDetected = true;

                                            if (showDebugPrint) {
                                                print ("stair step detected");
                                            }
                                        }
                                    }
                                }

                                if (stairStepDetected) {
                                    if (showDebugPrint) {
                                        print ("climbing step");
                                    }

                                    adhereToGround = true;
                                    groundPoint = adherenceHit.point;

                                    if (showGizmo) {
                                        Debug.DrawLine (currentPlayerPosition + currentPlayerUp * 2, groundPoint, Color.grey, 2);
                                    }

                                    if (usingAnimator && !useFirstPersonPhysicsInThirdPersonActive) {
                                        currentGroundAdherence = currentStairAdherenceValue;

                                        if (walkSpeed < 1) {
                                            float extraValue = walkSpeed;

                                            extraValue += 0.3f;

                                            extraValue = Mathf.Clamp (extraValue, 0, 1);

                                            currentGroundAdherence *= extraValue;
                                        }
                                    } else {
                                        currentGroundAdherence = noAnimatorStairsGroundAdherence;
                                    }

                                    stairsFound = true;

                                    if (Mathf.Abs (playerTransform.InverseTransformPoint (adherenceHit.point).y) < 0.02f) {
                                        stairStepDetected = false;

                                        if (showDebugPrint) {
                                            print ("new step reached");
                                        }
                                    }
                                }
                            } else {
                                stairStepDetected = false;
                            }
                        }
                    }

                    if (!slopeFound && !stairsFound) {
                        adhereToGround = true;
                        currentGroundAdherence = regularGroundAdherence;
                    }

                    //if the player is not moving and the angle of the surface is 0, adhere to it, so if the player jumps for example, the player is correctly
                    //placed in the ground, with out a gap between the player's collider and the ground
                    bool pressingInput = isPlayerUsingInput ();

                    if (useTankControls) {
                        pressingInput = isPlayerUsingVerticalInput ();
                    }

                    if (!pressingInput && hitAngle == 0) {
                        adhereToGround = true;
                        currentGroundAdherence = regularGroundAdherence;

                        if (stairStepDetected) {
                            groundPoint = currentPlayerPosition;
                        }
                    } else if (!pressingInput || (movingOnSlopeUp && hitAngle != 0 && !isPlayerMoving (0.99f)) || (movingOnSlopeDown && hitAngle != 0 && !isPlayerMoving (0.99f))) {
                        adhereToGround = true;

                        bool sphereCastFound = false;

                        if (Physics.SphereCast (currentPlayerPosition + currentPlayerUp, originalRadius, -currentPlayerUp, out adherenceHit, 0.75f, layer)) {

                            if (showGizmo) {
                                Debug.DrawLine (currentPlayerPosition + currentPlayerUp, adherenceHit.point, Color.black);
                            }

                            sphereCastFound = true;
                        }

                        if (movingOnSlopeUp) {
                            if (distanceToGround < 0.05f || sphereCastFound) {
                                groundPoint = currentPlayerPosition;
                            } else {
                                groundPoint = mainHitPoint;
                            }
                        } else if (movingOnSlopeDown) {
                            if (pressingInput) {

                                if (!isPlayerMoving (0.25f) && (sphereCastFound || distanceToGround < 0.05f)) {
                                    groundPoint = currentPlayerPosition;
                                }
                            } else {
                                if (distanceToGround < 0.05f || sphereCastFound) {
                                    if (!isPlayerMoving (0.05f)) {
                                        groundPoint = currentPlayerPosition;
                                    }
                                } else {
                                    groundPoint = mainHitPoint;
                                }
                            }
                        }
                    }
                }

                //ignore to adhere to the ground below if the player is rotating to a different gravity direction
                if (lockedCameraActive) {
                    if (isPlayerMovingOn2_5dWorld ()) {
                        if (characterRotatingToSurface) {
                            adhereToGround = false;
                        }
                    }
                }

                //the player has to ahdere to the current surface, so
                if (adhereToGround) {
                    //move towards the surface the player's rigidbody 

                    mainRigidbody.position = Vector3.MoveTowards (mainRigidbody.position, groundPoint, currentFixedUpdateDeltaTime * currentGroundAdherence);

                    if (showGizmo) {
                        Debug.DrawLine (mainRigidbody.position, groundPoint + 4 * playerTransform.up, Color.black);
                    }
                }
                //END OF CHECK ON GROUND STATE

                if (showGizmo) {
                    Debug.DrawLine (currentPlayerPosition + (4 * currentPlayerUp), adherenceHit.point, Color.black);
                }

                if (playerOnGround) {
                    if (useAutoCrouch) {
                        updateAutoCrouchCheck ();
                    }
                }
            }

            if (!playerOnGround) {
                lastTimeAir = Time.time;
            }
        } else {
            setOnGroundState (false);
        }
    }
}