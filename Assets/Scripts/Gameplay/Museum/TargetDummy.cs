namespace Gameplay.Museum
{
    using System.Collections;
    using Core;
    using Domain.Combat;
    using Domain.Entities;
    using Gameplay.Entities;
    using UnityEngine;

    /// <summary>
    /// Interactive combat testing dummy for the Museum Armory & Proving Grounds.
    /// Responds to damage with visual hit wobble and automatically restores HP after a brief cooldown.
    /// </summary>
    [AddComponentMenu("3D 2D World/Museum/Target Dummy")]
    [SelectionBase]
    public class TargetDummy : BaseActor
    {
        #region Inspector Fields

        [Header("Dummy Configuration")]
        [SerializeField] private float resetDelaySeconds = 3f;
        [SerializeField] private float wobbleIntensity = 0.2f;

        [Header("Visual Feedback")]
        [SerializeField] private Renderer meshRenderer;
        [SerializeField] private Color hitFlashColor = Color.white;

        #endregion

        #region Private State

        private Vector3 _originalScale;
        private Color _originalColor;
        private Coroutine _wobbleCoroutine;
        private float _lastHitTime;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            _originalScale = transform.localScale;

            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<Renderer>();
            }

            if (meshRenderer != null && meshRenderer.material != null)
            {
                _originalColor = meshRenderer.material.color;
            }
        }

        private void Update()
        {
            if (Attributes == null) return;

            var health = Attributes.GetAttribute("Health");
            if (health != null && health.CurrentValue < health.MaximumValue)
            {
                if (Time.time - _lastHitTime >= resetDelaySeconds)
                {
                    Attributes.ApplyHealing("Health", health.MaximumValue);
                }
            }
        }

        #endregion

        #region Combat Override

        public override void TakeDamage(DamageInfo info)
        {
            _lastHitTime = Time.time;
            base.TakeDamage(info);

            if (_wobbleCoroutine != null)
            {
                StopCoroutine(_wobbleCoroutine);
            }
            _wobbleCoroutine = StartCoroutine(HitWobbleRoutine());
        }

        #endregion

        #region Animation Routines

        private IEnumerator HitWobbleRoutine()
        {
            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.color = hitFlashColor;
            }

            // Squash down on hit
            transform.localScale = new Vector3(_originalScale.x * (1f + wobbleIntensity), _originalScale.y * (1f - wobbleIntensity), _originalScale.z * (1f + wobbleIntensity));
            yield return new WaitForSeconds(0.08f);

            // Stretch up
            transform.localScale = new Vector3(_originalScale.x * (1f - wobbleIntensity * 0.5f), _originalScale.y * (1f + wobbleIntensity * 0.5f), _originalScale.z * (1f - wobbleIntensity * 0.5f));
            yield return new WaitForSeconds(0.08f);

            // Restore
            transform.localScale = _originalScale;
            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.color = _originalColor;
            }
        }

        #endregion
    }
}
