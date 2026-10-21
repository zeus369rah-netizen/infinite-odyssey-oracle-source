using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RefugeActivityDirector
    {
        public const string MechanicsGuideCaption009="Protect the shrine through five waves, then use a monster portal to return to the refuge. Visit the anvil for towers and Raykon for spells. Ready up together at the violet portal. Each new stage opens another lane. Collect experience to level up. Each level adds fifteen percent damage. If you fall behind the wave, experience rewards increase. Forty waves await.";
        AudioClip mechanicsGuideClip009;
        bool mechanicsGuideVoiceOwned009,mechanicsGuidePaused015;
        public bool MechanicsGuidePlaying015=>mechanicsGuideVoiceOwned009&&!mechanicsGuidePaused015&&voice!=null&&voice.clip==mechanicsGuideClip009&&voice.isPlaying;
        void InitializeMechanicsVoice009(){mechanicsGuideClip009=Resources.Load<AudioClip>("Tutorial/mechanics_refuge_loop015");}
        bool MechanicsVoiceContext009=>session!=null&&session.Hud!=null&&session.Hud.IsMechanicsGuideVisible&&session.HasStarted&&session.Paused&&
            !session.IsDead&&!session.IsDowned&&!session.Victory&&Application.isFocused;
        public bool PlayMechanicsGuide()
        {
            if(!MechanicsVoiceContext009||voice==null||mechanicsGuideClip009==null||voice.mute||voice.volume<=0||AudioListener.volume<=0||AudioListener.pause)return false;
            if(mechanicsGuideVoiceOwned009&&voice.clip==mechanicsGuideClip009&&voice.isPlaying)return false;
            StopNpcGreeting009(null);session.Tutorial?.PauseForExternalNarration009();
            voice.Stop();voice.clip=mechanicsGuideClip009;voice.Play();mechanicsGuideVoiceOwned009=true;mechanicsGuidePaused015=false;
            session.Hud.SetMechanicsVoiceCaption009(this,voice.isPlaying);return true;
        }
        public void StopMechanicsGuide()
        {
            if(mechanicsGuideVoiceOwned009&&voice!=null&&voice.clip==mechanicsGuideClip009)voice.Stop();
            mechanicsGuideVoiceOwned009=mechanicsGuidePaused015=false;session?.Hud?.SetMechanicsVoiceCaption009(this,false);
        }
        void TickMechanicsVoice009()
        {
            if(!mechanicsGuideVoiceOwned009)return;
            if(session==null||session.Hud==null||!session.Hud.IsMechanicsGuideVisible||!session.HasStarted||!session.Paused||session.IsDead||session.IsDowned||session.Victory||voice==null||voice.clip!=mechanicsGuideClip009){StopMechanicsGuide();return;}
            if(!Application.isFocused||voice.mute||voice.volume<=0||AudioListener.volume<=0||AudioListener.pause){PauseMechanicsGuide015();return;}
            if(mechanicsGuidePaused015)return;
            if(!voice.isPlaying){mechanicsGuideVoiceOwned009=false;session.Hud.SetMechanicsVoiceCaption009(this,false);return;}
            session.Hud.SetMechanicsVoiceCaption009(this,true);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing visible/paused Mechanics Guide UI; no world, lesson, pointer or reward change.
// Array Formats: One cold optional clip/exact caption and source-owner bool; existing Activity AudioSource reused.
// Dependencies/Pointers: Root Hud.IsMechanicsGuideVisible contract required; explicit Play/Stop only, no auto start/replay. Pause separate Tutorial source without resetting lesson; close/focus/mute stops only owned clip and cannot stop a replacement NPC voice.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted original NPC conversation or explicit paused guide UI; no actor/root/authority writes.
// Array Formats: Eight cold banter clips with exact captions and four bounded role counters/cooldowns; one existing voice source.
// Dependencies/Pointers: Stock Kokoro/established Branok performances retain provenance; interaction only, close/focus/mute stop, no automatic replay.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.
