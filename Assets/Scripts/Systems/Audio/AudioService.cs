namespace Systems.Audio
{
    using Core;
    using Domain.Creator;
    using Domain.Museum;
    using UnityEngine;

    /// <summary>
    /// Service responsible for routing audio effects for UI interactions,
    /// placement feedback, and game events throughout the world.
    /// </summary>
    public class AudioService : IGameService
    {
        #region Fields

        private AudioSource _uiSource;
        private GameObject _audioHost;

        private AudioClip _clickClip;
        private AudioClip _interactClip;
        private AudioClip _lootClip;
        private AudioClip _placeClip;
        private AudioClip _demolishClip;

        #endregion

        #region IGameService Lifecycle

        /// <inheritdoc />
        public void Initialize()
        {
            EnsureAudioHost();
            LoadAudioClips();
            SubscribeEvents();
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            UnsubscribeEvents();

            if (_audioHost != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_audioHost);
                }
                else
                {
                    Object.DestroyImmediate(_audioHost);
                }

                _audioHost = null;
            }
        }

        #endregion

        #region Setup & Clips

        private void EnsureAudioHost()
        {
            if (_audioHost == null)
            {
                _audioHost = new GameObject("[AudioService_Host]");
                if (Application.isPlaying)
                {
                    Object.DontDestroyOnLoad(_audioHost);
                }

                _uiSource = _audioHost.AddComponent<AudioSource>();
                _uiSource.playOnAwake = false;
                _uiSource.spatialBlend = 0f; // Pure 2D for UI sounds
                _uiSource.volume = 0.8f;
            }
        }

        private void LoadAudioClips()
        {
#if UNITY_EDITOR
            _clickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Cowsins/SFX/UI/clickSFX.wav");
            _interactClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Cowsins/SFX/UI/InteractionAllowed_SFX.wav");
            _lootClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Cowsins/SFX/Others/Loot_Success_SFX.wav");
            _placeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Cowsins/SFX/Others/Unholster_SFX.wav");
            _demolishClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Cowsins/SFX/Others/DestroyWoodenBoxSFX.wav");
#endif
        }

        private void SubscribeEvents()
        {
            GameEventManager.AddListener<CreatorItemSelectedEvent>(OnCreatorItemSelected);
            GameEventManager.AddListener<CreatorObjectPlacedEvent>(OnCreatorObjectPlaced);
            GameEventManager.AddListener<CreatorObjectRemovedEvent>(OnCreatorObjectRemoved);
            GameEventManager.AddListener<ExhibitInspectedEvent>(OnExhibitInspected);
        }

        private void UnsubscribeEvents()
        {
            GameEventManager.RemoveListener<CreatorItemSelectedEvent>(OnCreatorItemSelected);
            GameEventManager.RemoveListener<CreatorObjectPlacedEvent>(OnCreatorObjectPlaced);
            GameEventManager.RemoveListener<CreatorObjectRemovedEvent>(OnCreatorObjectRemoved);
            GameEventManager.RemoveListener<ExhibitInspectedEvent>(OnExhibitInspected);
        }

        #endregion

        #region Event Handlers

        private void OnCreatorItemSelected(CreatorItemSelectedEvent evt)
        {
            PlayClick();
        }

        private void OnCreatorObjectPlaced(CreatorObjectPlacedEvent evt)
        {
            PlayPlace(evt.Position);
        }

        private void OnCreatorObjectRemoved(CreatorObjectRemovedEvent evt)
        {
            PlayDemolish(evt.RemovedObject != null ? evt.RemovedObject.transform.position : Vector3.zero);
        }

        private void OnExhibitInspected(ExhibitInspectedEvent evt)
        {
            PlayInteract();
        }

        #endregion

        #region Public Play Methods

        /// <summary>Plays standard UI click feedback.</summary>
        public void PlayClick()
        {
            Play2D(_clickClip, 0.6f);
        }

        /// <summary>Plays interaction notification feedback.</summary>
        public void PlayInteract()
        {
            Play2D(_interactClip, 0.7f);
        }

        /// <summary>Plays loot pickup audio feedback.</summary>
        public void PlayLoot()
        {
            if (_lootClip != null)
            {
                Play2D(_lootClip, 0.75f);
            }
            else
            {
                PlayInteract();
            }
        }

        /// <summary>Plays 3D placement audio at the given world point.</summary>
        public void PlayPlace(Vector3 worldPos)
        {
            if (_placeClip != null)
            {
                AudioSource.PlayClipAtPoint(_placeClip, worldPos, 0.75f);
            }
        }

        /// <summary>Plays 3D demolition audio at the given world point.</summary>
        public void PlayDemolish(Vector3 worldPos)
        {
            if (_demolishClip != null)
            {
                AudioSource.PlayClipAtPoint(_demolishClip, worldPos, 0.75f);
            }
        }

        private void Play2D(AudioClip clip, float volume = 1f)
        {
            if (clip != null && _uiSource != null)
            {
                _uiSource.PlayOneShot(clip, volume);
            }
        }

        #endregion
    }
}
