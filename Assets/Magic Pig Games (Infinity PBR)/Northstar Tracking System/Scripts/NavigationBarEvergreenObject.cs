using UnityEngine;
using UnityEngine.UI;

/*
 * A compass bar evergreen object is one which is always displayed on the compass bar, such as the cardinal
 * directions, tick lines, or a "forward" indicator.
 */

namespace MagicPigGames.Northstar
{
    public class NavigationBarEvergreenObject : MonoBehaviour
    {
        [Header("Options")] 
        [Range(-180, 180)]
        public float angle;
        public bool overrideYPosition = false;
        public float yPos = 0;
        public bool moveWithRotation = true;
        public bool fadeAtEdges = true;
        public bool clampPositionAtEdges = false;
        public bool useMaxAlpha = false;
        [Range(0,1)]
        public float maxAlpha = 1f;
        
        private Image _image;

        private void Awake() =>_image = GetComponent<Image>();
    }
}
