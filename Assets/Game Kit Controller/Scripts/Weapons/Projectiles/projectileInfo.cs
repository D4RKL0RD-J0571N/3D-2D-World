using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using GameKitController.Audio;

[System.Serializable]
public class projectileInfo
{
    public bool isHommingProjectile;
    public bool isSeeker;
    public bool targetOnScreenForSeeker = true;
    public float waitTimeToSearchTarget;

    public bool useRaycastCheckingOnRigidbody;

    public float customRaycastCheckingRate;

    public float customRaycastCheckingDistance = 0.1f;

    public bool useRayCastShoot;

    public bool useRaycastShootDelay;
    public float raycastShootDelay;
    public bool getDelayWithDistance;
    public float delayWithDistanceSpeed;
    public float maxDelayWithDistance;

    public bool useFakeProjectileTrails;

    public bool ignoreUseTrailRenderer;

    public float projectileDamage;
    public float projectileSpeed;
    public float impactForceApplied;
    public ForceMode forceMode;
    public bool applyImpactForceToVehicles;
    public float impactForceToVehiclesMultiplier;

    public bool checkObjectsWithMultipleDamageReceiversEnabled = true;

    public float forceMassMultiplier = 1;

    public bool projectileWithAbility;
    public AudioClip impactSoundEffect;
    public AudioElement impactAudioElement;
    public GameObject scorch;
    public GameObject owner;
    public GameObject projectileParticles;
    public GameObject impactParticles;

    public bool isExplosive;
    public bool isImplosive;
    public float explosionForce;
    public float explosionRadius;
    public bool useExplosionDelay;
    public float explosionDelay;
    public float explosionDamage;
    public bool pushCharacters;
    public bool canDamageProjectileOwner;
    public bool applyExplosionForceToVehicles;
    public float explosionForceToVehiclesMultiplier;

    public bool searchClosestWeakSpot;

    public bool killInOneShot;

    public bool useDisableTimer;
    public float noImpactDisableTimer;
    public float impactDisableTimer;

    public bool useCustomIgnoreTags;
    public List<string> customTagsToIgnoreList = new List<string> ();

    public LayerMask targetToDamageLayer;

    public LayerMask targetForScorchLayer;

    public float scorchRayCastDistance;

    public int impactDecalIndex;

    public bool launchProjectile;

    public bool adhereToSurface;
    public bool adhereToLimbs;

    public bool ignoreSetProjectilePositionOnImpact;

    public bool useGravityOnLaunch;
    public bool useGraivtyOnImpact;

    public bool breakThroughObjects;

    public bool canBreakThroughArmorSurface;

    public int breakThroughArmorSurfacePriorityValue = -1;

    public bool infiniteNumberOfImpacts;
    public int numberOfImpacts;
    public bool canDamageSameObjectMultipleTimes;
    public Vector3 forwardDirection;
    public bool ignoreNewRotationOnProjectileImpact;

    public bool stopBreakThroughObjectsOnLayer;
    public LayerMask layerToStopBreahtThroughObjects;

    public bool damageTargetOverTime;
    public float damageOverTimeDelay;
    public float damageOverTimeDuration;
    public float damageOverTimeAmount;
    public float damageOverTimeRate;
    public bool damageOverTimeToDeath;

    public bool removeDamageOverTimeState;

    public bool sedateCharacters;
    public float sedateDelay;
    public bool useWeakSpotToReduceDelay;
    public bool sedateUntilReceiveDamage;
    public float sedateDuration;

    public bool pushCharacter;
    public float pushCharacterForce;
    public float pushCharacterRagdollForce;

    public bool setProjectileMeshRotationToFireRotation;

    public bool useRemoteEventOnObjectsFound;
    public List<string> remoteEventNameList = new List<string> ();

    public bool useRemoteEventOnObjectsFoundOnExplosion;
    public string remoteEventNameOnExplosion;

    public bool ignoreShield;

    public bool canActivateReactionSystemTemporally;

    public int damageReactionID = -1;

    public int damageTypeID = -1;

    public bool damageCanBeBlocked = true;

    public bool projectilesPoolEnabled;

    public int maxAmountOfPoolElementsOnWeapon;

    public bool allowDamageForProjectileOwner;

    public bool projectileCanBeDeflected = true;


    public bool sliceObjectsDetected;
    public LayerMask layerToSlice;
    public bool useBodyPartsSliceList;
    public List<string> bodyPartsSliceList = new List<string> ();
    public float maxDistanceToBodyPart;
    public bool randomSliceDirection;
    public bool showSliceGizmo;

    public bool activateRigidbodiesOnNewObjects = true;

    public bool useGeneralProbabilitySliceObjects;
    [Range (0, 100)] public float generalProbabilitySliceObjects;


    public void InitializeAudioElements ()
    {
        if (impactSoundEffect != null) {
            impactAudioElement.clip = impactSoundEffect;
        }
    }

    public projectileInfo ()
    {

    }

    public projectileInfo (projectileInfo newInfo)
    {
        isHommingProjectile = newInfo.isHommingProjectile;
        isSeeker = newInfo.isSeeker;
        targetOnScreenForSeeker = newInfo.targetOnScreenForSeeker;
        waitTimeToSearchTarget = newInfo.waitTimeToSearchTarget;

        useRaycastCheckingOnRigidbody = newInfo.useRaycastCheckingOnRigidbody;

        customRaycastCheckingRate = newInfo.customRaycastCheckingRate;

        customRaycastCheckingDistance = newInfo.customRaycastCheckingDistance;

        useRayCastShoot = newInfo.useRayCastShoot;

        useRaycastShootDelay = newInfo.useRaycastShootDelay;
        raycastShootDelay = newInfo.raycastShootDelay;
        getDelayWithDistance = newInfo.getDelayWithDistance;
        delayWithDistanceSpeed = newInfo.delayWithDistanceSpeed;
        maxDelayWithDistance = newInfo.maxDelayWithDistance;

        useFakeProjectileTrails = newInfo.useFakeProjectileTrails;

        ignoreUseTrailRenderer = newInfo.ignoreUseTrailRenderer;

        projectileDamage = newInfo.projectileDamage;
        projectileSpeed = newInfo.projectileSpeed;
        impactForceApplied = newInfo.impactForceApplied;
        forceMode = newInfo.forceMode;
        applyImpactForceToVehicles = newInfo.applyImpactForceToVehicles;
        impactForceToVehiclesMultiplier = newInfo.impactForceToVehiclesMultiplier;

        forceMassMultiplier = newInfo.forceMassMultiplier;

        checkObjectsWithMultipleDamageReceiversEnabled = newInfo.checkObjectsWithMultipleDamageReceiversEnabled;

        projectileWithAbility = newInfo.projectileWithAbility;
        impactSoundEffect = newInfo.impactSoundEffect;
        impactAudioElement = newInfo.impactAudioElement;
        scorch = newInfo.scorch;
        owner = newInfo.owner;
        projectileParticles = newInfo.projectileParticles;
        impactParticles = newInfo.impactParticles;

        isExplosive = newInfo.isExplosive;
        isImplosive = newInfo.isImplosive;
        explosionForce = newInfo.explosionForce;
        explosionRadius = newInfo.explosionRadius;
        useExplosionDelay = newInfo.useExplosionDelay;
        explosionDelay = newInfo.explosionDelay;
        explosionDamage = newInfo.explosionDamage;
        pushCharacters = newInfo.pushCharacters;
        canDamageProjectileOwner = newInfo.canDamageProjectileOwner;
        applyExplosionForceToVehicles = newInfo.applyExplosionForceToVehicles;
        explosionForceToVehiclesMultiplier = newInfo.explosionForceToVehiclesMultiplier;

        searchClosestWeakSpot = newInfo.searchClosestWeakSpot;

        killInOneShot = newInfo.killInOneShot;

        useDisableTimer = newInfo.useDisableTimer;
        noImpactDisableTimer = newInfo.noImpactDisableTimer;
        impactDisableTimer = newInfo.impactDisableTimer;

        useCustomIgnoreTags = newInfo.useCustomIgnoreTags;
        customTagsToIgnoreList = newInfo.customTagsToIgnoreList;

        targetToDamageLayer = newInfo.targetToDamageLayer;

        targetForScorchLayer = newInfo.targetForScorchLayer;

        scorchRayCastDistance = newInfo.scorchRayCastDistance;

        impactDecalIndex = newInfo.impactDecalIndex;

        launchProjectile = newInfo.launchProjectile;

        adhereToSurface = newInfo.adhereToSurface;
        adhereToLimbs = newInfo.adhereToLimbs;

        ignoreSetProjectilePositionOnImpact = newInfo.ignoreSetProjectilePositionOnImpact;

        useGravityOnLaunch = newInfo.useGravityOnLaunch;
        useGraivtyOnImpact = newInfo.useGraivtyOnImpact;

        breakThroughObjects = newInfo.breakThroughObjects;

        canBreakThroughArmorSurface = newInfo.canBreakThroughArmorSurface;

        breakThroughArmorSurfacePriorityValue = newInfo.breakThroughArmorSurfacePriorityValue;

        infiniteNumberOfImpacts = newInfo.infiniteNumberOfImpacts;
        numberOfImpacts = newInfo.numberOfImpacts;
        canDamageSameObjectMultipleTimes = newInfo.canDamageSameObjectMultipleTimes;
        forwardDirection = newInfo.forwardDirection;
        ignoreNewRotationOnProjectileImpact = newInfo.ignoreNewRotationOnProjectileImpact;

        stopBreakThroughObjectsOnLayer = newInfo.stopBreakThroughObjectsOnLayer;
        layerToStopBreahtThroughObjects = newInfo.layerToStopBreahtThroughObjects;

        damageTargetOverTime = newInfo.damageTargetOverTime;
        damageOverTimeDelay = newInfo.damageOverTimeDelay;
        damageOverTimeDuration = newInfo.damageOverTimeDuration;
        damageOverTimeAmount = newInfo.damageOverTimeAmount;
        damageOverTimeRate = newInfo.damageOverTimeRate;
        damageOverTimeToDeath = newInfo.damageOverTimeToDeath;

        removeDamageOverTimeState = newInfo.removeDamageOverTimeState;

        sedateCharacters = newInfo.sedateCharacters;
        sedateDelay = newInfo.sedateDelay;
        useWeakSpotToReduceDelay = newInfo.useWeakSpotToReduceDelay;
        sedateUntilReceiveDamage = newInfo.sedateUntilReceiveDamage;
        sedateDuration = newInfo.sedateDuration;

        pushCharacter = newInfo.pushCharacter;
        pushCharacterForce = newInfo.pushCharacterForce;
        pushCharacterRagdollForce = newInfo.pushCharacterRagdollForce;

        setProjectileMeshRotationToFireRotation = newInfo.setProjectileMeshRotationToFireRotation;

        useRemoteEventOnObjectsFound = newInfo.useRemoteEventOnObjectsFound;
        remoteEventNameList = newInfo.remoteEventNameList;

        useRemoteEventOnObjectsFoundOnExplosion = newInfo.useRemoteEventOnObjectsFoundOnExplosion;
        remoteEventNameOnExplosion = newInfo.remoteEventNameOnExplosion;

        ignoreShield = newInfo.ignoreShield;

        canActivateReactionSystemTemporally = newInfo.canActivateReactionSystemTemporally;

        damageReactionID = newInfo.damageReactionID;

        damageTypeID = newInfo.damageTypeID;

        damageCanBeBlocked = newInfo.damageCanBeBlocked;

        projectilesPoolEnabled = newInfo.projectilesPoolEnabled;

        maxAmountOfPoolElementsOnWeapon = newInfo.maxAmountOfPoolElementsOnWeapon;

        allowDamageForProjectileOwner = newInfo.allowDamageForProjectileOwner;

        projectileCanBeDeflected = newInfo.projectileCanBeDeflected;

        sliceObjectsDetected = newInfo.sliceObjectsDetected;
        layerToSlice = newInfo.layerToSlice;
        useBodyPartsSliceList = newInfo.useBodyPartsSliceList;
        bodyPartsSliceList = newInfo.bodyPartsSliceList;
        maxDistanceToBodyPart = newInfo.maxDistanceToBodyPart;
        randomSliceDirection = newInfo.randomSliceDirection;
        showSliceGizmo = newInfo.showSliceGizmo;

        activateRigidbodiesOnNewObjects = newInfo.activateRigidbodiesOnNewObjects;

        useGeneralProbabilitySliceObjects = newInfo.useGeneralProbabilitySliceObjects;
        generalProbabilitySliceObjects = newInfo.generalProbabilitySliceObjects;
    }
}