using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty.MasterNavigationMap
{
    public class MapCreatorTag : MonoBehaviour
    {
        public bool _water = false;
        public string _tag;

        public void Bake()
        {
#if UNITY_EDITOR
            if (GetComponent<SkinnedMeshRenderer>() && GetComponent<MeshCollider>())
            {
                Mesh colliderMesh = new Mesh();
                GetComponent<SkinnedMeshRenderer>().BakeMesh(colliderMesh, true);
                GetComponent<MeshCollider>().sharedMesh = null;
                GetComponent<MeshCollider>().sharedMesh = colliderMesh;
            }
#endif
        }
    }
}
