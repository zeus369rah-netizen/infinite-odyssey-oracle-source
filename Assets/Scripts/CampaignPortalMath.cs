using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CampaignPortalPoint16{public float X,Y,Z;public int Lane;}
    public interface ICampaignPortalPoints{int Count{get;}CampaignPortalPoint16 At(int index);}
    public static class CampaignPortalMath
    {
        public const int Capacity=8;
        public const float LocalReach=3.25f,HostReach=3.4f;
        public static string RefugeBlockedReason(bool memberPresent,int visits,int towers,bool firstStageCoreLoadout,int stage,bool ready,int readyCount,int members)
        {
            if(!memberPresent)return "Waiting for accepted party membership.";
            if(!(firstStageCoreLoadout&&stage==1))
            {
                if((visits&CampaignQuestState.RequiredVisits)==0)return "Visit the Tower Anvil and Raykon before readying for the next level.";
                if((visits&CampaignQuestState.SmithRole)==0)return "Visit the Tower Anvil before readying for the next level.";
                if((visits&CampaignQuestState.RunekeeperRole)==0)return "Speak to Raykon before readying for the next level.";
            }
            if(towers!=CampaignQuestState.FourCoreTowers)return "Waiting for host-accepted core tower permission.";
            if(!ready)return "Return to the refuge portal and ready for the next level.";
            return readyCount<members?"Waiting for every current party member to ready.":"Waiting for the actual shared arena preparation barrier.";
        }
        public static int Select<T>(ref T points,int activeMask,float x,float y,float z,float reach)where T:struct,ICampaignPortalPoints
        {
            if(!CoopPartyCapacity.ValidLaneMask(activeMask)||points.Count<0||points.Count>Capacity||!Finite(x)||!Finite(y)||!Finite(z)||!Finite(reach)||reach<=0||reach>HostReach)return -1;
            int selected=-1,seen=0;double nearest=(double)reach*reach;
            for(int i=0;i<points.Count;i++)
            {
                var p=points.At(i);if(p.Lane==-1)continue;
                if(p.Lane<0||p.Lane>=8||!Finite(p.X)||!Finite(p.Y)||!Finite(p.Z)||(seen&(1<<p.Lane))!=0)return -1;
                seen|=1<<p.Lane;if((activeMask&(1<<p.Lane))==0)continue;
                double dx=(double)x-p.X,dy=(double)y-p.Y,dz=(double)z-p.Z,distance=dx*dx+dy*dy+dz*dz;
                if(distance<nearest||distance==nearest&&(selected<0||p.Lane<selected)){nearest=distance;selected=p.Lane;}
            }
            return selected;
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual canonical monster portal anchors and accepted peer feet in world metres/Y-up; squared reach only.
// Array Formats: Up to8 blittable PortalPoint16 records; scalar active mask/nearest lane, deterministic lower-lane tie. No allocation/Unity APIs.
// Dependencies/Pointers: Pure constrained storage math, called by cold Burst job; caller supplies actual World anchors and separately owns phase/authority/LOS validation.
