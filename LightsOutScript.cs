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
                Debug.LogWarning("LightsOutScript: No DaggerfallLocation found, are you outside a town/city?");
                return;
            }

            // 2. Find all buildings (they have DaggerfallRMBBlock and are children of location)
            var buildingBlocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);

            Debug.Log($"[LightsOutScript] Found {buildingBlocks.Length} RMB blocks in this location, nya!");

            int totalBuildings = 0;

            foreach (var block in buildingBlocks)
            {
                // Each block has lots of children, but buildings are usually top-level children under the block
                foreach (Transform child in block.transform)
                {
                    // Heuristic: buildings have DaggerfallMesh, are usually named "CombinedModels" or similar
                    var mesh = child.GetComponent<DaggerfallWorkshop.Utility.DaggerfallMesh>();
                    if (mesh != null)
                    {
                        // Log building name and worldspace position
                        Debug.Log($"[LightsOutScript] Building GameObject: '{child.name}' | World Pos: {child.position} (block: {block.name})");
                        totalBuildings++;
                    }
                }
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
