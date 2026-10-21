using System;

namespace OdysseyOracle
{
    public enum CampaignBookResult020 { None, Complete, Skipped, Cancelled }

    /// <summary>Fixed ten-page presentation state; no game or saved progression authority.</summary>
    public sealed class CampaignBookState020
    {
        public const int PageCount=10;
        readonly float[] holds=new float[PageCount];
        public int Page{get;private set;}
        public float Elapsed{get;private set;}
        public bool Active{get;private set;}
        public bool Paused{get;private set;}
        public uint Revision{get;private set;}
        public CampaignBookResult020 Result{get;private set;}
        public bool MarkSeen=>Result==CampaignBookResult020.Complete||Result==CampaignBookResult020.Skipped;

        public void SetHold(int index,float seconds)
        {
            if(index<0||index>=PageCount||!Finite(seconds)||seconds<=0f)throw new ArgumentOutOfRangeException();
            holds[index]=seconds;
        }
        public bool Begin()
        {
            for(int i=0;i<PageCount;i++)if(holds[i]<=0f)return false;
            Active=true;Paused=false;Result=CampaignBookResult020.None;Page=0;Elapsed=0f;Revision++;return true;
        }
        public bool Tick(float delta,bool focused,bool suspended,bool voicePending,bool voicePlaying)
        {
            if(!Active)return false;
            if(!focused||suspended){Pause();return false;}
            if(Paused||voicePending||!Finite(delta)||delta<=0f)return false;
            Elapsed+=Math.Min(delta,.1f);
            if(Elapsed<holds[Page]||voicePlaying)return false;
            return Next();
        }
        public bool Next()
        {
            if(!Active)return false;
            if(Page==PageCount-1){End(CampaignBookResult020.Complete);return false;}
            Page++;Elapsed=0f;Revision++;return true;
        }
        public bool Previous()
        {
            if(!Active||Page==0)return false;
            Page--;Elapsed=0f;Revision++;return true;
        }
        public void Pause(){if(Active&&!Paused){Paused=true;Revision++;}}
        public bool Resume(){if(!Active||!Paused)return false;Paused=false;Revision++;return true;}
        public void Skip(){if(Active)End(CampaignBookResult020.Skipped);}
        public void Cancel(){if(Active)End(CampaignBookResult020.Cancelled);}
        void End(CampaignBookResult020 result){Active=false;Paused=false;Result=result;Revision++;}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }

    [Serializable]
    public sealed class CampaignBookData020
    {
        public int schemaVersion;
        public float minimumPageSeconds,voiceTailSeconds,drawRevealSeconds,colorRevealSeconds;
        public CampaignBookFrame020[] frames;
    }
    [Serializable]
    public sealed class CampaignBookFrame020
    {
        public int index;
        public string title,text,imageResource,voiceResource;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure page index, bounded presentation seconds and explicit terminal result; no Unity/world/progression coordinates.
// Array Formats: Fixed10hold scalars and one page state; ten cold JSON frame records share original strings/resource keys.
// Dependencies/Pointers: HUD owns accepted actions, loaded media and seen persistence. Focus suspension never resumes; only Complete/explicit Skip requests a seen mark.
