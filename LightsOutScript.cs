using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.FallExe;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallConnect.Utility;

namespace LightsOutScriptMod
{
    public class LightsOutScript : MonoBehaviour
    {
        private static Mod mod;
        private float lastCheckedHour = -1;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<LightsOutScript>();
            mod.IsReady = true;
            Debug.Log("[LightsOut] Mod initialized, nya~!");
        }

        void Update()
        {
            Start(); // (Nyaa, you should only call this once, not every frame, but let's leave it for now...)
            var now = DaggerfallUnity.Instance.WorldTime.Now;
            if (Mathf.Floor(now.Hour) != lastCheckedHour)
            {
                lastCheckedHour = Mathf.Floor(now.Hour);

                Debug.Log($"[LightsOut] Hour changed to {now.Hour}, checking window state, nya!");

                if (now.Hour == 22)
                {
                    Debug.Log("[LightsOut] 22:00 - Turning OFF residential windows, nya!");
                    SetResidentialWindows(false); // Turn OFF
                }
                else if (now.Hour == 6)
                {
                    Debug.Log("[LightsOut] 06:00 - Turning ON residential windows, nya!");
                    SetResidentialWindows(true); // Turn ON
                }
                else if (now.Hour == 8)
                {
                    Debug.Log("[LightsOut] 08:00 - Turning OFF residential windows (let vanilla logic take over), nya!");
                    SetResidentialWindows(false); // Let vanilla logic resume, or forcibly OFF
                }
            }
        }

        void Start()
        {
            DaggerfallWorkshop.StreamingWorld.OnCreateLocationGameObject += OnLocationLoaded;
        }

        void OnDestroy()
        {
            DaggerfallWorkshop.StreamingWorld.OnCreateLocationGameObject -= OnLocationLoaded;
        }

        // This method will be called every time a new DaggerfallLocation is loaded.
        private void OnLocationLoaded(DaggerfallWorkshop.DaggerfallLocation location)
        {
            // For each BuildingDirectory in this location, apply your logic
            var buildingDirectories = location.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>(true);
            foreach (var bd in buildingDirectories)
            {
                foreach (var building in bd.GetBuildingsOfFaction(0))
                {
                    var go = FindBuildingGameObject(building);
                    if (go == null) continue;
                    var dayNight = go.GetComponentInChildren<DayNight>();
                    if (dayNight != null)
                    {
                        var meshRenderer = go.GetComponentInChildren<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            foreach (var mat in meshRenderer.materials)
                            {
                                if (mat.HasProperty("_EmissionColor"))
                                {
                                    var now = DaggerfallUnity.Instance.WorldTime.Now;
                                    Color color = (now.Hour >= 6 && now.Hour < 8) ? dayNight.nightColor : dayNight.dayColor;
                                    mat.SetColor("_EmissionColor", color);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnBlockLoad(DaggerfallWorkshop.DaggerfallRMBBlock block)
        {
            // Get BuildingDirectory on this block
            var bd = block.GetComponent<DaggerfallWorkshop.Game.BuildingDirectory>();
            if (bd == null) return;

            // Your logic to set windows for this block only
            foreach (var building in bd.GetBuildingsOfFaction(0))
            {
                var go = FindBuildingGameObject(building);
                if (go == null) continue;
                var dayNight = go.GetComponentInChildren<DayNight>();
                if (dayNight != null)
                {
                    var meshRenderer = go.GetComponentInChildren<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        foreach (var mat in meshRenderer.materials)
                        {
                            if (mat.HasProperty("_EmissionColor"))
                            {
                                var now = DaggerfallUnity.Instance.WorldTime.Now;
                                Color color;
                                if (now.Hour >= 6 && now.Hour < 8)
                                    color = dayNight.nightColor; // ON
                                else
                                    color = dayNight.dayColor;   // OFF
                                mat.SetColor("_EmissionColor", color);
                            }
                        }
                    }
                }
            }
        }

        int? GetFactionIdForMeshRenderer(MeshRenderer mr)
        {
            // Walk up to find the parent block object (should have DaggerfallStaticBuildings)
            Transform t = mr.transform;
            while (t != null)
            {
                var staticBuildings = t.GetComponent<DaggerfallWorkshop.DaggerfallStaticBuildings>();
                if (staticBuildings != null)
                {
                    // Use bounds center as point-in-building test
                    Vector3 point = mr.bounds.center;
                    DaggerfallWorkshop.StaticBuilding building;
                    if (staticBuildings.HasHit(point, out building))
                    {
                        // Use BuildingDirectory to get summary
                        var bd = t.GetComponentInParent<BuildingDirectory>();
                        if (bd != null)
                        {
                            BuildingSummary summary; // <-- just this change!
                            if (bd.GetBuildingSummary(building.buildingKey, out summary))
                                return summary.FactionId;
                        }
                    }
                }
                t = t.parent;
            }
            return null; // Not found
        }

        bool IsPartOfFaction(GameObject go, int factionId)
        {
            Transform t = go.transform;
            while (t != null)
            {
                var bd = t.GetComponent<BuildingDirectory>();
                if (bd != null)
                {
                    // We found the BuildingDirectory (one per city block)
                    // Now, try to find the closest building whose bounds contain this object's position
                    Vector3 pos = go.transform.position;
                    foreach (var summary in bd.GetBuildingsOfFaction(factionId))
                    {
                        // Try to find a matching building by proximity (since there's no direct GameObject link)
                        // We'll use bounding box center as a heuristic
                        if ((summary.Position - pos).sqrMagnitude < 16f) // You may need to tweak this threshold
                        {
                            return true;
                        }
                    }
                }
                t = t.parent;
            }
            return false;
        }

        void SetAllWindowEmissionsForFaction(bool on, int factionId)
        {
            var meshRenderers = GameObject.FindObjectsOfType<MeshRenderer>();
            int changed = 0;
            foreach (var mr in meshRenderers)
            {
                foreach (var mat in mr.materials)
                {
                    if (mat.HasProperty("_EmissionColor") && IsProbablyWindow(mat))
                    {
                        int? meshFaction = GetFactionIdForMeshRenderer(mr);
                        if (meshFaction != factionId)
                            continue;
                        Color color = on ? new Color(0.8f, 0.57f, 0.18f) : new Color(0.05f, 0.05f, 0.05f);
                        mat.SetColor("_EmissionColor", color);
                        mat.EnableKeyword("_EMISSION");
                        changed++;
                    }
                }
            }
            Debug.Log($"[LightsOut] Set emission for {changed} window materials with faction {factionId}, nya!");
        }

        void SetResidentialWindows(bool on)
        {
            var bdArray = FindObjectsOfType<DaggerfallWorkshop.Game.BuildingDirectory>();
            Debug.Log($"[LightsOut] Found {bdArray.Length} BuildingDirectory components in scene.");

            foreach (var bd in bdArray)
            {
                var blockGO = bd.gameObject.transform.parent ? bd.gameObject.transform.parent.name : "(no parent)";
                Debug.Log($"[LightsOut] Block parent: {blockGO}, BuildingDirectory: {bd.gameObject.name}");

                var buildings = bd.GetBuildingsOfFaction(0);
                Debug.Log($"[LightsOut] Buildings in this block: {(buildings != null ? buildings.Count : 0)}");

                if (buildings == null) continue;

                foreach (var building in buildings)
                {
                    var obj = building;
                    var go = FindBuildingGameObject(obj); // implement this!
                    if (go == null)
                    {
                        Debug.LogWarning($"[LightsOut] No GameObject found for building key {building.buildingKey} in block {blockGO}");
                        continue;
                    }

                    var dayNight = go.GetComponentInChildren<DayNight>();
                    if (dayNight != null)
                    {
                        var meshRenderer = go.GetComponentInChildren<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            foreach (var mat in meshRenderer.materials)
                            {
                                if (mat.HasProperty("_EmissionColor"))
                                {
                                    var color = on ? dayNight.nightColor : dayNight.dayColor;
                                    mat.SetColor("_EmissionColor", color);
                                    Debug.Log($"[LightsOut] Set emission for building {go.name} (key {building.buildingKey}) in block {blockGO}");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[LightsOut] No MeshRenderer for building {go.name} (key {building.buildingKey})");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[LightsOut] No DayNight for building {go.name} (key {building.buildingKey})");
                    }
                }
            }
        }

        GameObject FindBuildingGameObject(BuildingSummary summary)
        {
            foreach (var staticBuildings in FindObjectsOfType<DaggerfallWorkshop.DaggerfallStaticBuildings>())
            {
                if (staticBuildings.Buildings == null) continue;
                for (int i = 0; i < staticBuildings.Buildings.Length; i++)
                {
                    var sb = staticBuildings.Buildings[i];
                    if (sb.buildingKey == summary.buildingKey)
                    {
                        Vector3 targetPos = staticBuildings.transform.TransformPoint(sb.centre);

                        // 1. Check all children directly
                        foreach (Transform child in staticBuildings.transform)
                        {
                            if ((child.position - targetPos).sqrMagnitude < 0.1f)
                            {
                                EnsureDayNight(child.gameObject);
                                return child.gameObject;
                            }

                            // 2. Check CombinedModels
                            if (child.name == "CombinedModels")
                            {
                                foreach (Transform subChild in child)
                                {
                                    if ((subChild.position - targetPos).sqrMagnitude < 0.1f)
                                    {
                                        EnsureDayNight(subChild.gameObject);
                                        return subChild.gameObject;
                                    }
                                }
                            }
                        }
                        // No match, fallback
                        Debug.LogWarning($"[LightsOut] No child found at position {targetPos} for buildingKey {summary.buildingKey} under {staticBuildings.gameObject.name}");
                        EnsureDayNight(staticBuildings.gameObject);
                        return staticBuildings.gameObject;
                    }
                }
            }
            return null;
        }

        // Add DayNight if missing
        void EnsureDayNight(GameObject go)
        {
            Debug.Log($"Poop");
            var dn = go.GetComponent<DayNight>();
            if (!dn)
            {
                // Only add if we have a MeshRenderer
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer == null) return;
                dn = go.AddComponent<DayNight>();
                dn.dayColor = new Color(0.05f, 0.05f, 0.05f);
                dn.nightColor = new Color(0.8f, 0.57f, 0.18f);
                dn.materialIndex = 0;
                dn.emissionColors = DayNight.EmissionColors.CustomColors;
                // Try to init the emissive material
                typeof(DayNight).GetMethod("InitEmissiveMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(dn, null);
                // If it failed, remove the component to avoid errors
                var field = typeof(DayNight).GetField("emissiveMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field == null || field.GetValue(dn) == null)
                {
                    Destroy(dn);
                    Debug.LogWarning($"[LightsOut] Removed DayNight from {go.name} (no valid emissive material)");
                }
                else
                {
                    Debug.Log($"[LightsOut] Added missing DayNight to {go.name} at {go.transform.position}, nya~");
                }
            }
        }

        Material GetEmissiveMaterial(DayNight dayNight)
        {
            MeshRenderer renderer = dayNight.GetComponentInChildren<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning("[LightsOut] No MeshRenderer found on DayNight, nya!");
                return null;
            }
            try
            {
                var mat = renderer.materials[dayNight.materialIndex];
                if (!mat.IsKeywordEnabled("_EMISSION"))
                    mat.EnableKeyword("_EMISSION");
                return mat;
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("[LightsOut] Failed to get emissive material: {0}", e.Message);
                return null;
            }
        }

        List<DayNight> FindAllDayNightDeep()
        {
            List<DayNight> results = new List<DayNight>();
            foreach (GameObject go in Resources.FindObjectsOfTypeAll(typeof(GameObject)))
            {
                // skip prefabs (not in scene)
                if (!go.scene.IsValid() || go.hideFlags != HideFlags.None)
                    continue;

                foreach (var dn in go.GetComponents<DayNight>())
                {
                    results.Add(dn);
                }
            }
            return results;
        }

        void DebugLogAllDayNightsDeep()
        {
            var allDayNights = FindAllDayNightDeep();
            Debug.Log($"[LightsOut] Found {allDayNights.Count} DayNight components in scene (deep scan), nya!");
            foreach (var dn in allDayNights)
            {
                var parent = dn.transform.parent ? dn.transform.parent.name : dn.gameObject.name;
                Debug.Log($"[LightsOut] DayNight on GameObject: {dn.gameObject.name}, parent: {parent}, pos: {dn.transform.position}, active: {dn.gameObject.activeInHierarchy}");
            }
        }

        void DebugLogAllEmissiveMaterials()
        {
            var meshRenderers = GameObject.FindObjectsOfType<MeshRenderer>();
            int count = 0;
            foreach (var mr in meshRenderers)
            {
                foreach (var mat in mr.materials)
                {
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        count++;
                        Debug.Log($"[LightsOut] Material: {mat.name}, Shader: {mat.shader.name}, Emission: {mat.GetColor("_EmissionColor")}");
                    }
                }
            }
            Debug.Log($"[LightsOut] Found {count} materials with _EmissionColor property in scene, nya!");
        }

        void DebugLogAllMeshRenderers()
            {
            int count = 0;
            foreach (GameObject go in Resources.FindObjectsOfTypeAll(typeof(GameObject)))
            {
                if (!go.scene.IsValid() || go.hideFlags != HideFlags.None)
                    continue;

                var mr = go.GetComponent<MeshRenderer>();
                if (mr)
                {
                    Debug.Log($"[LightsOut] MeshRenderer found: {go.name} at {go.transform.position}");
                    count++;
                }
            }
            Debug.Log($"[LightsOut] Total MeshRenderers in scene: {count}");
        }

        bool IsProbablyWindow(Material mat)
        {
            // Most Daggerfall windows are index=3 and use Daggerfall/Default shader
            string name = mat.name;
            bool hasWindowIndex = name.Contains("[Index=3]");
            bool isDaggerfallShader = mat.shader != null && mat.shader.name == "Daggerfall/Default";
            // Windows have a non-black emission color
            Color emission = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            bool isEmissive = emission.maxColorComponent > 0.1f;
            return hasWindowIndex && isDaggerfallShader && isEmissive;
        }

        IEnumerable<BuildingSummary> GetAllBuildingSummaries(BuildingDirectory bd)
        {
            var field = typeof(BuildingDirectory).GetField("buildingDict", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = field?.GetValue(bd) as Dictionary<int, BuildingSummary>;
            return dict != null ? (IEnumerable<BuildingSummary>)dict.Values : new List<BuildingSummary>();
        }

    }
}
