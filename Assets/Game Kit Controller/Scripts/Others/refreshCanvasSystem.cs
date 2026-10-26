using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class refreshCanvasSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool refreshCanvasOnStart;

    public float refreshWaitTime = 0.2f;

    public bool showDebugValues;

    [Space]
    [Header ("Components")]
    [Space]

    public RectTransform mainRectTransformToRefresh;


    void Start ()
    {
        if (refreshCanvasOnStart) {
            if (showDebugValues) {
                Debug.Log ("screen values on start " + Screen.width + "x" + Screen.height);
            }

            refreshCanvas ();
        }
    }

    public void refreshCanvas ()
    {
        StartCoroutine (refreshCanvasCoroutine ());
    }

    IEnumerator refreshCanvasCoroutine ()
    {
        if (refreshWaitTime > 0) {
            yield return new WaitForSeconds (refreshWaitTime);
        } else {
            yield return new WaitForEndOfFrame ();
        }

        Canvas.ForceUpdateCanvases ();

        if (mainRectTransformToRefresh == null) {
            mainRectTransformToRefresh = GetComponent<RectTransform> ();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate (mainRectTransformToRefresh);

        if (showDebugValues) {
            Debug.Log ("screen values after refresh " + Screen.width + "x" + Screen.height);
        }
    }
}
