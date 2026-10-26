using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class hideBodyPartOnCharacterSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool hideBodyPartEnabled = true;

    [Space]
    [Header ("Time Bullet Settings")]
    [Space]

    public bool useTimeBullet = true;
    public float timeBulletDuration = 3;
    public float timeScale = 0.2f;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool storeHiddenBodyPartsEnabled;

    [Space]
    [Header ("Debug")]
    [Space]

    public Transform currentBodyPartToHide;

    public List<Transform> storedHiddentBodyPartsList = new List<Transform> ();

    [Space]
    [Header ("Components")]
    [Space]

    public Transform characterTransform;

    public void activateBodyPartFromMountPoint (string mountPointPartName)
    {
        setActiveStateBodyPartFromMountPoint (mountPointPartName, true);
    }

    public void deactivateBodyPartFromMountPoint (string mountPointPartName)
    {
        setActiveStateBodyPartFromMountPoint (mountPointPartName, false);
    }
    public void setActiveStateBodyPartFromMountPoint (string mountPointPartName, bool state)
    {
        if (characterTransform != null) {
            Transform newBodyPart = GKC_Utils.getHumanBoneMountPointTransformByName (mountPointPartName, characterTransform);

            if (newBodyPart != null) {
                if (newBodyPart.gameObject.activeSelf != state) {
                    newBodyPart.gameObject.SetActive (state);
                }

                if (!state) {
                    if (storeHiddenBodyPartsEnabled) {
                        if (!storedHiddentBodyPartsList.Contains (newBodyPart)) {
                            storedHiddentBodyPartsList.Add (newBodyPart);
                        }
                    }
                }
            }
        }
    }

    public void setEnabledStateColliderBodyPartFromMountPoint (string mountPointPartName)
    {
        setActiveStateColliderBodyPartFromMountPoint (mountPointPartName, true);
    }
    public void setDisabledStateColliderBodyPartFromMountPoint (string mountPointPartName)
    {
        setActiveStateColliderBodyPartFromMountPoint (mountPointPartName, false);
    }

    public void setActiveStateColliderBodyPartFromMountPoint (string mountPointPartName, bool state)
    {
        if (characterTransform != null) {
            Transform newBodyPart = GKC_Utils.getHumanBoneMountPointTransformByName (mountPointPartName, characterTransform);

            if (newBodyPart != null) {
                Collider currentCollider = newBodyPart.GetComponent<Collider> ();

                if (currentCollider != null && currentCollider.enabled != state) {
                    currentCollider.enabled = state;
                }
            }
        }
    }

    public void hideBodyPartFromMountPoint (string mountPointPartName)
    {
        if (characterTransform != null) {
            Transform newBodyPart = GKC_Utils.getHumanBoneMountPointTransformByName (mountPointPartName, characterTransform);

            if (newBodyPart != null) {
                hideBodyPart (newBodyPart);
            }
        }
    }

    public void hideBodyPartFromMountPointWithoutBulletTimeCheck (string mountPointPartName)
    {
        if (characterTransform != null) {
            currentBodyPartToHide = GKC_Utils.getHumanBoneMountPointTransformByName (mountPointPartName, characterTransform);

            if (currentBodyPartToHide != null) {
                setBodyPartScale ();
            }
        }
    }

    public void hideBodyPart ()
    {
        if (useTimeBullet) {
            GKC_Utils.activateTimeBulletXSeconds (timeBulletDuration, timeScale);
        }

        setBodyPartScale ();
    }

    public void hideBodyPart (Transform newBodyPart)
    {
        currentBodyPartToHide = newBodyPart;

        hideBodyPart ();
    }

    public void hideBodyPartWithoutBulletTimeCheck ()
    {
        setBodyPartScale ();
    }

    public void hideBodyPartWithoutBulletTimeCheck (Transform newBodyPart)
    {
        currentBodyPartToHide = newBodyPart;

        setBodyPartScale ();
    }

    public void setBodyPartScale ()
    {
        if (!hideBodyPartEnabled) {
            return;
        }

        if (currentBodyPartToHide != null) {
            currentBodyPartToHide.localScale = Vector3.zero;

            if (storeHiddenBodyPartsEnabled) {
                if (!storedHiddentBodyPartsList.Contains (currentBodyPartToHide)) {
                    storedHiddentBodyPartsList.Add (currentBodyPartToHide);
                }
            }
        }
    }

    public void resetHiddenBodyPartsList ()
    {
        if (storeHiddenBodyPartsEnabled) {
            for (int i = 0; i < storedHiddentBodyPartsList.Count; i++) {
                if (storedHiddentBodyPartsList [i] != null) {
                    if (storedHiddentBodyPartsList [i].localScale == Vector3.zero) {
                        storedHiddentBodyPartsList [i].localScale = Vector3.one;
                    }

                    if (!storedHiddentBodyPartsList [i].gameObject.activeSelf) {
                        storedHiddentBodyPartsList [i].gameObject.SetActive (true);
                    }
                }
            }

            storedHiddentBodyPartsList.Clear ();
        }
    }

    public void setUseTimeBulletValue (bool state)
    {
        useTimeBullet = state;
    }

    public void setHideBodyPartEnabledState (bool state)
    {
        hideBodyPartEnabled = state;
    }

    public void setUseTimeBulletValueFromEditor (bool state)
    {
        setUseTimeBulletValue (state);

        updateComponent ();
    }

    public void setHideBodyPartEnabledStateFromEditor (bool state)
    {
        setHideBodyPartEnabledState (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);
    }
}
