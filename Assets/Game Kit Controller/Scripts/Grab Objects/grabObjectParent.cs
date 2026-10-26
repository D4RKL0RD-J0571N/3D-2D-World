using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class grabObjectParent : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public GameObject objectToGrab;

    public GameObject getObjectToGrab ()
    {
        return objectToGrab;
    }
}
