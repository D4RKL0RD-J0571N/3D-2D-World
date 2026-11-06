namespace Core.Ids
{
    using System;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// Canonical, immutable, namespaced identifier for all engine, framework, and mod content definitions.
    /// Enforces the format <c>namespace:path</c> (e.g., <c>nordic_rpg:item/iron_sword</c>).
    /// Precomputes a 32-bit FNV-1a hash for O(1) dictionary lookups.
    /// </summary>
    [Serializable]
    public struct ResourceId : IEquatable<ResourceId>, IComparable<ResourceId>, ISerializationCallbackReceiver
    {
        #region Constants

        public const string DefaultNamespace = "nordic_rpg";
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        #endregion

        #region Serialized Fields

        [SerializeField]
        private string _value;

        #endregion

        #region Non-Serialized Cache

        [NonSerialized]
        private uint _hash;

        [NonSerialized]
        private int _colonIndex;

        #endregion

        #region Properties

        /// <summary>Gets an empty, unassigned <see cref="ResourceId"/>.</summary>
        public static ResourceId Empty => default;

        /// <summary>Returns true if this identifier is unset or empty.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(_value);

        /// <summary>Returns true if this identifier is non-empty and valid.</summary>
        public bool IsValid => !IsEmpty;

        /// <summary>Gets the full canonical string (<c>namespace:path</c>).</summary>
        public string Value => _value ?? string.Empty;

        /// <summary>Gets the namespace segment before the colon (e.g., <c>nordic_rpg</c>).</summary>
        public string Namespace
        {
            get
            {
                EnsureInitialized();
                return _colonIndex > 0 ? _value.Substring(0, _colonIndex) : string.Empty;
            }
        }

        /// <summary>Gets the path segment after the colon (e.g., <c>item/iron_sword</c>).</summary>
        public string Path
        {
            get
            {
                EnsureInitialized();
                return _colonIndex > 0 && _colonIndex + 1 < _value.Length
                    ? _value.Substring(_colonIndex + 1)
                    : string.Empty;
            }
        }

        /// <summary>Gets the precomputed 32-bit FNV-1a hash of the canonical string.</summary>
        public uint Hash
        {
            get
            {
                EnsureInitialized();
                return _hash;
            }
        }

        #endregion

        #region Constructors & Factory Methods

        /// <summary>
        /// Initializes a validated <see cref="ResourceId"/> from a <c>namespace:path</c> string.
        /// Throws <see cref="FormatException"/> if the format is invalid.
        /// </summary>
        public ResourceId(string rawId)
        {
            if (!TryNormalizeAndValidate(rawId, out string normalized, out int colonIdx, out string error))
            {
                throw new FormatException($"Invalid ResourceId '{rawId}': {error}");
            }

            _value = normalized;
            _colonIndex = colonIdx;
            _hash = ComputeFnv1aHash(normalized);
        }

        /// <summary>
        /// Initializes a validated <see cref="ResourceId"/> from separate namespace and path strings.
        /// </summary>
        public ResourceId(string namespacePart, string pathPart)
            : this($"{namespacePart}:{pathPart}")
        {
        }

        /// <summary>
        /// Parses a <c>namespace:path</c> string into a <see cref="ResourceId"/>, throwing <see cref="FormatException"/> on failure.
        /// </summary>
        public static ResourceId Parse(string rawId)
        {
            return new ResourceId(rawId);
        }

        /// <summary>
        /// Attempts to parse a <c>namespace:path</c> string without throwing exceptions.
        /// </summary>
        public static bool TryParse(string rawId, out ResourceId result)
        {
            if (TryNormalizeAndValidate(rawId, out string normalized, out int colonIdx, out _))
            {
                result = new ResourceId
                {
                    _value = normalized,
                    _colonIndex = colonIdx,
                    _hash = ComputeFnv1aHash(normalized)
                };
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Deterministically synthesizes a valid <see cref="ResourceId"/> from a legacy string ID or asset name
        /// (e.g., <c>("nordic_rpg", "item", "ITM_0001")</c> becomes <c>nordic_rpg:item/itm_0001</c>).
        /// </summary>
        public static ResourceId FromLegacyId(string defaultNamespace, string categoryFolder, string legacyIdOrName)
        {
            if (TryParse(legacyIdOrName, out ResourceId alreadyNamespaced))
            {
                return alreadyNamespaced;
            }

            string ns = SanitizeSegment(string.IsNullOrWhiteSpace(defaultNamespace) ? DefaultNamespace : defaultNamespace, false);
            string cat = SanitizeSegment(categoryFolder, true);
            string slug = SanitizeSegment(legacyIdOrName, true);

            if (string.IsNullOrEmpty(slug))
            {
                slug = "unnamed";
            }

            string fullPath = string.IsNullOrEmpty(cat) ? slug : $"{cat}/{slug}";
            return new ResourceId($"{ns}:{fullPath}");
        }

        #endregion

        #region Validation & Hashing

        private void EnsureInitialized()
        {
            if (string.IsNullOrEmpty(_value))
            {
                _colonIndex = -1;
                _hash = 0u;
                return;
            }

            if (_hash == 0u || _colonIndex <= 0)
            {
                _colonIndex = _value.IndexOf(':');
                _hash = ComputeFnv1aHash(_value);
            }
        }

        private static bool TryNormalizeAndValidate(string raw, out string normalized, out int colonIndex, out string error)
        {
            normalized = null;
            colonIndex = -1;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "Identifier cannot be null or whitespace.";
                return false;
            }

            string trimmed = raw.Trim();
            colonIndex = trimmed.IndexOf(':');

            if (colonIndex <= 0 || colonIndex >= trimmed.Length - 1)
            {
                error = "Identifier must contain a single ':' separating non-empty namespace and path.";
                return false;
            }

            if (trimmed.IndexOf(':', colonIndex + 1) >= 0)
            {
                error = "Identifier cannot contain more than one ':' character.";
                return false;
            }

            for (int i = 0; i < colonIndex; i++)
            {
                char c = trimmed[i];
                if (!IsValidNamespaceChar(c))
                {
                    error = $"Invalid character '{c}' in namespace. Only lowercase [a-z0-9_] are permitted.";
                    return false;
                }
            }

            for (int i = colonIndex + 1; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (!IsValidPathChar(c))
                {
                    error = $"Invalid character '{c}' in path. Only lowercase [a-z0-9_/.-] are permitted.";
                    return false;
                }
            }

            normalized = trimmed;
            return true;
        }

        private static bool IsValidNamespaceChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
        }

        private static bool IsValidPathChar(char c)
        {
            return IsValidNamespaceChar(c) || c == '/' || c == '.' || c == '-';
        }

        private static string SanitizeSegment(string input, bool allowSlashAndDash)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(input.Length);
            foreach (char rawChar in input.Trim())
            {
                char c = char.ToLowerInvariant(rawChar);
                if (IsValidNamespaceChar(c) || (allowSlashAndDash && (c == '/' || c == '-' || c == '.')))
                {
                    sb.Append(c);
                }
                else if (c == ' ' || c == '\\')
                {
                    sb.Append('_');
                }
            }

            return sb.ToString().Trim('_');
        }

        private static uint ComputeFnv1aHash(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0u;
            }

            uint hash = FnvOffsetBasis;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= FnvPrime;
            }

            return hash == 0u ? 1u : hash;
        }

        #endregion

        #region Equality, Comparison & Serialization Callbacks

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (!string.IsNullOrEmpty(_value))
            {
                _value = _value.Trim().ToLowerInvariant();
                _colonIndex = _value.IndexOf(':');
                _hash = ComputeFnv1aHash(_value);
            }
        }

        public bool Equals(ResourceId other)
        {
            if (IsEmpty && other.IsEmpty)
            {
                return true;
            }

            if (Hash != other.Hash)
            {
                return false;
            }

            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)Hash);
        }

        public int CompareTo(ResourceId other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(ResourceId left, ResourceId right) => left.Equals(right);

        public static bool operator !=(ResourceId left, ResourceId right) => !left.Equals(right);

        #endregion
    }
}
