#if UNITY_EDITOR
namespace Studio.Editor
{
    using System.Collections.Generic;
    using Domain.World;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// Data-driven "art pass" for the RPG Proving Grounds: rolling hill terrain, sky and atmosphere,
    /// post-processing, village dressing and seeded wilderness scatter.
    /// Idempotent: every run rebuilds <see cref="RootName"/> and the ground mesh from scratch.
    /// </summary>
    public static class WorldBeautificationSetup
    {
        #region Constants & Data Tables

        private const string RootName = "Beautification_Root";
        private const string GroundName = "Ground_Landscape_Terrain";
        private const string GeneratedDir = "Assets/Generated/World";
        private const string GrassMaterialPath = "Assets/Synty/PolygonFantasyKingdom/Materials/Ground/Grass_01.mat";
        private const string EnvDir = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/";
        private const string PropDir = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/";
        private const string BuildingDir = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Buildings/Presets/";
        private const float GroundLevel = -0.02f;
        private const float GroundHalfSize = 100f;
        private const int GroundCells = 160;
        private const int Seed = 20241105;

        private enum Zone { Wilds, Village }

        private struct ScatterRule
        {
            public string Label;
            public string[] Prefabs;
            public int Count;
            public float MinRadius;
            public float MaxRadius;
            public float MinScale;
            public float MaxScale;
            public float Clearance;
            public bool StripColliders;
            public Zone Area;
        }

        private struct Placement
        {
            public string Path;
            public string Name;
            public float X;
            public float Z;
            public float Yaw;
            public float Scale;
            public bool Lamp;
        }

        private static readonly Placement[] VillageDressing =
        {
            new Placement { Path = BuildingDir + "SM_Bld_Preset_House_03_Optimized.prefab", Name = "House_West_South", X = -9.5f, Z = -9f, Yaw = 90f, Scale = 1f },
            new Placement { Path = BuildingDir + "SM_Bld_Preset_House_05_Optimized.prefab", Name = "House_East_South", X = 9.5f, Z = -9f, Yaw = -90f, Scale = 1f },
            new Placement { Path = BuildingDir + "SM_Bld_Preset_Tavern_01_Optimized.prefab", Name = "Tavern_West_North", X = -11f, Z = 10f, Yaw = 90f, Scale = 1f },
            new Placement { Path = BuildingDir + "SM_Bld_Preset_Church_01_A_Optimized.prefab", Name = "Chapel_Southeast", X = 18f, Z = -15f, Yaw = -90f, Scale = 1f },
            new Placement { Path = BuildingDir + "SM_Bld_Preset_House_Windmill_01_Optimized.prefab", Name = "Windmill_West", X = -25f, Z = -14f, Yaw = 60f, Scale = 1f },
            new Placement { Path = BuildingDir + "SM_Bld_Preset_Stables_01_Optimized.prefab", Name = "Stables_East", X = 15f, Z = 20f, Yaw = -90f, Scale = 1f },
            new Placement { Path = PropDir + "SM_Prop_Well_01.prefab", Name = "Well_Square", X = -5.5f, Z = -6f, Yaw = 0f, Scale = 1f },
            new Placement { Path = PropDir + "SM_Prop_Market_Stall_02.prefab", Name = "Stall_West", X = -5f, Z = -12f, Yaw = 90f, Scale = 1f },
            new Placement { Path = PropDir + "SM_Prop_Market_Stall_05.prefab", Name = "Stall_East", X = 5f, Z = -12f, Yaw = -90f, Scale = 1f },
            new Placement { Path = PropDir + "SM_Prop_Hay_Pile_01.prefab", Name = "Hay_Stables", X = 11f, Z = 17f, Yaw = 20f, Scale = 1f },
            new Placement { Path = PropDir + "SM_Prop_Street_Lamp_01.prefab", Name = "Lamp_SW", X = -3f, Z = -15f, Lamp = true, Scale = 1f },
            new Placement { Path = PropDir + "SM_Prop_Street_Lamp_01.prefab", Name = "Lamp_SE", X = 3f, Z = -15f, Lamp = true, Scale = 1f },
            new Placement { Path = PropDir + "SM_Prop_Street_Lamp_02.prefab", Name = "Lamp_NW", X = -3f, Z = 8.5f, Lamp = true, Scale = 1f },
            new Placement { Path = PropDir + "SM_Prop_Street_Lamp_02.prefab", Name = "Lamp_NE", X = 3f, Z = 8.5f, Lamp = true, Scale = 1f },
            new Placement { Path = PropDir + "SM_Prop_Direction_Sign_01.prefab", Name = "Signpost_South", X = 2.8f, Z = -19f, Yaw = 200f, Scale = 1f },
        };

        private static readonly ScatterRule[] ScatterRules =
        {
            Rule("Wild_Pines", Zone.Wilds, 26f, 80f, 80, 1.0f, 1.7f, 2.5f, false, EnvDir, "SM_Env_Tree_Pine_Cluster_01", "SM_Env_Tree_Pine_Cluster_02", "SM_Env_Tree_Pine_Cluster_04", "SM_Env_Tree_Pine_Cluster_05"),
            Rule("Wild_RoundTrees", Zone.Wilds, 24f, 72f, 50, 0.9f, 1.5f, 2.5f, false, EnvDir, "SM_Env_Tree_Round_01", "SM_Env_Tree_Round_02", "SM_Env_Tree_Round_03", "SM_Env_Tree_Round_04", "SM_Env_Tree_Large_01", "SM_Env_Tree_Large_02"),
            Rule("Wild_ThinTrees", Zone.Wilds, 24f, 72f, 45, 0.9f, 1.4f, 1.5f, false, EnvDir, "SM_Env_Tree_Thin_01", "SM_Env_Tree_Thin_02", "SM_Env_Tree_Thin_03", "SM_Env_Tree_Thin_04", "SM_Env_Tree_Thin_05", "SM_Env_Tree_Thin_06"),
            Rule("Wild_DeadTrees", Zone.Wilds, 55f, 85f, 10, 1.0f, 1.4f, 2.0f, false, EnvDir, "SM_Env_Tree_Dead_01", "SM_Env_Tree_Dead_02", "SM_Env_Tree_Dead_03", "SM_Env_Tree_Dead_04"),
            Rule("Wild_Boulders", Zone.Wilds, 23f, 90f, 45, 0.8f, 2.2f, 1.5f, false, EnvDir, "SM_Env_Rock_01", "SM_Env_Rock_02", "SM_Env_Rock_03", "SM_Env_Rock_04", "SM_Env_Rock_Chunk_01", "SM_Env_Rock_Chunk_02", "SM_Env_Rock_Chunk_03"),
            Rule("Wild_Bushes", Zone.Wilds, 22f, 62f, 70, 0.9f, 1.5f, 1.0f, true, EnvDir, "SM_Env_Bush_Cluster_01", "SM_Env_Bush_Cluster_02", "SM_Env_Bush_Cluster_03", "SM_Env_Bush_Cluster_04", "SM_Env_Bush_01", "SM_Env_Bush_02", "SM_Env_Bush_03"),
            Rule("Wild_Meadow", Zone.Wilds, 20f, 58f, 70, 1.0f, 1.5f, 0.5f, true, EnvDir, "SM_Env_Flowers_01", "SM_Env_Flowers_03", "SM_Env_Flowers_05", "SM_Env_Flowers_08", "SM_Env_Flowers_11", "SM_Env_Grass_Tuft_01", "SM_Env_Grass_Tuft_02"),
            Rule("Village_Trees", Zone.Village, 16f, 29f, 8, 0.9f, 1.2f, 3.0f, false, EnvDir, "SM_Env_Tree_Round_01", "SM_Env_Tree_Round_03", "SM_Env_Tree_Large_01"),
            Rule("Village_Flowers", Zone.Village, 3f, 29f, 70, 1.0f, 1.4f, 0.6f, true, EnvDir, "SM_Env_Flowers_02", "SM_Env_Flowers_04", "SM_Env_Flowers_06", "SM_Env_Flowers_09", "SM_Env_Flowers_14", "SM_Env_Flowers_18", "SM_Env_Flowers_22"),
            Rule("Village_Shrubs", Zone.Village, 6f, 29f, 22, 0.8f, 1.2f, 0.9f, true, EnvDir, "SM_Env_Bush_Flowers_01", "SM_Env_Bush_Flowers_03", "SM_Env_Bush_Flowers_05", "SM_Env_Bush_Small_01", "SM_Env_Bush_Small_03"),
            Rule("Village_Grass", Zone.Village, 3f, 29f, 120, 1.0f, 1.6f, 0.4f, true, EnvDir, "SM_Env_Grass_Tuft_01", "SM_Env_Grass_Tuft_02", "SM_Env_Grass_Tuft_03"),
        };

        private static ScatterRule Rule(string label, Zone area, float minR, float maxR, int count,
            float minScale, float maxScale, float clearance, bool strip, string dir, params string[] names)
        {
            var paths = new string[names.Length];
            for (int i = 0; i < names.Length; i++) paths[i] = dir + names[i] + ".prefab";

            return new ScatterRule
            {
                Label = label, Area = area, MinRadius = minR, MaxRadius = maxR, Count = count,
                MinScale = minScale, MaxScale = maxScale, Clearance = clearance, StripColliders = strip, Prefabs = paths
            };
        }

        #endregion

        #region Entry Points

        [MenuItem("3D2DWorld/Beautify Proving Grounds World", false, 2)]
        public static void ApplyFromMenu()
        {
            Apply();
            Debug.Log("<color=green>✦ World beautification pass complete.</color>");
        }

        /// <summary>Runs the complete art pass. Safe to call repeatedly.</summary>
        public static void Apply()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            BuildGround();
            ApplyAtmosphere();
            PlaceVillageDressing(root.transform);
            PlaceMountainRing(root.transform);
            ScatterAll(root.transform);
            EnsureHeroCamera(root.transform);
        }

        #endregion

        #region Ground Mesh

        private static void BuildGround()
        {
            var old = GameObject.Find(GroundName);
            if (old != null) Object.DestroyImmediate(old);

            var envRoot = GameObject.Find("Environment_Root") ?? new GameObject("Environment_Root");
            var ground = new GameObject(GroundName);
            ground.transform.parent = envRoot.transform;
            ground.transform.position = new Vector3(0f, GroundLevel, 0f);

            var mesh = CreateGroundMesh();
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
        }

        private static Mesh CreateGroundMesh()
        {
            int side = GroundCells + 1;
            float cell = GroundHalfSize * 2f / GroundCells;
            var vertices = new Vector3[side * side];
            var uvs = new Vector2[vertices.Length];

            for (int zi = 0; zi < side; zi++)
            {
                for (int xi = 0; xi < side; xi++)
                {
                    float x = -GroundHalfSize + xi * cell;
                    float z = -GroundHalfSize + zi * cell;
                    vertices[zi * side + xi] = new Vector3(x, WorldHeightField.Evaluate(x, z), z);
                    uvs[zi * side + xi] = new Vector2(x / 6f, z / 6f);
                }
            }

            var mesh = new Mesh { name = "ProvingGrounds_Ground", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = BuildTriangles(side);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveGeneratedMesh(mesh);
        }

        private static int[] BuildTriangles(int side)
        {
            var tris = new int[(side - 1) * (side - 1) * 6];
            int t = 0;
            for (int zi = 0; zi < side - 1; zi++)
            {
                for (int xi = 0; xi < side - 1; xi++)
                {
                    int i = zi * side + xi;
                    tris[t++] = i; tris[t++] = i + side; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + side; tris[t++] = i + side + 1;
                }
            }
            return tris;
        }

        private static Mesh SaveGeneratedMesh(Mesh mesh)
        {
            EnsureFolder(GeneratedDir);
            string path = GeneratedDir + "/ProvingGrounds_Ground.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        #endregion

        #region Atmosphere & Post-Processing

        private static void ApplyAtmosphere()
        {
            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Skyrim_Sky" };
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_AtmosphereThickness", 1.05f);
            sky.SetFloat("_Exposure", 1.35f);
            sky.SetColor("_SkyTint", new Color(0.52f, 0.68f, 0.90f));
            sky.SetColor("_GroundColor", new Color(0.40f, 0.45f, 0.38f));
            RenderSettings.skybox = SaveMaterial(sky, "Skyrim_Sky.mat");

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 210f;
            RenderSettings.fogColor = new Color(0.70f, 0.78f, 0.88f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.80f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.64f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.38f, 0.30f);

            ConfigureSun();
            ConfigureCameras();
            CreatePostProcessVolume();
            DynamicGI.UpdateEnvironment();
            ConfigureSceneView();
        }

        private static Material SaveMaterial(Material mat, string fileName)
        {
            EnsureFolder(GeneratedDir);
            string path = GeneratedDir + "/" + fileName;
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void ConfigureSun()
        {
            var sun = GameObject.Find("Directional Light");
            if (sun == null)
            {
                sun = new GameObject("Directional Light");
                sun.AddComponent<Light>().type = LightType.Directional;
            }

            if (!sun.TryGetComponent<Light>(out var light)) return;

            light.intensity = 1.45f;
            light.color = new Color(1f, 0.96f, 0.90f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.8f;
            light.shadowNormalBias = 0.4f;
            light.shadowBias = 0.05f;
            sun.transform.rotation = Quaternion.Euler(46f, 140f, 0f);
            RenderSettings.sun = light;
        }

        private static void ConfigureCameras()
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 400f);
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
        }

        private static void CreatePostProcessVolume()
        {
            var existing = GameObject.Find("PostProcess_Volume");
            if (existing != null) Object.DestroyImmediate(existing);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Skyrim_PostProcess";
            AddOverride<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);

            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.4f);

            AddOverride<Vignette>(profile).intensity.Override(0.18f);

            var color = AddOverride<ColorAdjustments>(profile);
            color.postExposure.Override(0.25f);
            color.contrast.Override(14f);
            color.saturation.Override(18f);

            EnsureFolder(GeneratedDir);
            string path = GeneratedDir + "/Skyrim_PostProcess.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(profile, path);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostProcess_Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        }

        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            return component;
        }

        private static void ConfigureSceneView()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.sceneViewState.showSkybox = true;
            view.sceneViewState.showFog = true;
            view.sceneViewState.showImageEffects = true;
            view.Repaint();
        }

        #endregion

        #region Village Dressing & Mountains

        private static void PlaceVillageDressing(Transform root)
        {
            var group = new GameObject("Village_Dressing").transform;
            group.parent = root;

            foreach (var p in VillageDressing)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.Path);
                if (prefab == null) { Debug.LogWarning($"[WorldBeautification] Missing prefab {p.Path}"); continue; }

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.name = p.Name;
                inst.transform.parent = group;
                float y = WorldHeightField.Evaluate(p.X, p.Z);
                inst.transform.SetPositionAndRotation(new Vector3(p.X, y, p.Z), Quaternion.Euler(0f, p.Yaw, 0f));
                inst.transform.localScale = Vector3.one * (p.Scale <= 0f ? 1f : p.Scale);
                if (p.Lamp) EnsureLampLight(inst.transform);
            }
        }

        private static void EnsureLampLight(Transform lamp)
        {
            if (lamp.GetComponentInChildren<Light>() != null) return;

            var go = new GameObject("Light_Lamp_Glow");
            go.transform.parent = lamp;
            go.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.78f, 0.5f);
        }

        private static void PlaceMountainRing(Transform root)
        {
            var group = new GameObject("Distant_Mountains").transform;
            group.parent = root;
            var random = new System.Random(Seed + 7);
            string[] cliffs = { "SM_Env_Rock_Cliff_01", "SM_Env_Rock_Cliff_02", "SM_Env_Rock_Cliff_03", "SM_Env_Rock_Cliff_04", "SM_Env_Rock_Cliff_05" };
            const int count = 22;

            for (int i = 0; i < count; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.6f) / count * Mathf.PI * 2f;
                float radius = 88f + (float)random.NextDouble() * 8f;
                float x = WorldHeightField.Center.x + Mathf.Cos(angle) * radius;
                float z = WorldHeightField.Center.y + Mathf.Sin(angle) * radius;
                float scale = 5f + (float)random.NextDouble() * 4f;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvDir + cliffs[random.Next(cliffs.Length)] + ".prefab");
                SpawnStatic(prefab, "Mountain_" + i, x, z, (float)random.NextDouble() * 360f, scale, group, false);
            }
        }

        #endregion

        #region Seeded Scatter

        private static void ScatterAll(Transform root)
        {
            var blockers = CollectKeepOuts(root);
            var random = new System.Random(Seed);
            foreach (var rule in ScatterRules) ScatterRuleInto(rule, root, blockers, random);
        }

        private static void ScatterRuleInto(ScatterRule rule, Transform root, List<Bounds> blockers, System.Random random)
        {
            var group = new GameObject(rule.Label).transform;
            group.parent = root;
            int placed = 0;

            for (int attempt = 0; attempt < rule.Count * 14 && placed < rule.Count; attempt++)
            {
                SamplePosition(rule, random, out float x, out float z);
                if (IsBlocked(x, z, rule.Clearance, blockers)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rule.Prefabs[random.Next(rule.Prefabs.Length)]);
                float scale = Mathf.Lerp(rule.MinScale, rule.MaxScale, (float)random.NextDouble());
                var inst = SpawnStatic(prefab, rule.Label + "_" + placed, x, z, (float)random.NextDouble() * 360f, scale, group, rule.StripColliders);
                if (inst == null) return;

                blockers.Add(FootprintOf(inst));
                placed++;
            }
        }

        private static void SamplePosition(ScatterRule rule, System.Random random, out float x, out float z)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float min2 = rule.MinRadius * rule.MinRadius;
            float max2 = rule.MaxRadius * rule.MaxRadius;
            float radius = Mathf.Sqrt(Mathf.Lerp(min2, max2, (float)random.NextDouble()));
            x = WorldHeightField.Center.x + Mathf.Cos(angle) * radius;
            z = WorldHeightField.Center.y + Mathf.Sin(angle) * radius;
        }

        private static List<Bounds> CollectKeepOuts(Transform root)
        {
            var list = new List<Bounds>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.transform.IsChildOf(root) || r.gameObject.name == GroundName) continue;
                if (r.bounds.size.x > 60f || r.bounds.size.z > 60f) continue;
                list.Add(r.bounds);
            }
            return list;
        }

        private static bool IsBlocked(float x, float z, float clearance, List<Bounds> blockers)
        {
            if (IsOnRoadwayOrPlaza(x, z, clearance)) return true;

            for (int i = 0; i < blockers.Count; i++)
            {
                var b = blockers[i];
                if (Mathf.Abs(x - b.center.x) < b.extents.x + clearance && Mathf.Abs(z - b.center.z) < b.extents.z + clearance)
                    return true;
            }
            return false;
        }

        private static bool IsOnRoadwayOrPlaza(float x, float z, float clearance)
        {
            if (Mathf.Abs(x) < 3.0f + clearance && z >= -25f && z <= 26f) return true;
            if (Mathf.Abs(z - 2f) < 2.8f + clearance && Mathf.Abs(x) <= 13f) return true;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(0f, -3f)) < 5.5f + clearance) return true;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(-5.5f, -6f)) < 3.8f + clearance) return true;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(-5f, -12f)) < 3.5f + clearance) return true;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(5f, -12f)) < 3.5f + clearance) return true;
            if (Mathf.Abs(x) < 4.5f + clearance && z <= -16f && z >= -25f) return true;
            if (Mathf.Abs(x) < 4.5f + clearance && z >= 12f && z <= 24f) return true;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(-18f, -15f)) < 7.0f + clearance) return true;
            return false;
        }

        private static void EnsureHeroCamera(Transform root)
        {
            var camGo = new GameObject("Hero_Vista_Camera");
            camGo.transform.parent = root;
            camGo.transform.position = new Vector3(0f, 3.2f, -18f);
            camGo.transform.rotation = Quaternion.Euler(7f, 0f, 0f);

            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 500f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        private static Bounds FootprintOf(GameObject inst)
        {
            var renderers = inst.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(inst.transform.position, Vector3.one);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static GameObject SpawnStatic(GameObject prefab, string name, float x, float z, float yaw, float scale, Transform parent, bool stripColliders)
        {
            if (prefab == null) return null;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = name;
            inst.transform.parent = parent;
            inst.transform.SetPositionAndRotation(new Vector3(x, WorldHeightField.Evaluate(x, z) + GroundLevel, z), Quaternion.Euler(0f, yaw, 0f));
            inst.transform.localScale = Vector3.one * scale;

            if (stripColliders)
            {
                foreach (var col in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            }

            GameObjectUtility.SetStaticEditorFlags(inst, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            return inst;
        }

        #endregion
    }
}
#endif
