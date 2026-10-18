namespace OdysseyOracle
{
    public enum TutorialTowerPracticePhase { Inactive, Instruction, Preparing, Running, Complete }

    public static class TutorialTowerPracticeRules
    {
        public static bool CanOwn(bool activeLesson,bool finished,bool hasStarted,bool coop,
            bool roomConnected,bool arena,bool defenseRunning,bool dead,bool downed,bool victory) =>
            activeLesson&&!finished&&hasStarted&&!coop&&!roomConnected&&!arena&&!defenseRunning&&!dead&&!downed&&!victory;

        public static bool CanAct(TutorialTowerPracticePhase phase,bool ownerContext,bool playing) =>
            phase==TutorialTowerPracticePhase.Running&&ownerContext&&playing;

        public static bool AcceptedTowerDefeat(bool matchingOwner,bool matchingActors,float before,float after) =>
            matchingOwner&&matchingActors&&!float.IsNaN(before)&&!float.IsInfinity(before)&&
            !float.IsNaN(after)&&!float.IsInfinity(after)&&before>0f&&after>=0f&&after<before&&after==0f;
    }
}

// # ARCHITECTURE SNAPSHOT
// Pure scalar offline-tutorial authorization and observed existing-owner HP result; no spatial/pointer/native/save state.
// It neither applies damage nor accepts network/player reward transactions; paused/context-invalid practice cannot act.
