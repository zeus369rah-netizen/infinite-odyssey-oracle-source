using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure bounded two-lane pacing data/kernel; no Unity objects or native ownership.
    // The director consumes these values at a successful admission, not on a timer debt.
    public static class CurrentDefensePacing
    {
        public const int PhaseCount = 5, LaneCapacity = 2;
        public const double StageSeconds = 600d;
        public readonly struct Phase
        {
            public readonly double StartsAt, PerLaneSpacing;
            public readonly int PerLaneQuota, PerLaneAlive;
            public Phase(double start, int quota, double spacing, int alive)
            { StartsAt = start; PerLaneQuota = quota; PerLaneSpacing = spacing; PerLaneAlive = alive; }
        }
        public readonly struct EnemyProfile
        {
            public readonly int Archetype;
            public readonly bool Flying;
            public readonly float Health, SpeedScale, DamageScale;
            public EnemyProfile(int archetype, bool flying, float health, float speed, float damage)
            { Archetype = archetype; Flying = flying; Health = health; SpeedScale = speed; DamageScale = damage; }
        }
        static readonly Phase[] phases =
        {
            new Phase(5d, 1, 14d, 1), new Phase(30d, 8, 14d, 2),
            new Phase(150d, 9, 16d, 2), new Phase(300d, 10, 15d, 2),
            new Phase(450d, 8, 13d, 2)
        };
        public static Phase GetPhase(int index)
        {
            if ((uint)index >= PhaseCount) throw new ArgumentOutOfRangeException(nameof(index));
            return phases[index];
        }
        public static int PhaseAt(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0d) return -1;
            int phase = 0;
            while (phase + 1 < PhaseCount && now >= phases[phase + 1].StartsAt) phase++;
            return phase;
        }
        public static bool CanAdmitLane(int alive, int admitted, int phase)
        {
            if ((uint)phase >= PhaseCount || alive < 0 || admitted < 0) return false;
            return alive < phases[phase].PerLaneAlive && admitted < phases[phase].PerLaneQuota;
        }
        public static int FindLane(int[] alive, int[] admitted, int laneCount, int cursor, int phase)
        {
            if (alive == null || admitted == null || laneCount < 1 || laneCount > LaneCapacity ||
                alive.Length < laneCount || admitted.Length < laneCount || cursor < 0 || (uint)phase >= PhaseCount) return -1;
            for (int step = 0; step < laneCount; step++)
            {
                int lane = (cursor % laneCount + step) % laneCount;
                if (CanAdmitLane(alive[lane], admitted[lane], phase)) return lane;
            }
            return -1;
        }
        public static EnemyProfile EnemyAt(int phase, int admittedInLane)
        {
            Phase plan = GetPhase(phase);
            if (admittedInLane < 0 || admittedInLane >= plan.PerLaneQuota) throw new ArgumentOutOfRangeException(nameof(admittedInLane));
            bool heavy = phase >= 3 && admittedInLane == plan.PerLaneQuota - 1;
            int archetype = phase < 2 || heavy ? 0 : admittedInLane % 3 == 1 ? 1 : admittedInLane % 3 == 2 ? 2 : 0;
            float hp = heavy ? phase == 3 ? 50f : 60f : phase == 0 ? 20f : phase == 1 ? 25f : 30f;
            float speed = heavy ? .6f : phase == 0 ? .5f : phase == 1 ? .65f : phase == 2 ? .7f : phase == 3 ? .75f : .8f;
            float damage = heavy ? phase == 3 ? 1.4f : 1.6f : phase == 0 ? .45f : phase == 1 ? .65f : phase == 2 ? .8f : phase == 3 ? .9f : 1f;
            return new EnemyProfile(archetype, phase >= 2 && !heavy && archetype == 1, hp, speed, damage);
        }
        public static float BaseHealth(int archetype)
        {
            if ((uint)archetype > 2) throw new ArgumentOutOfRangeException(nameof(archetype));
            return archetype == 1 ? 80f : archetype == 2 ? 90f : 58f;
        }
    }
}
