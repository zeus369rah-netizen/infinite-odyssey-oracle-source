using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OdysseyOracle
{
    [DefaultExecutionOrder(-100)]
    public sealed partial class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        [SerializeField] private PassiveNodeIconLibrary passiveNodeIconCatalog;
        [SerializeField] private TowerModelAssetCatalog towerModelCatalog;
        public TowerModelAssetCatalog TowerModels=>towerModelCatalog;
        [SerializeField] private TutorialRoomV2Catalog refugeRoomCatalog;
        public TutorialRoomV2Catalog RefugeRoomModels=>refugeRoomCatalog;
        [SerializeField] private RefugeNpcAssetCatalog refugeNpcCatalog;
        public RefugeNpcAssetCatalog RefugeNpcModels=>refugeNpcCatalog;
#if UNITY_EDITOR
        public void SetRefugeRoomCatalog(TutorialRoomV2Catalog catalog)
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Bind the actual refuge catalog before Play.");
            RefugePropPlacement.ValidateCatalog(catalog);
            refugeRoomCatalog=catalog;
        }
        public void SetRefugeNpcCatalog(RefugeNpcAssetCatalog catalog)
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Bind original refuge NPC art before Play.");
            refugeNpcCatalog=catalog;
        }
        public void SetTowerModelCatalog(TowerModelAssetCatalog catalog)
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Bind saved tower art before Play.");
            towerModelCatalog=catalog;
        }
#endif
        public OraclePlayer Player;
        public OracleCombat Combat;
        public DungeonWorld World;
        public OracleViewmodel Viewmodel;
        public SpellImpactLighting SpellLights;
        public ElementProgression Progression;
        public TutorialDirector Tutorial;
        public OdysseyHud Hud;
        public RoomConnection Rooms;
        public CoopDefenseDirector Defense;
        public ShrinePlacementBuilder Builder;
        public GraphicsPreferences Graphics;
        public GameAudioDirector Audio;
        public LoreLibrary Books;
        public RefugeActivityDirector RefugeActivities;
        public TrainingLabDirector TrainingLab;
        public CoopTrainingLabDirector SharedTrainingLab;
        public CampfireOpening Opening;
        public OracleRevival Revival;
        public OracleTownShop Shop;
        public OracleFieldSmith FieldSmith;
        public RefugeNpcRuntimeOwner RefugeNpcs;
        public ArenaPreloader Loading;
        Transform listenerPoint;
        PeerCombatAuthority peerHealthAuthority;
        public bool TryGetPeerHealth(ulong peerId,out PeerHealthSnapshot snapshot)
        {
            snapshot=default;
            if(!CoopMode||World==null||!World.IsCoopArena||Rooms==null||!Rooms.Connected)return false;
            if(peerHealthAuthority==null)peerHealthAuthority=GetComponent<PeerCombatAuthority>();
            return peerHealthAuthority!=null&&peerHealthAuthority.TryGetPeerHealth(peerId,out snapshot);
        }
        public bool IsDowned => Revival != null && Revival.IsDowned;
        public bool CanBeRevived => Revival != null && Revival.CanBeRevived;
        public float ReviveRemainingSeconds => Revival != null ? Revival.ReviveRemainingSeconds : 0f;
        public float ReviveProgress => Revival != null ? Revival.ReviveProgress : 0f;
        public string DownedHint => Revival != null ? Revival.DownedHint : "";
        public bool CombatUnlocked;
        public bool CoopMode { get; private set; }
        public string FailureReason { get; private set; } = "";
        public bool HasStarted;
        public bool Paused = true;
        public bool Victory;
        public bool AwaitingBrowserPointerCapture { get; private set; }
        int pointerCaptureGuardFrame = -1;
        CursorMenuState015 cursorMenu015;
        public bool CursorFree015=>cursorMenu015.Active;
        public float HP = 125f, MaxHP = 125f;
        public float Experience;
        public int Wave = 1, Defeated, Goal = 4;
        public string Objective = "Begin your Oracle training.";
        public bool IsDead => LegacyCombatWorld020 && Combat != null && Combat.Shrine != null && Combat.Shrine.IsDestroyed;
        public bool IsPlaying => HasStarted && WorldControlReady020 && !Paused && !CursorFree015 && !AltPressed020 && Time.frameCount > pointerCaptureGuardFrame && !Victory && !IsDead && !IsDowned && (Loading == null || !Loading.IsLoading) && (Opening == null || (!Opening.IsTransitioning && !Opening.IsIntroPlaying));
        public bool SimulationActive => !soloRestartSimulationHold && HasStarted && WorldControlReady020 && !Victory && !IsDead && (!Paused || (CoopMode && Defense != null && Defense.Running && Rooms != null && Rooms.IsHost) || (SharedTrainingLab != null && SharedTrainingLab.HostSimulationActive));
        bool outcomeHandled;
        bool portalPrompted, pendingCoopStart;
        AudioSource ambience;
        BreakableVoxelWall breakableWall;
        Material breakableStone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSessionRegistry() { Instance = null; }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (Instance != null && !ReferenceEquals(Instance, this))
            {
                Debug.LogError("Multiple active Oracle sessions detected. Stop Play and restart the bootstrap scene.", this);
                enabled = false;
                return;
            }
            // Registry restoration only. The Editor compile guard must stop Play
            // before reload; assigning this static never reconstructs callbacks.
            Instance = this;
        }

        void OnDisable()
        {
            if (!ReferenceEquals(Instance, this)) return;
            RetireNibwick020();RetireCampaignHubAtmosphere020();
            CancelExitRequest020();CancelAltMouse020();
            Instance = null;
            ReleasePointer();
        }

        void Awake()
        {
            if (Instance != null && !ReferenceEquals(Instance, this))
            {
                Debug.LogError("Duplicate Oracle session cannot construct another runtime world.", this);
                enabled = false;
                return;
            }
            Instance = this;
            HasStarted = false;
            Paused = true;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            int desktopQuality = System.Array.IndexOf(QualitySettings.names, "PC");
            if (desktopQuality >= 0) QualitySettings.SetQualityLevel(desktopQuality, true);
            QualitySettings.vSyncCount = 0;
            Time.maximumDeltaTime = .15f;
            ConfigureAtmosphere();
            World = new GameObject("Voxel catacombs").AddComponent<DungeonWorld>();
            World.BuildTutorialRefuge(refugeRoomCatalog);
            SpellLights = new GameObject("Pooled spell illumination").AddComponent<SpellImpactLighting>();
            ResetBreakableWall();
            Player = new GameObject("Oracle — first person").AddComponent<OraclePlayer>();
            Player.Initialize(World.PlayerSpawn);
            ConfigureCamera(Player.ViewCamera);
            Progression = gameObject.AddComponent<ElementProgression>();
            Progression.Initialize(Player);
            Combat = Player.gameObject.AddComponent<OracleCombat>();
            Viewmodel = new GameObject("Oracle hands and focus").AddComponent<OracleViewmodel>();
            Viewmodel.Initialize(Player);
            Combat.Initialize(Player, World);
            Tutorial = gameObject.AddComponent<TutorialDirector>();
            Rooms = gameObject.AddComponent<RoomConnection>();
            Defense = gameObject.AddComponent<CoopDefenseDirector>();
            Builder = gameObject.AddComponent<ShrinePlacementBuilder>();
            Tutorial.Initialize(this);
            Graphics = gameObject.AddComponent<GraphicsPreferences>();
            Graphics.Initialize(this);
            Audio = gameObject.AddComponent<GameAudioDirector>();
            Audio.Initialize(this);
            string[] elementAudio = { "fire", "water", "earth", "air" };
            for (int element = 0; element < 4; element++)
            {
                var clip = Resources.Load<AudioClip>("Audio/Generated/element_" + elementAudio[element]);
                if (clip == null) continue;
                Audio.SetCastClip(element, clip);
                Audio.SetTowerClip(element, clip);
            }
            Audio.SetSupportClip(Resources.Load<AudioClip>("Audio/Generated/element_water"));
            string[] towerAudio = { "fire", "water", "nature", "air", "lightning", "crystal", "ice", "magma" };
            for (int element = 0; element < towerAudio.Length; element++)
            {
                var clip = Resources.Load<AudioClip>("Audio/Elements/element_" + towerAudio[element])
                    ?? Resources.Load<AudioClip>("Audio/Generated/element_" + towerAudio[element]);
                if (clip != null) Audio.SetTowerClip(element, clip);
            }
            Audio.SetMusicClip(Resources.Load<AudioClip>("Audio/Music/oracle_return_theme"));
            Audio.SetCampfirePosition(new Vector3(-4f, .35f, 5.25f));
            Hud = new GameObject("Odyssey interface").AddComponent<OdysseyHud>();
            Hud.SetPassiveIconCatalog(passiveNodeIconCatalog);
            Hud.Initialize(this);
            Defense.Initialize(this);
            Builder.Initialize(this);
            TrainingLab=gameObject.AddComponent<TrainingLabDirector>();TrainingLab.Initialize(this);Hud.InitializeTrainingLab();
            SharedTrainingLab=gameObject.AddComponent<CoopTrainingLabDirector>();SharedTrainingLab.Initialize(this);
            Books = gameObject.AddComponent<LoreLibrary>();
            Books.Initialize(this);
            RefugeActivities=gameObject.AddComponent<RefugeActivityDirector>();
            RefugeActivities.Initialize(this);
            var hearth = gameObject.AddComponent<CampfireAtmosphere>();
            hearth.Initialize(this);
            Opening = gameObject.AddComponent<CampfireOpening>();
            Opening.Initialize(this);
            Loading = gameObject.AddComponent<ArenaPreloader>();
            Loading.Initialize(this);
            var oldListener = Player.ViewCamera.GetComponent<AudioListener>();
            if (oldListener != null) { oldListener.enabled = false; Destroy(oldListener); }
            listenerPoint = new GameObject("Oracle listening point").transform;
            listenerPoint.SetParent(transform, false);
            listenerPoint.gameObject.AddComponent<AudioListener>();
            Audio.SetListenerTransform(listenerPoint);
            Audio.SetPortalPosition(World.PortalPosition + Vector3.up * 1.4f);
            Revival = gameObject.AddComponent<OracleRevival>();
            Revival.Initialize(this);
            Shop = gameObject.AddComponent<OracleTownShop>();
            Shop.Initialize(this);
            FieldSmith = gameObject.AddComponent<OracleFieldSmith>();
            FieldSmith.Initialize(this);
            RefugeNpcs = gameObject.AddComponent<RefugeNpcRuntimeOwner>();
            RefugeNpcs.Initialize(this, refugeNpcCatalog);
            Progression.RefreshPermanentTraining();
            var campaign = CampaignBootstrap.Install(this,PrepareSharedArena);
            Hud.BindCampaignUi(campaign);
            RefugeNpcs.SetQuestBridge(new CampaignNpcQuestBridge010(campaign),campaign);
            Rooms.CampaignStarted += BeginSharedCampaign;
            InitializeCampaignHubRouting020();
            ReleasePointer();
        }

        void Update()
        {
            UpdateSessionInactivity();
            UpdateMenuInput020();
            TickCampaignHubMode020();
            if (IsPlaying && (CombatUnlocked||CoopCampaignDirector.IsRefugeSession(this)) && Keyboard.current != null && Keyboard.current[GameActionBindings.GetKey("SkillTree")].wasPressedThisFrame && !AbilityBindings.IsRebinding) OpenSkillTree();
            if (Progression != null) Experience = Progression.Experience01;
            if (pendingCoopStart && Rooms.Connected)
            {
                pendingCoopStart = false;
                Rooms.SetCoopReady(true);
            }
            else if (pendingCoopStart && !Rooms.Busy) pendingCoopStart = false;
            if (HasStarted && World.Mode020==WorldMode020.TutorialRefuge)
            {
                float distance = Vector3.Distance(Player.transform.position, World.PortalPosition);
                if (distance > 5f) portalPrompted = false;
                if (distance < 3.4f && !portalPrompted && IsPlaying) {
                    if(Tutorial.Finished)EnterPortal();
                    else portalPrompted=Hud.ShowTutorialPortalSkip();
                }
            }
            if (!soloRestartSimulationHold && (Victory || IsDead) && !outcomeHandled)
            {
                outcomeHandled = true;
                Paused = true;
                ReleasePointer();
                if (IsDead) FailureReason = Combat != null && Combat.Shrine != null && Combat.Shrine.IsDestroyed ? "The green shrine was destroyed." : "The Oracle has fallen.";
                if (Victory) Objective = CoopMode ? "The shrine endures. Defense complete." : "Oracle's trial is complete.";
            }
        }

        public void Begin()
        {
            BeginCampaign();
        }

        public void SkipTutorialToHub()
        {
            if (World.Mode020!=WorldMode020.TutorialRefuge || (Loading != null && Loading.IsLoading)) return;
            if (!HasStarted)
            {
                // A connected party must keep its existing room and admission.
                if (Rooms != null && Rooms.Connected) BeginCoop();
                else BeginCampaign();
            }
            if (!HasStarted) return;
            Revival.ResetState();
            Combat.ResetEncounter();
            HP = MaxHP;
            Victory = false;
            CombatUnlocked = true;
            Tutorial.SkipTutorial();
            Hud.ClosePanels();
            Resume();
            Audio.StartAudioFromGesture();
        }

        public void BeginCampaign()
        {
            if (Opening != null && Opening.IsIntroPlaying) { Opening.SkipIntro(); return; }
            ResetCampaignHubSession020();
            Loading.Cancel();
            Revival.ResetState();
            Rooms.LeaveRoom();
            CoopMode = false;
            Progression.ResetProgression();
            Defense.ResetDefense();
            Builder.ResetBuildings();
            if (World.Mode020!=WorldMode020.TutorialRefuge) World.Build();
            Combat.ResetEncounter();
            Player.Teleport(World.PlayerSpawn);
            HP = MaxHP;
            Victory = false;
            outcomeHandled = false;
            portalPrompted = false;
            HasStarted = true;
            CombatUnlocked = false;
            Opening.BeginArrival();
            Tutorial.StartTutorial();
            Objective = "Complete your movement lessons, then approach the green shrine.";
            if (Tutorial == null || !Tutorial.InstructionMenuBlocksGameplay015) Resume();
            Audio.StartAudioFromGesture();
            Audio.SetGameplayMix(true);
        }

        public void BeginCoop()
        {
            ResetCampaignHubSession020();
            Loading.Cancel();
            Revival.ResetState();
            CoopMode = true;
            World.Build();
            Combat.ResetEncounter();
            if (Rooms != null && Rooms.Connected)
            {
                if (!Rooms.TryGetLocalHubSpawn(out Vector3 acceptedFeet, out float acceptedYaw))
                    throw new System.InvalidOperationException("The host's supported refuge spawn is not ready.");
                if (!Rooms.IsHost) Player.Teleport(acceptedFeet, acceptedYaw);
                Rooms.CommitLocalHubSpawn();
            }
            else Player.Teleport(World.PlayerSpawn);
            HasStarted = true;
            CombatUnlocked = true;
            // Explicit free practice: clears owned lessons/voice without practice credit or persistence.
            Tutorial.SkipTutorial();
            Opening.EnterGameplayDirect();
            Objective = "Shared refuge · Explore together, visit the NPCs, or ready up at the violet portal.";
            Hud.ClosePanels();
            Resume();
            Audio.StartAudioFromGesture();
            Audio.SetGameplayMix(true);
        }

        void BeginSharedCampaign()
        {
            if(!LegacyCombatWorld020)return;
            Loading.Begin(PrepareSharedArena, BeginPreparedDefense);
        }

        void PrepareSharedArena()
        {
            if(Rooms==null||!Rooms.Connected||!Rooms.TryGetAdmissionSlot(Rooms.LocalClientId,out int admissionSlot))
                throw new System.InvalidOperationException("Your validated party spawn is not ready. Leave and reconnect before entering the arena.");
            Revival.ResetState();
            Tutorial.EndTutorial();
            if (breakableWall != null) breakableWall.gameObject.SetActive(false);
            World.BuildCoopArena();
            Books?.PrepareArenaDiscoveryBooks();
            Combat.ResetEncounter();
            var activeCampaign=GetComponent<CoopCampaignDirector>();
            if(activeCampaign!=null&&activeCampaign.Active)
            {
                int stage=activeCampaign.StageNumber,mask=CampaignStagePacing.ActiveLaneMask(stage);
                if(!CoopPartyCapacity.ValidLaneMask(mask))
                    throw new System.InvalidOperationException("The campaign road roster is not ready.");
                World.ConfigureActiveLaneMask(mask);
                Player.Teleport(World.GetCampaignPlayerSpawn(admissionSlot,stage),World.GetCampaignPlayerSpawnYaw(admissionSlot,stage));
            }
            else Player.Teleport(World.GetCoopPlayerSpawn(admissionSlot),World.GetCoopPlayerSpawnYaw(admissionSlot));
            CombatUnlocked = true;
            HasStarted = true;
            RenderSettings.fogDensity = .006f;
            Player.ViewCamera.farClipPlane = 150f;
            Hud.ClosePanels();
            Audio.PlayCue("portal_travel");
            Audio.SetPortalPosition(World.PortalPosition, false);
            Objective = "Preparing the shared shrine defense.";
        }

        void BeginPreparedDefense()
        {
            Defense.StartDefense();
            Loading.FinishWhenReady(() => Defense.ArenaReady);
            Objective = "Defend the green shrine · Hold your road and protect the towers";
        }

        public void OpenSkillTree() { if (!LegacyCombatWorld020||!CombatUnlocked&&!CoopCampaignDirector.IsRefugeSession(this)) return; Hud.ShowSkillTree(false); }

        public void EnterPortal()
        {
            if (World.Mode020!=WorldMode020.TutorialRefuge || !Tutorial.Finished) return;
            portalPrompted = true;
            Hud.ShowPortalChoices();
        }

        public void StartCoopFromPortal()
        {
            if(World==null||World.Mode020!=WorldMode020.TutorialRefuge)return;
            if(CampaignBootstrap.TrySetReady(this,true))return;
            if (Rooms.Connected) { Rooms.SetCoopReady(true); return; }
            pendingCoopStart = true;
            Rooms.CreateRoom();
        }

        public void CompleteStage()
        {
            if (!IsPlaying || World.Mode020!=WorldMode020.TutorialRefuge || Defeated < Goal) return;
            if (CoopMode) return;
            Wave++;
            Defeated = 0;
            HP = MaxHP;
            Tutorial.EndTutorial();
            World.LockPortal();
            Player.Teleport(World.PlayerSpawn, 0f);
            Combat.BeginStage(Wave);
            Objective = "Stage " + Wave + " · Protect the green shrine and defeat all monsters.";
        }

        public void Pause()
        {
            if (!HasStarted) return;
            Paused = true;CancelAltMouse020();
            ReleasePointer();cursorMenu015.RecordPause020(Time.frameCount);
        }

        public void FailArenaPreparation(string reason)
        {
            if(CampaignBootstrap.TryReportPreparationFailure(this,reason))return;
            FailureReason = string.IsNullOrWhiteSpace(reason) ? "Arena preparation failed. Return to the main menu." : reason;
            Objective = FailureReason;
            pendingCoopStart = false;
            HasStarted = true;
            Victory = false;
            outcomeHandled = true;
            Paused = true;
            CombatUnlocked = false;
            ReleasePointer();
            Rooms?.LeaveRoom();
            Revival?.ResetState();
            Defense?.ResetDefense();
        }

        public void Resume()
        {
            if(CampaignBootstrap.IsTerminal(this))return;
            if(!TryArmSoloRestartResume())return;
            if (!HasStarted || !WorldControlReady020 || Victory || IsDead || !string.IsNullOrEmpty(FailureReason)) return;
            cursorMenu015.Reset();
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!Paused && Cursor.lockState == CursorLockMode.Locked) return;
            // This merely arms capture. Only a subsequent trusted DOM gesture
            // may ask the browser to lock; gameplay remains paused until granted.
            AwaitingBrowserPointerCapture = true;
            Paused = true;
            OracleBrowserPointerCapture.SetWanted(true);
#else
            pointerCaptureGuardFrame = Time.frameCount + 1;
            Paused = false;
            CompleteSoloRestartResume();
#if UNITY_EDITOR
            // Batch test workers advance the genuine public start path without desktop capture.
            if (Application.isBatchMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }
#endif
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
#endif
        }

        // Called by this game's template only after native pointerlockchange
        // confirms that its own canvas owns the lock. Never a browser focus call.
        public void BrowserPointerCaptured()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!AwaitingBrowserPointerCapture || !Application.isFocused || !HasStarted || !WorldControlReady020 ||
                Victory || IsDead || CampaignBootstrap.IsTerminal(this) || !string.IsNullOrEmpty(FailureReason) ||
                Loading != null && Loading.IsLoading)
            { var cursorState=cursorMenu015;ReleasePointer();cursorMenu015=cursorState;return; }
            AwaitingBrowserPointerCapture = false;
            pointerCaptureGuardFrame = Time.frameCount + 1;
            Paused = false;
            CompleteSoloRestartResume();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
#endif
        }

        public void BrowserPointerLost()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // A focused release reveals the existing HUD; blur still fully pauses.
            // Both paths disarm pending grants. Focus return never captures.
            if (HasStarted) Pause();
            else ReleasePointer();
#endif
        }

        public void Restart()
        {
            ResetCampaignHubSession020();
            CancelExitRequest020();CancelAltMouse020();
            Loading.Cancel();
            Revival.ResetState();
            Paused = true;
            Defense.ResetDefense();
            Builder.ResetBuildings();
            pendingCoopStart = portalPrompted = false;
            HasStarted = false;
            CombatUnlocked = false;
            CoopMode = false;
            Victory = false;
            outcomeHandled = false;
            HP = MaxHP;
            FailureReason = "";
            Defeated = 0;
            Wave = 1;
            Experience = 0;
            Objective = "Begin your Oracle training.";
            Rooms.LeaveRoom();
            Tutorial.ResetTutorial();
            Progression.ResetProgression();
            Hud.ClosePanels();
            ReleasePointer();
            if (World.Mode020!=WorldMode020.TutorialRefuge) World.Build();
            RenderSettings.fogDensity = .012f;
            Player.ViewCamera.farClipPlane = 90f;
            Opening.ReturnToMenu();
            Audio.SetGameplayMix(false);
            World.LockPortal();
            ResetBreakableWall();
            Player.Teleport(World.PlayerSpawn, 0f);
            Combat.ResetEncounter();
            SpellLights.ResetLights();
        }

        public void TakeDamage(float amount)
        {
            if(!LegacyCombatWorld020)return;
            // Promote only with the complete007 enemy/projectile/authority bundle.
            if(CoopMode&&Defense!=null&&Defense.PeerHealthAuthoritative)return;
            if (!SimulationActive || IsDowned || (Revival != null && Revival.Invulnerable) || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            HP = Mathf.Max(0f, HP - amount);
            Player.PlayDamageFeedback();
            if (HP <= 0f) Revival.EnterDowned(false);
        }

        void ResetBreakableWall()
        {
            World.ResolveBreakableCryptPose015(breakableWall,out Vector3 lower,out Quaternion rotation);
            if (breakableStone == null)
            {
                breakableStone = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                breakableStone.name = "Fractured crypt stone";
                breakableStone.SetColor("_BaseColor", new Color(.39f, .42f, .40f));
                breakableStone.SetFloat("_Smoothness", .07f);
            }
            if (breakableWall == null)
            {
                var panel = new GameObject("Breakable crypt masonry");
                panel.transform.SetParent(World.transform, false);
                panel.transform.rotation = rotation;
                breakableWall = panel.AddComponent<BreakableVoxelWall>();
            }
            breakableWall.transform.rotation = rotation;
            breakableWall.Initialize(lower, new Vector3Int(8, 6, 2), .25f, breakableStone);
            breakableWall.gameObject.SetActive(true);
            Audio?.RefreshAcousticCollider015(breakableWall.GetComponent<MeshCollider>());
            World.RebuildXpClearance015();
        }

        public void AddKill()
        {
            if(!LegacyCombatWorld020)return;
            if (!SimulationActive) return;
            Defeated++;
            if (CoopMode) return;
            if (Defeated >= Goal && Tutorial.Finished)
            {
                World.UnlockPortal();
                Objective = "All monsters defeated. Collect their XP and walk into the violet portal.";
            }
            else Objective = $"Defeat the guardians · {Defeated}/{Goal}";
        }

        public bool TryShowMouse015()=>TryShowMouse015(false);
        public bool ReturnToGuideControls015()=>TryShowMouse015(true);
        bool TryShowMouse015(bool guideReturn)
        {
            if(!Application.isFocused||!HasStarted||Paused&&!AwaitingBrowserPointerCapture&&!guideReturn||Victory||IsDead||IsDowned||SoloRestartHoldsSimulation||
                !string.IsNullOrEmpty(FailureReason)||Loading!=null&&Loading.IsLoading||
                Opening!=null&&(Opening.IsTransitioning||Opening.IsIntroPlaying)||Hud==null||!Hud.CanShowMouse015)return false;
            if(cursorMenu015.Active)return true;
            ReleasePointer();
            Paused=false;cursorMenu015.Release(Time.frameCount);return true;
        }

        public void ReleasePointer()
        {
            cursorMenu015.Reset();
            AwaitingBrowserPointerCapture = false;
            OracleBrowserPointerCapture.SetWanted(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        static void ConfigureCamera(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.017f, .026f, .040f);
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 90f;
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            data.volumeLayerMask = ~0;
            data.SetRenderer(SystemInfo.supportsComputeShaders ? 0 : 1);
        }

        void ConfigureAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.36f, .42f, .50f);
            RenderSettings.ambientEquatorColor = new Color(.23f, .28f, .34f);
            RenderSettings.ambientGroundColor = new Color(.13f, .15f, .18f);
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.242f, .286f, .352f));
            RenderSettings.ambientProbe = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.075f, .10f, .14f);
            RenderSettings.fogDensity = .012f;
            campaignDirectionalLight020 = new GameObject("Moonlight").AddComponent<Light>();
            var moon = campaignDirectionalLight020;
            moon.type = LightType.Directional;
            moon.color = new Color(.52f, .69f, 1f);
            moon.intensity = .32f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = .65f;
            moon.transform.rotation = Quaternion.Euler(48f, -38f, 0);
            var volume = new GameObject("Catacomb atmosphere").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = Resources.Load<VolumeProfile>("OracleAtmosphere") ?? CreateAtmosphereProfile();
        }

        public static VolumeProfile CreateAtmosphereProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.15f;
            bloom.intensity.value = .42f;
            bloom.scatter.value = .55f;
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.value = .65f;
            grade.contrast.value = 0f;
            grade.saturation.value = 0f;
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = .08f;
            vignette.smoothness.value = .55f;
            return profile;
        }

        void CreateAmbience()
        {
            const int rate = 22050, seconds = 8;
            var samples = new float[rate * seconds];
            // Original, quiet, exactly looping room tone. Playback begins with the Play gesture.
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                samples[i] = .024f * Mathf.Sin(2f * Mathf.PI * 55f * t)
                    + .013f * Mathf.Sin(2f * Mathf.PI * 82.5f * t)
                    + .007f * Mathf.Sin(2f * Mathf.PI * 110f * t);
            }
            var clip = AudioClip.Create("Original catacomb room tone", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.clip = clip;
            ambience.loop = true;
            ambience.playOnAwake = false;
            ambience.volume = .18f;
        }

        void OnApplicationFocus(bool focused) { NotifyIdleForeground(focused); if (!focused && HasStarted) Pause(); }
        void LateUpdate()
        {
            if (listenerPoint == null || Player == null) return;
            var camera = Opening != null ? Opening.ActiveCamera : Player.ViewCamera;
            if (camera != null) listenerPoint.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
        }
        void OnDestroy()
        {
            DisposeCampaignHubRouting020();
            CancelIdleShutdown();CancelExitRequest020();CancelAltMouse020();
            if (breakableStone != null) Destroy(breakableStone);
            if (Rooms != null) Rooms.CampaignStarted -= BeginSharedCampaign;
            if (ambience != null && ambience.clip != null) Destroy(ambience.clip);
            if (Instance == this) { Instance = null; ReleasePointer(); }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: main-thread browser canvas and existing Unity input; no spatial math changes.
// Array Formats: one pending-capture flag and one input suppression frame; no runtime allocations added to frame loops.
// Dependencies/Pointers: GameSession owns pause intent; the template owns native DOM capture; release cancels stale requests.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas, local XZ map projection and unchanged session world.
// Array Formats: Fixed tutorial UI markers and cached scalar visibility/context; no simulation ABI change.
// Dependencies/Pointers: Existing tutorial/party/session owners; root integrates exact guarded hunks and verifies actual Unity/browser behavior.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up refuge/Canvas and true floor contact.
// Array Formats: Fixed activity anchors, existing bounded combat/room owners and event-only UI.
// Dependencies/Pointers: Root-integrated tutorial activities retain the connected party and use the existing Skip/Resume authority.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge world metres/Y-up; host-owned shared lab, model local+Z.
// Array Formats: Two native peer records and bounded fixed frames; inherited48 projectile pool and two real actors.
// Dependencies/Pointers: Dedicated shared owner validates requests/fences; guests remote-only; no rewards, migration or narrator; root integrates complete slice.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing World/Canvas; one objective compass replaces floor-arrow publication, actual map legend retained.
// Array Formats: one bounded HUD/native store with fenced disposal; preserved original floor recipe stays inactive.
// Dependencies/Pointers: original tutorial/campaign/physics/input authorities unchanged; source-only integration/native/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Stable existing arena road IDs and host-owned eight-level campaign identity.
// Array Formats: Existing two peers/eight lane arrays/four shared pads/48 actors/4096 reward tombstones; no ABI growth.
// Dependencies/Pointers: Level-derived lane prefix, actual host ready/admission/recovery fences; root owns shared protocol and native acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing supported refuge World/Y-up crypt panel reset; exact south-edge lower corner and native floor proof clear the furnace bay.
// Array Formats: Same96breakable cells and retained mesh/collider/material; cold32Bpose plus bounded solid/floor job buffers, no hot owner changes.
// Dependencies/Pointers: Existing reset/arena visibility/damage lifetime retained; cold certification precedes publication, post-activation acoustic/XP geometry refresh is null-safe during Awake. Furnace node, discovery rewards and all saves unchanged.


// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Real main-thread input edges, current menu/canvas owner, and the one session-owned directional key.
// Array Formats: Scalar duplicate-Pause frame and Alt/exit command state; one retained Light reference; no hot collections.
// Dependencies/Pointers: ConfigureAtmosphere runs once from guarded Awake, constructs the existing directional key once, and retains it for a read-only atmosphere borrow; no other Light, shadow, or render-settings owner is added.
