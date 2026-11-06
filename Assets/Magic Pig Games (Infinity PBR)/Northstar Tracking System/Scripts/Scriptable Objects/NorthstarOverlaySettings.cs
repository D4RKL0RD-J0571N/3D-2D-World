using UnityEngine;
using UnityEngine.Serialization;

/*
 * Inspect any of the Northstar Overlay Settings objects to get the link to the docs. Check that out for sure.
 *
 * These objects hold all the main settings for the Northstar Overlay. You can create as many as you want, and then
 * assign one as the "Default" settings. Each tracked object can then override the default settings -- individually
 * as you'd like.
 */

namespace MagicPigGames.Northstar
{
    [CreateAssetMenu(fileName = "Northstar Overlay Settings", menuName = "Northstar/Overlay Settings", order = 1)]
    public class NorthstarOverlaySettings : ScriptableObject
    {
        public Vector3 iconOffset = Vector3.zero;
        public string type = "Default";

        // Main settings
        public bool enableScreenOverlay = true;
        public bool enableEdgeOverlay = true;
        public bool enableNavigationBar = true;
        
        public Sprite screenSprite;
        public Color screenSpriteColor = Color.white;
        public float screenDistanceMin = 0f;
        public float screenDistanceMax = 200f;

        // Size settings
        public int screenSpriteSize = 100;
        public bool screenSpriteSizeUseCurve = true;
        public AnimationCurve screenSpriteSizeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        
        // Opacity settings
        public float screenSpriteOpacity = 1f;
        public bool screenSpriteOpacityUseCurve = true;
        public AnimationCurve screenSpriteOpacityCurve = AnimationCurve.Linear(0, 1, 1, 0);
        
       
        public Sprite arrowSprite;
        public int arrowSize = 30;
        public int arrowSizeInitial = 10;
        public int offset = 5;
        public int iconOffsetFromArrow = 10; // In addition to arrow size
        
        // Edge main
        public Sprite edgeSprite;
        public Color edgeSpriteColor = Color.white;
        public Color arrowColor = Color.white;
        public Color arrowColorInitial = Color.white;
        public float edgeDistanceMin = 0f;
        public float edgeDistanceMax = 200f;
        public bool rotateArrow = true;

        // Size settings
        public int edgeSpriteSize = 100;
        public bool edgeSpriteSizeUseCurve = true;
        public AnimationCurve edgeSpriteSizeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        public bool edgeSizeByDistance = false;
        public bool edgeOpacityByDistance = false;
        
        // Arrow size settings
        public bool arrowSizeUseCurve = false;
        public AnimationCurve arrowSizeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        public bool arrowSizeByDistance = false;
        public bool arrowOpacityByDistance = false;
        
        // Arrow opacity settings
        public bool arrowOpacityUseCurve = false;
        public AnimationCurve arrowOpacityCurve = AnimationCurve.Linear(0, 1, 1, 0);

        // Edge opacity settings
        public float edgeSpriteOpacity = 1f;
        public bool edgeSpriteOpacityUseCurve = true;
        public AnimationCurve edgeSpriteOpacityCurve = AnimationCurve.Linear(0, 1, 1, 0);
        public float arrowOpacity = 1f;
        public float arrowOpacityInitial = 0f;
        
        // Compass Bar settings
        public bool overrideYPosition = false;
        public float xPosition = 0;
        public float yPosition = 0;
        public bool moveWithRotation = true;
        public Sprite navigationBarSprite;
        public Color navigationBarSpriteColor = Color.white;
        [FormerlySerializedAs("compassBarDistanceMin")] public float navigationBarDistanceMin = 0f;
        [FormerlySerializedAs("compassBarDistanceMax")] public float navigationBarDistanceMax = 200f;
        public int navigationBarSize = 100;
        public bool compassBarSizeByDistance = false;
        public bool compassBarOpacityByDistance = false;
        public bool navigationBarSizeUseCurve = true;
        public AnimationCurve navigationBarSizeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        public float navigationBarSpriteOpacity = 1f;
        public bool navigationBarSpriteOpacityUseCurve = true;
        public AnimationCurve navigationBarSpriteOpacityCurve = AnimationCurve.Linear(0, 1, 1, 0);
        public bool fadeAtEdges = true;
        public bool clampPositionAtEdges = false;
        
        // Optional arrow to show when target is off of the bar.
        public bool showArrowWhenOffBar = false;
        public Sprite compassBarArrowSprite;
        public Color compassBarArrowSpriteColor = Color.white;
        public int compassBarArrowSize = 30;
        public float compassBarArrowOpacity = 1f;
    }
}