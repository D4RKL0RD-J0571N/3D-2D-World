using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace SoftKitty
{
    /// <summary>
    /// Entity Data Collection, used for json serialization
    /// </summary>
    [System.Serializable]
    public class EntityCollection
    {
        public List<Entity> EntityList = new List<Entity>();
        public string ExtraInfo = "";
        public int Count
        {
            get
            {
                return EntityList.Count;
            }
        }
        public EntityCollection()
        {
            EntityList = new List<Entity>();
            ExtraInfo = "";
        }
    }

    /// <summary>
    ///  Manages and handles all entities in the system, providing easy access and modification.
    ///  This object can be created through the Unity Editor by right-clicking in the Project Panel, then selecting:
    ///  Create > SoftKitty > Data Objects > Entity Manager Data
    /// </summary>
    [CreateAssetMenu(fileName = "EntityManagerObject", menuName = "Soft Kitty/Data Objects/Entity Manager Data")]
    public class EntityManagerObject : DataObject
    {
        #region Variables
        public override string DataName() { return "Entity Manager Data"; }
        public override string TypeString() { return "SoftKitty.EntityManagerObject"; }
        /// <summary>
        /// The collection of entity data, holding all the registered entities.
        /// </summary>
        public EntityCollection EntityData = new EntityCollection();
        public StringIdManager IdManager=new StringIdManager();
        public List<CustomData> CustomDataSetting = new List<CustomData>();
        public int CustomFloat = 0;
        public int CustomInt = 0;
        public int CustomBool = 0;
        public int CustomString = 0;
        public int CustomIntList = 0;
        public int CustomIdIntList = 0;
        public int CustomIdFloatList = 0;
        public string SearchText = "";
        public int SearchType = 0;
        public List<string> SearchOptions = new List<string>();
        public string SearchOptionHash = "";
        public bool CustomFold = false;
        public bool ToolFold = false;
        private Dictionary<string, int> CustomDataDic = new Dictionary<string, int>();
        private Dictionary<int, Entity> EntityDataDic = new Dictionary<int, Entity>();
        private static bool runtimeInited = false;
        private List<string> UidList=new List<string>();
        private string oldListHash = "";
        /// <summary>
        /// Retrieve the instance of the EntityManagerObject instance assigned in SoftKitty Data Settings.
        /// </summary>
        public static EntityManagerObject instance
        {
            get
            {
                return SGD_Settings.Instance.GetData<EntityManagerObject>();
            }
        }
        #endregion

        #region Internal Methods
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            runtimeInited = false;
        }

        public override string GetDataJson()
        {
            string json = "";
            for (int i = 0; i < EntityData.EntityList.Count; i++)
            {
                Entity _temp = EntityData.EntityList[i].Copy();
                _temp.uiFold = false;
                _temp.attFold = false;
                _temp.customFold = false;
                json += JsonUtility.ToJson(_temp);
            }
            return json;
        }

        public override int GetDataCount()
        {
            return EntityData.EntityList.Count;
        }

        public void AddCustomData(string _uid, CustomData.CustomTypes _type)
        {
            switch (_type)
            {
                case CustomData.CustomTypes.Float:
                    CustomDataSetting.Add(new CustomData(CustomData.CustomTypes.Float, _uid, CustomFloat));
                    CustomFloat++;
                    foreach (var _entity in EntityData.EntityList) _entity.CustomFloat.Add(0F);
                    break;
                case CustomData.CustomTypes.Int:
                    CustomDataSetting.Add(new CustomData(CustomData.CustomTypes.Int, _uid, CustomInt));
                    CustomInt++;
                    foreach (var _entity in EntityData.EntityList) _entity.CustomInt.Add(0);
                    break;
                case CustomData.CustomTypes.Bool:
                    CustomDataSetting.Add(new CustomData(CustomData.CustomTypes.Bool, _uid, CustomBool));
                    CustomBool++;
                    foreach (var _entity in EntityData.EntityList) _entity.CustomBool.Add(false);
                    break;
                case CustomData.CustomTypes.String:
                    CustomDataSetting.Add(new CustomData(CustomData.CustomTypes.String, _uid, CustomString));
                    CustomString++;
                    foreach (var _entity in EntityData.EntityList) _entity.CustomString.Add("");
                    break;
                case CustomData.CustomTypes.IntList:
                    CustomDataSetting.Add(new CustomData(CustomData.CustomTypes.IntList, _uid, CustomIntList));
                    CustomIntList++;
                    foreach (var _entity in EntityData.EntityList) _entity.CustomIntList.Add(new IntList());
                    break;
                case CustomData.CustomTypes.IdIntList:
                    CustomDataSetting.Add(new CustomData(CustomData.CustomTypes.IdIntList, _uid, CustomIdIntList));
                    CustomIdIntList++;
                    foreach (var _entity in EntityData.EntityList) _entity.CustomIdIntList.Add(new IdIntList());
                    break;
                case CustomData.CustomTypes.IdFloatList:
                    CustomDataSetting.Add(new CustomData(CustomData.CustomTypes.IdFloatList, _uid, CustomIdFloatList));
                    CustomIdFloatList++;
                    foreach (var _entity in EntityData.EntityList) _entity.CustomIdFloatList.Add(new IdFloatList());
                    break;
            }
        }




        public void RemoveCustomData(string _uid)
        {
            for (int i = 0; i < CustomDataSetting.Count; i++)
            {
                if (CustomDataSetting[i].uid == _uid)
                {
                    switch (CustomDataSetting[i].type)
                    {
                        case CustomData.CustomTypes.Bool:
                            CustomBool--;
                            foreach (var _entity in EntityData.EntityList)
                            {
                                if (_entity.CustomBool.Count > CustomDataSetting[i].index) _entity.CustomBool.RemoveAt(CustomDataSetting[i].index);
                            }
                            break;
                        case CustomData.CustomTypes.Float:
                            CustomFloat--;
                            foreach (var _entity in EntityData.EntityList)
                            {
                                if (_entity.CustomFloat.Count > CustomDataSetting[i].index) _entity.CustomFloat.RemoveAt(CustomDataSetting[i].index);
                            }
                            break;
                        case CustomData.CustomTypes.IdFloatList:
                            CustomIdFloatList--;
                            foreach (var _entity in EntityData.EntityList)
                            {
                                if (_entity.CustomIdFloatList.Count > CustomDataSetting[i].index) _entity.CustomIdFloatList.RemoveAt(CustomDataSetting[i].index);
                            }
                            break;
                        case CustomData.CustomTypes.IdIntList:
                            CustomIdIntList--;
                            foreach (var _entity in EntityData.EntityList)
                            {
                                if (_entity.CustomIdIntList.Count > CustomDataSetting[i].index) _entity.CustomIdIntList.RemoveAt(CustomDataSetting[i].index);
                            }
                            break;
                        case CustomData.CustomTypes.Int:
                            CustomInt--;
                            foreach (var _entity in EntityData.EntityList)
                            {
                                if (_entity.CustomInt.Count > CustomDataSetting[i].index) _entity.CustomInt.RemoveAt(CustomDataSetting[i].index);
                            }
                            break;
                        case CustomData.CustomTypes.IntList:
                            CustomIntList--;
                            foreach (var _entity in EntityData.EntityList)
                            {
                                if (_entity.CustomIntList.Count > CustomDataSetting[i].index) _entity.CustomIntList.RemoveAt(CustomDataSetting[i].index);
                            }
                            break;
                        case CustomData.CustomTypes.String:
                            CustomString--;
                            foreach (var _entity in EntityData.EntityList)
                            {
                                if (_entity.CustomString.Count > CustomDataSetting[i].index) _entity.CustomString.RemoveAt(CustomDataSetting[i].index);
                            }
                            break;
                    }
                    CustomDataSetting.RemoveAt(i);
                    return;
                }
            }
        }
        #endregion


        #region Methods

        /// <summary>
        /// Returns a list of custom data UIDs for the entities.
        /// </summary>
        public string[] CustomDataUidArray
        {
            get
            {
                string[] _results = new string[CustomDataSetting.Count];
                for (int i = 0; i < CustomDataSetting.Count; i++) _results[i] = CustomDataSetting[i].uid;
                return _results;
            }
        }

        /// <summary>
        /// A list of all entity UIDs.
        /// </summary>
        public List<string> EntityUidList
        {
            get
            {
                if (UidList.Count < EntityData.EntityList.Count || oldListHash != Hash)
                {
                    UidList.Clear();
                    for (int i = 0; i < EntityData.EntityList.Count; i++)
                    {
                        UidList.Add(EntityData.EntityList[i].uid);
                    }
                    oldListHash = Hash;
                }
                return UidList;
            }
        }

        /// <summary>
        /// Converts all entities (along with any extra info) into a JSON string for saving. This is useful for saving and loading entity data.
        /// </summary>
        /// <param name="_extraInfo"></param>
        /// <returns></returns>
        public string ToJson(string _extraInfo)
        {
            if (SGD_Settings.isRuntime)
            {
                EntityCollection _data = new EntityCollection();
                foreach (var obj in EntityDataDic.Values) {
                    _data.EntityList.Add(obj.ToJson());
                }
                _data.ExtraInfo = _extraInfo;
                return JsonUtility.ToJson(_data);
            }
            else
            {
                EntityData.ExtraInfo = _extraInfo;
                return JsonUtility.ToJson(EntityData);
            }
        }

        /// <summary>
        /// Returns the JSON string for a specific entity by its UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public string SingleEntityToJson(string _uid)
        {
            if (GetEntity(_uid) == null)
            {
                if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<EntityManagerObject> ToJson() > Invalid file uid.");
                return "";
            }
            return JsonUtility.ToJson(GetEntity(_uid).ToJson());
        }

        /// <summary>
        /// Loads entity data from a JSON string. The method also returns the extra info from the saved file.
        /// </summary>
        /// <param name="_json"></param>
        /// <returns></returns>
        public string FromJson(string _json)
        {
            if (SGD_Settings.isRuntime)
            {
                EntityCollection _data = (EntityCollection)JsonUtility.FromJson(_json, typeof(EntityCollection));
                foreach (var obj in _data.EntityList) {
                    int _id = GetId(obj.uid);
                    if (EntityDataDic.ContainsKey(_id))
                    {
                        EntityDataDic[_id] = obj.FromJson();
                    }
                    else
                    {
                        EntityDataDic.Add(_id, obj.FromJson());
                    }
                }
                return _data.ExtraInfo;
            }
            else
            {
                EntityData = (EntityCollection)JsonUtility.FromJson(_json, typeof(EntityCollection));
                return EntityData.ExtraInfo;
            }
        }
        /// <summary>
        /// Loads specific entity data from its UID and a JSON string.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_json"></param>
        public void SingleEntityFromJson(string _uid, string _json)
        {
            if (GetEntity(_uid) == null)
            {
                if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<EntityManagerObject> ToJson() > Invalid file uid.");
                return;
            }
            GetEntity(_uid).CopyFrom(((Entity)JsonUtility.FromJson(_json, typeof(Entity))).FromJson());
        }
        /// <summary>
        /// Loads entity data from a file at the provided path and returns the extra info from the save.
        /// </summary>
        /// <param name="_path"></param>
        /// <returns></returns>
        public string Load(string _path)
        {
            if (SGD_Settings.isRuntime) RuntimeInit();
            string _extraInfo = "";
            if (System.IO.File.Exists(_path))
            {
                string _json = System.IO.File.ReadAllText(_path, System.Text.Encoding.UTF8);
                _extraInfo = FromJson(_json);
            }
            else
            {
                if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<EntityManagerObject> Load() > Invalid file path.");
            }
            if (SGD_Settings.isRuntime)
            {
                foreach (var obj in GameManager.GetEntityInstanceList())
                {
                    if (obj != null)
                    {
                        obj.RefreshData();
                        obj.ApplyData();
                    }
                }
                GameManager.RefreshCallback();
            }
            return _extraInfo;
        }
        /// <summary>
        /// Loads data for a single entity (by UID) from the provided path.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_path"></param>
        public void LoadSingleEntity(string _uid, string _path)
        {
            if (System.IO.File.Exists(_path))
            {
                string _json = System.IO.File.ReadAllText(_path, System.Text.Encoding.UTF8);
                SingleEntityFromJson(_uid, _json);
            }
            else
            {
                if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<EntityManagerObject> Load() > Invalid file path.");
            }
        }

        /// <summary>
        /// Saves all entities¡¯ data to a file at the provided path, along with any extra info in JSON format.
        /// </summary>
        /// <param name="_path"></param>
        /// <param name="_extraInfo"></param>
        public void Save(string _path, string _extraInfo="")
        {
            System.IO.File.WriteAllText(_path, ToJson(_extraInfo), System.Text.Encoding.UTF8);
        }
        /// <summary>
        /// Saves data for a single entity (identified by UID) to the provided path.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_path"></param>
        public void SaveSingleEntity(string _uid, string _path)
        {
            System.IO.File.WriteAllText(_path, SingleEntityToJson(_uid), System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Retrieves the list of all Entities.
        /// </summary>
        public List<Entity> EntityList
        {
            get
            {
                if (SGD_Settings.isRuntime)
                {
                    return EntityDataDic.Values.ToList();
                }
                else
                {
                    return EntityData.EntityList;
                }
            }
        }

        /// <summary>
        /// Creates a new entity and returns it.
        /// </summary>
        /// <returns></returns>
        public Entity NewEntity()
        {
            return NewEntity("Entity" + EntityData.EntityList.Count.ToString());
        }

        /// <summary>
        /// Creates a new entity with the specified UID and returns it.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public Entity NewEntity(string _uid)
        {
            Entity _newEntity = new Entity();
            _newEntity.uid = _uid;
            _newEntity.Position = Vector3.zero;
            _newEntity.Forward = new Vector3(0F, 0F, 1F);
            _newEntity.Scale = Vector3.one;
            _newEntity.uiFold = true;
            _newEntity.Attributes = new List<AttributeData>();
            _newEntity.Tags = new List<string>();
            _newEntity.CustomFloat = new List<float>();
            for (int i = 0; i < CustomFloat; i++) _newEntity.CustomFloat.Add(0F);
            for (int i = 0; i < CustomInt; i++) _newEntity.CustomInt.Add(0);
            for (int i = 0; i < CustomBool; i++) _newEntity.CustomBool.Add(false);
            for (int i = 0; i < CustomString; i++) _newEntity.CustomString.Add("");
            for (int i = 0; i < CustomIntList; i++) _newEntity.CustomIntList.Add(new IntList());
            for (int i = 0; i < CustomIdIntList; i++) _newEntity.CustomIdIntList.Add(new IdIntList());
            for (int i = 0; i < CustomIdFloatList; i++) _newEntity.CustomIdFloatList.Add(new IdFloatList());
            EntityData.EntityList.Add(_newEntity);
            return _newEntity;
        }
        /// <summary>
        /// Retrieves an entity by its string UID. This method caches results for better performance.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public Entity GetEntity(string _uid)
        {
            if (!SGD_Settings.isRuntime)
            {
                foreach (var obj in EntityData.EntityList)
                {
                    if (obj.uid == _uid) return obj;
                }
            }
            else
            {
                return GetEntity(GetId(_uid));
            }
            return null;
        }
        /// <summary>
        /// Retrieves an entity by its integer ID. Results are cached for better performance.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public Entity GetEntity(int _id)
        {
            if (!SGD_Settings.isRuntime)
            {
                return GetEntity(GetStringKey(_id));
            }
            else
            {
                if (!runtimeInited || EntityDataDic.Keys.Count == 0) RuntimeInit();
                Entity _entity = null;
                EntityDataDic.TryGetValue(_id, out _entity);
                return _entity;
            }
        }


        /// <summary>
        /// Convert Entity int id to string uid.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public string GetStringKey(int _id)
        {
            return IdManager.GetStringKey(_id);
        }

        /// <summary>
        /// Convert Entity string uid to int id.
        /// </summary>
        /// <param name="_key"></param>
        /// <returns></returns>
        public int GetId(string _key)
        {
            return IdManager.GetId(_key);
        }

        /// <summary>
        /// Retrieves the index number of custom data by providing its UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public int GetCustomIndex(string _uid)
        {
            if (!SGD_Settings.isRuntime)
            {
                foreach (var obj in CustomDataSetting)
                {
                    if (obj.uid == _uid) return obj.index;
                }
                return -1;
            }
            else
            {
                if (!runtimeInited || CustomDataDic.Keys.Count==0) RuntimeInit();
                int _index = -1;
                CustomDataDic.TryGetValue(_uid, out _index);
                return _index;
            }
        }

        /// <summary>
        /// Gets the custom data type by providing the UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public CustomData.CustomTypes GetCustomType(string _uid)
        {
            foreach (var obj in CustomDataSetting)
            {
                if (obj.uid == _uid) return obj.type;
            }
            return CustomData.CustomTypes.Float;
        }

        /// <summary>
        /// Retrieves the UID of custom data based on its type and index.
        /// </summary>
        /// <param name="_type"></param>
        /// <param name="_index"></param>
        /// <returns></returns>
        public string GetCustomUid(CustomData.CustomTypes _type, int _index)
        {
            foreach (var obj in CustomDataSetting) {
                if (obj.type == _type && obj.index == _index) return obj.uid;
            }
            return "Invalid";
        }


        /// <summary>
        /// Initialize the data fore runtime. Do not manually call it unless you want to refresh the database in memory.
        /// </summary>
        public void RuntimeInit()
        {
            EntityDataDic.Clear();
            foreach (var obj in EntityData.EntityList) {
                if (!EntityDataDic.ContainsKey(GetId(obj.uid)))
                {
                    var _entity = obj.Copy();
                    foreach (var att in _entity.Attributes) {
                        if(att!=null)att.Randomize();
                    }
                    foreach (var module in _entity.Modules) {
                        module.Recreate();
                        module.GetModule().RuntimeInit();
                    }
                   
                    EntityDataDic.Add(GetId(obj.uid), _entity);
                }
            }
            
            CustomDataDic.Clear();
            foreach (var obj in CustomDataSetting)
            {
                if (!CustomDataDic.ContainsKey(obj.uid)) CustomDataDic.Add(obj.uid,obj.index);
            }
            runtimeInited = true;
            if (SGD_Settings.isRuntime)
            {
                foreach (var obj in GameManager.GetEntityInstanceList())
                {
                    if (obj != null)
                    {
                        obj.RefreshData();
                        obj.ApplyData();
                    }
                }
                GameManager.RefreshCallback();
            }
           
        }

        #endregion

    }
}
