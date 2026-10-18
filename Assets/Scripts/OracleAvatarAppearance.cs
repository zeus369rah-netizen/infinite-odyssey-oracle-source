using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential, Pack=4, Size=4)]
    public readonly struct OracleAvatarAppearance
    {
        // A cosmetic payload, never a source of movement, health, rank or hit-box authority.
        public const int ProtocolVersion=1,StartingLooks=8, Colors=8, HatStyles=4, ClothingStyles=3, ShoulderStyles=3;
        public const uint AllowedBits=65535u;
        public readonly uint Packed;
        OracleAvatarAppearance(uint packed){Packed=packed;}
        public int HatStyle=>(int)(Packed&3u);
        public int HatColor=>(int)((Packed>>2)&7u);
        public int RobeColor=>(int)((Packed>>5)&7u);
        public int ClothingStyle=>(int)((Packed>>8)&3u);
        public int ShoulderStyle=>(int)((Packed>>10)&3u);
        public int HairColor=>(int)((Packed>>12)&7u);
        public bool BookVisible=>(Packed&32768u)!=0;
        public static bool TryDecode(uint packed,out OracleAvatarAppearance appearance)
        {
            appearance=default;
            if((packed&~AllowedBits)!=0||((packed>>8)&3u)>=ClothingStyles||((packed>>10)&3u)>=ShoulderStyles)return false;
            appearance=new OracleAvatarAppearance(packed);return true;
        }
        public static bool TryCreate(int hatStyle,int hatColor,int robeColor,int clothingStyle,int shoulderStyle,int hairColor,bool book,
            out OracleAvatarAppearance appearance)
        {
            appearance=default;
            if(hatStyle<0||hatStyle>=HatStyles||hatColor<0||hatColor>=Colors||robeColor<0||robeColor>=Colors||
                clothingStyle<0||clothingStyle>=ClothingStyles||shoulderStyle<0||shoulderStyle>=ShoulderStyles||hairColor<0||hairColor>=Colors)return false;
            uint packed=(uint)(hatStyle|(hatColor<<2)|(robeColor<<5)|(clothingStyle<<8)|(shoulderStyle<<10)|(hairColor<<12)|(book?32768:0));
            return TryDecode(packed,out appearance);
        }
        public static OracleAvatarAppearance StartingLook(int admissionSlot)
        {
            int slot=admissionSlot>=0&&admissionSlot<StartingLooks?admissionSlot:0;
            TryCreate(slot%3,slot,slot,slot%ClothingStyles,1+slot%2,slot, true,out var appearance);return appearance;
        }
        public OracleAvatarAppearance WithHatStyle(int value)=>Change(value,HatColor,RobeColor,ClothingStyle,ShoulderStyle,HairColor,BookVisible);
        public OracleAvatarAppearance WithHatColor(int value)=>Change(HatStyle,value,RobeColor,ClothingStyle,ShoulderStyle,HairColor,BookVisible);
        public OracleAvatarAppearance WithRobeColor(int value)=>Change(HatStyle,HatColor,value,ClothingStyle,ShoulderStyle,HairColor,BookVisible);
        public OracleAvatarAppearance WithClothingStyle(int value)=>Change(HatStyle,HatColor,RobeColor,value,ShoulderStyle,HairColor,BookVisible);
        public OracleAvatarAppearance WithShoulderStyle(int value)=>Change(HatStyle,HatColor,RobeColor,ClothingStyle,value,HairColor,BookVisible);
        public OracleAvatarAppearance WithHairColor(int value)=>Change(HatStyle,HatColor,RobeColor,ClothingStyle,ShoulderStyle,value,BookVisible);
        public OracleAvatarAppearance WithBook(bool value)=>Change(HatStyle,HatColor,RobeColor,ClothingStyle,ShoulderStyle,HairColor,value);
        OracleAvatarAppearance Change(int hat,int hatColor,int robe,int clothing,int shoulder,int hair,bool book)
            =>TryCreate(hat,hatColor,robe,clothing,shoulder,hair,book,out var changed)?changed:this;
    }
}

// # ARCHITECTURE SNAPSHOT
// Exactly4 cosmetic bytes: hat2/hatColor3/robeColor3/clothing2/shoulder2/hair3/book1; upper16 bits reserved.
// Stable admission slots0..7 choose defaults; no coordinates, native pointers or gameplay authority.
