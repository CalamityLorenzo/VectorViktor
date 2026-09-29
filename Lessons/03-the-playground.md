# 03: The playground: fixed ticks, time control and experiments

**What you'll learn:** why a game's world moves in fixed ticks while it's drawn in frames, and how that gives pause, single-stepping and slow motion almost for free; how a test harness is built round "experiments" with hooks; how to model a Segway's balance and tune it by watching plots; and three tools any harness needs: extra cameras, overlays and picking with the mouse.

**Built in:** [ToolsPlan.md](../ToolsPlan.md) step 2, as [Droid.Playground](../Droid.Playground/Playground.cs).

Run it: `Droid.Playground\bin\Debug\net10.0-windows\Droid.Playground.exe segway`, or `drive`, optionally followed by a start name (`town`, `street`, `pond`, ...).

## Ticks and frames

A frame is drawn whenever the screen is ready: 60 times a second, or 144, or fewer when the machine is busy. If the world moved by "however long this frame took", it would behave differently on every machine. A jump would go higher at 30 frames a second than at 144, and a fast body could pass through a wall in one long frame.

So the world moves in **fixed ticks** of exactly 1/60 s, and frames are drawn in between. The time since the last frame goes into an **accumulator**, and whole ticks are taken out of it:

```csharp
_pending += MathF.Min(frameSeconds, MaxFrame) * Speeds[_speed].scale;
while (_pending >= StepTime)
{
    Tick(asked);
    _pending -= StepTime;
}
```

- **`MaxFrame`** (a quarter of a second) caps a stall. Otherwise, after the machine pauses for two seconds, the world would try to catch up 120 ticks at once, take longer doing it, fall further behind, and so on: the "spiral of death".
- **Slow motion** is the `scale`. At a quarter speed the accumulator fills four times slower, so there's a tick about every fourth frame, and each tick is exactly the tick it would be at full speed. The world behaves *the same*, just slower to watch.
- **Pause** empties the accumulator. **Step** (N) runs exactly one tick. Because ticks are fixed, a stepped tick is the same as a running one.

Ticks move the **simulation** (`Player`, `PhysicsWorld`, doors, the experiment). Frames **draw** it: the rig is posed from the simulation's state each frame (`DroidMotion.Pose`, `DroidRig.Look`, then `Rig.Solve`). That's the split kept since World.Core was made: the simulation knows nothing about drawing, so it can be tested without a window, and tools can draw it however they like.

## Experiments

Each thing being tried is a class derived from [Experiment](../Droid.Playground/Experiment.cs), with **hooks** the harness calls at fixed points:

| Hook | When | For |
|---|---|---|
| `Start` | picked, or restarted | resetting its state |
| `Drive` | each tick, before the world moves | turning what the player asks for into what the droid is told |
| `AfterTick` | each tick, after | reading what happened, recording traces |
| `Pose` | each frame | anything extra on the rig |
| `Panel` | each frame | its Dear ImGui panel: live settings and plots |

Every hook has a default that does nothing, so an experiment writes only what's different. [Drive](../Droid.Playground/Experiments/Drive.cs), the baseline, only records and plots. The harness owns everything shared (the world, the droid, time, cameras, overlays), and an experiment reaches the world only through a [Session](../Droid.Playground/Session.cs). This shape (a fixed skeleton calling overridable steps) is the **template method** pattern. Adding an experiment is one class and one line in `Experiments.All`.

## The Segway: a small control system

The design says the starting droid balances like a Segway: *it leans in to go, leans back to stop, and wobbles as it settles.* The walker does the opposite: it simply goes. The [Segway](../Droid.Playground/Experiments/Segway.cs) experiment models the lean as what *causes* the motion:

```
lean wanted = Gain × (speed asked for − speed now), limited to ±MaxLean
lean       is pulled towards the lean wanted by a spring (Stiffness), slowed by Damping
speed      changes by Thrust × lean
```

That's a **feedback loop**. Asked for 2.5 m/s from rest, the gap is big, so it wants a big lean. The lean swings towards it, and the lean makes it speed up. As the speed catches up, the gap shrinks, the wanted lean falls back to upright, and it cruises. Let go, and the same happens backwards: it leans back and stops.

The spring gives the wobble. With low `Damping` the lean overshoots upright and swings back a few times, like the real thing. Too little and it never settles; too much and it moves stiffly, like a robot on rails.

**Tune it with the plots.** The panel plots the speed asked for, the speed, and the lean over the last five seconds. Hold forward and let go, and you can see the lean lead and the speed follow; the overshoot and the settling are there to measure. Try:
- `damping` down to 2: a longer wobble. Up to 20: no wobble, a stiffer droid.
- `gain` up: it leans harder and gets there sooner, but overshoots more.
- `thrust` down: a heavy, lazy droid.

Before any code was written, the model was simulated in a few lines of Python to check that it settles with the defaults. Checking a model before building it is worth the few minutes.

The walker's controller still does the moving, so ground, slopes, walls and water all still work. The experiment only decides how fast it's *asked* to go. If the droid drives into a wall, `AfterTick` sees it didn't reach the speed it wanted and resets its own speed to match.

## The tools

- **Three cameras.**
  - **Head (F1)** sits at the head camera on its visor rail (`DroidRig.CameraView`), so it tilts as the droid leans. Tick "level horizon" to stop that.
  - **Drone (F2)** follows from behind.
  - **Free (F3)** flies anywhere: W A S D, Space and Ctrl, with the right mouse button held to look.
- **Overlays** are coloured lines ([DebugLines](../Droid.Playground/DebugLines.cs)) drawn after the world and depth-tested, so things in front hide them:
  - collision walls, yellow
  - bodies' boxes, cyan
  - the rig's joints as little axes: x red, y green, z blue
  - the walker's capsule, green. It's taller than the droid, which shows the droid still uses the walker's size.
- **Picking.** Ctrl+click drops the droid where you clicked:
  1. The mouse's point on the picture is turned back into a ray from the camera (`Viewport.Unproject`, the inverse of drawing).
  2. The ray is walked forward in 25 cm steps until it's under the terrain.
  3. The last step is halved twelve times (bisection) to find where it crossed.

## Exercise (optional): look where you're going

Write an experiment where the head camera looks round into each turn, the way you glance where you're turning before you turn.

1. Add `Droid.Playground/Experiments/LookAhead.cs`, a `sealed class LookAhead : Experiment` in namespace `Droid.Playground`, with `Name` `"lookahead"` and an `About`.
2. Keep the droid's yaw from the last tick. In `AfterTick`, work out how fast it's turning: `MathHelper.WrapAngle(yaw - lastYaw) / dt`, radians per second, turning right is more.
3. Keep an angle `_around`, and ease it each tick towards `-Lead * turnRate` (with `Lead` about 0.6): `_around += (target - _around) * 0.1f`. It's negative because `DroidRig.Look` counts round to the droid's *left* as more.
4. In `Pose`, call `DroidRig.Look(rig, _around)`. It runs after the harness's own Look, so yours wins.
5. In `Panel`, a slider for `Lead`, and the angle it's looking.
6. Add `("lookahead", () => new LookAhead())` to `Experiments.All`.

**Check it:** run `Droid.Playground lookahead`, switch to the drone view (F2) and turn with the arrow keys. The bright lens should slide round the visor into the turn and come back when you straighten up. Then try the head view (F1).

**Stretch:** make it look further round the faster it's going, since it matters more at speed. `session.Motion.Speed` is its speed.

## Thinking questions

1. Why does pausing empty the accumulator rather than just stop taking ticks out of it?
2. Slow motion scales the time put *into* the accumulator. What would go wrong if it scaled `StepTime` instead?
3. The Segway can't sidestep, and the walker can. Where in the code is that decided, and why is that the right place?

<details>
<summary>Answers</summary>

1. Otherwise time would pile up while paused, and unpausing would run every missed tick in one burst (up to `MaxFrame`'s worth), so the world would jump.
2. Smaller ticks change the simulation itself: the same physics with a different step gives slightly different results (a jump's height, when a body comes to rest). Slow motion should show the *same* behaviour more slowly, so ticks must stay 1/60 s.
3. In `Segway.Drive`, which returns a move with no sideways part (`Move.X` = 0). That's the experiment's job: turning what the player asks for into what this droid can do. The controller underneath stays general, and the next experiment (legs, tracks) makes its own choice.

</details>

## Further reading

- Glenn Fiedler, *Fix Your Timestep!*: the accumulator, and interpolating between ticks when drawing (not done here yet; see ArchitectureReviewPlan.md 5.2).
- Any introduction to **PID control**: the Segway's lean is a proportional controller with a spring; real balancing robots add the rest.
- *Game Programming Patterns* by Robert Nystrom (free online): "Game Loop", "Update Method" and "Template Method" chapters.
