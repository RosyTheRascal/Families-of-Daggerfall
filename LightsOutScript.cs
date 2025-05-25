using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.FallExe;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Banking;
using DaggerfallWorkshop.Game.Guilds;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using UnityEngine.SceneManagement; // Add this line
using System.Reflection;

namespace LightsOutScriptMod
{
    public class LightsOutScript : MonoBehaviour
    {
        private static Mod mod;
        private float lastCheckedHour = -1;
        private const float WindowAssignmentRadius = 12f; // How close a window must be to a door to be counted

        // UwU: new mapping of buildingKey to their mapped window materials
        private Dictionary<int, List<WindowMatInfo>> buildingWindowsMap = new Dictionary<int, List<WindowMatInfo>>();

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
                    Debug.Log("[LightsOut] 22:00 - Turning OFF residential windows only, nya!");
                    MapWindowsToBuildingsIfNeeded();
                    SetFactionWindowEmissions(0, false); // 0 = Residential, off!
                }
                else if (now.Hour == 6)
                {
                    Debug.Log("[LightsOut] 06:00 - Turning ON all windows, nya!");
                    MapWindowsToBuildingsIfNeeded();
                    SetFactionWindowEmissions(0, true); // Turn ON for residences
                    // You can add more factions here if you want, nya!
                }
            }
            if (Input.GetKeyDown(KeyCode.Semicolon)) // Debug
            {
                Debug.Log("[LightsOut] Debugging window-to-building assignments, nya~!");
                DebugLogWindowMappings();
            }
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                DebugLogAllBlocksAndBuildings();
                DebugLogAllWindowMaterialMappings();
            }
        }

        // --- Window/Building Mapping ---

        // neko-helper: only rebuild the mapping if empty (so it's not super slow owo)
        void MapWindowsToBuildingsIfNeeded()
        {
            if (buildingWindowsMap.Count == 0)
            {
                Debug.Log("[LightsOut] Mapping all windows to buildings, nya!");
                buildingWindowsMap = MapAllWindowsToBuildings();
            }
        }

        // UwU: Main mapping logic!
        Dictionary<int, List<WindowMatInfo>> MapAllWindowsToBuildings()
        {
            // 1. Gather all buildings (with world pos, faction, etc.)
            var allBuildings = new List<(int buildingKey, Vector3 pos, int factionId, string type)>();
            foreach (var building in GetAllBuildingsWithBlockName())
            {
                Vector3 worldPos = GetBuildingWorldPosition(building.summary, building.blockName, building.locationTransform);
                allBuildings.Add((building.buildingKey, worldPos, building.summary.FactionId, building.summary.BuildingType.ToString()));
            }

            // 2. Gather all window materials UwU
            var windowMats = FindAllWindowMaterials();

            // 3. Map windows to nearest building
            var mapping = new Dictionary<int, List<WindowMatInfo>>();
            foreach (var win in windowMats)
            {
                float minDist = float.MaxValue;
                int nearestBuildingKey = -1;
                foreach (var b in allBuildings)
                {
                    float dist = Vector3.Distance(win.position, b.pos);
                    if (dist < minDist && dist < WindowAssignmentRadius)
                    {
                        minDist = dist;
                        nearestBuildingKey = b.buildingKey;
                    }
                }
                if (nearestBuildingKey != -1)
                {
                    if (!mapping.ContainsKey(nearestBuildingKey))
                        mapping[nearestBuildingKey] = new List<WindowMatInfo>();
                    mapping[nearestBuildingKey].Add(win);
                }
            }
            Debug.Log($"[LightsOut] Mapped {windowMats.Count} window materials to {allBuildings.Count} buildings, nya~!");
            return mapping;
        }

        // --- Faction-based Emission Logic ---

        // turns on/off all windows for a given faction (e.g. 0: residential)
        void SetFactionWindowEmissions(int factionId, bool on)
        {
            int changed = 0;
            foreach (var kvp in buildingWindowsMap)
            {
                int buildingKey = kvp.Key;
                // Get the faction for this building!
                int buildingFaction = GetBuildingFaction(buildingKey);
                if (buildingFaction == factionId)
                {
                    foreach (var win in kvp.Value)
                    {
                        SetWindowEmission(win.mat, on);
                        changed++;
                    }
                }
            }
            Debug.Log($"[LightsOut] Set emission for {changed} window materials for faction {factionId} ({(on ? "ON" : "OFF")}), nya~!");
        }

        // helper: get faction for a buildingKey
        int GetBuildingFaction(int buildingKey)
        {
            foreach (var b in GetAllBuildingsWithBlockName())
            {
                if (b.buildingKey == buildingKey)
                    return b.summary.FactionId;
            }
            return -1; // not found
        }

        // --- Emissive Window Material Helpers ---

        // Set emission (glow) for a window material, nyan~
        void SetWindowEmission(Material mat, bool on)
        {
            Color color = on ? new Color(0.8f, 0.57f, 0.18f) : new Color(0.05f, 0.05f, 0.05f);
            mat.SetColor("_EmissionColor", color);
            mat.EnableKeyword("_EMISSION");
        }

        // Find all window materials in the scene (vanilla logic, but neko-enhanced!)
        List<WindowMatInfo> FindAllWindowMaterials()
        {
            var results = new List<WindowMatInfo>();
            foreach (var mr in GameObject.FindObjectsOfType<MeshRenderer>())
            {
                Vector3 center = mr.bounds.center;
                foreach (var mat in mr.materials)
                {
                    if (mat.HasProperty("_EmissionColor") && IsProbablyWindow(mat))
                        results.Add(new WindowMatInfo { mat = mat, position = center });
                }
            }
            return results;
        }

        // --- Debug/Logging ---

        void DebugLogWindowMappings()
        {
            MapWindowsToBuildingsIfNeeded();
            foreach (var kvp in buildingWindowsMap)
            {
                int buildingKey = kvp.Key;
                int faction = GetBuildingFaction(buildingKey);
                string type = GetBuildingType(buildingKey);
                Debug.Log($"[LightsOut] BuildingKey={buildingKey} Faction={faction} Type={type} has {kvp.Value.Count} windows mapped, nya~");
            }
        }

        string GetBuildingType(int buildingKey)
        {
            foreach (var b in GetAllBuildingsWithBlockName())
            {
                if (b.buildingKey == buildingKey)
                    return b.summary.BuildingType.ToString();
            }
            return "Unknown";
        }

        // --- Your original methods you wanted to keep ---

        void DebugLogAllBlocksAndBuildings()
        {
            foreach (var building in GetAllBuildingsWithBlockName())
            {
                Vector3 worldPos = GetBuildingWorldPosition(building.summary, building.blockName, building.locationTransform);
                Debug.Log($"[LightsOut] BuildingKey={building.buildingKey} Block={building.blockName} FactionId={building.summary.FactionId} Type={building.summary.BuildingType} Pos={worldPos}");
            }
        }

        void DebugLogAllWindowMaterialMappings()
        {
            int total = 0;
            var buildingLookup = GetAllBuildingsWithBlockName()
                .ToDictionary(b => b.buildingKey, b => (b.blockName, b.locationTransform));
            foreach (var staticBuildings in FindObjectsOfType<DaggerfallWorkshop.DaggerfallStaticBuildings>())
            {
                var bd = staticBuildings.GetComponent<BuildingDirectory>();
                if (bd == null) continue;
                var meshRenderers = staticBuildings.GetComponentsInChildren<MeshRenderer>();
                foreach (var mr in meshRenderers)
                {
                    for (int i = 0; i < mr.materials.Length; i++)
                    {
                        var mat = mr.materials[i];
                        if (mat.HasProperty("_EmissionColor"))
                        {
                            Vector3 pos = mr.bounds.center;
                            DaggerfallWorkshop.StaticBuilding statBldg;
                            if (staticBuildings.HasHit(pos, out statBldg))
                            {
                                BuildingSummary summary;
                                if (bd.GetBuildingSummary(statBldg.buildingKey, out summary))
                                {
                                    if (buildingLookup.TryGetValue(summary.buildingKey, out var binfo))
                                    {
                                        Vector3 worldPos = GetBuildingWorldPosition(summary, binfo.blockName, binfo.locationTransform);
                                        Debug.Log($"[LightsOut] WindowMat: {mat.name} ({mr.gameObject.name} [{i}]) → BuildingKey={summary.buildingKey} FactionId={summary.FactionId} Type={summary.BuildingType} at {worldPos}");
                                    }
                                    else
                                    {
                                        Debug.Log($"[LightsOut] WindowMat: {mat.name} ({mr.gameObject.name} [{i}]) → Could not find block info for key={summary.buildingKey}");
                                    }
                                }
                                else
                                {
                                    Debug.Log($"[LightsOut] WindowMat: {mat.name} ({mr.gameObject.name} [{i}]) → Could not find BuildingSummary for key={statBldg.buildingKey}");
                                }
                            }
                            else
                            {
                                Debug.Log($"[LightsOut] WindowMat: {mat.name} ({mr.gameObject.name} [{i}]) → Not in any building bounds?");
                            }
                            total++;
                        }
                    }
                }
            }
            Debug.Log($"[LightsOut] Total window materials mapped: {total}");
        }

        // --- Core Building Lookup from your OG script ---

        // UwU: Struct for window mapping
        struct WindowMatInfo
        {
            public Material mat;
            public Vector3 position;
        }

        // Gathers all buildings with their block name and location transform for world space lookups, nya~
        List<(int buildingKey, Vector3 worldPos, int factionId, string buildingType, BuildingSummary summary, string blockName, Transform locationTransform)> GetAllBuildingsWithBlockName()
        {
            var result = new List<(int, Vector3, int, string, BuildingSummary, string, Transform)>();
            foreach (var location in FindObjectsOfType<DaggerfallLocation>())
            {
                var locationTransform = location.transform;
                foreach (var block in location.GetComponentsInChildren<DaggerfallWorkshop.DaggerfallRMBBlock>())
                {
                    string blockName = block.name;
                    foreach (var bd in block.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>())
                    {
                        foreach (var summary in GetAllBuildingSummaries(bd))
                        {
                            Vector3 worldPos = block.transform.TransformPoint(summary.Position);
                            result.Add((summary.buildingKey, worldPos, summary.FactionId, summary.BuildingType.ToString(), summary, blockName, locationTransform));
                        }
                    }
                }
            }
            return result;
        }

        Vector3 GetBuildingWorldPosition(BuildingSummary summary, string blockName, Transform locationTransform)
        {
            if (locationTransform == null)
                return summary.Position;

            DaggerfallWorkshop.DaggerfallRMBBlock foundBlock = null;
            foreach (var block in locationTransform.GetComponentsInChildren<DaggerfallWorkshop.DaggerfallRMBBlock>())
            {
                if (!string.IsNullOrEmpty(blockName) && block.name.Contains(blockName))
                {
                    foundBlock = block;
                    break;
                }
            }
            if (foundBlock == null)
                return summary.Position;

            return foundBlock.transform.TransformPoint(summary.Position);
        }

        IEnumerable<BuildingSummary> GetAllBuildingSummaries(BuildingDirectory bd)
        {
            var field = typeof(BuildingDirectory).GetField("buildingDict", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = field?.GetValue(bd) as Dictionary<int, BuildingSummary>;
            return dict != null ? (IEnumerable<BuildingSummary>)dict.Values : new List<BuildingSummary>();
        }

        // --- Improved IsProbablyWindow! (combines your logic with more flexible checks, nya~) ---
        bool IsProbablyWindow(Material mat)
        {
            string name = mat.name.ToLower();
            bool hasEmission = mat.HasProperty("_EmissionColor");
            bool isDaggerfall = mat.shader && mat.shader.name.Contains("Daggerfall");
            bool nameLooksLikeWindow = name.Contains("window") || name.Contains("[index=3]") || name.Contains("[index=2]");
            // Accept any mat that looks like a window and is using a Daggerfall shader, nya!
            return hasEmission && isDaggerfall && nameLooksLikeWindow;
        }
    }
}
