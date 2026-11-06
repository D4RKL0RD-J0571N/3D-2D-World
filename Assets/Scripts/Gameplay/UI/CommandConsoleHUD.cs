namespace Gameplay.UI
{
    using System;
    using System.Collections.Generic;
    using Systems.Console;
    using UnityEngine;

    /// <summary>
    /// In-game developer and cheat console emulating the Skyrim developer console (~).
    /// Features monospace scrollable log, command input history, tab autocompletion,
    /// and a global <see cref="ConsoleCommandRegistry"/> for extensible custom mod commands.
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
            // Ensure registry is initialized
            _ = Registry;
            AddLog("<color=cyan>Skyrim Developer & Cheat Console Initialized. Type 'help' for commands.</color>");
        }

        private void Update()
        {
            if (MCMMenu.ConsoleTildeHotkeyEnabled && (Input.GetKeyDown(_toggleKey) || Input.GetKeyDown(_alternateToggleKey)))
            {
                SetConsoleOpen(!_isOpen);
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            DrawConsoleWindow();
        }

        #endregion

        #region Console UI

        private void DrawConsoleWindow()
        {
            float width = Screen.width;
            float height = Screen.height * 0.45f;

            GUI.color = new Color(0.04f, 0.04f, 0.07f, 0.92f);
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
            reg.RegisterCommand(new ToggleGodModeCommand());
            reg.RegisterCommand(new ToggleCollisionCommand());
            reg.RegisterCommand(new AddItemCommand());
            reg.RegisterCommand(new SetActorValueCommand());
            reg.RegisterCommand(new AdvanceSkillCommand());
            reg.RegisterCommand(new KillCommand());
            reg.RegisterCommand(new KillAllCommand());
            reg.RegisterCommand(new ResurrectCommand());
            reg.RegisterCommand(new CenterOnCellCommand());
            reg.RegisterCommand(new QuitCommand());
            reg.RegisterCommand(new HelpCommand(reg));
        }

        #endregion
    }
}
