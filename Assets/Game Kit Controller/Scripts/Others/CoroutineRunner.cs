using System.Collections;
using UnityEngine;

public class CoroutineRunner : MonoBehaviour
{
    static CoroutineRunner instance;

    public static CoroutineRunner Instance
    {
        get
        {
            if (instance == null) {
                GameObject obj = new GameObject ("[CoroutineRunner]");
                Object.DontDestroyOnLoad (obj);

                instance = obj.AddComponent<CoroutineRunner> ();
            }

            return instance;
        }
    }

    public static Coroutine Run (IEnumerator routine)
    {
        if (routine == null)
            return null;

        return Instance.StartCoroutine (routine);
    }

    public static void Stop (Coroutine routine)
    {
        if (instance == null || routine == null)
            return;

        instance.StopCoroutine (routine);
    }

    public static void Stop (IEnumerator routine)
    {
        if (instance == null || routine == null)
            return;

        instance.StopCoroutine (routine);
    }

    public static void StopAll ()
    {
        if (instance == null)
            return;

        instance.StopAllCoroutines ();
    }
}