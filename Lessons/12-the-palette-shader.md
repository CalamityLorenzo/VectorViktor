# 12: The palette shader: one draw for every colour

**What you'll learn:** what a shader is and where it runs; how vertex data and constants get to it; how to read the one this project uses, line by line; how MonoGame compiles and loads it; and why a draw call costs what it does, so you can tell when this kind of work is worth doing.

**Built in:** [ArchitectureReviewPlan.md](../ArchitectureReviewPlan.md) 2.1 and 5.1, as [PaletteEffect.fx](../MeshRendering/Shaders/PaletteEffect.fx) and [PaletteEffect.cs](../MeshRendering/PaletteEffect.cs).

## The problem

Every mesh here is coloured from a **palette**: a short list of colours, one per *slot*. A box's sides are slot 0, its ends slot 1, its top slot 2 ([MeshBuilder.SetBoxShades](../MeshCore.Library/MeshBuilder.cs)). The palette belongs to each *instance* of a mesh, not to the mesh, so two crates can share one mesh and still be different colours. The player's palette even changes as they get wet.

Until now the faces were drawn with MonoGame's **`BasicEffect`**, which colours a whole draw call one colour. So a mesh was drawn a slot at a time: the droid's head (metal, pale head, ruby visor, three shades each) took eight draw calls. Each call has a fixed cost on the CPU of roughly 10 to 25 µs, whatever its size: the effect sends all its settings to the graphics card again, and the driver checks and queues the call. With the droids on show in the workshop yard, one view made 843 calls and spent 13 to 16 ms a frame just issuing them: the whole frame's budget at 60 frames a second.

The fix: give the graphics card the palette once, and let **every corner of every face say which slot it's coloured from**. Then a mesh's faces are one draw call however many colours they have.

## The idea: shaders

A **shader** is a small program that runs on the graphics card, thousands of copies at once. Drawing triangles goes through a fixed sequence (the *pipeline*), and two steps of it are programs you write:

```
vertices ──▶ VERTEX SHADER ──▶ assemble triangles ──▶ rasterise ──▶ PIXEL SHADER ──▶ depth test ──▶ the picture
            (once per corner)                        (find the pixels   (once per pixel
                                                      each covers)       it covers)
```

- The **vertex shader** runs once for every corner. Its main job is to say *where on the screen* the corner lands. It can also work out other values to hand on.
- The **rasteriser** (not programmable) finds which pixels each triangle covers. Whatever the vertex shader handed on, it *blends across the triangle* for each pixel.
- The **pixel shader** runs once for every pixel covered, and says *what colour* it is.

A shader gets two kinds of input:
- **Vertex data**, different for every corner: here, its position and its colour slot. These come from the mesh's vertex buffer.
- **Constants** (*uniforms*), the same for a whole draw call: here, the matrices that place the mesh, the palette and the fog. These are set from C# before the draw.

`BasicEffect` is a shader too, one MonoGame ships ready-made with a fixed set of options. Writing our own means we choose what goes in.

## Reading the shader

Open [PaletteEffect.fx](../MeshRendering/Shaders/PaletteEffect.fx). It's written in **HLSL**, DirectX's shader language: C-like, with vector types built in (`float3`, `float4`, `float4x4`).

**The constants** (near the top, shortened here; the grade's are in [Grading](#grading-draining-the-colour-and-bringing-it-back), below):

```hlsl
float4x4 WorldViewProj;     // from the mesh's own space straight to the screen
float4x4 WorldView;         // from the mesh's own space to the eye's: for the fog
float4 Palette[MAX_COLOURS];
float3 FogColor;
float FogStart;             // and FogEnd, and FogOn: 1 fogged, 0 not
```

`Palette` is the instance's palette, up to 64 colours. A `Color` becomes a `float4` (red, green, blue, alpha, each 0 to 1).

**The vertex data:**

```hlsl
struct VertexIn
{
    float4 Position : POSITION0;
    float Slot : TEXCOORD0;
};
```

The words after the colons are **semantics**: labels that match the shader's inputs to the parts of each vertex in the buffer. On the C# side, [VertexPositionSlot](../MeshCore.Library/VertexPositionSlot.cs) describes its 16 bytes with a `VertexDeclaration`: bytes 0 to 11 are a `Vector3` labelled `Position`, usage 0; bytes 12 to 15 are a `float` labelled `TextureCoordinate`, usage 0. The labels must match, or the shader reads the wrong bytes. `TEXCOORD` is just a free slot here: the slot number isn't a texture coordinate, but any spare label will do.

**The vertex shader** (shortened: the grade's lines are left out):

```hlsl
VertexOut Faces(VertexIn input)
{
    VertexOut output;
    output.Position = mul(input.Position, WorldViewProj);
    output.Colour = Palette[(int)(input.Slot + 0.5)];
    float depth = -mul(input.Position, WorldView).z;
    output.Fog = FogOn * saturate((depth - FogStart) / (FogEnd - FogStart));
    return output;
}
```

- `mul(position, WorldViewProj)` places the corner on the screen ([lesson 10](10-cameras-and-views.md) explains world, view and projection). XNA and MonoGame put the vector on the *left* of the matrix, so it's `mul(v, M)`, not `mul(M, v)`.
- The colour is looked up in the palette by the corner's slot. `+ 0.5` before turning it into a whole number guards against a slot of 3 arriving as 2.9999.
- The fog fades with **depth in front of the eye** (minus z in the eye's space, since the eye looks down −z), from nothing at `FogStart` to all fog at `FogEnd`. `saturate` clamps it between 0 and 1. It's exactly how `BasicEffect` fogs, because the edges are still drawn with `BasicEffect`: if the two fogged differently, far faces and their outlines would fade at different rates.

All three corners of a face have the same slot, so when the rasteriser blends the colour across the triangle, it's the same everywhere: flat colour, as the game's look wants.

**The pixel shader** (shortened the same way):

```hlsl
float4 Colour(VertexOut input) : COLOR0
{
    return float4(lerp(input.Colour.rgb, FogColor, input.Fog), input.Colour.a);
}
```

`lerp(a, b, t)` mixes from `a` to `b`: the face's colour, faded into the fog by however much fog there is.

**The technique** names the pair of programs and which shader model to compile them for. `vs_4_0_level_9_3` is DirectX 11's compiler aimed at the oldest hardware it supports, so the same shader runs whichever graphics profile a game asks for.

## The C# side

**Every corner gets its slot.** [MeshBuilder](../MeshCore.Library/MeshBuilder.cs#L225) already collected each slot's triangles together. Now `FacesBuffer` writes each corner as a `VertexPositionSlot`, its slot taken from the run it's in. The buffer is 16 bytes a corner instead of 12, and that's the only cost.

**Loading it.** `new Effect(device, bytes)` needs the shader *compiled*. MonoGame's content builder compiles it into an `.xnb` file. The `.xnb` is kept in the repository beside the `.fx`, built into the `MeshRendering` library as an embedded resource, and read back by [CompiledShader](../MeshRendering/PaletteEffect.cs#L91). That's the `.xnb`'s short header, then the compiled effect itself. A build step in [MeshRendering.csproj](../MeshRendering/MeshRendering.csproj#L29) runs the content builder again **whenever the `.fx` is newer than the `.xnb`**, so you edit the shader, build, and it's done. One effect is made per graphics device ([For](../MeshRendering/PaletteEffect.cs#L29)) and shared by every mesh.

**Drawing with it.** In [MeshInstance.DrawSolids](../MeshRendering/MeshInstance.cs#L139):

```csharp
var effect = PaletteEffect.For(gd);
effect.Take(fx);   // the BasicEffect's view, projection and fog
if (faces is { } background)
    effect.Draw(gd, Mesh.Solids, world, Faded(background));          // colours off
else if (effect.Grade is { } grade && !KeepsColour)
    effect.Draw(gd, Mesh.Solids, world, Graded(grade), grade.Zones); // see Grading, below
else
    effect.Draw(gd, Mesh.Solids, world, _colours);
```

and in [Send](../MeshRendering/PaletteEffect.cs#L78), which both of `PaletteEffect`'s `Draw`s end in:

```csharp
var worldView = world * _view;
_world.SetValue(world);
_worldView.SetValue(worldView);
_worldViewProj.SetValue(worldView * _projection);
_palette.SetValue(palette);
device.SetVertexBuffer(faces);
_pass.Apply();                       // send the constants to the graphics card
device.DrawPrimitives(PrimitiveType.TriangleList, 0, faces.VertexCount / 3);
```

- **The matrices are multiplied together on the CPU, once a draw**, rather than in the shader once a corner: a few hundred corners each multiplying three matrices would be wasted work.
- **`Take` copies the view, projection and fog from `BasicEffect`**, which everything else is still drawn with. So none of the code that calls `MeshBatch` or `MeshInstance` had to change.
- **The palette is kept ready as `Vector4`s** (`_colours`), changed only when `SetColor` changes a colour. With the colours off (C), it's a faded copy (`Faded`), worked out again only when the background or the tint changes.
- **Edges and outlines are still drawn with `BasicEffect`**: they're all one colour (white), so they were always one draw each.

## Grading: draining the colour and bringing it back

The game's world has lost its colour, and bringing it back is the game (see [GameDesign.md](../GameDesign.md) 3.3). Because every colour now goes through the shader as a palette, the whole world's colour can be changed as it's drawn without touching a mesh. That's what [IPaletteGrade](../MeshRendering/IPaletteGrade.cs) is for, and the playground's colour lab ([ColourLab](../Droid.Playground/Experiments/ColourLab.cs), `Droid.Playground colour`) uses it.

- **Set `PaletteEffect.Grade`**, and every instance's palette is graded before it's drawn: each colour sorted by its hue ([Hues](../World.Core/Colour/Hues.cs)) and drawn as much as that hue is back ([Drained](../World.Core/Colour/Drained.cs)). An instance with `KeepsColour` (a drop of paint, the droid) is left alone.
- **Three palettes, not one.** The grade gives each instance's palette three ways: as the *world* shows it, as a *place* shows it, and behind a *front* spreading out from the place's centre. The shader gets all three (`Palette`, `PlacePalette`, `FrontPalette`), passes all three colours from the vertex shader to the pixel shader, and also passes where the pixel is in the world (`mul(input.Position, World).xz`). The pixel shader then picks by distance from the centre:

```hlsl
float off = distance(input.Ground, Zones.xz);                       // how far across the ground
float3 place = off < Front.x ? input.Behind.rgb : input.Place.rgb;   // inside the front, the front's
colour = lerp(colour, place, saturate((Zones.w - off) / Front.y));    // the place's, faded at its edge
```

  So a wave of colour sweeping out from a barrel is smooth across a face, even across one huge terrain face, while the CPU only changes one number a frame (the front's radius).
- **Graded only when something changes.** Each instance keeps its three graded palettes, and grades again only when the grade's `Version` changes or its own colours do. A still world costs nothing; a fade costs a palette a mesh a frame while it runs.

## What it bought

`Basic.World.Benchmark` over all 33 starts, before and after: 352 → 125 draw calls a frame, 4.38 → 0.54 ms of CPU to draw a frame, 5.28 → 0.81 ms a frame including the graphics card. The draw-call *count* fell by about two thirds, but the *time* by seven eighths: the calls that went were dearer than the ones left. Counting calls is a good rough guide to what drawing costs; timing it is the real one.

## Why this way, and what else could be done

- **Colours on the vertices instead of slots?** `VertexPositionColor` with `BasicEffect`'s vertex colours needs no shader of our own. But then the colours are baked into the mesh, and two instances in different colours need two copies of the mesh. Getting wet, colours off and tinting would each mean rewriting a vertex buffer.
- **The palette in a texture?** A 64 × 1 texture, looked up by slot, would allow more than 64 colours. Constants are simpler and faster for a list this short.
- **Instancing:** drawing many copies of one mesh in a single call, each copy's matrix and palette from a second buffer. It was measured and isn't worth it yet: a view repeats a mesh only about 13 times in 80, which would save about 0.1 ms. It needs an instanced version of this shader, *and* one for the edges, since `BasicEffect` can't instance. When a view holds a forest or a crowd, it will be worth it.
- **Lighting:** the "shades" (`Side`, `Dim`, `Top`) are fake lighting chosen by hand per face. A shader could light faces from a sun direction instead, from each face's normal. It would need a normal on every vertex, and it would change the game's look.

## Exercise (optional): make the droid flash

Add a **flash** to the shader: a colour every face of an instance is mixed towards, and how far. A droid part being hit could flash red, or a picked thing in the map studio could glow. Then make the playground flash the droid while H is held.

1. **The shader.** In [PaletteEffect.fx](../MeshRendering/Shaders/PaletteEffect.fx), add a constant `float4 Flash;` (rgb the colour, a how far, 0 to 1). In the pixel shader, mix the face's colour towards `Flash.rgb` by `Flash.a` with `lerp`, *before* the fog: a far droid should still fade into the fog.
2. **The effect.** In [PaletteEffect.cs](../MeshRendering/PaletteEffect.cs), find the new parameter (`p["Flash"]`) beside the others. Give `Send` a `Vector4 flash` to `SetValue` before `Apply`, and both `Draw`s a `Vector4 flash = default` to pass on to it.
3. **The instance.** Give [MeshInstance](../MeshRendering/MeshInstance.cs) a `public Vector4 Flash { get; set; }`, and pass it to each `effect.Draw` in `DrawSolids`.
4. **The rig.** Give [RigView](../World.Rendering/RigView.cs) a `Flash` too, and in `Add` set every part's `view.Flash` from it, just before `batch.Add(view)`.
5. **The key.** In [Playground.cs](../Droid.Playground/Playground.cs) `UpdateWorld`, beside the other keys: while H is down, set `_rigView.Flash` to red with an amount that pulses, say `0.4f + 0.3f * MathF.Sin(_session.Clock * 20f)`; otherwise `Vector4.Zero`.

**Check it:** build (the shader recompiles: the build prints the `.fx`'s path as it does), run `Droid.Playground workshop`, press F2 for the drone, and hold H. Your droid pulses red; the droids on show don't; everything's edges stay white.

**Stretch:** flash a pushed crate while it's being pushed. `WorldView` draws the things (see `_things` in [WorldView.cs](../World.Maps/WorldView.cs)), and a body knows its speed.

## Thinking questions

1. Why does each corner carry its colour's *slot* and not the colour itself?
2. In the exercise, why do the edges stay white while the faces flash?
3. The slot is a `float` in the vertex but an index into an array in the shader. What could go wrong without the `+ 0.5`, and why only sometimes?
4. Why does it matter that the faces fog exactly as `BasicEffect` fogs?
5. The calls fell by two thirds but the time by seven eighths. What does that say about the calls that went, and how would you find out why?
6. The grade's wave picks its palette per *pixel*, in the shader. Why not just grade each instance by where it stands, on the CPU, which would need no shader change at all?

<details>
<summary>Answers</summary>

1. The colour belongs to the *instance*, the slot to the *mesh*. With slots, one mesh in the graphics card's memory serves every instance in every colour, and recolouring (getting wet, colours off) is changing a short list, not rewriting thousands of vertices.
2. They're drawn with `BasicEffect`, not the palette shader, so the flash never reaches them. To flash the lines too you'd set `BasicEffect.DiffuseColor` for that instance's edges, or draw edges with a shader of your own.
3. A whole number stored as a `float` and then interpolated, or worked on by the graphics card's own arithmetic, can arrive as 2.99999 instead of 3, and turning that into an `int` cuts it down to 2: the wrong colour. Usually it arrives exact, so the bug would show only on some hardware or some faces. Adding a half and cutting down always gives the nearest whole number.
4. The edges and outlines are drawn with `BasicEffect` over the faces. If the faces faded faster or slower than their own edges, far things would show white outlines round faded faces, or the other way round.
5. They cost more than the average call that's left. A likely reason: every one of them came with a `pass.Apply()` that set up and sent `BasicEffect`'s settings, and the ones left include many cheap edge draws. To find out, time the two kinds separately: the benchmark's `turn` and `still` modes, with a `Stopwatch` round each kind of draw, is how this project's other costs were found (see ArchitectureReviewPlan.md 5.3).
6. An instance can be huge: a terrain chunk, a whole floor. Graded by where it stands, it would change all at once as the front passed its middle, so the wave would jump across the ground a chunk at a time. Per pixel, the front crosses a face smoothly, and it costs only a distance and a comparison per pixel. The CPU still does the expensive part (sorting colours by hue) once per palette, not once per pixel.

</details>

## Further reading

- MonoGame's documentation (docs.monogame.net) on custom effects and the content builder.
- Microsoft's [HLSL reference](https://learn.microsoft.com/windows/win32/direct3dhlsl/dx-graphics-hlsl), especially semantics and the intrinsic functions (`mul`, `lerp`, `saturate`).
- *The Book of Shaders* (thebookofshaders.com): pixel shaders, from the very start.
