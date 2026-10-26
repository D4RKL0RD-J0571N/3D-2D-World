using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class sharedActionContent : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool firstCharacterIsPlayer;

    [Space]
    [Header ("Match Settings")]
    [Space]

    public bool adjustPositionToFirstCharacter;
    public bool adjustPositionToSecondCharacter;

    [Space]

    public bool matchPositionUsedOnAction;
    public bool adjustMatchPositionOnFirstCharacter;
    public bool adjustMatchPositionOnSecondCharacter;

    [Space]

    public bool alignMainSharedActionGameObjectToBothCharacters;

    [Space]

    public bool resumeAnyCharacterIfActionFinished;

    [Space]
    [Header ("Probability Settings")]
    [Space]

    public bool useProbabilityToUseAction;
    [Range (0, 100)] public float probabilityToUseAction;


    [Space]
    [Header ("Distance Condition Settings")]
    [Space]

    public bool useMinDistanceToActivateAction;
    public float minDistanceToActivateAction;

    public bool useMinAngleToActivateAction;
    public float minAngleToActivateAction;

    [Space]
    [Header ("Stats Condition Settings")]
    [Space]

    public bool checkCharacterStats;

    public List<sharedActionConditionStatInfo> sharedActionConditionStatInfoList = new List<sharedActionConditionStatInfo> ();

    [Space]
    [Header ("Weapon Condition Settings")]
    [Space]

    public bool checkWeaponsOnFirstCharacter;
    public bool useMeleeWeaponOnFirstCharacter;
    public bool useFireWeaponOnFirstCharacter;
    public bool checkWeaponNameOnFirstCharacter;
    public string weaponNameOnFirstCharacter;


    [Space]

    public bool checkWeaponsOnSecondCharacter;
    public bool useMeleeWeaponOnSecondCharacter;
    public bool useFireWeaponOnSecondCharacter;
    public bool checkWeaponNameOnSecondCharacter;
    public string weaponNameOnSecondCharacter;

    [Space]
    [Header ("Character Dead Condition Settings")]
    [Space]

    public bool stopActionIfAnyCharacterIsDead = true;
    public bool stopActionIfFirstCharacterIsDead;
    public bool stopActionIfSecondCharacterIsDead;

    [Space]
    [Header ("Conditions Failed Settings")]
    [Space]

    public bool useAlternativeSharedActionIfConditionsFailed;
    public string alternativeSharedActionName;

    [Space]
    [Header ("Components")]
    [Space]

    public actionSystem firstCharacterActionSystem;
    public actionSystem secondCharacterActionSystem;


    [System.Serializable]
    public class sharedActionConditionStatInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string statName;

        public bool statIsAmount;

        public bool checkStatIsHigher;

        public float statAmount;

        public bool stateValue;
    }
}
