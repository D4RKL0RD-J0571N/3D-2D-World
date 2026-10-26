using System.Collections.Generic;
using UnityEngine;


#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

[System.Serializable]
public class gamepadIconProfile
{
    [Space]
    [Header ("Main Settings")]
    [Space]

    public string profileName;

    public GamepadType gamepadType;

    [Space]
    [Header ("Detection")]
    [Space]

    public List<string> gamepadIdentifiers = new List<string> ();

    [Space]
    [Header ("Icons")]
    [Space]

    public List<GamepadButtonIcon> buttons =
        new List<GamepadButtonIcon> ();

    Dictionary<GamepadButton, Sprite> buttonLookup;

    public void BuildLookup ()
    {
        buttonLookup = new Dictionary<GamepadButton, Sprite> ();

        foreach (var buttonInfo in buttons) {
            if (buttonInfo == null)
                continue;

            buttonLookup [buttonInfo.button] = buttonInfo.icon;
        }
    }

    public Sprite GetIcon (GamepadButton button)
    {
        if (buttonLookup == null)
            BuildLookup ();

        if (buttonLookup.TryGetValue (button, out Sprite icon))
            return icon;

        return null;
    }

    public bool MatchesGamepad (Gamepad gamepad)
    {
        if (gamepad == null)
            return false;

        string searchString =
            (
                gamepad.description.product + " " +
                gamepad.description.manufacturer + " " +
                gamepad.name
            ).ToLowerInvariant ();

        foreach (string keyword in gamepadIdentifiers) {
            if (string.IsNullOrWhiteSpace (keyword))
                continue;

            if (searchString.Contains (keyword.ToLowerInvariant ()))
                return true;
        }

        return false;
    }
}

[System.Serializable]
public class GamepadButtonIcon
{
#if ENABLE_INPUT_SYSTEM
    public GamepadButton button;
#endif
    public Sprite icon;
}

public enum GamepadType
{
    Xbox,
    PlayStation,
    NintendoSwitch,
    SteamDeck,
    Generic
}