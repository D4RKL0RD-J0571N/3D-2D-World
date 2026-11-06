namespace Domain.Combat
{
    using UnityEngine;

    public enum DamageType
    {
        Physical,
        Acid,
        Fire,
        Magic
    }

    /// <summary>
    /// Value object representing structured damage delivered during combat interactions.
    /// </summary>
    [System.Serializable]
    public struct DamageInfo
    {
        #region Fields

        public int Amount;
        public DamageType Type;
        public GameObject Instigator;
        public Vector3 HitPoint;
        public Vector3 HitNormal;

        #endregion

        #region Constructors

        public DamageInfo(
            int amount,
            DamageType type,
            GameObject instigator = null,
            Vector3 hitPoint = default,
            Vector3 hitNormal = default)
        {
            Amount = amount;
            Type = type;
            Instigator = instigator;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
        }

        #endregion
    }
}
