using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // The driver's view of a right-hand-drive car, Hard Drivin' style: the windscreen's frame and pillars, the roof's
    // lining above it, the dashboard with its instrument binnacle and two dials, the centre console with its vents,
    // radio and gear lever, the glove box, the door tops either side, the rear-view mirror up to your left (and the
    // seats behind you in it) and the wing mirror out to your right. Not a part of the car's body (it's
    // bigger than CarMesh's cabin, to feel roomy): it's laid out round the driver's eye, and drawn over the world from
    // there. In metres, the eye at the origin, looking along +Z, +Y up, +X to your left - towards the car's middle.
    //
    // What moves has a mesh of its own, put where it goes by the car's state: the steering wheel (BuildWheel, put by
    // WheelAt) and the dials' needles (BuildNeedle, put by NeedleAt).
    public static class CockpitMesh
    {
        public const int Dash = 0, DashTop = 1, Frame = 2, Lining = 3, Door = 4, Dial = 5, Mirror = 6, Seat = 7, Knob = 8;
        public const int PaletteSize = 9;

        public const int Rim = 0, Spoke = 1;
        public const int WheelPaletteSize = 2;

        // The car's middle, to your left: where you sit (see Car.SeatOffset)
        private const float Middle = 0.45f;

        // The steering wheel: where its middle is, how far its top leans back towards you, and how far it turns for
        // the front wheels' turn
        private static readonly Vector3 WheelCentre = new Vector3(0f, -0.32f, 0.5f);
        private const float WheelTilt = 0.26f;   // radians, about 15 degrees
        private const float SteeringRatio = 4f;
        private const float WheelRadius = 0.19f;

        // The dials: the rev counter on the left, the speedometer on the right, as their needles are numbered (see
        // NeedleAt). Each needle sweeps DialSweep, from bottom left, clockwise.
        public const int RevCounter = 0, Speedometer = 1;
        private static readonly float[] DialX = { 0.075f, -0.075f };
        private const float DialY = -0.19f, DialZ = 0.699f, DialRadius = 0.065f;
        private const float DialSweep = MathHelper.Pi * 1.5f;
        public const float SpeedometerTop = 100f;   // mph, the speedometer's full scale
        public const float RevCounterTop = 8000f;   // rpm
        private static readonly int[] DialTicks = { 9, 11 };   // every thousand rpm, every ten mph

        public static Color[] Palette(Color dash, Color frame, Color lining, Color glass, Color seat)
        {
            var palette = new Color[PaletteSize];
            palette[Dash] = dash;
            palette[DashTop] = Color.Lerp(dash, Color.Black, 0.3f);
            palette[Frame] = frame;
            palette[Lining] = lining;
            palette[Door] = Color.Lerp(dash, frame, 0.25f);
            palette[Dial] = new Color(15, 15, 20);
            palette[Mirror] = glass;
            palette[Seat] = seat;
            palette[Knob] = new Color(20, 20, 22);
            return palette;
        }

        public static Color[] WheelPalette(Color rim, Color spoke) => new[] { rim, spoke };

        public static Color[] NeedlePalette(Color needle) => new[] { needle };

        // Where dial `dial`'s needle goes, `reading` of the way round its scale (0 to 1): from bottom left, up, over and
        // round to bottom right
        public static Matrix NeedleAt(int dial, float reading)
        {
            var angle = DialSweep * (MathHelper.Clamp(reading, 0f, 1f) - 0.5f);   // from straight up, clockwise as you see it
            return Matrix.CreateRotationZ(angle) * Matrix.CreateTranslation(DialX[dial], DialY, DialZ - 0.002f);
        }

        // The speedometer's reading, going at `speed` metres per second (either way)
        public static float SpeedReading(float speed) => MathF.Abs(speed) * 2.23694f / SpeedometerTop;

        // The rev counter's, going at `speed`: there's no gearbox to ask, so it's a made-up one, of five gears, each
        // taking the engine from low revs to near the red line before the next; a flooded engine shows none.
        private static readonly float[] GearTop = { 9f, 16f, 24f, 31f, 40f };   // metres per second, each gear's top speed
        public static float RevReading(float speed, bool running)
        {
            if (!running)
                return 0f;
            speed = MathF.Abs(speed);
            var gear = 0;
            while (gear < GearTop.Length - 1 && speed > GearTop[gear])
                gear++;
            return (800f + MathF.Min(speed / GearTop[gear], 1f) * 6200f) / RevCounterTop;
        }

        // Where the steering wheel goes, turned for the front wheels at `steerAngle` (positive, right): its top
        // goes the way you're turning
        public static Matrix WheelAt(float steerAngle) =>
            Matrix.CreateRotationZ(steerAngle * SteeringRatio) * Matrix.CreateRotationX(-WheelTilt) * Matrix.CreateTranslation(WheelCentre);

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();

            // The other side's the same, the far side of the car's middle
            static Vector3 Across(Vector3 p) => new Vector3(2f * Middle - p.X, p.Y, p.Z);

            // ---- The windscreen's pillars, from the dash up to the roof, leaning in towards the top
            var baseOuter = new Vector3(-0.42f, -0.25f, 0.95f);   // at the foot of the windscreen, on your side
            var topOuter = new Vector3(-0.36f, 0.25f, 0.55f);     // at the top
            var pillar = new Vector3(0.08f, 0f, 0f);             // its width
            foreach (var side in new[] { false, true })
            {
                Vector3 S(Vector3 p) => side ? Across(p) : p;
                var inward = side ? -pillar : pillar;
                var quad = new[] { S(baseOuter), S(baseOuter) + inward, S(topOuter) + inward, S(topOuter) };
                mesh.AddQuad(Frame, quad[0], quad[1], quad[2], quad[3]);
                mesh.AddLineLoop(quad);
            }

            // ---- The windscreen's top rail, between the pillars, down the glass from the roof's front edge
            float GlassZ(float y) => baseOuter.Z + (y - baseOuter.Y) / (topOuter.Y - baseOuter.Y) * (topOuter.Z - baseOuter.Z);
            const float railY = 0.20f;
            var t = (railY - baseOuter.Y) / (topOuter.Y - baseOuter.Y);
            var railInner = MathHelper.Lerp(baseOuter.X, topOuter.X, t) + pillar.X;
            var headerLow = new Vector3(railInner, railY, GlassZ(railY));
            var headerHigh = topOuter + pillar;
            mesh.AddQuad(Frame, headerLow, Across(headerLow), Across(headerHigh), headerHigh);
            mesh.AddLine(headerLow, Across(headerLow));

            // ---- The roof's lining, from the windscreen back over your head
            var liningBack = new Vector3(topOuter.X, 0.30f, -0.4f);
            mesh.AddQuad(Lining, topOuter, Across(topOuter), Across(liningBack), liningBack);
            mesh.AddLine(topOuter, Across(topOuter));

            // ---- The dashboard: its top, from the foot of the windscreen back towards you, and its face below that
            const float dashZ = 0.7f, dashY = -0.28f, floorY = -0.9f;
            const float doorX = -0.33f, sillX = -0.43f, doorBack = -0.3f;   // the door's inside, and its top's outer edge
            var dashBackOuter = new Vector3(sillX, dashY, dashZ);
            mesh.AddQuad(DashTop, dashBackOuter, Across(dashBackOuter), Across(baseOuter), baseOuter);
            mesh.AddLine(dashBackOuter, Across(dashBackOuter));
            mesh.AddLine(baseOuter, Across(baseOuter));
            mesh.AddLine(dashBackOuter, baseOuter);
            mesh.AddLine(Across(dashBackOuter), Across(baseOuter));
            var dashFace = new Vector3(doorX, dashY, dashZ);
            mesh.AddQuad(Dash, dashFace, Across(dashFace), Across(dashFace) with { Y = floorY }, dashFace with { Y = floorY });

            // ---- The doors: the top of each, level with the dash, and its inside below that
            foreach (var side in new[] { false, true })
            {
                Vector3 S(Vector3 p) => side ? Across(p) : p;
                var railFrontIn = S(new Vector3(doorX, dashY, dashZ));
                var railFrontOut = S(new Vector3(sillX, dashY, dashZ));
                var railBackIn = S(new Vector3(doorX, dashY, doorBack));
                var railBackOut = S(new Vector3(sillX, dashY, doorBack));
                mesh.AddQuad(Frame, railFrontIn, railFrontOut, railBackOut, railBackIn);
                mesh.AddLine(railFrontIn, railBackIn);
                mesh.AddLine(railFrontOut, railBackOut);
                mesh.AddQuad(Door, railFrontIn, railBackIn, railBackIn with { Y = floorY }, railFrontIn with { Y = floorY });
            }

            // ---- The instrument binnacle, on the dash in front of you: a hood over the two dials
            const float binnacleHalf = 0.16f, binnacleTop = -0.10f, binnacleFront = 0.8f, binnacleFrontTop = -0.12f;
            var dashRise = (baseOuter.Y - dashY) / (baseOuter.Z - dashZ);
            var binnacleFoot = dashY + (binnacleFront - dashZ) * dashRise;   // where its front meets the dash top
            Vector3 B(float x, float y, float z) => new Vector3(x, y, z);
            var backLow = new[] { B(-binnacleHalf, dashY, dashZ), B(binnacleHalf, dashY, dashZ) };
            var backHigh = new[] { B(-binnacleHalf, binnacleTop, dashZ), B(binnacleHalf, binnacleTop, dashZ) };
            var frontHigh = new[] { B(-binnacleHalf, binnacleFrontTop, binnacleFront), B(binnacleHalf, binnacleFrontTop, binnacleFront) };
            var frontLow = new[] { B(-binnacleHalf, binnacleFoot, binnacleFront), B(binnacleHalf, binnacleFoot, binnacleFront) };
            mesh.AddQuad(Dash, backLow[0], backLow[1], backHigh[1], backHigh[0]);
            mesh.AddQuad(DashTop, backHigh[0], backHigh[1], frontHigh[1], frontHigh[0]);
            mesh.AddQuad(Dash, frontHigh[0], frontHigh[1], frontLow[1], frontLow[0]);
            for (var k = 0; k < 2; k++)
            {
                mesh.AddPolygon(Dash, backLow[k], backHigh[k], frontHigh[k], frontLow[k]);
                mesh.AddLine(backLow[k], backHigh[k]);
                mesh.AddLine(backHigh[k], frontHigh[k]);
                mesh.AddLine(frontHigh[k], frontLow[k]);
            }
            mesh.AddLine(backHigh[0], backHigh[1]);
            mesh.AddLine(frontHigh[0], frontHigh[1]);

            // The dials, the rev counter and the speedometer, just proud of its face, each with the marks on its scale
            // (every other one longer); their needles are drawn over them (see NeedleAt)
            for (var d = 0; d < DialX.Length; d++)
            {
                var x = DialX[d];
                var dial = new Vector3[12];
                for (var k = 0; k < dial.Length; k++)
                {
                    var angle = (k + 0.5f) * MathHelper.TwoPi / dial.Length;
                    dial[k] = new Vector3(x + DialRadius * MathF.Cos(angle), DialY + DialRadius * MathF.Sin(angle), DialZ);
                }
                mesh.AddPolygon(Dial, dial);
                mesh.AddLineLoop(dial);
                var centre = new Vector3(x, DialY, DialZ);
                for (var k = 0; k < DialTicks[d]; k++)
                {
                    var angle = DialSweep * ((float)k / (DialTicks[d] - 1) - 0.5f);
                    var towards = new Vector3(-MathF.Sin(angle), MathF.Cos(angle), 0f);   // +X is to your left
                    var inner = k % 2 == 0 ? 0.65f : 0.78f;
                    mesh.AddLine(centre + towards * DialRadius * inner, centre + towards * DialRadius * 0.9f);
                }
            }

            // ---- The centre console: down the middle of the dash, its face sloping back to the floor between the seats,
            // with two air vents and the radio in it; and the gear lever, just behind it, to your left
            const float stackHalf = 0.15f, stackTop = -0.30f, stackBack = 0.6f, stackFoot = -0.58f, stackFootZ = 0.5f;
            var stack = new[]   // round its side, on your side of it
            {
                new Vector3(Middle - stackHalf, stackTop, dashZ), new Vector3(Middle - stackHalf, stackTop, stackBack),
                new Vector3(Middle - stackHalf, stackFoot, stackFootZ), new Vector3(Middle - stackHalf, stackFoot, dashZ),
            };
            var far = Array.ConvertAll(stack, p => Across(p));
            mesh.AddPolygon(Dash, stack);
            mesh.AddPolygon(Dash, far);
            mesh.AddQuad(DashTop, stack[0], far[0], far[1], stack[1]);   // its top
            mesh.AddQuad(Dash, stack[1], far[1], far[2], stack[2]);      // its face
            mesh.AddLine(stack[0], stack[1]);
            mesh.AddLine(far[0], far[1]);
            mesh.AddLine(stack[1], far[1]);
            mesh.AddLine(stack[1], stack[2]);
            mesh.AddLine(far[1], far[2]);

            // A panel on its face, from `x0` to `x1` across and `t0` to `t1` of the way down it, just proud of it
            var faceOut = Vector3.Normalize(new Vector3(0f, stackBack - stackFootZ, stackFoot - stackTop)) * 0.002f;
            Vector3 OnFace(float x, float t) =>
                new Vector3(x, MathHelper.Lerp(stackTop, stackFoot, t), MathHelper.Lerp(stackBack, stackFootZ, t)) + faceOut;
            void Panel(float x0, float x1, float t0, float t1)
            {
                var panel = new[] { OnFace(x0, t0), OnFace(x1, t0), OnFace(x1, t1), OnFace(x0, t1) };
                mesh.AddQuad(Dial, panel[0], panel[1], panel[2], panel[3]);
                mesh.AddLineLoop(panel);
            }
            foreach (var (x0, x1) in new[] { (Middle - 0.12f, Middle - 0.02f), (Middle + 0.02f, Middle + 0.12f) })
            {
                Panel(x0, x1, 0.05f, 0.16f);
                foreach (var slat in new[] { 0.09f, 0.12f })   // the vent's slats
                    mesh.AddLine(OnFace(x0, slat), OnFace(x1, slat));
            }
            Panel(Middle - 0.11f, Middle + 0.11f, 0.22f, 0.38f);
            mesh.AddLineLoop(OnFace(Middle - 0.08f, 0.25f), OnFace(Middle + 0.02f, 0.25f), OnFace(Middle + 0.02f, 0.31f), OnFace(Middle - 0.08f, 0.31f));   // its display

            // Behind the console, the floor's hump between the seats, and the gear lever standing up out of it
            var hump = new[]
            {
                new Vector3(Middle - stackHalf, stackFoot, stackFootZ), new Vector3(Middle + stackHalf, stackFoot, stackFootZ),
                new Vector3(Middle + stackHalf, stackFoot, -0.1f), new Vector3(Middle - stackHalf, stackFoot, -0.1f),
            };
            mesh.AddQuad(DashTop, hump[0], hump[1], hump[2], hump[3]);
            mesh.AddLine(hump[1], hump[2]);
            mesh.AddLine(hump[0], hump[3]);
            var leverTop = new Vector3(Middle, -0.38f, 0.5f);
            mesh.AddTube(new Vector3(Middle, stackFoot, 0.44f), leverTop, 0.012f, 0.012f, 6, Dash);
            mesh.AddFrustum(leverTop, 0.028f, 0.028f, 0.045f, 8, Knob, Knob, Knob);

            // ---- The glove box, in the dash in front of the passenger
            const float lidZ = dashZ - 0.001f;
            var glovebox = new[]
            {
                new Vector3(Middle + 0.33f, -0.34f, lidZ), new Vector3(Middle + 0.63f, -0.34f, lidZ),
                new Vector3(Middle + 0.63f, -0.46f, lidZ), new Vector3(Middle + 0.33f, -0.46f, lidZ),
            };
            mesh.AddQuad(DashTop, glovebox[0], glovebox[1], glovebox[2], glovebox[3]);
            mesh.AddLineLoop(glovebox);
            mesh.AddLine(new Vector3(Middle + 0.43f, -0.365f, lidZ), new Vector3(Middle + 0.53f, -0.365f, lidZ));   // its handle

            // ---- The steering column, from the dash to the back of the wheel's hub
            var axis = Vector3.TransformNormal(Vector3.Backward, Matrix.CreateRotationX(-WheelTilt));
            var columnEnd = WheelCentre + axis * ((dashZ - WheelCentre.Z) / axis.Z);
            mesh.AddTube(WheelCentre + axis * 0.01f, columnEnd, 0.035f, 0.035f, 8, Dash);

            // ---- The rear-view mirror, hanging from the windscreen's top rail, over the car's middle
            var mirror = new[]
            {
                new Vector3(Middle - 0.12f, 0.12f, 0.56f), new Vector3(Middle + 0.12f, 0.12f, 0.56f),
                new Vector3(Middle + 0.12f, 0.19f, 0.56f), new Vector3(Middle - 0.12f, 0.19f, 0.56f),
            };
            mesh.AddQuad(Mirror, mirror[0], mirror[1], mirror[2], mirror[3]);
            mesh.AddLineLoop(mirror);
            mesh.AddLine(new Vector3(Middle, 0.19f, 0.56f), new Vector3(Middle, railY, GlassZ(railY)));

            // What's in it, behind you: the rear window's frame, the back seats' headrests below it, and, nearer and
            // bigger, the passenger's (on the left, as it is behind you). Each just in front of what it's in front of.
            static Vector3 InMirror(float x, float y, float nearer) => new Vector3(Middle + x, y, 0.56f - 0.001f - nearer);
            mesh.AddLineLoop(InMirror(-0.085f, 0.178f, 0f), InMirror(0.085f, 0.178f, 0f), InMirror(0.10f, 0.145f, 0f), InMirror(-0.10f, 0.145f, 0f));
            foreach (var x in new[] { -0.06f, 0.0f })
            {
                var rest = new[] { InMirror(x - 0.02f, 0.126f, 0f), InMirror(x + 0.02f, 0.126f, 0f), InMirror(x + 0.02f, 0.142f, 0f), InMirror(x - 0.02f, 0.142f, 0f) };
                mesh.AddQuad(Seat, rest[0], rest[1], rest[2], rest[3]);
                mesh.AddLineLoop(rest);
            }
            var passenger = new[]
            {
                InMirror(0.045f, 0.121f, 0.001f), InMirror(0.115f, 0.121f, 0.001f), InMirror(0.115f, 0.152f, 0.001f),
                InMirror(0.10f, 0.162f, 0.001f), InMirror(0.06f, 0.162f, 0.001f), InMirror(0.045f, 0.152f, 0.001f),
            };
            mesh.AddPolygon(Seat, passenger);
            mesh.AddLineLoop(passenger);

            // ---- The wing mirror, out past the door on your side, on an arm from the foot of the pillar
            var housing = new[]
            {
                new Vector3(-0.48f, -0.22f, 0.82f), new Vector3(-0.64f, -0.22f, 0.82f),
                new Vector3(-0.64f, -0.12f, 0.82f), new Vector3(-0.48f, -0.12f, 0.82f),
            };
            var inset = new Vector3(0.015f, 0.015f, 0f);
            var glass = new[]
            {
                housing[0] + new Vector3(-inset.X, inset.Y, -0.001f), housing[1] + new Vector3(inset.X, inset.Y, -0.001f),
                housing[2] + new Vector3(inset.X, -inset.Y, -0.001f), housing[3] + new Vector3(-inset.X, -inset.Y, -0.001f),
            };
            mesh.AddQuad(Frame, housing[0], housing[1], housing[2], housing[3]);
            mesh.AddLineLoop(housing);
            mesh.AddQuad(Mirror, glass[0], glass[1], glass[2], glass[3]);
            mesh.AddLineLoop(glass);
            var armFoot = Vector3.Lerp(dashBackOuter, baseOuter, 0.6f);   // on the window's bottom edge
            mesh.AddLine(housing[0], armFoot);

            return mesh.Build(device);
        }

        // A dial's needle, pointing up (+Y) from the dial's middle: a long thin triangle, with a stub back past the
        // middle. Put it on its dial with NeedleAt.
        public static MeshData BuildNeedle(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            var needle = new[] { new Vector3(0.006f, -0.012f, 0f), new Vector3(0f, DialRadius * 0.85f, 0f), new Vector3(-0.006f, -0.012f, 0f) };
            mesh.AddPolygon(0, needle);
            mesh.AddLineLoop(needle);
            return mesh.Build(device);
        }

        // The steering wheel, flat in X-Y about its middle, facing you (-Z): its rim, and three spokes (left, right
        // and down) from a hub. Put it in the car with WheelAt.
        public static MeshData BuildWheel(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const int sides = 12;
            const float inner = WheelRadius - 0.025f, hub = 0.05f, spokeHalf = 0.018f;

            // A ring whose flats face along X and Y (so a spoke along either ends square on one)
            static Vector3[] Ring(float radius, int count)
            {
                var ring = new Vector3[count];
                for (var k = 0; k < count; k++)
                {
                    var angle = (k + 0.5f) * MathHelper.TwoPi / count;
                    ring[k] = new Vector3(radius * MathF.Cos(angle), radius * MathF.Sin(angle), 0f);
                }
                return ring;
            }

            var outside = Ring(WheelRadius, sides);
            var inside = Ring(inner, sides);
            for (var k = 0; k < sides; k++)
                mesh.AddQuad(Rim, outside[k], outside[(k + 1) % sides], inside[(k + 1) % sides], inside[k]);
            mesh.AddLineLoop(outside);
            mesh.AddLineLoop(inside);

            var hubRing = Ring(hub, 8);
            mesh.AddPolygon(Spoke, hubRing);
            mesh.AddLineLoop(hubRing);

            // Each spoke runs from the hub's flat to the rim's, along `along`, `across` its width
            var hubFlat = hub * MathF.Cos(MathHelper.Pi / 8f);
            var rimFlat = inner * MathF.Cos(MathHelper.Pi / sides);
            foreach (var along in new[] { Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitY })
            {
                var across = new Vector3(-along.Y, along.X, 0f) * spokeHalf;
                var spoke = new[] { along * hubFlat - across, along * rimFlat - across, along * rimFlat + across, along * hubFlat + across };
                mesh.AddQuad(Spoke, spoke[0], spoke[1], spoke[2], spoke[3]);
                mesh.AddLine(spoke[0], spoke[1]);
                mesh.AddLine(spoke[3], spoke[2]);
            }

            return mesh.Build(device);
        }
    }
}
