using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using Microsoft.SqlServer.Server;
using System.Linq;

namespace SoftKitty
{
    public class MeshPreview : UnityEditor.EditorWindow
    {
        Editor gameObjectEditor;
        static GameObject gameObject;

        public static Renderer renderer;
        public static MeshPreview instance;
        private static Mesh oriMesh;
        private static Material oriMat;
        private static Mesh newMesh;
        private static Material newMat;
        private static Vector3 oriScale;
        private static string OriMatPath;
        private bool pendingUpdate = false;

        public Editor GetEditor()
        {
            return gameObjectEditor;
        }

        public bool CheckMatChange(string _matPath)
        {
            if (_matPath == OriMatPath) return false;
            if (newMat != null) DestroyImmediate(newMat);
            
            newMat = Instantiate(Resources.Load<Material>(_matPath));
            renderer.material = newMat;
            renderer.sharedMaterial = newMat;
            OriMatPath = _matPath;
            return true;
        }

        public static void ShowPreview(string _meshPath, string _matPath, string _path)
        {
            if (newMesh!=null)  DestroyImmediate(newMesh);
            if (newMat != null) DestroyImmediate(newMat);

            gameObject = (GameObject)AssetDatabase.LoadAssetAtPath(_path, typeof(GameObject));
            if (_meshPath != "")
            {
                MeshFilter _mf = gameObject.GetComponentInChildren<MeshFilter>();
                newMesh = Instantiate(Resources.Load<Mesh>(_meshPath));
                oriMesh = _mf.sharedMesh;
                _mf.mesh = newMesh;
                _mf.sharedMesh = newMesh;
            }
            else
            {
                if (gameObject.GetComponentInChildren<MeshFilter>())
                {
                    MeshFilter _mf = gameObject.GetComponentInChildren<MeshFilter>();
                    newMesh = Instantiate(_mf.sharedMesh);
                    oriMesh = _mf.sharedMesh;
                    _mf.mesh = newMesh;
                    _mf.sharedMesh = newMesh;
                } 
           
            }
            if (_matPath != "")
            {
                renderer = gameObject.GetComponentInChildren<MeshRenderer>();
                newMat = Instantiate(Resources.Load<Material>(_matPath));
                oriMat = renderer.sharedMaterial;
                oriScale = renderer.transform.localScale;
                renderer.material = newMat;
                renderer.sharedMaterial = newMat;
                OriMatPath = _matPath;
            }
            else
            {
                if (gameObject.GetComponentInChildren<MeshFilter>())
                {
                    renderer = gameObject.GetComponentInChildren<MeshRenderer>();
                    newMat = Instantiate(renderer.sharedMaterial);
                    oriMat = renderer.sharedMaterial;
                    oriScale = renderer.transform.localScale;
                    renderer.material = newMat;
                    renderer.sharedMaterial = newMat;
                    OriMatPath = AssetDatabase.GetAssetPath(oriMat);
                }
                else if (gameObject.GetComponentInChildren<SkinnedMeshRenderer>())
                {
                    renderer = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
                    newMat = Instantiate(renderer.sharedMaterial);
                    oriMat = renderer.sharedMaterial;
                    oriScale = renderer.transform.localScale;
                    renderer.material = newMat;
                    renderer.sharedMaterial = newMat;
                    OriMatPath = AssetDatabase.GetAssetPath(oriMat);
                }
            }
            instance=GetWindowWithRect<MeshPreview>(new Rect(0, 0, 256, 256));
        }

        private void UpdatePreview()
        {
            MeshPreview.instance.GetEditor().ReloadPreviewInstances();
            MeshPreview.instance.Focus();
            pendingUpdate = false;
        }

        public void RequstUpdate(bool _changed)
        {
            if (_changed) pendingUpdate = true;
            if(!Resources.FindObjectsOfTypeAll<EditorWindow>().Any(w => w.GetType().Name == "ColorPicker") && pendingUpdate)
            {
                UpdatePreview();
            }
        }

        private void OnDestroy()
        {
            if (newMesh != null) DestroyImmediate(newMesh);
            if (newMat != null) DestroyImmediate(newMat);
            newMesh = null;
            newMat = null;
            if (renderer!=null)
            {
                renderer.material = oriMat;
                renderer.sharedMaterial = oriMat;
                OriMatPath = AssetDatabase.GetAssetPath(oriMat);
                renderer.transform.localScale= oriScale;
                if (renderer.GetComponent<MeshFilter>())
                {
                    renderer.GetComponent<MeshFilter>().mesh = oriMesh;
                    renderer.GetComponent<MeshFilter>().sharedMesh = oriMesh;
                }
                
            }
            oriMesh = null;
            oriMat = null;
            renderer = null;
        }

        void OnGUI()
        {
            instance = this;
           
            GUIStyle bgColor = new GUIStyle();

            if (gameObjectEditor == null)
            {
                gameObjectEditor = Editor.CreateEditor(gameObject);
            }
            gameObjectEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(256, 256), bgColor);
        }

    }
}
