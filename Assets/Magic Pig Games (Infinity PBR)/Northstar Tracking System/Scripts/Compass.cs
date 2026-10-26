using UnityEngine;

/*
 * Compass inherits from NorthstarSystem, and really doesn't do much else beyond that. It's just a way to differentiate
 * between this an Radar, which does have additional functionality.
 * 
 * You can override this class to create your own custom Compass.
 */

namespace MagicPigGames.Northstar
{
    public class Compass : NorthstarSystem
    {
        public static Compass CompassInstance { get; private set; }

        protected override void Awake()
        {
            if (CompassInstance != null)
            {
                Debug.LogError("There can only be one Compass / Radar in the scene.");
                Destroy(this);
                return;
            }

            CompassInstance = this;

            base.Awake(); // This calls the Awake method in NorthstarSystem

            SetDictionary();
        }

        protected override void OnDestroy()
        {
            if (CompassInstance == this) CompassInstance = null;
            base.OnDestroy();
        }
    }
}
