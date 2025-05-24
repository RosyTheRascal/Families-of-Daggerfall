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
        private const float WindowAssignmentRadius = 12f; // How close a window must be to a door to be counted

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<LightsOutScript>();
            mod.IsReady = true;
            Debug.Log("[LightsOut] Mod initialized, nya~!");
        }

        private readonly Dictionary<int, int> factionLightsOutHour = new Dictionary<int, int>()
{
    { 0, 22 },    // Residential
    { 45, 19 },   // Armorer (example, update with true FactionID as needed!)
    // Add more: { FactionID, CloseHour }
};

        // Helper to build a mapping from buildingKey to all StaticDoor(s) for that building
        Dictionary<int, List<StaticDoor>> BuildExteriorDoorMap()
        {
            var allDoors = FindObjectsOfType<DaggerfallWorkshop.DaggerfallStaticDoors>();
            var doorMap = new Dictionary<int, List<StaticDoor>>();
            foreach (var doors in allDoors)
            {
                foreach (var door in doors.Doors)
                {
                    if (!doorMap.ContainsKey(door.buildingKey))
                        doorMap[door.buildingKey] = new List<StaticDoor> { door };
                    else
                        doorMap[door.buildingKey].Add(door);
                }
            }
            return doorMap;
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
                    Debug.Log("[LightsOut] 22:00 - Turning OFF windows based on mapping, nya!");
                    MapWindowsToBuildingsAndSetEmission_BlockAware(false);
                }
                else if (now.Hour == 6)
                {
                    Debug.Log("[LightsOut] 06:00 - Turning ON windows based on mapping, nya!");
                    MapWindowsToBuildingsAndSetEmission_BlockAware(true);
                }
            }
            if (Input.GetKeyDown(KeyCode.Semicolon)) // Pick any debug key you like
            {
                Debug.Log("[LightsOut] Debugging window-to-building assignments, nya~!");
                MapAndLogWindowsToBuildings_SuperDebug();
                DumpAllRMBBlocks();
                DumpAllBuildingKeys();
            }
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                Debug.Log("[LightsOut] \\ key pressed! Debugging all blocks/buildings/window mappings, nya!");
                DebugLogAllBlocksAndBuildings();
                DebugLogAllWindowMaterialMappings();
            }
        }

        // Add this to your LightsOutScript class, nya~
        List<(int buildingKey, BuildingSummary summary, string blockName, Transform locationTransform)> GetAllBuildingsWithBlockName()
        {
            var result = new List<(int, BuildingSummary, string, Transform)>();
            foreach (var location in FindObjectsOfType<DaggerfallLocation>())
            {
                Transform locationTransform = location.transform;
                foreach (var block in location.GetComponentsInChildren<DaggerfallWorkshop.DaggerfallRMBBlock>())
                {
                    string blockName = block.name;
                    foreach (var bd in block.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>())
                    {
                        foreach (var summary in GetAllBuildingSummaries(bd))
                        {
                            // We want to know: the buildingKey, the summary, the blockName, and the location's transform!
                            result.Add((summary.buildingKey, summary, blockName, locationTransform));
                        }
                    }
                }
            }
            return result;
        }

        void MapWindowsToBuildingsAndSetEmission_FactionAware(bool on)
        {
            var allBuildings = GetAllBuildingWorldInfo();
            var windowMats = FindAllWindowMaterials();

            foreach (var win in windowMats)
            {
                float minDist = float.MaxValue;
                int matchedBuildingKey = -1;
                int matchedFaction = -1;
                string matchedType = null;

                foreach (var b in allBuildings)
                {
                    float dist = Vector3.Distance(win.position, b.worldPos);
                    if (dist < minDist && dist < WindowAssignmentRadius)
                    {
                        minDist = dist;
                        matchedBuildingKey = b.buildingKey;
                        matchedFaction = b.factionId;
                        matchedType = b.buildingType;
                    }
                }

                if (matchedBuildingKey != -1)
                {
                    // Here you can check matchedFaction or matchedType!
                    bool shouldBeOn = on;
                    if (matchedFaction == 0 && !on) // Example: only turn off for residential at night
                        shouldBeOn = false;
                    // (add more rules for other types/factions!)

                    var color = shouldBeOn ? new Color(0.8f, 0.57f, 0.18f) : new Color(0.05f, 0.05f, 0.05f);
                    win.mat.SetColor("_EmissionColor", color);
                    win.mat.EnableKeyword("_EMISSION");
                    Debug.Log($"[LightsOut] Set window at {win.position} for building {matchedBuildingKey} ({matchedType}, Faction {matchedFaction}) {(shouldBeOn ? "ON" : "OFF")}");
                }
                else
                {
                    Debug.Log($"[LightsOut][WARN] Window at {win.position} could not be matched to a building!");
                }
            }
        }

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
            // Build a lookup dictionary so we can find blockName and locationTransform for any buildingKey, nya~
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
                                    // UwU: Look up blockName and locationTransform for this buildingKey!
                                    if (buildingLookup.TryGetValue(summary.buildingKey, out var binfo))
                                    {
                                        Vector3 worldPos = GetBuildingWorldPosition(summary, binfo.blockName, binfo.locationTransform);
                                        Debug.Log($"[LightsOut] WindowMat: {mat.name} ({mr.gameObject.name} [{i}]) → BuildingKey={summary.buildingKey} FactionId={summary.FactionId} Type={summary.BuildingType} WorldPos={worldPos}");
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

        Dictionary<int, List<WindowMatInfo>> MapWindowsToBuildings(
    List<WindowMatInfo> windowMats,
    List<(int buildingKey, Vector3 pos)> allBuildings)
        {
            var result = new Dictionary<int, List<WindowMatInfo>>();
            foreach (var win in windowMats)
            {
                float minDist = float.MaxValue;
                int nearestBuildingKey = -1;
                foreach (var b in allBuildings)
                {
                    float dist = Vector3.Distance(win.position, b.pos);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearestBuildingKey = b.buildingKey;
                    }
                }
                if (nearestBuildingKey != -1)
                {
                    if (!result.ContainsKey(nearestBuildingKey))
                        result[nearestBuildingKey] = new List<WindowMatInfo>();
                    result[nearestBuildingKey].Add(win);
                }
            }
            return result;
        }

        void MapAndLogWindowsToBuildings_SuperDebug()
        {
            // 1. Gather all buildings
            var allBuildings = new List<(int buildingKey, Vector3 pos, int factionId, string type)>();
            foreach (var building in GetAllBuildingsWithBlockName())
            {
                Vector3 worldPos = GetBuildingWorldPosition(building.summary, building.blockName, building.locationTransform);
                allBuildings.Add((building.buildingKey, worldPos, building.summary.FactionId, building.summary.BuildingType.ToString()));
                Debug.Log($"[LightsOut][DBG] Building: key={building.buildingKey} type={building.summary.BuildingType} faction={building.summary.FactionId} worldPos={worldPos}");
            }

            // 2. Gather all window materials
            var windowMats = FindAllWindowMaterials();

            // 3. For each window, find the closest N buildings and log
            int N = 3; // How many closest to log
            foreach (var win in windowMats)
            {
                var dists = allBuildings
                    .Select(b => (b, dist: Vector3.Distance(win.position, b.pos)))
                    .OrderBy(x => x.dist)
                    .Take(N)
                    .ToList();

                string closestList = string.Join(", ", dists.Select(x => $"key={x.b.buildingKey} type={x.b.type} dist={x.dist:0.00}"));
                Debug.Log($"[LightsOut][DBG] Window at {win.position} - {closestList}");

                // Optionally, color window differently if ambiguous
                float first = dists[0].dist;
                float second = (dists.Count > 1) ? dists[1].dist : float.MaxValue;
                if (second - first < 2.0f) // if two are nearly the same distance
                {
                    win.mat.SetColor("_EmissionColor", Color.magenta);
                    win.mat.EnableKeyword("_EMISSION");
                }
            }

            // 4. Map windows to nearest building as before, but warn if distance > threshold
            var mapping = new Dictionary<int, List<WindowMatInfo>>();
            float assignRadius = WindowAssignmentRadius;
            foreach (var win in windowMats)
            {
                float minDist = float.MaxValue;
                int nearestKey = -1;
                foreach (var b in allBuildings)
                {
                    float dist = Vector3.Distance(win.position, b.pos);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearestKey = b.buildingKey;
                    }
                }
                if (nearestKey != -1 && minDist < assignRadius)
                {
                    if (!mapping.ContainsKey(nearestKey)) mapping[nearestKey] = new List<WindowMatInfo>();
                    mapping[nearestKey].Add(win);
                }
                else
                {
                    Debug.LogWarning($"[LightsOut][WARN] Window at {win.position} could not be confidently assigned! Closest is {minDist:0.00} units away.");
                }
            }
            // 5. Summary log
            foreach (var b in allBuildings)
            {
                int count = mapping.ContainsKey(b.buildingKey) ? mapping[b.buildingKey].Count : 0;
                Debug.Log($"[LightsOut] Building key={b.buildingKey} type={b.type} at {b.pos} has {count} mapped windows.");
            }
        }

        void MapAndLogWindowsToBuildings()
        {
            Debug.Log($"Poop");
            // 1. Gather all buildings (using world coordinates!)
            var allBuildings = new List<(int buildingKey, Vector3 pos, int factionId, string type)>();
            foreach (var building in GetAllBuildingsWithBlockName())
            {
                Vector3 worldPos = GetBuildingWorldPosition(building.summary, building.blockName, building.locationTransform);
                allBuildings.Add((building.buildingKey, worldPos, building.summary.FactionId, building.summary.BuildingType.ToString()));
            }

            // 2. Gather all window materials
            var windowMats = FindAllWindowMaterials();

            // 3. Map windows to nearest building
            var mapping = new Dictionary<int, List<WindowMatInfo>>();
            foreach (var win in windowMats)
            {
                Debug.Log($"[LightsOut][DBG] Window candidate at {win.position}");
            }
            foreach (var b in allBuildings)
            {
                foreach (var win in windowMats)
                {
                    float dist = Vector3.Distance(win.position, b.pos);
                    Debug.Log($"[LightsOut][DBG] Window at {win.position} -- dist to building {b.buildingKey} ({b.type}) at {b.pos}: {dist}");
                }
            }
            foreach (var win in windowMats)
            {
                float minDist = float.MaxValue;
                int nearestBuildingKey = -1;
                foreach (var b in allBuildings)
                {
                    float dist = Vector3.Distance(win.position, b.pos);
                    if (dist < minDist)
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

            // 4. Log the assignment!
            foreach (var b in allBuildings)
            {
                int count = mapping.ContainsKey(b.buildingKey) ? mapping[b.buildingKey].Count : 0;
                Debug.Log($"[LightsOut] {b.type} (Faction {b.factionId}) at {b.pos} (buildingKey={b.buildingKey}) has {count} mapped windows, nya~!");
            }
        }

        void MapWindowsToBuildingsAndSetEmission(bool on)
        {
            // 1. Get all blocks, buildings, and doors
            foreach (var location in FindObjectsOfType<DaggerfallLocation>())
            {
                foreach (var bd in location.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>())
                {
                    var block = bd.GetComponentInParent<DaggerfallWorkshop.DaggerfallRMBBlock>();
                    foreach (var summary in GetAllBuildingSummaries(bd))
                    {
                        Vector3 buildingPos = summary.Position;
                        if (block != null)
                            buildingPos = block.transform.TransformPoint(summary.Position);
                        int buildingKey = summary.buildingKey;
                        string buildingType = summary.BuildingType.ToString();
                        int factionId = summary.FactionId;

                        // Use the door as the "front" of the building if possible, but fallback to Position
                        Vector3 buildingFront = buildingPos;
                        StaticDoor bestDoor;
                        if (TryFindNearestDoor(location, buildingPos, buildingKey, out bestDoor))
                            buildingFront = DaggerfallStaticDoors.GetDoorPosition(bestDoor);
                        else
                        {
                            Debug.LogWarning($"[LightsOut] No door found for buildingKey={buildingKey} at {buildingPos}, skipping, nya!");
                            continue;
                        }

                        // Guess how many windows this model should have (use modelId or fallback)
                        int expectedWindowCount = GuessExpectedWindowCount(summary);

                        // For each "window slot", find nearest window material and set emission
                        var windowsSet = SetNearestWindowsFromDoor(bestDoor, expectedWindowCount, on);

                        Debug.Log($"[LightsOut] {buildingType} (Faction {factionId}) at {buildingFront} (buildingKey={buildingKey}) - Set {windowsSet} windows {(on ? "ON" : "OFF")}, nya!");
                    }
                }
            }
        }

        bool TryFindNearestDoor(DaggerfallLocation location, Vector3 buildingPos, int buildingKey, out StaticDoor nearestDoor)
        {
            nearestDoor = default;
            float minDist = float.MaxValue;
            bool found = false;
            foreach (var doors in location.StaticDoorCollections)
            {
                foreach (var door in doors.Doors)
                {
                    if (door.buildingKey != buildingKey) continue;
                    Vector3 doorPos = DaggerfallStaticDoors.GetDoorPosition(door);
                    float dist = Vector3.Distance(buildingPos, doorPos);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearestDoor = door;
                        found = true;
                    }
                }
            }
            return found;
        }

        // Scene-wide (not block-aware)
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

        int SetNearestWindowsFromDoor(StaticDoor door, int windowCount, bool on)
        {
            Vector3 doorPos = DaggerfallStaticDoors.GetDoorPosition(door);
            Debug.Log($"[LightsOut][DEBUG] Door for building {door.buildingKey} at {doorPos}");
            var windowMats = FindAllWindowMaterials();
            Debug.Log($"[LightsOut][DEBUG] Found {windowMats.Count} window material candidates in scene.");
            var used = new HashSet<Material>();
            int setCount = 0;

            for (int i = 0; i < windowCount; i++)
            {
                float minDist = float.MaxValue;
                Material nearestWin = null;
                foreach (var matInfo in windowMats)
                {
                    float dist = Vector3.Distance(doorPos, matInfo.position);
                    Debug.Log($"[LightsOut][DEBUG] Window {matInfo.mat.name} at {matInfo.position}, dist to door: {dist:0.00}");
                    if (used.Contains(matInfo.mat)) continue;
                    if (dist < minDist && dist < WindowAssignmentRadius)
                    {
                        minDist = dist;
                        nearestWin = matInfo.mat;
                    }
                }
                if (nearestWin != null)
                {
                    Color color = on ? new Color(0.8f, 0.57f, 0.18f) : new Color(0.05f, 0.05f, 0.05f);
                    nearestWin.SetColor("_EmissionColor", color);
                    nearestWin.EnableKeyword("_EMISSION");
                    used.Add(nearestWin);
                    setCount++;
                }
                else
                {
                    // No more windows in range to assign!
                    break;
                }
            }
            return setCount;
        }

        struct WindowMatInfo
        {
            public Material mat;
            public Vector3 position;
        }

        void MapWindowsToBuildingsAndSetEmission_BlockAware(bool on)
        {
            float blockSize = 4096f; // RMB block size in world units (can get from RMBLayout.RMBSide if you want)
            var allBlocks = GetAllBlockOrigins();

            // Build a lookup so we can get blockName and locationTransform for any buildingKey, nya~
            var buildingLookup = GetAllBuildingsWithBlockName()
                .ToDictionary(b => b.buildingKey, b => (b.blockName, b.locationTransform));

            foreach (var (blockOrigin, block) in allBlocks)
            {
                // 1. Get all buildings in this block (in world coordinates!)
                var blockBuildings = new List<(int buildingKey, Vector3 pos, int factionId, string type)>();
                foreach (var bd in block.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>())
                {
                    foreach (var summary in GetAllBuildingSummaries(bd))
                    {
                        // UwU: lookup blockName/locationTransform for this summary!
                        if (buildingLookup.TryGetValue(summary.buildingKey, out var binfo))
                        {
                            Vector3 worldPos = GetBuildingWorldPosition(summary, binfo.blockName, binfo.locationTransform);
                            blockBuildings.Add((summary.buildingKey, worldPos, summary.FactionId, summary.BuildingType.ToString()));
                        }
                        else
                        {
                            // fallback: just use local position
                            blockBuildings.Add((summary.buildingKey, summary.Position, summary.FactionId, summary.BuildingType.ToString()));
                        }
                    }
                }

                // 2. Get all windows in this block
                var windowMats = FindAllWindowMaterialsInBlock(blockOrigin, blockSize);

                // 3. Map windows to nearest building in this block
                var mapping = new Dictionary<int, List<WindowMatInfo>>();
                foreach (var win in windowMats)
                {
                    float minDist = float.MaxValue;
                    int nearestBuildingKey = -1;
                    foreach (var b in blockBuildings)
                    {
                        float dist = Vector3.Distance(win.position, b.pos);
                        if (dist < minDist)
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

                // 4. Set emission for each mapped window!
                foreach (var b in blockBuildings)
                {
                    int count = mapping.ContainsKey(b.buildingKey) ? mapping[b.buildingKey].Count : 0;
                    if (count == 0) continue;
                    foreach (var win in mapping[b.buildingKey])
                    {
                        Color color = on ? new Color(0.8f, 0.57f, 0.18f) : new Color(0.05f, 0.05f, 0.05f);
                        win.mat.SetColor("_EmissionColor", color);
                        win.mat.EnableKeyword("_EMISSION");
                    }
                    Debug.Log($"[LightsOut] {b.type} (Faction {b.factionId}) at {b.pos} (buildingKey={b.buildingKey}) - Set {count} windows {(on ? "ON" : "OFF")}, nya!");
                }
            }
        }

        List<WindowMatInfo> FindAllWindowMaterialsInBlock(Vector3 blockOrigin, float blockSize)
        {
            var results = new List<WindowMatInfo>();
            foreach (var mr in GameObject.FindObjectsOfType<MeshRenderer>())
            {
                Vector3 center = mr.bounds.center;
                if (Vector3.Distance(center, blockOrigin) < blockSize * 0.7f) // slightly > half the block size for tolerance
                {
                    foreach (var mat in mr.materials)
                    {
                        if (mat.HasProperty("_EmissionColor") && IsProbablyWindow(mat))
                            results.Add(new WindowMatInfo { mat = mat, position = center });
                    }
                }
            }
            return results;
        }

        // Guess window count by model (you can expand this for real model logic if you know it)
        int GuessExpectedWindowCount(BuildingSummary summary)
        {
            // TODO: Use summary.ModelID or Type for smarter guess
            // For now, just default to 2 for houses, 3 for shops, 4 for guilds etc.
            switch (summary.BuildingType)
            {
                case DFLocation.BuildingTypes.House1:
                case DFLocation.BuildingTypes.House2:
                case DFLocation.BuildingTypes.House3:
                case DFLocation.BuildingTypes.House4:
                case DFLocation.BuildingTypes.HouseForSale:
                    return 2;
                case DFLocation.BuildingTypes.Tavern:
                case DFLocation.BuildingTypes.GeneralStore:
                case DFLocation.BuildingTypes.Armorer:
                case DFLocation.BuildingTypes.Bookseller:
                case DFLocation.BuildingTypes.ClothingStore:
                case DFLocation.BuildingTypes.FurnitureStore:
                case DFLocation.BuildingTypes.GemStore:
                case DFLocation.BuildingTypes.PawnShop:
                case DFLocation.BuildingTypes.WeaponSmith:
                    return 3;
                case DFLocation.BuildingTypes.GuildHall:
                case DFLocation.BuildingTypes.Temple:
                    return 4;
                default:
                    return 2;
            }
        }

        // Revised GetBuildingWorldPosition, more robust logging and fallback!
        Vector3 GetBuildingWorldPosition(BuildingSummary summary, string blockName, Transform locationTransform)
        {
            Debug.Log($"[LightsOut][DBG] GetBuildingWorldPosition called for buildingKey={summary.buildingKey} in block '{blockName}'");

            if (locationTransform == null)
            {
                Debug.LogWarning($"[LightsOut][DBG] No DaggerfallLocation provided! Returning local position.");
                return summary.Position;
            }

            // Try to find the RMB block by name match (case-insensitive, ignore spaces etc)
            DaggerfallWorkshop.DaggerfallRMBBlock foundBlock = null;
            foreach (var block in locationTransform.GetComponentsInChildren<DaggerfallWorkshop.DaggerfallRMBBlock>())
            {
                string cleanedBlockName = block.name.Replace(" ", "").ToLower();
                string cleanedTarget = blockName.Replace(" ", "").ToLower();
                if (!string.IsNullOrEmpty(blockName) && cleanedBlockName.Contains(cleanedTarget))
                {
                    foundBlock = block;
                    break;
                }
            }

            if (foundBlock == null)
            {
                Debug.LogWarning($"[LightsOut][DBG] No RMB block found matching name '{blockName}'! Returning local position.");
                return summary.Position;
            }

            // Convert block-local position (summary.Position) to world position
            Vector3 worldPos = foundBlock.transform.TransformPoint(summary.Position);

            Debug.Log($"[LightsOut][DBG] BuildingKey={summary.buildingKey} block='{blockName}' block-local={summary.Position} block-world={foundBlock.transform.position} => world={worldPos}");

            return worldPos;
        }

        List<(Vector3 origin, DaggerfallRMBBlock block)> GetAllBlockOrigins()
        {
            var blocks = new List<(Vector3, DaggerfallRMBBlock)>();
            foreach (var block in FindObjectsOfType<DaggerfallWorkshop.DaggerfallRMBBlock>())
                blocks.Add((block.transform.position, block));
            return blocks;
        }

        // --- Utility/Logging/Debug ---
        IEnumerable<BuildingSummary> GetAllBuildingSummaries(BuildingDirectory bd)
        {
            var field = typeof(BuildingDirectory).GetField("buildingDict", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = field?.GetValue(bd) as Dictionary<int, BuildingSummary>;
            return dict != null ? (IEnumerable<BuildingSummary>)dict.Values : new List<BuildingSummary>();
        }

        bool IsProbablyWindow(Material mat)
        {
            // Accept any mat with "_EmissionColor" and Daggerfall shader, name containing "Window" or "[Index=3]" or "[Index=2]"
            string name = mat.name.ToLower();
            bool hasEmission = mat.HasProperty("_EmissionColor");
            bool isDaggerfall = mat.shader && mat.shader.name.Contains("Daggerfall");
            bool nameLooksLikeWindow = name.Contains("window") || name.Contains("[index=3]") || name.Contains("[index=2]");
            return hasEmission && isDaggerfall && nameLooksLikeWindow;
        }

        void DumpAllRMBBlocks()
        {
            foreach (var location in FindObjectsOfType<DaggerfallWorkshop.DaggerfallLocation>())
            {
                Debug.Log($"[LightsOut][DBG] Location: {location.name} @ {location.transform.position}");
                foreach (var block in location.GetComponentsInChildren<DaggerfallWorkshop.DaggerfallRMBBlock>(true))
                {
                    Debug.Log($"[LightsOut][DBG]   Block: {block.name}, localPos={block.transform.localPosition}, worldPos={block.transform.position}, parent={block.transform.parent?.name}");
                }
            }
        }

        void DumpAllBuildingKeys()
        {
            foreach (var location in FindObjectsOfType<DaggerfallLocation>())
            {
                foreach (var bd in location.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>())
                {
                    foreach (var summary in GetAllBuildingSummaries(bd))
                    {
                        int layoutX, layoutY, recordIndex;
                        DaggerfallWorkshop.Game.BuildingDirectory.ReverseBuildingKey(summary.buildingKey, out layoutX, out layoutY, out recordIndex);
                        Debug.Log($"[LightsOut][DBG] BuildingKey={summary.buildingKey} layout=({layoutX},{layoutY}) record={recordIndex} type={summary.BuildingType}");
                    }
                }
            }
        }

        // Gathers all buildings, with their block name and location transform for world space lookups, nya~
        // Gathers all buildings with their world position, faction, and type, nya~
        List<(int buildingKey, Vector3 worldPos, int factionId, string buildingType, BuildingSummary summary)> GetAllBuildingWorldInfo()
        {
            var result = new List<(int, Vector3, int, string, BuildingSummary)>();
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
                            // Convert block-local position to world space
                            Vector3 worldPos = block.transform.TransformPoint(summary.Position);
                            result.Add((summary.buildingKey, worldPos, summary.FactionId, summary.BuildingType.ToString(), summary));
                        }
                    }
                }
            }
            return result;
        }
    }
}
