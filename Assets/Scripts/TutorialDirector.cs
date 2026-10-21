using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    /// <summary>Performed lessons: movement, shrine guide, combat, explicit training echo, recovery and town shop.</summary>
    public sealed partial class TutorialDirector : MonoBehaviour
    {
        public enum Lesson { Move, Run, Jump, Shrine, Guide, Guardian, TrainingEcho, Downed, Recovery, Shop, Complete, NovaTeaching, NovaPractice, TowerPractice }
        public Lesson CurrentLesson { get; private set; }
        public bool Finished { get; private set; }
        public bool CompletedThroughPractice { get; private set; }
        /// <summary>Lesson presentation only; this never grants permission to attack.</summary>
        public bool WandPreviewVisible => active && !Finished && (CurrentLesson==Lesson.Guide && guideIndex>=4 || CurrentLesson==Lesson.Guardian || CurrentLesson==Lesson.TrainingEcho || CurrentLesson==Lesson.Recovery || CurrentLesson==Lesson.Shop || CurrentLesson==Lesson.NovaTeaching || CurrentLesson==Lesson.NovaPractice);

        public string Heading { get; private set; }="";
        public string Instruction { get; private set; }="";
        public string Subtitle { get; private set; }="";
        public bool NeedsGuideContinue => HasPendingInstruction;
        public bool VoiceAvailable => voice!=null && voice.clip!=null;
        public bool IsSpeaking => voice!=null && voice.isPlaying && !voicePaused;
        public int CurrentGuideIndex => guideIndex;
        public int GuideAbilityPreviewIndex => !active || Finished ? -1
            : CurrentLesson==Lesson.NovaTeaching ? novaTeachingStep*2+((int)(lessonTime/2f)&1) : -1;
        public int GuideHighlightElement {get;private set;}=-1;
        public float VoiceVolume
        {
            get => narrationVolume;
            set
            {
                narrationVolume=SafeVolume(value);
                PlayerPrefs.SetFloat("Oracle.GuideVolume",narrationVolume);
                if(voice!=null)voice.volume=narrationVolume;
            }
        }

        public static readonly string[] GuideLines={
            "This is the green shrine. The green shrine is the most important thing in this game. If the shrine gets destroyed, your game is over.",
            "Keep the shrine standing until the co-op timer ends. Raised arms and a floor warning mean an attack is winding up. Move out of shots aimed at you. Interrupt or defeat monsters threatening your towers.",
            "Cast a confirmed spell with a left click. Defeated monsters drop XP. Collect real XP to gain run levels and earn skill points. Saved Heartstones and permanent training are separate.",
            "Your passive tree has four elements: fire, water, earth, and air. Each branch has its own upgrades. Take a look at your tree.",
            "All four elements start at level one. Prepare one element, a pair, or three. Three selected elements fire together, then clear. Choose all four for Cosmic Nova. Selecting spends no cooldown.",
            "Fire burns, water slows, earth staggers, and air pushes. Confirm two different elements to make one of six pair combos: Combustion, Binding Mud, Steam, Shrapnel, Magma, or Frost Gale. A launched pair shot gives both used elements four seconds of recovery. Unused elements stay ready.",
            "Aim and left click once to cast a prepared recipe. Or hold left click and press an element key to cast fresh pressed elements as soon as recovery allows. The global interval is half a second. Normal ingredients recover for four seconds; Cosmic Nova recovers for five. Optional auto fire stays off by default and waits half a second after selection.",
            "Four tower roles are available. Fire and Water attack ground and flying enemies. Earth targets the ground; Air targets flyers. Look at an empty raised pad and press interact or right click to build. Tier one is included. Tower upgrades are optional and belong in the refuge between stages.",
            "This game is a prototype, and feedback is appreciated. Good luck."
        };
        public const string CombatLessonLine="Prepare one element, a pair, or three, then aim and left click. Three elements fire together and clear. All four make Cosmic Nova. Or hold left click and press fresh element keys to cast when ready. Normal ingredients recover for four seconds, Nova for five, with a half second global interval. Optional auto fire waits half a second after selection. Step aside during the guardian windup.";
        public const string DownedLessonLine="Next is a safe training echo. It will put you into the downed state for six seconds. This is practice, not an enemy attack. Your Heartcrystals and upgrades are safe.";
        public const string RevivedLessonLine="You're back on your feet. During this exercise, recovery took six seconds. In shared battles, a nearby teammate can hold the interact key for two seconds to revive you. Otherwise, recovery happens automatically. Your saved progress stays safe.";
        public const string ShopLessonLine="Find the blacksmith in the right-hand alcove. Look at him and use the interact key to open his shop. Pink Heartcrystals buy permanent wand upgrades and passive training. You do not need to buy anything to finish this lesson.";
        static readonly string[] BranchLines={
            "FIRE: permanent and run damage training. The same passive node never counts twice.",
            "WATER: each learned node adds 0.20 seconds to Water slows. Slow strength stays 50%; normal ability recovery stays four seconds.",
            "EARTH: damage training; the second and third nodes extend Earth interrupts by 0.10 seconds each. Later hits gain 25% during the interrupt; normal recovery stays four seconds.",
            "AIR: movement training. Run skill points and the blacksmith's permanent Heartcrystal training are separate."
        };
        public const string CampaignPacingHint="Protect the shrine through five waves, then return to the refuge. Eight milestones span forty waves.";
        static readonly string[] ElementNames={"Fire","Water","Earth","Air"};

        GameSession session;
        AudioSource voice;
        AudioClip[] clips;
        readonly AudioClip[] lessonClips=new AudioClip[4];
        readonly bool[] moved=new bool[4],elementsTried=new bool[4];
        readonly float[] observedPracticeCooldowns=new float[4];
        Vector3 previousPosition;
        float narrationVolume=.85f,sprintTime,guideTime,jumpStartY,lessonTime,narrationDuration,sharedAimTime;
        bool jumpObserved,active,voicePaused,applicationPaused,sharedPractice,guardianDefeated,observedDowned,visitedShop;
        int guideIndex,practiceShots;
        string narrationLine="";

        public void Initialize(GameSession owner)
        {
            session=owner;
            narrationVolume=SafeVolume(PlayerPrefs.GetFloat("Oracle.GuideVolume",.85f));
            voice=gameObject.AddComponent<AudioSource>();
            voice.playOnAwake=false;voice.spatialBlend=0f;voice.loop=false;voice.volume=narrationVolume;
            clips=new AudioClip[GuideLines.Length];
            for(int i=0;i<clips.Length;i++)clips[i]=Resources.Load<AudioClip>("Tutorial/guide_"+i.ToString("00"));
            string[] names={"lesson_combat","lesson_downed","lesson_revived","lesson_shop"};
            for(int i=0;i<names.Length;i++)lessonClips[i]=Resources.Load<AudioClip>("Tutorial/"+names[i]);
            // Preserve original masters, but do not play superseded confirmation,
            // auto timing or fixed-four-pad narration. Missing new clips use captions.
            clips[4]=Resources.Load<AudioClip>("Tutorial/guide_04_rapid150_v1");
            clips[6]=Resources.Load<AudioClip>("Tutorial/guide_06_rapid150_v1");
            clips[7]=Resources.Load<AudioClip>("Tutorial/guide_07_rapid150_v1");
            lessonClips[0]=Resources.Load<AudioClip>("Tutorial/lesson_combat_rapid150_v1");
            InitializeNovaLesson();InitializeTowerPracticeLesson();InitializeInstructionFlow();PrepareGuideUi020();ResetTutorial();
        }

        public void ResetTutorial()
        {
            ResetInstructionFlow();ResetTowerPracticeLesson();TowerPracticeAcceptedDefeats=0;ResetNovaLesson();active=false;Finished=false;CompletedThroughPractice=false;CurrentLesson=Lesson.Move;
            for(int i=0;i<4;i++){moved[i]=false;elementsTried[i]=false;}
            sprintTime=guideTime=lessonTime=sharedAimTime=0f;
            jumpObserved=sharedPractice=guardianDefeated=observedDowned=visitedShop=false;
            practiceShots=guideIndex=0;GuideHighlightElement=-1;Subtitle=narrationLine="";
            StopVoice();SetLessonText();
        }
        public void StartTutorial()
        {
            ResetTutorial();active=true;
            previousPosition=session.Player.transform.position;
            session.World.LockPortal();
            session.RefugeActivities?.UseGuideStationNarration();
            ShowInstructionCard(0);
        }
        public void SkipTutorial()
        {
            ResetInstructionFlow();RaykonIntroduced=true;ResetTowerPracticeLesson();ResetNovaLesson();active=false;Finished=true;CompletedThroughPractice=false;CurrentLesson=Lesson.Complete;
            GuideHighlightElement=-1;Subtitle=narrationLine="";StopVoice();
            Heading="The Oracle is ready";
            Instruction="Explore the campfire forge or walk to the violet portal for co-op. "+CampaignPacingHint;
            session.World.UnlockPortal();
            session.Objective="Visit the forge or ready up at the violet portal.";
        }
        /// <summary>Stops presentation. Only completing the final shop lesson marks Finished.</summary>
        public void EndTutorial()
        {
            AwaitingSkillsMenu=false;instructionState.Reset();ResetTowerPracticeLesson();ResetNovaLesson();active=false;GuideHighlightElement=-1;Heading=Instruction=Subtitle=narrationLine="";
            StopVoice();
        }

        void Update()
        {
            SyncVoice();UpdateTowerPracticeLifetime();
            if(!active || session==null || !session.HasStarted || session.Player==null || session.IsDead || session.Victory)return;
            bool advancing=CanAdvanceTime();
            float dt=advancing?Mathf.Min(Time.unscaledDeltaTime,.1f):0f;
            if(CurrentLesson==Lesson.Guide)return; // Only an explicit card action advances instruction presentation.
            if(CurrentLesson==Lesson.TowerPractice)
            {
                if(!instructionState.HasAcknowledged(6))
                {ResetTowerPracticeLesson();ShowInstructionCard(6);return;}
                if(advancing&&session.IsPlaying&&!GameActionBindings.IsRebinding)TickTowerPracticeLesson(dt);
                previousPosition=session.Player.transform.position;return;
            }
            if(CurrentLesson==Lesson.NovaTeaching || CurrentLesson==Lesson.NovaPractice)
            {
                if(advancing && session.IsPlaying && !GameActionBindings.IsRebinding)TickNovaLesson(dt);
                previousPosition=session.Player.transform.position;return;
            }
            // Revival owns the real timer, HP restoration and spawn teleport. The
            // tutorial observes that state rather than faking a six-second result.
            if(CurrentLesson==Lesson.Downed)
            {
                if(session.IsDowned)
                {
                    observedDowned=true;
                    Instruction="Safe training echo · recovery in "+Mathf.CeilToInt(session.ReviveRemainingSeconds)+"s. Your saved progress is safe.";
                }
                else if(observedDowned)BeginRecovery();
                previousPosition=session.Player.transform.position;
                return;
            }
            if(!advancing || !session.IsPlaying)
            {previousPosition=session.Player.transform.position;return;}
            Vector3 position=session.Player.transform.position,travel=position-previousPosition;
            previousPosition=position;
            float horizontal=new Vector2(travel.x,travel.z).magnitude;
            Keyboard keyboard=Keyboard.current;
            if(GameActionBindings.IsRebinding)return;
            switch(CurrentLesson)
            {
                case Lesson.Move:
                    if(keyboard!=null && horizontal>Mathf.Max(.000001f,Time.deltaTime*.10f))
                    {
                        moved[0]|=Held(keyboard,"Forward");moved[1]|=Held(keyboard,"Left");
                        moved[2]|=Held(keyboard,"Back");moved[3]|=Held(keyboard,"Right");
                    }
                    Instruction="Move in each direction: "+MoveMark(0,"Forward")+MoveMark(1,"Left")+MoveMark(2,"Back")+MoveMark(3,"Right")+" · Mouse to look.";
                    if(moved[0] && moved[1] && moved[2] && moved[3])NextLesson(Lesson.Run);
                    break;
                case Lesson.Run:
                    Instruction="Hold ["+GameActionBindings.GetLabel("Sprint")+"] while moving to run.";
                    if(keyboard!=null && Held(keyboard,"Sprint") && horizontal>Time.deltaTime*.5f)sprintTime+=dt;
                    if(sprintTime>=.6f)NextLesson(Lesson.Jump);
                    break;
                case Lesson.Jump:
                    Instruction="Press ["+GameActionBindings.GetLabel("Jump")+"] and leave the ground to jump.";
                    if(keyboard!=null && keyboard[GameActionBindings.GetKey("Jump")].wasPressedThisFrame)
                    {jumpObserved=true;jumpStartY=position.y;}
                    if(jumpObserved && position.y>jumpStartY+.2f)NextLesson(Lesson.Shrine);
                    break;
                case Lesson.Shrine:
                    UpdatePracticeStaffHint015();
                    if(session.World.TrainingStaffCollected015&&Vector2.Distance(new Vector2(position.x,position.z),new Vector2(session.World.ShrinePosition.x,session.World.ShrinePosition.z))<5f)
                    {ShowInstructionCard(2);}
                    break;
                case Lesson.Guardian:TickGuardian(dt);break;
                case Lesson.TrainingEcho:
                    lessonTime+=dt;
                    Instruction="A safe training echo will begin after this explanation. This is practice; your Heartcrystals and upgrades stay safe.";
                    if(lessonTime>=narrationDuration && session.Revival!=null)
                    {
                        session.Revival.EnterDowned(true);
                        if(session.IsDowned)
                        {
                            CurrentLesson=Lesson.Downed;observedDowned=true;StopVoice();
                            Heading="Safe training echo · downed";
                            Subtitle="Training echo only. No enemy caused this fall; your saved progress is safe.";
                        }
                    }
                    break;
                case Lesson.Recovery:
                    lessonTime+=dt;
                    Instruction="In shared battles, a nearby teammate holds ["+GameActionBindings.GetLabel("Interact")+"] for 2s to help. Otherwise recovery is automatic. This echo used 6s.";
                    if(lessonTime>=narrationDuration)BeginShop();
                    break;
                case Lesson.Shop:
                    lessonTime+=dt;
                    if(session.Shop!=null && session.Shop.Visible)visitedShop=true;
                    UpdateShopText();
                    if(visitedShop && session.Shop!=null && !session.Shop.Visible && lessonTime>=narrationDuration)CompleteTutorial();
                    break;
            }
        }

        void LateUpdate()
        {
            // Opening the shop pauses gameplay. Record the actual modal even
            // though ordinary lesson clocks and narration correctly stop there.
            if(active && CurrentLesson==Lesson.Shop && session!=null && session.Shop!=null && session.Shop.Visible)visitedShop=true;
            SyncVoice();
        }
        bool CanAdvanceTime() => Application.isFocused && !applicationPaused && session!=null && !session.Paused;
        static bool Held(Keyboard keyboard,string action)=>keyboard[GameActionBindings.GetKey(action)].isPressed;
        string MoveMark(int i,string action)=>(moved[i]?"✓ ":"")+GameActionBindings.GetLabel(action)+"  ";
        void NextLesson(Lesson lesson){CurrentLesson=lesson;SetLessonText();}
        void SetLessonText()
        {
            Heading="Oracle's first steps";
            switch(CurrentLesson)
            {
                case Lesson.Move:Instruction="Move with your bound movement keys. Use the mouse to look.";break;
                case Lesson.Run:Instruction="Hold ["+GameActionBindings.GetLabel("Sprint")+"] while moving to run.";break;
                case Lesson.Jump:Instruction="Press ["+GameActionBindings.GetLabel("Jump")+"] to jump.";break;
                case Lesson.Shrine:UpdatePracticeStaffHint015();break;
            }
        }

        void BeginCombat()
        {
            GuideHighlightElement=-1;CurrentLesson=Lesson.Guardian;lessonTime=0f;
            sharedPractice=session.CoopMode && session.Rooms!=null && session.Rooms.Connected;
            for(int i=0;i<4;i++){elementsTried[i]=false;observedPracticeCooldowns[i]=session.Combat.CooldownRemaining[i];}
            practiceShots=0;sharedAimTime=0f;guardianDefeated=false;
            session.CombatUnlocked=true;session.World.LockPortal();
            if(!sharedPractice)session.Combat.BeginTutorialEncounter();
            Heading=sharedPractice?"Shared-hub wand practice":"Your first guardian";
            session.Objective=sharedPractice?"Aim at the violet arch and try your four elements.":"Wake and defeat the seated guardian.";
            PlayLesson(0,CombatLessonLine);
        }
        void TickGuardian(float dt)
        {
            lessonTime+=dt;
            int confirmed=session.Combat.HasConfirmedSelection?session.Combat.ConfirmedMask:0;
            for(int i=0;i<4;i++)if((confirmed&(1<<i))!=0)elementsTried[i]=true;
            string keys=ElementChecklist();
            if(sharedPractice)
            {
                Vector3 target=session.World.PortalPosition+Vector3.up*1.5f;
                Transform eye=session.Player.ViewCamera.transform;
                Vector3 aim=target-eye.position;
                bool aiming=aim.sqrMagnitude>.01f && Vector3.Dot(eye.forward,aim.normalized)>.96f;
                bool acceptedCast=false;
                for(int i=0;i<4;i++)
                {
                    float remaining=session.Combat.CooldownRemaining[i];
                    acceptedCast|=remaining>observedPracticeCooldowns[i]+.5f;
                    observedPracticeCooldowns[i]=remaining;
                }
                if(aiming){sharedAimTime+=dt;if(acceptedCast)practiceShots++;}
                // Combat alone consumes the actual mouse gesture. The guide observes
                // accepted recovery; it never requests an automatic practice shot.
                Instruction="Shared-hub practice: prepare one element, a pair, three for a volley, or all four for Nova. Aim at the violet arch, then left-click for three accepted casts. You may hold left-click and press fresh element keys to cast when ready. No enemy is spawned here. "+keys+" · "+Mathf.Min(3,practiceShots)+"/3 accepted casts";
                if(practiceShots>=3 && sharedAimTime>=2.5f && AllElementsTried() && lessonTime>=narrationDuration)ShowInstructionCard(1);
                return;
            }
            guardianDefeated|=session.Combat.TutorialGuardianDefeated || (session.Goal==1 && session.Defeated>=1);
            var guardian=session.Combat.TutorialGuardian;
            string action=guardianDefeated?"Guardian defeated. Prepare any remaining recipe; three elements fire together and clear."
                :!session.Combat.TutorialGuardianAwake?"Approach the seated guardian to wake it, then aim at it."
                :guardian!=null && guardian.AttackPhase==OracleEnemy.GuardianAttackPhase.Windup?"The guardian is winding up: step aside, then aim again."
                :"Prepare one element, a pair, three for a volley or all four for Nova; aim at the guardian and left-click.";
            Instruction=action+" "+keys;
            if(guardianDefeated && AllElementsTried() && lessonTime>=narrationDuration)ShowInstructionCard(1);
        }
        string ElementChecklist()
        {
            string result="SELECTED: ";
            for(int i=0;i<4;i++)result+=(i>0?"  ":"")+(elementsTried[i]?"✓ ":"")+"["+AbilityBindings.GetLabel(i)+"] "+ElementNames[i];
            return result;
        }
        bool AllElementsTried()=>elementsTried[0] && elementsTried[1] && elementsTried[2] && elementsTried[3];
        void BeginEcho()
        {
            if(!instructionState.HasAcknowledged(11))
            {ResetTowerPracticeLesson();ResetNovaLesson();ShowInstructionCard(7);return;}
            ResetTowerPracticeLesson();ResetNovaLesson();CurrentLesson=Lesson.TrainingEcho;lessonTime=0f;session.CombatUnlocked=false;
            session.World.LockPortal();Heading="A safe training echo";
            session.Objective="Practice the downed state safely, then visit the blacksmith.";
            PlayLesson(1,DownedLessonLine);
        }
        void BeginRecovery()
        {
            CurrentLesson=Lesson.Recovery;lessonTime=0f;Heading="Recovered · your progress is safe";
            PlayLesson(2,RevivedLessonLine);
        }
        void BeginShop()
        {
            CurrentLesson=Lesson.Shop;lessonTime=0f;visitedShop=false;Heading="Meet the blacksmith";
            session.Objective="Visit the blacksmith in the right-hand alcove. No purchase is required.";
            PlayLesson(3,ShopLessonLine);UpdateShopText();
        }
        void UpdateShopText()
        {
            Instruction=visitedShop?"Shop visited. Close it with Return; no purchase is required. The portal opens after the guide's explanation."
                :"Walk to the blacksmith in the right alcove, look at him and press ["+GameActionBindings.GetLabel("Interact")+"]. Open the shop, then Return. No purchase is required.";
        }
        void CompleteTutorial()
        {
            RaykonIntroduced=true;Finished=true;CompletedThroughPractice=true;CurrentLesson=Lesson.Complete;active=false;GuideHighlightElement=-1;
            StopVoice();Subtitle=narrationLine="";session.CombatUnlocked=true;session.World.UnlockPortal();
            Heading="Tutorial complete";
            Instruction=session.Rooms!=null && session.Rooms.Connected
                ?"Meet your teammate, then walk to the violet portal to choose co-op."
                :"Collect any remaining XP and pink Heartcrystals, then walk to the violet portal.";
            Instruction+=" "+CampaignPacingHint;
            session.Objective=Instruction;
        }

        static float CaptionDuration(string text)=>Mathf.Max(6f,text.Split(' ').Length/2.35f);
        void PlayLesson(int index,string caption)
        {
            AudioClip clip=index==0?null:lessonClips[index]; // Recipes were already narrated once on explicit cards.
            narrationDuration=index==0?0f:clip!=null?clip.length+.7f:CaptionDuration(caption);
            PlayNarration(clip,caption);
        }
        void PlayNarration(AudioClip clip,string caption)
        {
            Subtitle=narrationLine=caption;voice.Stop();voicePaused=false;
            bool cardReplacedVoice=CurrentLesson==Lesson.TowerPractice || CurrentLesson==Lesson.NovaTeaching&&instructionState.HasAcknowledged(5);
            voice.clip=cardReplacedVoice||SuppressAutomaticTutorialPresentation?null:clip;
            if(CurrentLesson==Lesson.TowerPractice&&instructionState.HasAcknowledged(6))narrationDuration=0f;
            PrepareGuideStep015(12+(int)CurrentLesson,voice.clip,caption);
            SyncVoice();
        }
        // Compatibility entry points never restart a clip or reset a real practice clock.
        public void ReplayNarration(){TryPlayInstructionNarration();}
        public void AdvanceGuide(){ContinueInstructionCard();}
        void SyncVoice()
        {
            if(voice==null)return;
            if(SyncGuideVoice015())return;
            // Master is applied exactly once by GameAudioDirector's AudioListener
            // stage. Source gain remains the independent saved Guide volume.
            voice.volume=narrationVolume;
            bool labPreview=trainingLabNarration>=0&&TrainingLabNarrationScope;
            if(trainingLabNarration>=0&&!labPreview)StopTrainingLabNarration();
            // Co-op free interaction has no scripted narrator. Explicit NPC voice has its own owner.
            if(SuppressAutomaticTutorialPresentation&&!labPreview){voice.Stop();voicePaused=false;return;}
            bool preview=labPreview || active && HasPendingInstruction && session!=null && session.Hud!=null && session.Hud.IsTutorialInstructionVisible;
            bool npcGreeting=session!=null&&session.RefugeActivities!=null&&session.RefugeActivities.NpcDialogueSpeaking020;
            bool mechanicsGuide=session!=null&&session.Hud!=null&&session.Hud.IsMechanicsGuideVisible;
            bool suspend=mechanicsGuide || npcGreeting || (!active&&!labPreview) || !Application.isFocused || applicationPaused || session==null ||
                !session.HasStarted || session.IsDead || session.Victory || (session.Paused && !preview);
            if(suspend && !voicePaused){voice.Pause();voicePaused=true;}
            else if(!suspend && voicePaused){voice.UnPause();voicePaused=false;}
        }
        void StopVoice(){ForgetGuideSource015();if(voice!=null){voice.Stop();voice.clip=null;}voicePaused=false;trainingLabNarration=-1;}
        static float SafeVolume(float value)=>float.IsNaN(value) || float.IsInfinity(value)?.85f:Mathf.Clamp01(value);
        void OnApplicationFocus(bool focused){SyncVoice();}
        void OnApplicationPause(bool paused){applicationPaused=paused;SyncVoice();}
        void OnDisable(){PauseGuideNarration015();SuspendTowerPracticeLesson();SuspendNovaLesson();if(voice!=null)voice.Pause();voicePaused=true;}
        void OnDestroy(){ResetTowerPracticeLesson();ResetNovaLesson();if(voice!=null)Destroy(voice);}
    }
}


// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing Unity tutorial/player state and Canvas captions.
// Array Formats: existing fixed-four element/cooldown arrays; static lesson strings and cold audio loads.
// Dependencies/Pointers: Combat alone accepts casts/recovery; campaign owns 150/180-second and 100-admission pacing; missing updated voice uses accurate captions.

// # ARCHITECTURE SNAPSHOT
// Bounded offline tutorial adapter; existing actor/HP/tower/native owners and reward boundaries remain.

// # ARCHITECTURE SNAPSHOT
// Existing world metres and Canvas coordinates; fixed twelve-card value state, existing Combat/Nova/Tower owners and real point costs retained.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/lab modal and focus policy; no actor, world or saved progression mutation.
// Array Formats: Existing twelve guide clips plus two cold-loaded lab help references, one selected-caption index.
// Dependencies/Pointers: Sole TutorialDirector guide AudioSource; exact journal caption/resource pairs; root owns import/playback.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted NPC/guide audio ownership and immutable guide note metadata.
// Array Formats: Existing fixed guide clip/caption/sample stores; no new frame storage or caption generation.
// Dependencies/Pointers: All-role NPC lease pauses automatic guide intent; explicit eligible Play alone preempts/resumes. Root v5 resource/voiced captions remain exact; root owns integration and native/browser listening.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted guide/menu Canvas and actual guide view/audio state.
// Array Formats: Existing bounded UI/state stores; cold immutable65status labels, no frame formatting.
// Dependencies/Pointers: Original restart/portal/settings/narration/voice/input/achievement owners remain. Root owns source apply and full compile/native/browser/listening acceptance.
