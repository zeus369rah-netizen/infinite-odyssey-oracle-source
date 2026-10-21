using System.Runtime.InteropServices;
using UnityEngine;
namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct ArenaImpactFrame64:Unity.Netcode.INetworkSerializeByMemcpy
    {
        public uint Magic,Version;public ulong RunLow,RunHigh;public uint Sequence;public int ShotId;
        public ulong Caster;public Vector3 Point;public uint Element,Ordinal,Flags;
    }
    public static class ArenaImpactFrameRules
    {
        public const uint Magic=0x494d5039,Version=1;
        public static bool TryRun(string run,out ulong low,out ulong high)
        {
            low=high=0;if(run==null||run.Length!=32)return false;
            for(int i=0;i<32;i++)
            {char c=run[i];int digit=c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:c>='A'&&c<='F'?c-'A'+10:-1;if(digit<0){low=high=0;return false;}if(i<16)low=(low<<4)|(uint)digit;else high=(high<<4)|(uint)digit;}
            return low!=0||high!=0;
        }
        public static bool Shape(ArenaImpactFrame64 f)=>f.Magic==Magic&&f.Version==Version&&(f.RunLow!=0||f.RunHigh!=0)&&
            f.Sequence!=0&&f.ShotId>0&&f.Element<4&&f.Ordinal<3&&f.Flags<=1&&AcceptedImpactRules.Point(new Unity.Mathematics.float3(f.Point.x,f.Point.y,f.Point.z));
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: current32-hex accepted defense run encoded losslessly as two64-bit values, actual host World contact and accepted shot/ordinal/caster identity.
// Array Formats: sequential64B unmanaged fixed frame, no strings/arrays/managed fields;32-character scalar run parsing is cold-only and allocates nothing.
// Dependencies/Pointers: authenticated Room host, current numeric run and existing accepted-shot receipt/expiry/three-bit validation remain required; source shape alone never grants attack/damage permission.
