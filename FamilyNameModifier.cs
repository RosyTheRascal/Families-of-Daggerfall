using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
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
using UnityEngine.SceneManagement; 
using System.Reflection;
using CustomStaticNPCMod;
using CustomNPCBridgeMod;
using FactionNPCInitializerMod;
using FactionParserMod;
using CustomNPCClickHandlerMod;
using CustomTalkManagerMod;

namespace FamilyNameModifierMod
{
    public class FamilyNameModifier : MonoBehaviour
    {
        private static Mod mod;
        private static FamilyNameModifier instance;
        private string familyLastName;

        private Dictionary<string, string> buildingLastNames = new Dictionary<string, string>();
        private List<int> residentialModelIds = new List<int>();

        public static FamilyNameModifier Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("FamilyNameModifier");
                    instance = go.AddComponent<FamilyNameModifier>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            var instance = go.AddComponent<FamilyNameModifier>();
            mod.IsReady = true;
            Debug.Log("FamilyNameModifier initialized.");
        }

        void Start()
        {
            familyLastName = GenerateFamilyLastName();
            Debug.Log("FamilyNameModifier script started.");
        }

        private void SaveFamilyLastName(string buildingId, string lastName)
        {
            buildingLastNames[buildingId] = lastName;
        }

        private string LoadFamilyLastName(string buildingId)
        {
            if (buildingLastNames.TryGetValue(buildingId, out string lastName))
            {
                return lastName;
            }
            return null;
        }

        public string GenerateFamilyLastName()
        {
            string buildingId = GetBuildingIdentifier();
            string lastName = LoadFamilyLastName(buildingId);
            string lastNameNord = LoadFamilyLastName(buildingId);
            string lastNameRedguard = LoadFamilyLastName(buildingId);
            string lastNameDarkElf = LoadFamilyLastName(buildingId);
            string lastNameHighElf = LoadFamilyLastName(buildingId);
            string lastNameWoodElf = LoadFamilyLastName(buildingId);
            string lastNameKhajiit = LoadFamilyLastName(buildingId);
            string lastNameArgonian = LoadFamilyLastName(buildingId);

            if (string.IsNullOrEmpty(lastName))
            {
                int seed = GenerateUniqueSeed(buildingId);
                UnityEngine.Random.InitState(seed);
                lastName = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.Breton);
                lastNameNord = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.Nord);
                lastNameRedguard = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.Redguard);
                lastNameDarkElf = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.DarkElf);
                lastNameHighElf = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.HighElf);
                lastNameWoodElf = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.WoodElf);
                lastNameKhajiit = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.Khajiit);
                lastNameArgonian = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.Imperial);
                SaveFamilyLastName(buildingId, lastName);
            }

            return lastName;
        }

        private int GenerateUniqueSeed(string buildingId)
        {
            // Generate a unique seed based on the building ID
            return buildingId.GetHashCode();
        }

        private void SaveBuildingLastNames()
        {
            var json = JsonUtility.ToJson(new SerializableDictionary<string, string>(buildingLastNames));
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "buildingLastNames.json"), json);
        }

        private void LoadBuildingLastNames()
        {
            var path = Path.Combine(Application.persistentDataPath, "buildingLastNames.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                buildingLastNames = JsonUtility.FromJson<SerializableDictionary<string, string>>(json).ToDictionary();
            }
        }

        [Serializable]
        public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
        {
            [SerializeField] private List<TKey> keys = new List<TKey>();
            [SerializeField] private List<TValue> values = new List<TValue>();

            private Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

            public SerializableDictionary(Dictionary<TKey, TValue> dictionary)
            {
                this.dictionary = dictionary;
            }

            public Dictionary<TKey, TValue> ToDictionary()
            {
                return dictionary;
            }

            public void OnBeforeSerialize()
            {
                keys.Clear();
                values.Clear();

                foreach (var kvp in dictionary)
                {
                    keys.Add(kvp.Key);
                    values.Add(kvp.Value);
                }
            }

            public void OnAfterDeserialize()
            {
                dictionary = new Dictionary<TKey, TValue>();

                for (int i = 0; i < keys.Count; i++)
                {
                    dictionary[keys[i]] = values[i];
                }
            }
        }

        public void ReplaceAllNPCs()
        {
            Debug.Log("Calling ReplaceAllNPCs. Locating 'interior' parent.");

            GameObject interiorParent = null;
            if (GameManager.Instance.PlayerEnterExit != null && GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                interiorParent = GameManager.Instance.PlayerEnterExit.InteriorParent;
            }

            if (interiorParent == null)
            {
                interiorParent = GameObject.Find("Interior");
            }

            if (interiorParent == null)
            {
                Debug.LogWarning("ReplaceAllNPCs: 'interior' parent not found. Skipping processing.");
                return;
            }

            Debug.Log($"ReplaceAllNPCs: Found 'interior' parent: {interiorParent.name}. Searching for NPCs in the hierarchy.");

            // Find all StaticNPCs within the "interior" parent
            StaticNPC[] originalNPCs = interiorParent.GetComponentsInChildren<StaticNPC>();
            foreach (StaticNPC originalNpc in originalNPCs)
            {
                Debug.Log($"ReplaceAllNPCs: Found NPC '{originalNpc.name}' in hierarchy. Applying ReplaceAndRegisterNPC.");
                ReplaceAndRegisterNPC(originalNpc);
            }

            Debug.Log("ReplaceAllNPCs: Finished processing all NPCs in the 'interior' hierarchy.");

            // Now process all billboards in the "interior" parent
            Debug.Log("ReplaceAllNPCs: Processing billboards with specific archive indices.");
            ProcessBillboards(interiorParent);
        }

        // Method to process billboards with specific archive indices
        private void ProcessBillboards(GameObject parent)
        {
            // Dictionary to store shared last names for each race
            Dictionary<string, string> raceLastNames = new Dictionary<string, string>();

            Billboard[] billboards = parent.GetComponentsInChildren<Billboard>();
            foreach (Billboard billboard in billboards)
            {
                // Use the summary.Archive field to check for valid archive indices
                int archiveIndex = billboard.Summary.Archive;
                switch (archiveIndex)
                {
                    case 1300: // Dark Elf
                    case 1301: // High Elf
                    case 1302: // Wood Elf
                    case 1305: // Khajiit
                        Debug.Log($"ProcessBillboards: Found billboard with archive index {archiveIndex}. Attaching components.");

                        // Attach a CapsuleCollider if not already present
                        CapsuleCollider collider = billboard.gameObject.GetComponent<CapsuleCollider>();
                        if (collider == null)
                        {
                            collider = billboard.gameObject.AddComponent<CapsuleCollider>();
                            Debug.Log($"ProcessBillboards: Added CapsuleCollider to billboard '{billboard.name}'.");
                        }

                        // Attach the CustomStaticNPC component if not already present
                        CustomStaticNPC customNPC = billboard.gameObject.GetComponent<CustomStaticNPC>();
                        if (customNPC == null)
                        {
                            customNPC = billboard.gameObject.AddComponent<CustomStaticNPC>();
                            Debug.Log($"ProcessBillboards: Added CustomStaticNPC to billboard '{billboard.name}'.");
                        }

                        // Store the original billboard data before assigning names
                        customNPC.StoreOriginalBillboardData(billboard.Summary.Archive, billboard.Summary.Record);

                        // Set race-based display name
                        SetRaceDisplayName(billboard, archiveIndex, raceLastNames);
                        CustomStaticNPC.UpdateSpecialBillboardsFlag();
                        break;

                    default:
                        break;
                }
            }

            Debug.Log("ProcessBillboards: Finished processing billboards.");
        }

        private void SetRaceDisplayName(Billboard billboard, int archiveIndex, Dictionary<string, string> raceLastNames)
        {
            // Determine race based on archive index
            NameHelper.BankTypes race;
            switch (archiveIndex)
            {
                case 1300: race = NameHelper.BankTypes.DarkElf; break;
                case 1301: race = NameHelper.BankTypes.HighElf; break;
                case 1302: race = NameHelper.BankTypes.WoodElf; break;
                case 1305: race = NameHelper.BankTypes.Khajiit; break;
                default:
                    Debug.LogWarning($"SetRaceDisplayName: Unsupported archive index {archiveIndex} for billboard '{billboard.name}'. Skipping.");
                    return;
            }

            // Determine gender based on race/archive index and record
            Genders gender = Genders.Female; // Default to female
            switch (archiveIndex)
            {
                case 1300: if (new[] { 3, 5, 6, 7, 8 }.Contains(billboard.Summary.Record)) gender = Genders.Male; break;
                case 1301: if (new[] { 2, 3, 4 }.Contains(billboard.Summary.Record)) gender = Genders.Male; break;
                case 1302: if (new[] { 1, 2 }.Contains(billboard.Summary.Record)) gender = Genders.Male; break;
                case 1305: gender = Genders.Male; break;
            }

            // Use building ID as seed so names are stable per building
            string buildingId = GetBuildingIdentifier();
            string raceBuildingKey = $"{buildingId}_{race}";
            int seed = buildingId.GetHashCode();

            // Generate and cache a shared last name for this race/building combo
            if (!raceLastNames.TryGetValue(raceBuildingKey, out string lastName))
            {
                UnityEngine.Random.InitState(seed + (int)race); // add race to seed for extra safety
                lastName = DaggerfallUnity.Instance.NameHelper.Surname(race);
                raceLastNames[raceBuildingKey] = lastName;
            }

            // Generate a unique first name per NPC (using race/gender as usual)
            string firstName = DaggerfallUnity.Instance.NameHelper.FirstName(race, gender);

            // Set the display name
            CustomStaticNPC customNPC = billboard.gameObject.GetComponent<CustomStaticNPC>();
            if (customNPC != null)
            {
                customNPC.CustomDisplayName = $"{firstName} {lastName}";
                Debug.Log($"SetRaceDisplayName: Set name '{firstName} {lastName}' for race {race} (archive index {archiveIndex}), building '{buildingId}'.");
            }
        }

        public void ReplaceAndRegisterNPC(StaticNPC originalNpc)
        {
            int originalNpcId = originalNpc.GetInstanceID();
            Debug.Log($"ReplaceAndRegisterNPC: Original NPC ID: {originalNpcId}");

            // Unregister the original NPC from CustomNPCBridge
            CustomNPCBridge.Instance.UnregisterNPC(originalNpcId);

            // Create the new NPC GameObject and set its transform properties
            GameObject newNpcObject = new GameObject(originalNpc.gameObject.name);
            newNpcObject.transform.position = originalNpc.transform.position;
            newNpcObject.transform.rotation = originalNpc.transform.rotation;
            newNpcObject.transform.localScale = originalNpc.transform.localScale;

            // Add the CustomStaticNPC component and disable it initially
            CustomStaticNPCMod.CustomStaticNPC customNpc = newNpcObject.AddComponent<CustomStaticNPCMod.CustomStaticNPC>();
            customNpc.enabled = false;

            int customNpcId = customNpc.GetInstanceID();
            customNpc.Initialize(customNpcId, originalNpc.Data, familyLastName);

            // Ensure the new NPC's transform is set correctly
            newNpcObject.transform.position = originalNpc.transform.position;
            newNpcObject.transform.rotation = originalNpc.transform.rotation;
            newNpcObject.transform.localScale = originalNpc.transform.localScale;

            // Destroy the original NPC GameObject
            DestroyImmediate(originalNpc.gameObject);

            // Check if the NPC is already registered in CustomNPCBridge
            if (!CustomNPCBridge.Instance.IsCustomNPCRegistered(customNpcId))
            {
                CustomNPCBridge.Instance.RegisterCustomNPC(customNpcId, customNpc);
                Debug.Log($"ReplaceAndRegisterNPC: Custom NPC DisplayName: {customNpc.CustomDisplayName}");
            }

            // Enable the CustomStaticNPC component after initialization
            customNpc.enabled = true;
            Debug.Log($"Enabled CustomStaticNPC component for NPC ID {customNpcId}.");
        }

        private string GenerateFamilyLastName(string buildingId, int regionIndex)
        {
            // Ensure a consistent last name is generated for the building
            if (!buildingLastNames.TryGetValue(buildingId, out string lastName))
            {
                // Check if the region belongs to Hammerfell
                if (IsHammerfellRegion(regionIndex))
                {
                    int seed = buildingId.GetHashCode();
                    UnityEngine.Random.InitState(seed);
                    lastName = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.Redguard);
                }
                else if (IsHighRockRegion(regionIndex))
                {
                    // Generate unique last names for High Rock
                    int seed = buildingId.GetHashCode();
                    UnityEngine.Random.InitState(seed);
                    string redguardFirstName = DaggerfallUnity.Instance.NameHelper.FirstName(NameHelper.BankTypes.Redguard, Genders.Male);
                    lastName = $"son of {redguardFirstName}";
                }
                else
                {
                    // Default logic
                    int seed = buildingId.GetHashCode();
                    UnityEngine.Random.InitState(seed);
                    lastName = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.Breton);
                }

                buildingLastNames[buildingId] = lastName;
            }
            return lastName;
        }

        public bool IsHammerfellRegion(int regionIndex)
        {
            // Define Hammerfell regions based on findings
            int[] hammerfellRegions = new int[]
            {
        0, 1, 20, 22, 21, 25, 28, 29, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56
            };
            return System.Array.Exists(hammerfellRegions, region => region == regionIndex);
        }

        private bool IsHighRockRegion(int regionIndex)
        {
            // Define High Rock regions based on findings
            int[] highRockRegions = new int[]
            {
        2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 23, 24, 30, 31,
        32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42
            };
            return System.Array.Exists(highRockRegions, region => region == regionIndex);
        }

        public void ReplaceAndRegisterNPC(DaggerfallWorkshop.Game.StaticNPC npc, string buildingId)
        {
            // Get the region index using the PlayerGPS class
            int regionIndex = GameManager.Instance.PlayerGPS.CurrentRegionIndex;

            // Pass the regionIndex to GenerateFamilyLastName
            string familyLastName = GenerateFamilyLastName(buildingId, regionIndex);

            // Call logic to replace NPC with shared last name
            CustomStaticNPCMod.CustomStaticNPC customNPC = npc.gameObject.AddComponent<CustomStaticNPCMod.CustomStaticNPC>();
            customNPC.Initialize(npc.Data.nameSeed, npc.Data, familyLastName);
            Debug.Log($"NPC with ID {npc.GetInstanceID()} assigned to family '{familyLastName}'.");
        }

        private void RemoveFromCache(GameObject gameObject)
        {
            Debug.Log($"Attempting to remove GameObject {gameObject.name} from staticNpcCache.");
            var cache = typeof(ActiveGameObjectDatabase)
                .GetField("staticNpcCache", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null) as GameObjectCache;

            var cachedObjectsField = typeof(GameObjectCache)
                .GetField("cachedObjects", BindingFlags.NonPublic | BindingFlags.Instance);

            var cachedObjects = cachedObjectsField.GetValue(cache) as List<WeakReference<GameObject>>;

            int initialCount = cachedObjects.Count;
            cachedObjects.RemoveAll(weakRef => weakRef.TryGetTarget(out GameObject target) && target == gameObject);
            int finalCount = cachedObjects.Count;

            Debug.Log($"Removed {initialCount - finalCount} entries from staticNpcCache for GameObject {gameObject.name}.");
        }

        void Awake()
        {
            PlayerEnterExit.OnTransitionInterior += OnTransitionToInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionToExterior;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SaveBuildingLastNames(); // Save last names on destroy
            PlayerEnterExit.OnTransitionInterior -= OnTransitionToInterior;
            PlayerEnterExit.OnTransitionExterior -= OnTransitionToExterior;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnTransitionToExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("Player exited a building. Despawning custom NPCs.");
            DespawnCustomNPCs();
        }

        public void DespawnCustomNPCs()
        {
            Debug.Log("Despawning all custom NPCs.");

            // Find all custom NPCs
            CustomStaticNPCMod.CustomStaticNPC[] customNPCs = FindObjectsOfType<CustomStaticNPCMod.CustomStaticNPC>();

            // Destroy each custom NPC GameObject
            foreach (CustomStaticNPCMod.CustomStaticNPC customNpc in customNPCs)
            {
                int customNpcId = customNpc.GetInstanceID();
                Debug.Log($"Despawning Custom NPC with ID: {customNpcId}");

                // Unregister the custom NPC from CustomNPCBridge
                CustomNPCBridge.Instance.UnregisterNPC(customNpcId);

                // Destroy the custom NPC GameObject
                DestroyImmediate(customNpc.gameObject);
            }

            Debug.Log("All custom NPCs have been despawned.");
        }

        private Coroutine updateNPCCoroutine;
        private static bool isUpdateScheduled = false;

        private void OnTransitionToInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            DespawnCustomNPCs();

            if (updateNPCCoroutine != null)
            {
                StopCoroutine(updateNPCCoroutine);
                Debug.Log("Stopping previous IntermediateCoroutine.");
            }

            updateNPCCoroutine = StartCoroutine(IntermediateCoroutine());
            Debug.Log("Starting new IntermediateCoroutine.");
        }

        private IEnumerator IntermediateCoroutine()
        {
            Debug.Log("Waiting for 60 frames before updating NPC names.");
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForEndOfFrame();
            }

            // Check if we already scheduled an update
            if (isUpdateScheduled)
            {
                Debug.Log("Update already scheduled. Exiting coroutine.");
                yield break;
            }

            // Schedule the update
            isUpdateScheduled = true;
            yield return StartCoroutine(UpdateNPCNamesAfterSceneLoad());

            Debug.Log("IntermediateCoroutine completed. NPC names updated.");

            // Reset the flag
            isUpdateScheduled = false;
            updateNPCCoroutine = null;
        }

        private IEnumerator UpdateNPCNamesAfterSceneLoad()
        {
            Debug.Log("UpdateNPCNamesAfterSceneLoad started.");
            yield return null;

            if (IsInResidentialBuilding())
            {
                Debug.Log("Player entered a residential building.");

                // Get the region index using the PlayerGPS class
                int regionIndex = GameManager.Instance.PlayerGPS.CurrentRegionIndex;

                if (IsHammerfellRegion(regionIndex))
                {
                    Debug.Log("Residential building is in Hammerfell. Assigning unique last names to all static NPCs.");

                    // Replace all NPCs with unique last names
                    ReplaceAllNPCs();
                }
                else
                {
                    // Generate a shared family last name
                    familyLastName = GenerateFamilyLastName();
                    Debug.Log($"Generated family last name: {familyLastName}");
                    ReplaceAllNPCs();
                }
            }
            else
            {
                Debug.LogWarning("Not a residential building.");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"Scene loaded: {scene.name}");

            foreach (var npcEntry in CustomNPCBridge.Instance.GetAllCustomNPCs())
            {
                int npcId = npcEntry.Key;
                CustomStaticNPCMod.CustomStaticNPC npc = npcEntry.Value;

                if (npc == null)
                {
                    Debug.LogError($" ID {npcId} is null after scene load.");
                }
                else if (npc.gameObject == null)
                {
                    Debug.LogError($" ID {npcId} is null after scene load.");
                }
                else if (!npc.gameObject.activeSelf)
                {
                    Debug.LogError($"ID {npcId} is inactive after scene load.");
                }
                else
                {
                    Debug.Log($" ID {npcId} is active and exists after scene load.");
                }
            }
        }

        private bool IsInResidentialBuilding()
        {
            var playerEnterExit = GameManager.Instance.PlayerEnterExit;
            if (playerEnterExit.IsPlayerInsideBuilding)
            {
                // Get the current location index
                int locationIndex = GameManager.Instance.PlayerGPS.CurrentLocationIndex;
                if (locationIndex == -1)
                    return false;

                // Get the current building summary from the interior
                var buildingSummary = playerEnterExit.Interior.BuildingData;

                // Extract the building type correctly from the building summary
                DFLocation.BuildingTypes buildingType = buildingSummary.BuildingType;

                bool isResidential = buildingType == DFLocation.BuildingTypes.House1 ||
                                     buildingType == DFLocation.BuildingTypes.House2 ||
                                     buildingType == DFLocation.BuildingTypes.House3 ||
                                     buildingType == DFLocation.BuildingTypes.House4 ||
                                     buildingType == DFLocation.BuildingTypes.House5 ||
                                     buildingType == DFLocation.BuildingTypes.House6;

                Debug.Log($"Building Type: {buildingType}. Is residential: {isResidential}");
                return isResidential;
            }
            return false;
        }

        private string GetBuildingIdentifier()
        {
            var playerGPS = GameManager.Instance.PlayerGPS;
            var location = playerGPS.CurrentLocation;

            // Check if the player is inside a building and if there are exterior doors
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideBuilding &&
                GameManager.Instance.PlayerEnterExit.ExteriorDoors != null &&
                GameManager.Instance.PlayerEnterExit.ExteriorDoors.Length > 0)
            {
                var buildingKey = GameManager.Instance.PlayerEnterExit.ExteriorDoors[0].buildingKey;
                return $"{location.MapTableData.MapId}-{playerGPS.CurrentLocationIndex}-{buildingKey}";
            }

            // Fallback identifier if the player is not inside a building or there are no exterior doors
            return $"{location.MapTableData.MapId}-{playerGPS.CurrentLocationIndex}";
        }

        private void LogNPCDataState(string context, CustomStaticNPCMod.CustomStaticNPC npc)
        {
            if (npc != null && npc.Data.hash != 0)
            {
                Debug.Log($"{context}: NPC has valid data. NPC ID: {npc.GetInstanceID()}");
            }
            else
            {
                Debug.LogError($"{context}: NPC has invalid or null data. NPC ID: {npc?.GetInstanceID() ?? -1}");
            }
        }
    }
}