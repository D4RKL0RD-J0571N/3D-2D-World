#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Globalization;
using System.IO;
#if CRYSTALSAVE_NEWTONSOFT
using Newtonsoft.Json;
#endif

namespace Arawn.CrystalSave.Runtime
{
    // Only the existing provider DTOs/dictionaries use this codec. It never resolves
    // polymorphic type names. JsonUtility truncates strings at embedded null characters.
    internal static class CloudMetadataJson
    {
#if CRYSTALSAVE_NEWTONSOFT
        static JsonSerializer CreateSerializer() => JsonSerializer.Create(new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = 64,
            Culture = CultureInfo.InvariantCulture
        });
#endif
        internal static string Encode<T>(T value)
        {
#if CRYSTALSAVE_NEWTONSOFT
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            using (var writer = new JsonTextWriter(output)) CreateSerializer().Serialize(writer, value);
            return output.ToString();
#else
            throw MissingPackage();
#endif
        }

        internal static T Decode<T>(string json)
        {
#if CRYSTALSAVE_NEWTONSOFT
            if (string.IsNullOrEmpty(json)) throw new InvalidDataException("Cloud metadata JSON is empty.");
            try
            {
                using var input = new StringReader(json);
                using var reader = new JsonTextReader(input)
                {
                    DateParseHandling = DateParseHandling.None,
                    MaxDepth = 64,
                    SupportMultipleContent = true
                };
                var result = CreateSerializer().Deserialize<T>(reader);
                if (reader.Read()) throw new InvalidDataException("Trailing cloud metadata JSON.");
                return result;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Cloud metadata JSON is invalid.", ex);
            }
#else
            throw MissingPackage();
#endif
        }

        static NotSupportedException MissingPackage() => new(
            "Cloud metadata requires com.unity.nuget.newtonsoft-json. Install the declared Crystal Save dependency and enable CRYSTALSAVE_NEWTONSOFT.");
    }
}
#endif
