#if MEMORYPACK && ARAWN_REMEMBERME && TMP_PRESENT
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Arawn.CrystalSave.UI;

namespace Arawn.CrystalSave.Editor
{
    /// <summary>
    /// Creates a fully wired, TMP-based Dynamic Save Slot Menu from the
    /// hierarchy context menu. Every field on <see cref="SaveSlotsRuntimeUI"/>
    /// and <see cref="SaveSlotEntryUI"/> is populated so the user can
    /// immediately enter Play mode without manual setup.
    /// </summary>
    public static class CreateSaveMenuTMP
    {
        private const string MenuPath = "GameObject/Crystal Save/UI/Dynamic Save Slot Menu TMP";

        // ─── Apple Dark Mode palette ────────────────────────────────────
        // System backgrounds (layered depth)
        static readonly Color SystemBg        = C(28, 28, 30);       // #1C1C1E
        static readonly Color SecondaryBg     = C(44, 44, 46);       // #2C2C2E
        static readonly Color TertiaryBg      = C(58, 58, 60);       // #3A3A3C
        static readonly Color ElevatedBg      = C(50, 50, 52);       // #323234
        // Text
        static readonly Color LabelPrimary    = C(242, 242, 247);    // #F2F2F7
        static readonly Color LabelSecondary  = C(142, 142, 147);    // #8E8E93
        static readonly Color LabelTertiary   = C(99, 99, 102);      // #636366
        // Tints
        static readonly Color AccentBlue      = C(10, 132, 255);     // #0A84FF
        static readonly Color Destructive     = C(255, 69, 58);      // #FF453A
        static readonly Color ConfirmGreen    = C(48, 209, 88);      // #30D158
        // Fills & separators
        static readonly Color FillPrimary     = CA(120, 120, 128, 92);  // #78788060
        static readonly Color FillSecondary   = CA(120, 120, 128, 64);  // #78788040
        static readonly Color Separator       = CA(84, 84, 88, 163);    // #5454580A3
        static readonly Color InputFill       = C(36, 36, 38);       // #242426
        static readonly Color OverlayScrim    = CA(0, 0, 0, 178);    // 70% black
        static readonly Color ScrollTrack     = CA(255, 255, 255, 26);
        static readonly Color ScrollHandle    = CA(255, 255, 255, 77);
        static readonly Color PlaceholderCol  = CA(235, 235, 245, 76); // #EBEBF54C

        // Layout constants (8pt grid)
        const float CardHeight     = 200;
        const float CardPadding    = 20;   // inner padding of each card
        const float CardSpacing    = 12;   // gap between cards
        const float ContentPadding = 16;   // scroll content outer padding
        const float TitleBarH      = 64;
        const float ScreenshotW    = 230;  // matches Load + gap + Save width
        const float ScreenshotH    = 129;  // 16:9 ratio (230 × 9/16)
        const float BtnH           = 40;
        const float BtnW           = 110;
        const float BtnGap         = 10;
        const float SmallBtnW      = 90;
        const float ScrollbarW     = 6;    // thin macOS-style scrollbar
        const float RenameH        = 40;
        const float BtnY           = 12;    // button row y-offset from card bottom

        [MenuItem(MenuPath, false, 12)]
        public static void AddSaveMenuTMP(MenuCommand menuCommand)
        {
            EnsureEventSystem();

            // ── Root Canvas ─────────────────────────────────────────────
            var root = new GameObject("Save Menu TMP");
            root.layer = 5;
            root.AddComponent<RectTransform>();
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // ── Container ───────────────────────────────────────────────
            var container = MakeRect("SaveSlotsUI", root.transform);
            Stretch(container);

            // ── Full-screen background ──────────────────────────────────
            var bgRT = MakeRect("Menu Background Panel", container);
            Stretch(bgRT);
            var menuBgImg = bgRT.gameObject.AddComponent<Image>();
            menuBgImg.color = SystemBg;

            // ── Title bar ───────────────────────────────────────────────
            var titleBgRT = MakeRect("Title Background Panel", container);
            titleBgRT.anchorMin = new Vector2(0, 1);
            titleBgRT.anchorMax = new Vector2(1, 1);
            titleBgRT.pivot = new Vector2(0.5f, 1f);
            titleBgRT.anchoredPosition = Vector2.zero;
            titleBgRT.sizeDelta = new Vector2(0, TitleBarH);
            var titleBgImg = titleBgRT.gameObject.AddComponent<Image>();
            titleBgImg.color = ElevatedBg;

            // Separator line under title
            var sepRT = MakeRect("Title Separator", titleBgRT);
            sepRT.anchorMin = Vector2.zero;
            sepRT.anchorMax = new Vector2(1, 0);
            sepRT.pivot = new Vector2(0.5f, 0f);
            sepRT.anchoredPosition = Vector2.zero;
            sepRT.sizeDelta = new Vector2(0, 1);
            var sepImg = sepRT.gameObject.AddComponent<Image>();
            sepImg.color = Separator;

            // Title text
            var titleRT = MakeRect("Title Save Game Text (TMP)", titleBgRT);
            Stretch(titleRT);
            var titleTMP = titleRT.gameObject.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "Save Game";
            titleTMP.fontSize = 22;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = LabelPrimary;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.characterSpacing = 0.5f;

            // ── Scroll View ─────────────────────────────────────────────
            var scrollRT = MakeRect("Scroll View", container);
            Stretch(scrollRT, 0, 0, 0, -TitleBarH);
            var scrollRect = scrollRT.gameObject.AddComponent<ScrollRect>();
            var scrollBg = scrollRT.gameObject.AddComponent<Image>();
            scrollBg.color = SystemBg;

            // Viewport
            var viewportRT = MakeRect("Viewport", scrollRT);
            Stretch(viewportRT);
            var vpImg = viewportRT.gameObject.AddComponent<Image>();
            vpImg.color = Color.white;
            viewportRT.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            // Content
            var contentRT = MakeRect("Content", viewportRT);
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 300);
            var vlg = contentRT.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = CardSpacing;
            vlg.padding = new RectOffset(
                (int)ContentPadding, (int)ContentPadding,
                (int)ContentPadding, (int)ContentPadding);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = contentRT.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Thin scrollbar (macOS-style)
            var scrollbarRT = MakeRect("Scrollbar Vertical", scrollRT);
            scrollbarRT.anchorMin = new Vector2(1, 0);
            scrollbarRT.anchorMax = new Vector2(1, 1);
            scrollbarRT.pivot = new Vector2(1, 0.5f);
            scrollbarRT.anchoredPosition = new Vector2(-4, 0);
            scrollbarRT.sizeDelta = new Vector2(ScrollbarW, -24);
            var sbBg = scrollbarRT.gameObject.AddComponent<Image>();
            sbBg.color = ScrollTrack;

            var slidingRT = MakeRect("Sliding Area", scrollbarRT);
            Stretch(slidingRT);
            var handleRT = MakeRect("Handle", slidingRT);
            Stretch(handleRT);
            var handleImg = handleRT.gameObject.AddComponent<Image>();
            handleImg.color = ScrollHandle;

            var scrollbar = scrollbarRT.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handleRT;
            scrollbar.targetGraphic = handleImg;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scrollRect.content = contentRT;
            scrollRect.viewport = viewportRT;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.scrollSensitivity = 40f;
            scrollRect.decelerationRate = 0.135f;

            // ── Slot Prefab (hidden template) ───────────────────────────
            var slotPrefabGo = BuildSlotEntry(container);
            slotPrefabGo.SetActive(false);

            // ── Wire SaveSlotsRuntimeUI ─────────────────────────────────
            var runtimeUI = root.AddComponent<SaveSlotsRuntimeUI>();
            using (var so = new SerializedObject(runtimeUI))
            {
                so.FindProperty("scrollRect").objectReferenceValue          = scrollRect;
                so.FindProperty("slotPrefab").objectReferenceValue          = slotPrefabGo.GetComponent<SaveSlotEntryUI>();
                so.FindProperty("panel").objectReferenceValue               = menuBgImg;
                so.FindProperty("titleBackgroundPanel").objectReferenceValue = titleBgImg;
                so.FindProperty("showScreenshots").boolValue    = true;
                so.FindProperty("showMetadata").boolValue       = true;
                so.FindProperty("showCustomMetadata").boolValue = true;
                so.FindProperty("showRename").boolValue         = true;
                so.FindProperty("showLoad").boolValue           = true;
                so.FindProperty("showSave").boolValue           = true;
                so.FindProperty("showDelete").boolValue         = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObjectUtility.SetParentAndAlign(root, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, "Create Save Menu TMP");
            Selection.activeObject = root;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        }

        // ════════════════════════════════════════════════════════════════
        //  BUILD SLOT ENTRY — Apple card layout
        //
        //  ┌─────────────────────────────────────────────────────────────┐
        //  │  ┌──────────┐  Slot Name                     [Rename]      │
        //  │  │screenshot│  3/15/2026 2:11 AM                           │
        //  │  │  16:9    │  custom meta line …                          │
        //  │  └──────────┘                                              │
        //  │  ┌ Rename input (hidden) ──────────────────────────────┐   │
        //  │  │ Enter slot name…                                    │   │
        //  │  └────────────────────────────────────────────────────-┘   │
        //  │          [  Load  ]   [  Save  ]              [ Delete ]   │
        //  └─────────────────────────────────────────────────────────────┘
        // ════════════════════════════════════════════════════════════════
        static GameObject BuildSlotEntry(RectTransform parent)
        {
            var go = new GameObject("SaveSlotEntryUI");
            go.layer = 5;
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = CardHeight;
            le.flexibleWidth = 1;

            // ── Card background ─────────────────────────────────────────
            var bgRT = MakeRect("Background Panel", rt);
            Stretch(bgRT);
            var bgImg = bgRT.gameObject.AddComponent<Image>();
            bgImg.color = SecondaryBg;

            // ── Panel (subtle inner tint) ───────────────────────────────
            var panelRT = MakeRect("Panel", rt);
            Stretch(panelRT);
            var panelImg = panelRT.gameObject.AddComponent<Image>();
            panelImg.color = new Color(1, 1, 1, 0); // transparent; themes can override
            panelImg.raycastTarget = false;

            // ── Screenshot (same width as Load + Save buttons) ────────
            var ssRT = MakeRect("ScreenshotImage", rt);
            ssRT.anchorMin = ssRT.anchorMax = new Vector2(0, 0);
            ssRT.pivot = new Vector2(0, 0);
            ssRT.anchoredPosition = new Vector2(CardPadding, BtnH + BtnY + 4);
            ssRT.sizeDelta = new Vector2(ScreenshotW, ScreenshotH);
            var ssImg = ssRT.gameObject.AddComponent<Image>();
            ssImg.color = TertiaryBg;
            ssImg.preserveAspect = true;

            // ── Text block (right of screenshot) ────────────────────────
            float textLeft = CardPadding + ScreenshotW + 20; // screenshot + gap

            // Slot Name — large, semibold
            var nameRT = MakeRect("Slot Name (TMP)", rt);
            nameRT.anchorMin = new Vector2(0, 1);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.pivot = new Vector2(0, 1f);
            nameRT.anchoredPosition = new Vector2(textLeft, -CardPadding);
            nameRT.sizeDelta = new Vector2(-textLeft - CardPadding - 100, 28);
            var nameTMP = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
            nameTMP.text = "Slot Name";
            nameTMP.fontSize = 19;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.color = LabelPrimary;
            nameTMP.alignment = TextAlignmentOptions.TopLeft;

            // Metadata — secondary, smaller
            var metaRT = MakeRect("Metadata (TMP)", rt);
            metaRT.anchorMin = new Vector2(0, 1);
            metaRT.anchorMax = new Vector2(1, 1);
            metaRT.pivot = new Vector2(0, 1f);
            metaRT.anchoredPosition = new Vector2(textLeft, -CardPadding - 30);
            metaRT.sizeDelta = new Vector2(-textLeft - CardPadding, 20);
            var metaTMP = metaRT.gameObject.AddComponent<TextMeshProUGUI>();
            metaTMP.text = "Metadata";
            metaTMP.fontSize = 14;
            metaTMP.color = LabelSecondary;
            metaTMP.alignment = TextAlignmentOptions.TopLeft;

            // ── Custom Metadata Container ────────────────────────────────
            var customMetaRT = MakeRect("Custom Metadata Container", rt);
            customMetaRT.anchorMin = new Vector2(0, 1);
            customMetaRT.anchorMax = new Vector2(1, 1);
            customMetaRT.pivot = new Vector2(0, 1f);
            customMetaRT.anchoredPosition = new Vector2(textLeft, -CardPadding - 52);
            customMetaRT.sizeDelta = new Vector2(-textLeft - CardPadding, 36);

            // Custom metadata line template
            var customLineRT = MakeRect("CustomMetadataLine (TMP)", customMetaRT);
            customLineRT.anchorMin = new Vector2(0, 1);
            customLineRT.anchorMax = new Vector2(1, 1);
            customLineRT.pivot = new Vector2(0, 1f);
            customLineRT.sizeDelta = new Vector2(0, 16);
            var customLineTMPText = customLineRT.gameObject.AddComponent<TextMeshProUGUI>();
            customLineTMPText.text = "";
            customLineTMPText.fontSize = 12;
            customLineTMPText.color = LabelTertiary;
            customLineTMPText.alignment = TextAlignmentOptions.TopLeft;

            // ── Rename Input (TMP) — positioned below screenshot area ───
            var renameRT = MakeRect("Rename InputField (TMP)", rt);
            renameRT.anchorMin = new Vector2(0, 0);
            renameRT.anchorMax = new Vector2(1, 0);
            renameRT.pivot = new Vector2(0.5f, 0f);
            renameRT.anchoredPosition = new Vector2(0, BtnH + BtnGap + 8);
            renameRT.sizeDelta = new Vector2(-CardPadding * 2, RenameH);
            var renameImg = renameRT.gameObject.AddComponent<Image>();
            renameImg.color = InputFill;

            var textAreaRT = MakeRect("Text Area", renameRT);
            Stretch(textAreaRT, 12, 6, -12, -6);
            textAreaRT.gameObject.AddComponent<RectMask2D>();

            var phRT = MakeRect("Placeholder", textAreaRT);
            Stretch(phRT);
            var phTMP = phRT.gameObject.AddComponent<TextMeshProUGUI>();
            phTMP.text = "Enter slot name\u2026";
            phTMP.fontSize = 15;
            phTMP.fontStyle = FontStyles.Normal;
            phTMP.color = PlaceholderCol;
            ApplyNoWrap(phTMP);
            phTMP.overflowMode = TextOverflowModes.Ellipsis;

            var inputTextRT = MakeRect("Text", textAreaRT);
            Stretch(inputTextRT);
            var inputTextTMP = inputTextRT.gameObject.AddComponent<TextMeshProUGUI>();
            inputTextTMP.text = "";
            inputTextTMP.fontSize = 15;
            inputTextTMP.color = LabelPrimary;
            ApplyNoWrap(inputTextTMP);
            inputTextTMP.overflowMode = TextOverflowModes.Ellipsis;

            var tmpInput = renameRT.gameObject.AddComponent<TMP_InputField>();
            tmpInput.textViewport = textAreaRT;
            tmpInput.textComponent = inputTextTMP;
            tmpInput.placeholder = phTMP;
            tmpInput.fontAsset = inputTextTMP.font;
            tmpInput.pointSize = 15;
            tmpInput.caretColor = AccentBlue;
            tmpInput.selectionColor = CA(10, 132, 255, 77); // blue 30%

            // ── Button row — bottom of card ─────────────────────────────


            // Rename — top-right corner of card, compact
            var renameBtn = MakeButton("Rename Button (TMP)", rt, "Rename",
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-CardPadding, -CardPadding), new Vector2(SmallBtnW, 32),
                TertiaryBg, LabelPrimary, 13);

            // Load — left-aligned action button, blue tint
            var loadBtn = MakeButton("Load Button (TMP)", rt, "Load",
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(CardPadding, BtnY), new Vector2(BtnW, BtnH),
                AccentBlue, LabelPrimary, 15);

            // Save — next to Load
            var saveBtn = MakeButton("Save Button (TMP)", rt, "Save",
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(CardPadding + BtnW + BtnGap, BtnY), new Vector2(BtnW, BtnH),
                TertiaryBg, LabelPrimary, 15);

            // Delete — right-aligned, red
            var deleteBtn = MakeButton("Delete Button (TMP)", rt, "Delete",
                new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-CardPadding, BtnY), new Vector2(BtnW, BtnH),
                Destructive, LabelPrimary, 15);

            // ── Sync Overlay ────────────────────────────────────────────
            var overlayRT = MakeRect("SynchronizingOverlayPanel", rt);
            Stretch(overlayRT);
            var overlayImg = overlayRT.gameObject.AddComponent<Image>();
            overlayImg.color = OverlayScrim;
            overlayImg.raycastTarget = true;
            var overlayTxtRT = MakeRect("Text (TMP)", overlayRT);
            Stretch(overlayTxtRT);
            var overlayTMP = overlayTxtRT.gameObject.AddComponent<TextMeshProUGUI>();
            overlayTMP.text = "Synchronizing\u2026";
            overlayTMP.fontSize = 16;
            overlayTMP.color = LabelPrimary;
            overlayTMP.alignment = TextAlignmentOptions.Center;
            overlayRT.gameObject.SetActive(false);

            // ── Delete Confirmation ─────────────────────────────────────
            var confirmRT = MakeRect("ConfirmationWindow", rt);
            Stretch(confirmRT);
            var confirmBg = confirmRT.gameObject.AddComponent<Image>();
            confirmBg.color = CA(0, 0, 0, 204); // 80% scrim
            confirmBg.raycastTarget = true;

            var confirmLabelRT = MakeRect("Text (TMP)", confirmRT);
            confirmLabelRT.anchorMin = confirmLabelRT.anchorMax = new Vector2(0.5f, 0.62f);
            confirmLabelRT.sizeDelta = new Vector2(340, 50);
            var confirmLabelTMP = confirmLabelRT.gameObject.AddComponent<TextMeshProUGUI>();
            confirmLabelTMP.text = "Delete this save?";
            confirmLabelTMP.fontSize = 18;
            confirmLabelTMP.fontStyle = FontStyles.Bold;
            confirmLabelTMP.color = LabelPrimary;
            confirmLabelTMP.alignment = TextAlignmentOptions.Center;

            var confirmBtn = MakeButton("Confirm Delete Button (TMP)", confirmRT, "Delete",
                new Vector2(0.5f, 0.32f), new Vector2(1, 0.5f),
                new Vector2(-8, 0), new Vector2(140, 44),
                Destructive, LabelPrimary, 16);

            var cancelBtn = MakeButton("Cancel Button (TMP)", confirmRT, "Cancel",
                new Vector2(0.5f, 0.32f), new Vector2(0, 0.5f),
                new Vector2(8, 0), new Vector2(140, 44),
                TertiaryBg, LabelPrimary, 16);

            confirmRT.gameObject.SetActive(false);

            // ── Wire SaveSlotEntryUI ────────────────────────────────────
            var entryUI = go.AddComponent<SaveSlotEntryUI>();
            using (var so = new SerializedObject(entryUI))
            {
                so.FindProperty("textMode").enumValueIndex      = 1;
                so.FindProperty("background").objectReferenceValue = bgImg;
                so.FindProperty("screenshotImage").objectReferenceValue = ssImg;
                so.FindProperty("slotNameTMP").objectReferenceValue     = nameTMP;
                so.FindProperty("metadataTMP").objectReferenceValue     = metaTMP;
                so.FindProperty("customMetadataContainer").objectReferenceValue = customMetaRT;
                so.FindProperty("customMetadataLineTMPPrefab").objectReferenceValue = customLineTMPText;
                so.FindProperty("renameInputTMP").objectReferenceValue  = tmpInput;
                so.FindProperty("renameButton").objectReferenceValue    = renameBtn;
                so.FindProperty("loadButton").objectReferenceValue      = loadBtn;
                so.FindProperty("saveButton").objectReferenceValue      = saveBtn;
                so.FindProperty("deleteButton").objectReferenceValue    = deleteBtn;
                so.FindProperty("panel").objectReferenceValue           = panelImg;
                so.FindProperty("syncOverlay").objectReferenceValue     = overlayImg;
                so.FindProperty("deleteConfirmationWindow").objectReferenceValue = confirmRT.gameObject;
                so.FindProperty("confirmDeleteButton").objectReferenceValue = confirmBtn;
                so.FindProperty("cancelDeleteButton").objectReferenceValue  = cancelBtn;
                so.FindProperty("hideRenameInputOnStart").boolValue     = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return go;
        }

        // ════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════

        static Color C(int r, int g, int b) =>
            new Color(r / 255f, g / 255f, b / 255f, 1f);
        static Color CA(int r, int g, int b, int a) =>
            new Color(r / 255f, g / 255f, b / 255f, a / 255f);

        static void ApplyNoWrap(TextMeshProUGUI text)
        {
#if UNITY_6000_0_OR_NEWER
            text.textWrappingMode = TextWrappingModes.NoWrap;
#else
            text.enableWordWrapping = false;
#endif
        }

        static void EnsureEventSystem()
        {
#pragma warning disable CS0618
#if UNITY_2023_1_OR_NEWER
            var es = Object.FindFirstObjectByType<EventSystem>();
#else
            var es = Object.FindObjectOfType<EventSystem>();
#endif
#pragma warning restore CS0618
            if (es != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if USE_NEW_INPUT || (ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER)
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            var module = go.AddComponent<StandaloneInputModule>();
#if !UNITY_2020_1_OR_NEWER
            module.forceModuleActive = true;
#endif
#endif
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = 5;
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt,
            float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        /// <summary>Creates a button with per-button text color and font size.</summary>
        static Button MakeButton(string name, Transform parent, string label,
            Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPos, Vector2 size,
            Color bgColor, Color textColor, float fontSize)
        {
            var rt = MakeRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = rt.gameObject.AddComponent<Image>();
            img.color = bgColor;

            var btn = rt.gameObject.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.selectedColor    = cb.highlightedColor;
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;

            var txtRT = MakeRect("Text (TMP)", rt);
            Stretch(txtRT);
            var tmp = txtRT.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }
    }
}
#endif
#endif
