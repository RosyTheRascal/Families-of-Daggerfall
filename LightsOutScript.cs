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
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            int totalBuildings = 0;
            float rmbSize = 4096f;
            float epsilon = 0.1f; // For floating point comparison

            foreach (var location in allLocations)
            {
                // 1. Build a grid lookup: (x, y) => list of RMB blocks
                var blockGrid = new Dictionary<(int x, int y), List<DaggerfallRMBBlock>>();
                var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);
                foreach (var block in blocks)
                {
                    Vector3 lp = block.transform.localPosition;
                    int x = Mathf.RoundToInt(lp.x / rmbSize);
                    int y = Mathf.RoundToInt(lp.z / rmbSize);
                    var key = (x, y);
                    if (!blockGrid.ContainsKey(key))
                        blockGrid[key] = new List<DaggerfallRMBBlock>();
                    blockGrid[key].Add(block);
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

                        var gridKey = (layoutX, layoutY);
                        DaggerfallRMBBlock rmbBlock = null;
                        if (blockGrid.TryGetValue(gridKey, out var blockList) && blockList.Count > 0)
                        {
                            // Sometimes there are multiple blocks at the same slot (rare), pick the first active one
                            rmbBlock = blockList.FirstOrDefault(b => b.gameObject.activeInHierarchy) ?? blockList[0];
                            Vector3 worldPos = rmbBlock.transform.TransformPoint(summary.Position);
                            Debug.Log($"[LightsOutScript] {location.name} Block=({layoutX},{layoutY}) record={recordIndex} Faction={summary.FactionId} Type={summary.BuildingType} WorldPos={worldPos} (buildingKey={key})");
                        }
                        else
                        {
                            // Try to find a block whose localPosition is "close" to the correct (x, y)
                            bool found = false;
                            foreach (var block in blocks)
                            {
                                Vector3 lp = block.transform.localPosition;
                                int bx = Mathf.RoundToInt(lp.x / rmbSize);
                                int by = Mathf.RoundToInt(lp.z / rmbSize);
                                if (Mathf.Abs(bx - layoutX) < 1 && Mathf.Abs(by - layoutY) < 1)
                                {
                                    if ((lp - new Vector3(layoutX * rmbSize, lp.y, layoutY * rmbSize)).sqrMagnitude < (epsilon * epsilon))
                                    {
                                        rmbBlock = block;
                                        found = true;
                                        break;
                                    }
                                }
                            }
                            if (found && rmbBlock != null)
                            {
                                Vector3 worldPos = rmbBlock.transform.TransformPoint(summary.Position);
                                Debug.Log($"[LightsOutScript][Fuzzy] {location.name} Block=({layoutX},{layoutY}) record={recordIndex} Faction={summary.FactionId} Type={summary.BuildingType} WorldPos={worldPos} (buildingKey={key})");
                            }
                            else
                            {
                                Debug.LogWarning($"[LightsOutScript][WARN] Could not find RMB block at ({layoutX},{layoutY}) in '{location.name}' for buildingKey={key}, logging localPos only.");
                                Debug.Log($"[LightsOutScript] {location.name} Block=({layoutX},{layoutY}) record={recordIndex} Faction={summary.FactionId} Type={summary.BuildingType} LocalPos={summary.Position} (buildingKey={key})");
                            }
                        }
                        totalBuildings++;
                    }
                }
            }

            Debug.Log($"[LightsOutScript] Total buildings found and logged: {totalBuildings}");

            // The rest of your original mesh/block logging...
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
    }
}
