namespace OdysseyOracle
{
    public static class TrainingLabRules
    {
        public const int PrototypeCount=4,MaximumActors=2;
        public static bool CanOwn(bool started,bool tutorialFinished,bool unlocked,bool refuge,bool coop,bool connected,
            bool defenseRunning,bool dead,bool downed,bool victory)=>started&&tutorialFinished&&unlocked&&refuge&&
            !coop&&!connected&&!defenseRunning&&!dead&&!downed&&!victory;
        public static bool CanSelect(int prototype,bool hasActors)=>prototype>=0&&prototype<PrototypeCount; // Pending selection never mutates a live actor.
        public static bool AcceptedDamage(float before,float after)=>Finite(before)&&Finite(after)&&before>0f&&after>=0f&&after<before;
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
    public struct TrainingLabDamageMeter
    {
        public float Last{get;private set;}
        public float Total{get;private set;}
        public int Hits{get;private set;}
        public bool Observe(float before,float after)
        {
            if(!TrainingLabRules.AcceptedDamage(before,after))return false;
            Last=before-after;Total=(float)System.Math.Min(float.MaxValue,(double)Total+Last);
            if(Hits<int.MaxValue)Hits++;return true;
        }
    }
    public struct TrainingLabSnapshot
    {
        public bool HasActors,DummyAlive,TowerAlive,TowerAttacking,MonsterAttackingTower;
        public int Prototype,DummyHits,TowerHits,TowerShots,NovaLaunches;
        public float DummyHealth,DummyMaximum,TowerHealth,TowerMaximum;
        public float LastDummyDamage,TotalDummyDamage,LastTowerDamage,TotalTowerDamage,TowerDamageToDummy;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Scalar offline lab context and observed current-owner HP values; no transforms.
// Array Formats: Fixed four prototype indices, two actor cap; value snapshot and three bounded damage meters.
// Dependencies/Pointers: Pure authorization/observation only; never applies damage, invents HP, awards rewards or owns native memory.
// # ARCHITECTURE SNAPSHOT
// Existing Y-up/metre lab anchors and fixed actor pools/real HP owners remain.
// Pending four-type selectors apply only on explicit Replace/Add; zero/one actors retain the same Combat reward-exclusion owner.
// Remove/Replace reset both attacks and retire shots before actor mutation; full EndSession alone releases the lab session.
