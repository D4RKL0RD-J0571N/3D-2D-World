using System.Collections;
using System.Collections.Generic;
using GameKitController.Audio;
using UnityEngine;

public class plasmaCutterProjectile : projectileSystem
{
    [Header ("Main Settings")]
    [Space]

    public Material defaultSliceMaterial;

    public float forceToApplyToCutPart;
    public ForceMode forceMode;
    public float forceRadius;

    public bool cutMultipleTimesActive;

    public bool useCutLimit;
    public int cutLimit;

    public bool activateRigidbodiesOnNewObjects = true;

    [Space]
    [Header ("Shatter Object Settings")]
    [Space]

    public bool shatterObjectActive;
    public int shatterAmount;

    public bool applyForceOnShatter;

    public bool addSliceComponentToPieces = true;

    [Space]
    [Header ("Damage Settings")]
    [Space]

    public bool activateDamageOnSlice;
    public float damageAmountToApplyOnSlice;
    public bool ignoreShieldOnDamage = true;
    public bool canActivateReactionSystemTemporally;
    public int damageReactionID = -1;

    public int damageTypeID = -1;

    [Space]
    [Header ("Physics Settings")]
    [Space]

    public bool updateLastObjectSpeed;

    public bool applyForcesOnObjectsDetected;
    public float addForceMultiplier;
    public bool applyImpactForceToVehicles;
    public float impactForceToVehiclesMultiplier;

    public bool checkObjectLayerAndTagToApplyForces;

    public LayerMask targetToApplyForceLayer;
    public List<string> tagetToApplyForceTagList = new List<string> ();

    [Space]
    [Header ("Other Settings")]
    [Space]

    public float minDelayToSliceSameObject = 0.01f;

    public Transform cutPositionTransform;

    public Vector3 cutOverlapBoxSize = new Vector3 (5, 0.1f, 5);

    public bool addRigidbodyToBothSlicedObjects;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public List<GameObject> objectsDetected = new List<GameObject> ();

    [Space]
    [Header ("Gizmo Settings")]
    [Space]

    public bool showGizmo;
    public Color gizmoColor = Color.red;

    [Space]
    [Header ("Components")]
    [Space]

    public Transform cutDirectionTransform;
    public Transform planeDefiner1;
    public Transform planeDefiner2;
    public Transform planeDefiner3;

    surfaceToSlice currentSurfaceToSlice;

    int currentNumberOfCuts;

    List<GameObject> objectsToShatter;
    bool objectShattered = false;

    public void checkObjectDetected (Collider col)
    {
        if (canActivateEffect (col)) {

            objectShattered = false;

            if (currentProjectileInfo.impactAudioElement != null) {
                currentProjectileInfo.impactAudioElement.audioSource = GetComponent<AudioSource> ();
                AudioPlayer.PlayOneShot (currentProjectileInfo.impactAudioElement, gameObject);
            }

            Collider objectCollider = col.GetComponent<Collider> ();

            objectToDamage = objectCollider.gameObject;

            processObject (objectToDamage, objectCollider, cutPositionTransform.position);

            if (!cutMultipleTimesActive || currentNumberOfCuts >= cutLimit) {
                setProjectileUsedState (true);

                mainRigidbody.isKinematic = true;

                projectilePaused = true;

                destroyProjectile ();
            }
        }
    }

    public void processObject (GameObject objectToCheck, Collider objectCollider, Vector3 slicePosition)
    {
        if (cutMultipleTimesActive) {
            if (objectsDetected.Contains (objectToCheck)) {
                return;
            }
        }

        currentSurfaceToSlice = objectToCheck.GetComponent<surfaceToSlice> ();

        if (currentSurfaceToSlice == null) {
            currentSurfaceToSlice = sliceSystemUtils.getSurfaceToSlice (objectToCheck);
        }

        bool isVehicle = applyDamage.isVehicle (objectToCheck);

        if (isVehicle) {
            GameObject currentVehicle = applyDamage.getVehicle (objectToCheck);

            if (currentVehicle != null) {
                currentSurfaceToSlice = currentVehicle.GetComponent<surfaceToSlice> ();

                if (currentSurfaceToSlice != null) {
                    objectToCheck = currentVehicle;
                }
            }
        }

        bool objectIsSliceSurfaceDisabled = false;

        bool objectCanBeSliced = false;

        if (showDebugPrint) {
            print ("processing object " + objectToCheck.name);
        }

        bool ignoreRegularDamageIfCutSurfaceNotEnabled = false;

        bool isIgnoreDamageIfSliceNotActivatedActive = false;

        if (currentSurfaceToSlice != null) {
            bool isCutSurfaceEnabled = currentSurfaceToSlice.isCutSurfaceEnabled ();

            bool sliceCanBeActivated = currentSurfaceToSlice.sliceCanBeActivated (minDelayToSliceSameObject);

            bool checkSliceDirectionResult = currentSurfaceToSlice.checkSliceDirectionResult (cutPositionTransform.right, cutPositionTransform.up);

            isIgnoreDamageIfSliceNotActivatedActive = currentSurfaceToSlice.isIgnoreDamageIfSliceNotActivatedActive ();

            if (showDebugPrint) {
                print ("Surface cut enabled state " + isCutSurfaceEnabled +
                " can activate slice " + sliceCanBeActivated);
            }

            if (isCutSurfaceEnabled && sliceCanBeActivated && checkSliceDirectionResult) {
                Material crossSectionMaterial = currentSurfaceToSlice.getCrossSectionMaterial ();

                if (crossSectionMaterial == null) {
                    crossSectionMaterial = defaultSliceMaterial;
                }

                sliceCurrentObject (objectToCheck, objectCollider, crossSectionMaterial, slicePosition);

                objectCanBeSliced = true;
            } else {
                if (isVehicle && isCutSurfaceEnabled) {
                    objectCanBeSliced = true;
                }
            }

            if (!isCutSurfaceEnabled) {
                objectIsSliceSurfaceDisabled = true;

                ignoreRegularDamageIfCutSurfaceNotEnabled = currentSurfaceToSlice.isIgnoreRegularDamageIfCutSurfaceNotEnabled ();
            }
        } else {
            if (showDebugPrint) {
                print ("Surface to slice not found on " + objectToCheck.name);
            }
        }

        if (!objectCanBeSliced && !ignoreRegularDamageIfCutSurfaceNotEnabled) {
            if (activateDamageOnSlice && !isIgnoreDamageIfSliceNotActivatedActive) {
                Vector3 damagePosition = cutPositionTransform.position;

                if (applyDamage.checkIfDead (objectToCheck)) {
                    damagePosition = objectToCheck.transform.position;
                }

                applyDamage.checkCanBeDamaged (gameObject, objectToCheck, damageAmountToApplyOnSlice, -cutPositionTransform.forward, damagePosition,
                    currentProjectileInfo.owner, true, true, ignoreShieldOnDamage, false, true, canActivateReactionSystemTemporally,
                    damageReactionID, damageTypeID);
            }

            if (applyForcesOnObjectsDetected && !objectIsSliceSurfaceDisabled) {
                if (!checkObjectLayerAndTagToApplyForces ||
                    ((1 << objectToCheck.layer & targetToApplyForceLayer.value) == 1 << objectToCheck.layer && tagetToApplyForceTagList.Contains (objectToCheck.tag))) {
                    checkForceToApplyOnObject (objectToCheck);
                }
            }
        }
    }

    public void sliceCurrentObject (GameObject objectToCheck, Collider objectCollider, Material crossSectionMaterial, Vector3 slicePosition)
    {
        if (useCutLimit) {
            currentNumberOfCuts++;
        }

        // slice the provided object using the transforms of this object
        if (currentSurfaceToSlice.isObjectCharacter ()) {
            //			print ("character found " + objectToCheck.name + " is dead " + applyDamage.checkIfDead (objectToCheck));

            if (applyDamage.getCharacterOrVehicle (objectToCheck) != null && !applyDamage.checkIfDead (objectToCheck)) {
                processCharacter (objectToCheck);

                return;
            }

            Rigidbody mainObject = objectToCheck.GetComponent<Rigidbody> ();

            bool mainObjectHasRigidbody = mainObject != null;

            Vector3 lastSpeed = Vector3.zero;

            if (mainObjectHasRigidbody) {
                lastSpeed = mainObject.linearVelocity;
            }

            currentSurfaceToSlice.checkEventBeforeSlice ();

            currentSurfaceToSlice.checkEventsOnIgnoreDestroyOriginalObject ();

            currentSurfaceToSlice.getMainSimpleSliceSystem ().activateSlice (objectCollider, positionInWorldSpace,
                normalInWorldSpace, slicePosition, updateLastObjectSpeed, lastSpeed);

            currentSurfaceToSlice.checkEventOnCut ();

            currentSurfaceToSlice.checkTimeBulletOnCut ();
        } else {
            if (shatterObjectActive) {
                shatterObject (objectToCheck, crossSectionMaterial);

                return;
            }

            // slice the provided object using the transforms of this object
            bool objectSliced = false;

            GameObject object1 = null;
            GameObject object2 = null;

            currentSurfaceToSlice.checkEventBeforeSlice ();

            currentSurfaceToSlice.checkEventsOnIgnoreDestroyOriginalObject ();

            objectToCheck = currentSurfaceToSlice.getMainSurfaceToSlice ();

            // slice the provided objectToCheck	ect using the transforms of this object
            sliceSystemUtils.sliceObject (transform.position, objectToCheck, cutDirectionTransform.up, crossSectionMaterial, ref objectSliced, ref object1, ref object2);

            Vector3 objectPosition = objectToCheck.transform.position;
            Quaternion objectRotation = objectToCheck.transform.rotation;

            Transform objectParent = objectToCheck.transform.parent;

            if (objectSliced) {
                if (currentSurfaceToSlice.useParticlesOnSlice) {
                    Quaternion particlesRotation = Quaternion.LookRotation (positionInWorldSpace);

                    Instantiate (currentSurfaceToSlice.particlesOnSlicePrefab, slicePosition, particlesRotation);
                }

                currentSurfaceToSlice.checkEventOnCut ();

                currentSurfaceToSlice.checkTimeBulletOnCut ();

                Rigidbody mainObject = objectToCheck.GetComponent<Rigidbody> ();
                bool mainObjectHasRigidbody = mainObject != null;

                object1.transform.position = objectPosition;
                object1.transform.rotation = objectRotation;

                object2.transform.position = objectPosition;
                object2.transform.rotation = objectRotation;

                if (objectParent != null) {
                    object1.transform.SetParent (objectParent);
                    object2.transform.SetParent (objectParent);
                }

                surfaceToSlice newSurfaceToSlice1 = object1.AddComponent<surfaceToSlice> ();
                surfaceToSlice newSurfaceToSlice2 = object2.AddComponent<surfaceToSlice> ();

                currentSurfaceToSlice.copySurfaceInfo (newSurfaceToSlice1);
                currentSurfaceToSlice.copySurfaceInfo (newSurfaceToSlice2);

                newSurfaceToSlice1.checkDestroySlicedPartsAfterDelay ();
                newSurfaceToSlice2.checkDestroySlicedPartsAfterDelay ();

                float currentForceToApply = forceToApplyToCutPart;

                ForceMode currentForceMode = forceMode;

                if (currentSurfaceToSlice.useCustomForceMode) {
                    currentForceMode = currentSurfaceToSlice.customForceMode;
                }

                if (mainObjectHasRigidbody || activateRigidbodiesOnNewObjects) {

                    if (currentSurfaceToSlice.useCustomForceAmount) {
                        currentForceToApply = currentSurfaceToSlice.customForceAmount;
                    }

                    Rigidbody object1Rigidbody = object1.AddComponent<Rigidbody> ();

                    Rigidbody object2Rigidbody = object2.AddComponent<Rigidbody> ();

                    object2Rigidbody.AddExplosionForce (currentForceToApply, transform.position, 10, 1, currentForceMode);

                    object1Rigidbody.AddExplosionForce (currentForceToApply, transform.position, 10, 1, currentForceMode);
                } else {
                    if (currentSurfaceToSlice.useCustomForceAmount) {
                        currentForceToApply = currentSurfaceToSlice.customForceAmount;
                    }

                    bool addRigidbodyToObject1 = false;
                    bool addRigidbodyToObject2 = false;

                    if (addRigidbodyToBothSlicedObjects) {
                        addRigidbodyToObject1 = true;
                        addRigidbodyToObject2 = true;
                    } else {
                        float distance1 = GKC_Utils.distance (objectToCheck.transform.position, object1.transform.position);
                        float distance2 = GKC_Utils.distance (objectToCheck.transform.position, object2.transform.position);

                        if (distance1 < distance2) {
                            addRigidbodyToObject1 = true;
                        } else {
                            addRigidbodyToObject2 = true;
                        }
                    }

                    if (addRigidbodyToObject1) {
                        Rigidbody object2Rigidbody = object2.AddComponent<Rigidbody> ();

                        object2Rigidbody.AddExplosionForce (currentForceToApply, transform.position, 10, 1, currentForceMode);
                    }

                    if (addRigidbodyToObject2) {
                        Rigidbody object1Rigidbody = object1.AddComponent<Rigidbody> ();

                        object1Rigidbody.AddExplosionForce (currentForceToApply, transform.position, 10, 1, currentForceMode);
                    }
                }

                if (currentSurfaceToSlice.useBoxCollider) {
                    object1.AddComponent<BoxCollider> ();
                    object2.AddComponent<BoxCollider> ();
                } else {
                    MeshCollider object1Collider = object1.AddComponent<MeshCollider> ();
                    MeshCollider object2Collider = object2.AddComponent<MeshCollider> ();

                    object1Collider.convex = true;
                    object2Collider.convex = true;
                }

                if (currentSurfaceToSlice.setNewLayerOnCut) {
                    object1.layer = LayerMask.NameToLayer (currentSurfaceToSlice.newLayerOnCut);
                    object2.layer = LayerMask.NameToLayer (currentSurfaceToSlice.newLayerOnCut);
                }

                if (currentSurfaceToSlice.setNewTagOnCut) {
                    object1.tag = currentSurfaceToSlice.newTagOnCut;
                    object2.tag = currentSurfaceToSlice.newTagOnCut;
                }

                if (cutMultipleTimesActive) {
                    if (!objectsDetected.Contains (object1)) {
                        objectsDetected.Add (object1);
                    }

                    if (!objectsDetected.Contains (object2)) {
                        objectsDetected.Add (object2);
                    }
                }

                objectToCheck.SetActive (false);

                if (currentSurfaceToSlice.useEventToSendNewSlicedParts) {
                    currentSurfaceToSlice.eventToSendNewSlicedParts.Invoke (object1);
                    currentSurfaceToSlice.eventToSendNewSlicedParts.Invoke (object2);
                }
            }
        }
    }

    void processCharacter (GameObject currentCharacter)
    {
        StartCoroutine (processCharacterCoroutine (currentCharacter));
    }

    List<Collider> colliders = new List<Collider> ();

    IEnumerator processCharacterCoroutine (GameObject currentCharacter)
    {
        applyDamage.pushCharacterWithoutForceAndPauseGetUp (currentCharacter);

        Vector3 slicePosition = cutPositionTransform.position;

        Collider [] temporalHits = Physics.OverlapBox (slicePosition, cutOverlapBoxSize, cutDirectionTransform.rotation, currentProjectileInfo.targetToDamageLayer);

        bool bodyPartFound = false;

        if (temporalHits.Length > 0) {
            bool ragdollCollidersFoundOnCharacter = false;

            colliders = null;

            ragdollActivator currentRagdollActivator = currentCharacter.GetComponent<ragdollActivator> ();

            if (currentRagdollActivator != null) {
                colliders = currentRagdollActivator.getBodyColliderList ();

                if (colliders != null && colliders.Count > 0) {
                    ragdollCollidersFoundOnCharacter = true;
                }
            }

            Collider colliderToCheck = null;

            float minDistance = 1000;

            for (int i = 0; i < temporalHits.Length; i++) {
                Collider currentCollider = temporalHits [i];

                bool canActivateSliceResult = false;

                if (ragdollCollidersFoundOnCharacter) {
                    if (showDebugPrint) {
                        print ("ragoll detected");
                    }

                    if (colliders.Contains (currentCollider)) {
                        canActivateSliceResult = true;

                        if (showDebugPrint) {
                            print ("ragdoll part found");
                        }
                    }
                } else {
                    canActivateSliceResult = true;
                }

                if (canActivateSliceResult) {

                    if (applyDamage.isCharacter (currentCollider.gameObject)) {
                        bodyPartFound = true;

                        float currentDistance = GKC_Utils.distance (slicePosition, currentCollider.transform.position);

                        if (currentDistance < minDistance) {
                            minDistance = currentDistance;

                            colliderToCheck = currentCollider;
                        }
                    }
                }
            }

            if (bodyPartFound) {
                if (currentCharacter != null) {
                    applyDamage.killCharacter (currentCharacter);
                }

                processObject (colliderToCheck.gameObject, colliderToCheck, slicePosition);
            }
        }

        yield return null;
    }

    private Vector3 positionInWorldSpace
    {
        get
        {
            return (planeDefiner1.position + planeDefiner2.position + planeDefiner3.position) / 3f;
        }
    }

    private Vector3 normalInWorldSpace
    {
        get
        {
            Vector3 t0 = planeDefiner1.position;
            Vector3 t1 = planeDefiner2.position;
            Vector3 t2 = planeDefiner3.position;

            Vector3 vectorValue;

            vectorValue.x = t0.y * (t1.z - t2.z) + t1.y * (t2.z - t0.z) + t2.y * (t0.z - t1.z);
            vectorValue.y = t0.z * (t1.x - t2.x) + t1.z * (t2.x - t0.x) + t2.z * (t0.x - t1.x);
            vectorValue.z = t0.x * (t1.y - t2.y) + t1.x * (t2.y - t0.y) + t2.x * (t0.y - t1.y);

            return vectorValue;
        }
    }

    public void checkForceToApplyOnObject (GameObject objectToDamage)
    {
        Rigidbody objectToDamageRigidbody = objectToDamage.GetComponent<Rigidbody> ();

        Vector3 forceDirection = cutDirectionTransform.forward;

        float forceAmount = addForceMultiplier;

        float forceToVehiclesMultiplier = impactForceToVehiclesMultiplier;

        if (applyImpactForceToVehicles) {
            Rigidbody objectToDamageMainRigidbody = applyDamage.applyForce (objectToDamage);

            if (objectToDamageMainRigidbody != null) {
                Vector3 force = forceAmount * forceDirection;

                bool isVehicle = applyDamage.isVehicle (objectToDamage);

                if (isVehicle) {
                    force *= forceToVehiclesMultiplier;
                }

                objectToDamageMainRigidbody.AddForce (objectToDamageMainRigidbody.mass * force, forceMode);
            }
        } else {
            if (applyDamage.canApplyForce (objectToDamage)) {
                //print (objectToDamage.name);
                Vector3 force = forceAmount * forceDirection;

                if (objectToDamageRigidbody == null) {
                    objectToDamageRigidbody = objectToDamage.GetComponent<Rigidbody> ();
                }

                objectToDamageRigidbody.AddForce (objectToDamageRigidbody.mass * force, forceMode);
            }
        }
    }

    public override void setProjectileSpecialActionActiveState (bool state)
    {
        setShatterObjectActiveState (state);
    }

    //SHATTER FUNCTIONS
    public void setShatterObjectActiveState (bool state)
    {
        shatterObjectActive = state;
    }

    public void shatterObject (GameObject objectToCheck, Material crossSectionMaterial)
    {
        if (objectShattered) {
            return;
        }

        objectsToShatter = new List<GameObject> ();

        if (objectToCheck.transform.parent != null) {
            objectToCheck.transform.SetParent (null);
        }

        objectsToShatter.Add (objectToCheck);

        for (int i = 0; i < shatterAmount; i++) {
            randomShatter (crossSectionMaterial);
        }

        objectShattered = true;
    }

    public void randomShatter (Material crossSectionMaterial)
    {
        List<GameObject> adders = new List<GameObject> ();
        List<GameObject> removals = new List<GameObject> ();

        foreach (GameObject objectToShatter in objectsToShatter) {
            GameObject [] shatters = randomShatterSingle (objectToShatter, crossSectionMaterial);

            if (shatters != null) {
                foreach (GameObject add in shatters) {
                    adders.Add (add);
                }

                removals.Add (objectToShatter);
            }
        }

        foreach (GameObject rem in removals) {
            objectsToShatter.Remove (rem);
        }

        foreach (GameObject add in adders) {
            objectsToShatter.Add (add);
        }
    }

    public GameObject [] randomShatterSingle (GameObject objectToShatter, Material crossSectionMaterial)
    {
        Vector3 shatterPosition = objectToShatter.transform.position;

        GameObject [] shatters = sliceSystemUtils.shatterObject (objectToShatter, shatterPosition, crossSectionMaterial);

        if (shatters != null && shatters.Length > 0) {
            objectToShatter.SetActive (false);

            Vector3 cutPosition = objectToShatter.transform.position;

            // add rigidbodies and colliders
            foreach (GameObject shatteredObject in shatters) {
                shatteredObject.AddComponent<MeshCollider> ().convex = true;

                if (applyForceOnShatter) {
                    Rigidbody currentObjectRigidbody = shatteredObject.AddComponent<Rigidbody> ();

                    currentObjectRigidbody.AddExplosionForce (forceToApplyToCutPart, cutPosition, 10, 1, forceMode);
                } else {
                    shatteredObject.AddComponent<Rigidbody> ();
                }

                if (addSliceComponentToPieces) {
                    surfaceToSlice newSurfaceToSlice = shatteredObject.AddComponent<surfaceToSlice> ();

                    newSurfaceToSlice.setCrossSectionMaterial (crossSectionMaterial);
                }
            }
        }

        return shatters;
    }

    public override void resetProjectile ()
    {
        base.resetProjectile ();

        currentNumberOfCuts = 0;

        objectsDetected.Clear ();
    }

#if UNITY_EDITOR
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
            GKC_Utils.drawRectangleGizmo (cutPositionTransform.position, cutPositionTransform.rotation, Vector3.zero, cutOverlapBoxSize, gizmoColor);
        }
    }
#endif
}