using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class remoteEventSocket : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool remoteEventSocketEnabled = true;

    [Space]
    [Header ("Components")]
    [Space]

    public remoteEventSystem mainRemoteEventSystem;


    public remoteEventSystem getMainRemoteEventSystem ()
    {
        if (!remoteEventSocketEnabled) {
            return null;
        }

        return mainRemoteEventSystem;
    }

    public bool isRemoteEventSocketEnabled ()
    {
        return remoteEventSocketEnabled;
    }
}
