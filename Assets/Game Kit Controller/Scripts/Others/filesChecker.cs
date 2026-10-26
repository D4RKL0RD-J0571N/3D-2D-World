using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class filesChecker : MonoBehaviour
{
    public string prefabsPath = "Assets/Game Kit Controller/Prefabs/Inventory/Usable";

    public bool showObjectListInfoPrint;

    public bool showOnlyUncentererdInfo;

    public bool showUnscaledPrint;

    public bool searchOnSubFolders;

    [Space]
    [Space]

    public bool resetTransformValues;

    public bool resetScaleValues;

    [Space]
    [Space]

    public bool checkForAudioSource;

    public bool showOnlyMP3Source;

    public bool checkSoundName;
    public string soundNameToCheck;

    public void checkPrefabs ()
    {
#if UNITY_EDITOR

        if (!Directory.Exists (prefabsPath)) {
            Debug.Log ("WARNING: " + prefabsPath + " path doesn't exist, make sure the path is from an existing folder in the project");

            return;
        }

        string [] search_results = null;

        if (searchOnSubFolders) {
            search_results = System.IO.Directory.GetFiles (prefabsPath, "*.prefab", System.IO.SearchOption.AllDirectories);
        } else {
            search_results = System.IO.Directory.GetFiles (prefabsPath, "*.prefab");
        }

        print (search_results.Length + " objects found");

        int positionCentered = 0;

        int rotationCentered = 0;

        int scaleCentered = 0;

        int itemsCentered = 0;

        if (search_results.Length > 0) {
            foreach (string file in search_results) {
                //must convert file path to relative-to-unity path (and watch for '\' character between Win/Mac)
                GameObject currentPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath (file, typeof (GameObject)) as GameObject;

                if (currentPrefab != null) {
                    if (showObjectListInfoPrint) {
                        print (currentPrefab.name + " " +
                            currentPrefab.transform.position + " " +
                            currentPrefab.transform.eulerAngles
                            + " " + currentPrefab.transform.localScale);
                    }

                    if (checkForAudioSource) {
                        Component [] sourceList = currentPrefab.GetComponentsInChildren (typeof (AudioSource));
                        foreach (Component c in sourceList) {
                            AudioSource currentSource = c as AudioSource;

                            if (currentSource.clip != null) {
                                string clipPath = UnityEditor.AssetDatabase.GetAssetPath (currentSource.clip);

                                bool showPrintResult = true;

                                if (showOnlyMP3Source) {
                                    if (!clipPath.Contains ("mp3")) {
                                        showPrintResult = false;
                                    }
                                }

                                if (checkSoundName) {
                                    if (!clipPath.Contains (soundNameToCheck)) {
                                        showPrintResult = false;
                                    }

                                }

                                if (showPrintResult) {
                                    print (currentPrefab.name + " " + c.gameObject.name + " " + clipPath);
                                }
                            }

                        }
                    }

                    if (currentPrefab.transform.position != Vector3.zero) {
                        //print ("position not centered");

                        positionCentered++;
                    }

                    if (currentPrefab.transform.rotation != Quaternion.identity) {
                        //print ("rotation not centered");

                        rotationCentered++;
                    }

                    if (currentPrefab.transform.localScale != Vector3.one) {
                        // print ("scale not centered");


                        if (showUnscaledPrint) {
                            print ("SCALE " + currentPrefab.name + " " + currentPrefab.transform.localScale);
                        }

                        scaleCentered++;
                    }

                    if (currentPrefab.transform.position != Vector3.zero ||
                        currentPrefab.transform.rotation != Quaternion.identity ||
                        currentPrefab.transform.localScale != Vector3.one) {
                    } else {
                        itemsCentered++;
                    }

                    if (showOnlyUncentererdInfo) {

                        if (currentPrefab.transform.position != Vector3.zero ||
                        currentPrefab.transform.rotation != Quaternion.identity) {
                            print (currentPrefab.name + " " +
                           currentPrefab.transform.position + " " +
                           currentPrefab.transform.eulerAngles);
                        }
                    }

                    if (resetTransformValues) {
                        currentPrefab.transform.position = Vector3.zero;
                        currentPrefab.transform.rotation = Quaternion.identity;
                    }

                    if (resetScaleValues) {
                        currentPrefab.transform.localScale = Vector3.one;
                    }
                } else {
                    Debug.Log ("WARNING: something went wrong when trying to get the prefab in the path " + file);
                }
            }

            print ("\n\n\n");
            print ("objects position not centered properly " + positionCentered);
            print ("objects rotation not centered properly " + rotationCentered);
            print ("objects scale not centered properly " + scaleCentered);

            print ("objects centered " + itemsCentered);

            if (resetTransformValues) {
                GKC_Utils.refreshAssetDatabase ();
            }
        } else {
            Debug.Log ("Shield prefab not found in path " + prefabsPath);


        }
        // GameObject [] prefabs = Resources.LoadAll<GameObject> ("Game Kit Controller/Prefabs/Inventory/Usables");

#endif
    }
}
