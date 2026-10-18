using System;
namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure canonical0..7 lane admission scan. Existing phase/profile/timing data
    // is reused unchanged; no transform, clock owner or native storage here.
    public static class MaskedDefensePacing
    {
        public static int FindLane(int[] alive,int[] admitted,int mask,int cursor,int phase)
        {
            if(alive==null||admitted==null||alive.Length<8||admitted.Length<8||
                !CoopPartyCapacity.ValidLaneMask(mask)||cursor<0||(uint)phase>=CurrentDefensePacing.PhaseCount)return -1;
            for(int step=0;step<8;step++)
            {int lane=((cursor&7)+step)&7;if(CoopPartyCapacity.LaneActive(mask,lane)&&CurrentDefensePacing.CanAdmitLane(alive[lane],admitted[lane],phase))return lane;}
            return -1;
        }
    }
}
