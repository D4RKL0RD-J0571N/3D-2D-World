namespace Domain.Creator
{
    using System;
    using System.Collections.Generic;
    using Core.Ids;
    using Newtonsoft.Json;
    using UnityEngine;

    /// <summary>
    /// Directed trigger/action link from one placed object instance to another (<c>ARCH-05</c>).
    /// Example: Lever (<c>OnLeverPulled</c>) -> Portcullis/Door (<c>Activate</c> / <c>Toggle</c>).
    /// </summary>
    [Serializable]
    public sealed class CrossObjectLink
    {
        public string OutputPortName = "OnActivated";
        public InstanceGuid TargetGuid;
        public string TargetActionName = "Activate";
        public float DelaySeconds;

        public CrossObjectLink() { }

        public CrossObjectLink(string outputPortName, InstanceGuid targetGuid, string targetActionName, float delaySeconds = 0f)
        {
            OutputPortName = outputPortName;
            TargetGuid = targetGuid;
            TargetActionName = targetActionName;
            DelaySeconds = delaySeconds;
        }

        public CrossObjectLink Clone()
        {
            return new CrossObjectLink(OutputPortName, TargetGuid, TargetActionName, DelaySeconds);
        }
    }

    /// <summary>
    /// Serialized entry for a single placed object in a custom creator level (<c>WorldAuthoringData</c> v2).
    /// Retains full backward compatibility with v1 <see cref="JsonUtility"/> fields.
    /// </summary>
    [Serializable]
    public class PlacedObjectRecord
    {
        public InstanceGuid InstanceId;
        public ResourceId DefinitionId;
        public string CustomLabel;

        // Legacy v1 fields preserved for backward compatibility
        public string itemId;
        public float posX;
        public float posY;
        public float posZ;
        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW = 1f;
        public float scaleX = 1f;
        public float scaleY = 1f;
        public float scaleZ = 1f;

        /// <summary>Key-value per-instance component property overrides (e.g., <c>"lockDifficulty": "Master"</c>).</summary>
        public Dictionary<string, string> PropertyOverrides = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Directed trigger links to other placed objects by <see cref="InstanceGuid"/>.</summary>
        public List<CrossObjectLink> Links = new List<CrossObjectLink>();

        public PlacedObjectRecord()
        {
            InstanceId = InstanceGuid.NewGuid();
        }

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);

        public Quaternion GetRotation()
        {
            if (Mathf.Approximately(rotX, 0f) && Mathf.Approximately(rotY, 0f) &&
                Mathf.Approximately(rotZ, 0f) && Mathf.Approximately(rotW, 0f))
            {
                return Quaternion.identity;
            }
            return new Quaternion(rotX, rotY, rotZ, rotW);
        }

        public Vector3 GetScale() => new Vector3(scaleX, scaleY, scaleZ);

        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }

        public void SetRotation(Quaternion rot)
        {
            rotX = rot.x;
            rotY = rot.y;
            rotZ = rot.z;
            rotW = rot.w;
        }

        public void SetScale(Vector3 scale)
        {
            scaleX = scale.x;
            scaleY = scale.y;
            scaleZ = scale.z;
        }

        public void SetTransform(Transform t)
        {
            if (t == null) return;
            SetPosition(t.position);
            SetRotation(t.rotation);
            SetScale(t.localScale);
        }

        public PlacedObjectRecord Clone()
        {
            var copy = new PlacedObjectRecord
            {
                InstanceId = InstanceId.IsEmpty ? InstanceGuid.NewGuid() : InstanceId,
                DefinitionId = DefinitionId,
                CustomLabel = CustomLabel,
                itemId = itemId,
                posX = posX,
                posY = posY,
                posZ = posZ,
                rotX = rotX,
                rotY = rotY,
                rotZ = rotZ,
                rotW = rotW,
                scaleX = scaleX,
                scaleY = scaleY,
                scaleZ = scaleZ
            };

            if (PropertyOverrides != null)
            {
                foreach (var kvp in PropertyOverrides)
                {
                    copy.PropertyOverrides[kvp.Key] = kvp.Value;
                }
            }

            if (Links != null)
            {
                for (int i = 0; i < Links.Count; i++)
                {
                    if (Links[i] != null)
                    {
                        copy.Links.Add(Links[i].Clone());
                    }
                }
            }

            return copy;
        }
    }

    /// <summary>
    /// Root data container for custom level layouts created in the In-Game Creator Studio (<c>WorldAuthoringData</c> v2).
    /// </summary>
    [Serializable]
    public class CreatorLevelData
    {
        public int schemaVersion = 2;
        public ResourceId worldId;
        public string levelName = "Custom Level";
        public string author = "Creator";
        public string createdAt = "";
        public string modifiedAt = "";
        public List<string> requiredModIds = new List<string>();
        public List<PlacedObjectRecord> placedObjects = new List<PlacedObjectRecord>();
    }
}
