using UnityEngine;

/*
 * Helpful extension methods!
 */

namespace MagicPigGames.Northstar
{
    public static class NorthstarExtensions
    {
        public static bool Between(this float value, float a, float b)
            => value >= Mathf.Min(a, b) && value <= Mathf.Max(a, b);

        public static bool BetweenAngle(this float value, float a, float b)
        {
            value = (value + 360) % 360;
            a = (a + 360) % 360;
            b = (b + 360) % 360;

            if (a > b)
            {
                // If "a" is bigger, it means we've wrapped around the circle.
                // So, we need to verify if "value" is not within (b, a)
                return !(value > b && value < a);
            }
            else
            {
                // Otherwise, ensure "value" is between (a, b).
                return value >= a && value <= b;
            }
        }
    }
}