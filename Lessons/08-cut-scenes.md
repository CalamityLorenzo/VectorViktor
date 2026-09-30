# 08: Cut scenes: timelines and cameras

**What you'll learn:** what a cut scene is made of (things happening at set times on one clock); how a timeline drives animators without losing time between cues; how the picture cuts between a free cinematic camera and cameras that are part of the world; why "skip" should run the scene through rather than jump to its end; how to find a pose by searching instead of guessing; and how to test that something never jumps.

**Built in:** [AnimationPlan.md](../AnimationPlan.md) step 6: [Timeline.cs](../World.Core/Animation/Timeline.cs) (`Timeline`, `CameraTrack`), [FittingScene](../World.Core/Characters/FittingScene.cs), and the [FitArm](../Droid.Playground/Experiments/FitArm.cs) experiment. Lessons 04 and 07 come first.

Run it: `Droid.Playground.exe fitarm`. Watch it through, then press K during a replay ("restart it") to skip. Pause (P), step (N) and slow motion ([ and ]) work on it as on everything else.

## A timeline

A cut scene is a list of things that happen at set times, all on one clock:

- at 0 s, start the "present" clip on the droid
- when that clip passes "attach arm", hang the arm from the shoulder, and start easing it in
- at 1.9 s, fade the "present" clip out, so the stick arm drops back
- at 2.8 s, start the head camera looking; at 3.2 s, start the elbow flexing
- at 8 s, the arm's fitted: make its socket its rest, and stop the clips

A [Timeline](../World.Core/Animation/Timeline.cs) holds exactly that. `At(time, action)` is a **cue**: do this then. `Play` and `Stop` are cues that start and stop clips on an animator. `On(eventName, action)` runs whenever a clip passes that event (lesson 07). Cues are kept in time order, and those at the same time run in the order they were given.

The timeline **steps the animators it uses**. That keeps everything on one clock: the scene's time and the clips' times can't drift apart. So while the scene plays, the game must not step those animators itself. It still poses the rig from them each frame, as usual.

### Not losing time between cues

A step is 1/60 s. Say a clip is started by a cue at 1.005 s, and the step runs from 1.000 to 1.0167. If the cue ran first and then the animators stepped the whole 1/60 s, the clip would begin 5 ms ahead of where it should be. That's too small to see here, but it isn't in general: while this was being built, a test with a 1.2 s step had a clip's event firing 0.3 s early. So `Step` walks the animators up to each cue's time, runs the cue, and then walks on to the end of the step. A clip started partway through a step plays only the rest of it.

## Skipping

`Skip` could just set the time to the end. But then the "attach arm" handler would never run, and the arm would still be in the droid's hand, clips half-played, the end cue never reached. Everything that happens *along the way* would be lost.

So `Skip` runs the scene through to its end a tick at a time, as fast as it can (an 8 s scene is 480 ticks, over in an instant). Every cue, event and clip happens exactly as it would have, so the scene ends exactly as if it had been watched. The test `SkippedTheSceneEndsAsIfPlayed` checks that.

While the scene plays (`Playing`), the experiment's `Drive` gives the droid no input: the player's controls are paused.

## Cameras

The plan's question was whose camera a cut scene is seen through. Paul's answer: both kinds.

- **World cameras** are things that exist in the game: the droid's head camera on its visor rail, its drone, one day a mirror. Where they are is the game's business, worked out from the simulation each frame.
- The **free cinematic camera** isn't in the world at all. It goes where its keys put it: an eye position and a point to look at, each a `Channel<Vector3>` (lesson 04), eased between keys.

A [CameraTrack](../World.Core/Animation/Timeline.cs) is a list of **cuts**: from this time, the picture is from that camera. `CameraTrack.Free` names the free camera; any other name is a world camera. `ViewAt(time, world)` gives the view: the free camera's from its keys, a world camera's by asking the `world` function, which the game supplies:

```csharp
var view = _scene.Camera.ViewAt(_scene.Time, camera => camera switch
{
    FittingScene.HeadCamera => Head(session),
    FittingScene.DroneCamera => Drone(session),
    _ => null,
});
```

That split keeps World.Core knowing nothing about where a drone is drawn from. It also means a cut to the head camera shows what the head camera *really* sees, including where a clip has turned it. The scene's "look at it" clip runs the camera round the visor and tips it nearly straight down. That's Paul's point that the head can't always move, so the camera has to do the looking.

The free camera's keys are given round the droid (`At(1.7f, 1.3f, 2.3f)` is 1.7 m to its left, 1.3 m up, 2.3 m in front), then placed with the droid's `placement` matrix. So the scene works wherever the droid is standing and whichever way it faces.

The playground lets an experiment take over the picture with `Experiment.Camera`, which returns a view and which kind of camera it is. The kind matters: from the head camera, the camera's own mesh has to be left out, or the picture would be the inside of its lens.

## Finding a pose by searching

The droid's one arm is a straight stick. For the scene, it has to bring the new arm's servo to its left shoulder. Which rotation of the stick does that?

Rather than guess, a throwaway test tried them all: every combination of turns about three axes, in small steps, keeping the one that brought the servo nearest the socket. The first answer was bad news. The nearest it could get was 40 cm, because a straight stick pivoting on the right can't bring its tip to the middle of the chest. So the *grip* changed instead: the sporks hold the new arm by its own sporks, so it lies back along the stick with its servo up by the shoulders. Searched again, the best pose brought it within 6 cm. That's `FittingScene.Offering`, and `Held` is the grip.

A brute-force search over tens of thousands of poses takes a few seconds, and it gives an answer you can check, and also tells you when no answer exists. That's worth knowing before you spend an hour nudging numbers.

## Testing for jumps

"It mustn't jump" sounds hard to test, but it isn't. `TheNewArmMovesSmoothlyFromTheHandIntoItsSocket` plays the scene a tick at a time, as the game does (step the timeline, apply the animator, solve the rig). It records how far the new arm's root moves each tick and asserts the most it ever moved is under 2 cm, about 1.2 m/s. A reparent that got its maths wrong, or a seat clip without its fade, would jump several centimetres in one tick and fail. The same test checks the arm was let go of within 25 cm of its socket, and ends up in it.

## Exercise (optional): wave hello with the new arm

End the scene with the droid trying out its new arm by waving it.

1. In `FittingScene`, add `public static Clip HelloClip()`: a clip that raises the new arm's upper arm (`NewArm + "-upper"`) from `DroidRig.HangingLeft`, out to its left (look at `DroidRig.Wave` for how the stick arm is raised to its right, and mirror the turn about Z), holds it there while the forearm waggles side to side a few times, and lowers it again, about 2 s in all.
2. In `Build`, play it at 5.8 s, so it happens while the drone is watching.
3. Add its name to the clips the end cue stops.

**Check it:** run `Droid.Playground.exe fitarm` and watch the drone's shot at the end: the new arm, on the droid's left, should go up and wave. Then run `dotnet test World.Core.Tests --filter SceneTests`: the scene's tests must still pass.

**Stretch:** cut back to the free camera for the last second, with two keys of your own: one where the drone view leaves off, and one closer in. The free camera's keys go in time order with the others.

## Thinking questions

1. Why does the timeline step the animators, instead of the game stepping them as it would anyway?
2. The drone is a world camera. What would you see if the scene cut to it while the drone was somewhere odd (stuck behind a wall, say)? How could a scene make sure of its shot?
3. Pausing the playground pauses the scene. What in the design makes that free?

<details>
<summary>Answers</summary>

1. So there's one clock. If the game stepped them, a pause, a skip or a slowed tick in one and not the other would put the clips out of step with the cues, and a cue that starts a clip "at 3.2 s" would start it at some other point in the clip's own time.
2. Whatever the drone sees: the wall. A world camera is honest, so it can be badly placed. A scene can fly the drone to its spot first (a cue that sets where it goes), use the free camera where the shot matters, or check the drone can see the droid (`IGround.ClearLine`) and cut elsewhere if not.
3. The scene is stepped in the experiment's `AfterTick`, by the tick's `dt`, and ticks stop when the playground's paused (lesson 03). The scene doesn't know there's a pause at all.

</details>

## Further reading

- Unity's **Timeline** and Unreal's **Sequencer** documentation: the same ideas (tracks of clips, events, camera cuts) with an editor on top.
- *Game Programming Patterns* by Robert Nystrom, "Command" and "Event Queue": cues are commands kept for later.
