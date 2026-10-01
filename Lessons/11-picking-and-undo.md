# 11: Editing: picking, handles, undo, and playing what you've made

**What you'll learn:** how a click on the screen becomes a ray into the world, and how to find what it hits (boxes, turned boxes, the ground); how move handles ("gizmos") turn mouse movement into movement in the world; why every edit in an editor should be an object (the command pattern), and how immutable records make undo easy; how to show a change at once but record it once, when it's finished; and how the studio hands a map to the playtest harness and keeps it up to date.

**Built in:** [ToolsPlan.md](../ToolsPlan.md) steps 5 and 6: [Picking](../Map.Studio/Picking.cs), [MoveGizmo](../Map.Studio/MoveGizmo.cs), [Edits](../Map.Studio/Edits.cs), [Entries](../Map.Studio/Entries.cs), [Studio.Editing](../Map.Studio/Studio.Editing.cs) and [PlayHere](../Map.Studio/PlayHere.cs). Lessons 09 and 10 come first.

Run it: `Map.Studio.exe`. Click the stack of crates in the yard, west of the start, and drag it. Press Ctrl+Z, then Ctrl+Y. Pick `plant.oak` in the Add panel and click the ground a few times. Press P with the mouse over the yard, and the playground opens with the droid there.

## Picking: from a pixel to a thing

The mouse gives a point on the screen. To know what's under it, turn that point back into the world: lesson 10's matrices, run backwards.

`Viewport.Unproject(point, projection, view, world)` takes a screen point *and a depth* (0 at the near plane, 1 at the far plane) and gives the world point there. Do it twice, at depth 0 and 1, and the line through the two points is everything under that pixel: a **ray** ([Picking.RayAt](../Map.Studio/Picking.cs)). In a perspective view every ray starts near the eye and fans out. In a flat view they start side by side and run parallel. `Unproject` doesn't care which.

Then: what does the ray hit first?

### Boxes, turned

Everything the studio can select has a box: a crate's body, a prop's mesh, a start's marker, a pad (a thin slab). XNA's `Ray.Intersects(BoundingBox)` answers for a box square to the world's axes. But a prop turned 30 degrees isn't square to anything.

The trick: **don't turn the box; turn the ray.** Every box here has a matrix that places it (turned, then moved). Its inverse takes the world into the box's own frame, where the box *is* square to the axes. Put the ray through that inverse, and test the plain box:

```csharp
var into = Matrix.Invert(place);
var local = new Ray(Vector3.Transform(ray.Position, into), Vector3.TransformNormal(ray.Direction, into));
return local.Intersects(new BoundingBox(-half, half + Vector3.Up * size.Y));
```

`TransformNormal` turns the direction without moving it, since a direction has no position. And because the matrix only turns and moves (no scaling), a distance along the ray is the same in both frames, so the answer can be compared with any other hit.

### The ground

The ground has no box, and testing every terrain triangle would be slow. So [Picking.Ground](../Map.Studio/Picking.cs) **walks along the ray** in steps until it's below the ground (`HeightAt`), then **halves** the last step sixteen times to close in on where it crossed. The steps grow with distance, 1% of the way, because a metre's error far off is a pixel or less on the screen.

The risk is stepping right over a thin ridge. The test `TheGroundIsFoundUnderARay` walks back along the ray afterwards to check nothing nearer was underground.

### Nearest wins

[Studio.PickAt](../Map.Studio/Studio.Editing.cs) asks everything: every entry in every district file, every thing and building built in code, the ground. It keeps the nearest hit. What it found decides what a click does:

- an entry in a district file: **selected**, ready to drag, edit, turn or remove
- something built in code: **shown** (its name, its mass, where it is), but not editable, since it's C# and its home is its district's code
- the ground: its position and height shown

## Handles: dragging in 3D with a 2D mouse

A mouse moves in two dimensions; the world has three. Every 3D editor has to decide what a mouse movement means. The studio offers two answers.

**Drag the thing itself, and it slides over a level plane at its own height.** When you press, the ray is crossed with that plane (`Picking.Level`), and the gap between where you grabbed it and its middle is kept (`Offset`). Each frame after, the new ray crosses the same plane, and the thing goes there, offset and all. Whatever angle you look from, it stays under the mouse.

**Drag an arrow, and it moves only along that arrow.** The selected entry shows two arrows ([MoveGizmo](../Map.Studio/MoveGizmo.cs)): red east, blue south. Grabbing one locks the movement to that axis, and the arithmetic happens **on the screen**:

1. Project the arrow's two ends to the screen. That gives which way it points there, and how many pixels long it is.
2. Take how far the mouse has moved *along that screen direction* (a dot product). Sideways movement doesn't count.
3. Scale: if the arrow is 70 pixels long and 2 m long in the world, each pixel along it is 2/70 m.

The arrows are kept about 70 pixels long whatever the zoom (`MoveGizmo.Length` works out how many metres that is where they stand). So they're as easy to grab near or far, and the drag feels the same. Which arrow the mouse is over is also tested on the screen: the distance from the mouse to each arrow's line, within 8 pixels. The test `DraggingAlongAnArrowMovesInProportion` checks the sums with a made-up projection.

And **snapping**: while dragging, positions are rounded to the grid step picked in the Map panel (0.5 m to begin with; hold Ctrl not to). With G, the grid is drawn over the ground, draped over its bumps.

## Undo: every edit an object

An editor without undo is an editor people are afraid to use. The usual way to build it is the **command pattern**: every change is an object that knows how to do itself and how to undo itself ([IEdit](../Map.Studio/Edits.cs)). The [EditHistory](../Map.Studio/Edits.cs) keeps two lists:

- **done**: undo takes the last one, undoes it, and moves it to the other list
- **undone**: redo takes the last one, does it again, and moves it back

Making a new edit clears the undone list. After undoing three moves and making a fourth, "redo" has nothing it could sensibly mean.

Here there are only three kinds of edit, because district files are lists of entries:

| Edit | Does | Undoes |
|---|---|---|
| `AddEntry` | inserts an entry at an index | removes it |
| `RemoveEntry` | removes the entry at an index | puts it back *at the same index* |
| `ChangeEntry` | replaces an entry with another | puts the first back |

### Why records make it easy

Lesson 09's entries are **records that can't change**. Moving a crate doesn't change the crate's entry; it makes a new one (`with { At = ... }`) and puts it in the old one's place. So a `ChangeEntry` is just *the entry before and the entry after*. Undo puts back "before"; redo puts back "after". There's no need to work out what changed, or how to reverse it: any change at all to an entry, made any way at all, undoes the same way. (This is close to another pattern, the **memento**: keep a snapshot, and go back to it.)

`RemoveEntry` puts the entry back at its old index, not on the end, so everything after it is where it was. An undo that left things shuffled would quietly break the next undo, which remembers indexes.

### Showing at once, recording once

Dragging a crate across the yard is a hundred tiny moves, one a frame. That should be *one* undo, not a hundred. So:

- **While dragging**, the entry in the list is replaced each frame, so the markers and a ghost of the mesh follow the mouse. Nothing is recorded, and the world isn't rebuilt.
- **On letting go**, one `ChangeEntry` is recorded, from the entry as it was when grabbed to the entry now (`EditHistory.Record`: an edit already made, recorded so it can be undone).

The panels work the same way. Dragging a number in the Selected panel changes the entry live. The change is recorded once nothing in the panel is active any more (`ImGui.IsAnyItemActive`), whether that was a drag, a typed name or a ticked box.

### Building again, quickly

After each recorded edit, the whole map is built again from what's in memory (`Rebuild`), so what you see is what the game will build: buildings on their levelled pads, things settled onto the ground. Built naively, that took two seconds, nearly all of it making and drawing the terrain. But most edits don't change the ground. So while the pads are the same, the studio gives back **the same terrain** (emptied of the water the districts added: `Terrain.Drain`), and while its water is the same too, **the same terrain meshes** (`WorldView.ReleaseTerrain`). A typical edit now rebuilds in a few tens of milliseconds. When a pad does change, the terrain's drawn again a few chunks a frame, nearest first. The Map panel shows how long the last build took, and what of.

## Play here

Press P, and the studio:

1. saves, if there's anything to save
2. finds the playground's program, built beside its own ([PlayHere.Find](../Map.Studio/PlayHere.cs): up the folders from the studio's `bin\Debug\net10.0-windows` to the repository, then down into `Droid.Playground\bin\Debug\net10.0-windows`)
3. starts it as a separate program, with the map file and where to drop the droid: `Droid.Playground.exe D:\...\home.map.json at=-12.5,-6 yaw=-30`

Then both keep running. The playground watches the map's files (lesson 09's `MapWatcher`), so **every save in the studio shows up in the playground a moment later**, with the droid where it was. Edit, save, look, edit again: the loop the harness was built for.

A separate program, rather than playing inside the studio, keeps each simple: the studio never has a droid, physics ticking or experiments, and the playground never has editing. They share only the map files, which is the point of lesson 09.

## Other ways it could have been done

- **Picking by colour**: draw every object in a unique flat colour to a hidden picture, and read the pixel under the mouse. Exact to the pixel, whatever the shapes, but an extra drawing pass. Boxes are good enough here.
- **Physics raycasts**: an engine with a physics library would ask it. This project's physics is hand-rolled (axis-aligned boxes only), so the studio does its own.
- **Snapshots for undo**: save the whole district file after every edit, and undo by going back one. Simpler still, and fine for small files, but memory grows with every edit, and "what did that undo do?" has no name to show.
- **A rotate handle** (a ring to drag round): a natural next gizmo. Q and E do turning for now.

## Exercise (optional): nudging with the arrow keys

Make the arrow keys move what's selected by one grid step (or 10 cm with the grid off): Up north, Down south, Left west, Right east. Each nudge is an edit of its own, so Ctrl+Z undoes them one at a time.

1. In [Studio.Editing.cs](../Map.Studio/Studio.Editing.cs), add `private void Nudge(float east, float south)`. If something's selected: work out the step, make the moved entry with `Entries.MovedTo(before, Entries.Where(before) + new Vector2(east, south) * step)`, and `_history.Do` a `ChangeEntry` from before to after.
2. In `KeyboardInput` in [Studio.cs](../Map.Studio/Studio.cs), call it for the four arrow keys. North is -Z, so Up is `Nudge(0f, -1f)`.

**Hints:** `Turn` in the same file is nearly the same method. Use `Do` here, not `Record`: the nudge hasn't been made yet, and `Do` makes it.

**Check it:**
- Select the yard's pallet (west of the crate stack), press Right three times, then Ctrl+Z once. It should end up two steps east of where it began, and the Map panel's "last:" should say what you'd undo next.
- Save, and look at [yard.district.json](../Maps/districts/yard.district.json) in git's diff: the pallet's line changed, nothing else. (Then undo back and save again, or `git checkout` the file.)

**Stretch:** holding the key down makes a dozen edits, and undoing them is tedious. Make nudges within half a second of each other **merge** into one edit: if the last edit in the history is a nudge of the same entry, replace it with one going from *its* before to the new after. (You'll need a way to replace the newest edit in `EditHistory`.) Text editors do the same with typing.

## Thinking questions

1. `RemoveEntry` undoes by inserting the entry back at its old index. Suppose it appended it to the end instead. Undo a removal, then undo the move made just before it. What goes wrong?
2. A drag records one edit when the mouse is let go. What would happen if, instead, the world were rebuilt every frame of the drag?
3. Picking tests the boxes of things in district files where the *file* says they are, but things built in code where their *bodies* are. Why the difference? When would the two disagree for a thing in a file?

<details>
<summary>Answers</summary>

1. The move's `ChangeEntry` remembers an index. With the removed entry appended, everything after its old place has shifted down by one, so that index now points at a different entry. Undoing the move would put the moved entry's "before" over the wrong one, quietly overwriting a neighbour. Undo must leave the list exactly as it was before the edit.
2. Every frame would take tens of milliseconds, a second or more whenever a pad moved, so dragging would stutter or crawl. And the bodies would be dropped and settled again each frame, so a stack of crates would jitter. Showing the change cheaply (markers and a ghost mesh) and building once at the end gets the best of both.
3. A file's entry *is* where you put it, and that's what you want to grab and move. A code thing has no entry, only its body, so the body is all there is. They disagree once a file's thing has settled somewhere other than where it was put: on a slope it can slide, and a stack set a little askew can topple. If physics ran, they'd drift further apart: push a crate in the playground, and the file still says where it *started*.

</details>

## Further reading

- *Game Programming Patterns* by Robert Nystrom: "Command", which builds undo exactly this way.
- *Design Patterns* (Gamma, Helm, Johnson, Vlissides): "Command" and "Memento".
- On picking: "Mouse picking with ray casting" (Anton Gerdelan's tutorial) goes through the same unprojection by hand, with the matrix maths shown.
