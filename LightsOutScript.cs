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
            DebugLogAllEmissiveMaterials();
            var now = DaggerfallUnity.Instance.WorldTime.Now;
            if (Mathf.Floor(now.Hour) != lastCheckedHour)
            {
                lastCheckedHour = Mathf.Floor(now.Hour);

                Debug.Log($"[LightsOut] Hour changed to {now.Hour}, checking window state, nya!");

                if (now.Hour == 22)
                {
                    Debug.Log("[LightsOut] 22:00 - Turning OFF residential windows, nya!");
                    SetAllWindowEmissionsVanilla(false); // Turn OFF
                }
                else if (now.Hour == 6)
                {
                    Debug.Log("[LightsOut] 06:00 - Turning ON residential windows, nya!");
                    SetAllWindowEmissionsVanilla(true); // Turn ON
                }
                else if (now.Hour == 8)
                {
                    Debug.Log("[LightsOut] 08:00 - Turning OFF residential windows (let vanilla logic take over), nya!");
                    SetAllWindowEmissionsVanilla(false); // Let vanilla logic resume, or forcibly OFF
                }
            }
        }

        void SetAllWindowEmissionsVanilla(bool on)
        {
            var meshRenderers = GameObject.FindObjectsOfType<MeshRenderer>();
            int changed = 0;
            foreach (var mr in meshRenderers)
            {
                foreach (var mat in mr.materials)
                {
                    if (mat.HasProperty("_EmissionColor") && IsProbablyWindow(mat))
                    {
                        // Use a bright yellow for ON, dark for OFF
                        Color color = on ? new Color(0.8f, 0.57f, 0.18f) : new Color(0.05f, 0.05f, 0.05f);
                        mat.SetColor("_EmissionColor", color);
                        mat.EnableKeyword("_EMISSION");
                        changed++;
                    }
                }
            }
            Debug.Log($"[LightsOut] Set emission for {changed} window materials in scene, nya!");
        }

        void SetResidentialWindows(bool on)
        {
            int foundBuildings = 0;
            int changedWindows = 0;
            foreach (var bd in FindObjectsOfType<DaggerfallWorkshop.Game.BuildingDirectory>())
            {
                foreach (var building in bd.GetBuildingsOfFaction(0))
                {
                    foundBuildings++;
                    var go = FindBuildingGameObject(building);
                    if (go == null)
                    {
                        Debug.LogWarning($"[LightsOut] No GameObject found for buildingKey={building.buildingKey}, nya!");
                        continue;
                    }

                    var dayNight = go.GetComponentInChildren<DayNight>(true);
                    if (dayNight == null)
                    {
                        Debug.LogWarning($"[LightsOut] No DayNight component found on buildingKey={building.buildingKey}, nya!");
                        continue;
                    }

                    var mat = GetEmissiveMaterial(dayNight);
                    if (mat != null)
                    {
                        var color = on ? dayNight.nightColor : dayNight.dayColor;
                        mat.SetColor("_EmissionColor", color);
                        Debug.Log($"[LightsOut] Set emission for buildingKey={building.buildingKey} to {(on ? "ON" : "OFF")}, nya!");
                        changedWindows++;
                    }
                    else
                    {
                        Debug.LogWarning($"[LightsOut] No emission material found for buildingKey={building.buildingKey}, nya!");
                    }
                }
            }
            Debug.Log($"[LightsOut] Processed {foundBuildings} residential buildings, changed {changedWindows} window emissions, nya!");
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
    }
}
