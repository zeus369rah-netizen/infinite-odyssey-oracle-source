namespace OdysseyOracle
{
    public static class TrainingLabVoiceGuides
    {
        public const int Count=2;
        public static string Resource(int guide)=>guide==0?"Tutorial/training_lab_safe_attacks_v1":
            guide==1?"Tutorial/training_lab_replace_remove_v1":"";
        public static string Caption(int guide)=>guide==0?
            "Both training attacks start off. Turn Tower Attack on to let the practice tower fire at the monster. Turn Monster Attack Tower on to let the stationary monster hit the tower. You control each option separately. Training grants no rewards.":guide==1?
            "Choose a monster or tower type, then use Replace or Add for that side. Selection alone changes nothing. Remove Monster and Remove Tower clear one side; Remove All clears both. Every remove or replace turns both attacks off and cancels pending attacks.":"";
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: None; exact immutable caption/resource metadata for two explicit-play help clips.
// Array Formats: Two fixed string branches, no per-frame construction or mutable player/game state.
// Dependencies/Pointers: Exact stock af_heart generation journal owns text provenance; existing Tutorial voice owns playback.
