using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class deviceToControlAtDistance : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool deviceToControlEnabled = true;

    public GameObject deviceToControlGameObject;


    public bool isDeviceToControlEnabled ()
    {
        return deviceToControlEnabled;
    }

    public GameObject getDeviceToControlGameObject ()
    {
        if (deviceToControlGameObject == null) {
            deviceToControlGameObject = gameObject;
        }

        return deviceToControlGameObject;
    }
}
