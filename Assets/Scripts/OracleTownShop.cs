using System;
using UnityEngine;
using UnityEngine.InputSystem;
using OdysseyOracle.NextRelease;

namespace OdysseyOracle
{
    /// <summary>
    /// Local-device town progression. PlayerPrefs is an unencrypted convenience
    /// save, not a trusted account, network wallet or cross-device economy.
    /// Rewards are granted only by an explicit accepted-pickup caller.
    /// Permanent and run masks stay separate; consumers combine them once by OR.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OracleTownShop : MonoBehaviour
    {
        const string SaveKey="OdysseyOracle.TownProgression.v1";
        const string BackupKey="OdysseyOracle.TownProgression.v1.backup";
        const int SaveVersion=1,MaxSaveLength=16384;
        const float ShopReach=3.25f,TargetReach=3.65f;
        static readonly Vector3 SmithPosition=new Vector3(6.8f,0f,-3.6f);
        static readonly string[] Names={"Fire","Water","Earth","Air"};
        static readonly Color[] Colors={new Color(1f,.36f,.12f),new Color(.18f,.68f,1f),
            new Color(.62f,.78f,.29f),new Color(.66f,.91f,1f)};
        static readonly string[,] PassiveNames={
            {"Kindle","Living Flame","Solar Focus"},{"Flow","Returning Tide","Deep Current"},
            {"Heavy Focus","Steady Channel","Stone Tempo"},{"Light Step","Tailwind","Skystride"}
        };

        [Serializable]
        sealed class SaveEnvelope
        {
            public int version;
            public int revision;
            public string payload;
            public string checksum;
        }

        GameSession session;
        HeartstoneProgressionModel model=new HeartstoneProgressionModel();
        NpcBlacksmith smith;
        GameObject smithRoot;
        RectTransform uiRoot;
        BlacksmithShopPanel panel;
        string lastGoodSave="";
        string saveStatus="Progress stays on this device.";
        bool initialized,saveBlocked,modalOwnsPause,mutating;
        int revision,lastInteractFrame=-1,openedFrame=-1,sightFrame=-1;
        bool cachedSight;

        public bool Visible => panel!=null && panel.Visible;
        public int Heartcrystals => model.CrystalWallet;
        public int PermanentPassiveMask => model.PassiveMask;
        public NpcBlacksmith Blacksmith => smith;
        public string SaveStatus => saveStatus;
        public string HighlightedAbility {get;private set;}="";
        public HeartstoneProgressionSnapshot LocalSnapshot => model.Snapshot();
        public event Action ProgressionChanged;

        public string InteractionHint => CanOpen() && IsLookingAtSmith()
            ? "Blacksmith ["+GameActionBindings.GetLabel("Interact")+"] Shop" : "";

        /// <summary>Initialize once after the existing Hud, Books and Opening.</summary>
        public void Initialize(GameSession owner)
        {
            if(initialized)return;
            if(owner==null || owner.Player==null || owner.Hud==null)
                throw new ArgumentException("Initialize the town shop after Player and Hud.",nameof(owner));
            Canvas canvas=owner.Hud.GetComponentInChildren<Canvas>(true);
            if(canvas==null)throw new InvalidOperationException("The town shop needs the existing HUD Canvas.");
            session=owner;LoadLocal();
            smithRoot=new GameObject("Town blacksmith — original art");
            smithRoot.transform.SetParent(transform,false);
            smithRoot.transform.SetPositionAndRotation(SmithPosition,Quaternion.identity);
            smith=smithRoot.AddComponent<NpcBlacksmith>();smith.Initialize(owner.Player);
            uiRoot=new GameObject("TownBlacksmithUI",typeof(RectTransform)).GetComponent<RectTransform>();
            uiRoot.SetParent(canvas.transform,false);uiRoot.anchorMin=Vector2.zero;uiRoot.anchorMax=Vector2.one;
            uiRoot.offsetMin=uiRoot.offsetMax=Vector2.zero;
            panel=uiRoot.gameObject.AddComponent<BlacksmithShopPanel>();
            panel.Initialize(uiRoot,new BlacksmithShopPanel.Bindings {
                WandNames=Names,WandColors=Colors,CurrencyName="Heartcrystals",
                Wallet=()=>Heartcrystals,WandLevel=modelLevel,WandUpgradeCost=modelCost,
                CanBuyWand=e=>CanTransact() && model.CanBuyWand(e,true),
                TryBuyWand=(e,ignored)=>TryPurchaseWand(e),
                CurrentWandEffect=CurrentWandEffect,NextWandEffect=NextWandEffect,
                IsNearBlacksmith=CanBrowseShop,IsLookingAtBlacksmith=IsLookingAtSmith,
                InteractKeyLabel=()=>GameActionBindings.GetLabel("Interact"),
                IsPaused=()=>session.Paused,SetPaused=SetModalPause,Closed=()=>modalOwnsPause=false,
                PassiveName=(e,n)=>ValidNode(e,n)?PassiveNames[e,n]:"Unknown training",
                PassiveDescription=PassiveDescription,
                PassiveCost=(e,n)=>HeartstoneProgressionModel.GetPassiveUnlockCost(n),
                PassiveUnlocked=(e,n)=>model.IsPassiveUnlocked(e,n),
                CanUnlockPassive=(e,n)=>CanTransact() && model.CanUnlockPassive(e,n,true),
                TryUnlockPassive=(e,n,ignored)=>TryPurchasePassive(e,n),
                BranchHighlights=new[]{"Permanent damage training","Permanent water control",
                    "Permanent focus training","Permanent movement training"},
                TrainingNote=()=>"Permanent training improves solo adventures; shared defense keeps its separate loadout.\n"+saveStatus,
                DrawHoverHint=false
            });
            initialized=true;UpdateVisibility();
        }

        void Update()
        {
            if(!initialized || session==null)return;
            UpdateVisibility();
            if(Visible)
            {
                if(!TownContext() || !session.Paused || !InRange() || !HasLineOfSight())
                {panel.Hide(false);modalOwnsPause=false;return;}
                if(Time.frameCount>openedFrame && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    // Browser pointer lock resumes through the existing visible
                    // pause-menu Resume button, rather than an Escape relock.
                    panel.Hide(false);modalOwnsPause=false;session.Pause();
                }
                return;
            }
            if(CanOpen() && Keyboard.current!=null &&
                Keyboard.current[GameActionBindings.GetKey("Interact")].wasPressedThisFrame)TryInteract();
        }

        /// <summary>Bound-key polling is already handled here; no second E consumer is required.</summary>
        public bool TryInteract()
        {
            if(Time.frameCount==lastInteractFrame || !CanOpen() || !IsLookingAtSmith())return false;
            lastInteractFrame=Time.frameCount;openedFrame=Time.frameCount;
            uiRoot.SetAsLastSibling();panel.ShowShop();
            if(Visible){smith.NotifyAcceptedTalk009();session.RefugeActivities?.TryPlayNpcGreeting009(1,this,3);session.Audio?.PlayUi();}
            return Visible;
        }

        /// <summary>Only an accepted reward/pickup consumer calls this, once per claim.</summary>
        public bool AddHeartcrystals(int amount)
        {
            if(!initialized || amount<=0)return false;
            return Commit(candidate=>candidate.GrantCrystal(amount));
        }

        /// <summary>Only the caller's explicitly confirmed device-progression reset uses this.</summary>
        public bool ResetSavedProgression()
        {
            if(!initialized)return false;
            bool accepted=Commit(candidate=>true,true);
            if(accepted)
            {
                HighlightedAbility="";
                OracleProgressPrefs015.DeleteKey(ElementProgression.TutorialXpClaimKey);
                OracleProgressPrefs015.DeleteKey(ElementProgression.TutorialHeartstoneClaimKey);
                OracleProgressPrefs015.Save();
            }
            return accepted;
        }

        public float WandDamageMultiplier(int element) => element>=0 && element<4
            ? model.GetWandDamageMultiplier(element):1f;
        public int WandLevel(int element) => element>=0 && element<4?model.GetWandLevel(element):1;
        public bool IsPassiveUnlocked(int element,int node) => model.IsPassiveUnlocked(element,node);
        public int GetCombinedPassiveMask(int runMask) => (runMask|model.PassiveMask)&4095;

        // Cosmetics follow Commit; a feedback failure never changes a successful saved transaction.
        void NotifyCommittedElementUpgrade011(int element)
        {
            try{panel?.ShowAcceptedElementUpgrade011(element);}catch(Exception){ }
            try{if(session.Audio!=null&&!session.Audio.TryPlayElementUpgrade011(element))session.Audio.PlayUi();}catch(Exception){ }
        }

        public bool TryPurchaseWand(int element)
        {
            if(!CanTransact() || !CheckLineOfSight())return false;
            bool accepted=Commit(candidate=>candidate.TryBuyWand(element,true));
            if(accepted){HighlightedAbility=Names[element]+" wand level "+model.GetWandLevel(element);NotifyCommittedElementUpgrade011(element);}
            return accepted;
        }
        public bool TryPurchasePassive(int element,int node)
        {
            if(!CanTransact() || !CheckLineOfSight())return false;
            bool accepted=Commit(candidate=>candidate.TryUnlockPassive(element,node,true));
            if(accepted){HighlightedAbility=PassiveNames[element,node];NotifyCommittedElementUpgrade011(element);}
            return accepted;
        }

        bool TownContext() => initialized && session!=null && session.HasStarted &&
            (session.FieldSmith==null||!session.FieldSmith.CampaignSaveFence) &&
            !session.IsDead && !session.IsDowned && !session.Victory && session.World!=null &&
            session.World.Mode020==WorldMode020.TutorialRefuge && (session.Defense==null || !session.Defense.Running) &&
            (session.Opening==null || (!session.Opening.IsTransitioning && !session.Opening.IsIntroPlaying));
        bool CanOpen() => TownContext() && !Visible && session.IsPlaying &&
            (session.FieldSmith==null||!session.FieldSmith.ClaimsAnvilInteraction) &&
            Cursor.lockState==CursorLockMode.Locked && !AbilityBindings.IsRebinding && !GameActionBindings.IsRebinding &&
            (session.Builder==null || !session.Builder.IsPlacing) && InRange();
        bool CanTransact() => !saveBlocked && !mutating && CanBrowseShop();
        bool CanBrowseShop() => TownContext() &&
            (session.IsPlaying || (Visible && modalOwnsPause && session.Paused)) &&
            (session.Builder==null || !session.Builder.IsPlacing) && InRange() && HasLineOfSight();
        bool InRange()
        {
            if(smith==null || session.Player==null || !smithRoot.activeInHierarchy)return false;
            Vector3 delta=session.Player.transform.position-smith.transform.position;
            return Mathf.Abs(delta.y)<1.5f && delta.x*delta.x+delta.z*delta.z<=ShopReach*ShopReach;
        }
        bool IsLookingAtSmith()
        {
            if(!InRange() || session.Player.ViewCamera==null)return false;
            Transform eye=session.Player.ViewCamera.transform;
            return Physics.Raycast(eye.position,eye.forward,out RaycastHit hit,TargetReach,~(1<<2),QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<NpcBlacksmith>()==smith;
        }
        bool HasLineOfSight()
        {
            if(sightFrame==Time.frameCount)return cachedSight;
            sightFrame=Time.frameCount;cachedSight=CheckLineOfSight();return cachedSight;
        }
        bool CheckLineOfSight()
        {
            if(!InRange() || session.Player.ViewCamera==null)return false;
            Vector3 eye=session.Player.ViewCamera.transform.position,to=smith.AimPoint-eye;
            return to.sqrMagnitude>.0001f && Physics.Raycast(eye,to.normalized,out RaycastHit hit,to.magnitude+.08f,
                ~(1<<2),QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<NpcBlacksmith>()==smith;
        }
        void UpdateVisibility()
        {
            bool town=session.World!=null && session.World.Mode020==WorldMode020.TutorialRefuge;
            if(smithRoot!=null && smithRoot.activeSelf!=town)smithRoot.SetActive(town);
        }
        void SetModalPause(bool paused)
        {
            if(paused){modalOwnsPause=true;session.Pause();}
            else
            {
                modalOwnsPause=false;
                if(TownContext() && session.Paused)session.Resume();
            }
        }

        int modelLevel(int e) => model.GetWandLevel(e);
        int modelCost(int e) => model.GetWandUpgradeCost(e);
        string CurrentWandEffect(int e) => "Wand damage +"+Mathf.RoundToInt((WandDamageMultiplier(e)-1f)*100f)+"%.";
        string NextWandEffect(int e)
        {
            int next=Mathf.Min(5,model.GetWandLevel(e)+1);
            return "Wand damage +"+Mathf.RoundToInt((next-1)*15f)+"%.";
        }
        static bool ValidNode(int e,int n) => e>=0 && e<4 && n>=0 && n<3;
        static string PassiveDescription(int e,int n)
        {
            if(!ValidNode(e,n))return "";
            string effect=e==0?"All damage +"+(n==0?"10":n==1?"12":"15")+"%."
                :e==1?"Water/Mud/Water+Air slows +0.20 s per node. Slow stays 50%; duration caps 2.0/2.8/2.0 s."
                :e==2?n==0?"All damage +6%.":n==1?"Earth/Mud/Fire+Earth interrupts +0.10 s; later hits gain 25% during Earth interrupt.":"All damage +10%; Earth interrupts another +0.10 s (Earth hit/Mud/Fire+Earth effect caps 0.55/0.75/0.40 s)."
                :"Movement +"+(n==0?"6":n==1?"8":"10")+"%.";
            return effect+"\n"+Names[e]+" +8% damage. Recovery stays 4 s.\nPermanent; same run node won't stack.";
        }

        bool Commit(Func<HeartstoneProgressionModel,bool> mutation,bool reset=false)
        {
            if((saveBlocked && !reset) || mutating || (revision==int.MaxValue && !reset))return false;
            mutating=true;
            try
            {
                var candidate=new HeartstoneProgressionModel();
                if((!reset && !candidate.Restore(model.Snapshot())) || !mutation(candidate))return false;
                string payload=JsonUtility.ToJson(candidate.Snapshot());
                var envelope=new SaveEnvelope{version=SaveVersion,revision=reset?1:revision+1,payload=payload};
                envelope.checksum=Checksum(envelope.revision,payload);
                string serialized=JsonUtility.ToJson(envelope);
                if(serialized.Length>MaxSaveLength)return false;
                string oldPrimary=OracleProgressPrefs015.GetString(SaveKey,""),oldBackup=OracleProgressPrefs015.GetString(BackupKey,"");
                try
                {
                    if(reset)OracleProgressPrefs015.SetString(BackupKey,serialized);
                    else if(!string.IsNullOrEmpty(lastGoodSave))OracleProgressPrefs015.SetString(BackupKey,lastGoodSave);
                    OracleProgressPrefs015.SetString(SaveKey,serialized);OracleProgressPrefs015.Save();
                }
                catch(Exception)
                {
                    // Keep the live model unchanged; restore in-memory preferences.
                    try{RestorePreference(SaveKey,oldPrimary);RestorePreference(BackupKey,oldBackup);}
                    catch(Exception){saveBlocked=true;}
                    saveStatus="Could not store progress. No Heartcrystals were spent.";
                    return false;
                }
                model=candidate;revision=envelope.revision;lastGoodSave=serialized;
                saveBlocked=false;
                saveStatus=reset?"Saved town progression reset on this device.":"Progress stays on this device.";
            }
            catch(Exception)
            {
                saveStatus="Could not store progress. No Heartcrystals were spent.";return false;
            }
            finally{mutating=false;}
            // A consumer failure must not turn an already committed reward into
            // a reported failure and cause a pickup caller to award it twice.
            var listeners=ProgressionChanged;
            if(listeners!=null)foreach(Action listener in listeners.GetInvocationList())
                try{listener();}catch(Exception){Debug.LogWarning("A town progression listener failed after the local save completed.",this);}
            return true;
        }
        void LoadLocal()
        {
            string primary,backup;
            try{primary=OracleProgressPrefs015.GetString(SaveKey,"");backup=OracleProgressPrefs015.GetString(BackupKey,"");}
            catch(Exception)
            {
                saveBlocked=true;saveStatus="Local storage is unavailable. Purchases are unavailable; existing records were kept.";return;
            }
            if(string.IsNullOrEmpty(primary) && string.IsNullOrEmpty(backup))return;
            if(TryRead(primary,out var loaded,out int loadedRevision))
            {model=loaded;revision=loadedRevision;lastGoodSave=primary;return;}
            if(TryRead(backup,out loaded,out loadedRevision))
            {
                model=loaded;revision=loadedRevision;lastGoodSave=backup;
                saveStatus="Recovered the previous local progress record.";return;
            }
            saveBlocked=true;saveStatus="Local progress could not be read. Existing records were kept; purchases are unavailable.";
            Debug.LogWarning("Town progression has no valid local save or backup; original records were preserved.",this);
        }
        static bool TryRead(string serialized,out HeartstoneProgressionModel loaded,out int loadedRevision)
        {
            loaded=null;loadedRevision=0;
            if(string.IsNullOrEmpty(serialized) || serialized.Length>MaxSaveLength)return false;
            try
            {
                var envelope=JsonUtility.FromJson<SaveEnvelope>(serialized);
                if(envelope==null || envelope.version!=SaveVersion || envelope.revision<1 ||
                    string.IsNullOrEmpty(envelope.payload) || envelope.payload.Length>MaxSaveLength ||
                    envelope.checksum!=Checksum(envelope.revision,envelope.payload))return false;
                var candidate=new HeartstoneProgressionModel();
                if(!candidate.Restore(JsonUtility.FromJson<HeartstoneProgressionSnapshot>(envelope.payload)))return false;
                loaded=candidate;loadedRevision=envelope.revision;return true;
            }
            catch(Exception){return false;}
        }
        static string Checksum(int revision,string payload)
        {
            // Accidental corruption check only; this is deliberately not an auth signature.
            uint value=2166136261u;
            string input=revision.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+payload;
            foreach(char c in input){value=unchecked((value^(byte)c)*16777619u);value=unchecked((value^(byte)(c>>8))*16777619u);}
            return value.ToString("x8",System.Globalization.CultureInfo.InvariantCulture);
        }
        static void RestorePreference(string key,string oldValue)
        {
            if(string.IsNullOrEmpty(oldValue))OracleProgressPrefs015.DeleteKey(key);else OracleProgressPrefs015.SetString(key,oldValue);
        }
        void OnDisable()
        {
            if(Visible)panel.Hide(false);
            modalOwnsPause=false;if(smithRoot!=null)smithRoot.SetActive(false);
        }
        void OnDestroy()
        {
            if(uiRoot!=null)Destroy(uiRoot.gameObject);
            if(smithRoot!=null)Destroy(smithRoot);
            ProgressionChanged=null;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.
// # ARCHITECTURE SNAPSHOT
// World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Bounded cached collider/root references only; no new economy/native buffers or quest bits.
// Root owns Unity integration and native acceptance; this is a guarded work candidate.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted elemental shop/UI/listener or cosmetic atlas space; no authority or saved-key changes.
// Array Formats: Four bounded clip/image identities and accepted-element bits; existing12models/fourcuevoices and workshop mesh.
// Dependencies/Pointers: Root follow-up imports four retained generated cues; baseline/replay/request failure stays silent, no hot loads/new audio sources or media/provider work here.
