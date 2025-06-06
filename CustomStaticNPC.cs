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
using DaggerfallWorkshop.Game.Serialization;
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
using FactionNPCInitializerMod;
using FactionParserMod;
using CustomDaggerfallTalkWindowMod;
using CustomNPCClickHandlerMod;
using CustomTalkManagerMod;

namespace CustomStaticNPCMod
{
    public class CustomStaticNPC : MonoBehaviour
    {
        public static bool AnySpecialBillboardsPresent = false;
        private StaticNPC.NPCData npcData;
        private int npcId;
        private string customLastName;
        private string customFirstName;
        private string customDisplayName; // Custom field for the display name
        public int OriginalBillboardArchiveIndex { get; private set; }
        public int OriginalBillboardRecordIndex { get; private set; }
        private bool isProcessed = false;
        public static bool aidanFired = false;
        public static bool guardsCalled = false;
        //var customNpc = CustomTalkManagerMod.CustomTalkManager.Instance.GetTargetCustomNPC();
        //int billboardArchiveIndex = customNpc.OriginalBillboardArchiveIndex;
       //int billboardRecordIndex = customNpc.OriginalBillboardRecordIndex;
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<CustomStaticNPC>();

            mod.IsReady = true;
        }


        public Races DefaultRace { get; set; } = Races.Breton;


        public Races Race
        {
            get { return npcData.race; }
            set { npcData.race = value; }
        }


        public Genders Gender
        {
            get { return npcData.gender; }
            set { npcData.gender = value; }
        }

        void Start()
        {
            PlayerEnterExit.OnTransitionExterior += OnTransitionToExterior;
            StartCoroutine(DelayedNPCCheck());
            int livingNPCCount = CustomNPCBridgeMod.CustomNPCBridge.Instance.GetLivingNPCCountInInterior();
            int playerStealth = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
            int playerPickpocket = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Pickpocket);
            string npcDisplayName = CustomDisplayName;
            int houseID = GetCurrentHouseID(); // Implement this method to get the current house ID
            npcTopicHash = GenerateHash(npcDisplayName, houseID);
            CustomNPCBridgeMod.CustomNPCBridge.Instance.RegisterCustomNPC(GetInstanceID(), this);
            // Log the current and last NPC display names
            Debug.Log($"Hashbrowns: {npcDisplayName}");

            // Check if the NPC display name is different from the last one
            if (npcDisplayName != lastNpcDisplayName)
            {
                Debug.Log("NPC display name has changed, resetting necessary states.");
            }

            int buildingKey = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData.buildingKey;
            int archive = OriginalBillboardArchiveIndex;
            int record = OriginalBillboardRecordIndex;
            string npcName = this.CustomDisplayName;
            Billboard[] billboards = GetComponentsInChildren<Billboard>();

            if (CustomNPCBridge.Instance.IsNpcDead(buildingKey, archive, record))
            {
                Debug.Log("NPC marked dead A");
                MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer != null) meshRenderer.enabled = false;
                BoxCollider boxCollider = GetComponent<BoxCollider>();
                if (boxCollider != null) boxCollider.enabled = false;
                CapsuleCollider collider = GetComponent<CapsuleCollider>();
                if (collider != null) collider.enabled = false;
            }
            else
            {
                // Register the NPC with CustomNPCBridge
                CustomNPCBridgeMod.CustomNPCBridge.Instance.RegisterCustomNPC(GetInstanceID(), this);
            }
            // Update the last NPC display name
            lastNpcDisplayName = npcDisplayName;
        }

        public static void UpdateSpecialBillboardsFlag()
        {
            AnySpecialBillboardsPresent = false;
            // Find all DaggerfallBillboards in the scene
            var allBillboards = GameObject.FindObjectsOfType<DaggerfallBillboard>();
            foreach (var billboard in allBillboards)
            {
                int archive = billboard.Summary.Archive;
                if (archive == 1300 || archive == 1301 || archive == 1302 || archive == 1305)
                {
                    AnySpecialBillboardsPresent = true;
                    break;
                }
            }
        }

        private IEnumerator DelayedNPCCheck()
        {

            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            CustomStaticNPC.UpdateSpecialBillboardsFlag();

            int livingNPCCount = CustomNPCBridgeMod.CustomNPCBridge.Instance.GetLivingNPCCountInInterior();

            int playerStealth = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
            int playerPickpocket = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Pickpocket);
            string npcDisplayName = CustomDisplayName;
            int houseID = GetCurrentHouseID(); // Implement this method to get the current house ID
            int archive = OriginalBillboardArchiveIndex;
            int record = OriginalBillboardRecordIndex;
            npcTopicHash = GenerateHash(npcDisplayName, houseID);

       
            Debug.Log($"Hashbrowns: {npcDisplayName}");


            if (npcDisplayName != lastNpcDisplayName)
            {
              
                Debug.Log("NPC display name has changed, resetting necessary states.");
            }

            // Update the last NPC display name
            lastNpcDisplayName = npcDisplayName;

            int buildingKey = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData.buildingKey;

            // Check if the NPC is marked as dead
            string npcName = CustomDisplayName; 

            if (CustomNPCBridgeMod.CustomNPCBridge.Instance.IsNpcDead(buildingKey, archive, record))
            {
            
                MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }

           
                BoxCollider boxCollider = GetComponent<BoxCollider>();
                if (boxCollider != null)
                {
                    boxCollider.enabled = false;
                }
            }
            else
            {
                
                CustomNPCBridgeMod.CustomNPCBridge.Instance.RegisterCustomNPC(GetInstanceID(), this);
            }
      
            if (livingNPCCount <= 0 && !CustomStaticNPC.AnySpecialBillboardsPresent && !CustomStaticNPC.aidanFired)
            {
                NothingHereAidan();
            }
        }

        private int npcTopicHash;
        private string lastNpcDisplayName;

        public int GenerateHash(string npcDisplayName, int houseID)
        {
            string combinedString = npcDisplayName + houseID.ToString();
            int hash = combinedString.GetHashCode();
            return Math.Abs(hash); // Ensure the hash is positive
        }

        public int GetCurrentHouseID()
        {
            return GameManager.Instance.PlayerGPS.CurrentLocation.MapTableData.MapId;
        }

        public StaticNPC.NPCData Data
        {
            get { return npcData; }
        }

        public string CustomDisplayName
        {
            get => customDisplayName;
            set
            {
                customDisplayName = value;
   
                if (npcData.nameSeed != 0)
                {
                    SetCustomDisplayName(customDisplayName);
                }
            }
        }

        public static void NothingHereAidan()
        {
            Debug.Log("Aidan entered");
            if (CustomNPCBridgeMod.CustomNPCBridge.Instance.boostData.IsBoosted)
            {
                Debug.Log("Pickpocket buff already applied, skipping.");
                return;
            }
            CustomNPCBridgeMod.CustomNPCBridge.Instance.ApplyPickpocketBuff();
            Debug.Log("Nothing here Aidan");
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

        private void OnTransitionToExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            aidanFired = false;
            guardsCalled = false;
            int playerStealth = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
            int playerPickpocket = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Pickpocket);

            if (CustomNPCBridgeMod.CustomNPCBridge.Instance.boostData.IsBoosted)
            {
                GameManager.Instance.PlayerEntity.Skills.SetPermanentSkillValue(DFCareer.Skills.Pickpocket, (short)(playerPickpocket - 100));
                CustomNPCBridgeMod.CustomNPCBridge.Instance.boostData.IsBoosted = false;
                Debug.Log($"Chungus");
            }
            if (halt)
            {
                Vector3 fallbackPosition = GameManager.Instance.PlayerEntityBehaviour.transform.position;
                GameManager.Instance.PlayerEntity.SpawnCityGuard(fallbackPosition, Vector3.forward);
                GameManager.Instance.PlayerEntity.SpawnCityGuards(true);
            }
        }

        public void OnHitByWeapon()
        {
            int buildingKey = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData.buildingKey;
            string npcName = this.CustomDisplayName; 
            Debug.Log($"Custom NPC {customDisplayName} (ID: {npcId}) was hit by a weapon!");
            int livingNPCCount = CustomNPCBridgeMod.CustomNPCBridge.Instance.GetLivingNPCCountInInterior();
            int playerStealth = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
            int playerPickpocket = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Pickpocket);

            int archive = OriginalBillboardArchiveIndex;
            int record = OriginalBillboardRecordIndex;
            CustomNPCBridgeMod.CustomNPCBridge.Instance.MarkNpcAsDead(buildingKey, archive, record);


            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            // Disable the BoxCollider component
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }
            CapsuleCollider collider = GetComponent<CapsuleCollider>();
            if (collider != null) collider.enabled = false;

            PlayBloodEffect(transform.position);
            CustomNPCBridge.Instance.MarkNpcAsDead(buildingKey, archive, record);

            // Determine whether to call guards
            if (ShouldCallGuards())
            {
                Debug.Log("Guards are being called to the scene!");
                DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.Instance.UserInterfaceManager, DaggerfallUI.Instance.UserInterfaceManager.TopWindow);
                messageBox.SetText("You've been caught!");
                messageBox.ClickAnywhereToClose = true;
                messageBox.Show();
                GameManager.Instance.PlayerEntity.SpawnCityGuards(true); // 
                GameManager.Instance.PlayerEntity.CrimeCommitted = PlayerEntity.Crimes.Murder;
                GameManager.Instance.PlayerEntity.CrimeCommitted = PlayerEntity.Crimes.Assault;

               
                Vector3 entrancePosition = GameManager.Instance.PlayerEnterExit.Interior.transform.position;
                Vector3 forwardDirection = GameManager.Instance.PlayerEnterExit.Interior.transform.forward;

                
                GameManager.Instance.PlayerEntity.SpawnCityGuard(entrancePosition, forwardDirection);
                RegisterBuildingAsEmpty();
                halt = true;
                return;
            }

            if (livingNPCCount <= 1)
            {
               NothingHereAidan();
            }
        }

        private bool halt = false;

        private void RegisterBuildingAsEmpty()
        {
            int buildingKey = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData.buildingKey;
            CustomNPCBridgeMod.CustomNPCBridge.Instance.MarkBuildingAsEmpty(buildingKey);
        }

        public static bool ShouldCallGuards()
        {
            if (guardsCalled) return false; 

            int numNPCs = CustomNPCBridgeMod.CustomNPCBridge.Instance.GetLivingNPCCountInInterior();
            int playerStealth = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
            float randomFactor = UnityEngine.Random.Range(-0.5f, 0.5f); // Small randomness
            float probability = 1f / (1f + Mathf.Exp(-(0.5f * numNPCs - 2 + randomFactor) + (playerStealth - 50) / 25f));
            Debug.Log($"Guard probability rolled: {probability}");
            bool shouldCall = UnityEngine.Random.value < probability;
            if (shouldCall) guardsCalled = true; 
            return shouldCall;
        }


        private void PlayBloodEffect(Vector3 position)
        {
            const int bloodArchive = 380;
            const int bloodIndex = 0; 
            const float yOffset = .4f; 

            
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
            if (dfUnity)
            {
                GameObject go = GameObjectHelper.CreateDaggerfallBillboardGameObject(bloodArchive, bloodIndex, null);
                go.name = "BloodSplash";
                Billboard c = go.GetComponent<Billboard>();
                go.transform.position = position + new Vector3(0, yOffset, 0) + transform.forward * 0.02f;
                c.OneShot = true;
                c.FramesPerSecond = 10;
            }
        }

        public NameHelper.BankTypes NameBank
        {
            get => npcData.nameBank;
            set
            {
                npcData.nameBank = value;
            }
        }

        public bool IsChildNPC => IsChildNPCData(Data);

        public void InitializeNPCData(StaticNPC.NPCData data)
        {
            npcData = data;

            // Store the original billboard archive and record index
            OriginalBillboardArchiveIndex = npcData.billboardArchiveIndex;
            OriginalBillboardRecordIndex = npcData.billboardRecordIndex;

            Debug.Log($"Stored original billboard data: Archive={OriginalBillboardArchiveIndex}, Record={OriginalBillboardRecordIndex}");

            npcData.race = DefaultRace; // Set the default race to Breton or other defaults
        }

        public void StoreOriginalBillboardData(int archiveIndex, int recordIndex)
        {
            // Avoid overwriting if already set
            if (OriginalBillboardArchiveIndex == 0 && OriginalBillboardRecordIndex == 0)
            {
                OriginalBillboardArchiveIndex = archiveIndex;
                OriginalBillboardRecordIndex = recordIndex;

                Debug.Log($"Stored original billboard data: Archive={OriginalBillboardArchiveIndex}, Record={OriginalBillboardRecordIndex}");
            }
        }

        private Genders DetermineGender(int archiveIndex, int recordIndex)
        {
            if (archiveIndex == 180)
            {
                // Explicit cases for male
                if (recordIndex == 2 || recordIndex == 3)
                    return Genders.Male;

                else
                    return Genders.Female;
            }

            if (archiveIndex == 182)
            {
                // Explicit cases for female
                if (recordIndex == 4 || recordIndex == 15 || recordIndex == 16 || recordIndex == 17 || recordIndex == 18 || recordIndex == 19 || recordIndex == 20 || recordIndex == 21 || recordIndex == 22 || recordIndex == 23 || recordIndex == 24 || recordIndex == 25 || recordIndex == 29 || recordIndex == 35 || recordIndex == 36 || recordIndex == 39 || recordIndex == 42 || recordIndex == 43 || recordIndex == 46)
                    return Genders.Male;

                else
                    return Genders.Female;
            }

            if (archiveIndex == 183)
            {

                if (recordIndex == 0 || recordIndex == 2 || recordIndex == 3 || recordIndex == 4 || recordIndex == 5 || recordIndex == 6 || recordIndex == 7 || recordIndex == 10 || recordIndex == 12 || recordIndex == 13 || recordIndex == 16 || recordIndex == 19 || recordIndex == 20)
                    return Genders.Male;

                else
                    return Genders.Female;
            }

            // Handle archive 184 specifically
            if (archiveIndex == 184)
            {
                // Explicit cases for male
                if (recordIndex == 0 || recordIndex == 2 || recordIndex == 3 || recordIndex == 4 || recordIndex == 16 || recordIndex == 17 || recordIndex == 20 || recordIndex == 21 || recordIndex == 24 || recordIndex == 25 || recordIndex == 31 || recordIndex == 34)
                    return Genders.Male;

                else
                    return Genders.Female;
            }

            // Handle archive 334 specifically
            if (archiveIndex == 334)
            {
                if (recordIndex == 0 || recordIndex == 1 || recordIndex == 2 || recordIndex == 3 || recordIndex == 7 || recordIndex == 9 || recordIndex == 11 || recordIndex == 13 || recordIndex == 14 || recordIndex == 15 || recordIndex == 16 || recordIndex == 17 || recordIndex == 18 || recordIndex == 19 || recordIndex == 20)
                    return Genders.Male;

                else
                   return Genders.Female;
            }

            if (archiveIndex == 357)
            {
                // Explicit cases for male
                if (recordIndex == 1 || recordIndex == 3 || recordIndex == 4 || recordIndex == 5 || recordIndex == 7 || recordIndex == 12 || recordIndex == 13 || recordIndex == 14)
                    return Genders.Male;

                else
                    return Genders.Female;
            }

            // Default case for other archives
            Debug.LogWarning($"Unhandled archiveIndex: {archiveIndex}, recordIndex: {recordIndex}. Defaulting to Male.");
            return Genders.Male;
        }

        // Update the Initialize method to use DetermineGender

        public void Initialize(int newNpcId, StaticNPC.NPCData originalNpcData, string familyLastName)
        {
            npcData = originalNpcData;

            // Check if the region is Hammerfell
            int regionIndex = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
            if (FamilyNameModifierMod.FamilyNameModifier.Instance.IsHammerfellRegion(regionIndex))
            {
                // Determine if the NPC is a child
                bool isChildNPC =
                    (npcData.billboardArchiveIndex == 182 &&
                        (npcData.billboardRecordIndex == 4 || npcData.billboardRecordIndex == 38 || npcData.billboardRecordIndex == 42 ||
                         npcData.billboardRecordIndex == 43 || npcData.billboardRecordIndex == 52 || npcData.billboardRecordIndex == 53)) ||
                    (npcData.billboardArchiveIndex == 334 &&
                        (npcData.billboardRecordIndex == 2 || npcData.billboardRecordIndex == 9 || npcData.billboardRecordIndex == 12));

                if (isChildNPC)
                {
                    // Children in Hammerfell have an empty last name
                    familyLastName = string.Empty;
                }
                else
                {
                   
                    string redguardFirstName = DaggerfallUnity.Instance.NameHelper.FirstName(NameHelper.BankTypes.Redguard, Genders.Male);
                    familyLastName = $"son of {redguardFirstName}";
                }
            }

            // Assign last name
            string firstName = DaggerfallUnity.Instance.NameHelper.FirstName(npcData.nameBank, npcData.gender);
            customDisplayName = $"{firstName} {familyLastName}";

            npcData.gender = DetermineGender(npcData.billboardArchiveIndex, npcData.billboardRecordIndex);
            Debug.Log($"Gender determined for NPC ID {newNpcId}: {npcData.gender}");

            // Generate and set display name using gender and family last name
            SetCustomDisplayName(GenerateName(npcData.nameBank, npcData.gender, familyLastName));

            // Ensure NPC has all required components
            EnsureComponents(originalNpcData);
        }

        private string GenerateName(NameHelper.BankTypes nameBank, Genders gender, string lastName)
        {
            // Generate unique names for Hammerfell NPCs
            int regionIndex = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
            if (FamilyNameModifierMod.FamilyNameModifier.Instance.IsHammerfellRegion(regionIndex))
            {
                // Determine if the NPC is a child
                bool isChildNPC =
                    (npcData.billboardArchiveIndex == 182 &&
                        (npcData.billboardRecordIndex == 4 || npcData.billboardRecordIndex == 38 || npcData.billboardRecordIndex == 42 ||
                         npcData.billboardRecordIndex == 43 || npcData.billboardRecordIndex == 52 || npcData.billboardRecordIndex == 53)) ||
                    (npcData.billboardArchiveIndex == 334 &&
                        (npcData.billboardRecordIndex == 2 || npcData.billboardRecordIndex == 9 || npcData.billboardRecordIndex == 12));

                if (isChildNPC)
                {
                    // Children have no last name
                    return DaggerfallUnity.Instance.NameHelper.FirstName(nameBank, gender);
                }

                string firstName = DaggerfallUnity.Instance.NameHelper.FirstName(nameBank, gender);
                return $"{firstName} {lastName}";
            }

            // Default logic for other regions
            return $"{DaggerfallUnity.Instance.NameHelper.FirstName(nameBank, gender)} {lastName}";
        }

        // Method to set the custom display name
        public void SetCustomDisplayName(string newName)
        {
            customDisplayName = newName; // Set the display name here
            Debug.Log($"Custom DisplayName set to '{newName}' for NPC.");
        }

        // Method to get the custom display name
        public string GetCustomDisplayName()
        {
            return customDisplayName;
        }

        private void EnsureComponents(StaticNPC.NPCData originalNpcData)
        {
            // Add or ensure a MeshFilter component
            if (GetComponent<MeshFilter>() == null)
            {
                gameObject.AddComponent<MeshFilter>();
                Debug.Log("Added missing MeshFilter component.");
            }

            // Add or ensure a MeshRenderer component
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
                Debug.Log("Added missing MeshRenderer component.");
            }
            meshRenderer.enabled = true;

            // Add or ensure a DaggerfallBillboard component
            var billboard = GetComponent<DaggerfallBillboard>();
            if (billboard == null)
            {
                billboard = gameObject.AddComponent<DaggerfallBillboard>();
                billboard.SetMaterial(originalNpcData.billboardArchiveIndex, originalNpcData.billboardRecordIndex);
                billboard.AlignToBase();
                Debug.Log("Added missing DaggerfallBillboard component.");
            }
            billboard.enabled = true;

            // Add or ensure a BoxCollider component
            var boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider>();
                Debug.Log("Added missing BoxCollider component.");
            }
            boxCollider.enabled = true;
        }

        public void SetLayoutData(int hash, Genders gender, int factionID, int nameSeed)
        {
            npcData.hash = hash;
            npcData.flags = (gender == Genders.Male) ? 0 : 32;
            npcData.factionID = factionID;
            npcData.nameSeed = (nameSeed == -1) ? npcData.hash : nameSeed;
            npcData.gender = gender;
            npcData.race = StaticNPC.GetRaceFromFaction(factionID);
            npcData.context = StaticNPC.Context.Custom;
        }

        public void SetLayoutData(DFBlock.RdbObject obj)
        {
            PlayerGPS playerGPS = GameManager.Instance.PlayerGPS;
            StaticNPC.SetLayoutData(ref npcData,
                obj.XPos, obj.YPos, obj.ZPos,
                obj.Resources.FlatResource.Flags,
                obj.Resources.FlatResource.FactionOrMobileId,
                obj.Resources.FlatResource.TextureArchive,
                obj.Resources.FlatResource.TextureRecord,
                obj.Resources.FlatResource.Position,
                playerGPS.CurrentMapID,
                playerGPS.CurrentLocation.LocationIndex,
                0);
            npcData.context = StaticNPC.Context.Dungeon;
        }

        public void SetLayoutData(DFBlock.RmbBlockPeopleRecord obj, int buildingKey = 0)
        {
            PlayerGPS playerGPS = GameManager.Instance.PlayerGPS;
            StaticNPC.SetLayoutData(ref npcData,
                obj.XPos, obj.YPos, obj.ZPos,
                obj.Flags,
                obj.FactionID,
                obj.TextureArchive,
                obj.TextureRecord,
                obj.Position,
                playerGPS.CurrentMapID,
                playerGPS.CurrentLocation.LocationIndex,
                buildingKey);
            npcData.context = StaticNPC.Context.Building;
        }

        public static void SetLayoutData(ref StaticNPC.NPCData data, int XPos, int YPos, int ZPos, int flags, int factionId, int archive, int record, long position, int mapId, int locationIndex, int buildingKey)
        {
            // Store common layout data
            data.hash = StaticNPC.GetPositionHash(XPos, YPos, ZPos);
            data.flags = flags;
            data.factionID = factionId;
            data.billboardArchiveIndex = archive;
            data.billboardRecordIndex = record;
            data.nameSeed = (int)position ^ buildingKey + locationIndex;

            // Determine gender based on flags or billboard texture
            if (archive == 182 && record == 45)
            {
                data.gender = Genders.Female;
            }
            else if (archive == 334 && record >= 16)
            {
                data.gender = Genders.Male; // Example for male textures
            }
            else
            {
                data.gender = (flags & 32) == 32 ? Genders.Female : Genders.Male;
            }

            data.race = StaticNPC.GetRaceFromFaction(factionId);
            data.buildingKey = buildingKey;
            data.mapID = mapId;
        }

        public void SetCustomLastName(string lastName)
        {
            customLastName = lastName;

            if (npcData.nameSeed == 0)
            {
                Debug.LogError("NPC data is not initialized when setting custom last name.");
                return;
            }

            string originalFirstName = GetDisplayName().Split(' ')[0];
            customFirstName = originalFirstName;
            string modifiedName = originalFirstName + " " + customLastName;
            CustomDisplayName = modifiedName;
            Debug.Log($"SetCustomLastName: Custom last name set to {customLastName} for NPC ID: {npcId}");
        }



        private string GetDisplayName()
        {
            FactionFile.FactionData factionData;
            bool foundFaction = GameManager.Instance.PlayerEntity.FactionData.GetFactionData(npcData.factionID, out factionData);
            if (foundFaction && factionData.type == (int)FactionFile.FactionTypes.Individual)
            {
                return factionData.name;
            }
            else
            {
                DFRandom.srand(npcData.nameSeed);
                return DaggerfallUnity.Instance.NameHelper.FullName(npcData.nameBank, npcData.gender);
            }
        }

        private string GenerateName(NameHelper.BankTypes nameBank, Genders gender)
        {
            // Validate gender
            if (gender != Genders.Male && gender != Genders.Female)
            {
                Debug.LogError("Invalid gender for NPC. Defaulting to Male.");
                gender = Genders.Male;
            }

            // Generate full name
            return DaggerfallUnity.Instance.NameHelper.FullName(nameBank, gender);
        }



        public static bool IsChildNPCData(StaticNPC.NPCData data)
        {
            const int childrenFactionID = 514;

            bool isChildNPCTexture = DaggerfallWorkshop.Utility.TextureReader.IsChildNPCTexture(data.billboardArchiveIndex, data.billboardRecordIndex);
            bool isChildrenFaction = data.factionID == childrenFactionID;

            return isChildNPCTexture || isChildrenFaction;
        }

        private void LogNPCDataState(string context)
        {
            if (npcData.hash != 0)
            {
                Debug.Log($"{context}: NPC ID {npcId} has valid data.");
            }
            else
            {
                Debug.LogError($"{context}: NPC ID {npcId} has invalid data.");
            }
        }
    }
}