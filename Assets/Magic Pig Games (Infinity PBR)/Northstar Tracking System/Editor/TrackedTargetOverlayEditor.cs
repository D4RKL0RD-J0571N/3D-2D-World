using System.Collections.Generic;
using MagicPigGames.Shared;
using UnityEditor;
using UnityEngine;
using static MagicPigGames.Shared.MagicPigEditor;

namespace MagicPigGames.Northstar
{
    [CustomEditor(typeof(TrackedTargetOverlay))]
    public class TrackedTargetOverlayEditor : Editor
    {
        TrackedTargetOverlay Target => (TrackedTargetOverlay)target;
        
        private List<TrackedTargetOverlay> _targets = new List<TrackedTargetOverlay>();
        private bool _cachedTargets = false;
        private int TargetsOfThisType => _targets.Count;
        
        private NorthstarOverlaySettings OverlaySettings => Target.overlaySettings;

        private bool ShowAllOverrides
        {
            get => EditorPrefs.GetBool("Tracked Target Show Overrides", false);
            set => EditorPrefs.SetBool("Tracked Target Show Overrides", value);
        }
        
        private bool ShowScreenOverlayOverrides
        {
            get => EditorPrefs.GetBool("Tracked Target Show Screen Overlay Overrides", false);
            set => EditorPrefs.SetBool("Tracked Target Show Screen Overlay Overrides", value);
        }
        
        private bool ShowEdgeOverlayOverrides
        {
            get => EditorPrefs.GetBool("Tracked Target Show Edge Overlay Overrides", false);
            set => EditorPrefs.SetBool("Tracked Target Show Edge Overlay Overrides", value);
        }
        
        private bool ShowArrowOverlayOverrides
        {
            get => EditorPrefs.GetBool("Tracked Target Show Arrow Overlay Overrides", false);
            set => EditorPrefs.SetBool("Tracked Target Show Arrow Overlay Overrides", value);
        }
        
        private bool ShowBarOverrides
        {
            get => EditorPrefs.GetBool("Tracked Target Show Navigation Bar Overlay Overrides", false);
            set => EditorPrefs.SetBool("Tracked Target Show Navigation Bar Overlay Overrides", value);
        }
        
        private bool ShowOtherOverrides
        {
            get => EditorPrefs.GetBool("Tracked Target Show Other Overlay Overrides", false);
            set => EditorPrefs.SetBool("Tracked Target Show Other Overlay Overrides", value);
        }
        
        private bool _shiftDown;
        
        public override void OnInspectorGUI()
        {
            LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/northstar-tracking-system/northstar-documentation/tracked-target-overlay");

            CheckShift();
            CacheTargets();

            Header2($"Tracked Target Overlay");
            Label($"{textNormal}Default {textHightlight}Northstar Overlay Settings{textColorEnd} from the {textHightlight}Northstar Overlay{textColorEnd} object will be used " +
                  $"unless you override them here. Each {textHightlight}Tracked Target Overlay{textColorEnd} can override " +
                  $"the values individually.{textColorEnd}", false, true, true);
            GreyLine();

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(Target, "Tracked Target Overlay");
            if (Target.overlaySettings != null)
                Undo.RecordObject(Target.overlaySettings, "Overlay Settings");

            ShowTargetOptions();
            GreyLine();

            if (Target.overlaySettings == null)
            {
                Label($"<i>{textNormal}Please select a {textHightlight}Northstar Overlay Settings{textColorEnd} object if you would like to " +
                      $"override any values for this specific {textHightlight}Tracked Target{textColorEnd}. Otherwise, default values will be used.{textColorEnd}</i>", false, true, true);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(Target);
                return;
            }

            ShowOverrides();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(Target);
                if (Target.overlaySettings != null)
                    EditorUtility.SetDirty(Target.overlaySettings);
            }
        }
        
        private void CacheTargets()
        {
            if (_cachedTargets) return;
            GlobalNorthstarSettings.Instance.AddTrackedTargetOverlayPrefab(Target);

            _targets = GlobalNorthstarSettings.Instance.TrackedTargetOverlayPrefabsByType(Target.targetType);
            _targets.Remove(Target);
            
            _cachedTargets = true;
        }

        private void CheckShift()
        {
            if (KeyShift && !_shiftDown)
                _shiftDown = true;
            
            if (!KeyShift && _shiftDown)
                _shiftDown = false;
        }
        
        private void ShowOverrides()
        {
            StartRow();
            ShowAllOverrides = OnOffButton(ShowAllOverrides);
            Header2("Overrides");
            EndRow();
            Label($"{textNormal}Toggle on each aspect you'd like to override. The values from the {textHightlight}Northstar Overlay Settings{textColorEnd} object " +
                  $"you've set will be used instead of the default values.{textColorEnd}", false, true, true);

            if (!ShowAllOverrides)
                return;
            
            Space();
            Label($"{textWarning}Changes affect the Scriptable Object <b>{textHightlight}{Target.overlaySettings.name}{textColorEnd}</b> and will apply to all which utilize it.{textColorEnd}", false, true, true);
            if (GUILayout.Button("Make Unique (copy settings to this object only)"))
            {
                Undo.RecordObject(Target, "Make Overlay Settings Unique");
                var copy = UnityEngine.Object.Instantiate(Target.overlaySettings);
                copy.name = Target.overlaySettings.name + " (" + Target.name + ")";
                var path = UnityEditor.AssetDatabase.GetAssetPath(Target.overlaySettings);
                var dir = System.IO.Path.GetDirectoryName(path);
                var uniquePath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(dir + "/" + copy.name + ".asset");
                UnityEditor.AssetDatabase.CreateAsset(copy, uniquePath);
                UnityEditor.AssetDatabase.SaveAssets();
                Target.overlaySettings = copy;
                UnityEditor.EditorUtility.SetDirty(Target);
            }
            if (TargetsOfThisType > 0)
            {
                Space();
                Label($"{textNormal}There are {textHightlight}<b>{TargetsOfThisType} {Target.targetType}</b> {textColorEnd}other {textHightlight}Tracked " +
                      $"Target Overlay{textColorEnd} objects in the project (not scene). {textHightlight}Hold " +
                      $"\"Shift\"{textColorEnd} to modify the settings for all objects at once. <i>" +
                      $"\n\nWhen {textHightlight}holding shift{textColorEnd}, override bool changes will affect all other objects, changes affecting {textHightlight}Northstar Overlay Settings{textColorEnd} will only affect " +
                      $"those which are using {textHightlight}{Target.overlaySettings.name}{textColorEnd}!" +
                      $"</i>{textColorEnd}", false, true, true);
            }
            Space();
            ShowEnablement();
            GreyLine();
            ShowScreenOverrides();
            GreyLine();
            ShowEdgeOverrides();
            GreyLine();
            ShowArrowOverrides();
            GreyLine();
            ShowNavigationBarOverrides();
            GreyLine();
            ShowOptionsOverrides();
        }
        
        private void ShowEnablement()
        {
            Header2($"System Toggle");
            
            // Caching original target values
            bool cacheOverrideScreenOverlay = Target.overrideScreenOverlay;
            bool cacheOverrideEdgeOverlay = Target.overrideEdgeOverlay;
            bool cacheOverrideNavigationBar = Target.overrideNavigationBar;
            
            StartRow();
            Target.overrideScreenOverlay = Check(Target.overrideScreenOverlay, 25);
            Label($"<b>Screen Overlay</b>", 150, false, false, true);
            if (Target.overrideScreenOverlay)
                OverlaySettings.enableScreenOverlay = LeftCheck($"Screen Overlay {(OverlaySettings.enableScreenOverlay ? "Enabled" : "Disabled")}", OverlaySettings.enableScreenOverlay, 200);
            else
                Label($"{textMuted}Default Screen Enablement Values{textColorEnd}", false, false, true);
            EndRow();

            StartRow();
            Target.overrideEdgeOverlay = Check(Target.overrideEdgeOverlay, 25);
            Label($"<b>Edge Overlay</b>", 150, false, false, true);
            if (Target.overrideEdgeOverlay)
                OverlaySettings.enableEdgeOverlay = LeftCheck($"Edge Overlay {(OverlaySettings.enableEdgeOverlay ? "Enabled" : "Disabled")}", OverlaySettings.enableEdgeOverlay, 200);
            else
                Label($"{textMuted}Default Edge Enablement Values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideNavigationBar = Check(Target.overrideNavigationBar, 25);
            Label($"<b>Navigation Bar</b>", 150, false, false, true);
            if (Target.overrideNavigationBar)
                OverlaySettings.enableNavigationBar = LeftCheck($"Navigation Bar {(OverlaySettings.enableNavigationBar ? "Enabled" : "Disabled")}", OverlaySettings.enableNavigationBar, 200);
            else
                Label($"{textMuted}Default Navigation Bar Enablement Values{textColorEnd}", false, false, true);
            EndRow();

            if (KeyShift)
            {
                if (cacheOverrideScreenOverlay != Target.overrideScreenOverlay)
                    _targets.ForEach(x => x.overrideScreenOverlay = Target.overrideScreenOverlay);
                    
                if (cacheOverrideEdgeOverlay != Target.overrideEdgeOverlay)
                    _targets.ForEach(x => x.overrideEdgeOverlay = Target.overrideEdgeOverlay);
                    
                if (cacheOverrideNavigationBar != Target.overrideNavigationBar)
                    _targets.ForEach(x => x.overrideNavigationBar = Target.overrideNavigationBar);
            }
        }
        
        private void ShowOptionsOverrides()
        {
            StartRow();
            ShowOtherOverrides = OnOffButton(ShowOtherOverrides);
            Header2($"Option Overrides");
            EndRow();
            
            if (!ShowOtherOverrides)
                return;
            
            // Caching original target values
            var cacheOverrideScreenDistance = Target.overrideScreenDistance;
            var cacheOverrideEdgeDistance = Target.overrideEdgeDistance;
            var cacheOverrideOffset = Target.overrideOffset;
            var cacheOverrideIconOffsetFromArrow = Target.overrideIconOffsetFromArrow;

            // Caching original OverlaySettings values
            var cacheScreenDistanceMin = OverlaySettings.screenDistanceMin;
            var cacheScreenDistanceMax = OverlaySettings.screenDistanceMax;
            var cacheEdgeDistanceMin = OverlaySettings.edgeDistanceMin;
            var cacheEdgeDistanceMax = OverlaySettings.edgeDistanceMax;
            var cacheOffset = OverlaySettings.offset;
            var cacheIconOffsetFromArrow = OverlaySettings.iconOffsetFromArrow;
            
            StartRow();
            Target.overrideScreenDistance = Check(Target.overrideScreenDistance, 25);
            Label($"Screen Distance", 150);
            if (Target.overrideScreenDistance)
            {
                Label($"Min", 25);
                OverlaySettings.screenDistanceMin = DelayedFloat(OverlaySettings.screenDistanceMin, 50);
                if (OverlaySettings.screenDistanceMin < 0)
                    OverlaySettings.screenDistanceMin = 0f;
                if (OverlaySettings.screenDistanceMin >= OverlaySettings.screenDistanceMax)
                    OverlaySettings.screenDistanceMin = OverlaySettings.screenDistanceMax - 1;
                Label($"Max", 25);
                OverlaySettings.screenDistanceMax = DelayedFloat(OverlaySettings.screenDistanceMax, 50);
                if (OverlaySettings.screenDistanceMax <= OverlaySettings.screenDistanceMin)
                    OverlaySettings.screenDistanceMax = OverlaySettings.screenDistanceMin + 1;
            }
            else
                Label($"{textMuted}Default Distance values{textColorEnd}", false, false, true);

            EndRow();
            
            StartRow();
            Target.overrideEdgeDistance = Check(Target.overrideEdgeDistance, 25);
            Label($"Edge Distance", 150);
            if (Target.overrideEdgeDistance)
            {
                Label($"Min", 25);
                OverlaySettings.edgeDistanceMin = DelayedFloat(OverlaySettings.edgeDistanceMin, 50);
                if (OverlaySettings.edgeDistanceMin < 0)
                    OverlaySettings.edgeDistanceMin = 0f;
                if (OverlaySettings.edgeDistanceMin >= OverlaySettings.edgeDistanceMax)
                    OverlaySettings.edgeDistanceMin = OverlaySettings.edgeDistanceMax - 1;
                Label($"Max", 25);
                OverlaySettings.edgeDistanceMax = DelayedFloat(OverlaySettings.edgeDistanceMax, 50);
                if (OverlaySettings.edgeDistanceMax <= OverlaySettings.edgeDistanceMin)
                    OverlaySettings.edgeDistanceMax = OverlaySettings.edgeDistanceMin + 1;
            }
            else
                Label($"{textMuted}Default Distance values{textColorEnd}", false, false, true);

            EndRow();
            
            StartRow();
            Target.overrideOffset = Check(Target.overrideOffset, 25);
            Label($"Edge Offset", 150);
            if (Target.overrideOffset)
                OverlaySettings.offset = Mathf.Clamp(DelayedInt(OverlaySettings.offset, 150), 0, 99999999);
            else
                Label($"{textMuted}Default Offset values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideIconOffsetFromArrow = Check(Target.overrideIconOffsetFromArrow, 25);
            Label($"Offset from Arrow", 150);
            if (Target.overrideIconOffsetFromArrow)
                OverlaySettings.iconOffsetFromArrow = Mathf.Clamp(DelayedInt(OverlaySettings.iconOffsetFromArrow, 150), 0, 99999999);
            else
                Label($"{textMuted}Default Offset from Arrow values{textColorEnd}", false, false, true);
            EndRow();

            if (!KeyShift) return;
            
            if (cacheOverrideScreenDistance != Target.overrideScreenDistance)
                _targets.ForEach(x => x.overrideScreenDistance = Target.overrideScreenDistance);

            if (cacheOverrideEdgeDistance != Target.overrideEdgeDistance)
                _targets.ForEach(x => x.overrideEdgeDistance = Target.overrideEdgeDistance);

            if (cacheOverrideOffset != Target.overrideOffset)
                _targets.ForEach(x => x.overrideOffset = Target.overrideOffset);
                    
            if (cacheOverrideIconOffsetFromArrow != Target.overrideIconOffsetFromArrow)
                _targets.ForEach(x => x.overrideIconOffsetFromArrow = Target.overrideIconOffsetFromArrow);

            if (cacheScreenDistanceMin != OverlaySettings.screenDistanceMin)
                _targets.ForEach(x => x.overlaySettings.screenDistanceMin = OverlaySettings.screenDistanceMin);
                
            if (cacheScreenDistanceMax != OverlaySettings.screenDistanceMax)
                _targets.ForEach(x => x.overlaySettings.screenDistanceMax = OverlaySettings.screenDistanceMax);
               
            if (cacheEdgeDistanceMin != OverlaySettings.edgeDistanceMin)
                _targets.ForEach(x => x.overlaySettings.edgeDistanceMin = OverlaySettings.edgeDistanceMin);

            if (cacheEdgeDistanceMax != OverlaySettings.edgeDistanceMax)
                _targets.ForEach(x => x.overlaySettings.edgeDistanceMax = OverlaySettings.edgeDistanceMax);
                
            if (cacheOffset != OverlaySettings.offset)
                _targets.ForEach(x => x.overlaySettings.offset = OverlaySettings.offset);
                
            if (cacheIconOffsetFromArrow != OverlaySettings.iconOffsetFromArrow)
                _targets.ForEach(x => x.overlaySettings.iconOffsetFromArrow = OverlaySettings.iconOffsetFromArrow);
        }
        
        private void ShowScreenOverrides()
        {
            StartRow();
            ShowScreenOverlayOverrides = OnOffButton(ShowScreenOverlayOverrides);
            Header2($"Screen Overlay Overrides");
            EndRow();
            
            if (!ShowScreenOverlayOverrides)
                return;
            
            // Caching original target values
            var cacheOverrideScreenSprite = Target.overrideScreenSprite;
            var cacheOverrideScreenSpriteColor = Target.overrideScreenSpriteColor;
            var cacheOverrideScreenSpriteSize = Target.overrideScreenSpriteSize;
            var cacheOverrideScreenUseSizeCurve = Target.overrideScreenUseSizeCurve;
            var cacheOverrideScreenSizeCurve = Target.overrideScreenSizeCurve;
            var cacheOverrideScreenSpriteOpacity = Target.overrideScreenSpriteOpacity;
            var cacheOverrideScreenUseOpacityCurve = Target.overrideScreenUseOpacityCurve;
            var cacheOverrideScreenOpacityCurve = Target.overrideScreenOpacityCurve;

            // Caching original OverlaySettings values
            var cacheScreenSprite = OverlaySettings.screenSprite;
            var cacheScreenSpriteColor = OverlaySettings.screenSpriteColor;
            var cacheScreenSpriteSize = OverlaySettings.screenSpriteSize;
            var cacheScreenSpriteSizeUseCurve = OverlaySettings.screenSpriteSizeUseCurve;
            var cacheScreenSpriteSizeCurve = OverlaySettings.screenSpriteSizeCurve;
            var cacheScreenSpriteOpacity = OverlaySettings.screenSpriteOpacity;
            var cacheScreenSpriteOpacityUseCurve = OverlaySettings.screenSpriteOpacityUseCurve;
            var cacheScreenSpriteOpacityCurve = OverlaySettings.screenSpriteOpacityCurve;
            
            StartRow();
            Target.overrideScreenSprite = Check(Target.overrideScreenSprite, 25);
            Label($"Icon", 150);
            if (Target.overrideScreenSprite)
            {
                OverlaySettings.screenSprite = Object(OverlaySettings.screenSprite, typeof(Sprite), 150, false) as Sprite;
                if (OverlaySettings.screenSprite == null)
                    Label($"{textError}<b>Sprite must be populated!</b>{textColorEnd}", false, false, true);
            }
            else
                Label($"{textMuted}Default Sprite{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideScreenSpriteColor = Check(Target.overrideScreenSpriteColor, 25);
            Label($"Color", 150);
            if (Target.overrideScreenSpriteColor)
                OverlaySettings.screenSpriteColor = ColorField(OverlaySettings.screenSpriteColor, 150);
            else
                Label($"{textMuted}Default Edge Color{textColorEnd}", false, false, true);
            EndRow();
            
            BlackLine();
            
            StartRow();
            Target.overrideScreenSpriteSize = Check(Target.overrideScreenSpriteSize, 25);
            Label($"Size", 150);
            if (Target.overrideScreenSpriteSize)
                OverlaySettings.screenSpriteSize =Mathf.Clamp(DelayedInt(OverlaySettings.screenSpriteSize, 150), 0, 99999999);
            else
                Label($"{textMuted}Default Size values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideScreenUseSizeCurve = Check(Target.overrideScreenUseSizeCurve, 25);
            Label($"Use Size Curve", 150);
            if (Target.overrideScreenUseSizeCurve)
                OverlaySettings.screenSpriteSizeUseCurve = LeftCheck($"Size Curve {(OverlaySettings.screenSpriteSizeUseCurve ? "Enabled" : "Disabled")}", OverlaySettings.screenSpriteSizeUseCurve, 200);
            else
                Label($"{textMuted}Default use Curve values{textColorEnd}", false, false, true);
            EndRow();

            if (Target.overrideScreenUseSizeCurve && !OverlaySettings.screenSpriteSizeUseCurve)
                Label($"{textMuted}<i>Size Curve Disabled</i>{textColorEnd}", false, true, true);
            else
            {
                StartRow();
                Target.overrideScreenSizeCurve = Check(Target.overrideScreenSizeCurve, 25);
                Label($"Size Curve", 150);
                if (Target.overrideScreenSizeCurve)
                    OverlaySettings.screenSpriteSizeCurve = Curve(OverlaySettings.screenSpriteSizeCurve, -1, 200, 20);
                else
                    Label($"{textMuted}Default Size Curve values{textColorEnd}", false, false, true);
                EndRow();
            }
            
            BlackLine();
            
            StartRow();
            Target.overrideScreenSpriteOpacity = Check(Target.overrideScreenSpriteOpacity, 25);
            Label($"Opacity", 150);
            if (Target.overrideScreenSpriteOpacity)
                OverlaySettings.screenSpriteOpacity = Mathf.Clamp01(DelayedFloat(OverlaySettings.screenSpriteOpacity, 150));
            else
                Label($"{textMuted}Default Opacity values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideScreenUseOpacityCurve = Check(Target.overrideScreenUseOpacityCurve, 25);
            Label($"Use Opacity Curve", 150);
            if (Target.overrideScreenUseOpacityCurve)
                OverlaySettings.screenSpriteOpacityUseCurve = LeftCheck($"Opacity Curve {(OverlaySettings.screenSpriteOpacityUseCurve ? "Enabled" : "Disabled")}", OverlaySettings.screenSpriteOpacityUseCurve, 200);
            else
                Label($"{textMuted}Default use Curve values{textColorEnd}", false, false, true);
            EndRow();

            if (Target.overrideScreenUseOpacityCurve && !OverlaySettings.screenSpriteOpacityUseCurve)
                Label($"{textMuted}<i>Opacity Curve Disabled</i>{textColorEnd}", false, true, true);
            else
            {
                StartRow();
                Target.overrideScreenOpacityCurve = Check(Target.overrideScreenOpacityCurve, 25);
                Label($"Opacity Curve", 150);
                if (Target.overrideScreenOpacityCurve)
                    OverlaySettings.screenSpriteOpacityCurve = Curve(OverlaySettings.screenSpriteOpacityCurve, -1, 200, 20);
                else
                    Label($"{textMuted}Default Opacity Curve values{textColorEnd}", false, false, true);
                EndRow();
            }

            if (!KeyShift) return;
            
            if (cacheOverrideScreenSprite != Target.overrideScreenSprite)
                _targets.ForEach(x => x.overrideScreenSprite = Target.overrideScreenSprite);

            if (cacheOverrideScreenSpriteColor != Target.overrideScreenSpriteColor)
                _targets.ForEach(x => x.overrideScreenSpriteColor = Target.overrideScreenSpriteColor);

            if (cacheOverrideScreenSpriteSize != Target.overrideScreenSpriteSize)
                _targets.ForEach(x => x.overrideScreenSpriteSize = Target.overrideScreenSpriteSize);

            if (cacheOverrideScreenUseSizeCurve != Target.overrideScreenUseSizeCurve)
                _targets.ForEach(x => x.overrideScreenUseSizeCurve = Target.overrideScreenUseSizeCurve);
                    
            if (cacheOverrideScreenSizeCurve != Target.overrideScreenSizeCurve)
                _targets.ForEach(x => x.overrideScreenSizeCurve = Target.overrideScreenSizeCurve);

            if (cacheOverrideScreenSpriteOpacity != Target.overrideScreenSpriteOpacity)
                _targets.ForEach(x => x.overrideScreenSpriteOpacity = Target.overrideScreenSpriteOpacity);

            if (cacheOverrideScreenUseOpacityCurve != Target.overrideScreenUseOpacityCurve)
                _targets.ForEach(x => x.overrideScreenUseOpacityCurve = Target.overrideScreenUseOpacityCurve);

            if (cacheOverrideScreenOpacityCurve != Target.overrideScreenOpacityCurve)
                _targets.ForEach(x => x.overrideScreenOpacityCurve = Target.overrideScreenOpacityCurve);

            if (cacheScreenSprite != OverlaySettings.screenSprite)
                _targets.ForEach(x => x.overlaySettings.screenSprite = OverlaySettings.screenSprite);
                
            if (cacheScreenSpriteColor != OverlaySettings.screenSpriteColor)
                _targets.ForEach(x => x.overlaySettings.screenSpriteColor = OverlaySettings.screenSpriteColor);
                   
            if (cacheScreenSpriteSize != OverlaySettings.screenSpriteSize)
                _targets.ForEach(x => x.overlaySettings.screenSpriteSize = OverlaySettings.screenSpriteSize);

            if (cacheScreenSpriteSizeUseCurve != OverlaySettings.screenSpriteSizeUseCurve)
                _targets.ForEach(x => x.overlaySettings.screenSpriteSizeUseCurve = OverlaySettings.screenSpriteSizeUseCurve);
                
            if (cacheScreenSpriteSizeCurve != OverlaySettings.screenSpriteSizeCurve)
                _targets.ForEach(x => x.overlaySettings.screenSpriteSizeCurve = new AnimationCurve(OverlaySettings.screenSpriteSizeCurve.keys));
                
            if (cacheScreenSpriteOpacity != OverlaySettings.screenSpriteOpacity)
                _targets.ForEach(x => x.overlaySettings.screenSpriteOpacity = OverlaySettings.screenSpriteOpacity);
                
            if (cacheScreenSpriteOpacityUseCurve != OverlaySettings.screenSpriteOpacityUseCurve)
                _targets.ForEach(x => x.overlaySettings.screenSpriteOpacityUseCurve = OverlaySettings.screenSpriteOpacityUseCurve);
                
            if (cacheScreenSpriteOpacityCurve != OverlaySettings.screenSpriteOpacityCurve)
                _targets.ForEach(x => x.overlaySettings.screenSpriteOpacityCurve = new AnimationCurve(OverlaySettings.screenSpriteOpacityCurve.keys));
        }
        
        private void ShowEdgeOverrides()
        {
            StartRow();
            ShowEdgeOverlayOverrides = OnOffButton(ShowEdgeOverlayOverrides);
            Header2($"Edge Overlay Overrides");
            EndRow();
            
            if (!ShowEdgeOverlayOverrides)
                return;
            
            // Caching original target values
            var cacheOverrideEdgeSprite = Target.overrideEdgeSprite;
            var cacheOverrideEdgeSpriteColor = Target.overrideEdgeSpriteColor;
            var cacheOverrideEdgeSpriteSize = Target.overrideEdgeSpriteSize;
            var cacheOverrideEdgeUseSizeCurve = Target.overrideEdgeUseSizeCurve;
            var cacheOverrideEdgeSizeCurve = Target.overrideEdgeSizeCurve;
            var cacheOverrideEdgeSpriteOpacity = Target.overrideEdgeSpriteOpacity;
            var cacheOverrideEdgeUseOpacityCurve = Target.overrideEdgeUseOpacityCurve;
            var cacheOverrideEdgeOpacityCurve = Target.overrideEdgeOpacityCurve;

            // Caching original OverlaySettings values
            var cacheEdgeSprite = OverlaySettings.edgeSprite;
            var cacheEdgeSpriteColor = OverlaySettings.edgeSpriteColor;
            var cacheEdgeSpriteSize = OverlaySettings.edgeSpriteSize;
            var cacheEdgeSpriteSizeUseCurve = OverlaySettings.edgeSpriteSizeUseCurve;
            var cacheEdgeSpriteSizeCurve = OverlaySettings.edgeSpriteSizeCurve;
            var cacheEdgeSpriteOpacity = OverlaySettings.edgeSpriteOpacity;
            var cacheEdgeSpriteOpacityUseCurve = OverlaySettings.edgeSpriteOpacityUseCurve;
            var cacheEdgeSpriteOpacityCurve = OverlaySettings.edgeSpriteOpacityCurve;
            
            StartRow();
            Target.overrideEdgeSprite = Check(Target.overrideEdgeSprite, 25);
            Label($"Icon", 150);
            if (Target.overrideEdgeSprite)
            {
                OverlaySettings.edgeSprite = Object(OverlaySettings.edgeSprite, typeof(Sprite), 150, false) as Sprite;
                if (OverlaySettings.edgeSprite == null)
                    Label($"{textError}<b>Sprite must be populated!</b>{textColorEnd}", false, false, true);
            }
            else
                Label($"{textMuted}Default Sprite{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideEdgeSpriteColor = Check(Target.overrideEdgeSpriteColor, 25);
            Label($"Color", 150);
            if (Target.overrideEdgeSpriteColor)
                OverlaySettings.edgeSpriteColor = ColorField(OverlaySettings.edgeSpriteColor, 150);
            else
                Label($"{textMuted}Default Edge Color{textColorEnd}", false, false, true);
            EndRow();
            
            BlackLine();
            
            StartRow();
            Target.overrideEdgeSpriteSize = Check(Target.overrideEdgeSpriteSize, 25);
            Label($"Size", 150);
            if (Target.overrideEdgeSpriteSize)
                OverlaySettings.edgeSpriteSize =Mathf.Clamp(DelayedInt(OverlaySettings.edgeSpriteSize, 150), 0, 99999999);
            else
                Label($"{textMuted}Default Size values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideEdgeUseSizeCurve = Check(Target.overrideEdgeUseSizeCurve, 25);
            Label($"Use Size Curve", 150);
            if (Target.overrideEdgeUseSizeCurve)
                OverlaySettings.edgeSpriteSizeUseCurve = LeftCheck($"Size Curve {(OverlaySettings.edgeSpriteSizeUseCurve ? "Enabled" : "Disabled")}", OverlaySettings.edgeSpriteSizeUseCurve, 200);
            else
                Label($"{textMuted}Default use Curve values{textColorEnd}", false, false, true);
            EndRow();

            if (Target.overrideEdgeUseSizeCurve && !OverlaySettings.edgeSpriteSizeUseCurve)
                Label($"{textMuted}<i>Size Curve Disabled</i>{textColorEnd}", false, true, true);
            else
            {
                StartRow();
                Target.overrideEdgeSizeCurve = Check(Target.overrideEdgeSizeCurve, 25);
                Label($"Size Curve", 150);
                if (Target.overrideEdgeSizeCurve)
                    OverlaySettings.edgeSpriteSizeCurve = Curve(OverlaySettings.edgeSpriteSizeCurve, -1, 200, 20);
                else
                    Label($"{textMuted}Default Size Curve values{textColorEnd}", false, false, true);
                EndRow();
            }
            
            BlackLine();
            
            StartRow();
            Target.overrideEdgeSpriteOpacity = Check(Target.overrideEdgeSpriteOpacity, 25);
            Label($"Opacity", 150);
            if (Target.overrideEdgeSpriteOpacity)
                OverlaySettings.edgeSpriteOpacity = Mathf.Clamp01(DelayedFloat(OverlaySettings.edgeSpriteOpacity, 150));
            else
                Label($"{textMuted}Default Opacity values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideEdgeUseOpacityCurve = Check(Target.overrideEdgeUseOpacityCurve, 25);
            Label($"Use Opacity Curve", 150);
            if (Target.overrideEdgeUseOpacityCurve)
                OverlaySettings.edgeSpriteOpacityUseCurve = LeftCheck($"Opacity Curve {(OverlaySettings.edgeSpriteOpacityUseCurve ? "Enabled" : "Disabled")}", OverlaySettings.edgeSpriteOpacityUseCurve, 200);
            else
                Label($"{textMuted}Default use Curve values{textColorEnd}", false, false, true);
            EndRow();

            if (Target.overrideEdgeUseOpacityCurve && !OverlaySettings.edgeSpriteOpacityUseCurve)
                Label($"{textMuted}<i>Opacity Curve Disabled</i>{textColorEnd}", false, true, true);
            else
            {
                StartRow();
                Target.overrideEdgeOpacityCurve = Check(Target.overrideEdgeOpacityCurve, 25);
                Label($"Opacity Curve", 150);
                if (Target.overrideEdgeOpacityCurve)
                    OverlaySettings.edgeSpriteOpacityCurve = Curve(OverlaySettings.edgeSpriteOpacityCurve, -1, 200, 20);
                else
                    Label($"{textMuted}Default Opacity Curve values{textColorEnd}", false, false, true);
                EndRow();
            }

            if (!KeyShift) return;
            
            if (cacheOverrideEdgeSprite != Target.overrideEdgeSprite)
                _targets.ForEach(x => x.overrideEdgeSprite = Target.overrideEdgeSprite);

            if (cacheOverrideEdgeSpriteColor != Target.overrideEdgeSpriteColor)
                _targets.ForEach(x => x.overrideEdgeSpriteColor = Target.overrideEdgeSpriteColor);

            if (cacheOverrideEdgeSpriteSize != Target.overrideEdgeSpriteSize)
                _targets.ForEach(x => x.overrideEdgeSpriteSize = Target.overrideEdgeSpriteSize);

            if (cacheOverrideEdgeUseSizeCurve != Target.overrideEdgeUseSizeCurve)
                _targets.ForEach(x => x.overrideEdgeUseSizeCurve = Target.overrideEdgeUseSizeCurve);

            if (cacheOverrideEdgeSizeCurve != Target.overrideEdgeSizeCurve)
                _targets.ForEach(x => x.overrideEdgeSizeCurve = Target.overrideEdgeSizeCurve);

            if (cacheOverrideEdgeSpriteOpacity != Target.overrideEdgeSpriteOpacity)
                _targets.ForEach(x => x.overrideEdgeSpriteOpacity = Target.overrideEdgeSpriteOpacity);

            if (cacheOverrideEdgeUseOpacityCurve != Target.overrideEdgeUseOpacityCurve)
                _targets.ForEach(x => x.overrideEdgeUseOpacityCurve = Target.overrideEdgeUseOpacityCurve);

            if (cacheOverrideEdgeOpacityCurve != Target.overrideEdgeOpacityCurve)
                _targets.ForEach(x => x.overrideEdgeOpacityCurve = Target.overrideEdgeOpacityCurve);

            if (cacheEdgeSprite != OverlaySettings.edgeSprite)
                _targets.ForEach(x => x.overlaySettings.edgeSprite = OverlaySettings.edgeSprite);
                
            if (cacheEdgeSpriteColor != OverlaySettings.edgeSpriteColor)
                _targets.ForEach(x => x.overlaySettings.edgeSpriteColor = OverlaySettings.edgeSpriteColor);
                   
            if (cacheEdgeSpriteSize != OverlaySettings.edgeSpriteSize)
                _targets.ForEach(x => x.overlaySettings.edgeSpriteSize = OverlaySettings.edgeSpriteSize);

            if (cacheEdgeSpriteSizeUseCurve != OverlaySettings.edgeSpriteSizeUseCurve)
                _targets.ForEach(x => x.overlaySettings.edgeSpriteSizeUseCurve = OverlaySettings.edgeSpriteSizeUseCurve);
                
            if (cacheEdgeSpriteSizeCurve != OverlaySettings.edgeSpriteSizeCurve)
                _targets.ForEach(x => x.overlaySettings.edgeSpriteSizeCurve = new AnimationCurve(OverlaySettings.edgeSpriteSizeCurve.keys));
                
            if (cacheEdgeSpriteOpacity != OverlaySettings.edgeSpriteOpacity)
                _targets.ForEach(x => x.overlaySettings.edgeSpriteOpacity = OverlaySettings.edgeSpriteOpacity);
                
            if (cacheEdgeSpriteOpacityUseCurve != OverlaySettings.edgeSpriteOpacityUseCurve)
                _targets.ForEach(x => x.overlaySettings.edgeSpriteOpacityUseCurve = OverlaySettings.edgeSpriteOpacityUseCurve);
                
            if (cacheEdgeSpriteOpacityCurve != OverlaySettings.edgeSpriteOpacityCurve)
                _targets.ForEach(x => x.overlaySettings.edgeSpriteOpacityCurve = new AnimationCurve(OverlaySettings.edgeSpriteOpacityCurve.keys));
        }

        private void ShowNavigationBarOverrides()
        {
            StartRow();
            ShowBarOverrides = OnOffButton(ShowBarOverrides);
            Header2($"Navigation Bar Overrides");
            EndRow();
            
            if (!ShowBarOverrides)
                return;
            
            // Caching original target values
            var cacheOverrideFadeAtEdges = Target.overrideFadeAtEdges;
            var cacheOverrideClampPositionAtEdges = Target.overrideClampPositionAtEdges;
            var cacheOverrideNavigationBarYPosition = Target.overrideNavigationBarYPosition;
            var cacheOverrideNavigationBarSprite = Target.overrideNavigationBarSprite;
            var cacheOverrideNavigationBarSpriteColor = Target.overrideNavigationBarSpriteColor;
            var cacheOverrideNavigationBarSpriteSize = Target.overrideNavigationBarSpriteSize;
            var cacheOverrideNavigationBarUseSizeCurve = Target.overrideNavigationBarUseSizeCurve;
            var cacheOverrideNavigationBarSizeCurve = Target.overrideNavigationBarSizeCurve;
            var cacheOverrideNavigationBarSpriteOpacity = Target.overrideNavigationBarSpriteOpacity;
            var cacheOverrideNavigationBarUseOpacityCurve = Target.overrideNavigationBarUseOpacityCurve;
            var cacheOverrideNavigationBarOpacityCurve = Target.overrideNavigationBarOpacityCurve;

            // Caching original OverlaySettings values
            var cacheFadeAtEdges = OverlaySettings.fadeAtEdges;
            var cacheClampPositionAtEdges = OverlaySettings.clampPositionAtEdges;
            var cacheYPosition = OverlaySettings.yPosition;
            var cacheNavigationBarSprite = OverlaySettings.navigationBarSprite;
            var cacheNavigationBarSpriteColor = OverlaySettings.navigationBarSpriteColor;
            var cacheNavigationBarSize = OverlaySettings.navigationBarSize;
            var cacheNavigationBarSizeUseCurve = OverlaySettings.navigationBarSizeUseCurve;
            var cacheNavigationBarSizeCurve = OverlaySettings.navigationBarSizeCurve;
            var cacheNavigationBarSpriteOpacity = OverlaySettings.navigationBarSpriteOpacity;
            var cacheNavigationBarSpriteOpacityUseCurve = OverlaySettings.navigationBarSpriteOpacityUseCurve;
            var cacheNavigationBarSpriteOpacityCurve = OverlaySettings.navigationBarSpriteOpacityCurve;
            
            StartRow();
            Target.overrideFadeAtEdges = Check(Target.overrideFadeAtEdges, 25);
            Label($"Fade At Edges", 150);
            if (Target.overrideFadeAtEdges)
                OverlaySettings.fadeAtEdges = LeftCheck($"Fade At Edges {(OverlaySettings.fadeAtEdges ? "Enabled" : "Disabled")}", OverlaySettings.fadeAtEdges, 200);
            else
                Label($"{textMuted}Default Fade At Edges values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideClampPositionAtEdges = Check(Target.overrideClampPositionAtEdges, 25);
            Label($"Clamp Position At Edges", 150);
            if (Target.overrideClampPositionAtEdges)
                OverlaySettings.clampPositionAtEdges = LeftCheck($"Clamp Position At Edges {(OverlaySettings.clampPositionAtEdges ? "Enabled" : "Disabled")}", OverlaySettings.clampPositionAtEdges, 200);
            else
                Label($"{textMuted}Default Clamp Position At Edges values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideNavigationBarYPosition = Check(Target.overrideNavigationBarYPosition, 25);
            Label($"Y Position", 150);
            if (Target.overrideNavigationBarYPosition)
                OverlaySettings.yPosition = FloatSlider(OverlaySettings.yPosition, -180, 180, 200);
            else
                Label($"{textMuted}Default Y Position values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideNavigationBarMoveWithRotation = Check(Target.overrideNavigationBarMoveWithRotation, 25);
            Label($"Move with Rotation", 150);
            if (Target.overrideNavigationBarMoveWithRotation)
                OverlaySettings.moveWithRotation = LeftCheck($"Move with Rotation {(OverlaySettings.moveWithRotation ? "Enabled" : "Disabled")}", OverlaySettings.moveWithRotation, 200);
            else
                Label($"{textMuted}Default Move with Rotation values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideNavigationBarXPosition = Check(Target.overrideNavigationBarXPosition, 25);
            Label("X Position", 150);
            if (Target.overrideNavigationBarXPosition)
                OverlaySettings.xPosition = Float(OverlaySettings.xPosition, 50);
            else
                Label($"{textMuted}Default X Position value{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideNavigationBarSprite = Check(Target.overrideNavigationBarSprite, 25);
            Label($"Icon", 150);
            if (Target.overrideNavigationBarSprite)
            {
                OverlaySettings.navigationBarSprite = Object(OverlaySettings.navigationBarSprite, typeof(Sprite), 150, false) as Sprite;
                if (OverlaySettings.navigationBarSprite == null)
                    Label($"{textError}<b>Sprite must be populated!</b>{textColorEnd}", false, false, true);
            }
            else
                Label($"{textMuted}Default Sprite{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideNavigationBarSpriteColor = Check(Target.overrideNavigationBarSpriteColor, 25);
            Label($"Color", 150);
            if (Target.overrideNavigationBarSpriteColor)
                OverlaySettings.navigationBarSpriteColor = ColorField(OverlaySettings.navigationBarSpriteColor, 150);
            else
                Label($"{textMuted}Default Edge Color{textColorEnd}", false, false, true);
            EndRow();
            
            BlackLine();
            
            StartRow();
            Target.overrideNavigationBarSpriteSize = Check(Target.overrideNavigationBarSpriteSize, 25);
            Label($"Size", 150);
            if (Target.overrideNavigationBarSpriteSize)
                OverlaySettings.navigationBarSize =Mathf.Clamp(DelayedInt(OverlaySettings.navigationBarSize, 150), 0, 99999999);
            else
                Label($"{textMuted}Default Size values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideNavigationBarUseSizeCurve = Check(Target.overrideNavigationBarUseSizeCurve, 25);
            Label($"Use Size Curve", 150);
            if (Target.overrideNavigationBarUseSizeCurve)
                OverlaySettings.navigationBarSizeUseCurve = LeftCheck($"Size Curve {(OverlaySettings.navigationBarSizeUseCurve ? "Enabled" : "Disabled")}", OverlaySettings.navigationBarSizeUseCurve, 200);
            else
                Label($"{textMuted}Default use Curve values{textColorEnd}", false, false, true);
            EndRow();

            if (Target.overrideNavigationBarUseSizeCurve && !OverlaySettings.navigationBarSizeUseCurve)
                Label($"{textMuted}<i>Size Curve Disabled</i>{textColorEnd}", false, true, true);
            else
            {
                StartRow();
                Target.overrideNavigationBarSizeCurve = Check(Target.overrideNavigationBarSizeCurve, 25);
                Label($"Size Curve", 150);
                if (Target.overrideNavigationBarSizeCurve)
                    OverlaySettings.navigationBarSizeCurve = Curve(OverlaySettings.navigationBarSizeCurve, -1, 200, 20);
                else
                    Label($"{textMuted}Default Size Curve values{textColorEnd}", false, false, true);
                EndRow();
            }
            
            BlackLine();
            
            StartRow();
            Target.overrideNavigationBarSpriteOpacity = Check(Target.overrideNavigationBarSpriteOpacity, 25);
            Label($"Opacity", 150);
            if (Target.overrideNavigationBarSpriteOpacity)
                OverlaySettings.navigationBarSpriteOpacity = Mathf.Clamp01(DelayedFloat(OverlaySettings.navigationBarSpriteOpacity, 150));
            else
                Label($"{textMuted}Default Opacity values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideNavigationBarUseOpacityCurve = Check(Target.overrideNavigationBarUseOpacityCurve, 25);
            Label($"Use Opacity Curve", 150);
            if (Target.overrideNavigationBarUseOpacityCurve)
                OverlaySettings.navigationBarSpriteOpacityUseCurve = LeftCheck($"Opacity Curve {(OverlaySettings.navigationBarSpriteOpacityUseCurve ? "Enabled" : "Disabled")}", OverlaySettings.navigationBarSpriteOpacityUseCurve, 200);
            else
                Label($"{textMuted}Default use Curve values{textColorEnd}", false, false, true);
            EndRow();

            if (Target.overrideNavigationBarUseOpacityCurve && !OverlaySettings.navigationBarSpriteOpacityUseCurve)
                Label($"{textMuted}<i>Opacity Curve Disabled</i>{textColorEnd}", false, true, true);
            else
            {
                StartRow();
                Target.overrideNavigationBarOpacityCurve = Check(Target.overrideNavigationBarOpacityCurve, 25);
                Label($"Opacity Curve", 150);
                if (Target.overrideNavigationBarOpacityCurve)
                    OverlaySettings.navigationBarSpriteOpacityCurve = Curve(OverlaySettings.navigationBarSpriteOpacityCurve, -1, 200, 20);
                else
                    Label($"{textMuted}Default Opacity Curve values{textColorEnd}", false, false, true);
                EndRow();
            }

            if (!KeyShift) return;
            
            if (cacheOverrideFadeAtEdges != Target.overrideFadeAtEdges)
                _targets.ForEach(x => x.overrideFadeAtEdges = Target.overrideFadeAtEdges);

            if (cacheOverrideClampPositionAtEdges != Target.overrideClampPositionAtEdges)
                _targets.ForEach(x => x.overrideClampPositionAtEdges = Target.overrideClampPositionAtEdges);

            if (cacheOverrideNavigationBarYPosition != Target.overrideNavigationBarYPosition)
                _targets.ForEach(x => x.overrideNavigationBarYPosition = Target.overrideNavigationBarYPosition);

            if (cacheOverrideNavigationBarSprite != Target.overrideNavigationBarSprite)
                _targets.ForEach(x => x.overrideNavigationBarSprite = Target.overrideNavigationBarSprite);

            if (cacheOverrideNavigationBarSpriteColor != Target.overrideNavigationBarSpriteColor)
                _targets.ForEach(x => x.overrideNavigationBarSpriteColor = Target.overrideNavigationBarSpriteColor);

            if (cacheOverrideNavigationBarSpriteSize != Target.overrideNavigationBarSpriteSize)
                _targets.ForEach(x => x.overrideNavigationBarSpriteSize = Target.overrideNavigationBarSpriteSize);

            if (cacheOverrideNavigationBarUseSizeCurve != Target.overrideNavigationBarUseSizeCurve)
                _targets.ForEach(x => x.overrideNavigationBarUseSizeCurve = Target.overrideNavigationBarUseSizeCurve);

            if (cacheOverrideNavigationBarSizeCurve != Target.overrideNavigationBarSizeCurve)
                _targets.ForEach(x => x.overrideNavigationBarSizeCurve = Target.overrideNavigationBarSizeCurve);

            if (cacheOverrideNavigationBarSpriteOpacity != Target.overrideNavigationBarSpriteOpacity)
                _targets.ForEach(x => x.overrideNavigationBarSpriteOpacity = Target.overrideNavigationBarSpriteOpacity);

            if (cacheOverrideNavigationBarUseOpacityCurve != Target.overrideNavigationBarUseOpacityCurve)
                _targets.ForEach(x => x.overrideNavigationBarUseOpacityCurve = Target.overrideNavigationBarUseOpacityCurve);

            if (cacheOverrideNavigationBarOpacityCurve != Target.overrideNavigationBarOpacityCurve)
                _targets.ForEach(x => x.overrideNavigationBarOpacityCurve = Target.overrideNavigationBarOpacityCurve);

            if (cacheFadeAtEdges != OverlaySettings.fadeAtEdges)
                _targets.ForEach(x => x.overlaySettings.fadeAtEdges = OverlaySettings.fadeAtEdges);
                
            if (cacheClampPositionAtEdges != OverlaySettings.clampPositionAtEdges)
                _targets.ForEach(x => x.overlaySettings.clampPositionAtEdges = OverlaySettings.clampPositionAtEdges);
                   
            if (cacheYPosition != OverlaySettings.yPosition)
                _targets.ForEach(x => x.overlaySettings.yPosition = OverlaySettings.yPosition);

            if (cacheNavigationBarSprite != OverlaySettings.navigationBarSprite)
                _targets.ForEach(x => x.overlaySettings.navigationBarSprite = OverlaySettings.navigationBarSprite);
                
            if (cacheNavigationBarSpriteColor != OverlaySettings.navigationBarSpriteColor)
                _targets.ForEach(x => x.overlaySettings.navigationBarSpriteColor = OverlaySettings.navigationBarSpriteColor);
                
            if (cacheNavigationBarSize != OverlaySettings.navigationBarSize)
                _targets.ForEach(x => x.overlaySettings.navigationBarSize = OverlaySettings.navigationBarSize);
                
            if (cacheNavigationBarSizeUseCurve != OverlaySettings.navigationBarSizeUseCurve)
                _targets.ForEach(x => x.overlaySettings.navigationBarSizeUseCurve = OverlaySettings.navigationBarSizeUseCurve);
                
            if (cacheNavigationBarSizeCurve != OverlaySettings.navigationBarSizeCurve)
                _targets.ForEach(x => x.overlaySettings.navigationBarSizeCurve = new AnimationCurve(OverlaySettings.navigationBarSizeCurve.keys));
                
            if (cacheNavigationBarSpriteOpacity != OverlaySettings.navigationBarSpriteOpacity)
                _targets.ForEach(x => x.overlaySettings.navigationBarSpriteOpacity = OverlaySettings.navigationBarSpriteOpacity);
                
            if (cacheNavigationBarSpriteOpacityUseCurve != OverlaySettings.navigationBarSpriteOpacityUseCurve)
                _targets.ForEach(x => x.overlaySettings.navigationBarSpriteOpacityUseCurve = OverlaySettings.navigationBarSpriteOpacityUseCurve);
                
            if (cacheNavigationBarSpriteOpacityCurve != OverlaySettings.navigationBarSpriteOpacityCurve)
                _targets.ForEach(x => x.overlaySettings.navigationBarSpriteOpacityCurve = new AnimationCurve(OverlaySettings.navigationBarSpriteOpacityCurve.keys));
        }
        

        private void ShowArrowOverrides()
        {
            StartRow();
            ShowArrowOverlayOverrides = OnOffButton(ShowArrowOverlayOverrides);
            Header2($"Arrow Overrides");
            EndRow();
            
            if (!ShowArrowOverlayOverrides)
                return;
            
            // Caching original target values
            var cacheOverrideArrowSprite = Target.overrideArrowSprite;
            var cacheOverrideArrowRotation = Target.overrideArrowRotation;
            var cacheOverrideArrowColor = Target.overrideArrowColor;
            var cacheOverrideArrowColorInitial = Target.overrideArrowColorInitial;
            var cacheOverrideArrowSize = Target.overrideArrowSize;
            var cacheOverrideArrowSizeInitial = Target.overrideArrowSizeInitial;
            var cacheOverrideArrowUseSizeCurve = Target.overrideArrowUseSizeCurve;
            var cacheOverrideArrowSizeCurve = Target.overrideArrowSizeCurve;
            var cacheOverrideArrowOpacity = Target.overrideArrowOpacity;
            var cacheOverrideArrowOpacityInitial = Target.overrideArrowOpacityInitial;
            var cacheOverrideArrowUseOpacityCurve = Target.overrideArrowUseOpacityCurve;
            var cacheOverrideArrowOpacityCurve = Target.overrideArrowOpacityCurve;

            // Caching original OverlaySettings values
            var cacheArrowSprite = OverlaySettings.arrowSprite;
            var cacheRotateArrow = OverlaySettings.rotateArrow;
            var cacheArrowColor = OverlaySettings.arrowColor;
            var cacheArrowColorInitial = OverlaySettings.arrowColorInitial;
            var cacheArrowSize = OverlaySettings.arrowSize;
            var cacheArrowSizeInitial = OverlaySettings.arrowSizeInitial;
            var cacheArrowSizeUseCurve = OverlaySettings.arrowSizeUseCurve;
            var cacheArrowSizeCurve = OverlaySettings.arrowSizeCurve;
            var cacheArrowOpacity = OverlaySettings.arrowOpacity;
            var cacheArrowOpacityInitial = OverlaySettings.arrowOpacityInitial;
            var cacheArrowOpacityUseCurve = OverlaySettings.arrowOpacityUseCurve;
            var cacheArrowOpacityCurve = OverlaySettings.arrowOpacityCurve;
            
            StartRow();
            Target.overrideArrowSprite = Check(Target.overrideArrowSprite, 25);
            Label($"Icon", 150);
            if (Target.overrideArrowSprite)
            {
                OverlaySettings.arrowSprite = Object(OverlaySettings.arrowSprite, typeof(Sprite), 150, false) as Sprite;
                if (OverlaySettings.arrowSprite == null)
                    Label($"{textError}<b>Sprite must be populated!</b>{textColorEnd}", false, false, true);
            }
            else
                Label($"{textMuted}Default Sprite{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideArrowRotation = Check(Target.overrideArrowRotation, 25);
            Label($"Rotation", 150);
            if (Target.overrideArrowRotation)
                OverlaySettings.rotateArrow = LeftCheck($"Arrow Rotation {(OverlaySettings.rotateArrow ? "Enabled" : "Disabled")}", OverlaySettings.rotateArrow, 200);
            else
                Label($"{textMuted}Default Rotation values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideArrowColor = Check(Target.overrideArrowColor, 25);
            Label($"Color", 150);
            if (Target.overrideArrowColor)
                OverlaySettings.arrowColor = ColorField(OverlaySettings.arrowColor, 150);
            else
                Label($"{textMuted}Default Arrow Color{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideArrowColorInitial = Check(Target.overrideArrowColorInitial, 25);
            Label($"Initial Color", 150);
            if (Target.overrideArrowColorInitial)
                OverlaySettings.arrowColorInitial = ColorField(OverlaySettings.arrowColorInitial, 150);
            else
                Label($"{textMuted}Default Initial Arrow Color{textColorEnd}", false, false, true);
            EndRow();
            
            BlackLine();
            
            StartRow();
            Target.overrideArrowSize = Check(Target.overrideArrowSize, 25);
            Label($"Size", 150);
            if (Target.overrideArrowSize)
                OverlaySettings.arrowSize =Mathf.Clamp(DelayedInt(OverlaySettings.arrowSize, 150), 0, 99999999);
            else
                Label($"{textMuted}Default Size values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideArrowSizeInitial = Check(Target.overrideArrowSizeInitial, 25);
            Label($"Initial Size", 150);
            if (Target.overrideArrowSizeInitial)
                OverlaySettings.arrowSizeInitial =Mathf.Clamp(DelayedInt(OverlaySettings.arrowSizeInitial, 150), 0, 99999999);
            else
                Label($"{textMuted}Default Initial Size values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideArrowUseSizeCurve = Check(Target.overrideArrowUseSizeCurve, 25);
            Label($"Use Size Curve", 150);
            if (Target.overrideArrowUseSizeCurve)
                OverlaySettings.arrowSizeUseCurve = LeftCheck($"Size Curve {(OverlaySettings.arrowSizeUseCurve ? "Enabled" : "Disabled")}", OverlaySettings.arrowSizeUseCurve, 200);
            else
                Label($"{textMuted}Default use Curve values{textColorEnd}", false, false, true);
            EndRow();

            if (Target.overrideArrowUseSizeCurve && !OverlaySettings.arrowSizeUseCurve)
                Label($"{textMuted}<i>Size Curve Disabled</i>{textColorEnd}", false, true, true);
            else
            {
                StartRow();
                Target.overrideArrowSizeCurve = Check(Target.overrideArrowSizeCurve, 25);
                Label($"Size Curve", 150);
                if (Target.overrideArrowSizeCurve)
                    OverlaySettings.arrowSizeCurve = Curve(OverlaySettings.arrowSizeCurve, -1, 200, 20);
                else
                    Label($"{textMuted}Default Size Curve values{textColorEnd}", false, false, true);
                EndRow();
            }
            
            BlackLine();
            
            StartRow();
            Target.overrideArrowOpacity = Check(Target.overrideArrowOpacity, 25);
            Label($"Opacity", 150);
            if (Target.overrideArrowOpacity)
                OverlaySettings.arrowOpacity = Mathf.Clamp01(DelayedFloat(OverlaySettings.arrowOpacity, 150));
            else
                Label($"{textMuted}Default Opacity values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideArrowOpacityInitial = Check(Target.overrideArrowOpacityInitial, 25);
            Label($"Initial Opacity", 150);
            if (Target.overrideArrowOpacityInitial)
                OverlaySettings.arrowOpacityInitial = Mathf.Clamp01(DelayedFloat(OverlaySettings.arrowOpacityInitial, 150));
            else
                Label($"{textMuted}Default Initial Opacity values{textColorEnd}", false, false, true);
            EndRow();
            
            StartRow();
            Target.overrideArrowUseOpacityCurve = Check(Target.overrideArrowUseOpacityCurve, 25);
            Label($"Use Opacity Curve", 150);
            if (Target.overrideArrowUseOpacityCurve)
                OverlaySettings.arrowOpacityUseCurve = LeftCheck($"Opacity Curve {(OverlaySettings.arrowOpacityUseCurve ? "Enabled" : "Disabled")}", OverlaySettings.arrowOpacityUseCurve, 200);
            else
                Label($"{textMuted}Default use Curve values{textColorEnd}", false, false, true);
            EndRow();

            if (Target.overrideArrowUseOpacityCurve && !OverlaySettings.arrowOpacityUseCurve)
                Label($"{textMuted}<i>Opacity Curve Disabled</i>{textColorEnd}", false, true, true);
            else
            {
                StartRow();
                Target.overrideArrowOpacityCurve = Check(Target.overrideArrowOpacityCurve, 25);
                Label($"Opacity Curve", 150);
                if (Target.overrideArrowOpacityCurve)
                    OverlaySettings.arrowOpacityCurve = Curve(OverlaySettings.arrowOpacityCurve, -1, 200, 20);
                else
                    Label($"{textMuted}Default Opacity Curve values{textColorEnd}", false, false, true);
                EndRow();
            }

            if (!KeyShift) return;
            
            if (cacheOverrideArrowSprite != Target.overrideArrowSprite)
                _targets.ForEach(x => x.overrideArrowSprite = Target.overrideArrowSprite);

            if (cacheOverrideArrowRotation != Target.overrideArrowRotation)
                _targets.ForEach(x => x.overrideArrowRotation = Target.overrideArrowRotation);

            if (cacheOverrideArrowColor != Target.overrideArrowColor)
                _targets.ForEach(x => x.overrideArrowColor = Target.overrideArrowColor);

            if (cacheOverrideArrowColorInitial != Target.overrideArrowColorInitial)
                _targets.ForEach(x => x.overrideArrowColorInitial = Target.overrideArrowColorInitial);

            if (cacheOverrideArrowSize != Target.overrideArrowSize)
                _targets.ForEach(x => x.overrideArrowSize = Target.overrideArrowSize);

            if (cacheOverrideArrowSizeInitial != Target.overrideArrowSizeInitial)
                _targets.ForEach(x => x.overrideArrowSizeInitial = Target.overrideArrowSizeInitial);

            if (cacheOverrideArrowUseSizeCurve != Target.overrideArrowUseSizeCurve)
                _targets.ForEach(x => x.overrideArrowUseSizeCurve = Target.overrideArrowUseSizeCurve);

            if (cacheOverrideArrowSizeCurve != Target.overrideArrowSizeCurve)
                _targets.ForEach(x => x.overrideArrowSizeCurve = Target.overrideArrowSizeCurve);

            if (cacheOverrideArrowOpacity != Target.overrideArrowOpacity)
                _targets.ForEach(x => x.overrideArrowOpacity = Target.overrideArrowOpacity);

            if (cacheOverrideArrowOpacityInitial != Target.overrideArrowOpacityInitial)
                _targets.ForEach(x => x.overrideArrowOpacityInitial = Target.overrideArrowOpacityInitial);

            if (cacheOverrideArrowUseOpacityCurve != Target.overrideArrowUseOpacityCurve)
                _targets.ForEach(x => x.overrideArrowUseOpacityCurve = Target.overrideArrowUseOpacityCurve);

            if (cacheOverrideArrowOpacityCurve != Target.overrideArrowOpacityCurve)
                _targets.ForEach(x => x.overrideArrowOpacityCurve = Target.overrideArrowOpacityCurve);

            if (cacheArrowSprite != OverlaySettings.arrowSprite)
                _targets.ForEach(x => x.overlaySettings.arrowSprite = OverlaySettings.arrowSprite);

            if (cacheRotateArrow != OverlaySettings.rotateArrow)
                _targets.ForEach(x => x.overlaySettings.rotateArrow = OverlaySettings.rotateArrow);

            if (cacheArrowColor != OverlaySettings.arrowColor)
                _targets.ForEach(x => x.overlaySettings.arrowColor = OverlaySettings.arrowColor);

            if (cacheArrowColorInitial != OverlaySettings.arrowColorInitial)
                _targets.ForEach(x => x.overlaySettings.arrowColorInitial = OverlaySettings.arrowColorInitial);
                
            if (cacheArrowSize != OverlaySettings.arrowSize)
                _targets.ForEach(x => x.overlaySettings.arrowSize = OverlaySettings.arrowSize);
                
            if (cacheArrowSizeInitial != OverlaySettings.arrowSizeInitial)
                _targets.ForEach(x => x.overlaySettings.arrowSizeInitial = OverlaySettings.arrowSizeInitial);
                
            if (cacheArrowSizeUseCurve != OverlaySettings.arrowSizeUseCurve)
                _targets.ForEach(x => x.overlaySettings.arrowSizeUseCurve = OverlaySettings.arrowSizeUseCurve);
                
            if (cacheArrowSizeCurve != OverlaySettings.arrowSizeCurve)
                _targets.ForEach(x => x.overlaySettings.arrowSizeCurve = new AnimationCurve(OverlaySettings.arrowSizeCurve.keys));
                
            if (cacheArrowOpacity != OverlaySettings.arrowOpacity)
                _targets.ForEach(x => x.overlaySettings.arrowOpacity = OverlaySettings.arrowOpacity);
                
            if (cacheArrowOpacityInitial != OverlaySettings.arrowOpacityInitial)
                _targets.ForEach(x => x.overlaySettings.arrowOpacityInitial = OverlaySettings.arrowOpacityInitial);
                
            if (cacheArrowOpacityUseCurve != OverlaySettings.arrowOpacityUseCurve)
                _targets.ForEach(x => x.overlaySettings.arrowOpacityUseCurve = OverlaySettings.arrowOpacityUseCurve);
                
            if (cacheArrowOpacityCurve != OverlaySettings.arrowOpacityCurve)
                _targets.ForEach(x => x.overlaySettings.arrowOpacityCurve = new AnimationCurve(OverlaySettings.arrowOpacityCurve.keys));
        }
        
        private void ShowTargetOptions()
        {
            Header3("Options");
            
            // Caching original target values
            var cacheTargetType = Target.targetType;
            var cacheLayerOrder = Target.layerOrder;
            var cacheIconOffset = Target.iconOffset;
            var cacheOverlaySettings = Target.overlaySettings;
            var cacheFixedNavigationBarAngle = Target.fixedNavigationBarAngle;
            var cacheFixedAngle = Target.fixedAngle;
            
            StartRow();
            var cachedType = Target.targetType;
            Label($"Target Type {symbolInfo}", "Setting a target type allows you to modify all tracked icons of the same " +
                                        "type more easily. Check the docs for details.", 200);
            Target.targetType = TextField(Target.targetType, 200);
            if (Target.targetType != cachedType)
                _cachedTargets = false;
            EndRow();
            
            StartRow();
            Label($"Layer Order {symbolInfo}", "Sets the order in the UI layer. Higher = in front, can be a negative value.", 200);
            Target.layerOrder = DelayedInt(Target.layerOrder, 50);
            EndRow();
            
            StartRow();
            Label($"Icon Position Offset {symbolInfo}", "Offset the icon position from the object position.", 200);
            Target.iconOffset = Vector3Field(Target.iconOffset, 200);
            EndRow();
            
            StartRow();
            Label($"Northstar Overlay Settings {symbolInfo}", "The values from this Overlay Settings object will be used, if you choose to override them.", 200);
            Target.overlaySettings = Object(Target.overlaySettings, typeof(NorthstarOverlaySettings), 200, false) as NorthstarOverlaySettings;
            EndRow();

            Space();
            Label($"Custom Icon Prefabs {symbolInfo}", "Add another Icon Prefab here to use it instead of the default one. Leave empty to use the default. The prefab must have a ScreenOverlayIcon component, or " +
                                                       "one which inherits from ScreenOverlayIcon.", true);
            StartRow();
            Label("Custom Screen Icon", 150);
            Target.customScreenIcon = Object(Target.customScreenIcon, typeof(GameObject), 150, false) as GameObject;
            if (Target.customScreenIcon == null)
                Label($"{textMuted}<i>Optional</i>{textColorEnd}", 200, false, false, true);
            EndRow();
            StartRow();
            Label("Custom Navigation Bar Icon", 150);
            Target.customNavigationBarIcon = Object(Target.customNavigationBarIcon, typeof(GameObject), 150, false) as GameObject;
            if (Target.customNavigationBarIcon == null)
                Label($"{textMuted}<i>Optional</i>{textColorEnd}", 200, false, false, true);
            EndRow();
            
            Space();
            Label("Navigation Bar Options", true);
            StartRow();
            Target.fixedNavigationBarAngle = LeftCheck($"Fixed Angle {symbolInfo}",
                "When true, you can set a fixed angle for this object. Useful for cardinal directions and other things.",
                Target.fixedNavigationBarAngle, 150);
            if (Target.fixedNavigationBarAngle)
            {
                Label("Value", 50);
                Target.fixedAngle = FloatSlider(Target.fixedAngle, -180, 180, 200);
            }
            EndRow();
            
            bool cacheIgnoreRadar = Target.ignoreRadar;
            Space();
            Label("Radar Options", true);
            if (Target.gameObject.GetComponent<TrackedTargetCompassRadar>() == null)
                Label($"{textMuted}<i>Requires Tracked Target Compass Radar component</i>{textColorEnd}", false, true, true);
            StartRow();
            Target.ignoreRadar = LeftCheck($"Ignore Radar {symbolInfo}",
                "When true, these icons will not fade with pings, and movement will show, regardless of radar settings.",
                Target.ignoreRadar, 150);
            EndRow();

            if (KeyShift)
            {
                if (cacheIgnoreRadar != Target.ignoreRadar)
                    _targets.ForEach(x => x.ignoreRadar = Target.ignoreRadar);
                
                if (cacheTargetType != Target.targetType)
                    _targets.ForEach(x => x.targetType = Target.targetType);
                
                if (cacheLayerOrder != Target.layerOrder)
                    _targets.ForEach(x => x.layerOrder = Target.layerOrder);

                if (cacheIconOffset != Target.iconOffset)
                    _targets.ForEach(x => x.iconOffset = Target.iconOffset);

                if (cacheOverlaySettings != Target.overlaySettings)
                    _targets.ForEach(x => x.overlaySettings = Target.overlaySettings);

                if (cacheFixedNavigationBarAngle != Target.fixedNavigationBarAngle)
                    _targets.ForEach(x => x.fixedNavigationBarAngle = Target.fixedNavigationBarAngle);

                if (cacheFixedAngle != Target.fixedAngle)
                    _targets.ForEach(x => x.fixedAngle = Target.fixedAngle);
            }

            ValidateOptions();
        }
        
        private void ValidateOptions()
        {
            if (string.IsNullOrWhiteSpace(Target.targetType))
            {
                Debug.Log($"Type can not be empty.");
                Target.targetType = "Default";
            }

            Target.targetType = Target.targetType.SafeString();
        }
    }
}
