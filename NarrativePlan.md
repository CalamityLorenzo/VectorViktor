# Conversations, choices and the story record: investigation and plan

Written 2026-10-03, and revised the same day after Paul's answer on video calls (section 2: calls to the operator come from outside the game's simulation, so they can be real video; a video spike is in section 11). A design and tech investigation, nothing built yet. It answers: what kinds of conversation the game has, how they're shown in the droid's broadcast, how they're written, how what was said and what was decided is kept, how choices change what the game does, and how conversations and scripted events drive each other. The first scene in `Story and scripts/Story setup.docx` is the worked example throughout (section 8). Questions for Paul are in section 10.

## 1. The short answer

- **Three ways a conversation reaches the player**, all from the same script: **lines over the picture while you play** (most of them; play goes on), **conversations that take over** (play pauses, lines wait for a key, there may be a choice), and **video calls**, a picture-in-picture window with the caller in it, which only ever come to the operator: characters in the world talking to the droid get lines, never a picture. Any of them can start, or wait for, a **scripted event**: a cut scene on the existing `Timeline`.
- **Write the words in ink** (inkle's scripting language, MIT, plain C# runtime), not in C# or JSON. It reads like the screenplay already in the Word doc, handles branching, conditions, "say this only once" and variables, and saves its own state as JSON. Inky, its editor, lets you play a conversation without the game. The game keeps everything else hand-rolled: how lines look, the events, the state the world acts on, and the record.
- **The record is the game's, not ink's**: a **transcript** (every line shown: who, what, when, where), a **decision log** (every choice: which, what was picked, when, where), and **story flags** (`route = left`, `met.mask`). All three go in the save, which is the first piece of the game state that GameDesign.md section 4 says the whole design turns on. Conversations are a good place to start it.
- **Choices change behaviour through flags and parts.** A conversation sets a flag (`route`) or gives a part (`~ give("leg-left")`), and the world reads the game state: a trigger checks the flag, the droid's components change, a door opens.
- **There are two layers, and they follow different rules.** *Inside* the droid's broadcast, everything is what the damaged droid makes of it: low resolution, colourless, details made up. Characters in the world talking to the droid belong here. *Outside* it is the operator's own desk: **video calls to the operator come from outside the simulation**, so they're **real video**, in colour, at the window's full resolution, drawn over the scaled-up picture. The difference between the two is itself part of the story.
- **Video works, but not through MonoGame's own player.** In a spike (section 11), MonoGame 3.8.5.1's `VideoPlayer` on WindowsDX never gave a single frame. Decoding the `.mp4` ourselves with Windows Media Foundation does work: about 6 ms a frame for a 1062 x 542 clip. It uses SharpDX.MediaFoundation, which MonoGame WindowsDX already brings in, so there's no new dependency.
- **What's missing today is mostly text.** There is no font beyond the compass's 3 x 5 digits, no HUD, no game state and no saving. Step 1 (section 9) is a bitmap font and a console in the low-resolution picture.

## 2. What's wanted

From your message:
1. Characters talk to the **operator** (the player) or to the **droid**.
2. **Text conversations** and **smaller video conversations**, shown to the player.
3. A **record** that each has happened.
4. **Decisions stored** for later.
5. **Narrative choices change how the game behaves**: going left or right, the droid getting a new body or a new leg.
6. Most are **text over the screen while you play**; some come with **scripted events**.

From the opening scene in the Word doc, the kinds of line actually used:

| In the scene | What it is | How it looks |
|---|---|---|
| "Physical Connection: Complete. Broadcasting IFF..." | the drone's machine messages | console, "super elite single console font", appearing at once |
| the 6502 listing for "Wake Up" | a block of code scrolling past | console, fast scroll, set dressing more than reading |
| "Diagnostics report processing. Memory: 30%..." | the droid's replies | **typewriter** font, letter by letter, with its sound |
| "Your legs have been replaced by a milk urn. [Drone]" / "That's suboptimal [Droid]" | a dialogue between two machines | speaker-tagged lines |
| "Droid report" shouts the drone | said aloud ("Ether broadcast") | quoted speech with a speaker, a different style again |
| Kensington Mask's briefing | the employer speaking to the operator | a **video call** |
| falls over face first, the cable pops out | a scripted event in the middle of the conversation | a cut scene, the conversation waiting for it |

So a line has a **speaker**, and the speaker (or the line) has a **style**: font, colour, how it's revealed, its sound, where on screen. The scene also shows the two voices GameDesign.md section 3.7 wants: the droid's, and command's (Mask), neither honest.

**Paul's answer (2026-10-03): inside and outside.** The game looks the way it does because the droid is damaged and can't process what it sees properly. Its camera is broken enough that it's **making up details as it goes**: a hangar seen in the window of a house. Video calls **to the operator** come from **outside** the game's simulation, so they aren't bound by those rules: they can be real video. That gives two layers:

| | Inside: the droid's broadcast | Outside: the operator's desk |
|---|---|---|
| What it is | what the droid sees, says and thinks it sees | what reaches the operator directly |
| Who's there | the droid, the drone, people and machines in the world talking to the droid | command (Kensington Mask), anyone calling the operator |
| How it looks | 480 x 270, hard pixels, colourless until restored, details invented | real video, colour, the window's full resolution |
| Text | the droid's console: pixel fonts in the low-resolution picture, **typewriter at first, improving to console as the droid is repaired** | the operator's own text: **crisp**, at full resolution |
| Can it be wrong? | yes: damage, memory, made-up details | yes, but by lying, not by damage |
| Drawn by | the world renderer, into the low-resolution target | `RetroGame.DrawOverlay`, after the picture is scaled up |

The contrast does story work. A crisp, colourful, confident Mask on video beside the droid's broken grey picture says at once who has the full picture, and who claims to.

**Paul's answer on text (2026-10-03).** The **operator's own text is crisp**: orders, call subtitles, the operator's choices, the comms log's frame, all drawn outside the broadcast at the window's resolution. The **droid's text starts at typewriter quality and improves towards console fonts as the droid is repaired**: how well it can write is one more thing the damage takes away and repairs give back (section 6, "The droid's text getting better"). The drone, undamaged, writes in the console font from the start, as in the opening scene, so at first its tidy console lines sit beside the droid's battered typing, and by the end the droid writes as well as the drone.

## 3. What the engine already has

| Need | What exists | Where |
|---|---|---|
| Scripted events | `Timeline`: cues at times, clips on rigs, clip events (`"attach arm"`), a camera track (world cameras and a free camera), `Skip` | `World.Core/Animation/Timeline.cs`, `FittingScene` as the example |
| Pausing play during a scene | `Timeline.Playing` (the player's input is left alone while it plays) | same |
| A live picture in a window | TV feeds: a named channel rendered from a camera into a render target each frame, static when there's no feed | `WorldRenderer.Feed`/`DrawFeeds`, `World.Maps/Screens.cs` (`StaticPicture`, `ScreenView`) |
| Drawing over the picture | `RetroGame.DrawOverlay` (window resolution); the compass draws hard pixels into the low-resolution picture with a 3 x 5 font | `MeshRendering/RetroGame.cs`, `MeshRendering/Compass.cs` |
| Saving data | `System.Text.Json` for the map files, with a watcher that reloads on save | `World.Maps/Files` (`MapJson`, `MapWatcher`) |
| Places | maps, districts, starts; the house (`StartHouse`) with the bedroom the scene is set in | `World.Maps`, `Maps.Home/StartHouse.cs` |
| The droid in its churn | `Locomotion.Churn`, `DroidRig`, the ruby visor, the ear dishes | `World.Core/Characters` |
| Interacting | facing a door and pressing E (`BuildingGround.Interact`, returns an `IOpenable`) | `World.Buildings` |

Not there: a real font, a HUD, any game state or saving, triggers in maps (areas that start something), sound of any kind (MonoGame has `SoundEffect`; nothing uses it), and talking characters other than the droid and drone.

## 4. The model: what a conversation is made of

These words are used through the rest of the plan:

- **Speaker**: who's talking. `drone`, `droid`, `mask`, `operator`. Each has a default **style**.
- **Style**: how a line looks and sounds. A font, a colour (palette slot), a reveal (at once, typewriter at N letters a second, scrolling), a sound per letter or per line, a place (the console strip, the call window, over a character's head), and a layer (inside the broadcast, or outside it). The scene needs about five: `console`, `code`, `typewriter`, `ether` (spoken aloud), `call`. The droid's own style isn't fixed: it's `typewriter` or `console` or something between, depending on its **text quality**, which comes from the game state (section 6).
- **Line**: one thing said. Speaker, text, style (the speaker's unless it says otherwise), and **tags**: events to fire, a wait, a camera cut.
- **Choice**: two or more options shown together. Each has an **id** (for the decision log) and its text.
- **Conversation**: a named piece of script (an ink *knot*): lines, choices, conditions, effects, in order. `opening`, `junction`, `mask.checkin`.
- **Mode**: **ambient** (play goes on; lines come and go by themselves, timed by their length) or **holding** (play pauses or the camera is taken; a line stays until a key, choices wait for a pick). A conversation says which; a video call is usually holding but needn't be.
- **Trigger**: what starts a conversation. Entering an area, interacting with something, a flag changing, a part fitted, a timer, the end of a cut scene, another conversation.
- **Effect**: what a conversation does to the game. Set a flag, give or take a part, start a cut scene (and wait for it, or not), open a door, start a call, end a call.
- **Director**: the one thing in charge of what's being said. It owns the queue (an ambient line never talks over a holding conversation; urgent lines can interrupt), the triggers, once-only rules and cooldowns for repeated remarks, and it writes every line and choice to the record.

## 5. Writing the words: the options

### A. In C#, like the clips and cut scenes
`FittingScene` style: `scene.Say(2.0f, "droid", "Suboptimal!")`. Type-checked and testable, one language. But prose in string literals is painful, branching is hand-written `if`s, and you'd be rewriting a Word doc into code. Fine for the odd line a cut scene owns, wrong for the story.

### B. Our own JSON, like the maps
Map Studio could edit it. But JSON is a poor format for writing prose (every line quoted and escaped), and branching or conditions in JSON become a home-made programming language with no editor.

### C. Our own screenplay format
A text file that reads like the Word doc: `Drone: That's suboptimal.`, `* Go left`, `{if has wheels}`. Nice to teach (a small parser) and very readable. But the moment it needs conditions, variables, "only the first time", "pick one at random", nested choices and saving the reader's place, it becomes ink without the editor or years of use. Worth it only if you'd rather build that as a lesson.

### D. ink (recommended)
inkle's narrative language (used in *80 Days*, *Heaven's Vault*, *Overboard!*). MIT licensed. The **runtime is plain C#** with no Unity in it (the Unity plugin is separate). **Inky** is a free editor that plays the script as you write it, and the compiler can also run inside the game, so a script could reload on save the way map files do (`MapWatcher`). It has what the game needs out of the box:
- choices, nested choices, choices that only appear if a condition holds, choices that disappear once taken;
- variables (`VAR route = ""`), conditions (`{route == "left": ...}`), visit counts (`{opening}` is how many times it's been seen);
- **sequences**, made for remarks while playing: `{once: ...}`, `{stopping: ...}`, `{shuffle: ...}`;
- **tags** on lines and choices (`#style:typewriter`, `#id:left`), which the game reads, so styles and ids need no extra syntax;
- **external functions**, so the script calls into the game (`~ give("leg-left")`, `{has("wheels")}`) and **variable observers**, so the game hears when a flag changes;
- its whole state (where it is, variables, visit counts) **saved and loaded as JSON**.

Two things it doesn't do that the game will: stable ids for each line (it has none built in: the transcript stores the text, and the decision log uses choice tags), and anything about how lines look or are timed (that's the game's job in any option).

How to add it: the runtime is a small folder of C# source (or a DLL from inkle's releases), and NuGet packaging should be checked at the time of building. The compiler is separate (`inklecate` on the command line, or the compiler library in the game).

### E. Yarn Spinner
Also MIT, also a C# core (the `YarnSpinner` and `YarnSpinner.Compiler` packages), with line ids built in and `<<commands>>` for events. Its documentation and tools are mostly written for Unity, it pulls in more dependencies, and its scripts read less like prose than ink's. A sound second choice if ink disappoints.

**Recommendation: D.** You're writing the story as prose in Word; ink is the nearest thing to that which still branches and saves, and Inky lets you play a conversation before any of the game exists. Everything that touches the world stays ours.

### Who owns which state
Two places that both hold state can go wrong, so one rule: **anything the world acts on lives in the game state** (parts fitted, doors open, colour restored, `route`), and the script asks for it through external functions or changes it through effects. **Anything only conversations care about** (has the droid apologised yet, which joke was used) can be an ink variable. ink's own state is saved as a blob inside the game's save.

## 6. Showing it in the droid's broadcast

The picture is 480 x 270, scaled up with hard pixels, and GameDesign.md says the HUD *is* the droid's console. So text is drawn **into the low-resolution picture**, not over the window at full resolution, and it's pixelly too.

- **Fonts inside the broadcast.** Pixel bitmap fonts drawn into the low-resolution picture (an 8 x 8 font gives 60 columns x 33 rows), which suit the C64 and 6502 theme the story already has. Two glyph sets to start: **console** (blocky, even, for the drone and system, and for the droid once repaired) and **typewriter** (serifed and uneven, perhaps a little bigger, 8 x 10 or so, for the damaged droid). Either draw our own glyphs in code (as the compass's 3 x 5 digits are, and a good lesson) or start from a free font such as *Unscii* (public domain, viznut), checking the licence when picked. One texture per glyph set, drawn with `SpriteBatch`, no content pipeline needed.
- **The operator's font, outside.** Crisp text at the window's own resolution, which changes (a window, F11 full screen, different monitors), so a font baked at one size would blur when scaled. Better to rasterise a TrueType font at runtime at the size the window needs: **FontStashSharp** (MIT, with a MonoGame package) does that and caches glyphs in a texture. With a free font under the SIL Open Font License (a clean sans such as Inter, or a mono such as IBM Plex Mono), it looks like modern software: the operator's desk, not the droid. MonoGame's own `SpriteFont` would also work at a fixed size or two, through the content builder. (Dear ImGui draws crisp text already, but it's the tools' UI, not the game's.)
- **The console strip.** Lines arrive in a strip along the bottom (or top) of the picture, newest at the bottom, older ones scrolling up and fading. Each line shows its speaker's mark (`DRONE>`, `[droid]`), is revealed in its style, and stays for about 1.5 s plus 0.05 s a letter when ambient. A key finishes the line being typed, or moves on a holding conversation.
- **Choices.** Shown as numbered options in the console (`1> Go left   2> Go right`), picked with the number keys or the pad. Holding conversations wait for a pick; ambient ones could offer a choice that times out to a default (question 3).
- **Video calls to the operator** are outside the broadcast, so they're drawn **after** the picture is scaled up (`RetroGame.DrawOverlay`), at the window's own resolution: a window over a corner of the picture (or larger, the game paused, for a briefing), with the caller's name. On connecting, a moment of the *operator's* connection noise (a clean modern "connecting", not the droid's static), which also covers the decoder's first-frame warm-up (section 11). The caller's words are subtitled from the script, in the operator's own font. See section 7.
- **Characters speaking to the droid** are inside the broadcast, and are **never shown in a picture** (Paul, 2026-10-03): their words are lines in the droid's console, or over them in the world. Only calls to the operator, from outside, have a picture. See section 7.
- **Over a character.** For people and machines in the world speaking aloud (the drone's "Droid report"), a short line over them, placed by projecting their head into the picture. Later, and only if wanted.
- **Sound.** The typewriter's tick, the console's chirp, the drone's beeps, a buzz of static. None exists yet: this would be the first `SoundEffect` use (the roadmap's milestone 7 sound). Per-speaker "babble" (a pitched blip per letter, as in *Animal Crossing*) gives voices without actors.
- **The droid's text getting better.** One value in the game state, the droid's **text quality**, set by its repairs, decides how its lines look, are revealed and sound. Suggested stages (how many there are, and which repairs move it on, are question 11):

  | Stage | Looks like | Reveal and sound |
  |---|---|---|
  | 0. Broken typewriter (the start) | typewriter glyphs struck unevenly: letters a pixel high or low, some faint, some over-inked; now and then a wrong letter, then a backspace and the right one struck over it | slow, letter by letter, uneven rhythm, clack per letter, a ding and carriage return per line |
  | 1. Typewriter | the same glyphs, steadier, rarer slips | steadier, a little faster |
  | 2. Teleprinter | even, monospaced, no slips | fast, a softer chatter |
  | 3. Dot matrix | console glyphs built of visible dots | very fast |
  | 4. Console (fully repaired) | the clean console glyphs the drone uses | at once or a quick scroll, a chirp per line |

  Underneath it's a handful of numbers per stage (which glyph set, jitter, ink variation, slip rate, letters a second, sound), so stages can be tuned, or slid between, in the playground. Two rules keep it fair. **It stays readable**: a slip is always corrected on screen, never left wrong (unless the script means it to be: the unreliable narrator). And **it doesn't shimmer**: each letter's wobble comes from a random seeded by the line and the letter's place, so a line looks the same every frame and every time it's shown again.

  The stage changes how the droid's text *looks*. Whether it also changes what the droid *says* (terser, more garbled) is a script decision (Paul, 2026-10-03): the script can ask for the stage (`text_quality()`) and write lines for it where the writer wants to.
- **The log.** A key opens the **comms log**: the transcript, newest last, filterable by speaker, with each decision marked. It's an in-world thing (the droid's memory, the operator's comms record), so later it could be unreliable (section 7). The log itself is the operator's (crisp, outside), and shows each of the droid's lines **at the quality it arrived at**: reading back to the start shows how broken the droid was.

## 7. The pieces, worked through

### Text over the screen while you play
A trigger (an area in a map file, an interaction, a flag) asks the director to run a conversation in ambient mode. The director queues its lines; the console shows them; play goes on. ink's sequences keep repeats fresh: entering water the third time doesn't say the same thing as the first. Once-only and cooldowns live in the director, so a remark doesn't fire every frame the droid stands in a trigger.

### Conversations that take over
The director pauses play (or a `Timeline` has the camera), lines wait for a key, choices wait for a pick. When it ends, play resumes where it was.

### Choices that change behaviour
Each choice carries an id tag. Picking one: the director writes `{id, picked, text, when, where}` to the decision log, then ink carries on, and the option's effects run. What the world does with it, three examples:

```ink
=== junction ===
Droid: Two ways. The left one smells of oil. Probably.
* [Left] #id:junction.left
    ~ set("route", "left")
* [Right] #id:junction.right
    ~ set("route", "right")
- Droid: Rolling.
```
The world reads `route`: a trigger on the right-hand path only fires if `route == "right"`, the bridge on the left has collapsed if `route == "left"`, and so on.

```ink
=== scrapyard.leg ===
Droid: A leg. A real one. Hardly used.
* {not has("leg-left")} [Take it] #id:scrapyard.take-leg
    ~ give("leg-left")
    ~ play("fit-leg")
    Droid: Better. Considerably less suboptimal.
* [Leave it] #id:scrapyard.leave-leg
    Droid: Your loss. Well. Mine.
```
`give` changes the droid's components in the game state (GameDesign 3.1: what's fitted changes how it moves); `play` runs a cut scene like `FittingScene` and the conversation waits for it to finish before its next line.

A body works the same way (`give("body-dustbin")`), with the game state deciding what follows: more room to carry things, a different height, a gait.

### Scripted events, both ways
- **Script to event**: `~ play("name")` starts a named cut scene and holds the next line until it finishes; `~ start("name")` starts it without waiting; a tag `#event:zap` fires a one-off effect (a flash, a sound, the visor lighting up) as the line appears. The game keeps a registry of named scenes, as the playground keeps its experiments.
- **Event to script**: a `Timeline` gets `scene.Say(at, conversation)` (a cue that asks the director to run lines at a time in the scene), and a clip event can start a conversation the same way.

### Video calls to the operator (outside)
A call is a **video file** (H.264 `.mp4`) plus **its lines in the script**. The video is the picture and the voice. The ink lines carry the words, each with a time tag, so the subtitles, the transcript and any choice all come from the same script as everything else:
```ink
=== mask.briefing ===
~ call("mask", "mask-briefing-01")               // Videos/mask-briefing-01.mp4
Mask: Operator! I'm Kensington Mask, In External InHouse Droid Recovery Services. #at:0.4
Mask: Have we got a job for you? The answer is yes. #at:4.1
...
Mask: Cease your noisy dawdling and get me a droid home. #at:41.0
~ hangup()
```
- **Playing it**: our own reader on Media Foundation decodes frames into a `Texture2D` as the call's clock passes each frame's time (section 11), and the game draws it in the overlay. No content pipeline is needed: the `.mp4` is read straight from a `Videos/` folder copied beside the game, as `Maps/` is.
- **Sound**: the video's own audio track. Not tried yet: the same reader can hand out its audio as PCM for a MonoGame `DynamicSoundEffectInstance`, or each call can have a separate `.wav`. This would be the game's first sound.
- **Choices in a call**: video can't branch by itself, so a choice falls at the end of a clip. While the operator picks, the caller waits (a short idle loop, or the last frame held), and each answer plays its own clip. Each branch is another clip to make, which argues for few choices per call, and calls that react with a short clip before carrying on.
- **Recorded calls** replay by playing the file again: the log keeps which call, and when.
- **Making them** (filmed, animated, rendered, generated) is outside the code. All the game asks is an `.mp4` and the lines' times. A stand-in for testing can be any clip.

### Characters talking to the droid (inside)
These are inside the broadcast and obey its rules: the droid may get them wrong, garble them, or invent details. **They're never shown as a picture** (Paul, 2026-10-03): no call window, no portrait, only their lines, in the console or over the speaker in the world, in the droid's text. So a picture with someone talking in it always means the operator's desk, from outside. The rule is clean, and it keeps the two layers from blurring. (The live set-and-feed approach once suggested for them is dropped. The TV feeds stay what they are: screens in the world.)

### The camera making things up
Paul's hangar in a house's window is the same machinery as everything else here. A window is already a portal that can show another map (`Portal.ToMap`, the house's tinted windows), so "what the droid sees through this window" is a choice the game makes, and can change with game state. That makes it something the script can call for: `~ set("house.window.view", "hangar")`. The record can keep what the droid *claimed* to see beside what the drone, or a mirror, showed.

### The record
- **Transcript**: every line shown, `{time played, date, place (map.district), conversation, speaker, style, text}`. The text is stored, not just a reference, so the log reads correctly even after the script changes.
- **Decision log**: every choice made, `{id, picked, text, time, place}`. Code asks it questions (`decided("junction.left")`); so can the script, through an external function.
- **Seen**: which conversations have run (ink's visit counts cover its own knots; the director's once-only rules need their own set).
- **Story flags**: the game-state values that conversations set and the world reads.

One file per save, JSON, as the maps are:
```json
{
  "version": 1,
  "playTime": 1234.5,
  "place": "house.bedroom1",
  "flags": { "route": "left", "met.mask": true },
  "parts": [ "head", "broom", "arm-stick", "wheels" ],
  "decisions": [ { "id": "junction.left", "text": "Left", "time": 812.0, "place": "coast.station" } ],
  "transcript": [ { "time": 3.1, "place": "house.bedroom1", "speaker": "drone", "style": "console", "text": "Physical Connection: Complete." },
                  { "time": 9.6, "place": "house.bedroom1", "speaker": "droid", "style": "droid", "quality": 0, "text": "Diagnostics report processing." } ],
  "ink": "{ ...ink's own state... }"
}
```
A transcript of a whole game is a few thousand lines: small.

**Ideas the record makes possible** (not decisions; GameDesign 3.7 is the reason they're tempting):
- The transcript as **evidence**: what Mask said on day one, shown beside what the droid saw later.
- The droid's memory is **unreliable**: a log entry can later be shown corrupted, or "corrected" after a memory repair, the stored original kept so the game can show both.
- **Recorded calls replay** by re-running the call's script on its set: no video stored, only the conversation name and the state it ran with.

## 8. The opening scene, worked through

Set in `StartHouse`'s bedroom 1 (its window is the open gap the drone comes through). What would be C# and what would be ink:

| Beat | Done by |
|---|---|
| Drone buzzes in through the window, round the bedroom | a `Timeline`: the drone's path, the free camera, then the drone's own camera (`opening.arrive`) |
| Drone blinks, beeps, sends its cable into the droid's head | the same timeline: a cable (`Cable`/`Rig.Span`) eased out, a clip event `"plugged in"` that starts the conversation |
| Console messages, the 6502 listing | ink lines, styles `console` and `code` |
| "Sharing charge", fizz, blue flash | `#event:zap` (a flash, a sound) |
| The ruby band brightens, the camera indicator whirls | `~ play("opening.wake")`: the visor's palette slot brought up, the head camera run round the rail |
| Diagnostics, the droid and drone talking | ink lines, `typewriter` for the droid, `console` for the drone |
| Falls face first, the cable pops out, parts on the floor | `~ play("opening.fall")`: the droid tipped out of its churn, the cable let go, the parts dropped as bodies |
| "Droid report" shouted, the Ether broadcast | ink lines in style `ether` |
| Kensington Mask's briefing | `~ call("mask", "mask-briefing-01")`: a video call to the operator, from outside the broadcast, at full resolution, subtitled from his timed lines; `~ hangup()` |
| The tutorial begins | the conversation ends; `~ set("opening.done", true)`; the first tutorial trigger is live |

In ink, the start of it:
```ink
=== opening ===
~ play("opening.arrive")
Drone: Physical Connection: Complete. #style:console
Drone: Broadcasting IFF..... No response.
Drone: Retry IFF. No response.
Drone: Sharing charge. #event:zap
Droid: IFF Response. Droid xClassifiedxNoneOfYourBeesWaxx
Drone: Sending security protocols.....
Drone: Droid security agent..... is a dick.
Drone: Reading droid security agent a bedtime story.
Drone: Acquiring write access to 6502 core.
>>> listing wake-up                               // the code block, scrolled past
~ play("opening.wake")
Droid: Diagnostics report processing. #style:typewriter
Droid: Memory: 30%
Droid: CPU: 85%
...
Drone: Your legs have been replaced by a milk urn.
Droid: That's suboptimal.
...
Droid: A-HA! I'm reporting a slight increase in my locomotion abilities. Let's get moving.
~ play("opening.fall")
Drone: Droid report! #style:ether
...
-> mask.briefing                                  // the video call, section 7
~ set("opening.done", true)
-> END
```
`Speaker: text` at the start of a line is the game's own convention (split at the first colon); ink just passes it through. `>>> listing wake-up` stands for however the code block gets in (a tag naming a text file is simplest).

There are no choices in this scene as written. One natural place for the first one is answering Mask ("Yes" / "What's in it for me?"), which goes nowhere at first but starts the decision log, and the operator's attitude to command, from the first minute.

## 9. Steps

Each step is something to see working, in the playground first.

### Step 1: text in the picture
Inside: the two pixel glyph sets (console and typewriter), and a console that shows styled lines (at once, typewriter, scroll) and fades old ones, with the droid's text-quality stages (jitter, ink, slips struck over, reveal speed) as numbers to tune. Outside: FontStashSharp and an OFL font drawn in the overlay at the window's resolution. Playground experiment `console`: type lines in a panel, pick a speaker, drag the droid's text quality from broken typewriter to console and watch the same line change. *Lesson: bitmap fonts and text in a low-resolution picture, and text that isn't.*

### Step 2: the story library and the director
*Partly done 2026-10-03 (the ink demo):* ink v1.2.1's runtime and compiler are in `ThirdParty/Ink` (inkle's source, unchanged, MIT; their NuGet package stopped at 0.7.4). `World.Story` has `Script` (compiles in the game, lists the conversations), `StoryRunner` (lines with speakers and styles, cues, choices with `#id:` tags, the external functions in `Story and scripts/game.ink`), and `StoryRecord` (flags, parts, transcript, decisions, ink's state; JSON save and load). `World.Story.Tests` has 17 tests on the real script. Playground experiment `talk` plays `Story and scripts/opening.ink` in a Dear ImGui window, reloading it on save, revealing lines at their style's pace (the droid's by its text quality, with slips struck over at the lowest), pretending cut scenes, and showing the record. Each voice has its own face, Windows' own fonts standing in for step 1's glyph sets: Consolas for the drone and console, Segoe UI for people (operator, Mask, the choices), Courier New for the droid (struck unevenly at qualities 0 and 1: letters high or low, faint or over-inked, slips left faintly under the letter struck over them), and Consolas once it reaches dot matrix. Not done yet: the director (queue, ambient and holding modes, once-only, cooldowns). Gotchas: a choice's tag goes *inside* its brackets (`* [Left #id:junction.left]`), or it tags the line after; `#` and `*` at the start of text need a backslash.

A new library, `World.Story` (no graphics, testable like `World.Core`): ink's runtime added; `Line`, `Choice`, `Speaker`, `Style`; a runner that turns ink output into lines (speaker convention, tags) and picks choices; the director's queue, ambient and holding modes, once-only and cooldowns. Tests drive scripts without a window. Playground experiment `talk`: run a `.ink` file, reloading on save. *Lesson: a narrative script, and why ink.*

### Step 3: the game state and the record
`GameState`: flags, parts, the transcript, the decision log, seen conversations, ink's state; saved and loaded as JSON (a save folder, one file, autosave at the end of each conversation). External functions `set`, `get`, `has`, `give`, `take`, `decided`, `text_quality`. The droid's text quality worked out from its parts, so fitting the right repair moves its writing on a stage. This is GameDesign section 5's first step, started from the story end. *Lesson: one source of truth.*

### Step 4: choices and the comms log
Choices in the console, picked by number or pad; holding conversations pause play; the log viewer. Tests: a choice lands in the decision log, a flag changes what the next conversation says.

### Step 5: triggers and scripted events
Trigger areas in map files (and drawn and placed in Map Studio), interaction triggers, flag triggers; `play`/`start`/`#event` from the script to named `Timeline`s, `Say` cues from a `Timeline` to the script. The `fitarm` scene reworked as conversation + cut scene as the first example.

### Step 6: video calls to the operator
The spike's reader (section 11) made proper: decoding on a background thread a few frames ahead, the first frame read while the "connecting" shows, audio (or a `.wav` beside it), pause and resume, and disposal. The call window in the overlay, subtitles from the timed ink lines, `call`/`hangup`, the idle loop while a choice waits. First caller: Kensington Mask, with any clip as a stand-in. *Lesson: decoding video into a texture.*

### Step 7: the opening scene, end to end
Section 8 built in the house's bedroom: the drone's arrival, the conversation, the fall, the call, handing over to the tutorial. Needs the cut scenes above and leans on AnimationPlan step 2 (the droid as the player) for the hand-over.

### Later
Sound (the typewriter, chirps, babble, static), lines over speakers in the world, the unreliable log, replaying recorded calls.

## 10. Questions for Paul

1. **Writing**: are you happy writing in ink (in Inky, or any text editor), or would you rather keep writing in Word and have the scenes turned into scripts? Or build our own format as a lesson (option C)?
2. ~~**Video calls**: live, portraits or real video?~~ *Answered 2026-10-03: calls to the operator come from outside the simulation and can be real video (section 2). The operator's own text is crisp; the droid's starts as typewriter and improves to console as it's repaired. Characters inside the world who talk to the droid are never shown as a picture: lines only.*
3. **Choices while playing**: do all choices stop play, or can some come up during play and time out to a default (a call while you're driving)?
4. **Who speaks for the player?** Are choices always the **operator's** words (to the droid, to Mask), or can the operator also choose what the **droid** says or does? And can the operator ever type, or only pick?
5. **The log**: should the player be able to read the transcript in the game, and do you like the idea of it being unreliable (the droid's memory changing it)?
6. **Big choices**: should a choice that matters say so ("Mask will remember that"), or should the player find out later?
7. **Voices**: the video calls bring recorded voices. Inside the broadcast, per-letter sounds and babble only, or recorded voices there too? (There's no sound in the game yet.)
8. **Saving**: one save, autosaving at the end of conversations and on leaving a map, or save slots?
9. **Fonts**: draw our own pixel glyphs for the typewriter and console sets (a lesson, and they're ours), or start from free ones? And for the operator's crisp font, a clean sans or a monospace?
10. **Where the console sits**: along the bottom, the top, or a side, given the compass tape is at the top?
11. **The droid's text improving**: which repair moves it on? The diagnostics in the opening scene list a "Vocal Processor: 0%", which would be a natural part to find; or memory, or the CPU, or every repair a little. Does it go in a few clear stages (each a moment the player notices), or creep up gradually? *Partly answered 2026-10-03: whether the droid also says less well is a script decision, line by line. The engine gives the script the stage (`~ temp q = text_quality()`, or `{text_quality() < 2: ... - else: ...}`), and the writer decides what changes.*

## 11. The video spike (2026-10-03)

A throwaway program in the scratchpad (not in the repo): a 960 x 540 window, a 480 x 270 "broadcast" scaled up with hard pixels, and a video drawn over a corner at the window's own resolution, frames saved at set times. Test clip: a 1062 x 542, 12 fps, 5-second H.264 `.mp4` already on the machine (an ASUS wallpaper preview, used only as test input).

**MonoGame's own video doesn't work here.** MonoGame 3.8.5.1 WindowsDX has `Video`, `VideoPlayer` (on Media Foundation, through SharpDX), and `.mp4`/`.wmv` importers in the content builder. The clip built (the `.xnb` is a few bytes of size and length; the `.mp4` is copied beside it), loaded and "played": `State` was `Playing` and `PlayPosition` advanced. But every `VideoPlayer.GetTexture()` waited about 530 ms and then threw "Platform returned a null texture". Not one frame arrived, with this clip or a 4500 x 3000 one.

**Our own decoder does.** About 80 lines on SharpDX.MediaFoundation's `SourceReader`, which MonoGame WindowsDX already depends on:
1. `MediaManager.Startup()`; open a `SourceReader` on the file with `SourceReaderAttributeKeys.EnableVideoProcessing` set, so Media Foundation converts H.264's YUV to RGB.
2. Select only the first video stream, and ask for `VideoFormatGuids.Rgb32` output; read the frame size from the current media type.
3. Each frame: `ReadSample` gives a sample and its time (100 ns units). When the call's clock passes that time, copy the rows into a byte array, set every fourth byte to 255 (RGB32's fourth byte isn't alpha), and `SetData` into a `Texture2D` of `SurfaceFormat.Bgra32`.

Results, Release build: 60 frames of 60 decoded on time and shown; **about 6 ms to decode a frame** (the first one 30 to 130 ms while the decoder warms up); decoding and uploading averaged about 2 ms a drawn frame at 60 Hz. The saved frames are clean.

**Gotchas found:**
- **The row stride isn't width x 4, and isn't length / height.** The decoder pads to 16 pixels (this clip came out 1072 x 544 inside), and it says so by changing the media type on the first sample (`SourceReaderFlags.Currentmediatypechanged`). Read `MediaTypeAttributeKeys.DefaultStride` from the current type then, and again whenever that flag comes. (Dividing the length by the height gave 4303 and a skewed picture; the real stride was 4288.) A negative stride means bottom-up rows.
- **The first frame is slow**, so open and read it while the "connecting" shows, and decode on a background thread a few frames ahead in the real thing.
- **SharpDX is no longer maintained** (its last release was 2019), but it's what MonoGame WindowsDX itself is built on. If MonoGame ever moves off it, the same calls exist in Vortice.MediaFoundation, or FFmpeg could decode instead.
- **Audio isn't tried yet** (step 6).
