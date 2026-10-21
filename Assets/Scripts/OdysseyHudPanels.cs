using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        private enum JourneyPanel { None, Start, Choice, Pause, Settings, Minimap, Controls, Graphics, Audio, Acoustics015, MusicLibrary009, Tree, Build, Portal, Lore, Outcome, FieldForge, TutorialPortalSkip, Customize, TutorialInstruction, RaykonMentor, TrainingLab, MechanicsGuide, Exit020, Play020, Solo020 }
        private readonly Dictionary<JourneyPanel, RectTransform> panels = new Dictionary<JourneyPanel, RectTransform>();
        private readonly List<RectTransform> fittedCards = new List<RectTransform>();
        private readonly List<UnityEngine.UI.Button> hoverButtons = new List<UnityEngine.UI.Button>();
        private int appliedUiParticleQuality = -1;
        private Vector2 lastCanvasSize;
        private UnityEngine.UI.Slider uiScaleSlider, minimapOpacitySlider;
        private UnityEngine.UI.Text uiScaleValue, minimapOpacityValue, minimapFitNote;
        private UnityEngine.UI.Button[] minimapSizeButtons;
        private UnityEngine.UI.Text[] minimapSizeLabels;
        private static readonly string[] MinimapSizeNames = { "SMALL", "MEDIUM", "LARGE" };
        private static readonly string[] MinimapSelectedNames = { "SMALL | SELECTED", "MEDIUM | SELECTED", "LARGE | SELECTED" };
        private float displayedUiScale, displayedMapOpacity;
        private int displayedMapSize;
        private bool interfaceUiSnapshotValid, displayedMapFit;
        private string displayedInterfaceSaveStatus;
        private UnityEngine.UI.Button minimapShortcut;
        private JourneyPanel minimapReturn = JourneyPanel.Settings;
        private JourneyPanel currentPanel = JourneyPanel.Start;
        private JourneyPanel settingsReturn = JourneyPanel.Start;
        private bool observedStarted;
        private bool treePreview;
        private bool treeReturnsToPause;
        private UnityEngine.UI.Text roomStatus;
        private UnityEngine.UI.Text roomHudStatus;
        private UnityEngine.UI.Text roomRoster;
        private UnityEngine.UI.InputField roomCodeInput;
        private UnityEngine.UI.Button createRoomButton;
        private UnityEngine.UI.Button joinRoomButton;
        private UnityEngine.UI.Button campaignButton;
        private string localRoomMessage = "";
        private UnityEngine.UI.Text[] bindingLabels;
        private UnityEngine.UI.Text bindingStatus;
        private UnityEngine.UI.Text voiceValue;
        private UnityEngine.UI.Text audioNote;
        private UnityEngine.UI.Slider voiceSlider;
        private RectTransform voiceRow;
        private UnityEngine.UI.Button muteVoiceButton;
        private UnityEngine.UI.Button replayVoiceButton;
        private UnityEngine.UI.Button skillTreeButton;
        private UnityEngine.UI.Button skipTutorialPauseButton;
        private UnityEngine.UI.Button portalCoopButton;
        private UnityEngine.UI.Button portalUnreadyButton;
        private UnityEngine.UI.Text portalReadyLabel;
        private UnityEngine.UI.Button portalCampaignButton;
        private UnityEngine.UI.Text portalStatus;
        private UnityEngine.UI.Text portalCampaignExplanation;
        private float priorVoiceVolume = .85f;
        private UnityEngine.UI.Text outcomeHeading;
        private UnityEngine.UI.Text outcomeDescription;
        private UnityEngine.UI.Text pauseDescription;
        private UnityEngine.UI.Text pauseControls;
        private RectTransform tutorialPrompt;
        private UnityEngine.UI.Text tutorialHeading;
        private UnityEngine.UI.Text tutorialInstruction;
        private RectTransform captionPanel;
        private UnityEngine.UI.Text captionText;
        private UnityEngine.UI.Text shrineHealthText;
        private UnityEngine.UI.Image shrineHealthFill;
        private UnityEngine.UI.Text treePoints;
        private UnityEngine.UI.Text treeMessage;
        private UnityEngine.UI.Button[] treeTabButtons, treeNodeButtons;
        private UnityEngine.UI.Text[] treeTabLabels, treeNodeLabels;
        private UnityEngine.UI.Image[] treeCardEmblems;
        private OracleElementGlyph[] treeCardFallbacks;
        private UnityEngine.UI.Text treeDetailTitle, treeDetailDescription, treeDetailState, treeLearnLabel;
        private UnityEngine.UI.ScrollRect treeDetailScroll;
        private UnityEngine.UI.Button treeLearnButton;
        private readonly string[,] treeCardText = new string[4, 3];
        private readonly string[,] treeNodeNames = new string[4, 3];
        private readonly string[] treeElementTitles = new string[4];
        private int treeBranch, treeFocusedNode, treeOpenedFrame;
        private bool treeUiSnapshotValid;
        private TreeUiSnapshot treeUiSnapshot;
        private struct TreeUiSnapshot
        {
            public int Level, Points, Mask, L0, L1, L2, L3, Branch, Node, GuideBranch, GuideNode, BindingFrame;
            public bool Preview;
            public string Caption;
            public bool SameAs(TreeUiSnapshot other)
            {
                return Level == other.Level && Points == other.Points && Mask == other.Mask && L0 == other.L0
                    && L1 == other.L1 && L2 == other.L2 && L3 == other.L3 && Branch == other.Branch
                    && Node == other.Node && GuideBranch == other.GuideBranch && GuideNode == other.GuideNode
                    && BindingFrame == other.BindingFrame && Preview == other.Preview && Caption == other.Caption;
            }
        }
        private UnityEngine.UI.Text treeCloseLabel;
        private RectTransform defensePanel;
        private UnityEngine.UI.Text defenseTimer;
        private UnityEngine.UI.Text defenseStatus;
        private UnityEngine.UI.Text defenseWave;
        private UnityEngine.UI.Image defenseWaveFill;
        private UnityEngine.UI.Text buildFeedback;
        private UnityEngine.UI.Text comboText;
        private UnityEngine.UI.Text autoFireStatus;
        private UnityEngine.UI.Text autoFireSettingLabel;
        private UnityEngine.UI.Button autoFireSettingButton;
        private string displayedSelectionStatus;
        private bool displayedInputAuto, inputPresentationReady;
        private int displayedAutoSetting = -1;
        private UnityEngine.UI.Text graphicsDescription;
        private UnityEngine.UI.Text graphicsDrawState;
        private UnityEngine.UI.Slider renderDistanceSlider, farDetailSlider;
        private UnityEngine.UI.Text renderDistanceValue, farDetailValue, renderDistanceStatus, performanceToggleLabel;
        private PerformanceTelemetryReadout graphicsPerformanceReadout;
        private RectTransform performanceOverlayRect;
        private UnityEngine.UI.Button[] graphicsPresetButtons;
        private UnityEngine.UI.Text[] graphicsPresetButtonLabels;
        private string[] graphicsPresetLabels, graphicsPresetCustomLabels;
        private GraphicsUiSnapshot graphicsUiSnapshot;
        private bool graphicsUiSnapshotValid;
        private static readonly string[] GraphicsPresetDescriptions =
        {
            "ULTRA  ·  Full scene resolution, bounded soft shadows and the most lighting detail.",
            "MEDIUM  ·  Softer scene resolution, nearby shadows and moderate lighting detail.",
            "LOW  ·  Reduced scene resolution, short shadows and fewer lights.",
            "VERY LOW  ·  Half scene resolution, shadows off by default and minimal effects.",
            "ULTRA (BETA)  ·  With High shadows and support: 4096/54m shadows, desktop High SMAA, +8 saturation and a subtle flare. Desktop WebGPU uses the supported path; WebGL2/mobile keep the fallback. Portal/particle detail stay max; there is no global animation tier."
        };
        // # ARCHITECTURE SNAPSHOT
        // UI layout is in the existing 1920x1080 CanvasScaler reference space.
        // GraphicsPreferences owns saved values, preset migration and the runtime
        // pipeline clone. This panel only requests changes and reads actual state.
        // The value snapshot prevents unchanged per-frame text/array allocations;
        // it is presentation state, never a second simulation or preference store.
        private struct GraphicsUiSnapshot
        {
            public int Preset, Particles, FrameCap, ShadowMap, Lights, ShadowMetres, ParticlePercent, DistanceBlocks, EffectiveMetres, FarPercent, ShadowQuality020, ShadowCascades020;
            public float Scale, EffectiveScale, Brightness;
            public bool Auto, Bloom, SoftShadows, Reflections020, Customized, Opaque, TextureDetail, Overlay, Clouds020, WeatherEffects020, ShadowFiltering020;

            public bool SameAs(GraphicsUiSnapshot other)
            {
                return Preset == other.Preset && Particles == other.Particles && FrameCap == other.FrameCap
                    && ShadowMap == other.ShadowMap && Lights == other.Lights && ShadowMetres == other.ShadowMetres
                    && ParticlePercent == other.ParticlePercent && Scale == other.Scale && EffectiveScale == other.EffectiveScale
                    && Brightness == other.Brightness && Auto == other.Auto && Bloom == other.Bloom && SoftShadows == other.SoftShadows && Reflections020 == other.Reflections020
                    && Customized == other.Customized && Opaque == other.Opaque && DistanceBlocks == other.DistanceBlocks
                    && EffectiveMetres == other.EffectiveMetres && FarPercent == other.FarPercent
                    && TextureDetail == other.TextureDetail && Overlay == other.Overlay
                    && ShadowQuality020 == other.ShadowQuality020 && ShadowCascades020 == other.ShadowCascades020
                    && Clouds020 == other.Clouds020 && WeatherEffects020 == other.WeatherEffects020 && ShadowFiltering020 == other.ShadowFiltering020;
            }
        }
        private UnityEngine.UI.Text resolutionValue;
        private UnityEngine.UI.Text resolutionHeading, effectiveResolutionValue, autoResolutionValue;
        private UnityEngine.UI.Text resolutionExplanation;
        private UnityEngine.UI.Text brightnessValue, particleValue, frameCapValue, reflectionValue020;
        private UnityEngine.UI.Slider brightnessSlider;
        private UnityEngine.UI.Slider fieldOfViewSlider, screenShakeSlider, bobbingSlider;
        private UnityEngine.UI.Text fieldOfViewValue, screenShakeValue, bobbingValue;
        private UnityEngine.UI.Button progressionResetButton;
        private UnityEngine.UI.Text progressionResetStatus;
        private bool progressionResetArmed;
        private float progressionResetAt;
        private UnityEngine.UI.Text bloomValue;
        private UnityEngine.UI.Slider resolutionSlider;
        private UnityEngine.UI.Slider masterSlider, musicSlider, sfxSlider;
        private UnityEngine.UI.Text masterValue, musicValue, sfxValue;
        private UnityEngine.UI.Text loreTitle, loreBody;
        private UnityEngine.UI.ScrollRect loreScroll;
        private OracleInkRevealText loreInk;
        private UnityEngine.UI.Button loreShowFull, loreInstantToggle;
        private UnityEngine.UI.Text loreHint, loreInstantLabel;
        private bool bookInstantText, bookSystemReducedMotion, bookWritingForwarded;
        private const string BookInstantPreference = "Oracle.Interface.BookInstantText";
        public Action<bool> BookWritingStateChanged;
        public bool IsBookWriting => bookWritingForwarded;
        private CanvasGroup menuInput;
        private UnityEngine.UI.Image menuDimmer;
        private RectTransform introRoot, introCopy;
        private UnityEngine.UI.Image introFade;
        private UnityEngine.UI.Text introTitle, introSubtitle, introSkipHint;
        private UnityEngine.UI.Button skipIntroButton;
        private bool introWasVisible;
        private RectTransform loadingRoot, loadingCard;
        private UnityEngine.UI.Image loadingFill;
        private UnityEngine.UI.Text loadingStatus, loadingProgress;
        private bool loadingWasVisible;
        private RectTransform downedCard;
        private UnityEngine.UI.Text downedTitle, downedCountdown, downedHint;
        private UnityEngine.UI.Image reviveFill;
        private UnityEngine.UI.Button downedResumeButton;
        private RectTransform allyReviveProgress;
        private UnityEngine.UI.Image allyReviveFill;
        private bool IntroActive => session != null && session.Opening != null && session.Opening.IsIntroPlaying;
        private bool LoadingActive => session != null && session.Loading != null && session.Loading.IsLoading;
        private bool ShopVisible => session != null && session.Shop != null && session.Shop.Visible && !session.IsDead && !session.Victory;
        private bool DownedActive => session != null && session.HasStarted && session.IsDowned && !session.IsDead && !session.Victory;
        // Tutorial owns a read-only preview index. Showing it grants no combat input.
        private int AbilityGuidePreviewIndex => session != null && session.Tutorial != null
            ? session.Tutorial.GuideAbilityPreviewIndex : -1;
        public bool IsGuidePreviewVisible => built && treePreview && currentPanel == JourneyPanel.Tree
            && panels.ContainsKey(JourneyPanel.Tree) && panels[JourneyPanel.Tree].gameObject.activeInHierarchy;

        private void BuildExpandedMenus(Transform parent)
        {
            menuRoot = Stretch(parent, "JourneyMenuOverlay");
            menuDimmer = Paint(menuRoot, new Color(.015f, .022f, .022f, .77f), true);
            menuInput = menuRoot.gameObject.AddComponent<CanvasGroup>();
            BuildMenuShowcaseBackdrop();
            BuildStartPanel();BuildPlayModePanels020();
            BuildChoicePanel();
            BuildPausePanel();BuildExitPanel020();
            BuildTutorialPortalSkipPanel();
            BuildTutorialInstructionPanel();
            BuildCustomizationPanel();
            BuildSettingsPanel();
            BuildMechanicsGuide009();
            BuildMinimapSettingsPanel();
            BuildControlsPanel();
            BuildGraphicsPanel();
            BuildAudioPanel();
            BuildAcousticSettings015();
            BuildMusicLibrary009();
            BuildPassiveTree();
            BuildShrineMenu();
            BuildPortalPanel();
            BuildLorePanel();
            BuildRaykonMentorPanel();
            BuildOutcomePanel();
            SetPanel(JourneyPanel.Start);
            BuildMinimapShortcut(parent);
            BuildIntroOverlay(parent);
            BuildLoadingOverlay(parent);
            BuildQuickPortalButton(parent);
            BuildCampaignBook020(parent);
        }

        private RectTransform PanelContent(JourneyPanel id, string name, Vector2 size, bool centered = true)
        {
            var card = Frame(menuRoot, name, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, size, new Color(.035f, .045f, .043f, .98f), Gold);
            panels[id] = card;
            fittedCards.Add(card);
            var content = Stretch(card, "Content", 46, 38, 46, 38);
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 11;
            layout.childAlignment = centered ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return content;
        }

        private UnityEngine.UI.Button MenuButton(Transform parent, string name, string text,
            UnityEngine.Events.UnityAction action, float height = 48)
        {
            var button = Button(parent, name, text, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(0, height), action);
            Height(button.transform as RectTransform, height);
            return button;
        }

        private void BuildStartPanel()
        {
            var content = PanelContent(JourneyPanel.Start, "StartGamePanel", new Vector2(700, 650));
            var start = panels[JourneyPanel.Start];
            start.anchorMin = start.anchorMax = new Vector2(1, .5f);
            start.pivot = new Vector2(1, .5f);
            start.anchoredPosition = new Vector2(-78, 0);
            start.GetComponent<UnityEngine.UI.Image>().color = new Color(.025f, .04f, .033f, .91f);
            BuildReturnBrand(content);
            BuildMenuElementBadges(content);
            MenuButton(content,"StartGame020","START GAME",OpenPlayModes020,54);
            MenuButton(content, "StartSettings", "SETTINGS", () => OpenSettings(JourneyPanel.Start), 54);
            mainMoreLabel020=MenuButton(content,"MainMore020","MORE",ToggleMainMore020,32).GetComponentInChildren<UnityEngine.UI.Text>();
            mainExtras020=BuildMenuGroup020(content,"MainExtras020");
            // Campaign is a primary Solo choice; More retains optional tutorial/showcase tools.
            MenuButton(mainExtras020, "SkipTutorial", "TUTORIAL", () =>
            {
                if (IntroActive) return;
                session.Audio?.StartAudioFromGesture();
                session.BeginCampaign();
            }, 36);
            BuildShowcaseStartControls(mainExtras020);
            mainExtras020.gameObject.SetActive(false);
            BuildUnityCredit(content);
        }
        private void BuildReturnBrand(RectTransform parent)
        {
            var brand = Rect(parent, "OracleReturnTitleLogo", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 180));
            Height(brand, 180);
            var sprite = Resources.Load<Sprite>("Interface/OracleReturnLogo");
            if (sprite != null)
            {
                var picture = Paint(Stretch(brand, "OriginalGreenTitleLogo"), Color.white);
                picture.sprite = sprite;
                picture.preserveAspect = true;
                return;
            }
            Label(brand, "InfiniteOdysseyWordmark", "INFINITE ODYSSEY", 29, Gold, TextAnchor.UpperCenter,
                new Vector2(0, -18), new Vector2(600, 44), new Vector2(.5f, 1));
            Label(brand, "OracleReturnWordmark", "ORACLE’S RETURN", 42, new Color(.45f, .88f, .65f), TextAnchor.UpperCenter,
                new Vector2(0, -68), new Vector2(600, 66), new Vector2(.5f, 1));
            Rule(brand, "EmeraldWordmarkRule", new Vector2(0, -62), new Vector2(320, 1), MutedGold);
            Diamond(brand, "WordmarkEmerald", new Vector2(0, -62), 7, Emerald);
        }

        private void BuildMenuElementBadges(RectTransform parent)
        {
            var row = Rect(parent, "FourOriginalElementBadges", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 72));
            Height(row, 72);
            for (var i = 0; i < 4; i++)
            {
                var cell = Rect(row, "ElementBadge" + i, new Vector2((i + .5f) / 4, .5f), new Vector2(.5f, .5f),
                    Vector2.zero, new Vector2(142, 68));
                var glyphRoot = Rect(cell, "OriginalElementGlyph", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    new Vector2(0, 12), new Vector2(33, 33));
                var color = TutorialElementColor(i);
                foreach (var glyph in DrawSigil(glyphRoot, i)) glyph.color = color;
                Label(cell, "ElementName", session.Progression.ElementNames[i].ToUpperInvariant(), 15, color, TextAnchor.LowerCenter,
                    new Vector2(0, -31), new Vector2(142, 24), new Vector2(.5f, .5f));
            }
        }

        private void SkipTutorialFromMenu()
        {
            if (IntroActive || LoadingActive) return;
            session.Audio?.StartAudioFromGesture();
            AbilityBindings.CancelRebind();
            ClosePanels();
            session.SkipTutorialToHub();
            session.EnterPortal();
        }

        private void BuildIntroOverlay(Transform parent)
        {
            introRoot = Stretch(parent, "OpeningCinematicOverlay");
            // Transparent full-screen hit target consumes clicks while every menu is hidden.
            Paint(introRoot, Color.clear, true);
            introFade = Paint(Stretch(introRoot, "OpeningBlackFade"), Color.black);
            introCopy = Rect(introRoot, "OpeningTopWording", new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -116), new Vector2(1260, 218));
            introTitle = Label(introCopy, "OpeningTitle", "", 48, Ivory, TextAnchor.UpperCenter,
                Vector2.zero, new Vector2(1260, 108), new Vector2(.5f, 1));
            introSubtitle = Label(introCopy, "OpeningSubtitle", "", 26, Gold, TextAnchor.UpperCenter,
                new Vector2(0, -123), new Vector2(1180, 86), new Vector2(.5f, 1));
            foreach (var text in new[] { introTitle, introSubtitle })
            {
                var shadow = text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
                shadow.effectColor = new Color(0, 0, 0, .96f);
                shadow.effectDistance = new Vector2(1, -2);
            }
            skipIntroButton = Button(introRoot, "SkipOpeningCinematic", "SKIP INTRO  [ESC]", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-56, 46), new Vector2(238, 48), () => session.Opening?.SkipIntro());
            var navigation = skipIntroButton.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            skipIntroButton.navigation = navigation;
            introSkipHint = Label(introRoot, "OpeningSkipHint", "Click anywhere or press Escape to skip", 14, Muted, TextAnchor.LowerLeft,
                new Vector2(56, 53), new Vector2(740, 28), new Vector2(0, 0));
            introRoot.gameObject.SetActive(false);
        }

        private bool UpdateIntroOverlay()
        {
            var active = IntroActive;
            if (introRoot == null) return active;
            introRoot.gameObject.SetActive(active);
            if (active)
            {
                if (introRoot.GetSiblingIndex() != introRoot.parent.childCount - 1) introRoot.SetAsLastSibling();
                introFade.color = new Color(0, 0, 0, Mathf.Clamp01(session.Opening.FadeAlpha));
                introTitle.text = session.Opening.IntroTitle;
                introSubtitle.text = session.Opening.IntroSubtitle;
                menuInput.interactable = menuInput.blocksRaycasts = false;
                menuRoot.gameObject.SetActive(false);
                hudRoot.gameObject.SetActive(false);
                if (!introWasVisible && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
            else if (introWasVisible)
            {
                menuInput.interactable = menuInput.blocksRaycasts = true;
                SetPanel(currentPanel);
            }
            introWasVisible = active;
            return active;
        }

        private void BuildLoadingOverlay(Transform parent)
        {
            loadingRoot = Stretch(parent, "ActualArenaPreloadOverlay");
            Paint(loadingRoot, new Color(.008f, .016f, .012f, .96f), true);
            loadingCard = Frame(loadingRoot, "ArenaPreparationStatus", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(980, 340), new Color(.025f, .04f, .031f, 1), Gold);
            Label(loadingCard, "ArenaPreparationHeading", GameWaitMessage020, 20, Gold, TextAnchor.UpperCenter,
                new Vector2(0, -30), new Vector2(900, 44), new Vector2(.5f, 1));
            loadingStatus = Label(loadingCard, "ActualPreloadStage", "", 21, Ivory, TextAnchor.MiddleCenter,
                new Vector2(0, -88), new Vector2(900, 74), new Vector2(.5f, 1));
            var bar = Rect(loadingCard, "ActualPreparationProgress", new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 112), new Vector2(860, 10));
            Paint(bar, new Color(.12f, .17f, .13f));
            loadingFill = Paint(Stretch(bar, "ActualPreparationFill"), Emerald);
            Border(bar, MutedGold, 1);
            loadingProgress = Label(loadingCard, "ActualPreparationProgressText", "", 14, Muted, TextAnchor.MiddleCenter,
                new Vector2(0, 73), new Vector2(900, 30), new Vector2(.5f, 0));
            loadingCancel020=Button(loadingCard,"CancelActualLoading020","CANCEL",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,18),new Vector2(220,40),CancelGameLoading020);
            loadingRoot.gameObject.SetActive(false);
        }

        private bool UpdateLoadingOverlay()
        {
            var active = LoadingActive;
            if (loadingRoot == null) return active;
            loadingRoot.gameObject.SetActive(active);
            if (active)
            {
                if (loadingRoot.GetSiblingIndex() != loadingRoot.parent.childCount - 1) loadingRoot.SetAsLastSibling();
                loadingStatus.text = session.Loading.Status;
                SetFill(loadingFill, session.Loading.Progress);
                loadingProgress.text = "PREPARATION  " + Mathf.RoundToInt(Mathf.Clamp01(session.Loading.Progress) * 100) + "%";
                menuInput.interactable = menuInput.blocksRaycasts = false;
                menuRoot.gameObject.SetActive(false);
                hudRoot.gameObject.SetActive(false);
                if (!loadingWasVisible && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(loadingCancel020!=null?loadingCancel020.gameObject:null);
            }
            else if (loadingWasVisible) SetPanel(currentPanel);
            loadingWasVisible = active;
            return active;
        }

        private void BuildChoicePanel()
        {
            var content = PanelContent(JourneyPanel.Choice, "CampaignAndRoomsPanel", new Vector2(880, 760));
            roomModeTitle020=MenuLabel(content, "ChoiceTitle", "MULTIPLAYER · TOWER DEFENSE", 28, Ivory, 54);
            BuildRoomModeChoices020(content);
            roomCampaignNotice020=MenuLabel(content,"CampaignTownStatus020",CampaignTownPreparing020,17,Muted,46);
            RefreshRoomModeCopy020();
            // Connection status and the real code field carry the room context.
            campaignButton = MenuButton(content, "SoloCampaignUnavailable", "SOLO CAMPAIGN  ·  COMING SOON", () => { }, 48);
            campaignButton.interactable = false;
            campaignButton.GetComponentInChildren<UnityEngine.UI.Text>().color = new Color(.45f, .49f, .46f);
            campaignButton.gameObject.SetActive(false);
            var hostRow=BuildRoomButtonRow020(content,"MultiplayerHostChoices020",48);
            createRoomButton=RoomRowButton020(hostRow,"CreateGame","HOST",HostSelectedRoom020,48);
            roomHostRetryLabel020=createRoomButton.GetComponentInChildren<UnityEngine.UI.Text>();
            roomQuickHost020=RoomRowButton020(hostRow,"QuickHostRoom020","QUICK HOST · PREPARING",QuickHostSelectedRoom020,48);
            roomQuickHost020.interactable=false;
            var joinRow = Rect(content, "JoinRow", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 64));
            Height(joinRow, 64);
            var rowLayout = joinRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            rowLayout.spacing = 14;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childForceExpandWidth = false;
            roomCodeInput = RoomInput(joinRow);
            var inputLayout = roomCodeInput.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            inputLayout.minWidth = 250;
            inputLayout.flexibleWidth = 1;
            joinRoomButton = Button(joinRow, "JoinRoom", "JOIN", Vector2.zero, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(215, 64), JoinSelectedRoom020);
            roomJoinRetryLabel020=joinRoomButton.GetComponentInChildren<UnityEngine.UI.Text>();
            var joinLayout = joinRoomButton.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            joinLayout.minWidth = joinLayout.preferredWidth = 215;
            BuildRoomWait020(content);
            roomStatus = MenuLabel(content, "ActualConnectionStatus", "", 17, Muted, 78);
            roomRoster = MenuLabel(content, "ActualRoomCodeAndCount", "", 26, Ivory, 58);
            roomRoster.resizeTextForBestFit=true;roomRoster.resizeTextMinSize=18;roomRoster.resizeTextMaxSize=26;
            MenuButton(content, "ChoiceBack", "BACK", BackMenu015, 44);
        }

        private UnityEngine.UI.InputField RoomInput(Transform parent)
        {
            var rect = Frame(parent, "RoomCodeInput", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(430, 64), new Color(.07f, .095f, .08f, 1), Gold);
            rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var input = rect.gameObject.AddComponent<UnityEngine.UI.InputField>();
            var text = Label(rect, "InputText", "", 28, Ivory, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(16, 5);
            text.rectTransform.offsetMax = new Vector2(-16, -5);
            var placeholder = Label(rect, "Placeholder", "ROOM CODE", 16, Muted, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(16, 5);
            placeholder.rectTransform.offsetMax = new Vector2(-16, -5);
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            input.contentType = UnityEngine.UI.InputField.ContentType.Alphanumeric;
            input.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
            input.characterLimit = 16;
            input.caretColor = Emerald;
            input.selectionColor = new Color(.25f, .55f, .44f, .6f);
            return input;
        }

        private void BuildPausePanel()
        {
            var content = PanelContent(JourneyPanel.Pause, "PausePanel", new Vector2(780, 460));
            restartTutorialButton015=MenuButton(content,"RestartTutorialRun","RESTART TUTORIAL",()=>session.RestartTutorial015(),38);
            MenuButton(content, "Resume", "RESUME", ResumeJourney, 54);
            replayVoiceButton=MenuButton(content,"ContinueInstruction","GAME GUIDE",OpenGuideFromPause015,42);
            MenuButton(content, "PauseSettings", "SETTINGS", () => OpenSettings(JourneyPanel.Pause),42);
            MenuButton(content, "ReturnMain", "LEAVE", ShowExitPanel020,42);
            pauseMoreLabel020=MenuButton(content,"PauseMore020","MORE",TogglePauseMore020,32).GetComponentInChildren<UnityEngine.UI.Text>();
            pauseExtras020=BuildMenuGroup020(content,"PauseExtras020");
            content=ScrollingContent(pauseExtras020,"PauseActionsScroll",420,out _);
            pauseDescription = MenuLabel(content, "PauseDescription", "", 17, Muted, 44);
            legacyHowToHub020=MenuButton(content,"PauseMechanicsGuide","HOW TO PLAY",()=>OpenMechanicsGuide009(JourneyPanel.Pause),42);
            skipTutorialPauseButton = MenuButton(content, "SkipRemainingTutorial", "SKIP & ENTER PORTAL", SkipTutorialFromMenu);
            skillTreeButton = MenuButton(content, "OpenTree", "SKILLS", () => ShowSkillTree());
            MenuButton(content,"Achievements","ACHIEVEMENTS",()=>ShowAchievementBook());
            legacyJoinHub020=MenuButton(content,"JoinFriendFromPause","JOIN FRIEND",JoinFriendFromGuide015,40);
            legacyHostHub020=MenuButton(content,"HostPartyFromPause","HOST CO-OP",HostCoopFromGuide015,40);
            partyVitalsMenuButton015=MenuButton(content,"PartyVitalsDisclosure","EXPAND PARTY VITALS",TogglePartyVitals015,36);
            partyVitalsMenuLabel015=partyVitalsMenuButton015.GetComponentInChildren<UnityEngine.UI.Text>();
            pauseControls = MenuLabel(content, "PauseControls", "", 15, Muted, 91);
            MenuLabel(content, "PauseBackend", GraphicsLabel(), 11, Muted, 20);
            pauseExtras020.gameObject.SetActive(false);
            BuildPauseGuide020();
        }
        private void BuildSettingsPanel()
        {
            var panel = PanelContent(JourneyPanel.Settings, "SettingsPanel", new Vector2(920, 520));
            MenuLabel(panel, "SettingsTitle", "SETTINGS", 34, Ivory, 62);

            MenuButton(panel, "AudioSettings", "AUDIO / NARRATION", () => { RefreshSettings(); SetPanel(JourneyPanel.Audio); });
            MenuButton(panel, "AllControlSettings", "CONTROLS", () => { RefreshControls(); SetPanel(JourneyPanel.Controls); });
            MenuButton(panel, "GraphicsSettings", "GRAPHICS", () => { RefreshGraphics(); SetPanel(JourneyPanel.Graphics); });
            MenuButton(panel, "SettingsCustomizeOracle", "CHARACTER", () => OpenCustomization(JourneyPanel.Settings));
            settingsMoreLabel020=MenuButton(panel,"SettingsMore020","MORE",ToggleSettingsMore020,32).GetComponentInChildren<UnityEngine.UI.Text>();
            settingsExtras020=BuildMenuGroup020(panel,"SettingsExtras020");
            var content = ScrollingContent(settingsExtras020, "DesktopSettingsScrollView", 350, out _);
            MenuButton(content,"SettingsMechanicsGuide","HOW TO PLAY",()=>OpenMechanicsGuide009(JourneyPanel.Settings));
            MenuButton(content, "MinimapPreferences", "MAP", () => OpenMinimapSettings(JourneyPanel.Settings));
            BuildDialogueSettings020(content);
            uiScaleSlider = CameraPreferenceRow(content, "InterfaceScale", "INTERFACE SIZE", .75f, 1.35f,
                value => interfacePreferences?.SetUiScale(value), out uiScaleValue);
            MenuLabel(content, "InterfaceScaleExplanation", "Interface size is separate from scene resolution. Menus fit inside the window.", 14, Muted, 48);
            autoFireSettingButton = MenuButton(content, "OptionalAutoFire", "", () =>
            {
                if (session.Combat == null || !session.HasStarted) return;
                session.FieldSmith?.ForgetAutoRestore();
                session.Combat.SetAutoFire(!session.Combat.AutoFireEnabled);
                RefreshSettings();
            });
            autoFireSettingLabel = autoFireSettingButton.GetComponentInChildren<UnityEngine.UI.Text>();
            MenuLabel(content, "OptionalAutoFireScope", "Prepare 1, 2 or 3 elements, or all 4 for Nova. A triple volley clears after firing. Optional auto-fire waits 0.5 seconds after selection. Click to cast, or hold click and press fresh element keys when ready.", 14, Muted, 76);
            BuildSensitivity(content);
            fieldOfViewSlider = CameraPreferenceRow(content, "FieldOfView", "FIELD OF VIEW", 70, 110, value =>
            {
                if (session.Player != null) session.Player.FieldOfView = value;
            }, out fieldOfViewValue);
            screenShakeSlider = CameraPreferenceRow(content, "ScreenShake", "SCREEN SHAKE", 0, 1, value =>
            {
                if (session.Player != null) session.Player.ScreenShakeIntensity = value;
            }, out screenShakeValue);
            bobbingSlider = CameraPreferenceRow(content, "HeadBobbing", "WALKING BOB", 0, 1, value =>
            {
                if (session.Player != null) session.Player.BobbingIntensity = value;
            }, out bobbingValue);
            MenuLabel(content, "MotionBlurStatus", "NO MOTION BLUR", 13, Muted, 26);
            MenuButton(content, "ResetCameraAndGraphics", "RESET CAMERA & GRAPHICS SETTINGS", () =>
            {
                session.Player?.ResetControlPreferences();
                session.Graphics?.ResetGraphicsDefaults();
                RefreshSettings();
                RefreshGraphics();
            });
            MenuLabel(content, "ResetPreferencesScope", "Restores camera comfort and graphics defaults. Key bindings and audio use their own controls.", 14, Muted, 54);
            MenuButton(content, "ResetInterfacePreferences", "RESET INTERFACE SETTINGS", () =>
            {
                interfacePreferences?.ResetDefaults();
                RefreshInterfaceSettings();
            });
            MenuLabel(content, "InterfaceResetScope", "Restores interface size, minimap and NPC subtitle preferences. Progression, controls, camera and audio remain separate.", 14, Muted, 62);
            progressionResetButton = MenuButton(content, "ResetOracleProgression", "RESET SAVED PROGRESSION", ResetSavedProgression);
            progressionResetStatus = MenuLabel(content, "SavedProgressionResetScope", "Clears this Oracle's saved progression on this device only.", 15, Muted, 92);
            settingsExtras020.gameObject.SetActive(false);
            MenuButton(panel, "SettingsBack", "BACK", () =>
            {
                AbilityBindings.CancelRebind();
                SetPanel(settingsReturn);
            }, 47);
        }

        private UnityEngine.UI.Slider CameraPreferenceRow(RectTransform parent, string name, string heading, float minimum, float maximum,
            UnityEngine.Events.UnityAction<float> changed, out UnityEngine.UI.Text valueText)
        {
            var row = Rect(parent, name, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 70));
            Height(row, 70);
            Label(row, "Heading", heading, 12, Gold, TextAnchor.UpperLeft, Vector2.zero, new Vector2(400, 22), new Vector2(0, 1));
            valueText = Label(row, "Value", "", 13, Ivory, TextAnchor.UpperRight, Vector2.zero, new Vector2(130, 22), new Vector2(1, 1));
            return SettingSlider(row, name + "Slider", changed, minimum, maximum);
        }

        private void BuildMinimapSettingsPanel()
        {
            var content = PanelContent(JourneyPanel.Minimap, "LiveMinimapPreferences", new Vector2(800, 800));
            MenuLabel(content, "MinimapSettingsTitle", "MINIMAP", 34, Ivory, 62);
            MenuLabel(content, "MinimapExplanation", "Drawn from the current dungeon footprint.\nPlayer, active portals, towers and living ground units use their real positions.", 18, Muted, 76);
            var sizes = Rect(content, "MinimapSizes", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 52));
            Height(sizes, 52);
            var layout = sizes.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlHeight = layout.childControlWidth = layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            minimapSizeButtons = new UnityEngine.UI.Button[3];
            minimapSizeLabels = new UnityEngine.UI.Text[3];
            for (var i = 0; i < 3; i++)
            {
                var size = i;
                minimapSizeButtons[i] = Button(sizes, "MapSize" + i, MinimapSizeNames[i], Vector2.zero, new Vector2(.5f, .5f), Vector2.zero,
                    new Vector2(180, 52), () => { interfacePreferences?.SetMinimapSize(size); RefreshInterfaceSettings(); });
                minimapSizeLabels[i] = minimapSizeButtons[i].GetComponentInChildren<UnityEngine.UI.Text>();
            }
            minimapOpacitySlider = CameraPreferenceRow(content, "MinimapOpacity", "MINIMAP OPACITY", .2f, 1,
                value => interfacePreferences?.SetMinimapOpacity(value), out minimapOpacityValue);
            MenuLabel(content, "MapMarkerKey", "GREEN · You    GOLD · Towers    RED · Ground units\nFlying enemies are not included in ground-unit markers.", 15, Muted, 58);
            minimapFitNote = MenuLabel(content, "ActualMinimapPreferenceStatus", "", 15, Muted, 88);
            MenuButton(content, "ResetMinimapOnly", "RESET MINIMAP DEFAULTS", () =>
            { interfacePreferences?.ResetMinimapDefaults(); RefreshInterfaceSettings(); });
            MenuButton(content, "MinimapBack", "BACK", () =>
            { if (minimapReturn == JourneyPanel.None) ResumeJourney(); else SetPanel(minimapReturn); }, 47);
        }

        private void OpenMinimapSettings(JourneyPanel from)
        {
            if (IntroActive || LoadingActive || ShopVisible || DownedActive || session.IsDead || session.Victory) return;
            minimapReturn = from;
            AbilityBindings.CancelRebind();
            if (session.HasStarted) session.Pause();
            SetPanel(JourneyPanel.Minimap);
            RefreshInterfaceSettings();
        }

        private void BuildMinimapShortcut(Transform parent)
        {
            minimapShortcut = Button(parent, "ClickableMinimapPreferencesIcon", "", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(30, 30), () => OpenMinimapSettings(currentPanel == JourneyPanel.None ? JourneyPanel.None : JourneyPanel.Pause));
            var icon = Rect(minimapShortcut.transform, "FoldedMapSymbol", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(19, 19));
            DrawMapIconLine(icon, new Vector2(-7,-5), new Vector2(-7,6));
            DrawMapIconLine(icon, new Vector2(0,-7), new Vector2(0,4));
            DrawMapIconLine(icon, new Vector2(7,-4), new Vector2(7,7));
            DrawMapIconLine(icon, new Vector2(-7,6), new Vector2(0,4));
            DrawMapIconLine(icon, new Vector2(0,4), new Vector2(7,7));
            DrawMapIconLine(icon, new Vector2(-7,-5), new Vector2(0,-7));
            DrawMapIconLine(icon, new Vector2(0,-7), new Vector2(7,-4));
            minimapShortcut.gameObject.SetActive(false);
        }

        private static void DrawMapIconLine(Transform parent, Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var line = Rect(parent, "MapFold", new Vector2(.5f, .5f), new Vector2(.5f, .5f), (from + to) * .5f,
                new Vector2(delta.magnitude, 1.4f));
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Paint(line, Gold);
        }

        private void InterfacePreferencesChanged()
        {
            lastScreenSize = lastCanvasSize = Vector2.zero;
        }

        private void ApplyMinimapPreferences()
        {
            if (interfacePreferences == null || minimapRoot == null || canvas == null) return;
            var bounds = (canvas.transform as RectTransform).rect.size;
            var requested = interfacePreferences.MinimapSizeFactor;
            var availableWidth = tutorialPrompt != null && tutorialPrompt.gameObject.activeSelf ? (bounds.x - 900) * .5f - 56 : bounds.x * .3f - 56;
            bool performanceBelow=session.Graphics!=null&&session.Graphics.PerformanceTelemetry!=null&&session.Graphics.PerformanceTelemetry.OverlayVisible;
            var readyFit=ReadinessHudMath.Fit(bounds.x,bounds.y,minimapRoot.rect.height,requested,availableWidth,session.HasStarted,performanceBelow?94f:0f);
            readinessFooterFits=readyFit.FooterFits;
            var effectiveScale=readyFit.Scale;
            if (!Mathf.Approximately(minimapRoot.localScale.x, effectiveScale)) minimapRoot.localScale = Vector3.one * effectiveScale;
            if (!Mathf.Approximately(minimapOpacityGroup.alpha, interfacePreferences.MinimapOpacity)) minimapOpacityGroup.alpha = interfacePreferences.MinimapOpacity;
            if (minimapShortcut != null)
            {
                var root = canvas.transform as RectTransform;
                var corner = minimapRoot.TransformPoint(new Vector3(minimapRoot.rect.xMax - 24, minimapRoot.rect.yMax - 25, 0));
                var shortcut = minimapShortcut.transform as RectTransform;
                var position = (Vector2)root.InverseTransformPoint(corner);
                if ((shortcut.anchoredPosition - position).sqrMagnitude > .01f) shortcut.anchoredPosition = position;
                if (!Mathf.Approximately(shortcut.localScale.x, effectiveScale)) shortcut.localScale = minimapRoot.localScale;
                minimapShortcut.gameObject.SetActive(session.HasStarted && session.Paused && !IntroActive && !LoadingActive && !ShopVisible && !DownedActive
                    && !session.IsDead && !session.Victory && (currentPanel == JourneyPanel.Pause || currentPanel == JourneyPanel.None));
            }
            if (performanceOverlayRect != null)
            {
                var position = new Vector2(36, -54 - (minimapRoot.rect.height+ReadinessFooterExtraHeight) * effectiveScale);
                if ((performanceOverlayRect.anchoredPosition - position).sqrMagnitude > .01f)
                    performanceOverlayRect.anchoredPosition = position;
            }
            FitTopActions020(bounds);
        }

        private void RefreshInterfaceSettings()
        {
            if (interfacePreferences == null || uiScaleSlider == null) return;
            var fitted = minimapRoot != null && minimapRoot.localScale.x + .01f < interfacePreferences.MinimapSizeFactor;
            if (interfaceUiSnapshotValid && displayedUiScale == interfacePreferences.UiScale
                && displayedMapOpacity == interfacePreferences.MinimapOpacity && displayedMapSize == interfacePreferences.MinimapSize
                && displayedMapFit == fitted && displayedInterfaceSaveStatus == interfacePreferences.SaveStatus) return;
            interfaceUiSnapshotValid = true;
            displayedUiScale = interfacePreferences.UiScale;
            displayedMapOpacity = interfacePreferences.MinimapOpacity;
            displayedMapSize = interfacePreferences.MinimapSize;
            displayedMapFit = fitted;
            displayedInterfaceSaveStatus = interfacePreferences.SaveStatus;
            uiScaleSlider.SetValueWithoutNotify(interfacePreferences.UiScale);
            uiScaleValue.text = Mathf.RoundToInt(interfacePreferences.UiScale * 100) + "%";
            minimapOpacitySlider.SetValueWithoutNotify(interfacePreferences.MinimapOpacity);
            minimapOpacityValue.text = Mathf.RoundToInt(interfacePreferences.MinimapOpacity * 100) + "%";
            for (var i = 0; i < 3; i++) minimapSizeLabels[i].text = interfacePreferences.MinimapSize == i ? MinimapSelectedNames[i] : MinimapSizeNames[i];
            minimapFitNote.text = (fitted ? "Requested size is fitted to this window.\n" : "") + interfacePreferences.SaveStatus;
        }

        private void ResetSavedProgression()
        {
            if (session.Shop == null) return;
            if (!progressionResetArmed)
            {
                progressionResetArmed = true;
                progressionResetAt = Time.unscaledTime + .65f;
                progressionResetButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "CONFIRM RESET SAVED PROGRESSION";
                progressionResetStatus.text = "This clears saved Heartstones, wand levels and permanent training on this device. Current run XP stays separate. Click again to confirm.";
                progressionResetStatus.color = Gold;
                return;
            }
            if (Time.unscaledTime < progressionResetAt) return;
            var reset = session.Shop.ResetSavedProgression();
            progressionResetArmed = false;
            progressionResetButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "RESET SAVED PROGRESSION";
            progressionResetStatus.text = reset ? "Oracle saved progression reset on this device. Current run XP remains unchanged."
                : "Saved progression could not be reset. " + session.Shop.SaveStatus;
            progressionResetStatus.color = reset ? Emerald : Gold;
        }

        private void BuildControlsPanel()
        {
            var panel = PanelContent(JourneyPanel.Controls, "CompleteControlsPanel", new Vector2(1000, 1000));
            MenuLabel(panel, "ControlsTitle", "CONTROLS", 34, Ivory, 58);
            var content = ScrollingContent(panel, "AllActionsScrollView", 570, out _);
            bindingLabels = new UnityEngine.UI.Text[GameActionBindings.ActionNames.Length];
            MenuLabel(content, "MovementHeading", "MOVEMENT & INTERACTIONS", 15, Gold, 29);
            for (var i = 0; i < GameActionBindings.ActionNames.Length; i++)
            {
                var action = GameActionBindings.ActionNames[i];
                if (i == 8) MenuLabel(content, "ElementHeading", "FOUR ELEMENTS · SINGLE / PAIR / TRIPLE / NOVA", 15, Gold, 42);
                if (i == 12) MenuLabel(content, "MusicBindingsHeading015", "MUSIC · INDEPENDENT PLAYBACK CONTROLS", 15, Gold, 29);
                var row = Rect(content, "Binding" + action, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 50));
                Height(row, 50);
                Label(row, "ActionName", GameActionBindings.GetDisplayName(action), 19, Ivory, TextAnchor.MiddleLeft,
                    new Vector2(8, 0), new Vector2(505, 50), new Vector2(0, .5f));
                var button = Button(row, "Rebind" + action, "", new Vector2(1, .5f), new Vector2(1, .5f),
                    new Vector2(-8, 0), new Vector2(260, 46), () => GameActionBindings.BeginRebind(action));
                bindingLabels[i] = button.GetComponentInChildren<UnityEngine.UI.Text>();
            }
            MenuLabel(content, "FixedControlsHeading", "FIXED BROWSER & MOUSE CONTROLS", 15, Gold, 29);
            FixedControlRow(content, "Look and aim", "MOUSE · FIXED");
            FixedControlRow(content, "Cast prepared 1 / 2 / 3 / 4 elements", "LEFT MOUSE · FIXED");
            FixedControlRow(content, "Pause / release pointer", "ESCAPE · FIXED");
            FixedControlRow(content, "Tower menu / confirm placement", "RIGHT MOUSE · FIXED");
            bindingStatus = MenuLabel(panel, "BindingStatus", "", 16, Muted, 66);
            MenuButton(panel, "ResetAllControls", "RESTORE ALL DEFAULT CONTROLS", () =>
            {
                GameActionBindings.CancelRebind();
                GameActionBindings.ResetDefaults();
                RefreshControls();
            }, 43);
            MenuButton(panel, "ControlsBack", "BACK", () =>
            {
                GameActionBindings.CancelRebind();
                SetPanel(JourneyPanel.Settings);
            }, 47);
        }

        private void FixedControlRow(RectTransform parent, string label, string value)
        {
            var row = Rect(parent, "Fixed" + label, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 50));
            Height(row, 50);
            Label(row, "Action", label, 17, Muted, TextAnchor.MiddleLeft, new Vector2(8, 0), new Vector2(505, 50), new Vector2(0, .5f));
            Label(row, "FixedKey", value, 15, Gold, TextAnchor.MiddleRight, new Vector2(-8, 0), new Vector2(260, 50), new Vector2(1, .5f));
        }

        private RectTransform ScrollingContent(RectTransform parent, string name, float height, out UnityEngine.UI.ScrollRect scroll, bool gridContent = false)
        {
            var root = Rect(parent, name, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, height));
            Height(root, height);
            scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Stretch(root, "Viewport", 8, 0, 8, 0);
            Paint(viewport, new Color(.025f, .035f, .028f, .7f), true);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
            var content = Rect(viewport, "Content", new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            if (!gridContent)
            {
                var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                layout.padding = new RectOffset(12, 12, 12, 12);
                layout.spacing = 10;
                layout.childControlHeight = layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;
            }
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            return content;
        }

        private void RefreshControls()
        {
            if (bindingLabels == null || bindingStatus == null) return;
            for (var i = 0; i < bindingLabels.Length; i++)
            {
                var action = GameActionBindings.ActionNames[i];
                bindingLabels[i].text = GameActionBindings.IsRebinding && GameActionBindings.CurrentAction == action
                    ? "PRESS A KEY..." : GameActionBindings.GetLabel(action) + "   ·   CHANGE";
            }
            bindingStatus.text = !string.IsNullOrEmpty(GameActionBindings.LastMessage) ? GameActionBindings.LastMessage
                : GameActionBindings.IsRebinding ? "Press a new key. Escape cancels. Conflicts and fixed controls are rejected."
                : "Choose an action to change its key. Scroll for spells and fixed controls.";
        }

        private void BuildAudioPanel()
        {
            var panel = PanelContent(JourneyPanel.Audio, "AudioSettingsPanel", new Vector2(860, 800));
            MenuLabel(panel, "AudioTitle", "AUDIO", 34, Ivory, 62);
            var content = ScrollingContent(panel,"AudioPreferencesScrollView020",550,out _);
            masterSlider = VolumeRow(content, "MasterVolume", "MASTER VOLUME", value => { if (session.Audio != null) session.Audio.MasterVolume = value; }, out masterValue);
            musicSlider = VolumeRow(content, "MusicVolume", "MUSIC", value => { if (session.Audio != null) session.Audio.MusicVolume = value; }, out musicValue);
            sfxSlider = VolumeRow(content, "SfxVolume", "SOUND EFFECTS", value => { if (session.Audio != null) session.Audio.SfxVolume = value; }, out sfxValue);
            voiceRow = Rect(content, "NarrationVolume", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 70));
            Height(voiceRow, 70);
            Label(voiceRow, "VoiceLabel", "NARRATION VOLUME", 12, Gold, TextAnchor.UpperLeft,
                Vector2.zero, new Vector2(360, 22), new Vector2(0, 1));
            voiceValue = Label(voiceRow, "VoiceValue", "", 13, Ivory, TextAnchor.UpperRight,
                Vector2.zero, new Vector2(120, 22), new Vector2(1, 1));
            voiceSlider = SettingSlider(voiceRow, "VoiceSlider", value =>
            {
                if (session.Tutorial != null) session.Tutorial.VoiceVolume = value;
                voiceValue.text = Mathf.RoundToInt(value * 100) + "%";
            });
            muteVoiceButton = MenuButton(content, "MuteNarration", "MUTE / UNMUTE NARRATION", () =>
            {
                if (session.Tutorial == null) return;
                if (session.Tutorial.VoiceVolume > .001f)
                {
                    priorVoiceVolume = session.Tutorial.VoiceVolume;
                    session.Tutorial.VoiceVolume = 0;
                }
                else session.Tutorial.VoiceVolume = priorVoiceVolume;
                RefreshSettings();
            }, 38);
            audioNote = MenuLabel(content, "AudioAvailability", "Captions remain enabled.", 14, Muted, 36);
            MenuButton(content,"MusicLibraryEntry009","MUSIC LIBRARY",OpenMusicLibrary009,38);
            MenuButton(content, "PositionalSound015", "POSITIONAL SOUND", () => { SetPanel(JourneyPanel.Acoustics015); RefreshAcousticSettings015(true); }, 38);
            BuildWeatherAmbienceControls020(content);
            MenuButton(panel, "AudioBack", "BACK", () => SetPanel(JourneyPanel.Settings), 47);
        }

        private UnityEngine.UI.Slider VolumeRow(RectTransform parent, string name, string heading,
            UnityEngine.Events.UnityAction<float> changed, out UnityEngine.UI.Text valueText)
        {
            var row = Rect(parent, name, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 70));
            Height(row, 70);
            Label(row, "Heading", heading, 12, Gold, TextAnchor.UpperLeft, Vector2.zero, new Vector2(360, 22), new Vector2(0, 1));
            var display = Label(row, "Value", "", 13, Ivory, TextAnchor.UpperRight, Vector2.zero, new Vector2(120, 22), new Vector2(1, 1));
            valueText = display;
            return SettingSlider(row, name + "Slider", value => { changed(value); display.text = Mathf.RoundToInt(value * 100) + "%"; });
        }

        private void BuildGraphicsPanel()
        {
            var panel = PanelContent(JourneyPanel.Graphics, "GraphicsSettingsPanel", new Vector2(940, 980));
            MenuLabel(panel, "GraphicsTitle", "GRAPHICS", 34, Ivory, 62);
            var content = ScrollingContent(panel, "GraphicsPreferencesScrollView", 680, out _);
            var presets = Rect(content, "QualityPresets", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 180));
            Height(presets, 180);
            var rows = presets.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            rows.spacing = 12;
            rows.childControlWidth = rows.childControlHeight = rows.childForceExpandWidth = true;
            rows.childForceExpandHeight = false;
            graphicsPresetButtons = new UnityEngine.UI.Button[GraphicsPreferences.PresetCount];
            graphicsPresetButtonLabels = new UnityEngine.UI.Text[GraphicsPreferences.PresetCount];
            graphicsPresetLabels = new string[GraphicsPreferences.PresetCount];
            graphicsPresetCustomLabels = new string[GraphicsPreferences.PresetCount];
            RectTransform presetRow = null;
            for (var i = 0; i < GraphicsPreferences.PresetCount; i++)
            {
                if ((i & 1) == 0)
                {
                    presetRow = Rect(presets, "PresetRow" + (i / 2), Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 52));
                    Height(presetRow, 52);
                    var row = presetRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                    row.spacing = 12;
                    row.childControlWidth = row.childControlHeight = row.childForceExpandWidth = row.childForceExpandHeight = true;
                }
                var preset = i;
                graphicsPresetLabels[i] = GraphicsPreferences.PresetNames[i].ToUpperInvariant();
                graphicsPresetCustomLabels[i] = graphicsPresetLabels[i] + "  ·  CUSTOM";
                graphicsPresetButtons[i] = Button(presetRow, "Preset" + i, graphicsPresetLabels[i], Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(220, 52),
                    () => { session.Graphics?.SetPreset(preset); RefreshGraphics(); });
                graphicsPresetButtonLabels[i] = graphicsPresetButtons[i].GetComponentInChildren<UnityEngine.UI.Text>();
            }
            graphicsDescription = MenuLabel(content, "ActualQualityDescription", "", 18, Muted, 100);
            var automatic = MenuButton(content, "ToggleAutomaticResolution", "", () =>
            {
                if (session.Graphics != null) session.Graphics.SetAutoResolution(!session.Graphics.AutoResolutionEnabled);
                RefreshGraphics();
            });
            autoResolutionValue = automatic.GetComponentInChildren<UnityEngine.UI.Text>();
            brightnessSlider = CameraPreferenceRow(content, "Brightness", "BRIGHTNESS", .7f, 1.5f, value =>
            {
                session.Graphics?.SetBrightness(value);
            }, out brightnessValue);
            var scaleRow = Rect(content, "RenderResolutionScale", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 70));
            Height(scaleRow, 70);
            resolutionHeading = Label(scaleRow, "ResolutionHeading", "RENDER RESOLUTION", 12, Gold, TextAnchor.UpperLeft,
                Vector2.zero, new Vector2(400, 22), new Vector2(0, 1));
            resolutionValue = Label(scaleRow, "ResolutionValue", "", 13, Ivory, TextAnchor.UpperRight,
                Vector2.zero, new Vector2(130, 22), new Vector2(1, 1));
            resolutionSlider = SettingSlider(scaleRow, "ResolutionSlider", value =>
            {
                session.Graphics?.SetResolutionScale(value);
                resolutionValue.text = Mathf.RoundToInt(value * 100) + "%";
            });
            resolutionSlider.SetValueWithoutNotify(session.Graphics != null ? session.Graphics.ResolutionScale : 1f);
            resolutionSlider.minValue = .5f;
            effectiveResolutionValue = MenuLabel(content, "ActualResolutionAndMaximum", "", 15, Muted, 30);
            var bloomButton = MenuButton(content, "ToggleBloom", "", () =>
            {
                if (session.Graphics != null) session.Graphics.SetBloom(!session.Graphics.BloomEnabled);
                RefreshGraphics();
            });
            bloomValue = bloomButton.GetComponentInChildren<UnityEngine.UI.Text>();
            BuildShadowQualityDropdown020(content);
            var reflectionButton020=MenuButton(content,"ToggleReflections020","",()=>
            {
                if(session.Graphics!=null)session.Graphics.SetReflectionsEnabled020(!session.Graphics.ReflectionsEnabled020);
                RefreshGraphics();
            });
            reflectionValue020=reflectionButton020.GetComponentInChildren<UnityEngine.UI.Text>();
            var particles = MenuButton(content, "CycleParticleDetail", "", () =>
            {
                if (session.Graphics != null) session.Graphics.SetParticleQuality((session.Graphics.ParticleQuality + 1) % 3);
                RefreshGraphics();
            });
            particleValue = particles.GetComponentInChildren<UnityEngine.UI.Text>();
            graphicsDrawState = MenuLabel(content, "ActualDrawFeatures", "", 14, Muted, 82);
            var cap = MenuButton(content, "CycleFrameRateLimit", "", () =>
            {
                if (session.Graphics != null)
                    session.Graphics.SetFrameRateCap(session.Graphics.FrameRateCap == 30 ? 60 : session.Graphics.FrameRateCap == 60 ? 0 : 30);
                RefreshGraphics();
            });
            frameCapValue = cap.GetComponentInChildren<UnityEngine.UI.Text>();
            var distanceRow = Rect(content, "WorldRenderDistance", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 70));
            Height(distanceRow, 70);
            Label(distanceRow, "DistanceHeading", "RENDER DISTANCE", 12, Gold, TextAnchor.UpperLeft,
                Vector2.zero, new Vector2(420, 22), new Vector2(0, 1));
            renderDistanceValue = Label(distanceRow, "DistanceValue", "", 13, Ivory, TextAnchor.UpperRight,
                Vector2.zero, new Vector2(180, 22), new Vector2(1, 1));
            renderDistanceSlider = SettingSlider(distanceRow, "DistanceBlocksSlider", value =>
            {
                session.Graphics?.SetRenderDistanceBlocks(Mathf.RoundToInt(value));
                RefreshGraphics();
            }, 32, 128);
            renderDistanceSlider.wholeNumbers = true;
            renderDistanceStatus = MenuLabel(content, "ActualCameraReach", "", 14, Muted, 60);
            farDetailSlider = CameraPreferenceRow(content, "FarTextureStyle", "DISTANT TEXTURE PIXELATION", 0, 1,
                value => session.Graphics?.SetFarDetailPixelation(value), out farDetailValue);
            MenuLabel(content, "FarTextureExplanation", "A visual style for supported distant world textures. Camera reach still follows the actual preset and location limits.", 14, Muted, 60);
            var performanceToggle = MenuButton(content, "ToggleLocalPerformance", "", () =>
            {
                if (session.Graphics != null) session.Graphics.SetPerformanceOverlayVisible(!session.Graphics.PerformanceOverlayVisible);
                RefreshGraphics();
            });
            performanceToggleLabel = performanceToggle.GetComponentInChildren<UnityEngine.UI.Text>();
            var performanceDetails = MenuLabel(content, "ActualLocalPerformanceDetails", "", 14, Muted, 212);
            performanceDetails.alignment = TextAnchor.UpperLeft;
            var performanceOverlay = Label(hudRoot, "OptionalLocalPerformanceOverlay", "", 16, Muted, TextAnchor.UpperLeft,
                new Vector2(36, -350), new Vector2(380, 76), new Vector2(0, 1));
            performanceOverlayRect = performanceOverlay.rectTransform;
            graphicsPerformanceReadout = gameObject.AddComponent<PerformanceTelemetryReadout>();
            graphicsPerformanceReadout.Initialize(session.Graphics != null ? session.Graphics.PerformanceTelemetry : null, performanceOverlay, performanceDetails);
            resolutionExplanation = MenuLabel(content, "ResolutionExplanation", "", 15, Muted, 100);
            MenuLabel(content, "FrameCapExplanation", "Click particle detail and frame limit to cycle choices. Frame limits are requests to the browser.", 14, Muted, 52);
            MenuButton(content, "RestoreGraphicsDefaults", "RESTORE GRAPHICS DEFAULTS", () =>
            {
                session.Graphics?.ResetGraphicsDefaults();
                RefreshGraphics();
            });
            MenuButton(panel, "GraphicsBack", "BACK", () => SetPanel(JourneyPanel.Settings), 47);
        }

        private void RefreshGraphics()
        {
            if (session.Graphics == null || graphicsDescription == null) return;
            var quality = session.Graphics;
            var snapshot = new GraphicsUiSnapshot
            {
                Preset = quality.Preset, Particles = quality.ParticleQuality, FrameCap = quality.FrameRateCap,
                ShadowMap = quality.ShadowMapResolution, Lights = quality.AdditionalLightsPerObject,
                ShadowQuality020 = quality.ShadowQuality020, ShadowCascades020 = quality.ShadowCascadeCount020,
                Clouds020 = quality.CloudsEnabled020, WeatherEffects020 = quality.WeatherEffectsEnabled020, ShadowFiltering020 = quality.ShadowSoftFiltering020,
                ShadowMetres = Mathf.RoundToInt(quality.ShadowDistance), ParticlePercent = Mathf.RoundToInt(quality.EffectiveParticleFactor * 100),
                Scale = quality.ResolutionScale, EffectiveScale = quality.EffectiveResolutionScale, Brightness = quality.Brightness,
                Auto = quality.AutoResolutionEnabled, Bloom = quality.BloomEnabled, SoftShadows = quality.SoftShadowsEnabled, Reflections020 = quality.ReflectionsEnabled020,
                Customized = quality.PresetIsCustomized, Opaque = quality.OpaqueTextureEnabled,
                DistanceBlocks = quality.RenderDistanceBlocks, EffectiveMetres = Mathf.RoundToInt(quality.EffectiveRenderDistanceMetres),
                FarPercent = Mathf.RoundToInt(quality.FarDetailPixelation * 100), TextureDetail = quality.TextureDetailAvailable,
                Overlay = quality.PerformanceOverlayVisible
            };
            if (graphicsUiSnapshotValid && graphicsUiSnapshot.SameAs(snapshot)) return;
            graphicsUiSnapshot = snapshot;
            graphicsUiSnapshotValid = true;
            var selected = Mathf.Clamp(snapshot.Preset, 0, GraphicsPreferences.PresetCount - 1);
            graphicsDescription.text = GraphicsPresetDescriptions[selected] + (quality.PresetPreservesResolutionPolicy
                ? (snapshot.Customized
                    ? "\nCUSTOM EFFECTS  ·  Reselect Beta to reset effects; scale and automatic scaling stay as chosen."
                    : "\nPRESET EFFECTS  ·  Resolution limit and automatic scaling stay as chosen.")
                : (snapshot.Customized
                    ? "\nCUSTOM SETTINGS  ·  Select the preset again to restore its fixed values."
                    : "\nFIXED PRESET  ·  Selecting this preset switches automatic resolution off."));
            for (var i = 0; i < graphicsPresetButtons.Length; i++)
            {
                var active = i == selected;
                graphicsPresetButtonLabels[i].text = active && snapshot.Customized ? graphicsPresetCustomLabels[i] : graphicsPresetLabels[i];
                graphicsPresetButtonLabels[i].color = active ? Gold : Ivory;
                graphicsPresetButtons[i].targetGraphic.color = active ? new Color(.13f, .26f, .17f, .98f) : new Color(.08f, .12f, .105f, .98f);
            }
            resolutionSlider.SetValueWithoutNotify(quality.ResolutionScale);
            resolutionValue.text = Mathf.RoundToInt(quality.ResolutionScale * 100) + "%";
            resolutionHeading.text = quality.AutoResolutionEnabled ? "MAXIMUM RENDER RESOLUTION" : "RENDER RESOLUTION";
            autoResolutionValue.text = "AUTOMATIC RESOLUTION  ·  " + (quality.AutoResolutionEnabled ? "ON" : "OFF  (OPTIONAL)");
            effectiveResolutionValue.text = (quality.UsingFixedResolution ? "FIXED" : "ADAPTIVE") + " | CURRENT  " + Mathf.RoundToInt(quality.EffectiveResolutionScale * 100)
                + "%  ·  MAXIMUM  " + Mathf.RoundToInt(quality.ResolutionScale * 100) + "%";
            bloomValue.text = "BLOOM  ·  " + (quality.BloomEnabled ? "ON" : "OFF");
            shadowQualityDropdown020.SetValueWithoutNotify(quality.ShadowQuality020);
            cloudsValue020.text = "CLOUDS  ·  " + (quality.CloudsEnabled020 ? "ON" : "OFF");
            weatherEffectsValue020.text = "WEATHER EFFECTS  ·  " + (quality.WeatherEffectsEnabled020 ? "ON" : "OFF");
            reflectionValue020.text=quality.ReflectionsEnabled020?"REFLECTIONS  ·  ON":"REFLECTIONS  ·  OFF";
            brightnessSlider.SetValueWithoutNotify(quality.Brightness);
            brightnessValue.text = Mathf.RoundToInt(quality.Brightness * 100) + "%";
            particleValue.text = "PARTICLE DETAIL  ·  " + (quality.ParticleQuality == 0 ? "LOW" : quality.ParticleQuality == 1 ? "MEDIUM" : "HIGH");
            graphicsDrawState.text = "SHADOWS  " + (snapshot.ShadowMetres > 0 ? snapshot.ShadowMetres + " m  ·  MAP " + snapshot.ShadowMap + "  ·  CASCADES " + snapshot.ShadowCascades020 + (snapshot.ShadowFiltering020 ? "  ·  SOFT" : "  ·  HARD") : "OFF")
                + "\nADDITIONAL LIGHTS / OBJECT  " + snapshot.Lights + "  ·  SCENE COLOR COPY  " + (snapshot.Opaque ? "ON" : "OFF")
                + "\nEFFECTIVE PARTICLE FACTOR  " + snapshot.ParticlePercent + "%  ·  NO MOTION BLUR";
            frameCapValue.text = "FRAME RATE LIMIT  ·  " + (quality.FrameRateCap == 0 ? "UNCAPPED" : quality.FrameRateCap + " FPS");
            renderDistanceSlider.SetValueWithoutNotify(snapshot.DistanceBlocks);
            renderDistanceValue.text = snapshot.DistanceBlocks + " BLOCKS";
            renderDistanceStatus.text = "1 BLOCK = " + GraphicsPreferences.MetresPerBlock + " m | ACTUAL CAMERA REACH " + snapshot.EffectiveMetres
                + " m\nThe tutorial and selected preset may limit the requested distance.";
            farDetailSlider.SetValueWithoutNotify(quality.FarDetailPixelation);
            farDetailSlider.interactable = snapshot.TextureDetail;
            farDetailValue.text = snapshot.TextureDetail ? snapshot.FarPercent + "%" : "UNAVAILABLE";
            performanceToggleLabel.text = "LOCAL PERFORMANCE OVERLAY | " + (snapshot.Overlay ? "ON" : "OFF (OPTIONAL)");
            resolutionExplanation.text = quality.AutoResolutionEnabled
                ? "Automatic resolution may lower the scene toward a " + quality.AdaptiveTargetFrameRate
                    + " FPS target, up to your maximum. The interface stays sharp.\nUltra (Beta) keeps this choice; other presets turn it off. The frame cap stays separate."
                : "Scene resolution stays fixed while automatic resolution is off. Lower values give a softer image.\nUltra is the fresh-profile default. The frame cap is independent and does not guarantee a frame rate.";
        }

        private UnityEngine.UI.Slider SettingSlider(RectTransform parent, string name, UnityEngine.Events.UnityAction<float> changed,
            float minimum = 0, float maximum = 1)
        {
            var rect = Stretch(parent, name, 3, 31, 3, 12);
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var track = Stretch(rect, "Track", 0, 10, 0, 10);
            Paint(track, new Color(.20f, .25f, .23f, 1), true);
            var fill = Paint(Stretch(track, "Fill"), Gold);
            var handleArea = Stretch(rect, "HandleArea", 5, 0, 5, 0);
            var handle = Rect(handleArea, "Handle", new Vector2(0, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(11, 24));
            slider.targetGraphic = Paint(handle, Ivory, true);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.onValueChanged.AddListener(changed);
            return slider;
        }

        private void BuildPassiveTree()
        {
            var content = PanelContent(JourneyPanel.Tree, "FourElementPassiveTree", new Vector2(1240, 870));
            MenuLabel(content, "TreeTitle", "PASSIVE TREE", 34, Ivory, 58);
            treePoints = MenuLabel(content, "ActualSkillPoints", "", 18, Gold, 34);
            var tabs = Rect(content, "FourElementTabs", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 62));
            Height(tabs, 62);
            var tabLayout = tabs.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            tabLayout.spacing = 12;
            tabLayout.childControlHeight = tabLayout.childControlWidth = tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = true;
            treeTabButtons = new UnityEngine.UI.Button[4];
            treeTabLabels = new UnityEngine.UI.Text[4];
            for (var element = 0; element < 4; element++)
            {
                var e = element;
                treeElementTitles[e] = ElementName(e).ToUpperInvariant();
                treeTabButtons[e] = Button(tabs, "ElementTab" + e, treeElementTitles[e], Vector2.zero,
                    new Vector2(.5f, .5f), Vector2.zero, new Vector2(250, 62), () => SelectTreeBranch(e));
                treeTabLabels[e] = treeTabButtons[e].GetComponentInChildren<UnityEngine.UI.Text>();
                treeTabLabels[e].fontSize = 18;
                treeTabLabels[e].rectTransform.offsetMin = new Vector2(48, 4);
                var tabIcon = Rect(treeTabButtons[e].transform, "ElementTabEmblem", new Vector2(0, .5f), new Vector2(.5f, .5f),
                    new Vector2(29, 0), new Vector2(34, 34));
                var tabSprite = OracleElementEmblems.Get(e);
                if (tabSprite != null)
                {
                    var tabImage = Paint(tabIcon, Color.white);
                    tabImage.sprite = tabSprite;
                    tabImage.preserveAspect = true;
                    tabImage.raycastTarget = false;
                }
                else foreach (var graphic in DrawSigil(tabIcon, e)) graphic.color = TutorialElementColor(e);
                var tabFocus = treeTabButtons[e].gameObject.AddComponent<OraclePassiveCardFocus>();
                tabFocus.OnKeyboardFocus = () => { if (Time.frameCount > treeOpenedFrame) SelectTreeBranch(e); };
                for (var node = 0; node < 3; node++)
                {
                    var description = session.Progression != null ? session.Progression.UpgradeDescription(e, node) : "Passive — Upgrade details unavailable.";
                    var divider = description.IndexOf(" — ", StringComparison.Ordinal);
                    var name = divider >= 0 ? description.Substring(0, divider) : "Passive " + (node + 1);
                    // Keep decimal durations; full limits stay in the focused details.
                    var effect = PassivePrimaryEffect(e, node);
                    treeNodeNames[e, node] = name;
                    treeCardText[e, node] = "<size=22><b>" + name + "</b></size>\n<size=18>" + effect + "</size>\n<size=14>";
                }
            }
            var body = Rect(content, "SelectedBranchAndFocusedDetails", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 448));
            Height(body, 448);
            var bodyLayout = body.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            bodyLayout.spacing = 20;
            bodyLayout.childControlHeight = bodyLayout.childControlWidth = bodyLayout.childForceExpandWidth = bodyLayout.childForceExpandHeight = true;
            var cards = Rect(body, "ThreeUpgradeCards", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(440, 448));
            var cardSize = cards.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            cardSize.preferredWidth = 440;
            cardSize.flexibleWidth = .9f;
            var cardLayout = cards.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            cardLayout.spacing = 14;
            cardLayout.childControlHeight = cardLayout.childControlWidth = cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;
            treeNodeButtons = new UnityEngine.UI.Button[3];
            treeNodeLabels = new UnityEngine.UI.Text[3];
            treeCardEmblems = new UnityEngine.UI.Image[3];
            treeCardFallbacks = new OracleElementGlyph[3];
            for (var node = 0; node < 3; node++)
            {
                var n = node;
                treeNodeButtons[n] = MenuButton(cards, "UpgradeCard" + n, "", () => FocusTreeNode(n), 140);
                var focus = treeNodeButtons[n].gameObject.AddComponent<OraclePassiveCardFocus>();
                focus.OnKeyboardFocus = focus.OnPointerFocus = () => FocusTreeNode(n);
                treeNodeLabels[n] = treeNodeButtons[n].GetComponentInChildren<UnityEngine.UI.Text>();
                treeNodeLabels[n].alignment = TextAnchor.MiddleLeft;
                treeNodeLabels[n].supportRichText = true;
                treeNodeLabels[n].rectTransform.offsetMin = new Vector2(82, 10);
                treeNodeLabels[n].rectTransform.offsetMax = new Vector2(-18, -10);
                var cardIcon = Rect(treeNodeButtons[n].transform, "PassiveNodeIcon", new Vector2(0, .5f), new Vector2(.5f, .5f),
                    new Vector2(43, 0), new Vector2(64, 64));
                var spriteRoot = Stretch(cardIcon, "OptionalAtlasEmblem");
                treeCardEmblems[n] = Paint(spriteRoot, Color.white);
                treeCardEmblems[n].preserveAspect = true;
                treeCardEmblems[n].raycastTarget = false;
                var fallbackRoot = Stretch(cardIcon, "DrawnElementFallback", 7, 7, 7, 7);
                treeCardFallbacks[n] = DrawSigil(fallbackRoot, 0)[0] as OracleElementGlyph;
            }
            var details = Frame(body, "FocusedUpgradeDetails", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(600, 448), new Color(.035f, .065f, .048f, 1), MutedGold);
            var detailsSize = details.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            detailsSize.preferredWidth = 600;
            detailsSize.flexibleWidth = 1.1f;
            var detailsContent = Stretch(details, "DetailsContent", 22, 22, 22, 22);
            var detailsLayout = detailsContent.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            detailsLayout.spacing = 10;
            detailsLayout.childControlWidth = detailsLayout.childControlHeight = detailsLayout.childForceExpandWidth = true;
            detailsLayout.childForceExpandHeight = false;
            treeDetailTitle = MenuLabel(detailsContent, "FocusedUpgradeTitle", "", 24, Ivory, 34);
            treeDetailState = MenuLabel(detailsContent, "FocusedUpgradeState", "", 16, Gold, 28);
            var detailView = Rect(detailsContent, "FocusedDetailsScrollView", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 194));
            Height(detailView, 194);
            treeDetailScroll = detailView.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            treeDetailScroll.horizontal = false;
            treeDetailScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            treeDetailScroll.scrollSensitivity = 24;
            var detailViewport = Stretch(detailView, "Viewport");
            Paint(detailViewport, Color.white).raycastTarget = true;
            detailViewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            treeDetailDescription = Label(detailViewport, "FocusedUpgradeFullEffects", "", 19, Muted, TextAnchor.UpperLeft,
                Vector2.zero, new Vector2(0, 194), new Vector2(.5f, 1));
            var textRect = treeDetailDescription.rectTransform;
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.pivot = new Vector2(.5f, 1);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(0, 194);
            textRect.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            treeDetailScroll.viewport = detailViewport;
            treeDetailScroll.content = textRect;
            treeDetailDescription.alignment = TextAnchor.UpperLeft;
            treeLearnButton = MenuButton(detailsContent, "LearnFocusedUpgrade", "LEARN · COST 1 SKILL POINT", LearnFocusedTreeNode, 50);
            treeLearnLabel = treeLearnButton.GetComponentInChildren<UnityEngine.UI.Text>();
            MenuLabel(detailsContent, "TreeInspectionHint", "Hover or select a card; wheel / Page Up / Page Down reads details.\nLearn upgrades in order; each costs 1 skill point.", 14, Muted, 48);
            treeMessage = MenuLabel(content, "TreeFeedback", "", 16, Muted, 50);
            var close = MenuButton(content, "TreeCloseOrContinue", "CLOSE TREE", CloseTree, 50);
            treeCloseLabel = close.GetComponentInChildren<UnityEngine.UI.Text>();
        }

        private void SelectTreeBranch(int element)
        {
            if (element < 0 || element >= 4 || treeBranch == element) return;
            if (treePreview && treeUiSnapshotValid && treeUiSnapshot.GuideBranch >= 0 && treeUiSnapshot.GuideBranch != element) return;
            treeBranch = element;
            treeFocusedNode = 0;
            RefreshTree();
        }

        private void FocusTreeNode(int node)
        {
            if (node < 0 || node >= 3 || treeFocusedNode == node) return;
            if (treePreview && treeUiSnapshotValid && treeUiSnapshot.GuideNode >= 0 && treeUiSnapshot.GuideNode != node) return;
            treeFocusedNode = node;
            RefreshTree();
        }

        private void LearnFocusedTreeNode()
        {
            if (treePreview || session.Progression == null) return;
            var progression = session.Progression;
            var requested = progression.TryUpgrade(treeBranch, treeFocusedNode);
            treeMessage.text = requested
                ? progression.IsUnlocked(treeBranch, treeFocusedNode) ? "Passive learned." : "Upgrade requested. Waiting for the host."
                : "Learn the previous upgrade and earn 1 skill point first.";
            RefreshTree();
        }

        private void BuildOutcomePanel()
        {
            var content = PanelContent(JourneyPanel.Outcome, "OutcomePanel", new Vector2(800, 640));
            outcomeHeading = MenuLabel(content, "OutcomeHeading", "", 36, Ivory, 88);
            outcomeDescription = MenuLabel(content, "OutcomeDescription", "", 20, Muted, 126);
            campaignOutcomeReplay = MenuButton(content, "ReplayTutorial", "REPLAY TUTORIAL", () =>
            {
                session.Restart();
                session.BeginCampaign();
            }, 56);
            MenuButton(content, "OutcomeMainMenu", "RETURN TO MAIN MENU", ReturnToMainMenu);
        }

        private void BuildShrineMenu()
        {
            BuildCompactTowerPicker015();
        }

        private void BuildPortalPanel()
        {
            var content = PanelContent(JourneyPanel.Portal, "PortalJourneyChoices", new Vector2(1180, 960));
            MenuLabel(content, "PortalHeading", "BEYOND THE PORTAL", 34, Ivory, 62);
            campaignPortalDescription = MenuLabel(content, "PortalDescription", "Choose the journey beyond the tutorial hub.", 19, Muted, 42);
            var row = Rect(content, "MapChoices", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 435));
            Height(row, 435);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 22;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            portalCoopButton = Button(row, "CooperativeMapCard", "", Vector2.zero, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(680, 435), ActivateJourneyPortal);
            var coopSize = portalCoopButton.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            coopSize.minWidth = coopSize.preferredWidth = 660;
            coopSize.flexibleWidth = 1;
            var coop = portalCoopButton.transform as RectTransform;
            var imageArea = Rect(coop, "ArenaPreviewArea", new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -15), new Vector2(0, 226));
            imageArea.anchorMin = new Vector2(0, 1);
            imageArea.anchorMax = new Vector2(1, 1);
            imageArea.offsetMin = new Vector2(15, -241);
            imageArea.offsetMax = new Vector2(-15, -15);
            Paint(imageArea, new Color(.025f, .04f, .035f, 1));
            var picture = Stretch(imageArea, "ActualRuntimeCoopArenaPreview").gameObject.AddComponent<UnityEngine.UI.RawImage>();
            picture.texture = Resources.Load<Texture2D>("Interface/CoopMapPreview");
            picture.raycastTarget = false;
            picture.enabled = picture.texture != null;
            if (picture.texture != null)
            {
                var aspect = picture.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
                aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = picture.texture.width / (float)picture.texture.height;
            }
            else Label(imageArea, "ArenaCardTitle", "COOPERATIVE\nSHRINE DEFENSE", 30, Ivory, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(590, 150), new Vector2(.5f, .5f));
            campaignMapName = Label(coop, "CoopMapName", "SHRINE DEFENSE", 25, Gold, TextAnchor.UpperCenter,
                new Vector2(0, -252), new Vector2(626, 36), new Vector2(.5f, 1));
            campaignMapDescription = Label(coop, "CoopMapDescription", "40 waves · 8 milestones of five.\nFire · Water · Earth · Air towers.", 18, Ivory, TextAnchor.UpperCenter,
                new Vector2(0, -291), new Vector2(626, 50), new Vector2(.5f, 1));
            portalReadyLabel = Label(coop, "ActualLocalCoopReadyAction", "READY FOR CO-OP", 20, Emerald, TextAnchor.UpperCenter,
                new Vector2(0, -349), new Vector2(626, 28), new Vector2(.5f, 1));
            Label(coop, "PlannedGroupSize", ConfiguredPartyCapacity, 13, Gold, TextAnchor.UpperCenter,
                new Vector2(0, -382), new Vector2(626, 19), new Vector2(.5f, 1));
            Label(coop, "CurrentPlaytestCapacity", "PRIVATE ROOM | CAPACITY INCLUDES HOST", 13, Muted, TextAnchor.UpperCenter,
                new Vector2(0, -405), new Vector2(626, 24), new Vector2(.5f, 1));
            portalCampaignButton = Button(row, "SoloCampaignCard", "", Vector2.zero, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(340, 435), () => { });
            portalCampaignButton.interactable = false;
            var campaignSize = portalCampaignButton.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            campaignSize.minWidth = campaignSize.preferredWidth = 340;
            var solo = portalCampaignButton.transform as RectTransform;
            // Solo and parties share the same tower-defense campaign authority.
            portalCampaignButton.gameObject.SetActive(false);
            Label(solo, "SoloTitle", "SOLO\nCAMPAIGN", 29, Muted, TextAnchor.UpperCenter,
                new Vector2(0, -54), new Vector2(306, 92), new Vector2(.5f, 1));
            Label(solo, "SoloDescription", "A solo campaign is in development.\n\nPlay or skip the tutorial from the campfire menu.", 18, Muted, TextAnchor.UpperCenter,
                new Vector2(0, -167), new Vector2(294, 150), new Vector2(.5f, 1));
            portalCampaignExplanation = Label(solo, "ActualSoloAvailability", "", 16, Gold, TextAnchor.UpperCenter,
                new Vector2(0, -350), new Vector2(294, 65), new Vector2(.5f, 1));
            portalStatus = MenuLabel(content, "PortalActualConnectionFeedback", "", 17, Muted, 82);
            portalUnreadyButton = MenuButton(content, "WithdrawCoopReadiness", "UNREADY", WithdrawJourneyReadiness, 44);
            MenuButton(content, "PortalBack", "RETURN TO THE HUB", ResumeJourney, 47);
        }

        private void BuildLorePanel()
        {
            var page = PanelContent(JourneyPanel.Lore, "RefugeLoreBook", new Vector2(1000, 920));
            var paper = panels[JourneyPanel.Lore];
            paper.GetComponent<UnityEngine.UI.Image>().color = new Color(.94f, .90f, .80f, 1);
            var ink = new Color(.23f, .18f, .115f, 1);
            var paperShade = new Color(.50f, .36f, .18f, .12f);
            Paint(Stretch(paper, "PaperLeftShade", 8, 16, 978, 16), paperShade).raycastTarget = false;
            Paint(Stretch(paper, "PaperRightShade", 978, 16, 8, 16), paperShade).raycastTarget = false;
            for (var fibre = 0; fibre < 10; fibre++)
            {
                var mark = Rect(paper, "StaticPaperFibre" + fibre, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    new Vector2((fibre & 1) == 0 ? -375 : 375, -340 + fibre * 73), new Vector2(105 + fibre % 3 * 28, 1));
                Paint(mark, new Color(.42f, .31f, .17f, .045f)).raycastTarget = false;
            }
            bookInstantText = PlayerPrefs.GetInt(BookInstantPreference, 0) != 0;
            loreTitle = MenuLabel(page, "BookTitle", "", 30, ink, 64);
            loreTitle.fontStyle = FontStyle.Italic;
            var view = Rect(page, "BookScrollView", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 570));
            Height(view, 570);
            loreScroll = view.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Stretch(view, "Viewport", 10, 0, 10, 0);
            Paint(viewport, new Color(.975f, .95f, .875f, .8f), true);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
            var content = Rect(viewport, "Content", new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 24);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var body = Rect(content, "BookBody", Vector2.zero, new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 500));
            loreInk = body.gameObject.AddComponent<OracleInkRevealText>();
            loreBody = loreInk;
            loreBody.font = font;
            loreBody.fontSize = 22;
            loreBody.color = ink;
            loreBody.alignment = TextAnchor.UpperLeft;
            loreBody.raycastTarget = true;
            loreInk.WritingStateChanged = ForwardBookWritingState;
            loreInk.RevealStateChanged = RefreshBookRevealControls;
            loreBody.verticalOverflow = VerticalWrapMode.Overflow;
            loreScroll.content = content;
            loreScroll.viewport = viewport;
            loreScroll.horizontal = false;
            loreScroll.vertical = true;
            loreScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            loreScroll.scrollSensitivity = 30;
            var actions = Rect(page, "BookReadingControls", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 46));
            Height(actions, 46);
            var actionLayout = actions.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            actionLayout.spacing = 12;
            actionLayout.childControlWidth = actionLayout.childControlHeight = actionLayout.childForceExpandWidth = actionLayout.childForceExpandHeight = true;
            loreShowFull = Button(actions, "ShowFullBookText", "SHOW FULL TEXT", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(420, 46), () => loreInk.FinishReveal());
            loreInstantToggle = Button(actions, "ToggleInstantBookText", "", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(420, 46), () => SetInstantBookText(!bookInstantText));
            loreInstantLabel = loreInstantToggle.GetComponentInChildren<UnityEngine.UI.Text>();
            StylePaperButton(loreShowFull, ink);
            StylePaperButton(loreInstantToggle, ink);
            loreHint = MenuLabel(page, "BookScrollHint", "", 14, new Color(.40f, .31f, .19f), 32);
            StylePaperButton(MenuButton(page, "CloseBook", "RETURN TO THE REFUGE", ResumeJourney, 49), ink);
            RefreshBookRevealControls(false);
        }

        private static void StylePaperButton(UnityEngine.UI.Button button, Color ink)
        {
            button.targetGraphic.color = new Color(.82f, .76f, .62f, 1);
            button.GetComponentInChildren<UnityEngine.UI.Text>().color = ink;
            var colors = button.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1.08f, 1.05f, .96f, 1);
            colors.pressedColor = new Color(.82f, .78f, .67f, 1);
            colors.disabledColor = new Color(.70f, .68f, .61f, .85f);
            button.colors = colors;
        }

        private void RefreshBookRevealControls(bool revealing)
        {
            if (loreShowFull == null || loreHint == null) return;
            loreShowFull.interactable = revealing;
            loreInstantToggle.interactable = !bookSystemReducedMotion;
            loreInstantLabel.text = bookSystemReducedMotion ? "REDUCED MOTION | INSTANT TEXT"
                : bookInstantText ? "INSTANT TEXT | ON" : "INSTANT TEXT | OFF";
            loreHint.text = revealing ? "Click the writing or Show full text to finish. Scroll / Page Up / Page Down to read."
                : "Full text shown. Scroll / Page Up / Page Down to read; Return closes the book.";
        }

        private void SetInstantBookText(bool enabled)
        {
            bookInstantText = enabled;
            if (enabled) loreInk?.FinishReveal();
            RefreshBookRevealControls(loreInk != null && loreInk.IsRevealing);
            try { PlayerPrefs.SetInt(BookInstantPreference, enabled ? 1 : 0); PlayerPrefs.Save(); }
            catch (Exception) { loreHint.text = "Text mode changed for this session; your browser could not save it."; }
        }

        private void ForwardBookWritingState(bool writing)
        {
            if (bookWritingForwarded == writing) return;
            bookWritingForwarded = writing;
            BookWritingStateChanged?.Invoke(writing);
        }

        private void StopBookReveal()
        {
            loreInk?.CancelReveal();
            ForwardBookWritingState(false);
        }

        private void OnDisable() { HideNibwickControls020(); CloseCampaignBookImmediate020(); HideAnvilPreview(); menuShowcase?.SetVisible(false); StopBookReveal(); session?.World?.EndRaykonTalk(); }

        public void ShowLoreBook(string title, string body)
        {
            if (!built || session == null || !session.HasStarted || session.IsDead || session.IsDowned || session.Victory || ShopVisible) return;
            HideAchievementBadges020();
            HideNibwickControls020();
            ForgetRefugeNpcConversation();
            AbilityBindings.CancelRebind();
            session.Pause();
            session.Audio?.PlayCue("book_open");
            loreTitle.text = title ?? "Refuge chronicles";
            bookSystemReducedMotion = OracleBookAccessibility.PrefersReducedMotion;
            loreInk.BeginReveal(body, bookInstantText || bookSystemReducedMotion);
            SetPanel(JourneyPanel.Lore);
            Canvas.ForceUpdateCanvases();
            loreScroll.verticalNormalizedPosition = 1;
        }

        private void BuildCampaignHud()
        {
            tutorialPrompt = Frame(hudRoot, "TutorialInstructionPanel", new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -148), new Vector2(680, 108), new Color(.025f, .04f, .033f, .83f), MutedGold);
            tutorialHeading = Label(tutorialPrompt, "LessonHeading", "", 20, Gold, TextAnchor.UpperCenter,
                new Vector2(0, -8), new Vector2(630, 26), new Vector2(.5f, 1));
            tutorialInstruction = Label(tutorialPrompt, "LessonInstruction", "", 18, Ivory, TextAnchor.UpperCenter,
                new Vector2(0, -38), new Vector2(630, 64), new Vector2(.5f, 1));
            tutorialInstruction.gameObject.SetActive(false);
            BuildTutorialSkipHint();
            captionPanel = Rect(tutorialPrompt, "ExactPlayingCaption", new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -112), new Vector2(852, 126));
            captionText = Label(captionPanel, "FullNarrationCaption", "", 17, Ivory, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(852, 126), new Vector2(.5f, .5f));
            captionPanel.gameObject.SetActive(false);
            BuildPracticeInteractionPrompt();BuildCompactGuide015();
            BuildObjectiveCompass();
            var shrine = Frame(hudRoot, "ShrineHealth", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-36, 62), new Vector2(327, 95), Panel, MutedGold);
            Label(shrine, "ShrineName", "GREEN SHRINE", 15, Gold, TextAnchor.UpperLeft,
                new Vector2(17, -14), new Vector2(180, 24), new Vector2(0, 1));
            shrineHealthText = Label(shrine, "ActualShrineHealth", "", 16, Ivory, TextAnchor.UpperRight,
                new Vector2(-17, -14), new Vector2(130, 24), new Vector2(1, 1));
            var bar = Rect(shrine, "ShrineHealthBar", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(18, -50), new Vector2(291, 13));
            Paint(bar, new Color(.19f, .13f, .12f, 1));
            shrineHealthFill = Paint(Stretch(bar, "ActualHealthFill"), Emerald);
            Border(bar, Gold, 1);
            Label(shrine, "ShrineWarning", "PROTECT THE SHRINE", 10, Muted, TextAnchor.UpperLeft,
                new Vector2(18, -72), new Vector2(291, 17), new Vector2(0, 1));
            roomHudStatus = Label(hudRoot, "ActualConnectedRoom", "", 22, Ivory, TextAnchor.UpperRight,
                new Vector2(-36, -276), new Vector2(335, 52), new Vector2(1, 1));
            roomHudStatus.resizeTextForBestFit=true;roomHudStatus.resizeTextMinSize=14;roomHudStatus.resizeTextMaxSize=22;
            comboText = Label(hudRoot, "ActualElementCombo", "", 17, Gold, TextAnchor.MiddleCenter,
                new Vector2(0, 58), new Vector2(620, 36), new Vector2(.5f, .5f));
            autoFireStatus = Label(hudRoot, "ActualConfirmedCastingStatus", "MANUAL CAST | SELECT 1, 2 OR ALL 4 ELEMENTS", 14, Gold, TextAnchor.MiddleCenter,
                new Vector2(0, 63), new Vector2(1000, 24), new Vector2(.5f, 0));
            autoFireStatus.gameObject.SetActive(false);
            defensePanel = Frame(hudRoot, "CooperativeDefenseStatus", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-36, -334), new Vector2(335, 222), Panel, MutedGold);
            defenseTimer = Label(defensePanel, "ActualRemainingTime", "", 24, Ivory, TextAnchor.UpperLeft,
                new Vector2(17, -15), new Vector2(301, 34), new Vector2(0, 1));
            defenseWave = Label(defensePanel, "ActualWaveAdmissionAndLivingCount", "", 15, Gold, TextAnchor.UpperLeft,
                new Vector2(17, -55), new Vector2(301, 44), new Vector2(0, 1));
            var waveBar = Rect(defensePanel, "ActualWaveAdmissionProgress", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(17, -107), new Vector2(301, 4));
            Paint(waveBar, new Color(.15f, .09f, .22f, .95f));
            defenseWaveFill = Paint(Stretch(waveBar, "WaveAdmissionFill"), Gold);
            defenseStatus = Label(defensePanel, "ActualDefenseStatusAndTokens", "", 14, Muted, TextAnchor.UpperLeft,
                new Vector2(17, -120), new Vector2(301, 89), new Vector2(0, 1));
            downedCard = Frame(hudRoot, "DownedReviveStatus", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, -10), new Vector2(860, 224), new Color(.02f, .03f, .026f, .87f), MutedGold);
            downedTitle = Label(downedCard, "DownedWindowTitle", "", 21, Gold, TextAnchor.UpperCenter,
                new Vector2(0, -16), new Vector2(804, 30), new Vector2(.5f, 1));
            downedCountdown = Label(downedCard, "ActualDownedCountdown", "", 37, Ivory, TextAnchor.UpperCenter,
                new Vector2(0, -54), new Vector2(804, 48), new Vector2(.5f, 1));
            downedHint = Label(downedCard, "ActualDownedHint", "", 18, Ivory, TextAnchor.UpperCenter,
                new Vector2(0, -116), new Vector2(804, 56), new Vector2(.5f, 1));
            var reviveBar = Rect(downedCard, "ReviveProgress", new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 23), new Vector2(748, 7));
            Paint(reviveBar, new Color(.12f, .17f, .14f, 1));
            reviveFill = Paint(Stretch(reviveBar, "ActualReviveProgressFill"), Emerald);
            Border(reviveBar, MutedGold, 1);
            downedCard.gameObject.SetActive(false);
            downedResumeButton = Button(hudRoot, "ResumeDownedAfterFocusLoss", "RESUME & LOCK MOUSE", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-36, 180), new Vector2(300, 46), () => session.Resume());
            downedResumeButton.gameObject.SetActive(false);
            allyReviveProgress = Rect(hudRoot, "NearbyAllyReviveProgress", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, -119), new Vector2(420, 6));
            Paint(allyReviveProgress, new Color(.10f, .15f, .12f, 1));
            allyReviveFill = Paint(Stretch(allyReviveProgress, "ActualNearbyAllyReviveFill"), Emerald);
            Border(allyReviveProgress, MutedGold, 1);
            allyReviveProgress.gameObject.SetActive(false);
        }

        public void ShowSkillTree(bool preview = false)
        {
            if (!built || session == null || !session.HasStarted || session.IsDead || session.IsDowned || session.Victory || ShopVisible) return;
            treeReturnsToPause = session.Paused;
            treePreview = preview;
            treeOpenedFrame = Time.frameCount;
            treeUiSnapshotValid = false;
            if (!preview)
            {
                treeBranch = session.Progression != null ? session.Progression.SelectedElement : 0;
                treeFocusedNode = 0;
            }
            AbilityBindings.CancelRebind();
            session.Pause();
            treeMessage.text = preview
                ? "READ-ONLY GUIDE PREVIEW  ·  Follow the highlighted upgrade, then continue."
                : session.Tutorial != null && session.Tutorial.AwaitingSkillsMenu
                    ? "Choose an element and inspect an upgrade. Learn costs 1 real skill point. No points? Inspect now and return after collecting real XP. PLAY NEXT continues."
                    : "Choose an element, inspect an upgrade, then choose Learn. Each upgrade costs 1 skill point.";
            treeCloseLabel.text = preview ? "CONTINUE GUIDE" : "CLOSE TREE";
            RefreshTree();
            SetPanel(JourneyPanel.Tree);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(treePreview
                    ? treeNodeButtons[treeFocusedNode].gameObject : treeTabButtons[treeBranch].gameObject);
        }

        public void ClosePanels()
        {
            session?.World?.EndRaykonTalk();
            session?.Builder?.CancelPlacement();
            AbilityBindings.CancelRebind();
            treePreview = false;
            SetPanel(JourneyPanel.None);
        }

        public void ShowBuildMenu()
        {
            if (!built || session == null || !session.IsPlaying || currentPanel != JourneyPanel.None || session.IsDead || session.IsDowned || session.Victory || ShopVisible
                || session.FieldSmith != null && session.FieldSmith.Visible
                || session.Builder == null || !session.Builder.CanOpenHoveredBuildPad || session.Defense == null || !session.Defense.Running) return;
            bool opened=!CompactBuildPickerVisible015;
            ShowCompactTowerPicker015();
            if (opened) session.Audio?.PlayProductionCue(ProductionCue.TowerBuilderOpen);
        }

        public void HideBuildMenu()
        {
            HideCompactTowerPicker015();
        }

        public void ShowPortalChoices()
        {
            if (!built || session == null || !session.HasStarted || session.IsDead || session.IsDowned || session.Victory || ShopVisible
                || (session.Defense != null && session.Defense.Running) || currentPanel == JourneyPanel.Portal) return;
            AbilityBindings.CancelRebind();
            session.Pause();
            SetPanel(JourneyPanel.Portal);
        }

        private void CloseTree()
        {
            if (currentPanel != JourneyPanel.Tree) return;
            if (session.Tutorial != null && session.Tutorial.AwaitingSkillsMenu)
            {session.Tutorial.ContinueAfterSkillsMenu();return;}
            if (treePreview)
            {
                treePreview = false;
                // AdvanceGuide owns the associated ClosePanels/Resume transition.
                session.Tutorial?.AdvanceGuide();
                return;
            }
            if (treeReturnsToPause) SetPanel(JourneyPanel.Pause);
            else ResumeJourney();
        }

        private void OpenSettings(JourneyPanel from)
        {
            settingsReturn = from;
            RefreshSettings();
            SetPanel(JourneyPanel.Settings);
        }

        private void ReturnToMainMenu()
        {
            AbilityBindings.CancelRebind();
            treePreview = false;
            session.Restart();
            SetPanel(JourneyPanel.Start);
        }

        private void ResumeJourney()
        {
            if (CampaignUiTerminal) return;
            AbilityBindings.CancelRebind();
            SetPanel(JourneyPanel.None);
            session.Resume();
        }

        private void SetPanel(JourneyPanel panel)
        {
            if(panel!=JourneyPanel.None&&session!=null&&session.Builder!=null&&session.Builder.IsPlacing)session.Builder.CancelPlacement();
            if (currentPanel == JourneyPanel.Lore && panel != JourneyPanel.Lore) { HideNibwickControls020(); StopBookReveal(); session?.World?.EndRaykonTalk(); }
            if (panel != JourneyPanel.Settings && progressionResetArmed)
            {
                progressionResetArmed = false;
                progressionResetButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "RESET SAVED PROGRESSION";
                progressionResetStatus.text = "Clears this Oracle's saved progression on this device only.";
                progressionResetStatus.color = Muted;
            }
            if(panel!=JourneyPanel.FieldForge||IntroActive||LoadingActive||DownedActive||ShopVisible)HideAnvilPreview();
            if(currentPanel!=panel)CloseMenuExtras020(panel);
            if(!menuBack015)menuAncestry015.Navigate((int)currentPanel,(int)panel,(int)JourneyPanel.None,(int)JourneyPanel.Start);
            if(currentPanel==JourneyPanel.TutorialInstruction&&panel!=JourneyPanel.TutorialInstruction&&panel!=JourneyPanel.Settings)guideCursorReturn015=false;
            currentPanel = panel;
            if(panel!=JourneyPanel.Pause)HidePauseGuide020();
            foreach (var entry in panels) entry.Value.gameObject.SetActive(entry.Key == panel);
            var blocked = IntroActive || LoadingActive || DownedActive || ShopVisible;
            if (menuRoot != null) menuRoot.gameObject.SetActive(panel != JourneyPanel.None && !blocked);
            if (menuInput != null) menuInput.interactable = menuInput.blocksRaycasts = !blocked;
            if (loreInk != null) loreInk.SetPresentationActive(panel == JourneyPanel.Lore && !blocked);
            if (graphicsPerformanceReadout != null) graphicsPerformanceReadout.SetMenuOpen(panel == JourneyPanel.Graphics && !blocked);
            SetCustomizationVisible(panel == JourneyPanel.Customize && !blocked);
            if(panel!=JourneyPanel.Start||blocked||session==null||session.HasStarted)menuShowcase?.SetVisible(false);
            if (menuDimmer != null) menuDimmer.color = panel == JourneyPanel.Start && session != null && !session.HasStarted
                ? new Color(.008f, .014f, .012f, .12f) : new Color(.015f, .022f, .022f, .77f);
            if (EventSystem.current == null) return;
            UnityEngine.UI.Button selected = null;
            if (!blocked && panel != JourneyPanel.None && panels.ContainsKey(panel))
                foreach (var candidate in panels[panel].GetComponentsInChildren<UnityEngine.UI.Button>())
                    if (candidate.isActiveAndEnabled && candidate.interactable) { selected = candidate; break; }
            EventSystem.current.SetSelectedGameObject(selected != null ? selected.gameObject : null);
        }

        private void UpdateExpandedMenu()
        {
            if (menuRoot == null) return;
            TrackMenuContext015();RefreshGuidePauseButtons015();
            if (UpdateIntroOverlay()) { SetCustomizationVisible(false); return; }
            if (UpdateLoadingOverlay()) { SetCustomizationVisible(false); return; }
            if (DownedActive || ShopVisible)
            {
                if (currentPanel != JourneyPanel.None) SetPanel(JourneyPanel.None);
                menuRoot.gameObject.SetActive(false);
                menuInput.interactable = menuInput.blocksRaycasts = false;
                // The shop is a full modal; downed feedback remains visible.
                hudRoot.gameObject.SetActive(session.HasStarted && DownedActive);
                pauseButton.gameObject.SetActive(false);
                crosshair.gameObject.SetActive(false);
                observedStarted = session.HasStarted;
                return;
            }
            if (session.AwaitingBrowserPointerCapture)
            {
                // The HTML capture notice owns this explicit resume gesture.
                // A Settings/forge/menu click must never blindly lock the canvas.
                if (currentPanel != JourneyPanel.None) SetPanel(JourneyPanel.None);
                menuRoot.gameObject.SetActive(false);
                menuInput.interactable = menuInput.blocksRaycasts = false;
                hudRoot.gameObject.SetActive(false);
                pauseButton.gameObject.SetActive(false);
                crosshair.gameObject.SetActive(false);
                observedStarted = session.HasStarted;
                return;
            }
            menuInput.interactable = menuInput.blocksRaycasts = true;
            RefreshTutorialInstructionPresentation();
            var failure = session.IsDead || !string.IsNullOrEmpty(session.FailureReason) || CampaignUiFailed;
            if (session.HasStarted && (failure || session.Victory || CampaignUiTerminal))
            {
                var shrineLost = session.Combat != null && session.Combat.Shrine != null && session.Combat.Shrine.Health <= 0;
                outcomeHeading.text = failure ? shrineLost ? "THE SHRINE FALLS" : "THE ORACLE FALLS"
                    : session.CoopMode ? "THE SHRINE ENDURES" : "JOURNEY COMPLETE";
                outcomeDescription.text = failure
                    ? (string.IsNullOrEmpty(session.FailureReason) ? "The Oracle's vitality is spent. Begin again with renewed strength." : session.FailureReason)
                    : session.CoopMode ? "Ten minutes survived.\nYour Oracles protected the green shrine."
                    : "The guardians have fallen. The green shrine endures.";
                ApplyCampaignOutcome();
                if (currentPanel != JourneyPanel.Outcome) SetPanel(JourneyPanel.Outcome);
            }
            else if (!session.HasStarted)
            {
                if (observedStarted || currentPanel == JourneyPanel.None || currentPanel == JourneyPanel.Pause || currentPanel == JourneyPanel.Outcome)
                    SetPanel(JourneyPanel.Start);
            }
            else if (!observedStarted && currentPanel != JourneyPanel.Tree && currentPanel != JourneyPanel.Build && currentPanel != JourneyPanel.TutorialInstruction)
                SetPanel(JourneyPanel.None);
            else if (session.Paused && currentPanel == JourneyPanel.None)
                SetPanel(JourneyPanel.Pause);
            else if (!session.Paused && (currentPanel == JourneyPanel.Pause || currentPanel == JourneyPanel.Choice
                || currentPanel == JourneyPanel.Settings || currentPanel == JourneyPanel.Minimap || currentPanel == JourneyPanel.Controls || currentPanel == JourneyPanel.Graphics || currentPanel == JourneyPanel.Audio || currentPanel == JourneyPanel.Acoustics015 || currentPanel == JourneyPanel.MusicLibrary009 || currentPanel == JourneyPanel.TutorialPortalSkip || currentPanel == JourneyPanel.Customize || currentPanel == JourneyPanel.RaykonMentor || currentPanel == JourneyPanel.TrainingLab))
                SetPanel(JourneyPanel.None);
            observedStarted = session.HasStarted;
            // Full menus own the view. Keep guide and minimap previews visible.
            hudRoot.gameObject.SetActive(session.HasStarted && (currentPanel == JourneyPanel.None
                || currentPanel == JourneyPanel.Minimap || IsGuidePreviewVisible));
            var unobscured = session.HasStarted && !session.Paused && currentPanel == JourneyPanel.None && !failure && !session.Victory;
            pauseButton.gameObject.SetActive(false);
            if(pointerButtonLabel015!=null)
            {
                string text=session.CursorFree015?"RESUME":"PAUSE";
                if(pointerButtonLabel015.text!=text)pointerButtonLabel015.text=text;
            }
            crosshair.gameObject.SetActive(unobscured&&!session.CursorFree015);
            if (replayVoiceButton != null)
            {
                replayVoiceButton.gameObject.SetActive(!CampaignHubHudMode020);
            }
            skillTreeButton.gameObject.SetActive(!CampaignHubHudMode020&&(session.CombatUnlocked || CampaignUiInRefuge));
            skipTutorialPauseButton.gameObject.SetActive(CanOfferTutorialSkip);
            var connectionEnded = session.CoopMode && session.Rooms != null && !session.Rooms.Connected;
            pauseDescription.fontSize = connectionEnded || CampaignUiHasSoloRestartNotice ? 17 : 19;
            RefreshCampaignCopy();
            pauseDescription.text = CampaignUiHasSoloRestartNotice ? campaignPauseCopy : connectionEnded ? "ROOM CONNECTION ENDED\n" + session.Rooms.Status
                : CampaignUiActive ? campaignPauseCopy : "Your journey waits.\nReturn when you are ready.";
            RefreshCampaignHubPauseMenu020();
            RefreshPauseGuide020();
        }

        private void UpdateExpandedUi()
        {
            RefreshQuickPortalButton();RefreshExitPanel020();
            TickMenuShowcase();
            TickAnvilPreview();
            RefreshRaykonMentor();
            RefreshButtonMotion();
            RefreshRoomStatus();
            RefreshTutorialSkipAccess();
            if (currentPanel == JourneyPanel.Pause && pauseMore020 && pauseControlsBinding020!=GameActionBindings.LastChangeFrame)
            {
                pauseControlsBinding020=GameActionBindings.LastChangeFrame;
                pauseControls.text = "Move " + GameActionBindings.GetLabel("Forward") + "/" + GameActionBindings.GetLabel("Left")
                    + "/" + GameActionBindings.GetLabel("Back") + "/" + GameActionBindings.GetLabel("Right")
                    + "  ·  Run " + GameActionBindings.GetLabel("Sprint") + "  ·  Jump " + GameActionBindings.GetLabel("Jump")
                    + "\nInteract " + GameActionBindings.GetLabel("Interact") + "  ·  Tree " + GameActionBindings.GetLabel("SkillTree") + "  ·  ESC Pause"
                    + "\nPrepare 1 / 2 / 3 elements, or 4 for Nova. Triple volleys clear after firing.\nClick to cast, or hold click and press fresh element keys. Global interval 0.5s; elements 4s; Nova 5s. Optional auto waits 0.5s after selection.";
                skillTreeButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "PASSIVE TREE  [" + GameActionBindings.GetLabel("SkillTree") + "]";
            }
            if (currentPanel == JourneyPanel.Settings || currentPanel == JourneyPanel.Audio) RefreshSettings();
            RefreshMusicLibrary009();
            RefreshAcousticSettings015();
            RefreshAchievementBadgeLayout020();
            if (currentPanel == JourneyPanel.Minimap) RefreshInterfaceSettings();
            if (currentPanel == JourneyPanel.Controls) RefreshControls();
            if (currentPanel == JourneyPanel.Graphics) RefreshGraphics();
            if (currentPanel == JourneyPanel.Customize) UpdateCustomizationNavigation();
            RefreshTowerBuildMenu();
            if (currentPanel == JourneyPanel.Lore && Application.isFocused && !AbilityBindings.IsRebinding)
            {
                var bookKeyboard = UnityEngine.InputSystem.Keyboard.current;
                if (bookKeyboard != null && bookKeyboard.pageDownKey.wasPressedThisFrame)
                    loreScroll.verticalNormalizedPosition = Mathf.Clamp01(loreScroll.verticalNormalizedPosition - .25f);
                else if (bookKeyboard != null && bookKeyboard.pageUpKey.wasPressedThisFrame)
                    loreScroll.verticalNormalizedPosition = Mathf.Clamp01(loreScroll.verticalNormalizedPosition + .25f);
            }
            if (currentPanel == JourneyPanel.Tree)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (!treePreview && Application.isFocused && keyboard != null && !AbilityBindings.IsRebinding
                    && Time.frameCount > treeOpenedFrame && Time.frameCount > AbilityBindings.LastChangeFrame
                    && keyboard[GameActionBindings.GetKey("SkillTree")].wasPressedThisFrame)
                {
                    CloseTree();
                    return;
                }
                if (Application.isFocused && keyboard != null && !AbilityBindings.IsRebinding)
                {
                    if (keyboard.pageDownKey.wasPressedThisFrame)
                        treeDetailScroll.verticalNormalizedPosition = Mathf.Clamp01(treeDetailScroll.verticalNormalizedPosition - .25f);
                    else if (keyboard.pageUpKey.wasPressedThisFrame)
                        treeDetailScroll.verticalNormalizedPosition = Mathf.Clamp01(treeDetailScroll.verticalNormalizedPosition + .25f);
                }
                RefreshTree();
            }
            if (currentPanel == JourneyPanel.Portal && !RefreshCampaignPortal())
            {
                RestoreOuterPortalCopy();
                var rooms = session.Rooms;
                var busy = rooms != null && rooms.Busy;
                portalCampaignButton.interactable = false;
                portalCampaignExplanation.text = "COMING SOON";
                var ready = rooms != null && rooms.Connected && rooms.LocalReady;
                portalCoopButton.interactable = !busy && !ready;
                portalReadyLabel.text = ready ? "WAITING FOR PARTY" : "READY FOR CO-OP";
                portalUnreadyButton.gameObject.SetActive(ready);
                portalStatus.text = rooms != null && rooms.Connected
                    ? rooms.PartyReadyStatus + "\nREADY  " + rooms.ReadyPlayerCount + " / " + rooms.PlayerCount + " ORACLES"
                    : busy ? rooms.Status : "Ready for cooperative defense to create a room.";
            }
            var abilityPreview = AbilityGuidePreviewIndex >= 0 && AbilityGuidePreviewIndex < 4;
            spellRoot.gameObject.SetActive(session.HasStarted && (session.CombatUnlocked || abilityPreview) && session.Progression != null && !session.IsDowned);
            var tutorial = session.Tutorial;
            var instructionVisible = session.HasStarted && (!session.Paused || session.IsDowned)
                && (!session.IsDead || session.IsDowned) && !session.Victory;
            var live = instructionVisible && !session.IsDowned;
            RefreshConfirmedInputPresentation(live);
            comboText.text = !live || !session.CombatUnlocked || session.Combat == null ? ""
                : session.Combat.CastIndicator == "CAST" ? "CAST" : session.Combat.HitFlash > 0f ? "HIT" : "";
            RefreshSingleTopGuide(instructionVisible, tutorial);
            RefreshPracticeInteractionPrompt();
            RefreshObjectiveCompass();
            downedCard.gameObject.SetActive(DownedActive);
            downedResumeButton.gameObject.SetActive(DownedActive && session.Paused);
            allyReviveProgress.gameObject.SetActive(live && !string.IsNullOrEmpty(session.DownedHint) && session.ReviveProgress > 0);
            SetFill(allyReviveFill, session.ReviveProgress);
            if (DownedActive)
            {
                downedTitle.text = session.CanBeRevived ? "YOU ARE DOWNED  ·  REVIVE WINDOW" : "YOU ARE DOWNED  ·  TIME REMAINING";
                downedCountdown.text = Mathf.Max(0, Mathf.CeilToInt(session.ReviveRemainingSeconds)) + "s";
                downedHint.text = session.DownedHint;
                SetFill(reviveFill, session.ReviveProgress);
            }
            if (session.Combat != null && session.Combat.Shrine != null)
            {
                var shrine = session.Combat.Shrine;
                shrineHealthText.text = Mathf.CeilToInt(Mathf.Max(0, shrine.Health)) + " / " + Mathf.RoundToInt(shrine.MaxHealth);
                SetFill(shrineHealthFill, shrine.Health / Mathf.Max(1, shrine.MaxHealth));
                shrineHealthFill.color = shrine.Health < shrine.MaxHealth * .25f ? new Color(.79f, .33f, .27f) : Emerald;
            }
            var showDefense = session.Rooms != null && session.Rooms.Connected && session.Defense != null;
            defensePanel.gameObject.SetActive(showDefense);
            if (showDefense)
            {
                var seconds = Mathf.Max(0, Mathf.CeilToInt(session.Defense.RemainingSeconds));
                defenseTimer.text = session.Defense.Running ? "DEFENSE  " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") : "SHARED ROOM";
                RefreshDefenseWaveCopy015();
                SetFill(defenseWaveFill, session.Defense.Running ? session.Defense.SpawnedThisWave / (float)Mathf.Max(1, session.Defense.WaveQuota) : 0);
                defenseStatus.text = session.Defense.Status + "\nTOKENS " + session.Defense.LocalBuildTokens
                    + " | SPAWN " + session.Defense.AdmissionLaneCount + " | DRAIN " + session.Defense.DrainingLaneCount + "\n"
                    + ActualSharedPadHint();
            }
            if (live && !string.IsNullOrEmpty(session.DownedHint)) interactionText.text = session.DownedHint;
            else if (session.Builder != null && session.Builder.IsPlacing && live) interactionText.text = session.Builder.Hint;
            else if (session.FieldSmith != null && !string.IsNullOrEmpty(session.FieldSmith.InteractionHint) && live) interactionText.text = session.FieldSmith.InteractionHint;
            else if (session.Builder != null && !string.IsNullOrEmpty(session.Builder.Hint) && live) interactionText.text = session.Builder.Hint;
            else if (session.Shop != null && !string.IsNullOrEmpty(session.Shop.InteractionHint) && live) interactionText.text = session.Shop.InteractionHint;
            else if (session.RefugeNpcs != null && !string.IsNullOrEmpty(session.RefugeNpcs.InteractionHint) && live) interactionText.text = session.RefugeNpcs.InteractionHint;
            else if (session.Books != null && !string.IsNullOrEmpty(session.Books.Hint) && live) interactionText.text = session.Books.Hint;
            else if (session.Progression != null && !string.IsNullOrEmpty(session.Progression.HeartcrystalHint) && live) interactionText.text = session.Progression.HeartcrystalHint;
            ApplyMinimapPreferences();
        }

        private void RefreshRoomStatus()
        {
            if (roomStatus == null) return;
            var rooms = session.Rooms;
            var busy = rooms != null && rooms.Busy;
            createRoomButton.interactable = RoomMenuRequestAllowed020() && rooms.CanHostRoom020(roomMenuMode020);
            joinRoomButton.interactable = RoomMenuRequestAllowed020() && rooms.CanJoinRoom020(roomMenuMode020);
            roomCodeInput.interactable = !busy;
            roomQuickHost020.interactable=false;
            bool selectable=!busy&&(rooms==null||!rooms.Connected);
            roomCampaignMode020.interactable=roomDefenseMode020.interactable=selectable;
            campaignButton.interactable = false;
            bool online=rooms!=null&&rooms.RoomOnline020;
            bool failed=rooms!=null&&rooms.RoomPhase020==RoomConnectionPhase020.Failed;
            RefreshRoomRetry020(failed);
            roomStatus.text=busy||online||failed?rooms.RoomStatus020:!string.IsNullOrEmpty(localRoomMessage)?localRoomMessage
                :roomMenuMode020==CoopRoomMode020.CampaignTown&&(rooms==null||!rooms.SupportsRoomMode020(roomMenuMode020))?CampaignTownPreparing020
                :rooms!=null?rooms.RoomStatus020:"Room service is not connected.";
            RefreshRoomRoster020(online,online?rooms.RoomShareCode020:"",online?rooms.PlayerCount:0,online?rooms.RoomCapacity020:0);
            roomStatus.color=online?Emerald:Muted;
            RefreshGameWait020(busy);
        }

        private void RefreshButtonMotion()
        {
            var quality = session.Graphics != null ? session.Graphics.ParticleQuality : 1;
            if (quality == appliedUiParticleQuality) return;
            appliedUiParticleQuality = quality;
            foreach (var button in hoverButtons)
            {
                if (button == null) continue;
                var colors = button.colors;
                colors.fadeDuration = quality == 0 ? 0 : .12f;
                button.colors = colors;
            }
        }

        private void RefreshConfirmedInputPresentation(bool live)
        {
            var combat = session.Combat;
            autoFireStatus.gameObject.SetActive(live && session.CombatUnlocked && combat != null);
            if (combat == null) return;
            if (!inputPresentationReady || displayedSelectionStatus != combat.SelectionStatus || displayedInputAuto != combat.AutoFireEnabled)
            {
                inputPresentationReady = true; displayedSelectionStatus = combat.SelectionStatus; displayedInputAuto = combat.AutoFireEnabled;
                autoFireStatus.text = (displayedInputAuto ? "AUTO-FIRE ON | " : "MANUAL CAST | ") + displayedSelectionStatus;
            }
            var size = autoFireStatus.rectTransform.sizeDelta;
            var width = Mathf.Clamp(hudRoot.rect.width - 780f, 360f, 1000f);
            if (Mathf.Abs(size.x - width) > .5f) { size.x = width; autoFireStatus.rectTransform.sizeDelta = size; }
        }

        private void RefreshSettings()
        {
            if (autoFireSettingButton != null)
            {
                autoFireSettingButton.interactable = session.HasStarted && session.Combat != null;
                var value = session.Combat != null && session.Combat.AutoFireEnabled ? 1 : 0;
                if (displayedAutoSetting != value)
                {
                    displayedAutoSetting = value;
                    autoFireSettingLabel.text = value == 1 ? "OPTIONAL AUTO-FIRE | ON" : "OPTIONAL AUTO-FIRE | OFF (DEFAULT)";
                }
            }
            RefreshInterfaceSettings();
            if (sensitivitySlider != null && session.Player != null)
            {
                sensitivitySlider.SetValueWithoutNotify(session.Player.MouseSensitivity);
                sensitivityText.text = (session.Player.MouseSensitivity / .105f).ToString("0.00") + "x";
                fieldOfViewSlider.SetValueWithoutNotify(session.Player.FieldOfView);
                fieldOfViewValue.text = Mathf.RoundToInt(session.Player.FieldOfView) + "°";
                screenShakeSlider.SetValueWithoutNotify(session.Player.ScreenShakeIntensity);
                screenShakeValue.text = Mathf.RoundToInt(session.Player.ScreenShakeIntensity * 100) + "%";
                bobbingSlider.SetValueWithoutNotify(session.Player.BobbingIntensity);
                bobbingValue.text = Mathf.RoundToInt(session.Player.BobbingIntensity * 100) + "%";
            }
            if (progressionResetButton != null) progressionResetButton.interactable = session.Shop != null;
            var hasVoice = session.Tutorial != null && session.Tutorial.VoiceAvailable;
            voiceRow.gameObject.SetActive(session.Tutorial != null);
            muteVoiceButton.gameObject.SetActive(session.Tutorial != null);
            audioNote.text = hasVoice ? "Full guide captions remain enabled, including while narration is muted."
                : "Narration audio is currently unavailable. Guide captions remain enabled.";
            if (session.Tutorial != null)
            {
                voiceSlider.SetValueWithoutNotify(session.Tutorial.VoiceVolume);
                voiceValue.text = Mathf.RoundToInt(session.Tutorial.VoiceVolume * 100) + "%";
            }
            if (session.Audio != null)
            {
                masterSlider.SetValueWithoutNotify(session.Audio.MasterVolume);
                musicSlider.SetValueWithoutNotify(session.Audio.MusicVolume);
                sfxSlider.SetValueWithoutNotify(session.Audio.SfxVolume);
                masterValue.text = Mathf.RoundToInt(session.Audio.MasterVolume * 100) + "%";
                musicValue.text = Mathf.RoundToInt(session.Audio.MusicVolume * 100) + "%";
                sfxValue.text = Mathf.RoundToInt(session.Audio.SfxVolume * 100) + "%";
            }
            RefreshWeatherAmbienceControls020();
        }

        private void RefreshTree()
        {
            if (treePoints == null || session.Progression == null) return;
            var progression = session.Progression;
            var guideBranch = treePreview && session.Tutorial != null ? session.Tutorial.GuideHighlightElement : -1;
            var guideNode = -1;
            if (guideBranch >= 0 && guideBranch < 4)
            {
                guideNode = 2;
                for (var node = 0; node < 3; node++)
                    if (!progression.IsUnlocked(guideBranch, node)) { guideNode = node; break; }
            }
            if (guideBranch >= 0 && guideBranch < 4) treeBranch = guideBranch;
            if (guideNode >= 0 && guideNode < 3) treeFocusedNode = guideNode;
            var mask = 0;
            for (var e = 0; e < 4; e++)
                for (var n = 0; n < 3; n++) if (progression.IsUnlocked(e, n)) mask |= 1 << (e * 3 + n);
            var snapshot = new TreeUiSnapshot
            {
                Level = progression.Level, Points = progression.SkillPoints, Mask = mask,
                L0 = progression.Levels[0], L1 = progression.Levels[1], L2 = progression.Levels[2], L3 = progression.Levels[3],
                Branch = treeBranch, Node = treeFocusedNode, GuideBranch = guideBranch, GuideNode = guideNode,
                BindingFrame = AbilityBindings.LastChangeFrame, Preview = treePreview,
                Caption = treePreview && session.Tutorial != null ? session.Tutorial.Subtitle : null
            };
            if (treeUiSnapshotValid && treeUiSnapshot.SameAs(snapshot)) return;
            treeUiSnapshot = snapshot;
            treeUiSnapshotValid = true;
            treeCloseLabel.text = treePreview ? guideBranch >= 0 && guideBranch < 3 ? "NEXT ELEMENT" : "CONTINUE GUIDE"
                : session.Tutorial != null && session.Tutorial.AwaitingSkillsMenu ? "PLAY NEXT" : "CLOSE TREE  [" + GameActionBindings.GetLabel("SkillTree") + "]";
            treePoints.text = "ORACLE LEVEL " + progression.Level + "   ·   " + progression.SkillPoints + " SKILL POINT" + (progression.SkillPoints == 1 ? "" : "S")
                + (treePreview ? "   ·   READ-ONLY GUIDE PREVIEW" : "");
            for (var element = 0; element < 4; element++)
            {
                var active = element == treeBranch;
                treeTabLabels[element].text = treeElementTitles[element] + "  ·  LV " + progression.Levels[element];
                treeTabLabels[element].color = active ? TutorialElementColor(element) : Muted;
                treeTabLabels[element].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                treeTabButtons[element].targetGraphic.color = active ? new Color(.14f, .23f, .16f, 1) : new Color(.055f, .08f, .066f, 1);
                treeTabButtons[element].interactable = !treePreview || guideBranch < 0 || element == guideBranch;
            }
            for (var node = 0; node < 3; node++)
            {
                var owned = (mask & (1 << (treeBranch * 3 + node))) != 0;
                var previous = node == 0 || (mask & (1 << (treeBranch * 3 + node - 1))) != 0;
                var state = owned ? "LEARNED" : !previous ? "LOCKED · Learn the previous upgrade" : "COST 1 SKILL POINT";
                var selected = node == treeFocusedNode;
                treeNodeLabels[node].text = treeCardText[treeBranch, node] + state + "</size>";
                treeNodeLabels[node].color = owned ? Emerald : previous ? Ivory : Muted;
                treeNodeButtons[node].targetGraphic.color = selected ? new Color(.15f, .22f, .14f, 1) : new Color(.065f, .09f, .073f, 1);
                treeNodeButtons[node].interactable = !treePreview || guideNode < 0 || node == guideNode;
                var cardSprite = PassiveCardSprite(treeBranch, node);
                treeCardEmblems[node].sprite = cardSprite;
                treeCardEmblems[node].gameObject.SetActive(cardSprite != null);
                treeCardFallbacks[node].Element = treeBranch;
                treeCardFallbacks[node].color = TutorialElementColor(treeBranch);
                treeCardFallbacks[node].gameObject.SetActive(cardSprite == null);
            }
            var learned = progression.IsUnlocked(treeBranch, treeFocusedNode);
            var unlocked = treeFocusedNode == 0 || progression.IsUnlocked(treeBranch, treeFocusedNode - 1);
            treeDetailTitle.text = ElementName(treeBranch) + " · " + treeNodeNames[treeBranch, treeFocusedNode];
            treeDetailTitle.color = TutorialElementColor(treeBranch);
            treeDetailState.text = treePreview ? "GUIDE PREVIEW · READ ONLY" : learned ? "LEARNED"
                : !unlocked ? "LOCKED · LEARN THE PREVIOUS UPGRADE" : "COST 1 SKILL POINT";
            treeDetailDescription.text = progression.UpgradeDescription(treeBranch, treeFocusedNode)
                + (treePreview && !string.IsNullOrEmpty(snapshot.Caption) ? "\n\nGUIDE\n" + snapshot.Caption : "");
            treeDetailScroll.verticalNormalizedPosition = 1;
            treeLearnButton.interactable = !treePreview && !learned && unlocked && progression.SkillPoints > 0;
            treeLearnLabel.text = treePreview ? "READ-ONLY PREVIEW" : learned ? "LEARNED" : !unlocked ? "LOCKED"
                : progression.SkillPoints > 0 ? "LEARN · COST 1 SKILL POINT" : "NEED 1 SKILL POINT";
        }

        private string ElementName(int index)
        {
            return session.Progression != null && index >= 0 && index < session.Progression.ElementNames.Length
                ? session.Progression.ElementNames[index] : new[] { "Fire", "Water", "Earth", "Air" }[Mathf.Clamp(index, 0, 3)];
        }

        private void FitExpandedPanels()
        {
            var currentSize = new Vector2(Screen.width, Screen.height);
            if (canvas == null) return;
            var previousBounds = (canvas.transform as RectTransform).rect.size;
            if (lastScreenSize == currentSize && lastCanvasSize == previousBounds) return;
            lastScreenSize = currentSize;
            Canvas.ForceUpdateCanvases();
            var bounds = (canvas.transform as RectTransform).rect.size;
            lastCanvasSize = bounds;
            FitTopActions020(bounds);
            foreach (var card in fittedCards)
            {
                var size = card.sizeDelta;
                var startCard = panels.ContainsKey(JourneyPanel.Start) && card == panels[JourneyPanel.Start];
                var scale = Mathf.Min(1, (startCard ? bounds.x * .52f : bounds.x - 64) / size.x,
                    (bounds.y - 64) / size.y);
                card.localScale = Vector3.one * Mathf.Max(.1f, scale);
                if (startCard) card.anchoredPosition = new Vector2(-Mathf.Min(78, bounds.x * .045f), 0);
            }
            if (introCopy != null)
            {
                introCopy.localScale = Vector3.one * Mathf.Min(1, (bounds.x - 96) / 1260);
                introCopy.anchoredPosition = new Vector2(0, -Mathf.Min(116, bounds.y * .11f));
                introSkipHint.gameObject.SetActive(bounds.x >= 1100);
            }
            if (downedCard != null)
                downedCard.localScale = Vector3.one * Mathf.Min(1, (bounds.x - 96) / 860, (bounds.y - 96) / 224);
            if (loadingCard != null)
                loadingCard.localScale = Vector3.one * Mathf.Min(1, (bounds.x - 96) / 980, (bounds.y - 96) / 340);
        }
    }

    // # ARCHITECTURE SNAPSHOT
    // Canvas reference pixels; pointer/keyboard focus only. This helper never
    // purchases upgrades, polls input, changes pause state or owns a simulation.
    // Created once per tab/card; callbacks and card summaries are cached at build.
    public sealed class OraclePassiveCardFocus : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        public Action OnKeyboardFocus;
        public Action OnPointerFocus;
        public void OnSelect(BaseEventData eventData) { if (isActiveAndEnabled) OnKeyboardFocus?.Invoke(); }
        public void OnPointerEnter(PointerEventData eventData) { if (isActiveAndEnabled) OnPointerFocus?.Invoke(); }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing Canvas UI only.
// Array Formats: existing menu controls; no new per-frame arrays or heap allocations.
// Dependencies/Pointers: pending browser capture hides Unity menus until trusted HTML capture or Escape cancels it.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas, local XZ map projection and unchanged session world.
// Array Formats: Fixed tutorial UI markers and cached scalar visibility/context; no simulation ABI change.
// Dependencies/Pointers: Existing tutorial/party/session owners; root integrates exact guarded hunks and verifies actual Unity/browser behavior.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up refuge/Canvas and true floor contact.
// Array Formats: Fixed activity anchors, existing bounded combat/room owners and event-only UI.
// Dependencies/Pointers: Root-integrated tutorial activities retain the connected party and use the existing Skip/Resume authority.
// # ARCHITECTURE SNAPSHOT
// World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Bounded cached collider/root references only; no new economy/native buffers or quest bits.
// Root owns Unity integration and native acceptance; this is a guarded work candidate.

// # ARCHITECTURE SNAPSHOT
// Existing world metres and Canvas coordinates; fixed twelve-card value state, existing Combat/Nova/Tower owners and real point costs retained.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas/start-card reference coordinates; additive start-only scripted visual lease; existing pointer/focus policies unchanged.
// Array Formats: Private fixed native pose buffers and completed fences belong only to preview; no gameplay or website integration.
// Dependencies/Pointers: Existing World/Rooms/HP/save/audio owners remain canonical; saved assets are borrowed and the isolated visual lease is privately owned.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing World/Canvas; one objective compass replaces floor-arrow publication, actual map legend retained.
// Array Formats: one bounded HUD/native store with fenced disposal; preserved original floor recipe stays inactive.
// Dependencies/Pointers: original tutorial/campaign/physics/input authorities unchanged; source-only integration/native/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas/minimap-local readiness footer; actual accepted1/2-member state and separate8-level label.
// Array Formats: Cold bounded cached captions and value-only view/layout; no hot allocation or native owner.
// Dependencies/Pointers: Read-only ready/phase/remaining-seconds; existing parent/menu/map ownership and bottom-HUD fit retained; root native/browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing HUD/menu lifecycle; immediate hide/dispose for one private TowerAnvil atlas.
// Array Formats: cached presentation owner only; completed native writers before hiding/disposal, no perframe resource creation.
// Dependencies/Pointers: existing menu/input/pause/purchase/save authority unchanged; native/browser acceptance root-owned.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing2D music/uGUI only; world/spatial/campaign authority unchanged.
// Array Formats: Additive fixed18 playlist masks/rows and at most2 optional clip leases; existing two-source fade/duck/volume/focus remain canonical.
// Dependencies/Pointers: Root verifies/imports/binds recordings; pending assets use honest existing-theme fallback. Cold boundary loads only, no PCM/new sources; native listening/memory/browser/UI acceptance pending.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space:Unity World listener/emitter metres; existing settings use local uGUI.
// Array Formats:Existing pools remain fixed;8voice leases/2queries/16hits per ray; audio-local Morton refresh keys.
// Dependencies/Pointers:Audio owns native reader fences/low-pass/backend leases; accepted World/tower lifetime hooks invalidate acoustic revisions. No narration/gameplay authority replaced.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up/XZ, exact1m terrain/.25m build roots and locked active-road context.
// Array Formats: Bounded native semantic/preview/body stores, exact64B versioned command and existing2peer/4tower snapshots.
// Dependencies/Pointers: Root integrates exact guarded slices; host economy/readiness/LOS and World geometry remain canonical, prepared/source compile is not native or browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current authenticated run/epoch/context and monotonic real-time pending request ages; placement/world coordinates unchanged.
// Array Formats: Immutable Frame64, fixed Pending96 scalar state and one cached Frame64 per existing peer; no per-frame heap container or ABI change.
// Dependencies/Pointers: Bounded same-nonce delivery, exact host replay and response-controlled UI only. Root alone imports/native-tests; frozen original packet remains unchanged.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted metre/Y-up/XZ support roots and HUD Canvas; cosmetic floor boxes do not establish build permission.
// Array Formats: Existing fixed8192 preview records, one ray job, four cached original tower choices, unchanged Frame64 pending/replay transaction.
// Dependencies/Pointers: Local picker owns no pause/cursor/economy; accepted host result alone closes successful transaction. Normal input/native/Web rendering remain separate acceptance gates.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted Canvas cards and room-code focus; no player/world coordinates.
// Array Formats: Cold controls reuse existing fields, three scalar disclosure states and cached group references.
// Dependencies/Pointers: Existing authoritative Solo/Create/Join/settings/leave callbacks remain; root owns native/browser/layout acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing current-WEB graphics settings and RoomV2-local light presentation only; no structural/camera/gameplay position changes.
// Array Formats: One Off/Soft preference and existing scalar UI snapshot/private URP/light cache; one primary directional light/one cascade/25m1024cap, existing three-window mesh batch.
// Dependencies/Pointers: Current root dirty/dialogue/guide/flora hooks preserved; existing owners manage cold settings/resource updates. No new per-frame CPU/light/shadow-map/render-feature owner; root verifies native/WebGL shader/visual/frame behavior.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted guide/menu Canvas and actual guide view/audio state.
// Array Formats: Existing bounded UI/state stores; cold immutable65status labels, no frame formatting.
// Dependencies/Pointers: Original restart/portal/settings/narration/voice/input/achievement owners remain. Root owns source apply and full compile/native/browser/listening acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing graphics Canvas and world-metre shadow settings; palette/actor/world geometry unchanged.
// Array Formats: Four scalar tiers and two weather flags; native pipeline/resource buffers retain their existing owner.
// Dependencies/Pointers: Private clone/public APIs only; existing input/menu, light restoration and save owner retained; atmosphere consumes instance flags.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Sound uGUI panel and unchanged palette/font/input.
// Array Formats: Existing rows move into existing scroll factory; weather controls cache displayed scalars.
// Dependencies/Pointers: Existing navigation and audio owner persist explicit user choices; no hierarchy/prefab/scene writes by this work packet.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing graphics preference scalars and fitted HUD copy; no world/camera transforms.
// Array Formats: One derived Beta policy boolean and existing cached UI snapshot; no new runtime store or collection.
// Dependencies/Pointers: Existing catalog/schema/defaults, saved scale/auto policy and private render owners remain authoritative; root alone applies/imports and verifies native UI.
