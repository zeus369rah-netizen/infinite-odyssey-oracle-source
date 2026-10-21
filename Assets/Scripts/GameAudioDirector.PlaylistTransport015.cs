using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        bool musicUserPaused015,playlistStartObserved015,playlistCycleEnded015;
        public bool MusicPaused015=>musicUserPaused015;
        public bool PlaylistCycleEnded015=>playlistCycleEnded015;
        public uint PlaylistConsumed015=>playlist009.PlayedMask;
        // Read-only source-slot sample positions let native checks verify Pause/UnPause without private reflection.
        public int PlaylistSourceSample015=>music!=null&&music.clip!=null?music.timeSamples:-1;
        public int PlaylistNextSourceSample015=>musicNext!=null&&musicNext.clip!=null?musicNext.timeSamples:-1;
        bool PlaylistImmediateAdvance015=>playlistRequest009!=-1||playlistPendingManual009;
        public void MusicPause015()
        {
            if(!initialized||musicUserPaused015)return;
            musicUserPaused015=true;SuspendMusic();playlistRevision009++;
        }
        public void MusicResume015()
        {
            if(!initialized)return;
            if(musicUserPaused015){musicUserPaused015=false;playlistRevision009++;}
            StartAudioFromGesture();
        }
        public void ToggleMusicPause015(){if(musicUserPaused015)MusicResume015();else MusicPause015();}
        public void SkipSong015()=>NextPlaylistTrack009();
        void TickPlaylistTransport015()
        {
            // HasStarted is authoritative session state; Settings and gesture callbacks cannot manufacture this edge.
            if(!playlistStartObserved015&&session!=null&&session.HasStarted)
            {
                playlistStartObserved015=true;
                if(playlistEnabled009&&!musicUserPaused015&&playlist009.Current==0&&!musicTransition&&music!=null&&music.isPlaying)
                    NextPlaylistTrack009();
            }
            if(!Application.isFocused||applicationPaused||GameActionBindings.IsRebinding||Time.frameCount<=GameActionBindings.LastChangeFrame)return;
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if(keyboard.ctrlKey.isPressed||keyboard.altKey.isPressed||keyboard.shiftKey.isPressed||
                keyboard.leftMetaKey.isPressed||keyboard.rightMetaKey.isPressed)return;
            bool pause=keyboard[GameActionBindings.GetKey("MusicPause")].wasPressedThisFrame;
            bool skip=keyboard[GameActionBindings.GetKey("MusicSkip")].wasPressedThisFrame;
            if(!pause&&!skip)return;
            var events=EventSystem.current;var selected=events!=null?events.currentSelectedGameObject:null;
            if(selected!=null&&selected.TryGetComponent<InputField>(out var field)&&field.isFocused)return;
            if(pause)ToggleMusicPause015();else SkipSong015();
        }
        void RetirePlaylistCycle015()
        {
            if(playlistCycleEnded015)return;
            CancelMusicFade009();
            if(music!=null){music.Stop();music.clip=null;}
            if(musicNext!=null){musicNext.Stop();musicNext.clip=null;}
            musicTransition=false;musicGain=1f;musicNextGain=0f;
            playlistCycleEnded015=true;ReleasePlaylistLeases009();playlistRevision009++;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing non-spatial two-source music and accepted local session/input context.
// Array Formats: Independent pause/start/cycle scalar latches and existing saved14-action keyboard map; no new AudioSource/arrays or repeated allocations.
// Dependencies/Pointers: Main thread owns public Pause/UnPause; focus and enable gates remain conjunctive. Text input/rebinding/modifier capture blocks shortcuts. One observed accepted initial session start can advance an actually playing opening; ordinary menu gestures cannot reset it.

