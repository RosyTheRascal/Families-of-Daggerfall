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
using CustomStaticNPCMod;
using FactionNPCInitializerMod;
using DaggerfallWorkshop.Game.Serialization;
using FactionParserMod;
using CustomDaggerfallTalkWindowMod;

namespace CustomNPCBridgeMod
{
    public class CustomNPCBridge : MonoBehaviour, IHasModSaveData
    {
        private static CustomNPCBridge instance;
        private static Mod mod;
        private static Dictionary<int, CustomStaticNPCMod.CustomStaticNPC> customNPCs = new Dictionary<int, CustomStaticNPCMod.CustomStaticNPC>();
        private static Dictionary<int, int> npcGreetingSections = new Dictionary<int, int>(); // New dictionary to store NPC greeting sections

        private static HashSet<int> deadNPCs = new HashSet<int>();
        private static HashSet<int> emptyBuildings = new HashSet<int>();

        public CustomNPCBoostData boostData = new CustomNPCBoostData();

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            instance = go.AddComponent<CustomNPCBridge>();
            DontDestroyOnLoad(go);
            mod.IsReady = true;

            // Register the save data interface
            mod.SaveDataInterface = instance;
        }

        public Type SaveDataType
        {
            get { return typeof(CustomNPCBoostData); }
        }

        public object NewSaveData()
        {
            return new CustomNPCBoostData();
        }

        public object GetSaveData()
        {
            return boostData;
        }

        public void RestoreSaveData(object saveData)
        {

            Debug.Log($"Save restored");
            deadNPCs.Clear();
            emptyBuildings.Clear();
            boostData = (CustomNPCBoostData)saveData;
            if (boostData.IsBoosted)
            {
                RemoveBoost();
                Debug.Log($"Boost detected - removed");
            }
            else
            {
                Debug.Log($"No boost detected");
            }
        }

        public void SetBoost()
        {
            boostData.IsBoosted = true;
        }

        public void RemoveBoost()
        {
            boostData.IsBoosted = false;
            Debug.Log($"Save loaded in empty house - correcting buff");
            int playerStealth = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
            int playerPickpocket = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Pickpocket);
            GameManager.Instance.PlayerEntity.Skills.SetPermanentSkillValue(DFCareer.Skills.Stealth, (short)(playerStealth - 80));
            GameManager.Instance.PlayerEntity.Skills.SetPermanentSkillValue(DFCareer.Skills.Pickpocket, (short)(playerPickpocket - 80));
        }

        public static CustomNPCBridge Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("CustomNPCBridge");
                    instance = go.AddComponent<CustomNPCBridge>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        public void RegisterCustomNPC(int npcId, CustomStaticNPCMod.CustomStaticNPC customNpc)
        {
            if (!customNPCs.ContainsKey(npcId))
            {
                customNPCs[npcId] = customNpc;
                Debug.Log($"Registered Custom NPC with ID: {npcId}");
            }
        }

        public bool IsCustomNPCRegistered(int npcId)
        {
            return customNPCs.ContainsKey(npcId);
        }

        public void UnregisterNPC(int npcId)
        {
            if (customNPCs.ContainsKey(npcId))
            {
                customNPCs.Remove(npcId);
                npcGreetingSections.Remove(npcId); // Remove the greeting section if the NPC is unregistered
                Debug.Log($"Unregistered NPC: {npcId}");
            }
            else
            {
                Debug.LogWarning($"Custom NPC with ID {npcId} is not registered.");
            }
        }

        public CustomStaticNPCMod.CustomStaticNPC GetCustomNPC(int npcId)
        {
            if (customNPCs.ContainsKey(npcId))
            {
                CustomStaticNPCMod.CustomStaticNPC npc = customNPCs[npcId];
                // Log state of NPC data when retrieved
                LogNPCDataState("When Retrieved", npc);
                if (npc == null)
                {
                    Debug.LogError($"Retrieved NPC is null. NPC ID: {npcId}");
                }
                else
                {
                    Debug.Log($"Retrieved NPC is not null. NPC ID: {npcId}");
                }
                return npc;
            }
            Debug.LogWarning($"Custom NPC with ID {npcId} not found.");
            return null;
        }

        public bool IsNPCDead(int npcHash)
        {
            return deadNPCs.Contains(npcHash);
        }

        public void MarkNPCAsDead(int npcHash)
        {
            deadNPCs.Add(npcHash);
            Debug.Log($"NPC with hash {npcHash} marked as dead.");
        }

        public int GetLivingNPCCountInInterior()
        {
            int count = 0;
            foreach (var npc in customNPCs.Values)
            {
                int npcHash = npc.GenerateHash(npc.CustomDisplayName, npc.GetCurrentHouseID());
                if (!IsNPCDead(npcHash))
                {
                    count++;
                }
            }
            return count;
        }

        public void MarkBuildingAsEmpty(int buildingKey)
        {
            emptyBuildings.Add(buildingKey);
            Debug.Log($"Building with key {buildingKey} marked as empty.");
        }

        public bool IsBuildingEmpty(int buildingKey)
        {
            return emptyBuildings.Contains(buildingKey);
        }

        public void HandleStaticNPCClick(int npcId)
        {
            if (customNPCs.TryGetValue(npcId, out CustomStaticNPCMod.CustomStaticNPC customNpc))
            {
                Debug.Log($"HandleStaticNPCClick invoked for NPC ID: {npcId}");
                Debug.Log($"NPC Data: nameSeed = {customNpc.Data.nameSeed}, factionID = {customNpc.Data.factionID}, nameBank = {customNpc.Data.nameBank}");
                Debug.Log($"Talk window opened for: {customNpc.CustomDisplayName}");

                // Set the target NPC in the CustomTalkManager
                bool sameTalkTargetAsBefore = false;
                CustomTalkManagerMod.CustomTalkManager.Instance.SetTargetCustomNPC(customNpc, ref sameTalkTargetAsBefore);

                // Open the custom talk window
                var talkWindow = new CustomDaggerfallTalkWindowMod.CustomDaggerfallTalkWindow(DaggerfallUI.UIManager, DaggerfallUI.UIManager.TopWindow as DaggerfallBaseWindow, CustomTalkManagerMod.CustomTalkManager.Instance);

                // Pass the isChildNPC value to the talk window
                talkWindow.isChildNPC = CustomTalkManagerMod.CustomTalkManager.Instance.IsChildNPC;

                DaggerfallUI.UIManager.PushWindow(talkWindow);
            }
        }

        public Dictionary<int, CustomStaticNPCMod.CustomStaticNPC> GetAllCustomNPCs()
        {
            return customNPCs;
        }

        public void AssignGreetingSection(int npcId, int section)
        {
            npcGreetingSections[npcId] = section;
            Debug.Log($"Assigned greeting section {section} to NPC ID {npcId}");
        }

        public int? GetGreetingSection(int npcId)
        {
            if (npcGreetingSections.TryGetValue(npcId, out int section))
            {
                Debug.Log($"Retrieved greeting section {section} for NPC ID {npcId}");
                return section;
            }
            Debug.LogWarning($"No greeting section found for NPC ID {npcId}");
            return null;
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

    [Serializable]
    public class CustomNPCBoostData
    {
        public bool IsBoosted { get; set; }
    }
}