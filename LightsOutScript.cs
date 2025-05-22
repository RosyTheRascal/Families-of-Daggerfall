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
           
            var now = DaggerfallUnity.Instance.WorldTime.Now;
            if (Mathf.Floor(now.Hour) != lastCheckedHour)
            {
                lastCheckedHour = Mathf.Floor(now.Hour);

                Debug.Log($"[LightsOut] Hour changed to {now.Hour}, checking window state, nya!");

                if (now.Hour == 22)
                {
                    Debug.Log("[LightsOut] 22:00 - Turning OFF residential windows, nya!");
                    SetAllWindowEmissionsForFaction(false, 0); // Turn OFF
                }
                else if (now.Hour == 6)
                {
                    Debug.Log("[LightsOut] 06:00 - Turning ON residential windows, nya!");
                    SetAllWindowEmissionsForFaction(true, 0); // Turn ON
                }
                else if (now.Hour == 8)
                {
                    Debug.Log("[LightsOut] 08:00 - Turning OFF residential windows (let vanilla logic take over), nya!");
                    SetAllWindowEmissionsForFaction(false, 0); // Let vanilla logic resume, or forcibly OFF
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
            // Find the DaggerfallLocation parent (top-level for the current scene)
            var locationGO = GameObject.FindObjectOfType<DaggerfallWorkshop.Game.DaggerfallLocation>();
            if (locationGO == null)
                return;

            // Get all BuildingDirectory components under this location (even in inactive RMB blocks)
            var bds = locationGO.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>(true);

            foreach (var bd in bds)
            {
                foreach (var building in bd.GetBuildingsOfFaction(0))
                {
                    // Your original logic here~
                    var obj = building; // You may need to map from BuildingSummary to the GameObject in the scene
                    var go = FindBuildingGameObject(obj); // implement this!
                    if (go == null) continue;

                    var dayNight = go.GetComponentInChildren<DayNight>();
                    if (dayNight != null)
                    {
                        // Find a material with _EmissionColor, like DayNight.InitEmissiveMaterial()
                        var meshRenderer = go.GetComponentInChildren<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            foreach (var mat in meshRenderer.materials)
                            {
                                if (mat.HasProperty("_EmissionColor"))
                                {
                                    var color = on ? dayNight.nightColor : dayNight.dayColor;
                                    mat.SetColor("_EmissionColor", color);
                                }
                            }
                        }
                    }
                }
            }
        }

        GameObject FindBuildingGameObject(BuildingSummary summary)
        {
            foreach (var staticBuildings in FindObjectsOfType<DaggerfallWorkshop.DaggerfallStaticBuildings>())
            {
                if (staticBuildings.Buildings == null) continue;
                foreach (var building in staticBuildings.Buildings)
                {
                    if (building.buildingKey == summary.buildingKey)
                    {
                        return staticBuildings.gameObject;
                    }
                }
            }
            return null;
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
