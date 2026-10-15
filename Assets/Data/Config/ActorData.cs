namespace Data.Config
{
    using UnityEngine;
    using System.Collections.Generic;
    using Domain.Entities;

    [CreateAssetMenu(fileName = "NewActorData", menuName = "3D2DWorld/Entities/Actor Data")]
    public class ActorData : EntityData
    {
        public override string CategoryPrefix => "ACT";

        [Header("Starting Stats")]
        public int StartingLevel = 1;
        public float XpLevelUpBase = 75f;
        public float XpLevelUpMult = 25f;

        [Header("Starting Attributes")]
        public List<BaseAttribute> StartingAttributes = new List<BaseAttribute>();
    }
}
