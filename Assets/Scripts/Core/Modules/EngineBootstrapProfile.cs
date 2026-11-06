namespace Core.Modules
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Configurable bootstrap profile that dictates which <see cref="IEngineModule"/>s are loaded by <see cref="ModuleHost"/>.
    /// Supports booting Engine Core in isolation (<see cref="CoreOnlyMode"/>) without any Fantasy Framework modules.
    /// </summary>
    [CreateAssetMenu(fileName = "EngineBootstrapProfile", menuName = "3D2DWorld/Engine/Bootstrap Profile")]
    public sealed class EngineBootstrapProfile : ScriptableObject
    {
        [Tooltip("If true, only modules marked with [BuiltInEngineModule(isCoreModule: true)] are loaded (omits Fantasy Framework).")]
        public bool CoreOnlyMode = false;

        [Tooltip("Optional explicit whitelist of module IDs to load. If empty, all discovered modules matching CoreOnlyMode are loaded.")]
        public List<string> EnabledModuleIds = new();

        /// <summary>Creates a transient runtime profile configured to boot only Layer A Engine Core modules.</summary>
        public static EngineBootstrapProfile CreateCoreOnlyProfile()
        {
            var profile = CreateInstance<EngineBootstrapProfile>();
            profile.CoreOnlyMode = true;
            return profile;
        }
    }
}
