using System;
namespace OdysseyOracle
{
    public enum GuidePlaybackStatus015 { Idle,Pending,Playing,Paused,Ended }
    public sealed class GuidePlaybackState015
    {
        public const int StepCapacity=32,HistoryCapacity=12;
        readonly int[] positions=new int[StepCapacity];readonly byte[] history=new byte[HistoryCapacity];
        uint autoMask;ushort historyMask;int historyCount;double deadline;
        public int LiveKey{get;private set;}=-1;public int ViewKey{get;private set;}=-1;
        public GuidePlaybackStatus015 Status{get;private set;}
        public uint Revision{get;private set;}
        public int HistoryCount=>historyCount;
        public bool HasView=>Valid(ViewKey);
        public bool IsReview=>HasView&&ViewKey!=LiveKey;
        public int SavedSamples=>HasView?positions[ViewKey]:0;
        public void Activate(int key,double now)
        {
            if(!Valid(key)||LiveKey==key||double.IsNaN(now)||double.IsInfinity(now))return;LiveKey=ViewKey=key;Status=GuidePlaybackStatus015.Idle;
            if(key<HistoryCapacity&&(historyMask&(1<<key))==0&&historyCount<HistoryCapacity){history[historyCount++]=(byte)key;historyMask|=(ushort)(1<<key);}
            Request(true,now);Revision++;
        }
        public bool Request(bool automatic,double now)
        {
            if(!HasView||double.IsNaN(now)||double.IsInfinity(now))return false;
            uint bit=1u<<ViewKey;if(automatic&&(autoMask&bit)!=0)return false;if(automatic)autoMask|=bit;
            Status=GuidePlaybackStatus015.Pending;deadline=now+3d;Revision++;return true;
        }
        public bool TryAdmit(double now,bool ready)
        {
            if(Status!=GuidePlaybackStatus015.Pending)return false;
            if(double.IsNaN(now)||double.IsInfinity(now)||now>deadline){Status=GuidePlaybackStatus015.Idle;Revision++;return false;}
            if(!ready)return false;Status=GuidePlaybackStatus015.Playing;Revision++;return true;
        }
        public void Save(int samples){if(HasView&&samples>=0)positions[ViewKey]=samples;}
        public void Pause(int samples){Save(samples);if(HasView){Status=GuidePlaybackStatus015.Paused;Revision++;}}
        public void End(){if(HasView){Status=GuidePlaybackStatus015.Ended;positions[ViewKey]=0;Revision++;}}
        public void CancelPending(){if(Status==GuidePlaybackStatus015.Pending){Status=GuidePlaybackStatus015.Paused;Revision++;}}
        public bool CanReview(int direction)=>TryReviewTarget(direction,out _);
        public bool TryReviewTarget(int direction,out int key)
        {
            key=-1;if(direction!=-1&&direction!=1||historyCount==0)return false;
            int index=historyCount;for(int i=0;i<historyCount;i++)if(history[i]==ViewKey){index=i;break;}
            int next=index+direction;if(next<0||next>=historyCount)return false;key=history[next];return true;
        }
        public void View(int key){if(!Valid(key))return;ViewKey=key;Status=GuidePlaybackStatus015.Paused;Revision++;}
        public void ReturnLive(){if(Valid(LiveKey)&&ViewKey!=LiveKey)View(LiveKey);}
        public void Reset(){Array.Clear(positions,0,positions.Length);Array.Clear(history,0,history.Length);autoMask=0;historyMask=0;historyCount=0;deadline=0;LiveKey=ViewKey=-1;Status=GuidePlaybackStatus015.Idle;Revision++;}
        static bool Valid(int key)=>key>=0&&key<StepCapacity;
    }
    public sealed class MenuAncestryState015
    {
        public const int Capacity=16;readonly int[] parents=new int[Capacity];int count;
        public int Count=>count;
        public void Navigate(int from,int to,int none,int start)
        {
            if(from==to)return;if(to==none||to==start){Clear();return;}
            for(int i=count-1;i>=0;i--)if(parents[i]==to){count=i;return;}
            if(count==Capacity)count=0;parents[count++]=from;
        }
        public int Back(int fallback)=>count>0?parents[--count]:fallback;
        public void Clear(){count=0;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure per-tutorial audio presentation keys and menu ancestry; no Unity/world/progression state.
// Array Formats: Fixed32sample positions/12opened-card history/32bit auto mask/16menu parents, cold arrays only.
// Dependencies/Pointers: Canonical Tutorial reset starts a new narration run. Audio completion never acknowledges a lesson; focus cannot queue or replay; bounded readiness intent expires once.
