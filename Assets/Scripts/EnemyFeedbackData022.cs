// PSEUDOCODE: carry only accepted HP facts and exact actor-lifetime identity;
// synchronous consumers may copy these values into their own bounded pools.
using UnityEngine;
namespace OdysseyOracle
{
    public struct EnemyLifeToken022
    {
        public uint Encounter,WorldRevision,PoolGeneration;
        public int Slot,TargetId;
    }
    [System.Flags]
    public enum EnemyHitFlags022:byte { None=0,Survived=1,Wind=2,KnockbackRequested=4,SurfaceContact=8 }
    public struct EnemyAcceptedHit022
    {
        public EnemyLifeToken022 Life;
        public ulong Serial;
        public float Before,HP,MaxHP,AcceptedDamage;
        public Vector3 Position,Direction,Anchor;
        public int Ingredients,Element;
        public EnemyHitFlags022 Flags;
    }
    public struct EnemyHealthReadout022
    {
        public EnemyLifeToken022 Life;
        public float HP,MaxHP;
        public Vector3 Position;
        public byte Alive,Targetable,Remote;
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current admitted Combat world metres/Y-up, actual HP units.
// Array Formats: Fixed blittable lifetime/accepted-hit/health values, no Object/string/delegate field.
// Dependencies/Pointers: OracleCombat alone publishes accepted transactions; cached-owner reference plus full Life token fences consumers. Root composes UI/audio without granting damage authority.
