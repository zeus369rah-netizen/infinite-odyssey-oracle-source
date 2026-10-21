using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OdysseyOracle
{
    [DisallowMultipleComponent]
    public sealed partial class RefugeActivityDirector : MonoBehaviour
    {
        const string AchievementKey="Oracle.Achievements.v1";
        static readonly string[] Notices={"ACHIEVEMENT · A focus of your own","ACHIEVEMENT · Oracle training complete","ACHIEVEMENT · First guardian defeated","ACHIEVEMENT · Four elements united","ACHIEVEMENT · First tower built"};
        static readonly string[] Names={"A focus of your own","Oracle training complete","First guardian defeated","Four elements united","First tower built"};
        GameSession session; AudioSource voice,chime; AudioClip monsterVoice,raykonVoice,towerVoice,achievementSound;
        Text notice; int unlocked,voicesPlayed; float noticeUntil; int bindingFrame=int.MinValue;
        string staffHint="",raykonHint="";
        string trainingLabHint="";int trainingLabBindingFrame=int.MinValue;
        public string Hint{get;private set;}="";
        public void Initialize(GameSession owner)
        {
            session=owner;unlocked=OracleProgressPrefs015.GetInt(AchievementKey,0)&31;
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.volume=.7f;
            chime=gameObject.AddComponent<AudioSource>();chime.playOnAwake=false;chime.spatialBlend=0;chime.volume=.22f;
            monsterVoice=Resources.Load<AudioClip>("Tutorial/lesson_monster_practice");
            raykonVoice=Resources.Load<AudioClip>("Tutorial/raykon_greeting");
            towerVoice=Resources.Load<AudioClip>("Tutorial/lesson_tower_practice");
            achievementSound=Resources.Load<AudioClip>("Audio/WorldCue/achievement_unlock");
            Canvas canvas=owner.Hud.GetComponentInChildren<Canvas>();
            var panel=new GameObject("Achievement notice",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
            panel.transform.SetParent(canvas.transform,false);var rect=(RectTransform)panel.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,1f);rect.pivot=new Vector2(.5f,1f);
            rect.anchoredPosition=new Vector2(0,-100);rect.sizeDelta=new Vector2(800,48);
            notice=panel.GetComponent<Text>();notice.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");notice.fontSize=24;
            notice.color=new Color(1f,.82f,.38f);notice.alignment=TextAnchor.MiddleCenter;notice.raycastTarget=false;panel.SetActive(false);
            InitializeDiscoveryProgress();
            InitializeRefugeFlames020();
            InitializeNpcVoices009(canvas);
        }
        void Update()
        {
            if(session==null)return;
            voice.volume=session.Tutorial!=null?session.Tutorial.VoiceVolume:.7f;
            TickNpcVoices009();
            SyncRaykonMouth();
            chime.volume=.22f*(session.Audio!=null?session.Audio.SfxVolume:1f);
            if(notice.gameObject.activeSelf && Time.unscaledTime>=noticeUntil)notice.gameObject.SetActive(false);
            if(!session.HasStarted)return;
            if(session.Tutorial!=null&&session.Tutorial.CompletedThroughPractice)Unlock(1);
            if(session.Defeated>0)Unlock(2);
            if(session.Combat!=null&&session.Combat.OwnCosmicNovaCastCount>0)Unlock(3);
            if(session.Builder!=null&&session.Builder.PlacedCount>0)Unlock(4);
            Hint="";
            if(!session.IsPlaying||session.World==null||!session.World.IsTutorialRefuge||session.Player==null)return;
            if(bindingFrame!=AbilityBindings.LastChangeFrame){bindingFrame=AbilityBindings.LastChangeFrame;string key=GameActionBindings.GetLabel("Interact");staffHint="["+key+"] Take practice staff";raykonHint="["+key+"] Talk to Raykon";}
            if(trainingLabBindingFrame!=AbilityBindings.LastChangeFrame){trainingLabBindingFrame=AbilityBindings.LastChangeFrame;trainingLabHint="["+GameActionBindings.GetLabel("Interact")+"] Training lab";}
            bool labControl=session.TrainingLab!=null&&session.TrainingLab.LookingAtControl;
            Vector3 position=session.Player.transform.position;
            bool staff=session.World.TrainingStaffAvailable&&LookingAt(session.World.TutorialStaffPosition+Vector3.up*.9f,2.8f);
            bool raykon=CanTalkToRaykon();
            Hint=labControl?(session.TrainingLab.CanOpen?trainingLabHint:session.TrainingLab.Status):staff?staffHint:raykon?raykonHint:"";
            bool interact=Keyboard.current!=null&&Keyboard.current[GameActionBindings.GetKey("Interact")].wasPressedThisFrame;
            if(labControl&&interact){session.TrainingLab.Open();return;}
            if(staff&&interact&&session.World.TakeTrainingStaff())Unlock(0);
            else if(raykon&&interact)TryTalkToRaykon();
            if((voicesPlayed&1)==0&&(position-session.World.TutorialMonsterPracticePosition).sqrMagnitude<7f){voicesPlayed|=1;PlayVoice(monsterVoice);}
            if((voicesPlayed&2)==0&&(position-session.World.TutorialTowerPracticePosition).sqrMagnitude<7f){voicesPlayed|=2;PlayVoice(towerVoice);}
        }
        bool LookingAt(Vector3 point,float reach)
        {var eye=session.Player.ViewCamera.transform;Vector3 d=point-eye.position;return d.sqrMagnitude<reach*reach&&Vector3.Dot(eye.forward,d.normalized)>.82f;}
        void PlayVoice(AudioClip clip){if(clip==null)return;voice.clip=clip;voice.Play();}
        void Unlock(int index)
        {
            int bit=1<<index;if((unlocked&bit)!=0)return;unlocked|=bit;
            OracleProgressPrefs015.SetInt(AchievementKey,unlocked);OracleProgressPrefs015.Save();
            notice.text=Notices[index];noticeUntil=Time.unscaledTime+4f;notice.gameObject.SetActive(true);
            if(achievementSound!=null)chime.PlayOneShot(achievementSound);
        }
        public bool IsUnlocked(int index)=>index>=0&&index<5&&(unlocked&(1<<index))!=0;
        public static string AchievementName(int index)=>index>=0&&index<5?Names[index]:"";
        void OnDisable(){StopMechanicsGuide();StopNpcGreeting009(null);FlushDiscoveryProgress();FlushRefugeFlames020();if(session!=null&&session.Hud!=null)session.Hud.HideRaykonMentor(this);if(voice!=null)voice.Stop();session?.World?.SetRaykonSpeaking(false);if(chime!=null)chime.Stop();}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Cached player/view world metres; one screen-space notification and explicit input events.
// Array Formats: Five-bit once-only achievement state, fixed clip references and cached static notices.
// Dependencies/Pointers: Existing session/World/Tutorial owners remain authoritative; preferences write only at a real award, never each frame.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Array Formats: Bounded cached references and speaking bool only; no new economy/native buffers, quest bits or audio samples.
// Dependencies/Pointers: Existing Activity owns real greeting playback; World forwards a visual bool. Root owns integration/native acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: current local achievement owner and existing notice/chime.
// Array Formats: separate cached one-bit flame achievement state/key; original five legacy bits and20discovery rows untouched.
// Dependencies/Pointers: cold init and explicit disable flush only; accepted World Air completion invokes the new once-only adapter. Badge/book UI is owned by the achievement helper.
