namespace Domain.Museum
{
    using UnityEngine;
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    /// <summary>
    /// ScriptableObject blueprint defining an exhibit in the Museum Showroom.
    /// </summary>
    [CreateAssetMenu(fileName = "Exhibit_", menuName = "3D 2D World/Museum/Exhibit Definition", order = 100)]
    public class MuseumExhibitDefinitionSO : ScriptableObject
    {
        #region Identity & Meta

#if ODIN_INSPECTOR
        [TitleGroup("Exhibit Identity")]
        [Required]
#else
        [Header("Exhibit Identity")]
#endif
        [SerializeField] private string exhibitId = "EXH_0001";

#if ODIN_INSPECTOR
        [TitleGroup("Exhibit Identity")]
#endif
        [SerializeField] private string displayName = "New Exhibit";

#if ODIN_INSPECTOR
        [TitleGroup("Exhibit Identity")]
#endif
        [SerializeField] private ExhibitCategory category = ExhibitCategory.Weapon;

#if ODIN_INSPECTOR
        [TitleGroup("Exhibit Identity")]
#endif
        [SerializeField] private string packageOrigin = "Source Asset Package";

#if ODIN_INSPECTOR
        [TitleGroup("Exhibit Identity")]
        [MultiLineProperty(3)]
#else
        [TextArea(2, 4)]
#endif
        [SerializeField] private string description = "Exhibit description and test objectives.";

        #endregion

        #region Visual Representation

#if ODIN_INSPECTOR
        [TitleGroup("Visuals & Prefab")]
#endif
        [SerializeField] private GameObject previewPrefab;

#if ODIN_INSPECTOR
        [TitleGroup("Visuals & Prefab")]
        [PreviewField(60, ObjectFieldAlignment.Left)]
#endif
        [SerializeField] private Sprite billboardSprite;

#if ODIN_INSPECTOR
        [TitleGroup("Visuals & Prefab")]
#endif
        [SerializeField] private bool isBillboardSprite;

#if ODIN_INSPECTOR
        [TitleGroup("Transform Adjustments")]
#endif
        [SerializeField] private Vector3 displayOffset = new Vector3(0f, 0.5f, 0f);

#if ODIN_INSPECTOR
        [TitleGroup("Transform Adjustments")]
#endif
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

#if ODIN_INSPECTOR
        [TitleGroup("Transform Adjustments")]
        [Range(0.1f, 5f)]
#endif
        [SerializeField] private float previewScale = 1.0f;

#if ODIN_INSPECTOR
        [TitleGroup("Transform Adjustments")]
#endif
        [SerializeField] private bool rotateContinuously = true;

        #endregion

        #region Properties

        public string ExhibitId => exhibitId;
        public string DisplayName => displayName;
        public ExhibitCategory Category => category;
        public string PackageOrigin => packageOrigin;
        public string Description => description;
        public GameObject PreviewPrefab => previewPrefab;
        public Sprite BillboardSprite => billboardSprite;
        public bool IsBillboardSprite => isBillboardSprite;
        public Vector3 DisplayOffset => displayOffset;
        public Vector3 RotationOffset => rotationOffset;
        public float PreviewScale => previewScale;
        public bool RotateContinuously => rotateContinuously;

        #endregion

        #region Initialization

        public void Setup(string id, string name, ExhibitCategory cat, string package, string desc, bool isBillboard = false)
        {
            exhibitId = id;
            displayName = name;
            category = cat;
            packageOrigin = package;
            description = desc;
            isBillboardSprite = isBillboard;
        }

        public void SetBillboardSprite(Sprite sprite)
        {
            billboardSprite = sprite;
            isBillboardSprite = sprite != null;
        }

        public void SetPreviewPrefab(GameObject prefab, float scale = 1.0f, Vector3 offset = default)
        {
            previewPrefab = prefab;
            isBillboardSprite = false;
            previewScale = scale;
            if (offset != default)
            {
                displayOffset = offset;
            }
        }

        #endregion
    }
}
