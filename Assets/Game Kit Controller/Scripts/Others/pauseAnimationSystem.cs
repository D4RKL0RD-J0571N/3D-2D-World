using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pauseAnimationSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool pauseAnimationEnabled = true;

    public bool pauseAnimatorStateInsteadOfReduceFrames;

    [Range (1, 60)] public int numberOfFrames = 1;

    public bool pauseAnimationActive;

    [Space]
    [Header ("Transform List Settings")]
    [Space]

    public bool storeTransformListOnStart;

    public List<Transform> transformList = new List<Transform> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public bool transformListStored;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController mainPlayerController;
    public Animator animator;

    Dictionary<int, Snapshot> snapshots = new Dictionary<int, Snapshot> ();
    float updateTime = 0f;

    Coroutine pauseCoroutine;

    Transform currentSaveSnapshotTarget;


    void Start ()
    {
        if (pauseAnimationActive) {
            if (storeTransformListOnStart) {
                if (!transformListStored) {
                    if (transformList.Count == 0) {
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
        if (pauseAnimationActive) {
            if (Time.time - updateTime > 1f / numberOfFrames) {
                SaveSnapshot ();
                updateTime = Time.time;
            }

            foreach (KeyValuePair<int, Snapshot> item in snapshots) {
                item.Value.currentTransform.localPosition = item.Value.localPosition;
                item.Value.currentTransform.localEulerAngles = item.Value.localEulerAngles;
            }
        }
    }

    private void SaveSnapshot ()
    {
        foreach (Transform child in transformList) {
            currentSaveSnapshotTarget = child;

            int uid = GKC_Utils.GetObjectId (currentSaveSnapshotTarget);

            snapshots [uid] = new Snapshot (currentSaveSnapshotTarget);
        }
    }

    public void setPauseAnimationActiveState (bool state)
    {
        if (state && !pauseAnimationEnabled) {
            return;
        }

        pauseAnimationActive = state;
    }

    public void activatPauseAnimationXSeconds (float newDuration)
    {
        if (pauseCoroutine != null) {
            StopCoroutine (pauseCoroutine);
        }

        if (!pauseAnimationEnabled) {
            return;
        }

        pauseCoroutine = StartCoroutine (activatPauseAnimationXSecondsCoroutine (newDuration));
    }

    IEnumerator activatPauseAnimationXSecondsCoroutine (float newDuration)
    {
        if (!transformListStored) {
            if (transformList.Count == 0) {
                storeCharacterBones ();
            } else {
                transformListStored = true;
            }
        }

        if (showDebugPrint) {
            print ("activatPauseAnimationXSecondsCoroutine " + newDuration);
        }

        bool pauseActivatedOnThisAction = false;

        if (pauseAnimatorStateInsteadOfReduceFrames) {
            if (!mainPlayerController.overrideAnimationSpeedActive) {
                mainPlayerController.setOverrideAnimationSpeedActiveState (true);
                mainPlayerController.setReducedVelocity (0);

                pauseActivatedOnThisAction = true;
            }
        } else {
            pauseAnimationActive = true;
        }

        WaitForSeconds delay = new WaitForSeconds (newDuration);

        yield return delay;

        if (pauseAnimatorStateInsteadOfReduceFrames) {
            if (pauseActivatedOnThisAction || mainPlayerController.getAnimSpeedMultiplier () == 0) {
                if (!mainPlayerController.isAnimSpeedMultiplierChangedDuringXTimeActive ()) {
                    mainPlayerController.setOverrideAnimationSpeedActiveState (false);
                    mainPlayerController.setReducedVelocity (1);
                }
            }
        } else {
            pauseAnimationActive = false;
        }
    }

    public void setNumberOfFrames (int newValue)
    {
        numberOfFrames = newValue;
    }

    public void storeCharacterBones ()
    {
        transformList.Clear ();

        transformList.Add (animator.GetBoneTransform (HumanBodyBones.Head));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.Neck));

        transformList.Add (animator.GetBoneTransform (HumanBodyBones.Chest));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.Spine));

        transformList.Add (animator.GetBoneTransform (HumanBodyBones.Hips));

        transformList.Add (animator.GetBoneTransform (HumanBodyBones.RightShoulder));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.LeftShoulder));

        transformList.Add (animator.GetBoneTransform (HumanBodyBones.RightLowerArm));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.LeftLowerArm));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.RightUpperArm));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.LeftUpperArm));

        transformList.Add (animator.GetBoneTransform (HumanBodyBones.RightHand));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.LeftHand));

        transformList.Add (animator.GetBoneTransform (HumanBodyBones.RightLowerLeg));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.LeftLowerLeg));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.RightUpperLeg));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.LeftUpperLeg));

        transformList.Add (animator.GetBoneTransform (HumanBodyBones.RightFoot));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.LeftFoot));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.RightToes));
        transformList.Add (animator.GetBoneTransform (HumanBodyBones.LeftToes));

        transformListStored = true;

        for (int i = transformList.Count - 1; i >= 0; i--) {
            if (transformList [i] == null) {
                transformList.RemoveAt (i);
            }
        }

        if (!Application.isPlaying) {
            updateComponent ();
        }
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Pause Animation System transform list", gameObject);
    }

    [System.Serializable]
    public class Snapshot
    {
        public Transform currentTransform;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;

        public Snapshot (Transform newTransform)
        {
            currentTransform = newTransform;

            UpdateTransformValues ();
        }

        public void UpdateTransformValues ()
        {
            localPosition = currentTransform.localPosition;
            localEulerAngles = currentTransform.localEulerAngles;
        }
    }
}