using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    [System.Serializable]
    public class Arg
    {
        public string uid;
        public object mValue;
        public Arg(string _uid, object _value)
        {
            uid = _uid;
            mValue = _value;
        }
    }

    public delegate void JobCallback(ref SoftKittyJob _job);



    [System.Serializable]
    public class SoftKittyJob
    {
        public string uid;
        public int _skip = 0;
        public JobCallback callBack;
        public List<Arg> args = new List<Arg>();
        private Dictionary<string, Arg> argDic = new Dictionary<string, Arg>();
        private bool inited = false;

        public SoftKittyJob(string _uid, JobCallback _target, params Arg[] _args)
        {
            _uid = uid;
            _skip = 0;
            callBack = _target;
            args.AddRange(_args);
        }

        public object GetArg(string _uid)
        {
            if (!inited)
            {
                argDic.Clear();
                foreach (var obj in args)
                {
                    argDic.Add(obj.uid, obj);
                }
                inited = true;
            }
            return argDic[_uid].mValue;
        }

        public void SetArg(string _uid, int _value)
        {
            if (!inited)
            {
                argDic.Clear();
                foreach (var obj in args)
                {
                    argDic.Add(obj.uid, obj);
                }
                inited = true;
            }
             argDic[_uid].mValue= _value;
        }

        public void Skip(int _frame)
        {
            _skip = _frame;
        }

    }

    public class SoftKittyJobObj : MonoBehaviour
    {
        public SoftKittyJob mJob;

        private void Update()
        {
            if (mJob.callBack == null)
            {
                if (!SoftKittyJobs.EndJob(mJob.uid))
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                if (mJob._skip > 0)
                {
                    mJob._skip--;
                }
                else
                {
                    mJob.callBack(ref mJob);
                }
            }
        }

    }


    public class SoftKittyJobs
    {
        public static Dictionary<string, SoftKittyJobObj> jobDic = new Dictionary<string, SoftKittyJobObj>();

        public static bool isJobExist(string _uid)
        {
            return jobDic.ContainsKey(_uid);
        }
        public static void StartJob(string _uid, JobCallback _callback, params Arg[] _args)
        {
            GameObject _newJobGameObject = new GameObject("SoftKittyJob_" + _uid);
            SoftKittyJob _newJob = new SoftKittyJob(_uid, _callback, _args);
            SoftKittyJobObj _jobObj = _newJobGameObject.AddComponent<SoftKittyJobObj>();
            _jobObj.mJob = _newJob;
            if (jobDic.ContainsKey(_uid))
            {
                jobDic[_uid].mJob = _newJob;
            }
            else
            {
                jobDic.Add(_uid, _jobObj);
            }
        }

        public static bool EndJob(string _uid)
        {
            if (jobDic.ContainsKey(_uid))
            {
                GameObject.Destroy(jobDic[_uid].gameObject);
                jobDic.Remove(_uid);
                return true;
            }
            return false;
        }

    }
}
