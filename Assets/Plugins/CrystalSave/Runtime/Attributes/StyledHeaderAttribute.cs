#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Drop-in replacement for Unity's <see cref="HeaderAttribute"/> that renders
    /// with the Crystal Save dark-background section header style in the Inspector.
    /// </summary>
    public class StyledHeaderAttribute : PropertyAttribute
    {
        public readonly string Header;

        public StyledHeaderAttribute(string header)
        {
            Header = header;
        }
    }
}
#endif
