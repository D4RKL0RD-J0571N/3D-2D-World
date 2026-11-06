namespace Gameplay.UI
{
    using System;
    using System.Collections.Generic;
    using Domain.Console;
    using Systems.Console;
    using Systems.Skyrim;
    using UnityEngine;

    /// <summary>
    /// In-game developer and cheat console emulating the Skyrim developer console (~),
    /// enhanced with "More Informative Console" (Nexus mod 19250) and "ConsoleUtilSSE" (Nexus mod 24858).
    /// Supports mouse click target inspection, FormIDs, BaseIDs, defining mods, actor stats,
    /// and targeted commands (kill, resurrect, disable, enable, setscale, unlock, tai, moveto).
    /// </summary>
    [AddComponentMenu("3D2DWorld/UI/Command Console HUD")]
    public class CommandConsoleHUD : MonoBehaviour
    {
        #region Static Access

        private static ConsoleCommandRegistry _registry;

        /// <summary>Global console registry allowing any script or mod to register custom commands.</summary>
        public static ConsoleCommandRegistry Registry
        {
            get
            {
                if (_registry == null)
                {
                    _registry = new ConsoleCommandRegistry();
                    RegisterDefaultCommands(_registry);
                }
                return _registry;
            }
        }

        #endregion

        #region Fields

        [Header("Configuration")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote;
        [SerializeField] private KeyCode _alternateToggleKey = KeyCode.F1;
        [SerializeField] private bool _isOpen;

        private readonly List<string> _logs = new();
        private readonly List<string> _history = new();
        private string _inputBuffer = string.Empty;
        private int _historyIndex = -1;
        private Vector2 _scrollPos;
        private bool _focusInput;

        #endregion

        #region Properties

        public bool IsOpen
        {
            get => _isOpen;
            set => SetConsoleOpen(value);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _ = Registry;
            ConsoleUtil.ConsoleMessageLogged += AddLog;
            AddLog("<color=cyan>Skyrim Developer & Cheat Console Initialized with More Informative Console (Mod 19250).</color>");
            AddLog("<color=grey>Click any scene object in console mode to inspect FormID, BaseID, actor stats, and run targeted cheats.</color>");
        }

        private void OnDestroy()
        {
            ConsoleUtil.ConsoleMessageLogged -= AddLog;
        }

        private void Update()
        {
            if (MCMMenu.ConsoleTildeHotkeyEnabled && (Input.GetKeyDown(_toggleKey) || Input.GetKeyDown(_alternateToggleKey)))
            {
                SetConsoleOpen(!_isOpen);
                return;
            }

            if (_isOpen && Input.GetMouseButtonDown(0))
            {
                float consoleHeight = Screen.height * 0.45f;
                if (Input.mousePosition.y < (Screen.height - consoleHeight))
                {
                    HandleWorldClickSelection();
                }
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            DrawConsoleWindow();
            DrawTargetHeader();
            DrawMoreInformativeInspector();
        }

        #endregion

        #region Console UI

        private void DrawConsoleWindow()
        {
            float width = Screen.width;
            float height = Screen.height * 0.45f;

            GUI.color = new Color(0.04f, 0.04f, 0.07f, 0.94f);
            GUI.Box(new Rect(0, 0, width, height), GUIContent.none);
            GUI.color = Color.white;

            // Output Log Scrollview
            float logHeight = height - 35f;
            _scrollPos = GUI.BeginScrollView(new Rect(10f, 10f, width - 20f, logHeight), _scrollPos, new Rect(0, 0, width - 40f, _logs.Count * 20f));

            var logStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 12,
                wordWrap = true
            };

            for (int i = 0; i < _logs.Count; i++)
            {
                GUI.Label(new Rect(0, i * 20f, width - 40f, 20f), _logs[i], logStyle);
            }

            GUI.EndScrollView();

            // Bottom Input Line
            GUI.Label(new Rect(10f, height - 28f, 20f, 24f), ">");
            GUI.SetNextControlName("ConsoleInputField");
            _inputBuffer = GUI.TextField(new Rect(30f, height - 28f, width - 110f, 24f), _inputBuffer);

            if (_focusInput)
            {
                GUI.FocusControl("ConsoleInputField");
                _focusInput = false;
            }

            HandleInputEvents();

            if (GUI.Button(new Rect(width - 75f, height - 28f, 65f, 24f), "Submit"))
            {
                ExecuteCurrentBuffer();
            }
        }

        private void DrawTargetHeader()
        {
            var data = ConsoleUtil.SelectedData;
            if (data == null) return;

            string header = $"\" {data.Name} \" (0x{data.FormId:X8})";
            float headerWidth = 440f;
            float headerX = (Screen.width - headerWidth) * 0.5f;

            GUI.color = new Color(0.08f, 0.10f, 0.16f, 0.95f);
            GUI.Box(new Rect(headerX, 6f, headerWidth, 24f), GUIContent.none);
            GUI.color = Color.white;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                richText = true
            };
            GUI.Label(new Rect(headerX, 6f, headerWidth, 24f), $"<color=white>&lt; <color=cyan>{header}</color> &gt;</color>", style);
        }

        private void DrawMoreInformativeInspector()
        {
            var data = ConsoleUtil.SelectedData;
            if (data == null) return;

            float panelWidth = 360f;
            float panelHeight = Screen.height * 0.52f;
            float panelX = Screen.width - panelWidth - 15f;
            float panelY = Screen.height * 0.45f + 10f;

            GUI.color = new Color(0.04f, 0.05f, 0.09f, 0.95f);
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panelX + 10f, panelY + 10f, panelWidth - 20f, panelHeight - 20f));

            GUILayout.Label("<color=cyan><b>MORE INFORMATIVE CONSOLE</b></color>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13, fontStyle = FontStyle.Bold });
            GUILayout.Space(3);

            GUILayout.Label($"<b>Ref ID:</b> 0x{data.FormId:X8}  |  <b>Base ID:</b> 0x{data.BaseFormId:X8}", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            GUILayout.Label($"<b>Editor ID:</b> {data.EditorId}", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            GUILayout.Label($"<b>Defined by:</b> [{data.DefiningMod}]  |  <b>Last Mod:</b> [{data.LastModifiedBy}]", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            GUILayout.Space(3);

            GUILayout.Label($"<b>Pos:</b> ({data.Position.x:F1}, {data.Position.y:F1}, {data.Position.z:F1})  |  <b>Dist:</b> {data.DistanceFromPlayer:F1}m", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            GUILayout.Label($"<b>Scale:</b> ({data.Scale.x:F2}, {data.Scale.y:F2}, {data.Scale.z:F2})  |  <b>Layer:</b> {data.LayerName} ({data.TagName})", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            GUILayout.Space(3);

            if (data.IsActor)
            {
                string stateStr = data.IsDead ? "<color=red>DEAD</color>" : "<color=green>ALIVE</color>";
                GUILayout.Label($"<b>Actor Status:</b> {stateStr}  |  <b>Faction:</b> {data.FactionName}", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
                GUILayout.Label($"<b>Health:</b> {data.Health}/{data.MaxHealth}  |  <b>Magicka:</b> {data.Magicka}  |  <b>Stamina:</b> {data.Stamina}", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
                GUILayout.Label($"<b>AI Routine:</b> <color=yellow>{data.AIRoutine}</color>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
                GUILayout.Space(3);
            }

            if (data.IsInteractable)
            {
                GUILayout.Label($"<b>Interactable:</b> {data.InteractableType}  |  <b>Lock:</b> {data.LockDifficultyStr}", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
                GUILayout.Space(3);
            }

            if (data.HasInventory)
            {
                GUILayout.Label($"<b>Inventory:</b> {data.ItemSlotCount} slots, {data.TotalWeight:F1} wt", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
                for (int i = 0; i < Mathf.Min(3, data.InventorySummary.Count); i++)
                {
                    GUILayout.Label($"  • {data.InventorySummary[i]}", new GUIStyle(GUI.skin.label) { fontSize = 10 });
                }
                GUILayout.Space(3);
            }

            GUILayout.Label($"<b>Components ({data.ComponentNames.Count}):</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            string comps = string.Join(", ", data.ComponentNames.GetRange(0, Mathf.Min(6, data.ComponentNames.Count)));
            if (data.ComponentNames.Count > 6) comps += "...";
            GUILayout.Label($"<color=#AAAAAA>{comps}</color>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10, wordWrap = true });

            GUILayout.EndArea();
        }

        private void HandleWorldClickSelection()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                var player = SKSE.GetPlayer();
                ConsoleUtil.SetSelectedReference(hit.collider.gameObject, player);
                var data = ConsoleUtil.SelectedData;
                AddLog($"<color=white>Selected: <b>\"{data.Name}\"</b> (RefID: 0x{data.FormId:X8})</color>");
            }
            else
            {
                ConsoleUtil.ClearSelectedReference();
            }
        }

        private void HandleInputEvents()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                ExecuteCurrentBuffer();
                e.Use();
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                NavigateHistory(-1);
                e.Use();
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                NavigateHistory(1);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Tab)
            {
                AutoComplete();
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                SetConsoleOpen(false);
                e.Use();
            }
        }

        #endregion

        #region Execution & History

        public void ExecuteCurrentBuffer()
        {
            if (string.IsNullOrWhiteSpace(_inputBuffer)) return;

            string line = _inputBuffer.Trim();
            _inputBuffer = string.Empty;
            _history.Add(line);
            _historyIndex = _history.Count;

            AddLog($"<color=cyan>> {line}</color>");
            Registry.ExecuteLine(line, AddLog);

            _scrollPos.y = float.MaxValue;
            _focusInput = true;
        }

        public void AddLog(string message)
        {
            _logs.Add(message);
            if (_logs.Count > 200) _logs.RemoveAt(0);
        }

        private void NavigateHistory(int direction)
        {
            if (_history.Count == 0) return;

            _historyIndex = Mathf.Clamp(_historyIndex + direction, 0, _history.Count);
            _inputBuffer = _historyIndex < _history.Count ? _history[_historyIndex] : "";
        }

        private void AutoComplete()
        {
            if (string.IsNullOrEmpty(_inputBuffer)) return;

            var completions = Registry.GetCompletions(_inputBuffer);
            if (completions.Count > 0)
            {
                _inputBuffer = completions[0];
            }
        }

        private void SetConsoleOpen(bool open)
        {
            _isOpen = open;
            if (_isOpen)
            {
                _focusInput = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private static void RegisterDefaultCommands(ConsoleCommandRegistry reg)
        {
            reg.RegisterDefaults();
        }

        #endregion
    }
}
