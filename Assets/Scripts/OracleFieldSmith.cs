using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Field presentation/interaction owner, never an economy authority. One original
    // Smith belongs to the actual arena/run; one existing HUD menu reads a match-only
    // adapter. Range/LOS and visible context are local gates; Director independently
    // validates host pose/run/requests. No town-save fallback, rewards or new E poll
    // in HUD. World provides the reserved site; no old six/24-pad coordinates.
    public sealed partial class OracleFieldSmith : MonoBehaviour
    {
        const float ShopReach=3.25f,LookReach=3.65f;
        GameSession session;
        GameObject actorRoot;
        NpcBlacksmith smith;
        ITowerForgeModeAdapter adapter;
        string preparedRun="",failedRun="",autoRun="",hint="";
        bool initialized,wasVisible,autoOwned,pendingAutoRestore,stationConfigured;
        int openedFrame=-1,sightFrame=-1,lookFrame=-1,bindingFrame=-1;
        bool cachedSight,cachedLook;
        float nextPrepare,nextRefresh;
        public bool Visible=>session!=null&&session.Hud!=null&&session.Hud.IsFieldTowerForgeVisible;
        public string Status {get;private set;}="Field forge is available during a connected defense.";
        public ITowerForgeModeAdapter ForgeAdapter=>adapter;
        public bool StateReady=>ForgeActive&&session.Defense.ForgeStateReady;
        public MatchForgeOutcome LastOutcome=>session!=null&&session.Defense!=null?session.Defense.LastForgeOutcome:MatchForgeOutcome.None;
        public bool ModelsReady=>session!=null&&session.Builder!=null&&session.Builder.ModelsReady;
        public string ModelStatus=>session!=null&&session.Builder!=null?session.Builder.ModelStatus:"Tower models are unavailable.";
        public bool ClaimsInteract=>CanOpen&&LookingAtSmith()||CanOpenAnvilGuidance;
        public string InteractionHint
        {
            get
            {
                if(!ClaimsInteract)return "";
                if(bindingFrame!=AbilityBindings.LastChangeFrame||string.IsNullOrEmpty(hint))
                {bindingFrame=AbilityBindings.LastChangeFrame;hint="Tower Anvil ["+GameActionBindings.GetLabel("Interact")+"] Forge & guide";}
                return hint;
            }
        }
        bool RunActive=>initialized&&session!=null&&session.HasStarted&&session.CoopMode&&session.World!=null&&session.World.IsCoopArena&&
            session.Rooms!=null&&session.Rooms.Connected&&session.Defense!=null&&session.Defense.Running&&session.Defense.ArenaReady&&
            !string.IsNullOrEmpty(session.Defense.ForgeRunId);
        bool LocalEligible=>ForgeActive&&preparedRun==session.Defense.ForgeRunId&&smith!=null&&!session.IsDead&&!session.IsDowned&&!session.Victory&&
            !AbilityBindings.IsRebinding&&(session.Revival==null||!session.Revival.IsHelping)&&
            (session.Builder==null||!session.Builder.IsPlacing);
        bool CanOpen=>LocalEligible&&!Visible&&Application.isFocused&&session.IsPlaying&&Cursor.lockState==CursorLockMode.Locked&&
            (session.Shop==null||!session.Shop.Visible)&&InRange();
        public bool CanTransact()=>UpgradesAvailableHere&&LocalEligible&&RefugeVisitAccepted&&Visible&&Application.isFocused&&session.Paused&&StateReady&&ModelsReady&&InRange()&&HasLineOfSight();

        public void Initialize(GameSession owner)
        {
            if(initialized)throw new InvalidOperationException("Initialize a fresh field-Smith owner once.");
            if(owner==null||owner.Hud==null||owner.Player==null||owner.Defense==null||owner.Builder==null)
                throw new ArgumentException("Initialize the field Smith after Player, HUD, Director and Builder.");
            session=owner;owner.Hud.InitializeFieldTowerForge();initialized=true;
        }
        void Update()
        {
            if(!initialized||session==null)return;
            RefreshCampaignSaveFence();
            if(!ForgeActive)
            {
                if(actorRoot!=null||adapter!=null||Visible)RetireRun();
                pendingAutoRestore=autoOwned=false;failedRun="";TickAnvilGuidance();return;
            }
            if(preparedRun!=session.Defense.ForgeRunId||preparedInRefuge!=InCampaignRefuge)
            {
                if(actorRoot!=null||adapter!=null)RetireRun();
                if(failedRun==session.Defense.ForgeRunId)return;
                if(Time.unscaledTime>=nextPrepare){nextPrepare=Time.unscaledTime+.5f;PrepareRun();}
                return;
            }
            if(pendingAutoRestore&&Application.isFocused&&session.IsPlaying)RestoreAuto();
            if(Visible)
            {
                wasVisible=true;
                if(!LocalEligible||!session.Paused||!InRange()||!HasLineOfSight()){Close(false);return;}
                if(Time.frameCount>openedFrame&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){Close(false);return;}
                if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.15f;session.Hud.RefreshFieldTowerForge();}
                return;
            }
            if(wasVisible){wasVisible=false;QueueAutoRestore();}
            if(ClaimsInteract&&Keyboard.current!=null&&Keyboard.current[GameActionBindings.GetKey("Interact")].wasPressedThisFrame)TryInteract();
        }
        void PrepareRun()
        {
            if(InCampaignRefuge){PrepareRefugeForge();return;}
            Vector3 site=session.World.MatchForgePosition;
            if(!Finite(site.x)||!Finite(site.y)||!Finite(site.z)||!Finite(session.World.MatchForgeYaw))
            {Status="Field forge site is invalid; no station was created.";failedRun=session.Defense.ForgeRunId;return;}
            if(!Physics.Raycast(site+Vector3.up*2f,Vector3.down,out RaycastHit floor,4f,~(1<<2),QueryTriggerInteraction.Ignore)||
                floor.normal.y<.9f||Mathf.Abs(floor.point.y-site.y)>.2f)
            {Status="Field forge needs its actual walkable floor; station setup is waiting.";return;}
            actorRoot=new GameObject("Defense Smith — original shared NPC recipe");
            actorRoot.transform.SetParent(transform,false);
            actorRoot.transform.SetPositionAndRotation(new Vector3(site.x,floor.point.y,site.z),Quaternion.Euler(0,session.World.MatchForgeYaw,0));
            smith=actorRoot.AddComponent<NpcBlacksmith>();smith.BuildWorkstation=true;smith.Initialize(session.Player);
            session.Defense.ConfigureMatchForgeStation(smith);stationConfigured=true;
            adapter=session.Defense.CreateMatchForgeAdapter(CanTransact);
            if(adapter==null||adapter.Mode!=TowerForgeMode.CoopMatch)
            {Status="Field forge requires a host-accepted match adapter.";RetireRun();failedRun=session.Defense.ForgeRunId;return;}
            preparedRun=session.Defense.ForgeRunId;sightFrame=lookFrame=-1;
            Status="Forge charge is match-only. Tier-one blueprints are included; upgrades are optional.";
        }
        public bool TryInteract()
        {
            if(!ForgeActive)return TryOpenAnvilGuidance();
            if(Time.frameCount==openedFrame||!ClaimsInteract||adapter==null)return false;
            bool priorAuto=session.Combat!=null&&session.Combat.AutoFireEnabled;
            if(!session.Hud.ShowFieldTowerForge())return false;
            openedFrame=Time.frameCount;wasVisible=true;autoRun=preparedRun;pendingAutoRestore=false;autoOwned=priorAuto;
            if(priorAuto)session.Combat.SetAutoFire(false);
            RecordRefugeSmithVisit();
            session.Audio?.PlayUi();return true;
        }
        public TowerForgeSubmission Submit(int type)=>CanTransact()&&adapter!=null&&!adapter.Pending&&TowerForgeDefinitions.ValidType(type)
            ?adapter.Submit(type):TowerForgeSubmission.Rejected;
        public void Close(bool resumeFromGesture=false)
        {
            if(session!=null&&session.Hud!=null)session.Hud.HideFieldTowerForge();
            wasVisible=false;QueueAutoRestore();
            if(resumeFromGesture&&LocalEligible&&session.Paused)
            {
                session.Resume();
                if(session.IsPlaying&&Application.isFocused)RestoreAuto();
            }
        }
        // An explicit Settings edit wins over deferred restoration after Escape.
        public void ForgetAutoRestore(){autoOwned=pendingAutoRestore=false;autoRun="";}
        void QueueAutoRestore()
        {
            if(!autoOwned)return;
            pendingAutoRestore=RunActive&&autoRun==session.Defense.ForgeRunId;
            if(!pendingAutoRestore)ForgetAutoRestore();
        }
        void RestoreAuto()
        {
            bool restore=autoOwned&&RunActive&&autoRun==session.Defense.ForgeRunId;
            ForgetAutoRestore();if(restore&&session.Combat!=null)session.Combat.SetAutoFire(true);
        }
        bool InRange()
        {
            if(smith==null||session==null||session.Player==null||!smith.gameObject.activeInHierarchy)return false;
            Vector3 delta=session.Player.transform.position-(InCampaignRefuge?smith.TowerAnvilFeet:smith.transform.position);
            return Mathf.Abs(delta.y)<1.5f&&delta.x*delta.x+delta.z*delta.z<=ShopReach*ShopReach;
        }
        bool LookingAtSmith()
        {
            if(lookFrame==Time.frameCount)return cachedLook;lookFrame=Time.frameCount;cachedLook=false;
            if(!InRange()||session.Player.ViewCamera==null)return false;
            Transform eye=session.Player.ViewCamera.transform;
            cachedLook=Physics.Raycast(eye.position,eye.forward,out RaycastHit hit,LookReach,~(1<<2),QueryTriggerInteraction.Ignore)&&
                (InCampaignRefuge?smith.OwnsTowerAnvilCollider(hit.collider):hit.collider.GetComponentInParent<NpcBlacksmith>()==smith);return cachedLook;
        }
        bool HasLineOfSight()
        {
            if(sightFrame==Time.frameCount)return cachedSight;sightFrame=Time.frameCount;cachedSight=false;
            if(!InRange()||session.Player.ViewCamera==null)return false;
            Vector3 eye=session.Player.ViewCamera.transform.position,to=(InCampaignRefuge?smith.TowerAnvilAimPoint:smith.AimPoint)-eye;
            cachedSight=to.sqrMagnitude>.0001f&&Physics.Raycast(eye,to.normalized,out RaycastHit hit,to.magnitude+.08f,~(1<<2),QueryTriggerInteraction.Ignore)&&
                (InCampaignRefuge?smith.OwnsTowerAnvilCollider(hit.collider):hit.collider.GetComponentInParent<NpcBlacksmith>()==smith);return cachedSight;
        }
        void RetireRun()
        {
            if(session!=null&&session.Hud!=null)session.Hud.HideFieldTowerForge();
            if(stationConfigured&&session!=null&&session.Defense!=null)session.Defense.ConfigureMatchForgeStation(null);
            stationConfigured=false;
            if(actorRoot!=null){actorRoot.SetActive(false);Destroy(actorRoot);}
            actorRoot=null;smith=null;adapter=null;preparedRun="";preparedInRefuge=false;wasVisible=false;ForgetAutoRestore();
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        void OnDisable(){HideAnvilGuidance();if(initialized)RetireRun();}
        void OnDestroy(){if(initialized)RetireRun();initialized=false;session=null;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Unity metres/Y-up/XZ; presentation borrows the town Smith and saved models.
// Same host-owned per-peer Forge account spans campaign stages; no local-save writes.
// Existing bounded four-tower UI arrays and Director/native ownership remain unchanged.

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.
// # ARCHITECTURE SNAPSHOT
// World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Bounded cached collider/root references only; no new economy/native buffers or quest bits.
// Root owns Unity integration and native acceptance; this is a guarded work candidate.
