using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Refuge-only scalar DTO. No arena HP/selection/warning state is synthesized.
    // Existing007 full-personal health validation remains the arena contract.
    [Serializable]
    public sealed class CampaignProgressSnapshot
    {
        public ulong id;
        public int revision, level, xp, points, mask;
        public int forgeVersion, forgeBank, forgeTiers, forgeRevision, forgeOutcome;
        public uint forgeLastRequest;

        public static bool Valid(CampaignProgressSnapshot s)
        {
            if (s == null || s.revision < 1 || s.level < 1 || s.level > ElementProgression.MaximumLevel || s.xp < 0 || s.points < 0 ||
                s.mask < 0 || s.mask > 4095 || s.forgeVersion != MatchTowerForgeRules.Version ||
                !new MatchForgeSnapshot(s.forgeVersion,s.forgeBank,s.forgeTiers,s.forgeRevision).Valid ||
                (uint)s.forgeOutcome > (uint)MatchForgeOutcome.RateLimited ||
                (s.forgeLastRequest == 0) != (s.forgeOutcome == 0)) return false;
            int spent = 0;
            for (int element=0;element<4;element++)
            {
                int branch=(s.mask>>(element*3))&7;
                if(branch!=0&&branch!=1&&branch!=3&&branch!=7)return false;
                for(int bit=0;bit<3;bit++)if((branch&(1<<bit))!=0)spent++;
            }
            if((long)s.points+spent>s.level-1)return false;
            return s.level==ElementProgression.MaximumLevel?s.xp==0:s.xp<CampaignBalance015.XpThreshold(s.level);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world and accepted015 campaign/source identity, original arena positions retained.
// Array Formats: Eight milestones/40waves/905logical rewards within4096native slots, fixed128B XP channel and existing bounded pools.
// Dependencies/Pointers: Root composes actual current pool/audio owners with reviewed campaign/XP hunks; source/native/browser/performance/public acceptance remain separate.
