using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleEnemy
    {
        uint remoteLiveStatus009;
        EnemyStatusHandle009 statusHandle009;
        public uint LiveStatus009 => !Targetable?0u:owner!=null&&owner.RemotePresentation?remoteLiveStatus009:
            EnemyLiveStatusRules009.Derive(burnRemaining,slowRemaining,staggerRemaining,earthVulnerabilityRemaining,weakenRemaining);
        internal bool ApplyRemoteLiveStatus009(uint liveStatus)
        {
            if(owner==null||!owner.RemotePresentation||!EnemyLiveStatusRules009.Valid(liveStatus))return false;
            remoteLiveStatus009=Alive?liveStatus:0u;return true;
        }
        void OnEnable(){InitializeStatusHud009();}
        void InitializeStatusHud009()
        {
            if(owner==null||healthBar==null||statusHandle009.Generation!=0)return;
            statusHandle009=owner.RegisterEnemyStatus009();
        }
        void PublishStatusHud009()
        {
            if(owner==null||healthBar==null)return;
            if(statusHandle009.Generation==0)InitializeStatusHud009();
            Vector3 anchor=healthBar.position;
            owner.PublishEnemyStatus009(statusHandle009,anchor,LiveStatus009,PresentedElementTheme,
                Targetable&&healthBar.gameObject.activeInHierarchy);
        }
        void ReleaseStatusHud009()
        {
            if(owner!=null)owner.ReleaseEnemyStatus009(statusHandle009);
            statusHandle009=default;remoteLiveStatus009=0;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual world healthbar anchor and existing actor timer scalars; no target/collision/damage math.
// Array Formats: Known5live bits31 separate from caster12bit passives; one8B generation-bound presentation handle.
// Dependencies/Pointers: Host derives live timers; authenticated replica adapters apply bits only; cold registration/current LateUpdate publish and owner cleanup do not mutate HP.
