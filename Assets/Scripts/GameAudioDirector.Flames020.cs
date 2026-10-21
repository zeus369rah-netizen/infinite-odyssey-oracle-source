using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GameAudioDirector
    {
        AudioClip windExtinguish020;
        RefugeFlameReceipt020 windExtinguishReceipt020;
        DungeonWorld windExtinguishWorld020;
        public int WindExtinguishAttempts020{get;private set;}
        public int WindExtinguishPlayed020{get;private set;}
        void PrepareWindExtinguish020(){windExtinguish020=Resources.Load<AudioClip>("Audio/RefugeSound020/wind_extinguish020");Prime(windExtinguish020);}
        public bool TryPlayWindExtinguish020(Vector3 position,uint epoch,uint generation,uint revision)
        {
            if(session==null||session.World==null||!session.World.IsTutorialRefuge||session.World.IsCoopArena)return false;
            if(windExtinguishWorld020!=session.World){windExtinguishWorld020=session.World;windExtinguishReceipt020.Reset();}
            if(!windExtinguishReceipt020.Accept(epoch,generation,revision))return false;
            if(WindExtinguishAttempts020<int.MaxValue)WindExtinguishAttempts020++;
            if(!isActiveAndEnabled||!CanPlayCue()||!session.HasStarted||!session.SimulationActive||session.IsDead||session.Victory||!Near(position,30f))return false;
            bool played=PlayPrioritizedCue(windExtinguish020,position,true,.075f,1,.04f);
            if(played&&WindExtinguishPlayed020<int.MaxValue)WindExtinguishPlayed020++;return played;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: one accepted aggregate flame-off world anchor per current refuge owner and epoch/generation/revision; never per node/frame.
// Array Formats: one cold borrowed root-imported1.25s resource, fixed scalar receipt/counters and existing prioritized4voice pool; no new source/PCM/material or hot Resources load.
// Dependencies/Pointers: existing focus/gesture/mute/playing/range/pool policy may reject explicitly without replay queues. Missing media stays silent; receipt suppresses duplicate attempts even after rejection. Actual native/browser listening remains root-owned.
