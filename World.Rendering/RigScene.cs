using MeshCore.Library;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core.Animation;

namespace World.Rendering
{
    // A rig (see Rig) drawn as parts that move by themselves (see ScenePart): one for each of its parts with a mesh,
    // each where its joint is so many seconds in. `pose` poses the rig for a moment, from rest (clips, and whatever's
    // worked out in code); it's done once a moment, however many parts ask. Its cables (see Cable) are drawn a stretch
    // at a time with `cable`, a unit length of cable (see Rig.Span).
    //
    // For a rig whose every move is a function of time: something on show, a machine running. The parts' meshes
    // are fixed when it's made, so a node whose part changes as it goes (RigNode.Part: a track's baked frames) needs
    // `variants`, every part it might change to given the one it starts with: it gets a part for each, and all but the
    // one it has at the moment are put OutOfSight, where the batch leaves them out. Anything else that swaps a part
    // needs a view that follows the rig as it goes (see RigView).
    public static class RigScene
    {
        // Somewhere no camera's far plane reaches, for a part that isn't there just now
        public static readonly Matrix OutOfSight = Matrix.CreateTranslation(0f, -1e6f, 0f);

        public static ScenePart[] Parts(Rig rig, IReadOnlyDictionary<string, MeshSource> meshes, Action<Rig, float> pose, Matrix placement,
            MeshSource? cable = null, Func<string, IEnumerable<string>>? variants = null)
        {
            var posedAt = float.NaN;
            void PoseAt(float seconds)
            {
                if (seconds == posedAt)
                    return;
                rig.Reset();
                pose(rig, seconds);
                rig.Solve(placement);
                posedAt = seconds;
            }

            var parts = new List<ScenePart>();
            for (var i = 0; i < rig.Count; i++)
            {
                if (rig[i].Part is not { } first)
                    continue;
                var index = i;
                foreach (var part in variants?.Invoke(first) ?? new[] { first })
                {
                    if (!meshes.TryGetValue(part, out var mesh))
                        throw new KeyNotFoundException($"No mesh for the rig's part '{part}' (at '{rig[i].Name}').");
                    parts.Add(new ScenePart(mesh, seconds =>
                    {
                        PoseAt(seconds);
                        return rig[index].Part == part ? rig.World(index) : OutOfSight;
                    }));
                }
            }
            if (rig.Cables.Count > 0 && cable == null)
                throw new ArgumentException("The rig has cables, but there's no mesh to draw them with.", nameof(cable));
            foreach (var strung in rig.Cables)
                for (var stretch = 0; stretch < strung.Stretches; stretch++)
                {
                    var (along, s) = (strung, stretch);
                    parts.Add(new ScenePart(cable!, seconds =>
                    {
                        PoseAt(seconds);
                        return rig.Span(along, s);
                    }, RigView.CablesSeenWithin));
                }
            return parts.ToArray();
        }
    }
}
