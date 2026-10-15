namespace Core.Utils
{
    using UnityEngine;

    /// <summary>
    /// A utility class for physics-based interaction positioning.
    /// </summary>
    public static class InteractionPhysicsUtils
    {
        /// <summary>
        /// Returns a ray from the specified viewport position of the camera.
        /// </summary>
        /// <param name="camera">The camera to use for the ray calculation.</param>
        /// <param name="viewportPosition">The position in the viewport (e.g., 0.5, 0.5 for center).</param>
        /// <returns>A Ray starting from the camera at the specified viewport position.</returns>
        public static Ray GetCenterRay(Camera camera, Vector2 viewportPosition)
        {
            return camera.ViewportPointToRay(viewportPosition);
        }

        /// <summary>
        /// Executes a raycast for interaction purposes.
        /// </summary>
        /// <param name="ray">The ray to cast.</param>
        /// <param name="maxDistance">The maximum range for the interaction.</param>
        /// <param name="layerMask">The mask of layers to interact with.</param>
        /// <returns>True if the ray hit an object.</returns>
        /// <param name="hit">The resulting RaycastHit data.</param>
        public static bool PerformInteractionRaycast(Ray ray, float maxDistance, int layerMask, out RaycastHit hit)
        {
            return Physics.Raycast(ray, out hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
        }
    }
}