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
            // 1. Find all RMB blocks in the scene, nya~
            var allBlocks = GameObject.FindObjectsOfType<DaggerfallRMBBlock>();
            Debug.Log($"[LightsOutScript] Found {allBlocks.Length} RMB blocks in the entire scene, nya!");

            // Build a lookup so we can find an RMB block by layoutX, layoutY
            // RMB blocks are usually named like "DaggerfallBlock [TEMPAAH0.RMB]" or similar.
            var blockNameLookup = allBlocks.ToDictionary(
                b => b.name, // fallback, since block LayoutX/LayoutY is not public, use name-matching below
                b => b);

            int totalBuildings = 0;
            // 2. Find all BuildingDirectory components ANYWHERE in the scene
            var allDirs = GameObject.FindObjectsOfType<DaggerfallWorkshop.Game.BuildingDirectory>();
            foreach (var bd in allDirs)
            {
                // Use reflection to access the private 'buildingDict' field
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
                    var key = kvp.Key;
                    var summary = kvp.Value;

                    // The key encodes layoutX, layoutY, recordIndex
                    int layoutX, layoutY, recordIndex;
                    DaggerfallWorkshop.Game.BuildingDirectory.ReverseBuildingKey(key, out layoutX, out layoutY, out recordIndex);

                    // Find the RMB block for this building by matching its name to layoutX/layoutY
                    DaggerfallRMBBlock block = null;
                    foreach (var b in allBlocks)
                    {
                        // Block names contain their RMB name, which you can get from summary.BlockName if available
                        // But fallback: match by index (if you know your block order), or just print block.name for now
                        // You can also log all block names to cross-check!
                        // For now, let's just try all blocks and use the first one (could refine later)
                        if (b.name.Contains(summary.BlockName))
                        {
                            block = b;
                            break;
                        }
                    }

                    // If we can't find the block, just log the local position
                    Vector3 worldPos = summary.Position;
                    if (block != null)
                        worldPos = block.transform.TransformPoint(summary.Position);

                    Debug.Log($"[LightsOutScript] BuildingKey={key} (layout=({layoutX},{layoutY}) record={recordIndex}) Type={summary.BuildingType} Faction={summary.FactionId} WorldPos={worldPos} (block: {block?.name ?? "NOT FOUND"})");
                    totalBuildings++;
                }
            }

            Debug.Log($"[LightsOutScript] Total buildings found and logged: {totalBuildings}");

            // 4. Old logic for RMB blocks/meshes (kept for your investigation, not needed for buildings)
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

            // 5. Log player position for reference
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
