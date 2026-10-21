namespace OdysseyOracle
{
    public sealed partial class TutorialDirector
    {
        const int GuideUiStatusCount020=5;
        static readonly string[] guideUiLabels020=BuildGuideUiLabels020();
        static void PrepareGuideUi020(){if(guideUiLabels020.Length!=65)throw new System.InvalidOperationException("Guide label table is invalid.");}
        static string[] BuildGuideUiLabels020()
        {
            var labels=new string[13*GuideUiStatusCount020];
            string[] statuses={"READY","PLAYING","PAUSED","WAITING","NO AUDIO"};
            for(int card=0;card<=12;card++)
            {
                string prefix=card==0?"PRACTICE":"GUIDE "+card+" / 12";
                for(int state=0;state<GuideUiStatusCount020;state++)labels[card*GuideUiStatusCount020+state]=prefix+" · "+statuses[state];
            }
            return labels;
        }
        public int GuideCardNumber020=>guidePlayback015.HasView&&guidePlayback015.ViewKey<12?guidePlayback015.ViewKey+1:0;
        public bool GuideReady020=>GuideAudioReady015;
        int GuideUiStatus020=>!GuideAudioAvailable015?4:GuidePlaying015?1:guidePlayback015.Status==GuidePlaybackStatus015.Pending?3:GuideReady020?0:2;
        public string GuideCounterStatus020=>guideUiLabels020[GuideCardNumber020*GuideUiStatusCount020+GuideUiStatus020];
        public string GuidePlaybackLabel020=>GuidePlaying015||guidePlayback015.Status==GuidePlaybackStatus015.Pending?"PAUSE":guidePlayback015.Status==GuidePlaybackStatus015.Ended?"REPLAY":"PLAY";
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted guide card identity/audio status only; practice is not fabricated completed progress.
// Array Formats: Cold fixed65label strings for13identities×5states; existing guide arrays/source remain canonical.
// Dependencies/Pointers: Real readiness, playback and current view select immutable labels; no frame formatting, audio action, acknowledgement or progression change. Root verifies actual UI/audio/browser behavior.
