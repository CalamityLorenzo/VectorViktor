# Game design and feature ideas

Design notes, not plans. Nothing here is to be built yet, and most of it isn't fleshed out. Section 1 is the game as you described it; the rest turns it into feature areas. "Today" notes say what the code already does, so an idea's real size can be judged; they were checked against the code on 2026-09-26. Questions for you are in section 6.

## 1. The game

You operate a droid: a robot, an artificial thing. **It is damaged.** You see the world through it, and all it can broadcast is this low-polygon, low-resolution picture. **The world has no colour.**

The droid starts off as little more than a head on a broom handle, shaped like an urn on a stick. The head is a fat cylinder with a ruby-coloured band all the way round it where eyes would be; the urn's two handles are its ears, each with a little radar dish in it that moves about at random, sampling the air. The broom handle balances on a pair of hoverboard wheels (so no stairs), with wires running from the head down to the wheels as its spinal cord. Its one arm is another stick of wood with two forks for a hand. It is not humanoid, and moves like a Segway.

You will be able to replace arms, add different types of locomotion (tracks, legs, more wheels), long arms, strong arms, new body like a dustbin, a cupboard etc. How many implements the droid can carry depends on how big a body it has. The head, spine and drone all part of the droid. The head camera sits inside the ruby visor, which runs all the way round the head like a rail, and can run round it and turn within it independently of the head itself: if the head is lying on the floor without a body and you need a better view of something, you just turn the camera. It has several features, including zoom, 'identify' and recording. This camera and its view is your view on the world. **Mirrors** are how you see the droid itself, and watch it get better as it's repaired. But the droid's own memories can influence it, making it an unreliable narrator in places.

Over the whole game you **slowly repair the droid** (each repair gives it more it can do, and so more options) and you **put the missing colour back into the world**. 

There are **several worlds in the same solar system** to travel between.

It is a **puzzle adventure**: find X to do Y. There is **action, and violence**, too. There is a **narrative**, which unfolds as the robot recovers.

**The story.** You, the player, are the **operator**. You have completed some training (the tutorial) and have been given command of this droid. It went missing, and has recently begun broadcasting again. Your orders are to gain control of it and get it off the planet to a **satellite orbiting a different planet** (not the space station): the planet it was meant to be on. But the droid is on the **wrong planet** in the solar system. How did it get there? And how do you get it back? **Your orders, and what your droid remembers, tells you and sees, are not always honest.** Reaching the satellite is *one* way to end the game, but probably not the best one.

It is **not a linear run** through the worlds: you are **free to roam**, and can go back to earlier places with new abilities.

Everything else in this document either follows from that or is a way of making it feel true.

### How the premise shapes what the player sees
- **The picture is the droid's broadcast.** The low-resolution render, hard pixels, letterbox and wireframe are the droid's limits, not the game's. Colourless, white edges on a plain background, is what the world looks like *to it*, and is the state everything starts in (the wireframe view the game is meant to be mainly played in).
- **The HUD is the droid's own console**, and its messages are how the narrative arrives: what it notices, what it can't do yet, what it remembers. Your **orders** arrive on it too, so it carries two voices, the droid's and command's, and neither is always honest (see 3.7). Today the picture has no text at all (the window title shows how wet you are), so there is no HUD yet.
- **Being damaged is felt.** Early on it is slow and limited, the picture may glitch or drop out, and things it can't do yet are simply not there (no jump, no stairs, one crude arm, no drone, a short link). Repairs remove those limits one at a time.
- **The broadcast could improve as it's repaired** (see question 2): colour first, and perhaps also resolution, how far it can see, or how much detail comes through. This is an idea, not a decision.
- **Mirrors are how the droid sees itself.** Its view is from its own head, so it never sees its body except in a mirror (or from the drone, once it has one). Mirrors placed through the worlds let the player see what the droid has become, and each repair shows: a new arm, new wheels, a new body.
- **Controls with weight**: each way of moving (legs, wheels, tracks) accelerates, turns and stops differently, and the operator's inputs go through the machine. The starting hoverboard wheels balance like a Segway: the droid leans into moving off, leans back to stop, turns on the spot and wobbles as it settles. Heavy lag is less fun, so this wants restraint.
- **Waking and sleeping**: a session starting as the droid coming on, damaged, and ending as the link closing, costs almost nothing and sets the frame. It is also the story's opening: a missing droid broadcasting again, and a newly trained operator taking command of it.

## 2. The loop, and what it needs

1. **Explore** a world, colourless.
2. **Find** things and places (X) that let the droid do something (Y): a part to repair, a key, a tool, a colour, a way through.
3. **Repair and restore**: the droid gets a new ability; and colour comes back to part of the world.
4. **Overcome** the things in the way: puzzles, and enemies (action, violence).
5. **Travel** to another world, when the droid can, and the story moves on. Nothing forces an order: the droid can go where it is able to, and come back to earlier worlds with abilities that open what was closed before.

Free roaming shapes several things. What stops the droid getting somewhere too early is what it can do (its repairs, its power), not a door that opens only in sequence, so the game is built from *abilities and state* rather than *levels*. That means puzzles that can be met in any order, a narrative whose beats are gated by what has been done and not by where the player has got to, and worlds that keep their state (what's been restored, moved or opened) while the droid is away.

What that needs that the game doesn't have today, in one place (see 4): a game state (what's repaired, what's restored, what's been found, where the story is), a way to interact with more than doors, enemies and a way to fight, several worlds and the way between them, and a way to say the narrative.

## 3. Feature areas

### 3.1 The droid: components and repairs
- **The starting droid** is not humanoid; its outline is an urn on a broom handle:
  - **Head**: a fat cylinder about as tall as it is wide, its top and bottom edges rounded, on a short narrower neck, with a ruby-coloured visor running all the way round its upper part where the eyes would be. The head camera runs round inside the visor like a rail (see 3.2). Paul's front-view sketch of it is the basis of `DroidRig`'s sizes (see AnimationPlan.md 7).
  - **Ears**: two hoops on the sides of the head, like an urn's handles (face on from the front), level with its lower middle, each holding a little radar dish that moves around at random, sampling the air.
  - **Spine**: a broom handle, with wires running from the head down to the wheels.
  - **Arm**: another stick of wood, with two forks for a hand.
  - **Wheels**: a pair of hoverboard wheels, self-balancing like a Segway. On two wheels it can't climb stairs, only ramps and low kerbs.
  - **Before the wheels, a milk churn** (Paul, 2026-10-03): it starts with the broom stood in a milk churn just over a third its height, leaning against the rim as if it might tip over. In the churn it can't move, roll, slide, turn or jump; only the head camera looks about. The wheels are its first way of getting about (`Locomotion.Churn`).
- **Replaceable components**: arms, legs and body, each changing height, speed and how it moves; they are the repairs, and what's missing is what limits it.
- **The body is what it carries in**: how many implements the droid can carry depends on how big a body it has, so a dustbin or a cupboard is more room as well as a new shape. The starting droid has no body at all, so it can hold only what's in its fork hand; a bigger body could also be a trade-off, carrying more at the cost of speed or of fitting through gaps.
- **Ways of moving**: the hoverboard wheels it starts on (self-balancing, like a Segway), legs, more wheels, tractor tracks, and stranger ones such as strapped on top of a toy. Different ones suit different worlds and puzzles.
- **Abilities gate options**: a repair opens what the droid can reach, carry, open, climb or fight with. Arms are what let it hold an implement, so a weapon needs them (see 3.6).
- Today: one humanoid walker (`CharacterController`), drawn as a seven-box mesh (`PlayerMesh`) that darkens when wet. The starting droid would change it in three ways:
  - **Its shape.** The walker is a 1.8 m capsule of 0.3 m radius with its eye at 1.6 m, all constants in `WorldConstants`. The droid needs its own sizes, and since components change them, they become values of the droid rather than constants. A two-wheeled base is wide from side to side and short from front to back, which a round capsule only approximates.
  - **Its movement.** The walker reaches the speed asked for almost at once (`GroundAcceleration`), steps up 0.3 m (`MaxStepUp`, enough for stairs), jumps and swims. Segway movement means a slower lean-in and lean-back, a visible tilt and wobble, no jump, and a step-up of only a kerb's height; all of those belong to the locomotion component.
  - **Its mesh.** A droid made of parts (head, visor, ears and dishes, broom, wheels, arm, wires) is best built as a mesh per part, so a component can be swapped by swapping its mesh, and the lean can tilt the parts above the wheels. The ear dishes are the first parts of the player that move by themselves; a wandering aim that is a smooth function of time would do it, the way `ScenePart` moves the hangar's turntables.

### 3.2 The cameras: the head camera and the drone
- **The head camera** is the player's view. It sits inside the ruby visor, which runs all the way round the head, and runs round it like a rail: it can go right round and look any way (and tilt up and down within the band) independently of the head. That matters most when the droid is at its most broken: a head lying on the floor with no body can't turn itself, but its camera can turn to get a better view.
- **Its features**: zoom, 'identify' and recording.
- **Mirrors** show the droid to itself (see 1): its body is only ever seen in them, or from the drone.
- Today: the first-person view is the walker's eye, 1.6 m up, looking straight along the body's heading: it turns only when the body turns, and it can't look up or down. A head camera needs its own direction (yaw and pitch) relative to the head, limited to what the visor allows, and its own controls.
- Today, for mirrors: nothing reflects, and the first-person view doesn't draw the player's own mesh. The nearest thing is the window portals (`WindowPortals`), which draw another scene masked to a quad by the stencil. A mirror is the same trick with the world itself as the scene, seen from the camera reflected in the mirror's plane (with the triangles' winding flipped, and whatever is behind the mirror cut away), and the droid's own mesh drawn in it. Each mirror in view is another pass over the scene, so a few small ones are cheap and a hall of them isn't.
- **The drone** is a separate camera machine, with its own control scheme in drone mode and a distance limit from the droid: at the limit the picture and the link degrade, and past it the link drops. In fact, a possibility is the drone drops and has to be collected again. It suits the premise (a second thing sending pictures) and can itself be something to repair or find.
- Today: the drone view is toggled with the first-person view; the drone follows the player on a spring (3 m behind, 2 m up), keeps in sight, and stays clear of the ground and walls; you can't steer it and there is no range.

### 3.3 Restoring the colour: the central mechanic
The world's colour is missing; putting it back is the game's progress, and the most visible part of it. This is where the earlier ideas of *colouring in a mesh one colour at a time* and *painting the sides of objects* belong: the player is the one doing it. The way it would work is a design question (see question 1): by colour (find red, and reds return), by object or place, or by world.
- **Today it nearly already exists.** A mesh's faces are grouped by colour slot, every instance has its own palette, and the game has a colour switch (`C`) that shows either the palette or the wireframe (faces take the background colour). Making a slot restored or not, per mesh, is a small step from that; the world's own state is what would be new.
- **Painting a side of an object** needs that side to be a slot of its own in the mesh; today a box's slots are its side, dim and top shades.
- **What comes back can also be light**, the sun and moon, the lamps' pools: see 3.7.
- **The colour lab** (2026-10-02; `Droid.Playground colour`) tries the ways it could go. Every colour in the world is sorted by its hue (red, orange, yellow, green, blue, purple: [World.Core/Colour](World.Core/Colour)) and drawn as much as its hue is back, by the palette shader as it draws ([IPaletteGrade](MeshRendering/IPaletteGrade.cs)), so nothing in the meshes changes and it costs next to nothing. Drops of each colour lie about; the droid picks them up by driving into them and pours them into the barrel of their colour. Its panel switches between colour coming back everywhere a little with every drop, or in this place (round the barrels) all at once when its barrel's full, or a tint and then all of it; at once, fading in, or in a wave spreading out from the barrels; drained to the background (the wireframe look) or to grey; what the whites and greys do; and six colours to find or only red, yellow and blue, the rest mixed of them.

### 3.4 Worlds, and travelling between them
- **Several worlds in one solar system**, each its own place with its own look and puzzles, reached by travelling.
- Today a world is a list of districts and a seed (`WorldBuilder.Build(districts, seed)`): a second world is a second list, with its own terrain, buildings and starts. Going elsewhere is done by portals to scenes off the map (the corridor and the hangar are two).
- **Leaving the planet.** Space is somewhere you fly to. A vehicle launches, climbs through the clouds, the sky darkens, and it's in space with the planet below in **bands of light around its edge**. The reference picture is a flat-shaded, dithered planet from orbit, at a shallow angle, the atmosphere a few soft-edged bands, a galaxy smudge in the stars; it fits the limited-colour look.
- **The goal is a satellite orbiting a different planet**, the one the droid was meant to be on: the orders are to get the droid off the planet it's on and to that satellite (see 3.7), so reaching it means crossing the solar system. It is not the space station below.
- **It can't stay.** The vehicle doesn't have the power to stay up, and is pulled back down, unless it docks at the **space station**, which flies overhead at times of the day and can be launched up to. The station is a way off the planet, not the goal: a stepping stone on the way to the satellite. It can also be what gates travel: to reach another world takes more power, or stepping stones, and so more repairs.
- Today: a space plane mesh exists (`SpacePlaneMesh`, a prop in Basic.Levels only, not in the world). Nothing flies: the walker's gravity and movement are for the ground, and the drone is a spring-follow camera. The sky is one solid colour (the background, also the fog's), the far plane is the fog's end (95 m) and terrain is built only within about 110 m of the camera, so nothing large or distant is drawn.
- What it would need: an altitude-dependent sky, clouds to fly through, the planet from orbit as a scene of its own (a curved rim of flat-shaded patches, the atmosphere as a small number of concentric flat bands with dither), a power budget with a gravity that pulls back, docking, and a movement model for flight.

### 3.5 Puzzles and interaction
- **Find X to do Y**: things to pick up, carry and use; places that open when the droid can do something; things to push, stack, knock over or reach.
Find a raw chicken and heat it. Find sticky tape to attach something together.
Find a wrench to undo a bolt.
Break wood to uncover hole.
- Today: you press E to open or shut a door (`BuildingGround.Interact`), and bodies (crates, lockers, boxes) can be pushed, stacked, toppled and floated, so physical puzzles are possible. There is nothing to pick up, carry or use, and no state that a puzzle can set and something else read (a door that stays locked until a switch is thrown, say).
- **Switches** are one such thing, and the natural way to do the room lights in 3.7.

### 3.6 Action and violence
- **Implements that can be weapons**, in a progression that follows the droid's recovery and the worlds' technology:
  1. **Simple tools**: a bat, a shovel and the like, swung in melee. Tools can _also be tools_. 
  2. **Pistols.**
  3. **A rifle.**
  4. **A sci-fi laser gun**: it really damages enemies, and **can bore holes in certain items**, which makes it a puzzle tool as well as a weapon (a way through a wall or a door, something to cut open).
- **Enemies**, and things that hurt the droid (it is already damaged, and can be damaged further).
  - **The first section's enemies are small tank-like vehicles that look like converted vacuum cleaners.** They fire projectiles at the droid, at a fairly slow rate.
  - **They are spawned at points through the story**, so where and when they appear follows the story (3.7) and the game state (section 4) rather than being fixed in the map.
  - **At first they are more of a nuisance than a threat**, but they can **travel in packs** and **co-ordinate their firing**, so a group is more dangerous than its members.
  - **Before it has a weapon, the droid deals with them by its wits**: **avoiding** them, **tricking them into shooting each other** (getting between them, then out of the way), and **dropping a heavy box on them**. Fighting starts as a kind of puzzle, and the pack's co-ordinated fire is also what makes it easy to turn on itself.
  - Enemies in later sections, and whether they differ between worlds, are still open (question 5).
- **Effects**: particles for water splashes, backfire and explosions, and for hits and the laser.
- How it would fit what exists:
  - **Arms and repairs.** Wielding anything needs the droid to have arms, so the first implement is itself a reward for a repair (3.1). How many it can carry at once depends on how big its body is (3.1).
  - **Melee** can use the physics already there: crates and lockers take forces and topple, so a swing can knock things over.
  - **Ranged weapons** need a line from the droid to what it hits. Today `ClearLine` finds where a line meets the terrain, walls and ceilings (it is how the drone keeps you in sight), but not bodies or enemies; that is the missing piece for pistols, the rifle and the laser.
  - **Boring holes** is changing the world's shape while the game runs: a wall or an object gets a hole, and things can go through it. Today a building's walls are solid walls to collide with (kept in a grid for speed, built once) and every mesh is built once. The pattern the game already uses for something that changes is two meshes swapped by state (the cottages' windows: a shut one and an open one, shown by where you stand). A boreable item could be the same: a whole one and a bored one, swapped when hit, with its collision changed to match. It would also want to be in the game state (3.5, section 4) so a hole stays a hole.
  - **The vacuum-cleaner tanks** need three things the game doesn't have:
    - **Driving with a purpose.** Each tank moves on the ground like a walker, but steers itself: towards the droid, keeping a distance, turning to aim. It can use `ClearLine` to tell whether it can see the droid past the terrain and walls, the same test the drone uses to keep you in sight.
    - **Projectiles.** A slow rate of fire means only a few shots in flight at once, each a small moving thing stepped every tick and tested against the world (the line from where it was to where it is now) and against the droid. Slow projectiles can be seen coming and dodged, which suits the Segway's slow handling.
    - **A pack.** Co-ordinated fire wants something above the individual tanks: a pack that picks where each one goes (spread around the droid, say) and when they fire together. Its members are still ordinary tanks; the pack only gives orders.
    - **Being beaten without a weapon.** Friendly fire means a tank's projectiles are tested against the other tanks too, not only against the droid. A dropped box means the physics bodies (crates and the like, which can already be pushed, stacked and toppled) hurting what they land on: a hit from a body falling fast enough, or heavy enough, crushes or wrecks a tank. Both need tanks to have damage, like the droid.
  - **The droid's own weapons and ammunition**: pistols, the rifle and the laser could run on the droid's power, on ammunition to be found, or both (question 5).
- Today: nothing to fight with or against. The world's other bodies are crates and the like, moved by pushing; only the player and the drone are walkers, so enemies need a general list of walkers (the review plan's 5.5). There is no dynamic vertex data for particles, nothing to pick up or hold, and no damage.

### 3.7 The narrative
- **The setup** (see 1): you are the operator, fresh from training, given command of a droid that went missing and has begun broadcasting again. Your orders: gain control of it, and get it off the planet to a satellite orbiting a different planet, the one it was meant to be on. It is on the wrong planet.
- **The questions that drive it**: how did the droid get to the wrong planet, why did it go missing, why is it broadcasting again now, and how do you get it back?
- **Two unreliable sources**, and the player has to work out which to trust:
  - **Your orders**, from whoever gave you command: what you're told to do, and why.
  - **The droid**: what it remembers, what it tells you on its console, and what its camera shows, since its memories can influence the picture.
- **Ways the truth could come out** (ideas, not decisions):
  - **The cameras as evidence.** 'Identify' and recording let the player check a claim: record something, play it back later, compare it with what the orders or the droid said. The drone sees without the droid's memories, so it can show where the head camera is wrong; mirrors show what the droid really is.
  - **Repairs change the story.** Repairing the droid's memory could make it more reliable, or show that its memories were right and the orders weren't.
  - **Orders that are plainly wrong** once the player has seen enough, so obeying them becomes a choice.
- **More than one ending.** Doing as ordered and reaching the satellite ends the game, but it is probably not the best ending. Since the orders aren't honest, the better endings presumably come from finding out the truth and acting on it.
- **The tutorial is training**, so it belongs in the story: the operator learning the controls before being given command. It could be a simulator, or a practice droid somewhere safe.
- Told as the droid recovers: in what it says and remembers, in what the worlds hold, and in what each repair or restored colour unlocks. It should be tied to the game state (3.5) so that story beats are gated by what's been done.
- Needs text on screen (the console, in 1) and a way of scripting what is said and when.

### 3.8 Content of the worlds
- **Paths**: roads, railways, tunnels (general enough for roads, railways and walkers), road crossings (a crossover with road meshes).
- **Vehicles and people**: cars and traffic lights, trains going to places forward and backward on timetables, vehicles and people moving on circuits over the day.
- **Time**: a day and night cycle, with a sun that becomes a moon.
- **Lighting**: outside, the time of day; inside, within the 1980s 16-bit look, **coloured pools from lamps** (a lamp lights a patch of floor and nearby walls in its colour, as a few flat bands, not a smooth glow) and **a room's main light**, on a switch, for a whole room or a section of it, that changes the room between its light and dark views, the lamps' pools being what you see by in the dark. With the world colourless (see 1), lights might be what colour first comes back as (question 3).
- **Trees**: more types, more experimentation.
- Today:
  - Roads are pieces laid end to end (`RoadNetwork`: straight, T-junction, bend; the meshes also have a roundabout and an island), each levelling its own ground. There are car meshes (`CarMesh`, `TrabantMesh`) but nothing drives; there are no tracks, tunnels or lights. Bodies and the walker only know the terrain, buildings and free walls, so a tunnel means ground that isn't a heightfield (a room-like shell you drive into is how the game already does interiors).
  - There is a clock (seconds since the start, for what moves by itself, such as the hangar's turntables), and a moving thing is a function from time to a position (`ScenePart`): fine for things on rails and fixed loops. Nothing keeps a time of day, and nothing simulates something with a destination.
  - No lighting. Three shades per box (side, dim, top) are baked into the palette, terrain is shaded in three steps against one fixed light, and fog and background are one constant colour (`RetroStyle.Background`, which faces also take in wireframe).
  - A room is one shell mesh with its own palette plus its props, each an instance with a palette, so a room's light or dark view is a palette change across those instances, which is cheap. A lamp's pool would be flat-coloured shapes laid on the floor, the way the billboard's art is laid on its board. What doesn't exist is a room with a state its meshes follow, or a "section" of a room: rooms are whole outlines.
  - Trees: an oak (one large rounded canopy on a forked trunk, per your notes), a tree, a spiky bush and a fern. Canopies use view-dependent outlines that are recomputed every frame: cheap for a few, and the first cost to deal with before a forest (the review plan, 5.3).

## 4. What several of these share

Worth doing once, because more than one idea needs it:

- **A game state.** What's repaired, what colour is restored (and where), what's been found and used, which switches are thrown, where the story is; kept between sessions (there is no saving today), and for every world at once, since any of them can be visited at any time. Everything in 3.1, 3.3, 3.5 and 3.7 reads or writes it. This is the largest thing the game doesn't have and the design turns on.
- **Interaction.** More than doors: pick up, use, switch, talk. Doors already work by facing one and pressing E.
- **A world clock and a time of day**, read by the day and night cycle, timetables, the station's passes and lamps.
- **A sky that's a value, not a constant**: time of day and altitude both change the sky, and the fog, and what faces take in wireframe. Done once as "the sky colour at this time and height", it serves the sun and moon, the climb into space and the planet's banding alike.
- **A route**: roads, railways, tunnels and walked paths are all "a line through the world that things follow", the same idea as `RoadNetwork` pieces, with a length and a place at a distance along it. Trains, cars and people on circuits are things that move along routes on the clock.
- **Scenes and transitions**: rooms off the map with portals already work; interiors, tunnels, other worlds and space are the same trick.
- **Energy**: the droid (and its vehicles) having limited power, which flight, docking, the link and the HUD's readouts lean on.
- **Dynamic vertex data**, for particles: today every mesh is built once and never changes.
- **Palettes as the lever for the look**: time of day, damage, lighting, restored colour and painting can mostly be done by changing an instance's palette, which the design already supports and keeps cheap.

## 5. A rough order, by what depends on what (a suggestion only)

1. **The game state and interaction**, with the smallest loop of them: something found, something repaired, a slot of colour restored, kept between sessions. The rest hangs on this.
2. **The droid's components**, as values that the repairs change; then the drone's own controls and range.
3. **Colour restoration** properly: the mechanism, and its look in the picture.
4. **The console and the narrative's first beats** (text on screen).
5. **A second world**, to prove that worlds are lists of districts, then the way between them.
6. **Enemies and fighting**, with the walker registry, then particles and effects.
7. **The clock, sun and moon, and lights** (with the sky as a value), then **paths and vehicles**, then **space** (altitude sky and clouds first, then flight and power, then the planet from orbit and the station).

Trees, tunnels and crossings can be done any time; trees are cheap to try one at a time.

## 6. Questions

1. **How is colour restored?** By colour (find red, and every red in the world comes back), by object or place (repair or "paint" each), or by world (finish a world, it's coloured)? Your earlier "one colour at a time" suggests the first: is that right? *Partly answered (2026-10-02): colour is collected (a green, some drops of blue) and deposited in the barrel of its colour. Still open, to be settled by trying them in the colour lab (see 3.3): whether each deposit restores a little of that colour everywhere, or a place (the coast, the town) gets all of a colour back once enough's been collected, before moving on to the next.*
2. **Does the broadcast improve?** Beyond colour, do repairs also raise the picture's resolution, how far it can see, or the detail that comes through, or is colour the only thing that changes?
3. **Lights while colourless:** the world starts as wireframe, where a room can't get darker by changing colours. Do lamps' pools and room lights show at all before colour returns? If so, how: edges dimmed or dashed in an unlit room, faces tinted by a pool, or is light one of the things that gets restored?
4. **The flying vehicle and travel:** is the vehicle the droid itself (flight as a way of moving, fitted like legs or wheels), or a separate craft it controls? When its power runs out does it fall (and crash, or land), glide, or get hauled back? Is power what limits how far apart the worlds can be?
5. **Enemies and ammunition:** the weapons run from a bat or shovel up to a laser that bores holes. The first section's enemies are known (small vacuum-cleaner tanks, see 3.6). Before it has a weapon, the droid avoids them, tricks them into shooting each other, or drops a heavy box on them (answered). What does it fight in later sections, do the enemies differ between worlds, and roughly how much of the game is action against puzzle? Do the guns use ammunition to be found, the droid's power, or both? (How many implements it can carry depends on how big a body it has: answered, see 3.1.)
6. **What stops the player getting somewhere too early?** With free roaming, is it the droid's abilities and power alone (it simply can't reach or survive it yet), or also the world itself (danger, distance, a locked way)? And does the droid have to be able to travel between worlds before it can leave the first, or is there more than one world within reach from the start?
7. **The satellite** (answered: it orbits the planet the droid was meant to be on, and is not the space station; reaching it is one way to end the game, but probably not the best one).
8. **Who gives the orders, and why aren't they honest?** Does the player find out, and how? Can the player disobey them? Reaching the satellite is one ending and not the best: what are the others, roughly how many are there, and what earns the best one?
9. **Is the missing colour part of the story?** Is it damage in the droid's camera, something that happened to the wrong planet, or something the droid's memories are hiding? And is the droid's being on the wrong planet tied to it?
