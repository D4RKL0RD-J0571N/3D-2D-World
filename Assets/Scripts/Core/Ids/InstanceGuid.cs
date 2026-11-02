namespace Core.Ids
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Deterministic 128-bit unique identifier for placed world instances and runtime save entities.
    /// Distinguishes individual instances (e.g., two placed Iron Swords) from their shared <see cref="ResourceId"/> definition.
    /// </summary>
    [Serializable]
    public struct InstanceGuid : IEquatable<InstanceGuid>, IComparable<InstanceGuid>, ISerializationCallbackReceiver
    {
        #region Serialized Fields

        [SerializeField]
        private string _value;

        #endregion

        #region Non-Serialized Cache

        [NonSerialized]
        private Guid _guid;

        [NonSerialized]
        private bool _isParsed;

        #endregion

        #region Properties

        /// <summary>Gets an empty, unassigned <see cref="InstanceGuid"/>.</summary>
        public static InstanceGuid Empty => default;

        /// <summary>Returns true if this instance GUID is empty or unset.</summary>
        public bool IsEmpty => ToGuid() == Guid.Empty;

        /// <summary>Gets the canonical 32-character lowercase hex representation.</summary>
        public string Value
        {
            get
            {
                EnsureParsed();
                return _guid == Guid.Empty ? string.Empty : _value;
            }
        }

        #endregion

        #region Constructors & Factory Methods

        public InstanceGuid(Guid guid)
        {
            _guid = guid;
            _value = guid == Guid.Empty ? string.Empty : guid.ToString("N");
            _isParsed = true;
        }

        /// <summary>Generates a new unique 128-bit <see cref="InstanceGuid"/>.</summary>
        public static InstanceGuid NewGuid()
        {
            return new InstanceGuid(Guid.NewGuid());
        }

        /// <summary>Parses a GUID string into an <see cref="InstanceGuid"/>, throwing <see cref="FormatException"/> on invalid input.</summary>
        public static InstanceGuid Parse(string raw)
        {
            if (!TryParse(raw, out InstanceGuid result))
            {
                throw new FormatException($"Invalid InstanceGuid '{raw}'.");
            }

            return result;
        }

        /// <summary>Attempts to parse a GUID string into an <see cref="InstanceGuid"/> without throwing.</summary>
        public static bool TryParse(string raw, out InstanceGuid result)
        {
            if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw.Trim(), out Guid parsed) && parsed != Guid.Empty)
            {
                result = new InstanceGuid(parsed);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>Returns the underlying <see cref="Guid"/> value.</summary>
        public Guid ToGuid()
        {
            EnsureParsed();
            return _guid;
        }

        #endregion

        #region Internal Helpers & Serialization

        private void EnsureParsed()
        {
            if (_isParsed)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_value) && Guid.TryParse(_value, out Guid parsed))
            {
                _guid = parsed;
                _value = parsed == Guid.Empty ? string.Empty : parsed.ToString("N");
            }
            else
            {
                _guid = Guid.Empty;
                _value = string.Empty;
            }

            _isParsed = true;
        }

        public void OnBeforeSerialize()
        {
            if (_isParsed && _guid != Guid.Empty && string.IsNullOrEmpty(_value))
            {
                _value = _guid.ToString("N");
            }
        }

        public void OnAfterDeserialize()
        {
            _isParsed = false;
            EnsureParsed();
        }

        #endregion

        #region Equality & Comparison

        public bool Equals(InstanceGuid other)
        {
            return ToGuid().Equals(other.ToGuid());
        }

        public override bool Equals(object obj)
        {
            return obj is InstanceGuid other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ToGuid().GetHashCode();
        }

        public int CompareTo(InstanceGuid other)
        {
            return ToGuid().CompareTo(other.ToGuid());
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(InstanceGuid left, InstanceGuid right) => left.Equals(right);

        public static bool operator !=(InstanceGuid left, InstanceGuid right) => !left.Equals(right);

        #endregion
    }
}
