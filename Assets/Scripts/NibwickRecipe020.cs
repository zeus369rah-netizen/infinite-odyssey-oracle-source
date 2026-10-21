using static OdysseyOracle.CampaignNpcs.NpcModelBuilder;

namespace OdysseyOracle.CampaignNpcs
{
    // Independent original prototype. Role zero is deliberately unbound authoring
    // data; this must never be cast into an existing campaign/quest role slot.
    public static class NibwickRecipe020
    {
        public const string Key = "campaign-hub020.nibwick";
        public const int PartCount = 13, PartCeiling = 17;
        public const int Pelvis = 0, Torso = 1, Head = 2, Mouth = 3, Eyelids = 4;
        public const int LeftEar = 5, RightEar = 6, LeftArm = 7, RightArm = 8;
        public const int LeftLeg = 9, RightLeg = 10, Satchel = 11, Bell = 12;

        static int Part(NpcRecipe r, string name, int parent, float x, float y, float z)
        {
            r.Parts.Add(new NpcPart { Name = name, Parent = parent, Pivot = new NpcVector(x, y, z) });
            return r.Parts.Count - 1;
        }
        static NpcModelBuilder Model(NpcRecipe r, int part) => new NpcModelBuilder(r.Parts[part]);

        public static NpcRecipe Build()
        {
            var r = new NpcRecipe
            {
                Role = (RefugeNpcRole)0,
                Name = "Nibwick",
                RoleLabel = "Welcome-bell keeper",
                Identity = "Original small emerald gremlin, broad angular ears, uneven brows, blue coat, copper bell and well-used satchel."
            };
            Part(r, "Supported pelvis and coat hem", -1, 0, .36f, 0);
            Part(r, "Breathing blue torso", Pelvis, 0, .20f, 0);
            Part(r, "Gaze gremlin head", Torso, 0, .26f, 0);
            Part(r, "Accepted-line mouth opening", Head, 0, -.075f, .192f);
            Part(r, "Blink emerald lids", Head, 0, .075f, .212f);
            Part(r, "Left angular ear", Head, -.205f, .055f, -.01f);
            Part(r, "Right angular ear", Head, .205f, .055f, -.01f);
            Part(r, "Left pointing shoulder", Torso, -.19f, .10f, 0);
            Part(r, "Right bell shoulder", Torso, .19f, .10f, 0);
            Part(r, "Left supported leg", Pelvis, -.09f, -.025f, 0);
            Part(r, "Right supported leg", Pelvis, .09f, -.025f, 0);
            Part(r, "Patched satchel and keys", Pelvis, -.19f, .02f, -.08f);
            Part(r, "Copper welcome bell and tag", RightArm, .045f, -.26f, .085f);
            r.Torso = Torso; r.Head = Head; r.Mouth = Mouth; r.Eyelids = Eyelids;
            r.LeftArm = LeftArm; r.RightArm = RightArm;

            var m = Model(r, Pelvis);
            m.Bevel(0, 0, 0, .30f, .18f, .22f, .02f, Charcoal);
            m.Box(0, -.055f, .025f, .33f, .06f, .25f, Blue);
            m.Box(0, .055f, .13f, .31f, .055f, .025f, Hair);
            m.Box(.025f, .055f, .15f, .055f, .05f, .02f, Copper);
            m.Box(-.065f, -.045f, .16f, .055f, .055f, .018f, Ivory);

            m = Model(r, Torso);
            m.Bevel(0, .045f, 0, .31f, .30f, .23f, .025f, Blue);
            m.Box(0, .035f, .13f, .16f, .24f, .03f, Charcoal);
            m.Box(0, .18f, 0, .27f, .065f, .25f, Teal);
            m.Box(-.055f, .135f, .15f, .065f, .11f, .018f, Teal);
            m.Box(.075f, -.02f, .15f, .075f, .065f, .02f, Ivory);
            m.Box(.075f, -.02f, .165f, .04f, .012f, .012f, Blue);
            for (int i = 0; i < 3; ++i) m.Box(-.025f, .12f - i * .075f, .16f, .02f, .02f, .014f, Copper);
            m.Rod(new NpcVector(.14f, .16f, .14f), new NpcVector(-.13f, -.07f, .14f), .025f, Hair);

            m = Model(r, Head);
            m.Bevel(0, .045f, 0, .40f, .31f, .33f, .035f, Emerald);
            m.Bevel(0, -.07f, .09f, .26f, .10f, .21f, .015f, Emerald);
            m.Bevel(0, -.005f, .205f, .095f, .095f, .14f, .014f, Emerald);
            m.Box(0, -.032f, .28f, .05f, .026f, .018f, Hair);
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(side * .09f, .075f, .181f, .09f, .065f, .018f, EyeWhite);
                m.Box(side * .085f, .075f, .198f, .028f, .044f, .014f, Ink);
                m.Box(side * .08f, .09f, .208f, .009f, .012f, .006f, EyeWhite);
                m.Box(side * .135f, -.025f, .168f, .055f, .04f, .016f, PaleSkin);
            }
            m.Rod(new NpcVector(-.15f, .132f, .195f), new NpcVector(-.055f, .145f, .195f), .025f, Hair);
            m.Rod(new NpcVector(.055f, .135f, .195f), new NpcVector(.15f, .118f, .195f), .025f, Hair);
            m.Box(.04f, -.065f, .205f, .032f, .038f, .026f, Ivory);
            m.Box(-.07f, .207f, -.035f, .065f, .065f, .085f, Hair);
            m.Box(.015f, .22f, -.055f, .06f, .035f, .075f, Hair);
            m.Box(.085f, .195f, -.045f, .045f, .055f, .07f, Hair);

            m = Model(r, Mouth); m.Box(0, 0, 0, .13f, .012f, .014f, Ink);
            m = Model(r, Eyelids);
            for (int side = -1; side <= 1; side += 2) m.Box(side * .09f, 0, 0, .095f, .07f, .008f, Emerald);

            Ear(r, LeftEar, -1); Ear(r, RightEar, 1);
            Arm(r, LeftArm, -1); Arm(r, RightArm, 1);
            Leg(r, LeftLeg); Leg(r, RightLeg);

            m = Model(r, Satchel);
            m.Bevel(0, 0, 0, .15f, .19f, .12f, .015f, Hair);
            m.Box(0, .05f, .07f, .13f, .08f, .02f, Copper);
            m.Box(0, .015f, .085f, .025f, .04f, .012f, Ink);
            m.Box(.055f, -.08f, .075f, .025f, .10f, .02f, Copper);
            m.Box(.075f, -.12f, .075f, .06f, .02f, .025f, Copper);
            m.Box(-.045f, -.035f, .075f, .055f, .05f, .015f, Ivory);

            m = Model(r, Bell);
            m.Rod(new NpcVector(0, 0, 0), new NpcVector(0, -.045f, 0), .025f, Copper);
            m.Bevel(0, -.095f, 0, .14f, .12f, .14f, .015f, Copper);
            m.Box(0, -.155f, 0, .17f, .025f, .17f, Ivory);
            m.Box(0, -.175f, 0, .035f, .04f, .035f, Ink);
            m.Crystal(0, -.09f, .084f, .026f, .07f, LanternLight);
            m.Rod(new NpcVector(-.09f, -.025f, 0), new NpcVector(-.09f, -.12f, 0), .014f, Hair);
            m.Box(-.09f, -.14f, 0, .055f, .07f, .014f, Ivory);
            m.Box(-.09f, -.13f, .01f, .035f, .006f, .008f, Blue);
            m.Box(-.09f, -.15f, .01f, .035f, .006f, .008f, Blue);

            if (r.Parts.Count != PartCount || r.Parts.Count > PartCeiling)
                throw new System.InvalidOperationException("Nibwick prototype part budget changed.");
            Validate(r);
            return r;
        }

        static void Ear(NpcRecipe r, int index, int side)
        {
            var m = Model(r, index);
            float length = side < 0 ? .15f : .135f;
            m.Rod(new NpcVector(0, 0, 0), new NpcVector(side * length, .085f, -.02f), .105f, Emerald);
            m.Bevel(side * (length + .018f), .095f, -.02f, .055f, .06f, .065f, .008f, Emerald);
            m.Rod(new NpcVector(side * .015f, .005f, .055f), new NpcVector(side * (length - .005f), .075f, .035f), .045f, PaleSkin);
            if (side < 0) m.Box(-.10f, -.025f, .06f, .035f, .04f, .025f, Copper);
        }
        static void Arm(NpcRecipe r, int index, int side)
        {
            var m = Model(r, index);
            m.Bevel(side * .01f, -.08f, 0, .13f, .18f, .16f, .014f, Blue);
            m.Box(side * .015f, -.145f, .01f, .135f, .04f, .17f, Teal);
            m.Rod(new NpcVector(side * .015f, -.14f, .01f), new NpcVector(side * .045f, -.24f, .06f), .075f, Emerald);
            m.Bevel(side * .045f, -.26f, .085f, .10f, .09f, .10f, .012f, Emerald);
        }
        static void Leg(NpcRecipe r, int index)
        {
            var m = Model(r, index);
            m.Rod(new NpcVector(0, 0, 0), new NpcVector(0, -.14f, .025f), .105f, Charcoal);
            m.Rod(new NpcVector(0, -.13f, .025f), new NpcVector(0, -.235f, .025f), .09f, Hair);
            m.Bevel(0, -.255f, .055f, .14f, .15f, .24f, .014f, Charcoal);
            m.Box(0, -.31f, .065f, .15f, .05f, .25f, Copper);
            m.Box(0, -.225f, .182f, .105f, .024f, .018f, Teal);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original unscaled metres, +Y up/+Z face, neutral boot soles atY0; thirteen parent-before-child rigid parts.
// Array Formats: Cold NpcRecipe/NpcPart data uses the existing flat position/normal/paint/index builder and shared16-color palette, under2500 triangles/17parts.
// Dependencies/Pointers: Authoring-only role0 is never a gameplay role. Root caches accepted meshes once, borrows palette and binds bounded pose/voice/navigation adapters; no new runtime owner or provider call.
