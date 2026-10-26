using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class setRigidbodyStateSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public ForceMode forceMode;
    [Tooltip ("The force direction in external transform use the UP direction of that transform")] public float forceAmount;

    public float explosionRadius;
    public float explosioUpwardAmount;

    public bool checkToAddRigidbodyIfNotFound;
    public bool unparentRigidbody;

    [Space]
    [Header ("Rigidbody List Settings")]
    [Space]

    public List<GameObject> rigidbodyList = new List<GameObject> ();

    [Space]
    [Header ("Detect Rigidbodies Settings")]
    [Space]

    public bool searchRigidbodiesOnRadiusEnabled;
    public float radiusToSearch;
    public LayerMask radiusLayer;
    public Transform mainTransformCenter;

    [Space]
    [Header ("Remote Events Settings")]
    [Space]

    public bool useRemoteEventOnObjectsFound;
    public List<string> removeEventNameList = new List<string> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;


    public void searchRigidbodiesInRadius ()
    {
        if (!searchRigidbodiesOnRadiusEnabled) {
            return;
        }

        if (mainTransformCenter == null) {
            mainTransformCenter = transform;
        }

        Collider [] colliders = Physics.OverlapSphere (mainTransformCenter.position, radiusToSearch, radiusLayer);

        if (colliders.Length == 0) {
            if (showDebugPrint) {
                print ("objects not found on radius");
            }
        } else {
            if (showDebugPrint) {
                print ("objects found on radius " + colliders.Length);
            }
        }

        for (int i = 0; i < colliders.Length; i++) {
            Rigidbody currentRigidbody = colliders [i].attachedRigidbody;

            if (currentRigidbody != null) {
                if (!rigidbodyList.Contains (currentRigidbody.gameObject)) {
                    rigidbodyList.Add (currentRigidbody.gameObject);
                }
            }
        }
    }

    public void setKinematicState (bool state)
    {
        for (int i = 0; i < rigidbodyList.Count; i++) {
            if (rigidbodyList [i] != null) {
                Rigidbody currentRigidbody = rigidbodyList [i].GetComponent<Rigidbody> ();

                if (currentRigidbody != null) {
                    if (currentRigidbody.isKinematic != false) {
                        currentRigidbody.isKinematic = false;
                    }
                }
            }
        }
    }

    public void addForce (Transform forceDirection)
    {
        for (int i = 0; i < rigidbodyList.Count; i++) {
            if (rigidbodyList [i] != null) {
                Rigidbody currentRigidbody = rigidbodyList [i].GetComponent<Rigidbody> ();

                if (currentRigidbody != null) {
                    if (currentRigidbody.isKinematic != false) {
                        currentRigidbody.isKinematic = false;
                    }

                    currentRigidbody.AddForce (forceDirection.up * forceAmount, forceMode);
                }
            }
        }
    }

    public void addExplosiveForce (Transform explosionCenter)
    {
        for (int i = 0; i < rigidbodyList.Count; i++) {
            if (rigidbodyList [i] != null) {
                Rigidbody currentRigidbody = rigidbodyList [i].GetComponent<Rigidbody> ();

                if (currentRigidbody != null) {
                    if (currentRigidbody.isKinematic != false) {
                        currentRigidbody.isKinematic = false;
                    }

                    currentRigidbody.AddExplosionForce (forceAmount, explosionCenter.position, explosionRadius, explosioUpwardAmount, forceMode);
                }
            }
        }
    }

    public void addExplosiveForceUsingObjectsMass (Transform explosionCenter)
    {
        for (int i = 0; i < rigidbodyList.Count; i++) {
            if (rigidbodyList [i] != null) {
                Rigidbody currentRigidbody = rigidbodyList [i].GetComponent<Rigidbody> ();

                if (currentRigidbody != null) {
                    if (currentRigidbody.isKinematic != false) {
                        currentRigidbody.isKinematic = false;
                    }

                    currentRigidbody.AddExplosionForce (forceAmount * currentRigidbody.mass, explosionCenter.position, explosionRadius, explosioUpwardAmount, forceMode);
                }
            }
        }
    }

    public void addForceToThis (Transform forceDirection)
    {
        Rigidbody currentRigidbody = gameObject.GetComponent<Rigidbody> ();

        if (checkToAddRigidbodyIfNotFound) {
            if (currentRigidbody == null) {
                currentRigidbody = gameObject.AddComponent<Rigidbody> ();
            }
        }

        if (currentRigidbody != null) {
            if (currentRigidbody.isKinematic != false) {
                currentRigidbody.isKinematic = false;
            }

            if (unparentRigidbody) {
                if (transform.parent != null) {
                    transform.SetParent (null);
                }
            }

            currentRigidbody.AddForce (forceDirection.up * forceAmount, forceMode);
        }
    }

    public void checkRemoteEventsOnRigidbodyList ()
    {
        if (useRemoteEventOnObjectsFound) {
            for (int i = 0; i < rigidbodyList.Count; i++) {
                if (rigidbodyList [i] != null) {
                    remoteEventSystem currentRemoteEventSystem = rigidbodyList [i].GetComponent<remoteEventSystem> ();

                    if (currentRemoteEventSystem != null) {
                        for (int j = 0; j < removeEventNameList.Count; j++) {

                            currentRemoteEventSystem.callRemoteEvent (removeEventNameList [j]);
                        }
                    }
                }
            }
        }
    }
}
