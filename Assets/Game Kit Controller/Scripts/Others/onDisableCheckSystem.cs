using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class onDisableCheckSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool checkEnabled;

    [Space]
    [Header ("Main Settings")]
    [Space]

    public bool showDebugPrint;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public UnityEvent eventOnDisabled;

    void OnDisable ()
    {
        if (checkEnabled) {
            eventOnDisabled.Invoke ();

            if (showDebugPrint) {
                print ("on disable function " + gameObject.name);
            }
        }
    }
}
