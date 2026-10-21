namespace OdysseyOracle
{
    public sealed partial class TutorialDirector
    {
        // Presentation policy only. Root's co-op bootstrap still owns actual free-practice state.
        public bool SuppressAutomaticTutorialPresentation=>session!=null&&
            (session.CoopMode||session.Rooms!=null&&session.Rooms.Connected);

        // Mirrors the existing acceptance preconditions without accepting an interaction.
        // Distance and actual owner checks are repeated by TryStartTowerPractice itself.
        public bool TowerPracticeInteractionReady=>TowerPracticeOwnerContext&&
            towerPracticePhase==TutorialTowerPracticePhase.Instruction&&session.IsPlaying&&
            session.Combat!=null&&session.Builder!=null&&session.Player!=null&&lessonTime>=narrationDuration;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: none; read-only existing session/lesson presentation state.
// Array Formats: scalar policy/readiness predicates; no allocation, actor or native owner.
// Dependencies/Pointers: existing session/rooms, TowerPracticeOwnerContext and TryStartTowerPractice remain authoritative; no completion, reward, pause or save mutation.
