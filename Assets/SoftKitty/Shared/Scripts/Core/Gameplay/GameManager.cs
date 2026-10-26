using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace SoftKitty
{
    /// <summary>
    /// GameManager is a global static class responsible for managing data in memory and handling instances of EntityComponent within the scene. It provides access to entities, attributes, over-time effects, and convenient utilities for sound and object instantiation.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Internal Variables & Methods
        public delegate void RefreshEvent();
        private static EntityManagerObject _entityManagerData = null;
        private static AttributeObject _attributeData = null;
        private static OverTimeEffectObject _overTimeEffectData = null;
        private static Entity player = null;
        private static EntityComponent playerInstance;
        private static Dictionary<string,List<EntityComponent>> entityInstanceDic = new Dictionary<string, List<EntityComponent>>();
        private static AudioSource mAudioPlayer;
        public static RefreshEvent RefreshCallback;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            ClearAllEntityInstance();
            _entityManagerData = null;
            _attributeData = null;
            _overTimeEffectData = null;
            RefreshCallback -= Refresh;
            RefreshCallback += Refresh;
            RefreshCallback();
        }

        public static void Refresh()
        {
            player = null;
            playerInstance = null;
        }


        private static AudioSource AudioPlayer
        {
            get
            {
                if (mAudioPlayer == null)
                {
                    GameObject _newObj = new GameObject("GameManager_AudioPlayer");
                    mAudioPlayer = _newObj.AddComponent<AudioSource>();
                    mAudioPlayer.spatialBlend = 0F;
                    mAudioPlayer.playOnAwake = false;
                    mAudioPlayer.volume = SGD_Settings.Instance.VolumeMultiplier;
                    mAudioPlayer.priority = SGD_Settings.Instance.AudioPriority;
                    mAudioPlayer.loop = false;
                }
                return mAudioPlayer;
            }
        }
        #endregion


        /// <summary>
        /// Return the full path by providing the sub path with the file name.
        /// </summary>
        /// <param name="_fileName"></param>
        /// <returns></returns>
        public static string GetFullSavePath(string _fileName)
        {
            string _root = "";
            _root = Application.dataPath + "/../";
            string _path = _root + "SaveData/" + _fileName;
            if (Path.IsPathRooted(_fileName)) _path = _fileName;
            string _dirPath = _path.Replace(Path.GetFileName(_fileName), "");
            if (!Directory.Exists(_dirPath)) Directory.CreateDirectory(_dirPath);//There might be some sub folder is missing within the save path, create them when needed.
            return _path;
        }

        /// <summary>
        /// Returns the entity manager data object. It returns a copy in memory during runtime and links to the actual ScriptableObject in editor mode.
        /// </summary>
        public static EntityManagerObject EntityManagerData
        {
            get
            {
                if(!SGD_Settings.isRuntime)return SGD_Settings.Instance.GetData<EntityManagerObject>();
                if (_entityManagerData==null) _entityManagerData= SGD_Settings.Instance.GetData<EntityManagerObject>();
                return _entityManagerData;
            }
        }
        /// <summary>
        /// Returns the attribute object. It returns a copy in memory during runtime and links to the actual ScriptableObject in editor mode.
        /// </summary>
        public static AttributeObject AttributeData
        {
            get
            {
                if (!SGD_Settings.isRuntime) return SGD_Settings.Instance.GetData<AttributeObject>();
                if (_attributeData == null) _attributeData= SGD_Settings.Instance.GetData<AttributeObject>();
                return _attributeData;
            }
        }
        /// <summary>
        /// Returns the over-time effects object. It returns a copy in memory during runtime and links to the actual ScriptableObject in editor mode.
        /// </summary>
        public static OverTimeEffectObject OverTimeEffectData
        {
            get
            {
                if (!SGD_Settings.isRuntime) return SGD_Settings.Instance.GetData<OverTimeEffectObject>();
                if (_overTimeEffectData == null) _overTimeEffectData= SGD_Settings.Instance.GetData<OverTimeEffectObject>();
                return _overTimeEffectData;
            }
        }

        /// <summary>
        /// Delete all registered EntityComponents in the scene from the manager.
        /// </summary>
        public static void DeleteAllEntityInstance(bool _includePlayer=true)
        {
            foreach (var key in entityInstanceDic.Keys)
            {
                try
                {
                    if (entityInstanceDic[key] != null)
                    {
                        foreach (var obj in entityInstanceDic[key]) {
                            if(obj!=null)Destroy(obj.gameObject);
                        }
                        entityInstanceDic[key].Clear();
                    }
                }
                catch
                {

                }
            }
            entityInstanceDic.Clear();
            if (_includePlayer)
            {
                if (playerInstance != null) Destroy(playerInstance.gameObject);
                player = null;
                playerInstance = null;
            }
        }

        /// <summary>
        /// Clear all registered EntityComponents from the manager without verifying their deletion.
        /// </summary>
        public static void ClearAllEntityInstance()
        {
            entityInstanceDic.Clear();
            player = null;
            playerInstance = null;
        }
        /// <summary>
        /// Add an EntityComponent in the scene to the manager.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_entity"></param>
        public static void SetEntityInstance(string _uid, EntityComponent _entity)
        {
            if (entityInstanceDic.ContainsKey(_uid))
            {
                if (entityInstanceDic[_uid] == null)entityInstanceDic[_uid] = new List<EntityComponent>();
                entityInstanceDic[_uid].Add(_entity);
            }
            else
            {
                List<EntityComponent> _newList = new List<EntityComponent>();
                _newList.Add(_entity);
                entityInstanceDic.Add(_uid, _newList);
            }
            if (_uid.ToLower() == "player" || _entity.isPlayer)
            {
                playerInstance = _entity;
            }
        }
        /// <summary>
        /// Remove an EntityComponent in the scene from the manager.
        /// </summary>
        /// <param name="_uid"></param>
        public static void RemoveEntityInstance(string _uid, EntityComponent _instance)
        {
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<GameManager> Remove Entity Instance:" + _uid);
            if (entityInstanceDic.ContainsKey(_uid))
            {
                if (entityInstanceDic[_uid]!=null) {
                    if (entityInstanceDic[_uid].Contains(_instance)) entityInstanceDic[_uid].Remove(_instance);
                    if (entityInstanceDic[_uid].Count == 0) entityInstanceDic.Remove(_uid);
                }
            }
        }
        /// <summary>
        /// Retrieves the EntityComponent instance in the scene by its unique UID and index (optional).
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public static EntityComponent GetEntityInstance(string _uid, int _index=0)
        {
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<GameManager> Get Entity Instance:" + _uid);
            if (entityInstanceDic.ContainsKey(_uid))
            {
                if (entityInstanceDic[_uid] != null && entityInstanceDic[_uid].Count> _index)
                    return entityInstanceDic[_uid][_index];
            }
            return null;
        }
        
        /// <summary>
        /// Retrieves a list of all EntityComponent instances in the scene.
        /// </summary>
        /// <returns></returns>
        public static List<EntityComponent> GetEntityInstanceList( )
        {
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<GameManager> GetEntityInstanceList(" + entityInstanceDic.Keys.Count+")");
            List<EntityComponent> _list = new List<EntityComponent>();
            foreach (var obj in entityInstanceDic.Values) _list.AddRange(obj);
            return _list;
        }
        /// <summary>
        /// Set the player¡¯s EntityComponent instance to the manager.
        /// </summary>
        /// <param name="_player"></param>
        public static void SetPlayerInstance(EntityComponent _player)
        {
            playerInstance = _player;
        }
        /// <summary>
        /// Retrieves the player¡¯s EntityComponent instance in the scene.
        /// </summary>
        /// <returns></returns>
        public static EntityComponent GetPlayerInstance()
        {
            return playerInstance;
        }


        /// <summary>
        /// Retrieves the player entity data.
        /// </summary>
        /// <returns></returns>
        public static Entity GetPlayer()
        {
            if (player == null && EntityManagerData != null)
                player = EntityManagerData.GetEntity("player");
            return player;
        }
        /// <summary>
        /// Retrieves the entity data by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public static Entity GetEntity(string _uid)
        {
            return EntityManagerData.GetEntity(_uid);
        }
        /// <summary>
        /// Retrieves the entity data by its integer ID.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public static Entity GetEntity(int _id)
        {
            return EntityManagerData.GetEntity(_id);
        }
        /// <summary>
        /// Retrieves the attribute by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public static Attribute GetAttribute(string _uid)
        {
            return AttributeData.GetAttribute(_uid);
        }
        /// <summary>
        /// Retrieves the attribute by its integer ID.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public static Attribute GetAttribute(int _id)
        {
            return AttributeData.GetAttribute(_id);
        }
        /// <summary>
        /// Retrieves the over-time effect by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public static OverTimeEffect GetOverTimeEffect(string _uid)
        {
            return OverTimeEffectData.GetOverTimeEffect(_uid);
        }
        /// <summary>
        /// Retrieves the over-time effect by its integer ID.
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public static OverTimeEffect GetOverTimeEffect(int _id)
        {
            return OverTimeEffectData.GetOverTimeEffect(_id);
        }


        /// <summary>
        /// Instantiates a game object with the specified position, rotation (Euler angles), and scale, and sets its parent.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="parent"></param>
        /// <param name="position"></param>
        /// <param name="eulerAngles"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public static GameObject InstantiateGameObject(GameObject original,Transform parent,Vector3 position,Vector3 eulerAngles,Vector3 scale)
        {
            GameObject _newObj=  Instantiate(original, position, Quaternion.identity, parent);
            _newObj.transform.localEulerAngles = eulerAngles;
            _newObj.transform.localScale = scale;
            return _newObj;
        }
        /// <summary>
        /// Instantiates a game object and sets its parent without specifying position or scale.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static GameObject InstantiateGameObject(GameObject original, Transform parent)
        {
            GameObject _newObj = Instantiate(original,parent);
            return _newObj;
        }
        /// <summary>
        /// Plays a 2D sound at the specified volume.
        /// </summary>
        /// <param name="clip"></param>
        /// <param name="volume"></param>
        public static void PlaySound2D(AudioClip clip, float volume)
        {
            SoundManager.Play2D(clip, volume * SGD_Settings.Instance.VolumeMultiplier);
        }
        /// <summary>
        /// Plays a 3D sound at the specified volume and position.
        /// </summary>
        /// <param name="clip"></param>
        /// <param name="volume"></param>
        /// <param name="position"></param>
        public static void PlaySound3D(AudioClip clip, float volume, Vector3 position)
        {
            SoundManager.Play3D(clip, position, volume * SGD_Settings.Instance.VolumeMultiplier);
        }

    }
}
