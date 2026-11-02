namespace Core.Modding
{
    using System;
    using System.Collections.Generic;
    using Core.Ids;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a single mod entry inside an active <see cref="ModProfile"/>.
    /// </summary>
    [Serializable]
    public sealed class ModProfileEntry
    {
        [JsonProperty("modId")]
        public string ModId { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonProperty("contentHash")]
        public string ContentHash { get; set; } = string.Empty;

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Defines an ordered collection of enabled Content Packs and User Mods (<c>ARCH-04</c>).
    /// Supports hot-swapping between profiles (e.g., <c>"Vanilla (No Mods)"</c> vs modded loadouts)
    /// without restarting the application.
    /// </summary>
    [Serializable]
    public sealed class ModProfile
    {
        [JsonProperty("profileId")]
        public string ProfileId { get; set; } = "default";

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = "Default Loadout";

        [JsonProperty("entries")]
        public List<ModProfileEntry> Entries { get; set; } = new();

        /// <summary>Creates a clean vanilla profile containing only the base <c>nordic_rpg</c> content pack.</summary>
        public static ModProfile CreateVanillaProfile()
        {
            return new ModProfile
            {
                ProfileId = "vanilla",
                DisplayName = "Vanilla (No Mods)",
                Entries = new List<ModProfileEntry>
                {
                    new ModProfileEntry
                    {
                        ModId = ResourceId.DefaultNamespace,
                        Version = "1.0.0",
                        Enabled = true
                    }
                }
            };
        }

        /// <summary>Creates a profile enabling all discovered manifests in discovery order.</summary>
        public static ModProfile CreateFromManifests(IEnumerable<ModManifest> manifests, string profileId = "default", string displayName = "All Discovered Mods")
        {
            var profile = new ModProfile
            {
                ProfileId = profileId,
                DisplayName = displayName
            };

            if (manifests != null)
            {
                foreach (var manifest in manifests)
                {
                    if (manifest == null || string.IsNullOrWhiteSpace(manifest.ModId)) continue;
                    profile.Entries.Add(new ModProfileEntry
                    {
                        ModId = manifest.ModId,
                        Version = manifest.Version ?? "1.0.0",
                        Enabled = true
                    });
                }
            }

            return profile;
        }
    }
}
