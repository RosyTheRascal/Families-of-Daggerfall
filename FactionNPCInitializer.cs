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
        public void AddCustomNPC(GameObject billboard, int archiveIndex, int recordIndex)
        {
            // Add CustomStaticNPC component
            var customNPC = billboard.AddComponent<CustomStaticNPC>();

            // Add BoxCollider for raycasting
            var collider = billboard.AddComponent<BoxCollider>();
            collider.size = new Vector3(1, 2, 1); // Adjust size as needed
            collider.center = new Vector3(0, 1, 0); // Adjust position as needed

            // Determine race and gender
            string race = DetermineRace(archiveIndex);
            string gender = DetermineGender(archiveIndex, recordIndex);

            // Assign name using game's name generation logic
            customNPC.Name = GenerateName(race, gender);

            // Set default portrait (placeholder for now)
            customNPC.Portrait = null; // Load default portrait logic here

            // Custom logic for NPC
            customNPC.Race = race;
            customNPC.Gender = gender;
        }

        private string DetermineRace(int archiveIndex)
        {
            switch (archiveIndex)
            {
                case 1300:
                    return "Dark Elf";
                case 1301:
                    return "High Elf";
                case 1302:
                    return "Wood Elf";
                case 1305:
                    return "Khajiit";
                default:
                    return "Unknown";
            }
        }

        private string DetermineGender(int archiveIndex, int recordIndex)
        {
            // Logic for Dark Elves
            if (archiveIndex == 1300)
            {
                if (recordIndex == 3 || recordIndex == 5 || recordIndex == 6 || recordIndex == 7 || recordIndex == 8)
                    return "Male";
                else
                    return "Female";
            }

            // Logic for High Elves
            if (archiveIndex == 1301)
            {
                if (recordIndex == 2 || recordIndex == 3 || recordIndex == 4)
                    return "Male";
                else
                    return "Female";
            }

            // Logic for Wood Elves
            if (archiveIndex == 1302)
            {
                if (recordIndex == 1 || recordIndex == 2)
                    return "Male";
                else
                    return "Female";
            }

            // Logic for Khajiit
            if (archiveIndex == 1305)
            {
                return "Male";
            }

            return "Unknown";
        }

        private string GenerateName(string race, string gender)
        {
            // Use the game's existing name generation logic
            // Placeholder for now
            return NameGenerator.GetNameForRaceAndGender(race, gender);
        }
    }

}
