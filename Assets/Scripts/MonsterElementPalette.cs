using Unity.Mathematics;

namespace OdysseyOracle
{
    internal enum MonsterElementTheme { Ember, Tide, Moss, Storm }

    internal readonly struct MonsterElementPalette
    {
        internal readonly MonsterElementTheme Theme;
        internal readonly float3 Stone, Joints, Rune;
        internal readonly float Emission;
        MonsterElementPalette(MonsterElementTheme theme, float3 stone, float3 joints, float3 rune, float emission)
        { Theme = theme; Stone = stone; Joints = joints; Rune = rune; Emission = emission; }

        // Stable identity gives host and peers the same presentation without wire state.
        internal static MonsterElementPalette ForStableId(int id)
        {
            uint key = (uint)(id - 1);
            // Rotate each group of four so a repeating role cadence does not lock to one color.
            switch ((key + (key >> 2)) & 3u)
            {
                case 0: return new MonsterElementPalette(MonsterElementTheme.Ember,
                    new float3(.46f,.105f,.045f), new float3(.13f,.035f,.02f), new float3(1f,.35f,.045f), 1.75f);
                case 1: return new MonsterElementPalette(MonsterElementTheme.Tide,
                    new float3(.045f,.29f,.47f), new float3(.02f,.075f,.14f), new float3(.10f,.84f,1f), 1.5f);
                case 2: return new MonsterElementPalette(MonsterElementTheme.Moss,
                    new float3(.25f,.36f,.085f), new float3(.065f,.10f,.025f), new float3(.72f,1f,.12f), 1.4f);
                default: return new MonsterElementPalette(MonsterElementTheme.Storm,
                    new float3(.31f,.12f,.48f), new float3(.075f,.025f,.13f), new float3(.9f,.62f,1f), 1.6f);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Pure immutable numeric palette, deterministic positive stable ID with four balanced themes.
// RGB float3 values are presentation only; no element resistance, stats, roles, space,
// native memory, object, texture, material or network ownership is introduced.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing metre/Y-up/XZ routes, saved art local+Z with existing1.20 scale only.
// Array Formats: Four fixed numeric tower value slots; stable-ID four-theme palette; no new spatial/native storage.
// Dependencies/Pointers: Main-thread registry/LOS adapter is separate from pure math; V1 owns pivots/gait and composite V2 lease owns disposal; host HP/damage/projectile authority remains unchanged.
