using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class surfaceToSlice : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool cutSurfaceEnabled = true;

    public bool useCustomForceAmount;
    public float customForceAmount;

    public bool useCustomForceMode;
    public ForceMode customForceMode;

    [Space]

    public bool useBoxCollider;

    public bool ignoreRegularDamageIfCutSurfaceNotEnabled;

    [Space]

    public GameObject mainSurfaceToSlice;

    [Space]
    [Header ("Material Settings")]
    [Space]

    public Material crossSectionMaterial;

    [Space]
    [Header ("Tag and Layer Settings")]
    [Space]

    public bool setNewTagOnCut;
    public string newTagOnCut;

    public bool setNewLayerOnCut;
    public string newLayerOnCut;

    [Space]
    [Header ("Particle Settings")]
    [Space]

    public bool useParticlesOnSlice;
    public GameObject particlesOnSlicePrefab;

    [Space]
    [Header ("Slice Limit Settings")]
    [Space]

    public bool cutMultipleTimesActive = true;

    public bool useLimitedNumberOfCuts;
    public int limitedNumberOfCuts;

    [Space]
    [Header ("Slice Direction Settings")]
    [Space]

    public bool checkForCustomSliceDirection;

    public float maxSliceDirectionAngle;
    public Transform customSliceDirectionUp;
    public bool allowReverseSliceDirection;

    [Space]
    [Header ("Character Settings")]
    [Space]

    public bool objectIsCharacter;

    [Space]

    public bool ignoreDamageIfSliceNotActivated;

    [Space]

    public simpleSliceSystem mainSimpleSliceSystem;

    [Space]
    [Header ("Bullet Time Settings")]
    [Space]

    public bool useTimeBulletOnSliceEnabled;
    public float timeBulletDuration = 3;
    public float timeScale = 0.2f;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool destroySlicedPartsAfterDelay;
    public float delayToDestroySlicedParts;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool mainSimpleSliceSystemAssigned;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventOnCut;
    public UnityEvent eventOnCut;

    public bool copyEventOnSlicedObjects;

    [Space]

    public bool useEventBeforeCut;
    public UnityEvent eventBeforeCut;

    [Space]

    public bool ignoreDestroyOriginalObject;

    public UnityEvent eventsOnIgnoreDestroyOriginalObject;

    [Space]

    public bool useEventToSendNewSlicedParts;
    public eventParameters.eventToCallWithGameObject eventToSendNewSlicedParts;


    float lastTimeSliced;

    destroyGameObject mainDestroyGameObject;
    bool mainDestroyGameObjectAssigned;


    public void checkEventsOnIgnoreDestroyOriginalObject ()
    {
        if (ignoreDestroyOriginalObject) {
            eventsOnIgnoreDestroyOriginalObject.Invoke ();
        }
    }

    public void checkEventBeforeSlice ()
    {
        if (useEventBeforeCut) {
            eventBeforeCut.Invoke ();
        }
    }

    public void checkEventOnCut ()
    {
        if (useEventOnCut) {
            eventOnCut.Invoke ();
        }
    }

    public void copySurfaceInfo (surfaceToSlice surfaceToCopy)
    {
        lastTimeSliced = Time.time;

        surfaceToCopy.setCrossSectionMaterial (crossSectionMaterial);

        surfaceToCopy.setNewTagOnCut = setNewTagOnCut;
        surfaceToCopy.newTagOnCut = newTagOnCut;

        surfaceToCopy.setNewLayerOnCut = setNewLayerOnCut;
        surfaceToCopy.newLayerOnCut = newLayerOnCut;

        if (useParticlesOnSlice) {
            surfaceToCopy.useParticlesOnSlice = useParticlesOnSlice;
            surfaceToCopy.particlesOnSlicePrefab = particlesOnSlicePrefab;
        }

        surfaceToCopy.cutMultipleTimesActive = cutMultipleTimesActive;

        if (useLimitedNumberOfCuts) {
            surfaceToCopy.useLimitedNumberOfCuts = useLimitedNumberOfCuts;
            surfaceToCopy.limitedNumberOfCuts = limitedNumberOfCuts - 1;
        }

        surfaceToCopy.useCustomForceAmount = useCustomForceAmount;

        surfaceToCopy.customForceAmount = customForceAmount;

        surfaceToCopy.useCustomForceMode = useCustomForceMode;

        surfaceToCopy.customForceMode = customForceMode;

        surfaceToCopy.useBoxCollider = useBoxCollider;

        surfaceToCopy.lastTimeSliced = lastTimeSliced;

        surfaceToCopy.destroySlicedPartsAfterDelay = destroySlicedPartsAfterDelay;

        surfaceToCopy.delayToDestroySlicedParts = delayToDestroySlicedParts;

        surfaceToCopy.copyEventOnSlicedObjects = copyEventOnSlicedObjects;

        if (copyEventOnSlicedObjects) {

            surfaceToCopy.useEventOnCut = useEventOnCut;

            surfaceToCopy.eventOnCut = eventOnCut;
        }
    }

    public bool isObjectCharacter ()
    {
        return objectIsCharacter;
    }

    public bool isIgnoreDamageIfSliceNotActivatedActive ()
    {
        return ignoreDamageIfSliceNotActivated;
    }

    public bool isCutSurfaceEnabled ()
    {
        return cutSurfaceEnabled;
    }

    public void setCutSurfaceEnabledState (bool state)
    {
        cutSurfaceEnabled = state;

        if (showDebugPrint) {
            print ("Set Cut Surface Enabled State " + cutSurfaceEnabled);
        }
    }

    public void setIgnoreRegularDamageIfCutSurfaceNotEnabledState (bool state)
    {
        ignoreRegularDamageIfCutSurfaceNotEnabled = state;
    }

    public bool isIgnoreRegularDamageIfCutSurfaceNotEnabled ()
    {
        return ignoreRegularDamageIfCutSurfaceNotEnabled;
    }

    public float getLastTimeSliced ()
    {
        return lastTimeSliced;
    }

    public bool sliceCanBeActivated (float minDelayToSliceSameObject)
    {
        if (useLimitedNumberOfCuts) {
            if (limitedNumberOfCuts <= 0) {
                return false;
            }
        }

        if (Time.time > lastTimeSliced + minDelayToSliceSameObject) {
            return true;
        }

        return false;
    }

    public bool checkSliceDirectionResult (Vector3 sliceDirectionRight, Vector3 sliceDirectionUp)
    {
        if (checkForCustomSliceDirection) {
            float sliceAngleRight =
                Vector3.SignedAngle (sliceDirectionRight, customSliceDirectionUp.right,
                customSliceDirectionUp.forward);

            float sliceAngleRightABS = Mathf.Abs (sliceAngleRight);

            float sliceAngleUp =
                Vector3.SignedAngle (sliceDirectionUp, customSliceDirectionUp.up,
                customSliceDirectionUp.forward);

            float sliceAngleUpABS = Mathf.Abs (sliceAngleUp);

            if (showDebugPrint) {
                print ("checkSliceDirectionResult " + sliceDirectionRight + " " + sliceAngleRight);
                print (sliceDirectionUp + " " + sliceAngleUp);
            }

            if (allowReverseSliceDirection) {
                float oppositeRightAngle = (180 - sliceAngleRightABS);

                if (oppositeRightAngle < maxSliceDirectionAngle) {
                    return true;
                } else {
                    if (oppositeRightAngle < 90) {
                        float oppositeUpAngle = (180 - sliceAngleUpABS);

                        if (oppositeUpAngle < maxSliceDirectionAngle) {
                            return true;
                        }
                    }
                }
            }

            if (sliceAngleRightABS <= maxSliceDirectionAngle) {
                return true;
            } else {
                if (sliceAngleRightABS < 90) {
                    if (sliceAngleUpABS <= maxSliceDirectionAngle) {
                        return true;
                    }
                }
            }

            return false;
        }

        return true;
    }

    public simpleSliceSystem getMainSimpleSliceSystem ()
    {
        return mainSimpleSliceSystem;
    }

    public void setMainSimpleSliceSystem (GameObject newObject)
    {
        if (newObject != null) {
            mainSimpleSliceSystem = newObject.GetComponent<simpleSliceSystem> ();
        }
    }

    public void setMainSimpleSliceSystemCheckingNotNull (GameObject newObject)
    {
        setMainSimpleSliceSystem (newObject);

        cutSurfaceEnabled = (mainSimpleSliceSystem != null);
    }

    public void setDestructionPending (bool state)
    {
        if (!mainSimpleSliceSystemAssigned) {
            mainSimpleSliceSystemAssigned = mainSimpleSliceSystem != null;
        }

        if (mainSimpleSliceSystemAssigned) {
            mainSimpleSliceSystem.setDestructionPending (state);
        }
    }

    public void setUseTimeBulletValue (bool state)
    {
        useTimeBulletOnSliceEnabled = state;
    }

    public void checkTimeBulletOnCut ()
    {
        if (useTimeBulletOnSliceEnabled) {
            GKC_Utils.activateTimeBulletXSeconds (timeBulletDuration, timeScale);
        }
    }

    public Material getCrossSectionMaterial ()
    {
        return crossSectionMaterial;
    }

    public void setCrossSectionMaterial (Material newMaterial)
    {
        crossSectionMaterial = newMaterial;
    }

    public void checkDestroySlicedPartsAfterDelay ()
    {
        if (destroySlicedPartsAfterDelay && !objectIsCharacter) {
            mainDestroyGameObjectAssigned = mainDestroyGameObject != null;

            if (!mainDestroyGameObjectAssigned) {
                mainDestroyGameObject = gameObject.AddComponent<destroyGameObject> ();

                mainDestroyGameObjectAssigned = true;
            }

            if (mainDestroyGameObjectAssigned) {
                mainDestroyGameObject.setTimer (delayToDestroySlicedParts);
            }
        }
    }

    public void setMainSurfaceToSlice (GameObject newObject)
    {
        mainSurfaceToSlice = newObject;
    }

    public GameObject getMainSurfaceToSlice ()
    {
        if (mainSurfaceToSlice == null) {
            mainSurfaceToSlice = gameObject;
        }

        return mainSurfaceToSlice;
    }

    //EDITOR FUNCTIONS
    public void setCutSurfaceEnabledStateFromEditor (bool state)
    {
        setCutSurfaceEnabledState (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Surface To Slice", gameObject);
    }
}