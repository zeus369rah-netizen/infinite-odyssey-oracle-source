namespace OdysseyOracle
{
    public static class TutorialInstructionCards
    {
        static readonly string[] Titles={ "Welcome to the refuge", "Levels, experience and skill points", "Meet Raykon", "Your elemental recipes", "Automatic casting timing", "Cosmic Nova", "The anvil between stages", "Training lab", "Tower attack option", "Monster aggression option", "Skip when you are ready", "The party portal" };
        static readonly string[] Resources={
            "Tutorial/tutorial_v2_01_welcome_movement",
            "Tutorial/tutorial_v2_02_levels_spell_menu",
            "Tutorial/tutorial_v2_03_raykon_mentor",
            "Tutorial/tutorial_v2_04_element_recipes",
            "Tutorial/tutorial_v2_05_casting_auto_window",
            "Tutorial/tutorial_v2_06_cosmic_nova",
            "Tutorial/tutorial_v2_07_anvil_between_stages",
            "Tutorial/tutorial_v2_08_training_lab",
            "Tutorial/tutorial_v2_09_tower_attack_toggle",
            "Tutorial/tutorial_v2_10_monster_tower_aggro",
            "Tutorial/tutorial_v2_11_skip_tutorial",
            "Tutorial/tutorial_v2_12_party_portal"
        };
        static readonly string[] Captions={
            "Welcome to the refuge. This short tour introduces your spells, training stations, and the road ahead. Move with W, A, S, and D, and look around with your mouse. Explore at your own pace.",
            "Defeating real enemies earns experience. Experience raises your level and grants skill points. Open the spell menu to spend those points on your abilities. Training dummies are for practice, so they do not award experience or currency.",
            "Raykon is your spell mentor. Find him beside his cauldron in his own alcove, then interact to speak with him. His guidance explains your elemental choices. You can return to him when you want to review the basics.",
            "Select one element for a single spell, two for a combined spell, or all four for a powerful Nova. Three elements cannot cast. If three are selected, add the fourth element or change your selection before trying again.",
            "You have half a second to finish choosing your spell. Each element selection restarts that short window. With automatic casting enabled, the spell fires half a second after your last selection. Manual casting uses the same selection rules.",
            "Choose all four elements to prepare the Nova, a large elemental explosion. This powerful spell has a five second cooldown after it fires. Watch the cooldown indicator, keep moving, and choose the right moment to release it.",
            "The anvil is where you prepare your defenses. Tower upgrades are available between stages, when the fighting has stopped. Use that break to review your towers and choose upgrades before the next wave starts down the road.",
            "The training lab lets you test a stationary dummy. Choose a prototype, lock your choice, then spawn the dummy. You can despawn it when finished. These practice targets give no experience or currency, so use them to learn safely.",
            "Use the tower attack toggle in the training lab to control whether the practice tower fires. Turn it on to observe attacks, or off to study your own spells. The setting lets you test one interaction at a time.",
            "The monster aggression toggle controls whether a practice monster targets the training tower. Enable it to examine tower damage and enemy attacks. Disable it when you want a calmer test. This option belongs to the training lab.",
            "You can skip the guided tutorial whenever you are ready. Skipping removes the lesson dummy and ends the guided steps. The training lab remains available, so you can return to practice without replaying the whole tour.",
            "The portal leads out of the refuge and into the campaign. When playing together, stay with your connected party and let the host lead the transition. Finish your preparations before entering, then defend the road as a team."
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
                case 3:return "FIRE = RED / WATER = BLUE / EARTH = BROWN / AIR = WHITE. One, two or four ingredients can cast; three stay highlighted without spending recovery.";
                case 4:return "The 0.5s settle window applies to AUTO only and restarts after every accepted selection. Manual casts use the 0.5s global interval. Normal ingredients: 4s; Nova: 5s.";
                case 5:return "PLAY NEXT begins reward-free Nova practice offline. Progress requires a real Nova impact; shared rooms continue without private practice actors.";
                case 6:return "Upgrades belong in the refuge before stages 2 and 3. The next offline lesson is a real reward-free tower demonstration, not an upgrade purchase.";
                case 7:return "The separate lab opens after finishing or skipping this tutorial, offline only. Choose one of four prototypes before spawning. No XP or currency.";
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
