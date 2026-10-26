using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class zeroGravityRoomObjectChecker : MonoBehaviour
{
    [Header ("Main Setting")]
    [Space]

    public bool checkGravityRoomStatesEnabled = true;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool objectInsideGravityRoom;

    public zeroGravityRoomSystem currentZeroGravityRoom;


    public void setCurrentZeroGravityRoom (zeroGravityRoomSystem gravityRoom)
    {
        if (!checkGravityRoomStatesEnabled) {
            return;
        }

        currentZeroGravityRoom = gravityRoom;

        objectInsideGravityRoom = currentZeroGravityRoom != null;
    }

    public void checkAddObjectToCurrentRoom (GameObject newObject)
    {
        if (!checkGravityRoomStatesEnabled) {
            return;
        }

        if (objectInsideGravityRoom) {
            currentZeroGravityRoom.addObjectToRoom (newObject);
        }
    }
}
