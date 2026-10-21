using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class TutorialDirector
    {
        readonly AudioClip[] trainingLabHelpClips=new AudioClip[TrainingLabVoiceGuides.Count];
        int trainingLabNarration=-1;
        public string TrainingLabNarrationCaption=>TrainingLabVoiceGuides.Caption(trainingLabNarration);
        public bool TrainingLabNarrationAvailable(int guide)=>guide>=0&&guide<trainingLabHelpClips.Length&&trainingLabHelpClips[guide]!=null;
        bool TrainingLabNarrationScope=>isActiveAndEnabled&&Finished&&!active&&session!=null&&session.HasStarted&&
            !session.IsDead&&!session.IsDowned&&!session.Victory&&!session.CoopMode&&(session.Rooms==null||!session.Rooms.Connected)&&session.TrainingLab!=null&&session.TrainingLab.CanOpen&&
            session.Hud!=null&&session.Hud.IsTrainingLabVisible;

        void InitializeTrainingLabNarration()
        {
            for(int i=0;i<trainingLabHelpClips.Length;i++)
                trainingLabHelpClips[i]=Resources.Load<AudioClip>(TrainingLabVoiceGuides.Resource(i));
        }
        public bool TryPlayTrainingLabNarration(int guide)
        {
            if(!TrainingLabNarrationScope||!TrainingLabNarrationAvailable(guide)||voice==null||
                !Application.isFocused||applicationPaused)return false;
            if(trainingLabNarration==guide&&voice.isPlaying)return false;
            // One guide source only. Retire any legacy proximity greeting before explicit lab help.
            session.RefugeActivities?.UseGuideStationNarration();
            StopVoice();trainingLabNarration=guide;
            Subtitle=narrationLine=TrainingLabVoiceGuides.Caption(guide);
            voice.clip=trainingLabHelpClips[guide];voice.Play();SyncVoice();return true;
        }
        public void StopTrainingLabNarration()
        {
            if(trainingLabNarration<0)return;
            StopVoice();Subtitle=narrationLine="";
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing visible eligible offline lab modal; no actor/world/casting mutation.
// Array Formats: Two cached AudioClip references loaded cold and one scalar selected-caption index.
// Dependencies/Pointers: Reuses TutorialDirector's sole guide AudioSource and saved guide-volume/focus policy.
// Explicit UI actions play/switch; panel/context exit stops. Root integrates SyncVoice scope guards; no new source.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge world metres/Y-up; host-owned shared lab, model local+Z.
// Array Formats: Two native peer records and bounded fixed frames; inherited48 projectile pool and two real actors.
// Dependencies/Pointers: Dedicated shared owner validates requests/fences; guests remote-only; no rewards, migration or narrator; root integrates complete slice.
