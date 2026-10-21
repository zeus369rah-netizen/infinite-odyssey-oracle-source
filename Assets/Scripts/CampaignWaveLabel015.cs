namespace OdysseyOracle
{
    // Bounded scalar presentation cache; never owns or mutates accepted state.
    public sealed class CampaignWaveLabel015
    {
        bool initialized,shownCampaign,shownRunning;
        int shownStage,shownWave,shownAdmitted,shownQuota,shownAlive;
        public string Text{get;private set;}="WAITING FOR DEFENSE";
        public static bool ValidCampaignWave(int stage,int wave,int admitted,int quota)
        {
            int size=CampaignBalance015.WavesPerMilestone;
            return stage>=1&&stage<=CampaignBalance015.WaveCount/size&&CampaignBalance015.ValidWave(wave)&&
                (wave-1)/size+1==stage&&quota==CampaignBalance015.Admissions(wave)&&admitted>=0&&admitted<=quota;
        }
        public bool Update(bool campaign,bool running,int stage,int wave,int admitted,int quota,int alive)
        {
            if(initialized&&shownCampaign==campaign&&shownRunning==running&&shownStage==stage&&shownWave==wave&&shownAdmitted==admitted&&shownQuota==quota&&shownAlive==alive)return false;
            initialized=true;shownCampaign=campaign;shownRunning=running;shownStage=stage;shownWave=wave;shownAdmitted=admitted;shownQuota=quota;shownAlive=alive;
            bool valid=campaign?ValidCampaignWave(stage,wave,admitted,quota):wave>0&&quota>0&&admitted>=0&&admitted<=quota;
            Text=!running?"WAITING FOR DEFENSE":!valid||alive<0||alive>CampaignBalance015.MaximumAlive?"WAITING FOR ACCEPTED WAVE":
                "WAVE "+wave+(campaign?" / "+CampaignBalance015.WaveCount:"")+"  ·  "+admitted+" / "+quota+" ADMITTED\n"+alive+" ENEMIES ALIVE";
            return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted scalar global wave/milestone counts; no world or UnityEngine object access.
// Array Formats: One cold cache,seven input scalars/flags; formatting only after change, no frame allocations for stable inputs.
// Dependencies/Pointers: Frozen CampaignBalance015 forty-wave policy; no readiness, authority, progression, reward or protocol mutation.
