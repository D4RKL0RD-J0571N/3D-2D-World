
/*
 * This is a lookup table item for the distance check. Used by the NorthstarIcon and derived classes
 * to reduce the number of distance checks performed per frame, and decrease them when the distance
 * is greater, and there is less of a need to check often.
 */

namespace MagicPigGames.Northstar
{
    [System.Serializable]
    public class DistanceCheck
    {
        public float distance;
        public float frequency;

        public DistanceCheck(float distance, float frequency)
        {
            this.distance = distance;
            this.frequency = frequency;
        }
    }
}
