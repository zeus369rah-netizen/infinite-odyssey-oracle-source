using System;

namespace OdysseyOracle
{
    [Serializable]
    public struct DiscoveryProgress
    {
        public uint Version, ReadBooks, RevealedHints, QuestFlags;
        public const uint CurrentVersion = 1, BookMask = 0xFFF, HintMask = 0xFFFFF;
        public const uint StickHeld = 1, StickPlaced = 2, SpellUpgradeEver = 4, TowerUpgradeEver = 8;
        public const int BookCount = 12, AchievementCount = 20;
        public static DiscoveryProgress Empty => new DiscoveryProgress { Version = CurrentVersion };
        public bool Valid => Version == CurrentVersion && (ReadBooks & ~BookMask) == 0 &&
            (RevealedHints & ~HintMask) == 0 && (QuestFlags & ~15u) == 0 &&
            (QuestFlags & (StickHeld | StickPlaced)) != (StickHeld | StickPlaced);

        public bool Read(int book)
        {
            if (!Valid || (uint)book >= BookCount) return false;
            uint before = ReadBooks, hints = RevealedHints;
            ReadBooks |= 1u << book;
            RevealedHints |= DiscoveryCatalog.Hints(book);
            return before != ReadBooks || hints != RevealedHints;
        }
        public bool PickupStick()
        {
            if (!Valid || (QuestFlags & (StickHeld | StickPlaced)) != 0) return false;
            QuestFlags |= StickHeld; return true;
        }
        public bool PlaceStick()
        {
            if (!Valid || (QuestFlags & StickHeld) == 0 || (QuestFlags & StickPlaced) != 0) return false;
            QuestFlags = (QuestFlags & ~StickHeld) | StickPlaced; return true;
        }
        public bool RecordAcceptedUpgrade(bool tower)
        {
            if (!Valid) return false;
            uint bit = tower ? TowerUpgradeEver : SpellUpgradeEver;
            if ((QuestFlags & bit) != 0) return false;
            QuestFlags |= bit; return true;
        }
        public bool Earned(int row, int legacyFiveBits)
        {
            if ((uint)row >= AchievementCount) return false;
            if (row < 5) return ((legacyFiveBits & 31) & (1 << row)) != 0;
            if (!Valid) return false;
            if (row < 17) return (ReadBooks & (1u << (row - 5))) != 0;
            return (QuestFlags & (row == 17 ? StickPlaced : row == 18 ? SpellUpgradeEver : TowerUpgradeEver)) != 0;
        }
        public bool NameVisible(int row, int legacyFiveBits) => (uint)row < AchievementCount &&
            (Earned(row, legacyFiveBits) || Valid && (RevealedHints & (1u << row)) != 0);
    }

    public static class DiscoveryCatalog
    {
        public static string Title(int id)
        {
            switch (id)
            {
                case 0: return "The Ember Refuge"; case 1: return "The Living Ward";
                case 2: return "The Four Elements"; case 3: return "A Page Beneath the Bench";
                case 4: return "Embers Beyond the Road"; case 5: return "The Copper Ridge";
                case 6: return "Roots Remember"; case 7: return "The Quiet Quarry";
                case 8: return "A Cup of Returning Water"; case 9: return "Tracks in the Thaw";
                case 10: return "The Unfinished Song"; case 11: return "Blue Light at the Turn";
                default: return "";
            }
        }
        // Original existing pages0..2 stay in LoreLibrary, byte-for-byte.
        public static string AddedBody(int id)
        {
            switch (id)
            {
                case 3: return "A loose page waits where hurried feet rarely stop.\n\nThe refuge has lessons, but no debt. A staff is for practice; the little dry stick beside the reading table is for the hearth. Carry it there if you wish.\n\nRaykon can show you a spell's next step. The Smith can show you a tower's next rank when the campaign permits it. Reading spends nothing. Their first lessons finish only when a real upgrade is accepted.\n\nBeyond this room, eight roads divide eight landscapes. Search beside each road, leaving the road itself clear.";
                case 4: return "Someone sheltered this page from the sparks.\n\nFire is useful because it changes things. Choose one change you can afford, then watch what it does. Raykon's spell tree records an accepted lesson; a wish alone changes no spell.\n\nFollow the wheel toward copper-colored ground. Another reader left a page there.";
                case 5: return "The ridge keeps the day's warmth long after the air cools.\n\nA tower is not a promise of safety. Someone must place it, someone must tend it, and someone must leave enough room to pass. The first tower you build is worth remembering.\n\nThe next page rests where roots take hold.";
                case 6: return "Roots do not hurry, but they do arrive.\n\nPractice is like that. One honest attempt teaches more than the title of a lesson. Finish the refuge's practice when you need a quiet beginning.\n\nLook next where stone has been cut and carried.";
                case 7: return "A mason's marks remain beneath the dust.\n\nThe Smith's rank is earned through an accepted Forge upgrade between campaign stages. Opening the anvil or asking for a price is not the same thing. Keep the charge you have until you choose.\n\nWater carries the next page's story.";
                case 8: return "The cup was left full for the next traveler.\n\nA kindness may be small enough to fit in one hand: a dry stick set beside a hearth, a lane kept open, a warning given in time. Small does not mean forgotten.\n\nBeyond the water, search the thawing ground.";
                case 9: return "Two sets of tracks meet here, then turn together.\n\nThe first guardian you defeat will not be the last difficult thing you face. Remember the moment without demanding that it happen twice.\n\nA pale road carries the next song.";
                case 10: return "The singer left the final line unwritten.\n\nFour elements can meet in one spell. Let the living indicators tell you when they are ready; an unfinished selection is still a choice waiting to be made.\n\nA blue glimmer marks the last page around the wheel.";
                case 11: return "Blue light catches in the edge of the binding.\n\nEight landscapes, eight pages, and one refuge to return to. No book takes your earned charge or asks you to buy its ending. Read, remember, and choose your next work.\n\nIf you began elsewhere, a sheltered page waits beside the ember road.";
                default: return "";
            }
        }
        public static string AchievementName(int row)
        {
            switch (row)
            {
                case 0: return "A focus of your own"; case 1: return "Oracle training complete";
                case 2: return "First guardian defeated"; case 3: return "Four elements united";
                case 4: return "First tower built"; case 17: return "Warmth for the next traveler";
                case 18: return "Raykon's first spell lesson"; case 19: return "The Smith's first tower rank";
                default: return row >= 5 && row < 17 ? Title(row - 5) : "";
            }
        }
        public static uint Hints(int id)
        {
            if ((uint)id >= DiscoveryProgress.BookCount) return 0;
            uint own = 1u << (5 + id);
            switch (id)
            {
                case 0: return own | (1u << 0) | (1u << 17);
                case 1: return own | (1u << 4) | (1u << 19);
                case 2: return own | (1u << 3) | (1u << 18);
                case 3: return own | (1u << 1) | (1u << 2) | (1u << 9);
                case 4: return own | (1u << 18) | (1u << 10);
                case 5: return own | (1u << 4) | (1u << 11);
                case 6: return own | (1u << 1) | (1u << 12);
                case 7: return own | (1u << 19) | (1u << 13);
                case 8: return own | (1u << 17) | (1u << 14);
                case 9: return own | (1u << 2) | (1u << 15);
                case 10: return own | (1u << 3) | (1u << 16);
                default: return own | (1u << 9);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Local profile facts only; no world, HP, currency or match-rank authority.
// Array Formats: Serializable16B uint payload,12 book bits,20 hints,4 quest bits; legacy5 bits supplied read-only.
// Dependencies/Pointers: Physical-book acceptance and existing upgrade owners supply facts; no Unity dependency or request-to-success inference.
