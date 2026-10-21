using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        RefugePracticeCycle020 refugePracticeCycle020;
        OracleEnemy refugePracticeTarget020;
        DungeonWorld refugePracticeWorld020;
        internal bool IsRefugePracticeRepeat020(OracleEnemy value)=>refugePracticeCycle020.Repeat&&value!=null&&
            object.ReferenceEquals(value,refugePracticeTarget020)&&value.PoolLeased015&&
            refugePracticeCycle020.Matches(value.TargetId,value.PoolGeneration015);
        void ResetRefugePractice020()
        {refugePracticeCycle020.Reset();refugePracticeTarget020=null;refugePracticeWorld020=null;}
        void BeginRefugePractice020(OracleEnemy value)
        {
            ResetRefugePractice020();
            if(value==null||world==null||!world.IsTutorialRefuge||world.IsCoopArena)return;
            if(refugePracticeCycle020.Begin(value.TargetId,value.PoolGeneration015))
            {refugePracticeTarget020=value;refugePracticeWorld020=world;}
        }
        void SuspendRefugePractice020()
        {refugePracticeCycle020.Suspend();refugePracticeTarget020=null;}
        bool HandleRefugePracticeDefeat020(OracleEnemy value,out bool originalEarned)
        {
            originalEarned=false;
            if(value==null||!object.ReferenceEquals(value,refugePracticeTarget020)||
                !refugePracticeCycle020.Matches(value.TargetId,value.PoolGeneration015))return false;
            if(!value.Alive)originalEarned=refugePracticeCycle020.Defeat(value.TargetId,value.PoolGeneration015)==1;
            return true;
        }
        void ReleaseBoundRefugePractice020()
        {
            var value=refugePracticeTarget020;
            if(value!=null&&refugePracticeCycle020.Matches(value.TargetId,value.PoolGeneration015))
            {
                CompleteMonsterReaders015();RetireEnemy015(value);
                targetGrid.Clear();crowdSpacing.Clear();towerAttackSlots.Clear();
            }
            SuspendRefugePractice020();
        }
        void YieldRefugePracticeToLab020()
        {if(refugePracticeCycle020.FirstDefeated)ReleaseBoundRefugePractice020();}
        void TickRefugePractice020(float seconds)
        {
            if(!refugePracticeCycle020.Enabled)return;
            var context=GameSession.Instance;
            bool scope=!destroying&&initialized&&!remoteSimulation&&isActiveAndEnabled&&context!=null&&
                object.ReferenceEquals(context.Combat,this)&&world!=null&&world==context.World&&world==refugePracticeWorld020&&
                world.IsTutorialRefuge&&!world.IsCoopArena&&!context.CoopMode&&(context.Rooms==null||!context.Rooms.Connected)&&
                context.HasStarted&&!context.Victory&&!context.IsDead&&!context.IsDowned;
            if(!scope)
            {if(refugePracticeCycle020.Repeat&&!monsterQueryBusy015)ReleaseBoundRefugePractice020();return;}
            if(trainingLabOwner!=null||towerPracticeOwned||CosmicNovaTrialActive||
                context.Defense!=null&&context.Defense.Running)return;
            // Preserve every unrelated living encounter; this is not a general campaign spawner.
            for(int i=0;i<enemies.Count;i++)if(enemies[i]!=null&&enemies[i].Alive)return;
            if(!refugePracticeCycle020.Step(seconds,Playing&&context.IsPlaying&&Application.isFocused)||monsterQueryBusy015)return;
            if(nextTargetId==int.MaxValue||nextTargetId<refugePracticeCycle020.TargetId)return;
            var previous=refugePracticeTarget020;
            if(previous!=null&&refugePracticeCycle020.Matches(previous.TargetId,previous.PoolGeneration015))
            {CompleteMonsterReaders015();RetireEnemy015(previous);}
            refugePracticeTarget020=null;
            var next=SpawnGuardian(world.TutorialMonsterPracticePosition,false,false,visualStyle:MonsterVoxelCosmetics.Style.GuardianGolem);
            if(next==null)return;
            if(!refugePracticeCycle020.BindRepeat(next.TargetId,next.PoolGeneration015))
            {CompleteMonsterReaders015();RetireEnemy015(next);return;}
            next.transform.position=world.TutorialMonsterPracticePosition;
            refugePracticeTarget020=tutorialGuardian=next;
            encounterActive=true;
            targetGrid.Clear();crowdSpacing.Clear();towerAttackSlots.Clear();
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: exact original first-left-alcove foot anchor in existing World metres, one stable-ID/pool-generation lease and bounded active seconds.
// Array Formats: one scalar value-state, one borrowed World/actor reference; existing48actor pool, grids and collision reader fence retain ownership.
// Dependencies/Pointers: original accepted first defeat may earn once; repeat/duplicates return before existing rewards. Offline current World only; trials/lab preempt safely, new leases use existing nonwrapping generation and fresh contact acceptance. No new object graph, collider, Update, network channel or profile key.
