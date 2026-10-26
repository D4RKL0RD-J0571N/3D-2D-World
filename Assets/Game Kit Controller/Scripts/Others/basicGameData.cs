using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu (fileName = "Basic Game Data", menuName = "GKC/Create Basic Game Data", order = 51)]
public class basicGameData : ScriptableObject
{
    [Header ("Version Number Settings")]
    [Space]

    public string versionNumber = "3.78b";

    public string getVersionNumber ()
    {
        return versionNumber;
    }
}