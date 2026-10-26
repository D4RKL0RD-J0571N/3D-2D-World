using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class shakeCharacterBodyPartSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool shakeAnimationEnabled = true;

    public Vector3 shakeRotationSpeed;
    public Vector3 shakeRotationAmount;

    public float waitTimeToResetShake;

    public float resetShakeSpeed;

    public float regularShakeDuration;

    public float maxTimeToStopShake = 10;

    [Space]
    [Header ("Transform List Settings")]
    [Space]

    public bool storeTransformListOnStart;

    public List<characterBodyPartInfo> characterBodyPartInfoList = new List<characterBodyPartInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public bool transformListStored;

    public bool shakeAnimationActive;

    public string currentBodyPartName;

    [Space]

    public bool useCustomDuration;
    public float customDuration;

    public Vector3 currentRotationEuler;

    [Space]
    [Header ("Components")]
    [Space]

    public Animator animator;


    float lastTimeShakeActive;

    characterBodyPartInfo currentCharacterBodyPartInfo;

    Quaternion currentRotation;


    void Start ()
    {
        if (shakeAnimationActive) {
            if (storeTransformListOnStart) {
                if (!transformListStored) {
                    if (characterBodyPartInfoList.Count == 0) {
                        storeCharacterBones ();
                    } else {
                        transformListStored = true;
                    }
                }
            }
        }
    }

    private void LateUpdate ()
    {
        if (shakeAnimationActive) {
            float currentRotationDistance = GKC_Utils.distance (currentRotationEuler, Vector3.zero);

            float currentDurationValue = regularShakeDuration;

            if (useCustomDuration) {
                currentDurationValue = customDuration;
            }

            bool stopShakeAnimationResult = false;

            if (Time.time > lastTimeShakeActive + maxTimeToStopShake) {
                stopShakeAnimationResult = true;
            }

            if (Time.time > lastTimeShakeActive + currentDurationValue && currentRotationDistance < 0.1f) {
                stopShakeAnimationResult = true;
            }

            if (stopShakeAnimationResult) {
                shakeAnimationActive = false;
            } else {
                if (Time.time > waitTimeToResetShake + lastTimeShakeActive) {
                    currentRotationEuler = Vector3.Lerp (currentRotationEuler, Vector3.zero, Time.deltaTime * resetShakeSpeed);
                } else {
                    float currentTimeTime = Time.time;

                    float eulTargetX = Mathf.Sin (currentTimeTime * shakeRotationSpeed.x) * shakeRotationAmount.x;
                    float eulTargetY = Mathf.Sin (currentTimeTime * shakeRotationSpeed.y) * shakeRotationAmount.y;
                    float eulTargetZ = Mathf.Cos (currentTimeTime * shakeRotationSpeed.z) * shakeRotationAmount.z;

                    currentRotationEuler = new Vector3 (eulTargetX, eulTargetY, eulTargetZ);
                }

                currentRotation = Quaternion.Euler (currentRotationEuler);

                currentCharacterBodyPartInfo.bodyPartTransform.rotation =
                currentRotation * currentCharacterBodyPartInfo.bodyPartTransform.rotation;
            }
        }
    }

    public void setShakePartCustomDuration (float newDuration)
    {
        customDuration = newDuration;

        useCustomDuration = customDuration != 0;
    }

    public void activatShakeAnimationXSecondsFromEditor ()
    {
        activatShakeAnimationXSecondsByName (currentBodyPartName);
    }

    public void activatShakeAnimationXSecondsByName (string bodyPartName)
    {
        if (!shakeAnimationEnabled) {
            return;
        }

        int bodyPartIndex = characterBodyPartInfoList.FindIndex (s => s.Name.Equals (bodyPartName));

        if (bodyPartIndex > -1) {
            activatShakeAnimationXSecondsByIndex (bodyPartIndex);
        } else {
            if (showDebugPrint) {
                print ("body part not found " + bodyPartName);
            }
        }
    }

    public void activatShakeAnimationXSecondsByTransform (Transform bodyPartTransform)
    {
        if (!shakeAnimationEnabled) {
            return;
        }

        print (bodyPartTransform.name);

        int bodyPartIndex = characterBodyPartInfoList.FindIndex (s => s.bodyPartTransform.Equals (bodyPartTransform));

        if (bodyPartIndex > -1) {
            activatShakeAnimationXSecondsByIndex (bodyPartIndex);
        } else {
            if (showDebugPrint) {
                print ("body part not found ");
            }
        }
    }

    void activatShakeAnimationXSecondsByIndex (int bodyPartIndex)
    {
        if (!shakeAnimationEnabled) {
            return;
        }

        if (bodyPartIndex > -1) {
            currentCharacterBodyPartInfo = characterBodyPartInfoList [bodyPartIndex];

            currentBodyPartName = currentCharacterBodyPartInfo.Name;

            shakeAnimationActive = true;

            currentRotation = Quaternion.identity;

            currentRotationEuler = Vector3.zero;

            lastTimeShakeActive = Time.time;

            if (showDebugPrint) {
                print ("activating shake on body part " + currentBodyPartName);
            }
        }
    }

    public void storeCharacterBones ()
    {
        characterBodyPartInfoList.Clear ();

        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.Head));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.Neck));

        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.Chest));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.Spine));

        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.Hips));

        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.RightShoulder));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.LeftShoulder));

        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.RightLowerArm));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.LeftLowerArm));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.RightUpperArm));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.LeftUpperArm));

        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.RightHand));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.LeftHand));

        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.RightLowerLeg));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.LeftLowerLeg));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.RightUpperLeg));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.LeftUpperLeg));

        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.RightFoot));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.LeftFoot));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.RightToes));
        storeBodyPart (animator.GetBoneTransform (HumanBodyBones.LeftToes));

        transformListStored = true;

        for (int i = characterBodyPartInfoList.Count - 1; i >= 0; i--) {
            if (characterBodyPartInfoList [i].bodyPartTransform == null) {
                characterBodyPartInfoList.RemoveAt (i);
            }
        }

        if (!Application.isPlaying) {
            updateComponent ();
        }
    }

    void storeBodyPart (Transform newPart)
    {
        if (newPart == null) {
            return;
        }

        characterBodyPartInfo newCharacterBodyPartInfo = new characterBodyPartInfo ();

        newCharacterBodyPartInfo.Name = newPart.name;

        newCharacterBodyPartInfo.bodyPartTransform = newPart;

        characterBodyPartInfoList.Add (newCharacterBodyPartInfo);
    }


    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Shake Character Body Part System", gameObject);
    }

    [System.Serializable]
    public class characterBodyPartInfo
    {
        public string Name;
        public Transform bodyPartTransform;
    }
}