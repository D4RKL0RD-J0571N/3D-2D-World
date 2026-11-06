using System;
using System.Collections.Generic;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.Cameras
{
    [Title("Zoom In")]
    [Category("Zoom In")]

    [Image(typeof(IconZoom), ColorTheme.Type.Blue)]
    [Description("Zooms in until there is no obstacle between the target and the camera")]

    [Serializable]
    public class CameraClipZoom : TCameraClip
    {
        private static readonly Color GIZMOS_COLOR = new Color(0f, 1f, 0f, 0.5f);

        private const int GIZMOS_DIVISIONS = 5;
        private const int RAY_CAST_BUFFER_SIZE = 50;

        // EXPOSED MEMBERS: -----------------------------------------------------------------------

        [SerializeField] protected LayerMask m_LayerMask = Physics.DefaultRaycastLayers;

        [SerializeField] protected float m_Radius = 0.4f;
        [SerializeField] protected float m_MinDistance;

        [SerializeField] protected float m_SmoothTime = 0.5f;

        // MEMBERS: -------------------------------------------------------------------------------

        [NonSerialized] private readonly RaycastHit[] m_HitBuffer;

        [NonSerialized] private float m_CurrentDistance;
        [NonSerialized] private float m_Velocity;

        [NonSerialized] private ShotCamera m_LastShotCamera;
        [NonSerialized] private int m_LastFrame = -100;

        // PROPERTIES: ----------------------------------------------------------------------------

        public LayerMask LayerMask
        {
            get => this.m_LayerMask;
            set => this.m_LayerMask = value;
        }

        public float Radius
        {
            get => this.m_Radius;
            set => this.m_Radius = value;
        }

        public float MinDistance
        {
            get => this.m_MinDistance;
            set => this.m_MinDistance = value;
        }

        // INITIALIZERS: --------------------------------------------------------------------------

        public CameraClipZoom()
        {
            this.m_HitBuffer = new RaycastHit[RAY_CAST_BUFFER_SIZE];
            this.m_CurrentDistance = this.m_MinDistance + this.m_Radius;
            this.m_Velocity = 0f;
        }

        // UPDATE METHODS: ------------------------------------------------------------------------

        public override Vector3 Update(TCamera camera, Vector3 point, Transform[] ignore)
        {
            ShotCamera shotCamera = camera.Transition.CurrentShotCamera;
            if (!shotCamera.AvoidClipping) return camera.transform.position;

            Vector3 currentPosition = camera.transform.position;
            Vector3 direction = currentPosition - point;
            float distance = direction.magnitude;

            if (distance <= float.Epsilon) return currentPosition;
            direction /= distance;

            int count = this.RayCast(
                point, direction * distance,
                this.m_Radius, this.m_LayerMask
            );

            float targetDistance = distance;

            for (int i = 0; i < count; ++i)
            {
                RaycastHit hit = this.m_HitBuffer[i];
                if (this.IsChild(hit.transform, ignore)) continue;

                if (hit.distance < targetDistance)
                {
                    targetDistance = hit.distance;
                }
            }

            targetDistance = Mathf.Max(targetDistance, this.m_MinDistance + this.m_Radius);
            bool isFirstFrameShot = this.m_LastShotCamera != shotCamera || this.m_LastFrame < Time.frameCount - 1;

            this.m_LastShotCamera = shotCamera;
            this.m_LastFrame = Time.frameCount;

            if (isFirstFrameShot || this.m_CurrentDistance > targetDistance)
            {
                this.m_CurrentDistance = targetDistance;
                this.m_Velocity = 0f;
            }
            else
            {
                float deltaTime = camera.Time.DeltaTime;

                this.m_CurrentDistance = deltaTime > float.Epsilon
                    ? Mathf.SmoothDamp(
                        this.m_CurrentDistance,
                        targetDistance,
                        ref this.m_Velocity,
                        this.m_SmoothTime,
                        Mathf.Infinity,
                        deltaTime
                    )
                    : this.m_CurrentDistance;
            }

            return point + direction * Mathf.Min(this.m_CurrentDistance, distance);
        }

        // PRIVATE METHODS: -----------------------------------------------------------------------

        private int RayCast(Vector3 origin, Vector3 direction, float radius, LayerMask layerMask)
        {
            if (radius <= float.Epsilon)
            {
                return Physics.RaycastNonAlloc(
                    origin,
                    direction.normalized,
                    this.m_HitBuffer,
                    direction.magnitude,
                    layerMask,
                    QueryTriggerInteraction.Ignore
                );
            }

            return Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction.normalized,
                this.m_HitBuffer,
                direction.magnitude,
                layerMask,
                QueryTriggerInteraction.Ignore
            );
        }

        private bool IsChild(Transform hit, IEnumerable<Transform> ignoreList)
        {
            foreach (Transform ignore in ignoreList)
            {
                if (ignore == null) continue;
                if (hit.IsChildOf(ignore)) return true;
            }

            return false;
        }

        // GIZMOS: --------------------------------------------------------------------------------

        public override void OnDrawGizmos(TCamera camera)
        {
            Gizmos.color = GIZMOS_COLOR;

            GizmosExtension.Octahedron(
                camera.transform.position,
                camera.transform.rotation,
                this.m_Radius,
                GIZMOS_DIVISIONS
            );
        }
    }
}