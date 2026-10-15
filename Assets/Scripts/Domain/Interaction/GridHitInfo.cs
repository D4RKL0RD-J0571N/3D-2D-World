namespace Domain.Interaction
{
    using UnityEngine;

    public struct GridHitInfo
    {
        public bool IsHit;
        public Vector3Int BlockCoordinate;
        public Vector3Int Normal;
        public GameObject HitObject;
    }
}
