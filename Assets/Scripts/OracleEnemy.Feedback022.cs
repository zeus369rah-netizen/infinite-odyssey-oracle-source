// PSEUDOCODE: bind the original cold pool slot; expose generation-fenced actual
// health; apply only owner-admitted horizontal steps; suppress fixed practice
// targets and unknown future archetypes without guessing a boss from its size.
using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class OracleEnemy
    {
        internal int FeedbackSlot022{get;private set;}=-1;
        bool feedbackTransaction022;
        static readonly int feedbackBaseColor022=Shader.PropertyToID("_BaseColor"),feedbackColor022=Shader.PropertyToID("_Color");
        internal Vector3 FeedbackAnchor022=>healthBar!=null?healthBar.position:AimPoint;
        internal bool FeedbackMovementAllowed022=>owner!=null&&!owner.RemotePresentation&&!TrainingLabBound&&
            !owner.IsTutorialTowerPracticeTarget(this)&&!owner.IsRefugePracticeRepeat020(this)&&
            (Archetype==MonsterArchetype.Stalker||Archetype==MonsterArchetype.Shade||Archetype==MonsterArchetype.Spider);
        internal void BindFeedbackSlot022(int slot)
        {if(FeedbackSlot022!=-1||slot<0||slot>=48)throw new System.InvalidOperationException("One original cold pool slot is required.");FeedbackSlot022=slot;}
        public bool TryReadFeedbackHealth022(out EnemyHealthReadout022 readout)
        {readout=default;return owner!=null&&owner.TryReadEnemyHealth022(this,out readout);}
        internal void ApplyFeedbackStep022(Vector3 move)
        {transform.position+=move;crowdMotionFrame=Time.frameCount;}
        void ClearFeedbackPresentation022()
        {flash=0;owner?.ClearEnemyKick022(this);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing actor foot-root metres/Y-up and cached world healthbar anchor.
// Array Formats: One immutable pool slot and a transaction guard; no new mesh/material/renderer/array/Update owner.
// Dependencies/Pointers: Original three actual archetypes are ordinary; unknown future types are immune pending explicit boss metadata. Existing MPB/pooled-style reset restores each next life; numeric owner proves each movement before this adapter applies it.
