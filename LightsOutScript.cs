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
        private bool emissiveCombinedModelsActive;
        private bool emissiveFacadesActive;
        private bool initialized = false;
        private const int GameSceneIndex = 1;
        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<LightsOutScript>();

            mod.IsReady = true;
        }

        private HashSet<DaggerfallLocation> processedLocations = new HashSet<DaggerfallLocation>();
        private HashSet<string> processedBuildings = new HashSet<string>(); // Tracks buildings processed by facade spawning

        void Awake()
        {

            emissiveCombinedModelsActive = CheckEmissiveTextureStateCombinedModels();
            PlayerEnterExit.OnTransitionExterior += OnExteriorTransitionDetected;

            Debug.Log($"[LightsOutScript] Initial emissiveCombinedModelsActive state: {(emissiveCombinedModelsActive ? "ACTIVE" : "INACTIVE")}, nya~!");
            Debug.Log($"[LightsOutScript] Initial emissiveFacadesActive state: {(emissiveFacadesActive ? "ACTIVE" : "INACTIVE")}, nya~!");
        }

        void Update()
        {
            if (!initialized)
            {
                if (SceneManager.GetActiveScene().buildIndex == GameSceneIndex && GameObject.Find("Exterior")?.activeInHierarchy == true)
                {
                    // Now check both states since Exterior is loaded
                    emissiveCombinedModelsActive = CheckEmissiveTextureStateCombinedModels();
                    emissiveFacadesActive = CheckEmissiveTextureStateFacades();
                    initialized = true;
                    Debug.Log($"[LightsOutScript] Combined Models and Facades initialized, nya~!");
                }
                else
                {
                    Debug.Log($"[LightsOutScript] Waiting for game scene to load and 'Exterior' to be active, nya~!");
                    return;
                }
            }

            // Detect newly loaded DaggerfallLocations
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            var newLocations = allLocations.Where(location => !processedLocations.Contains(location)).ToList();

            if (newLocations.Count > 0)
            {
                StartCoroutine(ProcessNewLocations(newLocations));
            }

            if (Input.GetKeyDown(KeyCode.Quote)) // Toggles emissive window textures for combined models
            {
                emissiveCombinedModelsActive = !emissiveCombinedModelsActive;
                ControlEmissiveWindowTexturesInCombinedModels(emissiveCombinedModelsActive);
            }

            if (Input.GetKeyDown(KeyCode.Semicolon)) // Toggles emissive window textures for facades
            {
                emissiveFacadesActive = !emissiveFacadesActive;
                ControlEmissiveWindowTexturesInFacades(emissiveFacadesActive);
            }

            foreach (var location in allLocations)
            {
                var facadeTransforms = location.GetComponentsInChildren<Transform>()
                                               .Where(t => t.name.StartsWith("Facade_", StringComparison.OrdinalIgnoreCase));

                foreach (var facadeTransform in facadeTransforms)
                {
                    DFLocation.BuildingTypes buildingType = GetBuildingTypeFromFacadeName(facadeTransform.name);
                    bool shouldEnableFacadeEmission = ShouldEnableEmissiveForBuildingType(buildingType);

                    // Track previous state for each facade to avoid redundant updates nya~!
                    if (!processedBuildings.Contains(facadeTransform.name))
                    {
                        // Add to processedBuildings to initialize tracking nya~!
                        processedBuildings.Add(facadeTransform.name);
                        emissiveFacadesActive = shouldEnableFacadeEmission;
                        ControlEmissiveWindowTexturesInFacades(emissiveFacadesActive);
                        Debug.Log($"[LightsOutScript] Initialized Facades emissive state for BuildingType '{buildingType}' to {(emissiveFacadesActive ? "ACTIVE" : "INACTIVE")}, nya~!");
                    }
                    else if (emissiveFacadesActive != shouldEnableFacadeEmission)
                    {
                        // Only update if emissive state changes nya~!
                        emissiveFacadesActive = shouldEnableFacadeEmission;
                        ControlEmissiveWindowTexturesInFacades(emissiveFacadesActive);
                        Debug.Log($"[LightsOutScript] Facades emissive state updated for BuildingType '{buildingType}' to {(emissiveFacadesActive ? "ACTIVE" : "INACTIVE")} based on current time, nya~!");
                    }
                }
            }

            // Check time-based emissive activation/deactivation
            int currentHour = DaggerfallUnity.Instance.WorldTime.Now.Hour;

            if (currentHour >= 22 || currentHour < 6) // Between 22:00 and 6:00 -> Deactivate emissives
            {
                if (emissiveCombinedModelsActive)
                {
                    emissiveCombinedModelsActive = false;
                    ControlEmissiveWindowTexturesInCombinedModels(emissiveCombinedModelsActive);
                    Debug.Log("[LightsOutScript] Emissive textures automatically deactivated due to time, nya~!");
                }
            }
            else if (currentHour >= 6 && currentHour < 8) // Between 6:00 and 8:00 -> Reactivate emissives
            {
                if (!emissiveCombinedModelsActive)
                {
                    emissiveCombinedModelsActive = true;
                    ControlEmissiveWindowTexturesInCombinedModels(emissiveCombinedModelsActive);
                    Debug.Log("[LightsOutScript] Emissive textures automatically reactivated due to time, nya~!");
                }
            }

            // Handle save loading
            SaveLoadManager.OnLoad += delegate
            {
                Debug.Log("[LightsOutScript] Save loaded, rechecking emissive states, nya~!");
                ApplyTimeBasedEmissiveChanges();
            };
        }

        private void ApplyTimeBasedEmissiveChanges()
        {
            int currentHour = DaggerfallUnity.Instance.WorldTime.Now.Hour;

            if (currentHour >= 22 || currentHour < 6)
            {
                emissiveCombinedModelsActive = false;
                ControlEmissiveWindowTexturesInCombinedModels(emissiveCombinedModelsActive);

                emissiveFacadesActive = false;
                ControlEmissiveWindowTexturesInFacades(emissiveFacadesActive);

                Debug.Log("[LightsOutScript] Applied time-based emissive deactivation, nya~!");
            }
            else if (currentHour >= 6 && currentHour < 8)
            {
                emissiveCombinedModelsActive = true;
                ControlEmissiveWindowTexturesInCombinedModels(emissiveCombinedModelsActive);

                emissiveFacadesActive = true;
                ControlEmissiveWindowTexturesInFacades(emissiveFacadesActive);

                Debug.Log("[LightsOutScript] Applied time-based emissive activation, nya~!");
            }
        }

        private bool CheckEmissiveTextureStateCombinedModels()
        {
            // Logic specifically for combined models
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            foreach (var location in allLocations)
            {
                var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);
                foreach (var block in blocks)
                {
                    var combinedModelsTransform = block.transform.Find("Models/CombinedModels");
                    if (combinedModelsTransform != null)
                    {
                        var meshes = combinedModelsTransform.GetComponentsInChildren<MeshRenderer>();
                        foreach (var meshRenderer in meshes)
                        {
                            foreach (var material in meshRenderer.materials)
                            {
                                if (material.HasProperty("_EmissionMap") && material.IsKeywordEnabled("_EMISSION"))
                                {
                                    Debug.Log($"[LightsOutScript] Combined Models emissive detected ACTIVE, nya~!");
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log($"[LightsOutScript] Combined Models emissive detected INACTIVE, nya~!");
            return false;
        }

        private bool CheckEmissiveTextureStateFacades()
        {

            if (SceneManager.GetActiveScene().buildIndex != GameSceneIndex || GameObject.Find("Exterior")?.activeInHierarchy != true)
            {
                Debug.LogWarning($"[LightsOutScript] Scene or Exterior not ready, deferring Facades emissive check, nya~!");
                return emissiveFacadesActive; // Return the current state while waiting
            }

            // Logic specifically for facades
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            foreach (var location in allLocations)
            {
                var facadeTransforms = location.GetComponentsInChildren<Transform>()
                                               .Where(t => t.name.StartsWith("Facade_", StringComparison.OrdinalIgnoreCase));
                foreach (var facadeTransform in facadeTransforms)
                {
                    var meshes = facadeTransform.GetComponentsInChildren<MeshRenderer>();
                    foreach (var meshRenderer in meshes)
                    {
                        foreach (var material in meshRenderer.materials)
                        {
                            if (material.HasProperty("_EmissionMap") && material.IsKeywordEnabled("_EMISSION"))
                            {
                                Debug.Log($"[LightsOutScript] Facades emissive detected ACTIVE, nya~!");
                                emissiveFacadesActive = true; // Update the state
                                return true;
                            }
                        }
                    }
                }
            }

            Debug.Log($"[LightsOutScript] Facades emissive detected INACTIVE, nya~!");
            emissiveFacadesActive = false; // Update the state
            return false;
        }

        private void OnExteriorTransitionDetected(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("[LightsOutScript] Exterior transition detected, nya~!");
            ApplyTimeBasedEmissiveChanges();
        }

        private bool deferred = false;

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

        public void SpawnFacadeAtFactionBuildings(DaggerfallLocation location)
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

            // Define house types to exclude
            var houseTypes = new HashSet<DaggerfallConnect.DFLocation.BuildingTypes>
    {
        DaggerfallConnect.DFLocation.BuildingTypes.House1,
        DaggerfallConnect.DFLocation.BuildingTypes.House2,
        DaggerfallConnect.DFLocation.BuildingTypes.House3,
        DaggerfallConnect.DFLocation.BuildingTypes.House4,
        DaggerfallConnect.DFLocation.BuildingTypes.House5,
        DaggerfallConnect.DFLocation.BuildingTypes.House6,
        DaggerfallConnect.DFLocation.BuildingTypes.Ship,
    };

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

                    // Skip buildings that are house types
                    if (houseTypes.Contains(summary.BuildingType))
                        continue;

                    int layoutX, layoutY, recordIndex;
                    DaggerfallWorkshop.Game.BuildingDirectory.ReverseBuildingKey(key, out layoutX, out layoutY, out recordIndex);

                    string buildingId = $"{location.name}_{layoutX}_{layoutY}_{recordIndex}";

                    if (processedBuildings.Contains(buildingId))
                        continue;

                    if (blockGrid.TryGetValue((layoutX, layoutY), out var rmbBlock))
                    {
                        DaggerfallStaticDoors staticDoors = rmbBlock.GetComponent<DaggerfallStaticDoors>();
                        if (staticDoors == null || staticDoors.Doors == null)
                        {
                            Debug.LogWarning($"[LightsOutScript][WARN] Block '{rmbBlock.name}' has no StaticDoors, nya?");
                            continue;
                        }

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

                        Matrix4x4 buildingMatrix = myDoor.Value.buildingMatrix;
                        Quaternion buildingRotation = DaggerfallWorkshop.Utility.GameObjectHelper.QuaternionFromMatrix(buildingMatrix);

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
                            Debug.LogWarning($"[LightsOutScript][WARN] Could not convert {usedFieldName}={modelIdValue} (type={modelIdValue.GetType()}) to int for building type {summary.BuildingType} nya~ Error: {ex.Message}");
                            continue;
                        }

                        Debug.Log($"[LightsOutScript][DBG] About to spawn facade with modelId={modelId} at buildingOriginWorldPos={buildingOriginWorldPos} with rotation={buildingRotation.eulerAngles}");

                        GameObject buildingGo = DaggerfallWorkshop.Utility.GameObjectHelper.CreateDaggerfallMeshGameObject((uint)modelId, null, true, null, false);
                        if (buildingGo == null)
                        {
                            Debug.LogWarning($"[LightsOutScript][WARN] Could not create mesh GameObject for modelId={modelId} at worldPos={buildingOriginWorldPos} (building type={summary.BuildingType}), nya~");
                            continue;
                        }

                        buildingGo.transform.position = buildingOriginWorldPos;
                        buildingGo.transform.rotation = rmbBlock.transform.rotation * buildingRotation;
                        buildingGo.transform.localScale = Vector3.one * 1.01f;
                        buildingGo.name = $"Facade_{summary.BuildingType}_{location.name}_{layoutX}_{layoutY}_{recordIndex}";

                        var mesh = buildingGo.GetComponent<DaggerfallMesh>();
                        if (mesh != null)
                        {
                            mesh.SetClimate(location.Summary.Climate, location.CurrentSeason, location.WindowTextureStyle);
                        }
                        else
                        {
                            Debug.LogWarning($"[LightsOutScript][WARN] Spawned facade '{buildingGo.name}' has no DaggerfallMesh to set climate, nya~");
                        }

                        var meshCol = buildingGo.GetComponent<MeshCollider>();
                        if (meshCol != null)
                            Destroy(meshCol);

                        buildingGo.transform.SetParent(location.transform, true);

                        // Mark this building as processed
                        processedBuildings.Add(buildingId);
                    }
                }
            }
            CheckEmissiveTextureStateFacades();
        }

        public void ControlEmissiveWindowTexturesInCombinedModels(bool enableEmissive)
        {
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            foreach (var location in allLocations)
            {
                var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);
                foreach (var block in blocks)
                {
                    var combinedModelsTransform = block.transform.Find("Models/CombinedModels");
                    if (combinedModelsTransform != null)
                    {
                        var meshes = combinedModelsTransform.GetComponentsInChildren<MeshRenderer>();
                        foreach (var meshRenderer in meshes)
                        {
                            foreach (var material in meshRenderer.materials)
                            {
                                if (material.HasProperty("_EmissionMap"))
                                {
                                    if (enableEmissive)
                                    {
                                        material.EnableKeyword("_EMISSION");
                                        Debug.Log($"[LightsOutScript] Activated emissive for Combined Models material '{material.name}', nya~!");
                                    }
                                    else
                                    {
                                        material.DisableKeyword("_EMISSION");
                                        Debug.Log($"[LightsOutScript] Deactivated emissive for Combined Models material '{material.name}', nya~!");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[LightsOutScript] CombinedModels not found in block '{block.name}', nya~!");
                    }
                }
            }
        }

        public void ControlEmissiveWindowTexturesInFacades(bool enableEmissive)
        {
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            foreach (var location in allLocations)
            {
                var facadeTransforms = location.GetComponentsInChildren<Transform>()
                                               .Where(t => t.name.StartsWith("Facade_", StringComparison.OrdinalIgnoreCase));
                foreach (var facadeTransform in facadeTransforms)
                {
                    // Determine if emissive should be enabled based on BuildingType and time
                    DFLocation.BuildingTypes buildingType = GetBuildingTypeFromFacadeName(facadeTransform.name);
                    bool shouldEnableEmissive = ShouldEnableEmissiveForBuildingType(buildingType);

                    var meshes = facadeTransform.GetComponentsInChildren<MeshRenderer>();
                    foreach (var meshRenderer in meshes)
                    {
                        foreach (var material in meshRenderer.materials)
                        {
                            if (material.HasProperty("_EmissionMap"))
                            {
                                if (shouldEnableEmissive)
                                {
                                    material.EnableKeyword("_EMISSION");
                                    Debug.Log($"[LightsOutScript] Activated emissive for Facades material '{material.name}' (BuildingType={buildingType}), nya~!");
                                }
                                else
                                {
                                    material.DisableKeyword("_EMISSION");
                                    Debug.Log($"[LightsOutScript] Deactivated emissive for Facades material '{material.name}' (BuildingType={buildingType}), nya~!");
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool ShouldEnableEmissiveForBuildingType(DFLocation.BuildingTypes buildingType)
        {
            int currentHour = DaggerfallUnity.Instance.WorldTime.Now.Hour;

            switch (buildingType)
            {
                case DFLocation.BuildingTypes.Alchemist:
                    return currentHour < 22 && currentHour >= 7;
                case DFLocation.BuildingTypes.Armorer:
                    return currentHour < 19 && currentHour >= 9;
                case DFLocation.BuildingTypes.Bank:
                    return currentHour < 15 && currentHour >= 8;
                case DFLocation.BuildingTypes.Bookseller:
                    return currentHour < 21 && currentHour >= 9;
                case DFLocation.BuildingTypes.ClothingStore:
                    return currentHour < 19 && currentHour >= 10;
                case DFLocation.BuildingTypes.GemStore:
                    return currentHour < 18 && currentHour >= 9;
                case DFLocation.BuildingTypes.GeneralStore:
                    return currentHour < 23 && currentHour >= 6;
                case DFLocation.BuildingTypes.Library:
                    return currentHour < 23 && currentHour >= 9;
                case DFLocation.BuildingTypes.PawnShop:
                    return currentHour < 20 && currentHour >= 9;
                case DFLocation.BuildingTypes.WeaponSmith:
                    return currentHour < 19 && currentHour >= 9;
                case DFLocation.BuildingTypes.GuildHall:
                    return currentHour < 23 && currentHour >= 11;
                case DFLocation.BuildingTypes.Temple:
                case DFLocation.BuildingTypes.Tavern:
                    return true; // Never deactivate nya~!
                case DFLocation.BuildingTypes.HouseForSale:
                    return false; // Always deactivate nya~!
                default:
                    Debug.LogWarning($"[LightsOutScript][WARN] Unknown BuildingType '{buildingType}', defaulting to deactivate emissive, nya~!");
                    return false;
            }
        }

        private DFLocation.BuildingTypes GetBuildingTypeFromFacadeName(string facadeName)
        {
            string[] parts = facadeName.Split('_');
            if (parts.Length < 2)
            {
                Debug.LogWarning($"[LightsOutScript][WARN] Invalid facade name format '{facadeName}', nya~!");
                return DFLocation.BuildingTypes.None;
            }

            string buildingTypeName = parts[1];
            if (Enum.TryParse(buildingTypeName, out DFLocation.BuildingTypes buildingType))
            {
                return buildingType;
            }
            else
            {
                Debug.LogWarning($"[LightsOutScript][WARN] Could not parse BuildingType from facade name '{facadeName}', nya~!");
                return DFLocation.BuildingTypes.None;
            }
        }

        private IEnumerator ProcessNewLocations(List<DaggerfallLocation> newLocations)
        {
            // Wait a couple of frames to ensure all info is loaded
            yield return null;
            yield return null;

            foreach (var location in newLocations)
            {
                // Process each location for new facades
                SpawnFacadeAtFactionBuildings(location);

                // Add this location to the processed list
                processedLocations.Add(location);
            }
        }
    }
}
