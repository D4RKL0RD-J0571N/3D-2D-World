namespace Gameplay.UI
{
    using System.Collections.Generic;
    using Domain.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Upper-right HUD widget displaying active spell, potion, and blessing effects
    /// modeled after SkyUI's Active Effects HUD icons and timers.
    /// </summary>
    [AddComponentMenu("3D2DWorld/UI/SkyUI Active Effects HUD")]
    public class SkyUIActiveEffectsHUD : MonoBehaviour
    {
        #region Fields

        [Header("Appearance")]
        [SerializeField] private bool _showHUD = true;
        [SerializeField] private float _widgetWidth = 220f;
        [SerializeField] private float _entryHeight = 32f;

        private FortifyRestorationLoopManager _effectManager;

        #endregion

        #region Properties

        public FortifyRestorationLoopManager EffectManager
        {
            get => _effectManager;
            set => _effectManager = value;
        }

        public bool ShowHUD
        {
            get => _showHUD;
            set => _showHUD = value;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_effectManager == null)
            {
                _effectManager = new FortifyRestorationLoopManager();
            }
        }

        private void Update()
        {
            _effectManager?.Update(Time.deltaTime);
        }

        private void OnGUI()
        {
            if (!_showHUD || _effectManager == null) return;

            var effects = _effectManager.ActiveEffects;
            if (effects.Count == 0) return;

            float x = Screen.width - _widgetWidth - 20f;
            float y = 20f;

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Box(new Rect(x - 5f, y - 5f, _widgetWidth + 10f, (effects.Count * _entryHeight) + 10f), GUIContent.none);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                normal = { textColor = new Color(0.9f, 0.85f, 0.6f) }
            };

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = Color.white }
            };

            for (int i = 0; i < effects.Count; i++)
            {
                var eff = effects[i];
                float entryY = y + (i * _entryHeight);

                string bonusText = eff.Magnitude > 0f ? $"+{eff.Magnitude * 100f:F0}%" : "";
                string timerText = eff.IsPermanent ? "Permanent" : $"{Mathf.CeilToInt(eff.RemainingTime)}s";

                GUI.Label(new Rect(x, entryY, _widgetWidth * 0.65f, 16f), eff.Name, titleStyle);
                GUI.Label(new Rect(x + (_widgetWidth * 0.65f), entryY, _widgetWidth * 0.35f, 16f), timerText, subStyle);

                if (!string.IsNullOrEmpty(bonusText))
                {
                    GUI.Label(new Rect(x, entryY + 14f, _widgetWidth, 14f), $"Magnitude: {bonusText}", subStyle);
                }
            }
        }

        #endregion
    }
}
