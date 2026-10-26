#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrationManager", menuName = "Crystal Save/Settings/Create Migration Manager")]
	public class MigrationManager : ScriptableObject
	{
		[SerializeField]
		private List<MigrationStep> migrationSteps = new List<MigrationStep>();

		/// <summary>
		/// Applies all necessary migration steps to the provided SaveData.
		/// </summary>
		/// <param name="data">The SaveData instance to migrate.</param>
        public void Migrate(SaveData data) => MigrateThrough(data, null);

        // Automatic migration is bounded by the running game's version. The existing
        // public method retains its explicit all-configured-steps behavior for tools.
        internal void MigrateThrough(SaveData data, VersionData maximumVersion)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Version == null)
                throw new InvalidOperationException("MigrationManager: SaveData.Version is null. Cannot determine migration steps.");
            if (migrationSteps == null || migrationSteps.Count == 0)
                return;

            var originalVersion = (VersionData)data.Version.Clone();
            var upperBound = maximumVersion == null ? null : (VersionData)maximumVersion.Clone();
            var ordered = new List<(VersionData Target, MigrationAction[] Actions, int Order)>();
            var seenVersions = new HashSet<string>();
            for (int index = 0; index < migrationSteps.Count; index++)
            {
                var step = migrationSteps[index];
                if (step?.targetVersion == null)
                {
                    Logger.Log("MigrationManager: Encountered a null step or target version. Skipping.", LogLevel.Warning);
                    continue;
                }
                var target = (VersionData)step.targetVersion.Clone();
                if (originalVersion.CompareTo(target) >= 0 ||
                    upperBound != null && target.CompareTo(upperBound) > 0)
                    continue;
                if (!seenVersions.Add(target.ToString()))
                    Logger.Log($"MigrationManager: Multiple migration steps target version {target}. They will run in serialized list order after sorting.", LogLevel.Warning);
                // Freeze the plan before user actions run. Changes to data.Version or
                // migration assets inside a callback cannot silently skip later actions.
                ordered.Add((target, step.migrationActions?.ToArray() ?? Array.Empty<MigrationAction>(), index));
            }
            ordered.Sort((left, right) =>
            {
                int comparison = left.Target.CompareTo(right.Target);
                return comparison != 0 ? comparison : left.Order.CompareTo(right.Order);
            });

            int next = 0;
            while (next < ordered.Count)
            {
                VersionData target = ordered[next].Target;
                var previousVersion = (VersionData)data.Version.Clone();
                try
                {
                    do
                    {
                        var step = ordered[next++];
                        if (step.Actions.Length == 0)
                            Logger.Log($"MigrationManager: Step targeting version {target} has no MigrationActions.", LogLevel.Warning);
                        foreach (var action in step.Actions)
                        {
                            if (action == null)
                            {
                                Logger.Log($"MigrationManager: Encountered a null MigrationAction in step targeting version {target}.", LogLevel.Warning);
                                continue;
                            }
                            action.ApplyMigration(data);
                            Logger.Log($"MigrationManager: Applied action '{action.name}' for version {target}.", LogLevel.Info);
                        }
                    }
                    while (next < ordered.Count && ordered[next].Target.CompareTo(target) == 0);
                }
                catch (Exception error)
                {
                    // A failed group has not reached its version. Load/import callers
                    // discard this detached candidate, including any partially edited bytes.
                    data.Version = previousVersion;
                    throw new InvalidOperationException($"Migration to version {target} failed.", error);
                }
                data.Version = (VersionData)target.Clone();
                Logger.Log($"MigrationManager: Updated SaveData version to {data.Version}.", LogLevel.Info);
            }
        }

	}

	[System.Serializable]
	public class MigrationStep
	{
		public VersionData targetVersion;
		public string description;
		public List<MigrationAction> migrationActions = new List<MigrationAction>();
	}
}
#endif
