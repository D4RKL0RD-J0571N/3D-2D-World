namespace Core.Ids
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using UnityEngine;

    /// <summary>
    /// Serializes <see cref="ResourceId"/> as a compact JSON string (<c>"namespace:path"</c>).
    /// </summary>
    public sealed class ResourceIdJsonConverter : JsonConverter<ResourceId>
    {
        public override void WriteJson(JsonWriter writer, ResourceId value, JsonSerializer serializer)
        {
            if (value.IsEmpty)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(value.Value);
        }

        public override ResourceId ReadJson(
            JsonReader reader,
            Type objectType,
            ResourceId existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return ResourceId.Empty;
            }

            string raw = reader.Value?.ToString();
            return string.IsNullOrWhiteSpace(raw) ? ResourceId.Empty : ResourceId.Parse(raw);
        }
    }

    /// <summary>
    /// Serializes <see cref="InstanceGuid"/> as a 32-character hex JSON string.
    /// </summary>
    public sealed class InstanceGuidJsonConverter : JsonConverter<InstanceGuid>
    {
        public override void WriteJson(JsonWriter writer, InstanceGuid value, JsonSerializer serializer)
        {
            if (value.IsEmpty)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(value.Value);
        }

        public override InstanceGuid ReadJson(
            JsonReader reader,
            Type objectType,
            InstanceGuid existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return InstanceGuid.Empty;
            }

            string raw = reader.Value?.ToString();
            return string.IsNullOrWhiteSpace(raw) ? InstanceGuid.Empty : InstanceGuid.Parse(raw);
        }
    }

    /// <summary>
    /// Serializes Unity <see cref="Vector3"/> without self-referencing <c>normalized</c> loop exceptions.
    /// </summary>
    public sealed class Vector3JsonConverter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3 ReadJson(
            JsonReader reader,
            Type objectType,
            Vector3 existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return Vector3.zero;
            }

            JObject obj = JObject.Load(reader);
            return new Vector3(
                obj.Value<float?>("x") ?? 0f,
                obj.Value<float?>("y") ?? 0f,
                obj.Value<float?>("z") ?? 0f);
        }
    }

    /// <summary>
    /// Serializes Unity <see cref="Quaternion"/> cleanly as <c>{ x, y, z, w }</c>.
    /// </summary>
    public sealed class QuaternionJsonConverter : JsonConverter<Quaternion>
    {
        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WritePropertyName("w");
            writer.WriteValue(value.w);
            writer.WriteEndObject();
        }

        public override Quaternion ReadJson(
            JsonReader reader,
            Type objectType,
            Quaternion existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return Quaternion.identity;
            }

            JObject obj = JObject.Load(reader);
            return new Quaternion(
                obj.Value<float?>("x") ?? 0f,
                obj.Value<float?>("y") ?? 0f,
                obj.Value<float?>("z") ?? 0f,
                obj.Value<float?>("w") ?? 1f);
        }
    }

    /// <summary>
    /// Serializes Unity <see cref="Color"/> cleanly as <c>{ r, g, b, a }</c>.
    /// </summary>
    public sealed class ColorJsonConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("r");
            writer.WriteValue(value.r);
            writer.WritePropertyName("g");
            writer.WriteValue(value.g);
            writer.WritePropertyName("b");
            writer.WriteValue(value.b);
            writer.WritePropertyName("a");
            writer.WriteValue(value.a);
            writer.WriteEndObject();
        }

        public override Color ReadJson(
            JsonReader reader,
            Type objectType,
            Color existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return Color.white;
            }

            JObject obj = JObject.Load(reader);
            return new Color(
                obj.Value<float?>("r") ?? 1f,
                obj.Value<float?>("g") ?? 1f,
                obj.Value<float?>("b") ?? 1f,
                obj.Value<float?>("a") ?? 1f);
        }
    }

    /// <summary>
    /// Provides preconfigured <see cref="JsonSerializerSettings"/> for all external JSON files (mods, worlds, saves) per ADR-003.
    /// </summary>
    public static class FantasyEngineJsonSettings
    {
        public static JsonSerializerSettings CreateDefault(bool indented = true)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = indented ? Formatting.Indented : Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            settings.Converters.Add(new ResourceIdJsonConverter());
            settings.Converters.Add(new InstanceGuidJsonConverter());
            settings.Converters.Add(new Vector3JsonConverter());
            settings.Converters.Add(new QuaternionJsonConverter());
            settings.Converters.Add(new ColorJsonConverter());
            return settings;
        }
    }
}
