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
            // Find all RMB blocks in the scene, nya~
            var allBlocks = GameObject.FindObjectsOfType<DaggerfallRMBBlock>();

            Debug.Log($"[LightsOutScript] Found {allBlocks.Length} RMB blocks in the entire scene, nya!");

            int totalBuildings = 0;
            foreach (var block in allBlocks)
            {
                // Log world position of the RMB block itself!
                Debug.Log($"[LightsOutScript] RMB Block '{block.name}' world position: {block.transform.position}");

                int childCount = 0;
                int meshCount = 0;

                // Old logic: log all direct children for investigation
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

                // -------- NEW LOGIC: Enumerate buildings by BuildingDirectory! --------
                // Look for BuildingDirectory on this block or its children!
                var bds = block.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>(true);
                int buildingsInBlock = 0;
                foreach (var bd in bds)
                {
                    // Use reflection to access the private 'buildingDict' field
                    var field = typeof(DaggerfallWorkshop.Game.BuildingDirectory).GetField("buildingDict", BindingFlags.NonPublic | BindingFlags.Instance);
                    var dict = field?.GetValue(bd) as Dictionary<int, BuildingSummary>;
                    if (dict == null)
                    {
                        Debug.LogWarning($"[LightsOutScript][WARN] BuildingDirectory on block '{block.name}' has no buildingDict, nya?!");
                        continue;
                    }

                    foreach (var kvp in dict)
                    {
                        var key = kvp.Key;
                        var summary = kvp.Value;
                        // Convert from block-local to worldspace
                        Vector3 worldPos = block.transform.TransformPoint(summary.Position);
                        Debug.Log($"[LightsOutScript] Building Key={key} Type={summary.BuildingType} Faction={summary.FactionId} WorldPos={worldPos} (block: {block.name})");
                        buildingsInBlock++;
                        totalBuildings++;
                    }
                }
                Debug.Log($"[LightsOutScript] RMB Block '{block.name}' had {buildingsInBlock} buildings from BuildingDirectory (in addition to {meshCount} mesh objects), nya!");
            }

            Debug.Log($"[LightsOutScript] Total buildings found and logged: {totalBuildings} (in {allBlocks.Length} blocks)");

            // 3. Log player position for reference
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
