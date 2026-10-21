using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class ShrinePlacementBuilder
    {
        WorkshopBuildPool015 workshop015;bool workshopSnapshotPrimed015;uint workshopSnapshotSemantic015;
        public bool WorkshopReady015=>workshop015!=null;
        void InitializeWorkshop015()
        {
            if(workshop015!=null||towerModelLibrary==null)return;
            try{workshop015=new WorkshopBuildPool015(transform,towerModelLibrary,0,uint.MaxValue);}catch(System.Exception e){Debug.LogWarning("Original workshop visuals unavailable: "+e.Message);}
        }
        internal bool PresentAcceptedWorkshop015(int id,int revision,bool accepted)
        {
            if(!isActiveAndEnabled||workshop015==null||!placed.TryGetValue(id,out var tower)||tower==null||tower.IsDestroyed)return false;
            var model=tower.WorkshopModel015;if(model==null)return false;
            return workshop015.Bind(id,revision,tower.Element,model.AcceptedTier,model,null,tower.transform,accepted,Time.realtimeSinceStartupAsDouble);
        }
        void TickWorkshop015(){workshop015?.Tick(Time.realtimeSinceStartupAsDouble,OracleBookAccessibility.PrefersReducedMotion);}
        internal bool BeginWorkshopSnapshot015(bool sameRun)
        {if(!isActiveAndEnabled){ClearWorkshop015();return false;}uint semantic=session!=null&&session.World!=null?session.World.LaneBuildSemantic015:0;bool delta=sameRun&&workshopSnapshotPrimed015&&semantic!=0&&semantic==workshopSnapshotSemantic015;workshopSnapshotSemantic015=semantic;workshopSnapshotPrimed015=semantic!=0;return delta;}
        void ClearWorkshop015(){workshop015?.Clear();workshopSnapshotPrimed015=false;workshopSnapshotSemantic015=0;}
        void RemoveWorkshop015(int id){workshop015?.Remove(id);}
        void DisposeWorkshop015(){workshop015?.Dispose();workshop015=null;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted tower roots in current world; preview ghost excluded and atlas owns a separate lease.
// Array Formats: One cold capacity4 pool, cached placed registry and accepted id/revision/type; no hot Resources loads or effect Object creation.
// Dependencies/Pointers: Called only after host commit or authenticated replica baseline/delta; sound follows true visual admission via separate audio owner. Reset/remove/disable restore and detach before tower teardown.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing cosmetic tower-local floor and accepted snapshot identity only; roots/collision/authority unchanged.
// Array Formats: Existing fixed four-slot Clock32/Pose32 leases; same-tier metadata advances Revision without replacing the active lease.
// Dependencies/Pointers: Enabled Builder alone admits effects/baselines; disable invalidates snapshot state. Work-only reviewed follow-up; native/render/peer acceptance remains root-owned.
