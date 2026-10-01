# 10: Cameras: view, projection and snap views

**What you'll learn:** what the view and projection matrices do; how perspective and orthographic projections differ, and why a map editor wants both; how a camera described by a target, a direction and a distance can orbit, look about and fly; how to move smoothly from one view to another (easing, the short way round, zooming by ratios); and the snags: looking straight down, fog, and how far away a flat view's eye should be.

**Built in:** [ToolsPlan.md](../ToolsPlan.md) step 4: the map studio, [Map.Studio](../Map.Studio), above all [StudioCamera](../Map.Studio/StudioCamera.cs), and the hooks it uses in [WorldRenderer](../World.Maps/WorldRenderer.cs). Lesson 09 comes first.

Run it: `Map.Studio.exe` (or `Map.Studio.exe coast`). Press 1, 2, 3 and 4 for the overhead, front, side and three-quarter views, and 0 to go back to a free perspective view. Fly with W, A, S, D, R and F; zoom with the wheel; look about with the right mouse button; orbit with the middle one.

## Two matrices

Drawing a 3D point on the screen takes two steps, each a matrix:

1. **The view matrix** moves the world so the camera is at the origin, looking down its own -Z axis. It's built by `Matrix.CreateLookAt(eye, target, up)`: where the camera is, a point it's looking at, and which way is up on the screen. Everything about *where the camera is* is here.
2. **The projection matrix** squashes what the camera can see (a shape called the view volume) into a box from -1 to 1 across and up, which the graphics card then stretches over the picture. Everything about *what kind of lens* is here.

The game only ever had one lens: perspective. The studio needs a second.

### Perspective

`Matrix.CreatePerspectiveFieldOfView(fieldOfView, aspect, near, far)`. The view volume is a pyramid with its top cut off (a frustum). Things further off are divided by their distance, so they come out smaller: that's what makes a picture look deep. The game uses 70 degrees from the top of the picture to the bottom.

### Orthographic

`Matrix.CreateOrthographic(width, height, near, far)`. The view volume is a box. Nothing is divided by distance, so **a metre is the same size on the screen wherever it is**. Lines that are parallel in the world stay parallel on the screen.

That's how plans and elevations are drawn, and why a map editor wants it. In an overhead orthographic view, the map *is* a plan: a 4 m pad is the same size at the top of the screen as at the bottom, a road runs straight, and you can place things by eye on a grid. In perspective, the far end of the same pad is smaller, and "level with that" means nothing.

The studio's snap views:

| Key | View | Looking | Projection |
|---|---|---|---|
| 1 | overhead | straight down, north up the screen | orthographic: a plan |
| 2 | front | north | orthographic: an elevation |
| 3 | side | west | orthographic: an elevation |
| 4 | three-quarter | down at 35 degrees, from the south-west | perspective |
| 0 | free | wherever it was | perspective |

## A camera by target, direction and distance

[StudioCamera](../Map.Studio/StudioCamera.cs) doesn't store an eye position. It stores:

- `Target`: the point it's looking at
- `Yaw` and `Pitch`: which way it's looking (yaw round from north, clockwise; pitch up from level)
- `Distance`: how far back from the target the eye is

and works the rest out: `Forward` from the yaw and pitch, then `Eye = Target - Forward * Distance`.

This makes the three ways of moving a camera one line each:

- **Orbit** (middle button): change the yaw and pitch. The target stays put and the eye swings round it. This is how you look at one thing from all sides.
- **Look about** (right button): change the yaw and pitch, then move the target so the eye stays put. This is how you look round from where you are.
- **Fly** (W, A, S, D, R, F): move the target, and the eye goes with it.

Zooming changes `Distance`. In a perspective view the eye moves in. In a flat view there's nowhere to move in to, so `Distance` sets how much of the world the picture shows instead (see below).

Flying goes faster the further back the camera is, so crossing the view takes about the same time whether you're looking at a chair or a whole valley.

### Which way is up?

`CreateLookAt` needs an up direction. The obvious one is the world's up, (0, 1, 0). But **looking straight down, the world's up is exactly along the line of sight**, and "which way is up on the screen" has no answer. The matrix comes out full of NaNs or spins at random. This is the classic gimbal problem of look-at cameras.

So the camera works its up out from its own yaw: `Up = Cross(Right, Forward)`, where `Right` comes from the yaw alone and is always level. Looking straight down with a yaw of 0, `Right` is east and `Up` comes out as north. That's why the overhead view has north at the top. The test `OverheadIsFlatWithNorthUp` in [ViewTests](../Map.Studio.Tests/ViewTests.cs) checks it.

### How big is a flat view?

An orthographic projection needs a width and height in metres. The studio makes it **the size the perspective view shows at the target**:

```csharp
public float Height => 2f * Distance * MathF.Tan(fieldOfView / 2f);
```

That's simple trigonometry. Half the field of view, out to `Distance`, spans `Distance × tan(half the angle)` above the middle. So the moment a view turns flat, things at the target don't change size: the switch doesn't jump. The test `AFlatViewShowsWhatThePerspectiveDidAtTheTarget` puts a point at the top edge in perspective, snaps to flat, and finds it still at the top edge.

### Where does a flat view's eye go?

In a flat view the eye's distance doesn't change the picture. But it still matters, for three reasons:

1. **Nothing behind the eye is drawn** (the near plane). Put the eye at the target, looking down, and a hill higher than the target is behind it and vanishes. So a flat view's eye stands far back: `FlatBack`, 600 m, more than any hill on any map.
2. **Fog is worked out from the distance to the eye.** At 600 m everything would be fog. So the studio turns fog off (there's a box to turn it back on, to see what the game will look like) and sets a far plane of its own: two more settings `WorldRenderer` gained for tools (`Fog`, `FarPlane`, `ProjectionOverride`).
3. **The terrain is built in chunks round the camera** (see [TerrainView](../World.Rendering/TerrainView.cs)), measured across the ground. For the front view, with the eye 600 m south of what you're looking at, it would build the wrong bit of the world. So `WorldRenderer.TerrainCentre` lets the studio say "build round the target instead".

Each is a small, separate setting, defaulting to what the game does. The game doesn't change, and the tool gets what it needs.

## Moving smoothly between views

Pressing 1 doesn't cut to the overhead view; the camera moves there over 0.4 s. A cut loses you: you can't tell which bit of the map you're now looking at, or which way round it is. A short move shows you.

[StudioCamera.MoveTo](../Map.Studio/StudioCamera.cs) remembers where the move started and where it's going (each a `Pose`: target, yaw, pitch, distance), and `Step` blends between them. Four details make it look right:

- **Easing.** The blend isn't straight. `t² (3 − 2t)` ("smoothstep") starts slowly, speeds up, and slows into place, as anything with weight would. The same curve as `Ease.InOut` in lesson 04.
- **The short way round.** From a yaw of 170 degrees to -170 is 20 degrees, not 340 back the other way. `MathHelper.WrapAngle(to - from)` gives the short difference; the move adds that to where it starts. The test `ASnapTurnsTheShortWayRound` checks it's due south halfway.
- **Zoom by ratio, not by amount.** Going from 10 m away to 400 m, a straight blend spends most of the move far out and rushes the end. Blending the *logarithm* of the distance (`from × (to / from)^t`) changes it by the same *ratio* each moment, which looks even.
- **Perspective while moving, flat on arrival.** There's no in-between of the two projections that looks sensible. So a move is always seen in perspective, and a flat view starts the moment it arrives. Because a flat view is sized to match (above), the switch doesn't show.

And while it moves, the pitch is held just short of straight down. In perspective, exactly overhead with the yaw changing, the picture would spin about its middle.

## Other ways it could have been done

- **A free-fly camera only** (as the playground's [FreeCamera](../MeshRendering/FreeCamera.cs), now shared in MeshRendering): simple, but orbiting round a thing, the most common move in an editor, is awkward.
- **Quaternions** for the camera's rotation instead of a yaw and a pitch: no special case looking straight down, and smooth blends between any two rotations (slerp). The cost is that "level" isn't built in, so a camera can roll by accident. Map editors want the horizon level, which yaw and pitch give for free.
- **A dolly zoom** for the change between projections: narrowing the field of view while backing the eye away, so perspective flattens smoothly into orthographic. It looks lovely, and a later version could do it.
- **Several viewports at once** (top, front, side and 3D, as in Blender or 3ds Max). Better for precise work, but four times the drawing, and a small window gets crowded.

## Exercise (optional): a view from the back

Add a fifth snap view: **back**, key 5, flat, looking south (the opposite of front).

1. Add `Back` to the `ViewKind` enum in [StudioCamera.cs](../Map.Studio/StudioCamera.cs).
2. In `SnapTo`, give it a yaw, a pitch and flat. Which yaw faces south? (North is 0, and yaws go clockwise.)
3. In [Studio.cs](../Map.Studio/Studio.cs), add key 5 to the list in `KeyboardInput` that maps number keys to views.
4. If you like, add a button for it in [Studio.Panels.cs](../Map.Studio/Studio.Panels.cs), beside the others.

**Check it:**
- Run the studio, press 2 (front), then 5. The camera should swing round the target to look the other way, and arrive flat.
- Add a test to [ViewTests](../Map.Studio.Tests/ViewTests.cs), like `OverheadIsFlatWithNorthUp`: snap to `Back`, `Arrive`, then check that it's `Flat`, that `Forward` is +Z (south), and that `Up` is the world's up. `dotnet test Map.Studio.Tests` should pass.
- Press W and S in the back view. They should move the camera up and down the screen, as in the front view. Why does that already work? (Look at `Fly`.)

**Stretch:** make 3 pressed again, while already in the side view, swing round to the other side (looking east). `Kind` says which view it's in. And a puzzle: from front to back is half a turn either way, so `WrapAngle` has to pick one. Press 2, 5, 2, 5 and watch. Which way does it always go, and why? (Look up what `MathHelper.WrapAngle` gives for exactly -π.)

## Thinking questions

1. Why does the overhead view need an up that isn't the world's up, while the front and side views don't?
2. A flat view's eye is 600 m back. What would you see in the overhead view of the pass map, whose peaks are up to 300 m, if it were 200 m back instead?
3. Picking with the mouse (lesson 11) turns a point on the screen back into a ray in the world. In a perspective view every ray starts at the eye. Where do the rays start in a flat view?

<details>
<summary>Answers</summary>

1. Looking straight down, the world's up points straight back along the line of sight, so the camera can't tell which way round to turn the picture: the sums divide by zero. Looking level (front, side), the world's up is square to the line of sight and works perfectly. Working the up out from the yaw works in both cases, so the camera always does that.
2. The target is on the road, about 90 m up on the passes. 200 m above it is 290 m, below the highest peaks. Any peak taller than the eye would be behind the near plane and not drawn. You'd see holes where the summits should be, the terrain cut flat by an invisible ceiling.
3. On the near plane, each from a different point: every ray goes in the same direction (straight down, for the overhead view), side by side, like rain. `Viewport.Unproject` handles both cases the same way: it turns the mouse's point at depth 0 and at depth 1 back into the world, and the ray runs from one to the other.

</details>

## Further reading

- *Real-Time Rendering* (Akenine-Möller, Haines, Hoffman), chapter 4, "Transforms": the view and projection matrices worked through.
- Microsoft Learn / MonoGame docs: `Matrix.CreateLookAt`, `Matrix.CreatePerspectiveFieldOfView`, `Matrix.CreateOrthographic`, `Viewport.Project` and `Unproject`.
- On easing: Robert Penner's easing equations, and "smoothstep" on Wikipedia.
