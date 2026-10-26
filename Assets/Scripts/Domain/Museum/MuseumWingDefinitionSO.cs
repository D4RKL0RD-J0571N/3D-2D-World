namespace Domain.Museum
{
    using System.Collections.Generic;
    using UnityEngine;
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    /// <summary>
    /// ScriptableObject defining a thematic pavilion / wing within the Museum Showroom.
    /// </summary>
    [CreateAssetMenu(fileName = "Wing_", menuName = "3D 2D World/Museum/Wing Definition", order = 101)]
    public class MuseumWingDefinitionSO : ScriptableObject
    {
        #region Fields

#if ODIN_INSPECTOR
        [TitleGroup("Wing Configuration")]
#else
        [Header("Wing Configuration")]
#endif
        [SerializeField] private string wingId = "WING_ARMORY";

#if ODIN_INSPECTOR
        [TitleGroup("Wing Configuration")]
#endif
        [SerializeField] private string wingTitle = "The Armory & Proving Grounds";

#if ODIN_INSPECTOR
        [TitleGroup("Wing Configuration")]
        [ColorPalette]
#endif
        [SerializeField] private Color themeColor = new Color(0.9f, 0.4f, 0.2f, 1f);

#if ODIN_INSPECTOR
        [TitleGroup("Exhibits")]
        [ListDrawerSettings(ShowIndexLabels = true)]
#else
        [Header("Exhibits")]
#endif
        [SerializeField] private List<MuseumExhibitDefinitionSO> exhibits = new List<MuseumExhibitDefinitionSO>();

        #endregion

        #region Properties

        public string WingId => wingId;
        public string WingTitle => wingTitle;
        public Color ThemeColor => themeColor;
        public IReadOnlyList<MuseumExhibitDefinitionSO> Exhibits => exhibits;

        #endregion

        #region Initialization

        public void Setup(string id, string title, Color col, IEnumerable<MuseumExhibitDefinitionSO> exhibitList)
        {
            wingId = id;
            wingTitle = title;
            themeColor = col;
            exhibits.Clear();
            if (exhibitList != null)
            {
                exhibits.AddRange(exhibitList);
            }
        }

        #endregion
    }
}
