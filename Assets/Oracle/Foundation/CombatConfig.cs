using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CombatConfig
    {
        public float MonsterHealth;
        public float PlayerDamage;
        public float TowerDamage;
        public float TowerCooldown;

        public static CombatConfig Default => new CombatConfig
        {
            MonsterHealth = 100f,
            PlayerDamage = 25f,
            TowerDamage = 25f,
            TowerCooldown = 1.5f
        };

        public bool IsValid => math.all(math.isfinite(new float4(
            MonsterHealth, PlayerDamage, TowerDamage, TowerCooldown))) &&
            MonsterHealth > 0f && PlayerDamage > 0f && TowerDamage > 0f && TowerCooldown > 0f;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: no spatial coordinates; cooldown uses accepted simulation seconds.
// Array Formats: shared16-byte value config containing four float scalars.
// Dependencies/Pointers: Unity.Mathematics only; no object/container/native ownership or health/damage application.
