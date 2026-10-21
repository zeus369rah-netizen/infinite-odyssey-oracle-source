namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        public void SetLegacyWorldPresentation020(bool visible)
        {
            if(encounterRoot==null||encounterRoot.gameObject.activeSelf==visible)return;
            if(!visible){CancelHeldFire015();ClearCosmeticShots();}
            encounterRoot.gameObject.SetActive(visible);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing retained encounter root under the world; no geometry, actor or support reconstruction.
// Array Formats: One cold visibility edge over the existing pooled encounter hierarchy.
// Dependencies/Pointers: Accepted hub entry hides the old combat shrine/projectiles/colliders. Existing ResetEncounter restores only positive tutorial/arena modes; no combat authority or progression is granted.
