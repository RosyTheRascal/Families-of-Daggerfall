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

    public class CustomBillboardNPC : MonoBehaviour
    {
        private int npcId;
        private string displayName;
        private bool isInitialized = false;

        private void Start()
        {
            // Initialize the billboard NPC
            InitializeBillboardNPC();
        }

        private void InitializeBillboardNPC()
        {
            if (isInitialized)
                return;

            // Generate a unique NPC ID
            npcId = GenerateUniqueNPCId();

            // Generate a display name for the NPC
            displayName = GenerateBillboardDisplayName();

            // Log initialization for debugging
            Debug.Log($"CustomBillboardNPC: Initialized billboard NPC with ID = {npcId}, Name = {displayName}");

            isInitialized = true;
        }

        private int GenerateUniqueNPCId()
        {
            // Combine world position and other properties for a unique ID
            Vector3 position = transform.position;
            return Mathf.Abs((int)(position.x * 1000 + position.z)); // Example hash logic
        }

        private string GenerateBillboardDisplayName()
        {
            // Generate a display name based on NPC ID or some unique property
            return $"Billboard NPC {npcId}";
        }

        private void OnMouseDown()
        {
            // Handle interaction when the billboard is clicked
            StartConversation();
        }

        private void StartConversation()
        {
            if (!isInitialized)
            {
                Debug.LogError("CustomBillboardNPC: Attempted to start conversation before initialization.");
                return;
            }

            // Start the conversation through the CustomTalkManager
            CustomTalkManager.Instance.StartConversationWithBillboard(this);
        }

        public int GetNPCId()
        {
            return npcId;
        }

        public string GetDisplayName()
        {
            return displayName;
        }
    }

}
