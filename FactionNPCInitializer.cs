using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
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
using System.Reflection;
using FamilyNameModifierMod;
using CustomNPCBridgeMod;
using CustomStaticNPCMod;
using FactionParserMod;
using CustomNPCClickHandlerMod;
using CustomTalkManagerMod;

namespace FactionNPCInitializerMod
{
    public class FactionNPCInitializer : MonoBehaviour
    {
        private static Mod mod;
        public FactionParser factionParser;
        private Dictionary<int, FactionFile.FactionData> factions;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            var initializer = go.AddComponent<FactionNPCInitializer>();
            DontDestroyOnLoad(go);
            initializer.factionParser = go.AddComponent<FactionParser>();

            mod.IsReady = true;
        }

        private void Start()
        {
            if (factionParser == null)
            {
                Debug.LogError("FactionParser is not assigned!");
                return;
            }

            factions = factionParser.ParseFactionFile();
            foreach (var faction in factions.Values)
            {
                CreateNPCForFaction(faction);
            }
        }

        void CreateNPCForFaction(FactionFile.FactionData faction)
        {
            GameObject npcObject = new GameObject(faction.name);
            CustomStaticNPCMod.CustomStaticNPC customStaticNPC = npcObject.AddComponent<CustomStaticNPCMod.CustomStaticNPC>();

            // Assign name bank based on region
            int currentRegion = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
            NameHelper.BankTypes nameBank = (MapsFile.RegionRaces[currentRegion] == (int)Races.Redguard)
                ? NameHelper.BankTypes.Redguard
                : NameHelper.BankTypes.Breton;

            StaticNPC.NPCData npcData = new StaticNPC.NPCData
            {
                nameSeed = faction.id,
                factionID = faction.id,
                nameBank = nameBank
            };

            customStaticNPC.InitializeNPCData(npcData);
            Debug.Log($"Created NPC for faction: {faction.name} with name bank: {nameBank}");
        }
    }

    public class CustomNPCInitializer : MonoBehaviour
    {
        private void OnEnable()
        {
            // Subscribe to interior transition event
            PlayerEnterExit.OnTransitionToInterior += OnTransitionToInterior;
            Debug.Log("CustomNPCInitializer: Subscribed to OnTransitionToInterior event.");
        }

        private void OnDisable()
        {
            // Unsubscribe to avoid memory leaks
            PlayerEnterExit.OnTransitionToInterior -= OnTransitionToInterior;
            Debug.Log("CustomNPCInitializer: Unsubscribed from OnTransitionToInterior event.");
        }

        private void OnTransitionToInterior()
        {
            Debug.Log("CustomNPCInitializer: Transitioned to an interior. Initializing Custom NPCs.");

            // Find all game objects tagged as billboards (or use a specific method to find them)
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
                        Debug.Log($"CustomNPCInitializer: Found billboard with ArchiveIndex = {archiveIndex}, RecordIndex = {recordIndex}. Adding components.");

                        // Add the custom NPC components
                        AddCustomNPC(billboard, archiveIndex, recordIndex);
                    }
                }
            }

            Debug.Log("CustomNPCInitializer: Finished processing billboards.");
        }

        private bool IsCustomNPCArchive(int archiveIndex)
        {
            // Check if the archive index matches any of the defined custom NPC types
            return archiveIndex == 1300 || archiveIndex == 1301 || archiveIndex == 1302 || archiveIndex == 1305;
        }

        public void AddCustomNPC(GameObject billboard, int archiveIndex, int recordIndex)
        {
            // Add CustomStaticNPC component
            var customNPC = billboard.AddComponent<CustomStaticNPC>();

            // Add BoxCollider for raycasting
            var collider = billboard.AddComponent<BoxCollider>();
            collider.size = new Vector3(1, 2, 1); // Adjust size as needed
            collider.center = new Vector3(0, 1, 0); // Adjust position as needed

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

            Debug.Log($"CustomNPCInitializer: Created NPC: Name = {name}, Race = {race}, Gender = {gender}");
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
            // Placeholder logic for name generation
            // Replace this with actual logic from Daggerfall Unity's NameGenerator if available
            return $"{race}_{gender}_{UnityEngine.Random.Range(1000, 9999)}";
        }
    }

}
