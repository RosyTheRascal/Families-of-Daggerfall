using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;
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
using FamilyNameModifierMod;
using CustomNPCClickHandlerMod;
using CustomTalkManagerMod;
using CustomDaggerfallTalkWindowMod;


namespace CustomDaggerfallTalkWindowMod
{

    public class ExtendedListBox : ListBox
    {
        private int _selectedIndex = -1;
        private bool _isUpdating;
        private const int PixelWiseScrollIncrement = 20; // Adjust this value to control the scroll speed for PixelWise

        public int HighlightedIndex
        {
            get { return GetHighlightedIndex(); }
            set { SetHighlightedIndex(value); }
        }

        public new int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value != _selectedIndex && !_isUpdating) // Prevent recursive calls
                {
                    _isUpdating = true;
                    _selectedIndex = value;
                    SetSelectedIndexInternal(value);
                    HighlightedIndex = value;  // Ensure the highlighted index is the same as the selected index
                    _isUpdating = false;
                }
            }
        }

        private int GetHighlightedIndex()
        {
            return (int)typeof(ListBox)
                .GetField("highlightedIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        private void SetHighlightedIndex(int value)
        {
            typeof(ListBox)
                .GetField("highlightedIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(this, value);
        }

        private int GetSelectedIndex()
        {
            return _selectedIndex;
        }

        private void SetSelectedIndexInternal(int value)
        {
            Debug.Log($"SetSelectedIndexInternal called with value: {value}");

            typeof(ListBox)
                .GetField("selectedIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(this, value);

            // Ensure the selected index is valid and update any UI or logic as needed
            if (value >= 0 && value < this.Count)
            {
                this.Update(); // Force an update to the UI or any dependent logic
            }
        }

        private VerticalScrollModes GetVerticalScrollMode()
        {
            return (VerticalScrollModes)typeof(ListBox)
                .GetField("verticalScrollMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        private int GetScrollIndex()
        {
            return (int)typeof(ListBox)
                .GetField("scrollIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        private void SetScrollIndex(int value)
        {
            typeof(ListBox)
                .GetField("scrollIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(this, value);
        }

        private int GetRowsDisplayed()
        {
            return (int)typeof(ListBox)
                .GetField("rowsDisplayed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        private int GetHorizontalScrollIndex()
        {
            return (int)typeof(ListBox)
                .GetField("horizontalScrollIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        private Color GetSelectedShadowColor()
        {
            return (Color)typeof(ListBox)
                .GetField("selectedShadowColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        private Vector2 GetShadowPosition()
        {
            return (Vector2)typeof(ListBox)
                .GetField("shadowPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        private Color GetShadowColor()
        {
            return (Color)typeof(ListBox)
                .GetField("shadowColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        private List<ListItem> GetListItems()
        {
            return (List<ListItem>)typeof(ListBox)
                .GetField("listItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(this);
        }

        public override void Draw()
        {
            base.Draw();

            var listItems = GetListItems();
            var verticalScrollMode = GetVerticalScrollMode();
            var scrollIndex = GetScrollIndex();
            var rowsDisplayed = GetRowsDisplayed();
            var horizontalScrollIndex = GetHorizontalScrollIndex();

            if (verticalScrollMode == VerticalScrollModes.EntryWise)
            {
                float x = 0, y = 0;
                float currentLine = 0;
                for (int i = 0; i < listItems.Count; i++)
                {
                    TextLabel label = listItems[i].textLabel;

                    // Check if the current line is within the scroll range
                    if (currentLine < scrollIndex || currentLine >= scrollIndex + rowsDisplayed)
                    {
                        currentLine += label.NumTextLines;
                        continue;
                    }

                    currentLine += label.NumTextLines;
                    label.StartCharacterIndex = horizontalScrollIndex;

                    // Set the correct text color for each item
                    DecideTextColor(label, i);

                    // Position and draw the label
                    label.Position = new Vector2(x, y);
                    label.Draw();

                    y += label.TextHeight + RowSpacing;
                }
            }
            else if (verticalScrollMode == VerticalScrollModes.PixelWise)
            {
                int x = 0;
                int y = -scrollIndex;
                for (int i = 0; i < listItems.Count; i++)
                {
                    TextLabel label = listItems[i].textLabel;

                    // Check if the current item is within the visible area
                    if (y + label.TextHeight < 0 || y >= this.Size.y)
                    {
                        y += label.TextHeight + RowSpacing;
                        continue;
                    }

                    if (HorizontalScrollMode == HorizontalScrollModes.CharWise)
                        label.StartCharacterIndex = horizontalScrollIndex;
                    else if (HorizontalScrollMode == HorizontalScrollModes.PixelWise)
                        x = -horizontalScrollIndex;

                    // Set the correct text color for each item
                    DecideTextColor(label, i);

                    // Position and draw the label
                    label.HorzPixelScrollOffset = x;
                    label.Position = new Vector2(x, y);
                    label.Draw();

                    y += label.TextHeight + RowSpacing;
                }
            }
        }

        Color lilac = new Color(0.698f, 0.812f, 1.0f);
        Color pink = new Color(255f / 255f, 182f / 255f, 193f / 255f);

        private void DecideTextColor(TextLabel label, int i)
        {
            var listItems = GetListItems();
            var selectedShadowColor = GetSelectedShadowColor();
            var shadowPosition = GetShadowPosition();
            var shadowColor = GetShadowColor();

            if (i == HighlightedIndex && i == GetSelectedIndex())
            {
                label.TextColor = Color.white; // Both highlighted and selected
                label.ShadowPosition = shadowPosition;
                label.ShadowColor = selectedShadowColor;
            }
            else if (i == GetSelectedIndex())
            {
                label.TextColor = lilac; // Selected
                label.ShadowPosition = shadowPosition;
                label.ShadowColor = selectedShadowColor;
            }
            else if (i == HighlightedIndex)
            {
                label.TextColor = pink; // Highlighted
                label.ShadowPosition = shadowPosition;
                label.ShadowColor = shadowColor;
            }
            else
            {
                label.TextColor = listItems[i].Enabled ? listItems[i].textColor : listItems[i].disabledTextColor;
                label.ShadowPosition = shadowPosition;
                label.ShadowColor = shadowColor;
            }
        }

        public void ScrollUp()
        {
            var scrollIndex = GetScrollIndex();
            if (scrollIndex > 0)
            {
                if (GetVerticalScrollMode() == VerticalScrollModes.PixelWise)
                {
                    SetScrollIndex(scrollIndex - PixelWiseScrollIncrement); // Increase the scroll speed for PixelWise
                }
                else
                {
                    SetScrollIndex(scrollIndex - 1); // Default increment for EntryWise
                }
                Update();
            }
        }

        public void ScrollDown()
        {
            var scrollIndex = GetScrollIndex();
            var listItems = GetListItems();
            if (GetVerticalScrollMode() == VerticalScrollModes.PixelWise)
            {
                if (scrollIndex < HeightContent() - (int)Size.y)
                {
                    SetScrollIndex(scrollIndex + PixelWiseScrollIncrement); // Increase the scroll speed for PixelWise
                }
            }
            else
            {
                if (scrollIndex < listItems.Count - 1)
                {
                    SetScrollIndex(scrollIndex + 1); // Default increment for EntryWise
                }
            }
            Update();
        }
    }

    public class CustomDaggerfallTalkWindow : UserInterfaceWindow
    {
        //declarations
        #region
        private MacroDataSource macroDataSource;
        public event System.Action OnCloseWindow;
        private Coroutine talkManagerCoroutine;

        private int selectedTopicIndex = -1;
        private int npcTopicHash = 0;
        private const int NumVanillaTopics = 33;

        public bool isChildNPC = false;

        protected Panel npcPortrait;
        protected TextLabel labelNameNPC;
        private   ExtendedListBox listboxTopics;
        private   ExtendedListBox listboxConversation;
        protected Panel dialogPanel;
        protected TextLabel labelGreeting;
        protected ListBox playerSaysListBox;

        private Button buttonReturn;
        private Button buttonDummyLeft;
        private Button buttonLocation;
        private Button buttonPeople;
        private Button buttonThings;
        private Button buttonWork;
        private Button buttonTellMe;
        private Button buttonWhereIs;
        private Button buttonTellMeBlock;
        private Button buttonWhereIsBlock;

        private Texture2D textureLeftYellowButton;
        private Texture2D textureGreyHeaderButtons;
        private Texture2D textureCategoryHeaderGold;
        private Texture2D textureCategoryGrayedOut;
        private GameObject restMarker;

        // Reference to the Daggerfall UI Manager
        private IUserInterfaceManager uiManager;
        private DaggerfallBaseWindow previousWindow;

        public const string PortraitImgName = "FACES.CIF";
        public const string FacesImgName = "FACES.CIF";

        private Panel mainPanel;
        private Texture2D textureBackground;


        private VerticalScrollBar verticalScrollBarTopics;
        private VerticalScrollBar verticalScrollBarConversation;
        private HorizontalSlider horizontalSliderTopics;

        private const string talkWindowImgName = "TALK01I0.IMG";
        private Color ScreenDimColor = Color.black;
        private Texture2D texturePortrait;
        private const string redArrowsTextureName = "INVE07I0.IMG";
        private const string greenArrowsTextureName = "INVE06I0.IMG";
        private const float textScaleModernConversationStyle = 1.0f;
        private const float textBlockSizeModernConversationStyle = 0.75f;
        private Button buttonTopicUp;
        private Button buttonTopicDown;
        private Button buttonTopicLeft;
        private Button buttonTopicRight;
        private Button buttonConversationUp;
        private Button buttonConversationDown;
        private Button buttonCheckboxTonePolite;
        private Button buttonCheckboxToneNormal;
        private Button buttonCheckboxToneBlunt;

        private Panel panelPortrait;
        private Vector2 panelPortraitPos = new Vector2(119, 65);
        private Vector2 panelPortraitSize = new Vector2(64f, 64f);

        private Panel panelTone;
        private Vector2 panelTonePolitePos = new Vector2(258, 18);
        private Vector2 panelToneNormalPos = new Vector2(258, 28);
        private Vector2 panelToneBluntPos = new Vector2(258, 38);
        private Vector2 panelToneSize = new Vector2(6f, 6f);

        private Rect rectButtonTonePolite = new Rect(258, 18, 6, 6);
        private Rect rectButtonToneNormal = new Rect(258, 28, 6, 6);
        private Rect rectButtonToneBlunt = new Rect(258, 38, 6, 6);


        // New button positions and sizes
        private Vector2 buttonLocationPos = new Vector2(5, 26);
        private Vector2 buttonPeoplePos = new Vector2(5, 36);
        private Vector2 buttonThingsPos = new Vector2(5, 46);
        private Vector2 buttonWorkPos = new Vector2(5, 56);
        private Vector2 buttonSize = new Vector2(70, 10f);

        private Rect rectButtonLocation = new Rect(5, 26, 70, 10f);
        private Rect rectButtonPeople = new Rect(5, 36, 70, 10f);
        private Rect rectButtonThings = new Rect(5, 46, 70, 10f);
        private Rect rectButtonWork = new Rect(5, 56, 70, 10f);

        private Rect rectButtonTellMe = new Rect(4f, 4f, 107, 10);

        private Panel panelNameNPC;

        private Vector2 buttonReturnPos = new Vector2(120, 140);
        private Vector2 buttonReturnSize = new Vector2(16, 14);
        private Vector2 buttonDummyLeftPos = new Vector2(120, 140);
        private Vector2 buttonDummyLeftSize = new Vector2(16, 14);
        private Vector2 buttonWhereIsBlockPos = new Vector2(5, 13.6f);
        private Vector2 buttonWhereIsBlockSize = new Vector2(105, 10);
        private Vector2 buttonTellMeBlockPos = new Vector2(5, 3.6f);
        private Vector2 buttonTellMeBlockSize = new Vector2(105, 10);



        private Texture2D textureLeftGreyButton;
        private Texture2D arrowTopicUpRed;
        private Texture2D arrowTopicDownRed;
        private Texture2D arrowTopicLeftRed;
        private Texture2D arrowTopicRightRed;
        private Texture2D arrowTopicUpGreen;
        private Texture2D arrowTopicDownGreen;
        private Texture2D arrowTopicLeftGreen;
        private Texture2D arrowTopicRightGreen;
        private Texture2D arrowConversationUpRed;
        private Texture2D arrowConversationDownRed;
        private Texture2D arrowConversationUpGreen;
        private Texture2D arrowConversationDownGreen;

        private Rect rectButtonTopicUp = new Rect(102, 69, 9, 16);
        private Rect rectButtonTopicDown = new Rect(102, 161, 9, 16);
        private Rect rectButtonTopicLeft = new Rect(4, 177, 16, 9);
        private Rect rectButtonTopicRight = new Rect(86, 177, 16, 9);
        private Rect rectButtonConversationUp = new Rect(303, 64, 9, 16);
        private Rect rectButtonConversationDown = new Rect(303, 176, 9, 16);

        private DFSize arrowsFullSize = new DFSize(9, 152);
        private Rect upArrowRectInSrcImg = new Rect(0, 0, 9, 16);
        private Rect downArrowRectInSrcImg = new Rect(0, 136, 9, 16);

        private PlayerGPS.DiscoveredBuilding buildingData;
        private RoomRental_v1 rentedRoom;
        private int daysToRent = 0;
        private int tradePrice = 0;



        private Color textcolorHighlighted = Color.yellow; // Define appropriate color
        private Color textcolorAnswerBackgroundModernConversationStyle = Color.gray; // Define appropriate color
        private TalkTone selectedTalkTone = TalkTone.Normal;
        private CustomTalkManagerMod.CustomTalkManager customTalkManager;

        #endregion
        public enum TalkTone
        {
            Polite,
            Normal,
            Blunt
        }

        public CustomDaggerfallTalkWindow(IUserInterfaceManager uiManager, DaggerfallBaseWindow previousWindow, CustomTalkManagerMod.CustomTalkManager customTalkManager)
      : base(uiManager)
        {
            this.uiManager = uiManager;
            this.previousWindow = previousWindow;
            this.customTalkManager = customTalkManager;
            Setup();
        }

        public void CloseWindow()
        {
            // Logic to close the window
            if (uiManager != null)
            {
                uiManager.PopWindow();
            }

            // Invoke the OnCloseWindow event if there are subscribers
            OnCloseWindow?.Invoke();
            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.3f);
            // Perform any additional cleanup or state resetting if needed
            if (talkManagerCoroutine != null)
            {
                DaggerfallUI.Instance.StopCoroutine(talkManagerCoroutine);
                talkManagerCoroutine = null;
            }

            // Optionally, perform garbage collection
            DaggerfallGC.ThrottledUnloadUnusedAssets();
        }

        private void ButtonTopicUp_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.3f);
            listboxTopics.ScrollUp();
            UpdateScrollBarConversation();
            UpdateScrollButtonsConversation();
        }

        private void ButtonTopicDown_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.3f);
            listboxTopics.ScrollDown();
            UpdateScrollBarConversation();
            UpdateScrollButtonsConversation();
        }

        private void ButtonTopicLeft_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            // Implement left scroll behavior
        }

        private void ButtonTopicRight_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            // Implement right scroll behavior
        }

        private void ButtonConversationUp_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.3f);
            listboxConversation.ScrollUp();
            UpdateScrollBarConversation();
            UpdateScrollButtonsConversation();
        }

        private void ButtonConversationDown_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.3f);
            listboxConversation.ScrollDown();
            UpdateScrollBarConversation();
            UpdateScrollButtonsConversation();
        }

        private void UpdateScrollBarConversation()
        {
            int scrollIndex = listboxConversation.ScrollIndex;
            verticalScrollBarConversation.SetScrollIndexWithoutRaisingScrollEvent(scrollIndex);
            verticalScrollBarConversation.Update();
        }


        private void UpdateListConversationScrollerButtons(VerticalScrollBar verticalScrollBar, int index, int count, Button upButton, Button downButton)
        {
            upButton.BackgroundTexture = index > 0 ? arrowConversationUpGreen : arrowConversationUpRed;
            downButton.BackgroundTexture = index < count - 1 ? arrowConversationDownGreen : arrowConversationDownRed;
        }

        public bool PauseWhileOpen { get; set; }  // Add this property


        // Use this for initialization
        void Start()
        {
            PlayerEnterExit.OnTransitionExterior += OnTransitionToExterior;
            Setup();
        }

        public void SetMacroDataSource(MacroDataSource macroDataSource)
        {
            this.macroDataSource = macroDataSource;
        }

        protected void Setup()
        {
            ParentPanel.BackgroundColor = Color.clear;

            textureBackground = DaggerfallUI.GetTextureFromImg(talkWindowImgName, TextureFormat.ARGB32, false);
            textureBackground.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
            if (!textureBackground)
            {
                Debug.LogError($"Failed to load background image {talkWindowImgName} for talk window");
                CloseWindow();
                return;
            }

            mainPanel = DaggerfallUI.AddPanel(ParentPanel, AutoSizeModes.None);
            mainPanel.BackgroundTexture = textureBackground;
            mainPanel.Size = new Vector2(textureBackground.width, textureBackground.height);
            mainPanel.HorizontalAlignment = HorizontalAlignment.Center;
            mainPanel.VerticalAlignment = VerticalAlignment.Middle;
            mainPanel.BackgroundColor = Color.black;

            ParentPanel.Components.Add(mainPanel);

            // Calculate the scale factor based on the player's display size
            scaleFactor = Mathf.Min(Screen.width / 320f, Screen.height / 200f); // Initialize scaleFactor

            SetupPanels();
            SetupListboxes();
            SetupPlayerSaysPanel();
            SetupCheckboxes();
            SetupButtons();
            SetupScrollBars();
            SetupScrollButtons();
            UpdateCustomNameNPC();
            StartDialogue();

            AddClickableArea(new Rect(5, 4, 107, 9), HandleTellMe);
            AddClickableArea(new Rect(5, 14, 107, 9), HandleWhereIs);
            AddClickableArea(new Rect(4, 186, 107, 10), OnOkayClick);
            AddClickableArea(new Rect(118, 183, 67, 10), CloseWindow);

            ScalePanelAndChildren(mainPanel, scaleFactor);

            // Pass scaleFactor to other methods

            SetUpDummy(scaleFactor);
 
            if (isChildNPC)
            {
                SetUpBlockers(scaleFactor);
            }
            else
            {
                SetUpBlocker(scaleFactor);
            }
        }


        private void OnOkayClick()
        {
            if (isListboxTopicsClicked)
                return;

            ListboxTopics_OnUseSelectedItem();
            isListboxTopicsClicked = true;
            DaggerfallUI.Instance.StartCoroutine(ResetListboxTopicsClickedFlag());
        }

        private void AddClickableArea(Rect rect, System.Action onClickAction)
        {
            Button button = DaggerfallUI.AddButton(rect, mainPanel);
            button.BackgroundColor = Color.clear;
            button.OnMouseClick += (BaseScreenComponent sender, Vector2 position) => onClickAction();
        }

        private void ScalePanelAndChildren(BaseScreenComponent component, float scaleFactor, bool initialCall = true)
        {
            if (!initialCall && component.Tag != null && component.Tag.ToString() == "Scaled")
            {
                return;
            }

            Debug.Log($"Scaling component: {component.Name} with scaleFactor: {scaleFactor}");
            Debug.Log($"Before scaling - {component.Name} Position: {component.Position}, Size: {component.Size}");

            component.Position *= scaleFactor;

            if (component is Panel panel)
            {
                panel.Size *= scaleFactor;
                foreach (var child in panel.Components)
                {
                    ScalePanelAndChildren(child, scaleFactor, false);
                }
            }
            else if (component is Button button)
            {
                button.Size *= scaleFactor;
                Debug.Log($"Button {component.Name} scaled to - Position: {button.Position}, Size: {button.Size}");
            }
            else if (component is TextLabel label)
            {
                label.Size *= scaleFactor;
                label.TextScale *= scaleFactor;
                Debug.Log($"TextLabel {component.Name} scaled to - Position: {label.Position}, Size: {label.Size}, TextScale: {label.TextScale}");
            }
            else if (component is ListBox listBox)
            {
                listBox.Size *= scaleFactor;
                listBox.TextScale *= scaleFactor;
                Debug.Log($"ListBox {component.Name} scaled to - Position: {listBox.Position}, Size: {listBox.Size}, TextScale: {listBox.TextScale}");
            }

            component.Tag = "Scaled";

            Debug.Log($"After scaling - {component.Name} Position: {component.Position}, Size: {component.Size}");
        }

        public override void Draw()
        {
            base.Draw();
            if (mainPanel != null)
            {
                mainPanel.Draw();
            }
        }

        public override void Update()
        {
            base.Update();
            if (mainPanel != null)
            {
                mainPanel.Update();
            }

            if (Input.GetKeyUp(KeyCode.Escape))
            {
                CloseWindow();
            }
        }

        protected virtual void SetupPanels()
        {
            panelPortrait = DaggerfallUI.AddPanel(new Rect(panelPortraitPos, panelPortraitSize), mainPanel);
            if (panelPortrait == null)
            {
                Debug.LogError("Failed to create panelPortrait.");
                return;
            }
            else
            {
                Debug.Log("panelPortrait created successfully.");
            }

            panelPortrait.BackgroundTexture = texturePortrait; // Ensure this line is added

            panelNameNPC = DaggerfallUI.AddPanel(mainPanel, AutoSizeModes.None);
            panelNameNPC.Position = new Vector2(90, 12);
            panelNameNPC.Size = new Vector2(250, 90);

            labelNameNPC = new TextLabel();
            labelNameNPC.Position = new Vector2(0, 0);
            labelNameNPC.Size = new Vector2(50, 10);
            labelNameNPC.Name = "label_npcName";
            labelNameNPC.MaxCharacters = -1;
            labelNameNPC.TextScale = 1.2f;
            labelNameNPC.TextColor = Color.white;
            labelNameNPC.HorizontalAlignment = HorizontalAlignment.Center;
            labelNameNPC.VerticalAlignment = VerticalAlignment.Middle;
            panelNameNPC.Components.Add(labelNameNPC);

            mainPanel.Components.Add(panelNameNPC); // Add to mainPanel
        }

        protected virtual void SetupListboxes()
        {

            listboxTopics = new ExtendedListBox();
            listboxTopics.Position = new Vector2(6, 71);
            listboxTopics.Size = new Vector2(95, 104);
            listboxTopics.WrapWords = false;
            listboxTopics.WrapTextItems = false;
            listboxTopics.RowSpacing = 3;
            listboxTopics.MaxCharacters = -1;
            listboxTopics.RowsDisplayed = 13;
            listboxTopics.RectRestrictedRenderArea = new Rect(listboxTopics.Position, listboxTopics.Size);
            listboxTopics.RestrictedRenderAreaCoordinateType = BaseScreenComponent.RestrictedRenderArea_CoordinateType.ParentCoordinates;
            listboxTopics.VerticalScrollMode = ListBox.VerticalScrollModes.EntryWise;
            listboxTopics.SelectedShadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos;
            listboxTopics.OnMouseClick += (BaseScreenComponent sender, Vector2 position) => ListboxTopics_OnMouseClick(position);
            listboxTopics.OnMouseMove += ListboxTopics_OnMouseMove;
            mainPanel.Components.Add(listboxTopics);

            listboxConversation = new ExtendedListBox();
            listboxConversation.Position = new Vector2(189, 65);
            listboxConversation.Size = new Vector2(114, 126);
            listboxConversation.RowSpacing = 4;
            listboxConversation.MaxCharacters = -1;
            listboxConversation.WrapTextItems = true;
            listboxConversation.WrapWords = true;
            listboxConversation.RectRestrictedRenderArea = new Rect(listboxConversation.Position, listboxConversation.Size);
            listboxConversation.RestrictedRenderAreaCoordinateType = BaseScreenComponent.RestrictedRenderArea_CoordinateType.ParentCoordinates;
            listboxConversation.VerticalScrollMode = ListBox.VerticalScrollModes.PixelWise;
            listboxConversation.SelectedShadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos;
            listboxConversation.OnScroll += ListBoxConversation_OnScroll;
            listboxConversation.OnMouseClick += (BaseScreenComponent sender, Vector2 position) => ListboxConversation_OnMouseClick(position);
            listboxConversation.OnMouseDoubleClick += (BaseScreenComponent sender, Vector2 position) => ListboxConversation_OnMouseDoubleClick(position);
            mainPanel.Components.Add(listboxConversation);

            PopulateTopics();
        }

        private void ListboxTopics_OnMouseMove(int x, int y)
        {
            var extendedListboxTopics = listboxTopics as ExtendedListBox;
            if (extendedListboxTopics == null || extendedListboxTopics.Count == 0)
                return;

            extendedListboxTopics.HighlightedIndex = -1;
            if (extendedListboxTopics.VerticalScrollMode == ListBox.VerticalScrollModes.EntryWise)
            {
                int rowHeight = (int)(((extendedListboxTopics.Font.GlyphHeight * extendedListboxTopics.TextScale) + extendedListboxTopics.RowSpacing) / 1);
                int row = (int)(y / rowHeight);
                int index = extendedListboxTopics.ScrollIndex + row;
                if (index >= 0 && index < extendedListboxTopics.Count)
                {
                    extendedListboxTopics.HighlightedIndex = index;
                }
            }
            else if (extendedListboxTopics.VerticalScrollMode == ListBox.VerticalScrollModes.PixelWise)
            {
                int yCurrentItem = 0;
                int yNextItem = 0;
                for (int i = 0; i < extendedListboxTopics.Count; i++)
                {
                    yNextItem = yCurrentItem + extendedListboxTopics.ListItems[i].textLabel.TextHeight + extendedListboxTopics.RowSpacing;
                    int yVal = extendedListboxTopics.ScrollIndex + y;
                    if (yVal >= yCurrentItem - extendedListboxTopics.RowSpacing * 0.5 && yVal < yNextItem - extendedListboxTopics.RowSpacing * 0.5)
                    {
                        extendedListboxTopics.HighlightedIndex = i;
                        break;
                    }
                    yCurrentItem = yNextItem;
                }
            }
        }


        protected virtual void PopulateTopics()
        {
            if (listboxTopics != null)
            {
                listboxTopics.ClearItems();
                var customNpc = CustomTalkManagerMod.CustomTalkManager.Instance.GetTargetCustomNPC();
                if (isChildNPC)
                {
                    listboxTopics.AddItem("Play a game", out ListBox.ListItem item1);
                    return;
                }

                if (customNpc != null)
                {
                    listboxTopics.AddItem("A place to stay", out ListBox.ListItem item1);

                    listboxTopics.AddItem("My family", out ListBox.ListItem item2);
                }

                // Log the order of items
                for (int i = 0; i < listboxTopics.Count; i++)
                {
                    Debug.Log($"Item {i}: {listboxTopics.GetItem(i).textLabel.Text}");
                }
            }
        }

        protected virtual void SetupPlayerSaysPanel()
        {
            playerSaysListBox = new ListBox();
            playerSaysListBox.Position = new Vector2(124, 8);
            playerSaysListBox.Size = new Vector2(122, 60);
            playerSaysListBox.MaxCharacters = -1;
            playerSaysListBox.WrapTextItems = true;
            playerSaysListBox.WrapWords = true;

            mainPanel.Components.Add(playerSaysListBox); // Add to mainPanel
        }

        protected void HandleLocation()
        {
            if (isListboxTopicsClicked)
                return;
            if (TellMeClicked)
                return;
            if (StartScreen)
                return;

            if (PeopleClicked)
            {
                buttonPeople.Enabled = true;
                PeopleClicked = false;
            }
            if (ThingsClicked)
            {
                buttonThings.Enabled = true;
                ThingsClicked = false;
            }
            if (WorkClicked)
            {
                buttonWork.Enabled = true;
                WorkClicked = false;
            }

            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.5f);
            listboxTopics.ClearItems();
            List<Button> buttons = new List<Button> { buttonLocation };

            foreach (var button in buttons)
            {
                // Disable the button
                button.Enabled = false;
            }

            var vanillaLocation = CustomTalkManagerMod.CustomTalkManager.Instance.GetVanillaLocationTopics();
            Debug.Log("Location Clicked");
            LocationClicked = true;
            isListboxTopicsClicked = true;

            // Populate the listbox with location categories
            foreach (var topic in vanillaLocation)
            {
                listboxTopics.AddItem(topic.caption);
            }

            DaggerfallUI.Instance.StartCoroutine(ResetListboxTopicsClickedFlag());
        }

        private void HandlePeople()
        {
            if (isListboxTopicsClicked)
                return;
            if (TellMeClicked)
                return;
            if (StartScreen)
                return;

            if (LocationClicked)
            {
                buttonLocation.Enabled = true;
                LocationClicked = false;
            }
            if (ThingsClicked)
            {
                buttonThings.Enabled = true;
                ThingsClicked = false;
            }
            if (WorkClicked)
            {
                buttonWork.Enabled = true;
                WorkClicked = false;
            }

            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.5f);
            buttonPeople.Enabled = false;


            PeopleClicked = true;
            isListboxTopicsClicked = true;
            DaggerfallUI.Instance.StartCoroutine(ResetListboxTopicsClickedFlag());
        }

        private void HandleThings()
        {
            if (isListboxTopicsClicked)
                return;
            if (TellMeClicked)
                return;
            if (StartScreen)
                return;

            if (LocationClicked)
            {
                buttonLocation.Enabled = true;
                LocationClicked = false;
            }
            if (PeopleClicked)
            {
                buttonPeople.Enabled = true;
                PeopleClicked = false;
            }
            if (WorkClicked)
            {
                buttonWork.Enabled = true;
                WorkClicked = false;
            }

            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.5f);
            buttonThings.Enabled = false;

            ThingsClicked = true;
            isListboxTopicsClicked = true;
            DaggerfallUI.Instance.StartCoroutine(ResetListboxTopicsClickedFlag());
        }

        private void HandleWork()
        {
            if (isListboxTopicsClicked)
                return;
            if (TellMeClicked)
                return;
            if (StartScreen)
                return;

            if (LocationClicked)
            {
                buttonLocation.Enabled = true;
                LocationClicked = false;
            }
            if (PeopleClicked)
            {
                buttonPeople.Enabled = true;
                PeopleClicked = false;
            }
            if (ThingsClicked)
            {
                buttonThings.Enabled = true;
                ThingsClicked = false;
            }

            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.5f);
            buttonWork.Enabled = false;

            WorkClicked = true;
            isListboxTopicsClicked = true;
            DaggerfallUI.Instance.StartCoroutine(ResetListboxTopicsClickedFlag());
        }

        private bool LocationClicked = false;
        private bool PeopleClicked = false;
        private bool ThingsClicked = false;
        private bool WorkClicked = false;
        private bool TellMeClicked = false;
        private bool WhereIsClicked = false;

        protected void SetupButtons()
        {
            textureCategoryGrayedOut = DaggerfallUI.GetTextureFromImg("TALK02I0.IMG", TextureFormat.ARGB32, false);

            // Initialize buttons without scaling
            buttonLocation = CreateSubheaderButton(
                buttonLocationPos,
                buttonSize,
                new Rect(0, 0, 70, 10),
                HandleLocation, Color.red, "buttonLocation");

            buttonPeople = CreateSubheaderButton(
                buttonPeoplePos,
                buttonSize,
                new Rect(0, 10, 70, 10),
                HandlePeople, Color.gray, "buttonPeople");

            buttonThings = CreateSubheaderButton(
                buttonThingsPos,
                buttonSize,
                new Rect(0, 20, 70, 10),
                HandleThings, Color.blue, "buttonThings");

            buttonWork = CreateSubheaderButton(
                buttonWorkPos,
                buttonSize,
                new Rect(0, 30, 70, 10),
                HandleWork, Color.gray, "buttonWork");

            mainPanel.Components.Add(buttonLocation);
            mainPanel.Components.Add(buttonPeople);
            mainPanel.Components.Add(buttonThings);
            mainPanel.Components.Add(buttonWork);

            Debug.Log("Buttons setup completed.");
        }

        protected Button CreateSubheaderButton(Vector2 position, Vector2 size, Rect textureRect, Action clickHandler, Color backgroundColor, string componentName)
        {
            Button button = DaggerfallUI.AddButton(new Rect(position, size), mainPanel);
            button.Size = size;
            button.Name = componentName;
            Texture2D subTexture = ImageReader.GetSubTexture(textureCategoryGrayedOut, textureRect);
            if (subTexture == null)
            {
                Debug.LogError($"Failed to get subTexture from TALK02I0.IMG for {componentName}");
                return null;
            }
            button.BackgroundTexture = subTexture;
            button.BackgroundColor = backgroundColor;
            button.OnMouseClick += (BaseScreenComponent sender, Vector2 buttonPosition) => clickHandler();
            Debug.Log($"Button {componentName} created at (x:{position.x:0.00}, y:{position.y:0.00}, width:{size.x:0.00}, height:{size.y:0.00}) with background color RGBA({backgroundColor.r:0.000}, {backgroundColor.g:0.000}, {backgroundColor.b:0.000}, {backgroundColor.a:0.000})");
            return button;
        }

        protected void SetupScrollBars()
        {
            // Setup vertical scrollbar for conversation list
            verticalScrollBarConversation = new VerticalScrollBar();
            verticalScrollBarConversation.Position = new Vector2(305, 81);
            verticalScrollBarConversation.Size = new Vector2(1, 1);
            verticalScrollBarConversation.OnScroll += ListBoxConversation_OnScroll;
            mainPanel.Components.Add(verticalScrollBarConversation);
            Debug.Log("Setup verticalScrollBarConversation");

        }

        private void VerticalScrollBarTopics_OnScroll()
        {
            Debug.Log("VerticalScrollBarTopics_OnScroll called");
        }

        private void HorizontalSliderTopics_OnScroll()
        {
            int scrollIndex = horizontalSliderTopics.ScrollIndex;

            // Update the listbox horizontal scroll index and refresh its display
            listboxTopics.HorizontalScrollIndex = scrollIndex;
            listboxTopics.Update();
        }

        private void ListBoxConversation_OnScroll()
        {
            int scrollIndex = listboxConversation.ScrollIndex;

            // Update scroller
            verticalScrollBarConversation.SetScrollIndexWithoutRaisingScrollEvent(scrollIndex);
            verticalScrollBarConversation.Update();

            // Update scroller buttons
            UpdateListConversationScrollerButtons(verticalScrollBarConversation, scrollIndex, listboxConversation.HeightContent(), buttonConversationUp, buttonConversationDown);
        }

        private void UpdateScrollButtonsTopics()
        {
            int scrollIndex = listboxTopics.ScrollIndex;
            int itemCount = listboxTopics.Count;

            UpdateListTopicScrollerButtons(verticalScrollBarTopics, scrollIndex, itemCount, buttonTopicUp, buttonTopicDown);
            UpdateListTopicScrollerButtonsLeftRight(horizontalSliderTopics, horizontalSliderTopics.ScrollIndex, listboxTopics.WidthContent(), buttonTopicLeft, buttonTopicRight);
        }

        private void UpdateScrollButtonsConversation()
        {
            int scrollIndex = listboxConversation.ScrollIndex;
            int itemCount = listboxConversation.Count;

            UpdateListConversationScrollerButtons(verticalScrollBarConversation, scrollIndex, itemCount, buttonConversationUp, buttonConversationDown);
        }

        private void UpdateListTopicScrollerButtons(VerticalScrollBar verticalScrollBar, int index, int count, Button upButton, Button downButton)
        {
            upButton.BackgroundTexture = index > 0 ? arrowTopicUpGreen : arrowTopicUpRed;
            downButton.BackgroundTexture = index < count - verticalScrollBar.DisplayUnits ? arrowTopicDownGreen : arrowTopicDownRed;
        }

        private void UpdateListTopicScrollerButtonsLeftRight(HorizontalSlider horizontalSlider, int index, int count, Button leftButton, Button rightButton)
        {
            leftButton.BackgroundTexture = index > 0 ? arrowTopicLeftGreen : arrowTopicLeftRed;
            rightButton.BackgroundTexture = index < count - horizontalSlider.DisplayUnits ? arrowTopicRightGreen : arrowTopicRightRed;
        }

        protected void SetUpReturn(float scaleFactor)
        {
            buttonDummyLeft.Enabled = false;
            bool assetLoaded = ModManager.Instance.TryGetAsset("LeftYellow.png", false, out textureLeftYellowButton);
            buttonReturn = CreateBlockers(buttonReturnPos, buttonReturnSize, new Rect(0, 0, 16.5f, 15), textureLeftYellowButton, HandleReturn, "buttonReturn");
            mainPanel.Components.Add(buttonReturn);

            // Scale the newly added button
            ScalePanelAndChildren(buttonReturn, scaleFactor, false);
        }

        private float scaleFactor;

        private void HandleReturn()
        {
            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.6f);
            buttonReturn.Enabled = false;
            playerSaysListBox.ClearItems();
            listboxTopics.ClearItems();
            if (TellMeClicked)
            {
                buttonTellMe.Enabled = false;
                mainPanel.Components.Remove(buttonTellMe);
                buttonTellMe = null; // Ensure buttonTellMe is set to null
            }
            PopulateTopics();
            TellMeClicked = false;
            StartScreen = true;
            buttonDummyLeft.Enabled = true;
        }

        protected void SetUpDummy(float scaleFactor)
        {
            bool assetLoaded = ModManager.Instance.TryGetAsset("LeftGrey.png", false, out textureLeftGreyButton);
            buttonDummyLeft = CreateBlockers(buttonDummyLeftPos, buttonDummyLeftSize, new Rect(0, 0, 16.5f, 15), textureLeftGreyButton, DummyButtonAction, "buttonDummyLeft");
            mainPanel.Components.Add(buttonDummyLeft);

            // Scale the newly added button
            ScalePanelAndChildren(buttonDummyLeft, scaleFactor, false);
        }

        protected void SetUpBlocker(float scaleFactor)
        {
            bool assetLoaded = ModManager.Instance.TryGetAsset("GreyHeaderButtons.png", false, out textureGreyHeaderButtons);
            buttonWhereIsBlock = CreateBlockers(buttonWhereIsBlockPos, buttonWhereIsBlockSize, new Rect(0, 10, 105, 10), textureGreyHeaderButtons, DummyButtonAction, "buttonWhereIsBlock");
            mainPanel.Components.Add(buttonWhereIsBlock);

            // Scale the newly added button
            ScalePanelAndChildren(buttonWhereIsBlock, scaleFactor, false);
        }


        protected void SetUpBlockers(float scaleFactor)
        {
            bool assetLoaded = ModManager.Instance.TryGetAsset("GreyHeaderButtons.png", false, out textureGreyHeaderButtons);

            if (!assetLoaded || textureGreyHeaderButtons == null)
            {
                Debug.LogError("Failed to load custom texture asset 'GreyHeaderButtons.png'.");
                return;
            }

            Debug.Log("Texture loaded successfully");

            buttonTellMeBlock = CreateBlockers(buttonTellMeBlockPos, buttonTellMeBlockSize, new Rect(0, 0, 105, 10), textureGreyHeaderButtons, DummyButtonAction, "buttonTellMeBlock");
            buttonWhereIsBlock = CreateBlockers(buttonWhereIsBlockPos, buttonWhereIsBlockSize, new Rect(0, 10, 105, 10), textureGreyHeaderButtons, DummyButtonAction, "buttonWhereIsBlock");

            mainPanel.Components.Add(buttonTellMeBlock);
            mainPanel.Components.Add(buttonWhereIsBlock);

            // Scale the newly added buttons
            ScalePanelAndChildren(buttonTellMeBlock, scaleFactor, false);
            ScalePanelAndChildren(buttonWhereIsBlock, scaleFactor, false);

            Debug.Log("Buttons added to main panel");
        }

        // Example action methods
        private void DummyButtonAction()
        {
            Debug.Log("PINGASSSSS!");
        }

        protected Button CreateBlockers(Vector2 position, Vector2 size, Rect textureRect, Texture2D texture, Action onClickAction, string componentName)
        {
            Debug.Log("CreateBlockers called with position: " + position + " and size: " + size);
            Button button = DaggerfallUI.AddButton(new Rect(position, size), mainPanel);
            button.Size = size;
            button.Name = componentName;
            button.BackgroundTexture = ImageReader.GetSubTexture(texture, textureRect);
            button.OnMouseClick += (BaseScreenComponent sender, Vector2 buttonPosition) => onClickAction?.Invoke();
            Debug.Log("Button " + componentName + " position: " + button.Position + ", size: " + button.Size);
            return button;
        }

        protected void SetupScrollButtons()
        {
            Texture2D redArrowsTexture = ImageReader.GetTexture(redArrowsTextureName);
            arrowTopicUpRed = ImageReader.GetSubTexture(redArrowsTexture, upArrowRectInSrcImg, arrowsFullSize);
            arrowTopicDownRed = ImageReader.GetSubTexture(redArrowsTexture, downArrowRectInSrcImg, arrowsFullSize);
            Texture2D greenArrowsTexture = ImageReader.GetTexture(greenArrowsTextureName);
            arrowTopicUpGreen = ImageReader.GetSubTexture(greenArrowsTexture, upArrowRectInSrcImg, arrowsFullSize);
            arrowTopicDownGreen = ImageReader.GetSubTexture(greenArrowsTexture, downArrowRectInSrcImg, arrowsFullSize);
            arrowTopicLeftRed = CreateRotatedTexture(arrowTopicDownRed, true);
            arrowTopicRightRed = CreateRotatedTexture(arrowTopicDownRed, false);
            arrowTopicLeftGreen = CreateRotatedTexture(arrowTopicUpGreen, true);
            arrowTopicRightGreen = CreateRotatedTexture(arrowTopicDownGreen, false);
            arrowConversationUpRed = ImageReader.GetSubTexture(redArrowsTexture, upArrowRectInSrcImg, arrowsFullSize);
            arrowConversationDownRed = ImageReader.GetSubTexture(redArrowsTexture, downArrowRectInSrcImg, arrowsFullSize);
            arrowConversationUpGreen = ImageReader.GetSubTexture(greenArrowsTexture, upArrowRectInSrcImg, arrowsFullSize);
            arrowConversationDownGreen = ImageReader.GetSubTexture(greenArrowsTexture, downArrowRectInSrcImg, arrowsFullSize);
            buttonTopicUp = DaggerfallUI.AddButton(rectButtonTopicUp, mainPanel);
            buttonTopicUp.BackgroundTexture = arrowTopicUpRed;
            buttonTopicUp.OnMouseClick += ButtonTopicUp_OnMouseClick;
            buttonTopicDown = DaggerfallUI.AddButton(rectButtonTopicDown, mainPanel);
            buttonTopicDown.BackgroundTexture = arrowTopicDownRed;
            buttonTopicDown.OnMouseClick += ButtonTopicDown_OnMouseClick;
            buttonTopicLeft = DaggerfallUI.AddButton(rectButtonTopicLeft, mainPanel);
            buttonTopicLeft.BackgroundTexture = arrowTopicLeftRed;
            buttonTopicLeft.OnMouseClick += ButtonTopicLeft_OnMouseClick;
            buttonTopicRight = DaggerfallUI.AddButton(rectButtonTopicRight, mainPanel);
            buttonTopicRight.BackgroundTexture = arrowTopicRightRed;
            buttonTopicRight.OnMouseClick += ButtonTopicRight_OnMouseClick;
            buttonConversationUp = DaggerfallUI.AddButton(rectButtonConversationUp, mainPanel);
            buttonConversationUp.BackgroundTexture = arrowConversationUpRed;
            buttonConversationUp.OnMouseClick += ButtonConversationUp_OnMouseClick;
            buttonConversationDown = DaggerfallUI.AddButton(rectButtonConversationDown, mainPanel);
            buttonConversationDown.BackgroundTexture = arrowConversationDownRed;
            buttonConversationDown.OnMouseClick += ButtonConversationDown_OnMouseClick;
        }

        private Texture2D CreateRotatedTexture(Texture2D originalTexture, bool flipHorizontally)
        {
            Color32[] colors = originalTexture.GetPixels32();
            Color32[] rotated = ImageProcessing.RotateColors(ref colors, originalTexture.height, originalTexture.width);
            Texture2D rotatedTexture = new Texture2D(originalTexture.height, originalTexture.width, TextureFormat.ARGB32, false);
            if (flipHorizontally)
                rotatedTexture.SetPixels32(ImageProcessing.FlipHorizontallyColors(ref rotated, rotatedTexture.width, rotatedTexture.height), 0);
            else
                rotatedTexture.SetPixels32(rotated);
            rotatedTexture.Apply(false);
            rotatedTexture.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
            return rotatedTexture;
        }

        private bool firstTopicSelected = true;
        private static string lastNpcDisplayName = string.Empty;

        public override void OnPush()
        {
            base.OnPush();
            UpdateCustomNameNPC();
            UpdateCustomNPCPortrait();
            StartDialogue();
            PopulateTopics();
            SetDefaultTone();
            UpdateGreeting();

            float scaleFactor = Mathf.Min(Screen.width / 320f, Screen.height / 200f); // Calculate the scale factor

            if (isChildNPC)
            {
                SetUpBlockers(scaleFactor); // Pass scaleFactor
            }
            else
            {
                SetUpBlocker(scaleFactor); // Pass scaleFactor
            }
            SetUpDummy(scaleFactor); // Pass scaleFactor


            // Generate hash for NPC's display name and house ID
            var customNpc = CustomTalkManagerMod.CustomTalkManager.Instance.GetTargetCustomNPC();
            if (customNpc != null)
            {
                string npcDisplayName = customNpc.CustomDisplayName;
                int houseID = GetCurrentHouseID(); // Implement this method to get the current house ID
                npcTopicHash = GenerateHash(npcDisplayName, houseID);

                // Log the current and last NPC display names
                Debug.Log($"Current NPC Display Name: {npcDisplayName}");
                Debug.Log($"Last NPC Display Name: {lastNpcDisplayName}");

                // Check if the NPC display name is different from the last one
                if (npcDisplayName != lastNpcDisplayName)
                {
                    firstTopicSelected = false; // Reset the flag if the name is different
                    Debug.Log("firstTopicSelected set false");
                }

                // Update the last NPC display name
                lastNpcDisplayName = npcDisplayName;
            }

            // Assign the topic index based on the hash
            AssignTopicIndex();
        }

        private int GenerateHash(string npcDisplayName, int houseID)
        {
            string combinedString = npcDisplayName + houseID.ToString();
            int hash = combinedString.GetHashCode();
            return Math.Abs(hash); // Ensure the hash is positive
        }

        private void AssignTopicIndex()
        {
            if (npcTopicHash > 0)
            {
                // Ensure indexes 0 and 1 are always known
                selectedTopicIndex = 2 + (npcTopicHash % (NumVanillaTopics - 2));
            }
        }

        private bool StartScreen = false;

        void StartDialogue()
        {
            if (listboxConversation != null)
            {
                listboxConversation.ClearItems();
                ListBox.ListItem textLabelGreeting;
                int buildingKey = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData.buildingKey;
                var customNpc = CustomTalkManagerMod.CustomTalkManager.Instance.GetTargetCustomNPC();
                if (customNpc != null)
                {
                    bool isBuildingEmpty = CustomNPCBridgeMod.CustomNPCBridge.Instance.IsBuildingEmpty(buildingKey);

                    if (isBuildingEmpty)
                    {
                        // Handle the case where the building is empty
                        listboxConversation.AddItem("MURDERER! GET OUT YOU FUCKING MONSTER! AHH!", out textLabelGreeting);
                        textLabelGreeting.selectedTextColor = textcolorHighlighted;
                        textLabelGreeting.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
                        textLabelGreeting.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
                        textLabelGreeting.textColor = melon;
                        if (DaggerfallUnity.Settings.EnableModernConversationStyleInTalkWindow)
                        {
                            textLabelGreeting.textLabel.TextScale = textScaleModernConversationStyle;
                            textLabelGreeting.textLabel.MaxWidth = (int)(textLabelGreeting.textLabel.MaxWidth * textBlockSizeModernConversationStyle);
                            textLabelGreeting.textLabel.BackgroundColor = textcolorAnswerBackgroundModernConversationStyle;
                        }
                    }
                    else
                    {
                        Debug.Log($"StartDialogue: Found custom NPC with ID: {customNpc.GetInstanceID()}");
                        int npcId = customNpc.GetInstanceID();
                        string greeting = CustomTalkManagerMod.CustomTalkManager.Instance.GetGreeting(npcId);
                        listboxConversation.AddItem(greeting, out textLabelGreeting);
                        textLabelGreeting.selectedTextColor = textcolorHighlighted;
                        textLabelGreeting.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
                        textLabelGreeting.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
                        textLabelGreeting.textColor = melon;
                        if (DaggerfallUnity.Settings.EnableModernConversationStyleInTalkWindow)
                        {
                            textLabelGreeting.textLabel.TextScale = textScaleModernConversationStyle;
                            textLabelGreeting.textLabel.MaxWidth = (int)(textLabelGreeting.textLabel.MaxWidth * textBlockSizeModernConversationStyle);
                            textLabelGreeting.textLabel.BackgroundColor = textcolorAnswerBackgroundModernConversationStyle;
                        }
                        Debug.Log("Greeting added to listboxConversation: " + greeting);
                    }
                    StartScreen = true;
                    UpdateScrollBarConversation();
                    UpdateScrollButtonsConversation();
                    if (TalkManager.Instance != null)
                    {
                        TalkManager.Instance.enabled = false;
                        Debug.Log($"Vanilla TalkManager disabled");
                    }
                }
                else
                {
                    Debug.LogWarning("StartDialogue: Custom NPC is null.");
                }
            }
        }


        private void UpdateCustomNameNPC()
        {
            if (labelNameNPC != null)
            {
                var customNpc = CustomTalkManagerMod.CustomTalkManager.Instance.GetTargetCustomNPC();
                if (customNpc != null)
                {
                    Debug.Log($"Updating NPC name. Gender: {customNpc.Data.gender}");
                    labelNameNPC.Text = customNpc.CustomDisplayName;
                }
                else
                {
                    labelNameNPC.Text = "Unknown NPC";
                }
            }
        }

        private void UpdateCustomNPCPortrait()
        {
            if (panelPortrait == null)
            {
                Debug.LogError("panelPortrait is null in UpdateCustomNPCPortrait.");
                return;
            }

            var customNpc = customTalkManager.GetTargetCustomNPC();
            if (customNpc != null)
            {
                DaggerfallTalkWindow.FacePortraitArchive facePortraitArchive = DaggerfallTalkWindow.FacePortraitArchive.CommonFaces;
                int recordIndex;

                // Force portrait selection for specific billboard textures
                if (customNpc.Data.billboardArchiveIndex == 357) // Check if texture is 357.x
                {
                    recordIndex = 465; // Force record index to 465
                    Debug.Log($"Forced portrait selection: Texture 357 -> CommonFaces, Record Index: {recordIndex}");
                }
                else
                {
                    // Use default logic for other textures
                    GetPortraitIndexFromStaticNPCBillboard(customNpc, out facePortraitArchive, out recordIndex);
                }

                SetNPCPortrait(facePortraitArchive, recordIndex);

                Debug.Log($"customNpc.Data.billboardArchiveIndex: {customNpc.Data.billboardArchiveIndex}");
                Debug.Log($"recordIndex: {recordIndex}");

                isChildNPC = (customNpc.Data.billboardArchiveIndex == 182) &&
                             (recordIndex == 385 || recordIndex == 384 || recordIndex == 386 || recordIndex == 379 ||
                              recordIndex == 437 || recordIndex == 490 || recordIndex == 491 || recordIndex == 497 ||
                              recordIndex == 498 || recordIndex == 400 || recordIndex == 456 || recordIndex == 463 ||
                              recordIndex == 430);
                if (isChildNPC)
                {
                    Debug.Log("Child detected");
                    return;
                }
            }
            else
            {
                SetDefaultNPCPortrait();
            }
        }

        private static readonly Dictionary<int, int> billboardToRecordIndexMap = new Dictionary<int, int>
        {
        { 357, 465 }, // Example: Texture 357.1 -> Record Index 465
        // Add other mappings as needed
        };

        private void GetPortraitIndexFromStaticNPCBillboard(CustomStaticNPCMod.CustomStaticNPC customNpc, out DaggerfallTalkWindow.FacePortraitArchive facePortraitArchive, out int recordIndex)
        {
            if (billboardToRecordIndexMap.TryGetValue(customNpc.Data.billboardArchiveIndex, out recordIndex))
            {
                Debug.Log($"Mapping found for billboardArchiveIndex: {customNpc.Data.billboardArchiveIndex} -> Record Index: {recordIndex}");
            }
            else
            {
                Debug.LogWarning($"No mapping found for billboardArchiveIndex: {customNpc.Data.billboardArchiveIndex}. Using default logic.");
                // Fallback to default logic here
            }

            FactionFile.FactionData factionData;
            GameManager.Instance.PlayerEntity.FactionData.GetFactionData(customNpc.Data.factionID, out factionData);
            FactionFile.FlatData factionFlatData = FactionFile.GetFlatData(factionData.flat1);
            FactionFile.FlatData factionFlatData2 = FactionFile.GetFlatData(factionData.flat2);

            if (factionData.type == 4)
            {
                facePortraitArchive = (factionData.face > 60) ? DaggerfallTalkWindow.FacePortraitArchive.CommonFaces : DaggerfallTalkWindow.FacePortraitArchive.SpecialFaces;
                recordIndex = factionData.face;
                return;
            }
            facePortraitArchive = DaggerfallTalkWindow.FacePortraitArchive.CommonFaces;
            recordIndex = 410;
            FlatsFile.FlatData flatData;
            int archive = factionFlatData.archive;
            int record = factionFlatData.record;
            if (customNpc.Data.gender == Genders.Female)
            {
                archive = factionFlatData2.archive;
                record = factionFlatData2.record;
            }
            if (DaggerfallUnity.Instance.ContentReader.FlatsFileReader.GetFlatData(FlatsFile.GetFlatID(archive, record), out flatData))
            {
                recordIndex = flatData.faceIndex;
            }
            if (DaggerfallUnity.Instance.ContentReader.FlatsFileReader.GetFlatData(FlatsFile.GetFlatID(customNpc.Data.billboardArchiveIndex, customNpc.Data.billboardRecordIndex), out flatData))
            {
                recordIndex = flatData.faceIndex;
            }


        }

        private void SetDefaultNPCPortrait()
        {
            // Implement logic to set default NPC portrait if custom NPC is not available
        }

        private void SetNPCPortrait(DaggerfallTalkWindow.FacePortraitArchive facePortraitArchive, int recordId)
        {
            string imageName = facePortraitArchive == DaggerfallTalkWindow.FacePortraitArchive.CommonFaces ? "TFAC00I0.RCI" : FacesImgName;


            // Use ImageReader to get the image data
            var imageData = ImageReader.GetImageData(imageName, recordId, 0, false, true);

            if (imageData.type == ImageTypes.None || imageData.dfBitmap == null)
            {
                Debug.LogError($"Failed to load image data for {imageName} with record ID {recordId}");
                CloseWindow();
                return;
            }

            DFBitmap bitmap = imageData.dfBitmap;

            // Log the dimensions of the bitmap


            texturePortrait = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.ARGB32, false);
            Color32[] colors = bitmap.GetColor32(0);

            // Log the length of the colors array


            if (bitmap.Width * bitmap.Height == colors.Length)
            {
                texturePortrait.SetPixels32(colors);
                texturePortrait.Apply(false, false);
            }
            else
            {
                Debug.LogError("SetPixels32 called with invalid number of pixels in the array");
                Debug.LogError($"Expected: {bitmap.Width * bitmap.Height}, Got: {colors.Length}");
                CloseWindow();
                return;
            }

            texturePortrait.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
            if (!texturePortrait)
            {
                Debug.LogError(string.Format("Failed to load portrait image {0} for talk window", texturePortrait));
                CloseWindow();
                return;
            }
            if (panelPortrait != null)
            {
                panelPortrait.BackgroundTexture = texturePortrait;

            }
            else
            {
                Debug.LogError("panelPortrait is null.");
            }
        }


        private void UpdateGreeting()
        {
            var customNpc = CustomTalkManagerMod.CustomTalkManager.Instance.GetTargetCustomNPC();
            if (isChildNPC)
            {
                int npcId = customNpc.GetInstanceID();
                Debug.Log($"Child Greeting Called");
                listboxConversation.ClearItems();
                string greeting = "Hello adventurer!";
                ListBox.ListItem textLabelGreeting; // Declare the variable here
                listboxConversation.AddItem(greeting, out textLabelGreeting);
                textLabelGreeting.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
                textLabelGreeting.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
                textLabelGreeting.textColor = melon;
                if (DaggerfallUnity.Settings.EnableModernConversationStyleInTalkWindow)
                {
                    textLabelGreeting.textLabel.TextScale = textScaleModernConversationStyle;
                    textLabelGreeting.textLabel.MaxWidth = (int)(textLabelGreeting.textLabel.MaxWidth * textBlockSizeModernConversationStyle);
                    textLabelGreeting.textLabel.BackgroundColor = textcolorAnswerBackgroundModernConversationStyle;
                }
            }
            else
            {
                return;
            }
        }

        protected void SetupCheckboxes()
        {
            Color darkerGreen = new Color(0.0f, 0.4f, 0.0f);
            buttonCheckboxTonePolite = DaggerfallUI.AddButton(rectButtonTonePolite, mainPanel);
            buttonCheckboxToneNormal = DaggerfallUI.AddButton(rectButtonToneNormal, mainPanel);
            buttonCheckboxToneBlunt = DaggerfallUI.AddButton(rectButtonToneBlunt, mainPanel);
            panelTone = DaggerfallUI.AddPanel(new Rect(panelTonePolitePos, panelToneSize), mainPanel);
            panelTone.BackgroundColor = darkerGreen;
            DaggerfallUI.AddPanel(new Rect(panelToneNormalPos, panelToneSize), mainPanel).BackgroundColor = darkerGreen;
            DaggerfallUI.AddPanel(new Rect(panelToneBluntPos, panelToneSize), mainPanel).BackgroundColor = darkerGreen;
        }

        protected virtual void SetDefaultTone()
        {
            selectedTalkTone = TalkTone.Normal;
        }

        public void OnPop()
        {
            OnCloseWindow?.Invoke();
        }

        private bool isListboxTopicsClicked = false;
        private bool awaitingMouseDoubleClick = false;

        private void ListboxTopics_OnMouseClick(Vector2 position)
        {
            if (isListboxTopicsClicked)
                return;

            isListboxTopicsClicked = true;

            var extendedListboxTopics = listboxTopics as ExtendedListBox;
            if (extendedListboxTopics == null || extendedListboxTopics.Count == 0)
                return;

            if (extendedListboxTopics.VerticalScrollMode == ListBox.VerticalScrollModes.EntryWise)
            {
                int rowHeight = (int)(((extendedListboxTopics.Font.GlyphHeight * extendedListboxTopics.TextScale) + extendedListboxTopics.RowSpacing) / 1);
                int row = (int)(position.y / rowHeight);
                int calculatedIndex = extendedListboxTopics.ScrollIndex + row;
                if (calculatedIndex >= 0 && calculatedIndex < extendedListboxTopics.Count)
                {
                    extendedListboxTopics.SelectedIndex = calculatedIndex;
                    extendedListboxTopics.Update();
                    ListboxTopics_OnSelectItem(calculatedIndex); // Pass the calculated index to the selection method
                }
            }

            // Reset the flag after a short delay to allow for the next click
            DaggerfallUI.Instance.StartCoroutine(ResetListboxTopicsClickedFlag());

            // Handle double-click logic
            if (awaitingMouseDoubleClick)
            {
                ListboxTopics_OnUseSelectedItem();
            }
            else
            {
                awaitingMouseDoubleClick = true;
                DaggerfallUI.Instance.StartCoroutine(ResetAwaitingMouseDoubleClickFlag());
            }
        }

        private IEnumerator ResetListboxTopicsClickedFlag()
        {
            yield return new WaitForEndOfFrame();
            isListboxTopicsClicked = false;
        }

        private IEnumerator ResetAwaitingMouseDoubleClickFlag()
        {
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForEndOfFrame();
            }
            awaitingMouseDoubleClick = false;
        }

        Color lilac = new Color(0.698f, 0.812f, 1.0f);
        Color melon = new Color(1.0f, 0.75f, 0.45f);

        private void ListboxTopics_OnSelectItem(int manualIndex)
        {
            Debug.Log($"SelectItem Called, ManualIndex: {manualIndex}");

            if (manualIndex >= 0 && manualIndex < listboxTopics.Count)
            {
                // Verify the internal state of listboxTopics
                for (int i = 0; i < listboxTopics.Count; i++)
                {
                    Debug.Log($"Item {i}: {listboxTopics.GetItem(i).textLabel.Text}");
                }

                // Handle topic selection
                string selectedTopic = listboxTopics.GetItem(manualIndex).textLabel.Text;
                Debug.Log($"Selected Topic: {selectedTopic}");

                if (selectedTopic == "A place to stay")
                {
                    Debug.Log("A place to stay clicked");
                    playerSaysListBox.ClearItems();
                    playerSaysListBox.AddItem("I am weary from my travels. Might I take shelter here for a time?", out ListBox.ListItem playerRequestItem);
                    playerRequestItem.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
                    playerRequestItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
                    playerRequestItem.textColor = lilac; // Set the player's text color here
                }
                else if (selectedTopic == "My family")
                {
                    playerSaysListBox.ClearItems();
                    playerSaysListBox.AddItem("Could I enquire about your family?", out ListBox.ListItem playerInquiryItem);
                    playerInquiryItem.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
                    playerInquiryItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
                    playerInquiryItem.textColor = lilac; // Set the player's text color here
                }
                else if (selectedTopic == "Play a game")
                {
                    playerSaysListBox.ClearItems();
                    playerSaysListBox.AddItem("Would you like to play a game?", out ListBox.ListItem playerInquiryItem);
                    playerInquiryItem.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
                    playerInquiryItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
                    playerInquiryItem.textColor = lilac; // Set the player's text color here
                }
                else if (generatedAnimalTopics.Contains(selectedTopic))
                {
                    // Handle dynamically generated animal topics
                    string playerGuess = $"Is it a {selectedTopic}?";
                    playerSaysListBox.ClearItems();
                    playerSaysListBox.AddItem(playerGuess, out ListBox.ListItem playerGuessItem);
                    playerGuessItem.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
                    playerGuessItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
                    playerGuessItem.textColor = lilac;
                }
                else
                {
                    // Handle vanilla topics
                    var vanillaTopics = CustomTalkManager.Instance.GetVanillaTellMeAboutTopics();
                    foreach (var topic in vanillaTopics)
                    {
                        if (topic.caption == selectedTopic)
                        {
                            Debug.Log($"Vanilla Topic Selected");
                            string playerQuestion = CustomTalkManager.Instance.GetQuestionText(topic, DaggerfallTalkWindow.TalkTone.Normal);
                            playerSaysListBox.ClearItems();
                            playerSaysListBox.AddItem(playerQuestion, out ListBox.ListItem playerQuestionItem);
                            playerQuestionItem.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
                            playerQuestionItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
                            playerQuestionItem.textColor = lilac;
                            break;
                        }
                    }
                }
            }
        }

        private string GetOrganizationInfoResponse(TalkManager.ListItem listItem)
        {
            if (listItem == null || listItem.factionID == 0)
            {
                return "Invalid faction information.";
            }

            string response = TalkManager.Instance.GetOrganizationInfo(listItem);
            if (string.IsNullOrEmpty(response))
            {
                response = "No information available for this organization.";
            }

            return response;
        }

        private List<string> generatedAnimalTopics = new List<string>();

        private void ListboxTopics_OnUseSelectedItem()
        {
            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.7f);
            var extendedListboxTopics = listboxTopics as ExtendedListBox;
            if (extendedListboxTopics.SelectedIndex >= 0 && extendedListboxTopics.SelectedIndex < extendedListboxTopics.Count)
            {
                var customNpc = CustomTalkManagerMod.CustomTalkManager.Instance.GetTargetCustomNPC();

                // Ensure that the selected index is properly used
                string selectedTopic = extendedListboxTopics.GetItem(extendedListboxTopics.SelectedIndex).textLabel.Text;
                Debug.Log($"Selected Topic in UseSelectedItem: {selectedTopic}");

                if (selectedTopic == "A place to stay")
                {
                    HandlePlaceToStay();
                }
                else if (selectedTopic == "My family")
                {
                    HandleMyFamily(customNpc);
                }
                else if (selectedTopic == "Play a game")
                {
                    HandlePlayGame();
                }
                else if (generatedAnimalTopics.Contains(selectedTopic))
                {
                    HandleAnimalGuess(selectedTopic);
                }
                else
                {
                    if (extendedListboxTopics.SelectedIndex == 0 && firstTopicSelected)
                    {
                        Debug.Log("firstTopicSelected");
                        var vanillaTopics = CustomTalkManagerMod.CustomTalkManager.Instance.GetVanillaTellMeAboutTopics();
                        TalkManager.ListItem selectedListItem = vanillaTopics.FirstOrDefault(topic => topic.caption == selectedTopic);
                        string playerQuestion = CustomTalkManagerMod.CustomTalkManager.Instance.GetQuestionText(selectedListItem, DaggerfallTalkWindow.TalkTone.Normal);
                        listboxConversation.AddItem(playerQuestion, out ListBox.ListItem playerQuestionItem);
                        playerQuestionItem.textLabel.HorizontalAlignment = HorizontalAlignment.Right;
                        playerQuestionItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Right;
                        playerQuestionItem.textColor = Color.white;
                        string npcResponse = "Can't think of much at the moment, sorry.";
                        listboxConversation.AddItem(npcResponse, out ListBox.ListItem npcResponseItem);
                        npcResponseItem.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
                        npcResponseItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
                        npcResponseItem.textColor = melon;
                        return;
                    }
                    HandleVanillaTopics(selectedTopic);
                    if (extendedListboxTopics.SelectedIndex == 0)
                    {
                        firstTopicSelected = true; // Set the flag to true when the first topic is selected
                        Debug.Log("firstTopicSelected set true");
                    }
                }

                UpdateScrollBarConversation();
                UpdateScrollButtonsConversation();
            }
        }

        private void HandlePlaceToStay()
        {
            // Add player's request to conversation listbox
            string playerRequest = "I am weary from my travels. Might I take shelter here for a time?";
            listboxConversation.AddItem(playerRequest, out ListBox.ListItem playerRequestItem);
            playerRequestItem.textLabel.HorizontalAlignment = HorizontalAlignment.Right;
            playerRequestItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Right;
            playerRequestItem.textColor = Color.white;

            // Perform personality check
            int playerPersonality = GameManager.Instance.PlayerEntity.Stats.LivePersonality;
            int checkValue = 60; // Define a threshold value for personality check

            string npcResponse;

            if (HomeStay)
            {
                npcResponse = "I already gave you permission to stay.";
                listboxConversation.AddItem(npcResponse, out ListBox.ListItem npcAlreadyItem);
                npcAlreadyItem.textColor = melon;
                return;
            }
            if (playerPersonality >= checkValue)
            {
                // Successful check
                npcResponse = "You must be exhausted from your journey. Very well.";
                RentRoomAtCurrentLocation(24);
                HomeStay = true;
            }
            else
            {
                // Failed check
                npcResponse = "I can't accommodate anyone right now, sorry.";
            }

            // Add NPC's response to conversation listbox
            listboxConversation.AddItem(npcResponse, out ListBox.ListItem npcResponseItem);
            npcResponseItem.textColor = melon;
        }

        private void HandleMyFamily(CustomStaticNPCMod.CustomStaticNPC customNpc)
        {
            // Add player's inquiry to conversation listbox
            string playerInquiry = "Could I enquire about your family?";
            listboxConversation.AddItem(playerInquiry, out ListBox.ListItem playerInquiryItem);
            playerInquiryItem.textLabel.HorizontalAlignment = HorizontalAlignment.Right;
            playerInquiryItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Right;
            playerInquiryItem.textColor = Color.white;

            // Load family matters CSV and get a response
            if (customNpc != null)
            {
                string familyResponse = CustomTalkManagerMod.CustomTalkManager.Instance.GetFamilyResponse(customNpc.GetInstanceID());
                Debug.Log($"Family Response: {familyResponse}");
                listboxConversation.AddItem(familyResponse, out ListBox.ListItem familyResponseItem);
                familyResponseItem.textColor = melon;
            }
        }

        private void HandlePlayGame()
        {
            string playerGame = "Would you like to play a game?";
            listboxConversation.AddItem(playerGame, out ListBox.ListItem playerGameItem);
            playerGameItem.textLabel.HorizontalAlignment = HorizontalAlignment.Right;
            playerGameItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Right;
            playerGameItem.textColor = Color.white;
            PlayGame();
        }

        private void HandleAnimalGuess(string selectedTopic)
        {
            // Handle dynamically generated animal topics
            string playerGuess = $"Is it a {selectedTopic}?";
            listboxConversation.AddItem(playerGuess, out ListBox.ListItem playerGuessItem);
            playerGuessItem.textLabel.HorizontalAlignment = HorizontalAlignment.Right;
            playerGuessItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Right;
            playerGuessItem.textColor = Color.white;

            // Simulate the NPC's response with a chance logic
            System.Random random = new System.Random();
            double chance = random.NextDouble(); // Generate a random number between 0.0 and 1.0
            string npcResponse;

            if (chance <= 0.3333)
            {
                npcResponse = "Yes! How'd you know?";
            }
            else
            {
                npcResponse = $"No, it's not a {selectedTopic}. You lose!";
            }

            listboxConversation.AddItem(npcResponse, out ListBox.ListItem npcResponseItem);
            npcResponseItem.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
            npcResponseItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
            npcResponseItem.textColor = melon;
            playerSaysListBox.ClearItems();
            listboxTopics.ClearItems();
            listboxTopics.AddItem("Play a game", out ListBox.ListItem item1);
            item1.textLabel.Update();
        }

        private void PlayGame()
        {
            playerSaysListBox.ClearItems();
            listboxTopics.ClearItems();
            string okayGuess = "Okay, guess which animal I'm thinking of!";
            listboxConversation.AddItem(okayGuess, out ListBox.ListItem okayGuessItem);
            okayGuessItem.textColor = melon;

            // Define the list of animal strings
            List<string> animals = new List<string>
            {
                "Lion",
                "Bear",
                "Monkey",
                "Dragon",
                "Chicken",
                "Mammoth",
                "Horse",
                "Fox",
                "Camel"
            };

            // Randomly select 3 animals
            System.Random random = new System.Random();
            generatedAnimalTopics = animals.OrderBy(x => random.Next()).Take(3).ToList(); // Correct the method name to OrderBy

            // Display the selected animals in the listbox
            foreach (var animal in generatedAnimalTopics)
            {
                listboxTopics.AddItem(animal, out ListBox.ListItem animalItem);
                animalItem.textLabel.Update();
            }
        }

        private void HandleVanillaTopics(string selectedTopic)
        {
            var vanillaTopics = CustomTalkManagerMod.CustomTalkManager.Instance.GetVanillaTellMeAboutTopics();
            TalkManager.ListItem selectedListItem = vanillaTopics.FirstOrDefault(topic => topic.caption == selectedTopic);

            if (selectedListItem != null)
            {
                string playerQuestion = CustomTalkManagerMod.CustomTalkManager.Instance.GetQuestionText(selectedListItem, DaggerfallTalkWindow.TalkTone.Normal);
                string npcResponse = CustomTalkManagerMod.CustomTalkManager.Instance.GetAnswerTellMeAboutTopic(selectedListItem);

                Debug.Log($"Player Question: {playerQuestion}");
                Debug.Log($"NPC Response: {npcResponse}");

                listboxConversation.AddItem(playerQuestion, out ListBox.ListItem playerQuestionItem);
                playerQuestionItem.textLabel.HorizontalAlignment = HorizontalAlignment.Right;
                playerQuestionItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Right;
                playerQuestionItem.textColor = Color.white;

                listboxConversation.AddItem(npcResponse, out ListBox.ListItem npcResponseItem);
                npcResponseItem.textLabel.HorizontalAlignment = HorizontalAlignment.Left;
                npcResponseItem.textLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Left;
                npcResponseItem.textColor = melon;
            }
        }

        private void HandleTellMe()
        {
            if (isChildNPC)
                return;
            if (isListboxTopicsClicked)
                return;
            if (TellMeClicked)
                return;
            AudioSource audioSource = DaggerfallUI.Instance.GetComponent<AudioSource>();
            AudioClip buttonClickClip = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            audioSource.PlayOneShot(buttonClickClip, 0.7f);
            listboxTopics.ClearItems();
            playerSaysListBox.ClearItems();
            buttonLocation.Enabled = true;
            buttonPeople.Enabled = true;
            buttonThings.Enabled = true;
            buttonWork.Enabled = true;
            StartScreen = false;

            SetUpReturn(scaleFactor); // Pass scaleFactor

            if (WhereIsClicked)
            {
                if (buttonWhereIs != null)
                {
                    buttonWhereIs.Enabled = false;
                }
                Debug.Log("Components.Remove called");
                WhereIsClicked = false;
            }

            textureCategoryHeaderGold = DaggerfallUI.GetTextureFromImg("TALK03I0.IMG", TextureFormat.ARGB32, false);
            if (textureCategoryHeaderGold == null)
            {
                Debug.LogError("Failed to load textureCategoryHeaderGold.");
                return;
            }

            var vanillaTopics = CustomTalkManagerMod.CustomTalkManager.Instance.GetVanillaTellMeAboutTopics();
            for (int i = 0; i < vanillaTopics.Count; i++)
            {
                if (i == 0 || i == 1 || i == selectedTopicIndex)
                {
                    listboxTopics.AddItem(vanillaTopics[i].caption, out ListBox.ListItem item);
                    item.textLabel.Update();
                }
            }

            Rect subRect = new Rect(0, 0, 107, 10);
            Texture2D subTexture = ImageReader.GetSubTexture(textureCategoryHeaderGold, subRect, new DFSize(textureCategoryHeaderGold.width, textureCategoryHeaderGold.height));
            subTexture.filterMode = FilterMode.Point;

            // Remove the old button if it exists
            if (buttonTellMe != null)
            {
                mainPanel.Components.Remove(buttonTellMe);
                buttonTellMe = null;
            }

            // Create a new buttonTellMe
            buttonTellMe = DaggerfallUI.AddButton(rectButtonTellMe, mainPanel);
            buttonTellMe.BackgroundTexture = subTexture;
            buttonTellMe.OnMouseClick += (BaseScreenComponent sender, Vector2 position) => Debug.Log("ButtonTellMe clicked");
            mainPanel.Components.Add(buttonTellMe);

            // Prevent recursive scaling
            ScalePanelAndChildren(buttonTellMe, scaleFactor, false);

            TellMeClicked = true;
            WhereIsClicked = false;
            Debug.Log($"WhereIs = false");
            isListboxTopicsClicked = true;
            DaggerfallUI.Instance.StartCoroutine(ResetListboxTopicsClickedFlag());
        }

        private int GetCurrentHouseID()
        {
            // Implement logic to get the current house ID
            // This is a placeholder implementation
            return GameManager.Instance.PlayerGPS.CurrentLocation.MapTableData.MapId;
        }

        private void HandleWhereIs()
        {
            return;
            if (isChildNPC)
                return;
            if (isListboxTopicsClicked)
                return;
            if (WhereIsClicked)
                return;
            Debug.Log("WhereIs Clicked");
            listboxTopics.ClearItems();
            StartScreen = false;

            if (TellMeClicked)
            {
                if (buttonTellMe != null)
                {
                    buttonTellMe.Enabled = false;
                }
                Debug.Log("Components.Remove called");
                TellMeClicked = false;
            }

            textureCategoryHeaderGold = DaggerfallUI.GetTextureFromImg("TALK03I0.IMG", TextureFormat.ARGB32, false);
            if (textureCategoryHeaderGold == null)
            {
                Debug.LogError("Failed to load textureCategoryHeaderGold.");
                return;
            }

            HandleLocation();

            Rect subRect = new Rect(0, 10, 107, 10);
            Texture2D subTexture = ImageReader.GetSubTexture(textureCategoryHeaderGold, subRect, new DFSize(textureCategoryHeaderGold.width, textureCategoryHeaderGold.height));
            subTexture.filterMode = FilterMode.Point;

            buttonWhereIs = DaggerfallUI.AddButton(new Rect(16f, 61f, 461, 40), mainPanel);
            buttonWhereIs.BackgroundTexture = subTexture;
            buttonWhereIs.OnMouseClick += (BaseScreenComponent sender, Vector2 position) => Debug.Log("ButtonLocation clicked");
            mainPanel.Components.Add(buttonWhereIs);
            WhereIsClicked = true;
            TellMeClicked = false;
            Debug.Log($"TellMe = false");
            isListboxTopicsClicked = true;
            DaggerfallUI.Instance.StartCoroutine(ResetListboxTopicsClickedFlag());
        }



        private void RentRoomAtCurrentLocation(int rentalDurationHours)
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;

            // Ensure we have a valid building key
            if (playerEnterExit.Interior == null)
            {
                Debug.LogError("PlayerEnterExit.Interior is null. Cannot rent room.");
                return;
            }

            Debug.Log($"Interior object: {playerEnterExit.Interior}");
            Debug.Log($"BuildingData object: {playerEnterExit.Interior.BuildingData}");

            // Use the building key from BuildingDiscoveryData
            int buildingKey = playerEnterExit.BuildingDiscoveryData.buildingKey;
            Debug.Log($"Retrieved buildingKey: {buildingKey}");

            if (buildingKey == 0)
            {
                Debug.LogError("Invalid building key. Cannot rent room.");
                return;
            }

            int mapId = GameManager.Instance.PlayerGPS.CurrentLocation.MapTableData.MapId;
            StaticDoor firstDoor = playerEnterExit.ExteriorDoors[0];
            string sceneName = DaggerfallInterior.GetSceneName(GameManager.Instance.PlayerGPS.CurrentLocation, firstDoor);
            RoomRental_v1 rentedRoom = playerEntity.RentedRooms.Find(r => r.mapID == mapId && r.buildingKey == buildingKey);

            if (rentedRoom == null)
            {
                Vector3[] restMarkers = playerEnterExit.Interior.FindMarkers(DaggerfallInterior.InteriorMarkerTypes.Rest);
                if (restMarkers.Length == 0)
                {
                    Debug.LogError("No rest markers found. Cannot rent room.");
                    return;
                }

                int markerIndex = UnityEngine.Random.Range(0, restMarkers.Length);

                // Create room rental and add it to player rooms
                RoomRental_v1 room = new RoomRental_v1()
                {
                    name = playerEnterExit.Interior.BuildingData.NameSeed.ToString(),
                    mapID = mapId,
                    buildingKey = buildingKey,
                    allocatedBedIndex = markerIndex,
                    expiryTime = DaggerfallUnity.Instance.WorldTime.Now.ToSeconds() + (ulong)(DaggerfallDateTime.SecondsPerDay * rentalDurationHours / 24)
                };
                playerEntity.RentedRooms.Add(room);
                SaveLoadManager.StateManager.AddPermanentScene(sceneName);
                Debug.LogFormat("Rented room for {0} hours. {1}", rentalDurationHours, sceneName);
            }
            else
            {
                rentedRoom.expiryTime += (ulong)(DaggerfallDateTime.SecondsPerDay * rentalDurationHours / 24); // Extend by rental duration
                Debug.LogFormat("Rented room for additional {0} hours. {1}", rentalDurationHours, sceneName);
            }
        }

        private bool HomeStay = false;

        private void OnTransitionToExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HomeStay = false;
        }

        private void ListboxConversation_OnMouseClick(Vector2 position)
        {
            // Handle conversation item click or other logic here
        }

        private void ListboxConversation_OnMouseDoubleClick(Vector2 position)
        {
            // Handle conversation item double click or other logic here
        }
    }
}