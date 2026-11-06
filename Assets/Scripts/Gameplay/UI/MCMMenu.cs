namespace Gameplay.UI
{
    using UnityEngine;

    /// <summary>
    /// Mod Configuration Menu (MCM) interface emulating the SkyUI MCM panel.
    /// Provides runtime toggling for gameplay mods, Skyrim glitch exploits, and AI overhauls.
    /// Toggled via the F2 key.
    /// </summary>
    [AddComponentMenu("3D2DWorld/UI/MCM Menu")]
    public class MCMMenu : MonoBehaviour
    {
        #region Settings Data

        public static bool FortifyRestorationLoopEnabled = true;
        public static bool BucketHeadExploitEnabled = true;
        public static bool PlatterWallClipEnabled = true;
        public static float SpaceProgramVelocity = 75f;
        public static bool ImmersiveCitizenAIEnabled = true;
        public static bool ConsoleTildeHotkeyEnabled = true;

        #endregion

        #region Fields

        [SerializeField] private KeyCode _toggleKey = KeyCode.F2;
        [SerializeField] private bool _isOpen;

        #endregion

        #region Properties

        public bool IsOpen
        {
            get => _isOpen;
            set => _isOpen = value;
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _isOpen = !_isOpen;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            float width = 640f;
            float height = 420f;
            float x = (Screen.width - width) * 0.5f;
            float y = (Screen.height - height) * 0.5f;

            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                normal = { textColor = new Color(0.9f, 0.85f, 0.5f) }
            };

            GUI.Label(new Rect(x + 20f, y + 15f, 300f, 24f), "MOD CONFIGURATION MENU (MCM)", titleStyle);

            float curY = y + 55f;

            // Restoration Loop Toggle
            FortifyRestorationLoopEnabled = GUI.Toggle(
                new Rect(x + 25f, curY, 500f, 22f),
                FortifyRestorationLoopEnabled,
                " Enable Fortify Restoration Potion Loop (Vanilla Exploit)");
            curY += 32f;

            // Bucket Head Exploit Toggle
            BucketHeadExploitEnabled = GUI.Toggle(
                new Rect(x + 25f, curY, 500f, 22f),
                BucketHeadExploitEnabled,
                " Enable Bucket-on-Head Stealth Perception Blindness");
            curY += 32f;

            // Platter Wall Clip Toggle
            PlatterWallClipEnabled = GUI.Toggle(
                new Rect(x + 25f, curY, 500f, 22f),
                PlatterWallClipEnabled,
                " Enable Wooden Platter Wall-Clipping Speedrun Phasing");
            curY += 32f;

            // Immersive Citizens AI Toggle
            ImmersiveCitizenAIEnabled = GUI.Toggle(
                new Rect(x + 25f, curY, 500f, 22f),
                ImmersiveCitizenAIEnabled,
                " Enable Immersive Citizens AI Overhaul (Daily Schedules & Panic)");
            curY += 32f;

            // Console Tilde Toggle
            ConsoleTildeHotkeyEnabled = GUI.Toggle(
                new Rect(x + 25f, curY, 500f, 22f),
                ConsoleTildeHotkeyEnabled,
                " Enable Developer Console Tilde Key (~)");
            curY += 40f;

            // Space Program Slider
            GUI.Label(new Rect(x + 25f, curY, 260f, 22f), $"Space Program Launch Force: {SpaceProgramVelocity:F0}");
            SpaceProgramVelocity = GUI.HorizontalSlider(new Rect(x + 270f, curY + 4f, 200f, 20f), SpaceProgramVelocity, 10f, 150f);
            curY += 50f;

            if (GUI.Button(new Rect(x + width - 110f, y + height - 38f, 90f, 26f), "Close (Esc)"))
            {
                _isOpen = false;
            }
        }

        #endregion
    }
}
