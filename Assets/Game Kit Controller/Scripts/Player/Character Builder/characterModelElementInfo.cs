using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class characterModelElementInfo : MonoBehaviour
{
    [Header ("Main Setting")]
    [Space]

    public bool setNewHeightOnCharacter;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool setNewAnimator;
    public RuntimeAnimatorController newRuntimeAnimatorController;

    [Space]

    public bool setNewChestUpVectorValueOnRuntime;

    public Vector3 chestUpVector;

    [Space]
    [Header ("Armor Cloth Data Settings")]
    [Space]

    public bool setNewArmorClothData;

    public armorClothPieceTemplateData mainArmorClothPieceTemplateData;

    [Space]
    [Space]

    public fullArmorClothTemplateData mainFullArmorClothTemplateData;

    [Space]
    [Space]

    public characterAspectCustomizationTemplateData mainCharacterAspectCustomizationTemplateData;

    [Space]
    [Space]

    public bool setNewArmorClothSet;

    public characterAspectCustomizationTemplate initialCharacterAspectCustomizationTemplate;

    [Space]

    public bool setRandomArmorClothSet;

    public List<characterAspectCustomizationTemplate> characterAspectCustomizationTemplateList = new List<characterAspectCustomizationTemplate> ();


    [Space]
    [Header ("Character Elements")]
    [Space]

    public Transform head;
    public Transform neck;
    public Transform chest;
    public Transform spine;

    public Transform hips;

    public Transform rightLowerArm;
    public Transform leftLowerArm;
    public Transform rightHand;
    public Transform leftHand;

    public Transform rightLowerLeg;
    public Transform leftLowerLeg;
    public Transform rightFoot;
    public Transform leftFoot;
    public Transform rightToes;
    public Transform leftToes;

    [Space]
    [Header ("Ragdoll Elements")]
    [Space]

    public bool setRagdollBonesManually;

    public Transform rightUpperArm;
    public Transform leftUpperArm;

    public Transform rightUpperLeg;
    public Transform leftUpperLeg;

    public Transform middleSpine;
    public Transform pelvis;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject characterGameObject;

    public Animator mainAnimator;


    public void getCharacterElements ()
    {
        getCharacterBones ();

        updateComponent ();
    }

    public void getCharacterBones ()
    {
        if (mainAnimator == null) {
            mainAnimator = GetComponent<Animator> ();

            if (mainAnimator == null) {
                Debug.Log ("WARNING: There is not an animator component in this model, make sure to attach that component before create a new character");

                return;
            }
        }

        head = mainAnimator.GetBoneTransform (HumanBodyBones.Head);
        neck = mainAnimator.GetBoneTransform (HumanBodyBones.Neck);

        if (neck == null) {
            if (head != null) {
                neck = head.parent;
            } else {
                Debug.Log ("WARNING: no head found, assign it manually to make sure all of them are configured correctly");
            }
        }

        chest = mainAnimator.GetBoneTransform (HumanBodyBones.Chest);
        spine = mainAnimator.GetBoneTransform (HumanBodyBones.Spine);

        if (spine != null) {
            if (chest != null) {
                if (spine != chest.parent) {
                    spine = chest.parent;
                }
            } else {
                Debug.Log ("WARNING: no chest found, assign it manually to make sure all of them are configured correctly");
            }
        } else {
            Debug.Log ("WARNING: no spine found, assign it manually to make sure all of them are configured correctly");
        }

        hips = mainAnimator.GetBoneTransform (HumanBodyBones.Hips);

        rightLowerArm = mainAnimator.GetBoneTransform (HumanBodyBones.RightLowerArm);
        leftLowerArm = mainAnimator.GetBoneTransform (HumanBodyBones.LeftLowerArm);
        rightHand = mainAnimator.GetBoneTransform (HumanBodyBones.RightHand);
        leftHand = mainAnimator.GetBoneTransform (HumanBodyBones.LeftHand);

        rightLowerLeg = mainAnimator.GetBoneTransform (HumanBodyBones.RightLowerLeg);
        leftLowerLeg = mainAnimator.GetBoneTransform (HumanBodyBones.LeftLowerLeg);
        rightFoot = mainAnimator.GetBoneTransform (HumanBodyBones.RightFoot);
        leftFoot = mainAnimator.GetBoneTransform (HumanBodyBones.LeftFoot);
        rightToes = mainAnimator.GetBoneTransform (HumanBodyBones.RightToes);
        leftToes = mainAnimator.GetBoneTransform (HumanBodyBones.LeftToes);
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Character Model Info", gameObject);
    }
}
