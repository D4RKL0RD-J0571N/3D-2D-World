using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace SoftKitty
{
    public delegate void EntityEvent(string _eventName, float _floatArg, Entity _entityArg, bool _boolArg);
    public delegate void OverTimeEffectChangeEvent(string _uid, OverTimeEffectData _data, OverTimeEffectEventType _eventType);
    public delegate void OverTimeEffectTickEvent(string _uid, int _layer, Entity _dealer, Entity _target);

    #region EntityModule
    [System.Serializable]
    public abstract class EntityModule
    {
        [SerializeReference]
        protected Entity entity;
        public void Init(Entity _entity)
        {
            entity = _entity;
        }

        /// <summary>
        /// Initialize the Module when runtime starts.
        /// </summary>
        public virtual void RuntimeInit()
        {

        }

        /// <summary>
        /// Additional attributes this Module provides. Requires integer attribute _id.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public virtual float GetAttributeValue(int _id)
        {
            return 0F;
        }

        /// <summary>
        /// Additional attributes this Module provides.Requires string attribute _uid.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public virtual float GetAttributeValue(string _uid)
        {
            return 0F;
        }

        /// <summary>
        /// Clone the Module class.
        /// </summary>
        /// <returns></returns>
        public abstract EntityModule Copy();

        /// <summary>
        /// Convert the data of the Module into a json string.
        /// </summary>
        /// <returns></returns>
        public abstract string ToJson();

        /// <summary>
        /// Convert json string to the data of the Module.
        /// </summary>
        /// <returns></returns>
        public abstract EntityModule FromJson(string _json);

        /// <summary>
        /// Compress full data into a compact format.
        /// </summary>
        public abstract string CompressData();

        /// <summary>
        /// Uncompress data from a compact format.
        /// </summary>
        /// <param name="_compactJson"></param>
        public abstract string UncompressData(string _compactJson);

    };

    [System.Serializable]
    public class EntityModuleWrapper
    {
        public string type;
        public string jsonData;

        [System.NonSerialized]
        private EntityModule runtimeModule=null;

        public EntityModule RuntimeModule
        {
            get
            {
                return runtimeModule;
            }
        }
        public void Recreate()
        {
            runtimeModule = null;
        }
        public EntityModule GetModule()
        {
            if(runtimeModule != null)return runtimeModule;
            var t = ResolveType(type);
            if (t == null) return null;
            runtimeModule = (EntityModule)System.Activator.CreateInstance(t);
            runtimeModule= runtimeModule.FromJson(jsonData);
            return runtimeModule;
        }

        public void Save()
        {
            if (SGD_Settings.isRuntime) return;
            if (runtimeModule != null)
            {
                jsonData = runtimeModule.ToJson();
            }
        }

        public void RuntimeCopy(EntityModule _module)
        {
            runtimeModule = _module.Copy();
        }

        public void Compress()
        {
            jsonData = GetModule().CompressData();
        }

        public void Uncompress()
        {
            jsonData= GetModule().UncompressData(jsonData);
        }

        public static System.Type ResolveType(string typeName)
        {
            var t = System.Type.GetType(typeName);
            if (t != null) return t;

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(typeName);
                if (t != null)
                    return t;
            }

            return null;
        }

        public EntityModuleWrapper(EntityModule _module)
        {
            type = _module.GetType().FullName;
            jsonData = _module.ToJson();
            runtimeModule = _module;
        }
        public EntityModuleWrapper(){}


        public EntityModuleWrapper Copy()
        {
            EntityModuleWrapper _copy = new EntityModuleWrapper();
            _copy.type = type;
            _copy.jsonData = jsonData;
            return _copy;
        }
    }

    #endregion
    /// <summary>
    /// Entity represents a data class for an entity in the system. This could be anything from a character to an interactive object in the world, holding information such as its attributes, position, and tags.
    /// </summary>
    [System.Serializable]
    public class Entity : ISerializationCallbackReceiver
    {
        #region Variables
        /// <summary>
        /// The unique string ID for this entity.
        /// </summary>
        public string uid;
        /// <summary>
        /// The unique integer ID for this entity.
        /// </summary>
        public int id
        {
            get
            {
                return GameManager.EntityManagerData.GetId(uid);
            }
        }
        /// <summary>
        /// A list of attribute data associated with this entity.
        /// </summary>
        public List<AttributeData> Attributes = new List<AttributeData>();
        /// <summary>
        /// The level of attribute upgrades (e.g., character level). This is tied to the upgradeIncrement of attributes.
        /// </summary>
        public int AttributesUpgradeLevel = 0;
        /// <summary>
        /// Whether to apply transform data from database to the instantiated entity instance.
        /// </summary>
        public bool ApplyTransformDataWhenInstantiate = true;
        /// <summary>
        /// The position of the entity in the world space.
        /// </summary>
        public Vector3 Position = new Vector3(0F, 0F, 0F);
        /// <summary>
        /// The forward direction of the entity. Used instead of rotation since, in many calculations, only the forward direction matters. The up direction can be inferred from the forward vector.
        /// </summary>
        public Vector3 Forward = new Vector3(0F, 0F, 1F);
        /// <summary>
        /// The scale of the entity.
        /// </summary>
        public Vector3 Scale = new Vector3(1F, 1F, 1F);
        /// <summary>
        /// A list of tags associated with the entity.
        /// </summary>
        public List<string> Tags = new List<string>();



        /// <summary>
        /// Determines whether this entity can be interacted with or controlled. When set to false, all calculations and controls stop, such as for a dead character.
        /// </summary>
        public bool AvailableForInteraction = true;
        /// <summary>
        /// A 'MultipleInstances' Entity will no longer be one to one connection with EntityComponent, it will allow multiple EntityComponents inherit the data instead, but they don't write data back, think of the Entity as prefab,EntityComponent as instantiation. 
        /// </summary>
        public bool MultipleInstances = false;
        /// ======Custom Data
        public List<float> CustomFloat = new List<float>();
        public List<int> CustomInt = new List<int>();
        public List<bool> CustomBool = new List<bool>();
        public List<string> CustomString = new List<string>();
        public List<IntList> CustomIntList = new List<IntList>();
        public List<IdIntList> CustomIdIntList = new List<IdIntList>();
        public List<IdFloatList> CustomIdFloatList = new List<IdFloatList>();
        /// ======Custom Data

      

        ///======Editor UI
        [HideInInspector,System.NonSerialized]
        public bool uiFold = false;
        [HideInInspector, System.NonSerialized]
        public bool attFold = false;
        [HideInInspector, System.NonSerialized]
        public bool customFold = false;
        ///======Editor UI

        private Dictionary<int, OverTimeEffectData> mOverTimeEffect = new Dictionary<int, OverTimeEffectData>();
        private Dictionary<int, Dictionary<string, TempAttribute>> TempAttributes = new Dictionary<int, Dictionary<string, TempAttribute>>();
        private Dictionary<int, AttributeData> attributeDic = new Dictionary<int, AttributeData>();
        private EntityEvent EntityEventCallback;
        private AttributeChangeEvent AttributeChangeCallback;
        private OverTimeEffectChangeEvent OverTimeEffectChangeCallback;
        private static OverTimeEffectTickEvent OverTimeEffectTickCallback;
        private AttributeData nullPlaceHolder = null;
        private bool runtimeInited = false;
        #endregion

 
        #region Extensions

        public List<EntityModuleWrapper> Modules = new List<EntityModuleWrapper>();
        private Dictionary<System.Type, EntityModuleWrapper> moduleCache;
        public void OnAfterDeserialize()
        {
            //BuildCache();
        }

        public void OnBeforeSerialize() {}

        private void BuildCache()
        {
            moduleCache = new Dictionary<System.Type, EntityModuleWrapper>();
            foreach (var wrapper in Modules)
            {
                wrapper.Recreate();
                var type = EntityModuleWrapper.ResolveType(wrapper.type);
                if (type == null) continue;
                moduleCache[type] = wrapper;
                if (SGD_Settings.isRuntime) wrapper.GetModule().RuntimeInit();
            }
        }

        /// <summary>
        /// Retrieve an extension Module by its type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModule<T>() where T : EntityModule
        {
            if (moduleCache == null)BuildCache();
            moduleCache.TryGetValue(typeof(T), out var wrapper);
            if (wrapper != null)
            {
                return wrapper.GetModule() as T;
            }
            return null;
        }

        /// <summary>
        /// Add an extension Module.
        /// </summary>
        /// <param name="module"></param>
        public void AddModule(EntityModule _module)
        {
            if (moduleCache == null)
                BuildCache();
            var type = _module.GetType();
            if (moduleCache.ContainsKey(type))return;
            var typeString = _module.GetType().FullName;
            foreach (var obj in Modules)
            {
                if (obj.type == typeString)
                {
                    return;
                }
            }
            var _wrapper = new EntityModuleWrapper(_module);
            Modules.Add(_wrapper);
            moduleCache[type] = _wrapper;
        }

        /// <summary>
        /// Remove an extension Module by its type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void RemoveModule<T>() where T : EntityModule
        {
            if (moduleCache == null)
                BuildCache();

            if (moduleCache.TryGetValue(typeof(T), out var module))
            {
                Modules.Remove(module);
                moduleCache.Remove(typeof(T));
            }
        }

        /// <summary>
        /// Verify whether a Module already exists via its type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool HasModule<T>() where T : EntityModule
        {
            return Modules.Any(m => m is T);
        }
        #endregion


        #region Internal Methods
        public void OnAttributeChange(string _uid, float _value, Entity _dealer)
        {
            if (AttributeChangeCallback != null) AttributeChangeCallback(_uid, _value, _dealer);
        }
        public void OnEntityEvent(string _eventName, float _floatArg, Entity _entityArg, bool _boolArg)
        {
            if (EntityEventCallback != null) EntityEventCallback(_eventName, _floatArg, _entityArg, _boolArg);
        }
        public void OnOverTimeChange(string _uid, OverTimeEffectData _data, OverTimeEffectEventType _eventType)
        {
            if (OverTimeEffectChangeCallback != null) OverTimeEffectChangeCallback(_uid, _data, _eventType);
        }
        private void Init()
        {
            attributeDic.Clear();
            foreach (var obj in Attributes)
            {
                obj.RegisterCallback(OnAttributeChange);
                if (!attributeDic.ContainsKey(obj.id))
                    attributeDic.Add(obj.id, obj);
                else if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("Duplicated id for attribute values:" + obj.id);
            }
            foreach (var obj in Modules)
            {
                obj.GetModule().Init(this);
            }
            runtimeInited = true;
        }
        #endregion



        public Entity ToJson()
        {
            Entity _copy = Copy();
            foreach (var obj in _copy.Modules) {
                obj.Compress();
            }
            return _copy;
        }

        public Entity FromJson()
        {
            Entity _copy = Copy();

            foreach (var obj in _copy.Modules)
            {
                obj.Recreate();
                obj.Uncompress();
                obj.Recreate();
            }
            return _copy;
        }

        /// <summary>
        /// Retrieves a copy of the data.
        /// </summary>
        /// <returns></returns>
        public Entity Copy()
        {
            Entity _copy = new Entity();
            _copy.uid = uid;
            _copy.AttributesUpgradeLevel = AttributesUpgradeLevel;
            _copy.Position = Position;
            _copy.Forward = Forward;
            _copy.Scale = Scale;
            _copy.AvailableForInteraction = AvailableForInteraction;
            _copy.MultipleInstances = MultipleInstances;
            _copy.Tags = new List<string>();
            _copy.Tags.AddRange(Tags);
            _copy.Attributes = new List<AttributeData>();
            
            for (int i = 0; i < Attributes.Count; i++)
            {
                _copy.Attributes.Add(Attributes[i].Copy());
            }
            _copy.Modules.Clear();
            for (int i = 0; i < Modules.Count; i++) {
                _copy.Modules.Add(Modules[i].Copy());
                if (SGD_Settings.isRuntime)
                {
                    _copy.Modules[i].RuntimeCopy(Modules[i].GetModule());
                }
            }
            _copy.CustomFloat = new List<float>();
            for (int i = 0; i < CustomFloat.Count; i++)
            {
                _copy.CustomFloat.Add(CustomFloat[i]);
            }
            _copy.CustomInt = new List<int>();
            for (int i = 0; i < CustomInt.Count; i++)
            {
                _copy.CustomInt.Add(CustomInt[i]);
            }
            _copy.CustomBool = new List<bool>();
            for (int i = 0; i < CustomBool.Count; i++)
            {
                _copy.CustomBool.Add(CustomBool[i]);
            }
            _copy.CustomString = new List<string>();
            for (int i = 0; i < CustomString.Count; i++)
            {
                _copy.CustomString.Add(CustomString[i]);
            }
            _copy.CustomIntList = new List<IntList>();
            for (int i = 0; i < CustomIntList.Count; i++)
            {
                _copy.CustomIntList.Add(CustomIntList[i].Copy());
            }
            _copy.CustomIdIntList = new List<IdIntList>();
            for (int i = 0; i < CustomIdIntList.Count; i++)
            {
                _copy.CustomIdIntList.Add(CustomIdIntList[i].Copy());
            }
            _copy.CustomIdFloatList = new List<IdFloatList>();
            for (int i = 0; i < CustomIdFloatList.Count; i++)
            {
                _copy.CustomIdFloatList.Add(CustomIdFloatList[i].Copy());
            }
            _copy.uiFold = uiFold;
            _copy.attFold = attFold;
            _copy.customFold = customFold;
            return _copy;
        }

        /// <summary>
        /// Set the data from a copy of another Entity
        /// </summary>
        /// <param name="_source"></param>
        public void CopyFrom(Entity _source)
        {
            uid = _source.uid;
            AttributesUpgradeLevel = _source.AttributesUpgradeLevel;
            Position = _source.Position;
            Forward = _source.Forward;
            Scale = _source.Scale;
            AvailableForInteraction = _source.AvailableForInteraction;
            Tags = new List<string>();
            Tags.AddRange(_source.Tags);
            Modules.Clear();
            for (int i = 0; i < _source.Modules.Count; i++)
            {
                Modules.Add(_source.Modules[i].Copy());
            }
            Attributes = new List<AttributeData>();
            for (int i = 0; i < _source.Attributes.Count; i++)
            {
                Attributes.Add(_source.Attributes[i].Copy());
            }
            CustomFloat = new List<float>();
            for (int i = 0; i < _source.CustomFloat.Count; i++)
            {
                CustomFloat.Add(_source.CustomFloat[i]);
            }
            CustomInt = new List<int>();
            for (int i = 0; i < _source.CustomInt.Count; i++)
            {
                CustomInt.Add(_source.CustomInt[i]);
            }
            CustomBool = new List<bool>();
            for (int i = 0; i < _source.CustomBool.Count; i++)
            {
                CustomBool.Add(_source.CustomBool[i]);
            }
            CustomString = new List<string>();
            for (int i = 0; i < _source.CustomString.Count; i++)
            {
                CustomString.Add(_source.CustomString[i]);
            }
            CustomIntList = new List<IntList>();
            for (int i = 0; i < _source.CustomIntList.Count; i++)
            {
                CustomIntList.Add(_source.CustomIntList[i].Copy());
            }
            CustomIdIntList = new List<IdIntList>();
            for (int i = 0; i < _source.CustomIdIntList.Count; i++)
            {
                CustomIdIntList.Add(_source.CustomIdIntList[i].Copy());
            }
            CustomIdFloatList = new List<IdFloatList>();
            for (int i = 0; i < _source.CustomIdFloatList.Count; i++)
            {
                CustomIdFloatList.Add(_source.CustomIdFloatList[i].Copy());
            }
            uiFold = _source.uiFold;
            attFold = _source.attFold;
            customFold = _source.customFold;
        }

        /// <summary>
        /// Retrieves a custom float value by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public float GetCustomFloat(string _uid)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            return _index >= 0 && _index < CustomFloat.Count ? CustomFloat[_index] : 0F;
        }

        /// <summary>
        /// Retrieves a custom integer value by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public int GetCustomInt(string _uid)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            return _index >= 0 && _index < CustomInt.Count ? CustomInt[_index] : 0;
        }

        /// <summary>
        /// Retrieves a custom boolean value by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public bool GetCustomBool(string _uid)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            return _index >= 0 && _index < CustomBool.Count ? CustomBool[_index] : false;
        }

        /// <summary>
        /// Retrieves a custom string value by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public string GetCustomString(string _uid)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            return _index >= 0 && _index < CustomString.Count ? CustomString[_index] : "";
        }

        /// <summary>
        /// Retrieves a custom integer list by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public IntList GetCustomIntList(string _uid)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            return _index >= 0 && _index < CustomIntList.Count ? CustomIntList[_index] : new IntList();
        }
        /// <summary>
        /// Retrieves a custom ID-to-integer list by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public IdIntList GetCustomIdIntList(string _uid)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            return _index >= 0 && _index < CustomIdIntList.Count ? CustomIdIntList[_index] : new IdIntList();
        }
        /// <summary>
        /// Retrieves a custom ID-to-float list by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public IdFloatList GetCustomIdFloatList(string _uid)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            return _index >= 0 && _index < CustomIdFloatList.Count ? CustomIdFloatList[_index] : new IdFloatList();
        }
        /// <summary>
        /// Overrides the value for a custom float data by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetCustomFloat(string _uid, float _value)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            if (_index >= 0 && _index < CustomFloat.Count) CustomFloat[_index] = _value;
        }
        /// <summary>
        /// Overrides the value for a custom integer data by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetCustomInt(string _uid, int _value)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            if (_index >= 0 && _index < CustomInt.Count) CustomInt[_index] = _value;
        }
        /// <summary>
        /// Overrides the value for a custom boolean data by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetCustomBool(string _uid, bool _value)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            if (_index >= 0 && _index < CustomBool.Count) CustomBool[_index] = _value;
        }
        /// <summary>
        /// Overrides the value for a custom string data by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetCustomString(string _uid, string _value)
        {
            int _index = GameManager.EntityManagerData.GetCustomIndex(_uid);
            if (_index >= 0 && _index < CustomString.Count) CustomString[_index] = _value;
        }

        /// <summary>
        /// Returns whether this entity has a matching tag with the provided tag string.
        /// </summary>
        /// <param name="_filter"></param>
        /// <returns></returns>
        public bool hasMatchingTag(string _filter)
        {
            return Tags.Contains(_filter);
        }

        /// <summary>
        /// Returns whether this entity has any matching tag from the provided list of tag strings.
        /// </summary>
        /// <param name="_filters"></param>
        /// <returns></returns>
        public bool hasMatchingTag(List<string> _filters)
        {
            foreach (var obj in _filters)
            {
                if (hasMatchingTag(obj)) return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the entity's position. If _stickToGround is true, the position's Y-value will be automatically adjusted based on the ground height.
        /// </summary>
        /// <param name="_pos"></param>
        /// <param name="_stickToGround"></param>
        public void SetPosition(Vector3 _pos, bool _stickToGround)
        {
            if (_stickToGround)
            {
                RaycastHit hit;
                if (Physics.Raycast(Position, Vector3.down, out hit, 500f, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
                {
                    _pos.y = hit.point.y + 0.01f;
                }
            }
            Position = _pos;
        }

        /// <summary>
        /// Adds a temporary attribute to this entity based on its integer ID.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <param name="_duration"></param>
        /// <param name="_math"></param>
        /// <param name="_sourceUid"></param>
        public void AddTempAttributes(int _id, float _value, float _duration, BasicMathFunction _math, string _sourceUid)
        {
            if (!TempAttributes.ContainsKey(_id))
            {
                TempAttributes.Add(_id, new Dictionary<string, TempAttribute>());
            }
            if (!TempAttributes[_id].ContainsKey(_sourceUid))
            {
                TempAttributes[_id].Add(_sourceUid, new TempAttribute(_value, Time.time + _duration, _math));
            }
            else
            {
                TempAttributes[_id][_sourceUid] = new TempAttribute(_value, Mathf.Max(TempAttributes[_id][_sourceUid].timeStamp, Time.time + _duration), _math);
            }
        }

        /// <summary>
        /// Adds a temporary attribute to this entity based on its string UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <param name="_duration"></param>
        /// <param name="_math"></param>
        /// <param name="_sourceUid"></param>
        public void AddTempAttributes(string _uid, float _value, float _duration, BasicMathFunction _math, string _sourceUid)
        {
            AddTempAttributes(GameManager.AttributeData.GetId(_uid), _value, _duration, _math, _sourceUid);
        }

        /// <summary>
        /// Retrieves the value of an attribute with its integer ID by merging the base value of the time based temporary value.
        /// </summary>
        /// <param name="_base"></param>
        /// <param name="_id"></param>
        /// <returns></returns>
        public float GetTempAttributesMerge(float _base, int _id)
        {
            if (TempAttributes.ContainsKey(_id))
            {
                List<string> _uidKeys = new List<string>(TempAttributes[_id].Keys);
                float _value = _base;
                foreach (var _uid in _uidKeys)
                {
                    if (Time.time > TempAttributes[_id][_uid].timeStamp)
                    {
                        TempAttributes[_id].Remove(_uid);
                    }
                    else
                    {
                        _value = TempAttributes[_id][_uid].GetValue(_value);
                    }
                }
                if (TempAttributes[_id].Keys.Count <= 0) TempAttributes.Remove(_id);
                return _value;
            }
            else
            {
                return _base;
            }
        }
        /// <summary>
        /// Retrieves the value of an attribute with its string UID by merging the base value of the time based temporary value.
        /// </summary>
        /// <param name="_base"></param>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public float GetTempAttributesMerge(float _base, string _uid)
        {
            return GetTempAttributesMerge(_base, GameManager.AttributeData.GetId(_uid));
        }


        /// <summary>
        /// Retrieves a list of over-time effects applied to this entity.
        /// </summary>
        /// <returns></returns>
        public List<OverTimeEffectData> GetOverTimeEffectList()
        {
            List<OverTimeEffectData> _list = new List<OverTimeEffectData>();
            _list.AddRange(mOverTimeEffect.Values);
            return _list;
        }
        /// <summary>
        /// Retrieves the over-time effect data by its string UID if it exists.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public OverTimeEffectData GetOverTimeEffect(string _uid)
        {
            return GetOverTimeEffect(GameManager.OverTimeEffectData.GetId(_uid));
        }
        /// <summary>
        /// Retrieves the over-time effect data by its integer ID if it exists.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public OverTimeEffectData GetOverTimeEffect(int _id)
        {
            OverTimeEffectData _effect = null;
            mOverTimeEffect.TryGetValue(_id, out _effect);
            return _effect;
        }

        /// <summary>
        /// Adds an over-time effect by its integer ID and the dealer entity. Returns the OverTimeEffectData.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public OverTimeEffectData AddOverTimeEffect(int _id, Entity _dealer)
        {
            OverTimeEffectData _newEffect = null;
            OverTimeEffect _setting = GameManager.GetOverTimeEffect(_id);
            if (_setting != null)
            {
                if (mOverTimeEffect.TryGetValue(_id, out _newEffect))
                {
                    if (_setting.layered && _newEffect.layer < _setting.maxLayer)
                    {
                        _newEffect.layer++;
                    }
                    if (_setting.canBeExtended)
                    {
                        _newEffect.timer += _setting.duration;
                    }
                    if (_setting.canBeRefreshed && _newEffect.timer < _setting.duration)
                    {
                        _newEffect.timer = _setting.duration;
                    }
                    OnOverTimeChange(_setting.uid, _newEffect, OverTimeEffectEventType.Update);
                }
                else
                {
                    _newEffect = new OverTimeEffectData(_setting.uid, _setting.duration, _dealer.uid);
                    mOverTimeEffect.Add(_id, _newEffect);
                    OnOverTimeChange(_setting.uid, _newEffect, OverTimeEffectEventType.Add);
                }
            }
            return _newEffect;
        }

        /// <summary>
        /// Adds an over-time effect by its string UID and the dealer entity. Returns the OverTimeEffectData.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public OverTimeEffectData AddOverTimeEffect(string _uid, Entity _dealer)
        {
            return AddOverTimeEffect(GameManager.OverTimeEffectData.GetId(_uid), _dealer);
        }

        /// <summary>
        /// Dispels an over-time effect by its string UID. The success depends on the settings, and it only removes one layer of the effect.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public OverTimeEffectData DispellOverTimeEffect(string _uid)
        {
            return DispellOverTimeEffect(GameManager.OverTimeEffectData.GetId(_uid));
        }

        /// <summary>
        /// Dispels an over-time effect by its integer ID. The success depends on the settings, and it only removes one layer of the effect.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public OverTimeEffectData DispellOverTimeEffect(int _id)
        {
            OverTimeEffectData _effect = null;
            OverTimeEffect _setting = GameManager.GetOverTimeEffect(_id);
            if (_setting != null)
            {
                if (_setting.canBeDispelled && mOverTimeEffect.TryGetValue(_id, out _effect))
                {

                    _effect.layer--;
                    if (_effect.layer <= 0)
                    {
                        RemoveOverTimeEffect(_id);
                    }
                    else
                    {
                        OnOverTimeChange(_setting.uid, _effect, OverTimeEffectEventType.Update);
                    }
                }
            }
            return _effect;
        }

        /// <summary>
        /// Completely removes an over-time effect by its string UID, ensuring success and removing all layers.
        /// </summary>
        /// <param name="_uid"></param>
        public void RemoveOverTimeEffect(string _uid)
        {
            RemoveOverTimeEffect(GameManager.OverTimeEffectData.GetId(_uid));
        }

        /// <summary>
        /// Completely removes an over-time effect by its integer ID, ensuring success and removing all layers.
        /// </summary>
        /// <param name="_id"></param>
        public void RemoveOverTimeEffect(int _id)
        {
            if (mOverTimeEffect.ContainsKey(_id))
            {
                OnOverTimeChange(GameManager.OverTimeEffectData.GetStringKey(_id), mOverTimeEffect[_id], OverTimeEffectEventType.Remove);
                mOverTimeEffect.Remove(_id);
            }
        }

        /// <summary>
        /// Update the over-time effects with a time offset in seconds, for example, if you tick every second, then call UpdateOverTimeEffect(1F);
        /// </summary>
        /// <param name="_timerChange"></param>
        public void UpdateOverTimeEffect(float _timerChange)
        {
            List<int> _keys = new List<int>(mOverTimeEffect.Keys);
            foreach (var _key in _keys)
            {
                if (mOverTimeEffect.ContainsKey(_key))
                {//Without this check, effect being Dispell by another script could cause error.
                    if (mOverTimeEffect[_key].timer > 0F)
                    {
                        if (mOverTimeEffect.ContainsKey(_key) && OverTimeEffectTickCallback != null) OverTimeEffectTickCallback(mOverTimeEffect[_key].uid, mOverTimeEffect[_key].layer, GameManager.GetEntity(mOverTimeEffect[_key].dealer), this);
                        if (mOverTimeEffect.ContainsKey(_key))
                        {
                            mOverTimeEffect[_key].timer -= _timerChange;
                            if (mOverTimeEffect[_key].timer <= 0F)
                            {
                                RemoveOverTimeEffect(_key);
                            }
                            else
                            {
                                OnOverTimeChange(GameManager.OverTimeEffectData.GetStringKey(_key), mOverTimeEffect[_key], OverTimeEffectEventType.Update);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the stack count (layer) of a specific over-time effect by its string UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public int GetOverTimeEffectLayer(string _uid)
        {
            return GetOverTimeEffectLayer(GameManager.OverTimeEffectData.GetId(_uid));
        }

        /// <summary>
        /// Retrieves the stack count (layer) of a specific over-time effect by its integer ID.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public int GetOverTimeEffectLayer(int _id)
        {
            OverTimeEffectData _effect;
            if (mOverTimeEffect.TryGetValue(_id, out _effect))
            {
                return _effect.layer;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Add an new attribute data by its string UID. 
        /// </summary>
        /// <param name="_uid"></param>
        public void AddAttributeData(string _uid)
        {
            for (int i = Attributes.Count - 1; i >= 0; i--)
            {
                if (Attributes[i].uid == _uid)
                {
                    return;
                }
            }
            string _defaultString = GameManager.GetAttribute(_uid).defaultValue;
            float _defaultFloat = 0F;
            float.TryParse(_defaultString, out _defaultFloat);
            Attributes.Add(new AttributeData(_uid, _defaultFloat, _defaultString));
        }
        /// <summary>
        /// Remove an new attribute data by its string UID. 
        /// </summary>
        /// <param name="_uid"></param>
        public void RemoveAttributeData(string _uid)
        {
            for (int i = Attributes.Count - 1; i >= 0; i--)
            {
                if (Attributes[i].uid == _uid)
                {
                    Attributes.RemoveAt(i);
                }
            }
        }
        /// <summary>
        /// Retrieves the attribute data by its integer ID. If the entity does not have this attribute, it will return a placeholder value of 0.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public AttributeData GetAttributeData(int _id)
        {

            string _uid = GameManager.AttributeData.GetStringKey(_id);
            if (!SGD_Settings.isRuntime)
            {
                foreach (var obj in Attributes)
                {
                    if (obj.uid == _uid) return obj;
                }
                if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("no attribute values found with key:" + _uid);
                return new AttributeData(_uid, 0F);
            }

            if (!runtimeInited || attributeDic.Count == 0) Init();
            AttributeData _value;
            if (attributeDic.TryGetValue(_id, out _value))
            {
                return _value;
            }
            else
            {
                if (nullPlaceHolder == null) nullPlaceHolder = new AttributeData(_uid, 0F, "");
                return nullPlaceHolder;
            }
        }

        /// <summary>
        /// Retrieves the attribute data by its string UID. If the entity does not have this attribute, it will return a placeholder value of 0.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public AttributeData GetAttributeData(string _uid)
        {
            if (!SGD_Settings.isRuntime)
            {
                foreach (var obj in Attributes)
                {
                    if (obj.uid == _uid) return obj;
                }
                return new AttributeData(_uid, 0F, "");
            }

            return GetAttributeData(GameManager.AttributeData.GetId(_uid));
        }

        /// <summary>
        /// Sets the value of an attribute by its integer ID to a float value.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        public void SetAttributeValue(int _id, float _value)
        {
            GetAttributeData(_id).SetValue(_value);
        }
        /// <summary>
        /// Sets the value of an attribute by its string UID to a float value.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetAttributeValue(string _uid, float _value)
        {
            GetAttributeData(_uid).SetValue(_value);
        }
        /// <summary>
        /// Sets the value of an attribute by its integer ID to an integer value.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        public void SetAttributeValue(int _id, int _value)
        {
            GetAttributeData(_id).SetValue(_value);
        }
        /// <summary>
        /// Sets the value of an attribute by its string UID to an integer value.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetAttributeValue(string _uid, int _value)
        {
            GetAttributeData(_uid).SetValue(_value);
        }
        /// <summary>
        /// Sets the value of an attribute by its integer ID to a string value.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        public void SetAttributeValue(int _id, string _value)
        {
            GetAttributeData(_id).SetValue(_value);
        }
        /// <summary>
        /// Sets the value of an attribute by its string UID to a string value.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetAttributeValue(string _uid, string _value)
        {
            GetAttributeData(_uid).SetValue(_value);
        }
        /// <summary>
        /// Adds a float value to an existing attribute by its integer ID and returns the result.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <returns></returns>
        public float AddAttributeValue(int _id, float _value)
        {
            return GetAttributeData(_id).AddValue(_value);
        }
        /// <summary>
        /// Adds a float value to an existing attribute by its string UID and returns the result.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <returns></returns>
        public float AddAttributeValue(string _uid, float _value)
        {
            return GetAttributeData(_uid).AddValue(_value);
        }
        /// <summary>
        /// Adds an integer value to an existing attribute by its integer ID and returns the result.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <returns></returns>
        public int AddAttributeValue(int _id, int _value)
        {
            return GetAttributeData(_id).AddValue(_value);
        }
        /// <summary>
        /// Adds an integer value to an existing attribute by its string UID and returns the result.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <returns></returns>
        public int AddAttributeValue(string _uid, int _value)
        {
            return GetAttributeData(_uid).AddValue(_value);
        }
        /// <summary>
        /// Adds a float value to an existing attribute by its integer ID, clamps the result, and returns it.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <param name="_min"></param>
        /// <param name="_max"></param>
        /// <returns></returns>
        public float AddAttributeValueClamp(int _id, float _value, float _min, float _max)
        {
            return GetAttributeData(_id).AddValueClamp(_value, _min, _max);
        }
        /// <summary>
        /// Adds a float value to an existing attribute by its string UID, clamps the result, and returns it.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <param name="_min"></param>
        /// <param name="_max"></param>
        /// <returns></returns>
        public float AddAttributeValueClamp(string _uid, float _value, float _min, float _max)
        {
            return GetAttributeData(_uid).AddValueClamp(_value, _min, _max);
        }
        /// <summary>
        /// Adds a integer value to an existing attribute by its integer ID, clamps the result, and returns it.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <param name="_min"></param>
        /// <param name="_max"></param>
        /// <returns></returns>
        public int AddAttributeValueClamp(int _id, int _value, int _min, int _max)
        {
            return GetAttributeData(_id).AddValueClamp(_value, _min, _max);
        }
        /// <summary>
        /// Adds a integer value to an existing attribute by its string UID, clamps the result, and returns it.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <param name="_min"></param>
        /// <param name="_max"></param>
        /// <returns></returns>
        public int AddAttributeValueClamp(string _uid, int _value, int _min, int _max)
        {
            return GetAttributeData(_uid).AddValueClamp(_value, _min, _max);
        }
        /// <summary>
        /// Retrieves the float value of an attribute by its integer ID, optionally including temporary values.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_inculdTempValue"></param>
        /// <returns></returns>
        public float GetAttributeFloat(int _id, bool _inculdTempValue=true)
        {
            float _additional = 0F;
            foreach (var obj in Modules) _additional += obj.GetModule().GetAttributeValue(_id);
            if (!_inculdTempValue) return GetAttributeData(_id).GetFloat(AttributesUpgradeLevel) + _additional;
            return GetTempAttributesMerge(GetAttributeData(_id).GetFloat(AttributesUpgradeLevel), _id) + _additional;
        }

        /// <summary>
        /// Retrieves the float value of an attribute by its string UID, optionally including temporary values.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_inculdTempValue"></param>
        /// <returns></returns>
        public float GetAttributeFloat(string _uid, bool _inculdTempValue = true)
        {
            float _additional = 0F;
            foreach (var obj in Modules) _additional += obj.GetModule().GetAttributeValue(_uid);
            if (!_inculdTempValue) return GetAttributeData(_uid).GetFloat(AttributesUpgradeLevel)+ _additional;
            return GetTempAttributesMerge(GetAttributeData(_uid).GetFloat(AttributesUpgradeLevel), _uid) + _additional;
        }
        /// <summary>
        /// Retrieves the string value of an attribute by its integer ID.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public string GetAttributeString(int _id)
        {
            return GetAttributeData(_id).GetString();
        }
        /// <summary>
        /// Retrieves the string value of an attribute by its string UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public string GetAttributeString(string _uid)
        {
            return GetAttributeData(_uid).GetString();
        }
        /// <summary>
        /// Retrieves the integer value of an attribute by its integer ID, optionally including temporary values.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_inculdTempValue"></param>
        /// <returns></returns>
        public int GetAttributeInt(int _id, bool _inculdTempValue = true)
        {
            return Mathf.FloorToInt(GetAttributeFloat(_id, _inculdTempValue));
        }
        /// <summary>
        /// Retrieves the integer value of an attribute by its string UID, optionally including temporary values.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_inculdTempValue"></param>
        /// <returns></returns>
        public int GetAttributeInt(string _uid, bool _inculdTempValue = true)
        {
            return Mathf.FloorToInt(GetAttributeFloat(_uid, _inculdTempValue));
        }

        
        /// <summary>
        /// Registers a callback for changes to an attribute.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterAttributeChangeCallback(AttributeChangeEvent _callback)
        {
            AttributeChangeCallback += _callback;
        }
        /// <summary>
        /// Unregisters a callback for changes to an attribute.
        /// </summary>
        /// <param name="_callback"></param>
        public void UnRegisterAttributeChangeCallback(AttributeChangeEvent _callback)
        {
            AttributeChangeCallback -= _callback;
        }
        /// <summary>
        /// Clears all registered attribute change callbacks.
        /// </summary>
        public void ClearAttributeChangeCallback()
        {
            AttributeChangeCallback = null;
        }

       
        /// <summary>
        /// Registers an entity event callback to receive events.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterEntityEventCallback(EntityEvent _callback)
        {
            EntityEventCallback += _callback;
        }
        /// <summary>
        /// Unregisters an entity event callback.
        /// </summary>
        /// <param name="_callback"></param>
        public void UnRegisterEntityEventCallback(EntityEvent _callback)
        {
            EntityEventCallback -= _callback;
        }
        /// <summary>
        /// Clears all registered entity event callbacks.
        /// </summary>
        public void ClearEntityEventCallback()
        {
            EntityEventCallback = null;
        }

        /// <summary>
        /// Registers a callback for tick of over-time effects on all entites.
        /// </summary>
        /// <param name="_callback"></param>
        public static void RegisterOverTimeTickCallback(OverTimeEffectTickEvent _callback)
        {
            OverTimeEffectTickCallback += _callback;
        }

        /// <summary>
        /// Unregisters a callback for tick of over-time effects on all entites.
        /// </summary>
        /// <param name="_callback"></param>
        public static void UnRegisterOverTimeTickCallback(OverTimeEffectTickEvent _callback)
        {
            OverTimeEffectTickCallback -= _callback;
        }

        /// <summary>
        /// Clear the registered callback for tick of over-time effects on all entites.
        /// </summary>
        public static void ClearOverTimeTickCallback()
        {
            OverTimeEffectTickCallback = null;
        }

        /// <summary>
        /// Registers a callback for changes to over-time effects on this entity.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterOverTimeChangeCallback(OverTimeEffectChangeEvent _callback)
        {
            OverTimeEffectChangeCallback += _callback;
        }
        /// <summary>
        /// Unregisters a callback for changes to over-time effects on this entity.
        /// </summary>
        /// <param name="_callback"></param>
        public void UnRegisterOverTimeChangeCallback(OverTimeEffectChangeEvent _callback)
        {
            OverTimeEffectChangeCallback -= _callback;
        }
        /// <summary>
        /// Clears all registered over-time effect change callbacks.
        /// </summary>
        public void ClearOverTimeChangeCallback()
        {
            OverTimeEffectChangeCallback = null;
        }

       
    }

}
