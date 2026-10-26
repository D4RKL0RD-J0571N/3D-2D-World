using UnityEngine;

namespace SoftKitty
{
    /// <summary>
    /// The callback delegate for attribute value changes.
    /// </summary>
    /// <param name="_uid"></param>
    /// <param name="_value"></param>
    /// <param name="_dealer"></param>
    public delegate void AttributeChangeEvent(string _uid, float _value, Entity _dealer);

    /// <summary>
    /// TempAttribute represents runtime data for time-based values of attributes, such as temporary buffs or debuffs. For example, if a character receives a speed boost for 10 seconds, the boosted value would be represented by a TempAttribute.
    /// </summary>
    [System.Serializable]
    public class TempAttribute
    {
        /// <summary>
        /// The value of this TempAttribute (e.g., the amount of speed boost).
        /// </summary>
        public float value = 0F;
        /// <summary>
        /// The timestamp indicating when the TempAttribute will expire or the remaining time for the attribute.
        /// </summary>
        public float timeStamp = 0F;
        /// <summary>
        /// The math function used to modify this TempAttribute's value. The available functions are:
        /// Add	| Multiply	| Override	| Max	| Min
        /// </summary>
        public BasicMathFunction mathFunction = BasicMathFunction.Add;
        /// <summary>
        /// TempAttribute represents runtime data for time-based values of attributes, such as temporary buffs or debuffs. For example, if a character receives a speed boost for 10 seconds, the boosted value would be represented by a TempAttribute.
        /// </summary>
        public TempAttribute(){}
        /// <summary>
        /// TempAttribute represents runtime data for time-based values of attributes, such as temporary buffs or debuffs. For example, if a character receives a speed boost for 10 seconds, the boosted value would be represented by a TempAttribute.
        /// </summary>
        /// <param name="_value"></param>
        /// <param name="_timeStamp"></param>
        /// <param name="_math"></param>
        public TempAttribute(float _value, float _timeStamp, BasicMathFunction _math ) 
        {
            value = _value;
            timeStamp = _timeStamp;
            mathFunction = _math;
        }

        /// <summary>
        /// Returns the merged result of the base value and the TempAttribute value. This method combines the base value with the time-based attribute value depending on the selected math function.
        /// </summary>
        /// <param name="_base"></param>
        /// <returns></returns>
        public float GetValue(float _base)
        {
            if (Time.time < timeStamp)
            {
                if (mathFunction == BasicMathFunction.Override)
                {
                    return value;
                }
                else if (mathFunction == BasicMathFunction.Max)
                {
                    return Mathf.Max(_base + value);
                }
                else if (mathFunction == BasicMathFunction.Min)
                {
                    return Mathf.Min(_base + value);
                }
                else if (mathFunction == BasicMathFunction.Add)
                {
                    return _base + value;
                }
                else if (mathFunction == BasicMathFunction.Multiply)
                {
                    return _base * value;
                }
            }
            return _base;
        }
    }


    /// <summary>
    /// AttributeData represents the runtime data for the value of attributes, allowing manipulation and retrieval of an attribute¡¯s value during gameplay.
    /// </summary>
    [System.Serializable]
    public class AttributeData
    {
        /// <summary>
        /// The unique string ID that links this attribute data to its corresponding setting.
        /// </summary>
        public string uid;
        /// <summary>
        ///The unique integer ID that connects this attribute data to its corresponding setting.
        /// </summary>
        public int id
        {
            get
            {
                return GameManager.AttributeData.GetId(uid);
            }
        }
        /// <summary>
        /// Retrieve the Attribute settings of this data.
        /// </summary>
        public Attribute setting
        {
            get
            {
                if (SGD_Settings.isRuntime)
                {
                    if (mAttributeSetting == null || !runtimeInited) RuntimeInit();
                    return mAttributeSetting;
                }
                else
                {
                    return GameManager.GetAttribute(uid);
                }
            }
        }
        /// <summary>
        /// The string value of this attribute.
        /// </summary>
        public string stringValue = "";
        /// <summary>
        /// The float value of this attribute.
        /// </summary>
        public float floatValue = 0F;
        /// <summary>
        /// Whether this attribute is Locked, locked attribute is not valid and invisible.
        /// </summary>
        public bool locked = false;

        /// <summary>
        /// Random chance to unlock this attribute. (For rogue-like game)
        /// </summary>
        public int randomChange = 100;
        /// <summary>
        /// Whether this attribute has fixed value.
        /// </summary>
        public bool isFixed = true;
        /// <summary>
        /// The minimal value of this attribute if this attribute has random value.
        /// </summary>
        public float minValue = 0F;
        /// <summary>
        /// The maximum value of this attribute if this attribute has random value.
        /// </summary>
        public float maxValue = 0F;

       

        /// <summary>
        /// AttributeData represents the runtime data for the value of attributes, allowing manipulation and retrieval of an attribute¡¯s value during gameplay.
        /// </summary>
        public AttributeData() { }
        /// <summary>
        /// AttributeData represents the runtime data for the value of attributes, allowing manipulation and retrieval of an attribute¡¯s value during gameplay.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public AttributeData(string _uid, string _value) { uid = _uid; floatValue = 0F; stringValue = _value; }
        /// <summary>
        /// AttributeData represents the runtime data for the value of attributes, allowing manipulation and retrieval of an attribute¡¯s value during gameplay.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public AttributeData(string _uid, float _value) { uid = _uid; floatValue = _value; stringValue = ""; }
        /// <summary>
        /// AttributeData represents the runtime data for the value of attributes, allowing manipulation and retrieval of an attribute¡¯s value during gameplay.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_float"></param>
        /// <param name="_string"></param>
        public AttributeData(string _uid, float _float, string _string) { uid = _uid; floatValue = _float; stringValue = _string; }

        private AttributeChangeEvent AttributeChangeCallback;
        private bool runtimeInited = false;
        [SerializeReference]
        private Attribute mAttributeSetting=null;
        private int _id = -1;
        private float _oldValue;
        private void RuntimeInit()
        {
            if (_id == -1) _id = id;
            mAttributeSetting = GameManager.GetAttribute(_id);
            runtimeInited = true;
        }


        /// <summary>
        /// Retrieves a copy of the data
        /// </summary>
        /// <returns></returns>
        public AttributeData Copy() {
            AttributeData _copy = new AttributeData(uid, floatValue, stringValue);
            _copy.randomChange = randomChange;
            _copy.minValue = minValue;
            _copy.maxValue = maxValue;
            _copy.isFixed = isFixed;
            _copy.locked = locked;
            return _copy;
        }

        /// <summary>
        /// For rogue-like game, call this method to decide whether this attribute should be locked and generate a random value for it.
        /// </summary>
        public void Init()
        {
            if (isFixed)
            {
                locked = false;
            }
            else
            {
                SetLock (Random.Range(0, 100) > randomChange);
                if (!setting.stringValue) floatValue = Random.Range(minValue, maxValue);
            }
        }

        /// <summary>
        /// Randomize the value of this attribute based on its min value and max value setting.
        /// </summary>
        public void Randomize()
        {
            if (!isFixed && !setting.stringValue) floatValue = Random.Range(minValue, maxValue);
        }

        /// <summary>
        /// Registers a callback for changes to this attribute's value.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterCallback(AttributeChangeEvent _callback)
        {
            AttributeChangeCallback = _callback;
        }

        /// <summary>
        /// Determines whether this attribute is numerical.
        /// </summary>
        /// <returns></returns>
        public bool isNumber()
        {
            return !setting.stringValue;
        }

        /// <summary>
        /// Determines whether this attribute is a string value.
        /// </summary>
        /// <returns></returns>
        public bool isString()
        {
            return setting.stringValue;
        }

        /// <summary>
        /// Sets whether this attribute is locked. A locked attribute is not valid and is invisible.
        /// </summary>
        public void SetLock(bool _lock)
        {
            locked = _lock;
        }

        /// <summary>
        /// Retrieves the float value of this attribute. An optional upgrade level can be provided.
        /// </summary>
        /// <param name="_upgradeLevel"></param>
        /// <returns></returns>
        public float GetFloat(int _upgradeLevel = 0)
        {
            if (SGD_Settings.isRuntime)
            {
                if (setting == null || setting.stringValue || (locked && !isFixed)) return 0F;
                return floatValue + setting.upgradeIncrement * _upgradeLevel;
            }
            else if (setting.stringValue) return 0F;
            return floatValue + setting.upgradeIncrement * _upgradeLevel;
        }

        /// <summary>
        /// Retrieves the integer value of this attribute. An optional upgrade level can be provided.
        /// </summary>
        /// <param name="_upgradeLevel"></param>
        /// <returns></returns>
        public int GetInt(int _upgradeLevel = 0)
        {
            return Mathf.FloorToInt(GetFloat(_upgradeLevel));
        }

        /// <summary>
        /// Retrieves the string value of this attribute.
        /// </summary>
        /// <returns></returns>
        public string GetString()
        {
            if (SGD_Settings.isRuntime)
            {
                if (setting == null || !setting.stringValue) return "";
                if (locked && !isFixed) return "None";
            }
            else if (!setting.stringValue) return "";
            return stringValue;
        }

        /// <summary>
        /// Overrides the value of this attribute with a float. The dealer entity of this change is optional.
        /// </summary>
        /// <param name="_value"></param>
        public void SetValue(float _value, Entity _dealer=null)
        {
            _oldValue = GetFloat();
            floatValue = _value;
            if (AttributeChangeCallback != null) AttributeChangeCallback(uid, _value- _oldValue, _dealer);
           
        }

        /// <summary>
        /// Overrides the value of this attribute with an integer value. The dealer entity of this change is optional.
        /// </summary>
        /// <param name="_value"></param>
        public void SetValue(int _value, Entity _dealer=null)
        {
            _oldValue = GetInt();
            floatValue = _value;
            if (AttributeChangeCallback != null) AttributeChangeCallback(uid, _value - _oldValue, _dealer);
        }

        /// <summary>
        /// Overrides the value of this attribute with a string value.
        /// </summary>
        /// <param name="_value"></param>
        public void SetValue(string _value)
        {
            stringValue = _value;
        }

        /// <summary>
        /// Adds a float value to the current attribute value, then returns the updated result.The dealer entity of this change is optional.
        /// </summary>
        /// <param name="_value"></param>
        public float AddValue(float _value, Entity _dealer=null)
        {
            floatValue += _value;
            if (AttributeChangeCallback != null) AttributeChangeCallback(uid, _value, _dealer);
            return floatValue;
        }

        /// <summary>
        /// Adds an integer value to the current attribute value, then returns the updated result.The dealer entity of this change is optional.
        /// </summary>
        /// <param name="_value"></param>
        public int AddValue(int _value, Entity _dealer = null)
        {
            floatValue += _value;
            if (AttributeChangeCallback != null) AttributeChangeCallback(uid, _value, _dealer);
            return Mathf.FloorToInt(floatValue);
        }

        /// <summary>
        /// Adds a float value to the current attribute, clamps the result within the specified minimum and maximum bounds, then returns the updated result.
        /// </summary>
        /// <param name="_value"></param>
        public float AddValueClamp(float _value,float _min, float _max, Entity _dealer = null)
        {
            floatValue = Mathf.Clamp(floatValue + _value, _min, _max);
            if (AttributeChangeCallback != null) AttributeChangeCallback(uid, _value, _dealer);
            return floatValue;
        }

        /// <summary>
        /// Adds an integer value to the current attribute, clamps the result within the specified minimum and maximum bounds, then returns the updated result.
        /// </summary>
        /// <param name="_value"></param>
        public int AddValueClamp(int _value, int _min, int _max, Entity _dealer = null)
        {
            floatValue = Mathf.Clamp(floatValue + _value, _min, _max);
            if (AttributeChangeCallback != null) AttributeChangeCallback(uid, _value, _dealer);
            return Mathf.FloorToInt(floatValue);
        }
    }


    /// <summary>
    /// Attribute is a data class representing individual attributes within the system, such as character stats and other configurable traits.
    /// </summary>
    [System.Serializable]
    public class Attribute
    {
        /// <summary>
        /// A unique identifier for this attribute (integer value).
        /// </summary>
        public string uid;
        /// <summary>
        /// A unique integer ID for this attribute.
        /// </summary>
        public int id
        {
            get {
                return GameManager.AttributeData.GetId(uid);
            }
        }
        /// <summary>
        /// The display name for this attribute (e.g., "Health", "Attack").
        /// </summary>
        public string name;
        /// <summary>
        /// Indicates whether the attribute holds a string value (e.g., character name).
        /// </summary>
        public bool stringValue = false;
        /// <summary>
        /// The upgrade increment tied to this attribute's level. This can be used for things like character leveling, equipment upgrades, or item quality.
        /// </summary>
        public float upgradeIncrement = 0F;
        /// <summary>
        /// Defines if this attribute is a core stat (e.g., attack, health, stamina).
        /// </summary>
        public bool coreStats = false;
        /// <summary>
        /// Whether this attribute visible in the hover information panel.
        /// </summary>
        public bool visible = true;
        /// <summary>
        /// Whether this attribute visible in the stats panel.
        /// </summary>
        public bool visibleInStatsPanel = true;
        /// <summary>
        /// The display format type in the hover information panel.
        /// </summary>
        public int displayFormat = 0;
        /// <summary>
        /// Suffixes string when display this attribute.
        /// </summary>
        public string suffixes = "";
        /// <summary>
        /// Whether display compare information for this attribute in mouse hover information panel.
        /// </summary>
        public bool compareInfo = true;

       
        public string defaultValue = "0";

        [HideInInspector]
        public bool fold = true;

        /// <summary>
        /// Returns a formatted string for displaying this attribute based on the provided value.
        /// </summary>
        /// <param name="_value"></param>
        /// <returns></returns>
        public string GetDisplayString(string _value)
        {
            float _floatValue = 0F;
            if (isNumber()) {
                float.TryParse(_value, out _floatValue);
            }
            return name + (displayFormat == 0 ? (_floatValue >= 0F ? " +" : " -") : " : ") + " " + suffixes;
        }

        /// <summary>
        /// Retrieves a copy of the data.
        /// </summary>
        /// <returns></returns>
        public Attribute Copy()
        {
            Attribute _newAtt = new Attribute();
            _newAtt.uid = uid;
            _newAtt.name = name;
            _newAtt.stringValue = stringValue;
            _newAtt.upgradeIncrement = upgradeIncrement;
            _newAtt.visible = visible;
            _newAtt.displayFormat = displayFormat;
            _newAtt.suffixes = suffixes;
            _newAtt.coreStats = coreStats;
            _newAtt.compareInfo = compareInfo;
            _newAtt.visibleInStatsPanel = visibleInStatsPanel;
            _newAtt.defaultValue = defaultValue;

            return _newAtt;
        }

        /// <summary>
        /// Whether this attribute numberical
        /// </summary>
        /// <returns></returns>
        public bool isNumber()
        {
            return !stringValue;
        }

    }

}
