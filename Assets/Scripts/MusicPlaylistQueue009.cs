using System;

namespace OdysseyOracle
{
    public enum MusicPlaylistMode009 { Sequential,Shuffle,Adaptive }
    public readonly struct MusicTrackDefinition009
    {
        public readonly string Id,Title,Resource;
        public readonly int Seconds,Bpm,Energy;
        public MusicTrackDefinition009(string id,string title,int seconds,int bpm,int energy)
        {Id=id;Title=title;Seconds=seconds;Bpm=bpm;Energy=energy;Resource="Audio/MusicExpansion009/"+id;}
    }
    public static class MusicPlaylistCatalog009
    {
        public const int Count=18;
        public const uint All=Count==32?uint.MaxValue:(1u<<Count)-1u,Long=1u<<15,ShortMid=All&~(Long|1u);
        public static readonly MusicTrackDefinition009[] Tracks={
            new MusicTrackDefinition009("sacred_horizon","Sacred Horizon",60,0,0),
            new MusicTrackDefinition009("raykons_violet_reverie","Raykon's Violet Reverie",120,76,0),
            new MusicTrackDefinition009("brams_midnight_watch","Bram's Midnight Watch",120,88,1),
            new MusicTrackDefinition009("ilyras_rune_circuit","Ilyra's Rune Circuit",120,104,2),
            new MusicTrackDefinition009("the_anvil_awakens","The Anvil Awakens",120,118,3),
            new MusicTrackDefinition009("petals_above_the_catacombs","Petals Above the Catacombs",120,124,3),
            new MusicTrackDefinition009("tidebound_spires","Tidebound Spires",120,128,4),
            new MusicTrackDefinition009("eight_gates_rising","Eight Gates Rising",60,132,5),
            new MusicTrackDefinition009("staff_of_the_starforged","Staff of the Starforged",60,138,6),
            new MusicTrackDefinition009("webs_in_the_crucible","Webs in the Crucible",60,144,7),
            new MusicTrackDefinition009("oracle_ascendant","Oracle Ascendant",60,160,8),
            new MusicTrackDefinition009("raykon_and_the_sacred_horizon","Sacred Horizon Return",60,100,2),
            new MusicTrackDefinition009("the_eightfold_crucible","The Eightfold Crucible",60,132,6),
            new MusicTrackDefinition009("lanterns_of_the_first_gate","Lanterns of the First Gate",60,112,2),
            new MusicTrackDefinition009("horizon_gate","Horizon Gate",60,0,4),
            new MusicTrackDefinition009("ritual_breaker","Ritual Breaker",240,0,4),
            new MusicTrackDefinition009("ten_stack_rampage","Ten-Stack Rampage",120,0,4),
            new MusicTrackDefinition009("push_limit","Push Limit",120,0,4)
        };
        public static uint MaskForCount(int count){if(count<1||count>32)throw new ArgumentOutOfRangeException();return count==32?uint.MaxValue:(1u<<count)-1u;}
        public static bool Valid(int index)=>index>=0&&index<Count;
    }
    public static class MusicPlaylistBoundary009
    {
        public static bool Prepare(bool forced,int current,bool playing,float remaining)=>forced||current<0||!playing||remaining<=12f;
        public static bool Admit(bool manual,bool playing,float remaining)=>manual||!playing||remaining<=MusicCrossfadeMath009.TriggerSeconds;
        public static bool Expired(bool loaded,float now,float deadline)=>!loaded&&now>=deadline;
        public static bool ObservedStart(bool transition,bool currentPlaying,bool incomingPlaying)=>transition?incomingPlaying:currentPlaying;
    }
    // Pure non-spatial selection; only actual playback admission consumes a cycle bit.
    public sealed class MusicPlaylistQueue009
    {
        public uint Disabled,Liked,Disliked;
        public MusicPlaylistMode009 Mode;
        public int Current{get;private set;}=-1;
        public int SinceLong{get;private set;}
        public int NextLong{get;private set;}
        public uint Seed{get;private set;}=0x4f726163u;
        public uint PlayedMask{get;private set;}
        int cursor;
        bool opened;
        public void Sanitize(){Disabled&=MusicPlaylistCatalog009.All;Liked&=MusicPlaylistCatalog009.All;Disliked&=MusicPlaylistCatalog009.All&~Liked;if((uint)Mode>2)Mode=MusicPlaylistMode009.Sequential;}
        public void ResetOpening(){Current=-1;SinceLong=cursor=0;NextLong=0;opened=false;PlayedMask=0;}
        static uint NextSeed(uint seed){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed==0?1:seed;}
        public bool Enabled(int i)=>MusicPlaylistCatalog009.Valid(i)&&(Disabled&(1u<<i))==0;
        public int Rating(int i)=>!MusicPlaylistCatalog009.Valid(i)?0:(Liked&(1u<<i))!=0?1:(Disliked&(1u<<i))!=0?-1:0;
        public void SetRating(int i,int rating){if(!MusicPlaylistCatalog009.Valid(i))return;uint bit=1u<<i;Liked&=~bit;Disliked&=~bit;if(rating>0)Liked|=bit;else if(rating<0)Disliked|=bit;}
        public int Propose(uint available,int energy)
        {
            uint eligible=available&MusicPlaylistCatalog009.All&~Disabled;
            uint remaining=eligible&~PlayedMask;
            if(remaining==0)
            {
                if(Mode!=MusicPlaylistMode009.Shuffle||eligible==0)return -1;
                remaining=eligible;
                if(MusicPlaylistCatalog009.Valid(Current)&&(remaining&~(1u<<Current))!=0)remaining&=~(1u<<Current);
            }
            if(!opened&&(remaining&1u)!=0)return 0;
            uint shortMask=remaining&MusicPlaylistCatalog009.ShortMid,longMask=remaining&MusicPlaylistCatalog009.Long;
            int longer=Forward(longMask,NextLong);int cadence=longer>=0&&Rating(longer)>0?2:longer>=0&&Rating(longer)<0?4:3;
            if(longMask!=0&&(SinceLong>=cadence||shortMask==0))return longer;
            if(shortMask==0)return (remaining&1u)!=0?0:-1;
            if(Mode==MusicPlaylistMode009.Sequential)
            {for(int offset=1;offset<=MusicPlaylistCatalog009.Count;offset++){int i=(cursor+offset)%MusicPlaylistCatalog009.Count;if((shortMask&(1u<<i))!=0)return i;}}
            int total=0;for(int i=0;i<MusicPlaylistCatalog009.Count;i++)if((shortMask&(1u<<i))!=0)total+=Weight(i,energy);
            int pick=(int)(NextSeed(Seed)%(uint)total);
            for(int i=0;i<MusicPlaylistCatalog009.Count;i++)if((shortMask&(1u<<i))!=0){pick-=Weight(i,energy);if(pick<0)return i;}
            return -1;
        }
        static int Forward(uint mask,int start){for(int offset=0;offset<MusicPlaylistCatalog009.Count;offset++){int i=(start+offset)%MusicPlaylistCatalog009.Count;if((mask&(1u<<i))!=0)return i;}return -1;}
        int Weight(int i,int energy)
        {int value=Rating(i)>0?4:Rating(i)<0?1:2;return Mode==MusicPlaylistMode009.Adaptive?value*(9-Math.Abs(MusicPlaylistCatalog009.Tracks[i].Energy-Math.Max(0,Math.Min(8,energy)))):value;}
        public bool CanCommit(int i,uint available,bool manual=false)
        {
            uint eligible=available&MusicPlaylistCatalog009.All&~Disabled;
            if(!Enabled(i)||(eligible&(1u<<i))==0)return false;
            uint bit=1u<<i;
            if(!manual&&(PlayedMask&bit)!=0)
            {
                if(Mode!=MusicPlaylistMode009.Shuffle||(eligible&~PlayedMask)!=0)return false;
                if(i==Current&&(eligible&~bit)!=0)return false;
            }
            return true;
        }
        public bool Commit(int i,uint available,bool manual=false)
        {
            if(!CanCommit(i,available,manual))return false;
            uint bit=1u<<i;
            if(!manual&&(PlayedMask&bit)!=0)PlayedMask=0;
            Current=i;opened=true;PlayedMask|=bit;Seed=NextSeed(Seed);
            if((MusicPlaylistCatalog009.Long&bit)!=0){SinceLong=0;NextLong=(i+1)%MusicPlaylistCatalog009.Count;}
            else if(i!=0){SinceLong=Math.Min(4,SinceLong+1);cursor=i;}
            return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure non-spatial18-track music selection; energy0..8 read only.
// Array Formats: Fixed metadata and18-bit masks, deterministic seed/cursor; no engine/native/PCM calls or hot allocation.
// Dependencies/Pointers: Successful actual playback alone commits; Sequential/Adaptive stop after eligible cycle, Shuffle refills only at exhaustion and avoids immediate repeat when alternatives exist. Manual replay never clears consumed history.

