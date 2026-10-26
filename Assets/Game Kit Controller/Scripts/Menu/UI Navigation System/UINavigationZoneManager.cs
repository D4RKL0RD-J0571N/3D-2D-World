using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UINavigationZoneManager : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool navigationZoneEnabled = true;

    [Space]

    [SerializeField]
    private List<NavigationZone> zones = new List<NavigationZone> ();

    public IReadOnlyList<NavigationZone> Zones => zones;

    public int ZoneCount => zones.Count;

    public bool HasZones => zones != null && zones.Count > 0;

    public bool HasMultipleZones => zones != null && zones.Count > 1;

    public bool IsEnabled =>
        navigationZoneEnabled &&
        isActiveAndEnabled &&
        HasZones;

    //=====================================================
    // CURRENT ZONE
    //=====================================================

    [Space]
    [Header ("Debug")]
    [Space]

    [SerializeField]
    private int currentZoneIndex = -1;

    public int CurrentZoneIndex => currentZoneIndex;


    public bool IsValidZone (int index)
    {
        return index >= 0 &&
               index < zones.Count;
    }

    public void SetCurrentZone (int index)
    {
        if (!IsValidZone (index)) {
            currentZoneIndex = -1;
            return;
        }

        currentZoneIndex = index;
    }

    public void ClearCurrentZone ()
    {
        currentZoneIndex = -1;
    }

    public NavigationZone GetCurrentZone ()
    {
        return GetZone (currentZoneIndex);
    }

    public NavigationZone GetZone (int index)
    {
        if (!IsValidZone (index))
            return null;

        return zones [index];
    }

    //=====================================================
    // NAVIGATION
    //=====================================================

    public int GetNextZoneIndex (int index)
    {
        if (!HasZones)
            return -1;

        return (index + 1) % zones.Count;
    }

    public int GetPreviousZoneIndex (int index)
    {
        if (!HasZones)
            return -1;

        return (index - 1 + zones.Count) % zones.Count;
    }

    public int SelectNextZone ()
    {
        currentZoneIndex = GetNextZoneIndex (currentZoneIndex);

        return currentZoneIndex;
    }

    public int SelectPreviousZone ()
    {
        currentZoneIndex = GetPreviousZoneIndex (currentZoneIndex);

        return currentZoneIndex;
    }

    public int getNavigationZoneIndexByName (string zoneName)
    {
        if (!HasZones) {
            return -1;
        }

        int currentIndex = zones.FindIndex (s => s.zoneName.Equals (zoneName));

        if (currentIndex > -1) {
            currentZoneIndex = currentIndex;

            return currentZoneIndex;
        }

        return -1;
    }

    //=====================================================
    // LAST SELECTED
    //=====================================================

    public void SetLastSelected (int zoneIndex, UINavigationItem item)
    {
        NavigationZone zone = GetZone (zoneIndex);

        if (zone == null)
            return;

        zone.lastSelectedItem = item;
    }

    public UINavigationItem GetLastSelected (int zoneIndex)
    {
        NavigationZone zone = GetZone (zoneIndex);

        if (zone == null)
            return null;

        return zone.lastSelectedItem;
    }

    //=====================================================
    // DATA
    //=====================================================

    [System.Serializable]
    public class NavigationZone
    {
        [Header ("Main Settings")]
        [Space]

        public string zoneName = "Zone";

        [Tooltip ("If enabled, the last selected item of this zone will be restored when returning to it.")]
        public bool restoreLastSelection = true;

        [Space]
        [Header ("Scroll Rect Settings")]
        [Space]

        [Tooltip ("Optional ScrollRect used to focus items inside this zone.")]
        public ScrollRect scrollRect;

        public ScrollRectSelectionMode scrollRectSelectionMode = ScrollRectSelectionMode.First;

        [Space]
        [Header ("Default Item On Panel Settings")]
        [Space]

        public RectTransform panel;
        public PanelSelectionMode panelSelectionMode;

        [Space]
        [Header ("Default Item Settings")]
        [Space]

        [Tooltip ("Optional default item.")]
        public UINavigationItem defaultItem;

        [Tooltip ("Optional fixed UI items belonging to this zone.")]
        public List<UINavigationItem> items = new List<UINavigationItem> ();

        [HideInInspector]
        public UINavigationItem lastSelectedItem;

        public enum ScrollRectSelectionMode
        {
            First,
            Last
        }

        public enum PanelSelectionMode
        {
            First,
            Last
        }

        public bool HasScrollRect => scrollRect != null;

        public bool HasPanel => panel != null;

        public bool HasItems =>
            items != null &&
            items.Count > 0;
    }
}