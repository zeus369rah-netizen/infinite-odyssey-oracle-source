using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    [DisallowMultipleComponent]
    public sealed class NibwickRuntime020 : MonoBehaviour
    {
        GameSession session;
        NibwickActorLease020 actor;
        NibwickMotionLease020 motion;
        readonly AudioClip[] clips=new AudioClip[NibwickLines020.Count+1];
        readonly UnityEngine.Object[] stationOwners=new UnityEngine.Object[5];
        byte stationMask;
        uint stationGeneration,validatedGeneration,validatedRevision,rejectedGeneration,rejectedRevision;
        NibwickTourState020 state;
        NibwickCommand020 command;
        uint rejectedVoiceToken;
        int bindingRevision=int.MinValue;
        string hint="[E] Talk to Nibwick";
        public string Status020 {get;private set;}="Waiting for actual Campaign hub readiness.";
        public bool TourStarted020=>state.Tour==1;
        public bool CanBeginTour020=>state.Tour==1||state.Node==1;
        public NibwickPhase020 Phase020=>state.Phase;
        public uint Generation020=>validatedGeneration;

        public void Initialize020(GameSession owner)
        {
            if(session!=null||owner==null)throw new System.InvalidOperationException("Initialize one fresh Nibwick owner with its session.");
            session=owner;
            for(int line=1;line<=NibwickLines020.Count;line++)
            {clips[line]=Resources.Load<AudioClip>(NibwickLines020.Resource(line));if(clips[line]!=null)clips[line].LoadAudioData();}
        }
        public bool LeaseCurrent020(uint generation)=>isActiveAndEnabled&&session!=null&&session.Nibwick020==this&&
            session.CampaignHubActive020&&session.World!=null&&generation!=0&&generation==validatedGeneration&&
            generation==session.World.CampaignHubGeneration020&&validatedRevision==session.World.WorldRevision020&&
            actor!=null&&actor.Root!=null&&actor.Root.activeInHierarchy;
        public bool CanAdmitLine020(uint token,int line)=>LeaseCurrent020(validatedGeneration)&&Application.isFocused&&
            state.VoiceToken==token&&state.RequestedLine==line&&line>=1&&line<=NibwickLines020.Count&&
            (line==1?state.Greeted==1&&state.OpenPanel==1:state.Tour==1||state.Phase==NibwickPhase020.Choosing);

        public void SetStationReady020(int stop,UnityEngine.Object lifetime,bool ready,uint generation)
        {
            if(stop<0||stop>=5||generation==0||session==null||!session.CampaignHubActive020||session.World==null||generation!=session.World.CampaignHubGeneration020)return;
            if(stationGeneration!=generation){stationMask=0;stationGeneration=generation;for(int i=0;i<5;i++)stationOwners[i]=null;}
            byte bit=(byte)(1<<stop);
            if(ready&&lifetime!=null){stationOwners[stop]=lifetime;stationMask|=bit;}
            else{stationOwners[stop]=null;stationMask=(byte)(stationMask&~bit);}
        }
        byte StationMask020()
        {
            if(stationGeneration!=validatedGeneration)return 0;
            for(int i=0;i<5;i++)if(stationOwners[i]==null||stationOwners[i] is Behaviour b&&!b.isActiveAndEnabled)stationMask=(byte)(stationMask&~(1<<i));
            return stationMask;
        }
        public void Request020(NibwickCommand020 value)
        {
            if(!LeaseCurrent020(validatedGeneration)||!Application.isFocused||session.Hud==null||!session.Hud.IsNibwickConversationVisible020(this)||command!=NibwickCommand020.None)return;
            if(value!=NibwickCommand020.TakeTour&&value!=NibwickCommand020.SkipStop&&value!=NibwickCommand020.EndTour&&value!=NibwickCommand020.OtherArrivals)return;
            command=value;
        }
        public void Tick020()
        {
            if(session==null||session.World==null||session.Hud==null||!session.Hud.isActiveAndEnabled||!session.CampaignHubActive020||session.Player==null||session.Player.ViewCamera==null||session.RefugeActivities==null)
            {Retire020();return;}
            var world=session.World;uint generation=world.CampaignHubGeneration020,revision=world.WorldRevision020;
            if(generation==0||revision==0||!world.TryGetPortalLandingPrototype020(generation,out var snapshot)||snapshot.Root==null||!snapshot.Root.gameObject.activeInHierarchy||snapshot.Generation!=generation)
            {Retire020();return;}
            var admission=new LandingAdmission020{Kind=PortalLandingLayout020.ContextKind,Generation=generation,StoryRevision=revision,Ready=1,Authority=1,UnlockedEndpoints=0};
            // This local context authorizes only immutable geometry queries. It
            // never grants a story endpoint or mutates a travel/unlock owner.
            if(validatedGeneration!=generation||validatedRevision!=revision)
            {
                if(rejectedGeneration==generation&&rejectedRevision==revision)return;
                Retire020();
                if(actor==null)
                {
                    var catalog=session.RefugeNpcModels;
                    if(catalog==null||!catalog.TryValidate(out _)||catalog.SharedPaint==null)
                    {Reject020(generation,revision,"Nibwick needs the retained valid shared NPC palette.");return;}
                    try{actor=new NibwickActorLease020(session.transform,catalog.SharedPaint);motion=new NibwickMotionLease020(actor.Pivots);}
                    catch{motion?.Dispose();motion=null;actor?.Dispose();actor=null;Reject020(generation,revision,"Nibwick cold visual lease could not be prepared.");throw;}
                }
                if(!motion.Validate(admission)){Reject020(generation,revision,"Nibwick route/body clearance or support validation failed; generation remains closed.");return;}
                validatedGeneration=generation;validatedRevision=revision;motion.Reset();rejectedVoiceToken=0;
                Status020="Nibwick is ready for an explicit interaction.";
            }
            Vector3 player=session.Player.transform.position,eye=session.Player.ViewCamera.transform.position,aim=session.Player.ViewCamera.transform.forward;
            bool control=session.IsPlaying&&Application.isFocused&&Cursor.lockState==CursorLockMode.Locked&&!AbilityBindings.IsRebinding;
            NibwickCommand020 next=command;command=NibwickCommand020.None;
            if(next==NibwickCommand020.None&&control&&Keyboard.current!=null&&Keyboard.current[GameActionBindings.GetKey("Interact")].wasPressedThisFrame)next=NibwickCommand020.Interact;
            var result=rejectedVoiceToken!=0&&rejectedVoiceToken==state.VoiceToken?NibwickVoiceResult020.Interrupted:
                session.RefugeActivities.CampaignNpcResult020(this,state.VoiceToken);
            var input=new NibwickTourInput020{
                Admission=admission,WorldRevision=revision,Admitted=1,Focused=Application.isFocused?(byte)1:(byte)0,Control=control?(byte)1:(byte)0,
                BlockingMenu=session.Hud.NibwickMenuBlocks020(this)?(byte)1:(byte)0,
                Player=new LandingPoint020(player.x,player.y,player.z),Eye=new LandingPoint020(eye.x,eye.y,eye.z),Aim=new LandingPoint020(aim.x,aim.y,aim.z),
                Delta=Mathf.Clamp(Time.unscaledDeltaTime,.000001f,.1f),Command=next,StationMask=StationMask020(),VoiceToken=state.VoiceToken,VoiceResult=result,
                Speaking=session.RefugeActivities.IsCampaignNpcSpeaking020(this,state.VoiceToken)?(byte)1:(byte)0
            };
            state=motion.Evaluate(input);
            if(state.Generation==0){Retire020();Reject020(generation,revision,"Nibwick numeric motion or rigid joint reach failed; generation remains closed.");return;}
            if(state.CancelVoice==1)session.RefugeActivities.StopNpcGreeting009(this);
            actor.Apply(state,motion);actor.SetVisible(state.Generation!=0);
            if(state.OpenPanel==1)session.Hud.ShowNibwickConversation020(this,NibwickLines020.Caption(state.RequestedLine));
            if(state.RequestedLine!=0)
            {
                session.Hud.SetNibwickReadingLine020(this,NibwickLines020.Caption(state.RequestedLine));
                bool accepted=session.RefugeActivities.TryBeginCampaignNpcLine020(this,validatedGeneration,state.VoiceToken,state.RequestedLine,clips[state.RequestedLine],NibwickLines020.Caption(state.RequestedLine));
                rejectedVoiceToken=accepted?0:state.VoiceToken;
            }
            if(bindingRevision!=AbilityBindings.LastChangeFrame)
            {bindingRevision=AbilityBindings.LastChangeFrame;hint="["+GameActionBindings.GetLabel("Interact")+"] Talk to Nibwick";}
            session.Hud.SetCampaignHubHint020(state.CanInteract==1?hint:"");
            session.Hud.RefreshNibwickControls020(this);
        }
        void Reject020(uint generation,uint revision,string reason)
        {rejectedGeneration=generation;rejectedRevision=revision;Status020=reason;actor?.SetVisible(false);}
        public void Retire020()
        {
            command=NibwickCommand020.None;
            if(session!=null){session.RefugeActivities?.StopNpcGreeting009(this);session.RefugeActivities?.ReleaseCampaignNpcResult020(this);session.Hud?.HideNibwickControls020(this);session.Hud?.SetCampaignHubHint020("");}
            actor?.SetVisible(false);motion?.Reset();validatedGeneration=validatedRevision=0;state=default;
            stationMask=0;stationGeneration=0;for(int i=0;i<5;i++)stationOwners[i]=null;
        }
        void OnDisable(){Retire020();}
        void OnDestroy(){Retire020();motion?.Dispose();motion=null;actor?.Dispose();actor=null;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual same-generation Campaign hub/world/player eye snapshots -> one supported rigid actor; current route/query context never grants story travel.
// Array Formats: One cold seventeen-part cache partitioned from thirteen original groups, one fixed numeric motion lease, fifteen borrowed clips, five consumer-lifetime slots and one pending value command.
// Dependencies/Pointers: Root-approved World/session snapshot alone admits; actual E begins first voice, tour requires UI opt-in. Common NPC voice/subtitle preferences own audio. Retire clears only owned voice/view/route; meshes/material lifetimes stay explicit. No quest/shop/reward fabrication or provider call.
