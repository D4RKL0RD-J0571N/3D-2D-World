using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    [System.Serializable]
    public class GraphBase : ScriptableObject
    {
         
    }

    /// <summary>
    /// OverTimeEffect represents the setting data for an over-time effect (e.g., DoT, buffs, debuffs) applied to entities during combat.
    /// </summary>
    [System.Serializable]
    public class OverTimeEffect
    {
        #region Variables
        /// <summary>
        /// The unique string identifier (UID) for this effect. This helps to track and reference the effect within the system.
        /// </summary>
        public string uid;
        /// <summary>
        /// The unique integer ID for this effect. This is typically used internally for quick lookups.
        /// </summary>
        public int id
        {
            get
            {
                return GameManager.OverTimeEffectData.GetId(uid);
            }
        }
        /// <summary>
        /// The display name of the effect (e.g., "Poison", "Burn", "Freeze"). Used for UI and user-facing elements.
        /// </summary>
        public string name;
        public string iconPath;
        /// <summary>
        /// The category of the effect (e.g., "Debuff", "Buff", "DoT"). This is used to categorize and organize effects.
        /// </summary>
        public string category;
        /// <summary>
        /// The icon representing the effect in the UI. Depending on the load method chosen, the icon can be loaded via:
        ///o DirectReference
        ///o Resources folder
        ///o Asset bundle
        ///o Addressable system
        ///o Or custom loading methods.
        /// </summary>
        public Texture icon
        {
            get
            {
                if (iconLoadMethod == LoadMethod.DirectReference)
                {
                    return directRefIcon;
                }
                if (mIcon == null)
                {
                    if (iconLoadMethod == LoadMethod.Resources)
                    {
                        mIcon = Resources.Load<Texture>(iconPath);
                    }
                    else if (iconLoadMethod == LoadMethod.Custom)
                    {
                        mIcon = AssetLoader.instance.Load<Texture>(iconPath);
                    }
                }
                return mIcon;
            }
        }
        public Texture directRefIcon;
        public LoadMethod iconLoadMethod;
        private Texture mIcon;
        public bool permanent = false;
        /// <summary>
        /// The total duration of the effect in seconds. This determines how long the effect lasts before it is removed.
        /// </summary>
        public float duration = 1F;
        public bool layered = false;
        public int maxLayer = 99;
        public bool canBeExtended = false;
        public bool canBeRefreshed = true;
        public bool canBeDispelled = true;
        /// <summary>
        /// The background color used for the UI representation of the effect. This allows you to customize how it appears on the screen.
        /// </summary>
        public Color backGroundColor;
        /// <summary>
        /// The frame color used for the UI representation of the effect. It defines the border color in the UI.
        /// </summary>
        public Color frameColor;

        /// <summary>
        /// The graph object associated with this over-time effect. This graph defines how the effect behaves over time (e.g., tick damage, scaling buffs, etc.).
        /// </summary>
        public GraphBase DesignGraph;
        public List<DynamicFloat> DynamicVariables = new List<DynamicFloat>();

        /// <summary>
        /// A list of custom data fields associated with the effect. These fields can store any object with a key, allowing you to add custom information to the effect.
        /// </summary>
        public List<CustomField> mCustomField = new List<CustomField>();
        public bool fold = false;
        #endregion

        #region Master GPU-HealthBar
#if MASTER_HEALTH_BAR
        public int barEffectType = -1;
        public Color barEffctColor;
        public bool endTime = false;
        public Color timeColor;
        public bool fadeVisible = false;
        public enum FadeType
        {
            Type0,
            Type1,
            Type2
        }
        public FadeType fadeType;
        public bool flash = false;
        public float flashThreshold = 1f;
#endif
        #endregion

        #region Methods
        /// <summary>
        /// Retrieves a copy of the data.
        /// </summary>
        /// <returns></returns>
        public OverTimeEffect Copy()
        {
            OverTimeEffect _copy = new OverTimeEffect();
            _copy.uid = uid;
            _copy.name = name;
            _copy.iconPath = iconPath;
            _copy.category = category;
            _copy.directRefIcon = directRefIcon;
            _copy.iconLoadMethod = iconLoadMethod;
            _copy.permanent = permanent;
            _copy.duration = duration;
            _copy.layered = layered;
            _copy.maxLayer = maxLayer;
            _copy.canBeExtended = canBeExtended;
            _copy.canBeRefreshed = canBeRefreshed;
            _copy.canBeDispelled = canBeDispelled;
            _copy.backGroundColor = backGroundColor;
            _copy.frameColor = frameColor;
#if MASTER_COMBAT_CORE
            _copy.DesignGraph = DesignGraph;
            _copy.DynamicVariables = new List<DynamicFloat>();
            for (int i = 0; i < DynamicVariables.Count; i++)
            {
                _copy.DynamicVariables.Add(DynamicVariables[i].Copy());
            }
#endif
            _copy.mCustomField = new List<CustomField>();
            for (int i=0;i< mCustomField.Count;i++) {
                _copy.mCustomField.Add(mCustomField[i].Copy());
            }
            _copy.fold = fold;
            return _copy;
        }
        /// <summary>
        /// Retrieves a custom data object based on its key and expected type. This allows flexibility for storing and accessing custom data associated with this effect.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T GetCustomObject<T>(string key) where T : UnityEngine.Object
        {
            foreach (var obj in mCustomField) {
                if (obj.key == key) return obj.GetObject<T>();
            }
            return null;
        }
        #endregion
    }

    /// <summary>
    /// OverTimeEffectData represents the runtime data for an over-time effect (e.g., DoT, buffs, debuffs) currently active on an entity during combat.
    /// </summary>
    [System.Serializable]
    public class OverTimeEffectData
    {
        /// <summary>
        /// The unique string identifier (UID) that links to the OverTimeEffect setting. This allows the system to track and reference the effect¡¯s settings.
        /// </summary>
        public string uid;
        /// <summary>
        /// The remaining time for this effect to expire. The timer counts down until the effect ends or is removed.
        /// </summary>
        public float timer = 0F;
        /// <summary>
        /// The stack layer count of this over-time effect. For effects that can stack (e.g., poison, fire), this represents the number of active stacks. Each layer may increase the intensity or duration of the effect.
        /// </summary>
        public int layer = 0;
        /// <summary>
        /// The dealer¡¯s UID ¡ª the entity responsible for applying this over-time effect. This could be the attacker in the case of DoT or the caster for a buff/debuff.
        /// </summary>
        public string dealer;

        /// <summary>
        /// Retrieves a copy of the data.
        /// </summary>
        /// <returns></returns>
        public OverTimeEffectData Copy()
        {
            OverTimeEffectData _copy = new OverTimeEffectData();
            _copy.uid = uid;
            _copy.timer = timer;
            _copy.layer = layer;
            _copy.dealer = dealer;
            return _copy;
        }

        /// <summary>
        /// OverTimeEffectData represents the runtime data for an over-time effect (e.g., DoT, buffs, debuffs) currently active on an entity during combat.
        /// </summary>
        public OverTimeEffectData(){}

        /// <summary>
        /// OverTimeEffectData represents the runtime data for an over-time effect (e.g., DoT, buffs, debuffs) currently active on an entity during combat.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_timer"></param>
        /// <param name="_dealer"></param>
        public OverTimeEffectData(string _uid, float _timer, string _dealer) {
            uid = _uid;
            timer = _timer;
            dealer = _dealer;
            layer = 1;
        }

    }

}
