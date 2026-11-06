namespace Gameplay.UI
{
    using System.Collections.Generic;
    using UnityEngine;
    using Core;
    using Domain.Combat;
    using Domain.Dialogue;

    /// <summary>
    /// Presents Skyrim-style narrative dialogue overlays displaying speaker name, title,
    /// spoken dialogue lines, and selectable dialogue topic choices.
    /// </summary>
    [AddComponentMenu("3D2DWorld/UI/Dialogue HUD")]
    public class DialogueHUD : MonoBehaviour
    {
        #region Fields

        private bool _isOpen;
        private string _npcName = string.Empty;
        private string _npcTitle = string.Empty;
        private string _currentLine = string.Empty;
        private IReadOnlyList<DialogueTopic> _topics;

        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _buttonStyle;

        #endregion

        #region Properties

        public bool IsOpen => _isOpen;
        public string CurrentNpcName => _npcName;
        public string CurrentSpokenLine => _currentLine;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            GameEventManager.AddListener<NPCDialogueOpenedEvent>(OnDialogueOpened);
            GameEventManager.AddListener<NPCDialogueClosedEvent>(OnDialogueClosed);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<NPCDialogueOpenedEvent>(OnDialogueOpened);
            GameEventManager.RemoveListener<NPCDialogueClosedEvent>(OnDialogueClosed);
        }

        private void Update()
        {
            if (!_isOpen) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                CloseDialogue();
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            InitStyles();
            DrawDialogueWindow();
        }

        #endregion

        #region Event Handlers

        private void OnDialogueOpened(NPCDialogueOpenedEvent evt)
        {
            if (evt == null) return;

            _isOpen = true;
            _npcName = evt.NpcName;
            _npcTitle = evt.Title;
            _currentLine = evt.Greeting;
            _topics = evt.Topics;
        }

        private void OnDialogueClosed(NPCDialogueClosedEvent evt)
        {
            _isOpen = false;
        }

        public void CloseDialogue()
        {
            _isOpen = false;
            GameEventManager.Publish(new NPCDialogueClosedEvent());
        }

        #endregion

        #region GUI Rendering

        private void InitStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box);
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.12f, 0.92f));
            bgTex.Apply();
            _boxStyle.normal.background = bgTex;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.95f, 0.85f, 0.5f);

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };
            _textStyle.normal.textColor = Color.white;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawDialogueWindow()
        {
            float width = Mathf.Min(650f, Screen.width * 0.75f);
            float height = 220f;
            float left = (Screen.width - width) * 0.5f;
            float top = Screen.height - height - 40f;

            GUILayout.BeginArea(new Rect(left, top, width, height), _boxStyle);
            GUILayout.Space(8);

            string header = string.IsNullOrEmpty(_npcTitle) ? _npcName : $"{_npcName}  —  {_npcTitle}";
            GUILayout.Label(header, _titleStyle);
            GUILayout.Space(10);

            GUILayout.Label($"\"{_currentLine}\"", _textStyle);
            GUILayout.FlexibleSpace();

            DrawTopicsAndControls();

            GUILayout.Space(8);
            GUILayout.EndArea();
        }

        private void DrawTopicsAndControls()
        {
            if (_topics != null && _topics.Count > 0)
            {
                for (int i = 0; i < _topics.Count; i++)
                {
                    var topic = _topics[i];
                    if (GUILayout.Button(topic.PromptText, _buttonStyle, GUILayout.Height(28)))
                    {
                        _currentLine = topic.ResponseText;
                    }
                }
            }

            if (GUILayout.Button("Farewell [Space / Esc]", _buttonStyle, GUILayout.Height(30)))
            {
                CloseDialogue();
            }
        }

        #endregion
    }
}
