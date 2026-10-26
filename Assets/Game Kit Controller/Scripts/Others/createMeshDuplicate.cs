using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class createMeshDuplicate : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public List<SkinnedMeshRenderer> mainSkinnedMeshRenderer = new List<SkinnedMeshRenderer> ();

    [Space]

    public bool useNewMaterial;

    public Material duplicateMaterial;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public UnityEvent eventOnCreateDuplicate;

    public bool useEventToSentDuplicatedMeshes;
    public eventParameters.eventToCallWithGameObject eventToSentDuplicatedMeshes;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject duplicateBaseGameObject;
    public Transform meshTransform;

    public void createDuplicate ()
    {
        List<GameObject> newList = new List<GameObject> ();

        int mainSkinnedMeshRendererCount = mainSkinnedMeshRenderer.Count;

        for (int i = 0; i < mainSkinnedMeshRendererCount; i++) {
            if (mainSkinnedMeshRenderer [i] != null) {
                GameObject newCopy = new GameObject ();

                Vector3 targetPosition = mainSkinnedMeshRenderer [i].transform.position;
                Vector3 targetRotation = mainSkinnedMeshRenderer [i].transform.eulerAngles;

                newCopy.transform.position = targetPosition;
                newCopy.transform.eulerAngles = targetRotation;

                MeshRenderer newMeshRenderer = newCopy.AddComponent<MeshRenderer> ();

                MeshFilter newMeshFilter = newCopy.AddComponent<MeshFilter> ();

                Mesh newMesh = new Mesh ();

                mainSkinnedMeshRenderer [i].BakeMesh (newMesh);

                newMeshFilter.mesh = newMesh;

                if (useNewMaterial) {
                    Material [] allMats = mainSkinnedMeshRenderer [i].materials;

                    int materialsLength = allMats.Length;

                    for (int m = 0; m < materialsLength; m++) {
                        allMats [m] = duplicateMaterial;
                    }

                    newMeshRenderer.materials = allMats;
                }

                newCopy.AddComponent<MeshCollider> ();

                if (useEventToSentDuplicatedMeshes) {
                    eventToSentDuplicatedMeshes.Invoke (newCopy);
                }

                newList.Add (newCopy);
            }
        }

        GameObject newDuplicateTransform = (GameObject)Instantiate (duplicateBaseGameObject, meshTransform.position, meshTransform.rotation);

        for (int i = 0; i < newList.Count; i++) {
            newList [i].transform.SetParent (newDuplicateTransform.transform);
        }


        eventOnCreateDuplicate.Invoke ();

        if (!newDuplicateTransform.activeSelf) {
            newDuplicateTransform.SetActive (true);
        }
    }
}
