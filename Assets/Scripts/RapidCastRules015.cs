using System;
using System.Runtime.InteropServices;
namespace OdysseyOracle
{
    public static class RapidCastRules015
    {
        public const double IntentLifetimeSeconds = 6d;
        public static int ComponentCount(int mask) => !ConfirmedSelection.ValidCastMask(mask) ? 0 : SpellIngredientRules.Count(mask) == 3 ? 3 : 1;
        public static int ElementAt(int mask, int ordinal)
        {
            if (SpellIngredientRules.Count(mask) != 3 || ordinal < 0 || ordinal >= 3) return -1;
            for (int element = 0; element < 4; element++)
                if ((mask & (1 << element)) != 0 && ordinal-- == 0) return element;
            return -1;
        }
        public static bool FreshSelection(ref ConfirmedSelection selection, int freshMask)
        {
            if (!ConfirmedSelection.ValidMask(freshMask)) return false;
            selection.Clear();
            for (int element = 0; element < 4; element++) if ((freshMask & (1 << element)) != 0) selection.Tap(element);
            return true;
        }
        public static void Accepted(ref ConfirmedSelection selection, int usedMask, bool held)
        { if (held || SpellIngredientRules.Count(usedMask) == 3) selection.Clear(); else selection.MarkCastAccepted(); }
        public static bool Capacity(int mask, int freeFlights, int receiptCount, int receiptCapacity, int lastShotId)
        {
            int n = ComponentCount(mask);
            return n > 0 && freeFlights >= n && receiptCapacity >= 0 && receiptCount >= 0 &&
                receiptCount <= receiptCapacity - n && lastShotId >= 0 && lastShotId <= int.MaxValue - n;
        }
        public static bool ReserveThree(ulong occupied,int length,int cursor,out int first,out int second,out int third)
        {
            first=second=third=-1;
            if(length<3||length>48||cursor<0||cursor>=length||(occupied>>length)!=0)return false;
            int a=-1,b=-1;
            for(int i=0;i<length;i++)
            {
                int slot=(cursor+i)%length;if((occupied&(1ul<<slot))!=0)continue;
                if(a<0)a=slot;else if(b<0)b=slot;else{first=a;second=b;third=slot;return true;}
            }
            return false;
        }
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
    // Separate scalar owner state: never borrow a reserved wire/native-record word.
    [StructLayout(LayoutKind.Sequential, Pack=8)]
    public struct HeldFireIntent015
    {
        public double Expires;
        public uint SelectionSequence;
        public int Mask;
        public bool Active => Mask != 0;
        public bool Begin(int mask, uint sequence, double now)
        {
            if (!ConfirmedSelection.ValidMask(mask) || sequence == 0 || !RapidCastRules015.Finite(now) || now < 0d ||
                !RapidCastRules015.Finite(now + RapidCastRules015.IntentLifetimeSeconds)) return false;
            Mask=mask;SelectionSequence=sequence;Expires=now+RapidCastRules015.IntentLifetimeSeconds;return true;
        }
        public bool Valid(int mask, uint sequence, double now)
        {
            return Active && mask == Mask && sequence == SelectionSequence && RapidCastRules015.Finite(now) &&
                now >= 0d && now <= Expires && RapidCastRules015.Finite(Expires);
        }
        public bool Cancel(uint selectionSequence)
        { if (!Active || selectionSequence != SelectionSequence) return false; Clear(); return true; }
        public void Clear() { this=default; }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.
