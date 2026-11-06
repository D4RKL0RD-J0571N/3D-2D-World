namespace Domain.Dialogue
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Represents an interactive conversation choice and corresponding NPC response.
    /// </summary>
    [Serializable]
    public class DialogueTopic
    {
        public string PromptText;
        [TextArea(2, 4)]
        public string ResponseText;
        public string NextNodeId;

        public DialogueTopic() { }

        public DialogueTopic(string promptText, string responseText, string nextNodeId = "")
        {
            PromptText = promptText;
            ResponseText = responseText;
            NextNodeId = nextNodeId;
        }
    }

    /// <summary>
    /// ScriptableObject defining NPC dialogue lines, greetings, and conversational topics.
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue_", menuName = "3D2DWorld/Dialogue/NPC Dialogue Data")]
    public class NPCDialogueData : ScriptableObject
    {
        #region Serialized Fields

        [Header("NPC Identity")]
        [SerializeField] private string _npcName = "Whiterun Guard";
        [SerializeField] private string _title = "Town Protector";

        [Header("Greetings")]
        [TextArea(2, 3)]
        [SerializeField] private string[] _greetings = new string[]
        {
            "I used to be an adventurer like you, then I took an arrow in the knee.",
            "Let me guess... someone stole your sweetroll?",
            "Wait... I know you.",
            "Disrespect the law, and you disrespect me.",
            "No lollygaggin'."
        };

        [Header("Topics")]
        [SerializeField] private List<DialogueTopic> _topics = new List<DialogueTopic>();

        [Header("Audio")]
        [SerializeField] private AudioClip _voiceGreetingClip;

        #endregion

        #region Properties

        public string NpcName => _npcName;
        public string Title => _title;
        public IReadOnlyList<string> Greetings => _greetings;
        public IReadOnlyList<DialogueTopic> Topics => _topics;
        public AudioClip VoiceGreetingClip => _voiceGreetingClip;

        #endregion

        #region Methods

        /// <summary>
        /// Selects a random greeting string from the defined array.
        /// </summary>
        public string GetRandomGreeting()
        {
            if (_greetings == null || _greetings.Length == 0)
                return "Greetings, traveler.";

            int index = UnityEngine.Random.Range(0, _greetings.Length);
            return _greetings[index];
        }

        /// <summary>
        /// Programmatic configuration helper for tests and automated setup.
        /// </summary>
        public void Configure(string npcName, string title, string[] greetings, List<DialogueTopic> topics = null)
        {
            _npcName = npcName;
            _title = title;
            _greetings = greetings;
            if (topics != null) _topics = topics;
        }

        #endregion
    }
}
