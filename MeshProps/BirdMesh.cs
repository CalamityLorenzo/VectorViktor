using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // The bird from the first VectorViktor: a small flattened diamond of a body, nose to +Z, and two long swept-back
    // wings, each a fan of WingSegments triangles from its middle, whose rim ripples as they flap - a sine wave
    // travelling out from the root to the tip, bigger towards the tip, so the wing undulates rather than hinging.
    //
    // A mesh can't bend, so a flap is Frames meshes, one for each step through it (see Source): the wave comes back
    // round to where it started after one flap, so they loop without a join. The body sits a hair above the wings'
    // roots, so the two don't fight to be seen where they overlap.
    public static class BirdMesh
    {
        public const int Body = 0, Wing = 1;
        public const int PaletteSize = 2;
        // Enough that at its flap's pace (0.35 a second, see Bird.FlapFrequency) it's 63 a second: a new one every
        // frame the game draws, as the first VectorViktor worked the wing out afresh every frame. Far fewer and the
        // flap visibly steps from one to the next. Each is only a couple of dozen triangles.
        public const int Frames = 180;

        public const float Scale = 1.3f;
        public const int WingSegments = 5;
        public const float WingSpan = 1.4f * Scale;     // each wing, root to tip
        public const float WingSweep = 0.5f * Scale;    // how far back the tip curves
        public const float FlapAmplitude = 0.5f * Scale;
        public const float WaveCount = 1.1f;            // wave cycles across the span
        private const float BodyLength = 0.5f * Scale, BodyWidth = 0.14f * Scale, BodyLift = 0.01f;

        public static Color[] Palette(Color body, Color wing) => new[] { body, wing };

        // The frame for `phase` of the way through a flap (0 up to 1).
        public static int FrameAt(float phase) => (int)(phase * Frames) % Frames;

        public static MeshSource Source(int frame, Color[] palette) =>
            new MeshSource($"bird:{frame}", d => Build(d, (float)frame / Frames), palette);

        // `phase` of the way through a flap.
        public static MeshData Build(GraphicsDevice device, float phase)
        {
            var mesh = new MeshBuilder();

            var nose = new Vector3(0f, BodyLift, BodyLength * 0.6f);
            var tail = new Vector3(0f, BodyLift, -BodyLength * 0.4f);
            var left = new Vector3(-BodyWidth, BodyLift, 0f);
            var right = new Vector3(BodyWidth, BodyLift, 0f);
            mesh.AddQuad(Body, nose, right, tail, left);
            mesh.AddLineLoop(nose, right, tail, left);

            foreach (var side in new[] { -1f, 1f })
            {
                var rim = new Vector3[WingSegments + 1];
                for (var i = 1; i <= WingSegments; i++)
                {
                    var t = (float)i / WingSegments;   // root to tip
                    var wave = MathF.Sin(MathHelper.TwoPi * (phase - t * WaveCount));
                    rim[i] = new Vector3(side * t * WingSpan, wave * FlapAmplitude * t, -t * t * WingSweep);
                }
                for (var i = 1; i < WingSegments; i++)
                    mesh.AddTri(Wing, rim[0], rim[i], rim[i + 1]);
                for (var i = 0; i < WingSegments; i++)
                    mesh.AddLine(rim[i], rim[i + 1]);
            }

            return mesh.Build(device);
        }
    }
}
