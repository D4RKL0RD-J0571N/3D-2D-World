namespace Domain.Lab
{
    using System;
    using UnityEngine;

    /// <summary>The data type a lab parameter can represent.</summary>
    public enum LabParamType { Float, Int, Bool }

    /// <summary>
    /// Serializable descriptor for a single tuneable runtime parameter.
    /// Values are stored as floats; bool uses 0/1.
    /// </summary>
    [Serializable]
    public class LabParameter
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        [TextArea(1, 2)]
        public string Description;

        [Header("Type & Range")]
        public LabParamType Type = LabParamType.Float;
        public float MinValue   = 0f;
        public float MaxValue   = 100f;
        public float DefaultValue;

        [Header("Runtime")]
        [NonSerialized]
        public float CurrentValue;

        /// <summary>Resets CurrentValue to DefaultValue.</summary>
        public void ResetToDefault() => CurrentValue = DefaultValue;

        /// <summary>Clamps and assigns a new runtime value.</summary>
        public void SetValue(float value)
        {
            CurrentValue = Mathf.Clamp(value, MinValue, MaxValue);
        }

        /// <summary>Returns CurrentValue as bool (non-zero = true).</summary>
        public bool AsBool() => CurrentValue > 0.5f;

        /// <summary>Returns CurrentValue rounded to int.</summary>
        public int AsInt() => Mathf.RoundToInt(CurrentValue);
    }
}
