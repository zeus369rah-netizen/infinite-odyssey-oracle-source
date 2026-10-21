using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        public event System.Action<int,int,int,Vector3,bool> AuthoritativeSpellImpact;
        public int AcceptedImpactFeedbackFailures{get;private set;}
        void RecordImpactFeedbackFailure(){if(AcceptedImpactFeedbackFailures<int.MaxValue)AcceptedImpactFeedbackFailures++;}
        void NotifyAcceptedSpellImpact(int shotId,int ordinal,int element,Vector3 point,bool terminal,int combo020=0)
        {
            if(element<0||element>3||ordinal<0||ordinal>2||!FinitePosition(point))return;
            // Presentation failures cannot abort damage resolution or pooled-flight retirement.
            try{GameSession.Instance?.Audio?.PlayPairImpact020(element,combo020,point);}catch(System.Exception){RecordImpactFeedbackFailure();}
            try{if(shotId>0)AuthoritativeSpellImpact?.Invoke(shotId,ordinal,element,point,terminal);}catch(System.Exception){RecordImpactFeedbackFailure();}
        }
        public void PlayAcceptedNetworkImpact(int shotId,int element,Vector3 point,bool terminal)
        {PlayAcceptedPairNetworkImpact020(shotId,element,point,terminal,0);}
        public void PlayAcceptedPairNetworkImpact020(int shotId,int element,Vector3 point,bool terminal,int combo020)
        {
            var session=GameSession.Instance;
            if(!initialized||!remoteSimulation||session==null||!session.SimulationActive||!session.CombatUnlocked||shotId<=0||element<0||element>3||!FinitePosition(point))return;
            SpawnPairImpact020(point,SpellColors[element],element==2?.85f:.45f,SpellPairPresentation020.Allows(combo020,element)?combo020:0);session.Audio?.PlayPairImpact020(element,combo020,point);
            if(terminal)
                for(int i=0;i<cosmeticShots.Length;i++)
                {var shot=cosmeticShots[i];if(shot!=null&&shot.NetworkShotId==shotId&&shot.Root.gameObject.activeSelf)shot.Root.gameObject.SetActive(false);}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: real accepted collision points from existing host projectile; guest consumes only after its owning protocol receipt validation.
// Array Formats: existing12 cosmetic shot leases/impact pool; scalar event signature includes shot ID, ordinal0..2, element, contact and terminal flag.
// Dependencies/Pointers: existing host damage/selection/launch/Nova policies unchanged; no speculative collision/audio publication. Audio uses existing owner/gesture/focus/pool policy; actual peer/audio acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world contacts, projectile-local three-part visuals and cached shot identity; physical collision and gameplay semantics unchanged.
// Array Formats: Existing48/12 bolt pools,20 three-part impact slots, fixed six numeric/cue recipes and one extra local combo integer per accepted-shot receipt;64B/256B wire formats unchanged.
// Dependencies/Pointers: Existing host acceptance/dedupe/expiry and audio voice policies remain canonical. Presentation failures grant no damage, status, recovery or authority; root owns actual renderer/listening/browser/peer/performance acceptance.
