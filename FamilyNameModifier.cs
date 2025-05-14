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
            Debug.Log("Calling ReplaceAllNPCs");
            StaticNPC[] originalNPCs = FindObjectsOfType<StaticNPC>();
            foreach (StaticNPC originalNpc in originalNPCs)
            {
                ReplaceAndRegisterNPC(originalNpc);
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
            DespawnCustomNPCs();

            // Determine the current region
            int currentRegionIndex = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
            NameHelper.BankTypes surnameBank;

            // Assign surname bank based on region
            if (IsBretonRegion(currentRegionIndex))
            {
                surnameBank = NameHelper.BankTypes.Breton;
            }
            else if (IsRedguardRegion(currentRegionIndex))
            {
                surnameBank = NameHelper.BankTypes.Redguard;
            }
            else
            {
                surnameBank = NameHelper.BankTypes.Breton; // Default to Breton
            }

            // Pass the detected region to the coroutine
            updateNPCCoroutine = StartCoroutine(IntermediateCoroutine(surnameBank));
            Debug.Log("Starting new IntermediateCoroutine with region-specific surnames.");
        }

        private IEnumerator IntermediateCoroutine(NameHelper.BankTypes surnameBank)
        {
            Debug.Log("Waiting for 60 frames before updating NPC names.");
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForEndOfFrame();
            }

            // Update NPC names with the appropriate surname bank
            UpdateNPCNamesAfterSceneLoad(surnameBank);

            Debug.Log("IntermediateCoroutine completed. NPC names updated.");
        }

        private void UpdateNPCNamesAfterSceneLoad(NameHelper.BankTypes surnameBank)
        {
            // Find all custom NPCs and update their names
            CustomStaticNPCMod.CustomStaticNPC[] customNPCs = FindObjectsOfType<CustomStaticNPCMod.CustomStaticNPC>();
            foreach (var npc in customNPCs)
            {
                string firstName = DaggerfallUnity.Instance.NameHelper.FirstName(npc.NameBank, npc.Gender);
                string lastName = DaggerfallUnity.Instance.NameHelper.Surname(surnameBank);
                npc.CustomDisplayName = $"{firstName} {lastName}";
                Debug.Log($"Updated NPC {npc.CustomDisplayName} with region-appropriate last name.");
            }
        }

        // Helper methods for region detection
        private bool IsBretonRegion(int regionIndex)
        {
            int[] highRockRegions = { 1, 2, 3, 4, 5 }; // Replace with actual Breton region indexes
            return highRockRegions.Contains(regionIndex);
        }

        private bool IsRedguardRegion(int regionIndex)
        {
            int[] hammerfellRegions = { 6, 7, 8, 9, 10 }; // Replace with actual Redguard region indexes
            return hammerfellRegions.Contains(regionIndex);
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