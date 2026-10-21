using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        readonly UnityEngine.UI.Text[] musicTitles009=new UnityEngine.UI.Text[MusicPlaylistCatalog009.Count];
        readonly UnityEngine.UI.Button[] musicEnabledButtons009=new UnityEngine.UI.Button[MusicPlaylistCatalog009.Count],musicLikeButtons009=new UnityEngine.UI.Button[MusicPlaylistCatalog009.Count],musicDislikeButtons009=new UnityEngine.UI.Button[MusicPlaylistCatalog009.Count],musicPlayButtons009=new UnityEngine.UI.Button[MusicPlaylistCatalog009.Count];
        readonly UnityEngine.UI.Text[] musicEnabledLabels009=new UnityEngine.UI.Text[MusicPlaylistCatalog009.Count],musicLikeLabels009=new UnityEngine.UI.Text[MusicPlaylistCatalog009.Count],musicDislikeLabels009=new UnityEngine.UI.Text[MusicPlaylistCatalog009.Count];
        readonly string[] musicMetadata009=new string[MusicPlaylistCatalog009.Count];
        readonly UnityEngine.UI.Button[] musicModes009=new UnityEngine.UI.Button[3];
        UnityEngine.UI.Text musicCurrent009,musicLibraryNote009,musicToggleLabel009,musicIntroLabel009;
        UnityEngine.UI.Button musicToggle009,musicNext009,musicPause015;
        UnityEngine.UI.Text musicPauseLabel015;
        int musicUiRevision009=int.MinValue;
        bool musicUiOpen009;
        void BuildMusicLibrary009()
        {
            var panel=PanelContent(JourneyPanel.MusicLibrary009,"MusicLibrary009",new Vector2(1040,1020));
            MenuLabel(panel,"MusicLibraryTitle009","MUSIC LIBRARY",32,Ivory,54);
            var controls=MusicLibraryRow009(panel,"MusicTransport009",40);
            musicToggle009=MenuButton(controls,"MusicOnOff009","MUSIC: ON",()=>{var audio=session.Audio;if(audio!=null)audio.SetMusicEnabled009(!audio.MusicEnabled009);RefreshMusicLibrary009(true);},40);musicToggleLabel009=musicToggle009.GetComponentInChildren<UnityEngine.UI.Text>();
            musicNext009=MenuButton(controls,"MusicNext009","SKIP SONG",()=>{session.Audio?.NextPlaylistTrack009();RefreshMusicLibrary009(true);},40);
            musicPause015=MenuButton(controls,"MusicPause015","PAUSE MUSIC",()=>{session.Audio?.ToggleMusicPause015();RefreshMusicLibrary009(true);},40);musicPauseLabel015=musicPause015.GetComponentInChildren<UnityEngine.UI.Text>();
            var intro=MenuButton(panel,"MusicShortIntros009","SHORT INTRO TRANSITIONS: ON",()=>{var audio=session.Audio;if(audio!=null)audio.SetPlaylistShortIntros009(!audio.PlaylistShortIntros009);RefreshMusicLibrary009(true);},36);musicIntroLabel009=intro.GetComponentInChildren<UnityEngine.UI.Text>();
            var modes=MusicLibraryRow009(panel,"MusicModes009",38);
            for(int i=0;i<3;i++){int index=i;musicModes009[i]=MenuButton(modes,"MusicMode009_"+i,((MusicPlaylistMode009)i).ToString().ToUpperInvariant(),()=>{session.Audio?.SetPlaylistMode009((MusicPlaylistMode009)index);RefreshMusicLibrary009(true);},38);}
            musicCurrent009=MenuLabel(panel,"MusicNow009","Waiting for the current player.",17,Gold,60);
            var content=ScrollingContent(panel,"MusicLibraryMaskedScroll009",520,out _);
            for(int i=0;i<MusicPlaylistCatalog009.Count;i++)
            {
                int index=i;var track=MusicPlaylistCatalog009.Tracks[i];musicMetadata009[i]=i==0?"MAIN THEME · opening and fallback":track.Seconds+"s · "+(track.Bpm>0?"~"+track.Bpm+" BPM":"tempo untagged")+(track.Bpm>0?" · energy "+track.Energy+"/8":" · neutral mood");
                var row=Rect(content,"MusicTrack009_"+i,Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(0,102));Height(row,102);
                var layout=row.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=4;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
                musicTitles009[i]=MenuLabel(row,"TrackTitle009_"+i,track.Title,17,Ivory,54);
                var actions=MusicLibraryRow009(row,"MusicTrackActions009_"+i,36);
                musicEnabledButtons009[i]=MenuButton(actions,"MusicEnabled009_"+i,"ENABLED",()=>{var audio=session.Audio;if(audio!=null)audio.SetPlaylistTrackEnabled009(index,!audio.PlaylistTrackEnabled009(index));RefreshMusicLibrary009(true);},36);
                musicLikeButtons009[i]=MenuButton(actions,"MusicLike009_"+i,"LIKE",()=>{var audio=session.Audio;if(audio!=null)audio.SetPlaylistRating009(index,audio.PlaylistRating009(index)==1?0:1);RefreshMusicLibrary009(true);},36);
                musicDislikeButtons009[i]=MenuButton(actions,"MusicDislike009_"+i,"DISLIKE",()=>{var audio=session.Audio;if(audio!=null)audio.SetPlaylistRating009(index,audio.PlaylistRating009(index)==-1?0:-1);RefreshMusicLibrary009(true);},36);
                musicPlayButtons009[i]=MenuButton(actions,"MusicPlay009_"+i,"PLAY",()=>{session.Audio?.PlayPlaylistTrack009(index);RefreshMusicLibrary009(true);},36);
                musicEnabledLabels009[i]=musicEnabledButtons009[i].GetComponentInChildren<UnityEngine.UI.Text>();musicLikeLabels009[i]=musicLikeButtons009[i].GetComponentInChildren<UnityEngine.UI.Text>();musicDislikeLabels009[i]=musicDislikeButtons009[i].GetComponentInChildren<UnityEngine.UI.Text>();
            }
            musicLibraryNote009=MenuLabel(panel,"MusicAvailability009","New recordings remain pending until verified and imported.",14,Muted,92);
            MenuButton(panel,"MusicLibraryBack009","BACK TO AUDIO",()=>SetPanel(JourneyPanel.Audio),44);
        }
        RectTransform MusicLibraryRow009(RectTransform parent,string name,float height)
        {
            var row=Rect(parent,name,Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(0,height));Height(row,height);
            var layout=row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;return row;
        }
        void OpenMusicLibrary009(){session.Audio?.StartAudioFromGesture();musicUiRevision009=int.MinValue;SetPanel(JourneyPanel.MusicLibrary009);RefreshMusicLibrary009(true);}
        void RefreshMusicLibrary009(bool force=false)
        {
            bool open=currentPanel==JourneyPanel.MusicLibrary009;if(!open){musicUiOpen009=false;return;}
            var audio=session.Audio;if(audio==null||musicCurrent009==null)return;
            if(!force&&musicUiOpen009&&musicUiRevision009==audio.PlaylistRevision009)return;
            musicUiOpen009=true;musicUiRevision009=audio.PlaylistRevision009;musicToggleLabel009.text=audio.MusicEnabled009?"MUSIC: ON":"MUSIC: OFF";musicNext009.interactable=audio.MusicEnabled009;musicPause015.interactable=audio.MusicEnabled009;musicPauseLabel015.text=audio.MusicPaused015?"RESUME MUSIC":"PAUSE MUSIC";
            musicIntroLabel009.text=audio.PlaylistShortIntros009?"SHORT INTRO TRANSITIONS: ON":"SHORT INTRO TRANSITIONS: OFF";
            for(int i=0;i<3;i++)musicModes009[i].interactable=audio.PlaylistMode009!=(MusicPlaylistMode009)i;
            int current=audio.PlaylistCurrent009,ready=0,eligible=0;
            for(int i=0;i<MusicPlaylistCatalog009.Count;i++)
            {
                bool enabled=audio.PlaylistTrackEnabled009(i),available=audio.PlaylistTrackAvailable009(i);int rating=audio.PlaylistRating009(i);if(i>0&&available)ready++;if(enabled&&available)eligible++;
                musicTitles009[i].text=(current==i&&audio.PlaylistPlaying009?"NOW · ":"")+audio.PlaylistTitle009(i)+"\n"+musicMetadata009[i]+(available?"":" · PENDING / UNAVAILABLE");musicTitles009[i].color=available?Ivory:Muted;
                musicEnabledLabels009[i].text=enabled?"ENABLED":"DISABLED";musicLikeLabels009[i].text=rating==1?"LIKED":"LIKE";musicDislikeLabels009[i].text=rating==-1?"DISLIKED":"DISLIKE";musicPlayButtons009[i].interactable=enabled&&available;
            }
            musicCurrent009.text=!audio.MusicEnabled009?"Music is off. Turning it on resumes its saved position.":audio.MusicPaused015?"Music paused. Resume keeps its position and queue.":eligible==0?"No enabled, available recordings. Enable a track to play music.":audio.PlaylistAwaitGesture009?"Audio did not start. Click PLAY to try again.":audio.PlaylistTransition009?"CROSSFADING · "+audio.PlaylistTitle009(current):audio.PlaylistPlaying009?"NOW · "+audio.PlaylistTitle009(current):audio.PlaylistCycleEnded015?"Cycle complete. Choose PLAY to replay, or Shuffle for a new cycle.":"Preparing an enabled recording.";
            musicLibraryNote009.text=ready+" / "+(MusicPlaylistCatalog009.Count-1)+" new recordings available. Pending tracks stay silent.\nSequential / Adaptive play each eligible track once, then stop. Shuffle repeats only after the complete cycle. Ratings affect Shuffle / Adaptive.\nShort intros skip up to2.5s on later plays, with a3s blend. Opening main starts at0. Original recordings stay unchanged.";
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing local Canvas/uGUI panel/scroll layout only; existing Music Library entry is Audio Settings only, never gameplay input/authority.
// Array Formats: Eighteen stable cold-created masked rows/cached labels/buttons, three mode buttons and scalar revision gate. No per-frame strings/hierarchy rebuilds or clip/source previews.
// Dependencies/Pointers: GameAudioDirector owns real two-source playback/prefs/availability. UI submits ordinary player requests, never loads audio or relabels missing recordings as completed. Distinct MusicLibrary009 enum insertion composes with MechanicsGuide.
