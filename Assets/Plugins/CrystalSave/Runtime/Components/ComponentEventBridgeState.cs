#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using EventValueType = Arawn.CrystalSave.Runtime.SaveableComponent.EventValueType;

namespace Arawn.CrystalSave.Runtime
{
    // Per-component runtime state only. Serialized defaults, UnityEvents and their
    // invocation/error lifetime remain on SaveableComponent.
    internal struct ComponentEventBridgeState
    {
        private EventValueType? detectedType;
        private string stringValue;
        internal bool Bool { get; private set; }
        internal int Int { get; private set; }
        internal float Float { get; private set; }
        internal string String => stringValue ?? string.Empty;
        internal Vector2 Vector2 { get; private set; }
        internal Vector3 Vector3 { get; private set; }
        internal Vector4 Vector4 { get; private set; }
        internal Color Color { get; private set; }
        internal Quaternion Quaternion { get; private set; }

        internal void Set(bool value) { Bool = value; detectedType = EventValueType.Bool; }
        internal void Set(int value) { Int = value; detectedType = EventValueType.Int; }
        internal void Set(float value) { Float = value; detectedType = EventValueType.Float; }
        internal void Set(string value) { stringValue = value ?? string.Empty; detectedType = EventValueType.String; }
        internal void Set(Vector2 value) { Vector2 = value; detectedType = EventValueType.Vector2; }
        internal void Set(Vector3 value) { Vector3 = value; detectedType = EventValueType.Vector3; }
        internal void Set(Vector4 value) { Vector4 = value; detectedType = EventValueType.Vector4; }
        internal void Set(Color value) { Color = value; detectedType = EventValueType.Color; }
        internal void Set(Quaternion value) { Quaternion = value; detectedType = EventValueType.Quaternion; }
        internal EventValueType ResolveType(EventValueType configured) => detectedType ?? configured;

        internal void Reset(bool boolValue, int intValue, float floatValue, string text,
            Vector2 vector2, Vector3 vector3, Vector4 vector4, Color color, Quaternion quaternion)
        {
            detectedType = null;
            Bool = boolValue; Int = intValue; Float = floatValue; stringValue = text ?? string.Empty;
            Vector2 = vector2; Vector3 = vector3; Vector4 = vector4; Color = color; Quaternion = quaternion;
        }

        internal EventBridgePayload Capture(EventValueType configured)
        {
            var resolved = ResolveType(configured);
            var payload = new EventBridgePayload { type = (int)resolved };
            switch (resolved)
            {
                case EventValueType.Bool: payload.boolValue = Bool; break;
                case EventValueType.Int: payload.intValue = Int; break;
                case EventValueType.Float: payload.floatValue = Float; break;
                case EventValueType.String: payload.stringValue = String; break;
                case EventValueType.Vector2: payload.v2x = Vector2.x; payload.v2y = Vector2.y; break;
                case EventValueType.Vector3: payload.v3x = Vector3.x; payload.v3y = Vector3.y; payload.v3z = Vector3.z; break;
                case EventValueType.Vector4: payload.v4x = Vector4.x; payload.v4y = Vector4.y; payload.v4z = Vector4.z; payload.v4w = Vector4.w; break;
                case EventValueType.Color: payload.cr = Color.r; payload.cg = Color.g; payload.cb = Color.b; payload.ca = Color.a; break;
                case EventValueType.Quaternion: payload.qx = Quaternion.x; payload.qy = Quaternion.y; payload.qz = Quaternion.z; payload.qw = Quaternion.w; break;
            }
            return payload;
        }

        internal void Apply(EventBridgePayload payload)
        {
            if (payload == null) return;
            detectedType = (EventValueType)payload.type;
            switch (detectedType)
            {
                case EventValueType.Bool: Bool = payload.boolValue; break;
                case EventValueType.Int: Int = payload.intValue; break;
                case EventValueType.Float: Float = payload.floatValue; break;
                case EventValueType.String: stringValue = payload.stringValue ?? string.Empty; break;
                case EventValueType.Vector2: Vector2 = new Vector2(payload.v2x, payload.v2y); break;
                case EventValueType.Vector3: Vector3 = new Vector3(payload.v3x, payload.v3y, payload.v3z); break;
                case EventValueType.Vector4: Vector4 = new Vector4(payload.v4x, payload.v4y, payload.v4z, payload.v4w); break;
                case EventValueType.Color: Color = new Color(payload.cr, payload.cg, payload.cb, payload.ca); break;
                case EventValueType.Quaternion: Quaternion = new Quaternion(payload.qx, payload.qy, payload.qz, payload.qw); break;
            }
        }
    }
}
#endif
