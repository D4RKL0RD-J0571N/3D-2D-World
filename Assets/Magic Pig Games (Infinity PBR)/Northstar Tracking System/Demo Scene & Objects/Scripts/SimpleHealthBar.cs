using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MagicPigGames.Northstar
{
    public class SimpleHealthBar : MonoBehaviour
    {
        [Header("Required")] public Image background;
        public Image healthBar;
        private int HealthBarMin => 0;
        private float HealthBarMax => background.rectTransform.sizeDelta.x;

        [Header("Demo Values")] 
        public int health = 5;
        public int healthMax = 5;
        
        [Header("Fade Options")]
        public bool startHidden = true;
        [Min(0f)] public float fadeTime = 1f;
        public bool showBasedOnDistance = false;
        [Min(0f)] public float minDistance = 0f;
        [Min(0f)] public float maxDistance = 10f;
        public bool showOnValueChange = true;
        public float valueChangeTime = 4f;
        public bool showOnEdges = false;
        
        public float HealthPercent => (float) health / healthMax;
        public float HealthBarValue => Mathf.Lerp(HealthBarMin, HealthBarMax, HealthPercent);

        private GameObject _character;
        private OverlayIcon _screenIcon;
        
        private float _valueChangeTimer;
        private float _fadeTimer;
        private float _elapsedTime;
        private float _goalAlpha;
        private Coroutine _fadeCoroutine;
        
        private float CurrentAlpha => background.color.a;
        private float DistanceToTarget => _screenIcon.DistanceToTarget;
        private float PercentToEdge => _screenIcon.PercentToEdge;

        private void Start()
        {
            _goalAlpha = startHidden ? 0 : 1;
            SetAlpha(background, _goalAlpha);
            SetAlpha(healthBar, _goalAlpha);
        }

        private void Update()
        {
            if (_screenIcon == null) return;
            
            var goalShouldBe = ShouldShow() ? 1 : 0;
            if (Math.Abs(goalShouldBe - _goalAlpha) > 0.001f)
                StartFade(goalShouldBe);
        }

        private bool ShouldShow()
        {
            // If we are not showing on edges, check to see if this is on the edge of the screen.
            if (!showOnEdges && PercentToEdge > 0)
                return false;
            
            // If we show when the value changes, then show it, and count down the timer.
            if (showOnValueChange && _valueChangeTimer > 0f)
            {
                _valueChangeTimer -= Time.deltaTime;
                return true;
            }
            
            // If we show on distance, then check the distance and don't show if we are out of range.
            if (showBasedOnDistance)
            {
                if (DistanceToTarget < minDistance || DistanceToTarget > maxDistance)
                    return false;
            }

            return true;
        }

        private void StartFade(float value)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _elapsedTime = fadeTime - _elapsedTime;
            }

            _goalAlpha = value;
            _fadeCoroutine = StartCoroutine(Fade(CurrentAlpha, _goalAlpha));
        }

        private IEnumerator Fade(float startAlpha, float endAlpha)
        {
            while (_elapsedTime < fadeTime)
            {
                var newAlpha = Mathf.Lerp(startAlpha, endAlpha, _elapsedTime / fadeTime);

                SetAlpha(background, newAlpha);
                SetAlpha(healthBar, newAlpha);

                _elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // Ensure the final alpha is set to the desired alpha
            SetAlpha(background, endAlpha);
            SetAlpha(healthBar, endAlpha);
        }

        private void SetAlpha(Image image, float value)
        {
            var imageColor = image.color;
            imageColor.a = value;
            image.color = imageColor;
        }
        
        public void TakeDamage(int damage = 1)
        {
            health -= damage;
            _valueChangeTimer = valueChangeTime;
            UpdateHealthBar();
            Death();
        }

        private void Death()
        {
            if (health > 0) 
                return;

            Destroy(_character);
        }

        private void UpdateHealthBar() 
            => healthBar.rectTransform.sizeDelta = new Vector2(HealthBarValue, healthBar.rectTransform.sizeDelta.y);

        public void SetCharacter(GameObject character) => _character = character;

        public void SetOverlayIcon(OverlayIcon screenIcon) => _screenIcon = screenIcon;
    }

}
