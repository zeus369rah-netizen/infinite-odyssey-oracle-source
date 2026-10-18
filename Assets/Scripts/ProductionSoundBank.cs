namespace OdysseyOracle
{
    public enum ProductionCue
    {
        GuardianAggro, GuardianAttack, GuardianStep, GuardianSpecial,
        StalkerAggro, StalkerAttack, StalkerStep, StalkerSpecial,
        ShadeAggro, ShadeAttack, ShadeStep, ShadeSpecial,
        SpiderAggro, SpiderAttack, SpiderStep, SpiderSpecial,
        TowerConstruct, TowerBuilderOpen, TowerConfirm, TowerUpgrade,
        ShrineConstruct, ShrineConfirm, LightningZap
    }
    // # ARCHITECTURE SNAPSHOT
    // Pure enum/table policy; clips and the existing four source objects remain
    // GameAudioDirector-owned. Role0 is tutorial art, not a new monster archetype.
    public static class ProductionSoundBank
    {
        public const int Count = 23;
        public static readonly string[] Names = {
            "guardian_aggro","guardian_attack","guardian_step","guardian_special",
            "stalker_aggro","stalker_attack","stalker_step","stalker_special",
            "shade_aggro","shade_attack","shade_step","shade_special",
            "spider_aggro","spider_attack","spider_step","spider_special",
            "tower_construct","tower_builder_open","tower_confirm","tower_upgrade",
            "shrine_construct","shrine_confirm","lightning_zap" };
        public static bool Valid(ProductionCue cue) => (int)cue >= 0 && (int)cue < Count;
        public static ProductionCue Enemy(int role, int phase) => role >= 0 && role < 4 && phase >= 0 && phase < 4 ?
            (ProductionCue)(role * 4 + phase) : (ProductionCue)(-1);
        public static bool Step(ProductionCue cue) => (int)cue < 16 && ((int)cue & 3) == 2;
        public static int Priority(ProductionCue cue) => Step(cue) ? 0 : (int)cue < 16 && ((int)cue & 3) == 1 ? 3 :
            cue == ProductionCue.TowerConfirm || cue == ProductionCue.TowerBuilderOpen ? 2 : 1;
        public static float Gain(ProductionCue cue) => Step(cue) ? .028f : (int)cue < 16 ? .06f :
            cue == ProductionCue.LightningZap ? .055f : .05f;
        public static float Interval(ProductionCue cue) => Step(cue) ? .5f : (int)cue < 16 ? .12f : .18f;
    }
    // Pure fixed-four admission ledger. Low movement never evicts an audible cue;
    // committed warnings can replace low priorities immediately. No repeat GC.
    public sealed class ProductionCueAdmission
    {
        readonly int[] priorities = new int[4];
        readonly double[] started = new double[4], protectedUntil = new double[4], next = new double[4];
        public int Select(double now, int priority, int busyMask)
        {
            if (!Finite(now) || now < 0d || priority < 0 || priority > 3 || busyMask < 0 || busyMask > 15) return -1;
            int selected = -1;
            for (int i = 0; i < 4; i++)
            {
                bool busy = (busyMask & (1 << i)) != 0;
                if (!busy) { if (now >= next[i]) return i; continue; }
                if (priority == 0 || priority < priorities[i] || priority == priorities[i] &&
                    (now < next[i] || now < protectedUntil[i])) continue;
                if (selected < 0 || priorities[i] < priorities[selected] ||
                    priorities[i] == priorities[selected] && started[i] < started[selected]) selected = i;
            }
            return selected;
        }
        public bool Commit(int slot, double now, int priority, double protection, double cadence)
        {
            if (slot < 0 || slot > 3 || !Finite(now) || now < 0d || priority < 0 || priority > 3 ||
                !Finite(protection) || protection < 0 || protection > 2 || !Finite(cadence) || cadence < 0 || cadence > 2) return false;
            priorities[slot] = priority; started[slot] = now; protectedUntil[slot] = now + protection; next[slot] = now + cadence; return true;
        }
        public void Clear() { for (int i = 0; i < 4; i++) { priorities[i] = 0; started[i] = protectedUntil[i] = next[i] = 0d; } }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
