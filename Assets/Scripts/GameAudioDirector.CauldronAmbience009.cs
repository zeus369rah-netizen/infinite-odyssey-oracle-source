using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        const int CauldronHearthEmitter009=2147483001;
        AudioSource cauldronBubbles009,cauldronStir009;
        AudioClip cauldronBubbleClip009,cauldronStirClip009;
        bool cauldronHearthRegistered009,cauldronContextActive009;
        float cauldronBubbleGain009,cauldronStirGain009,nextCauldronHearthCheck009;
        public bool CauldronBubbleAvailable009=>cauldronBubbleClip009!=null;
        public bool CauldronStirAvailable009=>cauldronStirClip009!=null;
        public bool IsNarrationDucking009=>CampaignIntroSpeechDucking020||session!=null&&
            (session.Tutorial!=null&&session.Tutorial.IsSpeaking&&session.Tutorial.VoiceVolume>0f||
             session.RefugeActivities!=null&&session.RefugeActivities.IsNarrationPlaying009);

        void InitializeCauldronAmbience009()
        {
            cauldronBubbleClip009=Resources.Load<AudioClip>("Audio/RefugeLayers009/cauldron_bubbles009");
            cauldronStirClip009=Resources.Load<AudioClip>("Audio/RefugeLoops015/cauldron_stir009_overlap015");
            Prime(cauldronBubbleClip009);Prime(cauldronStirClip009);
            cauldronBubbles009=Source("Raykon cauldron bubbles009",true,232);
            cauldronStir009=Source("Raykon cauldron mixing009",true,232);
            cauldronBubbles009.loop=true;cauldronStir009.loop=true;
            cauldronBubbles009.clip=cauldronBubbleClip009;cauldronStir009.clip=cauldronStirClip009;
            cauldronBubbles009.minDistance=cauldronStir009.minDistance=1.25f;
            cauldronBubbles009.maxDistance=cauldronStir009.maxDistance=CauldronAudioMath009.Range;
            // Root integration pins the retained original generation/hash. This
            // is the verified torch recording, never excluded legacy fireClip.
            if(namedClips.TryGetValue("torch_crackle",out AudioClip originalCrackle)&&originalCrackle!=null)
            {
                fireClip=originalCrackle;campfire.clip=originalCrackle;Prime(originalCrackle);
                cauldronHearthRegistered009=SetAmbientEmitter(CauldronHearthEmitter009,"torch_crackle",Vector3.zero,true,9f,0f);
            }
        }

        void TickCauldronAmbience009(float dt)
        {
            DungeonWorld world=session!=null?session.World:null;
            CauldronAudioFrame009 frame=default;
            bool acceptedMentor=session!=null&&session.Hud!=null&&session.RefugeActivities!=null&&
                session.Hud.IsRaykonMentorVisible(session.RefugeActivities);
            bool context=isActiveAndEnabled&&CanPlayCue()&&session!=null&&session.HasStarted&&
                CauldronAudioMath009.AllowsPause(session.Paused,acceptedMentor)&&!session.IsDead&&!session.IsDowned&&!session.Victory&&world!=null&&
                world.TryGetCauldronAudio009(out frame)&&Near(frame.LiquidPosition,CauldronAudioMath009.Range);
            if(!context){StopCauldronAmbience009();return;}
            cauldronContextActive009=true;
            CauldronAudioMix009 mix=CauldronAudioMath009.Target(true,frame.TalkBlend,frame.HearthFuel);
            cauldronBubbleGain009=CauldronAudioMath009.Approach(cauldronBubbleGain009,mix.Bubbles,dt,.28f);
            cauldronStirGain009=CauldronAudioMath009.Approach(cauldronStirGain009,mix.Stir,dt,.22f);
            if(cauldronBubbles009!=null)
            {
                cauldronBubbles009.transform.position=frame.LiquidPosition;
                cauldronBubbles009.pitch=CauldronAudioMath009.Approach(cauldronBubbles009.pitch,mix.BubblePitch,dt,.3f);
                if(cauldronBubbleClip009!=null&&cauldronBubbleClip009.loadState==AudioDataLoadState.Loaded&&!cauldronBubbles009.isPlaying)
                {cauldronBubbles009.volume=0f;cauldronBubbles009.Play();}
            }
            if(cauldronStir009!=null)
            {
                cauldronStir009.transform.position=frame.LiquidPosition;cauldronStir009.pitch=1f;
                // Preserve loop phase while talk blend rests at1; gain approaches
                // zero instead of restarting the recording every speech frame.
                if(cauldronStirClip009!=null&&cauldronStirClip009.loadState==AudioDataLoadState.Loaded&&!cauldronStir009.isPlaying)
                {cauldronStir009.volume=0f;cauldronStir009.Play();}
            }
            if(cauldronHearthRegistered009&&Time.unscaledTime>=nextCauldronHearthCheck009)
            {
                nextCauldronHearthCheck009=Time.unscaledTime+.1f;
                if(ambientEmitters.TryGetValue(CauldronHearthEmitter009,out var emitter))
                {emitter.Position=frame.HearthPosition;emitter.Gain=mix.Hearth;nextAmbientCheck=0f;}
                else cauldronHearthRegistered009=false; // External clear fails closed, never hot reallocation.
            }
        }

        void ApplyCauldronVolumes009()
        {
            if(cauldronBubbles009!=null)cauldronBubbles009.volume=sfxVolume*cauldronBubbleGain009*duck;
            if(cauldronStir009!=null)cauldronStir009.volume=sfxVolume*cauldronStirGain009*duck;
        }
        void StopCauldronAmbience009()
        {
            cauldronContextActive009=false;
            if(cauldronBubbles009!=null&&cauldronBubbles009.isPlaying)cauldronBubbles009.Stop();
            if(cauldronStir009!=null&&cauldronStir009.isPlaying)cauldronStir009.Stop();
            cauldronBubbleGain009=cauldronStirGain009=0f;
            // In-place gain update only: registration stays cold and the managed
            // emitter/source arrays are reused through pause/world transitions.
            if(cauldronHearthRegistered009&&ambientEmitters.TryGetValue(CauldronHearthEmitter009,out var emitter)&&emitter.Gain!=0f)
            {emitter.Gain=0f;nextAmbientCheck=0f;}
        }
        void DisposeCauldronAmbience009()
        {
            StopCauldronAmbience009();
            if(cauldronHearthRegistered009)RemoveAmbientEmitter(CauldronHearthEmitter009);
            cauldronHearthRegistered009=false;
            if(cauldronBubbles009!=null)Destroy(cauldronBubbles009.gameObject);
            if(cauldronStir009!=null)Destroy(cauldronStir009.gameObject);
            cauldronBubbles009=cauldronStir009=null;
            // Resources clips are borrowed; never destroy/unload shared assets.
            cauldronBubbleClip009=cauldronStirClip009=null;
        }

        internal void NotifyPortalReady009(DungeonWorld acceptedWorld)
        {
            // Called only after the existing canonical UnlockPortal changes false
            // to true. No polling accent, readiness request or replay-on-focus.
            if(session==null||!object.ReferenceEquals(acceptedWorld,session.World)||
                !session.HasStarted||session.Paused||session.IsDowned||!acceptedWorld.PortalReadyForAudio009||
                !acceptedWorld.IsTutorialRefuge||!CanPlayCue())return;
            PlayCue("portal_open",acceptedWorld.PortalPosition+Vector3.up*1.4f);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual supported cauldron/listener/Hearth world metres via O(1) World frame; positional sources stay on main thread.
// Array Formats: Exactly2 cold owned continuous sources/2 borrowed small clips; existing32-emitter/4-voice registry borrows1 stable record, no new hot arrays/Objects/PCM or spatial scan.
// Dependencies/Pointers: Existing Audio owns gesture/focus/master/SFX/narration duck/pause/disposal. Canonical World accepts fuel/readiness; Activity/Tutorial own narration. No music restart, gameplay/protocol/save or light mutation. Native loop seam/listening/GC/peer acceptance pending.

