using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof (weaponTransformInfoSystem))]
public class weaponTransformInfoSystemEditor : Editor
{
    SerializedProperty mainWeaponTransformData;
    SerializedProperty weaponID;
    SerializedProperty weaponsTransformInfoList;
    SerializedProperty newListToAddName;
    SerializedProperty newListToAddParent;

    SerializedProperty numberOfElements;

    SerializedProperty mainWeaponTransform;

    SerializedProperty mainWeaponPositionsParentThirdPerson;

    SerializedProperty mainWeaponPositionChildsThirdPerson;

    SerializedProperty currentOffsetParentThirdPerson;

    //	SerializedProperty mainWeaponPositionsParentFirstPerson;
    //
    //	SerializedProperty mainWeaponPositionChildsFirstPerson;
    //
    //	SerializedProperty currentOffsetParentFirstPerson;

    weaponTransformInfoSystem manager;

    GUIStyle buttonStyle = new GUIStyle ();
    GUIStyle bigButtonStyle = new GUIStyle ();

    GUIStyle sectionStyle = new GUIStyle ();

    bool expanded;

    void OnEnable ()
    {
        mainWeaponTransformData = serializedObject.FindProperty ("mainWeaponTransformData");
        weaponID = serializedObject.FindProperty ("weaponID");
        weaponsTransformInfoList = serializedObject.FindProperty ("weaponsTransformInfoList");
        newListToAddName = serializedObject.FindProperty ("newListToAddName");
        newListToAddParent = serializedObject.FindProperty ("newListToAddParent");
        numberOfElements = serializedObject.FindProperty ("numberOfElements");

        mainWeaponTransform = serializedObject.FindProperty ("mainWeaponTransform");

        mainWeaponPositionsParentThirdPerson = serializedObject.FindProperty ("mainWeaponPositionsParentThirdPerson");

        mainWeaponPositionChildsThirdPerson = serializedObject.FindProperty ("mainWeaponPositionChildsThirdPerson");

        currentOffsetParentThirdPerson = serializedObject.FindProperty ("currentOffsetParentThirdPerson");


        //		mainWeaponPositionsParentFirstPerson = serializedObject.FindProperty ("mainWeaponPositionsParentFirstPerson");
        //
        //		mainWeaponPositionChildsFirstPerson = serializedObject.FindProperty ("mainWeaponPositionChildsFirstPerson");
        //
        //		currentOffsetParentFirstPerson = serializedObject.FindProperty ("currentOffsetParentFirstPerson");


        manager = (weaponTransformInfoSystem)target;
    }

    public override void OnInspectorGUI ()
    {
        EditorGUI.BeginChangeCheck ();

        GUILayout.BeginVertical (GUILayout.Height (30));

        EditorGUILayout.Space ();

        buttonStyle = new GUIStyle (GUI.skin.button);

        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.fontSize = 12;

        bigButtonStyle = new GUIStyle (GUI.skin.button);

        bigButtonStyle.fontStyle = FontStyle.Bold;
        bigButtonStyle.fontSize = 15;


        GUILayout.BeginVertical ("Main Settings", "window");
        EditorGUILayout.Space ();

        EditorGUILayout.PropertyField (mainWeaponTransformData);
        EditorGUILayout.PropertyField (weaponID);
        EditorGUILayout.PropertyField (numberOfElements);
        GUILayout.EndVertical ();

        EditorGUILayout.Space ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Weapon Transform Info List", "window");

        EditorGUILayout.Space ();

        showWeaponsTransformInfoList (weaponsTransformInfoList);

        EditorGUILayout.Space ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Add New List Settings", "window");
        EditorGUILayout.PropertyField (newListToAddName);
        EditorGUILayout.PropertyField (newListToAddParent);

        GUILayout.EndVertical ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Add New List")) {
            manager.addNewList ();
        }
        GUILayout.EndVertical ();

        EditorGUILayout.Space ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Weapon Position Reference Settings", "window");

        EditorGUILayout.Space ();

        EditorGUILayout.PropertyField (mainWeaponTransform);

        EditorGUILayout.Space ();

        showSimpleList (mainWeaponPositionsParentThirdPerson);

        EditorGUILayout.Space ();

        showSimpleList (mainWeaponPositionChildsThirdPerson);

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Get Main Weapon Position Childs Third Person")) {
            manager.getMainWeaponPositionChildsThirdPerson ();
        }
        GUILayout.EndVertical ();

        EditorGUILayout.Space ();

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Weapon Offset Settings", "window");

        EditorGUILayout.Space ();

        EditorGUILayout.PropertyField (currentOffsetParentThirdPerson);

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Set Current Offset Parent Third Person")) {
            manager.setCurrentOffsetParentOnThirdPerson ();
        }

        //		EditorGUILayout.Space ();
        //
        //		EditorGUILayout.Space ();
        //
        //		EditorGUILayout.Space ();
        //
        //		showSimpleList (mainWeaponPositionsParentFirstPerson);
        //
        //		EditorGUILayout.Space ();
        //
        //		showSimpleList (mainWeaponPositionChildsFirstPerson);
        //
        //		EditorGUILayout.Space ();
        //
        //		if (GUILayout.Button ("Get Main Weapon Position Childs First Person")) {
        //			manager.getMainWeaponPositionChildsFirstPerson ();
        //		}
        //
        //		EditorGUILayout.Space ();
        //
        //		EditorGUILayout.PropertyField (currentOffsetParentFirstPerson);
        //
        EditorGUILayout.Space ();
        //
        //		if (GUILayout.Button ("Set Current Offset Parent First Person")) {
        //			manager.setCurrentOffsetParentOnFirstPerson ();
        //		}
        //
        EditorGUILayout.Space ();

        if (GUILayout.Button ("Adjust Draw/Keep Position To Weapon Position")) {
            manager.adjustDrawKeepPositionToWeaponPosition ();
        }
        GUILayout.EndVertical ();

        EditorGUILayout.Space ();

        GUILayout.EndVertical ();

        EditorGUILayout.Space ();


        showSectionTitle ("IK WEAPON POSITIONS EDITOR");


        EditorGUILayout.Space ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("\nCopy Positions\n", bigButtonStyle)) {
            manager.copyTransformValuesToBuffer ();
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("\nPaste Positions\n", bigButtonStyle)) {

            manager.pasteTransformValuesToBuffer ();

            EditorUtility.SetDirty (target);
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("\nClear Positions On Scriptable\n", bigButtonStyle)) {
            manager.cleanPositionsOnScriptable ();
        }

        EditorGUILayout.Space ();

        EditorGUILayout.Space ();

        if (EditorGUI.EndChangeCheck ()) {
            serializedObject.ApplyModifiedProperties ();

            Repaint ();
        }
    }

    void showWeaponsTransformInfoList (SerializedProperty list)
    {
        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide " + list.displayName, buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {
            GUILayout.BeginVertical ("box");
            EditorGUILayout.Space ();
            GUILayout.Label ("Number Of List: \t" + list.arraySize);

            EditorGUILayout.Space ();

            GUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Add")) {
                manager.addNewEmptyList ();
            }
            if (GUILayout.Button ("Clear")) {
                list.ClearArray ();
            }
            GUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            GUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Expand All")) {
                for (int i = 0; i < list.arraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = true;
                }
            }
            if (GUILayout.Button ("Collapse All")) {
                for (int i = 0; i < list.arraySize; i++) {
                    list.GetArrayElementAtIndex (i).isExpanded = false;
                }
            }
            GUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            for (int i = 0; i < list.arraySize; i++) {
                expanded = false;
                GUILayout.BeginHorizontal ();
                GUILayout.BeginHorizontal ("box");
                EditorGUILayout.Space ();
                if (i < list.arraySize && i >= 0) {
                    EditorGUILayout.BeginVertical ();

                    EditorGUILayout.PropertyField (list.GetArrayElementAtIndex (i), false);
                    if (list.GetArrayElementAtIndex (i).isExpanded) {
                        showWeaponsTransformInfoListElement (list.GetArrayElementAtIndex (i));
                        expanded = true;
                    }
                    GUILayout.EndVertical ();
                }
                GUILayout.EndHorizontal ();
                if (expanded) {
                    GUILayout.BeginVertical ();
                } else {
                    GUILayout.BeginHorizontal ();
                }
                if (GUILayout.Button ("x")) {
                    list.DeleteArrayElementAtIndex (i);
                }
                if (GUILayout.Button ("v")) {
                    if (i >= 0) {
                        list.MoveArrayElement (i, i + 1);
                    }
                }
                if (GUILayout.Button ("^")) {
                    if (i < list.arraySize) {
                        list.MoveArrayElement (i, i - 1);
                    }
                }
                if (expanded) {
                    GUILayout.EndVertical ();
                } else {
                    GUILayout.EndHorizontal ();
                }
                GUILayout.EndHorizontal ();
            }

            EditorGUILayout.Space ();

            GUILayout.EndVertical ();
        }
    }

    void showWeaponsTransformInfoListElement (SerializedProperty list)
    {
        GUILayout.BeginVertical ("box");
        EditorGUILayout.PropertyField (list.FindPropertyRelative ("Name"));

        EditorGUILayout.Space ();

        GUILayout.BeginVertical ("Weapons Transform Info List", "window");
        showSimpleList (list.FindPropertyRelative ("objectTransformInfoList"));
        GUILayout.EndVertical ();

        GUILayout.EndVertical ();
    }

    void showSimpleList (SerializedProperty list)
    {
        EditorGUILayout.Space ();

        if (GUILayout.Button ("Show/Hide " + list.displayName, buttonStyle)) {
            list.isExpanded = !list.isExpanded;
        }

        EditorGUILayout.Space ();

        if (list.isExpanded) {
            EditorGUILayout.Space ();

            GUILayout.Label ("Number Of Transforms: \t" + list.arraySize);

            EditorGUILayout.Space ();

            GUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Add")) {
                list.arraySize++;
            }
            if (GUILayout.Button ("Clear")) {
                list.arraySize = 0;
            }
            GUILayout.EndHorizontal ();

            EditorGUILayout.Space ();

            for (int i = 0; i < list.arraySize; i++) {
                GUILayout.BeginHorizontal ();
                if (i < list.arraySize && i >= 0) {
                    EditorGUILayout.PropertyField (list.GetArrayElementAtIndex (i), new GUIContent ("", null, ""), false);
                }
                GUILayout.BeginHorizontal ();
                if (GUILayout.Button ("x")) {
                    GKC_Utils.removeElementFromEditorList (list, i);

                    return;
                }
                GUILayout.EndHorizontal ();
                GUILayout.EndHorizontal ();
            }
        }
    }

    void showSectionTitle (string sectionTitle)
    {
        sectionStyle.fontStyle = FontStyle.Bold;
        sectionStyle.fontSize = 25;
        sectionStyle.alignment = TextAnchor.MiddleCenter;

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        EditorGUILayout.LabelField (sectionTitle, sectionStyle);

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();
        EditorGUILayout.Space ();
    }
}
#endif