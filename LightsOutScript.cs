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
using CustomStaticNPCMod;
using CustomNPCBridgeMod;
using FactionNPCInitializerMod;
using FactionParserMod;
using FamilyNameModifierMod;
using CustomNPCClickHandlerMod;
using CustomTalkManagerMod;
using CustomDaggerfallTalkWindowMod;

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
            DaggerfallWorkshop.Game.PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            emissiveCombinedModelsActive = CheckEmissiveTextureStateCombinedModels();
            PlayerEnterExit.OnTransitionExterior += OnExteriorTransitionDetected;
            WorldTime.OnNewHour += HandleNewHourEvent;
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
                    // Determine if emissive should be enabled based on BuildingType and time
                    DFLocation.BuildingTypes buildingType = GetBuildingTypeFromFacadeName(facadeTransform.name);
                    bool shouldEnableEmissive = ShouldEnableEmissiveForBuildingType(buildingType);

                    // Add facade to the processedBuildings if not already tracked nya~!
                    if (!processedBuildings.Contains(facadeTransform.name))
                    {
                        processedBuildings.Add(facadeTransform.name);
                        Debug.Log($"[LightsOutScript] Tracking new facade '{facadeTransform.name}', nya~!");
                    }

                    // Skip redundant updates if emissive state has not changed nya~!
                    bool currentEmissiveState = facadeTransform.GetComponentsInChildren<MeshRenderer>()
                                                               .Any(meshRenderer => meshRenderer.materials.Any(material =>
                                                                   material.HasProperty("_EmissionMap") &&
                                                                   material.IsKeywordEnabled("_EMISSION")));
                    if (currentEmissiveState == shouldEnableEmissive)
                    {
                        continue; // Skip redundant updates nya~!
                    }

                    // Apply emissive state updates nya~!
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
                LightsOut = false;
                Caught = false;
                StopCoroutine(PeriodicStealthCheckCoroutine());
                ApplyTimeBasedEmissiveChanges();

                // Add a deferred coroutine check for Exterior state
                StartCoroutine(CheckExteriorStateAfterLoad());
            };
        }

        private IEnumerator CheckExteriorStateAfterLoad()
        {
            // Wait a few frames to ensure Exterior is fully active
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.9f);
            var exterior = GameObject.Find("Exterior");
            var interior = GameObject.Find("Interior");

            if (exterior?.activeInHierarchy == true && interior == null)
            {
                Debug.Log("[LightsOutScript] Player is in an exterior, nya~!");
                yield break; // Stop further logic
            }
            else
            {
                Debug.Log("[LightsOutScript] Player not in exterior, starting LightsOut!");
                StartCoroutine(TriggerLightsOutCoroutine());
            }
        }

        public bool LightsOut = false;
        private DaggerfallLocation[] allLocations;

        private void HandleNewHourEvent()
        {
            if (LightsOut == false)
            {
                return;
            }

            allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>(); // Populate allLocations
            ApplyTimeBasedEmissiveChanges();
            StartCoroutine(ResetShadersCoroutine(1.0f));
            Debug.Log($"Hour event raised!");
        }

        private IEnumerator ResetShadersCoroutine(float waitTime)
        {

            Debug.Log($"[LightsOutScript] Coroutine started, nya~! Waiting for {waitTime} seconds..."); // Debug log for tracking nya~!
            if (LightsOut == false)
            {
                yield break;
            }

            // Wait for the specified amount of time
            yield return new WaitForSeconds(waitTime);

            Debug.Log("[LightsOutScript] Resetting shaders now, nya~!");

            // Execute the shader reset logic
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            foreach (var location in allLocations)
            {
                var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);
                foreach (var block in blocks)
                {
                    var combinedModelsTransform = block.transform.Find("Models/CombinedModels");
                    if (combinedModelsTransform != null)
                    {
                        foreach (var meshRenderer in combinedModelsTransform.GetComponentsInChildren<MeshRenderer>())
                        {
                            foreach (var material in meshRenderer.materials)
                            {
                                Shader standardShader = Shader.Find("Standard");
                                if (standardShader != null)
                                {
                                    material.shader = standardShader;
                                    Debug.Log($"[LightsOutScript] Forced shader reset to 'Standard' for material '{material.name}', nya~!");
                                }
                                else
                                {
                                    Debug.LogWarning($"[LightsOutScript] Shader 'Standard' not found, unable to reset material '{material.name}', nya~!");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[LightsOutScript] CombinedModelsTransform not found in block '{block.name}', nya~!");
                    }
                }
            }

            Debug.Log("[LightsOutScript] Shader reset completed, nya~!");
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

        private Coroutine stopMusicCoroutine; // Keep track of the coroutine, nya~!

        private AudioSource cricketAudio;

        private List<AudioSource> cricketSources = new List<AudioSource>();

        private void OnExteriorTransitionDetected(PlayerEnterExit.TransitionEventArgs args)
        {
            StopCoroutine(PeriodicStealthCheckCoroutine());
            if (Caught == true)
            {
                Vector3 fallbackPosition = GameManager.Instance.PlayerEntityBehaviour.transform.position;
                GameManager.Instance.PlayerEntity.SpawnCityGuard(fallbackPosition, Vector3.forward);
            }
            Caught = false;
            Debug.Log("[LightsOutScript] Exterior transition detected!");
            var songPlayer = FindObjectOfType<DaggerfallSongPlayer>();
            // Cease the music-stopping coroutine if it's running
            if (stopMusicCoroutine != null)
            {
                StopCoroutine(stopMusicCoroutine);
                stopMusicCoroutine = null; // Reset the reference
                Debug.Log("[LightsOutScript] Music-stopping coroutine ceased!");
            }

            // Stop and remove the cricket sound effect completely
            foreach (var source in cricketSources)
            {
                if (source != null && source.isPlaying)
                {
                    source.Stop();
                    Debug.Log("[LightsOutScript] Stopped cricket sound source.");
                }
                Destroy(source);
                Debug.Log("[LightsOutScript] Destroyed cricket AudioSource.");
            }
            cricketSources.Clear(); //
            LightsOut = false;
            StopCoroutine(StopMusicCoroutine(songPlayer));
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
        DaggerfallConnect.DFLocation.BuildingTypes.Town4,
        DaggerfallConnect.DFLocation.BuildingTypes.Town23,
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
            ApplyTimeBasedEmissiveChanges();
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
                case DFLocation.BuildingTypes.Palace:
                case DFLocation.BuildingTypes.Tavern:
                    return true; // Never deactivate nya~!
                case DFLocation.BuildingTypes.HouseForSale:
                case DFLocation.BuildingTypes.Town23:
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

        private void OnTransitionInterior(DaggerfallWorkshop.Game.PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log($"[LightsOutScript] Transitioned to Interior: {args.DaggerfallInterior.name}, nya~!");
            StartCoroutine(TriggerLightsOutCoroutine());
        }

        private void OnTransitionExterior(DaggerfallWorkshop.Game.PlayerEnterExit.TransitionEventArgs args)
        {
            CheckEmissiveTextureStateFacades();
            CheckEmissiveTextureStateCombinedModels();
            ApplyTimeBasedEmissiveChanges();
        }

        private bool Caught = false;

        private IEnumerator PeriodicStealthCheckCoroutine()
        {
            Debug.Log("[LightsOutScript] Starting periodic Stealth check coroutine, nya~!");

            // Keep checking until caught
            while (!Caught)
            {
                yield return new WaitForSeconds(8.0f); //Time to wait between checks uwu

                // Get the player's Stealth skill value
                int playerStealth = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);

                // Calculate the probability of failing based on Stealth skill
                float randomFactor = UnityEngine.Random.Range(-0.25f, 0.25f); // Add some randomness
                float failureProbability = Mathf.Clamp01(0.5f - (playerStealth - 80) / 140f + randomFactor);
                Debug.Log($"[LightsOutScript] Stealth failure probability rolled: {failureProbability}, nya~!");

                // Roll the dice to see if guards are called
                if (UnityEngine.Random.value < failureProbability)
                {
                    Debug.Log("[LightsOutScript] Player failed Stealth check! Guards called, nya~!");

                    // Show a message box to notify the player
                    DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.Instance.UserInterfaceManager, DaggerfallUI.Instance.UserInterfaceManager.TopWindow);
                    messageBox.SetText("You've been caught!");
                    messageBox.ClickAnywhereToClose = true;
                    messageBox.Show();

                    // Access the interior component from PlayerEnterExit
                    DaggerfallInterior interior = GameManager.Instance.PlayerEnterExit.Interior;

                    if (interior != null && interior.Markers.Length > 0)
                    {
                        Vector3 entrancePosition = Vector3.zero;
                        bool foundMarker = false;

                        // Loop through markers to find the closest "Enter" marker
                        foreach (var marker in interior.Markers)
                        {
                            if (marker.type == DaggerfallInterior.InteriorMarkerTypes.Enter)
                            {
                                entrancePosition = marker.gameObject.transform.position;
                                foundMarker = true;
                                break; // Stop after finding the first "Enter" marker
                            }
                        }

                        if (foundMarker)
                        {
                            Vector3 guardDirection = Vector3.forward; // Default direction, adjust if needed
                            GameManager.Instance.PlayerEntity.CrimeCommitted = PlayerEntity.Crimes.Trespassing;
                            GameManager.Instance.PlayerEntity.CrimeCommitted = PlayerEntity.Crimes.Breaking_And_Entering;
                            GameManager.Instance.PlayerEntity.SpawnCityGuard(entrancePosition, guardDirection);

                            Debug.Log($"[LightsOutScript] Guards spawned at entrance marker position: {entrancePosition}, nya~!");
                        }
                        else
                        {
                            Debug.LogWarning("[LightsOutScript] No 'Enter' markers found! Guards will spawn at player's position instead.");
                            Vector3 fallbackPosition = GameManager.Instance.PlayerEntityBehaviour.transform.position;
                            GameManager.Instance.PlayerEntity.SpawnCityGuard(fallbackPosition, Vector3.forward);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[LightsOutScript] No markers found in interior! Guards will spawn at player's position instead.");
                        Vector3 fallbackPosition = GameManager.Instance.PlayerEntityBehaviour.transform.position;
                        GameManager.Instance.PlayerEntity.SpawnCityGuard(fallbackPosition, Vector3.forward);
                    }

                    Caught = true; // Stop further checks
                }
                else
                {
                    Debug.Log("[LightsOutScript] Stealth check passed, nya~!");
                    GameManager.Instance.PlayerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                    Debug.Log("[LightsOutScript] Tallied 1 Stealth skill use for player, nya~!");
                }
            }

            Debug.Log("[LightsOutScript] Stopping Stealth check coroutine because player was caught, nya~!");
        }

        // This is your coroutine nya~!
        private IEnumerator TriggerLightsOutCoroutine()
        {
            Debug.Log("[LightsOutScript] Coroutine started, nya~! Waiting for 1.5 seconds..."); // Debug log for tracking nya~!
            yield return null;
            yield return new WaitForSeconds(0.7f); // Pause for n seconds nya~!
            yield return null;
            TurnOutTheLights(); // Call the TurnOutTheLights method nya~!
            Debug.Log("[LightsOutScript] TurnOutTheLights method called, nya~!"); // Log the method execution nya~!
        }

        private void TurnOutTheLights()
        {
           
            int currentHour = DaggerfallUnity.Instance.WorldTime.Now.Hour;
            // Get the PlayerEnterExit instance nya~!
            PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
            Debug.Log("[LightsOutScript] Turning out the Lights!");
            // If we can't find the PlayerEnterExit, skip the method nya~!
            if (playerEnterExit == null)
            {
                Debug.LogWarning("[LightsOutScript] PlayerEnterExit instance not found, skipping TurnOutTheLights(), nya~!");
                return;
            }

            DFLocation.BuildingTypes buildingType = playerEnterExit.BuildingType;

            // Check if we are in an exterior scene nya~
            if (GameObject.Find("Exterior")?.activeInHierarchy == true && GameObject.Find("Interior")?.activeInHierarchy == false)
            {
                Debug.Log("[LightsOutScript] TurnOutTheLights() skipped because the player is in an exterior, nya~!");
                return;
            }

            // Skip specific building types nya~
            if (buildingType == DFLocation.BuildingTypes.Tavern || buildingType == DFLocation.BuildingTypes.Temple || buildingType == DFLocation.BuildingTypes.Palace || buildingType == DFLocation.BuildingTypes.Town23 || buildingType == DFLocation.BuildingTypes.Town4 || buildingType == DFLocation.BuildingTypes.Ship)
            {
                Debug.Log($"[LightsOutScript] TurnOutTheLights() skipped because the player is in a {buildingType}, nya~!");
                return;
            }

            // Add time-based conditions for other building types nya~
            switch (buildingType)
            {
                case DFLocation.BuildingTypes.Alchemist:
                    if (currentHour >= 7 && currentHour < 22) return; // Only fire between 22:00 and 7:00
                    break;
                case DFLocation.BuildingTypes.Armorer:
                    if (currentHour >= 9 && currentHour < 19) return; // Only fire between 19:00 and 9:00
                    break;
                case DFLocation.BuildingTypes.Bank:
                    if (currentHour >= 8 && currentHour < 15) return; // Only fire between 15:00 and 8:00
                    break;
                case DFLocation.BuildingTypes.Bookseller:
                    if (currentHour >= 9 && currentHour < 21) return; // Only fire between 21:00 and 9:00
                    break;
                case DFLocation.BuildingTypes.ClothingStore:
                    if (currentHour >= 10 && currentHour < 19) return; // Only fire between 19:00 and 10:00
                    break;
                case DFLocation.BuildingTypes.GemStore:
                    if (currentHour >= 9 && currentHour < 18) return; // Only fire between 18:00 and 9:00
                    break;
                case DFLocation.BuildingTypes.GeneralStore:
                    if (currentHour >= 6 && currentHour < 23) return; // Only fire between 23:00 and 6:00
                    break;
                case DFLocation.BuildingTypes.Library:
                    if (currentHour >= 9 && currentHour < 23) return; // Only fire between 23:00 and 9:00
                    break;
                case DFLocation.BuildingTypes.PawnShop:
                    if (currentHour >= 9 && currentHour < 20) return; // Only fire between 20:00 and 9:00
                    break;
                case DFLocation.BuildingTypes.WeaponSmith:
                    if (currentHour >= 9 && currentHour < 19) return; // Only fire between 19:00 and 9:00
                    break;
                case DFLocation.BuildingTypes.GuildHall:
                    if (currentHour >= 11 && currentHour < 23) return; // Only fire between 23:00 and 11:00
                    break;
                case DFLocation.BuildingTypes.HouseForSale:
                    // Always fire nya~!
                    break;
                case DFLocation.BuildingTypes.House1:
                case DFLocation.BuildingTypes.House2:
                case DFLocation.BuildingTypes.House3:
                case DFLocation.BuildingTypes.House4:
                case DFLocation.BuildingTypes.House5:
                case DFLocation.BuildingTypes.House6:
                    if (currentHour >= 6 && currentHour < 22) return; // Only fire between 22:00 and 6:00
                    break;
                default:
                    Debug.Log($"[LightsOutScript] TurnOutTheLights() skipped because the building type '{buildingType}' is not handled, nya~!");
                    return;
            }
            
            var allObjects = GameObject.FindObjectsOfType<GameObject>();
            var billboards = GameObject.FindObjectsOfType<DaggerfallBillboard>();
            foreach (var obj in allObjects) // Defining `obj` hewe fwom `allObjects`, nya~!
            {
                // Ensure the object is under the "Interior" parent nya~!
                if (obj.transform.root.name != "Interior")
                {
                    continue; // Skip objects not under "Interior", nya~!
                }

                // Check if the GameObject is a DaggerfallBillboard with TEXTURE.210 nya~!
                if (obj.name.IndexOf("TEXTURE.210", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log($"[LightsOutScript] Found Texture.210 on '{obj.name}', nya~!");

                    // Check if the matewiaw contains the wecowd index 13 nya~!
                    var renderer = obj.GetComponent<Renderer>();
                    var billboard = obj.GetComponent<DaggerfallBillboard>();
                    if (renderer != null && billboard != null)
                    {
                        var meshRenderer = obj.GetComponent<MeshRenderer>();
                        var materials = renderer.materials;
                        foreach (var material in materials)
                        {
                            int archive = GetTextureArchiveIndex(material);
                            int record = GetTextureRecordIndex(material);

                            if (archive == 210)
                            {
                                Debug.Log($"[LightsOutScript] Found Texture.210 record={record} on '{obj.name}', nya~!");

                                meshRenderer.enabled = false; // Disabwe the mesh wendewew, nya~!

                                // Replace the texture using DaggerfallBillboard nya~!
                                if (record == 13)
                                {
                                    billboard.SetMaterial(archive, 12); // Use SetMaterial to wepwace the texture, nya~!
                                    meshRenderer.enabled = true; // We-enabwe the mesh wendewew aftew wepwacing, nya~!
                                    Debug.Log($"[LightsOutScript] Replaced texture for GameObject '{obj.name}' with TEXTURE.210 Index=12, nya~!");
                                }
                            }
                        }
                    }
                }
              
                // Check if the GameObject has a Light component nya~!
                var lightComponent = obj.GetComponent<Light>();
                if (lightComponent != null)
                {
                    // Disable the light nya~!
                    lightComponent.enabled = false;
                    Debug.Log($"[LightsOutScript] Disabled light on GameObject '{obj.name}', nya~!");
                }
            }
   
            // Iterate thwough aww DaggwefawwBiwwboawds nya~!
            foreach (var billboard in billboards)
            {
                // Add extwa conditions to ensuwe onwy wight biwwboawds awe tawgeted nya~!
                if (billboard.customArchive == 210 && IsLightBillboard(billboard)) // Check customArchive and additionaw cwidewia nya~!
                {
                    var meshRenderer = billboard.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        meshRenderer.enabled = false; // Disabwe the mesh wendewew fow wight biwwboawds, nya~!
                        Debug.Log($"[LightsOutScript] Disabwed DaggerfallBillboard '{billboard.name}' because it is part of TEXTURE.210 and a light billboard, nya~!");
                    }
                }
            }
  
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            foreach (var location in allLocations)
            {
                var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);
                foreach (var block in blocks)
                {
                    var combinedModelsTransform = block.transform.Find("Models/CombinedModels");
                    if (combinedModelsTransform != null)
                    {
                        foreach (var meshRenderer in combinedModelsTransform.GetComponentsInChildren<MeshRenderer>())
                        {
                            foreach (var material in meshRenderer.materials)
                            {
                                Shader standardShader = Shader.Find("Standard");
                                if (standardShader != null)
                                {
                                    material.shader = standardShader;
                                    Debug.Log($"[LightsOutScript] Forced shader reset to 'Standard' for material '{material.name}', nya~!");
                                }
                                else
                                {
                                    Debug.LogWarning($"[LightsOutScript] Shader 'Standard' not found, unable to reset material '{material.name}', nya~!");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[LightsOutScript] CombinedModelsTransform not found in block '{block.name}', nya~!");
                    }
                }
            }
  
            var songPlayer = FindObjectOfType<DaggerfallSongPlayer>();
            if (songPlayer != null)
            {
                StartCoroutine(StopMusicCoroutine(songPlayer)); // Stawt the coroutine, nya~!
            }

            AudioClip cricketClip = DaggerfallUnity.Instance.SoundReader.GetAudioClip((int)SoundClips.AmbientCrickets);
            if (cricketClip == null)
            {
                Debug.LogWarning("[LightsOutScript] Cricket sound effect not found!");
            }
            else
            {
                AudioSource newCricketAudio = gameObject.AddComponent<AudioSource>();
                newCricketAudio.clip = cricketClip;
                newCricketAudio.loop = true;
                newCricketAudio.volume = 0.8f;
                newCricketAudio.spatialBlend = 0; // Non-spatial sound
                newCricketAudio.Play();
                cricketSources.Add(newCricketAudio); // Track the new AudioSource
                Debug.Log("[LightsOutScript] Playing cricket sound effect on loop.");
            }
            if (cricketAudio != null)
            {
                if (cricketAudio.isPlaying)
                {
                    cricketAudio.Stop();
                    Debug.Log("[LightsOutScript] Cricket sound stopped!");
                }
                Destroy(cricketAudio);
                cricketAudio = null;
                Debug.Log("[LightsOutScript] Cricket AudioSource destroyed!");
            }


            var lingeringSources = FindObjectsOfType<AudioSource>();
            foreach (var source in lingeringSources)
            {
                if (source.clip != null && source.clip.name.Contains("AmbientCrickets"))
                {
                    source.Stop();
                    Destroy(source);
                    Debug.Log("[LightsOutScript] Lingering cricket AudioSource found and destroyed!");
                }
            }


            var customNPCs = GameObject.FindObjectsOfType<CustomStaticNPCMod.CustomStaticNPC>();
            foreach (var customNPC in customNPCs)
            {
                var meshRenderer = customNPC.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false; // Disable the mesh renderer nya~!
                    Debug.Log($"[LightsOutScript] Disabled MeshRenderer for CustomStaticNPC '{customNPC.name}', nya~!");
                }
            }
            int livingNPCCount = CustomNPCBridgeMod.CustomNPCBridge.Instance.GetLivingNPCCountInInterior();
            CustomStaticNPCMod.CustomStaticNPC.NothingHereAidan();
            StartCoroutine(PeriodicStealthCheckCoroutine());
            LightsOut = true;
        }

        private bool stopMusicFlag = false; // Flag to terminate the coroutine

        private IEnumerator StopMusicCoroutine(DaggerfallSongPlayer songPlayer)
        {
            Debug.Log("[LightsOutScript] Starting music-stopping coroutine!");

            while (!stopMusicFlag) // Keep checking until the flag is set to true
            {
                if (songPlayer != null && songPlayer.IsPlaying)
                {
                    songPlayer.AudioSource.volume = 0f; // Ensure the volume is muted
                    Debug.Log("[LightsOutScript] Music volume set to 0!");
                }

                yield return null; // Wait until the next frame
            }

            Debug.Log("[LightsOutScript] Music-stopping coroutine terminated!");
        }

        private bool IsLightBillboard(DaggerfallBillboard billboard)
        {
            // Use IndexOf to check if the name contains "Light" nya~!
            // IndexOf supports case-insensitive compawison nya~!
            return billboard.name.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int GetTextureArchiveIndex(Material material)
        {
            // Extwact archive index fwom the matewiaw name (e.g., "TEXTURE.210 Index=13"), nya~!
            if (material.name.StartsWith("TEXTURE.", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = material.name.Split(new[] { ' ', '.', '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && int.TryParse(parts[1], out int archive))
                {
                    return archive;
                }
            }
            return -1; // Invawid archive index, nya~!
        }

        private int GetTextureRecordIndex(Material material)
        {
            Debug.Log($"[LightsOutScript][DBG] Attempting to retrieve texture record index using GameObject's name, nya~!");

            // Step 1: Find the GameObject owning the material
            GameObject parentObject = FindParentObject(material);
            if (parentObject == null)
            {
                Debug.LogError($"[LightsOutScript][ERR] GameObject owning the material not found, nya~!");
                return -1; // Invalid record index
            }

            // Step 2: Check if GameObject's name contains "Index="
            string objectName = parentObject.name;
            if (!objectName.Contains("Index="))
            {
                Debug.LogError($"[LightsOutScript][WARN] 'Index=' not found in GameObject's name: {objectName}, nya~!");
                return -1; // Invalid record index
            }

            // Step 3: Extract the number between "=" and "]"
            try
            {
                Debug.Log($"[LightsOutScript][DBG] Found 'Index=' in GameObject's name: {objectName}, nya~!");
                int startIndex = objectName.IndexOf("Index=") + "Index=".Length;
                int endIndex = objectName.IndexOf(']', startIndex);
                string indexString = objectName.Substring(startIndex, endIndex - startIndex);
                if (int.TryParse(indexString, out int record))
                {
                    Debug.Log($"[LightsOutScript][DBG] Successfully extracted record index {record} from GameObject's name: {objectName}, nya~!");
                    return record;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LightsOutScript][ERR] Failed to parse 'Index=' from GameObject's name: {e.Message}, nya~!");
            }

            Debug.LogError($"[LightsOutScript][WARN] Unable to retrieve a valid record index from GameObject's name: {objectName}, nya~!");
            return -1; // Graceful failure
        }

        // Helper method to find the parent GameObject of a Material
        private GameObject FindParentObject(Material material)
        {
            foreach (Renderer renderer in GameObject.FindObjectsOfType<Renderer>())
            {
                if (renderer.sharedMaterial == material || renderer.material == material)
                {
                    return renderer.gameObject;
                }
            }
            return null; // No parent object found
        }

        // Helper method to replace texture, nya~!
        private void ReplaceTexture(Material material, int archive, int record)
        {
            string newMaterialName = $"TEXTURE.{archive} Index={record}";
            material.name = newMaterialName;
            Debug.Log($"[LightsOutScript] Texture replaced to '{newMaterialName}', nya~!");
        }
    }
}