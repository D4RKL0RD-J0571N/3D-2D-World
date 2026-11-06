using UnityEditor;
using UnityEngine;
using MagicPigGames.Northstar;
using static MagicPigGames.Shared.MagicPigEditor;

namespace MagicPigGames.Northstar
{

[CustomEditor(typeof(NorthstarOverlaySettings))]
public class NorthstarOverlaySettingsEditor : Editor
{
    NorthstarOverlaySettings Target => (NorthstarOverlaySettings)target;

    private bool ShowScreenOverlayOptions
    {
        get => EditorPrefs.GetBool("Northstar Overlay Settings Show Screen Overlay Options", false);
        set => EditorPrefs.SetBool("Northstar Overlay Settings Show Screen Overlay Options", value);
    }
    
    private bool ShowEdgeOverlayOptions
    {
        get => EditorPrefs.GetBool("Northstar Overlay Settings Show Edge Overlay Options", false);
        set => EditorPrefs.SetBool("Northstar Overlay Settings Show Edge Overlay Options", value);
    }
    
    private bool ShowEdgeArrowOptions
    {
        get => EditorPrefs.GetBool("Northstar Overlay Settings Show Edge Arrow Options", false);
        set => EditorPrefs.SetBool("Northstar Overlay Settings Show Edge Arrow Options", value);
    }
    
    private bool ShowAdditionalOptions
    {
        get => EditorPrefs.GetBool("Northstar Overlay Settings Show Additional Options", false);
        set => EditorPrefs.SetBool("Northstar Overlay Settings Show Additional Options", value);
    }
    
    private bool ShowCompassBarOptions
    {
        get => EditorPrefs.GetBool("Northstar Overlay Settings Show Navigation Bar Options", false);
        set => EditorPrefs.SetBool("Northstar Overlay Settings Show Navigation Bar Options", value);
    }
    
    public override void OnInspectorGUI()
    {
        HeaderAndIntro();
        
        GreyLine();
        Undo.RecordObject(Target, "Screen Overlay Options");
        ScreenOverlaySettings();
        
        GreyLine();
        Undo.RecordObject(Target, "Edge Overlay Options");
        EdgeOverlaySettings();
        
        GreyLine();
        Undo.RecordObject(Target, "Edge Arrow Options");
        ArrowSettings();
        
        GreyLine();
        Undo.RecordObject(Target, "Navigation Bar Options");
        CompassBarSettings();
        
        GreyLine();
        Undo.RecordObject(Target, "Additional Options");
        AdditionalSettings();
            
        EditorUtility.SetDirty(Target);
    }

    private void HeaderAndIntro()
    {
        LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/northstar-tracking-system/northstar-documentation/northstar-overlay-settings");
            
        Header2($"Northstar Overlay Settings");
        Label($"{textNormal}Create as many {textHightlight}Northstar Overlay Settings{textColorEnd} objects as you'd like. Add " +
              $"them to a {textHightlight}Northstar Overlay{textColorEnd} object in your scene to set the default values. " +
              $"Individual tracked items can have their own settings, and can override only the default " +
              $"options you set.{textColorEnd}", false, true, true);
    }
    
    private void CompassBarSettings()
    {
        StartRow();
        ShowCompassBarOptions = OnOffButton(ShowCompassBarOptions);
        Header2($"Navigation Bar {symbolInfo}", "These options control how the sprites appear on the horizontal " +
                                             "Navigation Bar.", 250);
        EndRow();

        if (!ShowCompassBarOptions)
            return;
        
        StartRow();
        Target.fadeAtEdges = LeftCheck($"Fade At Edges {symbolInfo}", "When true, the sprite will fade out when it gets close " +
                                                                      "to the edges. Generally this is desired.", Target.fadeAtEdges);
        EndRow();
        
        StartRow();
        Target.clampPositionAtEdges = LeftCheck($"Clamp Position at Edges {symbolInfo}", "When true, sprites will remain at the edge when they " +
            "otherwise would have moved off of the horizontal Navigation Bar. If fadeAtEdges is true, they will still fade out based on the " +
            "fade out distance settings.", Target.clampPositionAtEdges);
        EndRow();
        
        StartRow();
        Label($"Y Position {symbolInfo}", "The local Y position on the horizontal Navigation Bar.", 150);
        Target.yPosition = Float(Target.yPosition, 50);
        EndRow();
        
        StartRow();
        Target.moveWithRotation = LeftCheck($"Move with Rotation {symbolInfo}", "USUALLY THIS SHOULD BE TRUE! When false, the sprite will not move as the player rotates.", Target.moveWithRotation);
        EndRow();
        
        StartRow();
        Label($"X Position {symbolInfo}", "No effect when Move With Rotation is false! The local X position on the horizontal Navigation Bar.", 150);
        Target.xPosition = Float(Target.xPosition, 50);
        if (!Target.moveWithRotation)
            Label($"<i>{textMuted} Value is ignored when Move With Rotation is false{textColorEnd}</i>", 300, false, false, true);
        EndRow();
        
        StartRow();
        Label($"Icon",  150);
        Target.navigationBarSprite = Object(Target.navigationBarSprite, typeof(Sprite), 150, false) as Sprite;
        if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
            Target.navigationBarSprite = Target.screenSprite;
        EndRow();
        
        StartRow();
        Label($"Color",  150);
        Target.navigationBarSpriteColor = ColorField(Target.navigationBarSpriteColor, 150);
        if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
            Target.navigationBarSpriteColor = Target.screenSpriteColor;
        EndRow();
        
        BlackLine();
        
        StartRow();
        Label($"Size {symbolInfo}", "This is the default size. If you'd like to adjust size based on the distance " +
                                    "to the object, toggle on \"Use Size Curve\".",  150);
        Target.navigationBarSize = Int(Target.navigationBarSize, 150);
        if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
            Target.navigationBarSize = Target.screenSpriteSize;
        EndRow();
        
        if (Target.navigationBarSize < 0)
        {
            Debug.LogWarning("Sprite size must be greater than 0. Setting to 10.");
            Target.navigationBarSize = 10;
        }
        
        StartRow();
        Target.navigationBarSizeUseCurve = LeftCheck($"Use Size Curve {symbolInfo}",
            "When true, the size of the icon will be adjusted based on the distance " +
            "to the object. Min and Max distance values can be set on the " +
            "Northstar Overlay in your scene, and overridden by individual " +
            "Tracked Targets.", Target.navigationBarSizeUseCurve, 150);
        if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
            Target.navigationBarSizeUseCurve = Target.screenSpriteSizeUseCurve;
        EndRow();

        if (Target.navigationBarSizeUseCurve)
        {
            StartRow();
            StartVertical(150);
            Label($"Size Curve {symbolInfo}",
                "The left represents the minimum distance, closest to the player. The right is the " +
                "farthest distance away. Y value 1 = full size, value 0 = 0 size, i.e. not visible. \n\n" +
                "Min and Max distance values can be set on the " +
                "Northstar Overlay in your scene, and overridden by individual " +
                "Tracked Targets.", 150);
            Label($"{textMuted}Closest distance on left{textColorEnd}", 150, false, false, true);
            EndVertical();
            Target.navigationBarSizeCurve = Curve(Target.navigationBarSizeCurve, -1, 200, 40);
            if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
                Target.navigationBarSizeCurve = Target.screenSpriteSizeCurve;
            EndRow();
        }
        
        BlackLine();
        
        StartRow();
        Label($"Opacity {symbolInfo}", "This is the opacity for the icon. If you'd like to adjust opacity based on the distance " +
                                    "to the object, toggle on \"Use Opacity Curve\".",  150);
        Target.navigationBarSpriteOpacity = DelayedFloat(Target.navigationBarSpriteOpacity, 150);
        if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
            Target.navigationBarSpriteOpacity = Target.screenSpriteOpacity;
        EndRow();
        
        if (Target.navigationBarSpriteOpacity < 0 || Target.navigationBarSpriteOpacity > 1)
        {
            Debug.LogWarning("Sprite opacity must be between 0 and 1. Setting to 1.");
            Target.navigationBarSpriteOpacity = 1;
        }
        
        StartRow();
        Target.navigationBarSpriteOpacityUseCurve = LeftCheck($"Use Opacity Curve {symbolInfo}",
            "When true, the opacity of the icon will be adjusted based on the distance " +
            "to the object. This is a great way to gently remove the icon as the player " +
            "gets close to the target.\n\nMin and Max distance values can be set on the " +
            "Northstar Overlay in your scene, and overridden by individual " +
            "Tracked Targets.", Target.navigationBarSpriteOpacityUseCurve, 150);
        if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
            Target.navigationBarSpriteOpacityUseCurve = Target.screenSpriteOpacityUseCurve;
        EndRow();

        if (Target.navigationBarSpriteOpacityUseCurve)
        {
            StartRow();
            StartVertical(150);
            Label($"Opacity Curve {symbolInfo}",
                "The left represents the minimum distance, closest to the player. The right is the " +
                "farthest distance away. Y value 1 = full opacity, value 0 = 0 size, i.e. not visible. \n\n" +
                "Min and Max distance values can be set on the " +
                "Northstar Overlay in your scene, and overridden by individual " +
                "Tracked Targets.", 150);
            Label($"{textMuted}Closest distance on left{textColorEnd}", 150, false, false, true);
            EndVertical();
            Target.navigationBarSpriteOpacityCurve = Curve(Target.navigationBarSpriteOpacityCurve, -1, 200, 40);
            if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
                Target.navigationBarSpriteOpacityCurve = Target.screenSpriteOpacityCurve;
            EndRow();
        }
    }

    private void ScreenOverlaySettings()
    {
        StartRow();
        ShowScreenOverlayOptions = OnOffButton(ShowScreenOverlayOptions);
        Header2($"Screen Overlay {symbolInfo}", "This is displayed on top of a target object. Often it is the " +
                                                "same icon used for the Edge Overlay.", 250);
        EndRow();

        if (!ShowScreenOverlayOptions)
            return;
        
        StartRow();
        Label($"Icon",  150);
        Target.screenSprite = Object(Target.screenSprite, typeof(Sprite), 150, false) as Sprite;
        EndRow();
        if (Target.edgeSprite == null)
            Target.edgeSprite = Target.screenSprite;
        if (Target.navigationBarSprite == null)
            Target.navigationBarSprite = Target.screenSprite;
        
        StartRow();
        Label($"Color",  150);
        Target.screenSpriteColor = ColorField(Target.screenSpriteColor, 150);
        EndRow();
        
        BlackLine();
        
        StartRow();
        Label($"Size {symbolInfo}", "This is the default size. If you'd like to adjust size based on the distance " +
                                    "to the object, toggle on \"Use Size Curve\".",  150);
        Target.screenSpriteSize = Int(Target.screenSpriteSize, 150);
        EndRow();
        
        if (Target.screenSpriteSize < 0)
        {
            Debug.LogWarning("Sprite size must be greater than 0. Setting to 10.");
            Target.screenSpriteSize = 10;
        }
        
        StartRow();
        Target.screenSpriteSizeUseCurve = LeftCheck($"Use Size Curve {symbolInfo}",
            "When true, the size of the icon will be adjusted based on the distance " +
            "to the object. Min and Max distance values can be set on the " +
            "Northstar Overlay in your scene, and overridden by individual " +
            "Tracked Targets.", Target.screenSpriteSizeUseCurve, 150);
        EndRow();

        if (Target.screenSpriteSizeUseCurve)
        {
            StartRow();
            StartVertical(150);
            Label($"Size Curve {symbolInfo}",
                "The left represents the minimum distance, closest to the player. The right is the " +
                "farthest distance away. Y value 1 = full size, value 0 = 0 size, i.e. not visible. \n\n" +
                "Min and Max distance values can be set on the " +
                "Northstar Overlay in your scene, and overridden by individual " +
                "Tracked Targets.", 150);
            Label($"{textMuted}Closest distance on left{textColorEnd}", 150, false, false, true);
            EndVertical();
            Target.screenSpriteSizeCurve = Curve(Target.screenSpriteSizeCurve, -1, 200, 40);
            EndRow();
        }
        
        BlackLine();
        
        StartRow();
        Label($"Opacity {symbolInfo}", "This is the opacity for the icon. If you'd like to adjust opacity based on the distance " +
                                    "to the object, toggle on \"Use Opacity Curve\".",  150);
        Target.screenSpriteOpacity = DelayedFloat(Target.screenSpriteOpacity, 150);
        EndRow();
        
        if (Target.screenSpriteOpacity < 0 || Target.screenSpriteOpacity > 1)
        {
            Debug.LogWarning("Sprite opacity must be between 0 and 1. Setting to 1.");
            Target.screenSpriteOpacity = 1;
        }
        
        Target.screenSpriteOpacityUseCurve = LeftCheck($"Use Opacity Curve {symbolInfo}",
            "When true, the opacity of the icon will be adjusted based on the distance " +
            "to the object. This is a great way to gently remove the icon as the player " +
            "gets close to the target.\n\nMin and Max distance values can be set on the " +
            "Northstar Overlay in your scene, and overridden by individual " +
            "Tracked Targets.", Target.screenSpriteOpacityUseCurve, 150);

        if (Target.screenSpriteOpacityUseCurve)
        {
            StartRow();
            StartVertical(150);
            Label($"Opacity Curve {symbolInfo}",
                "The left represents the minimum distance, closest to the player. The right is the " +
                "farthest distance away. Y value 1 = full opacity, value 0 = 0 size, i.e. not visible. \n\n" +
                "Min and Max distance values can be set on the " +
                "Northstar Overlay in your scene, and overridden by individual " +
                "Tracked Targets.", 150);
            Label($"{textMuted}Closest distance on left{textColorEnd}", 150, false, false, true);
            EndVertical();
            Target.screenSpriteOpacityCurve = Curve(Target.screenSpriteOpacityCurve, -1, 200, 40);
            EndRow();
        }
    }
    
    private void EdgeOverlaySettings()
    {
        StartRow();
        ShowEdgeOverlayOptions = OnOffButton(ShowEdgeOverlayOptions);
        Header2($"Edge Overlay {symbolInfo}", "This is displayed near the edge of the screen in the direction " +
                                              "of the target object. Often it is the " +
                                                "same icon used for the Screen Overlay.\n\nAt runtime, Screen settings " +
                                              "will smoothly transition to Edge settings when the target gets closer to " +
                                              "the edge.", 250);
        EndRow();

        if (!ShowEdgeOverlayOptions)
            return;
        
        StartRow();
        Label($"Icon",  150);
        Target.edgeSprite = Object(Target.edgeSprite, typeof(Sprite), 150, false) as Sprite;
        if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
            Target.edgeSprite = Target.screenSprite;
        EndRow();
        if (Target.screenSprite == null)
            Target.screenSprite = Target.edgeSprite;
        
        StartRow();
        Label($"Color",  150);
        Target.edgeSpriteColor = ColorField(Target.edgeSpriteColor, 150);
        if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
            Target.edgeSpriteColor = Target.screenSpriteColor;
        EndRow();
        
        BlackLine();
        
        StartRow();
        Label($"Size {symbolInfo}", "This is the default size. If you'd like to adjust size based on the distance " +
                                    "to the object, toggle on \"Use Size Curve\".",  150);
        Target.edgeSpriteSize = Int(Target.edgeSpriteSize, 150);
        if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
            Target.edgeSpriteSize = Target.screenSpriteSize;
        EndRow();
        
        if (Target.edgeSpriteSize < 0)
        {
            Debug.LogWarning("Sprite size must be greater than 0. Setting to 10.");
            Target.edgeSpriteSize = 10;
        }
        
        StartRow();
        Target.edgeSpriteSizeUseCurve = LeftCheck($"Use Size Curve {symbolInfo}",
            "When true, the size of the icon will be adjusted based on the distance " +
            "to the object. Min and Max distance values can be set on the " +
            "Northstar Overlay in your scene, and overridden by individual " +
            "Tracked Targets.", Target.edgeSpriteSizeUseCurve, 150);
        EndRow();

        if (Target.edgeSpriteSizeUseCurve)
        {
            StartRow();
            StartVertical(150);
            Label($"Size Curve {symbolInfo}",
                "The left represents the minimum distance, closest to the player. The right is the " +
                "farthest distance away. Y value 1 = full size, value 0 = 0 size, i.e. not visible. \n\n" +
                "Min and Max distance values can be set on the " +
                "Northstar Overlay in your scene, and overridden by individual " +
                "Tracked Targets.", 150);
            Label($"{textMuted}Closest distance on left{textColorEnd}", 150, false, false, true);
            EndVertical();
            Target.edgeSpriteSizeCurve = Curve(Target.edgeSpriteSizeCurve, -1, 200, 40);
            if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
                Target.edgeSpriteSizeCurve = Target.screenSpriteSizeCurve;
            EndRow();
        }
        
        BlackLine();
        
        StartRow();
        Label($"Opacity {symbolInfo}", "This is the opacity for the icon. If you'd like to adjust opacity based on the distance " +
                                    "to the object, toggle on \"Use Opacity Curve\".",  150);
        Target.edgeSpriteOpacity = DelayedFloat(Target.edgeSpriteOpacity, 150);
        if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
            Target.edgeSpriteOpacity = Target.screenSpriteOpacity;
        EndRow();
        
        if (Target.edgeSpriteOpacity < 0 || Target.edgeSpriteOpacity > 1)
        {
            Debug.LogWarning("Sprite opacity must be between 0 and 1. Setting to 1.");
            Target.edgeSpriteOpacity = 1;
        }
        
        Target.edgeSpriteOpacityUseCurve = LeftCheck($"Use Opacity Curve {symbolInfo}",
            "When true, the opacity of the icon will be adjusted based on the distance " +
            "to the object. This is a great way to gently remove the icon as the player " +
            "gets close to the target.\n\nMin and Max distance values can be set on the " +
            "Northstar Overlay in your scene, and overridden by individual " +
            "Tracked Targets.", Target.edgeSpriteOpacityUseCurve, 150);

        if (Target.edgeSpriteOpacityUseCurve)
        {
            StartRow();
            StartVertical(150);
            Label($"Opacity Curve {symbolInfo}",
                "The left represents the minimum distance, closest to the player. The right is the " +
                "farthest distance away. Y value 1 = full opacity, value 0 = 0 size, i.e. not visible. \n\n" +
                "Min and Max distance values can be set on the " +
                "Northstar Overlay in your scene, and overridden by individual " +
                "Tracked Targets.", 150);
            Label($"{textMuted}Closest distance on left{textColorEnd}", 150, false, false, true);
            EndVertical();
            Target.edgeSpriteOpacityCurve = Curve(Target.edgeSpriteOpacityCurve, -1, 200, 40);
            if (Button($"{symbolCarrotLeft} Screen Overlay", 125))
                Target.edgeSpriteOpacityCurve = Target.screenSpriteOpacityCurve;
            EndRow();
        }
    }
    
    private void ArrowSettings()
    {
        StartRow();
        ShowEdgeArrowOptions = OnOffButton(ShowEdgeArrowOptions);
        Header2($"Edge Arrow {symbolInfo}", "This is displayed near the edge, closer to the edge than the " +
                                            "Edge Overlay, and generally will rotate to point in the direction of the " +
                                            "target. While it is meant to be an arrow, it can optionally be any icon, and " +
                                            "can not rotate, if you choose.\n\nIndividual Tracked Target objects can " +
                                            "override these settings.", 250);
        EndRow();

        if (!ShowEdgeArrowOptions)
            return;

        StartRow();
        Label($"{textMuted}Leave the icon null to turn off the arrow. Individual {textHightlight}Tracked Targets{textColorEnd} can override " +
              $"this by supplying an {textHightlight}Arrow Icon (Sprite){textColorEnd} of their own, which will then be displayed using the " +
              $"default options set here, unless those are also overridden.{textColorEnd}", false, true, true);
        EndRow();
        
        StartRow();
        Label($"Icon",  150);
        Target.arrowSprite = Object(Target.arrowSprite, typeof(Sprite), 150, false) as Sprite;
        EndRow();
        
        if (Target.arrowSprite == null)
            Label($"{textWarning}Arrow will only show if Tracked Target supplies an arrow sprite.{textColorEnd}", false, true, true);
        
        StartRow();
        Target.rotateArrow = LeftCheck($"Rotate {symbolInfo}", "When true, the arrow sprite will rotate toward " +
                                                               "the direction of the target as it moves around the " +
                                                               "edge of the screen.", Target.rotateArrow, 150);
        EndRow();
        
        StartRow();
        Label($"Color",  150);
        Target.arrowColor = ColorField(Target.arrowColor, 150);
        if (Button($"{symbolCarrotLeft} Edge Overlay", 125))
            Target.arrowColor = Target.edgeSpriteColor;
        EndRow();
        
        StartRow();
        Label($"Color Initial {symbolInfo}", "As the target gets close to the edge, the initial values will " +
                                             "fade into the final values, enabling more pleasing appearances. This is " +
                                             "the initial color of the sprite.",  150);
        Target.arrowColorInitial = ColorField(Target.arrowColorInitial, 150);
        if (Button($"{symbolCarrotLeft} Edge Overlay", 125))
            Target.arrowColorInitial = Target.edgeSpriteColor;
        EndRow();
        
        BlackLine();
        
        StartRow();
        Label($"Size {symbolInfo}", "This is the default size. If you'd like to adjust size based on the distance " +
                                    "to the object, toggle on \"Use Size Curve\".",  150);
        Target.arrowSize = Int(Target.arrowSize, 150);
        if (Button($"{symbolCarrotLeft} Edge Overlay", 125))
            Target.arrowSize = Target.edgeSpriteSize;
        EndRow();
        
        StartRow();
        Label($"Size Initial {symbolInfo}", "This is the initial size. Similar to initial color, this is the starting " +
                                            "size of the arrow icon, blending to the full size as the target gets off screen.",  150);
        Target.arrowSizeInitial = Int(Target.arrowSizeInitial, 150);
        if (Button($"{symbolCarrotLeft} Edge Overlay", 125))
            Target.arrowSizeInitial = Target.edgeSpriteSize;
        EndRow();
        
        if (Target.arrowSize < 0)
        {
            Debug.LogWarning("Sprite size must be greater than 0. Setting to 10.");
            Target.arrowSize = 10;
        }
        
        if (Target.arrowSizeInitial < 0)
        {
            Debug.LogWarning("Sprite size must be >= than 0. Setting to 10.");
            Target.arrowSizeInitial = 0;
        }
        
        StartRow();
        Target.arrowSizeUseCurve = LeftCheck($"Use Size Curve {symbolInfo}",
            "When true, the size of the icon will be adjusted based on the distance " +
            "to the object. Min and Max distance values can be set on the " +
            "Northstar Overlay in your scene, and overridden by individual " +
            "Tracked Targets.", Target.arrowSizeUseCurve, 150);
        EndRow();

        if (Target.arrowSizeUseCurve)
        {
            StartRow();
            StartVertical(150);
            Label($"Size Curve {symbolInfo}",
                "The left represents the minimum distance, closest to the player. The right is the " +
                "farthest distance away. Y value 1 = full size, value 0 = 0 size, i.e. not visible. \n\n" +
                "Min and Max distance values can be set on the " +
                "Northstar Overlay in your scene, and overridden by individual " +
                "Tracked Targets.", 150);
            Label($"{textMuted}Closest distance on left{textColorEnd}", 150, false, false, true);
            EndVertical();
            Target.arrowSizeCurve = Curve(Target.arrowSizeCurve, -1, 200, 40);
            if (Button($"{symbolCarrotLeft} Edge Overlay", 125))
                Target.arrowSizeCurve = Target.edgeSpriteSizeCurve;
            EndRow();
        }
        
        BlackLine();
        
        StartRow();
        Label($"Opacity {symbolInfo}", "This is the opacity for the icon. If you'd like to adjust opacity based on the distance " +
                                       "to the object, toggle on \"Use Opacity Curve\".",  150);
        Target.arrowOpacity = DelayedFloat(Target.arrowOpacity, 150);
        if (Button($"{symbolCarrotLeft} Edge Overlay", 125))
            Target.arrowOpacity = Target.edgeSpriteOpacity;
        EndRow();
        
        StartRow();
        Label($"Opacity Initial {symbolInfo}", "This is the initial opacity for the icon as it appears when the target " +
                                               "gets close to the edge. Set this to 0 to enable a \"fade in\" effect.",  150);
        Target.arrowOpacityInitial = Float(Target.arrowOpacityInitial, 150);
        if (Button($"{symbolCarrotLeft} Edge Overlay", 125))
            Target.arrowOpacityInitial = Target.edgeSpriteOpacity;
        EndRow();
        
        if (Target.arrowOpacityInitial is < 0 or > 1)
        {
            Debug.LogWarning("Sprite opacity must be between 0 and 1. Setting to 0.");
            Target.arrowOpacityInitial = Mathf.Clamp(Target.arrowOpacityInitial, 0f, 1f);
        }
        if (Target.arrowOpacity is < 0 or > 1)
        {
            Debug.LogWarning("Sprite opacity must be between 0 and 1. Setting to 1.");
            Target.arrowOpacity = 1;
        }
        
        Target.arrowOpacityUseCurve = LeftCheck($"Use Opacity Curve {symbolInfo}",
            "When true, the opacity of the icon will be adjusted based on the distance " +
            "to the object. This is a great way to gently remove the icon as the player " +
            "gets close to the target.\n\nMin and Max distance values can be set on the " +
            "Northstar Overlay in your scene, and overridden by individual " +
            "Tracked Targets.", Target.arrowOpacityUseCurve, 150);

        if (Target.arrowOpacityUseCurve)
        {
            StartRow();
            StartVertical(150);
            Label($"Opacity Curve {symbolInfo}",
                "The left represents the minimum distance, closest to the player. The right is the " +
                "farthest distance away. Y value 1 = full opacity, value 0 = 0 size, i.e. not visible. \n\n" +
                "Min and Max distance values can be set on the " +
                "Northstar Overlay in your scene, and overridden by individual " +
                "Tracked Targets.", 150);
            Label($"{textMuted}Closest distance on left{textColorEnd}", 150, false, false, true);
            EndVertical();
            Target.arrowOpacityCurve = Curve(Target.arrowOpacityCurve, -1, 200, 40);
            if (Button($"{symbolCarrotLeft} Edge Overlay", 125))
                Target.arrowOpacityCurve = Target.edgeSpriteOpacityCurve;
            EndRow();
        }
    }
    
    private void AdditionalSettings()
    {
        StartRow();
        ShowAdditionalOptions = OnOffButton(ShowAdditionalOptions);
        Header2($"Options", 250);
        EndRow();

        if (!ShowAdditionalOptions)
            return;

        Header3("Screen Overlay");
        StartRow();
        Label($"Distance {symbolInfo}", "This determines the minimum and maximum distance for activating a screen overlay. Use the Size and Opacity curve settings to " +
                                        "smoothly fade in and out of these distance values. Outside this range, no overlay will show.",  150);
        Label("Min", 25);
        Target.screenDistanceMin = Float(Target.screenDistanceMin, 34);
        Gap();
        Label("Max", 25);
        Target.screenDistanceMax = Float(Target.screenDistanceMax, 34);
        EndRow();

        Space();
        Header3("Edge Overlay");
        StartRow();
        Label($"Distance {symbolInfo}", "This determines the minimum and maximum distance for activating a screen overlay. Use the Size and Opacity curve settings to " +
                                        "smoothly fade in and out of these distance values. Outside this range, no overlay will show.",  150);
        Label("Min", 25);
        Target.edgeDistanceMin = Float(Target.edgeDistanceMin, 34);
        Gap();
        Label("Max", 25);
        Target.edgeDistanceMax = Float(Target.edgeDistanceMax, 34);
        EndRow();
        StartRow();
        Label($"Edge Offset {symbolInfo}", "This is the edge offset for all edge icons; offset from the edge position of the screen.",  150);
        Target.offset = Int(Target.offset, 150);
        EndRow();
        
        if (Target.offset < 0)
        {
            Debug.LogWarning("Offset must be >= 0. Setting to 0.");
            Target.offset = 0;
        }
        
        StartRow();
        Label($"Offset from Arrow {symbolInfo}", "This is the gap between the arrow and the icon. If arrow is not being used, it is " +
                                    "ignored in the total offset calculation for the icon.",  150);
        Target.iconOffsetFromArrow = Int(Target.iconOffsetFromArrow, 150);
        EndRow();
        
        if (Target.iconOffsetFromArrow < 0)
        {
            Debug.LogWarning("Offset must be >= 0. Setting to 0.");
            Target.iconOffsetFromArrow = 0;
        }
    }
}
} // namespace MagicPigGames.Northstar
