using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core;
using World.Core.Animation;
using World.Core.Characters;
using World.Rendering;
using World.Maps;

namespace Maps.Home
{
    // The starting droid (see DroidRig) on show on the hills, to watch its rig at work: it rocks back and forth on its
    // wheels, leaning into each start and stop as a Segway does, while its head camera runs round and round the
    // visor, its ear dishes sample the air, and every seven seconds it waves (a keyframed clip, laid over the rest).
    // It faces north, towards the 'droid' start.
    public static class DroidDisplay
    {
        public static readonly Vector2 At = new Vector2(-3f, 3f);
        public static readonly Vector2 WatchFrom = At - new Vector2(0f, 2f);

        private const float Travel = 0.35f;   // metres either way it rocks
        private const float Rate = 0.9f;      // radians a second: once back and forth every seven seconds
        private const float LeanPerAcceleration = 0.5f;   // radians of lean for each m/s² it speeds up or slows by

        public static IEnumerable<ScenePart> Parts(Terrain terrain)
        {
            var rig = DroidRig.Build();
            var wave = DroidRig.Wave();
            var heading = -Vector2.UnitY;   // north
            var turn = Matrix.CreateRotationY(MathHelper.Pi);   // the rig faces +Z; this faces it north
            void Pose(Rig r, float t)
            {
                // Rolling along its heading, up and down with the ground under it
                var along = Travel * MathF.Sin(Rate * t);
                var acceleration = -Travel * Rate * Rate * MathF.Sin(Rate * t);
                var here = At + heading * along;
                r.Change(DroidRig.Root, p => p with { Translation = new Vector3(0f, terrain.HeightAt(here.X, here.Y), along) });
                DroidRig.Roll(r, along);
                DroidRig.Tilt(r, LeanPerAcceleration * acceleration);

                wave.Apply(r, t);
                DroidRig.Look(r, 0.7f * t, up: 0.15f * MathF.Sin(0.5f * t));   // round and round the visor
                DroidRig.Listen(r, t);
            }
            var palette = DroidMesh.StartingPalette();
            return RigScene.Parts(rig, DroidMesh.Sources(palette), Pose, turn * Matrix.CreateTranslation(At.X, 0f, At.Y),
                DroidMesh.CableSource(palette));
        }
    }
}
