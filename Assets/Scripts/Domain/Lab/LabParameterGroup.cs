namespace Domain.Lab
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// ScriptableObject containing a named group of lab parameters.
    /// Groups correspond to game sub-systems (Player, Combat, LevelGen, etc.).
    /// </summary>
    [CreateAssetMenu(fileName = "LabParameterGroup", menuName = "3D2DWorld/Lab/Parameter Group")]
    public class LabParameterGroup : ScriptableObject
    {
        [Header("Group Identity")]
        public string GroupId;
        public string DisplayName;

        [Header("Parameters")]
        public List<LabParameter> Parameters = new List<LabParameter>();

        /// <summary>Resets all parameters to their default values.</summary>
        public void ResetAll()
        {
            foreach (var p in Parameters)
                p.ResetToDefault();
        }

        /// <summary>Tries to find a parameter by ID.</summary>
        public bool TryGet(string id, out LabParameter param)
        {
            foreach (var p in Parameters)
            {
                if (p.Id == id)
                {
                    param = p;
                    return true;
                }
            }
            param = null;
            return false;
        }
    }
}
