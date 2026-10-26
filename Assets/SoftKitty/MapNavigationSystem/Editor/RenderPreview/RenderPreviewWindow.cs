using UnityEditor;
using UnityEngine;
using Unity.EditorCoroutines.Editor;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine.AI;

namespace SoftKitty.MasterNavigationMap
{
    public class RenderPreviewWindow : EditorWindow
    {

        public Texture2D PreviewTexture;
        public float Progress=0F;
        public Color32[] fullCpuPixelBuffer; // Holds ALL pixels on the CPU
        public string ProgressText = "";
        public MapCreator myTarget;
        List<Vector3> ScanBlocks = new List<Vector3>();
        EditorCoroutine mBakingCo;
        bool _previewTextureReady = false;
        float _scanLinePos=1F;
        int _selChannel = 0;

        private UnityEngine.Rendering.ColorWriteMask GetChannel()
        {
            if (_selChannel == 0)
            {
                return UnityEngine.Rendering.ColorWriteMask.All;
            }
            else if (_selChannel == 1)
            {
                return UnityEngine.Rendering.ColorWriteMask.Red;
            }
            else if (_selChannel == 2)
            {
                return UnityEngine.Rendering.ColorWriteMask.Green;
            }
            else if (_selChannel == 3)
            {
                return UnityEngine.Rendering.ColorWriteMask.Blue;
            }
            return UnityEngine.Rendering.ColorWriteMask.All;
        }

        void OnGUI()
        {
            EditorGUI.ProgressBar(new Rect(3, 0, position.width - 3- 100, 20), Progress, ProgressText+" ( "+ Mathf.FloorToInt(Progress * 100F).ToString() + "% )");
            if (myTarget.Preview)
            {
                GUI.backgroundColor = _selChannel == 0 ? Color.white : Color.gray;
                if (GUI.Button(new Rect(position.width - 3 - 96, 0, 36, 20), "RGB"))
                {
                    _selChannel = 0;
                }
                GUI.backgroundColor = _selChannel == 1 ? Color.red : (Color.red * 0.5F);
                if (GUI.Button(new Rect(position.width - 3 - 60, 0, 20, 20), "R"))
                {
                    _selChannel = 1;
                }
                GUI.backgroundColor = _selChannel == 2 ? Color.green : (Color.green * 0.5F);
                if (GUI.Button(new Rect(position.width - 3 - 40, 0, 20, 20), "G"))
                {
                    _selChannel = 2;
                }
                GUI.backgroundColor = _selChannel == 3 ? Color.blue : (Color.blue * 0.5F);
                if (GUI.Button(new Rect(position.width - 3 - 20, 0, 20, 20), "B"))
                {
                    _selChannel = 3;
                }
            }
            else
            {
                GUI.backgroundColor = Color.green;
                if (GUI.Button(new Rect(position.width - 3 - 100, 0, 100, 20), "Cancel"))
                {
                    Close();
                }
            }

            if (myTarget.Preview && PreviewTexture != null && _previewTextureReady)
            {
                float _size = position.width - 3;
                if (_scanLinePos < 1F)
                {
                    EditorGUI.DrawPreviewTexture(new Rect(3, 21, _size, _size), PreviewTexture,null, ScaleMode.ScaleToFit,0F,-1F, GetChannel());
                    EditorGUI.DrawRect(new Rect(3, 21 + _size * (1F - _scanLinePos), _size, 2), Color.white);
                }
                foreach (var obj in ScanBlocks)
                {
                    EditorGUI.DrawRect(new Rect(3F+obj.x/ GetSize(myTarget.Size)* _size, 21F + obj.y/ GetSize(myTarget.Size) * _size, 128F/ GetSize(myTarget.Size) * _size, 128F / GetSize(myTarget.Size) * _size), Color.white*obj.z);
                }
            }

        }

        void OnDestroy()
        {
            if (mBakingCo != null) this.StopCoroutine(mBakingCo);
        }

        IEnumerator StartBakingCo(string _path)
        {
            baking = true;
            _previewTextureReady = false;
            Progress = 0F;
            _scanLinePos = 1F;
            ScanBlocks.Clear();
            if (myTarget.Preview)
            {
                PreviewTexture = new Texture2D(GetSize(myTarget.Size), GetSize(myTarget.Size), TextureFormat.RGB24, false);
                fullCpuPixelBuffer = new Color32[GetSize(myTarget.Size) * GetSize(myTarget.Size)];
                for (int i = 0; i < fullCpuPixelBuffer.Length; i++)
                {
                    fullCpuPixelBuffer[i] = new Color32(0, 0, 0, 1);
                }
                PreviewTexture.SetPixels32(fullCpuPixelBuffer);
                yield return 1;
                PreviewTexture.Apply();
                yield return 1;
            }
            _previewTextureReady = true;
            ProgressText = "Start Rendering...";
            yield return 1;
            Repaint();

            float _progressStep = 1F / (GetSize(myTarget.Size) * 2F);
            foreach (MapCreatorTag obj in myTarget.GetComponentsInChildren<MapCreatorTag>())
            {
                obj.Bake();
            }
            Vector3 _topRight;
            Vector3 _bottomLeft;
            if (myTarget.BakeForSubMap)
            {
                _topRight = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._topRight;
                _bottomLeft = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._bottomLeft;
            }
            else
            {
                _topRight = myTarget.CurrentMap._topRight;
                _bottomLeft = myTarget.CurrentMap._bottomLeft;
            }

            float _stepX = (_topRight.x - _bottomLeft.x) / GetSize(myTarget.Size);
            float _stepY = (_topRight.z - _bottomLeft.z) / GetSize(myTarget.Size);
            float _step = Mathf.Min(_stepX, _stepY);
            _bottomLeft.y = myTarget.Lowest;

            myTarget._ids = new int[GetSize(myTarget.Size), GetSize(myTarget.Size)];
            myTarget._heights = new float[GetSize(myTarget.Size), GetSize(myTarget.Size)];
            myTarget._normals = new Vector3[GetSize(myTarget.Size), GetSize(myTarget.Size)];
            myTarget._outNavmesh = new float[GetSize(myTarget.Size), GetSize(myTarget.Size)];
            NavMeshHit navHit;
            RaycastHit hit;
            float _maxHeight = 0F;
            float _heightRange = myTarget.Height - myTarget.Lowest;
            int _sampleRadius = Mathf.CeilToInt(myTarget.SampleRadius * 1F / GetSizeStep(myTarget.Size));

            Dictionary<int, Color> _customColor = new Dictionary<int, Color>();
            _customColor.Add(1, myTarget.WaterColor);

            for (int i = 0; i < myTarget.CustomColors.Count; i++)
            {
                myTarget.CustomColors[i]._id = i + 2;
                _customColor.Add(myTarget.CustomColors[i]._id, myTarget.CustomColors[i]._color);
            }

            for (int x = 0; x < GetSize(myTarget.Size); x++)
            {
                for (int y = 0; y < GetSize(myTarget.Size); y++)
                {
                    Vector3 _startPos = _bottomLeft + Vector3.forward * y * _step + Vector3.right * x * _step + Vector3.up * _heightRange;
                    bool _water = false;
                    if (Physics.Raycast(_startPos, Vector3.down, out hit, _heightRange + 5F, myTarget.GroundLayer, QueryTriggerInteraction.Ignore))
                    {
                        bool _match = false;
                        string _tag = "";

                        if (myTarget.InNavmeshColor != myTarget.OutsideNavmeshColor)
                        {
                            if (NavMesh.SamplePosition(hit.point, out navHit, 1F, NavMesh.AllAreas))
                            {
                                myTarget._outNavmesh[x, y] = navHit.distance;
                            }
                            else
                            {
                                myTarget._outNavmesh[x, y] = 1F;
                            }
                        }
                        else
                        {
                            myTarget._outNavmesh[x, y] = 1F;
                        }

                        if (hit.collider.GetComponent<MapCreatorTag>())
                        {
                            _tag = hit.collider.GetComponent<MapCreatorTag>()._tag;
                            _water = hit.collider.GetComponent<MapCreatorTag>()._water;
                        }
                        else if (hit.collider.GetComponentInParent<MapCreatorTag>())
                        {
                            _tag = hit.collider.GetComponentInParent<MapCreatorTag>()._tag;
                            _water = hit.collider.GetComponentInParent<MapCreatorTag>()._water;
                        }
                        else if (hit.collider.gameObject.tag != null)
                        {
                            _tag = hit.collider.gameObject.tag;
                        }

                        if (_water)
                        {
                            myTarget._ids[x, y] = 1;
                            myTarget._normals[x, y] = Vector3.up;
                        }
                        else
                        {
                            foreach (var obj in myTarget.CustomColors)
                            {
                                if (_tag.ToLower() == obj._tag.ToLower() || (obj._layer & (1 << hit.collider.gameObject.layer)) != 0)
                                {
                                    myTarget._ids[x, y] = obj._id;
                                    _match = true;
                                }
                            }
                            float _normalDot = Mathf.RoundToInt(Vector3.Dot(hit.normal, Vector3.up) * 10F) / 10F;
                            if (!_match) myTarget._ids[x, y] = (_normalDot < 0.6F) ? -1 : 0;
                        }
                        myTarget._heights[x, y] = hit.point.y;
                        myTarget._normals[x, y] = _water ? Vector3.up : hit.normal;
                    }
                    else
                    {
                        myTarget._outNavmesh[x, y] = 1F;
                        if (myTarget.UseBackGroundTexture && myTarget.BackGroundTexture != null)
                        {
                            myTarget._ids[x, y] = 100;
                            myTarget._heights[x, y] = myTarget.Lowest;
                            myTarget._normals[x, y] = Vector3.up;
                        }
                        else
                        {
                            myTarget._ids[x, y] = 0;
                            myTarget._heights[x, y] = 0.1F;
                            myTarget._normals[x, y] = Vector3.up;
                        }
                    }
                    _maxHeight = Mathf.Max(_maxHeight, myTarget._heights[x, y]);
                    if (x % 128 == 0 && y%128==0)
                    {
                        if(myTarget.Preview) ScanBlocks.Add(new Vector3(x, y, 
                            myTarget._normals[x, y].y*(myTarget._heights[x, y]/ 100F)));
                        yield return 1;
                        Repaint();
                    }
                }
                Progress += _progressStep*0.5F;
                ProgressText = "Collecting data from 3d objects...";

            }

            ScanBlocks.Clear();
            Vector3 _startPoint = _bottomLeft + Vector3.up * _heightRange;
            Texture2D _image = new Texture2D(GetSize(myTarget.Size), GetSize(myTarget.Size), TextureFormat.RGB24, false);
            int _previewLine = 0;

            for (int y = GetSize(myTarget.Size)-1; y >=0 ; y--)
            {
                for (int x = 0; x < GetSize(myTarget.Size); x++)
                {
                    Color _color = myTarget.WaterColor;
                    Color _pattern = myTarget.PatternTexture.GetPixel(Mathf.FloorToInt( x * GetSizeStep(myTarget.Size)), Mathf.FloorToInt(y * GetSizeStep(myTarget.Size)));
                    float _ao = Mathf.Clamp(Mathf.Max(myTarget._heights[x, y], 0.1F) / GetAvergeHeight(x, y, _sampleRadius * 2), 1F - myTarget.AoIntensity, 1F + myTarget.AoIntensity * 0.3F);
                    float _normalDot = Mathf.RoundToInt(Vector3.Dot(myTarget._normals[x, y], Vector3.up) * 10F) / 10F;
                    float _heightColorGround = _ao * Mathf.Clamp(Mathf.Pow(_normalDot, myTarget.GroundDetailIntensity * 4F), Mathf.Lerp(0.9F, 0.35F, myTarget.GroundDetailIntensity), 1F);
                    float _heightColorMountain = Mathf.Clamp(_ao, 0.8F, 1.3F) * 0.2F + myTarget._heights[x, y] / _maxHeight * 0.1F + _normalDot * 0.2F + 0.5F;
                    float _edge = isTurn(x, y, 1) ? 0.8F : 1F;
                    

                    if (myTarget._ids[x, y] == 1)
                    {
                        _color = myTarget.WaterColor * _pattern.b;
                    }
                    else if (myTarget._ids[x, y] == 100 && myTarget.UseBackGroundTexture && myTarget.BackGroundTexture != null)
                    {
                        _color = Color.Lerp(myTarget.BackGroundTexture.GetPixel(x, y), myTarget.WaterColor, 0.5F) * 0.8F;
                    }
                    else if (myTarget._ids[x, y] > 1)
                    {
                        _color = _customColor[myTarget._ids[x, y]] * _pattern.r * _heightColorGround;
                    }
                    else if (isBorder(x, y, myTarget) || isEdge(x, y))
                    {
                        _color = myTarget.BorderColor * _pattern.g;
                        _edge = 1F;
                    }
                    else if (_normalDot < 0.6F)
                    {
                        _color = myTarget.MountainColor * _pattern.g * _heightColorMountain;
                    }
                    else if (_normalDot < 0.8F)
                    {
                        _color = (AreaNormalDifference(x, y, _sampleRadius, myTarget) < myTarget.MountainAngleThreshold && AreaHeightDifference(x, y, _sampleRadius, myTarget) < myTarget.MountainStepHeightThreshold) ? (myTarget.GroundColor * _pattern.r * _heightColorGround) : (myTarget.MountainColor * _pattern.g * _heightColorMountain);
                    }
                    else
                    {
                        _color = myTarget.GroundColor * _pattern.r * _heightColorGround;
                    }

                    if (myTarget.InNavmeshColor != myTarget.OutsideNavmeshColor)
                    {
                        _color *= Color.Lerp( myTarget.InNavmeshColor,myTarget.OutsideNavmeshColor, myTarget._outNavmesh[x, y]);
                    }

                    _image.SetPixel(x, y, _color * _edge);
                    if (myTarget.Preview)
                    {
                        fullCpuPixelBuffer[y * GetSize(myTarget.Size) + x] = _color * _edge;
                    }
                }
                Progress += _progressStep*1.5F;

                ProgressText = "Rendering Map Texture...";
                if (myTarget.Preview)
                {
                    if (_previewLine >= 10)
                    {
                        _scanLinePos = y*1F / GetSize(myTarget.Size);
                        Color32[] chunkData = new Color32[GetSize(myTarget.Size) * 10];
                        System.Array.Copy(fullCpuPixelBuffer, y * GetSize(myTarget.Size), chunkData, 0, GetSize(myTarget.Size) * 10);
                        PreviewTexture.SetPixels32(0, y, GetSize(myTarget.Size), 10, chunkData);
                        PreviewTexture.Apply();
                        _previewLine = 0;
                        yield return 1;
                        Repaint();
                    }
                    else
                    {
                        if (y % 100 == 0) yield return 1;
                    }
                    _previewLine++;
                }
                else
                {
                    if (y % 100 == 0)
                    {
                        yield return 1;
                        Repaint();
                    }
                }
            }
            _image.Apply();
            byte[] _bytes = _image.EncodeToPNG();
            File.WriteAllBytes(myTarget.FilePath, _bytes);
            ProgressText = myTarget.FilePath;
            Progress = 1F;
            yield return 1;
            AssetDatabase.Refresh();
            yield return 1;
            if (myTarget.CurrentMap != null)
            {
                TextureImporter _importer = (TextureImporter)AssetImporter.GetAtPath(_path);
                _importer.isReadable = true;
                _importer.mipmapEnabled = false;
                _importer.maxTextureSize = 4096;
                _importer.SaveAndReimport();
                AssetDatabase.Refresh();
                if (myTarget.BakeForSubMap)
                {
                    myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._map = (Texture2D)AssetDatabase.LoadAssetAtPath<Texture2D>(_path);
                }
                else
                {
                    myTarget.CurrentMap._maps[myTarget.CurrentLayer]._map = (Texture2D)AssetDatabase.LoadAssetAtPath<Texture2D>(_path);
                }
            }
            EditorUtility.DisplayDialog("Baking Map", "Successfully rendered the map texture into:" + _path, "Confirm");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture>(_path);
        }

        private bool baking = false;
        public void StartBaking(string _path)
        {
            if (!baking)
            {
                if (mBakingCo != null) this.StopCoroutine(mBakingCo);
                mBakingCo = this.StartCoroutine(StartBakingCo(_path));
            }
        }

        private int GetSize(MapCreator.MapSize Size)
        {
            switch (Size)
            {
                case MapCreator.MapSize.Square_512: return 512;
                case MapCreator.MapSize.Square_1024: return 1024;
                case MapCreator.MapSize.Square_2048: return 2048;
                case MapCreator.MapSize.Square_4096: return 4096;
                case MapCreator.MapSize.Square_8192: return 8192;
            }
            return 1024;
        }

        private float GetSizeStep(MapCreator.MapSize Size)
        {
            switch (Size)
            {
                case MapCreator.MapSize.Square_512: return 8;
                case MapCreator.MapSize.Square_1024: return 4;
                case MapCreator.MapSize.Square_2048: return 2;
                case MapCreator.MapSize.Square_4096: return 1;
                case MapCreator.MapSize.Square_8192: return 0.5F;
            }
            return 1F;
        }

        private float GetAvergeHeight(int _x, int _y, int _range)
        {
            int _minX = Mathf.Clamp(_x - _range, 0, GetSize(myTarget.Size) - 1);
            int _maxX = Mathf.Clamp(_x + _range, 0, GetSize(myTarget.Size) - 1);
            int _minY = Mathf.Clamp(_y - _range, 0, GetSize(myTarget.Size) - 1);
            int _maxY = Mathf.Clamp(_y + _range, 0, GetSize(myTarget.Size) - 1);
            float _result = 0F;
            int _count = 0;
            for (int x = _minX; x <= _maxX; x++)
            {
                for (int y = _minY; y <= _maxY; y++)
                {
                    _result += myTarget._heights[x, y];
                    _count++;
                }
            }
            float _averge = _result / _count;
            if (_averge == 0F) _averge = 0.1F;
            return _averge;
        }

        private bool isTurn(int _x, int _y, int _offset)
        {
            int _id = myTarget._ids[_x, _y];
            int _minX = _x;
            int _maxX = Mathf.Clamp(_x + _offset, 0, GetSize(myTarget.Size) - 1);
            int _minY = _y;
            int _maxY = Mathf.Clamp(_y + _offset, 0, GetSize(myTarget.Size) - 1);
            bool _result = false;
            for (int x = _minX; x <= _maxX; x++)
            {
                for (int y = _minY; y <= _maxY; y++)
                {
                    if (myTarget._ids[x, y] != _id) _result = true;
                }
            }
            return _result;
        }

        private bool isEdge(int _x, int _y)
        {
            int _id = Mathf.Max(0, myTarget._ids[_x, _y]);
            int _offset = 1;
            int _minX = Mathf.Clamp(_x - _offset, 0, GetSize(myTarget.Size) - 1);
            int _maxX = Mathf.Clamp(_x + _offset, 0, GetSize(myTarget.Size) - 1);
            int _minY = Mathf.Clamp(_y - _offset, 0, GetSize(myTarget.Size) - 1);
            int _maxY = Mathf.Clamp(_y + _offset, 0, GetSize(myTarget.Size) - 1);
            bool _result = false;
            for (int x = _minX; x <= _maxX; x++)
            {
                for (int y = _minY; y <= _maxY; y++)
                {
                    if (Mathf.Max(0, myTarget._ids[x, y]) != _id) _result = true;
                }
            }
            return _result;
        }

        private bool isBorder(int _x, int _y, MapCreator myTarget)
        {

            if (myTarget._ids[_x, _y] == 1) return false;
            int _offset = Mathf.Max(1, Mathf.RoundToInt(2F / GetSizeStep(myTarget.Size)));
            int _minX = Mathf.Clamp(_x - _offset, 0, GetSize(myTarget.Size) - 1);
            int _maxX = Mathf.Clamp(_x + _offset, 0, GetSize(myTarget.Size) - 1);
            int _minY = Mathf.Clamp(_y - _offset, 0, GetSize(myTarget.Size) - 1);
            int _maxY = Mathf.Clamp(_y + _offset, 0, GetSize(myTarget.Size) - 1);
            bool _result = false;
            for (int x = _minX; x <= _maxX; x++)
            {
                for (int y = _minY; y <= _maxY; y++)
                {
                    if (myTarget._ids[x, y] == 1 || myTarget._ids[x, y] == 100) _result = true;
                }
            }
            return _result;
        }
        private float AreaNormalDifference(int _x, int _y, int _range, MapCreator myTarget)
        {
            int _minX = Mathf.Clamp(_x - _range, 0, GetSize(myTarget.Size) - 1);
            int _maxX = Mathf.Clamp(_x + _range, 0, GetSize(myTarget.Size) - 1);
            int _minY = Mathf.Clamp(_y - _range, 0, GetSize(myTarget.Size) - 1);
            int _maxY = Mathf.Clamp(_y + _range, 0, GetSize(myTarget.Size) - 1);
            float _angle = 0F;
            float _lerp = 6F / _range;
            for (int x = _minX; x <= _maxX; x++)
            {
                for (int y = _minY; y <= _maxY; y++)
                {
                    _angle = Mathf.Max(_angle, Mathf.Lerp(_angle, Vector3.Angle(myTarget._normals[x, y], myTarget._normals[_x, _y]), _lerp));
                }
            }
            return _angle;
        }

        private float AreaHeightDifference(int _x, int _y, int _range, MapCreator myTarget)
        {
            int _minX = Mathf.Clamp(_x - _range, 0, GetSize(myTarget.Size) - 1);
            int _maxX = Mathf.Clamp(_x + _range, 0, GetSize(myTarget.Size) - 1);
            int _minY = Mathf.Clamp(_y - _range, 0, GetSize(myTarget.Size) - 1);
            int _maxY = Mathf.Clamp(_y + _range, 0, GetSize(myTarget.Size) - 1);
            float _height = 0F;
            float _lerp = 1F / _range;
            for (int x = _minX; x <= _maxX; x++)
            {
                for (int y = _minY; y <= _maxY; y++)
                {
                    _height = Mathf.Max(_height, Mathf.Lerp(_height, Mathf.Abs(myTarget._heights[x, y] - myTarget._heights[_x, _y]), _lerp));
                }
            }
            return _height;
        }
    }
}
