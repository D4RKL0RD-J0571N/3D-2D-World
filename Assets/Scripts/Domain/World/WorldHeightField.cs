namespace Domain.World
{
    using UnityEngine;

    /// <summary>
    /// Deterministic, side-effect-free description of the Proving Grounds landscape shape.
    /// The village basin stays perfectly flat so buildings, roads and gameplay props remain valid,
    /// then smoothly blends into rolling Nordic hills that hide the world edge.
    /// Shared by the ground mesh builder, prop scatter and unit tests so they always agree.
    /// </summary>
    public static class WorldHeightField
    {
        #region Configuration

        /// <summary>World-space XZ centre of the flat village basin.</summary>
        public static readonly Vector2 Center = new Vector2(0f, 5f);

        /// <summary>Radius (metres) around <see cref="Center"/> that is guaranteed to be flat (height 0).</summary>
        public const float FlatRadius = 30f;

        /// <summary>Width (metres) of the smooth transition from flat basin to hills.</summary>
        public const float BlendWidth = 16f;

        /// <summary>Peak hill amplitude (metres) at the far edge of the map.</summary>
        public const float MaxHillHeight = 16f;

        /// <summary>Distance over which the hill amplitude ramps up after the blend zone.</summary>
        public const float RiseDistance = 55f;

        #endregion

        #region Public API

        /// <summary>
        /// Returns the terrain height (metres, relative to the village ground level) at a world XZ position.
        /// </summary>
        public static float Evaluate(float x, float z)
        {
            float distance = Vector2.Distance(new Vector2(x, z), Center);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FlatRadius, FlatRadius + BlendWidth, distance));
            if (blend <= 0f) return 0f;

            float rise = Mathf.InverseLerp(FlatRadius, FlatRadius + RiseDistance, distance);
            float amplitude = MaxHillHeight * (0.25f + 0.75f * rise);
            return blend * (1.5f + SampleNoise(x, z) * amplitude);
        }

        /// <summary>True when the position lies inside the guaranteed-flat village basin.</summary>
        public static bool IsInsideBasin(float x, float z)
        {
            return Vector2.Distance(new Vector2(x, z), Center) <= FlatRadius;
        }

        #endregion

        #region Helpers

        private static float SampleNoise(float x, float z)
        {
            float broad = Mathf.PerlinNoise(x * 0.04f + 137.5f, z * 0.04f + 59.25f);
            float detail = Mathf.PerlinNoise(x * 0.13f + 11.1f, z * 0.13f + 73.7f);
            return broad * 0.75f + detail * 0.25f;
        }

        #endregion
    }
}
