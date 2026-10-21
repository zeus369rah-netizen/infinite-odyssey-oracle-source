namespace OdysseyOracle
{
    public static class TutorialInstructionCards
    {
        static readonly string[] Titles={ "Welcome to the refuge", "Levels, experience and skill points", "Meet Raykon", "Your elemental recipes", "Automatic casting timing", "Cosmic Nova", "Building and the Tower Anvil", "Training lab", "Tower attack option", "Monster aggression option", "Skip when you are ready", "The party portal" };
        static readonly string[] Resources={
            "Tutorial/tutorial_v2_01_welcome_movement",
            "Tutorial/tutorial_v2_02_levels_spell_menu",
            "Tutorial/tutorial_v2_03_raykon_mentor",
            "Tutorial/tutorial_v3_04_element_recipes_rapid015",
            "Tutorial/tutorial_v3_05_casting_held_rapid015",
            "Tutorial/tutorial_v2_06_cosmic_nova",
            "Tutorial/tutorial_v5_07_compact_build020",
            "Tutorial/tutorial_v2_08_training_lab_controls_v2",
            "Tutorial/tutorial_v2_09_tower_attack_toggle",
            "Tutorial/tutorial_v2_10_monster_tower_aggro",
            "Tutorial/tutorial_v2_11_skip_tutorial",
            "Tutorial/tutorial_v4_12_party_portal020"
        };
        static readonly string[] Captions={
            "Welcome to the refuge. This short tour introduces your spells, training stations, and the road ahead. Move with W, A, S, and D, and look around with your mouse. Explore at your own pace.",
            "Defeating real enemies earns experience. Experience raises your level and grants skill points. Open the spell menu to spend those points on your abilities. Training dummies are for practice, so they do not award experience or currency.",
            "Raykon is your spell mentor. Find him beside his cauldron in his own alcove, then interact to speak with him. His guidance explains your elemental choices. You can return to him when you want to review the basics.",
            "Prepare one element, a pair, or three. Three selected elements fire together, then clear. Choose all four for Nova.",
            "Hold the left mouse button and press an element key to fire as soon as it is ready. Used elements recover for four seconds. Nova recovers for five. You can also prepare a combination before clicking to cast.",
            "Choose all four elements to prepare the Nova, a large elemental explosion. This powerful spell has a five second cooldown after it fires. Watch the cooldown indicator, keep moving, and choose the right moment to release it.",
            "In the arena, press Interact beside your lane to open the tower picker. Scroll to choose a tower, then aim at a green floor block on flat land within two blocks of the road edge. Press Interact again to place it. A red floor box and message explain why a spot is blocked. Roads, water and lava cannot be built on. Visit the Tower Anvil in the refuge between stages to upgrade.",
            "In the offline training lab, choose a monster and a tower separately. Use Replace or Add to apply each choice. Removing or replacing an actor turns both attacks off. Practice gives no experience or currency.",
            "Use the tower attack toggle in the training lab to control whether the practice tower fires. Turn it on to observe attacks, or off to study your own spells. The setting lets you test one interaction at a time.",
            "The monster aggression toggle controls whether a practice monster targets the training tower. Enable it to examine tower damage and enemy attacks. Disable it when you want a calmer test. This option belongs to the training lab.",
            "You can skip the guided tutorial whenever you are ready. Skipping removes the lesson dummy and ends the guided steps. The training lab remains available, so you can return to practice without replaying the whole tour.",
            "The portal leads to tower defense. Play alone or with one friend. Defend the Hearthstone from waves, then return to the refuge between stages to upgrade. The wider campaign is still in development."
        };
        public static string Title(int card)=>Valid(card)?Titles[card]:"";
        public static string Resource(int card)=>Valid(card)?Resources[card]:"";
        public static string Caption(int card)=>Valid(card)?Captions[card]:"";
        public static string Note(int card)
        {
            switch(card)
            {
                case 0:return "PLAY narration once, or read the caption. PLAY NEXT begins real movement practice.";
                case 1:return "PLAY NEXT opens the real spell menu. Learn costs 1 real skill point. If you have none, inspect a node and return after collecting real XP; this card grants no points.";
                case 2:return "Raykon appears in his own alcove now. Visiting him is optional here; only a real nearby interaction opens his choices.";
                case 3:return "FIRE = RED / WATER = BLUE / EARTH = BROWN / AIR = WHITE. One ingredient casts a single, two a pair, three a simultaneous volley that clears, and four a Nova.";
                case 4:return "AUTO waits at least 0.5s after the latest accepted selection, plus any remaining global/ingredient cooldown. Manual and an explicit held-click element press skip only that selection wait. A held-key gesture uses fresh pressed keys and clears after acceptance. Global interval 0.5s; normal ingredients 4s; Nova 5s.";
                case 5:return "PLAY NEXT begins reward-free Nova practice offline. Progress requires a real Nova impact; shared rooms continue without private practice actors.";
                case 6:return "ARENA BUILDING: within two metres of your locked road edge. Fire and Earth target ground enemies; Water and Air target flyers. Shared tower cap: 2 in waves 1-2, 3 in waves 3-4, 4 from wave 5. Only the owner may Remove (no refund) or Replace (one remaining placement token). Upgrade at the Tower Anvil between stages. Offline practice grants no rewards.";
                case 7:return "The separate lab opens after finishing or skipping this tutorial, offline only. Select pending monster/tower types, then Replace/Add each side. Remove/Replace resets both attacks OFF. No XP or currency.";
                case 8:return "Lab option state uses its ON/OFF label; it does not change the element colors.";
                case 9:return "This separate lab option controls only practice monster aggression. It does not alter campaign enemy targeting.";
                case 10:return "Pause > SKIP TUTORIAL keeps the current connected party. Skipping is not practice completion.";
                case 11:return "PLAY NEXT continues the safe recovery and shop exercises. The portal still waits for actual tutorial completion or an explicit Skip.";
                default:return "";
            }
        }
        static bool Valid(int card)=>card>=0&&card<TutorialInstructionState.CardCount;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: none; immutable caption/resource metadata. Array Formats: twelve fixed entries in resource/title/caption order from SCRIPT_FREEZE schema2.
// Dependencies/Pointers: only presentation value state; no actor, network, reward, native memory or casting mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/lab modal and focus policy; no actor, world or saved progression mutation.
// Array Formats: Existing twelve guide clips plus two cold-loaded lab help references, one selected-caption index.
// Dependencies/Pointers: Sole TutorialDirector guide AudioSource; exact journal caption/resource pairs; root owns import/playback.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up/XZ, exact1m terrain/.25m build roots and locked active-road context.
// Array Formats: Bounded native semantic/preview/body stores, exact64B versioned command and existing2peer/4tower snapshots.
// Dependencies/Pointers: Root integrates exact guarded slices; host economy/readiness/LOS and World geometry remain canonical, prepared/source compile is not native or browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Immutable guide presentation metadata, no actor or economy coordinates.
// Array Formats: One corrected caption/resource pair among twelve fixed guide cards.
// Dependencies/Pointers: Preserved v3 original; new ElevenLabs v4 recording and exact spoken-script provenance. Root owns native import and playback acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: One non-spatial tutorial-card narration reference.
// Array Formats: Twelve fixed caption slots; only card7 resource changes.
// Dependencies/Pointers: Verified existing af_heart preset, exact caption hash; original recordings preserved.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted NPC/guide audio ownership and immutable guide note metadata.
// Array Formats: Existing fixed guide clip/caption/sample stores; no new frame storage or caption generation.
// Dependencies/Pointers: All-role NPC lease pauses automatic guide intent; explicit eligible Play alone preempts/resumes. Root v5 resource/voiced captions remain exact; root owns integration and native/browser listening.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: One non-spatial portal guide card; no campaign or room state changes.
// Array Formats: Exact matching af_heart clip/caption pair among twelve cards.
// Dependencies/Pointers: Wider campaign remains in development; real solo/two-peer tower-defense admission remains authoritative.
