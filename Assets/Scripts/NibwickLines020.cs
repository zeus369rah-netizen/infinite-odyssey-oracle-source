namespace OdysseyOracle
{
    public static class NibwickLines020
    {
        public const int Count=15;
        public const string FrozenDialogueSha256="11f56189cff9ed474fa940e7d488e11d017da9c2ab55e87ac8546a0aa47916ac";
        public static string Caption(int line)
        {
            switch(line)
            {
                case 1:return "Three! You're our third arrival! I'm Nibwick, keeper of the welcome bell. Welcome to Portal Landing. Mind the step. It has ambitions.";
                case 2:return "Want a look around? Shrine, tower, terrace, smith, bridges. Five stops. I counted twice.";
                case 3:return "Right, stay close. I walk faster when I'm being useful.";
                case 4:return "The Shrine hones your abilities. Take your time. Glowing confidently is no substitute for knowing what a spell does.";
                case 5:return "The Watchtower keeps our bestiary and quests. Knowing what has teeth is useful. Knowing which way they're pointing is better.";
                case 6:return "The Ruined Terrace has our party table. Gather your companions here. I repaired the wobble. Mostly.";
                case 7:return "Our smith repairs recovered Heartstone shards and stocks staves and gear. Bring back what you find. Ask before leaning on anything expensive.";
                case 8:return "That bridge leads to Lantern Town. Bloodstorm Gardens stays sealed until the final crossing is ready. The sign is very firm about it.";
                case 9:return "There. A whole town, and nobody fell off. I'll be by the welcome bell if you need me.";
                case 10:return "I'll wait here. No point giving a tour to an empty pair of boots.";
                case 11:return "There you are. Shall we pick up where we paused?";
                case 12:return "I'll keep your place. Come back when you're ready.";
                case 13:return "Of course. I'm very good at standing here. Years of practice.";
                case 14:return "The first two? Oh, that's a tale for another evening. You're here now, and the kettle is still warm.";
                case 15:return "Bloodstorm Gardens is sealed for now. The signs will tell you which crossings are open. They enjoy being obeyed.";
                default:return "";
            }
        }
        public static string Resource(int line)
        {
            switch(line)
            {
                case 1:return "CampaignHub020/Nibwick/Voice/greeting01";
                case 2:return "CampaignHub020/Nibwick/Voice/tour_offer01";
                case 3:return "CampaignHub020/Nibwick/Voice/tour_start01";
                case 4:return "CampaignHub020/Nibwick/Voice/tour_shrine01";
                case 5:return "CampaignHub020/Nibwick/Voice/tour_watchtower01";
                case 6:return "CampaignHub020/Nibwick/Voice/tour_terrace01";
                case 7:return "CampaignHub020/Nibwick/Voice/tour_smith01";
                case 8:return "CampaignHub020/Nibwick/Voice/tour_bridges01";
                case 9:return "CampaignHub020/Nibwick/Voice/tour_complete01";
                case 10:return "CampaignHub020/Nibwick/Voice/tour_wait01";
                case 11:return "CampaignHub020/Nibwick/Voice/tour_resume01";
                case 12:return "CampaignHub020/Nibwick/Voice/tour_pause01";
                case 13:return "CampaignHub020/Nibwick/Voice/tour_decline01";
                case 14:return "CampaignHub020/Nibwick/Voice/other_arrivals01";
                case 15:return "CampaignHub020/Nibwick/Voice/gardens_locked01";
                default:return "";
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact immutable NPC caption/resource lookup, no world or UI state.
// Array Formats: Fifteen frozen first-party Nibwick utterances in bounded constant switches; no frame string construction, tags, queue or generated paraphrase.
// Dependencies/Pointers: dialogue-v2.json pinned by full SHA256; root-generated stock greeting resource borrows its clip and uses exactly these words. Missing later clips are subtitle-only through the same accepted lease; no provider call.
