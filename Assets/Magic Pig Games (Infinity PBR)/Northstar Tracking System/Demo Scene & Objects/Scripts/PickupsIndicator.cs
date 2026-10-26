using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MagicPigGames.Northstar
{
    public class PickupsIndicator : MonoBehaviour
    {
        [Header("Options")] public float pickupTime = 5f;

        public float
            pickupTimeModifier = 1f; // Placeholder for custom speed modifier based on player stats / your game logic

        public float fadeTime = 0.3f;

        [Header("Plumbing")] public Image progressBar;
        public Image progressBarBackground;

        private GameObject TrackedObject => _screenIcon.TrackedObject;
        private Transform TrackedTransform => TrackedObject.transform;

        private float _barWidth;
        private float _progress;
        private float _progressBarAlphaStart;
        private Key _interactionKey;
        private Coroutine _runningProgress;
        private Coroutine _runningFadeIn;
        private Coroutine _runningFadeOut;
        private OverlayIcon _screenIcon;

        private void Awake()
        {
            _barWidth = progressBar.rectTransform.rect.width;
            _screenIcon = GetComponent<OverlayIcon>();
        }

        public void StartProgress(Key interactionKey)
        {
            _interactionKey = interactionKey;
            _progress = 0;
            CancelProgressCoroutine();
            if (!SimplePlayerActions.Instance.CloseEnoughToInteractWith(TrackedTransform))
                return;
            
            Debug.Log("Start Pickup Progress");
            StartFadeIn();
            _runningProgress = StartCoroutine(PickupProgress());
        }

        public void CancelProgress()
        {
            Debug.Log("Cancel Pickup Progress");
            CancelProgressCoroutine();
            StartFadeOut();
        }

        private void PickupComplete()
        {
            StartFadeOut();
            Debug.Log("Pickup Complete - Do something here!");

            Destroy(TrackedObject);
        }

        private void StartFadeOut()
        {
            StopFadeIn();
            _runningFadeOut ??= StartCoroutine(FadeOut());
        }

        private void StartFadeIn()
        {
            StopFadeOut();
            _runningFadeIn ??= StartCoroutine(FadeIn());
        }

        private void StopFadeOut()
        {
            if (_runningFadeOut == null)
                return;
            
            StopCoroutine(_runningFadeOut);
            _runningFadeOut = null;
        }
        
        private void StopFadeIn()
        {
            if (_runningFadeIn == null)
                return;
            
            StopCoroutine(_runningFadeIn);
            _runningFadeIn = null;
        }

        private IEnumerator FadeIn()
        {
            CacheAlphaValues();

            while (progressBar.color.a < 1)
            {
                var alpha = Mathf.MoveTowards(progressBar.color.a, 1, Time.deltaTime / fadeTime);
                SetProgressBarAlpha(alpha);
                yield return null;
            }

            _runningFadeIn = null;
        }

        private IEnumerator FadeOut()
        {
            CacheAlphaValues();

            while (progressBar.color.a > 0)
            {
                var alpha = Mathf.MoveTowards(progressBar.color.a, 0, Time.deltaTime / fadeTime);
                SetProgressBarAlpha(alpha);
                yield return null;
            }

            _runningFadeOut = null;
        }

        private IEnumerator PickupProgress()
        {
            while (_progress < 1)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard[_interactionKey].wasReleasedThisFrame)
                {
                    CancelProgress();
                    yield break;
                }

                if (!SimplePlayerActions.Instance.CloseEnoughToInteractWith(TrackedTransform))
                {
                    Debug.Log($"Not close enough to interact with {TrackedTransform.gameObject.name}");
                    CancelProgress();
                    yield break;
                }

                _progress += Time.deltaTime / (pickupTime * pickupTimeModifier);
                UpdateProgress(_progress);
                yield return null;
            }

            PickupComplete();
            _runningProgress = null;
        }

        private void UpdateProgress(float progress) => progressBar.rectTransform.offsetMax
            = new Vector2(-_barWidth * (1 - progress), progressBar.rectTransform.offsetMax.y);

        private void CacheAlphaValues() => _progressBarAlphaStart = progressBar.color.a;

        private void SetProgressBarAlpha(float alpha)
        {
            progressBar.color = new Color(progressBar.color.r, progressBar.color.g, progressBar.color.b, alpha);
            progressBarBackground.color = new Color(progressBarBackground.color.r, progressBarBackground.color.g,
                progressBarBackground.color.b, alpha);
        }

        private void CancelProgressCoroutine()
        {
            if (_runningProgress == null) return;
            StopCoroutine(_runningProgress);
        }
    }

}