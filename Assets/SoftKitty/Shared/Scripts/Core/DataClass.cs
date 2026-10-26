using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{

    public enum OverTimeEffectEventType
    {
        Add,
        Remove,
        Update
    }
    public enum BasicMathFunction
    {
        Add,
        Multiply,
        Override,
        Max,
        Min
    }

    /// <summary>
    /// Dynamic Float is instance-based runtime variables of visual graph object.
    /// </summary>
    [System.Serializable]
    public class DynamicFloat
    {
        /// <summary>
        /// Unique string uid of this DynamicFloat.
        /// </summary>
        public string uid;
        /// <summary>
        /// Float value of this DynamicFloat.
        /// </summary>
        public float value;
        /// <summary>
        /// Dynamic Float is instance-based runtime variables of visual graph object.
        /// </summary>
        public DynamicFloat() { }
        /// <summary>
        /// Dynamic Float is instance-based runtime variables of visual graph object.
        /// </summary>
        /// <param name="_uid"></param>
        public DynamicFloat(string _uid) { uid = _uid; }
        /// <summary>
        /// Dynamic Float is instance-based runtime variables of visual graph object.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public DynamicFloat(string _uid, float _value) { uid = _uid; value = _value; }
        /// <summary>
        /// Retrieves a copy of the data.
        /// </summary>
        /// <returns></returns>
        public DynamicFloat Copy()
        {
            DynamicFloat _copy = new DynamicFloat(uid, value);
            return _copy;
        }
    }

    /// <summary>
    /// Custom object field that could be loaded with various methods 
    /// </summary>
    [System.Serializable]
    public class CustomField
    {
        public string key;
        public LoadMethod loadMethod = LoadMethod.DirectReference;
        public string loadPath="";
        public Object value;
        public bool fold = false;
        public CustomField Copy()
        {
            CustomField _newObj = new CustomField();
            _newObj.key = key;
            _newObj.value = value;
            _newObj.loadMethod = loadMethod;
            _newObj.loadPath = loadPath;
            _newObj.fold = fold;
            return _newObj;
        }

        public T GetObject<T>() where T : UnityEngine.Object
        {
            if (loadMethod == LoadMethod.DirectReference)
            {
                return (T)value;
            }
            else if (loadMethod == LoadMethod.Resources)
            {
                return Resources.Load<T>(loadPath);
            }
            else
            {
                return SGD_Settings.Instance.CustomLoader.Load<T>(loadPath);
            }
        }
    }

    #region Custom Data
    /// <summary>
    /// Custom Data settings for Entity data.
    /// </summary>
    [System.Serializable]
    public class CustomData
    {
        public enum CustomTypes
        {
            Float,
            Int,
            Bool,
            String,
            IntList,
            IdIntList,
            IdFloatList,
        }
        public string uid;
        public CustomTypes type;
        public int index;
        public CustomData(CustomTypes _type, string _uid, int _index)
        {
            type = _type;
            uid = _uid;
            index = _index;
        }
    }


    /// <summary>
    /// A custom data type with a pair of integer ID and integer value.
    /// </summary>
    [System.Serializable]
    public class IdInt
    {
        /// <summary>
        /// The unique integer ID.
        /// </summary>
        public int id = 0;
        /// <summary>
        /// The integer value.
        /// </summary>
        public int value = 0;
        /// <summary>
        /// A custom data type with a pair of integer ID and integer value.
        /// </summary>
        public IdInt()
        {
            id = 0;
            value = 0;
        }
        /// <summary>
        /// A custom data type with a pair of integer ID and integer value.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        public IdInt(int _id,int _value)
        {
            id = _id;
            value = _value;
        }
        /// <summary>
        /// Retrieves a copy of the data.
        /// </summary>
        /// <returns></returns>
        public IdInt Copy()
        {
            IdInt _copy = new IdInt();
            _copy.id = id;
            _copy.value = value;
            return _copy;
        }
    }

    /// <summary>
    /// A custom data type with a pair of integer ID and float value.
    /// </summary>
    [System.Serializable]
    public class IdFloat
    {
        /// <summary>
        /// The unique integer ID.
        /// </summary>
        public int id = 0;
        /// <summary>
        /// The float value.
        /// </summary>
        public float value = 0F;
        /// <summary>
        /// A custom data type with a pair of integer ID and float value.
        /// </summary>
        public IdFloat()
        {
            id = 0;
            value = 0F;
        }
        /// <summary>
        /// A custom data type with a pair of integer ID and float value.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        public IdFloat(int _id,float _value)
        {
            id = _id;
            value = _value;
        }
        /// <summary>
        /// Retrieves a copy of the data.
        /// </summary>
        /// <returns></returns>
        public IdFloat Copy()
        {
            IdFloat _copy = new IdFloat();
            _copy.id = id;
            _copy.value = value;
            return _copy;
        }
    }

    /// <summary>
    /// A custom data type with a list of integer ID and integer value pair.
    /// </summary>
    [System.Serializable]
    public class IdIntList
    {
        /// <summary>
        /// The list of the data.
        /// </summary>
        public List<IdInt> list = new List<IdInt>();
        private Dictionary<int, IdInt> valueDic = new Dictionary<int, IdInt>();
        private bool runtimeInited = false;

        /// <summary>
        /// A custom data type with a list of integer ID and integer value pair.
        /// </summary>
        public IdIntList()
        {
            list = new List<IdInt>();
        }
        /// <summary>
        /// Retrieves a copy of the data.
        /// </summary>
        /// <returns></returns>
        public IdIntList Copy()
        {
            IdIntList _copy = new IdIntList();
            _copy.list = new List<IdInt>();
            for (int i=0;i< list.Count;i++) {
                _copy.list.Add(list[i].Copy());
            }
            return _copy;
        }

        public void Add(IdInt _value)
        {
            list.Add(_value);
        }
        public void Remove(IdInt _value)
        {
            list.Remove(_value);
        }
        public void RemoveAt(int _index)
        {
            list.RemoveAt(_index);
        }
        public void Clear()
        {
            list.Clear();
        }

        /// <summary>
        /// Retrieves the integer value based on its integer index.
        /// </summary>
        /// <param name="_index"></param>
        /// <returns></returns>
        public IdInt GetValueByIndex(int _index)
        {
            return _index >= 0 && _index < list.Count ? list[_index] : new IdInt();
        }

        /// <summary>
        /// Override the value by its index to an integer value.
        /// </summary>
        /// <param name="_index"></param>
        /// <param name="_value"></param>
        public void SetValueByIndex(int _index, IdInt _value)
        {
            if (_index >= 0 && _index < list.Count)
            {
                list[_index] = _value;
            }
        }

        private void RuntimeInit()
        {
            valueDic.Clear();
            foreach (var obj in list)
            {
                if (!valueDic.ContainsKey(obj.id)) valueDic.Add(obj.id, obj);
            }
            runtimeInited = true;
        }

        /// <summary>
        /// Retrieves the integer value based on its integer id.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public int GetValueById(int _id)
        {
            if (!runtimeInited) RuntimeInit();
            IdInt _result = new IdInt();
            valueDic.TryGetValue(_id, out _result);
            return _result.value;
        }
        /// <summary>
        /// Override the value by its integer id to an integer value.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        public void SetValueById(int _id, int _value)
        {
            if (!SGD_Settings.isRuntime)
            {
                foreach (var obj in list)
                {
                    if (obj.id == _id)
                    {
                        obj.value = _value;
                        return;
                    }
                }
                list.Add(new IdInt(_id, _value));
                return;
            }
            if (!runtimeInited) RuntimeInit();
            if (valueDic.ContainsKey(_id)) valueDic[_id].value = _value;
        }
        /// <summary>
        /// Retrieves the count number of the list.
        /// </summary>
        public int Count
        {
            get
            {
                return list.Count;
            }
        }
    }

    /// <summary>
    /// A custom data type with a list of integer ID and float value pair.
    /// </summary>
    [System.Serializable]
    public class IdFloatList
    {
        /// <summary>
        /// The list of the data.
        /// </summary>
        public List<IdFloat> list = new List<IdFloat>();
        private Dictionary<int, IdFloat> valueDic = new Dictionary<int, IdFloat>();
        private bool runtimeInited = false;
        /// <summary>
        /// A custom data type with a list of integer ID and float value pair.
        /// </summary>
        public IdFloatList()
        {
            list = new List<IdFloat>();
        }
        /// <summary>
        /// Retrieves a copy of the data.
        /// </summary>
        /// <returns></returns>
        public IdFloatList Copy()
        {
            IdFloatList _copy = new IdFloatList();
            _copy.list = new List<IdFloat>();
            for (int i = 0; i < list.Count; i++)
            {
                _copy.list.Add(list[i].Copy());
            }
            return _copy;
        }
        public void Add(IdFloat _value)
        {
            list.Add(_value);
        }
        public void Remove(IdFloat _value)
        {
            list.Remove(_value);
        }
        public void RemoveAt(int _index)
        {
            list.RemoveAt(_index);
        }
        public void Clear()
        {
            list.Clear();
        }
        /// <summary>
        /// Retrieves the float value based on its integer index.
        /// </summary>
        /// <param name="_index"></param>
        /// <returns></returns>
        public IdFloat GetValueByIndex(int _index)
        {
            return _index >= 0 && _index < list.Count ? list[_index] : new IdFloat();
        }
        /// <summary>
        /// Override the value by its index to an integer value.
        /// </summary>
        /// <param name="_index"></param>
        /// <param name="_value"></param>
        public void SetValueByIndex(int _index, IdFloat _value)
        {
            if (_index >= 0 && _index < list.Count)
            {
                list[_index] = _value;
            }
        }

        private void RuntimeInit()
        {
            valueDic.Clear();
            foreach (var obj in list)
            {
                if (!valueDic.ContainsKey(obj.id)) valueDic.Add(obj.id, obj);
            }
            runtimeInited = true;
        }
        /// <summary>
        /// Retrieves the float value based on its integer id.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public float GetValueById(int _id)
        {
            if (!runtimeInited) RuntimeInit();
            IdFloat _result = new IdFloat();
            valueDic.TryGetValue(_id, out _result);
            return _result.value;
        }
        /// <summary>
        /// Override the value by its integer id to an integer value.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        public void SetValueById(int _id, float _value)
        {
            if (!SGD_Settings.isRuntime) {
                foreach (var obj in list) {
                    if (obj.id == _id)
                    {
                        obj.value = _value;
                        return;
                    }
                }
                list.Add(new IdFloat(_id,_value));
                return;
            }
            if (!runtimeInited) RuntimeInit();
            if (valueDic.ContainsKey(_id)) valueDic[_id].value = _value;
        }
        /// <summary>
        /// Retrieves the count number of the list.
        /// </summary>
        public int Count
        {
            get
            {
                return list.Count;
            }
        }
    }

    /// <summary>
    /// A custom data type with a list of integer value.
    /// </summary>
    [System.Serializable]
    public class IntList
    {
        /// <summary>
        /// The list of the data.
        /// </summary>
        public List<int> list = new List<int>();
        /// <summary>
        /// A custom data type with a list of integer value.
        /// </summary>
        public IntList()
        {
            list = new List<int>();
        }
        /// <summary>
        /// Retrieves a copy of the data.
        /// </summary>
        /// <returns></returns>
        public IntList Copy()
        {
            IntList _copy = new IntList();
            _copy.list = new List<int>();
            for (int i = 0; i < list.Count; i++)
            {
                _copy.list.Add(list[i]);
            }
            return _copy;
        }

        public void Add(int _value)
        {
            list.Add(_value);
        }
        public void Remove(int _value)
        {
            list.Remove(_value);
        }
        public void RemoveAt(int _index)
        {
            list.RemoveAt(_index);
        }
        public void Clear()
        {
            list.Clear();
        }

        /// <summary>
        /// Retrieves the integer value based on its integer index.
        /// </summary>
        /// <param name="_index"></param>
        /// <returns></returns>
        public int GetValueByIndex(int _index)
        {
            return _index >= 0 && _index < list.Count ? list[_index] : 0;
        }
        /// <summary>
        /// Override the value by its index to an integer value.
        /// </summary>
        /// <param name="_index"></param>
        /// <param name="_value"></param>
        public void SetValueByIndex(int _index, int _value)
        {
            if (_index >= list.Count)
            {
                for (int i = list.Count; i <= _index; i++) list.Add(0);
            }
            if (_index >= 0)
            {
                list[_index] = _value;
            }
        }
        /// <summary>
        /// Retrieves the count number of the list.
        /// </summary>
        public int Count
        {
            get
            {
                return list.Count;
            }
        }
    }
    #endregion



    #region ID, Key
    [System.Serializable]
    public class IdKey
    {
        public int id;
        public string key;
        public IdKey(int _id, string _key)
        {
            id = _id;
            key = _key;
        }

    }
    [System.Serializable]
    public class StringIdManager
    {
        public List<IdKey> idToKey=new List<IdKey>();
        public bool uiFold=false;
        private Dictionary<string, int> runtimeKeyToID=new Dictionary<string, int>();
        private Dictionary<int,string> runtimeIdToKey = new Dictionary<int, string>();
        private bool runtimeInited = false;
        public int AvaliableId
        {
            get
            {
                int _maxId = -1;
                List<int> _idList = new List<int>();
                foreach (var obj in idToKey) {
                    if (obj.id > _maxId) _maxId = obj.id;
                    _idList.Add(obj.id);
                }
                int _id = _maxId+1;
                for (int i=0;i<= _maxId+1;i++) {
                    if (!_idList.Contains(i))
                    {
                        _id = i;
                        break;
                    }
                }
                return _id;
            }
        }

        public void Clear()
        {
            idToKey.Clear();
            runtimeInited = false;
        }
        public void ReplaceKey(string _oldKey,string _newKey)
        {
            for (int i = 0; i < idToKey.Count; i++)
            {
                if (idToKey[i].key == _oldKey)
                {
                    idToKey[i].key= _newKey;
                    break;
                }
            }
        }

        public void ReplaceKey(int _id, string _newKey)
        {
            for (int i = 0; i < idToKey.Count; i++)
            {
                if (idToKey[i].id == _id)
                {
                    idToKey[i].key = _newKey;
                    break;
                }
            }
        }

        public void RemoveKey(int _id)
        {
            for (int i = 0; i < idToKey.Count; i++)
            {
                if (idToKey[i].id == _id)
                {
                    idToKey.RemoveAt(i);
                    break;
                }
            }
        }

        public string GetStringKey(int _id)
        {
            if (SGD_Settings.isRuntime)
            {
               if (!runtimeInited) RuntimeInit();
               string _key= "Invalid Key";
                if (!runtimeIdToKey.TryGetValue(_id, out _key))
                {
                    for (int i = 0; i < idToKey.Count; i++)
                    {
                        if (idToKey[i].id == _id)
                        {
                            _key = idToKey[i].key;
                            break;
                        }
                    }
                }
                return _key;
            }
            else
            {
                string _key = "Invalid Key";
                for (int i = 0; i < idToKey.Count; i++)
                {
                    if (idToKey[i].id == _id)
                    {
                        _key = idToKey[i].key;
                        break;
                    }
                }
                return _key;
            }


        }

        public int GetId(string _key)
        {
            
            if (SGD_Settings.isRuntime)
            {
                if (!runtimeInited) RuntimeInit();
                int _id = -1;
                if (string.IsNullOrEmpty(_key)) return _id;
                if (!runtimeKeyToID.TryGetValue(_key, out _id))
                {
                    for (int i = 0; i < idToKey.Count; i++)
                    {
                        if (idToKey[i].key == _key)
                        {
                            _id = idToKey[i].id;
                            break;
                        }
                    }
                }
                return _id;
            }
            else
            {
                int _id = -1;
                for (int i=0;i< idToKey.Count;i++) {
                    if (idToKey[i].key == _key)
                    {
                        _id = idToKey[i].id;
                        break;
                    }
                }
                if (_id == -1)
                {
                    _id = AvaliableId;
                    idToKey.Add(new IdKey(_id, _key));
                }
                return _id;
            }
        }

        private void RuntimeInit()
        {
            runtimeKeyToID.Clear();
            runtimeIdToKey.Clear();
            for (int i = 0; i < idToKey.Count; i++)
            {
                runtimeKeyToID.Add(idToKey[i].key, idToKey[i].id);
                runtimeIdToKey.Add(idToKey[i].id, idToKey[i].key);
            }
          
            runtimeInited = true;
        }
    }
    #endregion
}
