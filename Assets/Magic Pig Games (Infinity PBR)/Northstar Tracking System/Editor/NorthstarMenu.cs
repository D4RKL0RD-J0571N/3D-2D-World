using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using static MagicPigGames.Shared.MagicPigEditor;

namespace MagicPigGames.Northstar
{
    public class NorthstarMenu : EditorWindow
    {
        private const string showCompassKey = "Show Compass Prefabs";
        private const string showRadarKey = "Show Radar Prefabs";
        private const string showNavigationBarKey = "Show Navigation Bar Prefabs";
        
        private static List<string> navigationBarPrefabs = new List<string>();
        private static List<string> compassPrefabs = new List<string>();
        private static List<string> radarPrefabs = new List<string>();

        private static bool _foundOverlay = false;
        
        [MenuItem("GameObject/Northstar/Create Window")]
        public static void ShowWindow() => GetWindow<NorthstarMenu>("Northstar Menu");
        
        private bool _prefabsLoaded = false;

        public void OnFocus() => _foundOverlay = GameObject.FindAnyObjectByType<NorthstarOverlay>() != null;

        public void OnEnable()
        {
            _foundOverlay = GameObject.FindAnyObjectByType<NorthstarOverlay>() != null;
            // Prefab lists are populated on demand via the Refresh button, not on every focus/enable.
        }

        private void ResetButtons()
        {
            compassPrefabs.Clear();
            radarPrefabs.Clear();

            compassPrefabs = FindPrefabsOfType<Compass>().ToList();
            radarPrefabs = FindPrefabsOfType<Radar>().ToList();
            navigationBarPrefabs = FindNavigationBarPrefabs<NavigationBar>().ToList();
            _prefabsLoaded = true;
        }

        private Vector2 _scrollPosition = Vector2.zero;

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/northstar-tracking-system/overview-and-quickstart");
            Space();
            if (GUILayout.Button("Refresh Prefab Lists"))
                ResetButtons();
            if (!_prefabsLoaded)
                EditorGUILayout.HelpBox("Click \"Refresh Prefab Lists\" to load available prefabs.", MessageType.Info);
            Space();
            ShowScreenOverlay();
            Line();
            ShowNavigationBar();
            Line();
            ShowCompass();
            ShowRadar();

            EditorGUILayout.EndScrollView();
        }

        private void ShowScreenOverlay()
        {
            StartRow();
            if (_foundOverlay)
            {
                
                PingButton(GameObject.FindAnyObjectByType<NorthstarOverlay>());
                Header2("Northstar Screen Overlay");
                EndRow();
                return;
            }
            
            if (Button("Add Northstar Screen Overlay"))
                AddNorthstarOverlay();
            EndRow();
        }

        private void ShowNavigationBar()
        {
            StartRow();
            ButtonOpenClose(showNavigationBarKey);
            Header2("Navigation Bar");
            EndRow();
            
            if (!GetBool(showNavigationBarKey))
                return;

            OverwriteNavigationBarWarning();
            foreach (var prefab in 
                     from prefab in navigationBarPrefabs 
                     let prefabName = System.IO.Path.GetFileNameWithoutExtension(prefab) 
                     where GUILayout.Button(prefabName) 
                     select prefab)
                AddNavigationBar<NavigationBar>(prefab);
        }

        private void OverwriteCompassWarning()
        {
            if (FindAnyObjectByType<Compass>() != null 
                || FindAnyObjectByType<Radar>() != null)
                EditorGUILayout.HelpBox("WARNING: There is already a Compass or Radar object in the scene. Adding a new one will replace the existing one.", MessageType.Warning);
        }
        
        private void OverwriteNavigationBarWarning()
        {
            if (FindAnyObjectByType<NavigationBar>() != null)
                EditorGUILayout.HelpBox("WARNING: There is already a Navigation Bar in the scene. Adding a new one will replace the existing one.", MessageType.Warning);
        }
        
        private void ShowCompass()
        {
            StartRow();
            ButtonOpenClose(showCompassKey);
            Header2("Compass");
            EndRow();

            if (!GetBool(showCompassKey))
                return;
            
            OverwriteCompassWarning();
            foreach (var prefab in 
                     from prefab in compassPrefabs 
                     let prefabName = System.IO.Path.GetFileNameWithoutExtension(prefab) 
                     where GUILayout.Button(prefabName) 
                     select prefab)
                AddNorthstarComponent<Compass>(prefab);
        }

        private void ShowRadar()
        {
            StartRow();
            ButtonOpenClose(showRadarKey);
            Header2("Radar");
            EndRow();
            
            if (!GetBool(showRadarKey))
                return;
            
            OverwriteCompassWarning();
            foreach (var prefab in 
                     from prefab in radarPrefabs 
                     let prefabName = System.IO.Path.GetFileNameWithoutExtension(prefab) 
                     where GUILayout.Button(prefabName) 
                     select prefab)
                AddNorthstarComponent<Radar>(prefab);
        }

        private static IEnumerable<string> FindPrefabsOfType<T>() where T : NorthstarSystem 
            => (from guid in AssetDatabase.FindAssets("t:Prefab") 
                    select AssetDatabase.GUIDToAssetPath(guid) into path 
                    where path.Contains("/Resources/") 
                    let startIndex = path.IndexOf("/Resources/", StringComparison.Ordinal) + 11 
                    let relativePath = path[startIndex..] 
                    let prefabPath = System.IO.Path.ChangeExtension(relativePath, null) 
                    let prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) 
                    where prefab != null && prefab.GetComponentInChildren<T>() != null 
                    select prefabPath)
                .ToArray();
        
        private static IEnumerable<string> FindNavigationBarPrefabs<T>() where T : NavigationBar 
            => (from guid in AssetDatabase.FindAssets("t:Prefab") 
                    select AssetDatabase.GUIDToAssetPath(guid) into path 
                    where path.Contains("/Resources/") 
                    let startIndex = path.IndexOf("/Resources/", StringComparison.Ordinal) + 11 
                    let relativePath = path[startIndex..] 
                    let prefabPath = System.IO.Path.ChangeExtension(relativePath, null) 
                    let prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) 
                    where prefab != null && prefab.GetComponentInChildren<T>() != null 
                    select prefabPath)
                .ToArray();

        private static void AddNorthstarComponent<T>(string prefabName) where T : NorthstarSystem
        {
            var existingObject = GameObject.FindAnyObjectByType<NorthstarSystem>() as NorthstarSystem;
            
            Transform savedPlayerTransform = default;
            GameObject savedDefaultTrackedIconPrefab = default;
            
            if (existingObject != null)
            {
                var shouldReplace = EditorUtility.DisplayDialog(
                    "Remove Existing Compass/Radar?",
                    "There is already a Northstar Compass or Radar in the scene. Do you want to replace that with a new one?",
                    "Yes",
                    "Cancel"
                );

                if (!shouldReplace)
                {
                    EditorGUIUtility.PingObject(existingObject);
                    return;
                }

                savedPlayerTransform = existingObject.player;
                savedDefaultTrackedIconPrefab = existingObject.defaultTrackedIcon;

                DestroyImmediate(existingObject.gameObject);
            }
            
            var prefab = Resources.Load<GameObject>(prefabName);
            if (prefab == null)
            {
                Debug.LogError($"{prefabName} prefab not found!");
                return;
            }

            var canvas = GameObject.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            //var instance = Instantiate(prefab, canvas.transform);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                instance.transform.SetParent(canvas.transform, false);
                instance.name = prefabName;

                var newRadar = instance.GetComponent<NorthstarSystem>();
                newRadar.player = savedPlayerTransform;
                if (savedDefaultTrackedIconPrefab != null)
                    newRadar.defaultTrackedIcon = savedDefaultTrackedIconPrefab;
                
                EditorGUIUtility.PingObject(instance);
                Selection.activeObject = instance;
            
                Debug.Log($"<color=cyan>{typeof(T).Name} Added!</color> You may need to populate targets on the new Northstar System before it will operate properly. Expand the object and check out each child components for places to add targets.");
                return;
            }
            
            Debug.Log("Instantiate Failed!");
        }

        private static void SetNavigationBarOnScreenOverlay(NorthstarOverlay overlay, NavigationBar navigationBar)
        {
            overlay.SetNavigationBar(navigationBar);
            if (navigationBar.northstarOverlaySettings == null)
            {
                Debug.LogError($"Navigation Bar {navigationBar.name} does not have a Northstar Overlay Settings set!");
                return;
            }
            overlay.SetNorthstarOverlaySettings(navigationBar.northstarOverlaySettings);
            EditorUtility.SetDirty(overlay);
        }

        private static void AddNavigationBar<T>(string prefabName) where T : NavigationBar
        {
            var existingObject = GameObject.FindAnyObjectByType<NavigationBar>() as NavigationBar;
            
            // Data we can move to the new prefab
            //Transform savedPlayerTransform = default;
            //GameObject savedDefaultTrackedIconPrefab = default;
            
            if (existingObject != null)
            {
                var shouldReplace = EditorUtility.DisplayDialog(
                    "Remove Existing Navigation Bar?",
                    "There is already a Navigation Bar in the scene. Do you want to replace that with a new one?",
                    "Yes",
                    "Cancel"
                );

                if (!shouldReplace)
                {
                    EditorGUIUtility.PingObject(existingObject);
                    return;
                }

                // Set from current data
                //savedPlayerTransform = existingObject.player;
                //savedDefaultTrackedIconPrefab = existingObject.defaultTrackedIcon;
                
                DestroyImmediate(existingObject.gameObject);
            }
            
            // Ensure we have an overlay set up, and cache it for later
            AddNorthstarOverlay(false);
            var overlay = GameObject.FindAnyObjectByType<NorthstarOverlay>();
            var northstarOverlay = overlay.GetComponent<NorthstarOverlay>();
            
            var prefab = Resources.Load<GameObject>(prefabName);
            if (prefab == null)
            {
                Debug.LogError($"{prefabName} prefab not found!");
                return;
            }
            
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                instance.transform.SetParent(overlay.transform, false);
                instance.name = prefabName;
                
                EditorGUIUtility.PingObject(instance);
                Selection.activeObject = instance;
            
                // Copy the rect transform settings from the prefab to the instance
                var prefabRectTransform = prefab.GetComponent<RectTransform>();
                var instanceRectTransform = instance.GetComponent<RectTransform>();
                instanceRectTransform.anchorMin = prefabRectTransform.anchorMin;
                instanceRectTransform.anchorMax = prefabRectTransform.anchorMax;
                instanceRectTransform.pivot = prefabRectTransform.pivot;
                instanceRectTransform.anchoredPosition = prefabRectTransform.anchoredPosition;
                instanceRectTransform.sizeDelta = prefabRectTransform.sizeDelta;
                
                var navigationBar = instance.GetComponent<NavigationBar>();
                SetNavigationBarValues(northstarOverlay, navigationBar);
                SetNavigationBarOnScreenOverlay(northstarOverlay, navigationBar);
                Debug.Log($"<color=cyan>{typeof(T).Name} Added!</color> You may need to adjust settings of " +
                          $"the new bar, or of the objects which you will be displaying on the bar. Common things like " +
                          $"the Y Position may need to be adjusted based on the UI you are using.");
                return;
            }
            
            Debug.Log("Instantiate Failed!");
        }

        private static void SetNavigationBarValues(NorthstarOverlay northstarOverlay, NavigationBar navigationBar)
        {
            navigationBar.northstarOverlay = northstarOverlay;
        }

        static void AddNorthstarOverlay(bool pingIfFound = true)
        {
            // Check if there is already an object with a NorthstarOverlay component
            if (GameObject.FindAnyObjectByType<NorthstarOverlay>() != null)
            {
                if (!pingIfFound) return;
                Debug.Log("There is already a Northstar Overlay in the project");
                EditorGUIUtility.PingObject(GameObject.FindAnyObjectByType<NorthstarOverlay>());
                return;
            }

            // Load the prefab
            var northstarPrefab = Resources.Load<GameObject>("Northstar Screen Overlay");
            if (northstarPrefab == null)
            {
                Debug.LogError("Northstar Screen Overlay prefab not found!");
                return;
            }

            // Find or create a canvas
            var canvas = GameObject.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            
            var northstarInstance = PrefabUtility.InstantiatePrefab(northstarPrefab) as GameObject;
            if (northstarInstance != null)
            {
                northstarInstance.transform.SetParent(canvas.transform, false);
                northstarInstance.name = "Northstar Screen Overlay";
                EditorGUIUtility.PingObject(northstarInstance);
                Selection.activeObject = northstarInstance;
            
                Debug.Log($"<color=cyan>Northstar Screen Overlay Added!</color> You may need to populate targets on the new Northstar System before it will operate properly. Expand the object and check out each child components for places to add targets.");
                _foundOverlay = true;
                return;
            }
            
            Debug.Log("Instantiate Failed!");
        }
    }
}