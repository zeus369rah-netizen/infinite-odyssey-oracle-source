using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Two saved ORIGINAL visual instances, one interaction/idle owner.010 authority
    // receives actual roots and visit requests; no quest bits/currency/HP/points are
    // written. Neutral assets are borrowed unchanged and never destroyed by runtime.
    public sealed class RefugeNpcRuntimeOwner : MonoBehaviour
    {
        const float Reach=3.25f,LookReach=3.65f;
        sealed class Actor
        {
            public int Role;public GameObject Root;public CapsuleCollider Body;
            public Transform Feet,Interact,Voice,Torso,Head,Left,Right,Lids;
            public Vector3 TorsoRest,LidsRest;public Quaternion HeadRest,LeftRest,RightRest;
            public float Age,Blink=-1,NextBlink=2.5f,Yaw,Pitch;
        }
        readonly Actor[] actors=new Actor[2];
        readonly Vector3[] sites=new Vector3[2];readonly float[] yaws=new float[2];
        readonly Collider[] overlap=new Collider[24];
        GameSession session;RefugeNpcAssetCatalog catalog;
        IRefugeNpcQuestBridge bridge;UnityEngine.Object bridgeLifetime;
        RefugeNpcQuestView quest;
        GameObject visualRoot;
        bool initialized,catalogFailed,catalogValidated,questDirty=true,wasSmithVisible;
        int lookFrame=-1,lookRole,openedFrame=-1,conversationRole,bindingFrame=int.MinValue;
        float retryAt;string ilyraHint="",bramHint="",objectives="";
        public string Status {get;private set;}="Original refuge NPCs are not initialized.";
        public string NextObjectives=>objectives;
        public bool ClaimsInteract=>CanInteract&&LookTarget()!=0;
        public string InteractionHint
        {
            get
            {
                if(!CanInteract)return "";int role=LookTarget();if(role==0)return "";
                if(bindingFrame!=AbilityBindings.LastChangeFrame)
                {
                    bindingFrame=AbilityBindings.LastChangeFrame;
                    string key=GameActionBindings.GetLabel("Interact");
                    ilyraHint="["+key+" / Right-click] Ilyra · Optional lore";
                    bramHint="["+key+" / Right-click] Bram · Optional lore";
                }
                return role==2?ilyraHint:bramHint;
            }
        }
        bool RefugeWorld=>session!=null&&session.World!=null&&session.World.IsTutorialRefuge&&!session.World.IsCoopArena;
        bool TutorialOwnsInput=>session.Tutorial!=null&&!session.Tutorial.Finished&&!string.IsNullOrEmpty(session.Tutorial.Heading);
        bool CanInteract=>initialized&&RefugeWorld&&session.HasStarted&&session.IsPlaying&&Application.isFocused&&
            Cursor.lockState==CursorLockMode.Locked&&!session.IsDowned&&!session.IsDead&&!session.Victory&&
            !AbilityBindings.IsRebinding&&!TutorialOwnsInput&&(session.Revival==null||!session.Revival.IsHelping)&&
            (session.Shop==null||!session.Shop.Visible)&&visualRoot!=null&&visualRoot.activeInHierarchy;

        public void Initialize(GameSession owner,RefugeNpcAssetCatalog savedCatalog)
        {
            if(initialized)throw new InvalidOperationException("Initialize a fresh saved refuge NPC owner once.");
            if(owner==null||owner.Hud==null||owner.Player==null)throw new ArgumentException("Initialize after the actual Player and HUD.");
            session=owner;catalog=savedCatalog;initialized=true;wasSmithVisible=owner.Shop!=null&&owner.Shop.Visible;
            Status="Waiting for the actual008 refuge and its original saved NPC catalog.";
        }
        public void SetQuestBridge(IRefugeNpcQuestBridge source,UnityEngine.Object lifetimeOwner)
        {
            if(source!=null&&lifetimeOwner==null)throw new ArgumentException("A quest bridge needs its real Unity lifetime owner.");
            DetachBridge();bridge=source;bridgeLifetime=lifetimeOwner;
            if(bridge!=null)bridge.Changed+=QuestChanged;questDirty=true;
            wasSmithVisible=session!=null&&session.Shop!=null&&session.Shop.Visible;
        }
        void QuestChanged(){questDirty=true;}
        void Update()
        {
            if(!initialized||session==null)return;
            if(bridge!=null&&!bridgeLifetime){DetachBridge();questDirty=true;}
            if(questDirty)RefreshQuest();
            if(!RefugeWorld)
            {
                HideOwnedModal();SetVisible(false);wasSmithVisible=false;return;
            }
            if((actors[0]==null||actors[1]==null)&&!catalogFailed&&Time.unscaledTime>=retryAt)
            {retryAt=Time.unscaledTime+.5f;TryCreateActors();}
            SetVisible(true);
            bool shopVisible=session.Shop!=null&&session.Shop.Visible;
            if(shopVisible&&!wasSmithVisible&&quest.Active&&quest.InRefuge&&BridgeAlive())
            {RegisterTargets();bridge.RequestNpcVisit(1);}
            wasSmithVisible=shopVisible;
            if(session.Hud.IsRefugeNpcConversationVisible(this))
            {
                if(!RefugeWorld||session.IsDead||session.IsDowned||session.Victory){HideOwnedModal();return;}
                var keyboard=Keyboard.current;
                if(Time.frameCount>openedFrame&&keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)HideOwnedModal();
                return;
            }
            conversationRole=0;
            if(!CanInteract)return;
            bool interact=Keyboard.current!=null&&Keyboard.current[GameActionBindings.GetKey("Interact")].wasPressedThisFrame;
            bool right=Mouse.current!=null&&Mouse.current.rightButton.wasPressedThisFrame;
            if((interact||right)&&LookTarget()!=0)OpenConversation(lookRole);
        }
        void TryCreateActors()
        {
            if(!catalogValidated)
            {
                string reason="Original saved NPC catalog is not bound.";
                if(catalog==null||!catalog.TryValidate(out reason)){catalogFailed=true;Status=reason;return;}
                catalogValidated=true;
            }
            for(int i=0;i<2;i++)
            {
                if(actors[i]!=null)continue;
                // Optional Bram's occupied site must never suppress a valid Ilyra.
                if(!session.World.TryGetRefugeNpcPose(i+2,out sites[i],out yaws[i])||!ValidSite(sites[i],out float floor))continue;
                sites[i].y=floor;
                if(visualRoot==null){visualRoot=new GameObject("Original saved Ilyra and Bram instances");visualRoot.transform.SetParent(transform,false);visualRoot.SetActive(false);}
                int role=i+2;GameObject root=Instantiate(catalog.Prefab(role),visualRoot.transform,false);
                root.name=role==2?"Ilyra — original saved Runekeeper":"Bram — original saved Gatekeeper";
                root.transform.SetPositionAndRotation(sites[i],Quaternion.Euler(0,yaws[i],0));
                var actor=actors[i]=new Actor{Role=role,Root=root,Feet=root.transform.Find("Feet"),Interact=root.transform.Find("Interact"),
                    Voice=root.transform.Find("Voice"),Torso=root.transform.Find(RefugeNpcAssetCatalog.PartPath(role,1)),
                    Head=root.transform.Find(RefugeNpcAssetCatalog.PartPath(role,2)),Left=root.transform.Find(RefugeNpcAssetCatalog.PartPath(role,5)),
                    Right=root.transform.Find(RefugeNpcAssetCatalog.PartPath(role,6)),Lids=root.transform.Find(RefugeNpcAssetCatalog.PartPath(role,4)),Age=i*.7f};
                actor.TorsoRest=actor.Torso.localPosition;actor.HeadRest=actor.Head.localRotation;actor.LeftRest=actor.Left.localRotation;
                actor.RightRest=actor.Right.localRotation;actor.LidsRest=actor.Lids.localScale;
                actor.Body=root.AddComponent<CapsuleCollider>();actor.Body.radius=.29f;actor.Body.height=1.82f;actor.Body.center=new Vector3(0,.91f,0);
            }
            if(visualRoot!=null)visualRoot.SetActive(true);RegisterTargets();lookFrame=-1;
            Status=actors[0]!=null?(actors[1]!=null?"Two original saved NPCs ready. Ilyra guides; Bram offers optional lore.":
                "Ilyra ready; optional Bram is waiting for a clear floor."):"Waiting for a clear actual Ilyra floor/pose.";
        }
        bool ValidSite(Vector3 seed,out float floor)
        {
            floor=0;if(!Finite(seed.x)||!Finite(seed.y)||!Finite(seed.z))return false;
            if(!Physics.Raycast(seed+Vector3.up*2f,Vector3.down,out RaycastHit hit,3f,~(1<<2),QueryTriggerInteraction.Ignore)||
                hit.normal.y<.9f||Mathf.Abs(hit.point.y-seed.y)>.15f)return false;
            floor=hit.point.y;Vector3 bottom=new Vector3(seed.x,floor+.33f,seed.z),top=bottom+Vector3.up*1.20f;
            return Physics.OverlapCapsuleNonAlloc(bottom,top,.29f,overlap,~(1<<2),QueryTriggerInteraction.Ignore)==0;
        }
        void RefreshQuest()
        {
            questDirty=false;var previous=quest;quest=default;
            if(BridgeAlive()&&bridge.TryReadAccepted(out var accepted)&&accepted.Active&&accepted.InRefuge&&accepted.Stage>=1&&accepted.Stage<=3&&
                accepted.VisitMask>=0&&accepted.VisitMask<=3&&(accepted.TowerMask==0||accepted.TowerMask==15)&&
                accepted.Members>=1&&accepted.Members<=RoomConnection.MaximumPlayers&&accepted.Ready>=0&&accepted.Ready<=accepted.Members)quest=accepted;
            objectives=!quest.Active?"":(quest.VisitMask&1)==0?"Visit the Tower Anvil. No purchase is required.":(quest.VisitMask&2)==0?
                "Speak to Raykon, the spell mentor.":quest.TowerMask!=15?"Waiting for recorded core tower permission.":"Go to the violet portal and review party readiness.";
            if(quest.Active)RegisterTargets();
            if(conversationRole!=0&&(previous.VisitMask!=quest.VisitMask||previous.Active!=quest.Active))
                session.Hud.RefreshRefugeNpcConversation(this,ConversationBody(conversationRole));
        }
        bool BridgeAlive()=>bridge!=null&&bridgeLifetime;
        void RegisterTargets()
        {
            if(!BridgeAlive()||!quest.Active||!quest.InRefuge||!RefugeWorld)return;
            var smith=session.Shop!=null?session.Shop.Blacksmith:null;
            if(smith!=null&&smith.gameObject.activeInHierarchy)bridge.RegisterNpcTarget(1,smith.transform,smith.TowerAnvilFeet,smith.TowerAnvilAimPoint);
            if(session.World.TryGetRaykonInteractionTarget(out var mentor,out var feet,out var aim))bridge.RegisterNpcTarget(2,mentor,feet,aim);
            else bridge.RegisterNpcTarget(2,null,default,default);
        }
        int LookTarget()
        {
            if(lookFrame==Time.frameCount)return lookRole;lookFrame=Time.frameCount;lookRole=0;
            if(!CanInteract||session.Player.ViewCamera==null)return 0;
            Transform eye=session.Player.ViewCamera.transform;
            if(!Physics.Raycast(eye.position,eye.forward,out RaycastHit hit,LookReach,~(1<<2),QueryTriggerInteraction.Ignore))return 0;
            for(int i=0;i<2;i++)
            {
                Actor actor=actors[i];if(actor==null||hit.collider!=actor.Body)continue;
                Vector3 delta=session.Player.transform.position-actor.Feet.position;
                if(Mathf.Abs(delta.y)<1.5f&&delta.x*delta.x+delta.z*delta.z<=Reach*Reach)lookRole=actor.Role;
            }
            return lookRole;
        }
        void OpenConversation(int role)
        {
            if(Time.frameCount==openedFrame||!CanInteract||LookTarget()!=role)return;
            if(!session.Hud.TryShowRefugeNpcConversation(this,role==2?"Ilyra · Optional lore":"Bram · Gatekeeper",ConversationBody(role)))return;
            conversationRole=role;openedFrame=Time.frameCount;
            // Ilyra remains optional lore. Only the actual Raykon interaction requests role two.
        }
        string ConversationBody(int role)
        {
            string body=role==2?
                "Raykon keeps the spell lessons beside his cauldron in the middle left alcove. Speak to him to review your actual spell tree or casting guide.\n\nMy stories are optional. No point is spent here, and visiting me is not a campaign requirement.":
                "A good gatekeeper counts the people coming home. Leave a path for a friend, and watch the ward before watching the horizon.\n\nI keep the lantern here for the returning party. My stories are optional; you do not need my permission or a purchase to depart.";
            if(quest.Active&&quest.InRefuge)body+="\n\nACCEPTED REFUGE VISITS\nTower Anvil: "+((quest.VisitMask&1)!=0?"recorded":"needed — no purchase")+
                "\nRaykon: "+((quest.VisitMask&2)!=0?"recorded":"needed")+"\nVisits count only after the host records them. Either order is valid.";
            return body;
        }
        public bool TryGetActiveTarget(int role,out Transform root,out Vector3 feet,out Vector3 aim)
        {
            root=null;feet=aim=default;if(!RefugeWorld||role<2||role>3)return false;
            Actor actor=actors[role-2];if(actor==null||actor.Root==null||!actor.Root.activeInHierarchy)return false;
            root=actor.Root.transform;feet=actor.Feet.position;aim=actor.Interact.position;return true;
        }
        void LateUpdate()
        {
            if(!initialized||visualRoot==null||!visualRoot.activeInHierarchy||!Application.isFocused||session==null||session.Paused)return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);if(!Finite(dt)||dt<=0)return;
            for(int i=0;i<2;i++)
            {
                Actor a=actors[i];if(a==null||a.Root==null)continue;a.Age+=dt;float breath=Mathf.Sin(a.Age*1.55f+a.Role*.71f);
                a.Torso.localPosition=a.TorsoRest+Vector3.up*(breath*.0025f);
                a.Left.localRotation=a.LeftRest*Quaternion.Euler(Mathf.Sin(a.Age*.7f)*.3f,0,Mathf.Sin(a.Age*.8f)*.35f);
                a.Right.localRotation=a.RightRest*Quaternion.Euler(Mathf.Sin(a.Age*.63f+.8f)*.3f,0,Mathf.Sin(a.Age*.75f)*.35f);
                float yaw=0,pitch=0;
                if(session.Player!=null&&session.Player.ViewCamera!=null)
                {
                    Vector3 toward=a.Root.transform.InverseTransformPoint(session.Player.ViewCamera.transform.position)-new Vector3(0,1.68f,0);
                    if(toward.z>.1f&&toward.sqrMagnitude<64f){yaw=Mathf.Clamp(Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg,-20,20);
                        pitch=Mathf.Clamp(-Mathf.Atan2(toward.y,new Vector2(toward.x,toward.z).magnitude)*Mathf.Rad2Deg,-8,8);}
                }
                float blend=1-Mathf.Exp(-dt*3);a.Yaw=Mathf.Lerp(a.Yaw,yaw,blend);a.Pitch=Mathf.Lerp(a.Pitch,pitch,blend);
                a.Head.localRotation=a.HeadRest*Quaternion.Euler(a.Pitch,a.Yaw,0);
                if(a.Blink<0&&a.Age>=a.NextBlink)a.Blink=0;
                float closed=0;if(a.Blink>=0){a.Blink+=dt;closed=Mathf.Sin(Mathf.Clamp01(a.Blink/.16f)*Mathf.PI);
                    if(a.Blink>=.16f){a.Blink=-1;a.NextBlink=a.Age+3.2f+Mathf.Sin(a.Age)*.65f;}}
                a.Lids.localScale=new Vector3(a.LidsRest.x,Mathf.Lerp(a.LidsRest.y,1,closed),a.LidsRest.z);
                // Original mouth remains static. No voice or lip-sync is fabricated.
            }
        }
        void SetVisible(bool value){if(visualRoot!=null&&visualRoot.activeSelf!=value){visualRoot.SetActive(value);lookFrame=-1;if(value)RegisterTargets();}}
        void HideOwnedModal(){if(session!=null&&session.Hud!=null)session.Hud.HideRefugeNpcConversation(this);conversationRole=0;}
        void DetachBridge(){if(bridge!=null)bridge.Changed-=QuestChanged;bridge=null;bridgeLifetime=null;quest=default;objectives="";}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        void OnDisable(){HideOwnedModal();SetVisible(false);DetachBridge();questDirty=true;}
        void OnDestroy()
        {
            DetachBridge();HideOwnedModal();if(visualRoot!=null){visualRoot.SetActive(false);Destroy(visualRoot);}
            visualRoot=null;actors[0]=actors[1]=null;session=null;catalog=null;initialized=false;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Bounded cached collider/root references only; no new economy/native buffers or quest bits.
// Root owns Unity integration and native acceptance; this is a guarded work candidate.
