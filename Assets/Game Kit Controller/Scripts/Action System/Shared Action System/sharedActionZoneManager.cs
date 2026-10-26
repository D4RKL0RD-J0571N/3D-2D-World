using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static sharedActionSystem;

public class sharedActionZoneManager : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool sharedActionZonesEnabled = true;

    [Space]
    [Header ("Shared Action Zone Settings")]
    [Space]

    public List<sharedActionZone> sharedActionZoneList = new List<sharedActionZone> ();


    public bool isSharedActionZonesEnabled ()
    {
        return sharedActionZonesEnabled;
    }

    public List<sharedActionZone> getSharedActionZoneList ()
    {
        return sharedActionZoneList;
    }

    public void setSharedActionZonesEnabledState (bool state)
    {
        sharedActionZonesEnabled = state;
    }

    public void getAllSharedActionZonesOnScene ()
    {
        sharedActionZoneList.Clear ();

        sharedActionZone [] newSharedActionZoneList = GKC_Utils.findObjectsByComponent<sharedActionZone> ();

        foreach (sharedActionZone currentSharedActionZone in newSharedActionZoneList) {
            if (!sharedActionZoneList.Contains (currentSharedActionZone)) {
                sharedActionZoneList.Add (currentSharedActionZone);
            }
        }

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Shared Action Manager", gameObject);
    }
}
