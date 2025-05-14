using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using DaggerfallWorkshop.Game.Formulas;
using Wenzil.Console;
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
using UnityEngine.SceneManagement; // Add this line
using System.Reflection;
using CustomStaticNPCMod;
using CustomNPCBridgeMod;
using FactionNPCInitializerMod;
using FactionParserMod;
using CustomNPCClickHandlerMod;
using FamilyNameModifierMod;
using CustomDaggerfallTalkWindowMod;

namespace DaggerfallWorkshop.Game.Questing
{
    /// <summary>
    /// Represents the resources associated with a quest.
    /// </summary>
    ///

    public class QuestResources
    {
        public Dictionary<string, QuestResourceInfo> resourceInfo;

        public QuestResources()
        {
            resourceInfo = new Dictionary<string, QuestResourceInfo>();
        }
    }
}
namespace DaggerfallWorkshop.Game.Questing
{
    public enum QuestInfoResourceType
    {
        NotSet,
        Location,
        Person,
        Thing
    }

    public class QuestResourceInfo
    {
        public QuestInfoResourceType resourceType;
        public QuestResource questResource;
        public bool availableForDialog;
        public bool hasEntryInTellMeAbout;

        public QuestResourceInfo()
        {
            resourceType = QuestInfoResourceType.NotSet;
            questResource = null;
            availableForDialog = true;
            hasEntryInTellMeAbout = false;
        }
    }
}

namespace CustomTalkManagerMod
{

    public class CustomTalkManager : MonoBehaviour
    {
        public enum CustomQuestionType
        {
            NoQuestion,
            News,
            WhereAmI,
            OrganizationInfo,
            Work,
            LocalBuilding,
            Regional,
            Person,
            Thing,
            QuestLocation,
            QuestPerson,
            QuestItem,
            RegionalBuilding // Custom enum value
        }

        private Dictionary<ulong, QuestResources> dictQuestInfo = new Dictionary<ulong, QuestResources>();
        private int[] infoFactionIDs;
        public class TalkManagerProxy
        {
            public static List<TalkManager.RumorMillEntry> GetValidRumors(TalkManager talkManager, bool readingSign = false)
            {
                return (List<TalkManager.RumorMillEntry>)typeof(TalkManager)
                    .GetMethod("GetValidRumors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(talkManager, new object[] { readingSign });
            }

            public static string ExpandRandomTextRecord(TalkManager talkManager, int recordIndex)
            {
                return (string)typeof(TalkManager)
                    .GetMethod("ExpandRandomTextRecord", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(talkManager, new object[] { recordIndex });
            }

            public static TalkManager.RumorMillEntry WeightedRandomRumor(TalkManager talkManager, List<TalkManager.RumorMillEntry> validRumors)
            {
                return (TalkManager.RumorMillEntry)typeof(TalkManager)
                    .GetMethod("WeightedRandomRumor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(talkManager, new object[] { validRumors });
            }
        }
        private StaticNPC targetStaticNPC;
        private TalkManager.NPCType currentNPCType;
        private string nameNPC;
        private Dictionary<TalkManager.ListItem, (string PlayerQuestion, string NpcResponse)> listItemData = new Dictionary<TalkManager.ListItem, (string PlayerQuestion, string NpcResponse)>();
        private CustomNPCData npcData;
        private TalkManager.ListItem currentQuestionListItem;
        private static Mod mod;
        private static CustomTalkManager instance;
        private CustomStaticNPC lastTargetCustomNPC; // Use the correct type here
        private Dictionary<int, List<string>> customTokens = new Dictionary<int, List<string>>();

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            if (instance != null)
            {
                Debug.LogWarning("CustomTalkManager instance already exists.");
                return;
            }

            var go = new GameObject(mod.Title);
            instance = go.AddComponent<CustomTalkManager>();

            instance.InitializeData();

            DontDestroyOnLoad(go);
            mod.IsReady = true;
        }

        public bool IsChildNPC { get; private set; }

        public CustomTalkManager()
        {
            // Manually define the specific faction IDs you want to include
            infoFactionIDs = new int[] { 40, 41, 42, 108, 129, 306, 353, 67, 82, 84, 88, 92, 94, 106, 36, 83, 85, 89, 93, 95, 98, 99, 107, 37, 368, 408, 409, 410, 411, 413, 414, 415, 416, 98 };
            listItemData = new Dictionary<TalkManager.ListItem, (string PlayerQuestion, string NpcResponse)>();
        }

        public class CustomNPCData : TalkManager.NPCData
        {
            public Genders gender { get; set; }
        }

        public static CustomTalkManager Instance
        {
            get
            {
                if (instance == null)
                {
                    Debug.Log("Creating new CustomTalkManager instance.");
                    var go = new GameObject("CustomTalkManager");
                    instance = go.AddComponent<CustomTalkManager>();
                    DontDestroyOnLoad(go);
                }
                else
                {
                    Debug.Log("Using existing CustomTalkManager instance.");
                }
                return instance;
            }
        }

        void Awake()
        {
            if (instance != null && instance != this)
                Destroy(this);
            else
                instance = this;
        }

        public MacroDataSource GetMacroDataSource()
        {
            // Ensure npcData and currentQuestionListItem are initialized
            if (npcData == null)
            {
                npcData = new CustomNPCData(); // Ensure npcData is initialized
                npcData.race = Races.Breton; // or any default race
                npcData.gender = Genders.Male; // or any default gender
            }

            if (currentQuestionListItem == null)
            {
                currentQuestionListItem = new TalkManager.ListItem();
            }

            // Ensure TalkManagerContext is properly initialized
            TalkManagerContext context = new TalkManagerContext
            {
                currentQuestionListItem = currentQuestionListItem,
                npcRace = npcData.race,
                potentialQuestorGender = npcData.gender // Assign a default gender
            };

            if (currentQuestionListItem != null && currentQuestionListItem.questionType == TalkManager.QuestionType.Work && TalkManager.Instance.HasNPCsWithWork)
            {
                context.potentialQuestorGender = TalkManager.Instance.GetQuestorGender();
            }
            return new TalkManagerDataSource(context);
        }

        public CustomStaticNPC GetTargetCustomNPC()
        {
            if (lastTargetCustomNPC == null)
            {
                Debug.LogWarning("Last target custom NPC is null.");
            }
            return lastTargetCustomNPC;
        }

        public void SetTargetCustomNPC(CustomStaticNPC targetCustomNPC, ref bool sameTalkTargetAsBefore)
        {
            if (lastTargetCustomNPC != null && lastTargetCustomNPC == targetCustomNPC)
            {
                sameTalkTargetAsBefore = true;
            }
            else
            {
                sameTalkTargetAsBefore = false;
                lastTargetCustomNPC = targetCustomNPC ?? throw new ArgumentNullException(nameof(targetCustomNPC), "Target Custom NPC cannot be null.");

                // Initialize currentQuestionListItem here or elsewhere as appropriate
                if (currentQuestionListItem == null)
                {
                    currentQuestionListItem = new TalkManager.ListItem();
                }

                if (npcData == null)
                {
                    npcData = new CustomNPCData(); // Ensure npcData is initialized
                }

                // Ensure gender is set
                npcData.gender = targetCustomNPC.Gender; // Add this line to set the gender
            }
            int recordIndex = targetCustomNPC.Data.billboardRecordIndex;
            IsChildNPC = (recordIndex == 4 || recordIndex == 38 || recordIndex == 42 || recordIndex == 43 || recordIndex == 52 || recordIndex == 53);
        }

        public FactionFile.FactionData GetNPCData(int factionID)
        {
            FactionFile.FactionData factionData;
            GameManager.Instance.PlayerEntity.FactionData.GetFactionData(factionID, out factionData);
            return factionData;
        }

        public int GetToneIndex(DaggerfallTalkWindow.TalkTone talkTone)
        {
            return DaggerfallTalkWindow.TalkToneToIndex(talkTone);
        }

        private void InitializeData()
        {
            if (npcData == null)
            {
                npcData = new CustomNPCData();
            }

            if (currentQuestionListItem == null)
            {
                currentQuestionListItem = new TalkManager.ListItem();
            }
        }

        // Ensure that the CustomTalkManager is used in StartConversation and not the vanilla TalkManager
        public void StartConversation(CustomStaticNPC customNpc)
        {
            if (customNpc == null)
            {
                Debug.LogWarning("StartConversation: Custom NPC is null in StartConversation");
                return;
            }

            Debug.Log($"StartConversation: Starting conversation with custom NPC ID: {customNpc.GetInstanceID()}");

            bool sameTalkTargetAsBefore = false;
            CustomTalkManager.Instance.SetTargetCustomNPC(customNpc, ref sameTalkTargetAsBefore);

            // Load the custom CSV file
            CustomTalkManager.Instance.LoadCustomCSV("GreetingsAndSalutations.csv");
            Debug.Log("Parsing Greetings File");

            var talkWindow = new CustomDaggerfallTalkWindowMod.CustomDaggerfallTalkWindow(DaggerfallUI.UIManager, DaggerfallUI.UIManager.TopWindow as DaggerfallBaseWindow, CustomTalkManagerMod.CustomTalkManager.Instance);

            // Set the macro data source for the talk window
            var macroDataSource = CustomTalkManager.Instance.GetMacroDataSource();
            Debug.Log($"StartConversation: MacroDataSource - NPC Race: {(macroDataSource as CustomTalkManager.TalkManagerDataSource).NpcRace}, Gender: {(macroDataSource as CustomTalkManager.TalkManagerDataSource).PotentialQuestorGender}");
            talkWindow.SetMacroDataSource(macroDataSource);

            npcData = new CustomNPCData();

            // Open the custom talk window
            DaggerfallUI.UIManager.PushWindow(talkWindow);
            Debug.Log("Opening CustomDaggerfallTalkWindow");
        }

        public string GetGreeting(int npcId)
        {
            Debug.Log("GetGreeting called.");
            if (customTokens == null || customTokens.Count == 0)
            {
                Debug.LogWarning("Custom tokens have not been loaded.");
                return "Hello, stranger.";
            }

            // Retrieve or assign a greeting section for the NPC
            var bridge = CustomNPCBridgeMod.CustomNPCBridge.Instance;
            int? assignedSection = bridge.GetGreetingSection(npcId);

            if (!assignedSection.HasValue)
            {
                // Generate a unique hash for the NPC
                int hash = GenerateUniqueHash(lastTargetCustomNPC.CustomDisplayName);

                // Convert the hash to an index
                int sectionKey = (Math.Abs(hash) % customTokens.Count) + 1; // Ensure the sectionKey is positive and not zero
                bridge.AssignGreetingSection(npcId, sectionKey);
                assignedSection = sectionKey;
                Debug.Log($"Assigned new greeting section {sectionKey} to NPC ID {npcId}");
            }

            // Get the dialog lines for the assigned section
            var dialogLines = customTokens[assignedSection.Value];
            var dialogRandom = new System.Random();
            int dialogIndex = dialogRandom.Next(dialogLines.Count);

            Debug.Log($"Selected greeting: {dialogLines[dialogIndex]}");
            return dialogLines[dialogIndex];
        }

        private int GenerateUniqueHash(string displayName)
        {
            var gps = GameManager.Instance.PlayerGPS;
            int worldX = gps.WorldX / 500; // Reduce precision by dividing 
            int worldZ = gps.WorldZ / 500; // Reduce precision by dividing 
            int buildingId = gps.CurrentLocationIndex;

            Debug.Log($"Generating hash for NPC ID: {displayName}");
            Debug.Log($"Normalized WorldX: {worldX}, Normalized WorldZ: {worldZ}, BuildingID: {buildingId}, NPCID: {displayName}");

            // Combine the normalized GPS coordinates, building ID, and NPC ID to generate a unique hash
            unchecked // Allow overflow
            {
                int hash = 17;
                hash = hash * 31 + worldX;
                hash = hash * 31 + worldZ;
                hash = hash * 31 + buildingId;
                foreach (char c in displayName)
                {
                    if (char.IsLetter(c))
                    {
                        hash = hash * 31 + c;
                    }
                }
                Debug.Log($"Generated hash: {hash}");
                return hash;
            }
        }


        private void LoadCustomCSV(string csvFileName)
        {
            customTokens.Clear(); // Ensure the dictionary is cleared before loading new content

            // Use ModManager to load the CSV file
            TextAsset csvAsset;
            Debug.Log("Attempting to load CSV file using ModManager...");
            if (!ModManager.Instance.TryGetAsset(csvFileName, true, out csvAsset))
            {
                Debug.LogError($"CSV file not found: {csvFileName}");
                return;
            }

            if (csvAsset == null)
            {
                Debug.LogError($"CSV asset is null: {csvFileName}");
                return;
            }

            Debug.Log("CSV file successfully loaded. Beginning to parse CSV content...");
            string[] lines = csvAsset.text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Debug.Log($"Total lines in CSV: {lines.Length}");
            foreach (string line in lines)
            {
                Debug.Log($"Processing line: {line}");
                string[] parts = line.Split(new[] { ',' }, 2);
                if (parts.Length != 2)
                {
                    Debug.LogWarning($"Invalid line in CSV: {line}");
                    continue;
                }

                if (!int.TryParse(parts[0], out int section) || section == 0)
                {
                    Debug.LogWarning($"Invalid section number in CSV: {parts[0]}");
                    continue;
                }

                string[] dialogLines = parts[1].Split(new[] { "[/record]" }, StringSplitOptions.RemoveEmptyEntries);
                Debug.Log($"Section {section} has {dialogLines.Length} dialog lines.");
                if (!customTokens.ContainsKey(section))
                {
                    customTokens[section] = new List<string>();
                }

                customTokens[section].AddRange(dialogLines);
            }

            Debug.Log("CSV content successfully parsed and loaded into customTokens dictionary.");
        }


        private void LoadFamilyMattersCSV(string csvFileName)
        {
            customTokens.Clear(); // Ensure the dictionary is cleared before loading new content

            // Use ModManager to load the CSV file
            TextAsset csvAsset;
            Debug.Log("Attempting to load CSV file using ModManager...");
            if (!ModManager.Instance.TryGetAsset(csvFileName, true, out csvAsset))
            {
                Debug.LogError($"CSV file not found: {csvFileName}");
                return;
            }

            if (csvAsset == null)
            {
                Debug.LogError($"CSV asset is null: {csvFileName}");
                return;
            }

            Debug.Log("CSV file successfully loaded. Beginning to parse CSV content...");
            string[] lines = csvAsset.text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Debug.Log($"Total lines in CSV: {lines.Length}");
            foreach (string line in lines)
            {
                Debug.Log($"Processing line: {line}");
                string[] parts = line.Split(new[] { ',' }, 2);
                if (parts.Length != 2)
                {
                    Debug.LogWarning($"Invalid line in CSV: {line}");
                    continue;
                }

                if (!int.TryParse(parts[0], out int section) || section == 0)
                {
                    Debug.LogWarning($"Invalid section number in CSV: {parts[0]}");
                    continue;
                }

                string[] dialogLines = parts[1].Split(new[] { "[/record]" }, StringSplitOptions.RemoveEmptyEntries);
                Debug.Log($"Section {section} has {dialogLines.Length} dialog lines.");
                if (!customTokens.ContainsKey(section))
                {
                    customTokens[section] = new List<string>();
                }

                customTokens[section].AddRange(dialogLines);
            }

            Debug.Log("CSV content successfully parsed and loaded into customTokens dictionary.");
        }
        
        public string GetFamilyResponse(int npcId)
        {
            Debug.Log("GetFamilyResponse called.");
            LoadFamilyMattersCSV("FamilyMatters.csv");

            if (customTokens == null || customTokens.Count == 0)
            {
                Debug.LogWarning("Custom tokens have not been loaded.");
                return "I have nothing to say about my family.";
            }

            // Retrieve or assign a greeting section for the NPC
            var bridge = CustomNPCBridgeMod.CustomNPCBridge.Instance;
            int? assignedSection = bridge.GetGreetingSection(npcId);

            if (!assignedSection.HasValue)
            {
                Debug.LogWarning($"No greeting section assigned for NPC ID {npcId}");
                return "I have nothing to say about my family.";
            }

            // Get the dialog lines for the assigned section
            var dialogLines = customTokens[assignedSection.Value];
            var dialogRandom = new System.Random();
            int dialogIndex = dialogRandom.Next(dialogLines.Count);

            Debug.Log($"Selected family response: {dialogLines[dialogIndex]}");
            return dialogLines[dialogIndex];
        }

        private const int maxNumAnswersNpcGivesTellMeAboutOrRumors = 5;

        //GET VANILLA TELL ME TOPICS
        #region
        public List<TalkManager.ListItem> GetVanillaTellMeAboutTopics()
        {
            List<TalkManager.ListItem> listTopicTellMeAbout = new List<TalkManager.ListItem>();

            // Assemble the list of "Tell Me About" topics
            AssembleTopiclistTellMeAbout(listTopicTellMeAbout);

            return listTopicTellMeAbout;
        }

        private void AssembleTopiclistTellMeAbout(List<TalkManager.ListItem> listTopicTellMeAbout)
        {
            listTopicTellMeAbout.Clear();

            // Add "Any News" topic
            TalkManager.ListItem itemAnyNews = new TalkManager.ListItem();
            itemAnyNews.type = TalkManager.ListItemType.Item;
            itemAnyNews.questionType = TalkManager.QuestionType.News;
            itemAnyNews.caption = TextManager.Instance.GetLocalizedText("AnyNews");
            itemAnyNews.index = 1000; // Example index for "Any News"
            listTopicTellMeAbout.Add(itemAnyNews);

            // Add "Where Am I" topic
            TalkManager.ListItem itemWhereAmI = new TalkManager.ListItem();
            itemWhereAmI.type = TalkManager.ListItemType.Item;
            itemWhereAmI.questionType = TalkManager.QuestionType.WhereAmI;
            itemWhereAmI.caption = TextManager.Instance.GetLocalizedText("WhereAmI");
            itemWhereAmI.index = 1001; // Example index for "Where Am I"
            listTopicTellMeAbout.Add(itemWhereAmI);

            // Add quest-related topics
            foreach (KeyValuePair<ulong, QuestResources> questInfo in dictQuestInfo)
            {
                foreach (KeyValuePair<string, QuestResourceInfo> questResourceInfo in questInfo.Value.resourceInfo)
                {
                    TalkManager.ListItem itemQuestTopic = new TalkManager.ListItem();
                    itemQuestTopic.type = TalkManager.ListItemType.Item;
                    string captionString = string.Empty;

                    switch (questResourceInfo.Value.resourceType)
                    {
                        case QuestInfoResourceType.Location:
                            itemQuestTopic.questionType = TalkManager.QuestionType.QuestLocation;
                            Place place = (Place)questResourceInfo.Value.questResource;
                            if (place != null)
                            {
                                if (place.SiteDetails.buildingName != null)
                                    captionString = place.SiteDetails.buildingName;
                                else if (place.SiteDetails.locationName != null)
                                    captionString = place.SiteDetails.locationName;
                            }
                            itemQuestTopic.index = 2000 + questInfo.Key.GetHashCode(); // Example index based on quest ID
                            break;
                        case QuestInfoResourceType.Person:
                            itemQuestTopic.questionType = TalkManager.QuestionType.QuestPerson;
                            Person person = (Person)questResourceInfo.Value.questResource;
                            if (person != null)
                            {
                                captionString = person.DisplayName;
                            }
                            itemQuestTopic.index = 3000 + questInfo.Key.GetHashCode(); // Example index based on quest ID
                            break;
                        case QuestInfoResourceType.Thing:
                            itemQuestTopic.questionType = TalkManager.QuestionType.QuestItem;
                            Item item = (Item)questResourceInfo.Value.questResource;
                            if (item?.DaggerfallUnityItem != null)
                            {
                                captionString = item.DaggerfallUnityItem.ItemName;
                            }
                            itemQuestTopic.index = 4000 + questInfo.Key.GetHashCode(); // Example index based on quest ID
                            break;
                    }

                    itemQuestTopic.caption = captionString;
                    listTopicTellMeAbout.Add(itemQuestTopic);
                }
            }

            // Add faction-related topics
            foreach (var factionID in infoFactionIDs)
            {
                string factionName = GameManager.Instance.PlayerEntity.FactionData.GetFactionName(factionID);

                if (!string.IsNullOrEmpty(factionName))
                {
                    TalkManager.ListItem itemOrganizationInfo = new TalkManager.ListItem();
                    itemOrganizationInfo.type = TalkManager.ListItemType.Item;
                    itemOrganizationInfo.questionType = TalkManager.QuestionType.OrganizationInfo;
                    itemOrganizationInfo.factionID = factionID;
                    itemOrganizationInfo.caption = factionName;
                    itemOrganizationInfo.index = GetFactionTextIndex(factionID); // Use a method to get a valid index
                    listTopicTellMeAbout.Add(itemOrganizationInfo);
                }
                else
                {
                    Debug.LogWarning($"Faction ID {factionID} has an empty or invalid name.");
                }
            }
        }


        private int GetFactionTextIndex(int factionID)
        {
            // Map faction IDs to valid text indices
            switch (factionID)
            {
                case 40: return 861; // Example valid index for Clavicus Vile
                case 41: return 866; // Example valid index for Mehrunes Dagon
                case 42: return 860; // Example valid index for Molag Bal
                case 108: return 862;
                case 129: return 863;
                case 306: return 864;
                case 353: return 865;
                case 67: return 867;
                case 82: return 869;
                case 84: return 870;
                case 88: return 871;
                case 92: return 872;
                case 94: return 873;
                case 106: return 874;
                case 36: return 875;
                case 83: return 876;
                case 85: return 877;
                case 89: return 878;
                case 93: return 879;
                case 95: return 880;
                case 98: return 894;
                case 99: return 881;
                case 107: return 882;
                case 37: return 883;
                case 368: return 884;
                case 408: return 885;
                case 409: return 886;
                case 410: return 887;
                case 411: return 888;
                case 413: return 889;
                case 414: return 890;
                case 415: return 891;
                case 416: return 892;
                                   
                default: return -1; // Return -1 for invalid or unknown factions
            }
        }

        private void RetrievePlayerQuestionsAndNpcResponses(List<TalkManager.ListItem> listTopicTellMeAbout)
        {
            foreach (var listItem in listTopicTellMeAbout)
            {
                string playerQuestion = GetQuestionText(listItem, DaggerfallTalkWindow.TalkTone.Normal);
                string npcResponse = GetAnswerTellMeAboutTopic(listItem);

                listItemData[listItem] = (playerQuestion, npcResponse);
            }
        }

        public string GetQuestionText(TalkManager.ListItem listItem, DaggerfallTalkWindow.TalkTone talkTone)
        {
            return "I must enquire about a matter - " + listItem.caption;
        }

        public string GetAnswerTellMeAboutTopic(TalkManager.ListItem listItem)
        {
            Debug.Log($"GetAnswerTellMeAboutTopic called with listItem: {listItem?.caption}");

            try
            {
                switch (listItem.questionType)
                {
                    case TalkManager.QuestionType.News:
                        return GetNewsResponse();
                    case TalkManager.QuestionType.WhereAmI:
                        return GetWhereAmIResponse();
                    case TalkManager.QuestionType.QuestLocation:
                        return GetQuestLocationResponse(listItem);
                    case TalkManager.QuestionType.QuestPerson:
                        return GetQuestPersonResponse(listItem);
                    case TalkManager.QuestionType.QuestItem:
                        return GetQuestItemResponse(listItem);
                    case TalkManager.QuestionType.OrganizationInfo:
                        return GetOrganizationInfoResponse(listItem);
                    default:
                        return $"NPC's response for {listItem.caption}";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing answer for listItem {listItem?.caption}: {ex.Message}");
                return $"Error processing response for {listItem?.caption}";
            }
        }

        private string GetNewsResponse()
        {
            Debug.Log("Fetching News");

            // Check if TalkManager instance is available
            if (TalkManager.Instance == null)
            {
                Debug.LogError("TalkManager.Instance is null.");
                return "Error: TalkManager instance not available.";
            }

            try
            {
                // Log before fetching news or rumors
                Debug.Log("Attempting to fetch news or rumors...");

                // Ensure npcData is initialized
                if (npcData == null)
                {
                    Debug.LogError("npcData is null.");
                    return "Error: NPC data is not available.";
                }

                // Fetch news or rumors
                var newsOrRumors = GetCustomNewsOrRumors();

                // Check if the fetched news or rumors is null or empty
                if (string.IsNullOrEmpty(newsOrRumors))
                {
                    Debug.LogWarning("No news or rumors available.");
                    return "No news or rumors available at the moment.";
                }

                // Log the fetched news or rumors
                Debug.Log($"Fetched News/Rumors: {newsOrRumors}");

                return newsOrRumors;
            }
            catch (NullReferenceException ex)
            {
                Debug.LogError($"Null reference error fetching news or rumors: {ex.Message}");
                Debug.LogError($"Stack Trace: {ex.StackTrace}");
                return "Error fetching news or rumors due to a null reference.";
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur during the fetching process
                Debug.LogError($"Error fetching news or rumors: {ex.Message}");
                Debug.LogError($"Stack Trace: {ex.StackTrace}");
                return "Error fetching news or rumors due to an unexpected error.";
            }
        }

        private string GetCustomNewsOrRumors()
        {
            const int outOfNewsRecordIndex = 1457;
            if (npcData.numAnswersGivenTellMeAboutOrRumors < maxNumAnswersNpcGivesTellMeAboutOrRumors || npcData.isSpyMaster || TalkManager.Instance.NPCsKnowEverything())
            {
                string news = TextManager.Instance.GetLocalizedText("resolvingError");
                List<TalkManager.RumorMillEntry> validRumors = TalkManagerProxy.GetValidRumors(TalkManager.Instance);

                if (validRumors == null)
                {
                    Debug.LogError("validRumors is null.");
                    return "Error: No valid rumors available.";
                }

                if (validRumors.Count == 0)
                    return TalkManagerProxy.ExpandRandomTextRecord(TalkManager.Instance, outOfNewsRecordIndex);

                TalkManager.RumorMillEntry entry = TalkManagerProxy.WeightedRandomRumor(TalkManager.Instance, validRumors);

                if (entry == null)
                {
                    Debug.LogError("RumorMillEntry is null.");
                    return "Error: Could not retrieve a valid rumor entry.";
                }

                if (entry.rumorType == TalkManager.RumorType.CommonRumor)
                {
                    if (entry.listRumorVariants != null)
                    {
                        TextFile.Token[] tokens = entry.listRumorVariants[0];

                        if (tokens == null)
                        {
                            Debug.LogError("tokens is null.");
                            return "Error: No tokens available for the rumor.";
                        }

                        int regionID = -1;
                        FactionFile.FactionData factionData;

                        if (entry.regionID != -1)
                            regionID = entry.regionID;
                        else if (GameManager.Instance.PlayerEntity.FactionData.GetFactionData(entry.faction1, out factionData) && factionData.region != -1)
                            regionID = factionData.region;
                        else if (GameManager.Instance.PlayerEntity.FactionData.GetFactionData(entry.faction2, out factionData) && factionData.region != -1)
                            regionID = factionData.region;
                        else // Classic uses a random region in this case, but that can create odd results for the witches rumor and maybe more. Using current region.
                            regionID = GameManager.Instance.PlayerGPS.CurrentRegionIndex;

                        MacroHelper.SetFactionIdsAndRegionID(entry.faction1, entry.faction2, regionID);
                        MacroHelper.ExpandMacros(ref tokens, TalkManager.Instance);
                        MacroHelper.SetFactionIdsAndRegionID(-1, -1, -1); // Reset again so %reg macro may resolve to current region if needed
                        news = TalkManager.TokensToString(tokens, false);
                    }
                }
                else if (entry.rumorType == TalkManager.RumorType.QuestRumorMill || entry.rumorType == TalkManager.RumorType.QuestProgressRumor)
                {
                    int variant = UnityEngine.Random.Range(0, entry.listRumorVariants.Count);
                    TextFile.Token[] tokens = entry.listRumorVariants[variant];

                    if (tokens == null)
                    {
                        Debug.LogError("tokens is null.");
                        return "Error: No tokens available for the rumor variant.";
                    }

                    // Expand tokens and reveal dialog-linked resources
                    QuestMacroHelper macroHelper = new QuestMacroHelper();
                    macroHelper.ExpandQuestMessage(GameManager.Instance.QuestMachine.GetQuest(entry.questID), ref tokens, true);
                    news = TalkManager.TokensToString(tokens);
                }

                npcData.numAnswersGivenTellMeAboutOrRumors++;

                return news;
            }

            return TalkManagerProxy.ExpandRandomTextRecord(TalkManager.Instance, outOfNewsRecordIndex);
        }

        private string GetWhereAmIResponse()
        {
            // Implement logic to fetch where am I response
            return TalkManager.Instance.GetAnswerWhereAmI();
        }

        private string GetQuestLocationResponse(TalkManager.ListItem listItem)
        {
            // Implement logic to fetch quest location response
            return TalkManager.Instance.GetAnswerTellMeAboutTopic(listItem);
        }

        private string GetQuestPersonResponse(TalkManager.ListItem listItem)
        {
            // Implement logic to fetch quest person response
            return TalkManager.Instance.GetAnswerTellMeAboutTopic(listItem);
        }

        private string GetQuestItemResponse(TalkManager.ListItem listItem)
        {
            // Implement logic to fetch quest item response
            return TalkManager.Instance.GetAnswerTellMeAboutTopic(listItem);
        }

        private string GetOrganizationInfoResponse(TalkManager.ListItem listItem)
        {
            Debug.Log($"GetOrganizationInfoResponse called with listItem: {listItem?.caption}, factionID: {listItem?.factionID}");

            if (listItem == null || listItem.factionID == 0)
            {
                Debug.LogError("Invalid faction information.");
                return "Invalid faction information.";
            }

            if (TalkManager.Instance == null)
            {
                Debug.LogError("TalkManager.Instance is null.");
                return "Error: TalkManager instance not available.";
            }

            // Check if the faction data exists for the given faction ID
            if (!GameManager.Instance.PlayerEntity.FactionData.GetFactionData(listItem.factionID, out var factionData))
            {
                Debug.LogError($"Faction data not found for faction ID {listItem.factionID}");
                return "Error: Faction data not found.";
            }

            Debug.Log($"Faction data found for faction ID {listItem.factionID}: {factionData.name}");

            string response = null;
            try
            {
                int recordIndex = listItem.index;
                Debug.Log($"Record index: {recordIndex}"); // Add logging for recordIndex

                if (recordIndex < 0)
                {
                    Debug.LogError($"Invalid record index: {recordIndex}");
                    return "Error: Invalid record index.";
                }

                Debug.Log("Attempting to call custom ExpandRandomTextRecord");
                response = ExpandRandomTextRecord(recordIndex);
                Debug.Log($"ExpandRandomTextRecord returned: {response}");
            }
            catch (NullReferenceException ex)
            {
                Debug.LogError($"Error getting organization info for faction ID {listItem.factionID}: {ex.Message}");
                Debug.LogError($"Stack Trace: {ex.StackTrace}");
                return "Error retrieving organization information due to a null reference.";
            }
            catch (Exception ex)
            {
                Debug.LogError($"General error getting organization info for faction ID {listItem.factionID}: {ex.Message}");
                Debug.LogError($"Stack Trace: {ex.StackTrace}");
                return "Error retrieving organization information due to an unexpected error.";
            }

            if (string.IsNullOrEmpty(response))
            {
                Debug.LogWarning("No information available for this organization.");
                response = "No information available for this organization.";
            }

            return response;
        }

        public (string PlayerQuestion, string NpcResponse) GetListItemData(TalkManager.ListItem listItem)
        {
            if (listItemData.TryGetValue(listItem, out var data))
            {
                return data;
            }
            return ("", "");
        }
        #endregion

        //GET VANILLA LOCATION TOPICS
        #region

        public List<TalkManager.ListItem> GetVanillaLocationTopics()
        {
            List<TalkManager.ListItem> listTopicLocation = new List<TalkManager.ListItem>();

            // Assemble the list of location categories
            AssembleTopiclistLocation(listTopicLocation);

            return listTopicLocation;
        }

        private void AssembleTopiclistLocation(List<TalkManager.ListItem> listTopicLocation)
        {
            listTopicLocation.Clear();

            // Add location categories
            AddLocationCategories(listTopicLocation);
        }

        private void AddLocationCategories(List<TalkManager.ListItem> listTopicLocation)
        {
            // Add "Banks" category
            TalkManager.ListItem itemBanks = new TalkManager.ListItem();
            itemBanks.type = TalkManager.ListItemType.Item;
            itemBanks.questionType = TalkManager.QuestionType.LocalBuilding;
            itemBanks.caption = "Banks";
            itemBanks.index = 1001; // Example index for "Banks"
            listTopicLocation.Add(itemBanks);

            // Add "General Stores" category
            TalkManager.ListItem itemGeneralStores = new TalkManager.ListItem();
            itemGeneralStores.type = TalkManager.ListItemType.Item;
            itemGeneralStores.questionType = TalkManager.QuestionType.LocalBuilding;
            itemGeneralStores.caption = "General Stores";
            itemGeneralStores.index = 1002; // Example index for "General Stores"
            listTopicLocation.Add(itemGeneralStores);

            // Add "Guilds" category
            TalkManager.ListItem itemGuilds = new TalkManager.ListItem();
            itemGuilds.type = TalkManager.ListItemType.Item;
            itemGuilds.questionType = TalkManager.QuestionType.LocalBuilding;
            itemGuilds.caption = "Guilds";
            itemGuilds.index = 1003; // Example index for "Guilds"
            listTopicLocation.Add(itemGuilds);

            // Add "Local Temples" category
            TalkManager.ListItem itemLocalTemples = new TalkManager.ListItem();
            itemLocalTemples.type = TalkManager.ListItemType.Item;
            itemLocalTemples.questionType = TalkManager.QuestionType.LocalBuilding;
            itemLocalTemples.caption = "Local Temples";
            itemLocalTemples.index = 1004; // Example index for "Local Temples"
            listTopicLocation.Add(itemLocalTemples);

            // Add "Taverns" category
            TalkManager.ListItem itemTaverns = new TalkManager.ListItem();
            itemTaverns.type = TalkManager.ListItemType.Item;
            itemTaverns.questionType = TalkManager.QuestionType.LocalBuilding;
            itemTaverns.caption = "Taverns";
            itemTaverns.index = 1005; // Example index for "Taverns"
            listTopicLocation.Add(itemTaverns);

            // Add "Regional" category
            TalkManager.ListItem itemRegional = new TalkManager.ListItem();
            itemRegional.type = TalkManager.ListItemType.Item;
            itemRegional.questionType = (TalkManager.QuestionType)(object)CustomQuestionType.RegionalBuilding;
            itemRegional.caption = "Regional";
            itemRegional.index = 1006; // Example index for "Regional"
            listTopicLocation.Add(itemRegional);
        }
        #endregion










        private string ExpandRandomTextRecord(int recordIndex)
        {
            Debug.Log($"Expanding record with index: {recordIndex}");

            TextFile.Token[] tokens = null;
            try
            {
                tokens = DaggerfallUnity.Instance.TextProvider.GetRandomTokens(recordIndex);
                if (tokens == null || tokens.Length == 0)
                {
                    Debug.LogError($"No tokens found for the given record index: {recordIndex}");
                    return "No information available.";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting random tokens for record index {recordIndex}: {ex.Message}");
                return "Error retrieving text.";
            }

            try
            {
                MacroHelper.ExpandMacros(ref tokens);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error expanding macros for tokens of record index {recordIndex}: {ex.Message}");
                return "Error processing text.";
            }

            StringBuilder sb = new StringBuilder();
            foreach (var token in tokens)
            {
                if (token.text != null)
                {
                    sb.Append(token.text);
                }
            }
            return sb.ToString();
        }



        // Add these classes within CustomTalkManager

        public class TalkManagerContext
        {
            public TalkManager.ListItem currentQuestionListItem;
            public Races npcRace;
            public Genders potentialQuestorGender;
        }

        private class TalkManagerDataSource : MacroDataSource
        {
            private TalkManagerContext parent;

            public TalkManagerDataSource(TalkManagerContext context)
            {
                this.parent = context;
            }

            public Races NpcRace => parent.npcRace;
            public Genders PotentialQuestorGender => parent.potentialQuestorGender;

            public override string Name()
            {
                return MacroHelper.GetRandomFullName();
            }

            public override string FemaleName()
            {
                NameHelper.BankTypes nameBank = (NameHelper.BankTypes)MapsFile.RegionRaces[GameManager.Instance.PlayerGPS.CurrentRegionIndex];
                return DaggerfallUnity.Instance.NameHelper.FullName(nameBank, Genders.Female);
            }

            public override string MaleName()
            {
                NameHelper.BankTypes nameBank = (NameHelper.BankTypes)MapsFile.RegionRaces[GameManager.Instance.PlayerGPS.CurrentRegionIndex];
                return DaggerfallUnity.Instance.NameHelper.FullName(nameBank, Genders.Male);
            }

            public override string Direction()
            {
                if (parent.currentQuestionListItem.questionType == TalkManager.QuestionType.LocalBuilding || parent.currentQuestionListItem.questionType == TalkManager.QuestionType.Person)
                {
                    return GameManager.Instance.TalkManager.GetKeySubjectLocationCompassDirection();
                }
                return TextManager.Instance.GetLocalizedText("resolvingError");
            }
            public class CustomTalkManagerContext
            {
                public TalkManager.ListItem currentQuestionListItem;
                public Races npcRace;
                public Genders potentialQuestorGender;
            }

            private class CustomTalkManagerDataSource : MacroDataSource
            {
                private CustomTalkManagerContext parent;

                public CustomTalkManagerDataSource(CustomTalkManagerContext context)
                {
                    this.parent = context;
                  
                }

                public override string Name()
                {
                    // Used for greeting messages only: 7215, 7216, 7217
                    if (!string.IsNullOrEmpty(GameManager.Instance.TalkManager.GreetingNameNPC))
                        return GameManager.Instance.TalkManager.GreetingNameNPC;

                    return MacroHelper.GetRandomFullName();
                }

                public override string FemaleName()
                {
                    NameHelper.BankTypes nameBank = (NameHelper.BankTypes)MapsFile.RegionRaces[GameManager.Instance.PlayerGPS.CurrentRegionIndex];
                    return DaggerfallUnity.Instance.NameHelper.FullName(nameBank, Genders.Female);
                }

                public override string MaleName()
                {
                    DFRandom.Seed += 3547;
                    NameHelper.BankTypes nameBank = (NameHelper.BankTypes)MapsFile.RegionRaces[GameManager.Instance.PlayerGPS.CurrentRegionIndex];
                    string name = DaggerfallUnity.Instance.NameHelper.FullName(nameBank, Genders.Male);
                    DFRandom.Seed -= 3547;
                    return name;
                }

                public override string Direction()
                {
                    if (parent.currentQuestionListItem.questionType == TalkManager.QuestionType.LocalBuilding || parent.currentQuestionListItem.questionType == TalkManager.QuestionType.Person)
                    {
                        return GameManager.Instance.TalkManager.GetKeySubjectLocationCompassDirection();
                    }
                    return TextManager.Instance.GetLocalizedText("resolvingError");
                }

                public override string DialogHint()
                {
                    if (parent.currentQuestionListItem.questionType == TalkManager.QuestionType.LocalBuilding)
                    {
                        return GameManager.Instance.TalkManager.GetKeySubjectBuildingHint();
                    }
                    else if (parent.currentQuestionListItem.questionType == TalkManager.QuestionType.Person)
                    {
                        return GameManager.Instance.TalkManager.GetKeySubjectPersonHint();
                    }
                    else if (parent.currentQuestionListItem.questionType == TalkManager.QuestionType.QuestLocation || parent.currentQuestionListItem.questionType == TalkManager.QuestionType.QuestPerson || parent.currentQuestionListItem.questionType == TalkManager.QuestionType.OrganizationInfo)
                    {
                        return GameManager.Instance.TalkManager.GetDialogHint(parent.currentQuestionListItem);
                    }
                    return TextManager.Instance.GetLocalizedText("resolvingError");
                }

                public override string DialogHint2()
                {
                    if (parent.currentQuestionListItem.questionType == TalkManager.QuestionType.LocalBuilding)
                    {
                        return GameManager.Instance.TalkManager.GetKeySubjectBuildingHint();
                    }
                    else if (parent.currentQuestionListItem.questionType == TalkManager.QuestionType.Person)
                    {
                        return GameManager.Instance.TalkManager.GetKeySubjectPersonHint();
                    }
                    else if (parent.currentQuestionListItem.questionType == TalkManager.QuestionType.QuestLocation || parent.currentQuestionListItem.questionType == TalkManager.QuestionType.QuestPerson || parent.currentQuestionListItem.questionType == TalkManager.QuestionType.OrganizationInfo)
                    {
                        return GameManager.Instance.TalkManager.GetDialogHint2(parent.currentQuestionListItem);
                    }
                    return TextManager.Instance.GetLocalizedText("resolvingError");
                }

                public override string Oath()
                {
                    // Get NPC race with fallback to race of current region
                    Races race = parent.npcRace;
                    if (race == Races.None)
                        race = GameManager.Instance.PlayerGPS.GetRaceOfCurrentRegion();

                    int oathId = (int)RaceTemplate.GetFactionRaceFromRace(race);

                    return DaggerfallUnity.Instance.TextProvider.GetRandomText(201 + oathId);
                }

                public override string Pronoun()
                {
                    switch (parent.potentialQuestorGender)
                    {
                        default:
                        case Genders.Male:
                            return TextManager.Instance.GetLocalizedText("pronounHe");
                        case Genders.Female:
                            return TextManager.Instance.GetLocalizedText("pronounShe");
                    }
                }

                public override string Pronoun2()
                {
                    switch (parent.potentialQuestorGender)
                    {
                        default:
                        case Genders.Male:
                            return TextManager.Instance.GetLocalizedText("pronounHim");
                        case Genders.Female:
                            return TextManager.Instance.GetLocalizedText("pronounHer");
                    }
                }

                public override string Pronoun3()
                {
                    switch (parent.potentialQuestorGender)
                    {
                        default:
                        case Genders.Male:
                            return TextManager.Instance.GetLocalizedText("pronounHis");
                        case Genders.Female:
                            return TextManager.Instance.GetLocalizedText("pronounHer2");
                    }
                }

                public override string Pronoun4()
                {
                    switch (parent.potentialQuestorGender)
                    {
                        default:
                        case Genders.Male:
                            return TextManager.Instance.GetLocalizedText("pronounHis2");
                        case Genders.Female:
                            return TextManager.Instance.GetLocalizedText("pronounHers");
                    }
                }

                public override string PotentialQuestorName()
                {
                    return TalkManager.Instance.GetQuestorName();
                }

                public override string PotentialQuestorLocation()
                {
                    return TalkManager.Instance.GetQuestorLocation();
                }
            }
        }
    }
} 