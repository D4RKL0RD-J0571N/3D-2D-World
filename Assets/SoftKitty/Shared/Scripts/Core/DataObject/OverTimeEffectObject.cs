using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    /// <summary>
    /// OverTimeEffectObject is a sub-data object that manages the over-time effect settings within the system. You can create this object by right-clicking in the Project Panel and selecting:
    /// Create > SoftKitty > Data Objects > Over Time Effect Data.
    /// </summary>
    [CreateAssetMenu(fileName = "OverTimeEffectObject", menuName = "Soft Kitty/Data Objects/Over Time Effect Data")]
    public class OverTimeEffectObject : DataObject
    {
        #region Variables
        /// <summary>
        /// A list of all over-time effects (DoT, buffs, debuffs) available in the system. Each effect in the list defines specific behavior, duration, and stack rules.
        /// </summary>
        public List<OverTimeEffect> overTimeEffects = new List<OverTimeEffect>();
        /// <summary>
        /// The tick interval for applying over-time effects. This defines how often effects are evaluated (e.g., every 1 second) during gameplay.
        /// </summary>
        public float OverTimeEffectInterval = 1F;
        public string SearchText = "";
        public int SearchType = 0;
        public string CategoryHash = "";
        public StringIdManager IdManager=new StringIdManager();
        private Dictionary<int, OverTimeEffect> OverTimeEffectDic = new Dictionary<int, OverTimeEffect>();
        private static bool runtimeInited = false;
        private OverTimeEffect nullPlaceHolder = null;

        public override string DataName(){return "OverTimeEffect Data";}
        public override string TypeString() { return "SoftKitty.OverTimeEffectObject"; }
        /// <summary>
        /// Retrieve the instance of the OverTimeEffectObject instance assigned in SoftKitty Data Settings.
        /// </summary>
        public static OverTimeEffectObject instance
        {
            get
            {
                return SGD_Settings.Instance.GetData<OverTimeEffectObject>();
            }
        }
        #endregion


        #region Internal Methods
        public override string GetDataJson()
        {
            string json = "";
            for (int i = 0; i < overTimeEffects.Count; i++)
            {
                OverTimeEffect _temp = overTimeEffects[i].Copy();
                _temp.fold = false;
                json += JsonUtility.ToJson(_temp);
            }
            return json;
        }

        public override int GetDataCount()
        {
            return overTimeEffects.Count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            runtimeInited = false;
        }
        private void Init()
        {
            OverTimeEffectDic.Clear();
            foreach (var obj in overTimeEffects)
            {
                if (!OverTimeEffectDic.ContainsKey(obj.id))
                    OverTimeEffectDic.Add(obj.id, obj);
                else
                    Debug.LogError("Duplicated script id for OverTimeEffect:" + obj.id);
            }
            runtimeInited = true;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Retrieves an over-time effect by its unique string UID. This allows for more specific lookups using the UID reference.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public OverTimeEffect GetOverTimeEffect(string _uid)
        {
            return GetOverTimeEffect(IdManager.GetId(_uid));
        }

        /// <summary>
        /// Retrieves an over-time effect by its integer ID. This method is useful for fast lookups based on the internal ID.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public OverTimeEffect GetOverTimeEffect(int _id)
        {
#if UNITY_EDITOR 
            if (!Application.isPlaying)
            {
                foreach (var obj in overTimeEffects)
                {
                    if (obj.uid == IdManager.GetStringKey(_id)) return obj;
                }
                if (nullPlaceHolder == null) nullPlaceHolder= new OverTimeEffect() { name = "No such OverTimeEffect{ id:" + _id + "}", uid = IdManager.GetStringKey(_id) };
                return nullPlaceHolder;
            }
#endif
            if (!runtimeInited || OverTimeEffectDic.Count == 0) Init();
            if (OverTimeEffectDic.ContainsKey(_id))
            {
                return OverTimeEffectDic[_id];
            }
            else
            {
                if (nullPlaceHolder == null) nullPlaceHolder = new OverTimeEffect() { name = "No such OverTimeEffect{ id:" + _id + "}", uid = IdManager.GetStringKey(_id) };
                return nullPlaceHolder;
            }
        }


        /// <summary>
        /// Convert attribute int id to string uid.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public string GetStringKey(int _id)
        {
            return IdManager.GetStringKey(_id);
        }
        /// <summary>
        /// Convert attribute string uid to int id.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public int GetId(string _uid)
        {
            return IdManager.GetId(_uid);
        }
        #endregion
    }
}
