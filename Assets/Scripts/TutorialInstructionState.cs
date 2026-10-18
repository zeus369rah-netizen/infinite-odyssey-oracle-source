namespace OdysseyOracle
{
    /// <summary>Presentation only. No actors, rewards, progression or completion authority.</summary>
    public struct TutorialInstructionState
    {
        public const int CardCount=12;
        ushort spokenMask,acknowledgedMask;
        byte cardPlusOne;
        public int CurrentCard => cardPlusOne-1;
        public bool HasCurrent => cardPlusOne!=0;
        public bool HasSpoken(int card) => Valid(card)&&(spokenMask&(1<<card))!=0;
        public bool HasAcknowledged(int card) => Valid(card)&&(acknowledgedMask&(1<<card))!=0;
        public bool Begin(int card)
        {
            if(!Valid(card)||HasAcknowledged(card))return false;
            cardPlusOne=(byte)(card+1);return true;
        }
        public bool TryStartNarration()
        {
            int card=CurrentCard;if(!HasCurrent||HasSpoken(card))return false;
            spokenMask|=(ushort)(1<<card);return true;
        }
        public bool TryAcknowledge(out int card)
        {
            card=CurrentCard;if(!HasCurrent)return false;
            acknowledgedMask|=(ushort)(1<<card);cardPlusOne=0;return true;
        }
        public void Reset(){spokenMask=acknowledgedMask=0;cardPlusOne=0;}
        static bool Valid(int card)=>card>=0&&card<CardCount;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: none; pure presentation state. Array Formats: two twelve-bit masks and a bounded byte card discriminator (6-byte sequential struct).
// Dependencies/Pointers: no Unity object/native pointer; existing tutorial owns all real practice and completion predicates.
