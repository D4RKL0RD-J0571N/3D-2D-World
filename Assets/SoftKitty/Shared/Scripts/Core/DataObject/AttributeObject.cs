using UnityEngine;
using System.Collections.Generic;


namespace SoftKitty
{
    /// <summary>
    /// AttributeObject is a sub-data object that manages the attribute settings within the system. This object can be created through the Unity Editor by right-clicking in the Project Panel, then selecting:
    /// Create > SoftKitty > Data Objects > Attribute Data.
    /// </summary>
    [CreateAssetMenu(fileName = "AttributeObject", menuName = "Soft Kitty/Data Objects/Attribute Data")]
    public class AttributeObject : DataObject
    {
        #region Variables
        public List<Attribute> AttributeList = new List<Attribute>();
        public override string DataName() { return "Attribute Data"; }
        public override string TypeString() { return "SoftKitty.AttributeObject"; }
        public StringIdManager IdManager=new StringIdManager();
        private Dictionary<int, Attribute> AttributeDic = new Dictionary<int, Attribute>();
        private static bool runtimeInited = false;
        private Attribute nullPlaceHolder=null;
        /// <summary>
        /// Retrieve the instance of the AttributeObject instance assigned in SoftKitty Data Settings.
        /// </summary>
        public static AttributeObject instance
        {
            get
            {
                return SGD_Settings.Instance.GetData<AttributeObject>();
            }
        }
        #endregion

        #region Internal Methods
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            runtimeInited = false;
        }
        private void Init()
        {
            AttributeDic.Clear();
            foreach (var obj in AttributeList)
            {
                if (!AttributeDic.ContainsKey(obj.id))
                    AttributeDic.Add(obj.id, obj);
                else
                    Debug.LogError("Duplicated id for attributes:" + obj.id);
            }
            runtimeInited = true;
        }
        public int IndexOfAttributes(string _uid)
        {
            for (int i = 0; i < AttributeList.Count; i++)
            {
                if (_uid == AttributeList[i].uid) return i;
            }
            return -1;
        }

        public override string GetDataJson()
        {
            string json = "";
            for (int i = 0; i < AttributeList.Count; i++)
            {
                Attribute _temp = AttributeList[i].Copy();
                _temp.fold = false;
                json += JsonUtility.ToJson(_temp);
            }
            return json;
        }

        public override int GetDataCount()
        {
            return AttributeList.Count;
        }
        #endregion


        #region Methods
        /// <summary>
        /// An array of attribute names. This provides a simple list of the attribute names for quick reference (e.g., "Health", "Attack", etc.).
        /// </summary>
        public string[] AttributesNames
        {
            get
            {
                string[] _names = new string[AttributeList.Count];
                for (int i=0;i< _names.Length;i++) {
                    _names[i] = AttributeList[i].name;
                }
                return _names;
            }
        }

        /// <summary>
        /// An array of attribute UIDs. This contains the unique identifiers for each attribute, allowing for direct reference by UID.
        /// </summary>
        public string[] AttributesUidArray
        {
            get
            {
                string[] _names = new string[AttributeList.Count];
                for (int i = 0; i < _names.Length; i++)
                {
                    _names[i] = AttributeList[i].uid;
                }
                return _names;
            }
        }

        /// <summary>
        /// Retrieves the attribute setting based on its unique string UID. This is used for fetching specific attribute data efficiently.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public Attribute GetAttribute(string _uid)
        {
            return GetAttribute(IdManager.GetId(_uid));
        }

        /// <summary>
        /// Retrieves the attribute setting based on its integer ID. This method allows for fetching by index for faster lookups in certain use cases.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public Attribute GetAttribute(int _id)
        {

            if (!SGD_Settings.isRuntime)
            {
                foreach (var obj in AttributeList)
                {
                    if (obj.uid == IdManager.GetStringKey(_id))return obj;
                }
                if (nullPlaceHolder == null) nullPlaceHolder= new Attribute() { name = "Invalid{" + _id + "}", uid = IdManager.GetStringKey(_id) };
                return nullPlaceHolder;
            }

            if (!runtimeInited || AttributeDic.Count == 0) Init();
            if (AttributeDic.ContainsKey(_id))
            {
                return AttributeDic[_id];
            } else {
                if (nullPlaceHolder == null)  nullPlaceHolder= new Attribute() { name = "Invalid{" + _id + "}", uid = IdManager.GetStringKey(_id) };
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
