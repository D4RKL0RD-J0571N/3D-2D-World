namespace Core.Utils
{
    using UnityEngine;

    /// <summary>
    /// A utility class for physics-based positioning.
    /// </summary>
    public static class PhysicsUtils
    {
        /// <summary>
        /// Finds the ground position using a downward raycast.
        /// </summary>
        /// <param name="origin">The starting position for the raycast.</param>
        /// <param name="maxDistance">The maximum distance to cast the ray.</param>
        /// <returns>The hit point of the ground, or the origin if no hit is found.</returns>
        public static Vector3 FindGroundPosition(Vector3 origin, float maxDistance = 50f)
        {
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, maxDistance))
            {
                return hit.point;
            }
            return origin;
        }
    }
}