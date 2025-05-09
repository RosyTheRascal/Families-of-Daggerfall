using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
using FactionNPCInitializerMod;
using CustomNPCClickHandlerMod;
using CustomTalkManagerMod;

namespace FactionParserMod
{
    public class FactionParser : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            
            var go = new GameObject(mod.Title);
            go.AddComponent<FactionParser>();

            mod.IsReady = true;
        }

        public string factionFilePath = "Assets/StreamingAssets/Factions/Faction.Txt";  // Corrected file path

        public Dictionary<int, FactionFile.FactionData> ParseFactionFile()
        {
            Dictionary<int, FactionFile.FactionData> factions = new Dictionary<int, FactionFile.FactionData>();
            string[] lines = File.ReadAllLines(factionFilePath);  // Use File.ReadAllLines to read lines from the file
            FactionFile.FactionData currentFaction = new FactionFile.FactionData();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split(':');
                if (parts.Length < 2)
                    continue;

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentFaction.id != 0)
                    {
                        factions[currentFaction.id] = currentFaction;
                    }
                    currentFaction = new FactionFile.FactionData();
                }

                SetFactionData(ref currentFaction, key, value);
            }

            if (currentFaction.id != 0)
            {
                factions[currentFaction.id] = currentFaction;
            }

            return factions;
        }

        private void SetFactionData(ref FactionFile.FactionData faction, string key, string value)
        {
            switch (key.ToLower())
            {
                case "id":
                    faction.id = int.Parse(value);
                    break;
                case "name":
                    faction.name = value;
                    break;
                case "rep":
                    faction.rep = int.Parse(value);
                    break;
                case "summon":
                    faction.summon = int.Parse(value);
                    break;
                case "region":
                    faction.region = int.Parse(value);
                    break;
                case "power":
                    faction.power = int.Parse(value);
                    break;
                case "flags":
                    faction.flags = int.Parse(value);
                    break;
                case "face":
                    faction.face = int.Parse(value);
                    break;
                case "race":
                    faction.race = int.Parse(value);
                    break;
                case "flat":
                    int flat = int.Parse(value.Split()[0]);
                    if (faction.flat1 == 0)
                    {
                        faction.flat1 = flat;
                    }
                    else
                    {
                        faction.flat2 = flat;
                    }
                    break;
                case "sgroup":
                    faction.sgroup = int.Parse(value);
                    break;
                case "ggroup":
                    faction.ggroup = int.Parse(value);
                    break;
                case "minf":
                    faction.minf = int.Parse(value);
                    break;
                case "maxf":
                    faction.maxf = int.Parse(value);
                    break;
            }
        }

        private void Start()
        {

        }
    }
}
