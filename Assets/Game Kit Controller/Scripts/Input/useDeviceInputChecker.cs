using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class useDeviceInputChecker : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool useDeviceInputEnabled = true;

    public string customDeviceInputActionName;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    [Space]
    [Header ("Components")]
    [Space]

    public usingDevicesSystem mainUsingDevicesSystem;


    public void checkUseDevice ()
    {
        if (!useDeviceInputEnabled) {
            return;
        }

        GameObject currentObjectToUse = mainUsingDevicesSystem.getCurrentObjectToUse ();

        if (currentObjectToUse != null) {
            if (showDebugPrint) {
                print ("currentObjectToUse " + currentObjectToUse.name);
            }

            deviceStringAction currentDeviceStringAction = currentObjectToUse.GetComponent<deviceStringAction> ();

            if (currentDeviceStringAction != null) {
                if (showDebugPrint) {
                    print ("device string action component found, check for custom input " +
                        currentDeviceStringAction.useCustomDeviceInputActionName + " " +
                        currentDeviceStringAction.customDeviceInputActionName);
                }

                if (currentDeviceStringAction.useCustomDeviceInputActionName) {
                    if (currentDeviceStringAction.customDeviceInputActionName.Equals (customDeviceInputActionName)) {
                        if (showDebugPrint) {
                            print ("current device to use with custom input action found, sending use signal " +
                                currentDeviceStringAction.getDeviceName () + " " + customDeviceInputActionName);
                        }

                        mainUsingDevicesSystem.useDevice ();
                    }
                }
            }
        }
    }
}
