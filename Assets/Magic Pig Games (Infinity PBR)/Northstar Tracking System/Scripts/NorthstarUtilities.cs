using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/*
 * Utility methods for the Northstar system.
 */

namespace MagicPigGames.Northstar
{
    public static class NorthstarUtilities
    {
        // Get the angle between two points, ignoring the y axis
        public static float GetAngleBetweenPoints(Vector3 source, Vector3 target)
        {
            var angle = Mathf.Atan2(target.x - source.x, target.z - source.z) * Mathf.Rad2Deg;
            return angle;
        }

        // get the angle between two points, ignoring the y axis, from the perspective of PointA Forward
        public static float GetAngleBetweenPointsSourceForward(Transform source, Vector3 target)
        {
            var angle = Vector3.SignedAngle(source.forward, target - source.position, Vector3.up);
            return angle;
        }

        public static float GetAngleBetweenPointsSourceForwardIgnoringRotation(Transform source, Vector3 target)
        {
            Vector3 sourceForwardXZ = new Vector3(source.forward.x, 0f, source.forward.z);
            Vector3 targetDirectionXZ = new Vector3(target.x - source.position.x, 0f, target.z - source.position.z);

            //Debug.DrawRay(source.position, sourceForwardXZ, Color.green);
            //Debug.DrawRay(source.position, targetDirectionXZ, Color.red);
            
            float angle = Vector3.SignedAngle(sourceForwardXZ, targetDirectionXZ, Vector3.up);
            return angle;
        }
        
        public static float GetAngleBetweenPointsSourceForwardIgnoringRotation(Transform source, float angle, float northAngle)
        {
            Vector3 sourceForwardXZ = new Vector3(source.forward.x, 0f, source.forward.z);
            Vector3 direction = Quaternion.Euler(0f, northAngle + angle, 0f) * Vector3.forward; // construct a direction vector from the given angles
            Vector3 targetDirectionXZ = new Vector3(direction.x, 0f, direction.z);
                    
            float resultantAngle = Vector3.SignedAngle(sourceForwardXZ, targetDirectionXZ, Vector3.up);
            return resultantAngle;
        }

        // get the angle between two points, ignoring the y axis, Based on an angle representing north
        public static float GetAngleBetweenPoints(Vector3 source, Vector3 target, float northAngle)
        {
            var angle = Mathf.Atan2(target.x - source.x, target.z - source.z) * Mathf.Rad2Deg;
            angle -= northAngle;
            return angle;
        }

        public static void SetImageAlpha(Graphic image, float value = 1f)
        {
            //Debug.Log($"image is null: {image == null}");
            if (image == null) return;
            
            //Debug.Log($"Setting alpha to {value} for image {image.name}");
            var color = image.color;
            color.a = value;
            image.color = color;
            //Debug.Log($"Color is now {image.color} from ({color})");
        }

        public static bool IsVisibleFrom(Camera targetCamera, Renderer getComponent)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(targetCamera);
            return GeometryUtility.TestPlanesAABB(planes, getComponent.bounds);
        }

        public static Vector2 EdgeScreenPosition(Transform source, Transform target)
        {
            // Direction from source to target
            Vector3 dir = target.position - source.position;

            // Transform direction from world space into source's local space
            Vector3 localDir = source.InverseTransformDirection(dir);

            // Normalize to a top-down 2D perspective
            localDir.y = 0;
            localDir.Normalize();

            // Get the aspect ratio of the screen
            float aspectRatio = (float)Screen.width / Screen.height;

            // Scale the local direction's x coordinate by the aspect ratio
            Vector2 dir2D = new Vector2(localDir.x * aspectRatio, localDir.z);

            float maxX = Mathf.Abs(dir2D.x);
            float maxY = Mathf.Abs(dir2D.y);

            // Determine which edge of the screen the target intercepts
            if (maxX > maxY)
            {
                // The target intercepts the left or right edge of the screen
                dir2D = dir2D / maxX;
            }
            else
            {
                // The target intercepts the top or bottom edge of the screen
                dir2D = dir2D / maxY;
            }

            // Translate from [-1, 1] range to [0, 1] range
            dir2D = dir2D / 2.0f + Vector2.one / 2.0f;

            return dir2D;
        }

        private static readonly string[] _shortScaleLabels = { "", "K", "M", "B", "T" };

        public static string ShortScaleString(float number, int totalRounding, int totalDigits)
        {
            var scale = _shortScaleLabels;
            var idx = 0;

            while (number >= Math.Pow(10, totalDigits) && idx < scale.Length-1)
            {
                number /= 1000;
                idx++;
            }
        
            // Compute number of decimal places based on number of digits in number
            var decimalPlaces = Math.Max(0, totalDigits - ((int) Math.Log10(Math.Floor(number)) + 1));
            decimalPlaces = Math.Min(decimalPlaces, totalRounding); // limit to totalRounding
        
            var numberFormat = string.Format("F{0}", decimalPlaces);
            var formattedNumber = number.ToString(numberFormat);
            return formattedNumber + scale[idx];
        }
        
        public static int GetLayerIndex(int layerOrder, Dictionary<TrackedTargetOverlay, OverlayIcon> icons, bool sort = true)
        {
            List<KeyValuePair<TrackedTargetOverlay, OverlayIcon>> orderedIcons;

            if (sort)
            {
                orderedIcons = icons
                    .OrderBy(i => i.Key.layerOrder)
                    .ThenByDescending(i => i.Value.DistanceToTarget)
                    .ToList();
            }
            else
            {
                orderedIcons = icons.ToList();
            }

            var layerIndex = 0;
            foreach (var overlay in orderedIcons)
            {
                if (overlay.Key.layerOrder > layerOrder)
                    return layerIndex;

                layerIndex++;
            }

            return layerIndex;
        }

        public static void SortLayers(Dictionary<TrackedTargetOverlay, OverlayIcon> icons)
        {
            List<KeyValuePair<TrackedTargetOverlay, OverlayIcon>> orderedIcons;
            orderedIcons = icons
                .OrderBy(i => i.Key.layerOrder)
                .ThenByDescending(i => i.Value.DistanceToTarget)
                .ToList();

            for (var index = 0; index < orderedIcons.Count; index++)
            {
                var overlay = orderedIcons[index];
                overlay.Value.transform.SetSiblingIndex(index);
            }
        }

    }

}
