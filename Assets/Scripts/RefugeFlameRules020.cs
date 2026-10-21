using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace OdysseyOracle
{
    public static class RefugeFlameRules020
    {
        public const uint EligibleFlag=2u;
        public const string PreferenceKey="Oracle.RefugeFlames.v1";
        public const int AchievementRow=20,DisplayCount=21;
        public const string AchievementName="Lights out";
        public const string AchievementNotice="ACHIEVEMENT · Lights out";
        public static bool Visual(RoomReactionKind kind)=>kind==RoomReactionKind.Candle||kind==RoomReactionKind.VoxelFlame||
            kind==RoomReactionKind.LivingFlame||kind==RoomReactionKind.Lantern||kind==RoomReactionKind.TorchGlow||kind==RoomReactionKind.Hearth;
        public static bool MainRoom(float3 point,float halfWidth)=>math.all(math.isfinite(point))&&math.isfinite(halfWidth)&&halfWidth>=7.5f&&halfWidth<=9f&&
            math.abs(point.x)<=halfWidth+.001f&&point.z>=TutorialRoomV2Layout.StartZ-.001f&&point.z<=TutorialRoomV2Layout.EndZ+.001f;
        public static bool Eligible(RoomReactionKind kind,uint flags)=>Visual(kind)&&(flags&1u)==0&&(flags&EligibleFlag)!=0;
        public static bool Completed(int ingredient,uint count,uint before,uint after)=>ingredient==8&&count>0&&before>0&&before<=count&&after==0;
        public static bool ValidStored(int value)=>value==0||value==1;
        public static bool Newer(uint value,uint previous)=>value!=previous&&unchecked(value-previous)<0x80000000u;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)] public struct RoomReactionAccepted24
    {
        public uint Generation,Revision,Extinguished,EligibleCount,BeforeLit,AfterLit;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)] public struct RefugeFlameEvent56
    {
        public uint Epoch,Generation,Revision,Shot;
        public ulong Caster,LayoutHash;
        public float3 Position;
        public uint Extinguished,EligibleCount,Flags;
        public bool Complete=>Flags==1;
    }
    public struct RefugeFlameReceipt020
    {
        uint epoch,generation,revision;
        bool known;
        public void Reset(){epoch=generation=revision=0;known=false;}
        public bool Accept(uint nextEpoch,uint nextGeneration,uint nextRevision)
        {
            if(nextGeneration==0||nextRevision==0)return false;
            if(known&&nextEpoch==epoch)
            {
                if(nextGeneration==generation&&!RefugeFlameRules020.Newer(nextRevision,revision))return false;
                if(nextGeneration!=generation&&!RefugeFlameRules020.Newer(nextGeneration,generation))return false;
            }
            epoch=nextEpoch;generation=nextGeneration;revision=nextRevision;known=true;return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: pure main-refuge local rectangle and accepted epoch/generation/revision identities; no Object or physics API.
// Array Formats: existing Node32 flags bit1 marks initially lit main visual flames; Accepted24 stack value, explicit56B event, fixed scalar monotonic receipt and independent int preference key.
// Dependencies/Pointers: only existing host mutation creates completion; snapshot/reset cannot award. Legacy20rows/bits and Frame112 unchanged; callers validate current authority/epoch/layout before receipt acceptance.
