using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>Local placement preview; only the defense director authorizes actual buildings.</summary>
    public sealed partial class ShrinePlacementBuilder : MonoBehaviour
    {
        private const float BuildReach = 25f;
        private const int MaximumBuildings = 4;
        public const int TowerTypeCount = TowerModelRecipes.Types;
        public static readonly string[] TowerNames = { "Fire Tower", "Water Tower", "Earth Tower", "Air Spire" };
        public static readonly string[] TowerDescriptions =
        {
            "Burns ground foes within 9 metres; cannot target flyers.",
            "Slows flying foes within 9 metres; cannot target ground foes.",
            "Staggers ground foes within 9 metres; cannot target flyers.",
            "Repels flying foes within 9 metres; cannot target ground foes."
        };
        public static readonly Color[] TowerColors =
        {
            new Color(1f,.36f,.12f),new Color(.18f,.68f,1f),new Color(.62f,.78f,.29f),new Color(.66f,.91f,1f)
        };
        private readonly Dictionary<int, PlacedElementShrine> placed = new Dictionary<int, PlacedElementShrine>(16);
        private readonly RaycastHit[] groundHits = new RaycastHit[32];
        private readonly List<Mesh> ghostMeshes = new List<Mesh>(3);
        private readonly Material[] elementMaterials = new Material[TowerTypeCount];
        private GameSession session;
        SavedTowerModelLibrary towerModelLibrary;
        SavedTowerModelPreview towerModelPreview;
        TowerPadHighlightBatch padHighlights;
        TowerPlatformGeometry highlightedPlatforms,menuPlatforms;
        readonly TowerPadDisplayState[] padStates=new TowerPadDisplayState[4];
        int hoveredPad=-1,menuPad=-1;
        string menuRun=string.Empty;
        bool hoveredAvailable;
        public bool ModelsReady=>towerModelLibrary!=null;
        public string ModelStatus{get;private set;}="Tower building is unavailable.";
        public string ModelDiagnostic{get;private set;}="Not initialized.";
        public int HoveredBuildPadIndex=>hoveredPad;
        public bool CanOpenHoveredBuildPad=>Available&&ModelsReady&&(laneOpening015||LaneBuildContextValid015);
        public bool BuildMenuContextValid=>LaneBuildContextValid015;
        private Material green, gold, ghostMaterial;
        private GameObject ghost, contextMarker;
        private Vector3 placementPosition;
        private int selectedElement;
        private bool validPreview;
        private int suppressThroughFrame;

        public bool IsPlacing { get; private set; }
        public string Hint { get; private set; } = "";
        public int PlacedCount => placed.Count;
        private static readonly Vector3[] NoBuildPads = new Vector3[0];
        public Vector3[] BuildPads => session != null && session.World != null
            ? session.World.TowerBuildPads : NoBuildPads;
        public int GetLane(Vector3 position) => position.x < 0f ? 0 : 1;

        public void Initialize(GameSession gameSession)
        {
            session = gameSession;
            if(towerModelLibrary==null)
            {
                if(SavedTowerModelLibrary.TryCreate(session.TowerModels,out towerModelLibrary,out string diagnostic))
                {TowerModelClearanceProfiles.Prewarm(towerModelLibrary);ModelStatus="Saved tower art ready.";ModelDiagnostic=string.Empty;}
                else {ModelStatus="Tower building is unavailable.";ModelDiagnostic=diagnostic;}
            }
            if (green == null)
            {
                green = MakeMaterial("Placed shrine emerald", new Color(.055f, .23f, .13f), .12f);
                gold = MakeMaterial("Placed shrine astral brass", new Color(.63f, .43f, .12f), .2f);
                for (int i = 0; i < TowerTypeCount; i++)
                    elementMaterials[i] = MakeMaterial("Placed " + TowerNames[i] + " crystal", TowerColors[i], 1.8f);
                ghostMaterial = MakeMaterial("Shrine placement preview", new Color(.2f, 1f, .5f, .5f), .6f);
                ghostMaterial.SetFloat("_Surface", 1f);
                ghostMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                ghostMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                ghostMaterial.SetInt("_ZWrite", 0);
                ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                ghostMaterial.renderQueue = (int)RenderQueue.Transparent;
                ghost = new GameObject("Ignore-raycast shrine placement preview");
                if(ModelsReady){towerModelPreview=ghost.AddComponent<SavedTowerModelPreview>();towerModelPreview.Initialize(towerModelLibrary,ghostMaterial);}
                foreach (Transform child in ghost.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
                ghost.SetActive(false);
                contextMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                contextMarker.name = "Tower pad interaction highlight";
                contextMarker.layer = 2;
                contextMarker.transform.SetParent(session.World.transform, false);
                contextMarker.transform.localScale = new Vector3(.95f,.025f,.95f);
                Collider markerCollider = contextMarker.GetComponent<Collider>();
                markerCollider.enabled = false;
                Destroy(markerCollider);
                Renderer markerRenderer = contextMarker.GetComponent<Renderer>();
                markerRenderer.sharedMaterial = ghostMaterial;
                markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
                markerRenderer.receiveShadows = false;
                contextMarker.SetActive(false);
            }
            InitializeLaneBuilding015();InitializeWorkshop015();
            CancelPlacement();
        }

        private bool Available => session != null && session.CoopMode && session.Defense != null && session.Defense.Running&&session.World!=null&&session.World.IsCoopArena&&session.World.TowerPlatforms!=null;

        private void Update()
        {
            if (session == null) return;
            TickWorkshop015();
            if(UpdateLaneBuilding015())return;
            if (IsPlacing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }
            if (!Available) { CancelPlacement();padHighlights?.Apply(padStates,false);return; }
            UpdatePadHighlights();
            if(!ModelsReady){CancelPlacement();Hint=ModelStatus;return;}
            if (!session.IsPlaying || AbilityBindings.IsRebinding || Cursor.lockState != CursorLockMode.Locked)
            {
                if (ghost != null) ghost.SetActive(false);
                if (contextMarker != null) contextMarker.SetActive(false);
                return;
            }
            Mouse mouse = Mouse.current;
            bool interact = (session.FieldSmith==null||!session.FieldSmith.ClaimsInteract) && (session.Revival == null || !session.Revival.IsHelping) && Keyboard.current != null && Keyboard.current[GameActionBindings.GetKey("Interact")].wasPressedThisFrame;
            bool rightClick = mouse != null && mouse.rightButton.wasPressedThisFrame;
            if (!IsPlacing)
            {
                bool ready = UpdateContextPreview();
                if (ready && (rightClick || interact) && session.Hud != null) session.Hud.ShowBuildMenu();
                return;
            }
            if (contextMarker != null) contextMarker.SetActive(false);
            UpdatePreview();
            if (Time.frameCount <= suppressThroughFrame || (!interact && !rightClick)) return;
            if (!validPreview) return;
            if (session.Defense.LocalBuildTokens <= 0) { Hint = "You have used all three tower tokens."; return; }
            Vector3 requestedPosition = placementPosition;
            int requestedElement = selectedElement;
            CancelPlacement();
            session.Defense.RequestBuild(requestedElement, requestedPosition);
            Hint = "Placement sent to the room host.";
        }

        public void SelectShrine(int element){TrySelectShrine(element);}

        public bool TrySelectShrine(int element)
        {return TrySelectLaneType015(element);}

        private void UpdatePreview()
        {
            if (!TryAimPad(out RaycastHit hit, out int nearestPad))
            {
                ghost.SetActive(false);
                validPreview = false;
                Hint = "Aim at an active raised platform within 25 metres. Escape cancels.";
                return;
            }
            placementPosition = BuildPads[nearestPad];
            ghost.transform.position = placementPosition;
            if(towerModelPreview==null||!towerModelPreview.SetSelectedType(selectedElement)){validPreview=false;ghost.SetActive(false);return;}
            ghost.SetActive(true);
            validPreview = ValidatePlacement(placementPosition, out string reason);
            Color selectedColor = selectedElement >= 0 && selectedElement < TowerColors.Length
                ? TowerColors[selectedElement] : Color.green;
            selectedColor.a = .48f;
            Color tint = validPreview ? selectedColor : new Color(1f, .2f, .12f, .45f);
            ghostMaterial.SetColor("_BaseColor", tint);
            ghostMaterial.SetColor("_Color", tint);
            ghostMaterial.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * .6f);
            Hint = validPreview ? TowerNames[selectedElement] +
                " · " + GameActionBindings.GetLabel("Interact") + " / Right-click places · Escape cancels" : reason + " Escape cancels.";
        }

        private bool UpdateContextPreview()
        {
            Hint = "";hoveredPad=-1;hoveredAvailable=false;
            if (ghost != null) ghost.SetActive(false);
            if (contextMarker != null) contextMarker.SetActive(false);
            if (!TryAimPad(out _, out int pad)) return false;
            hoveredPad=pad;Vector3 position = BuildPads[pad];
            if (!ValidatePlacement(position, out string reason)) { Hint = reason; return false; }
            if (session.Defense.LocalBuildTokens <= 0) { Hint = "All your tower tokens have been used."; return false; }
            contextMarker.transform.position = position + Vector3.up * .035f;
            contextMarker.SetActive(true);
            Color tint = new Color(.2f,1f,.5f,.2f);
            ghostMaterial.SetColor("_BaseColor", tint);
            ghostMaterial.SetColor("_Color", tint);
            ghostMaterial.SetColor("_EmissionColor", new Color(.08f,.4f,.2f));
            Hint = "[" + GameActionBindings.GetLabel("Interact") + " / Right-click] Choose tower";
            hoveredAvailable=true;menuPad=pad;menuPlatforms=session.World.TowerPlatforms;menuRun=session.Defense.ForgeRunId;
            return true;
        }

        private bool TryAimPad(out RaycastHit hit,out int pad)
        {
            hit=default;pad=-1;
            return Available&&session.World.TowerPlatforms.TryPick(session.Player,BuildReach,out pad,out hit)&&session.World.IsBuildPadActive(BuildPads[pad]);
        }

        public bool ValidatePlacement(Vector3 position,out string reason)
        {
            if(session==null||session.Player==null){reason=LaneReasonText015(OdysseyOracle.Foundation.World.LaneBuildReason015.Unavailable);return false;}
            return ValidatePlacementFrom(session.Player.transform.position,position,out reason);
        }

        public bool ValidatePlacementFrom(Vector3 feet,Vector3 position,out string reason)
        {
            reason=LaneReasonText015(OdysseyOracle.Foundation.World.LaneBuildReason015.Unavailable);if(!Available||!ModelsReady)return false;
            int lane=LaneBuildContextValid015?laneLocked015:OdysseyOracle.Foundation.World.LaneBuildMath015.NearestLane(new Unity.Mathematics.float3(feet.x,feet.y,feet.z),session.World.AdmissionLaneMask);
            bool valid=session.World.EvaluateLaneBuild015(position,feet,lane,selectedElement,laneOwned015,false,out var result);reason=LaneReasonText015(result);return valid;
        }

        public void ApplyPlacedShrine(int id,int element,Vector3 position,int acceptedTier=1,int acceptedRevision=0,int acceptedLane=-1)
        {TryApplyPlacedShrine(id,element,position,acceptedTier,acceptedRevision,acceptedLane);}

        public bool TryApplyPlacedShrine(int id,int element,Vector3 position,int acceptedTier=1,int acceptedRevision=0,int acceptedLane=-1)
        {
            if(!ModelsReady||session==null||session.World==null||id<1||
                !TowerForgeDefinitions.ValidType(element)||!MatchTowerForgeRules.ValidTier(acceptedTier)||acceptedRevision<0||acceptedRevision>MatchTowerForgeRules.MaximumRevision)return false;
            if(!CanonicalLanePosition015(position,out Vector3 canonical))return false;
            if(placed.TryGetValue(id,out var existing))
            {
                if(existing==null||existing.Element!=element||(existing.Position-canonical).sqrMagnitude>.001f)return false;
                return existing.ApplyAcceptedModelTier(acceptedTier,acceptedRevision,MatchTowerForgeRules.Interval(acceptedTier));
            }
            if(PlacedCount>=MaximumBuildings)return false;
            foreach(var other in placed.Values)if(other!=null&&!other.IsDestroyed&&XZSquared(other.Position,canonical)<4f)return false;
            return TryInstallLaneTower015(0,id,element,canonical,acceptedTier,acceptedRevision,acceptedLane);
        }

        void UpdatePadHighlights()
        {
            var platforms=session.World.TowerPlatforms;
            if(highlightedPlatforms!=platforms)
            {padHighlights?.Dispose();padHighlights=new TowerPadHighlightBatch(session.World.transform,BuildPads,ghostMaterial);highlightedPlatforms=platforms;}
            for(int i=0;i<4;i++)
            {
                bool occupied=false;foreach(var tower in placed.Values)if(tower!=null&&!tower.IsDestroyed&&XZSquared(tower.Position,BuildPads[i])<4f){occupied=true;break;}
                bool range=session.Player!=null&&(session.Player.transform.position-BuildPads[i]).sqrMagnitude<=BuildReach*BuildReach;
                bool capacity=session.Defense.PlacedCount<session.Defense.ActiveTowerCapacity&&session.Defense.PlacedCount<4;
                padStates[i]=TowerPadPolicy.State(session.World.IsBuildPadActive(BuildPads[i]),occupied,ModelsReady,session.Defense.LocalBuildTokens>0,capacity,range,i==hoveredPad);
            }
            padHighlights.Apply(padStates,true);
        }

        public void CancelPlacement()
        {
            CloseLaneContext015();
            session?.Hud?.HideBuildMenu();
            IsPlacing = validPreview = false;hoveredPad=menuPad=-1;hoveredAvailable=false;menuPlatforms=null;menuRun=string.Empty;
            Hint = "";
            if (ghost != null) ghost.SetActive(false);
            if (contextMarker != null) contextMarker.SetActive(false);
        }

        public bool RemovePlacedShrine(int id)
        {
            if (!placed.TryGetValue(id, out PlacedElementShrine shrine)) return false;
            RemoveWorkshop015(id);placed.Remove(id);
            if (shrine != null) { shrine.gameObject.SetActive(false); Destroy(shrine.gameObject); }
            return true;
        }

        public void ResetBuildings()
        {
            ClearWorkshop015();CancelPlacement();
            ReleaseTutorialPracticeTower(tutorialPracticeTower);
            foreach (PlacedElementShrine shrine in placed.Values)
                if (shrine != null) { shrine.gameObject.SetActive(false); Destroy(shrine.gameObject); }
            placed.Clear();
        }

        public void ClearPlacedShrines() { ResetBuildings(); }

        public float SupportDamageMultiplierAt(Vector3 position)
        {
            if (!Finite(position)) return 1f;
            foreach (PlacedElementShrine shrine in placed.Values)
                if (shrine != null && shrine.IsSupport && shrine.gameObject.activeInHierarchy &&
                    (shrine.Position - position).sqrMagnitude <= 8f * 8f) return 1.2f;
            return 1f;
        }

        private int ClosestPad(Vector3 position, out float squared)
        {
            int closest = -1;
            squared = float.PositiveInfinity;
            for (int i = 0; i < BuildPads.Length; i++)
            {
                float distance = XZSquared(position, BuildPads[i]);
                if (distance >= squared) continue;
                squared = distance;
                closest = i;
            }
            return closest;
        }

        private static float XZSquared(Vector3 a, Vector3 b)
        { float x = a.x - b.x, z = a.z - b.z; return x * x + z * z; }
        private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
            !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);

        private static Material MakeMaterial(string name, Color color, float glow)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", .24f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * glow);
            return material;
        }

        private void OnDestroy()
        {
            ResetBuildings();DisposeWorkshop015();DisposeLaneBuilding015();padHighlights?.Dispose();padHighlights=null;
            towerModelLibrary?.Dispose();towerModelLibrary=null;
            if (ghost != null) Destroy(ghost);
            if (contextMarker != null) Destroy(contextMarker);
            for (int i = 0; i < ghostMeshes.Count; i++) if (ghostMeshes[i] != null) Destroy(ghostMeshes[i]);
            if (green != null) Destroy(green);
            if (gold != null) Destroy(gold);
            if (ghostMaterial != null) Destroy(ghostMaterial);
            for (int i = 0; i < elementMaterials.Length; i++) if (elementMaterials[i] != null) Destroy(elementMaterials[i]);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Bounded offline tutorial adapter; existing actor/HP/tower/native owners and reward boundaries remain.
// # ARCHITECTURE SNAPSHOT
// Existing Y-up world/tower anchors, real native enemy HP and sole shot/cooldown owners remain unchanged.
// One separate tutorial tower field never enters player placed counts; reset/destruction release before model-library disposal.
// No rewards, quest completion, save state, currency or worker/native ownership changes.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up/XZ, exact1m terrain/.25m build roots and locked active-road context.
// Array Formats: Bounded native semantic/preview/body stores, exact64B versioned command and existing2peer/4tower snapshots.
// Dependencies/Pointers: Root integrates exact guarded slices; host economy/readiness/LOS and World geometry remain canonical, prepared/source compile is not native or browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted metre/Y-up/XZ support roots and HUD Canvas; cosmetic floor boxes do not establish build permission.
// Array Formats: Existing fixed8192 preview records, one ray job, four cached original tower choices, unchanged Frame64 pending/replay transaction.
// Dependencies/Pointers: Local picker owns no pause/cursor/economy; accepted host result alone closes successful transaction. Normal input/native/Web rendering remain separate acceptance gates.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted global waves1..40, tiers1..3 and existing Y-up/XZ tower roots; exact bound lesson/lab adapters remain separate.
// Array Formats: Existing fixed4 towers and unchanged host snapshots plus cached scalar policy tier/wave/maximum; no ABI, save, cost, damage, enemy-aggression or hot allocation change.
// Dependencies/Pointers: CampaignBalance015 owns target groups/capacity/additive4percent rate and durability math. Host alone accepts context; monotonic policy rebases preserve damaged fraction, repeated contexts are inert, and dead towers stay dead.
