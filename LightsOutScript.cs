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
            }

        }

        public void CollectAndLogBuildingWorldspaceInfo()
        {
            // 1. Find every city/town in the scene (DaggerfallLocation is the city root)
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            int totalBuildings = 0;

            foreach (var location in allLocations)
            {
                // 1a. Build a grid lookup: (layoutX, layoutY) => RMB block GameObject
                var blockGrid = new Dictionary<(int x, int y), DaggerfallRMBBlock>();
                var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>();
                foreach (var block in blocks)
                {
                    // RMB blocks are placed at (x * 4096, 0, y * 4096) in localPosition
                    Vector3 lp = block.transform.localPosition;
                    int x = Mathf.RoundToInt(lp.x / 4096f);
                    int y = Mathf.RoundToInt(lp.z / 4096f); // Z axis is Y in grid!
                    if (!blockGrid.ContainsKey((x, y)))
                        blockGrid[(x, y)] = block;
                    else
                        Debug.LogWarning($"[LightsOutScript][WARN] Duplicate RMB block at ({x},{y}) in {location.name}, nya!");
                }

                // 1b. For each BuildingDirectory (should be just one per city)
                foreach (var bd in location.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>())
                {
                    // Get the private buildingDict field
                    var field = typeof(DaggerfallWorkshop.Game.BuildingDirectory).GetField("buildingDict", BindingFlags.NonPublic | BindingFlags.Instance);
                    var dict = field?.GetValue(bd) as Dictionary<int, BuildingSummary>;
                    if (dict == null)
                    {
                        Debug.LogWarning($"[LightsOutScript][WARN] BuildingDirectory on '{bd.gameObject.name}' has no buildingDict, nya?!");
                        continue;
                    }

                    Debug.Log($"[LightsOutScript] Found BuildingDirectory on '{bd.gameObject.name}', contains {dict.Count} buildings!");

                    foreach (var kvp in dict)
                    {
                        int key = kvp.Key;
                        BuildingSummary summary = kvp.Value;

                        // Decode the building key to get block grid coords & which building in block
                        int layoutX, layoutY, recordIndex;
                        DaggerfallWorkshop.Game.BuildingDirectory.ReverseBuildingKey(key, out layoutX, out layoutY, out recordIndex);

                        // Try to find the RMB block GameObject for this building
                        if (blockGrid.TryGetValue((layoutX, layoutY), out var rmbBlock))
                        {
                            // Convert building's block-local position to world space
                            Vector3 worldPos = rmbBlock.transform.TransformPoint(summary.Position);

                            Debug.Log($"[LightsOutScript] {location.name} Block=({layoutX},{layoutY}) record={recordIndex} Faction={summary.FactionId} Type={summary.BuildingType} WorldPos={worldPos} (buildingKey={key})");
                        }
                        else
                        {
                            // Block not found: fallback to just block-local info
                            Debug.LogWarning($"[LightsOutScript][WARN] Could not find RMB block at ({layoutX},{layoutY}) in '{location.name}' for buildingKey={key}, logging localPos only.");
                            Debug.Log($"[LightsOutScript] {location.name} Block=({layoutX},{layoutY}) record={recordIndex} Faction={summary.FactionId} Type={summary.BuildingType} LocalPos={summary.Position} (buildingKey={key})");
                        }
                        totalBuildings++;
                    }
                }
            }

            Debug.Log($"[LightsOutScript] Total buildings found and logged: {totalBuildings}");

            // Original mesh/block logic (unchanged, still useful for debug!)
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

            // Log player position for reference
            var player = GameManager.Instance.PlayerObject;
            if (player != null)
            {
                Debug.Log($"[LightsOutScript] Player world position: {player.transform.position}");
            }
            else
            {
                Debug.LogWarning("[LightsOutScript] Could not find player object to log position, nya~");
            }
        }


    }
}
