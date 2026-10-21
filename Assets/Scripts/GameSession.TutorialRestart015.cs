namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        public bool CanRestartTutorial015=>HasStarted&&!CoopMode&&(Rooms==null||!Rooms.Connected)&&World!=null&&World.IsTutorialRefuge&&!World.IsCoopArena&&
            !IsDead&&!IsDowned&&!Victory&&(Loading==null||!Loading.IsLoading)&&(Opening==null||!Opening.IsIntroPlaying&&!Opening.IsTransitioning)&&
            (Shop==null||!Shop.Visible)&&(FieldSmith==null||!FieldSmith.Visible)&&(Builder==null||Builder.PlacedCount==0&&!Builder.IsPlacing);
        public bool RestartTutorial015()
        {
            if(!CanRestartTutorial015||Tutorial==null||Hud==null)return false;
            RefugeActivities?.StopMechanicsGuide();TrainingLab?.EndSession();Combat?.ResetEncounter();CombatUnlocked=false;
            Hud.ClosePanels();Tutorial.StartTutorial();Resume();return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact existing offline refuge and owned tutorial/lab lifetime; no teleport, HP, XP, saved economy or world rebuild.
// Array Formats: Scalar admission only; canonical StartTutorial resets per-run presentation/lesson state.
// Dependencies/Pointers: Explicit Restart UI only. No ResetProgression/Restart/new room; live placed builds reject the request instead of being deleted.
