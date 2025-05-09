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
            StaticNPC.NPCData npcData = new StaticNPC.NPCData
            {
                nameSeed = faction.id,
                factionID = faction.id,
                nameBank = (NameHelper.BankTypes)faction.type
            };

            FactionFile.FlatData flatData = FactionFile.GetFlatData(faction.flat1);
            DaggerfallBillboard billboard = npcObject.AddComponent<DaggerfallBillboard>();
            billboard.SetMaterial(flatData.archive, flatData.record);

            customStaticNPC.InitializeNPCData(npcData);
            customStaticNPC.SetLayoutData(npcData.hash, npcData.gender, npcData.factionID, npcData.nameSeed);

            ActiveGameObjectDatabase.RegisterStaticNPC(npcObject);
            CustomNPCBridge.Instance.RegisterCustomNPC(npcObject.GetInstanceID(), customStaticNPC);
            Debug.Log($"Created and registered NPC for faction: {faction.name} with ID: {faction.id}");
        }
    }
}
