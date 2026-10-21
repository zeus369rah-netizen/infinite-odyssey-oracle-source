using System;
using System.Globalization;

namespace OdysseyOracle
{
    public enum CoopRoomMode020:byte {None=0,CampaignTown=1,TowerDefense=2}
    public enum RoomConnectionPhase020:byte {Offline=0,Hosting=1,Joining=2,Negotiating=3,RegionPreparing=4,ArrivalReserved=5,Online=6,Leaving=7,Failed=8,Canceled=9}
    public static class CoopRoomModeRules020
    {
        public const string ModeProperty="oracle.mode020",CapacityProperty="oracle.cap020",ProtocolProperty="oracle.wire020";
        public const string TownServiceKey="odyssey-oracle-campaign-town020",TownProtocol=CoopPartyCapacity.WireProtocol+"|town1";
        public static bool Valid(CoopRoomMode020 mode)=>mode==CoopRoomMode020.CampaignTown||mode==CoopRoomMode020.TowerDefense;
        public static int RequestedMaximum(CoopRoomMode020 mode)=>mode==CoopRoomMode020.CampaignTown?2:mode==CoopRoomMode020.TowerDefense?8:0;
        // TD transport stays at its current audited capacity. Requested8 is not support.
        public static int SupportedMaximum(CoopRoomMode020 mode)=>mode==CoopRoomMode020.CampaignTown?2:mode==CoopRoomMode020.TowerDefense?CoopPartyCapacity.MaximumPlayers:0;
        public static string Tag(CoopRoomMode020 mode)=>mode==CoopRoomMode020.CampaignTown?"campaign-town":mode==CoopRoomMode020.TowerDefense?"tower-defense":string.Empty;
        public static string ServiceKey(CoopRoomMode020 mode)=>mode==CoopRoomMode020.CampaignTown?TownServiceKey:mode==CoopRoomMode020.TowerDefense?CoopPartyCapacity.RoomType:string.Empty;
        public static string Protocol(CoopRoomMode020 mode)=>mode==CoopRoomMode020.CampaignTown?TownProtocol:mode==CoopRoomMode020.TowerDefense?CoopPartyCapacity.WireProtocol:string.Empty;
        public static bool MatchesPayload(CoopRoomMode020 mode,byte[] data)
        {
            if(!Valid(mode)||data==null)return false;string expected=Protocol(mode);if(data.Length!=expected.Length)return false;
            for(int i=0;i<data.Length;i++)if(data[i]!=(byte)expected[i])return false;return true;
        }
        public static bool SessionMatches(CoopRoomMode020 mode,int actualCapacity,string tag,string cap,string protocol)
        {
            if(!Valid(mode)||actualCapacity<1||actualCapacity>SupportedMaximum(mode))return false;
            bool absent=string.IsNullOrEmpty(tag)&&string.IsNullOrEmpty(cap)&&string.IsNullOrEmpty(protocol);
            if(absent)return mode==CoopRoomMode020.TowerDefense; // Existing p2 TD peers remain compatible.
            return tag==Tag(mode)&&protocol==Protocol(mode)&&int.TryParse(cap,NumberStyles.None,CultureInfo.InvariantCulture,out int declared)&&
                declared==actualCapacity&&(mode!=CoopRoomMode020.CampaignTown||actualCapacity==2);
        }
        public static bool CanPublish(CoopRoomMode020 mode,int acceptedOperation,int currentOperation,bool service,bool transport,bool arrival,bool closing)=>
            Valid(mode)&&acceptedOperation>0&&acceptedOperation==currentOperation&&service&&transport&&arrival&&!closing;
    }
    public struct CampaignTownRoomContext020
    {
        public string RoomId;public int Operation;public ulong LocalClientId;public bool Hosting;
        public uint LocalGeneration,LocalRevision;
    }
    public struct CampaignTownRoomGrant020
    {
        public string RoomId,RegionName;public uint RegionInstance,LocalGeneration,LocalRevision;public ulong ClientId;public byte Seat;
        public float X,Y,Z,Yaw;public bool Supported,Applied,Acknowledged,Distinct;
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Authenticated typed service room and future named-town/local-world lease; no geometry or TD stage reinterpretation.
// Array Formats: Two enums, scalar metadata/protocol/cap/publication predicates and value context/grant. Current TD2 buffers/wire stay unchanged; requested TD8 is explicitly separate.
// Dependencies/Pointers: RoomConnection remains the sole SDK/NGO operation owner. Campaign destination implementation and actual peer proof are root-owned and absent by default; no new packet serialization or gameplay authority is implied by these values.
