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
            float fuzz = 4.0f; // Try a wider fuzz in case of floating point weirdness, nya!

            foreach (var location in allLocations)
            {
                Vector3 cityOrigin = location.transform.position;
                Debug.Log($"[LightsOutScript][DBG] City origin for '{location.name}' is {cityOrigin}");

                var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);

                int width = location.Summary.BlockWidth;
                int height = location.Summary.BlockHeight;
                Debug.Log($"[LightsOutScript][DBG] '{location.name}' grid size: width={width}, height={height}");

                // Build grid mapping: (x, y) => RMB block, ignore Y (height) when matching!
                var blockGrid = new Dictionary<(int x, int y), DaggerfallRMBBlock>();

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

            // The rest of your original mesh/block logging, nya~!
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
