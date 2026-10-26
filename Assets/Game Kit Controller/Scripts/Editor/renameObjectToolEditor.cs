using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.IO;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;

public class renameObjectToolEditor : EditorWindow
{
    GUISkin guiSkin;
    Rect windowRect = new Rect ();
    Event currentEvent;

    Vector2 rectSize = new Vector2 (600, 600);

    float timeToBuild = 0.2f;
    float timer;

    GUIStyle style = new GUIStyle ();

    GUIStyle labelStyle = new GUIStyle ();

    float windowHeightPercentage = 0.45f;

    Vector2 screenResolution;

    float minHeight = 300;

    Vector2 scrollPos1;

    float maxLayoutWidht = 220;

    Vector2 previousRectSize;



    public bool isRegularObject;

    public bool isWeapon;

    public bool isMeleeWeapon;

    public string currentObjectName;

    public string newObjectName;

    public bool isAmmo;

    public bool isArmorCloth;

    bool objectRenamed;


    [MenuItem ("Game Kit Controller/Rename Object Tool", false, 205)]


    public static void openRenameObjectToolEditor ()
    {
        GetWindow<renameObjectToolEditor> ();
    }

    void OnEnable ()
    {
        screenResolution = new Vector2 (Screen.currentResolution.width, Screen.currentResolution.height);

        float windowHeight = screenResolution.y * windowHeightPercentage;

        windowHeight = Mathf.Clamp (windowHeight, minHeight, screenResolution.y);

        rectSize = new Vector2 (500, windowHeight);

        resetCreatorValues ();
    }

    void OnDisable ()
    {
        resetCreatorValues ();
    }

    void resetCreatorValues ()
    {
        newObjectName = "";

        currentObjectName = "";

        isRegularObject = false;

        isWeapon = false;

        isMeleeWeapon = false;

        objectRenamed = false;

        isAmmo = false;

        isArmorCloth = false;

        Debug.Log ("Object window closed");
    }

    void OnGUI ()
    {
        if (!guiSkin) {
            guiSkin = Resources.Load ("GUI") as GUISkin;
        }
        GUI.skin = guiSkin;

        this.minSize = rectSize;

        this.titleContent = new GUIContent ("Objects", null, "Rename Object");

        GUILayout.BeginVertical ("Rename Object Tool", "window");

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        windowRect = GUILayoutUtility.GetLastRect ();

        windowRect.width = this.maxSize.x;

        GUILayout.BeginHorizontal ();

        EditorGUILayout.HelpBox ("", MessageType.Info);

        style = new GUIStyle (EditorStyles.helpBox);
        style.richText = true;

        style.fontStyle = FontStyle.Bold;
        style.fontSize = 17;

        EditorGUILayout.LabelField ("Write the current Object name and the new Object name, " +
            "along if the searched Object is regular or a weapon or not, and if so, if it is melee or not.\n", style);
        GUILayout.EndHorizontal ();

        EditorGUILayout.Space ();

        GUILayout.BeginHorizontal ();
        GUILayout.Label ("Window Height", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));

        if (previousRectSize != rectSize) {
            previousRectSize = rectSize;

            this.maxSize = rectSize;
        }

        rectSize.y = EditorGUILayout.Slider (rectSize.y, minHeight, screenResolution.y, GUILayout.ExpandWidth (true));

        GUILayout.EndHorizontal ();

        EditorGUILayout.Space ();

        scrollPos1 = EditorGUILayout.BeginScrollView (scrollPos1, false, false);

        EditorGUILayout.Space ();

        GUILayout.BeginHorizontal ();
        GUILayout.Label ("Current Object Name", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
        currentObjectName = (string)EditorGUILayout.TextField (currentObjectName, GUILayout.ExpandWidth (true));
        GUILayout.EndHorizontal ();

        EditorGUILayout.Space ();

        GUILayout.BeginHorizontal ();
        GUILayout.Label ("New Object Name", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
        newObjectName = (string)EditorGUILayout.TextField (newObjectName, GUILayout.ExpandWidth (true));
        GUILayout.EndHorizontal ();

        EditorGUILayout.Space ();

        GUILayout.BeginHorizontal ();
        GUILayout.Label ("Is Regular Object", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
        isRegularObject = (bool)EditorGUILayout.Toggle (isRegularObject, GUILayout.ExpandWidth (true));
        GUILayout.EndHorizontal ();

        EditorGUILayout.Space ();

        if (!isRegularObject) {
            GUILayout.BeginHorizontal ();
            GUILayout.Label ("Is Weapon", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
            isWeapon = (bool)EditorGUILayout.Toggle (isWeapon, GUILayout.ExpandWidth (true));
            GUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            if (isWeapon) {
                GUILayout.BeginHorizontal ();
                GUILayout.Label ("Is Melee Weapon", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
                isMeleeWeapon = (bool)EditorGUILayout.Toggle (isMeleeWeapon, GUILayout.ExpandWidth (true));
                GUILayout.EndHorizontal ();

                EditorGUILayout.Space ();

            } else {

                GUILayout.BeginHorizontal ();
                GUILayout.Label ("Is Ammo", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
                isAmmo = (bool)EditorGUILayout.Toggle (isAmmo, GUILayout.ExpandWidth (true));
                GUILayout.EndHorizontal ();

                EditorGUILayout.Space ();

                GUILayout.BeginHorizontal ();
                GUILayout.Label ("Is Armor Cloth", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
                isArmorCloth = (bool)EditorGUILayout.Toggle (isArmorCloth, GUILayout.ExpandWidth (true));
                GUILayout.EndHorizontal ();

                EditorGUILayout.Space ();
            }
        }

        GUILayout.FlexibleSpace ();

        EditorGUILayout.EndScrollView ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Rename Object")) {
            renameObject ();
        }

        if (GUILayout.Button ("Cancel")) {
            this.Close ();
        }

        GUILayout.EndVertical ();
    }

    void renameObject ()
    {
        if (currentObjectName == "" || newObjectName == "") {
            return;
        }

        bool mainInventoryManagerLocated = false;

        inventoryListManager mainInventoryListManager = inventoryListManager.Instance;

        mainInventoryManagerLocated = mainInventoryListManager != null;

        if (!mainInventoryManagerLocated) {
            GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (inventoryListManager.getMainManagerName (), typeof (inventoryListManager), true);

            mainInventoryListManager = inventoryListManager.Instance;

            mainInventoryManagerLocated = (mainInventoryListManager != null);
        }

        if (!mainInventoryManagerLocated) {
            mainInventoryListManager = GKC_Utils.findObjectByComponent<inventoryListManager> ();

            mainInventoryManagerLocated = mainInventoryListManager != null;
        }

        if (mainInventoryManagerLocated) {
            mainInventoryListManager.renameInventoryObject (currentObjectName, newObjectName);
        }

        if (isWeapon) {
            if (isMeleeWeapon) {
                meleeWeaponsGrabbedManager [] newMeleeWeaponsGrabbedManagerList = GKC_Utils.findObjectsByComponent<meleeWeaponsGrabbedManager> (true);

                foreach (meleeWeaponsGrabbedManager currentMeleeWeaponsGrabbedManager in newMeleeWeaponsGrabbedManagerList) {
                    currentMeleeWeaponsGrabbedManager.renameWeapon (currentObjectName, newObjectName);
                }

                //List<meleeWeaponsGrabbedManager> newMeleeWeaponsGrabbedManagerList = GKC_Utils.FindObjectsOfTypeAll<meleeWeaponsGrabbedManager> ();

                //if (newMeleeWeaponsGrabbedManagerList != null) {
                //    for (var i = 0; i < newMeleeWeaponsGrabbedManagerList.Count; i++) {
                //        newMeleeWeaponsGrabbedManagerList [i].renameWeapon (currentObjectName, newObjectName);
                //    }
                //}
            } else {
                playerWeaponsManager [] newPlayerWeaponsManagerList = GKC_Utils.findObjectsByComponent<playerWeaponsManager> (true);

                foreach (playerWeaponsManager currentPlayerWeaponsManager in newPlayerWeaponsManagerList) {
                    currentPlayerWeaponsManager.renameWeapon (currentObjectName, newObjectName);
                }

                //List<playerWeaponsManager> newPlayerWeaponsManagerList = GKC_Utils.FindObjectsOfTypeAll<playerWeaponsManager> ();

                //if (newPlayerWeaponsManagerList != null) {
                //    for (var i = 0; i < newPlayerWeaponsManagerList.Count; i++) {
                //        newPlayerWeaponsManagerList [i].renameWeapon (currentObjectName, newObjectName);
                //    }
                //}
            }
        } else {
            if (isAmmo) {
                playerWeaponsManager [] newPlayerWeaponsManagerList = GKC_Utils.findObjectsByComponent<playerWeaponsManager> (true);

                foreach (playerWeaponsManager currentPlayerWeaponsManager in newPlayerWeaponsManagerList) {
                    currentPlayerWeaponsManager.renameWeaponAmmo (currentObjectName, newObjectName);
                }

                //List<playerWeaponsManager> newPlayerWeaponsManagerList = GKC_Utils.FindObjectsOfTypeAll<playerWeaponsManager> ();

                //if (newPlayerWeaponsManagerList != null) {
                //    for (var i = 0; i < newPlayerWeaponsManagerList.Count; i++) {
                //        newPlayerWeaponsManagerList [i].renameWeaponAmmo (currentObjectName, newObjectName);
                //    }
                //}
            } else if (isArmorCloth) {
                //check on the characters customization component on characters on scene

                characterCustomizationManager [] characterCustomizationManagerList = GKC_Utils.findObjectsByComponent<characterCustomizationManager> (true);

                foreach (characterCustomizationManager currentCharacterCustomizationManager in characterCustomizationManagerList) {
                    currentCharacterCustomizationManager.renamePiece (currentObjectName, newObjectName);
                }

                //List<characterCustomizationManager> characterCustomizationManagerList = GKC_Utils.FindObjectsOfTypeAll<characterCustomizationManager> ();

                //if (characterCustomizationManagerList != null) {
                //    for (var i = 0; i < characterCustomizationManagerList.Count; i++) {
                //        characterCustomizationManagerList [i].renamePiece (currentObjectName, newObjectName);
                //    }
                //}

                //check on the scriptable objects for full armor list and for the armor part it self

                inventoryCharacterCustomizationSystem [] inventoryCharacterCustomizationSystemList = GKC_Utils.findObjectsByComponent<inventoryCharacterCustomizationSystem> (true);

                foreach (inventoryCharacterCustomizationSystem currentInventoryCharacterCustomizationSystem in inventoryCharacterCustomizationSystemList) {
                    currentInventoryCharacterCustomizationSystem.renameArmorClothObjectByName (currentObjectName, newObjectName);
                }

                //List<inventoryCharacterCustomizationSystem> inventoryCharacterCustomizationSystemList = GKC_Utils.FindObjectsOfTypeAll<inventoryCharacterCustomizationSystem> ();

                //if (inventoryCharacterCustomizationSystemList != null) {
                //    for (var i = 0; i < inventoryCharacterCustomizationSystemList.Count; i++) {
                //        inventoryCharacterCustomizationSystemList [i].renameArmorClothObjectByName (currentObjectName, newObjectName);
                //    }
                //}
            }
        }

        inventoryManager [] newInventoryManagerList = GKC_Utils.findObjectsByComponent<inventoryManager> (true);

        foreach (inventoryManager currentInventoryManager in newInventoryManagerList) {
            currentInventoryManager.getInventoryListManagerList ();

            currentInventoryManager.setInventoryObjectListNames ();
        }


        //List<inventoryManager> newInventoryManagerList = GKC_Utils.FindObjectsOfTypeAll<inventoryManager> ();

        //if (newInventoryManagerList != null) {
        //    for (var i = 0; i < newInventoryManagerList.Count; i++) {
        //        newInventoryManagerList [i].getInventoryListManagerList ();

        //        newInventoryManagerList [i].setInventoryObjectListNames ();
        //    }
        //}

        if (mainInventoryManagerLocated) {
            mainInventoryListManager.renameCraftingObjectByName (currentObjectName, newObjectName);
        }

        objectRenamed = true;
    }

    void Update ()
    {
        if (objectRenamed) {
            if (timer < timeToBuild) {
                timer += 0.01f;

                if (timer > timeToBuild) {
                    timer = 0;

                    this.Close ();
                }
            }
        }
    }
}
#endif