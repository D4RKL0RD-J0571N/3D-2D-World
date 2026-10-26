namespace Domain.Creator
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Serialized entry for a single placed object in a custom creator level.
    /// </summary>
    [Serializable]
    public class PlacedObjectRecord
    {
        public string itemId;
        public float posX;
        public float posY;
        public float posZ;
        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;
        public float scaleX = 1f;
        public float scaleY = 1f;
        public float scaleZ = 1f;

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
        public Quaternion GetRotation() => new Quaternion(rotX, rotY, rotZ, rotW);
        public Vector3 GetScale() => new Vector3(scaleX, scaleY, scaleZ);

        public void SetTransform(Transform t)
        {
            posX = t.position.x;
            posY = t.position.y;
            posZ = t.position.z;
            rotX = t.rotation.x;
            rotY = t.rotation.y;
            rotZ = t.rotation.z;
            rotW = t.rotation.w;
            scaleX = t.localScale.x;
            scaleY = t.localScale.y;
            scaleZ = t.localScale.z;
        }
    }

    /// <summary>
    /// Root data container for custom level layouts created in the In-Game Creator Studio.
    /// </summary>
    [Serializable]
    public class CreatorLevelData
    {
        public string levelName = "Custom Level";
        public string author = "Creator";
        public string createdAt = "";
        public string modifiedAt = "";
        public List<PlacedObjectRecord> placedObjects = new List<PlacedObjectRecord>();
    }
}
