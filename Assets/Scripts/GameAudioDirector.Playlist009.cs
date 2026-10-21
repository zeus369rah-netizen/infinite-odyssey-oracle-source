using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        const string PlaylistPrefs009="Oracle.Audio.Playlist009.";
        readonly MusicPlaylistQueue009 playlist009=new MusicPlaylistQueue009();
        readonly AudioClip[] playlistLeases009=new AudioClip[MusicPlaylistCatalog009.Count];
        uint playlistBound009,playlistFailed009;
        bool playlistInitialized009,playlistEnabled009=true,playlistAwaitGesture009,playlistShortIntros009=true;
        int playlistPending009=-1,playlistRequest009=-1,playlistRevision009;
        bool playlistPendingManual009,playlistPendingReplay015;
        float playlistLoadDeadline009;
        AudioClip playlistPendingClip009;
        public bool MusicEnabled009=>!playlistInitialized009||playlistEnabled009;
        public int PlaylistRevision009=>playlistRevision009;
        public int PlaylistCurrent009=>playlist009.Current;
        public MusicPlaylistMode009 PlaylistMode009=>playlist009.Mode;
        public bool PlaylistTransition009=>musicTransition;
        public bool PlaylistPlaying009=>music!=null&&music.isPlaying||musicNext!=null&&musicNext.isPlaying;
        public int PlaylistPending009=>playlistPending009;
        public bool PlaylistAwaitGesture009=>playlistAwaitGesture009;
        public bool PlaylistShortIntros009=>playlistShortIntros009;
        public uint PlaylistAvailable009=>(playlistBound009|((menuMusicClips[0]!=null)?1u:0u))&~playlistFailed009;
        public bool PlaylistOriginalVerified009=>(playlistBound009&~playlistFailed009&1u)!=0;
        public bool PlaylistTrackEnabled009(int i)=>playlist009.Enabled(i);
        public int PlaylistRating009(int i)=>playlist009.Rating(i);
        public bool PlaylistTrackAvailable009(int i)=>MusicPlaylistCatalog009.Valid(i)&&(PlaylistAvailable009&(1u<<i))!=0;
        public string PlaylistTitle009(int i)=>!MusicPlaylistCatalog009.Valid(i)?"NO TRACK SELECTED":i==0&&!PlaylistOriginalVerified009?"Existing theme · Sacred Horizon pending":MusicPlaylistCatalog009.Tracks[i].Title;
        void InitializePlaylist009()
        {
            playlist009.Disabled=(uint)PlayerPrefs.GetInt(PlaylistPrefs009+"Disabled",0);playlist009.Liked=(uint)PlayerPrefs.GetInt(PlaylistPrefs009+"Liked",0);playlist009.Disliked=(uint)PlayerPrefs.GetInt(PlaylistPrefs009+"Disliked",0);playlist009.Mode=(MusicPlaylistMode009)PlayerPrefs.GetInt(PlaylistPrefs009+"Mode",0);playlist009.Sanitize();
            playlistEnabled009=PlayerPrefs.GetInt(PlaylistPrefs009+"Enabled",1)!=0;playlistShortIntros009=PlayerPrefs.GetInt(PlaylistPrefs009+"ShortIntros",1)!=0;playlistInitialized009=true;
            BindSoundtrack009(Resources.Load<SoundtrackBinding009>("Audio/MusicExpansion009/library009"));
        }
        public void BindSoundtrack009(SoundtrackBinding009 binding)
        {
            CancelPlaylistPending009();playlistBound009=binding!=null?binding.VerifiedMask:0;playlistFailed009=0;
            playlist009.ResetOpening();playlistRequest009=-1;playlistStartObserved015=false;playlistCycleEnded015=false;playlistRevision009++;
        }
        public void SetMusicEnabled009(bool value)
        {
            if(!playlistInitialized009||playlistEnabled009==value)return;playlistEnabled009=value;
            if(!value)SuspendMusic();else StartAudioFromGesture();
            SavePlaylistPrefs009();playlistRevision009++;
        }
        public void SetPlaylistMode009(MusicPlaylistMode009 value)
        {if((uint)value>2||playlist009.Mode==value)return;playlist009.Mode=value;CancelPlaylistPending009();playlistCycleEnded015=false;SavePlaylistPrefs009();playlistRevision009++;}
        public void SetPlaylistShortIntros009(bool value)
        {if(playlistShortIntros009==value)return;playlistShortIntros009=value;SavePlaylistPrefs009();playlistRevision009++;}
        public void SetPlaylistTrackEnabled009(int i,bool enabled)
        {
            if(!MusicPlaylistCatalog009.Valid(i)||playlist009.Enabled(i)==enabled)return;uint bit=1u<<i;if(enabled)playlist009.Disabled&=~bit;else playlist009.Disabled|=bit;
            if(enabled)playlistCycleEnded015=false;
            if(!enabled&&playlistPending009==i)CancelPlaylistPending009();
            if(!enabled&&playlistRequest009==i)playlistRequest009=-2;
            if(!enabled&&playlist009.Current==i){if(!musicTransition)CancelPlaylistPending009();playlistRequest009=-2;}
            SavePlaylistPrefs009();playlistRevision009++;
        }
        public void SetPlaylistRating009(int i,int rating)
        {if(!MusicPlaylistCatalog009.Valid(i))return;playlist009.SetRating(i,rating);if(!playlistPendingManual009)CancelPlaylistPending009();SavePlaylistPrefs009();playlistRevision009++;}
        public bool PlayPlaylistTrack009(int i)
        {
            if(!playlistInitialized009||!playlist009.Enabled(i)||!PlaylistTrackAvailable009(i))return false;
            playlistRequest009=i;if(!musicTransition)CancelPlaylistPending009();
            if(!playlistEnabled009)SetMusicEnabled009(true);MusicResume015();playlistRevision009++;return true;
        }
        public void NextPlaylistTrack009()
        {
            if(!playlistInitialized009)return;playlistRequest009=-2;
            if(!musicTransition)CancelPlaylistPending009();StartAudioFromGesture();playlistRevision009++;
        }
        void SavePlaylistPrefs009()
        {
            PlayerPrefs.SetInt(PlaylistPrefs009+"Disabled",(int)playlist009.Disabled);PlayerPrefs.SetInt(PlaylistPrefs009+"Liked",(int)playlist009.Liked);PlayerPrefs.SetInt(PlaylistPrefs009+"Disliked",(int)playlist009.Disliked);PlayerPrefs.SetInt(PlaylistPrefs009+"Mode",(int)playlist009.Mode);PlayerPrefs.SetInt(PlaylistPrefs009+"Enabled",playlistEnabled009?1:0);PlayerPrefs.SetInt(PlaylistPrefs009+"ShortIntros",playlistShortIntros009?1:0);PlayerPrefs.Save();
        }
        int PlaylistEnergy009()
        {if(session==null||!session.HasStarted||session.World==null||!session.World.IsCoopArena)return 0;var campaign=session.GetComponent<CoopCampaignDirector>();return campaign!=null&&campaign.Active?Mathf.Clamp(campaign.StageNumber,0,8):Mathf.Clamp(session.Wave,0,8);}
        // Existing two-source crossfade owns playback. A proposal does not consume history.
        bool TickPlaylist009()
        {
            if(!playlistInitialized009)return false;if(!playlistEnabled009||musicUserPaused015||!playlistHasGesture009||playlistAwaitGesture009)return true;
            if(playlistCycleEnded015&&playlistRequest009==-1)return true;
            if(playlistPending009>=0)
            {
                var clip=playlistPendingClip009;
                if(clip==null||!MusicCrossfadeMath009.ValidLength(clip.length)||clip.frequency<=0||clip.samples<=1||clip.loadState==AudioDataLoadState.Failed||MusicPlaylistBoundary009.Expired(clip.loadState==AudioDataLoadState.Loaded,Time.unscaledTime,playlistLoadDeadline009))
                {
                    int failed=playlistPending009;bool advance=playlistPendingManual009&&!playlistPendingReplay015;
                    CancelPlaylistPending009();playlistFailed009|=1u<<failed;
                    if(advance)playlistRequest009=-2;playlistRevision009++;return true;
                }
                if(clip.loadState!=AudioDataLoadState.Loaded)return true;
                if(!playlist009.CanCommit(playlistPending009,PlaylistAvailable009,playlistPendingReplay015))
                {CancelPlaylistPending009();playlistRevision009++;return true;}
                if(!MusicPlaylistBoundary009.Admit(playlistPendingManual009,music.clip!=null&&music.isPlaying,music.clip!=null?MusicRemaining009(music):0))return true;
                music.loop=false;float priorGain=musicGain;
                if(BeginMusicTransition(clip,false))
                {
                    if(!MusicPlaylistBoundary009.ObservedStart(musicTransition,music.isPlaying,musicNext.isPlaying))
                    {
                        // A blocked start keeps its proposal; only a fresh explicit gesture retries admission.
                        if(musicTransition){musicNext.Stop();musicNext.clip=null;musicTransition=false;musicGain=priorGain;musicNextGain=0;}
                        CancelMusicFade009();playlistAwaitGesture009=true;playlistRevision009++;return true;
                    }
                    playlist009.Commit(playlistPending009,PlaylistAvailable009,playlistPendingReplay015);
                    playlistPending009=-1;playlistPendingClip009=null;playlistPendingManual009=playlistPendingReplay015=false;
                    playlistCycleEnded015=false;playlistRevision009++;ReleasePlaylistLeases009();
                }
                return true;
            }
            bool manual=playlistRequest009!=-1,explicitPlay=playlistRequest009>=0;
            if(!MusicPlaylistBoundary009.Prepare(manual,playlist009.Current,music.clip!=null&&music.isPlaying,music.clip!=null?MusicRemaining009(music):0))return true;
            int next=explicitPlay?playlistRequest009:playlist009.Propose(PlaylistAvailable009,PlaylistEnergy009());
            if(next>=0&&(!playlist009.Enabled(next)||!PlaylistTrackAvailable009(next))){next=playlist009.Propose(PlaylistAvailable009,PlaylistEnergy009());explicitPlay=false;}
            playlistRequest009=-1;
            if(next<0)
            {
                // Let the last recording finish. Exhaustion never turns a12s prefetch boundary into an early stop/replay.
                if(manual||music==null||!music.isPlaying)RetirePlaylistCycle015();
                return true;
            }
            AudioClip selected=LoadPlaylistClip009(next);
            if(selected==null){playlistFailed009|=1u<<next;if(manual&&!explicitPlay)playlistRequest009=-2;playlistRevision009++;return true;}
            playlistPending009=next;playlistPendingClip009=selected;playlistPendingManual009=manual;playlistPendingReplay015=explicitPlay;
            playlistLoadDeadline009=Time.unscaledTime+10f;Prime(selected);playlistRevision009++;return true;
        }
        AudioClip LoadPlaylistClip009(int i)
        {
            if(!playlist009.Enabled(i)||!PlaylistTrackAvailable009(i))return null;
            if(i==0&&!PlaylistOriginalVerified009)return menuMusicClips[0];
            ReleasePlaylistLeases009();
            var clip=playlistLeases009[i];if(clip==null){clip=Resources.Load<AudioClip>(MusicPlaylistCatalog009.Tracks[i].Resource);playlistLeases009[i]=clip;}return clip;
        }
        void CancelPlaylistPending009()
        {playlistPending009=-1;playlistPendingClip009=null;playlistPendingManual009=playlistPendingReplay015=playlistAwaitGesture009=false;ReleasePlaylistLeases009();}
        void PlaylistGesture009(){playlistHasGesture009=true;if(!playlistAwaitGesture009)return;playlistAwaitGesture009=false;playlistLoadDeadline009=Time.unscaledTime+10;playlistRevision009++;}
        void PlaylistFadeCompleted009(){playlistRevision009++;ReleasePlaylistLeases009();}
        void ReleasePlaylistLeases009()
        {
            // Cold boundary only, exclusive optional music resources. Never release borrowed legacy clips.
            for(int i=0;i<playlistLeases009.Length;i++)
            {
                var clip=playlistLeases009[i];if(clip==null||clip==playlistPendingClip009||music!=null&&music.clip==clip||musicNext!=null&&musicNext.clip==clip)continue;
                playlistLeases009[i]=null;Resources.UnloadAsset(clip);
            }
        }
        void StopPlaylist009()
        {
            CancelMusicFade009();
            if(music!=null){music.Stop();music.clip=null;}if(musicNext!=null){musicNext.Stop();musicNext.clip=null;}
            musicTransition=musicSuspended=false;musicGain=1;musicNextGain=0;playlistPending009=-1;playlistPendingClip009=null;
            playlistPendingManual009=playlistPendingReplay015=playlistAwaitGesture009=false;ReleasePlaylistLeases009();playlistRevision009++;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing non-spatial two-source music only; campaign energy read cannot mutate gameplay.
// Array Formats: Fixed18 queue/reference slots,18-bit consumed/available/preferences masks and bounded pending/request/fade/pause scalars. No PCM copy or new sources.
// Dependencies/Pointers: Actual observed playback commits once. Loading never loops/repeats the outgoing clip; last track reaches its natural end. Music Off/On preserves samples/history, focus/user-pause remain independent. Root owns import/browser/listening/performance acceptance.

