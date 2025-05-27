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
                SpawnFacadeAtFactionBuildings();
                ControlEmissiveWindowTexturesInCombinedModels();
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
                            Debug.Log($"[LightsOutScript] {location.name} Block=({layoutX},{layoutY}) record={recordIndex} Faction={summary.FactionId} Type={summary.BuildingType} WorldPos={worldPos} ([...]");
                        }
                        else
                        {
                            Debug.LogWarning($"[LightsOutScript][WARN] Could not find RMB block at ({layoutX},{layoutY}) in '{location.name}' for buildingKey={key}, logging localPos only.");
                            Debug.Log($"[LightsOutScript] {location.name} Block=({layoutX},{layoutY}) record={recordIndex} Faction={summary.FactionId} Type={summary.BuildingType} LocalPos={summary.Position} ([...]");
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

                // this is the onwy pawt that changed, nya~
                Transform modelsChild = block.transform.Find("Models");
                if (modelsChild != null)
                {
                    Transform combinedModels = modelsChild.Find("CombinedModels");
                    if (combinedModels != null)
                    {
                        var mesh = combinedModels.GetComponent<DaggerfallMesh>();
                        if (mesh != null)
                        {
                            Debug.Log($"[LightsOutScript] Found CombinedModels GameObject with DaggerfallMesh in block '{block.name}': '{combinedModels.name}' | World Pos: {combinedModels.position}");
                            Debug.Log($"[LightsOutScript][DBG] RMB Block '{block.name}' Models->CombinedModels had 1 child (itself!), 1 with DaggerfallMesh, nya!");
                        }
                        else
                        {
                            Debug.Log($"[LightsOutScript][DBG] RMB Block '{block.name}' Models->CombinedModels exists but has no DaggerfallMesh, nya? (name: '{combinedModels.name}')");
                        }
                    }
                    else
                    {
                        Debug.Log($"[LightsOutScript][DBG] RMB Block '{block.name}' has no Models->CombinedModels child, nya!");
                    }
                }
                else
                {
                    Debug.Log($"[LightsOutScript][DBG] RMB Block '{block.name}' has no Models child, nya!");
                }
            }

            var player = GameManager.Instance.PlayerObject;
            if (player != null)
                Debug.Log($"[LightsOutScript] Player world position: {player.transform.position}");
            else
                Debug.LogWarning("[LightsOutScript] Could not find player object to log position, nya~");
        }

        public void SpawnFacadeAtFactionBuildings()
        {
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();

            var houseTypes = new HashSet<DaggerfallConnect.DFLocation.BuildingTypes>
    {
        DaggerfallConnect.DFLocation.BuildingTypes.House1,
        DaggerfallConnect.DFLocation.BuildingTypes.House2,
        DaggerfallConnect.DFLocation.BuildingTypes.House3,
        DaggerfallConnect.DFLocation.BuildingTypes.House4,
        DaggerfallConnect.DFLocation.BuildingTypes.House5,
    };

            foreach (var location in allLocations)
            {
                var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);
                int width = location.Summary.BlockWidth;
                int height = location.Summary.BlockHeight;

                var blockGrid = new Dictionary<(int x, int y), DaggerfallRMBBlock>();
                if (blocks.Length == width * height)
                {
                    var sortedBlocks = blocks.OrderBy(b => b.transform.position.z).ThenBy(b => b.transform.position.x).ToArray();
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int idx = y * width + x;
                            blockGrid[(x, y)] = sortedBlocks[idx];
                        }
                    }
                }
                else if (width == 1 && height == 1 && blocks.Length == 1)
                {
                    blockGrid[(0, 0)] = blocks[0];
                }
                else
                {
                    float rmbSize = 4096f;
                    float fuzz = 4.0f;
                    Vector3 cityOrigin = location.transform.position;
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
                                if (dist < fuzz)
                                {
                                    if (!blockGrid.ContainsKey((x, y)))
                                        blockGrid[(x, y)] = block;
                                    found = true;
                                    break;
                                }
                            }
                            if (found) break;
                        }
                    }
                }

                foreach (var bd in location.GetComponentsInChildren<DaggerfallWorkshop.Game.BuildingDirectory>())
                {
                    var field = typeof(DaggerfallWorkshop.Game.BuildingDirectory).GetField("buildingDict", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var dict = field?.GetValue(bd) as Dictionary<int, BuildingSummary>;
                    if (dict == null)
                        continue;

                    foreach (var kvp in dict)
                    {
                        int key = kvp.Key;
                        BuildingSummary summary = kvp.Value;

                        int layoutX, layoutY, recordIndex;
                        DaggerfallWorkshop.Game.BuildingDirectory.ReverseBuildingKey(key, out layoutX, out layoutY, out recordIndex);

                        if (houseTypes.Contains(summary.BuildingType))
                            continue;

                        if (blockGrid.TryGetValue((layoutX, layoutY), out var rmbBlock))
                        {
                            DaggerfallStaticDoors staticDoors = rmbBlock.GetComponent<DaggerfallStaticDoors>();
                            if (staticDoors == null || staticDoors.Doors == null)
                            {
                                Debug.LogWarning($"[LightsOutScript][WARN] Block '{rmbBlock.name}' has no StaticDoors, nya?");
                                continue;
                            }

                            // Find the StaticDoor for THIS building (by recordIndex)
                            StaticDoor? myDoor = null;
                            foreach (var door in staticDoors.Doors)
                            {
                                if (door.recordIndex == recordIndex)
                                {
                                    myDoor = door;
                                    break;
                                }
                            }

                            if (myDoor == null)
                            {
                                Debug.LogWarning($"[LightsOutScript][WARN] No exterior StaticDoor found for building {summary.BuildingType} in block '{rmbBlock.name}' (recordIndex={recordIndex}), nya~");
                                continue;
                            }

                            // --- Get buildingMatrix rotation and use for facade! (uwu) ---
                            Matrix4x4 buildingMatrix = myDoor.Value.buildingMatrix;
                            Quaternion buildingRotation = DaggerfallWorkshop.Utility.GameObjectHelper.QuaternionFromMatrix(buildingMatrix);

                            // --- The important fix, nya! ---
                            // Instead of using the door's world pos, spawn the facade at the building's origin in world space
                            Vector3 buildingOriginWorldPos = rmbBlock.transform.rotation * buildingMatrix.MultiplyPoint3x4(Vector3.zero) + rmbBlock.transform.position;

                            int modelId = 0;
                            var summaryType = summary.GetType();

                            var modelIdField = summaryType.GetField("ModelID");
                            var modelField = summaryType.GetField("Model");
                            object modelIdValue = null;
                            string usedFieldName = null;

                            if (modelIdField != null)
                            {
                                modelIdValue = modelIdField.GetValue(summary);
                                usedFieldName = "ModelID";
                            }
                            else if (modelField != null)
                            {
                                modelIdValue = modelField.GetValue(summary);
                                usedFieldName = "Model";
                            }
                            else
                            {
                                Debug.LogWarning($"[LightsOutScript][WARN] BuildingSummary type for {summary.BuildingType} has neither ModelID nor Model field, nya~ Skipping!");
                                continue;
                            }

                            if (modelIdValue == null)
                            {
                                Debug.LogWarning($"[LightsOutScript][WARN] {usedFieldName} is null for building type {summary.BuildingType}, skipping, nya~");
                                continue;
                            }
                            try
                            {
                                modelId = Convert.ToInt32(modelIdValue);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[LightsOutScript][WARN] Could not convert {usedFieldName}={modelIdValue} (type={modelIdValue.GetType()}) to int for building type {summary.BuildingType}, skipping, nya~");
                                continue;
                            }

                            Debug.Log($"[LightsOutScript][DBG] About to spawn facade with modelId={modelId} at buildingOriginWorldPos={buildingOriginWorldPos} with rotation={buildingRotation.eulerAngles}, nya~");

                            GameObject buildingGo = DaggerfallWorkshop.Utility.GameObjectHelper.CreateDaggerfallMeshGameObject((uint)modelId, null, true, null, false);
                            if (buildingGo == null)
                            {
                                Debug.LogWarning($"[LightsOutScript][WARN] Could not create mesh GameObject for modelId={modelId} at worldPos={buildingOriginWorldPos} (building type={summary.BuildingType}) in location '{location.name}', nya~");
                                continue;
                            }

                            buildingGo.transform.position = buildingOriginWorldPos;
                            buildingGo.transform.rotation = rmbBlock.transform.rotation * buildingRotation;

                            // === NEW: make it a bit bigger! ===
                            buildingGo.transform.localScale = Vector3.one * 1.01f;

                            buildingGo.name = $"Facade_{summary.BuildingType}_{location.name}_{layoutX}_{layoutY}_{recordIndex}";

                            // ===== MAGIC PART: MATCH CLIMATE & SEASON! =====
                            var mesh = buildingGo.GetComponent<DaggerfallMesh>();
                            if (mesh != null)
                            {
                                mesh.SetClimate(
                                    location.Summary.Climate,
                                    location.CurrentSeason,
                                    location.WindowTextureStyle
                                );
                            }
                            else
                            {
                                Debug.LogWarning($"[LightsOutScript][WARN] Spawned facade '{buildingGo.name}' has no DaggerfallMesh to set climate, nya~");
                            }

                            // === NEW: Remove MeshCollider if it exists! ===
                            var meshCol = buildingGo.GetComponent<MeshCollider>();
                            if (meshCol != null)
                                Destroy(meshCol);

                            buildingGo.transform.SetParent(location.transform, true);
                        }
                    }
                }
            }
        }

        public void ControlEmissiveWindowTexturesInCombinedModels()
        {
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            foreach (var location in allLocations)
            {
                var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);
                foreach (var block in blocks)
                {
                    Transform combinedModelsTransform = block.transform.Find("Models/CombinedModels");
                    if (combinedModelsTransform != null)
                    {
                        var meshes = combinedModelsTransform.GetComponentsInChildren<MeshRenderer>();
                        foreach (var meshRenderer in meshes)
                        {
                            foreach (var material in meshRenderer.materials)
                            {
                                if (material.HasProperty("_EmissionMap") && material.GetTexture("_EmissionMap") != null)
                                {
                                    material.DisableKeyword("_EMISSION");
                                    material.SetTexture("_EmissionMap", null);

                                    Debug.Log($"[LightsOutScript] Emissive texture deactivated for material '{material.name}' in '{combinedModelsTransform.name}', nya~!");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[LightsOutScript][WARN] CombinedModels not found in block '{block.name}', nya~");
                    }
                }
            }
        }
    }
}
