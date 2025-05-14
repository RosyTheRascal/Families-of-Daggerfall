using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
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
}
