# 06: The TV: textures and render targets

**What you'll learn:** what a texture is, and two ways of changing one while the game runs: writing its pixels from the CPU (static), and having the GPU draw into it (a **render target**, a camera's feed); how to switch where the GPU draws and switch back; why a screen can't show a picture of itself; and point sampling, for pixels that stay hard.

**Built in:** [AnimationPlan.md](../AnimationPlan.md) step 4: [Screens.cs](../World.Maps/Screens.cs) (`StaticPicture`, `ScreenView`), `ScreenSpec` in [RoomSpec.cs](../World.Buildings/RoomSpec.cs), and `Feed` and `DrawFeeds` in [WorldRenderer](../World.Maps/WorldRenderer.cs).

Run it: `Basic.World.exe telly` puts you in the cottage facing its television, which shows what your drone sees: you, from behind. The old set up in the attic (`Basic.World.exe attic`) shows static.

## Everything else here has no textures

Every mesh so far is flat colours: each triangle's colour comes from a palette slot, and the white edges are lines. There's no image anywhere. A television needs one: a picture, changing every frame, laid over its screen.

A **texture** is an image on the GPU: a grid of pixels (a `Texture2D`) that a triangle can be painted with. Each vertex says which point of the image it sits on (its **texture coordinates**, `u` across and `v` down, each 0 to 1), and the GPU stretches the image across the triangle between them.

[ScreenView](../World.Maps/Screens.cs) is a 4 × 4 grid of quads laid over the set's glass. The glass bulges like an old tube, so the grid follows the bulge (`TelevisionMesh.ScreenPoint`, which the set's own mesh uses too). It stands 3 mm proud of the glass so they don't z-fight. Its vertices are `VertexPositionTexture`: a position and a `(u, v)`. Its `v` is turned upside down (`1 - v`), because the grid counts up the screen and an image counts down from its top.

It's drawn after everything else in the frame, with a `BasicEffect` that has `TextureEnabled` and the same fog as the world, depth-tested so walls in front of it hide it. The sampler is `SamplerState.PointClamp`: each screen pixel takes the colour of the nearest texture pixel, with no blending between neighbours. Blended (linear) sampling would blur a 48-pixel-wide picture into mush; point sampling keeps its pixels square and hard, like everything else in the game.

## Way one: pixels from the CPU (static)

[StaticPicture](../World.Maps/Screens.cs) keeps a 48 × 36 array of colours. Every two ticks it fills the array with random greys and copies it to its texture with `Texture.SetData(pixels)`. That's 1,728 pixels, nothing to the GPU.

The random numbers come from **xorshift**: three shifts and exclusive-ors of a 32-bit number. It's fast, needs no allocation, and is plenty random for looking at. `System.Random` would do too; xorshift just shows how little randomness a picture of noise needs.

## Way two: the GPU draws into a texture (a feed)

To show what a camera sees, the world has to be *drawn* from that camera, and the drawing kept as a picture. The GPU can draw into a texture instead of onto the screen: a `RenderTarget2D` is a texture you can also draw into.

`WorldRenderer.Feed(channel, camera, extra)` makes one, 96 × 72, for a named **channel**. Before each frame, `DrawFeeds` does this for each feed:

```csharp
var previous = _device.GetRenderTargets();       // wherever the frame is going (the game's low-res picture)
_device.SetRenderTarget(target);                 // draw into the feed's texture instead
_device.Clear(RetroStyle.Background);
DrawFrom(view.Eye, view.Eye + view.Forward, view.Up, you, clock, colorsOn, extra);
...
_device.SetRenderTargets(previous);              // back again
_device.Clear(RetroStyle.Background);
```

- **The same drawing code.** `DrawFrom` is what draws the main picture too. The projection takes its shape from the viewport, which is now 96 × 72, so the feed comes out 4:3 without being told.
- **Its own depth buffer, with a stencil.** The feed has a depth buffer so walls still hide what's behind them. It needs a stencil as well, because the windows onto elsewhere are drawn with one (see WindowPortals).
- **`PreserveContents`.** A render target's contents may be thrown away when you switch off it. This one keeps its last picture, so a frame without a new picture (no camera just then: you're driving, and the drone's put away) still shows the last one.
- **Clearing after switching back.** Switching away and back can lose what the main target held. Nothing has been drawn to it yet this frame, so clearing it again costs nothing and makes sure.

A room's [ScreenSpec](../World.Buildings/RoomSpec.cs) says where a set stands and which channel it's tuned to. When a screen is drawn, `WorldRenderer.Picture(channel)` gives it that channel's feed, or static if nothing feeds that channel. The game decides what feeds there are: [Basic.World](../Basic.World/Game1.cs) and the playground both feed `WorldRenderer.DroneChannel` from the drone, with an `extra` that adds the player (or the droid) to the picture, since the world alone has no characters in it.

## A screen can't show itself

While a feed is being drawn, the screens are left out (`_feeding`). If they weren't, a screen tuned to that feed would be textured with the very render target being drawn into. The GPU can't read from and write to the same texture in one draw; at best the picture is garbage, and at worst the draw fails. Leaving screens out of feeds is the simplest way round it. The cost: a set seen *in* a feed shows its bare glass.

A real camera pointed at its own monitor gives a tunnel of pictures within pictures: **video feedback**. You could get that here with two render targets, drawing into one while the screens show the other and swapping each frame (double buffering). Then each picture would show the one from the frame before.

## What it costs

Each feed draws the whole world again, every frame, just smaller. A 96 × 72 picture has few pixels, but the work of gathering and drawing meshes is much the same as for the main view. Two feeds are fine. Twenty would not be. Screens are only drawn within `ScreenReach` (30 m), where their picture could be made out, but feeds are drawn whether a screen is in view or not.

## Exercise (optional): the head camera on the attic set

Tune the attic's set to the droid's head camera, in the playground.

1. In `Playground.LoadWorld`, after the drone's feed, add a feed on a channel called `"head"`. The camera is the droid's head camera: `DroidRig.CameraView(_rig)` gives its eye, forward and up, to make a `CameraView` from. For `extra`, add the droid's rig, leaving out the camera's own mesh as the head view does (see `DrawWorld`), or the picture will be the inside of the lens.
2. In `Houses.TwoStorey`, tune the attic's `ScreenSpec` to `"head"` in place of `ScreenSpec.Static`.

**Check it:** run `Droid.Playground.exe attic`. The set stands a little ahead of you, its screen facing south (to your right): drive round in front of it and turn to face it. The screen should show what you're seeing, and the set in that picture should have blank glass.

**Stretch:** feeds are drawn every frame even when no screen tuned to them is in view. Make `DrawFeeds` skip a feed when it isn't needed: when none of the screens within `ScreenReach` of the eye is tuned to it. `PreserveContents` means a skipped feed still shows its last picture when you come back.

## Thinking questions

1. Why is static made every *two* ticks, not every frame?
2. The feed is drawn before the frame. What would go wrong if it were drawn after the main picture, in the same frame?
3. In the exercise, the set seen in the head camera's picture shows bare glass. Why, exactly?

<details>
<summary>Answers</summary>

1. It's tied to the clock, not to frames, so it looks the same at 60 frames a second or 144. Thirty new pictures a second still reads as flicker, for half the work of sixty. And when time's paused in the playground, the static freezes with everything else.
2. The screens in the main picture would show the feed as it was *last* frame, a frame behind. Also, switching render targets after the main picture is drawn could lose it, unless it were preserved.
3. While the head feed is drawn, `_feeding` is set and every screen is left out, so the set is drawn with only its own mesh: the flat glass colour from its palette.

</details>

## Further reading

- MonoGame's documentation on render targets ("How to create a render target") and on `SamplerState`.
- *Real-Time Rendering* (Akenine-Möller, Haines, Hoffman), the texturing chapter: texture coordinates, magnification and why point sampling looks blocky.
- George Marsaglia, *Xorshift RNGs* (2003): four pages, and the source of those three shifts.
