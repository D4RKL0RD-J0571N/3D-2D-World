using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace SoftKitty
{
    /// <summary>
    /// EntityComponent is a behavior class attached to a GameObject in Unity, responsible for linking entity data with a game entity's behavior. It manages the entity's interactions and updates in the game world.
    /// </summary>
    public class EntityComponent : MonoBehaviour
    {

        #region Variables
        /// <summary>
        /// A reference to the entity's data.
        /// </summary>
        public Entity mData
        {
            get
            {
                if (!SGD_Settings.isRuntime)
                {
                    return GameManager.GetEntity(uid);
                }
                if (id == -1) id = GameManager.EntityManagerData.GetId(uid);
                if (GameManager.GetEntity(id) == null)
                {
                    return null;
                }
                if (GameManager.GetEntity(id).MultipleInstances)
                {
                    if (_multipleInstanceData == null)_multipleInstanceData = GameManager.GetEntity(id).Copy();
                    return _multipleInstanceData;
                }
                else
                {
                    if (_data == null) _data = GameManager.GetEntity(id);
                    return _data;
                }
             
            }
        }
        /// <summary>
        /// The unique string ID for this entity, which connects it to its corresponding entity data.
        /// </summary>
        public string uid;
        /// <summary>
        /// Returns whether this instance represents the player entity.
        /// </summary>
        public bool isPlayer { get {  return uid.ToLower() == "player"; } }
        /// <summary>
        /// Returns whether this entity is currently engaged in some action or interaction.
        /// </summary>
        public bool Engage = false;
        /// <summary>
        /// Returns the target entity that this entity is currently engaging with.
        /// </summary>
        [SerializeReference]
        public EntityComponent EngagedEntity = null;
        /// <summary>
        /// Determines whether this entity can be interacted with or controlled. When set to false, all calculations and controls stop, such as for a dead character.
        /// </summary>
        public bool AvailableForInteraction
        {
            get
            {
                if (mData == null) return false;
                return mData.AvailableForInteraction;
            }
            set
            {
                if (mData != null) mData.AvailableForInteraction = value;
            }
        }
        /// <summary>
        /// The CharacterController component attached on this Entity.
        /// </summary>
        public CharacterController MyCharacterController
        {
            get
            {
                if (mControler == null)
                {
                    if (GetComponent<CharacterController>()) 
                        mControler = GetComponent<CharacterController>();
                    else if(GetComponentInParent<CharacterController>())
                        mControler = GetComponentInParent<CharacterController>();
                }
                return mControler;
            }
        }
        /// <summary>
        /// The Rigidbody component attached on this Entity.
        /// </summary>
        public Rigidbody MyRigidbody
        {
            get
            {
                if (mRigidbody == null)
                {
                    if (GetComponent<Rigidbody>())
                        mRigidbody = GetComponent<Rigidbody>();
                    else if (GetComponentInParent<Rigidbody>())
                        mRigidbody = GetComponentInParent<Rigidbody>();
                }
                return mRigidbody;
            }
        }
        /// <summary>
        /// The position of the entity in the world space.
        /// </summary>
        public Vector3 Position
        {
            get
            {
                return transform.position;
            }
        }
        /// <summary>
        /// The forward direction of the entity. Used instead of rotation since, in many calculations, only the forward direction matters. The up direction can be inferred from the forward vector.
        /// </summary>
        public Vector3 Forward
        {
            get
            {
                return transform.forward;
            }
        }
        /// <summary>
        /// The local scale of the entity.
        /// </summary>
        public Vector3 LocalScale
        {
            get
            {
                return transform.localScale;
            }
        }

        #endregion


        #region MonoBehaviour
        public virtual void OnDestroy()
        {
            GameManager.RemoveEntityInstance(mData.uid, this);
        }
        public virtual void Awake()
        {
            if (id == -1) id = GameManager.EntityManagerData.GetId(uid);
            RefreshData();
            if (!string.IsNullOrEmpty(uid))
            {
                ApplyData();
                GameManager.SetEntityInstance(uid, this);
            }
            inited = true;
        }
        public virtual void Update()
        {
            if (!inited) return;
            if (GameManager.OverTimeEffectData!=null)
            {
                internalTimer += Time.deltaTime;
                if (internalTimer >= GameManager.OverTimeEffectData.OverTimeEffectInterval)
                {
                    if (mData != null) mData.UpdateOverTimeEffect(internalTimer);
                    internalTimer = 0F;
                }
            }
            UpdateData();
        }
        #endregion

        #region Internal Methods
        public bool UiFold = false; //=====Editor UI

        [System.NonSerialized] protected bool inited = false;
        [SerializeReference, System.NonSerialized] protected Entity _data = null;
        [HideInInspector]
        public Entity _multipleInstanceData = null;
        private int id = -1;
        private CharacterController mControler;
        private Rigidbody mRigidbody;
        private float internalTimer = 0F;
        IEnumerator RotateCo(Vector3 _angleOffset, float _duration, bool _worldSpace)
        {
            float _timer = 0F;
            while (_timer < _duration)
            {
                if (_worldSpace)
                {
                    transform.localEulerAngles += _angleOffset * Time.deltaTime / _duration;
                }
                else
                {
                    transform.eulerAngles += _angleOffset * Time.deltaTime / _duration;
                }
                yield return 1;
                _timer += _duration * Time.deltaTime;
            }
        }
        IEnumerator MoveCo(Vector3 _positionOffset, float _duration, bool _worldSpace, bool _stickToGround)
        {
            float _timer = 0F;
            while (_timer < _duration)
            {
                yield return new WaitForFixedUpdate();
                if (MyCharacterController)
                {
                    MyCharacterController.Move((_worldSpace ? _positionOffset : transform.InverseTransformPoint(_positionOffset)) * Time.fixedDeltaTime / _duration);
                }
                else if (MyRigidbody)
                {
                    MyRigidbody.MovePosition(transform.position + (_worldSpace ? _positionOffset : transform.InverseTransformPoint(_positionOffset)) * Time.fixedDeltaTime / _duration);
                }
                else
                {
                    Vector3 _pos = transform.position + (_worldSpace ? _positionOffset : transform.InverseTransformPoint(_positionOffset)) * Time.fixedDeltaTime / _duration;
                    if (_stickToGround)
                    {
                        RaycastHit hit;
                        if (Physics.Raycast(transform.position, Vector3.down, out hit, 500f, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
                        {
                            _pos.y = hit.point.y + 0.01f;
                        }
                    }
                    transform.position = _pos;
                }
                yield return new WaitForEndOfFrame();
                _timer += _duration * Time.fixedDeltaTime;
            }
        }
        IEnumerator ScaleCo(Vector3 _target, float _duration)
        {
            float _timer = 0F;
            while (_timer < _duration)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _target, Time.deltaTime / _duration);
                yield return 1;
                _timer += _duration * Time.deltaTime;
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Retrieve an extension Module by its type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModule<T>() where T : EntityModule
        {
            if (mData == null) return null;
            return mData.GetModule<T>();
        }


        /// <summary>
        /// Returns whether the entity has a matching tag with the provided tag string.
        /// </summary>
        /// <param name="_filter"></param>
        /// <returns></returns>
        public bool hasMatchingTag(string _filter)
        {
            if (mData != null)
            {
                return mData.hasMatchingTag(_filter);
            }
            return false;
        }
        /// <summary>
        /// Returns whether the entity has a matching tag from the provided list of tag strings.
        /// </summary>
        /// <param name="_filters"></param>
        /// <returns></returns>
        public bool hasMatchingTag(List<string> _filters)
        {
            foreach (var obj in _filters) {
                if (hasMatchingTag(obj)) return true;
            }
            return false;
        }

        /// <summary>
        /// Relink the Entity data with database.
        /// </summary>
        public void RefreshData()
        {
            _data = null;
            _multipleInstanceData = null;
        }

        /// <summary>
        /// Registers a callback for changes to the entity's attributes.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterAttributeChangeCallback(AttributeChangeEvent _callback)
        {
             mData.RegisterAttributeChangeCallback(_callback);
        }
        /// <summary>
        /// Unregisters a previously registered attribute change callback.
        /// </summary>
        /// <param name="_callback"></param>
        public void UnRegisterAttributeChangeCallback(AttributeChangeEvent _callback)
        {
            mData.UnRegisterAttributeChangeCallback(_callback);
        }
        /// <summary>
        /// Clears all registered attribute change callbacks.
        /// </summary>
        public void ClearAttributeChangeCallback()
        {
            mData.ClearAttributeChangeCallback();
        }
        /// <summary>
        /// Registers a callback for entity events.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterEntityEventCallback(EntityEvent _callback)
        {
            mData.RegisterEntityEventCallback(_callback);
        }
        /// <summary>
        /// Unregisters a previously registered entity event callback.
        /// </summary>
        /// <param name="_callback"></param>
        public void UnRegisterEntityEventCallback(EntityEvent _callback)
        {
            mData.UnRegisterEntityEventCallback(_callback);
        }
        /// <summary>
        /// Clears all registered entity event callbacks.
        /// </summary>
        public void ClearEntityEventCallback()
        {
            mData.ClearEntityEventCallback();
        }
        /// <summary>
        /// Registers a callback for over-time effect changes on this entity.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterOverTimeChangeCallback(OverTimeEffectChangeEvent _callback)
        {
            mData.RegisterOverTimeChangeCallback(_callback);
        }
        /// <summary>
        /// Unregisters a previously registered over-time effect change callback.
        /// </summary>
        /// <param name="_callback"></param>
        public void UnRegisterOverTimeChangeCallback(OverTimeEffectChangeEvent _callback)
        {
            mData.UnRegisterOverTimeChangeCallback(_callback);
        }
        /// <summary>
        /// Clears all registered over-time effect change callbacks.
        /// </summary>
        public void ClearOverTimeChangeCallback()
        {
            mData.ClearOverTimeChangeCallback();
        }
        /// <summary>
        /// Applies entity data from the database to this instance, such as position, rotation, and scale. Developers can override this for extra data specific to their needs.
        /// </summary>
        public virtual void ApplyData() {
            if (mData == null) return;
            if (mData.MultipleInstances || !mData.ApplyTransformDataWhenInstantiate) return;
            if (MyCharacterController) MyCharacterController.enabled = false;
            transform.position = mData.Position;
            transform.rotation = Quaternion.LookRotation(mData.Forward, Vector3.up);
            transform.localScale = mData.Scale;
            if (MyCharacterController) MyCharacterController.enabled = true;
        }
        /// <summary>
        /// Updates the entity data to the database, including position, rotation, and scale. Developers can override this for extra data updates.
        /// </summary>
        public virtual void UpdateData()
        {
            if (mData == null) return;
            mData.Position = transform.position;
            mData.Forward = transform.forward;
            mData.Scale = transform.localScale;
        }


        /// <summary>
        /// Adds an over-time effect by its string UID and the dealer entity. Returns the OverTimeEffectData.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public OverTimeEffectData AddOverTimeEffect(string _uid, Entity _dealer)
        {
            if (mData == null) return null;
            return mData.AddOverTimeEffect(_uid, _dealer);
        }
        /// <summary>
        /// Dispels an over-time effect by its string UID. The success depends on the settings, and it only removes one layer of the effect.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public OverTimeEffectData DispellOverTimeEffect(string _uid)
        {
            if (mData == null) return null;
            return mData.DispellOverTimeEffect(_uid);
        }
        /// <summary>
        /// Completely removes an over-time effect by its string UID, ensuring success and removing all layers.
        /// </summary>
        /// <param name="_uid"></param>
        public void RemoveOverTimeEffect(string _uid)
        {
            if (mData == null) return;
            mData.RemoveOverTimeEffect(_uid);
        }

        /// <summary>
        /// Sets the entity's position in world space. If _stickToGround is true, it will adjust the Y-axis to match the ground height.
        /// </summary>
        /// <param name="_pos"></param>
        /// <param name="_stickToGround"></param>
        public void SetPosition(Vector3 _pos, bool _stickToGround = true)
        {
            if (_stickToGround)
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position, Vector3.down, out hit, 500f, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
                {
                    _pos.y = hit.point.y + 0.01f;
                }
            }
            transform.position = _pos;
        }
        /// <summary>
        /// Sets the euler angles in world space or local space.
        /// </summary>
        /// <param name="_angle"></param>
        /// <param name="_worldSpace"></param>
        public void SetEulerAngles(Vector3 _angle,bool _worldSpace=true)
        {
            if (_worldSpace)
            {
                transform.eulerAngles = _angle;
            }
            else
            {
                transform.localEulerAngles = _angle;
            }
        }
        /// <summary>
        /// Sets the scale in local space.
        /// </summary>
        /// <param name="_scale"></param>
        public void SetScale(Vector3 _scale)
        {
            transform.localScale = _scale;
        }
        /// <summary>
        /// Moves the entity over a specified duration, optionally sticking to the ground.
        /// </summary>
        /// <param name="_positionOffset"></param>
        /// <param name="_duration"></param>
        /// <param name="_worldSpace"></param>
        /// <param name="_stickToGround"></param>
        public void Move(Vector3 _positionOffset, float _duration,bool _worldSpace,bool _stickToGround=true)
        {
            StartCoroutine(MoveCo(_positionOffset,Mathf.Max(Time.fixedDeltaTime, _duration), _worldSpace, _stickToGround));
        }

        /// <summary>
        /// Rotates the entity over a specified duration.
        /// </summary>
        /// <param name="_angleOffset"></param>
        /// <param name="_duration"></param>
        /// <param name="_worldSpace"></param>
        public void Rotate(Vector3 _angleOffset, float _duration, bool _worldSpace)
        {
            StartCoroutine(RotateCo(_angleOffset, Mathf.Max(Time.deltaTime, _duration), _worldSpace));
        }

        /// <summary>
        /// Scales the entity over a specified duration.
        /// </summary>
        /// <param name="_scale"></param>
        /// <param name="_duration"></param>
        public void Scale(Vector3 _scale, float _duration)
        {
            StartCoroutine(ScaleCo(_scale, Mathf.Max(Time.deltaTime, _duration)));
        }

        /// <summary>
        /// Retrieves a custom float value for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public float GetCustomFloat(string _uid)
        {
            if (mData == null) return 0F;
            return mData.GetCustomFloat(_uid);
        }
        /// <summary>
        /// Retrieves a custom integer value for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public int GetCustomInt(string _uid)
        {
            if (mData == null) return 0;
            return mData.GetCustomInt(_uid);
        }
        /// <summary>
        /// Retrieves a custom boolean value for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public bool GetCustomBool(string _uid)
        {
            if (mData == null) return false;
            return mData.GetCustomBool(_uid);
        }
        /// <summary>
        /// Retrieves a custom string value for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public string GetCustomString(string _uid)
        {
            if (mData == null) return "";
            return mData.GetCustomString(_uid);
        }
        /// <summary>
        /// Retrieves a custom integer list for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public IntList GetCustomIntList(string _uid)
        {
            if (mData == null) return new IntList();
            return mData.GetCustomIntList(_uid);
        }
        /// <summary>
        /// Retrieves a custom ID-to-integer list for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public IdIntList GetCustomIdIntList(string _uid)
        {
            if (mData == null) return new IdIntList();
            return mData.GetCustomIdIntList(_uid);
        }
        /// <summary>
        /// Retrieves a custom ID-to-float list for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public IdFloatList GetCustomIdFloatList(string _uid)
        {
            if (mData == null) return new IdFloatList();
            return mData.GetCustomIdFloatList(_uid);
        }
        /// <summary>
        /// Overrides the custom float value for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetCustomFloat(string _uid, float _value)
        {
            if (mData == null) return;
            mData.SetCustomFloat(_uid, _value);
        }
        /// <summary>
        /// Overrides the custom integer value for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetCustomInt(string _uid, int _value)
        {
            if (mData == null) return;
            mData.SetCustomInt(_uid, _value);
        }
        /// <summary>
        /// Overrides the custom boolean value for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetCustomBool(string _uid, bool _value)
        {
            if (mData == null) return;
            mData.SetCustomBool(_uid, _value);
        }
        /// <summary>
        /// Overrides the custom string value for the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetCustomString(string _uid, string _value)
        {
            if (mData == null) return;
            mData.SetCustomString(_uid, _value);
        }
        /// <summary>
        /// Retrieves the attribute associated with the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public Attribute GetAttribute(string _uid)
        {
            return GameManager.GetAttribute(_uid);
        }
        /// <summary>
        /// Retrieves the attribute data associated with the entity by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public AttributeData GetAttributeData(string _uid)
        {
            if (mData == null) return null;
            return mData.GetAttributeData(_uid);
        }
        /// <summary>
        /// Retrieves the float value of an attribute by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public float GetAttributeFloat(string _uid)
        {
            if (mData == null) return 0F;
            return mData.GetAttributeFloat(_uid);
        }
        /// <summary>
        /// Retrieves the string value of an attribute by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public string GetAttributeString(string _uid)
        {
            if (mData == null) return "";
            return mData.GetAttributeString(_uid);
        }
        /// <summary>
        /// Retrieves the integer value of an attribute by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public int GetAttributeInt(string _uid)
        {
            if (mData == null) return 0;
            return mData.GetAttributeInt(_uid);
        }
        /// <summary>
        /// Sets the value of an attribute by its unique UID (float).
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <param name="_dealer"></param>
        public void SetAttributeValue(string _uid, float _value, Entity _dealer=null)
        {
            if (mData == null) return;
            mData.GetAttributeData(_uid).SetValue(_value, _dealer);
        }
        /// <summary>
        /// Sets the value of an attribute by its unique UID (string).
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetAttributeValue(string _uid, string _value)
        {
            if (mData == null) return;
            mData.GetAttributeData(_uid).SetValue(_value);
        }
        /// <summary>
        /// Sets the value of an attribute by its unique UID (integer).
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <param name="_dealer"></param>
        public void SetAttributeValue(string _uid, int _value, Entity _dealer = null)
        {
            if (mData == null) return;
            mData.GetAttributeData(_uid).SetValue(_value, _dealer);
        }
        /// <summary>
        /// Sets the value of an attribute by its integer ID (float).
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <param name="_dealer"></param>
        public void SetAttributeValue(int _id, float _value, Entity _dealer = null)
        {
            if (mData == null) return;
            mData.GetAttributeData(_id).SetValue(_value, _dealer);
        }
        /// <summary>
        /// Sets the value of an attribute by its integer ID (string).
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        public void SetAttributeValue(int _id, string _value)
        {
            if (mData == null) return;
            mData.GetAttributeData(_id).SetValue(_value);
        }
        /// <summary>
        /// Sets the value of an attribute by its integer ID (integer).
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <param name="_dealer"></param>
        public void SetAttributeValue(int _id, int _value, Entity _dealer = null)
        {
            if (mData == null) return;
            mData.GetAttributeData(_id).SetValue(_value, _dealer);
        }
        /// <summary>
        /// Adds a float value to an existing attribute by its unique UID and returns the result.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public float AddAttributeValue(string _uid, float _value, Entity _dealer = null)
        {
            if (mData == null) return 0F;
            return mData.GetAttributeData(_uid).AddValue(_value, _dealer);
        }
        /// <summary>
        /// Adds an integer value to an existing attribute by its unique UID and returns the result.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public int AddAttributeValue(string _uid, int _value, Entity _dealer = null)
        {
            if (mData == null) return 0;
            return mData.GetAttributeData(_uid).AddValue(_value, _dealer);
        }
        /// <summary>
        /// Adds a float value to an existing attribute by its integer ID and returns the result.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public float AddAttributeValue(int _id, float _value, Entity _dealer = null)
        {
            if (mData == null) return 0F;
            return mData.GetAttributeData(_id).AddValue(_value, _dealer);
        }
        /// <summary>
        /// Adds an integer value to an existing attribute by its integer ID and returns the result.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public int AddAttributeValue(int _id, int _value, Entity _dealer = null)
        {
            if (mData == null) return 0;
            return mData.GetAttributeData(_id).AddValue(_value, _dealer);
        }
        /// <summary>
        /// Adds a float value to an existing attribute by its unique UID, clamps the result, and returns the updated value.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <param name="_min"></param>
        /// <param name="_max"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public float AddAttributeValueClamp(string _uid, float _value, float _min, float _max,  Entity _dealer = null)
        {
            if (mData == null) return 0F;
            return mData.GetAttributeData(_uid).AddValueClamp(_value, _min, _max, _dealer);
        }
        /// <summary>
        /// Adds a integer value to an existing attribute by its unique UID, clamps the result, and returns the updated value.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        /// <param name="_min"></param>
        /// <param name="_max"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public int AddAttributeValueClamp(string _uid, int _value, int _min, int _max, Entity _dealer = null)
        {
            if (mData == null) return 0;
            return mData.GetAttributeData(_uid).AddValueClamp(_value, _min, _max, _dealer);
        }
        /// <summary>
        /// Adds a float value to an existing attribute by its integer ID, clamps the result, and returns the updated value.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <param name="_min"></param>
        /// <param name="_max"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public float AddAttributeValueClamp(int _id, float _value, float _min, float _max, Entity _dealer = null)
        {
            if (mData == null) return 0F;
            return mData.GetAttributeData(_id).AddValueClamp(_value, _min, _max, _dealer);
        }
        /// <summary>
        /// Adds a integer value to an existing attribute by its integer ID, clamps the result, and returns the updated value.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_value"></param>
        /// <param name="_min"></param>
        /// <param name="_max"></param>
        /// <param name="_dealer"></param>
        /// <returns></returns>
        public int AddAttributeValueClamp(int _id, int _value, int _min, int _max, Entity _dealer = null)
        {
            if (mData == null) return 0;
            return mData.GetAttributeData(_id).AddValueClamp(_value, _min, _max, _dealer);
        }


        #endregion 
    }
}
