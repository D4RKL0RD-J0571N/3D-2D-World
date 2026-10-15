namespace Data.Config
{
    using UnityEngine;

    public abstract class EntityData : ScriptableObject
    {
        [Header("Base Entity Details")]
        [Tooltip("Auto-generated ID. Do not edit manually.")]

        public string Id;
        public string DisplayName;

        public abstract string CategoryPrefix { get; }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(Id))
            {
                // Delay the call because AssetDatabase operations inside OnValidate can cause Unity warnings
                UnityEditor.EditorApplication.delayCall -= AssignNewId;
                UnityEditor.EditorApplication.delayCall += AssignNewId;
            }
        }

        private void AssignNewId()
        {
            UnityEditor.EditorApplication.delayCall -= AssignNewId;
            if (this == null) return; // Asset was deleted before delay call
            if (!string.IsNullOrEmpty(Id)) return;

            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(EntityData)}");
            int maxId = 0;

            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<EntityData>(path);

                if (asset != null && asset != this && asset.CategoryPrefix == this.CategoryPrefix)
                {
                    if (!string.IsNullOrEmpty(asset.Id) && asset.Id.StartsWith(CategoryPrefix + "_"))
                    {
                        string numberPart = asset.Id.Substring(CategoryPrefix.Length + 1);
                        if (int.TryParse(numberPart, out int parsedNum))
                        {
                            if (parsedNum > maxId) maxId = parsedNum;
                        }
                    }
                }
            }

            Id = $"{CategoryPrefix}_{(maxId + 1):D4}";
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif

#if UNITY_EDITOR
        protected virtual void OnIdAssigned()
        {
        }
#endif
    }
}
