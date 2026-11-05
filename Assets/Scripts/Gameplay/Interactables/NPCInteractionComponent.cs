namespace Gameplay.Interactables
{
    using System.Collections;
    using UnityEngine;
    using Core;
    using Domain.Combat;
    using Domain.Dialogue;
    using Gameplay.Entities;

    /// <summary>
    /// Interactive component attached to NPCs enabling conversational dialogue.
    /// Implements IInteractable to integrate seamlessly with PlayerInteractionController raycasting.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Interactables/NPC Interaction Component")]
    [RequireComponent(typeof(BaseActor))]
    public class NPCInteractionComponent : MonoBehaviour, IInteractable
    {
        #region Serialized Fields

        [Header("Dialogue Definition")]
        [SerializeField] private NPCDialogueData _dialogueData;

        [Header("Audio")]
        [SerializeField] private AudioClip _greetingGruntClip;

        private BaseActor _actor;
        private bool _isInteracting;

        #endregion

        #region Properties

        public NPCDialogueData DialogueData
        {
            get => _dialogueData;
            set => _dialogueData = value;
        }

        public string InteractionPrompt
        {
            get
            {
                string name = _dialogueData != null ? _dialogueData.NpcName : gameObject.name;
                return $"[E] Talk to {name}";
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
            EnsureDefaultAssets();
            InitializeDefaultDialogue();
        }

        #endregion

        #region IInteractable Implementation

        public bool CanInteract
        {
            get
            {
                if (_isInteracting) return false;

                var attr = GetComponent<AttributeComponent>();
                if (attr != null && attr.GetAttribute("Health")?.CurrentValue <= 0)
                    return false;

                return true;
            }
        }

        public void OnInteract(BaseActor interactor)
        {
            if (!CanInteract) return;

            Interact(interactor != null ? interactor.gameObject : null);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;

            StartCoroutine(FaceInteractorAndSpeak(interactor));
        }

        #endregion

        #region Interaction Handling

        private IEnumerator FaceInteractorAndSpeak(GameObject interactor)
        {
            _isInteracting = true;

            if (interactor != null)
            {
                Vector3 lookDir = interactor.transform.position - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.05f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    float elapsed = 0f;
                    Quaternion startRot = transform.rotation;
                    while (elapsed < 0.25f)
                    {
                        elapsed += Time.deltaTime;
                        transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / 0.25f);
                        yield return null;
                    }
                }
            }

            PlayGreetingAudio();
            DispatchDialogueEvent();

            yield return new WaitForSeconds(0.5f);
            _isInteracting = false;
        }

        private void DispatchDialogueEvent()
        {
            if (_dialogueData == null) return;

            string greeting = _dialogueData.GetRandomGreeting();
            GameEventManager.Publish(new NPCDialogueOpenedEvent(
                _dialogueData.NpcName,
                _dialogueData.Title,
                greeting,
                _dialogueData.Topics));
        }

        private void PlayGreetingAudio()
        {
            AudioClip clip = _dialogueData != null && _dialogueData.VoiceGreetingClip != null
                ? _dialogueData.VoiceGreetingClip
                : _greetingGruntClip;

            if (clip != null && ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                audio.Play3D(clip, transform.position, 0.85f);
            }
        }

        #endregion

        #region Setup & Default Data

        private void InitializeDefaultDialogue()
        {
            if (_dialogueData == null)
            {
                _dialogueData = ScriptableObject.CreateInstance<NPCDialogueData>();
                _dialogueData.Configure(
                    "Whiterun Guard",
                    "Hold Guard",
                    new string[]
                    {
                        "I used to be an adventurer like you, then I took an arrow in the knee.",
                        "Let me guess... someone stole your sweetroll?",
                        "Disrespect the law, and you disrespect me.",
                        "Watch the skies, traveler."
                    });
            }
        }

        private void EnsureDefaultAssets()
        {
#if UNITY_EDITOR
            if (_greetingGruntClip == null)
            {
                _greetingGruntClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbariansGrunt1.wav");
            }
#endif
        }

        #endregion
    }
}
