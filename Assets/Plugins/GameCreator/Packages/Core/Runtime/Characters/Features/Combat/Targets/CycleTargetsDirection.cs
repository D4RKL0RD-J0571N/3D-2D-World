using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCreator.Runtime.Characters
{
    public static partial class CycleTargets
    {
        public static void Direction(Character character, Camera camera, Vector2 direction)
        {
            if (camera == null) return;
            if (direction.sqrMagnitude <= 0f) return;

            Targets targets = character.Combat.Targets;
            List<GameObject> list = targets.List;

            Vector2 originPoint = GetOriginPoint(camera, targets.Primary);

            float minAngle = INFINITY;
            GameObject nextCandidate = null;

            foreach (GameObject candidate in list)
            {
                if (candidate == targets.Primary) continue;

                Vector3 point = camera.WorldToScreenPoint(candidate.transform.position);
                if (point.z <= 0f) continue;

                Vector2 offset = (Vector2) point - originPoint;
                if (offset.sqrMagnitude <= float.Epsilon) continue;

                float angle = Vector2.Angle(offset, direction);
                if (angle >= Math.Min(minAngle, 90f)) continue;

                minAngle = angle;
                nextCandidate = candidate;
            }

            if (nextCandidate != null)
            {
                targets.Primary = nextCandidate;
            }
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private static Vector2 GetOriginPoint(Camera camera, GameObject primary)
        {
            if (primary != null)
            {
                Vector3 primaryPoint = camera.WorldToScreenPoint(primary.transform.position);
                if (primaryPoint.z > 0f) return primaryPoint;
            }

            Vector3 center = camera.transform.TransformPoint(Vector3.forward);
            return camera.WorldToScreenPoint(center);
        }
    }
}