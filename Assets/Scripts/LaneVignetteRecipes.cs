using System;

namespace OdysseyOracle
{
    public sealed class LaneVignetteBuildResult
    {
        public readonly int[] Triangles = new int[8], Trees = new int[8], Buildings = new int[8], GrassTufts = new int[8];
        public int Pieces, TrunkProxies, SceneryProxies;
        public int TotalTriangles { get { int count = 0; for (int i = 0; i < 8; i++) count += Triangles[i]; return count; } }
    }

    // # ARCHITECTURE SNAPSHOT
    // Original static architecture + anchored canopy meshes, emitted at build time.
    // Material/mesh/collider lifetime belongs to the World presentation adapter.
    // Two trees per road are independent of active lanes; wind is GPU-only cosmetic.
    public static class LaneVignetteRecipes
    {
        public const int MaximumTrianglesPerLane = 2400, MaximumTrianglesTotal = 19200;
        public const int TreesPerLane = 2, MaximumCanopyPartsPerTree = 8;
        public const double MaximumWindDisplacement = .80;
        // Four core road families, with lava/ice/crystal/storm companion vistas only.
        private static int Appearance(int lane)=>lane==0?0:lane==1?7:lane==2?6:lane==3?5:lane==4?3:lane==5?4:lane==6?2:1;
        private static readonly double[] HeroHeights = { 32, 29, 27, 30, 26, 28, 31, 25 };
        private static readonly double[] CompanionHeights = { 20, 22, 19, 21, 24, 20, 22, 19 };

        public static LaneVignetteBuildResult Build(LaneVignetteLayout layout, LaneVignetteWriter write,
            LaneVignetteColliderWriter optionalTrunkProxy = null)
        {
            if (layout == null || write == null) throw new ArgumentNullException();
            layout.ResetReservations();
            var c = new Context(layout, write, optionalTrunkProxy);
            // Reserve all hero roots before companions/architecture: no lane-count-dependent art.
            for (int lane = 0; lane < 8; lane++) c.Tree(lane, true, HeroHeights[lane]);
            for (int lane = 0; lane < 8; lane++) c.Tree(lane, false, CompanionHeights[lane]);
            for (int lane = 0; lane < 8; lane++) { c.Building(lane); c.Grass(lane); c.OuterVista(lane); }
            if (c.Result.TotalTriangles > MaximumTrianglesTotal) throw new InvalidOperationException("Vignette arena triangle cap exceeded.");
            return c.Result;
        }

        private sealed class Context
        {
            public readonly LaneVignetteBuildResult Result = new LaneVignetteBuildResult();
            private readonly LaneVignetteLayout layout;
            private readonly LaneVignetteWriter write;
            private readonly LaneVignetteColliderWriter collider;
            private VignettePoint origin;
            private int lane;
            private double dx, dz, tx, tz, yaw;
            public Context(LaneVignetteLayout layout, LaneVignetteWriter write, LaneVignetteColliderWriter collider)
            { this.layout = layout; this.write = write; this.collider = collider; }
            private void Set(int index, VignettePoint anchor)
            {
                lane = index; origin = anchor; double a = index * Math.PI / 4;
                dx = Math.Cos(a); dz = Math.Sin(a); tx = -dz; tz = dx; yaw = 90 - index * 45;
            }
            private VignettePoint Position(double x, double y, double z)
                => new VignettePoint(origin.X + tx * x + dx * z, origin.Y + y, origin.Z + tz * x + dz * z);
            private void Emit(LaneVignettePiece piece)
            {
                if (Result.Triangles[lane] + piece.Triangles > MaximumTrianglesPerLane)
                    throw new InvalidOperationException("Vignette lane triangle cap exceeded.");
                write(piece); Result.Triangles[lane] += piece.Triangles; Result.Pieces++;
            }
            private void Box(VignetteSurface surface, double x, double y, double z, double w, double h, double d)
            {
                if (w <= 0 || h <= 0 || d <= 0) throw new InvalidOperationException("Positive voxel dimensions required.");
                Emit(new LaneVignettePiece(lane, VignettePrimitive.Box, surface, Position(x, y, z), new VignettePoint(w, h, d), yaw));
            }
            private void Top(VignettePrimitive kind, VignetteSurface surface, double x, double y, double z, double w, double d)
                => Emit(new LaneVignettePiece(lane, kind, surface, Position(x, y, z), new VignettePoint(w, 0, d), yaw));

            public void Tree(int index, bool hero, double height)
            {
                if (!layout.TryAnchor(index, hero, .82, out VignettePoint root))
                    throw new InvalidOperationException("No safe tree root. Re-author the layout; do not place inside a route.");
                int theme=Appearance(index);
                double crown = hero ? 9.5 : 9.0;
                if (Math.Sqrt(root.X * root.X + root.Z * root.Z) + crown + MaximumWindDisplacement > layout.ArenaRadius)
                    throw new InvalidOperationException("Tree crown exceeds the supplied arena boundary.");
                Set(index, root);
                VignetteSurface bark = theme == 0 || theme == 7 ? VignetteSurface.DarkStone : VignetteSurface.Wood;
                double width = hero ? 1.15 : .88, lean = theme == 2 ? 1.8 : theme == 3 ? -.8 : .35;
                Box(bark, 0, height * .33, 0, width, height * .66, width);
                Box(bark, lean * .45, height * .74, 0, width * .82, height * .22, width * .82);
                Box(bark, -crown * .20, height * .82, 0, crown * .53, .54, .60);
                Box(bark, crown * .20 + lean * .25, height * .845, 0, crown * .53, .54, .60);
                if (collider != null)
                {
                    collider(new VignetteTrunkProxy(lane, Position(0, height * .33, 0), new VignettePoint(width, height * .66, width),VignetteProxyKind.Trunk,yaw));
                    Result.TrunkProxies++;
                }
                for (int part = 0; part < MaximumCanopyPartsPerTree; part++)
                {
                    double angle = part * Math.PI * .25;
                    double spread = part == 0 ? 0 : crown * .34;
                    double x = Math.Cos(angle) * spread + lean, z = Math.Sin(angle) * spread;
                    double y = height * (.89 + (part % 3 - 1) * .035);
                    double w = crown * (part == 0 ? .98 : .73), d = crown * .66, h = height * .13;
                    // Eight deliberately different silhouettes without multiplying mesh count.
                    if (theme == 2) { x += (part % 3) * .35; h *= .58; w *= .95; }
                    if (theme == 3) { y -= part % 2 * height * .09; h *= 1.45; d *= .70; }
                    if (theme == 4) { y = height * (.61 + part * .043); w = d = crown * (1.03 - part * .085); h *= .76; x = z = 0; }
                    if (theme == 5) { w *= .65; d *= .65; h *= 1.65; y += (part % 2) * height * .025; }
                    if (theme == 7) { h *= .58; y -= part % 2 * height * .05; }
                    Palette(theme, part, out double r, out double g, out double b, out double glow);
                    Emit(new LaneVignettePiece(lane, VignettePrimitive.CanopyBox, VignetteSurface.Green,
                        Position(x, y, z), new VignettePoint(w, h, d), yaw, root, height, r, g, b, glow));
                }
                Result.Trees[index]++;
            }

            private static void Palette(int theme, int part, out double r, out double g, out double b, out double glow)
            {
                glow = 0;
                switch (theme)
                {
                    case 0: r = .83; g = part % 3 == 0 ? .56 : .31; b = .08; glow = part % 3 == 0 ? .18 : 0; break;
                    case 1: r = .23; g = .64; b = .76; glow = part % 3 == 0 ? .20 : .04; break;
                    case 2: r = .78; g = .89; b = .81; break;
                    case 3: r = .10; g = .65; b = .63; break;
                    case 4: r = part % 2 == 0 ? .83 : .24; g = part % 2 == 0 ? .91 : .65; b = part % 2 == 0 ? .88 : .79; break;
                    case 5: r = .69; g = .31; b = .88; glow = .13; break;
                    case 6: r = part == 3 ? .80 : .24; g = part == 3 ? .31 : .61; b = part == 3 ? .49 : .12; break;
                    default: r = part % 3 == 0 ? .94 : .21; g = part % 3 == 0 ? .27 : .16; b = .055; glow = part % 3 == 0 ? .25 : 0; break;
                }
            }

            public void Building(int index)
            {
                if (!layout.TryBuildingAnchor(index, 2.7, out VignettePoint p)) return;
                Set(index, p);
                int theme=Appearance(index);
                if (theme == 3) TideBridge();
                else if (theme == 2) OpenObservatory();
                else Ruin(theme);
                if(collider!=null)
                {
                    // The3.8m square fits the independently reserved2.7m radius.
                    // These landmarks are scenery, never a new accessible bridge/platform.
                    collider(new VignetteTrunkProxy(index,Position(0,3.3,0),new VignettePoint(3.8,6.6,3.8),VignetteProxyKind.NonwalkableScenery,yaw));
                    Result.SceneryProxies++;
                }
                Result.Buildings[index]++;
            }
            private void Ruin(int theme)
            {
                VignetteSurface wall = theme == 4 ? VignetteSurface.PaleStone : theme == 6 ? VignetteSurface.Moss
                    : theme == 7 || theme == 1 ? VignetteSurface.DarkStone : VignetteSurface.Stone;
                Box(VignetteSurface.DarkStone, 0, .13, 0, 3.6, .26, 3.6);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(wall, side * 1.42, 1.70, .20, .52, 2.88, 2.95);
                    for (int level = 0; level < 5; level++)
                        Box(level % 2 == 0 ? wall : VignetteSurface.PaleStone, side * 1.12, .55 + level * .59, -1.30, .58, .56, .64);
                    Box(VignetteSurface.PaleStone, side * 1.42, 3.22, .20, .72, .18, 3.22);
                    Box(wall, side * 1.35, 4.10, 1.25, .62, 1.60, .66);
                    Box(VignetteSurface.Brass, side * 1.35, 4.95, 1.25, .70, .10, .74);
                    for (int band = 0; band < 3; band++)
                        Box(VignetteSurface.DarkStone, side * 1.695, .93 + band * .78, .22, .024, .045, 2.78);
                }
                // Broken back wall, open window, stepped arch and recessed header.
                Box(wall, 0, .83, 1.42, 2.35, 1.14, .50);
                Box(wall, -.92, 2.13, 1.42, .48, 1.46, .50);
                Box(wall, .92, 2.13, 1.42, .48, 1.46, .50);
                Box(VignetteSurface.PaleStone, 0, 2.95, 1.42, 2.35, .18, .66);
                for (int step = -2; step <= 2; step++)
                    Box(VignetteSurface.PaleStone, step * .42, 3.44 + (2 - Math.Abs(step)) * .18, -1.30, .46, .32, .74);
                for (int step = 0; step < 3; step++)
                    Box(wall, 0, .09 + step * .10, -1.50 + step * .20, 2.15, .18, .30);
                VignetteSurface accent = theme == 0 || theme == 7 ? VignetteSurface.Flame : theme == 1 ? VignetteSurface.Azure
                    : theme == 4 ? VignetteSurface.Snow : theme == 5 ? VignetteSurface.PortalCrystal : VignetteSurface.RuneGreen;
                for (int inset = 0; inset < 4; inset++)
                    Box(accent, (inset - 1.5) * .30, 3.12, -1.69, .12, .12, .035);
                if (theme == 0)
                {
                    Box(VignetteSurface.Ember, .60, 4.42, .72, .90, 2.70, .90);
                    Box(VignetteSurface.Iron, .60, 5.84, .72, 1.13, .14, 1.13);
                    Box(VignetteSurface.FlameCore, .60, .48, -.24, 1.16, .05, 1.32);
                }
                else if (theme == 1 || theme == 5)
                {
                    for (int side = -1; side <= 1; side += 2)
                        Emit(new LaneVignettePiece(lane, VignettePrimitive.Crystal, accent,
                            Position(side * .84, 4.19, .56), new VignettePoint(.24, 2.14, .24), yaw));
                }
                else if (theme == 4)
                {
                    for (int step = 0; step < 5; step++)
                    { Box(VignetteSurface.Snow, (step - 2) * .56, 3.85 + (2 - Math.Abs(step)) * .18, -1.30, .59, .12, .80); }
                    Emit(new LaneVignettePiece(lane, VignettePrimitive.Crystal, VignetteSurface.Azure,
                        Position(.58, 1.55, .25), new VignettePoint(.39, 2.6, .39), yaw));
                }
                else if (theme == 6)
                {
                    for (int vine = 0; vine < 7; vine++)
                        Box(vine % 3 == 0 ? VignetteSurface.Blossom : VignetteSurface.Green,
                            -1.72, .65 + vine * .49, .78 - vine % 3 * .52, .16, .62, .43);
                }
                else if (theme == 7)
                {
                    // Actual visible shallow lava faces over the continuous existing floor.
                    // Cosmetic only: no pit, damage trigger, navigation or floor-collider change.
                    for (int strip = 0; strip < 6; strip++)
                        Top(VignettePrimitive.LavaTop, VignetteSurface.Flame, 0, .278, -.96 + strip * .36, 2.28 - strip % 2 * .30, .40);
                    for (int vent = 0; vent < 4; vent++)
                        Emit(new LaneVignettePiece(lane, VignettePrimitive.SteamQuad, VignetteSurface.Cloud,
                            Position((vent - 1.5) * .48, .70 + vent % 2 * .31, .1), new VignettePoint(.40, 1.55, 0), yaw));
                }
            }
            private void OpenObservatory()
            {
                Box(VignetteSurface.PaleStone, 0, .13, 0, 3.6, .26, 3.6);
                for (int side = -1; side <= 1; side += 2)
                for (int depth = -1; depth <= 1; depth++)
                {
                    Box(VignetteSurface.PaleStone, side * 1.37, 2.10, depth * 1.27, .44, 3.68, .44);
                    Box(VignetteSurface.Brass, side * 1.37, 3.99, depth * 1.27, .59, .10, .59);
                }
                for (int depth = -1; depth <= 1; depth++)
                    Box(VignetteSurface.PaleStone, 0, 4.19, depth * 1.27, 3.23, .30, .62);
                for (int vane = 0; vane < 4; vane++)
                    Box(vane % 2 == 0 ? VignetteSurface.PaleStone : VignetteSurface.Brass,
                        (vane - 1.5) * .38, 4.55 + vane * .18, 0, .68, .12, .62);
            }
            private void TideBridge()
            {
                Box(VignetteSurface.DarkStone, 0, .13, 0, 3.6, .26, 3.6);
                Box(VignetteSurface.Water, 0, .29, 0, 3.20, .06, 3.20);
                for (int side = -1; side <= 1; side += 2)
                for (int depth = -1; depth <= 1; depth++)
                {
                    Box(VignetteSurface.Stone, side * 1.25, 1.08, depth * 1.20, .45, 1.64, .50);
                    Box(VignetteSurface.PaleStone, side * 1.25, 1.96, depth * 1.20, .63, .12, .66);
                }
                for (int plank = 0; plank < 7; plank++)
                    Box(VignetteSurface.WoodEnd, 0, 1.42, (plank - 3) * .40, 2.45, .14, .37);
                for (int side = -1; side <= 1; side += 2)
                { Box(VignetteSurface.Brass, side * 1.25, 2.12, 0, .10, .10, 2.95); }
                for (int crown = 0; crown < 3; crown++)
                    Box(VignetteSurface.Azure, (crown - 1) * .55, .40, 1.38, .34, .16, .16);
            }

            public void Grass(int index)
            {
                for (int tuft = 0; tuft < 12; tuft++)
                {
                    double a = (index * 45 + (tuft % 2 == 0 ? -19 : 19)) * Math.PI / 180;
                    double radius = layout.PortalRadius * (.37 + (tuft / 2) * .095);
                    double x = Math.Cos(a) * radius, z = Math.Sin(a) * radius;
                    if (!layout.TryReserve(x, z, .30)) continue;
                    Set(index, new VignettePoint(x, 0, z));
                    Palette(Appearance(index), tuft, out double r, out double g, out double b, out double glow);
                    for (int blade = 0; blade < 2; blade++)
                    {
                        double h = .32 + blade * .15;
                        Emit(new LaneVignettePiece(lane, VignettePrimitive.CanopyBox, VignetteSurface.Green,
                            Position((blade - .5) * .18, h * .5, 0), new VignettePoint(.07, h, .26), yaw,
                            origin, 1, r, g, b, glow * .25));
                    }
                    Result.GrassTufts[index]++;
                }
            }

            public void OuterVista(int index)
            {
                // Render-only dead zone beyond the supplied arena collision boundary.
                // No floor collider, step, route, enemy admission or new walkable surface.
                int theme=Appearance(index);
                VignetteSurface rock = theme == 0 ? VignetteSurface.Ember : theme == 2 || theme == 4 ? VignetteSurface.PaleStone
                    : theme == 3 ? VignetteSurface.Sand : theme == 6 ? VignetteSurface.Moss : VignetteSurface.DarkStone;
                VignetteSurface cap = theme == 4 ? VignetteSurface.Snow : theme == 6 ? VignetteSurface.Green
                    : theme == 0 ? VignetteSurface.Autumn : theme == 2 ? VignetteSurface.PaleStone : rock;
                for (int ridge = 0; ridge < 3; ridge++)
                {
                    double a = (index * 45 + (ridge - 1) * 12) * Math.PI / 180;
                    double radius = layout.ArenaRadius + 9 + ridge * 6;
                    Set(index, new VignettePoint(Math.Cos(a) * radius, 0, Math.Sin(a) * radius));
                    double height = 8 + ridge * 4 + (index % 3) * 1.3;
                    Box(rock, 0, height * .5 - 3.5, 0, 10.5, height, 9.5);
                    Box(rock, -.6, height - 2.4, -.4, 8.6, 2.2, 8.4);
                    Box(cap, -.6, height - 1.13, -.4, 8.9, .34, 8.7);
                    Box(rock, 1.20, height + 1.1, 1.1, 4.9, 4.1, 4.6);
                    Box(cap, 1.20, height + 3.31, 1.1, 5.1, .32, 4.8);
                    if (theme == 7 || theme == 0)
                    {
                        for (int stream = 0; stream < 5; stream++)
                            Top(VignettePrimitive.LavaTop, VignetteSurface.Flame, -.6 + stream % 2 * .4,
                                height - .96 + .008, -3.10 + stream * 1.20, 2.35, 1.24);
                        if (theme == 7)
                            for (int vent = 0; vent < 2; vent++)
                                Emit(new LaneVignettePiece(lane, VignettePrimitive.SteamQuad, VignetteSurface.Cloud,
                                    Position(-.5 + vent * 1.1, height + .40, -.5), new VignettePoint(1.30, 4.2, 0), yaw));
                    }
                    else if (theme == 3)
                    {
                        Box(VignetteSurface.Water, 0, height - .925, -.4, 3.1, .035, 7.8);
                        Box(VignetteSurface.WoodEnd, -3.70, height - 1.10, 0, 1.2, .25, 5.3);
                    }
                    else if (theme == 4 || theme == 5 || theme == 1)
                        Emit(new LaneVignettePiece(lane, VignettePrimitive.Crystal,
                            theme == 4 ? VignetteSurface.Azure : theme == 5 ? VignetteSurface.PortalCrystal : VignetteSurface.Brass,
                            Position(-2.3, height + .65, .6), new VignettePoint(1.2, 7.0, 1.2), yaw));
                    else if (theme == 6)
                    {
                        Box(VignetteSurface.Wood, -2.1, height + .2, .4, .65, 3.0, .65);
                        Box(VignetteSurface.Blossom, -2.1, height + 1.7, .4, 3.8, .85, 3.2);
                    }
                }
            }
        }
    }
}
