using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Events;

public class onEnableCheckSystem : MonoBehaviour
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

    public UnityEvent eventOnEnabled;

    void OnEnable ()
    {
        if (checkEnabled) {
            eventOnEnabled.Invoke ();

            if (showDebugPrint) {
                print ("on enable function " + gameObject.name);
            }
        }
    }
}
