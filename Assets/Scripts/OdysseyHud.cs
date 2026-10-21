using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace OdysseyOracle
{
    /// <summary>Runtime uGUI presentation; all encounter state stays in GameSession/OracleCombat.</summary>
    public sealed partial class OdysseyHud : MonoBehaviour
    {
        private static readonly Color Gold = new Color(.70f, .59f, .34f, 1f);
        private static readonly Color MutedGold = new Color(.43f, .37f, .24f, .85f);
        private static readonly Color Ivory = new Color(.91f, .91f, .85f, 1f);
        private static readonly Color Muted = new Color(.65f, .68f, .65f, 1f);
        private static readonly Color Emerald = new Color(.29f, .77f, .64f, 1f);
        private static readonly Color Panel = new Color(.035f, .045f, .044f, .76f);

        private GameSession session;
        private Font font;
        private Canvas canvas;
        private OracleInterfacePreferences interfacePreferences;
        private RectTransform hudRoot;
        private RectTransform menuRoot;
        private RectTransform menuCard;
        private RectTransform mapSurface;
        private RectTransform minimapRoot;
        private CanvasGroup minimapOpacityGroup;
        private UnityEngine.UI.Text minimapTitle;
        private readonly List<RectTransform> groundUnitMarkers = new List<RectTransform>(OracleCombat.MaxActiveEnemies);
        private readonly List<RectTransform> towerMarkers = new List<RectTransform>(24);
        private PlacedElementShrine[] minimapTowers = Array.Empty<PlacedElementShrine>();
        private float nextMapGeometryAudit, nextMapUnitRefresh;
        private int mapGeometrySignature;
        private int observedMapTowerCount = -1;
        private RectTransform mapPlayer;
        private RectTransform spellRoot;
        private UnityEngine.UI.Image portalMarker;
        private UnityEngine.UI.Image healthFill;
        private UnityEngine.UI.Image lowHealthEdges;
        private UnityEngine.UI.Image downedEdges;
        private Texture2D lowHealthTexture;
        private Sprite lowHealthSprite;
        private UnityEngine.UI.Image progressFill;
        private UnityEngine.UI.Slider experienceSlider;
        private UnityEngine.UI.Text experiencePickupText;
        private UnityEngine.UI.Image crosshair;
        private UnityEngine.UI.Text healthText;
        private UnityEngine.UI.Text chargeText;
        private UnityEngine.UI.Text ultimateLabel020;
        private UnityEngine.UI.Text elementLabel;
        private UnityEngine.UI.Text objectiveText;
        private UnityEngine.UI.Text progressText;
        private int xpRangeUiLevel=-1,xpRangeUiXp=-1,xpRangeUiPoints=-1;
        private float xpRangeUiMetres=-1f;
        private UnityEngine.InputSystem.Key xpRangeUiKey=UnityEngine.InputSystem.Key.None;
        private UnityEngine.UI.Text interactionText;
        private UnityEngine.UI.Text shrineStatusText;
        private UnityEngine.UI.Text walletText;
        private UnityEngine.UI.Text menuTitle;
        private UnityEngine.UI.Text menuSubtitle;
        private UnityEngine.UI.Text menuBody;
        private UnityEngine.UI.Text primaryLabel;
        private UnityEngine.UI.Text sensitivityText;
        private UnityEngine.UI.Button primaryButton;
        private UnityEngine.UI.Button restartButton;
        private UnityEngine.UI.Button pauseButton;
        private UnityEngine.UI.Text pointerButtonLabel015;
        private UnityEngine.UI.Slider sensitivitySlider;
        private RectTransform sensitivityRow;
        private UnityEngine.UI.Image[] charges;
        private SpellView[] spells;
        private UnityEngine.Rect mapBounds;
        private bool mapBuilt;
        private bool renderedCoopMap;
        private UnityEngine.Rect[] renderedMapRooms;
        private Vector3[] renderedMapPortals;
        private UnityEngine.UI.Text mapLegend;
        private bool built;
        private float textRefresh;
        private float warningPhase;
        private float nextUiHoverAt;
        private bool experiencePresentationReady;
        private int displayedLevel, observedXpSequence, observedWallet;
        private float displayedExperience, experienceWrapUntil, xpNoticeUntil, walletNoticeUntil;
        private string xpNotice = "", walletNotice = "";
        private Vector2 lastScreenSize;

        private sealed class SpellView
        {
            public UnityEngine.UI.Text Name;
            public UnityEngine.UI.Text Key;
            public UnityEngine.UI.Text State;
            public UnityEngine.UI.Image Background;
            public UnityEngine.UI.Image Veil;
            public UnityEngine.UI.Image[] Border;
            public UnityEngine.UI.Graphic[] Icon;
            public UnityEngine.UI.Image EmblemImage;
            public UnityEngine.UI.Image UltimateOverlay020;
            public int DisplayState = -1, DisplayLevel = -1, DisplayCooldownTenths = -1;
            public string DisplayKey;
        }

        public void Initialize(GameSession gameSession)
        {
            session = gameSession;
            if (built || session == null) return;
            built = true;
            // Explicitly requested built-in runtime font: no imported font asset is required.
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            var root = new GameObject("OracleInterface", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            interfacePreferences = root.AddComponent<OracleInterfacePreferences>();
            interfacePreferences.Initialize(scaler);
            interfacePreferences.Changed += InterfacePreferencesChanged;
            BuildLowHealthWarning(root.transform);
            hudRoot = Stretch(root.transform, "AdventureHUD");
            BuildAdventureHud();
            BuildMenu(root.transform);
            RefreshText();
            UpdateMenu();
        }

        private static void EnsureEventSystem()
        {
            var events = FindAnyObjectByType<EventSystem>();
            if (events == null) events = new GameObject("OracleEventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var input = events.GetComponent<InputSystemUIInputModule>();
            if (input == null) input = events.gameObject.AddComponent<InputSystemUIInputModule>();
            foreach (var oldModule in events.GetComponents<BaseInputModule>())
                oldModule.enabled = oldModule == input;
            input.AssignDefaultActions();
        }

        private void BuildLowHealthWarning(Transform parent)
        {
            const int size = 128;
            lowHealthTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Oracle health warning edge alpha",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var nx = Mathf.Abs(x / (float)(size - 1) * 2 - 1);
                var ny = Mathf.Abs(y / (float)(size - 1) * 2 - 1);
                // The center 70% of each axis stays fully clear; only the outer rim receives color.
                var rim = Mathf.InverseLerp(.70f, 1, Mathf.Max(nx, ny));
                var alpha = Mathf.SmoothStep(0, 1, rim);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
            lowHealthTexture.SetPixels32(pixels);
            lowHealthTexture.Apply(false, true);
            lowHealthSprite = Sprite.Create(lowHealthTexture, new UnityEngine.Rect(0, 0, size, size),
                new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            lowHealthSprite.name = "Oracle health warning edge sprite";
            lowHealthSprite.hideFlags = HideFlags.HideAndDontSave;
            // This is the first canvas child, behind all HUD labels, frames, and menus.
            lowHealthEdges = Paint(Stretch(parent, "LowHealthEdgeWarning"), Color.clear);
            lowHealthEdges.sprite = lowHealthSprite;
            lowHealthEdges.raycastTarget = false;
            lowHealthEdges.enabled = false;
            downedEdges = Paint(Stretch(parent, "DownedSoftEdgeDarkness"), new Color(.005f, .008f, .006f, .74f));
            downedEdges.sprite = lowHealthSprite;
            downedEdges.raycastTarget = false;
            downedEdges.enabled = false;
        }

        private void UpdateLowHealthWarning()
        {
            if (session == null || lowHealthEdges == null) return;
            downedEdges.enabled = DownedActive;
            if (downedEdges.enabled)
                downedEdges.color = new Color(.005f, .008f, .006f, .70f + Mathf.Sin(Time.unscaledTime * 2.2f) * .045f);
            var healthFraction = LocalHudHealthFraction;
            var severity = Mathf.InverseLerp(.35f, 0, healthFraction);
            var active = localHealthKnown && session.HasStarted && !session.Paused && !session.Victory && !session.IsDead && !session.IsDowned && severity > 0;
            lowHealthEdges.enabled = active;
            if (!active) return;
            warningPhase += Time.unscaledDeltaTime * Mathf.Lerp(.85f, 2.7f, severity) * Mathf.PI * 2;
            warningPhase = Mathf.Repeat(warningPhase, Mathf.PI * 2);
            var pulse = .5f + .5f * Mathf.Sin(warningPhase);
            lowHealthEdges.color = new Color(.72f, .025f, .035f, severity * Mathf.Lerp(.10f, .40f, pulse));
        }

        private void OnDestroy()
        {
            DisposeCampaignBook020();
            DisposeAchievementBadges020();
            DisposeObjectiveCompass();
            DisposeAnvilPreview();
            DisposeMenuShowcase();
            DisposeCustomization();
            StopBookReveal();
            BookWritingStateChanged = null;
            if (interfacePreferences != null) interfacePreferences.Changed -= InterfacePreferencesChanged;
            DetachDialogueSettings020();
            if (lowHealthSprite != null) Destroy(lowHealthSprite);
            if (lowHealthTexture != null) Destroy(lowHealthTexture);
        }

        private void BuildAdventureHud()
        {
            var location = Rect(hudRoot, "Location", new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -37), new Vector2(630, 90));
            BindTopHeading020(location);
            Label(location, "Journey", "INFINITE ODYSSEY", 13, Muted, TextAnchor.UpperCenter,
                new Vector2(0, -2), new Vector2(620, 24), new Vector2(.5f, 1));
            Label(location, "LocationName", "THE LOWER CATACOMBS", 27, Ivory, TextAnchor.MiddleCenter,
                new Vector2(0, -39), new Vector2(630, 40), new Vector2(.5f, 1));
            Rule(location, "LocationRule", new Vector2(0, -39), new Vector2(250, 1), Gold);
            Diamond(location, "LocationGem", new Vector2(0, -39), 6, Gold);

            BuildMinimap();

            var objective = Frame(hudRoot, "ObjectivePanel", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-36, -110), new Vector2(335, 117), Panel, MutedGold);
            Label(objective, "ObjectiveHeading", "THE AWAKENING", 13, Gold, TextAnchor.UpperLeft,
                new Vector2(18, -15), new Vector2(299, 22), new Vector2(0, 1));
            objectiveText = Label(objective, "Objective", "", 18, Ivory, TextAnchor.UpperLeft,
                new Vector2(18, -45), new Vector2(299, 62), new Vector2(0, 1));
            pauseButton = Button(hudRoot, "PauseButton", "PAUSE", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-36, -35), new Vector2(155, 43), () => {if(session.CursorFree015)session.Resume();else session.Pause();});
            pointerButtonLabel015=pauseButton.GetComponentInChildren<UnityEngine.UI.Text>();
            Label(hudRoot, "GraphicsAPI", GraphicsLabel(), 11, Muted, TextAnchor.UpperRight,
                new Vector2(-36, -86), new Vector2(335, 20), new Vector2(1, 1));
            shrineStatusText = Label(hudRoot, "ActiveShrineStatus", "", 12, Emerald, TextAnchor.UpperRight,
                new Vector2(-36, -243), new Vector2(335, 26), new Vector2(1, 1));

            var health = Frame(hudRoot, "OracleVitals", new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(36, 56), new Vector2(327, 137), Panel, MutedGold);
            Label(health, "OracleName", "ORACLE", 17, Gold, TextAnchor.MiddleLeft,
                new Vector2(17, -15), new Vector2(135, 27), new Vector2(0, 1));
            healthText = Label(health, "HealthValue", "125 / 125", 18, Ivory, TextAnchor.MiddleRight,
                new Vector2(-17, -15), new Vector2(157, 27), new Vector2(1, 1));
            walletText = Label(hudRoot, "ActualSavedHeartcrystals", "", 14, new Color(1f, .63f, .79f), TextAnchor.LowerLeft,
                new Vector2(36, 204), new Vector2(327, 24), new Vector2(0, 0));
            var healthBar = Rect(health, "HealthBar", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(18, -53), new Vector2(291, 14));
            Paint(healthBar, new Color(.19f, .13f, .12f, 1));
            healthFill = Paint(Stretch(healthBar, "HealthFill"), Emerald);
            Border(healthBar, Gold, 1);
            elementLabel = Label(health, "ElementLabel", "ELEMENT", 12, Muted, TextAnchor.MiddleLeft,
                new Vector2(18, -85), new Vector2(80, 22), new Vector2(0, 1));
            charges = new UnityEngine.UI.Image[4];
            for (var i = 0; i < charges.Length; i++)
            {
                var gem = Rect(health, "ArcaneCharge" + (i + 1), new Vector2(0, 1), new Vector2(.5f, .5f),
                    new Vector2(111 + i * 28, -96), new Vector2(12, 12));
                gem.localRotation = Quaternion.Euler(0, 0, 45);
                charges[i] = Paint(gem, new Color(.11f, .16f, .15f, 1));
                Border(gem, MutedGold, 1);
            }
            chargeText = Label(health, "ChargeValue", "0 / 4", 13, Ivory, TextAnchor.MiddleRight,
                new Vector2(-18, -85), new Vector2(80, 22), new Vector2(1, 1));
            BuildStamina020(health);

            spellRoot = Rect(hudRoot, "ElementAbilities", new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 58), new Vector2(660, 164));
            spells = new SpellView[4];
            for (var i = 0; i < spells.Length; i++) spells[i] = BuildSpell(spellRoot, i);
            ultimateLabel020 = Label(spellRoot, "UltimateAbility020", "ULTIMATE ABILITY", 22, Ivory, TextAnchor.MiddleCenter,
                new Vector2(0, 116), new Vector2(500, 32), new Vector2(.5f, .5f));
            ultimateLabel020.raycastTarget = false;
            ultimateLabel020.gameObject.SetActive(false);

            var progress = Rect(hudRoot, "OracleExperienceSlider", new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(36, 14), new Vector2(0, 34));
            progress.anchorMax = new Vector2(1, 0);
            progress.offsetMax = new Vector2(-36, 48);
            Paint(progress, new Color(.16f, .075f, .25f, .98f));
            progressFill = Paint(Stretch(progress, "GoldExperienceFill"), new Color(.78f, .62f, .31f));
            Border(progress, Gold, 1);
            experienceSlider = progress.gameObject.AddComponent<UnityEngine.UI.Slider>();
            experienceSlider.minValue = 0;
            experienceSlider.maxValue = 1;
            experienceSlider.fillRect = progressFill.rectTransform;
            experienceSlider.transition = UnityEngine.UI.Selectable.Transition.None;
            experienceSlider.interactable = false;
            var xpNavigation = experienceSlider.navigation;
            xpNavigation.mode = UnityEngine.UI.Navigation.Mode.None;
            experienceSlider.navigation = xpNavigation;
            progressText = Label(progress, "ActualLevelAndExperience", "", 21, Ivory, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
            progressText.rectTransform.anchorMin = Vector2.zero;
            progressText.rectTransform.anchorMax = Vector2.one;
            progressText.rectTransform.offsetMin = new Vector2(12, 1);
            progressText.rectTransform.offsetMax = new Vector2(-12, -1);
            var xpShadow = progressText.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            xpShadow.effectColor = new Color(.015f, .005f, .025f, 1);
            xpShadow.effectDistance = new Vector2(1, -1);
            experiencePickupText = Label(hudRoot, "AcceptedExperienceAndHeartstoneNotice", "", 17, Ivory, TextAnchor.LowerLeft,
                new Vector2(36, 236), new Vector2(327, 45), new Vector2(0, 0));

            var cross = Rect(hudRoot, "FocusReticle", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(4, 4));
            cross.localRotation = Quaternion.Euler(0, 0, 45);
            crosshair = Paint(cross, new Color(.95f, .95f, .86f, .75f));
            interactionText = Label(hudRoot, "InteractionHint", "", 19, Ivory, TextAnchor.MiddleCenter,
                new Vector2(0, -78), new Vector2(800, 62), new Vector2(.5f, .5f));
            var shadow = interactionText.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .95f);
            shadow.effectDistance = new Vector2(1, -1);
            BuildCampaignHud();
            BuildPartyHealth();
        }

        private void BuildMinimap()
        {
            var map = Frame(hudRoot, "CatacombMap", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(36, -36), new Vector2(229, 295), Panel, MutedGold);
            minimapRoot = map;
            minimapOpacityGroup = map.gameObject.AddComponent<CanvasGroup>();
            minimapOpacityGroup.blocksRaycasts = false;
            minimapTitle = Label(map, "MapTitle", "TUTORIAL ROOM · SOLO", 12, Gold, TextAnchor.MiddleCenter,
                new Vector2(0, -16), new Vector2(207, 22), new Vector2(.5f, 1));
            mapDividerLine=Rect(map,"MapDivider",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,93.5f),new Vector2(190,1));
            Paint(mapDividerLine,MutedGold);
            mapSurface = Rect(map, "ActualDungeonFootprint", new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -59), new Vector2(182, 186));
            mapSurface.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            mapLegend = Label(map, "MapLegend", "GREEN: YOU · VIOLET: PORTAL", 9, Muted, TextAnchor.MiddleCenter,
                new Vector2(0, 5), new Vector2(214, 42), new Vector2(.5f, 0));
            TryBuildMap();
            BuildReadinessHud();
            BuildCampaignCycleHud020();
            ApplyMinimapPreferences();
        }

        private void TryBuildMap()
        {
            if (session.World == null || session.World.MapRooms == null || session.World.MapRooms.Length == 0) return;
            if (mapBuilt && Time.unscaledTime < nextMapGeometryAudit) return;
            nextMapGeometryAudit = Time.unscaledTime + .25f;
            var signature = CalculateMapGeometrySignature();
            if (mapBuilt && (signature != mapGeometrySignature || renderedCoopMap != session.World.IsCoopArena || !ReferenceEquals(renderedMapRooms, session.World.MapRooms)
                || session.World.IsCoopArena && !ReferenceEquals(renderedMapPortals, session.World.InvasionPortals)))
            {
                // The world changed stages. Rebuild only the obsolete floor-plan children.
                for (var i = mapSurface.childCount - 1; i >= 0; i--)
                {
                    var previous = mapSurface.GetChild(i).gameObject;
                    previous.SetActive(false);
                    Destroy(previous);
                }
                mapPlayer = null;
                portalMarker = null;
                groundUnitMarkers.Clear();
                towerMarkers.Clear();
                minimapTowers = Array.Empty<PlacedElementShrine>();
                observedMapTowerCount = -1;
                nextMapUnitRefresh = 0;
                mapBuilt = false;
            }
            if (mapBuilt) return;
            mapBounds = session.World.MapBounds;
            if (mapBounds.width <= 0 || mapBounds.height <= 0) return;
            mapBuilt = true;
            mapGeometrySignature = signature;
            renderedCoopMap = session.World.IsCoopArena;
            renderedMapRooms = session.World.MapRooms;
            renderedMapPortals = session.World.InvasionPortals;
            RefreshMinimapContext(true);
            foreach (var room in session.World.MapRooms)
            {
                var r = Rect(mapSurface, "WalkableRoom", Vector2.zero, Vector2.zero,
                    MapPosition(new Vector3(room.xMin, 0, room.yMin)),
                    new Vector2(room.width * MapScale, room.height * MapScale));
                Paint(r, new Color(.30f, .33f, .30f, .75f));
                Border(r, new Color(.46f, .45f, .34f, .52f), 1);
            }
            BuildTutorialZoneMapMarkers();
            if (renderedCoopMap)
            {
                var sites = session.World.InvasionPortals;
                if (sites != null)
                    foreach (var site in sites)
                    {
                        var rift = Rect(mapSurface, "ActualInvasionPortal", Vector2.zero, new Vector2(.5f, .5f),
                            MapPosition(site), new Vector2(8, 8));
                        rift.localRotation = Quaternion.Euler(0, 0, 45);
                        Paint(rift, new Color(.72f, .40f, .96f));
                    }
                var shrine = Rect(mapSurface, "ActualCentralShrine", Vector2.zero, new Vector2(.5f, .5f),
                    MapPosition(session.World.ShrinePosition), new Vector2(6, 6));
                Paint(shrine, Emerald);
            }
            else
            {
                var portal = Rect(mapSurface, "PortalPosition", Vector2.zero, new Vector2(.5f, .5f),
                    MapPosition(session.World.PortalPosition), new Vector2(8, 8));
                portal.localRotation = Quaternion.Euler(0, 0, 45);
                portalMarker = Paint(portal, new Color(.42f, .25f, .56f));
                var portalGlyph=CreateMapIcon020(portal,5,Ivory,14);
                portalGlyph.rectTransform.localRotation=Quaternion.Euler(0,0,-45);
            }
            mapPlayer = Rect(mapSurface, "PlayerPositionAndFacing", Vector2.zero, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(10, 10));
            CreateMapIcon020(mapPlayer,6,Emerald,12);
            for (var i = 0; i < OracleCombat.MaxActiveEnemies; i++)
                AcquireMapMarker(groundUnitMarkers, i, "LiveGroundUnit", 3.5f, new Color(.92f, .38f, .31f)).gameObject.SetActive(false);
            for (var i = 0; i < 24; i++)
                AcquireMapMarker(towerMarkers, i, "LiveElementalTower", 5, Gold).gameObject.SetActive(false);
            mapPlayer.SetAsLastSibling();
        }

        private float MapScale => Mathf.Min(182 / Mathf.Max(1, mapBounds.width), 186 / Mathf.Max(1, mapBounds.height));

        private Vector2 MapPosition(Vector3 worldPosition)
        {
            var scale = MapScale;
            return new Vector2((182 - mapBounds.width * scale) * .5f + (worldPosition.x - mapBounds.xMin) * scale,
                (186 - mapBounds.height * scale) * .5f + (worldPosition.z - mapBounds.yMin) * scale);
        }

        private int CalculateMapGeometrySignature()
        {
            unchecked
            {
                var world = session.World;
                var signature = world.MapBounds.GetHashCode() * 31 + world.MapRooms.Length;
                for (var i = 0; i < world.MapRooms.Length && i < 128; i++) signature = signature * 31 + world.MapRooms[i].GetHashCode();
                var portals = world.InvasionPortals;
                if (portals != null)
                    for (var i = 0; i < portals.Length && i < 8; i++) signature = signature * 31 + portals[i].GetHashCode();
                return (signature * 31 + world.PortalPosition.GetHashCode()) * 31 + world.ShrinePosition.GetHashCode();
            }
        }

        private void RefreshLiveMapMarkers()
        {
            if (!mapBuilt || mapSurface == null || Time.unscaledTime < nextMapUnitRefresh) return;
            nextMapUnitRefresh = Time.unscaledTime + .1f;
            var enemyIndex = 0;
            var enemies = session.Combat != null ? session.Combat.ActiveEnemies : null;
            if (enemies != null)
                for (var i = 0; i < enemies.Count && i < OracleCombat.MaxActiveEnemies; i++)
                {
                    var enemy = enemies[i];
                    if (enemy == null || !enemy.Alive || !enemy.gameObject.activeInHierarchy
                        || !ContainsMapPoint(enemy.transform.position)) continue;
                    var marker = AcquireMapMarker(groundUnitMarkers, enemyIndex++, "LiveGroundUnit", 3.5f, new Color(.92f, .38f, .31f));
                    marker.anchoredPosition = MapPosition(enemy.transform.position);
                    marker.gameObject.SetActive(true);
                }
            HideUnusedMapMarkers(groundUnitMarkers, enemyIndex);
            var actualTowerCount = session.Builder != null ? session.Builder.PlacedCount : 0;
            var towersChanged = actualTowerCount != observedMapTowerCount;
            for (var i = 0; !towersChanged && i < minimapTowers.Length; i++)
            {
                var cached = minimapTowers[i];
                if (cached == null || cached.IsDestroyed || !cached.gameObject.activeInHierarchy || session.World == null
                    || !cached.transform.IsChildOf(session.World.transform)) towersChanged = true;
            }
            if (session.World != null && session.World.IsCoopArena && towersChanged)
            {
                // No public enumeration exists: discover only on membership change
                // or a stale cached object, never on an unchanged repeating timer.
                observedMapTowerCount = actualTowerCount;
                minimapTowers = actualTowerCount == 0 ? Array.Empty<PlacedElementShrine>()
                    : FindObjectsByType<PlacedElementShrine>(FindObjectsInactive.Exclude);
            }
            var towerIndex = 0;
            for (var i = 0; i < minimapTowers.Length && i < 128 && towerIndex < 24; i++)
            {
                var tower = minimapTowers[i];
                if (tower == null || tower.IsDestroyed || !tower.gameObject.activeInHierarchy || session.World == null
                    || !tower.transform.IsChildOf(session.World.transform) || !ContainsMapPoint(tower.Position)) continue;
                var marker = AcquireMapMarker(towerMarkers, towerIndex++, "LiveElementalTower", 5, Gold);
                marker.anchoredPosition = MapPosition(tower.Position);
                marker.gameObject.SetActive(true);
            }
            HideUnusedMapMarkers(towerMarkers, towerIndex);
            RefreshRefugeStationMapOverlay();
            if (mapPlayer != null && mapPlayer.GetSiblingIndex() != mapSurface.childCount - 1) mapPlayer.SetAsLastSibling();
        }

        private bool ContainsMapPoint(Vector3 position)
            => !float.IsNaN(position.x) && !float.IsNaN(position.z) && !float.IsInfinity(position.x) && !float.IsInfinity(position.z)
                && mapBounds.Contains(new Vector2(position.x, position.z));

        private RectTransform AcquireMapMarker(List<RectTransform> pool, int index, string name, float size, Color color)
        {
            if (index >= pool.Count)
            {
                var marker = Rect(mapSurface, name, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
                Paint(marker, color);
                pool.Add(marker);
            }
            return pool[index];
        }

        private static void HideUnusedMapMarkers(List<RectTransform> pool, int count)
        {
            for (var i = count; i < pool.Count; i++) if (pool[i] != null) pool[i].gameObject.SetActive(false);
        }

        private SpellView BuildSpell(RectTransform parent, int index)
        {
            var slot = Rect(parent, "Spell" + (index + 1), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2((index - 1.5f) * 157, 0), new Vector2(150, 164));
            var key = Label(slot, "Key", AbilityBindings.GetLabel(index), 14, Muted, TextAnchor.MiddleCenter,
                new Vector2(0, 70), new Vector2(100, 24), new Vector2(.5f, .5f));
            var diamond = Rect(slot, "AbilityDiamond", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 14), new Vector2(62, 62));
            diamond.localRotation = Quaternion.Euler(0, 0, 45);
            var view = new SpellView();
            view.Key = key;
            view.Background = Paint(diamond, Panel);
            view.Border = Border(diamond, MutedGold, 1.5f);
            view.UltimateOverlay020 = Paint(Stretch(diamond, "UltimateRainbow020", 2, 2, 2, 2), Color.clear);
            view.UltimateOverlay020.raycastTarget = false;
            view.UltimateOverlay020.enabled = false;
            var icon = Rect(slot, "AbilitySigil", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 14), new Vector2(32, 32));
            var emblem = OracleElementEmblems.Get(index);
            if (emblem != null)
            {
                icon.sizeDelta = new Vector2(44, 44);
                view.EmblemImage = Paint(icon, Color.white);
                view.EmblemImage.sprite = emblem;
                view.EmblemImage.preserveAspect = true;
                view.EmblemImage.raycastTarget = false;
                view.Icon = new UnityEngine.UI.Graphic[] { view.EmblemImage };
            }
            else view.Icon = DrawSigil(icon, index);
            var veil = Rect(slot, "CooldownVeil", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 14), new Vector2(58, 58));
            veil.localRotation = Quaternion.Euler(0, 0, 45);
            view.Veil = Paint(veil, new Color(.015f, .025f, .025f, .72f));
            view.State = Label(slot, "CooldownOrCharge", "", 22, Ivory, TextAnchor.MiddleCenter,
                new Vector2(0, 13), new Vector2(84, 36), new Vector2(.5f, .5f));
            view.Name = Label(slot, "SpellName", "", 15, Ivory, TextAnchor.UpperCenter,
                new Vector2(0, -42), new Vector2(150, 42), new Vector2(.5f, .5f));
            return view;
        }

        private UnityEngine.UI.Graphic[] DrawSigil(RectTransform parent, int index)
        {
            var glyph = parent.gameObject.AddComponent<OracleElementGlyph>();
            glyph.Element = index;
            glyph.color = Emerald;
            glyph.raycastTarget = false;
            return new UnityEngine.UI.Graphic[] { glyph };
        }

        private void BuildMenu(Transform parent)
        {
            BuildExpandedMenus(parent);
        }

        private void BuildSensitivity(RectTransform parent)
        {
            sensitivityRow = Rect(parent, "MouseLookSetting", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 70));
            Height(sensitivityRow, 70);
            Label(sensitivityRow, "SettingName", "MOUSE LOOK SENSITIVITY", 12, Gold, TextAnchor.UpperLeft,
                Vector2.zero, new Vector2(360, 22), new Vector2(0, 1));
            sensitivityText = Label(sensitivityRow, "SettingValue", "1.00x", 13, Ivory, TextAnchor.UpperRight,
                Vector2.zero, new Vector2(120, 22), new Vector2(1, 1));
            var sliderRoot = Stretch(sensitivityRow, "SensitivitySlider", 3, 31, 3, 12);
            sensitivitySlider = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var track = Stretch(sliderRoot, "Track", 0, 10, 0, 10);
            Paint(track, new Color(.20f, .25f, .23f, 1), true);
            var fill = Paint(Stretch(track, "Fill"), Gold);
            var handleArea = Stretch(sliderRoot, "HandleArea", 5, 0, 5, 0);
            var handle = Rect(handleArea, "Handle", new Vector2(0, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(11, 24));
            var handleImage = Paint(handle, Ivory, true);
            sensitivitySlider.fillRect = fill.rectTransform;
            sensitivitySlider.handleRect = handle;
            sensitivitySlider.targetGraphic = handleImage;
            sensitivitySlider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            sensitivitySlider.minValue = .035f;
            sensitivitySlider.maxValue = .25f;
            sensitivitySlider.onValueChanged.AddListener(value =>
            {
                if (session.Player == null) return;
                session.Player.MouseSensitivity = value;
                sensitivityText.text = (value / .105f).ToString("0.00") + "x";
            });
        }

        private void ActivatePrimary()
        {
            if (session.IsDead || session.Victory) session.Restart();
            else if (!session.HasStarted) session.Begin();
            else session.Resume();
        }

        private void Update()
        {
            if (!built || session == null) return;
            if(UpdateCampaignBook020())return;
            FitMenuToWindow();
            UpdateMenu();
            UpdateLowHealthWarning();
            UpdateExpandedUi();
            RefreshTrainingLab();
            UpdatePartyHealth();
            RefreshReadinessHud();
            if (!session.HasStarted) { experiencePresentationReady = false; return; }
            RefreshLocalHudHealth();
            UpdateStamina020();
            if(UpdateCampaignHubHud020())return;
            UpdateExperiencePresentation();
            TryBuildMap();
            RefreshMinimapContext();
            RefreshLiveMapMarkers();
            if (mapPlayer != null && session.Player != null)
            {
                mapPlayer.anchoredPosition = MapPosition(session.Player.transform.position);
                mapPlayer.localRotation = Quaternion.Euler(0, 0, -session.Player.transform.eulerAngles.y);
            }
            if (portalMarker != null) portalMarker.color = session.Tutorial != null && session.Tutorial.Finished
                ? new Color(.72f, .40f, .96f) : new Color(.42f, .25f, .56f);
            if (session.Combat != null)
            {
                var previewIndex = AbilityGuidePreviewIndex;
                var abilityPreview = previewIndex >= 0 && previewIndex < 4;
                crosshair.color = session.Combat.HitFlash > 0 ? Emerald : new Color(.95f, .95f, .86f, .75f);
                for (var i = 0; i < charges.Length; i++)
                {
                    charges[i].gameObject.SetActive(session.CombatUnlocked || abilityPreview);
                    if (session.Progression != null)
                    {
                        var color = session.Progression.ElementColors[i];
                        var highlighted = abilityPreview ? previewIndex == i : session.Combat != null &&
                            (session.Combat.SelectionPending ? session.Combat.PreparedElement == i : (session.Combat.ConfirmedMask & (1 << i)) != 0);
                        color.a = highlighted ? 1 : .27f;
                        charges[i].color = color;
                    }
                }
                elementLabel.text = abilityPreview ? "GUIDE" : "SELECTION";
                elementLabel.gameObject.SetActive(session.CombatUnlocked || abilityPreview);
                chargeText.gameObject.SetActive(session.CombatUnlocked || abilityPreview);
                RefreshSpells();
            }
            textRefresh -= Time.unscaledDeltaTime;
            if (textRefresh <= 0)
            {
                textRefresh = .1f;
                RefreshText();
            }
        }

        private void FitMenuToWindow()
        {
            FitExpandedPanels();
        }

        private void UpdateExperiencePresentation()
        {
            var progression = session.Progression;
            if (progression == null) return;
            var now = Time.unscaledTime;
            var wallet = session.Shop != null ? session.Shop.Heartcrystals : 0;
            if (!experiencePresentationReady)
            {
                experiencePresentationReady = true;
                displayedLevel = progression.Level;
                displayedExperience = progression.Experience01;
                observedXpSequence = progression.XpGainSequence;
                observedWallet = wallet;
                experienceWrapUntil = xpNoticeUntil = walletNoticeUntil = 0;
                xpNotice = walletNotice = "";
            }
            if (progression.Level < displayedLevel || progression.XpGainSequence < observedXpSequence)
            {
                displayedExperience = progression.Experience01;
                experienceWrapUntil = 0;
                xpNotice = walletNotice = "";
                xpNoticeUntil = walletNoticeUntil = 0;
            }
            else if (progression.Level > displayedLevel)
            {
                displayedExperience = 1;
                experienceWrapUntil = now + .16f;
            }
            if (progression.XpGainSequence > observedXpSequence && progression.LastXpGainAmount > 0)
            {
                xpNotice = "+" + progression.LastXpGainAmount + " XP" + (progression.Level > displayedLevel ? "  ·  LEVEL " + progression.Level : "");
                xpNoticeUntil = now + 2.2f;
            }
            if (!session.CoopMode && wallet > observedWallet)
            {
                var gained = wallet - observedWallet;
                walletNotice = "+" + gained + (gained == 1 ? " HEARTSTONE" : " HEARTSTONES");
                walletNoticeUntil = now + 2.2f;
            }
            else if (wallet < observedWallet) walletNoticeUntil = 0;
            displayedLevel = progression.Level;
            observedXpSequence = progression.XpGainSequence;
            observedWallet = wallet;
            if (experienceWrapUntil > 0 && now >= experienceWrapUntil)
            { displayedExperience = 0; experienceWrapUntil = 0; }
            if (experienceWrapUntil <= 0)
                displayedExperience = Mathf.Lerp(displayedExperience, progression.Experience01, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 14));
            experienceSlider.SetValueWithoutNotify(displayedExperience);
            var skillKey=GameActionBindings.GetKey("SkillTree");
            float pickupMetres=progression.PickupRangeMetres;
            if(xpRangeUiLevel!=progression.Level||xpRangeUiXp!=progression.Experience||xpRangeUiPoints!=progression.SkillPoints||
                xpRangeUiMetres!=pickupMetres||xpRangeUiKey!=skillKey||string.IsNullOrEmpty(progressText.text))
            {
                xpRangeUiLevel=progression.Level;xpRangeUiXp=progression.Experience;xpRangeUiPoints=progression.SkillPoints;
                xpRangeUiMetres=pickupMetres;xpRangeUiKey=skillKey;
                progressText.text = (progression.Level >= ElementProgression.MaximumLevel
                    ? "LEVEL " + progression.Level + "  ·  MAXIMUM LEVEL"
                    : "LEVEL " + progression.Level + "  ·  XP " + progression.Experience + " / " + progression.ExperienceToNextLevel
                        + "  ·  " + progression.SkillPoints + " SKILL POINTS  [" + GameActionBindings.GetLabel("SkillTree") + "]")
                    + "  |  " + progression.PickupRangeLabel;
            }
            walletText.text = session.Shop != null && !session.CoopMode ? "HEARTSTONES  " + wallet : "";
            var xpVisible = now < xpNoticeUntil;
            var walletVisible = now < walletNoticeUntil;
            experiencePickupText.text = (xpVisible ? xpNotice : "") + (xpVisible && walletVisible ? "\n" : "") + (walletVisible ? walletNotice : "");
        }

        private void RefreshText()
        {
            RefreshLocalHudHealth();
            objectiveText.text = CampaignObjectiveOr(session.RefugeNpcs != null && !string.IsNullOrEmpty(session.RefugeNpcs.NextObjectives)
                && (session.Tutorial == null || session.Tutorial.Finished) ? session.RefugeNpcs.NextObjectives : session.Objective);
            if (session.Combat != null)
            {
                chargeText.text = AbilityGuidePreviewIndex >= 0 ? "PREVIEW" : session.Combat.ConfirmedMask == 15 ? "4 / 4" : SelectionCountLabel(session.Combat.ConfirmedMask);
                interactionText.text = session.Paused || session.Victory || session.IsDead || session.IsDowned ? ""
                    : !string.IsNullOrEmpty(session.DownedHint) ? session.DownedHint
                    : session.Builder != null && session.Builder.IsPlacing ? session.Builder.Hint
                    : session.FieldSmith != null && !string.IsNullOrEmpty(session.FieldSmith.InteractionHint) ? session.FieldSmith.InteractionHint
                    : session.Builder != null && !string.IsNullOrEmpty(session.Builder.Hint) ? session.Builder.Hint
                    : session.Shop != null && !string.IsNullOrEmpty(session.Shop.InteractionHint) ? session.Shop.InteractionHint
                    : session.RefugeNpcs != null && !string.IsNullOrEmpty(session.RefugeNpcs.InteractionHint) ? session.RefugeNpcs.InteractionHint
                    : session.Books != null && !string.IsNullOrEmpty(session.Books.Hint) ? session.Books.Hint
                    : session.Progression != null && !string.IsNullOrEmpty(session.Progression.HeartcrystalHint) ? session.Progression.HeartcrystalHint
                    : session.RefugeActivities != null && !string.IsNullOrEmpty(session.RefugeActivities.Hint) ? session.RefugeActivities.Hint
                    : session.Combat.InteractionHint == session.Combat.SelectionStatus ? "" : session.Combat.InteractionHint;
                shrineStatusText.text = session.Combat.ShrineActive ? "SHRINE AWAKENED  ·  AUTOMATIC GUARDIAN" : "";
            }
        }

        private void RefreshSpells()
        {
            var combat = session.Combat;
            var progression = session.Progression;
            if (progression == null || combat == null) return;
            var previewIndex = AbilityGuidePreviewIndex;
            var preview = previewIndex >= 0 && previewIndex < 4;
            var tripleRecipe = SpellIngredientRules.Count(combat.ConfirmedMask) == 3;
            bool ultimate020 = !preview && combat.ConfirmedMask == 15;
            if (ultimateLabel020 != null && ultimateLabel020.gameObject.activeSelf != ultimate020) ultimateLabel020.gameObject.SetActive(ultimate020);
            float rainbowPhase020 = Mathf.Repeat(Time.unscaledTime * .12f, 1f);
            for (var i = 0; i < spells.Length; i++)
            {
                var view = spells[i];
                var prepared = !preview && combat.SelectionPending && combat.PreparedElement == i;
                var confirmed = !preview && (combat.ConfirmedMask & (1 << i)) != 0;
                var selected = preview ? previewIndex == i : prepared || confirmed;
                var color = progression.ElementColors[i];
                var cooldown = !preview && combat.ElementSwitchCooldowns != null && i < combat.ElementSwitchCooldowns.Length
                    ? Mathf.Max(0, combat.ElementSwitchCooldowns[i]) : 0;
                var tenths = cooldown > .001f ? Mathf.CeilToInt(cooldown * 10f) : 0;
                var state = ultimate020 ? 7 : preview ? selected ? 0 : 1 : prepared ? 2 : confirmed ? tripleRecipe ? 6 : 3 : tenths > 0 ? 4 : 5;
                foreach (var edge in view.Border) edge.color = prepared ? Gold : confirmed ? tripleRecipe ? Gold : Emerald : selected ? Gold : MutedGold;
                view.UltimateOverlay020.enabled = ultimate020;
                if (ultimate020)
                {
                    Color rainbow020 = Color.HSVToRGB(Mathf.Repeat(rainbowPhase020 + i * .25f, 1f), .76f, 1f);
                    foreach (var edge in view.Border) edge.color = rainbow020;
                    rainbow020.a = .28f;
                    view.UltimateOverlay020.color = rainbow020;
                }
                foreach (var icon in view.Icon) icon.color = icon == view.EmblemImage ? Color.white : Color.Lerp(color, Ivory, selected ? .22f : 0);
                view.Background.color = selected ? new Color(.07f, .13f, .11f, .95f) : Panel;
                view.Veil.enabled = tenths > 0;
                if (view.DisplayCooldownTenths != tenths)
                {
                    view.DisplayCooldownTenths = tenths;
                    view.State.text = tenths > 0 ? (tenths * .1f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "";
                }
                if (view.DisplayState != state || view.DisplayLevel != progression.Levels[i])
                {
                    view.DisplayState = state; view.DisplayLevel = progression.Levels[i];
                    var label = state == 0 ? "GUIDE PREVIEW" : state == 1 ? "AVAILABLE" : state == 2 ? "PREPARED"
                        : state == 3 ? "CONFIRMED" : state == 4 ? "RECOVERING" : state == 6 ? "TRIPLE VOLLEY" : state == 7 ? "ULTIMATE" : "READY";
                    view.Name.text = progression.ElementNames[i] + "  Lv " + progression.Levels[i] + "\n" + label;
                }
                var key = AbilityBindings.GetLabel(i);
                if (view.DisplayKey != key) { view.DisplayKey = key; view.Key.text = key; }
                view.Key.color = selected ? Gold : Muted;
                view.Name.color = selected ? Ivory : Muted;
            }
        }

        private void UpdateMenu()
        {
            UpdateExpandedMenu();
        }

        private static string GraphicsLabel() => "BETA " + Application.version + "  ·  Unity 6.6 (" + Application.unityVersion + ")  ·  " + SystemInfo.graphicsDeviceType;

        private static void SetFill(UnityEngine.UI.Image fill, float value)
        {
            if (fill == null) return;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
        }

        private RectTransform Frame(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Color background, Color borderColor)
        {
            var rect = Rect(parent, name, anchor, pivot, position, size);
            Paint(rect, background);
            Border(rect, borderColor, 1);
            // Extended gold corners make a light ornamental frame with four simple pairs of strokes.
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            {
                var corner = Rect(rect, "Ornament" + x + y, new Vector2(x, y), new Vector2(x, y),
                    new Vector2(x == 0 ? -3 : 3, y == 0 ? -3 : 3), new Vector2(17, 1));
                Paint(corner, Gold);
                var stem = Rect(rect, "CornerStem" + x + y, new Vector2(x, y), new Vector2(x, y),
                    new Vector2(x == 0 ? -3 : 3, y == 0 ? -3 : 3), new Vector2(1, 17));
                Paint(stem, Gold);
            }
            return rect;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Stretch(Transform parent, string name, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            var rect = Rect(parent, name, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        private static UnityEngine.UI.Image Paint(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private UnityEngine.UI.Text Label(Transform parent, string name, string text, int size, Color color,
            TextAnchor alignment, Vector2 position, Vector2 dimensions, Vector2 anchor)
        {
            var rect = Rect(parent, name, anchor, anchor, position, dimensions);
            var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private UnityEngine.UI.Text MenuLabel(Transform parent, string name, string text, int size, Color color, float height)
        {
            var label = Label(parent, name, text, size, color, TextAnchor.MiddleCenter, Vector2.zero,
                new Vector2(0, height), new Vector2(.5f, .5f));
            Height(label.rectTransform, height);
            return label;
        }

        private UnityEngine.UI.Button Button(Transform parent, string name, string text, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var rect = Frame(parent, name, anchor, pivot, position, size, new Color(.08f, .12f, .105f, .98f), MutedGold);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.22f, 1.14f, 1);
            colors.selectedColor = new Color(1.12f, 1.22f, 1.14f, 1);
            colors.pressedColor = new Color(.65f, .8f, .7f, 1);
            colors.fadeDuration = session.Graphics != null && session.Graphics.ParticleQuality == 0 ? 0 : .12f;
            button.colors = colors;
            hoverButtons.Add(button);
            button.onClick.AddListener(action);
            var hover = rect.gameObject.AddComponent<OdysseyHudHoverCue>();
            hover.OnEnter = () =>
            {
                if (!button.IsInteractable() || Time.unscaledTime < nextUiHoverAt) return;
                nextUiHoverAt = Time.unscaledTime + .1f;
                session.Audio?.PlayCue("ui_hover");
            };
            var label = Label(rect, "Label", text, size.y < 50 ? 13 : 16, Ivory, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10, 4);
            label.rectTransform.offsetMax = new Vector2(-10, -4);
            return button;
        }

        private static void Height(RectTransform rect, float height)
        {
            var item = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            item.minHeight = item.preferredHeight = height;
            item.flexibleHeight = 0;
        }

        private static UnityEngine.UI.Image[] Border(RectTransform parent, Color color, float thickness)
        {
            var result = new UnityEngine.UI.Image[4];
            for (var i = 0; i < 4; i++)
            {
                var edge = Rect(parent, "Border" + i, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                if (i < 2)
                {
                    edge.anchorMin = new Vector2(0, i);
                    edge.anchorMax = new Vector2(1, i);
                    edge.pivot = new Vector2(.5f, i);
                    edge.sizeDelta = new Vector2(0, thickness);
                }
                else
                {
                    edge.anchorMin = new Vector2(i - 2, 0);
                    edge.anchorMax = new Vector2(i - 2, 1);
                    edge.pivot = new Vector2(i - 2, .5f);
                    edge.sizeDelta = new Vector2(thickness, 0);
                }
                edge.anchoredPosition = Vector2.zero;
                result[i] = Paint(edge, color);
            }
            return result;
        }

        private static void Rule(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            Paint(Rect(parent, name, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size), color);
        }

        private static void Diamond(Transform parent, string name, Vector2 position, float size, Color color)
        {
            var rect = Rect(parent, name, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, new Vector2(size, size));
            rect.localRotation = Quaternion.Euler(0, 0, 45);
            Paint(rect, color);
        }
    }

    // Handles hover only, so wheel and drag events can reach a parent ScrollRect.
    public sealed class OdysseyHudHoverCue : MonoBehaviour, IPointerEnterHandler
    {
        internal Action OnEnter;
        public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();
    }

    /// <summary>Four original, scalable line glyphs: flame, droplet, mountain and wind.</summary>
    public sealed class OracleElementGlyph : UnityEngine.UI.MaskableGraphic
    {
        private int element;
        public int Element { get => element; set { element = Mathf.Clamp(value, 0, 3); SetVerticesDirty(); } }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            switch (element)
            {
                case 0:
                    Path(mesh, true, new Vector2(0,.48f), new Vector2(.12f,.15f), new Vector2(.28f,.23f),
                        new Vector2(.34f,-.03f),new Vector2(.25f,-.32f),new Vector2(0,-.46f),new Vector2(-.25f,-.32f),
                        new Vector2(-.34f,-.03f),new Vector2(-.22f,.24f),new Vector2(-.17f,.01f),new Vector2(-.02f,.16f));
                    break;
                case 1:
                    Path(mesh, true, new Vector2(0,.49f),new Vector2(.32f,.03f),new Vector2(.33f,-.20f),
                        new Vector2(.18f,-.40f),new Vector2(0,-.46f),new Vector2(-.18f,-.40f),new Vector2(-.33f,-.20f),new Vector2(-.32f,.03f));
                    Path(mesh, false, new Vector2(-.15f,-.17f),new Vector2(0,-.23f),new Vector2(.15f,-.17f));
                    break;
                case 2:
                    Path(mesh, true, new Vector2(-.45f,-.36f),new Vector2(-.14f,.26f),new Vector2(.02f,.02f),
                        new Vector2(.19f,.46f),new Vector2(.45f,-.36f));
                    Path(mesh, false, new Vector2(-.25f,-.16f),new Vector2(.26f,-.16f));
                    break;
                default:
                    Path(mesh, false,new Vector2(-.43f,.24f),new Vector2(.10f,.24f),new Vector2(.23f,.34f),
                        new Vector2(.14f,.46f),new Vector2(.02f,.42f));
                    Path(mesh, false,new Vector2(-.48f,0),new Vector2(.29f,0),new Vector2(.44f,.12f),
                        new Vector2(.35f,.26f),new Vector2(.24f,.22f));
                    Path(mesh, false,new Vector2(-.33f,-.25f),new Vector2(.10f,-.25f),new Vector2(.22f,-.37f),
                        new Vector2(.14f,-.47f),new Vector2(.02f,-.43f));
                    break;
            }
        }

        private void Path(UnityEngine.UI.VertexHelper mesh, bool closed, params Vector2[] points)
        {
            for (var i = 1; i < points.Length; i++) Segment(mesh, points[i - 1], points[i]);
            if (closed && points.Length > 2) Segment(mesh, points[points.Length - 1], points[0]);
        }

        private void Segment(UnityEngine.UI.VertexHelper mesh, Vector2 from, Vector2 to)
        {
            var rectangle = GetPixelAdjustedRect();
            var scale = Mathf.Min(rectangle.width, rectangle.height);
            var delta = to - from;
            if (scale <= 0 || delta.sqrMagnitude < .00001f) return;
            var normal = new Vector2(-delta.y, delta.x).normalized * .038f;
            var index = mesh.currentVertCount;
            mesh.AddVert(rectangle.center + (from - normal) * scale, color, Vector2.zero);
            mesh.AddVert(rectangle.center + (from + normal) * scale, color, Vector2.zero);
            mesh.AddVert(rectangle.center + (to + normal) * scale, color, Vector2.zero);
            mesh.AddVert(rectangle.center + (to - normal) * scale, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas, local XZ map projection and unchanged session world.
// Array Formats: Fixed tutorial UI markers and cached scalar visibility/context; no simulation ABI change.
// Dependencies/Pointers: Existing tutorial/party/session owners; root integrates exact guarded hunks and verifies actual Unity/browser behavior.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up refuge/Canvas and true floor contact.
// Array Formats: Fixed activity anchors, existing bounded combat/room owners and event-only UI.
// Dependencies/Pointers: Root-integrated tutorial activities retain the connected party and use the existing Skip/Resume authority.
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
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing HUD/start/pause label rectangles; no world or gameplay changes.
// Array Formats: Cold-created beta/game/engine/backend caption; reads the actual Application.unityVersion.
// Dependencies/Pointers: Game release channel is Beta; engine version is Unity 6.6 with its actual full patch identifier. No frame-loop allocation or version-setting mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing player world metres/Y-up, listener-relative audio and lower-left HUD vitals Canvas.
// Array Formats: One scalar stamina state, cold cached UI/clip references and same four shared cue voices; no save, wire, collision or authority fields change.
// Dependencies/Pointers: OraclePlayer owns controlled stamina transitions and existing motion smoothing. Exhaustion locks until fresh recovered press; root owns all Assets/import/runtime/browser acceptance.
