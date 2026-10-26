using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[System.Serializable]
public class scanElementInfo : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public elementInfo dataObject;
    public GameObject dataGameObject;

    [Space]

    public bool useCustomScanSpeed;
    public float customScanSpeed;

    [Space]
    public string databaseFileName = "dataBase.txt";

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public bool objectScanned;
    public bool showDebugInfo;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventFunction;
    public UnityEvent eventFunction = new UnityEvent ();

    //this script is to add info to an object, so the player can scan it and read that info.
    //the script is prepared to make database with all the scannable objects, and it will be completed in the next version


    string getDataPath ()
    {
        return Application.persistentDataPath + "/" + databaseFileName;
    }

    //save this element in the database
    public void saveElement ()
    {
        if (!objectScanned) {
            //get the number of the database, to add this element with an auto incremental id
            List<elementInfo> infoListToSave = getList ();

            int index = infoListToSave.Count;

            SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

            if (getFileContent (getDataPath (), out saveData)) {
                infoListToSave = saveData.infoList;
            }

            //increase the id in 1
            dataObject.id = index + 1;

            infoListToSave.Add (dataObject);

            saveData.infoList = infoListToSave;

            string jsonToSave = JsonUtility.ToJson (saveData, true);

            saveFileContent (getDataPath (), jsonToSave);

            //element added
            objectScanned = true;

            //uncomment to see all the database content
            //readDataBase ();
        }
    }

    List<elementInfo> getList ()
    {
        List<elementInfo> list = new List<elementInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (getDataPath (), out saveData)) {
            list = saveData.infoList;
        }

        return list;
    }

    //just a function to see if the database is correctly saved
    void readDataBase ()
    {
        List<elementInfo> list = getList ();

        if (showDebugPrint) {
            for (int i = 0; i < list.Count; i++) {
                print (list [i].id + " " + list [i].info);
            }
        }
    }

    //remove this element from the database
    public void removeELement ()
    {
        List<elementInfo> list = new List<elementInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (getDataPath (), out saveData)) {
            list = saveData.infoList;
        }

        for (int i = 0; i < list.Count; i++) {
            if (list [i].id == dataObject.id) {
                list.RemoveAt (i);

                objectScanned = false;

                dataObject.id = 0;

                saveList (list);

                return;
            }
        }
    }

    //save the list removing the empty element when an object is removed
    void saveList (List<elementInfo> completList)
    {
        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        saveData.infoList = completList;

        string jsonToSave = JsonUtility.ToJson (saveData, true);

        saveFileContent (getDataPath (), jsonToSave);

        readDataBase ();
    }

    public bool isScanned ()
    {
        return dataObject.read;
    }

    public void scanObject ()
    {
        dataObject.read = true;

        if (useEventFunction) {
            if (eventFunction.GetPersistentEventCount () > 0) {
                eventFunction.Invoke ();
            }
        }
    }

    bool getFileContent (string path, out SaveSlotListWrapper saveData)
    {
        bool fileResult = false;

        saveData = new SaveSlotListWrapper ();

        if (File.Exists (path)) {
            try {
                string json = File.ReadAllText (path);

                if (string.IsNullOrWhiteSpace (json)) {
                    if (showDebugInfo) {
                        Debug.Log ("Save File located but empty" + path);
                        Debug.Log ("\n\n");
                    }
                } else {
                    saveData = JsonUtility.FromJson<SaveSlotListWrapper> (json);

                    if (saveData == null) {
                        saveData = new SaveSlotListWrapper ();
                    }

                    if (saveData.infoList == null) {
                        saveData.infoList = new List<elementInfo> ();
                    }

                    fileResult = true;

                    if (showDebugInfo) {
                        Debug.Log ("Save File located " + path);
                        Debug.Log ("\n\n");
                    }
                }
            }

            catch (Exception ex) {
                Debug.LogError (
                    $"Error loading save file {path}\n{ex}"
                );

                saveData = new SaveSlotListWrapper ();

                if (saveData.infoList == null) {
                    saveData.infoList = new List<elementInfo> ();
                }
            }
        } else {
            if (showDebugInfo) {
                Debug.Log ("Save File not located " + path);
                Debug.Log ("\n\n");
            }
        }

        return fileResult;
    }

    void saveFileContent (string path, string jsonToSave)
    {
        try {
            string dir = Path.GetDirectoryName (path);

            if (!string.IsNullOrEmpty (dir)) {
                Directory.CreateDirectory (dir);
            }

            File.WriteAllText (path, jsonToSave, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) {
            Debug.LogError (
                $"Error saving file {path}\n{ex}"
            );
        }
    }


    [System.Serializable]
    public class SaveSlotListWrapper
    {
        public List<elementInfo> infoList = new List<elementInfo> ();
    }

    //every object has a name, the info, a bool to check if it is already read and a id for the database
    [System.Serializable]
    public class elementInfo
    {
        public string name;
        [TextArea (3, 10)]
        public string info;
        public bool read;
        public int id;
    }
}