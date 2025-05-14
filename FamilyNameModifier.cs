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

        //REGIONS DICTIONARY xP ;D :333
        #region
        private static readonly Dictionary<int, Races> RegionToRaceMap = new Dictionary<int, Races>
        {
    // Alik'r Desert regions (Redguard)
    { 0, Races.Redguard }, // Alik'r Desert
    { 1, Races.Redguard }, // Dragontail Mountains

    // High Rock regions (Breton)
    { 2, Races.Breton },   // Glenpoint Foothills
    { 3, Races.Breton },   // Daggerfall Bluffs
    { 4, Races.Breton },   // Yeorth Burrowland
    { 5, Races.Breton },   // Dwynnen
    { 6, Races.Breton },   // Ravennian Forest
    { 7, Races.Redguard }, // Devilrock
    { 8, Races.Redguard }, // Malekna Forest
    { 9, Races.Breton },   // Isle of Balfiera

    // More regions
    { 10, Races.Redguard }, // Bantha
    { 11, Races.Redguard }, // Dak'fron
    { 12, Races.Breton },   // Islands in the Western Iliac Bay
    { 13, Races.Breton },   // Tamarilyn Point
    { 14, Races.Redguard }, // Lainlyn Cliffs
    { 15, Races.Breton },   // Bjoulsae River
    { 16, Races.Breton },   // Wrothgarian Mountains
    { 17, Races.Breton },   // Daggerfall
    { 18, Races.Breton },   // Glenpoint
    { 19, Races.Breton },   // Betony
    { 20, Races.Redguard }, // Sentinel
    { 21, Races.Breton },   // Anticlere
    { 22, Races.Redguard }, // Lainlyn
    { 23, Races.Breton },   // Wayrest

    // Villages and coastlines
    { 24, Races.Breton },   // Gen Tem High Rock village
    { 25, Races.Redguard }, // Gen Rai Hammerfell village
    { 26, Races.Breton },   // Orsinium Area
    { 27, Races.Breton },   // Skeffington Wood
    { 28, Races.Redguard }, // Hammerfell Bay Coast
    { 29, Races.Redguard }, // Hammerfell Sea Coast
    { 30, Races.Breton },   // High Rock Bay Coast
    { 31, Races.Breton },   // High Rock Sea Coast

    // Remaining High Rock regions
    { 32, Races.Breton }, // Northmoor
    { 33, Races.Breton }, // Menevia
    { 34, Races.Breton }, // Alcaire
    { 35, Races.Breton }, // Koegria
    { 36, Races.Breton }, // Bhoriane
    { 37, Races.Breton }, // Kambria
    { 38, Races.Breton }, // Phrygias
    { 39, Races.Breton }, // Urvaius
    { 40, Races.Breton }, // Ykalon
    { 41, Races.Breton }, // Daenia
    { 42, Races.Breton }, // Shalgora

    // Remaining Hammerfell regions
    { 43, Races.Redguard }, // Abibon-Gora
    { 44, Races.Redguard }, // Kairou
    { 45, Races.Redguard }, // Pothago
    { 46, Races.Redguard }, // Myrkwasa
    { 47, Races.Redguard }, // Ayasofya
    { 48, Races.Redguard }, // Tigonus
    { 49, Races.Redguard }, // Kozanset
    { 50, Races.Redguard }, // Satakalaam
    { 51, Races.Redguard }, // Totambu
    { 52, Races.Redguard }, // Mournoth
    { 53, Races.Redguard }, // Ephesus
    { 54, Races.Redguard }, // Santaki
    { 55, Races.Redguard }, // Antiphyllos
    { 56, Races.Redguard }, // Bergama

    // Miscellaneous regions
    { 57, Races.Breton }, // Gavaudon
    { 58, Races.Breton }, // Tulune
    { 59, Races.Breton }, // Glenumbra Moors
    { 60, Races.Breton }, // Ilessan Hills
    { 61, Races.Redguard }, // Cybiades
        };
        #endregion

        public string GenerateFamilyLastName()
        {
            string buildingId = GetBuildingIdentifier();
            string lastName = LoadFamilyLastName(buildingId);

            if (string.IsNullOrEmpty(lastName))
            {
                int seed = GenerateUniqueSeed(buildingId);
                UnityEngine.Random.InitState(seed);

                // Detect current region and assign last name bank
                int currentRegion = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
                Races regionRace = RegionToRaceMap.ContainsKey(currentRegion) ? RegionToRaceMap[currentRegion] : Races.Breton;
                Debug.Log($"Region race for current region index {currentRegion}: {regionRace}");

                NameHelper.BankTypes nameBank;

                if (regionRace == Races.Redguard)
                {
                    Debug.Log("Detected Hammerfell region. Assigning Redguard name bank.");
                    nameBank = NameHelper.BankTypes.Redguard; // Hammerfell regions
                }
                else if (regionRace == Races.Breton)
                {
                    Debug.Log("Detected High Rock region. Assigning Breton name bank.");
                    nameBank = NameHelper.BankTypes.Breton; // High Rock regions
                }
                else
                {
                    Debug.LogWarning($"Unexpected race for region index {currentRegion}: {regionRace}. Defaulting to Breton.");
                    nameBank = NameHelper.BankTypes.Breton; // Default to Breton
                }

                lastName = DaggerfallUnity.Instance.NameHelper.Surname(nameBank);
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
            Debug.Log("FamilyNameModifier: ReplaceAllNPCs triggered. Locating 'interior' parent.");

            // Attempt to find the "interior" parent object using PlayerEnterExit
            GameObject interiorParent = null;
            if (GameManager.Instance.PlayerEnterExit != null && GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                interiorParent = GameManager.Instance.PlayerEnterExit.InteriorParent;
            }

            // Fallback to find the "interior" parent by name if not found above
            if (interiorParent == null)
            {
                interiorParent = GameObject.Find("interior");
            }

            // Check if the "interior" parent was successfully found
            if (interiorParent == null)
            {
                Debug.LogWarning("FamilyNameModifier: 'interior' parent not found. Skipping processing.");
                return;
            }

            Debug.Log($"FamilyNameModifier: Found 'interior' parent: {interiorParent.name}. Searching for NPCs in the hierarchy.");

            // Recursively process all child objects under the "interior" parent
            ProcessNPCsInHierarchy(interiorParent.transform);

            Debug.Log("FamilyNameModifier: Finished processing all NPCs in the 'interior' hierarchy.");
        }

        // Recursive method to process NPCs in the hierarchy
        private void ProcessNPCsInHierarchy(Transform parentTransform)
        {
            foreach (Transform child in parentTransform)
            {

                AssignCustomNPC();
                // Check if the child has an NPC component (StaticNPC)
                var originalNpc = child.GetComponent<DaggerfallWorkshop.Game.StaticNPC>();
                if (originalNpc != null)
                {
                    Debug.Log($"FamilyNameModifier: Found NPC '{child.name}' in hierarchy. Applying ReplaceAndRegisterNPC and AssignCustomNPC.");

                    // Replace and register the NPC using the existing method
                    ReplaceAndRegisterNPC(originalNpc);

                }
                else
                {
                    Debug.Log($"FamilyNameModifier: Skipping '{child.name}' as it does not have a StaticNPC component.");
                }

                // Recursively process the child's children
                ProcessNPCsInHierarchy(child);
            }
        }

        public void AssignCustomNPC()
        {
            Debug.Log("FamilyNameModifier: AssignCustomNPC triggered. Initializing custom NPCs for interior.");

            // Find all billboards in the scene
            GameObject[] billboards = FindObjectsOfType<GameObject>();

            foreach (GameObject billboard in billboards)
            {
                // Check if the billboard has the required archive index
                if (billboard.TryGetComponent(out DaggerfallBillboard dfBillboard))
                {
                    int archiveIndex = dfBillboard.Summary.Archive;
                    int recordIndex = dfBillboard.Summary.Record;

                    // Check if the archive index matches the custom NPC criteria
                    if (IsCustomNPCArchive(archiveIndex))
                    {
                        Debug.Log($"FamilyNameModifier: Found billboard with ArchiveIndex = {archiveIndex}, RecordIndex = {recordIndex}. Ensuring components are added.");

                        // Add the custom NPC components if they don't already exist
                        AddCustomNPCIfMissing(billboard, archiveIndex, recordIndex);
                    }
                }
            }

            Debug.Log("FamilyNameModifier: Finished processing billboards.");
        }

        private bool IsCustomNPCArchive(int archiveIndex)
        {
            // Check if the archive index matches any of the defined custom NPC types
            return archiveIndex == 1300 || archiveIndex == 1301 || archiveIndex == 1302 || archiveIndex == 1305;
        }

        private void AddCustomNPCIfMissing(GameObject billboard, int archiveIndex, int recordIndex)
        {
            // Ensure the GameObject only has one CustomStaticNPC component
            if (!billboard.TryGetComponent<CustomStaticNPC>(out var customNPC))
            {
                customNPC = billboard.AddComponent<CustomStaticNPC>();
                Debug.Log($"FamilyNameModifier: Added CustomStaticNPC component to billboard (ArchiveIndex = {archiveIndex}, RecordIndex = {recordIndex}).");
            }

            // Ensure the GameObject only has one Collider component
            if (!billboard.TryGetComponent<CapsuleCollider>(out var collider))
            {
                collider = billboard.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0, 1, 0); // Adjust position as needed
                Debug.Log($"FamilyNameModifier: Added Collider component to billboard (ArchiveIndex = {archiveIndex}, RecordIndex = {recordIndex}).");
            }

            // Determine race and gender (using enums)
            Races race = DetermineRace(archiveIndex);
            Genders gender = DetermineGender(archiveIndex, recordIndex);

            // Assign name using a placeholder logic (since NameGenerator is missing)
            string name = GenerateName(race, gender);

            // Initialize NPC data
            customNPC.InitializeNPCData(new StaticNPC.NPCData
            {
                nameSeed = name.GetHashCode(),
                factionID = 0, // Placeholder for an actual faction ID
                nameBank = NameHelper.BankTypes.Breton // Placeholder for name bank type
            });

            Debug.Log($"FamilyNameModifier: Initialized CustomStaticNPC data for billboard (Name = {name}, Race = {race}, Gender = {gender}).");
        }

        private Races DetermineRace(int archiveIndex)
        {
            switch (archiveIndex)
            {
                case 1300: return Races.DarkElf;
                case 1301: return Races.HighElf;
                case 1302: return Races.WoodElf;
                case 1305: return Races.Khajiit;
                default: return Races.Breton; // Default to Breton as a fallback
            }
        }

        private Genders DetermineGender(int archiveIndex, int recordIndex)
        {
            // Logic for Dark Elves
            if (archiveIndex == 1300)
            {
                return (recordIndex == 3 || recordIndex == 5 || recordIndex == 6 || recordIndex == 7 || recordIndex == 8)
                    ? Genders.Male
                    : Genders.Female;
            }

            // Logic for High Elves
            if (archiveIndex == 1301)
            {
                return (recordIndex == 2 || recordIndex == 3 || recordIndex == 4)
                    ? Genders.Male
                    : Genders.Female;
            }

            // Logic for Wood Elves
            if (archiveIndex == 1302)
            {
                return (recordIndex == 1 || recordIndex == 2)
                    ? Genders.Male
                    : Genders.Female;
            }

            // Logic for Khajiit (all male)
            if (archiveIndex == 1305)
            {
                return Genders.Male;
            }

            return Genders.Female; // Default to Female as a fallback
        }

        private string GenerateName(Races race, Genders gender)
        {
            // Map the Races enum to NameHelper.BankTypes
            NameHelper.BankTypes bankType;
            switch (race)
            {
                case Races.Breton:
                    bankType = NameHelper.BankTypes.Breton;
                    break;
                case Races.Redguard:
                    bankType = NameHelper.BankTypes.Redguard;
                    break;
                case Races.Nord:
                    bankType = NameHelper.BankTypes.Nord;
                    break;
                case Races.DarkElf:
                    bankType = NameHelper.BankTypes.DarkElf;
                    break;
                case Races.HighElf:
                    bankType = NameHelper.BankTypes.HighElf;
                    break;
                case Races.WoodElf:
                    bankType = NameHelper.BankTypes.WoodElf;
                    break;
                case Races.Khajiit:
                    bankType = NameHelper.BankTypes.Khajiit;
                    break;
                case Races.Argonian:
                    bankType = NameHelper.BankTypes.Imperial;
                    break;
                default:
                    Debug.LogWarning($"FamilyNameModifier: Unsupported race '{race}'. Defaulting to Breton names.");
                    bankType = NameHelper.BankTypes.Breton;
                    break;
            }

            // Use the NameHelper to generate a name
            var nameHelper = DaggerfallUnity.Instance.NameHelper;
            if (nameHelper == null)
            {
                Debug.LogError("FamilyNameModifier: NameHelper instance is null. Cannot generate name.");
                return "Unknown Name";
            }

            string generatedName = nameHelper.FullName(bankType, gender);
            Debug.Log($"FamilyNameModifier: Generated name '{generatedName}' for race '{race}' and gender '{gender}'.");
            return generatedName;
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

        private string GenerateFamilyLastName(string buildingId)
        {
            // Ensure a consistent last name is generated for the building
            if (!buildingLastNames.TryGetValue(buildingId, out string lastName))
            {
                int seed = buildingId.GetHashCode();
                UnityEngine.Random.InitState(seed);
                lastName = DaggerfallUnity.Instance.NameHelper.Surname(NameHelper.BankTypes.Breton);
                buildingLastNames[buildingId] = lastName;
            }
            return lastName;
        }

        public void ReplaceAndRegisterNPC(DaggerfallWorkshop.Game.StaticNPC npc, string buildingId)
        {
            string familyLastName = GenerateFamilyLastName(buildingId);
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
            Debug.Log("Player transitioning to interior.");

            // Log the current region
            int currentRegion = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
            Debug.Log($"Current region index: {currentRegion}");

            // Log building type
            if (GameManager.Instance.PlayerEnterExit.Interior != null)
            {
                var buildingData = GameManager.Instance.PlayerEnterExit.Interior.BuildingData;
                Debug.Log($"Building type: {buildingData.BuildingType}");
            }

            // Existing functionality
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
                familyLastName = GenerateFamilyLastName();
                Debug.Log($"Generated family last name: {familyLastName}");

                ReplaceAllNPCs();
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