#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    // Scoped to one restore, so pooled identities and later loads never reuse stale helpers.
    internal sealed class PrefabComponentBlobLookup
    {
        private readonly Component root;
        private readonly Func<ISaveable, bool> eligible;
        private readonly Dictionary<string, List<ISaveable>> bySuffix = new(StringComparer.Ordinal);

        internal PrefabComponentBlobLookup(Component root, Func<ISaveable, bool> eligible)
        {
            this.root = root;
            this.eligible = eligible;
            Rebuild();
        }

        private void Rebuild()
        {
            bySuffix.Clear();
            PrefabLoadDiagnosticCounters.RecordComponentBlobHierarchyScan();
            foreach (var helper in root.GetComponentsInChildren<ISaveable>(true))
            {
                if (helper is UnityEngine.Object obj && !obj) continue;
                string id = helper.UniqueIdentifier;
                if (string.IsNullOrEmpty(id)) continue;
                // Preserve the legacy EndsWith("_" + suffix) contract, including IDs
                // containing underscores and first eligible helper in hierarchy order.
                for (int separator = id.IndexOf('_'); separator >= 0; separator = id.IndexOf('_', separator + 1))
                {
                    string suffix = id.Substring(separator + 1);
                    if (!bySuffix.TryGetValue(suffix, out var candidates))
                        bySuffix.Add(suffix, candidates = new List<ISaveable>());
                    candidates.Add(helper);
                }
            }
        }

        internal ISaveable Find(string suffix)
        {
            string key = suffix ?? string.Empty;
            var found = FindIndexed(key);
            if (found != null) return found;
            // A previous LoadData callback can add, reparent, suppress or rebind helpers.
            // Keep that compatibility fallback off the normal successful lookup path.
            Rebuild();
            return FindIndexed(key);
        }

        private ISaveable FindIndexed(string suffix)
        {
            if (!bySuffix.TryGetValue(suffix, out var candidates)) return null;
            foreach (var helper in candidates)
            {
                if (helper is UnityEngine.Object obj && !obj || !eligible(helper)) continue;
                string id = helper.UniqueIdentifier;
                if (!string.IsNullOrEmpty(id) && id.EndsWith("_" + suffix, StringComparison.Ordinal))
                    return helper;
            }
            return null;
        }
    }
}
#endif
