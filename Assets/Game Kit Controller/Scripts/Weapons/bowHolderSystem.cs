using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class bowHolderSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public int bowWeaponID;

    public bool canUseMultipleArrowType;

    public List<int> bowWeaponIDList = new List<int> ();

    [Space]
    [Header ("Events Settings")]
    [Space]

    public UnityEvent eventOnLoadArrow;

    public UnityEvent eventOnFireArrow;

    [Space]

    public UnityEvent eventOnStartAim;

    public UnityEvent eventOnEndAim;

    [Space]

    public UnityEvent eventOnCancelBowAction;

    [Space]

    public UnityEvent eventOnMaxPullBowMultiplierStart;

    public UnityEvent eventOnMaxPullBowMultiplierEnd;

    [Space]
    [Header ("Debug Settings")]
    [Space]

    public bool showDebugPrint;


    public void checkEventOnLoadArrow ()
    {
        eventOnLoadArrow.Invoke ();

        if (showDebugPrint) {
            print ("Event On Load Arrow");
        }
    }

    public void checkEventOnFireArrow ()
    {
        eventOnFireArrow.Invoke ();

        if (showDebugPrint) {
            print ("Event On Fire Arrow");
        }
    }

    public void checkEventOnStartAim ()
    {
        eventOnStartAim.Invoke ();

        if (showDebugPrint) {
            print ("Event On Start Aim");
        }
    }

    public void checkEventOnEndAim ()
    {
        eventOnEndAim.Invoke ();

        if (showDebugPrint) {
            print ("Event On End Aim");
        }
    }

    public void checkEventOnCancelBowAction ()
    {
        eventOnCancelBowAction.Invoke ();

        if (showDebugPrint) {
            print ("Event On Cancel Bow Action");
        }
    }

    public void checkEventOnMaxPullBowMultiplierStart ()
    {
        eventOnMaxPullBowMultiplierStart.Invoke ();

        if (showDebugPrint) {
            print ("Event On Max Pull Bow Multiplier Start");
        }
    }

    public void checkEventOnMaxPullBowMultiplierEnd ()
    {
        eventOnMaxPullBowMultiplierEnd.Invoke ();

        if (showDebugPrint) {
            print ("Event On Max Pull Bow Multiplier End");
        }
    }

    public int getBowWeaponID ()
    {
        return bowWeaponID;
    }
}
