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
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using UnityEngine.SceneManagement; // Add this line
using DaggerfallWorkshop.Utility.AssetInjection;
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

        private int lastMinute = -1;
        private int lastHourChecked = -1;
        private PlayerGPS playerGPS;
        private Coroutine clockWatcherCoroutine;
        private HashSet<string> robbedBuildings = new HashSet<string>();
        private HashSet<DaggerfallLocation> processedLocations = new HashSet<DaggerfallLocation>();
        private HashSet<string> processedBuildings = new HashSet<string>(); // Tracks buildings processed by facade spawning
        private HashSet<DaggerfallLocation> locationsBeingProcessed = new HashSet<DaggerfallLocation>();

        private bool breakInScriptEnabled;
        private bool lightsOutScriptEnabled;
        private bool talkWindowScriptEnabled;

        void Awake()
        {
            ModSettings settings = mod.GetSettings();

            breakInScriptEnabled = settings.GetBool("MyModSettings", "BreakInScript");
            lightsOutScriptEnabled = settings.GetBool("MyModSettings", "LightsOutScript");
            talkWindowScriptEnabled = settings.GetBool("MyModSettings", "TalkWindowScript");

            DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallRestWindow.OnSleepEnd += OnSleepEnd;
            DaggerfallWorkshop.Game.PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            emissiveCombinedModelsActive = CheckEmissiveTextureStateCombinedModels();
            PlayerEnterExit.OnTransitionExterior += OnExteriorTransitionDetected;
            WorldTime.OnNewHour += HandleNewHourEvent;
            Debug.Log($"[LightsOutScript] Initial emissiveCombinedModelsActive state: {(emissiveCombinedModelsActive ? "ACTIVE" : "INACTIVE")}, nya~!");
            Debug.Log($"[LightsOutScript] Initial emissiveFacadesActive state: {(emissiveFacadesActive ? "ACTIVE" : "INACTIVE")}, nya~!");
            playerGPS = GameManager.Instance.PlayerGPS; // Safe in Awake, but if null, move to Start()!
            if (playerGPS != null)
                PlayerGPS.OnEnterLocationRect += OnEnterLocationRect;
            else
                Debug.LogWarning("[LightsOutScript] PlayerGPS not found in Awake! Will try again in Start, nya~!");

            mod.IsReady = true;
        }

        void Start()
        {
            StartCoroutine(ClockWatcher());
            StartCoroutine(FacadeMinuteWatcher());
            SaveLoadManager.OnLoad += OnSaveLoaded;
        }

        private void OnSaveLoaded(SaveData_v1 saveData)
        {
            var exterior = GameObject.Find("Exterior");
            var interior = GameObject.Find("Interior");
            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            foreach (var location in allLocations)
            {
                SpawnFacadeAtFactionBuildings(location);
            }
            Debug.Log("[LightsOutScript] Save loaded, rechecking emissive states, nya~!");
            poop = false;
            LightsOut = false;
            Caught = false;
            StartCoroutine(FacadeMinuteWatcher());
            StopCoroutine(PeriodicStealthCheckCoroutine());
            ApplyTimeBasedEmissiveChanges();
            StartCoroutine(CheckExteriorStateAfterLoad());
            if (exterior?.activeInHierarchy == true && interior != null)
            {
                StartCoroutine(PooChungus());
            }

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
        }

        void OnDestroy()
        {
            DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallRestWindow.OnSleepEnd -= OnSleepEnd;
            DaggerfallWorkshop.Game.PlayerEnterExit.OnTransitionInterior -= OnTransitionInterior;
            PlayerEnterExit.OnTransitionExterior -= OnExteriorTransitionDetected;
            WorldTime.OnNewHour -= HandleNewHourEvent;
            SaveLoadManager.OnLoad -= OnSaveLoaded;

            if (playerGPS != null)
                PlayerGPS.OnEnterLocationRect -= OnEnterLocationRect;

            StopCoroutine(FacadeMinuteWatcher());

            // Stop and clean up cricket AudioSources
            foreach (var source in cricketSources)
            {
                if (source != null)
                {
                    source.Stop();
                    Destroy(source);
                }
            }
            cricketSources.Clear();

            StopCoroutine(PeriodicStealthCheckCoroutine());
            if (stopMusicCoroutine != null)
                StopCoroutine(stopMusicCoroutine);

            if (clockWatcherCoroutine != null)
            {
                StopCoroutine(clockWatcherCoroutine);
                clockWatcherCoroutine = null;
            }

            Debug.Log("[LightsOutScript] OnDestroy called, cleaned up event handlers and resources, nya~!");
        }

        private void OnSleepEnd()
        {
            Debug.Log("[LightsOutScript] Player finished resting/loitering, nya~!");
            StartCoroutine(FacadeMinuteWatcher());
        }

        private void OnEnterLocationRect(DFLocation location)                                                   
        {
            Debug.Log("[LightsOutScript] Player entered a new location rect, nya~!");
            StartCoroutine(FacadeMinuteWatcher());
        }

        private bool clockWatcherRunning = false;

        private IEnumerator ClockWatcher()
        {
            if (!lightsOutScriptEnabled)
            {
                yield break;
            }

            if (clockWatcherRunning)
            {
                Debug.Log("[LightsOutScript] ClockWatcher called but already running, nya~!");
                yield break; // Exit early if already running
            }

            clockWatcherRunning = true;
            Debug.Log("[LightsOutScript] ClockWatcher entered!");

            while (true)
            {
                int currentHour = DaggerfallUnity.Instance.WorldTime.Now.Hour;
                if (currentHour != lastHourChecked)
                {
                    lastHourChecked = currentHour;

                  
                    GameObject interior = GameObject.Find("Interior");

                    Debug.Log("[LightsOutScript] Hour boundary crossed! Calling FacadeMinuteWatcher, nya~!");
                    StartCoroutine(FacadeMinuteWatcher());
                }
                yield return new WaitForSeconds(1f);
            }
            // clockWatcherRunning = false;
        }

        public bool poop = false;

        private IEnumerator PooChungus()
        {
            if (!lightsOutScriptEnabled)
            {
                yield break;
            }

            BuildingDirectory buildingDirectory = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory();

            Debug.Log($"[LightsOutScript] PooChungus called!");
            var playerEnterExit = GameManager.Instance.PlayerEnterExit;
            var buildingInfo = playerEnterExit.BuildingDiscoveryData;

            if (buildingDirectory != null && buildingInfo.buildingKey != 0)
            {
                yield return null;
                yield return null;
                yield return new WaitForSeconds(1.5f);
                int buildingKey = buildingInfo.buildingKey; // This is the key used in BuildingDirectory
                int layoutX, layoutY, recordIndex;
                BuildingDirectory.ReverseBuildingKey(buildingKey, out layoutX, out layoutY, out recordIndex);
                Debug.Log($"Building contains key in PooChungus");
                // Get the current location name from PlayerGPS
                string locationName = GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                string buildingId = $"{locationName}_{layoutX}_{layoutY}_{recordIndex}";

                if (processedBuildings.Contains(buildingId))
                {
                    playerIsInSpecialBuilding = true;
                    currentSpecialBuildingId = buildingId;
                    Debug.Log($"[LightsOutScript] Player loaded into a special building: {buildingId}, nya~!");
                }
                else
                {
                    Debug.Log($"[LightsOutScript] Player did not load into a special building!");
                    playerIsInSpecialBuilding = false;
                    currentSpecialBuildingId = null;
                }
            }
            else
            {
                playerIsInSpecialBuilding = false;
                currentSpecialBuildingId = null;
            }

            int currentHour = DaggerfallUnity.Instance.WorldTime.Now.Hour;

            if (currentHour >= 18 || currentHour < 6) // Between 22:00 and 6:00 -> Deactivate emissives
            {

                emissiveCombinedModelsActive = false;
                ControlEmissiveWindowTexturesInCombinedModels(emissiveCombinedModelsActive);
                Debug.Log("[LightsOutScript] Turning lights out in PooChungus");
                yield return null;
                yield return null;
                yield return new WaitForSeconds(0.9f);
                // Disable all emission on CombinedModels to force black emission
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
                                    if (material.HasProperty("_EmissionMap") || material.HasProperty("_EmissionColor"))
                                    {
                                        material.DisableKeyword("_EMISSION");
                                        material.SetColor("_EmissionColor", Color.black);
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

            }
            else if (currentHour >= 6 && currentHour < 8) // Between 6:00 and 8:00 -> Reactivate emissives
            {


                emissiveCombinedModelsActive = true;
                ControlEmissiveWindowTexturesInCombinedModels(emissiveCombinedModelsActive);
                Debug.Log("[LightsOutScript] Emissive textures automatically reactivated due to time, nya~!");

            }
        }

        private DaggerfallLocation[] allLocations;

        private IEnumerator CheckExteriorStateAfterLoad()
        {
            // Wait a few frames to ensure Exterior is fully active
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.9f);
            var exterior = GameObject.Find("Exterior");
            var interior = GameObject.Find("Interior");
            var songPlayer = FindObjectOfType<DaggerfallSongPlayer>();
            if (exterior?.activeInHierarchy == true && interior == null)
            {
                stopMusicFlag = true;
                Caught = false;
                LightsOut = false;
                StopCoroutine(PeriodicStealthCheckCoroutine());
                StopCoroutine(StopMusicCoroutine(songPlayer));
                Debug.Log("[LightsOutScript] Player is in an exterior, nya~!");
                yield break; // Stop further logic
            }
            else
            {
                Debug.Log("[LightsOutScript] NOT Calling LightsOut CoRoutine from CheckState");
            }
        }

        private IEnumerator FacadeMinuteWatcher()
        {
            if (!lightsOutScriptEnabled)
            {
                yield break;
            }

            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return new WaitForSeconds(1.5f);
          
                // Wait until the minute changes
                int currentMinute = DaggerfallUnity.Instance.WorldTime.Now.Minute;
                if (currentMinute != lastMinute)
                {
                    lastMinute = currentMinute;

                  

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
   
                    }

                    var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
                    var trulyNewLocations = allLocations
                        .Where(location => !processedLocations.Contains(location) && !locationsBeingProcessed.Contains(location))
                        .ToList();

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
                           
                        }
                    }

                    // Check time-based emissive activation/deactivation
                    int currentHour = DaggerfallUnity.Instance.WorldTime.Now.Hour;

                    if (currentHour >= 8 && currentHour < 17)
                    {
                        Debug.Log($"[LightsOutScript] Scheduling 326 day coroutine");
                        StartCoroutine(Restore326_3_0EmissionMapsForDayDelayed());
                    }
                    if (currentHour < 18 && currentHour >= 17 || (currentHour >= 6 && currentHour < 8))
                    {
                        Debug.Log($"[LightsOutScript] Scheduling 326 evening coroutine");
                        StartCoroutine(Restore326_3_0EmissionMapsForEveningDelayed());
                    }
                    if ((currentHour >= 18 && currentHour <= 23) || (currentHour >= 0 && currentHour < 6))
                    {
                        Debug.Log($"[LightsOutScript] Scheduling 326 night coroutine");
                        StartCoroutine(Restore326_3_0EmissionMapsForNightDelayed());
                    }

                    foreach (var location in allLocations)
                    {
                            SpawnFacadeAtFactionBuildings(location);
                    }



                    // === Update CombinedModels window emission color based on time of day, nya! ===
                    // === Update CombinedModels window emission color based on time of day, nya! ===
                    var matReader = DaggerfallUnity.Instance.MaterialReader;
                    if (matReader != null)
                    {
                        Color dayColor = matReader.DayWindowColor * matReader.DayWindowIntensity;
                        Color eveningColor = matReader.NightWindowColor * matReader.NightWindowIntensity;
                        Color nightColor = Color.black;
                        int hour = DaggerfallUnity.Instance.WorldTime.Now.Hour;

                        Debug.Log($"[LightsOutScript] [Emission Debug] Current hour: {hour}");

                        foreach (var location in allLocations)
                        {
                            var blocks = location.GetComponentsInChildren<DaggerfallRMBBlock>(true);
                            foreach (var block in blocks)
                            {
                                var combinedModelsTransform = block.transform.Find("Models/CombinedModels");
                                if (combinedModelsTransform != null)
                                {
                                    var renderers = combinedModelsTransform.GetComponentsInChildren<MeshRenderer>();
                                    if (renderers.Length == 0)
                                    {
                                        Debug.Log($"[LightsOutScript] [Emission Debug] No MeshRenderers found in CombinedModels of block {block.name}");
                                    }

                                    foreach (var meshRenderer in renderers)
                                    {
                                        // Get the Interior GameObject once at the top
                                        GameObject interior = GameObject.Find("Interior");

                                        foreach (var material in meshRenderer.materials)
                                        {
                                            Debug.Log($"[LightsOutScript] [Emission Debug] Found material: {material.name} (shader: {material.shader.name}) on block {block.name}");

                                            if (material.HasProperty("_EmissionMap") && material.HasProperty("_EmissionColor"))
                                            {
                                                // Only proceed if there's actually an emission map assigned
                                                if (material.GetTexture("_EmissionMap") != null)
                                                {
                                                        if ((hour >= 6 && hour < 8) || (hour >= 17 && hour < 18))
                                                        {
                                                            material.SetColor("_EmissionColor", eveningColor);
                                                            material.EnableKeyword("_EMISSION");
                                                            Debug.Log($"[LightsOutScript] [Emission Debug] Set {material.name} emission to EVENING (yellow-orange) color: {eveningColor} on block {block.name} at hour {hour}");
                                                        }
                                                        else if (hour >= 8 && hour < 17)
                                                        {
                                                            material.SetColor("_EmissionColor", dayColor);
                                                            material.EnableKeyword("_EMISSION");
                                                            Debug.Log($"[LightsOutScript] [Emission Debug] Set {material.name} emission to DAY (blue-grey) color: {dayColor} on block {block.name} at hour {hour}");
                                                        }
                                                        else // 18pm–6am
                                                        {
                                                            material.SetColor("_EmissionColor", nightColor);
                                                            material.DisableKeyword("_EMISSION");
                                                            Debug.Log($"[LightsOutScript] [Emission Debug] Set {material.name} emission to NIGHT (black) color: {nightColor} on block {block.name} at hour {hour}");
                                                        }
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Debug.Log($"[LightsOutScript] [Emission Debug] CombinedModelsTransform not found in block '{block.name}'");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[LightsOutScript] [Emission Debug] MaterialReader instance is null!");
                    }
                if (currentHour >= 18 || currentHour < 6)
                {

                    emissiveCombinedModelsActive = false;
                    ControlEmissiveWindowTexturesInCombinedModels(emissiveCombinedModelsActive);
                    Debug.Log("[LightsOutScript] Emissive textures automatically deactivated due to time, nya~!");

                }
                else if (currentHour >= 6 && currentHour < 18)
                {

                    emissiveCombinedModelsActive = true;
                    ControlEmissiveWindowTexturesInCombinedModels(emissiveCombinedModelsActive);
                    Debug.Log("[LightsOutScript] Emissive textures automatically reactivated due to time, nya~!");

                }
                Debug.Log("[LightsOutScript] Main CoRoutine is running nyan!");
                }
                //yield return null; // Wait for next frame to check again
            
        }

        public bool LightsOut = false;

        private void HandleNewHourEvent()
        {
            if (!lightsOutScriptEnabled)
            {
                return;
            }

            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            int currentHour = DaggerfallUnity.Instance.WorldTime.Now.Hour;
            if (currentHour >= 8 && currentHour < 17)
            {
                Debug.Log($"[LightsOutScript] Scheduling 326 day coroutine");
                StartCoroutine(Restore326_3_0EmissionMapsForDayDelayed());
            }
            if (currentHour < 18 && currentHour >= 17 || (currentHour >= 6 && currentHour < 8))
            {
                Debug.Log($"[LightsOutScript] Scheduling 326 evening coroutine");
                StartCoroutine(Restore326_3_0EmissionMapsForEveningDelayed());
            }
            if ((currentHour >= 18 && currentHour <= 23) || (currentHour >= 0 && currentHour < 6))
            {
                Debug.Log($"[LightsOutScript] Scheduling 326 night coroutine");
                StartCoroutine(Restore326_3_0EmissionMapsForNightDelayed());
            }

            Debug.Log($"Hour event raised!");
            var exterior = GameObject.Find("Exterior");
            var interior = GameObject.Find("Interior");
            var songPlayer = FindObjectOfType<DaggerfallSongPlayer>();
            allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>(); // Populate allLocations
            ApplyTimeBasedEmissiveChanges();
            if (exterior?.activeInHierarchy == true && interior == null)
            {
                stopMusicFlag = true;
                Caught = false;
                LightsOut = false;
                StopCoroutine(PeriodicStealthCheckCoroutine());
                StopCoroutine(StopMusicCoroutine(songPlayer));
                Debug.Log($"Player detected in exerior, returning");
                return;
            }
            StartCoroutine(ResetShadersCoroutine(1.0f));
        }

        private IEnumerator Restore326_3_0EmissionMapsForNightDelayed()
        {
            // Wait several frames to make sure DFU is done with its stuff
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            Debug.Log($"Calling 326 Nighttime black");
            Restore326_3_0EmissionMapsForNight();
        }

        private void Restore326_3_0EmissionMapsForNight()
        {
            var matReader = DaggerfallUnity.Instance.MaterialReader;
            var texReader = matReader.TextureReader;
            var blackColor = Color.black;

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
                                if (material.name.Contains("326_3-0"))
                                {
                                    material.shader = Shader.Find("Daggerfall/Default");
                                    try
                                    {
                                        if (texReader != null)
                                        {
                                            Debug.Log($"[LightsOutScript] Generating black emission map for 326_3-0");
                                            string arena2 = DaggerfallUnity.Instance.Arena2Path;
                                            var textureFile = new DaggerfallConnect.Arena2.TextureFile();
                                            textureFile.Load(Path.Combine(arena2, DaggerfallConnect.Arena2.TextureFile.IndexToFileName(326)), FileUsage.UseMemory, true);
                                            var dfBitmap = textureFile.GetDFBitmap(3, 0);
                                            var emissionColors = textureFile.GetWindowColors32(dfBitmap);
                                            Texture2D emissionMap = new Texture2D(dfBitmap.Width, dfBitmap.Height, TextureFormat.ARGB32, false);
                                            emissionMap.SetPixels32(emissionColors);
                                            emissionMap.Apply();
                                            material.SetTexture("_EmissionMap", emissionMap);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"[LightsOutScript] Error generating emission map for 326_3-0: {ex}");
                                    }
                                    material.EnableKeyword("_EMISSION");
                                    material.SetColor("_EmissionColor", blackColor);
                                }
                            }
                        }
                    }
                }
            }
        }

        private IEnumerator Restore326_3_0EmissionMapsForEveningDelayed()
        {
            // Wait 10 frames to ensure DFU is done clobbering materials
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            Debug.Log($"Done waiting!");
            Restore326_3_0EmissionMapsForEvening();
        }

        private void Restore326_3_0EmissionMapsForEvening()
        {
            var matReader = DaggerfallUnity.Instance.MaterialReader;
            var texReader = matReader.TextureReader;
            var eveningColor = matReader.NightWindowColor * matReader.NightWindowIntensity;

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
                                if (material.name.Contains("326_3-0"))
                                {
                                    material.shader = Shader.Find("Daggerfall/Default");
                                    try
                                    {
                                        if (texReader != null)
                                        {
                                            Debug.Log($"[LightsOutScript] Generating yellow emission map for 326_3-0");
                                            string arena2 = DaggerfallUnity.Instance.Arena2Path;
                                            var textureFile = new DaggerfallConnect.Arena2.TextureFile();
                                            textureFile.Load(Path.Combine(arena2, DaggerfallConnect.Arena2.TextureFile.IndexToFileName(326)), FileUsage.UseMemory, true);
                                            var dfBitmap = textureFile.GetDFBitmap(3, 0);
                                            var emissionColors = textureFile.GetWindowColors32(dfBitmap);
                                            Texture2D emissionMap = new Texture2D(dfBitmap.Width, dfBitmap.Height, TextureFormat.ARGB32, false);
                                            emissionMap.SetPixels32(emissionColors);
                                            emissionMap.Apply();
                                            material.SetTexture("_EmissionMap", emissionMap);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"[LightsOutScript] Error generating emission map for 326_3-0: {ex}");
                                    }
                                    material.EnableKeyword("_EMISSION");
                                    material.SetColor("_EmissionColor", eveningColor);
                                }
                            }
                        }
                    }
                }
            }
        }

        private IEnumerator Restore326_3_0EmissionMapsForDayDelayed()
        {
       
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            Debug.Log($"Done waiting!");
            Restore326_3_0EmissionMapsForDay();
        }

        private void Restore326_3_0EmissionMapsForDay()
        {
            var matReader = DaggerfallUnity.Instance.MaterialReader;
            var texReader = matReader.TextureReader;
            var dayColor = matReader.DayWindowColor * matReader.DayWindowIntensity;

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
                                if (material.name.Contains("326_3-0"))
                                {
                                    material.shader = Shader.Find("Daggerfall/Default");
                                    try
                                    {
                                        if (texReader != null)
                                        {
                                            Debug.Log($"[LightsOutScript] Generating blue emission map for 326_3-0");
                                            string arena2 = DaggerfallUnity.Instance.Arena2Path;
                                            var textureFile = new DaggerfallConnect.Arena2.TextureFile();
                                            textureFile.Load(Path.Combine(arena2, DaggerfallConnect.Arena2.TextureFile.IndexToFileName(326)), FileUsage.UseMemory, true);
                                            var dfBitmap = textureFile.GetDFBitmap(3, 0);
                                            var emissionColors = textureFile.GetWindowColors32(dfBitmap);
                                            Texture2D emissionMap = new Texture2D(dfBitmap.Width, dfBitmap.Height, TextureFormat.ARGB32, false);
                                            emissionMap.SetPixels32(emissionColors);
                                            emissionMap.Apply();
                                            material.SetTexture("_EmissionMap", emissionMap);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"[LightsOutScript] Error generating emission map for 326_3-0: {ex}");
                                    }
                                    material.EnableKeyword("_EMISSION");
                                    material.SetColor("_EmissionColor", dayColor);
                                }
                            }
                        }
                    }
                }
            }
        }

        private IEnumerator ResetShadersCoroutine(float waitTime)
        {
            if (!lightsOutScriptEnabled)
            {
                yield break;
            }


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
                                if (material.HasProperty("_EmissionMap") || material.HasProperty("_EmissionColor"))
                                {
                                    material.DisableKeyword("_EMISSION");
                                    material.SetColor("_EmissionColor", Color.black);
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
            if (!lightsOutScriptEnabled)
            {
                return;
            }

            int currentHour = DaggerfallUnity.Instance.WorldTime.Now.Hour;

            if (currentHour >= 18 || currentHour < 6)
            {
                emissiveCombinedModelsActive = false;
                ControlEmissiveWindowTexturesInCombinedModels(emissiveCombinedModelsActive);

                emissiveFacadesActive = false;
                ControlEmissiveWindowTexturesInFacades(emissiveFacadesActive);

                Debug.Log("[LightsOutScript] Applied time-based emissive deactivation, nya~!");
            }
            else if (currentHour >= 6 && currentHour < 18)
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
                GameManager.Instance.PlayerEntity.SpawnCityGuards(true);
            }
            Caught = false;
            stopMusicFlag = true;
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
            if (clockWatcherCoroutine == null)
            {
                clockWatcherCoroutine = StartCoroutine(ClockWatcher());
                Debug.Log("[LightsOutScript] ClockWatcher started in exterior, nya~!");
            }
            StopCoroutine(StopMusicCoroutine(songPlayer));
            ApplyTimeBasedEmissiveChanges();
            StartCoroutine(FacadeMinuteWatcher());
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
            if (!lightsOutScriptEnabled)
            {
                return;
            }

            Debug.Log($"[LightsOutScript] SpawnFacades Called!");
            // Check all children of this location for any existing facade objects
            bool locationHasFacade = location.GetComponentsInChildren<Transform>(true)
                .Any(t => t.name.IndexOf("Facade", StringComparison.OrdinalIgnoreCase) >= 0);
            if (locationHasFacade)
            {
                Debug.Log($"[LightsOutScript] Facade(s) already exist in location '{location.name}', skipping SpawnFacadeAtFactionBuildings for this location, nya~!");
                return;
            }

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
            if (!lightsOutScriptEnabled)
            {
                return;
            }

            var allLocations = GameObject.FindObjectsOfType<DaggerfallLocation>();
            var matReader = DaggerfallUnity.Instance.MaterialReader;
            var texReader = matReader.TextureReader;
            int hour = DaggerfallUnity.Instance.WorldTime.Now.Hour;
            Debug.Log($"[LightsOutScript] Control Windows called!");
         
            var dayColor = matReader != null ? matReader.DayWindowColor * matReader.DayWindowIntensity : Color.blue;
            var eveningColor = new Color(0.3f, 0.3f, 0.5f); // example: dimmer/warmer
            var nightColor = Color.black;

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
                                // Special logic for 326_3-0
                                // In ControlEmissiveWindowTexturesInCombinedModels(bool enableEmissive)
                                if (material.name.Contains("326_3-0"))
                                {
                                    material.shader = Shader.Find("Daggerfall/Default");
                                    // (existing: assign emission map etc.)

                                    // ==== Time-of-day color logic for 326_3-0 ====
                                    // Only set color if *not* in 6–8am or 17–22pm (leave to coroutine)
                                    if (!((hour >= 6 && hour < 8) || (hour >= 17 && hour < 18)))
                                    {
                                        if (hour >= 8 && hour < 17)
                                            material.SetColor("_EmissionColor", dayColor);
                                        else if (hour >= 18 || hour < 6)
                                            material.SetColor("_EmissionColor", nightColor);
                                    }
                                    // else: skip color set, coroutine will handle it!

                                    // Enable/disable emission as before
                                    if (enableEmissive && (hour >= 6 && hour < 18))
                                        material.EnableKeyword("_EMISSION");
                                    else
                                        material.DisableKeyword("_EMISSION");

                                    continue;
                                }

                                // Other classic windows (unchanged)
                                if (IsProbablyWindowMaterial(material))
                                {
                                    try
                                    {
                                        Texture mainTex = material.GetTexture("_MainTex");
                                        Texture2D emissionMap = null;
                                        if (mainTex is Texture2D mainTex2D)
                                        {
                                            emissionMap = new Texture2D(mainTex2D.width, mainTex2D.height, TextureFormat.ARGB32, false);
                                            emissionMap.SetPixels32(mainTex2D.GetPixels32());
                                            emissionMap.Apply();
                                        }
                                        if (emissionMap != null)
                                        {
                                            material.SetTexture("_EmissionMap", emissionMap);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"[LightsOutScript] Error generating fallback emission for window: {material.name}: {ex}");
                                    }
                               
                                    material.SetColor("_EmissionColor", enableEmissive ? dayColor : nightColor);
                                    if (enableEmissive)
                                        material.EnableKeyword("_EMISSION");
                                    else
                                        material.DisableKeyword("_EMISSION");
                                    continue;
                                }

                                // Non-window, classic logic
                                if (material.HasProperty("_EmissionMap"))
                                {
                                    if (enableEmissive)
                                        material.EnableKeyword("_EMISSION");
                                    else
                                        material.DisableKeyword("_EMISSION");
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

        // Simple heuristic for window materials (expand as needed)
        private bool IsProbablyWindowMaterial(Material material)
        {
            string name = material.name.ToLowerInvariant();
            return name.Contains("window") || name.Contains("glass");
        }

        public void ControlEmissiveWindowTexturesInFacades(bool enableEmissive)
        {
            if (!lightsOutScriptEnabled)
            {
                return;
            }

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
                    return currentHour < 19 && currentHour >= 8;
                case DFLocation.BuildingTypes.Bank:
                    return currentHour < 17 && currentHour >= 8;
                case DFLocation.BuildingTypes.Bookseller:
                    return currentHour < 21 && currentHour >= 8;
                case DFLocation.BuildingTypes.ClothingStore:
                    return currentHour < 19 && currentHour >= 8;
                case DFLocation.BuildingTypes.GemStore:
                    return currentHour < 18 && currentHour >= 8;
                case DFLocation.BuildingTypes.GeneralStore:
                    return currentHour < 23 && currentHour >= 6;
                case DFLocation.BuildingTypes.Library:
                    return currentHour < 23 && currentHour >= 8;
                case DFLocation.BuildingTypes.PawnShop:
                    return currentHour < 20 && currentHour >= 8;
                case DFLocation.BuildingTypes.FurnitureStore:
                    return currentHour < 20 && currentHour >= 6;
                case DFLocation.BuildingTypes.WeaponSmith:
                    return currentHour < 20 && currentHour >= 8;
                case DFLocation.BuildingTypes.GuildHall:
                    return currentHour < 23 && currentHour >= 8;
                case DFLocation.BuildingTypes.Temple:
                case DFLocation.BuildingTypes.Palace:
                case DFLocation.BuildingTypes.Tavern:
                case DFLocation.BuildingTypes.Ship:
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

        private IEnumerator ProcessNewLocation(DaggerfallLocation location)
        {
            // Wait a couple of frames to ensure all info is loaded
            yield return null;
            yield return null;

            SpawnFacadeAtFactionBuildings(location);
            processedLocations.Add(location);
            locationsBeingProcessed.Remove(location);
        }

        // Tracks if player is in a facade-spawned/special building
        private bool playerIsInSpecialBuilding = false;

        // Stores the current building's unique id (if inside one)
        private string currentSpecialBuildingId = null;

        private void OnTransitionInterior(DaggerfallWorkshop.Game.PlayerEnterExit.TransitionEventArgs args)
        {
            if (DaggerfallUI.Instance.FadeBehaviour.FadeTargetPanel == null)
                DaggerfallUI.Instance.FadeBehaviour.FadeTargetPanel = DaggerfallUI.Instance.DaggerfallHUD.NativePanel;
            DaggerfallUI.Instance.FadeBehaviour.SmashHUDToBlack();
            var exterior = GameObject.Find("Exterior");
            var interior = GameObject.Find("Interior");
            Debug.Log($"[LightsOutScript] Transitioned to Interior: {args.DaggerfallInterior.name}, nya~!");
            if (exterior?.activeInHierarchy == true && interior != null)
            {
                StartCoroutine(PooChungus());
            }
            Debug.Log($"[LightsOutScript] Starting CoRoutine from Transition");
            StartCoroutine(TriggerLightsOutCoroutine());
            poop = true;
            //StartCoroutine(TeleportPlayerToEnterDoorOrMarkerAfterDelay(args.DaggerfallInterior, args.StaticDoor, 0.3f));
            if (clockWatcherCoroutine == null)
            {
                clockWatcherCoroutine = StartCoroutine(ClockWatcher());
                Debug.Log("[LightsOutScript] ClockWatcher started in interior, nya~!");
            }
        }

        private IEnumerator TeleportPlayerToEnterDoorOrMarkerAfterDelay(
            DaggerfallWorkshop.DaggerfallInterior interior,
            DaggerfallWorkshop.StaticDoor entryDoor,
            float delay = 0.3f)
        {
            // Wait extra frames for timing/stability
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return new WaitForSeconds(delay);

            Vector3 entryWorldPos = DaggerfallStaticDoors.GetDoorPosition(entryDoor);
            Debug.Log($"[DBG] Entry door world pos: {entryWorldPos}");

            // 2. Fallback: use the closest enter marker (nudged up)
            DaggerfallWorkshop.DaggerfallInterior.InteriorEditorMarker? closestMarker = null;
            float minMarkerDist = float.MaxValue;
            foreach (var marker in interior.Markers)
            {
                if (marker.type == DaggerfallWorkshop.DaggerfallInterior.InteriorMarkerTypes.Enter)
                {
                    Vector3 markerPos = marker.gameObject.transform.position;
                    float dist = Vector3.Distance(markerPos, entryWorldPos);
                    Debug.Log($"[DBG] Enter marker at {markerPos}, distance to entry door: {dist}");
                    if (dist < minMarkerDist)
                    {
                        minMarkerDist = dist;
                        closestMarker = marker;
                    }
                }
            }
            if (closestMarker != null && closestMarker.Value.gameObject != null)
            {
                Vector3 markerPos = closestMarker.Value.gameObject.transform.position;
                Vector3 toDoor = (entryWorldPos - markerPos).normalized;
                Vector3 fakeNormal = -toDoor; // "inside" direction

                float upNudge = 0.3f; // vertical
                float height = GameManager.Instance.PlayerController ? GameManager.Instance.PlayerController.height : 1.8f;

                Vector3 spawnPos = markerPos
                    + Vector3.up * (height * upNudge);

                GameManager.Instance.PlayerObject.transform.position = spawnPos;
                Debug.Log($"[DBG] Fallback: teleported to closest enter marker at {spawnPos} (distance: {minMarkerDist})");
            }
            else
            {
                Debug.LogWarning("[DBG] No enter markers found, player position unchanged!");
            }
        }

        private void OnTransitionExterior(DaggerfallWorkshop.Game.PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log($"[LightsOutScript] Transitioned to Exterior!");
            stopMusicFlag = true;
            LightsOut = false;
            poop = false;
            var songPlayer = FindObjectOfType<DaggerfallSongPlayer>();
            StopCoroutine(StopMusicCoroutine(songPlayer));
            StopCoroutine(PeriodicStealthCheckCoroutine());
            CheckEmissiveTextureStateFacades();
            CheckEmissiveTextureStateCombinedModels();
            ApplyTimeBasedEmissiveChanges();
        }

        private bool Caught = false;

        private static readonly (int archive, int record)[] GuardDogBillboardRecords = new (int, int)[]
{
    (201, 9),
    (201, 10),
    (10010, 73),
    (10010, 74),
    (10010, 75),
    (10010, 76),
    (10010, 77),
    (10010, 78),
};

        private bool IsGuardDogPresentInScene()
        {
            var billboards = GameObject.FindObjectsOfType<DaggerfallBillboard>();
            foreach (var billboard in billboards)
            {
                // Only consider billboards under the "Interior" root
                if (billboard.transform.root == null || billboard.transform.root.name != "Interior")
                    continue;

                var summary = billboard.Summary;
                foreach (var record in GuardDogBillboardRecords)
                {
                    if (summary.Archive == record.archive && summary.Record == record.record)
                        return true;
                }
            }
            return false;
        }

        private IEnumerator PeriodicStealthCheckCoroutine()
        {
            Debug.Log("[LightsOutScript] Starting periodic Stealth check coroutine, nya~!");
            float runningTimeAccum = 0f;

            // Keep checking until caught or LightsOut disabled
            while (!Caught && LightsOut)
            {
                runningTimeAccum = 0f;
                float timeWaited = 0f;
                while (timeWaited < 7.0f)
                {
                    if (Caught || !LightsOut)
                    {
                        CustomStaticNPCMod.CustomStaticNPC.aidanFired = false;
                        Debug.Log("[LightsOutScript] Coroutine exiting early due to Caught or LightsOut=false, nya~!");
                        yield break; // Exit instantly if conditions change
                    }

                    var playerMotor = GameManager.Instance.PlayerMotor;
                    if (playerMotor != null && playerMotor.IsRunning)
                    {
                        runningTimeAccum += 0.1f; // Add .1s for each .1s interval spent running
                    }

                    yield return new WaitForSeconds(0.1f);
                    timeWaited += 0.1f;
                }

                // Get the player's Stealth skill value
                int playerStealth = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                int playerLuck = GameManager.Instance.PlayerEntity.Stats.GetLiveStatValue(DFCareer.Stats.Luck);
                float luckBias = playerLuck / 1000f;

                float randomFactor = UnityEngine.Random.Range(-0.1f, 0.1f) + luckBias;
                float failureProbability = Mathf.Clamp01(0.5f - (playerStealth) / 180f + randomFactor);

                failureProbability += runningTimeAccum;
                Debug.Log($"[LightsOutScript] Added running penalty ({runningTimeAccum}) to failureProbability, new value: {failureProbability}, nya~!");

                if (IsGuardDogPresentInScene())
                {
                    float before = failureProbability;
                    failureProbability = failureProbability * 3.5f + .1f;
                    Debug.Log($"[LightsOutScript] Doggo present! Failure probability increased from {before} to {failureProbability}, nya~!");
                }
                Debug.Log($"[LightsOutScript] Stealth failure probability rolled: {failureProbability}, nya~!");

                // Roll the dice to see if guards are called
                if (UnityEngine.Random.value < failureProbability)
                {
                    Debug.Log("[LightsOutScript] Player failed Stealth check! Guards called, nya~!");

                    // Show a message box to notify the player
                    DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.Instance.UserInterfaceManager, DaggerfallUI.Instance.UserInterfaceManager.TopWindow);
                    messageBox.SetText("You've been caught!");
                    GameManager.Instance.PlayerEntity.CrimeCommitted = PlayerEntity.Crimes.Trespassing;
                    GameManager.Instance.PlayerEntity.CrimeCommitted = PlayerEntity.Crimes.Breaking_And_Entering;
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
                    GameManager.Instance.PlayerEntity.TallySkill(DFCareer.Skills.Stealth, 5);
                    Debug.Log("[LightsOutScript] Tallied 5 Stealth skill uses for player, nya~!");
                }
            }

            Debug.Log("[LightsOutScript] Stopping Stealth check coroutine because player was caught or LightsOut turned off, nya~!");
        }

        private IEnumerator TriggerLightsOutCoroutine()
        {
            Debug.Log("[LightsOutScript] Coroutine started, nya~! Waiting for 1.5 seconds..."); // Debug log for tracking nya~!
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return new WaitForSeconds(1.0f); // Pause for n seconds nya~!
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            TurnOutTheLights(); // Call the TurnOutTheLights method nya~!
            Debug.Log("[LightsOutScript] TurnOutTheLights method called, nya~!"); // Log the method execution nya~!
        }

        public bool skipStealthCheck;

        private void TurnOutTheLights()
        {
            if (!breakInScriptEnabled) 
            {
                return;
            }

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

            BuildingDirectory buildingDirectory = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory();
            if (buildingDirectory != null)
            {
                // Get the building summary
                BuildingSummary buildingSummary;
                if (buildingDirectory.GetBuildingSummary(playerEnterExit.BuildingDiscoveryData.buildingKey, out buildingSummary))
                {
                    // Check for Thieves Guild (42) or Dark Brotherhood (108) by raw number
                    // regardless of building type since they use regular house types
                    if (buildingSummary.FactionId == 42 || buildingSummary.FactionId == 108 || buildingSummary.FactionId == 77)
                    {
                        Debug.Log($"[LightsOutScript] TurnOutTheLights() skipped because player is in a special faction building (Faction ID: {buildingSummary.FactionId}, Building Type: {buildingType})");
                        return;
                    }
                }
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
                case DFLocation.BuildingTypes.FurnitureStore:
                    if (currentHour >= 10 && currentHour < 23) return; // Only fire between 20:00 and 9:00
                    break;
                case DFLocation.BuildingTypes.WeaponSmith:
                    if (currentHour >= 9 && currentHour < 20) return; // Only fire between 19:00 and 9:00
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
                    if (currentHour >= 6 && currentHour < 18) return; // Only fire between 22:00 and 6:00
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
                                if (material.HasProperty("_EmissionMap") || material.HasProperty("_EmissionColor"))
                                {
                                    material.DisableKeyword("_EMISSION");
                                    material.SetColor("_EmissionColor", Color.black);
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

            stopMusicFlag = false;
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
                BoxCollider boxCollider = customNPC.GetComponent<BoxCollider>();
                if (boxCollider != null)
                {
                    boxCollider.enabled = false;
                }
                CapsuleCollider collider = customNPC.GetComponent<CapsuleCollider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
            LightsOut = true;
            int livingNPCCount = CustomNPCBridgeMod.CustomNPCBridge.Instance.GetLivingNPCCountInInterior();
            CustomStaticNPCMod.CustomStaticNPC.NothingHereAidan();
            Debug.Log("[LightsOutScript] NothingHereAidan called");
            bool skipStealthCheck = false;
            if (buildingType == DFLocation.BuildingTypes.GuildHall)
            {
                if (buildingDirectory != null)
                {
                    BuildingSummary buildingSummary;
                    if (buildingDirectory.GetBuildingSummary(playerEnterExit.BuildingDiscoveryData.buildingKey, out buildingSummary))
                    {
                        PlayerGPS.DiscoveredBuilding discoveredBuilding;
                        if (GameManager.Instance.PlayerGPS.GetDiscoveredBuilding(playerEnterExit.BuildingDiscoveryData.buildingKey, out discoveredBuilding))
                        {
                            GuildManager guildManager = GameManager.Instance.GuildManager;
                            if (guildManager != null)
                            {
                                IGuild guild = guildManager.GetGuild(discoveredBuilding.factionID);
                                if (guild != null && guild.IsMember() && guild.Rank >= 5)
                                {
                                    skipStealthCheck = true;
                                }
                            }
                        }
                    }
                }
            }

            if (!skipStealthCheck)
            {
                StartCoroutine(PeriodicStealthCheckCoroutine());
            }
            else
            {
                Debug.Log("[LightsOutScript] Skipping stealth checks due to high guild rank in this guild");
            }

            int mapId = GameManager.Instance.PlayerGPS.CurrentMapID;
            int buildingKey = playerEnterExit.BuildingDiscoveryData.buildingKey;
            string uniqueBuildingId = $"{mapId}_{buildingKey}";
            var randomMarkers = new List<DaggerfallBillboard>();
            foreach (var billboard in GameObject.FindObjectsOfType<DaggerfallBillboard>())
            {
                if (billboard.transform.root != null &&
                    billboard.transform.root.name == "Interior" &&
                    billboard.Summary.Archive == 199 &&
                    billboard.Summary.Record == 20)
                {
                    randomMarkers.Add(billboard);
                }
            }

            if (buildingKey == 0)
            {
                Debug.LogWarning("[LightsOutScript] Could not determine buildingKey, loot pile spawn aborted, nya~!");
                return;
            }

            if (robbedBuildings.Contains(uniqueBuildingId))
            {
                Debug.Log("[LightsOutScript] This building was already robbed, no more loot piles for you, nya~!");
                return;
            }

            // 2. If any found, pick one at random and spawn loot container
            if (randomMarkers.Count > 0)
            {
                int chosen = UnityEngine.Random.Range(0, randomMarkers.Count);
                DaggerfallBillboard selectedMarker = randomMarkers[chosen];

                var meshRenderer = selectedMarker.GetComponent<MeshRenderer>();

                Vector3 markerPos = selectedMarker.transform.position;
                Transform markerParent = selectedMarker.transform.parent;

                // Destroy the old marker
                GameObject.Destroy(selectedMarker.gameObject);

                // Create the loot object
                DaggerfallLoot loot = GameObjectHelper.CreateLootContainer(
                    LootContainerTypes.RandomTreasure,
                    InventoryContainerImages.Chest,
                    markerPos,
                    markerParent,
                    253,  // archive
                    36    // record (chest icon)
                );

                // Add a SerializableLootContainer if not present
                if (loot.GetComponent<SerializableLootContainer>() == null)
                    loot.gameObject.AddComponent<SerializableLootContainer>();

                // Enable the MeshRenderer if it exists (should be on the loot object, not the destroyed marker)
                var lootMeshRenderer = loot.GetComponent<MeshRenderer>();
                if (lootMeshRenderer != null)
                {
                    lootMeshRenderer.enabled = true;
                    Debug.Log($"[LightsOutScript] Enabled MeshRenderer on loot chest!");
                }

                // Add a SphereCollider for interaction if not present
                if (loot.GetComponent<SphereCollider>() == null)
                {
                    var sphere = loot.gameObject.AddComponent<SphereCollider>();
                    sphere.isTrigger = true;
                    sphere.radius = 0.4f;
                }

                // Optionally fill with random loot
                loot.Items.Clear();
                DaggerfallLoot.GenerateItems("K", loot.Items);
                robbedBuildings.Add(uniqueBuildingId);
                Debug.Log("[LightsOutScript] Spawned interactable random loot chest at a random marker, nya~!");
            }
            else
            {
                Debug.Log("[LightsOutScript] No random treasure marker billboards found for loot, nya~!");
            }
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