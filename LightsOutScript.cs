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

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<LightsOutScript>();

            mod.IsReady = true;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Semicolon)) // Pick any debug key you like
            {
                CollectAndLogBuildingWorldspaceInfo();
                ListAllWindowMaterialsAndLogPositions();
                //MapAndLogWindowsByBuildingKey();
                CreateFacadesForNonResidentials();
            }

        }

        public void CollectAndLogBuildingWorldspaceInfo()
        {
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            int totalBuildings = 0;
            float rmbSize = 4096f;
            float fuzz = 4.0f;

            foreach (var location in allLocations)
            {
                Vector3 cityOrigin = location.transform.position;
                Debug.Log($"[LightsOutScript][DBG] City origin for '{location.name}' is {cityOrigin}");

                var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);

                int width = location.Summary.BlockWidth;
                int height = location.Summary.BlockHeight;
                Debug.Log($"[LightsOutScript][DBG] '{location.name}' grid size: width={width}, height={height}");

                var blockGrid = new Dictionary<(int x, int y), DaggerfallRMBBlock>();

                if (blocks.Length == width * height)
                {
                    // Sort blocks by Z (south to north), then X (west to east)
                    var sortedBlocks = blocks.OrderBy(b => b.transform.position.z).ThenBy(b => b.transform.position.x).ToArray();

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int idx = y * width + x;
                            blockGrid[(x, y)] = sortedBlocks[idx];
                            Debug.Log($"[LightsOutScript][DBG] Auto-mapped block '{sortedBlocks[idx].name}' at {sortedBlocks[idx].transform.position} to grid ({x},{y})");
                        }
                    }
                }
                else if (width == 1 && height == 1 && blocks.Length == 1)
                {
                    blockGrid[(0, 0)] = blocks[0];
                    Debug.Log($"[LightsOutScript][DBG] Special case: village with 1x1 grid, mapped block '{blocks[0].name}' at {blocks[0].transform.position} to (0,0)");
                }
                else
                {
                    // Fallback to old (city) logic if weird mismatch
                    foreach (var block in blocks)
                    {
                        Vector3 wp = block.transform.position;
                        bool found = false;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                Vector3 expected = cityOrigin + new Vector3(x * rmbSize, 0, y * rmbSize);
                                float dx = wp.x - expected.x;
                                float dz = wp.z - expected.z;
                                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                                Debug.Log($"[LightsOutScript][DBG] Comparing block '{block.name}' @ XZ=({wp.x},{wp.z}) to grid ({x},{y}) expected XZ=({expected.x},{expected.z}) dist={dist}");

                                if (dist < fuzz)
                                {
                                    if (!blockGrid.ContainsKey((x, y)))
                                    {
                                        blockGrid[(x, y)] = block;
                                        Debug.Log($"[LightsOutScript][DBG] Mapped '{block.name}' to grid ({x},{y})");
                                    }
                                    found = true;
                                    break;
                                }
                            }
                            if (found) break;
                        }
                        if (!found)
                        {
                            Debug.LogWarning($"[LightsOutScript][WARN] RMB block '{block.name}' at {wp} could not be mapped to grid position in '{location.name}'!");
                        }
                    }
                }

                foreach (var bd in location.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>())
                {
                    var field = typeof(DaggerfallWorkshop.Game.BuildingDirectory).GetField("buildingDict", BindingFlags.NonPublic | BindingFlags.Instance);
                    var dict = field?.GetValue(bd) as Dictionary<int, BuildingSummary>;
                    if (dict == null)
                    {
                        Debug.LogWarning($"[LightsOutScript][WARN] BuildingDirectory on '{bd.gameObject.name}' has no buildingDict, nya?!");
                        continue;
                    }

                    Debug.Log($"[LightsOutScript] Found BuildingDirectory on '{location.name}', contains {dict.Count} buildings!");

                    foreach (var kvp in dict)
                    {
                        int key = kvp.Key;
                        BuildingSummary summary = kvp.Value;

                        int layoutX, layoutY, recordIndex;
                        DaggerfallWorkshop.Game.BuildingDirectory.ReverseBuildingKey(key, out layoutX, out layoutY, out recordIndex);

                        Debug.Log($"[LightsOutScript][DBG] BuildingKey={key} expects grid=({layoutX},{layoutY})");

                        if (blockGrid.TryGetValue((layoutX, layoutY), out var rmbBlock))
                        {
                            Vector3 worldPos = rmbBlock.transform.TransformPoint(summary.Position);
                            Debug.Log($"[LightsOutScript] {location.name} Block=({layoutX},{layoutY}) record={recordIndex} Faction={summary.FactionId} Type={summary.BuildingType} WorldPos={worldPos} (buildingKey={key})");
                        }
                        else
                        {
                            Debug.LogWarning($"[LightsOutScript][WARN] Could not find RMB block at ({layoutX},{layoutY}) in '{location.name}' for buildingKey={key}, logging localPos only.");
                            Debug.Log($"[LightsOutScript] {location.name} Block=({layoutX},{layoutY}) record={recordIndex} Faction={summary.FactionId} Type={summary.BuildingType} LocalPos={summary.Position} (buildingKey={key})");
                        }
                        totalBuildings++;
                    }
                }
            }

            Debug.Log($"[LightsOutScript] Total buildings found and logged: {totalBuildings}");

            var allBlocks = GameObject.FindObjectsOfType<DaggerfallRMBBlock>();
            foreach (var block in allBlocks)
            {
                Debug.Log($"[LightsOutScript] RMB Block '{block.name}' world position: {block.transform.position}");

                int childCount = 0;
                int meshCount = 0;
                foreach (Transform child in block.transform)
                {
                    childCount++;
                    var mesh = child.GetComponent<DaggerfallMesh>();
                    if (mesh != null)
                    {
                        Debug.Log($"[LightsOutScript] Building GameObject: '{child.name}' | World Pos: {child.position} (block: {block.name})");
                        meshCount++;
                    }
                    else
                    {
                        Debug.Log($"[LightsOutScript][DBG] Child '{child.name}' has no DaggerfallMesh (type: {child.GetType()})");
                    }
                }
                Debug.Log($"[LightsOutScript][DBG] RMB Block '{block.name}' had {childCount} children, {meshCount} with DaggerfallMesh, nya!");
            }

            var player = GameManager.Instance.PlayerObject;
            if (player != null)
                Debug.Log($"[LightsOutScript] Player world position: {player.transform.position}");
            else
                Debug.LogWarning("[LightsOutScript] Could not find player object to log position, nya~");
        }

        // UwU: New IsProbablyWindow logic, as you described!
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

        // (o･ω･o) This collects all building info (key, worldPos, factionId, type) as a list!
        // This method finds EVERY building in every loaded location, gets their world position, and their faction and type, and returns a list for you!
        List<(int buildingKey, Vector3 worldPos, int factionId, string buildingType, BuildingSummary summary)> GetAllBuildingWorldspaceInfo()
        {

            var result = new List<(int, Vector3, int, string, BuildingSummary)>();
            foreach (var location in FindObjectsOfType<DaggerfallLocation>())
            {
                var locationTransform = location.transform;
                // Go through every RMB block in this location
                foreach (var block in location.GetComponentsInChildren<DaggerfallWorkshop.DaggerfallRMBBlock>())
                {
                    string blockName = block.name;
                    // Get all BuildingDirectory scripts in this block (they keep track of buildings!)
                    foreach (var bd in block.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>())
                    {
                        // Extract a list of all buildings from the private field (using Reflection, ow!)
                        var field = typeof(BuildingDirectory).GetField("buildingDict", BindingFlags.NonPublic | BindingFlags.Instance);
                        var dict = field?.GetValue(bd) as Dictionary<int, BuildingSummary>;
                        if (dict == null) continue;
                        foreach (var summary in dict.Values)
                        {
                            // Convert the building's local position (in the block) to world position, nya!
                            Vector3 worldPos = block.transform.TransformPoint(summary.Position);
                            result.Add((summary.buildingKey, worldPos, summary.FactionId, summary.BuildingType.ToString(), summary));
                        }
                    }
                }
            }
            var allBuildings = GetAllBuildingWorldspaceInfo();
            int nonZeroFactionCount = allBuildings.Count(b => b.factionId != 0);
            Debug.Log($"[LightsOutScript] Total non-0-faction buildings: {nonZeroFactionCount}");
            return result;
        }

        // =^._.^= ∫  List and log every window material in the scene, nya!
        void ListAllWindowMaterialsAndLogPositions()
        {
            int count = 0;
            foreach (var mr in GameObject.FindObjectsOfType<MeshRenderer>())
            {
                Vector3 worldPos = mr.bounds.center;
                foreach (var mat in mr.materials)
                {
                    if (IsProbablyWindow(mat))
                    {
                        Debug.Log($"[LightsOut][WindowDump] Found window! Material='{mat.name}' at position={worldPos}");
                        count++;
                    }
                }
            }
            Debug.Log($"[LightsOut][WindowDump] Total windows found: {count} nya~!");
        }

        // (｡･ω･｡)ﾉ♡ This will map each window in the scene to its nearest building (via static door), and log the totals, nya!
        void MapAndLogWindowsByBuildingKey()
        {
            // 1. Gather all buildings with their buildingKeys
            var buildingMap = new Dictionary<int, string>(); // buildingKey -> buildingType (for logging)
            foreach (var location in FindObjectsOfType<DaggerfallLocation>())
            {
                foreach (var bd in location.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>())
                {
                    foreach (var summary in GetAllBuildingSummaries(bd))
                    {
                        buildingMap[summary.buildingKey] = summary.BuildingType.ToString();
                    }
                }
            }

            // 2. Gather all static doors, grouped by buildingKey
            var doorPositions = new Dictionary<int, List<Vector3>>(); // buildingKey -> door world positions
            foreach (var doorsObj in FindObjectsOfType<DaggerfallWorkshop.DaggerfallStaticDoors>())
            {
                foreach (var door in doorsObj.Doors)
                {
                    if (!doorPositions.ContainsKey(door.buildingKey))
                        doorPositions[door.buildingKey] = new List<Vector3>();
                    doorPositions[door.buildingKey].Add(DaggerfallWorkshop.DaggerfallStaticDoors.GetDoorPosition(door));
                }
            }

            // 3. Gather all window candidates (world position)
            var windowList = new List<(Material mat, Vector3 pos)>();
            foreach (var mr in GameObject.FindObjectsOfType<MeshRenderer>())
            {
                Vector3 center = mr.bounds.center;
                foreach (var mat in mr.materials)
                {
                    if (IsProbablyWindow(mat))
                        windowList.Add((mat, center));
                }
            }

            // 4. Assign each window to the nearest door/buildingKey
            var buildingWindows = new Dictionary<int, List<Vector3>>(); // buildingKey -> window positions
            float maxAssignDist = 16f; // fudge this if needed, nya~
            foreach (var (mat, winPos) in windowList)
            {
                float minDist = float.MaxValue;
                int closestKey = -1;
                foreach (var kvp in doorPositions)
                {
                    foreach (var doorPos in kvp.Value)
                    {
                        float dist = Vector3.Distance(winPos, doorPos);
                        if (dist < minDist && dist < maxAssignDist)
                        {
                            minDist = dist;
                            closestKey = kvp.Key;
                        }
                    }
                }
                if (closestKey != -1)
                {
                    if (!buildingWindows.ContainsKey(closestKey))
                        buildingWindows[closestKey] = new List<Vector3>();
                    buildingWindows[closestKey].Add(winPos);
                }
                else
                {
                    Debug.LogWarning($"[LightsOut][WindowDump] Window at {winPos} could not be mapped to a building!");
                }
            }

            // 5. Log the window count per building, nya!
            int totWindows = 0;
            foreach (var kvp in buildingWindows)
            {
                string btype = buildingMap.ContainsKey(kvp.Key) ? buildingMap[kvp.Key] : "???";
                Debug.Log($"[LightsOut][WindowDump] BuildingKey={kvp.Key} ({btype}) has {kvp.Value.Count} windows.");
                totWindows += kvp.Value.Count;
            }
            Debug.Log($"[LightsOut][WindowDump] Total windows mapped to buildings: {totWindows} nya~!");
        }

        // (｡･ω･｡)ﾉ♡ spawn facades fow each non-residential building!
        // (｡･ω･｡)ﾉ♡ spawns a cute facade for each non-residential building (faction > 0) at its world position!
        // (｡･ω･｡)ﾉ♡ spawn a facade for every non-residential building!
        void CreateFacadesForNonResidentials()
        {
            var buildings = GetAllBuildingWorldspaceInfo();
            int facades = 0;

            foreach (var b in buildings)
            {
                if (b.factionId == 0)
                    continue;

                GameObject facadeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                facadeGO.name = $"Facade_{b.buildingKey}_{b.buildingType}";

                // Set position (keep y the same, or maybe raise it a bit so it doesn't intersect ground)
                facadeGO.transform.position = b.worldPos + new Vector3(0, 4f, 0); // raise by 4 units for visibility, adjust as needed

                // Scale the cube to kinda cover a buiwding, you can tweak
                facadeGO.transform.localScale = new Vector3(8f, 8f, 8f);

                // Remove collider so player can still click doors/windows
                var collider = facadeGO.GetComponent<Collider>();
                if (collider) DestroyImmediate(collider);

                // Make it look shadowy/dark
                var renderer = facadeGO.GetComponent<MeshRenderer>();
                if (renderer)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.1f, 0.1f, 0.1f, 0.85f); // dark and kinda see-thru
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                    renderer.material = mat;
                }

                Debug.Log($"[LightsOutScript] Facade spawned for {b.buildingType} (faction={b.factionId}) at {b.worldPos} (buildingKey={b.buildingKey})");
                facades++;
            }
            Debug.Log($"[LightsOutScript] Facades spawned: {facades}");
        }
    }
}
