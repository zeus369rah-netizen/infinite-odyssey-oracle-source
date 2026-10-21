using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class SavedTowerModelPresentation
    {
        internal bool WorkshopLive015=>initialized&&!destroyed&&isActiveAndEnabled;
        internal void ApplyWorkshopScale015(float rise,float floor)
        {
            if(!initialized||!float.IsFinite(rise)||!float.IsFinite(floor))return;rise=Mathf.Clamp(rise,.08f,1);
            var pivot=TowerModelLibrary.Vector(template.Recipe.HeadPivot);
            groups[0].localScale=new Vector3(1,rise,1);groups[0].localPosition=new Vector3(0,floor*(1-rise),0);
            groups[1].localScale=new Vector3(1,rise,1);groups[1].localPosition=new Vector3(pivot.x,floor+(pivot.y-floor)*rise,pivot.z);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Only cached cosmetic Foundation/Head local-Y scale about actual saved minimumY; accepted root/footprint/collider and child attack clocks are unchanged.
// Array Formats: Two cached Transform adapters and scalar completed-job pose, no arrays/search/allocation or model/material mutation.
// Dependencies/Pointers: Builder-owned workshop lease alone calls this visual adapter after Complete; cancellation restores1. No purchase/damage/cooldown/target or native pose authority.
