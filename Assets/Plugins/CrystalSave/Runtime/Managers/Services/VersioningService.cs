#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Handles version related utilities such as migrations and
    /// serializer setup.
    /// </summary>
    public class VersioningService
    {
        readonly SaveManager manager;

        public VersionManager VersionManager { get; private set; }
        public MigrationManager MigrationManager { get; private set; }
        public SaveDataSerializer Serializer { get; private set; }

        public VersioningService(SaveManager manager)
        {
            this.manager = manager;
        }

        public Task InitializeAsync()
        {
            VersionManager = new VersionManager(manager.SaveSettings);
            Serializer = SaveDataSerializer.Instance;
            MigrationManager = AssetProvider.Load<MigrationManager>("MigrationManager")
                ?? throw new InvalidOperationException("MigrationManager asset not found.");

            manager.VersionManagerInternal = VersionManager;
            manager.SerializerInternal = Serializer;
            manager.MigrationManagerInternal = MigrationManager;
            return Task.CompletedTask;
        }

        public void Migrate(SaveData data)
        {
            MigrationManager?.MigrateThrough(data, VersionManager?.CurrentVersion ?? manager.SaveSettings.version);

            // Backward-compat: rewrite legacy GameObject identifiers that were
            // saved with a component-id suffix before GetUniqueID was made
            // consistent for active and inactive design-time GameObjects.
            // Runs unconditionally because the bug also affects same-version
            // saves written by older builds.
            LegacyIdMigration.NormalizeSaveData(data);
        }

        public void ConfigureSerializer(Action<SaveDataSerializer> configure)
        {
            configure?.Invoke(Serializer);
        }
    }
}
#endif
