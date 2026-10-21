namespace OdysseyOracle
{
    public sealed partial class OracleFieldSmith
    {
        public bool IsTalkingToSmith009(NpcBlacksmith actor)=>actor!=null&&smith==actor&&Visible;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted Forge UI/borrowed Smith lifetime, no spatial change.
// Array Formats: Read-only identity/visible predicate only; no event/input/ledger mutation.
// Dependencies/Pointers: Used only to continue that actor's numeric talk animation while its own menu is paused; all range/LOS/transaction owners unchanged.
