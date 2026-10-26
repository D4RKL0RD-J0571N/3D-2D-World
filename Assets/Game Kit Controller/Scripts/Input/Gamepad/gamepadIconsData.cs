using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

[CreateAssetMenu (fileName = "Gamepad Icons List", menuName = "GKC/Create Gamepad Icons List", order = 51)]
public class gamepadIconsData : ScriptableObject
{
    public List<gamepadIconProfile> gamepads =
        new List<gamepadIconProfile> ();

    Dictionary<GamepadType, gamepadIconProfile> profileLookup;

    void BuildLookup ()
    {
        profileLookup = new Dictionary<GamepadType, gamepadIconProfile> ();

        foreach (var profile in gamepads) {
            if (profile == null)
                continue;

            profileLookup [profile.gamepadType] = profile;
        }
    }

#if ENABLE_INPUT_SYSTEM
    public Sprite GetIcon (GamepadType type, GamepadButton button)
    {
        if (profileLookup == null)
            BuildLookup ();

        if (profileLookup.TryGetValue (type, out var profile))
            return profile.GetIcon (button);

        return null;
    }

    public GamepadType GetGamepadType (Gamepad gamepad)
    {
        foreach (var profile in gamepads) {
            if (profile != null && profile.MatchesGamepad (gamepad))
                return profile.gamepadType;
        }

        return GamepadType.Generic;
    }
#endif
}