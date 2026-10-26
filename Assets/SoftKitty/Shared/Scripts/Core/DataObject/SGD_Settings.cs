using UnityEngine;
using System.Collections.Generic;


namespace SoftKitty
{
    /// <summary>
    /// This is the core data object of the system, handling system-wide configuration settings.
    /// </summary>
    [CreateAssetMenu(fileName = "SGD_Settings", menuName = "Soft Kitty/Data Objects/General Settings Data")]
    public class SGD_Settings : ScriptableObject
    {
        #region Variables
        public const string CONFIG_NAME = "com.SoftKitty.settings";
        public int DebugLevel = 1;
        public List<DataObject> DataObjects=new List<DataObject>();
        public float VolumeMultiplier = 1F;
        public int AudioPriority = 128;
        public bool GraphDebugMode = false;
        public bool data_expand = false;
        public bool audio_expand = false;
        public bool general_expand = false;
        public LayerMask GroundLayer;
        public AssetLoader CustomLoader;
        private static SGD_Settings _instance;
        private static Dictionary<System.Type, DataObject> DataDic = new Dictionary<System.Type, DataObject>();

       
#endregion

#region Internal Methods
        private void OnEnable()
        {
            if (_instance == null) _instance = this;
        }

        public void ApplyData()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        #endregion
       

 #region Methods:
        /// <summary>
        /// Returns whether the system is currently in runtime mode (true or false). Useful for checking if the game is running or if you¡¯re in the editor.
        /// </summary>
        public static bool isRuntime
        {
            get
            {
#if UNITY_EDITOR
                return Application.isPlaying;
#else
                return true;
#endif
            }
        }

        /// <summary>
        /// Returns the singleton instance assigned in ProjectSettings > SoftKitty > Data Settings. Access this to get global system settings.
        /// </summary>
        public static SGD_Settings Instance
        {
            get
            {
                if (_instance) return _instance;
#if UNITY_EDITOR
                UnityEditor.EditorBuildSettings.TryGetConfigObject(CONFIG_NAME, out _instance);
#else
        // Loads from the memory.
        _instance = FindObjectOfType<SGD_Settings>();
#endif
                return _instance;
            }
        }

        /// <summary>
        /// Retrieves sub-data objects like EntityManager, Attributes, and OverTimeEffects. These objects are defined in ProjectSettings > SoftKitty > Data Settings.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetData<T>() where T : DataObject
        {
            if (DataDic.ContainsKey(typeof(T)))
            {
                if(DataDic[typeof(T)]!=null) return (T)DataDic[typeof(T)];
            }
            foreach (var obj in DataObjects) {
                if (typeof(T).ToString() == obj.TypeString())
                {
                    DataDic.Add(typeof(T), obj);
                    return (T)DataDic[typeof(T)];
                }
            }
            return null;
        }

        /// <summary>
        /// Refresh the sub-data objects like EntityManager, Attributes, and OverTimeEffects. This is required if you manually assigned them via script to the database.
        /// </summary>
        public void RefreshDatabase()
        {
            DataDic.Clear();
        }
 #endregion


    }
}
