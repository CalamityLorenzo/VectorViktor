using Hexa.NET.ImGui;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Core.Colour;
using World.Maps;
using Num = System.Numerics;

namespace Droid.Playground
{
    // Bringing colour back to a world it's been drained from (GameDesign.md 3.3, question 1), to try out the ways it could
    // go before choosing one. Every colour in the world is drawn as much as its hue is back (see Drained, and the palette
    // shader's grade: IPaletteGrade); drops of each colour lie about; the droid picks them up by driving into them and pours
    // them into the barrel of their colour by driving up to it.
    //
    // Where it comes back: everywhere, a little with every drop; or only in this place (round the barrels), all at once
    // when its barrel's full; or both, a tint as the barrel fills and then all of it. How: at once, fading in, or in a wave
    // spreading out from the barrels. And how drained looks, what happens to the whites and greys, and whether there are
    // six colours to find or only the three primaries the rest are mixed from.
    public sealed class ColourLab : Experiment
    {
        public enum Spread { Everywhere, ThisPlace, Both }
        public enum Change { AtOnce, Fade, Wave }

        public override string Name => "colour";
        public override string About =>
            "The world with its colour drained out. Drive into the drops to pick them up, and up to the barrel of their colour " +
            "to pour them in. Try where colour comes back (everywhere bit by bit, or this place all at once), how (at once, " +
            "fading, or a wave), and how drained looks. The sliders set how full each barrel is. K finishes a change at once.";

        private static readonly string[] SpreadNames =
            { "everywhere, a little with every drop", "this place, all at once when full", "this place, a tint, then all when full" };
        private static readonly string[] ChangeNames = { "at once", "fading in", "a wave from the barrels" };
        private static readonly string[] DrainedNames = { "the background (wireframe)", "grey (an old photograph)" };
        private static readonly string[] NeutralNames = { "never missing", "bit by bit with every colour", "with the first colour", "with the last colour" };

        // Where colour comes back, and how
        public Spread Where = Spread.Everywhere;
        public Change How = Change.Wave;
        public int Need = 5;                // drops to fill a barrel
        public float Tint = 0.3f;           // in both: how much of a colour's back in the place with its barrel all but full
        public float PlaceRadius = 30f, PlaceEdge = 8f;
        public float FadeTime = 2f, WaveSpeed = 15f, Rim = 0.6f;

        // How drained looks
        public DrainedTo To = DrainedTo.Background;
        public float Left;                  // how much colour's left in everything, drained
        public Neutrals Neutrals = Neutrals.BitByBit;
        public bool Primaries;              // only red, yellow and blue to find: the rest are mixed of them
        public bool DroidKeepsColour = true;

        public int Carry = 3;               // drops the droid can carry at once

        // Everywhere, a wave runs out this far (past where the fog's hidden everything) before it's everywhere
        private const float WaveReach = WorldRenderer.DrawDistance;
        private const float Reach = 0.3f;   // how near the droid's edge must come to a drop or a barrel
        private const float BarrelsAside = 3f, BarrelGap = 0.9f;   // the barrels in a row on its right, from it, and apart

        private readonly LabGrade _grade;
        private Vector3 _centre, _along;
        private (Hue hue, Vector3 at)[] _barrels = Array.Empty<(Hue, Vector3)>();
        private readonly List<(Hue hue, Vector3 at, float phase)> _drops = new List<(Hue, Vector3, float)>();
        private readonly List<Hue> _carried = new List<Hue>();
        private readonly float[] _filled = new float[Hues.Count];   // drops poured into each colour's barrel
        private readonly float[] _worldTarget = new float[Hues.Count], _placeTarget = new float[Hues.Count];
        private readonly float[] _worldShown = new float[Hues.Count], _placeShown = new float[Hues.Count], _front = new float[Hues.Count];
        private float? _wave;   // how far the front's spread, while there's one
        private int _scatter = 1;
        private bool _laidOutWithPrimaries;

        private readonly Dictionary<Hue, (MeshInstance drop, MeshInstance barrel, MeshInstance paint)> _views =
            new Dictionary<Hue, (MeshInstance, MeshInstance, MeshInstance)>();

        public ColourLab() => _grade = new LabGrade(this);

        private IEnumerable<Hue> InPlay => Primaries ? Hues.Primaries : Hues.All;

        public override void Start(Session session)
        {
            PaletteEffect.For(session.Device).Grade = _grade;
            Beside(session);
        }

        public override void Stop(Session session)
        {
            PaletteEffect.For(session.Device).Grade = null;
            session.View.KeepsColour = false;
        }

        // The barrels in a row on the droid's right, running the way it's facing, so they're not in its way
        private void Beside(Session session)
        {
            var body = session.Player.Body;
            _along = body.Heading;
            var right = Vector3.Normalize(Vector3.Cross(body.Heading, Vector3.Up));
            LayOut(session, body.Position + right * BarrelsAside + _along * BarrelsAside);
        }

        // The barrels in a row along `_along` through `centre`, the place round them, and the drops scattered over it afresh
        private void LayOut(Session session, Vector3 centre)
        {
            _laidOutWithPrimaries = Primaries;
            var hues = InPlay.ToArray();
            _centre = centre with { Y = GroundAt(session, centre.X, centre.Z) };
            _barrels = hues.Select((hue, k) =>
            {
                var at = _centre + _along * ((k - (hues.Length - 1) / 2f) * BarrelGap);
                return (hue, at with { Y = GroundAt(session, at.X, at.Z) });
            }).ToArray();

            _drops.Clear();
            _carried.Clear();
            var random = new Random(_scatter);
            foreach (var hue in hues)
                for (var k = 0; k <= Need; k++)
                {
                    var angle = (float)random.NextDouble() * MathHelper.TwoPi;
                    var far = MathHelper.Lerp(4f, PlaceRadius * 0.8f, MathF.Sqrt((float)random.NextDouble()));
                    var (x, z) = (_centre.X + far * MathF.Cos(angle), _centre.Z + far * MathF.Sin(angle));
                    _drops.Add((hue, new Vector3(x, GroundAt(session, x, z), z), (float)random.NextDouble() * MathHelper.TwoPi));
                }
        }

        // The ground at a point: whatever's to stand on there (a floor, a platform), if it's not far above the terrain
        private static float GroundAt(Session session, float x, float z)
        {
            var terrain = session.World.Terrain.HeightAt(x, z);
            return session.World.Ground.GroundBelow(new Vector3(x, terrain, z), 2.5f) ?? terrain;
        }

        private static float Across(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

        public override void AfterTick(Session session, float dt)
        {
            if (Primaries != _laidOutWithPrimaries)
                LayOut(session, _centre);

            // Drops picked up, and poured into their barrels
            var body = session.Player.Body;
            var feet = body.Position;
            var reach = body.Gait.Radius + Reach;
            for (var i = _drops.Count - 1; i >= 0 && _carried.Count < Carry; i--)
                if (Across(_drops[i].at, feet) < reach + PaintMesh.DropRadius && MathF.Abs(_drops[i].at.Y - feet.Y) < 1.5f)
                {
                    _carried.Add(_drops[i].hue);
                    _drops.RemoveAt(i);
                }
            foreach (var (hue, at) in _barrels)
                if (Across(at, feet) < reach + PaintMesh.BarrelRadius)
                    _filled[(int)hue] += _carried.RemoveAll(h => h == hue);

            Targets();
            Move(dt);
        }

        // How much of each colour should be back, in the world and in the place, from how full the barrels are
        private void Targets()
        {
            Span<float> world = stackalloc float[Hues.Count], place = stackalloc float[Hues.Count];
            for (var h = 0; h < Hues.Count; h++)
            {
                var full = _filled[h] >= Need - 1e-3f;
                var part = Math.Clamp(_filled[h] / Need, 0f, 1f);
                world[h] = Where == Spread.Everywhere ? part : 0f;
                place[h] = Where switch { Spread.Everywhere => part, Spread.ThisPlace => full ? 1f : 0f, _ => full ? 1f : part * Tint };
            }
            if (Primaries)
            {
                Hues.FromPrimaries(world[(int)Hue.Red], world[(int)Hue.Yellow], world[(int)Hue.Blue], _worldTarget);
                Hues.FromPrimaries(place[(int)Hue.Red], place[(int)Hue.Yellow], place[(int)Hue.Blue], _placeTarget);
            }
            else
            {
                world.CopyTo(_worldTarget);
                place.CopyTo(_placeTarget);
            }
        }

        // What's shown moved on towards the targets: all at once, fading, or as a wave
        private void Move(float dt)
        {
            var waving = _wave != null;
            var changed = false;
            switch (How)
            {
                case Change.AtOnce:
                    _wave = null;
                    changed |= Copy(_worldTarget, _worldShown) | Copy(_placeTarget, _placeShown);
                    break;
                case Change.Fade:
                    _wave = null;
                    changed |= Towards(_worldTarget, _worldShown, dt / FadeTime) | Towards(_placeTarget, _placeShown, dt / FadeTime);
                    break;
                default:
                    if (_wave == null && (!_worldTarget.SequenceEqual(_worldShown) || !_placeTarget.SequenceEqual(_placeShown)))
                        _wave = 0f;
                    if (_wave is { } spread)
                    {
                        changed |= Copy(Where == Spread.Everywhere ? _worldTarget : _placeTarget, _front);
                        _wave = spread + WaveSpeed * dt;
                        if (_wave >= (Where == Spread.Everywhere ? WaveReach : PlaceRadius))
                            Finish();
                    }
                    break;
            }
            if (changed || waving != (_wave != null))
                _grade.Changed();
        }

        // Whatever's changing, changed
        private void Finish()
        {
            Copy(_worldTarget, _worldShown);
            Copy(_placeTarget, _placeShown);
            _wave = null;
            _grade.Changed();
        }

        private static bool Copy(float[] from, float[] to)
        {
            if (from.SequenceEqual(to))
                return false;
            from.CopyTo(to, 0);
            return true;
        }

        private static bool Towards(float[] target, float[] shown, float step)
        {
            var changed = false;
            for (var h = 0; h < shown.Length; h++)
                if (shown[h] != target[h])
                {
                    shown[h] = shown[h] < target[h] ? MathF.Min(shown[h] + step, target[h]) : MathF.Max(shown[h] - step, target[h]);
                    changed = true;
                }
            return changed;
        }

        public override void Skip(Session session) => Finish();

        // ---- Drawn

        public override void Add(Session session, MeshBatch batch)
        {
            session.View.KeepsColour = DroidKeepsColour;
            var clock = session.Clock;
            foreach (var (hue, at) in _barrels)
            {
                var (_, barrel, paint) = ViewsOf(session, hue);
                barrel.Position = at;
                batch.Add(barrel);
                var full = Math.Clamp(_filled[(int)hue] / Need, 0f, 1f);
                if (full <= 0f)
                    continue;
                paint.Transform = Matrix.CreateScale(1f, full * PaintMesh.PaintDepth, 1f) * Matrix.CreateTranslation(at + Vector3.Up * PaintMesh.PaintBottom);
                batch.Add(paint);
            }
            foreach (var (hue, at, phase) in _drops)
            {
                var drop = ViewsOf(session, hue).drop;
                drop.Transform = Matrix.CreateRotationY(clock * 1.5f + phase) *
                                 Matrix.CreateTranslation(at + Vector3.Up * (0.45f + 0.08f * MathF.Sin(clock * 2f + phase)));
                batch.Add(drop);
            }
            // What it's carrying, going round above its head
            var body = session.Player.Body;
            for (var k = 0; k < _carried.Count; k++)
            {
                var angle = clock * 2f + k * MathHelper.TwoPi / _carried.Count;
                var drop = ViewsOf(session, _carried[k]).drop;
                drop.Transform = Matrix.CreateRotationY(-angle) * Matrix.CreateTranslation(body.Position +
                    new Vector3(0.3f * MathF.Cos(angle), body.Gait.Height + 0.15f, 0.3f * MathF.Sin(angle)));
                batch.Add(drop);
            }
        }

        // A colour's drop, barrel and the paint in it, made the first time they're wanted: in their own colours, whatever's
        // drained (they're what colour's made of)
        private (MeshInstance drop, MeshInstance barrel, MeshInstance paint) ViewsOf(Session session, Hue hue)
        {
            if (_views.TryGetValue(hue, out var views))
                return views;
            var colour = Hues.Example(hue);
            views = (session.Meshes.CreateInstance(session.Device, PaintMesh.Drop(colour)),
                     session.Meshes.CreateInstance(session.Device, PaintMesh.Barrel(colour)),
                     session.Meshes.CreateInstance(session.Device, PaintMesh.PaintIn(colour)));
            views.drop.KeepsColour = views.barrel.KeepsColour = views.paint.KeepsColour = true;
            _views[hue] = views;
            return views;
        }

        // ---- The panel

        public override void Panel(Session session)
        {
            ImGui.SeparatorText("Collected");
            ImGui.Text(_carried.Count == 0 ? $"carrying nothing (room for {Carry})"
                : $"carrying {string.Join(", ", _carried.Select(Hues.NameOf))} ({_carried.Count} of {Carry})");
            ImGui.Text($"{_drops.Count} drops left about");
            foreach (var hue in InPlay)
            {
                var c = Hues.Example(hue).ToVector4();
                ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Num.Vector4(c.X, c.Y, c.Z, 1f));
                ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Num.Vector4(c.X, c.Y, c.Z, 1f));
                ImGui.SliderFloat(Hues.NameOf(hue), ref _filled[(int)hue], 0f, Need, "%.1f drops in");
                ImGui.PopStyleColor(2);
            }
            if (ImGui.Button("all back"))
                foreach (var hue in InPlay)
                    _filled[(int)hue] = Need;
            ImGui.SameLine();
            if (ImGui.Button("all drained"))
                Array.Clear(_filled);
            ImGui.SameLine();
            if (ImGui.Button("scatter again"))
            {
                _scatter++;
                LayOut(session, _centre);
            }
            ImGui.SameLine();
            if (ImGui.Button("barrels here"))
                Beside(session);

            ImGui.SeparatorText("Where it comes back");
            var where = (int)Where;
            if (ImGui.Combo("where", ref where, SpreadNames, SpreadNames.Length))
                _grade.Changed();
            Where = (Spread)where;
            ImGui.SliderInt("drops to fill", ref Need, 1, 12);
            if (Where == Spread.Both)
                ImGui.SliderFloat("tint till full", ref Tint, 0f, 0.8f, "%.2f");
            if (Where != Spread.Everywhere)
            {
                ImGui.SliderFloat("place radius", ref PlaceRadius, 8f, 90f, "%.0f m");
                ImGui.SliderFloat("place edge", ref PlaceEdge, 0.5f, 30f, "%.1f m");
            }

            ImGui.SeparatorText("How it comes back");
            var how = (int)How;
            ImGui.Combo("how", ref how, ChangeNames, ChangeNames.Length);
            How = (Change)how;
            if (How == Change.Fade)
                ImGui.SliderFloat("fade time", ref FadeTime, 0.2f, 10f, "%.1f s");
            if (How == Change.Wave)
            {
                ImGui.SliderFloat("wave speed", ref WaveSpeed, 2f, 40f, "%.0f m/s");
                ImGui.SliderFloat("wave rim", ref Rim, 0f, 1f, "%.2f");
                if (_wave is { } spread)
                    ImGui.TextDisabled($"the wave's {spread:F0} m out");
            }

            ImGui.SeparatorText("How drained looks");
            var to = (int)To;
            var neutrals = (int)Neutrals;
            var look = (To, Left, Neutrals);
            ImGui.Combo("drained to", ref to, DrainedNames, DrainedNames.Length);
            ImGui.SliderFloat("colour left", ref Left, 0f, 0.5f, "%.2f");
            ImGui.Combo("whites, greys", ref neutrals, NeutralNames, NeutralNames.Length);
            (To, Neutrals) = ((DrainedTo)to, (Neutrals)neutrals);
            if (look != (To, Left, Neutrals))
                _grade.Changed();
            ImGui.Checkbox("only red, yellow and blue (the rest mixed)", ref Primaries);
            ImGui.Checkbox("the droid keeps its colours", ref DroidKeepsColour);
            ImGui.SliderInt("carries", ref Carry, 1, 10);
        }

        // The palette shader's grade (see IPaletteGrade): every colour as much as its hue's back in the world, in the place,
        // and behind the wave's front
        private sealed class LabGrade : IPaletteGrade
        {
            private readonly ColourLab _lab;

            public LabGrade(ColourLab lab) => _lab = lab;

            public int Version { get; private set; }

            public void Changed() => Version++;

            public GradeZones Zones
            {
                get
                {
                    var lab = _lab;
                    var (front, rim) = lab._wave is { } spread ? (spread, lab.Rim) : (-1f, 0f);
                    // Everywhere, the place is the whole world
                    return lab.Where == Spread.Everywhere
                        ? new GradeZones(lab._centre, 1e9f, 1f, front, rim)
                        : new GradeZones(lab._centre, lab.PlaceRadius, lab.PlaceEdge, front, rim);
                }
            }

            public void Grade(ReadOnlySpan<Vector4> colours, ref object? state, Span<Vector4> world, Span<Vector4> place, Span<Vector4> front)
            {
                var lab = _lab;
                var drained = new Drained(lab.To, RetroStyle.Background.ToVector3(), lab.Left, lab.Neutrals);
                // What its colours are made of, kept with it till they change or how drained looks does
                if (state is not Made made || made.Look != drained || made.Of.Length != colours.Length)
                {
                    state = made = new Made(drained, new Drained.Makeup[colours.Length]);
                    for (var slot = 0; slot < colours.Length; slot++)
                        made.Of[slot] = drained.Of(new Vector3(colours[slot].X, colours[slot].Y, colours[slot].Z));
                }
                // Everywhere, the place is the world; and the front's only seen while there's a wave
                var placed = lab.Where != Spread.Everywhere;
                var waving = lab._wave != null;
                var (worldGrey, placeGrey, frontGrey) = (drained.Grey(lab._worldShown), drained.Grey(lab._placeShown), drained.Grey(lab._front));
                for (var slot = 0; slot < colours.Length; slot++)
                {
                    var a = colours[slot].W;
                    world[slot] = new Vector4(drained.Restore(made.Of[slot], lab._worldShown, worldGrey), a);
                    place[slot] = placed ? new Vector4(drained.Restore(made.Of[slot], lab._placeShown, placeGrey), a) : world[slot];
                    front[slot] = waving ? new Vector4(drained.Restore(made.Of[slot], lab._front, frontGrey), a) : place[slot];
                }
            }

            private sealed record Made(Drained Look, Drained.Makeup[] Of);
        }
    }
}
