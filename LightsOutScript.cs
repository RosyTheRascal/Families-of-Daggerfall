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
            // 1. Find DaggerfallLocation in scene
            var location = FindObjectOfType<DaggerfallLocation>();
            if (location == null)
            {
                Debug.LogWarning("LightsOutScript: No DaggerfallLocation found, awe you outside a town/city, nya?");
                return;
            }

            // 2. Find all RMB blocks
            var buildingBlocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);

            Debug.Log($"[LightsOutScript] Found {buildingBlocks.Length} RMB blocks in this location, nya!");

            int totalBuildings = 0;
            foreach (var block in buildingBlocks)
            {
                // Log the world position of the RMB block itself!
                Debug.Log($"[LightsOutScript] RMB Block '{block.name}' world position: {block.transform.position}");

                int childCount = 0;
                int meshCount = 0;

                // Log all direct children of the block for investigation
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
                        // Log what the child is for investigation
                        Debug.Log($"[LightsOutScript][DBG] Child '{child.name}' has no DaggerfallMesh (type: {child.GetType()})");
                    }
                }
                Debug.Log($"[LightsOutScript][DBG] RMB Block '{block.name}' had {childCount} children, {meshCount} with DaggerfallMesh, nya!");

                totalBuildings += meshCount;
            }

            Debug.Log($"[LightsOutScript] Total buildings found and logged: {totalBuildings} (in {buildingBlocks.Length} blocks)");

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
