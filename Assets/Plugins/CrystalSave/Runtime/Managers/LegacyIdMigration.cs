#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Backward-compatibility migration for save files written before the
    /// GameObject-level identifier was made consistent across active and
    /// inactive design-time GameObjects.
    ///
    /// <para>
    /// Older versions of <see cref="GameObjectTracker.GetUniqueID"/> returned
    /// the suffixed component identifier (<c>{UniqueID}_{ComponentID}</c>) for
    /// GameObjects whose <see cref="RememberGameObject"/> had executed Awake
    /// before scene-load registration ran (i.e. design-time-active objects),
    /// while design-time-inactive GameObjects were registered with the bare
    /// <see cref="UniqueID"/> value. This produced mixed keys in
    /// <see cref="SaveData.GameObjectStates"/> and
    /// <see cref="SaveData.DestroyedGameObjects"/> that no longer resolve
    /// after the fix that always uses the bare GameObject identifier.
    /// </para>
    ///
    /// <para>
    /// This class normalizes saved keys by stripping a trailing
    /// <c>_{guid}</c> suffix when the suffix matches the GUID format produced
    /// by <see cref="System.Guid.NewGuid"/> (default <c>"D"</c> format with
    /// dashes used by <see cref="SaveableComponent.ComponentID"/>) or the
    /// <c>"N"</c> format used by the <see cref="UniqueID"/> component itself.
    /// Bare GameObject IDs (32 hex chars without dashes) cannot match the
    /// stricter <c>"D"</c> pattern, so legitimate IDs are not corrupted.
    /// </para>
    /// </summary>
    internal static class LegacyIdMigration
    {
        // GUID "D" format: 8-4-4-4-12 hex digits separated by dashes.
        // This matches the default ToString() form used by SaveableComponent.componentID.
        private static readonly Regex ComponentSuffixPattern = new Regex(
            @"_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
            RegexOptions.Compiled);

        /// <summary>
        /// If <paramref name="id"/> ends with a component-id suffix in the
        /// canonical GUID "D" format, returns the stripped (bare) identifier
        /// via <paramref name="bareId"/> and returns <c>true</c>.
        /// Otherwise <paramref name="bareId"/> is set to <paramref name="id"/>
        /// and <c>false</c> is returned.
        /// </summary>
        public static bool TryStripComponentSuffix(string id, out string bareId)
        {
            if (string.IsNullOrEmpty(id))
            {
                bareId = id;
                return false;
            }

            var match = ComponentSuffixPattern.Match(id);
            if (match.Success)
            {
                bareId = id.Substring(0, match.Index);
                return true;
            }

            bareId = id;
            return false;
        }

        /// <summary>
        /// Normalizes legacy GameObject identifiers in the supplied
        /// <see cref="SaveData"/> in-place. Targets <see cref="SaveData.GameObjectStates"/>
        /// and <see cref="SaveData.DestroyedGameObjects"/> only; component-level
        /// data (which legitimately retains the component-id suffix) is left
        /// untouched.
        /// </summary>
        /// <param name="data">The save data to normalize. Safe to call with null.</param>
        /// <returns>The number of identifiers that were rewritten.</returns>
        public static int NormalizeSaveData(SaveData data)
        {
            if (data == null) return 0;

            int rewrites = 0;

            if (data.GameObjectStates != null && data.GameObjectStates.Count > 0)
            {
                // Build a set of bare IDs already present so we can drop
                // suffixed duplicates in favour of the bare entry (which was
                // written by inactive-at-design-time objects and is therefore
                // the canonical key used by the post-fix tracker).
                var bareIds = new HashSet<string>();
                for (int i = 0; i < data.GameObjectStates.Count; i++)
                {
                    var s = data.GameObjectStates[i];
                    if (s != null && !string.IsNullOrEmpty(s.UniqueID) &&
                        !ComponentSuffixPattern.IsMatch(s.UniqueID))
                    {
                        bareIds.Add(s.UniqueID);
                    }
                }

                for (int i = data.GameObjectStates.Count - 1; i >= 0; i--)
                {
                    var s = data.GameObjectStates[i];
                    if (s == null || string.IsNullOrEmpty(s.UniqueID)) continue;

                    if (TryStripComponentSuffix(s.UniqueID, out var bare))
                    {
                        if (bareIds.Contains(bare))
                        {
                            // A bare entry already exists; drop the suffixed duplicate.
                            data.GameObjectStates.RemoveAt(i);
                        }
                        else
                        {
                            s.UniqueID = bare;
                            bareIds.Add(bare);
                        }
                        rewrites++;
                    }
                }
            }

            if (data.DestroyedGameObjects != null && data.DestroyedGameObjects.Count > 0)
            {
                var seen = new HashSet<string>();
                for (int i = data.DestroyedGameObjects.Count - 1; i >= 0; i--)
                {
                    var id = data.DestroyedGameObjects[i];
                    if (string.IsNullOrEmpty(id)) continue;

                    string normalized = id;
                    if (TryStripComponentSuffix(id, out var bare))
                    {
                        normalized = bare;
                        rewrites++;
                    }

                    if (!seen.Add(normalized))
                    {
                        // Duplicate after normalization — drop.
                        data.DestroyedGameObjects.RemoveAt(i);
                    }
                    else if (!ReferenceEquals(normalized, id))
                    {
                        data.DestroyedGameObjects[i] = normalized;
                    }
                }
            }

            if (rewrites > 0)
            {
                Logger.Log(
                    $"LegacyIdMigration: normalized {rewrites} legacy GameObject identifier(s) in save data.",
                    LogCategory.SaveManager,
                    LogLevel.Info);
            }

            return rewrites;
        }
    }
}
#endif
