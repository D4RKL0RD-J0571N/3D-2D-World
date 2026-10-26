using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class simpleGUILayoutStateManager : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool setStateOnStart;
    public bool stateOnStart;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    void Start ()
    {
        if (setStateOnStart) {
            setGUILayoutState (stateOnStart);
        }
    }


    public void setGUILayoutState (bool state)
    {
        MonoBehaviour [] MonoBehaviourList = GKC_Utils.findObjectsByComponent<MonoBehaviour> ();

        foreach (MonoBehaviour currentMonoBehaviour in MonoBehaviourList) {
            if (currentMonoBehaviour.useGUILayout != state) {
                currentMonoBehaviour.useGUILayout = state;
            }

            if (showDebugPrint) {
                print (currentMonoBehaviour.name + "  " + currentMonoBehaviour.useGUILayout);
            }
        }

        if (showDebugPrint) {
            print (MonoBehaviourList.Length + " monobehaviors found");
        }
    }
}
