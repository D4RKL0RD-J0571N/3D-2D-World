using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class decalTypeInformation : MonoBehaviour
{
    public string mainManagerName = "Decal Manager";

    public string [] impactDecalList;
    public int impactDecalIndex;
    public string impactDecalName;

    public bool parentScorchOnThisObject;

    public bool decalSurfaceActive = true;

    public bool showDebugPrint;

    public bool showGizmo;
    public Color gizmoLabelColor;

    public decalManager impactDecalManager;

    public void getImpactListInfo ()
    {
        checkGetDecalManager ();

        if (impactDecalManager != null) {
            impactDecalList = new string [impactDecalManager.impactListInfo.Count + 1];

            for (int i = 0; i < impactDecalManager.impactListInfo.Count; i++) {
                string name = impactDecalManager.impactListInfo [i].name;
                impactDecalList [i] = name;
            }

            updateComponent ();
        }
    }

    public void checkGetDecalManager ()
    {
        if (impactDecalManager == null) {
            impactDecalManager = decalManager.Instance;

            bool impactDecalManagerLocated = impactDecalManager != null;

            if (!impactDecalManagerLocated) {
                GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (decalManager.getMainManagerName (), typeof (decalManager), true);

                impactDecalManager = decalManager.Instance;

                impactDecalManagerLocated = impactDecalManager != null;
            }

            if (!impactDecalManagerLocated) {

                impactDecalManager = GKC_Utils.findObjectByComponent<decalManager> ();

                impactDecalManagerLocated = impactDecalManager != null;
            }
        }
    }

    public int getDecalImpactIndex ()
    {
        if (decalSurfaceActive) {
            return impactDecalIndex;
        } else {
            return -1;
        }
    }

    public void setNewDecalType (string decalName)
    {
        checkGetDecalManager ();

        if (impactDecalManager != null) {
            int impactDecalListLength = impactDecalList.Length;

            for (int i = 0; i < impactDecalListLength; i++) {
                if (decalName.Equals (impactDecalList [i])) {
                    impactDecalName = decalName;

                    impactDecalIndex = i;

                    if (showDebugPrint) {
                        print ("Setting new decal type to " + decalName);
                    }

                    return;
                }
            }
        }
    }

    public bool isParentScorchOnThisObjectEnabled ()
    {
        return parentScorchOnThisObject;
    }

    public void setDecalSurfaceActiveState (bool state)
    {
        decalSurfaceActive = state;
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);
    }
}
