using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace OdysseyOracle
{
    public enum SharedLabKind:uint { Hello=1,State=2,Control=3,Tap=4,Cast=5,Auto=6,Shot=7,Nova=8,TowerShot=9,Hit=10,Impact=11,HeldFire=12 }
    public enum SharedLabControl:uint { ReplaceMonster=1,RemoveMonster=2,ReplaceTower=3,RemoveTower=4,RemoveAll=5,SpawnPair=6,TowerAttack=7,MonsterAttack=8 }
    [StructLayout(LayoutKind.Explicit,Size=256,Pack=4)]
    public struct SharedLabFrame:Unity.Netcode.INetworkSerializeByMemcpy
    {
        public const uint LabVersion=3;
        [FieldOffset(0)] public uint Version;
        [FieldOffset(4)] public SharedLabKind Kind;
        [FieldOffset(8)] public uint Epoch;
        [FieldOffset(12)] public uint Revision;
        [FieldOffset(16)] public uint Sequence;
        [FieldOffset(20)] public uint Argument;
        [FieldOffset(24)] public uint DummyGeneration;
        [FieldOffset(28)] public uint TowerGeneration;
        [FieldOffset(32)] public ulong Member;
        [FieldOffset(40)] public uint MembershipGeneration;
        [FieldOffset(44)] public uint SelectionSequence;
        [FieldOffset(48)] public uint Mask;
        [FieldOffset(52)] public int Dominant;
        [FieldOffset(56)] public int Prepared;
        [FieldOffset(60)] public uint Auto;
        [FieldOffset(64)] public Vector3 A;
        [FieldOffset(76)] public Vector3 B;
        [FieldOffset(88)] public Vector3 C;
        [FieldOffset(100)] public Vector3 D;
        [FieldOffset(112)] public float DummyHP;
        [FieldOffset(116)] public float DummyMax;
        [FieldOffset(120)] public float TowerHP;
        [FieldOffset(124)] public float TowerMax;
        [FieldOffset(128)] public float LastDummy;
        [FieldOffset(132)] public float TotalDummy;
        [FieldOffset(136)] public float LastTower;
        [FieldOffset(140)] public float TotalTower;
        [FieldOffset(144)] public float TowerDamage;
        [FieldOffset(148)] public int DummyHits;
        [FieldOffset(152)] public int TowerHits;
        [FieldOffset(156)] public int TowerShots;
        [FieldOffset(160)] public int NovaLaunches;
        [FieldOffset(164)] public int Prototype;
        [FieldOffset(168)] public int TowerType;
        [FieldOffset(172)] public uint Flags;
        [FieldOffset(176)] public float R0;
        [FieldOffset(180)] public float R1;
        [FieldOffset(184)] public float R2;
        [FieldOffset(188)] public float R3;
        [FieldOffset(192)] public float Primary;
        [FieldOffset(196)] public int AttackSequence;
        [FieldOffset(200)] public int Phase;
        [FieldOffset(204)] public float Progress;
        [FieldOffset(208)] public float Windup;
        [FieldOffset(212)] public float Strike;
        [FieldOffset(216)] public float Recovery;
        [FieldOffset(220)] public int AttackStyle;
        [FieldOffset(224)] public uint LiveStatus;
        [FieldOffset(228)] public float TowerCooldown;
        [FieldOffset(232)] public uint Result;
        [FieldOffset(236)] public uint Reserved1;
        [FieldOffset(240)] public ulong ClientToken;
        [FieldOffset(248)] public ulong Reserved2;
    }
    [StructLayout(LayoutKind.Sequential,Pack=8)]
    public struct SharedLabCastState
    {
        public ulong Id;
        public uint Generation,LastSequence,LastTap,Auto,Pending,Mask;
        public int Dominant;
        public uint Flags;
        public double NextCast,AutoReady,R0,R1,R2,R3,NextControl,NextInput,NextManual;
        public uint Token,LastResult;
        public ulong Reserved;
    }
    public static class SharedLabCastMath
    {
        public static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        public static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        public static bool Newer(uint a,uint b)=>unchecked((int)(a-b))>0;
        public static bool Select(ref SharedLabCastState s,int element,double now,uint sequence)
        {
            if(element<0||element>3||!Finite(now)||now<0)return false;
            var selection=new ConfirmedSelection();
            if((s.Flags&1)==0&&!selection.Restore((int)s.Pending-1,(int)s.Mask,s.Mask==0?-1:s.Dominant))return false;
            if(!selection.Tap(element))return false;
            s.Pending=(uint)(selection.PreparedElement+1);s.Mask=(uint)selection.ConfirmedMask;s.Dominant=selection.ConfirmedElement;
            s.Flags&=~1u;s.LastTap=sequence;s.AutoReady=now+.5d;return true;
        }
        public static bool Ready(SharedLabCastState s,double now,bool automatic)
        {
            if(!Finite(now)||now<0||s.Pending!=0||!ConfirmedSelection.ValidCastMask((int)s.Mask)||
                !Finite(s.NextCast)||s.NextCast<0||now<s.NextCast||automatic&&(s.Auto==0||!Finite(s.AutoReady)||s.AutoReady<0||now<s.AutoReady))return false;
            return Finite(s.R0)&&Finite(s.R1)&&Finite(s.R2)&&Finite(s.R3)&&
                s.R0>=0&&s.R1>=0&&s.R2>=0&&s.R3>=0&&
                ((s.Mask&1)==0||now>=s.R0)&&((s.Mask&2)==0||now>=s.R1)&&((s.Mask&4)==0||now>=s.R2)&&((s.Mask&8)==0||now>=s.R3);
        }
        public static bool Commit(ref SharedLabCastState s,double now,bool launchAccepted)
        {
            if(!launchAccepted||!Ready(s,now,false))return false;
            double ready=now+(s.Mask==15?5d:4d);
            if((s.Mask&1)!=0)s.R0=ready;if((s.Mask&2)!=0)s.R1=ready;
            if((s.Mask&4)!=0)s.R2=ready;if((s.Mask&8)!=0)s.R3=ready;
            s.NextCast=now+.5d;s.Flags|=1;
            if(SpellIngredientRules.Count((int)s.Mask)==3){s.Mask=s.Pending=0;s.Dominant=-1;s.Flags&=~1u;}
            return true;
        }
        public static bool AdmitInput(ref SharedLabCastState s,double now,bool autoToggle)
        {
            if(!Finite(now)||now<0)return false;
            if(now>=s.NextInput){s.NextInput=now+1;s.Reserved=0;}
            uint taps=(uint)s.Reserved,autos=(uint)(s.Reserved>>32);
            if(autoToggle){if(autos>=4)return false;autos++;}else{if(taps>=16)return false;taps++;}
            s.Reserved=((ulong)autos<<32)|taps;return true;
        }
        public static bool RequestShape(SharedLabFrame f)
        {
            if(f.Version!=SharedLabFrame.LabVersion||f.Kind<SharedLabKind.Hello||(f.Kind>SharedLabKind.Auto&&f.Kind!=SharedLabKind.HeldFire)||f.Kind==SharedLabKind.State||
                f.LiveStatus!=0||f.Reserved1!=0||f.Reserved2!=0||f.Member!=0||!Finite(f.A))return false;
            if(f.Kind==SharedLabKind.Hello)return f.ClientToken>0&&f.ClientToken<=uint.MaxValue;
            if(f.Epoch==0||f.MembershipGeneration==0||f.Sequence==0)return false;
            if(f.Kind==SharedLabKind.HeldFire)return HeldShape015(f);
            if(f.Kind==SharedLabKind.Control)
                return f.Mask>=1&&f.Mask<=8&&(f.Mask==6?f.Argument<=15:f.Mask==1||f.Mask==3?f.Argument<=3:f.Mask>=7?f.Argument<=1:f.Argument==0);
            if(f.Kind==SharedLabKind.Tap)return f.Argument<4;
            if(f.Kind==SharedLabKind.Auto)return f.Argument<=1;
            double length=(double)f.A.x*f.A.x+(double)f.A.y*f.A.y+(double)f.A.z*f.A.z;
            return f.Kind==SharedLabKind.Cast&&length>=.9d&&length<=1.1d;
        }
        static bool HeldShape015(SharedLabFrame f)
        {
            // Only the declared begin/cancel fields may be present in request12.
            if(f.Argument>1||f.SelectionSequence==0||f.Revision==0||f.DummyGeneration==0||f.TowerGeneration==0||
                f.Dominant!=0||f.Prepared!=0||f.Auto!=0||!ZeroVector015(f.A)||!ZeroVector015(f.B)||!ZeroVector015(f.C)||!ZeroVector015(f.D)||
                f.Flags!=0||f.DummyHP!=0||f.DummyMax!=0||f.TowerHP!=0||f.TowerMax!=0||f.LastDummy!=0||f.TotalDummy!=0||f.LastTower!=0||f.TotalTower!=0||
                f.TowerDamage!=0||f.DummyHits!=0||f.TowerHits!=0||f.TowerShots!=0||f.NovaLaunches!=0||f.R0!=0||f.R1!=0||f.R2!=0||f.R3!=0||
                f.Prototype!=0||f.TowerType!=0||f.Primary!=0||f.AttackSequence!=0||f.Phase!=0||f.Progress!=0||f.Windup!=0||f.Strike!=0||f.Recovery!=0||
                f.AttackStyle!=0||f.TowerCooldown!=0||f.Result!=0)return false;
            return f.Argument==0?f.Mask==0:ConfirmedSelection.ValidMask((int)f.Mask)&&f.SelectionSequence==f.Sequence;
        }
        static bool ZeroVector015(Vector3 v)=>v.x==0f&&v.y==0f&&v.z==0f;
        public static bool AdmitRequest(ref SharedLabCastState state,SharedLabFrame frame,uint epoch)
        {
            if(!RequestShape(frame)||frame.Kind==SharedLabKind.Hello||epoch==0||frame.Epoch!=epoch||state.Generation==0||
                frame.MembershipGeneration!=state.Generation||state.Token==0||frame.ClientToken!=state.Token||!Newer(frame.Sequence,state.LastSequence))return false;
            state.LastSequence=frame.Sequence;state.LastResult=1;return true;
        }
        public static bool StateShape(SharedLabFrame f)
        {
            if(f.Version!=SharedLabFrame.LabVersion||f.Kind!=SharedLabKind.State||f.Epoch==0||f.Sequence==0||f.MembershipGeneration==0||
                !EnemyLiveStatusRules009.Valid(f.LiveStatus)||f.Reserved1!=0||f.Reserved2!=0||(f.Flags&~31u)!=0||f.Auto>1||
                f.Prototype< -1||f.Prototype>3||f.TowerType< -1||f.TowerType>3||!Finite(f.A)||!Finite(f.B)||!Finite(f.C)||!Finite(f.D))return false;
            if(((f.Flags&1)==0||f.DummyHP<=0)&&f.LiveStatus!=0)return false;
            if((f.Flags&1)!=0&&(f.Prototype<0||f.DummyGeneration==0||f.DummyMax<=0)||(f.Flags&2)!=0&&(f.TowerType<0||f.TowerGeneration==0||f.TowerMax<=0)||
                System.Math.Abs(f.A.x)>2048||System.Math.Abs(f.A.y)>256||System.Math.Abs(f.A.z)>2048||System.Math.Abs(f.D.x)>360.01||System.Math.Abs(f.D.y)>360.01||System.Math.Abs(f.D.z)>360.01)return false;
            if(!ConfirmedSelection.ValidState(f.Prepared,(int)f.Mask,f.Dominant)||!Health(f.DummyHP,f.DummyMax)||!Health(f.TowerHP,f.TowerMax)||
                !Nonnegative(f.LastDummy)||!Nonnegative(f.TotalDummy)||!Nonnegative(f.LastTower)||!Nonnegative(f.TotalTower)||!Nonnegative(f.TowerDamage)||
                f.DummyHits<0||f.TowerHits<0||f.TowerShots<0||f.NovaLaunches<0||!Cooldown(f.R0)||!Cooldown(f.R1)||!Cooldown(f.R2)||!Cooldown(f.R3)||!Cooldown(f.Primary))return false;
            return f.Phase>=0&&f.Phase<=5&&f.AttackSequence>=0&&f.AttackStyle>=0&&f.AttackStyle<=2&&
                Finite(f.Progress)&&f.Progress>=0&&f.Progress<=1&&Nonnegative(f.Windup)&&f.Windup<=10&&Nonnegative(f.Strike)&&f.Strike<=10&&Nonnegative(f.Recovery)&&f.Recovery<=10;
        }
        static bool Health(float hp,float max)=>Nonnegative(hp)&&Nonnegative(max)&&hp<=max&&max<=1000000;
        static bool Nonnegative(float f)=>Finite(f)&&f>=0;
        static bool Cooldown(float f)=>Nonnegative(f)&&f<=5.001f;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure host seconds and scalar NGO identity; frame vectors use existing Y-up world metres.
// Array Formats: Explicit256-byte unmanaged frame, sequential128-byte cast records; no managed fields or mutable Object access.
// Dependencies/Pointers: Existing ConfirmedSelection semantics reused; charge only accepted48-pool launch;1/2/3/4 logical casts, triple components only,.5 cadence/optional-auto settle,normal4/Nova5.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing actor world healthbar anchor and authenticated host status snapshot.
// Array Formats: Five live bits31 distinct from caster12bit passive mask; fixed status presentation slots,256B lab word224 dedicated.
// Dependencies/Pointers: Existing HP/damage/timer owners unchanged; root composes protocol/version bumps and verifies native/two-peer visuals.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: host accepted World contacts and existing run/epoch/shot identity; no damage/HP/path permission changes.
// Array Formats: existing bounded launch receipts add three-bit landing dedupe; fixed256B shared wire unchanged; new64B arena frame through existing NGO pipeline.
// Dependencies/Pointers: sole host/network/audio owners retain authority, lifetime and gesture/focus policies; actual peer/audio acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: authenticated existing NGO room and Y-up lab world; no new physics or gameplay authority.
// Array Formats: fixed256-byte LabVersion2 frame, word224 LiveStatus only on State; fixed64-byte arena impact event.
// Dependencies/Pointers: exact room tl2/ls1/bi1 capability gate and v2 lab channel reject older peers; requests and Impact require zero LiveStatus; root native/two-peer acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.
