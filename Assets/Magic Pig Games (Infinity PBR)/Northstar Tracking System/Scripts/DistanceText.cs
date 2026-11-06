using TMPro;
using UnityEngine;

/*
 * This handles the text which shows the distance from the player to a tracked object. You can override this to
 * create custom functionality.
 */

namespace MagicPigGames.Northstar
{
    public class DistanceText : MonoBehaviour
    {
        [Header("Required")] 
        public TMP_Text text;
        
        [Header("Options")]
        public bool overrideColor = false;
        public Color color = Color.white;
        public int maxDecimals = 2;
        public int maxDigits = 3;
        public string meters = " m";
        public bool matchIconOpacity = true;
        public bool matchIconSize = true;
        public bool screenAnchoredPositionYOffset = true;
        public float screenAnchoredPositionYOffsetPercent = 0.6f;
        public bool compassBarAnchoredPositionYOffset = true;
        public float compassBarAnchoredPositionYOffsetPercent = 0.6f;
        
        private OverlayIcon _icon;
        private RectTransform _textRectTransform;
        private RectTransform _iconRectTransform;
        private float IconWidth => _iconRectTransform.sizeDelta.x;
        private float _heightToWidthRatio;
        private string _lastDistanceText;

        protected virtual void Awake()
        {
            _icon = GetComponentInParent<OverlayIcon>();
            _textRectTransform = text.GetComponent<RectTransform>();
            _iconRectTransform = _icon.GetComponent<RectTransform>();
            var sizeDelta = _textRectTransform.sizeDelta;
            _heightToWidthRatio = sizeDelta.y / sizeDelta.x;
        }

        protected virtual void Update()
        {
            if (_icon == null)
                return;

            if (!_icon.Image.isActiveAndEnabled || (_icon.Image.color.a == 0 && matchIconOpacity))
            {
                text.enabled = false;
                return;
            }

            text.enabled = true;
            
            UpdateText();
            UpdateTextColorAndOpacity();
            UpdateTextSize();
            UpdateTextOffset();
        }

        // Handle the size of the text.
        protected virtual void UpdateTextSize()
        {
            if (!matchIconSize)
                return;
            
            _textRectTransform.sizeDelta = new Vector2(IconWidth, IconWidth * _heightToWidthRatio);
        }

        // Handle the offset of the text.
        protected virtual void UpdateTextOffset()
        {
            var doOffset = _icon.IsNavigationBarIcon 
                ? compassBarAnchoredPositionYOffset 
                : screenAnchoredPositionYOffset;
            
            if (!doOffset)
                return;

            var offsetPercent = _icon.IsNavigationBarIcon
                ? compassBarAnchoredPositionYOffsetPercent
                : screenAnchoredPositionYOffsetPercent;
            
            
            // Compute the offset to the bottom of the icon
            var offset = _iconRectTransform.sizeDelta.y / 2;
            var totalYOffset = offset * offsetPercent;
            
            // Keep _textRectTransform anchored to the bottom of the _iconRectTransform, adding totalYOffset
            _textRectTransform.anchoredPosition = new Vector2(0, -IconWidth * _heightToWidthRatio - totalYOffset);
        }

        // Handle the color and opacity of the text.
        protected virtual void UpdateTextColorAndOpacity()
        {
            if (!matchIconOpacity)
                return;

            var textColor = overrideColor ? color : _icon.Image.color;
            textColor.a = _icon.Image.color.a;
            text.color = textColor;
        }

        
        // This will update the text. Create your own class which inherits from DistanceText to override this
        // method and create custom logic.
        protected virtual void UpdateText()
        {
            var newText = $"{NorthstarUtilities.ShortScaleString(_icon.DistanceToTarget, maxDecimals, maxDigits)}{meters}";
            if (newText == _lastDistanceText) return;
            _lastDistanceText = newText;
            text.text = newText;
        }
    }

}
