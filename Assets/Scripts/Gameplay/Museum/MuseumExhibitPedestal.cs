namespace Gameplay.Museum
{
    using Core;
    using Domain.Museum;
    using Systems.Museum;
    using UnityEngine;

    /// <summary>
    /// Interactive physical pedestal displaying a museum exhibit in the showroom.
    /// Publishes <see cref="ExhibitInspectedEvent"/> when the player enters its detection trigger.
    /// </summary>
    [AddComponentMenu("3D 2D World/Museum/Museum Exhibit Pedestal")]
    [SelectionBase]
    public class MuseumExhibitPedestal : Gameplay.Entities.BaseEntity
    {
        #region Inspector Fields

        [Header("Exhibit Data")]
        [SerializeField] private MuseumExhibitDefinitionSO exhibitDefinition;

        [Header("Display Hierarchy")]
        [SerializeField] private Transform displaySocket;
        [SerializeField] private float rotationSpeed = 30f;
        [SerializeField] private bool disablePreviewRotation = false;

        [Header("Interaction Radius")]
        [SerializeField] private float triggerRadius = 2.5f;

        #endregion

        #region Properties

        public override string InteractionPrompt => exhibitDefinition != null
            ? $"[E] Inspect {exhibitDefinition.DisplayName} (F2 Curator)"
            : "[E] Inspect Exhibit";

        public bool IsPlayerInRange => _isPlayerInRange;

        #endregion

        #region Private State

        private GameObject _spawnedPreviewInstance;
        private bool _isPlayerInRange;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureSocketAndCollider();
        }

        private void Start()
        {
            SpawnPreview();
        }

        private void Update()
        {
            if (!disablePreviewRotation && _spawnedPreviewInstance != null && exhibitDefinition != null && exhibitDefinition.RotateContinuously && !exhibitDefinition.IsBillboardSprite)
            {
                _spawnedPreviewInstance.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponentInParent<Gameplay.Player.Player_Movement>() != null)
            {
                _isPlayerInRange = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponentInParent<Gameplay.Player.Player_Movement>() != null)
            {
                _isPlayerInRange = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }

        #endregion

        #region Public Methods

        public override void OnInteract(GameObject user)
        {
            NotifyCuratorInspection();
        }

        public void SetExhibit(MuseumExhibitDefinitionSO definition)
        {
            exhibitDefinition = definition;
            SpawnPreview();
        }

        public void NotifyCuratorInspection()
        {
            if (exhibitDefinition == null) return;

            CuratorEvaluationRecord currentEval = null;
            if (ServiceLocator.TryGet<CuratorService>(out var curator))
            {
                currentEval = curator.GetEvaluation(exhibitDefinition.ExhibitId);
            }
            else
            {
                currentEval = new CuratorEvaluationRecord(exhibitDefinition.ExhibitId, CuratorRating.Unrated, string.Empty);
            }

            GameEventManager.Publish(new ExhibitInspectedEvent(exhibitDefinition, currentEval));
        }

        #endregion

        #region Internal Display Logic

        private void EnsureSocketAndCollider()
        {
            if (displaySocket == null)
            {
                var existingSocket = transform.Find("DisplaySocket");
                if (existingSocket != null)
                {
                    displaySocket = existingSocket;
                }
                else
                {
                    var socketObj = new GameObject("DisplaySocket");
                    socketObj.transform.SetParent(transform, false);
                    socketObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                    displaySocket = socketObj.transform;
                }
            }

            var sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider == null)
            {
                sphereCollider = gameObject.AddComponent<SphereCollider>();
            }
            sphereCollider.isTrigger = true;
            sphereCollider.radius = triggerRadius;

            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        private void SpawnPreview()
        {
            if (_spawnedPreviewInstance != null)
            {
                Destroy(_spawnedPreviewInstance);
                _spawnedPreviewInstance = null;
            }

            if (exhibitDefinition == null || displaySocket == null) return;

            if (exhibitDefinition.IsBillboardSprite && exhibitDefinition.BillboardSprite != null)
            {
                var spriteObj = new GameObject($"{exhibitDefinition.ExhibitId}_Billboard");
                spriteObj.transform.SetParent(displaySocket, false);
                spriteObj.transform.localPosition = exhibitDefinition.DisplayOffset;
                spriteObj.transform.localScale = Vector3.one * exhibitDefinition.PreviewScale;

                var sr = spriteObj.AddComponent<SpriteRenderer>();
                sr.sprite = exhibitDefinition.BillboardSprite;

                // Attach billboard utility to lock perspective
                spriteObj.AddComponent<Core.Utils.SpriteBillboard>();

                _spawnedPreviewInstance = spriteObj;
            }
            else if (exhibitDefinition.PreviewPrefab != null)
            {
                var instance = Instantiate(exhibitDefinition.PreviewPrefab, displaySocket);
                instance.transform.localPosition = exhibitDefinition.DisplayOffset;
                instance.transform.localEulerAngles = exhibitDefinition.RotationOffset;
                instance.transform.localScale = Vector3.one * exhibitDefinition.PreviewScale;

                // Disable any physics or combat components on the preview display item
                foreach (var rb in instance.GetComponentsInChildren<Rigidbody>())
                {
                    rb.isKinematic = true;
                }

                _spawnedPreviewInstance = instance;
            }
        }

        #endregion
    }
}
